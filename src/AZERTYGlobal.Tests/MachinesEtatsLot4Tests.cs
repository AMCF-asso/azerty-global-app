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
    private static readonly DateTime Instant = new(2026, 9, 25, 21, 30, 5);
    private string ConfigPath => Path.Combine(_dir, "config.json");
    private const string ConfigCorrompu = "{\"activationConsent\":true,\"appLanguage\":\"fr\",";

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
        FileQuarantine.Clock = () => DateTime.Now;
        ConfigManager.SaveSchedulerForTests = null;
        ConfigManager.DiskWritePhaseForTests = null;
        ConfigManager.SaveDelayMilliseconds = -1;
        for (int essai = 0; essai < 20; essai++)
        {
            try { Directory.Delete(_dir, true); return; }
            catch (IOException) { Thread.Sleep(25); }
            catch (UnauthorizedAccessException) { Thread.Sleep(25); }
        }
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

    // ───────────────────────── ME-09 : instance unique et fermeture ─────────────────────────

    // Constat C4.5
    // config.json est corrompu ; l'autre instance l'a déjà mis de côté entre la lecture et le
    // renommage (File.Move lève FileNotFoundException). Le fichier n'existe plus : l'état
    // « chargement échoué » ne doit pas rester collé, et l'accord doit pouvoir être mémorisé.
    [Fact]
    public void Fichier_corrompu_deja_mis_de_cote_par_l_autre_instance_ne_bloque_pas_les_ecritures()
    {
        File.WriteAllText(ConfigPath, ConfigCorrompu);
        FileQuarantine.Clock = () =>
        {
            File.Delete(ConfigPath); // l'autre processus l'a déjà déplacé
            return Instant;
        };

        ConfigManager.AcceptActivation();
        bool accordEnSession = ConfigManager.ActivationConsent;
        ConfigManager.Flush();

        Assert.True(accordEnSession);
        Assert.True(File.Exists(ConfigPath));
        ConfigManager.OverrideConfigPathForTests(ConfigPath);
        Assert.True(ConfigManager.ActivationConsent);
    }

    // Constat C4.5
    // La langue lue au tout début du démarrage, avant les mutex, ne charge pas le fichier : un
    // fichier corrompu n'est pas mis de côté par une instance qui ne sait pas encore si elle
    // gagne. Le gagnant le met de côté ensuite, au premier accès, et dit une fois le message.
    [Fact]
    public void Langue_lue_au_demarrage_ne_met_pas_le_fichier_corrompu_de_cote()
    {
        File.WriteAllText(ConfigPath, ConfigCorrompu);

        string langue = ConfigManager.AppLanguageWithoutLoading();

        Assert.Equal(ConfigCorrompu, File.ReadAllText(ConfigPath));
        Assert.Empty(Directory.GetFiles(_dir, "config.json.illisible-*"));
        Assert.Equal(ConfigManager.DefaultAppLanguage(ConfigManager.WindowsUiLanguageIdForTests()), langue);

        Assert.True(ConfigManager.TakeQuarantineNotice());
        Assert.Single(Directory.GetFiles(_dir, "config.json.illisible-*"));
    }

    // Constat C4.5
    // La lecture de démarrage rend la langue du chargement normal, pour chaque forme de fichier,
    // et ne laisse aucune trace (ni fichier créé, ni copie mise de côté).
    [Theory]
    [InlineData("{\"appLanguage\":\"en\"}")]
    [InlineData("{\"appLanguage\":\"fr\"}")]
    [InlineData("{\"activationConsent\":true}")]
    [InlineData("{\"appLanguage\":3}")]
    [InlineData("[1,2]")]
    [InlineData(ConfigCorrompu)]
    [InlineData(null)]
    public void Langue_lue_au_demarrage_est_celle_du_chargement_normal(string? contenu)
    {
        if (contenu != null) File.WriteAllText(ConfigPath, contenu);

        string auDemarrage = ConfigManager.AppLanguageWithoutLoading();

        Assert.Equal(contenu != null, File.Exists(ConfigPath));
        Assert.Empty(Directory.GetFiles(_dir, "config.json.illisible-*"));
        ConfigManager.OverrideConfigPathForTests(ConfigPath);
        Assert.Equal(ConfigManager.AppLanguage, auDemarrage);
    }

    private readonly List<int> _armes = new();
    private int _echecsAVenir;

    /// <summary>Remplace la minuterie par une liste et fait échouer les prochaines écritures.</summary>
    private void FaireEchouerLEcriture(int echecs)
    {
        File.WriteAllText(ConfigPath, "{\"appLanguage\":\"fr\"}");
        ConfigManager.SaveDelayMilliseconds = 400; // valeur de production
        ConfigManager.SaveSchedulerForTests = delai => _armes.Add(delai);
        _echecsAVenir = echecs;
        ConfigManager.DiskWritePhaseForTests = () =>
        {
            if (_echecsAVenir > 0)
            {
                _echecsAVenir--;
                throw new IOException("remplacement refusé (simulé)");
            }
        };
    }

    // Constat C4.3
    // Un verrou externe fait échouer les deux premières écritures ; l'écriture de fermeture
    // réessaie sur place au lieu de compter sur une minuterie qui meurt avec le processus.
    [Fact]
    public void Ecriture_de_fermeture_reessaie_puis_ecrit_le_reglage()
    {
        FaireEchouerLEcriture(2);
        ConfigManager.AcceptActivation();
        var pauses = new List<int>();

        bool ecrit = ConfigManager.FlushFinal(sleep: pauses.Add);

        Assert.True(ecrit);
        Assert.Equal(2, pauses.Count);
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ConfigPath));
        Assert.True(doc.RootElement.GetProperty("activationConsent").GetBoolean());
    }

    // Constat C4.3
    // Verrou permanent : l'écriture de fermeture rend la main sous le plafond de durée (pauses
    // simulées, jamais attendues) et journalise la perte au lieu de la taire.
    [Fact]
    public void Ecriture_de_fermeture_impossible_rend_la_main_sous_le_plafond_et_journalise_la_perte()
    {
        FaireEchouerLEcriture(int.MaxValue);
        ConfigManager.AcceptActivation();
        var pauses = new List<int>();

        bool ecrit = ConfigManager.FlushFinal(sleep: pauses.Add);

        Assert.False(ecrit);
        Assert.Equal(ConfigManager.FinalFlushRetries, pauses.Count);
        Assert.InRange(pauses.Sum(), 1, 1500);
        using var flux = new FileStream(Path.Combine(_dir, "error.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var lecteur = new StreamReader(flux);
        Assert.Contains("ConfigFinalWriteLost", lecteur.ReadToEnd());
    }

    // Constat C4.3 (comportement conservé) : rien à écrire, aucune attente.
    [Fact]
    public void Ecriture_de_fermeture_sans_reglage_en_attente_n_attend_pas()
    {
        FaireEchouerLEcriture(0);
        _ = ConfigManager.ActivationConsent; // charge, sans rien poser
        var pauses = new List<int>();

        Assert.True(ConfigManager.FlushFinal(sleep: pauses.Add));
        Assert.Empty(pauses);
    }

    // Constat C4.3
    // Les deux sorties du processus passent par l'écriture de fermeture : WM_QUERYENDSESSION
    // (arrêt annoncé) et Cleanup (menu Quitter, WM_ENDSESSION). Un simple Flush y perdrait le
    // réglage sur un échec d'écriture.
    [Fact]
    public void Arret_et_sortie_du_tray_passent_par_l_ecriture_de_fermeture()
    {
        string source = File.ReadAllText(SourceDeLApp("TrayApplication.cs"));
        int debut = source.IndexOf("case Win32.WM_QUERYENDSESSION:", StringComparison.Ordinal);
        int fin = source.IndexOf("case Win32.WM_ENDSESSION:", StringComparison.Ordinal);
        Assert.True(debut > 0 && fin > debut, "WM_QUERYENDSESSION introuvable dans TrayApplication.cs");
        string arret = source[debut..fin];

        Assert.Contains("ConfigManager.FlushFinal();", arret);
        Assert.DoesNotContain("ConfigManager.Flush();", arret);
        Assert.Contains("try { ConfigManager.FlushFinal(); }", source);
        Assert.DoesNotContain("try { ConfigManager.Flush(); }", source);
    }

    private static string SourceDeLApp(string fichier)
    {
        for (var dossier = new DirectoryInfo(AppContext.BaseDirectory); dossier != null; dossier = dossier.Parent)
        {
            string chemin = Path.Combine(dossier.FullName, "src", fichier);
            if (File.Exists(chemin)) return chemin;
        }
        throw new FileNotFoundException(fichier);
    }

    private static List<string> Acquerir(Func<string, (Mutex, bool)> fabrique,
        out Program.SingleInstanceOutcome issue, out Mutex? local, out Mutex? global)
    {
        var journal = new List<string>();
        issue = Program.AcquireSingleInstance("Local\\essai", "Global\\essai", fabrique,
            (evenement, detail) => journal.Add(evenement + ":" + detail), out local, out global);
        return journal;
    }

    // Constat C4.1
    // Une instance ÉLEVÉE tient déjà le mutex Local\ : son DACL interdit l'ouverture au jeton
    // non élevé du même compte (UnauthorizedAccessException). Ce lancement est une seconde
    // instance, il sort sans planter ; le refus est journalisé.
    [Fact]
    public void Mutex_local_refuse_par_une_instance_elevee_est_une_seconde_instance()
    {
        var appeles = new List<string>();
        var journal = Acquerir(nom =>
        {
            appeles.Add(nom);
            throw new UnauthorizedAccessException("accès refusé (simulé)");
        }, out var issue, out var local, out var global);
        try
        {
            Assert.Equal(Program.SingleInstanceOutcome.SecondInstance, issue);
            Assert.Null(local);
            Assert.Null(global);
            Assert.Equal(new[] { "Local\\essai" }, appeles);
            Assert.Contains("SingleInstanceMutexDenied:UnauthorizedAccessException", journal);
        }
        finally { local?.Dispose(); global?.Dispose(); }
    }

    // Constat C4.1 (comportement conservé) : les quatre issues ordinaires de l'acquisition.
    [Theory]
    [InlineData(true, true, "Winner")]
    [InlineData(false, true, "SecondInstance")]
    [InlineData(true, false, "OtherSession")]
    [InlineData(false, false, "SecondInstance")]
    public void Acquisition_des_deux_mutex_dit_qui_l_on_est(bool localNeuf, bool globalNeuf, string attendu)
    {
        var journal = Acquerir(nom => (new Mutex(), nom.StartsWith("Local") ? localNeuf : globalNeuf),
            out var issue, out var local, out var global);
        try
        {
            Assert.Equal(Enum.Parse<Program.SingleInstanceOutcome>(attendu), issue);
            Assert.NotNull(local);
            Assert.NotNull(global);
            Assert.Empty(journal);
        }
        finally { local?.Dispose(); global?.Dispose(); }
    }

    // Constat C4.1 (comportement conservé) : sans le privilège Global\, on démarre quand même.
    [Fact]
    public void Mutex_global_indisponible_laisse_demarrer_en_mode_degrade()
    {
        var journal = Acquerir(nom => nom.StartsWith("Global")
                ? throw new UnauthorizedAccessException("privilège absent (simulé)")
                : (new Mutex(), true),
            out var issue, out var local, out var global);
        try
        {
            Assert.Equal(Program.SingleInstanceOutcome.Winner, issue);
            Assert.NotNull(local);
            Assert.Null(global);
            Assert.Contains("CrossSessionMutexUnavailable:UnauthorizedAccessException", journal);
        }
        finally { local?.Dispose(); global?.Dispose(); }
    }
}
