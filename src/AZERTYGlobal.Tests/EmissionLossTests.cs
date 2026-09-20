using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// AG130-09 — perte silencieuse à l'émission.
///
/// Ce que l'audit a trouvé : <c>SendInput</c> était déclaré sans
/// <c>SetLastError</c> et la compensation de <c>SendInputsWithRecovery</c> ne se
/// déclenche que sur un lot <b>partiel</b> (<c>sent &gt; 0 &amp;&amp; sent &lt;
/// Length</c>). Un retour <c>0</c> — UIPI, bureau sécurisé, session verrouillée —
/// tombait donc dans un angle mort complet : le caractère est perdu alors que la
/// touche physique a déjà été bloquée, et rien n'en garde la trace.
///
/// Ce que la 1.3.0 corrige : le compteur et le journal, pas le blocage. Laisser
/// passer la touche produirait le caractère <b>natif</b>, c'est-à-dire un mauvais
/// caractère plutôt qu'aucun, et toucherait les sept sites d'émission — reporté en
/// 1.3.1 (décision d'Antoine, 2026-09-20).
///
/// Chaque témoin positif est suivi de son contrôle négatif : sans lui, un compteur
/// qui s'incrémenterait à chaque émission passerait tous les tests ci-dessous.
/// </summary>
public class EmissionLossTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Layout _layout = LayoutLoader.LoadFromResource();

    public EmissionLossTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGLOSS_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_tempDir, "config.json"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private (KeyMapper Mapper, MockWin32Api Api, TestWindowsTypingHost Host) Build()
    {
        var api = new MockWin32Api();
        var host = new TestWindowsTypingHost();
        return (new KeyMapper(_layout, api, host), api, host);
    }

    // ═══════════════════════════════════════════════════════════════
    // Le refus en bloc est compté et journalisé
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void UnRetourZeroSurUnLotNonVide_EstComptéCommePerte()
    {
        var (mapper, api, host) = Build();
        api.SendInputResult = 0;

        var result = mapper.TryEmitText("a");

        Assert.Equal(1, mapper.EmissionLossCount);
        Assert.False(result.IsComplete);
        Assert.Equal(0u, result.SentEvents);
        Assert.True(result.RequestedEvents > 0);
        Assert.Single(host.CriticalEvents);
        Assert.Equal("EmissionRefusee", host.CriticalEvents[0].EventName);
    }

    /// <summary>Contrôle négatif du témoin ci-dessus : une émission acceptée ne compte rien.</summary>
    [Fact]
    public void UneÉmissionAcceptée_NeCompteAucunePerte()
    {
        var (mapper, api, host) = Build();
        api.SendInputResult = null; // le mock rend inputs.Length, donc un lot complet

        var result = mapper.TryEmitText("a");

        Assert.Equal(0, mapper.EmissionLossCount);
        Assert.True(result.IsComplete);
        Assert.Empty(host.CriticalEvents);
    }

    [Fact]
    public void LeDétailJournalisé_PorteLeCodeDErreurWin32()
    {
        var (mapper, api, host) = Build();
        api.SendInputResult = 0;
        api.LastSendInputError = 5; // ERROR_ACCESS_DENIED, le retour typique sous UIPI

        mapper.TryEmitText("a");

        Assert.Contains("erreur Win32 5", host.CriticalEvents[0].Details);
    }

    /// <summary>
    /// Contrôle négatif : sans code d'erreur relevé, le journal dit 0 plutôt que
    /// d'inventer une cause. Un mock qui rendrait toujours 5 ferait passer le témoin
    /// précédent sans rien prouver.
    /// </summary>
    [Fact]
    public void SansCodeDErreurRelevé_LeJournalDitZéro()
    {
        var (mapper, api, host) = Build();
        api.SendInputResult = 0;

        mapper.TryEmitText("a");

        Assert.Contains("erreur Win32 0", host.CriticalEvents[0].Details);
    }

    // ═══════════════════════════════════════════════════════════════
    // Une émission suspendue n'est pas une perte
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// <c>SendInputs</c> rend déjà 0 quand l'émission est suspendue. Compter ce 0
    /// comme une perte ferait exploser le compteur sur chaque pause volontaire et
    /// chaque application en mode anti-triche — c'est le faux positif à éviter.
    /// </summary>
    [Fact]
    public void UnePauseVolontaire_NEstPasUnePerte()
    {
        var (mapper, api, host) = Build();
        api.SendInputResult = 0;
        mapper.EmissionPaused = true;

        var result = mapper.TryEmitText("a");

        Assert.True(result.Blocked);
        Assert.Equal(0, mapper.EmissionLossCount);
        Assert.Empty(host.CriticalEvents);
    }

    /// <summary>Contrôle négatif : la même frappe, pause levée, compte bien la perte.</summary>
    [Fact]
    public void PauseLevée_LaMêmeFrappeCompteLaPerte()
    {
        var (mapper, api, _) = Build();
        api.SendInputResult = 0;
        mapper.EmissionPaused = true;
        mapper.TryEmitText("a");
        mapper.EmissionPaused = false;

        mapper.TryEmitText("a");

        Assert.Equal(1, mapper.EmissionLossCount);
    }

    [Fact]
    public void UnTexteVide_NeCompteRien()
    {
        var (mapper, api, host) = Build();
        api.SendInputResult = 0;

        mapper.TryEmitText("");

        Assert.Equal(0, mapper.EmissionLossCount);
        Assert.Empty(host.CriticalEvents);
    }

    // ═══════════════════════════════════════════════════════════════
    // Le journal est bridé, le compteur ne l'est pas
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Le canal critique écrit sur disque à chaque appel (<c>ConfigManager.LogCompatCriticalEvent</c>).
    /// Une session verrouillée refuserait une frappe après l'autre : on trace le premier
    /// refus d'une série puis un sur cinquante. Le compteur, lui, n'en perd aucun.
    /// </summary>
    [Fact]
    public void CinquanteRefusDAffilée_NeJournalisentQueLePremierEtLeCinquantième()
    {
        var (mapper, api, host) = Build();
        api.SendInputResult = 0;

        for (int i = 0; i < 50; i++) mapper.TryEmitText("a");

        Assert.Equal(50, mapper.EmissionLossCount);
        Assert.Equal(2, host.CriticalEvents.Count);
        Assert.Contains("serie 1,", host.CriticalEvents[0].Details);
        Assert.Contains("serie 50,", host.CriticalEvents[1].Details);
        Assert.Contains("total 50", host.CriticalEvents[1].Details);
    }

    /// <summary>
    /// Contrôle négatif du bridage : une émission qui repasse remet la série à zéro,
    /// donc le refus suivant est journalisé de nouveau. Sans cette remise à zéro, un
    /// utilisateur qui reverrouille sa session dix minutes plus tard n'aurait aucune
    /// trace avant quarante-neuf caractères perdus.
    /// </summary>
    [Fact]
    public void UneÉmissionQuiRepasse_RemetLaSérieÀZéro()
    {
        var (mapper, api, host) = Build();

        api.SendInputResult = 0;
        mapper.TryEmitText("a");
        api.SendInputResult = null;
        mapper.TryEmitText("a");
        api.SendInputResult = 0;
        mapper.TryEmitText("a");

        Assert.Equal(2, mapper.EmissionLossCount);
        Assert.Equal(2, host.CriticalEvents.Count);
        Assert.Contains("serie 1,", host.CriticalEvents[1].Details);
        Assert.Contains("total 2", host.CriticalEvents[1].Details);
    }

    // ═══════════════════════════════════════════════════════════════
    // Ce que le correctif ne change pas
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// La frontière de la décision d'Antoine : un lot refusé ne rejoue rien et le texte
    /// n'est pas compté comme saisi. Le blocage de la touche physique reste inchangé en
    /// 1.3.0 ; si ce témoin passe au rouge, c'est que quelqu'un a implémenté la 1.3.1.
    /// </summary>
    [Fact]
    public void UnLotRefusé_NEstPasComptéCommeTexteSaisi()
    {
        var (mapper, api, host) = Build();
        api.SendInputResult = 0;

        mapper.TryEmitText("bonjour");

        Assert.Empty(host.EmittedTexts);
        Assert.Single(api.SendInputCalls); // un seul lot, aucun rejeu
    }

    /// <summary>Contrôle négatif : le même texte accepté est bien compté comme saisi.</summary>
    [Fact]
    public void UnLotAccepté_EstComptéCommeTexteSaisi()
    {
        var (mapper, api, host) = Build();
        api.SendInputResult = null;

        mapper.TryEmitText("bonjour");

        Assert.Single(host.EmittedTexts);
        Assert.Equal("bonjour", host.EmittedTexts[0]);
    }
}
