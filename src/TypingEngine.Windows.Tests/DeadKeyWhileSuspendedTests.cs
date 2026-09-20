using System.Reflection;
using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Témoins du mécanisme (b) de l'écart 5 (AG130-08, audit du 2026-09-20).
///
/// Deux sous-cas, mesurés séparément par l'audit :
///
/// b1 — l'émission est déjà suspendue quand la lettre arrive. La garde d'entrée de
/// <c>ProcessKey</c> laisse passer la touche, la lettre sort en natif, et la touche morte
/// restait en attente indéfiniment : « ^a » rendait « a », puis une lettre tapée bien plus
/// tard sortait accentuée sans raison visible.
///
/// b2 — l'émission devient indisponible entre l'entrée de <c>ProcessKey</c> et la branche
/// de composition, parce que l'instantané de premier plan est relu. <c>Process</c>
/// consommait la touche morte, puis <c>EmitText</c> ne faisait rien : le caractère
/// disparaissait entièrement.
///
/// Arbitrage retenu (décision d'Antoine du 2026-09-20) : la touche morte est abandonnée
/// quand la suspension vient du premier plan — l'utilisateur a changé d'application — et
/// conservée pendant une pause volontaire, où il reste dans la même fenêtre.
/// </summary>
public class DeadKeyWhileSuspendedTests
{
    private const uint SC_CIRCONFLEXE = 0x1A;
    private const uint SC_A = 0x1E;
    private const uint VK_CIRCONFLEXE = 0xDD;
    private const uint VK_A = 0x41;

    private static Layout LayoutAvecCirconflexe()
    {
        var layout = new Layout();
        layout.Keys[SC_CIRCONFLEXE] = new KeyDefinition
        {
            Scancode = (ushort)SC_CIRCONFLEXE,
            Position = "D11",
            Base = "dk_circumflex"
        };
        layout.Keys[SC_A] = new KeyDefinition
        {
            Scancode = (ushort)SC_A,
            Position = "C01",
            Base = "a"
        };

        var dk = new DeadKeyDefinition { Name = "dk_circumflex", Description = "circonflexe" };
        dk.Table["a"] = "â";
        dk.Table[" "] = "^";
        layout.DeadKeys["dk_circumflex"] = dk;
        return layout;
    }

    /// <summary>Lit l'état interne de composition : c'est lui que le correctif arbitre.</summary>
    private static string? ToucheMorteEnAttente(KeyMapper km)
    {
        var champ = typeof(KeyMapper).GetField("_composition",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(champ);
        var composition = (CompositionEngine)champ!.GetValue(km)!;
        return composition.ActiveDeadKey;
    }

    private static KeyMapper MapperAvecCirconflexePendant(MockWin32Api api)
    {
        var km = new KeyMapper(LayoutAvecCirconflexe(), api);
        km.ProcessKey(VK_CIRCONFLEXE, SC_CIRCONFLEXE, 0, isKeyDown: true);
        Assert.Equal("dk_circumflex", ToucheMorteEnAttente(km));
        return km;
    }

    /// <summary>
    /// Garde-fou du banc lui-même : sans lui, un test qui ne verrait jamais de touche morte
    /// en attente passerait toutes les assertions d'abandon pour la mauvaise raison.
    /// </summary>
    [Fact]
    public void SansSuspension_LaToucheMorteComposeNormalement()
    {
        var km = MapperAvecCirconflexePendant(new MockWin32Api());

        km.ProcessKey(VK_A, SC_A, 0, isKeyDown: true);

        Assert.Null(ToucheMorteEnAttente(km));
    }

    /// <summary>
    /// b1, moitié « pause » : la pause est volontaire et courte, l'utilisateur reste dans
    /// la même fenêtre. La touche morte l'attend.
    /// </summary>
    [Fact]
    public void PauseVolontaire_ConserveLaToucheMorte()
    {
        var km = MapperAvecCirconflexePendant(new MockWin32Api());
        km.EmissionPaused = true;

        bool bloquee = km.ProcessKey(VK_A, SC_A, 0, isKeyDown: true);

        Assert.False(bloquee); // la lettre part en natif, l'inertie est respectée
        Assert.Equal("dk_circumflex", ToucheMorteEnAttente(km));
    }

    /// <summary>
    /// b1, moitié « premier plan » : la suspension vient de l'application au premier plan,
    /// donc l'utilisateur a changé d'application. Garder la touche morte ferait surgir un
    /// accent sur une lettre tapée bien plus tard — le « même geste, deux résultats » de la
    /// recette du 2026-09-19.
    /// </summary>
    [Fact]
    public void SuspensionParLePremierPlan_AbandonneLaToucheMorte()
    {
        var api = new MockWin32Api { ScriptedProcessName = "jeu.exe", ShouldFailSetWinEventHook = true };
        var km = MapperAvecCirconflexePendant(api);
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);
        Assert.Equal(CompatibilityMode.DisabledAntiCheat, monitor.CurrentMode);
        km.SetForegroundMonitor(monitor);

        bool bloquee = km.ProcessKey(VK_A, SC_A, 0, isKeyDown: true);

        Assert.False(bloquee); // inertie totale : jamais bloquer sous suspension de sécurité
        Assert.Null(ToucheMorteEnAttente(km));
    }

    /// <summary>
    /// La touche morte abandonnée ne doit pas ressurgir : la lettre suivante, une fois la
    /// suspension levée, sort nue.
    /// </summary>
    [Fact]
    public void ApresAbandon_LaLettreSuivanteNEstPasAccentuee()
    {
        var api = new MockWin32Api { ScriptedProcessName = "jeu.exe", ShouldFailSetWinEventHook = true };
        var km = MapperAvecCirconflexePendant(api);
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);
        km.SetForegroundMonitor(monitor);
        km.ProcessKey(VK_A, SC_A, 0, isKeyDown: true);
        km.ProcessKey(VK_A, SC_A, 0, isKeyDown: false);

        km.SetForegroundMonitor(null); // la suspension se lève

        Assert.Null(ToucheMorteEnAttente(km));
    }

    /// <summary>
    /// Symétrique du précédent : après une pause, la touche morte conservée compose bien
    /// la lettre suivante. C'est ce qui justifie de la garder.
    /// </summary>
    [Fact]
    public void ApresUnePause_LaToucheMorteConserveeCompose()
    {
        var km = MapperAvecCirconflexePendant(new MockWin32Api());
        km.EmissionPaused = true;
        km.ProcessKey(VK_A, SC_A, 0, isKeyDown: true);
        km.ProcessKey(VK_A, SC_A, 0, isKeyDown: false);

        km.EmissionPaused = false;
        km.ProcessKey(VK_A, SC_A, 0, isKeyDown: true);

        Assert.Null(ToucheMorteEnAttente(km)); // consommée par la composition, cette fois
    }
}
