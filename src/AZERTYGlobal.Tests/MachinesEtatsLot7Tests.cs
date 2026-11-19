using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests de régression du lot 7 de l'analyse des machines à états (accueil, sollicitation d'avis).
/// Chaque test affirme l'invariant correct : il échoue tant que le constat est vrai.
/// </summary>
public sealed class MachinesEtatsLot7Tests : IDisposable
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Statique = BindingFlags.Static | BindingFlags.NonPublic;

    private readonly Func<bool, bool> _setterOrigin = OnboardingWindow.AutoStartSetter;
    private readonly Func<bool> _registeredOrigin = OnboardingWindow.AutoStartIsRegistered;
    private readonly Func<IntPtr, string, string, uint, int> _boxOrigin = OnboardingWindow.ShowMessageBox;
    private readonly Func<long> _clockOrigin = OnboardingWindow.Clock;
    private readonly Func<int> _delayOrigin = OnboardingWindow.DoubleClickDelay;
    private readonly string _languageOrigin = L.Language;
    private readonly Func<IntPtr, bool, bool> _minuterieOrigin = TrayApplication.ReviewTimerSwitch;

    public void Dispose()
    {
        OnboardingWindow.AutoStartSetter = _setterOrigin;
        OnboardingWindow.AutoStartIsRegistered = _registeredOrigin;
        OnboardingWindow.ShowMessageBox = _boxOrigin;
        OnboardingWindow.Clock = _clockOrigin;
        OnboardingWindow.DoubleClickDelay = _delayOrigin;
        L.Language = _languageOrigin;
        TrayApplication.ReviewTimerSwitch = _minuterieOrigin;
    }

    private static OnboardingWindow OpenOnboarding()
    {
        var window = new OnboardingWindow();
        window.Show();
        return window;
    }

    private static void GoToLastStep(OnboardingWindow window)
    {
        while (BancCapture.Field<int>(window, "_currentStep") < OnboardingStep.Last)
            BancCapture.Call(window, "GoToNextStep");
    }

    // ── C7.1 ────────────────────────────────────────────────────────────────

    // Constat C7.1
    // Double clic sur « Suivant » à l'étape 2 : deux vrais clics sur le bouton (BM_CLICK, donc deux
    // WM_COMMAND IDC_BTN_NEXT) se suivent dans le délai de double clic de Windows. Le premier amène à
    // l'étape 3 ; le second, devenu « C'est parti ! » sous le même identifiant, ne doit pas valider
    // l'accueil avant que l'étape 3 ait été vue. Un clic posé après le délai valide toujours.
    // Horloge et délai injectés. Le double clic est rejoué par les messages de la souris que le bouton
    // système reçoit (WM_LBUTTONDOWN puis WM_LBUTTONUP, puis WM_LBUTTONDBLCLK et WM_LBUTTONUP) : la
    // suite de C7.1 ne filtre que ce second clic-là, pas une activation au clavier.
    [Fact]
    public void Accueil_DoubleClicSuivantÀLÉtape2_NeValidePasSansAvoirVuLÉtape3()
    {
        using var isolation = new BancCapture.Isolation();
        int ecritures = 0;
        long maintenant = 1_000_000;
        OnboardingWindow.Clock = () => maintenant;
        OnboardingWindow.DoubleClickDelay = () => 500;
        OnboardingWindow.AutoStartIsRegistered = () => false;
        OnboardingWindow.AutoStartSetter = _ => { ecritures++; return true; };
        OnboardingWindow.ShowMessageBox = (_, _, _, _) => 1;
        var window = OpenOnboarding();
        try
        {
            BancCapture.SetField(window, "_activationAccepted", true);
            BancCapture.Call(window, "GoToNextStep"); // étape 1 -> étape 2 (index 1)
            Assert.Equal(1, BancCapture.Field<int>(window, "_currentStep"));
            IntPtr suivant = BancCapture.Field<IntPtr>(window, "_hWndBtnNext");
            maintenant += 2_000; // l'utilisateur est resté un moment sur l'étape 2

            ClicSouris(suivant, doubleClic: false);
            Assert.Equal(OnboardingStep.Last, BancCapture.Field<int>(window, "_currentStep"));
            maintenant += 120; // second clic du double clic
            ClicSouris(suivant, doubleClic: true);

            Assert.True(BancCapture.Field<bool>(window, "_visible"),
                "le second clic a validé et fermé l'accueil sans que l'étape 3 ait été vue");
            Assert.Equal(0, ecritures);
            Assert.Equal(OnboardingStep.Last, BancCapture.Field<int>(window, "_currentStep"));

            maintenant += 600; // un clic voulu, après le délai de double clic
            ClicSouris(suivant, doubleClic: false);

            Assert.False(BancCapture.Field<bool>(window, "_visible"),
                "un clic après le délai de double clic doit valider l'accueil");
            Assert.Equal(1, ecritures);
        }
        finally { BancCapture.Teardown(window); }
    }

    private static void ClicSouris(IntPtr bouton, bool doubleClic)
    {
        IntPtr point = (IntPtr)((5 << 16) | 5);
        Win32.SendMessageW(bouton, doubleClic ? Win32.WM_LBUTTONDBLCLK : Win32.WM_LBUTTONDOWN, (IntPtr)1, point);
        Win32.SendMessageW(bouton, Win32.WM_LBUTTONUP, IntPtr.Zero, point);
    }

    // Constat C7.1 (suite : le filtre ne vise que le double clic de souris)
    // Entrée ou Espace répétés au clavier, ou un lecteur d'écran qui invoque « Suivant » deux fois :
    // l'app reçoit deux WM_COMMAND (BM_CLICK, comme l'envoie IDOK ou l'invocation d'accessibilité)
    // dans le délai de double clic, sans aucun message de souris. Les deux agissent.
    [Fact]
    public void Accueil_DeuxActivationsAuClavierRapprochées_AvancentDeDeuxÉtapes()
    {
        using var isolation = new BancCapture.Isolation();
        long maintenant = 1_000_000;
        OnboardingWindow.Clock = () => maintenant;
        OnboardingWindow.DoubleClickDelay = () => 500;
        OnboardingWindow.AutoStartIsRegistered = () => false;
        OnboardingWindow.AutoStartSetter = _ => true;
        OnboardingWindow.ShowMessageBox = (_, _, _, _) => 1;
        var window = OpenOnboarding();
        try
        {
            BancCapture.SetField(window, "_activationAccepted", true);
            IntPtr suivant = BancCapture.Field<IntPtr>(window, "_hWndBtnNext");

            Win32.SendMessageW(suivant, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            maintenant += 120;
            Win32.SendMessageW(suivant, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);

            Assert.Equal(OnboardingStep.Last, BancCapture.Field<int>(window, "_currentStep"));
        }
        finally { BancCapture.Teardown(window); }
    }

    // Constat C7.1 (suite : « Précédent » puis « Suivant » enchaînés)
    // « Précédent » arme aussi l'horodatage du changement d'étape : le « Suivant » qui suit, au
    // clavier ou par l'accessibilité, ne doit pas être perdu.
    [Fact]
    public void Accueil_PrécédentPuisSuivantEnchaînés_AgissentTousLesDeux()
    {
        using var isolation = new BancCapture.Isolation();
        long maintenant = 1_000_000;
        OnboardingWindow.Clock = () => maintenant;
        OnboardingWindow.DoubleClickDelay = () => 500;
        OnboardingWindow.AutoStartIsRegistered = () => false;
        OnboardingWindow.AutoStartSetter = _ => true;
        OnboardingWindow.ShowMessageBox = (_, _, _, _) => 1;
        var window = OpenOnboarding();
        try
        {
            BancCapture.SetField(window, "_activationAccepted", true);
            BancCapture.Call(window, "GoToNextStep"); // étape 2 (index 1)
            IntPtr suivant = BancCapture.Field<IntPtr>(window, "_hWndBtnNext");
            IntPtr precedent = BancCapture.Field<IntPtr>(window, "_hWndBtnPrev");

            Win32.SendMessageW(precedent, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            Assert.Equal(0, BancCapture.Field<int>(window, "_currentStep"));
            maintenant += 100;
            Win32.SendMessageW(suivant, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);

            Assert.Equal(1, BancCapture.Field<int>(window, "_currentStep"));
        }
        finally { BancCapture.Teardown(window); }
    }

    // Constat C7.1 (suite : un double clic abandonné ne laisse pas de trace)
    // Le second clic d'un double clic est pressé puis relâché hors du bouton : aucun clic n'est
    // produit. L'activation au clavier qui suit, dans le délai, agit.
    [Fact]
    public void Accueil_DoubleClicAbandonné_NeFiltrePasLActivationAuClavierSuivante()
    {
        using var isolation = new BancCapture.Isolation();
        long maintenant = 1_000_000;
        OnboardingWindow.Clock = () => maintenant;
        OnboardingWindow.DoubleClickDelay = () => 500;
        OnboardingWindow.AutoStartIsRegistered = () => false;
        OnboardingWindow.AutoStartSetter = _ => true;
        OnboardingWindow.ShowMessageBox = (_, _, _, _) => 1;
        var window = OpenOnboarding();
        try
        {
            BancCapture.SetField(window, "_activationAccepted", true);
            IntPtr suivant = BancCapture.Field<IntPtr>(window, "_hWndBtnNext");

            ClicSouris(suivant, doubleClic: false); // étape 2
            maintenant += 100;
            IntPtr dedans = (IntPtr)((5 << 16) | 5);
            IntPtr dehors = (IntPtr)((5000 << 16) | 5000);
            Win32.SendMessageW(suivant, Win32.WM_LBUTTONDBLCLK, (IntPtr)1, dedans);
            Win32.SendMessageW(suivant, Win32.WM_LBUTTONUP, IntPtr.Zero, dehors);
            Assert.Equal(1, BancCapture.Field<int>(window, "_currentStep"));
            maintenant += 100;
            Win32.SendMessageW(suivant, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);

            Assert.Equal(OnboardingStep.Last, BancCapture.Field<int>(window, "_currentStep"));
        }
        finally { BancCapture.Teardown(window); }
    }

    // ── C7.2 ────────────────────────────────────────────────────────────────

    // Constat C7.2
    // « Ne plus afficher » persisté à false (refus explicite) ; l'accueil est rouvert, « C'est parti ! »
    // est validé sans toucher la case : le refus ne doit pas être réécrit à true.
    // Modèle suivi : Audit0911AccueilAProposTests.Accueil_CaseNePlusAfficher_ÉcriteAuClic.
    [Fact]
    public void Accueil_RefusPersisté_NEstPasDéfaitParUneValidationSansToucherLaCase()
    {
        using var isolation = new BancCapture.Isolation();
        OnboardingWindow.AutoStartIsRegistered = () => false;
        OnboardingWindow.AutoStartSetter = _ => true;
        OnboardingWindow.ShowMessageBox = (_, _, _, _) => 1;
        ConfigManager.SetShowOnboardingAtStartup(false);
        var window = OpenOnboarding();
        try
        {
            BancCapture.SetField(window, "_activationAccepted", true);
            GoToLastStep(window);

            BancCapture.Call(window, "Close", true);

            Assert.False(ConfigManager.ShowOnboardingAtStartup,
                "la validation a réécrit à true un refus d'affichage déjà persisté");
        }
        finally { BancCapture.Teardown(window); }
    }

    // Constat C7.2 (variante miroir)
    // La case « Ne plus afficher » affichée reflète le réglage persisté : cochée à l'ouverture quand
    // l'affichage au démarrage est refusé.
    // Modèle suivi : Audit0911AccueilAProposTests.Accueil_CaseNePlusAfficher_ÉcriteAuClic.
    [Fact]
    public void Accueil_CaseNePlusAfficher_EstCochéeÀLOuvertureQuandLeRéglageEstRefusé()
    {
        using var isolation = new BancCapture.Isolation();
        OnboardingWindow.AutoStartIsRegistered = () => false;
        OnboardingWindow.AutoStartSetter = _ => true;
        ConfigManager.SetShowOnboardingAtStartup(false);
        var window = OpenOnboarding();
        try
        {
            Assert.True(BancCapture.Field<bool>(window, "_dontShowChecked"),
                "la case est affichée décochée alors que le réglage persisté est « ne plus afficher »");
        }
        finally { BancCapture.Teardown(window); }
    }

    // Constat C7.2 (garde de la décision v0.9.7.1 et de l'audit 24/09, B2)
    // Case reprise du refus enregistré : Échap ou la croix à l'étape 3, sans toucher la case, ne
    // valident rien ; décochée par l'utilisateur, la case est un choix, que la fermeture enregistre.
    [Fact]
    public void Accueil_CaseReprise_ÉchapNeValideRien_MaisUnChoixDécochéEstEnregistré()
    {
        using var isolation = new BancCapture.Isolation();
        OnboardingWindow.AutoStartIsRegistered = () => false;
        OnboardingWindow.AutoStartSetter = _ => true;
        ConfigManager.SetShowOnboardingAtStartup(false);
        var window = OpenOnboarding();
        try
        {
            BancCapture.SetField(window, "_activationAccepted", true);
            GoToLastStep(window);
            BancCapture.Call(window, "Close", false); // Échap sans toucher la case
            Assert.False(ConfigManager.ShowOnboardingAtStartup, "Échap a défait le refus enregistré");

            window.Show();
            BancCapture.SetField(window, "_activationAccepted", true);
            GoToLastStep(window);
            // L'utilisateur décoche : un choix, même fermé par la croix ou Échap.
            BancCapture.Call(window, "SetChecked", BancCapture.Field<IntPtr>(window, "_hWndChkDontShow"), false, false);
            BancCapture.Call(window, "Close", false);
            Assert.True(ConfigManager.ShowOnboardingAtStartup, "décocher puis fermer doit enregistrer « afficher »");
        }
        finally { BancCapture.Teardown(window); }
    }


    // Constat C7.3
    // L'accueil, rouvert depuis le menu (donc _reviewPromptDeferred faux), est visible ; le signal de
    // frappe est armé et la quiétude atteinte. Le tick ne doit pas consommer l'essai 1 par-dessus
    // l'accueil : il est différé, signal conservé.
    // Modèle suivi : SollicitationAvisCaracterisationTests.Tick_accueil_ouvert_differe.
    [Fact]
    public void Avis_AccueilRouvertDepuisLeMenu_DiffèreLaSollicitationAuTickDeQuiétude()
    {
        using var env = new EnvAvis();
        UsageStats.ArmEnrichedThresholdSignal();
        DerniereFrappeIlYa(20_000);

        var accueil = (OnboardingWindow)RuntimeHelpers.GetUninitializedObject(typeof(OnboardingWindow));
        typeof(OnboardingWindow).GetField("_visible", Instance)!.SetValue(accueil, true);
        Assert.True(accueil.IsVisible);

        var tray = (TrayApplication)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplication));
        typeof(TrayApplication).GetField("_versionFirstRun", Instance)!
            .SetValue(tray, new ConfigManager.VersionFirstRun("1.3.0", Aujourdhui, false));
        typeof(TrayApplication).GetField("_reviewPromptDeferred", Instance)!.SetValue(tray, false);
        typeof(TrayApplication).GetField("_onboarding", Instance)!.SetValue(tray, accueil);

        typeof(TrayApplication).GetMethod("MaybeShowReviewAfterQuietTyping", Instance)!.Invoke(tray, null);

        Assert.Equal(0, ConfigManager.ReviewPromptCount);
        Assert.Null(ConfigManager.ReviewPromptLastShown);
        Assert.True(UsageStats.EnrichedThresholdCrossed, "le signal doit rester armé : l'avis est différé, pas annulé");
    }

    // Constat C7.4
    // Notifications coupées au lancement : la minuterie de quiétude n'est pas posée. Rallumées dans
    // les Paramètres (même processus), la fenêtre prévient le tray, qui la repose : l'essai 1 n'attend
    // plus le prochain lancement.
    [Fact]
    public void Avis_NotificationsRallumeesDansLesParametres_ReposentLaMinuterieDeQuietude()
    {
        using var env = new EnvAvis();
        var appels = new List<bool>();
        TrayApplication.ReviewTimerSwitch = (_, on) => { appels.Add(on); return on; };
        ConfigManager.SetNotifications(false);

        var tray = (TrayApplication)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplication));
        tray.EnsureReviewTimer(); // démarrage : rien à solliciter
        Assert.False(tray.ReviewTimerArmed);

        using var parametres = new SettingsWindow();
        typeof(TrayApplication).GetMethod("ConfigureSettingsWindow", Instance)!.Invoke(tray, new object[] { parametres });
        ConfigManager.SetNotifications(true);
        parametres.NotificationsChanged?.Invoke();

        Assert.True(tray.ReviewTimerArmed, "la minuterie doit être reposée quand les notifications sont rallumées");
        Assert.Equal(new[] { true }, appels);
    }

    // Constat C7.4 (côté fenêtre : la case des Paramètres prévient le tray après avoir écrit le réglage)
    [Fact]
    public void Parametres_CaseNotifications_PrevientLeTrayApresEcriture()
    {
        using var env = new EnvAvis();
        ConfigManager.SetNotifications(false); // la case se construit décochée
        using var parametres = new SettingsWindow();
        bool? valeurVue = null;
        parametres.NotificationsChanged = () => valeurVue = ConfigManager.NotificationsEnabled;

        ConfigManager.SetNotifications(true); // réglage antérieur différent de la case
        typeof(SettingsWindow).GetMethod("ApplyNotifications", Instance)!.Invoke(parametres, null);

        Assert.False(ConfigManager.NotificationsEnabled);
        Assert.False(valeurVue, "le tray doit être prévenu après l'écriture, et voir la nouvelle valeur");
    }

    // Constat C7.4 (cycle complet : arrêt par le tick pendant la coupure, puis reprise au rallumage)
    [Fact]
    public void Avis_TickPendantLaCoupure_ArreteLaMinuterie_PuisLeRallumageLaRepose()
    {
        using var env = new EnvAvis();
        var appels = new List<bool>();
        TrayApplication.ReviewTimerSwitch = (_, on) => { appels.Add(on); return on; };

        var tray = (TrayApplication)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplication));
        tray.EnsureReviewTimer();
        Assert.True(tray.ReviewTimerArmed);

        ConfigManager.SetNotifications(false);
        typeof(TrayApplication).GetMethod("MaybeShowReviewAfterQuietTyping", Instance)!.Invoke(tray, null);
        Assert.False(tray.ReviewTimerArmed);

        ConfigManager.SetNotifications(true);
        tray.EnsureReviewTimer();
        tray.EnsureReviewTimer(); // idempotente
        Assert.True(tray.ReviewTimerArmed);
        Assert.Equal(new[] { true, false, true }, appels);
    }

    private static DateOnly Aujourdhui => DateOnly.FromDateTime(DateTime.Now);

    private static void DerniereFrappeIlYa(long ms) =>
        typeof(UsageStats).GetField("_lastRemapTickCount", Statique)!
            .SetValue(null, Environment.TickCount64 - ms);

    /// <summary>Config et statistiques isolées : essai 1 tout juste atteint, notifications actives.</summary>
    private sealed class EnvAvis : IDisposable
    {
        private readonly string _dossier;
        private readonly IDisposable _politique;
        private readonly IDisposable _canal;

        public EnvAvis()
        {
            _dossier = Path.Combine(Path.GetTempPath(), "AZGLot7_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dossier);
            _politique = PolicyManager.OverrideForTests((_, _) => null);
            _canal = AppChannel.OverrideForTests(DistributionChannel.Store);
            LearningSessionTracker.ResetForTests();
            typeof(ConfigManager).GetField("_lastErrorUtc", Statique)!.SetValue(null, null);

            string config = Path.Combine(_dossier, "config.json");
            File.WriteAllText(config, JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["notificationsEnabled"] = true,
            }));
            ConfigManager.OverrideConfigPathForTests(config);

            string stats = Path.Combine(_dossier, "usage-stats.json");
            string Jour(DateOnly d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            File.WriteAllText(stats, JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["activeDaysCount"] = 10,
                ["totalActiveMinutes"] = 10L,
                ["accentedUppercaseCount"] = 20L,
                ["firstRemapDate"] = Jour(Aujourdhui.AddDays(-30)),
                ["lastActiveDate"] = Jour(Aujourdhui),
            }));
            UsageStats.OverrideStatsPathForTests(stats);
        }

        public void Dispose()
        {
            UsageStats.ClearEnrichedThresholdSignal();
            LearningSessionTracker.ResetForTests();
            _canal.Dispose();
            _politique.Dispose();
            ConfigManager.OverrideConfigPathForTests(Path.Combine(_dossier, "fin.json"));
            UsageStats.OverrideStatsPathForTests(Path.Combine(_dossier, "fin-stats.json"));
            try { Directory.Delete(_dossier, true); } catch { }
        }
    }
}
