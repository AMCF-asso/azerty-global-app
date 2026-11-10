using System.Runtime.InteropServices;
using System.Text;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit UX du 09/11, lot Accueil (W6, W16, W17, W24, E28), À propos et Statistiques (W8, W11, E29).
/// Vraies fenêtres, isolées et invisibles par <see cref="BancCapture.Isolation"/>. Aucun vrai
/// démarrage Windows, aucune vraie boîte, aucun vrai presse-papiers : les points d'appui sont remplacés.
/// </summary>
public sealed class Audit0911AccueilAProposTests : IDisposable
{
    private readonly Func<bool, bool> _setterOrigin = OnboardingWindow.AutoStartSetter;
    private readonly Func<bool> _registeredOrigin = OnboardingWindow.AutoStartIsRegistered;
    private readonly Func<IntPtr, string, string, uint, int> _boxOrigin = OnboardingWindow.ShowMessageBox;
    private readonly string _languageOrigin = L.Language;

    public void Dispose()
    {
        OnboardingWindow.AutoStartSetter = _setterOrigin;
        OnboardingWindow.AutoStartIsRegistered = _registeredOrigin;
        OnboardingWindow.ShowMessageBox = _boxOrigin;
        L.Language = _languageOrigin;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLongW(IntPtr hWnd, int nIndex);

    private static bool StyleVisible(IntPtr hwnd) => (GetWindowLongW(hwnd, -16) & 0x10000000) != 0;

    private static string TextOf(IntPtr hwnd)
    {
        var buffer = new StringBuilder(Math.Max(16, Win32.GetWindowTextLengthW(hwnd) + 1));
        Win32.GetWindowTextW(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static OnboardingWindow OpenOnboarding()
    {
        var window = new OnboardingWindow();
        window.Show();
        return window;
    }

    // ── W16 : les flèches suivent le bouton qui a le focus ──────────────────

    [Theory]
    [InlineData(0, false, true, false, "LaunchTutorial")]   // étape 1, focus ailleurs : le parcours
    [InlineData(0, false, false, false, "FocusTry")]        // étape 1, pas d'accord : le bouton d'essai
    [InlineData(0, false, true, true, "Next")]              // étape 1, focus sur « Suivant » : suivant
    [InlineData(0, true, true, false, "Next")]              // parcours fait
    [InlineData(1, false, true, false, "Next")]
    [InlineData(3, false, true, false, "Finish")]           // dernière étape, accord donné
    [InlineData(3, false, false, false, "FocusNext")]       // dernière étape, pas d'accord
    public void FlècheAvant_FaitCeQueLeFocusAnnonce(int step, bool done, bool accepted, bool focusNext, string attendu)
    {
        Assert.Equal(attendu, OnboardingWindow.ForwardArrowAction(step, done, accepted, focusNext).ToString());
    }

    // ── W17 : les raccourcis de l'accueil suivent les réglages ──────────────

    [Fact]
    public void Accueil_RaccourcisParDéfaut_QEtW()
    {
        using var isolation = new BancCapture.Isolation();
        L.Language = "fr";
        Assert.EndsWith(" + Q", OnboardingWindow.KeyboardShortcut());
        Assert.EndsWith(" + W", OnboardingWindow.SearchShortcut());
    }

    [Fact]
    public void Accueil_RaccourcisChangés_LAccueilLesÉcrit()
    {
        using var isolation = new BancCapture.Isolation();
        L.Language = "fr";
        ConfigManager.ShortcutVirtualKeyboardVk = 0x4B;   // K
        ConfigManager.ShortcutCharacterSearchVk = 0x4A;   // J
        Assert.EndsWith(" + K", OnboardingWindow.KeyboardShortcut());
        Assert.EndsWith(" + J", OnboardingWindow.SearchShortcut());
        Assert.Contains("Ctrl + Maj + K", OnboardingWindow.StepAccessibleSummary(OnboardingStep.HowTo));
        Assert.Contains("Ctrl + Maj + J", OnboardingWindow.StepAccessibleSummary(OnboardingStep.HowTo));
    }

    /// <summary>Audit de régression du 10/11, RA15 : l'astuce dessinée à l'étape des langues
    /// gardait « W » écrit en dur, alors que son texte lu suivait déjà le réglage.</summary>
    [Theory]
    [InlineData(0u, "Ctrl + Maj + W")]     // réglage par défaut
    [InlineData(0x4Au, "Ctrl + Maj + J")]  // recherche réglée sur J
    public void Accueil_AstuceDesLangues_ÉcritLeRaccourciRéglé(uint vk, string attendu)
    {
        using var isolation = new BancCapture.Isolation();
        L.Language = "fr";
        if (vk != 0) ConfigManager.ShortcutCharacterSearchVk = vk;

        var runs = OnboardingWindow.TipRuns();
        string astuce = string.Concat(runs.Select(r => r.Text));

        Assert.Equal(L.Onboarding_LanguagesTipPrefix + attendu + L.Onboarding_LanguagesTipSuffix, astuce);
        Assert.Equal(L.Onboarding_LanguagesTipPrefix + OnboardingWindow.SearchShortcut() + L.Onboarding_LanguagesTipSuffix, astuce);
        // Les touches restent en semi-gras, la lettre réglée comprise.
        Assert.Contains(runs, r => r.Strong && r.Text == attendu[^1..]);
    }

    // ── W6 : un refus d'activation laisse une issue ─────────────────────────

    [Fact]
    public void Accueil_ActivationRefusée_SuivantApparaît()
    {
        using var isolation = new BancCapture.Isolation();
        var window = OpenOnboarding();
        try
        {
            window.ActivationRequested = () => false;
            IntPtr next = BancCapture.Field<IntPtr>(window, "_hWndBtnNext");
            Assert.False(StyleVisible(next), "« Suivant » ne devrait pas être là avant le refus (état A)");

            Assert.False((bool)BancCapture.Call(window, "RequestActivation")!);

            Assert.True(BancCapture.Field<bool>(window, "_activationRefused"));
            Assert.True(StyleVisible(next), "« Suivant » reste caché après un refus");
        }
        finally { BancCapture.Teardown(window); }
    }

    // ── W16 / W1 : les cases de l'étape 3 s'écrivent au clic ────────────────

    private static void GoToLastStep(OnboardingWindow window)
    {
        while (BancCapture.Field<int>(window, "_currentStep") < OnboardingStep.Last)
            BancCapture.Call(window, "GoToNextStep");
    }

    [Fact]
    public void Accueil_CaseDémarrage_ÉcriteAuClic_SiActivationAcceptée()
    {
        using var isolation = new BancCapture.Isolation();
        bool registered = false;
        bool? asked = null;
        OnboardingWindow.AutoStartIsRegistered = () => registered;
        OnboardingWindow.AutoStartSetter = on => { asked = on; registered = on; return true; };
        var window = OpenOnboarding();
        try
        {
            BancCapture.SetField(window, "_activationAccepted", true);
            GoToLastStep(window);
            IntPtr box = BancCapture.Field<IntPtr>(window, "_hWndChkAutoStart");
            bool shown = BancCapture.Field<bool>(window, "_autoStartChecked");

            BancCapture.Call(window, "ToggleCheckBox", box);

            Assert.Equal(!shown, asked);
            Assert.Equal(!shown, BancCapture.Field<bool>(window, "_autoStartShownChecked"));
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void Accueil_CaseDémarrage_SansAccord_RienNEstÉcrit()
    {
        using var isolation = new BancCapture.Isolation();
        int calls = 0;
        OnboardingWindow.AutoStartIsRegistered = () => false;
        OnboardingWindow.AutoStartSetter = _ => { calls++; return true; };
        var window = OpenOnboarding();
        try
        {
            BancCapture.SetField(window, "_activationAccepted", false);
            GoToLastStep(window);
            BancCapture.Call(window, "ToggleCheckBox", BancCapture.Field<IntPtr>(window, "_hWndChkAutoStart"));
            Assert.Equal(0, calls);
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void Accueil_CaseDémarrage_ÉchecDitEtRendLaCaseÀLÉtatRéel()
    {
        using var isolation = new BancCapture.Isolation();
        int boxes = 0;
        OnboardingWindow.AutoStartIsRegistered = () => false;
        OnboardingWindow.AutoStartSetter = _ => false;
        OnboardingWindow.ShowMessageBox = (_, _, _, _) => { boxes++; return 1; };
        var window = OpenOnboarding();
        try
        {
            BancCapture.SetField(window, "_activationAccepted", true);
            GoToLastStep(window);
            BancCapture.Call(window, "ToggleCheckBox", BancCapture.Field<IntPtr>(window, "_hWndChkAutoStart"));
            Assert.Equal(1, boxes);
            Assert.False(BancCapture.Field<bool>(window, "_autoStartChecked"));
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void Accueil_CaseNePlusAfficher_ÉcriteAuClic()
    {
        using var isolation = new BancCapture.Isolation();
        OnboardingWindow.AutoStartIsRegistered = () => false;
        OnboardingWindow.AutoStartSetter = _ => true;
        var window = OpenOnboarding();
        try
        {
            BancCapture.SetField(window, "_activationAccepted", true);
            GoToLastStep(window);
            IntPtr box = BancCapture.Field<IntPtr>(window, "_hWndChkDontShow");

            BancCapture.Call(window, "ToggleCheckBox", box);
            Assert.False(ConfigManager.ShowOnboardingAtStartup);
            BancCapture.Call(window, "ToggleCheckBox", box);
            Assert.True(ConfigManager.ShowOnboardingAtStartup);
        }
        finally { BancCapture.Teardown(window); }
    }

    // ── E28 : Échap dans le champ d'essai vide le texte avant de fermer ─────

    [Fact]
    public void Accueil_ÉchapDansLeChampDEssai_VideLeTexteAvantDeFermer()
    {
        using var isolation = new BancCapture.Isolation();
        var window = OpenOnboarding();
        try
        {
            while (BancCapture.Field<int>(window, "_currentStep") < OnboardingStep.Languages)
                BancCapture.Call(window, "GoToNextStep");
            IntPtr trial = BancCapture.Field<IntPtr>(window, "_hWndTrial");
            Win32.SetWindowTextW(trial, "ñ");
            Win32.SetFocus(trial);
            Assert.Equal(trial, Win32.GetFocus()); // sans focus réel, ce test ne prouverait rien

            BancCapture.Call(window, "OnEscape");
            Assert.Equal("", TextOf(trial));
            Assert.True(BancCapture.Field<bool>(window, "_visible"), "le premier Échap ne devait que vider le champ");

            BancCapture.Call(window, "OnEscape");
            Assert.False(BancCapture.Field<bool>(window, "_visible"));
        }
        finally { BancCapture.Teardown(window); }
    }

    // ── W24 : plus de place réservée à Discord quand le lien est éteint ─────

    [Fact]
    public void Accueil_GrilleDeLiens_QuatreLignesAvecDiscord_TroisSans()
    {
        using (AppChannel.OverrideForTests(DistributionChannel.Store))
            Assert.Equal(4, OnboardingWindow.ResourceLinkRows);
        using (AppChannel.OverrideForTests(DistributionChannel.Amcf))
            Assert.Equal(3, OnboardingWindow.ResourceLinkRows);
    }

    [Fact]
    public void Paramètres_AideAccueil_DitQueLaCaseNAPlusDEffetQuandLesExercicesSontFaits()
    {
        using var isolation = new BancCapture.Isolation();
        L.Language = "fr";
        Assert.Equal(L.SettingsClarity_HintOnboardingPending, L.SettingsClarity_HintOnboarding);
        for (int i = 1; i <= TutorialSteps.DoneThreshold; i++)
            ConfigManager.SetLearningMaxStepCompleted(i);
        Assert.Equal(L.SettingsClarity_HintOnboardingDone, L.SettingsClarity_HintOnboarding);
    }

    // ── W8 : À propos et Statistiques changent de langue ouvertes ───────────

    [Fact]
    public void APropos_LangueChangéePendantLOuverture_LesTextesSuivent()
    {
        using var isolation = new BancCapture.Isolation();
        L.Language = "fr";
        var window = new AboutWindow();
        try
        {
            IntPtr close = BancCapture.Field<IntPtr>(window, "_hWndBtnClose");
            Assert.Equal(L.About_Close, TextOf(close));

            L.Language = "en"; // comme les Paramètres, avant SetAppLanguage
            ConfigManager.SetAppLanguage("en");

            Assert.Equal("en", window.UiLanguage);
            Assert.Equal(L.About_Close, TextOf(close));
            Assert.Equal(L.About_LinkSite, TextOf(BancCapture.Field<IntPtr>(window, "_hWndLinkSite")));
            Assert.Equal(L.About_WindowTitle, TextOf(BancCapture.Handle(window)));
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void Statistiques_LangueChangéePendantLOuverture_LesTextesSuivent()
    {
        using var isolation = new BancCapture.Isolation();
        L.Language = "fr";
        var window = new UsageStatsWindow();
        try
        {
            IntPtr copy = BancCapture.Field<IntPtr>(window, "_hWndBtnCopy");
            Assert.Equal(L.Stats_BtnCopy, TextOf(copy));

            L.Language = "en";
            ConfigManager.SetAppLanguage("en");

            Assert.Equal("en", window.UiLanguage);
            Assert.Equal(L.Stats_BtnCopy, TextOf(copy));
            Assert.Equal(L.Common_Close, TextOf(BancCapture.Field<IntPtr>(window, "_hWndBtnClose")));
            Assert.Equal(L.Stats_WindowTitle, TextOf(BancCapture.Handle(window)));
        }
        finally { BancCapture.Teardown(window); }
    }

    // ── W11 : la copie des statistiques dit son échec ───────────────────────

    [Fact]
    public void Statistiques_CopieEchouée_LeDit_EtLaCopieRéussieLeDitAussi()
    {
        using var isolation = new BancCapture.Isolation();
        var window = new UsageStatsWindow();
        try
        {
            window.ClipboardWriter = (_, _) => false;
            BancCapture.Call(window, "OnCopyStats");
            Assert.True(BancCapture.Field<bool>(window, "_copyFailed"));
            Assert.False(BancCapture.Field<bool>(window, "_showCopiedFeedback"));

            window.ClipboardWriter = (_, _) => true;
            BancCapture.Call(window, "OnCopyStats");
            Assert.False(BancCapture.Field<bool>(window, "_copyFailed"));
            Assert.True(BancCapture.Field<bool>(window, "_showCopiedFeedback"));
        }
        finally { BancCapture.Teardown(window); }
    }

    // ── E29 : Ctrl+C copie les statistiques ─────────────────────────────────

    [Theory]
    [InlineData(0x43, true, false, true)]    // Ctrl+C
    [InlineData(0x43, false, false, false)]  // C seul
    [InlineData(0x43, true, true, false)]    // Alt Gr + C (Ctrl + Alt) : un caractère, pas une copie
    [InlineData(0x56, true, false, false)]   // Ctrl+V
    public void Statistiques_CtrlC_EstUneCopie(int vk, bool ctrl, bool alt, bool attendu)
    {
        Assert.Equal(attendu, UsageStatsWindow.IsCopyChord(vk, ctrl, alt));
    }
}
