using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Granularité des lots SendInput et ordre d'émission sur le chemin des touches mortes.
/// Ajouté par l'audit de release v1.2.0 (constats R6 et R8), qui a trouvé ce chemin
/// non couvert.
///
/// Pourquoi les tests existants ne pouvaient pas le voir : ils passent tous par
/// <c>MockWin32Api.AllInputs</c>, qui aplatit <c>SendInputCalls</c> avec un
/// <c>SelectMany</c>. Un lot de deux caractères et deux lots d'un caractère y rendent
/// exactement la même chaîne. Le mock, lui, enregistre bien les lots séparément — c'est
/// l'assertion qui était aveugle, pas l'instrumentation. Ces tests assertent donc sur
/// <c>SendInputCalls</c> directement.
///
/// Ce sont des tests de caractérisation : ils figent le comportement d'aujourd'hui pour
/// qu'un changement futur soit bruyant. Ils n'affirment pas que ce comportement est le bon.
/// </summary>
public class DeadKeyEmissionBatchTests : IDisposable
{
    // Scancodes AZERTY (set 1), identiques à ceux de DeadKeyAndSmartCapsTests
    private const uint SC_D03 = 0x12; // e / E / €
    private const uint SC_C11 = 0x28; // dk_acute en accès direct
    private const uint SC_B08 = 0x33; // . en accès direct

    private readonly string _tempDir;
    private readonly Layout _layout = LayoutLoader.LoadFromResource();

    public DeadKeyEmissionBatchTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGBATCH_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_tempDir, "config.json"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    /// <summary>Texte reconstruit depuis un seul lot SendInput.</summary>
    private static string TextOfBatch(TypingEngine.Windows.Win32.INPUT[] batch)
    {
        var chars = new List<char>();
        foreach (var input in batch)
        {
            var ki = input.u.ki;
            if (ki.wVk == 0 && (ki.dwFlags & 0x0004) != 0 && (ki.dwFlags & 0x0002) == 0)
                chars.Add((char)ki.wScan);
        }
        return new string(chars.ToArray());
    }

    // ═══════════════════════════════════════════════════════════════
    // R6 — un lot au lieu de deux sur le cas « isolé + caractère »
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Le point n'est pas dans la table de dk_acute (70 entrées, vérifié sur le layout
    /// embarqué) : la composition tombe donc sur l'isolé suivi du caractère.
    ///
    /// Aujourd'hui, CompositionEngine concatène les deux et KeyMapper émet UN lot.
    /// Avant l'extraction du moteur, le code faisait deux appels successifs —
    /// <c>EmitText(isolatedChar)</c> puis <c>EmitText(output)</c> — donc DEUX lots.
    /// C'est le seul changement du chemin de frappe qu'aucune assertion ne voyait.
    /// </summary>
    [Fact]
    public void ProcessKey_DeadKeyThenUnmappedChar_EmetUnSeulLot()
    {
        var mock = new MockWin32Api();
        var mapper = new KeyMapper(_layout, mock);

        mapper.ProcessKey(0, SC_C11, 0, true);
        Assert.Equal("dk_acute", mapper.ActiveDeadKey);
        Assert.Empty(mock.SendInputCalls); // l'activation n'émet rien

        mapper.ProcessKey(0, SC_B08, 0, true);

        Assert.Single(mock.SendInputCalls);
        Assert.Equal("´.", TextOfBatch(mock.SendInputCalls[0]));
        Assert.Null(mapper.ActiveDeadKey);
    }

    /// <summary>
    /// Contre-témoin : sur un caractère qui EST dans la table, un seul lot est attendu de
    /// toute façon. Sans lui, un moteur qui n'émettrait jamais qu'un lot passerait le test
    /// ci-dessus sans rien prouver.
    /// </summary>
    [Fact]
    public void ProcessKey_DeadKeyThenMappedChar_EmetAussiUnSeulLot()
    {
        var mock = new MockWin32Api();
        var mapper = new KeyMapper(_layout, mock);

        mapper.ProcessKey(0, SC_C11, 0, true);
        mapper.ProcessKey(0, SC_D03, 0, true);

        Assert.Single(mock.SendInputCalls);
        Assert.Equal("é", TextOfBatch(mock.SendInputCalls[0])); // é
    }

    // ═══════════════════════════════════════════════════════════════
    // R8 — StateChanged après l'émission, alors qu'il la précédait
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Compte les lots déjà émis au moment où StateChanged se déclenche.
    ///
    /// Aujourd'hui KeyMapper émet puis notifie, donc la résolution voit 1 lot déjà parti.
    /// Avant l'extraction, <c>StateChanged?.Invoke()</c> était appelé AVANT toute émission :
    /// la résolution aurait vu 0. Tout abonné qui lit l'état du clavier dans ce callback
    /// observe donc un instant différent selon la version.
    /// </summary>
    [Fact]
    public void ProcessKey_DeadKeyResolution_NotifieApresAvoirEmis()
    {
        var mock = new MockWin32Api();
        var mapper = new KeyMapper(_layout, mock);

        var lotsAuMomentDeLEvenement = new List<int>();
        mapper.StateChanged += () => lotsAuMomentDeLEvenement.Add(mock.SendInputCalls.Count);

        mapper.ProcessKey(0, SC_C11, 0, true); // activation : notifie, n'émet rien
        mapper.ProcessKey(0, SC_D03, 0, true); // résolution : émet é, puis notifie

        Assert.Equal(2, lotsAuMomentDeLEvenement.Count);
        Assert.Equal(0, lotsAuMomentDeLEvenement[0]); // activation, rien d'émis
        Assert.Equal(1, lotsAuMomentDeLEvenement[1]); // résolution, émission déjà faite
    }

    // ═══════════════════════════════════════════════════════════════
    // R7 — le pass-through n'est plus atteint sur ce chemin
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Toute la branche de composition retourne true sans consulter CanPassThrough. Ce test
    /// fixe la conséquence observable côté Windows : la touche est toujours consommée quand
    /// une touche morte est active, jamais laissée passer au layout natif.
    ///
    /// Le mécanisme de la touche morte orpheline est couvert au niveau pur par
    /// CompositionEngineOrphanDeadKeyTests, qui peut construire un layout malformé — ce que
    /// le layout embarqué, valide par construction, ne permet pas ici.
    /// </summary>
    [Fact]
    public void ProcessKey_TouchemorteActive_ConsommeToujoursLaTouche()
    {
        var mock = new MockWin32Api();
        var mapper = new KeyMapper(_layout, mock);

        mapper.ProcessKey(0, SC_C11, 0, true);

        // SC_D03 produit « e », que le layout Windows natif produit aussi : hors composition
        // ce serait un candidat au pass-through.
        bool consomme = mapper.ProcessKey(0, SC_D03, 0, true);

        Assert.True(consomme);
    }
}
