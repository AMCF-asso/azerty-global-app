using System.Reflection;
using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Témoin R13 de la revue de code du 2026-09-21.
///
/// L'arbitrage écrit dans <c>KeyMapper.ArbitratePendingDeadKeyWhileSuspended</c> ne
/// s'appliquait qu'aux suspensions imposées par le premier plan — anti-triche, accès
/// distant, premier plan inconnu. Un Alt+Tab vers une application ordinaire ne suspend
/// rien : la touche morte en attente survivait au changement d'application, et l'accent
/// surgissait sur une lettre tapée bien plus tard, ailleurs. Le geste 17 de la recette
/// était donc faux par construction.
///
/// ⛔ Ce qui décide n'est pas le premier plan courant mais la dernière *application* :
/// la barre des tâches, le sélecteur Alt+Tab et nos propres fenêtres changent le premier
/// plan sans changer d'application. Les deux dernières classes de témoins tiennent ce
/// point sur <c>ForegroundMonitor</c> ; sans elles, le correctif détruirait la
/// composition au clic le plus banal — la faute même que la 1.3.0 a réparée.
/// </summary>
public class ApplicationChangeDeadKeyTests
{
    private const uint SC_CIRCONFLEXE = 0x1A;
    private const uint SC_A = 0x1E;
    private const uint VK_CIRCONFLEXE = 0xDD;
    private const uint VK_A = 0x41;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    private static readonly ForegroundProcessIdentity Word = new(1234, 111);
    private static readonly ForegroundProcessIdentity Navigateur = new(5678, 222);

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

    private static string? ToucheMorteEnAttente(KeyMapper km)
    {
        var champ = typeof(KeyMapper).GetField("_composition",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(champ);
        return ((CompositionEngine)champ!.GetValue(km)!).ActiveDeadKey;
    }

    /// <summary>Accent circonflexe armé dans Word — l'état d'avant le changement.</summary>
    private static KeyMapper MapperAvecToucheMorteDans(ForegroundProcessIdentity application,
        out MockWin32Api api)
    {
        api = new MockWin32Api();
        var km = new KeyMapper(LayoutAvecCirconflexe(), api);
        km.ApplyForegroundApplication(application);
        km.ProcessKey(VK_CIRCONFLEXE, SC_CIRCONFLEXE, 0, isKeyDown: true);
        Assert.Equal("dk_circumflex", ToucheMorteEnAttente(km));
        return km;
    }

    // ── La décision, pure ───────────────────────────────────────────────

    [Fact]
    public void Changer_d_application_abandonne_la_touche_morte()
    {
        Assert.True(KeyMapper.ShouldAbandonDeadKeyOnApplicationChange(Word, Navigateur));
    }

    [Fact]
    public void Rester_dans_la_meme_application_la_conserve()
    {
        Assert.False(KeyMapper.ShouldAbandonDeadKeyOnApplicationChange(Word, Word));
    }

    /// <summary>
    /// Même PID, processus relancé : deux instances distinctes, donc un changement.
    /// Sans l'heure de création dans l'identité, Windows recyclant un PID rendrait ce
    /// cas indiscernable du précédent.
    /// </summary>
    [Fact]
    public void Un_pid_recycle_est_un_changement()
    {
        Assert.True(KeyMapper.ShouldAbandonDeadKeyOnApplicationChange(
            new ForegroundProcessIdentity(1234, 111), new ForegroundProcessIdentity(1234, 999)));
    }

    /// <summary>
    /// Aux deux bouts, une identité nulle ne décide rien : ni l'arrivée de la première
    /// application observée, ni la perte du suivi ne doivent détruire une composition.
    /// </summary>
    [Theory]
    [InlineData(true)]  // rien → Word : arrivée
    [InlineData(false)] // Word → rien : perte du suivi
    public void Une_identite_nulle_n_est_pas_un_changement(bool arrivee)
    {
        var (avant, apres) = arrivee ? (default(ForegroundProcessIdentity), Word)
                                     : (Word, default(ForegroundProcessIdentity));
        Assert.False(KeyMapper.ShouldAbandonDeadKeyOnApplicationChange(avant, apres));
    }

    // ── La couture, sur le moteur ───────────────────────────────────────

    [Fact]
    public void Un_alt_tab_vers_une_autre_application_abandonne_la_touche_morte()
    {
        var km = MapperAvecToucheMorteDans(Word, out _);

        km.ApplyForegroundApplication(Navigateur);

        Assert.Null(ToucheMorteEnAttente(km));
    }

    /// <summary>
    /// ⛔ Le témoin qui compte : après l'abandon, la lettre suivante doit sortir nue.
    /// Un champ remis à null ne prouve rien si la composition continue d'accentuer.
    ///
    /// « Nue » veut dire *pas d'émission du tout* : « a » est au même endroit sur l'AZERTY
    /// natif, le moteur rend donc la main et laisse passer la touche physique. Mesuré ici
    /// le 2026-09-22, en écrivant d'abord l'assertion inverse — c'est la forme même du
    /// « 99 % des frappes préservées », et elle se lit sur la valeur de retour, pas sur
    /// un lot d'émission vide, qu'une panne muette produirait aussi.
    /// </summary>
    [Fact]
    public void Apres_l_abandon_la_lettre_suivante_sort_nue()
    {
        var km = MapperAvecToucheMorteDans(Word, out var api);
        km.ApplyForegroundApplication(Navigateur);
        api.SendInputCalls.Clear();

        bool interceptee = km.ProcessKey(VK_A, SC_A, 0, isKeyDown: true);

        Assert.False(interceptee);
        Assert.Empty(api.SendInputCalls);
    }

    /// <summary>
    /// Réciproque obligatoire : sans elle, un <c>ApplyForegroundApplication</c> devenu
    /// destructeur en toutes circonstances rendrait les deux témoins ci-dessus verts pour
    /// la mauvaise raison.
    /// </summary>
    [Fact]
    public void Un_evenement_de_premier_plan_sans_changement_d_application_conserve_la_touche_morte()
    {
        var km = MapperAvecToucheMorteDans(Word, out var api);

        km.ApplyForegroundApplication(Word);
        api.SendInputCalls.Clear();
        bool interceptee = km.ProcessKey(VK_A, SC_A, 0, isKeyDown: true);

        Assert.True(interceptee);
        var inputs = Assert.Single(api.SendInputCalls);
        Assert.True((inputs[0].u.ki.dwFlags & KEYEVENTF_UNICODE) != 0);
        Assert.Equal('â', (char)inputs[0].u.ki.wScan);
    }

    // ── Ce qui alimente la couture : la dernière application du moniteur ─

    private static MockWin32Api Api(string processName, uint pid) => new()
    {
        ScriptedProcessName = processName,
        ScriptedFullPath = @"C:\Windows\" + processName,
        ScriptedPid = pid,
    };

    /// <summary>
    /// Les cinq surfaces éphémères du shell ne sont pas une application : un clic sur la
    /// barre des tâches ou l'ouverture du sélecteur Alt+Tab ne doit rien faire oublier.
    /// </summary>
    [Theory]
    [InlineData("explorer.exe")]
    [InlineData("ShellExperienceHost.exe")]
    [InlineData("SearchHost.exe")]
    [InlineData("StartMenuExperienceHost.exe")]
    [InlineData("TextInputHost.exe")]
    public void Une_surface_du_shell_ne_change_pas_la_derniere_application(string processName)
    {
        var api = Api("winword.exe", 1234);
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);
        var applicationVue = monitor.LastApplicationIdentity;
        Assert.NotEqual(default, applicationVue);

        api.ScriptedProcessName = processName;
        api.ScriptedFullPath = @"C:\Windows\" + processName;
        api.ScriptedPid = 4242;
        monitor.Recompute();

        Assert.Equal(applicationVue, monitor.LastApplicationIdentity);
        Assert.True(monitor.IsTransientShellForeground);
    }

