using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests de régression du lot 7 de l'analyse des machines à états (accueil, sollicitation d'avis).
/// Chaque test affirme l'invariant correct : il échoue tant que le constat est vrai.
/// </summary>
public sealed class MachinesEtatsLot7Tests : IDisposable
{
    private readonly Func<bool, bool> _setterOrigin = OnboardingWindow.AutoStartSetter;
    private readonly Func<bool> _registeredOrigin = OnboardingWindow.AutoStartIsRegistered;
    private readonly Func<IntPtr, string, string, uint, int> _boxOrigin = OnboardingWindow.ShowMessageBox;
    private readonly Func<long> _clockOrigin = OnboardingWindow.Clock;
    private readonly Func<int> _delayOrigin = OnboardingWindow.DoubleClickDelay;
    private readonly string _languageOrigin = L.Language;

    public void Dispose()
    {
        OnboardingWindow.AutoStartSetter = _setterOrigin;
        OnboardingWindow.AutoStartIsRegistered = _registeredOrigin;
        OnboardingWindow.ShowMessageBox = _boxOrigin;
        OnboardingWindow.Clock = _clockOrigin;
        OnboardingWindow.DoubleClickDelay = _delayOrigin;
        L.Language = _languageOrigin;
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
    // Horloge et délai injectés. Le vrai double clic à la souris (conversion de WM_LBUTTONDBLCLK en
    // clic par le bouton système) reste à vérifier à la main.
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

            Win32.SendMessageW(suivant, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            Assert.Equal(OnboardingStep.Last, BancCapture.Field<int>(window, "_currentStep"));
            maintenant += 120; // second clic du double clic
            Win32.SendMessageW(suivant, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);

            Assert.True(BancCapture.Field<bool>(window, "_visible"),
                "le second clic a validé et fermé l'accueil sans que l'étape 3 ait été vue");
            Assert.Equal(0, ecritures);
            Assert.Equal(OnboardingStep.Last, BancCapture.Field<int>(window, "_currentStep"));

            maintenant += 600; // un clic voulu, après le délai de double clic
            Win32.SendMessageW(suivant, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);

            Assert.False(BancCapture.Field<bool>(window, "_visible"),
                "un clic après le délai de double clic doit valider l'accueil");
            Assert.Equal(1, ecritures);
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
}
