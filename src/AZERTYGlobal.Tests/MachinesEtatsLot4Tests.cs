using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests de régression du lot 4 de l'analyse des machines à états (lancement automatique).
/// Chaque test affirme le comportement CORRECT : « effectif » veut dire « Windows l'exécutera »
/// (état Enabled), jamais « le raccourci existe ».
/// </summary>
public class MachinesEtatsLot4Tests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AZGLOT4_" + Guid.NewGuid().ToString("N"));
    private readonly Func<AutoStartWindowsState> _stateOrigin = AutoStart.WindowsStateReader;
    private readonly Func<bool> _existsOrigin = AutoStart.ShortcutExists;
    private readonly Func<bool> _enablerOrigin = AutoStart.ShortcutEnabler;
    private readonly string _languageOrigin = L.Language;

    public MachinesEtatsLot4Tests()
    {
        Directory.CreateDirectory(_dir);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_dir, "config.json"));
    }

    public void Dispose()
    {
        AutoStart.WindowsStateReader = _stateOrigin;
        AutoStart.ShortcutExists = _existsOrigin;
        AutoStart.ShortcutEnabler = _enablerOrigin;
        L.Language = _languageOrigin;
        try { Directory.Delete(_dir, true); } catch { }
    }

    /// <summary>Raccourci présent mais refusé par Windows (Gestionnaire des tâches, stratégie).</summary>
    private void WindowsRefuse(AutoStartWindowsState etat)
    {
        AutoStart.WindowsStateReader = () => etat;
        AutoStart.ShortcutExists = () => true;
        AutoStart.ShortcutEnabler = () => true;
    }

    // Constat C4.2
    // Raccourci de démarrage présent mais refusé dans Windows (Gestionnaire des tâches ou
    // stratégie), premier accueil sans trace de choix : la case ne doit pas être cochée,
    // car Windows n'exécutera pas l'entrée au redémarrage.
    [Theory]
    [InlineData("DisabledByUser")]
    [InlineData("DisabledByPolicy")]
    public void Accueil_ne_coche_pas_la_case_quand_Windows_refuse_meme_si_le_raccourci_existe(
        string etat)
    {
        Assert.False(AutoStart.DefaultOnboardingCheck(
            Enum.Parse<AutoStartWindowsState>(etat), isRegistered: true, choiceAlreadyPossible: false));
    }

    // Constat C4.2
    // Activer depuis les Paramètres alors que Windows refuse l'entrée : l'activation ne se dit
    // pas réussie et ne s'écrit pas dans la configuration.
    [Theory]
    [InlineData("DisabledByUser")]
    [InlineData("DisabledByPolicy")]
    public void Activation_refusee_par_Windows_n_est_pas_dite_reussie(string etat)
    {
        WindowsRefuse(Enum.Parse<AutoStartWindowsState>(etat));

        Assert.False(AutoStart.Set(true));
        Assert.False(ConfigManager.AutoStartEnabled);
    }

    // Constat C4.2
    // Case « Lancer au démarrage » des Paramètres et de l'accueil : elle lit l'état réel, donc
    // décochée tant que Windows refuse, même si le raccourci existe.
    [Fact]
    public void Case_des_Parametres_et_de_l_accueil_refletent_le_refus_de_Windows()
    {
        WindowsRefuse(AutoStartWindowsState.DisabledByUser);

        Assert.False(AutoStart.IsRegistered);
        Assert.False(SettingsWindow.AutoStartIsRegistered());
        Assert.False(OnboardingWindow.AutoStartIsRegistered());
    }

    // Constat C4.2
    // Entrée active pour Windows : l'activation réussit et s'écrit, comme avant.
    [Fact]
    public void Activation_acceptee_par_Windows_reste_dite_reussie()
    {
        AutoStart.WindowsStateReader = () => AutoStartWindowsState.Enabled;
        AutoStart.ShortcutExists = () => true;
        AutoStart.ShortcutEnabler = () => true;

        Assert.True(AutoStart.Set(true));
        Assert.True(ConfigManager.AutoStartEnabled);
    }

    // Constat C4.2
    // Le refus se dit et renvoie vers les Paramètres Windows, en français et en anglais, au lieu
    // d'un conseil sur les droits du dossier Démarrage.
    [Theory]
    [InlineData("fr", "Paramètres Windows")]
    [InlineData("en", "Windows Settings")]
    public void Refus_de_Windows_renvoie_vers_les_Parametres_Windows(string langue, string attendu)
    {
        L.Language = langue;
        WindowsRefuse(AutoStartWindowsState.DisabledByUser);

        string message = AutoStart.GetFailureMessage();

        Assert.Contains(attendu, message);
        Assert.DoesNotContain(langue == "fr" ? "dossier" : "folder", message);
    }

    // Constat C4.4
    // Relance unique : jamais proposée quand Windows refuse ou que l'état est illisible ; proposée
    // quand l'application peut obtenir l'activation (Disabled).
    [Theory]
    [InlineData("DisabledByUser", false)]
    [InlineData("DisabledByPolicy", false)]
    [InlineData("Unknown", false)]
    [InlineData("Disabled", true)]
    public void Relance_unique_n_est_pas_proposee_quand_l_etat_est_refuse_ou_inconnu(
        string etat, bool attendu)
    {
        var signaux = new AutoStartSignals(
            NudgeDone: false, AlreadyRegistered: false, NotificationsEnabled: true,
            FirstRemapDate: new DateOnly(2026, 8, 10), ActiveDaysCount: AutoStartNudge.MinActiveDays,
            WindowsState: Enum.Parse<AutoStartWindowsState>(etat));

        Assert.Equal(attendu, AutoStartNudge.ShouldPrompt(signaux));
    }

    // Constat C4.4
    // La photographie des signaux porte l'état Windows, et un raccourci refusé n'y compte pas
    // comme « déjà actif » : la décision se prend sur l'état, pas sur le fichier.
    [Fact]
    public void Signaux_de_la_relance_portent_l_etat_Windows()
    {
        WindowsRefuse(AutoStartWindowsState.DisabledByUser);

        var signaux = AutoStartNudge.Snapshot();

        Assert.Equal(AutoStartWindowsState.DisabledByUser, signaux.WindowsState);
        Assert.False(signaux.AlreadyRegistered);
    }

    // Constat C4.4
    // Un refus ne consomme pas l'unique relance : aucune proposition émise, donc aucune marque.
    [Fact]
    public void Relance_refusee_ne_consomme_pas_la_proposition()
    {
        WindowsRefuse(AutoStartWindowsState.DisabledByUser);
        var signaux = AutoStartNudge.Snapshot() with
        {
            AlreadyRegistered = false,
            NotificationsEnabled = true,
            FirstRemapDate = new DateOnly(2026, 8, 10),
            ActiveDaysCount = AutoStartNudge.MinActiveDays,
        };

        Assert.False(AutoStartNudge.ShouldPrompt(signaux));
        Assert.False(ConfigManager.AutoStartNudgeDone);
    }
}