    /// <summary>
    /// Nos propres fenêtres non plus — clavier visuel, recherche de caractères, réglages.
    /// L'utilisateur n'a pas quitté son texte pour autant.
    /// </summary>
    [Fact]
    public void Notre_propre_processus_ne_change_pas_la_derniere_application()
    {
        var api = Api("winword.exe", 1234);
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);
        var applicationVue = monitor.LastApplicationIdentity;

        api.ScriptedProcessName = "AZERTY Global.exe";
        api.ScriptedFullPath = @"C:\Program Files\AZERTY Global.exe";
        api.ScriptedPid = (uint)Environment.ProcessId;
        monitor.Recompute();

        Assert.Equal(applicationVue, monitor.LastApplicationIdentity);
    }

    /// <summary>
    /// ⛔ La réciproque des deux précédents : une application ordinaire, elle, change bien
    /// la dernière application. Sans ce témoin, les deux au-dessus passeraient aussi bien
    /// si <c>LastApplicationIdentity</c> ne bougeait **jamais**.
    /// </summary>
    [Fact]
    public void Une_application_ordinaire_change_la_derniere_application()
    {
        var api = Api("winword.exe", 1234);
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);
        var applicationVue = monitor.LastApplicationIdentity;

        api.ScriptedProcessName = "firefox.exe";
        api.ScriptedFullPath = @"C:\Program Files\firefox.exe";
        api.ScriptedPid = 5678;
        monitor.Recompute();

        Assert.NotEqual(applicationVue, monitor.LastApplicationIdentity);
        Assert.False(monitor.IsTransientShellForeground);
    }
}
