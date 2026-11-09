using System.Runtime.InteropServices;
using System.Text;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit UX du 09/11 (W1 à W24, E25 à E30), lot fenêtres : chaque correctif a son verrou. Les fenêtres
/// sont de vraies fenêtres Win32, isolées (config, stats, politique, canal) et invisibles par
/// <see cref="BancCapture.Isolation"/>. Aucune vraie boîte de message, aucun vrai démarrage Windows :
/// les points d'appui <c>ShowMessageBox</c> et <c>AutoStartSetter</c> sont remplacés puis rendus.
/// </summary>
public sealed class Audit0911FenetresTests : IDisposable
{
    private const uint WM_COMMAND = 0x0111;
    private const uint WM_KEYDOWN = 0x0100;
    private const int EN_CHANGE = 0x0300;
    private const int EN_KILLFOCUS = 0x0200;
    private const int VK_ESCAPE = 0x1B, VK_BACK = 0x08, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3,
        VK_LMENU = 0xA4, VK_RMENU = 0xA5, VK_SHIFT = 0x10;

    private readonly Func<int, bool> _keyHeldOrigin = SettingsWindow.KeyHeld;
    private readonly Func<bool, bool> _autoStartSetterOrigin = SettingsWindow.AutoStartSetter;
    private readonly Func<bool> _autoStartIsRegisteredOrigin = SettingsWindow.AutoStartIsRegistered;
    private readonly Func<IntPtr, string, string, uint, int> _settingsBoxOrigin = SettingsWindow.ShowMessageBox;
    private readonly Func<IntPtr, string, string, uint, int> _layersBoxOrigin = MaintainableLayersWindow.ShowMessageBox;
    private readonly string _languageOrigin = L.Language;

    public void Dispose()
    {
        SettingsWindow.KeyHeld = _keyHeldOrigin;
        SettingsWindow.AutoStartSetter = _autoStartSetterOrigin;
        SettingsWindow.AutoStartIsRegistered = _autoStartIsRegisteredOrigin;
        SettingsWindow.ShowMessageBox = _settingsBoxOrigin;
        MaintainableLayersWindow.ShowMessageBox = _layersBoxOrigin;
        L.Language = _languageOrigin;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLongW(IntPtr hWnd, int nIndex);

    private static void Click(IntPtr parent, IntPtr control, int id)
    {
        // BN_CLICKED = 0 dans le mot haut ; lParam = le contrôle, comme Windows l'envoie.
        Win32.SendMessageW(parent, WM_COMMAND, (IntPtr)id, control);
    }

    private static void SetCheck(IntPtr box, bool on) =>
        Win32.SendMessageW(box, Win32.BM_SETCHECK, on ? (IntPtr)Win32.BST_CHECKED : IntPtr.Zero, IntPtr.Zero);

    private static string TextOf(IntPtr hwnd)
    {
        var buffer = new StringBuilder(Math.Max(16, Win32.GetWindowTextLengthW(hwnd) + 1));
        Win32.GetWindowTextW(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static void Key(IntPtr edit, int vk) =>
        Win32.SendMessageW(edit, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);

    // ── W5 / E25 : décision pure du champ de raccourci ──────────────────────

    [Theory]
    [InlineData(0x73u, false, true, false, "PassToSystem")]   // Alt+F4
    [InlineData(0x41u, false, true, true, "PassToSystem")]    // Alt+Maj+A
    [InlineData(0x41u, true, false, false, "RefuseCtrl")]     // Ctrl+A
    [InlineData(0x41u, true, false, true, "Apply")]           // Ctrl+Maj+A
    [InlineData(0x08u, false, false, false, "RestoreDefault")] // Retour arrière
    [InlineData(0x2Eu, false, false, false, "RestoreDefault")] // Suppr
    [InlineData(0x45u, false, false, false, "Apply")]         // E
    public void Raccourci_LaTouchePrendLaBonneDécision(uint vk, bool ctrl, bool alt, bool shift,
        string attendu)
    {
        Assert.Equal(attendu, SettingsWindow.ClassifyShortcutKey(vk, ctrl, alt, shift).ToString());
    }

    [Theory]
    [InlineData(false, false, false, false, false, false)]
    [InlineData(true, false, false, false, true, false)]    // Ctrl gauche seul
    [InlineData(false, true, false, false, true, false)]    // Ctrl droit seul
    [InlineData(false, false, true, false, false, true)]    // Alt gauche
    [InlineData(false, false, false, true, false, true)]    // Alt droit sans Ctrl gauche : un vrai Alt
    [InlineData(true, false, false, true, false, false)]    // Alt Gr = Ctrl gauche + Alt droit : ni Ctrl ni Alt
    [InlineData(true, true, false, true, true, false)]      // Alt Gr + Ctrl droit : Ctrl réel
    public void Raccourci_AltGrNEstNiCtrlNiAlt(bool lCtrl, bool rCtrl, bool lAlt, bool rAlt, bool ctrl, bool alt)
    {
        Assert.Equal((ctrl, alt), SettingsWindow.EffectiveModifiers(lCtrl, rCtrl, lAlt, rAlt));
    }

    [Fact]
    public void Raccourci_AltGrPlusLettre_EstAccepté()
    {
        var (ctrl, alt) = SettingsWindow.EffectiveModifiers(true, false, false, true);
        Assert.Equal(SettingsWindow.ShortcutKeyAction.Apply, SettingsWindow.ClassifyShortcutKey(0x45, ctrl, alt, false));
    }

    private static SettingsWindow OpenSettings(out IntPtr hwnd)
    {
        var window = new SettingsWindow();
        window.Show();
        hwnd = BancCapture.Handle(window);
        return window;
    }

    [Fact]
    public void Raccourci_CtrlSeul_EstRefuséAvecMessage_EtLaToucheNChangePas()
    {
        using var isolation = new BancCapture.Isolation();
        var window = OpenSettings(out _);
        try
        {
            IntPtr edit = BancCapture.Field<IntPtr>(window, "_hWndEditKeyboard");
            uint before = ConfigManager.ShortcutVirtualKeyboardVk;
            SettingsWindow.KeyHeld = vk => vk == VK_LCONTROL;
            Key(edit, 0x45);
            Assert.Equal(before, ConfigManager.ShortcutVirtualKeyboardVk);
            Assert.Equal(L.SettingsClarity_ShortcutCtrlOnly, BancCapture.Field<string?>(window, "_validationMessage"));
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void Raccourci_RetourArrière_RétablitLaToucheDOrigine()
    {
        using var isolation = new BancCapture.Isolation();
        var window = OpenSettings(out _);
        try
        {
            IntPtr edit = BancCapture.Field<IntPtr>(window, "_hWndEditKeyboard");
            SettingsWindow.KeyHeld = _ => false;
            Key(edit, 0x45);
            Assert.Equal(0x45u, ConfigManager.ShortcutVirtualKeyboardVk);
            Key(edit, VK_BACK);
            Assert.Equal(0x51u, ConfigManager.ShortcutVirtualKeyboardVk);
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void Raccourci_AltGrPlusLettre_EstEnregistré()
    {
        using var isolation = new BancCapture.Isolation();
        var window = OpenSettings(out _);
        try
        {
            IntPtr edit = BancCapture.Field<IntPtr>(window, "_hWndEditKeyboard");
            SettingsWindow.KeyHeld = vk => vk is VK_LCONTROL or VK_RMENU; // ce que Windows signale pour Alt Gr
            Key(edit, 0x45);
            Assert.Equal(0x45u, ConfigManager.ShortcutVirtualKeyboardVk);
        }
        finally { BancCapture.Teardown(window); }
    }

    // ── W18 : Échap après une capture réussie quitte la case, la fenêtre reste ─

    [Fact]
    public void Raccourci_ÉchapAprèsCapture_QuitteLaCaseSansFermerLaFenêtre()
    {
        using var isolation = new BancCapture.Isolation();
        var window = OpenSettings(out _);
        try
        {
            IntPtr edit = BancCapture.Field<IntPtr>(window, "_hWndEditKeyboard");
            SettingsWindow.KeyHeld = _ => false;
            Key(edit, 0x45);
            Assert.True(BancCapture.Field<bool>(window, "_captureConfirmed"));

            Key(edit, VK_ESCAPE);

            Assert.True(BancCapture.Field<bool>(window, "_visible"), "Échap a fermé la fenêtre au lieu de quitter la case");
            Assert.False(BancCapture.Field<bool>(window, "_captureConfirmed"));
            Assert.Equal(0x45u, ConfigManager.ShortcutVirtualKeyboardVk);

            // Un second Échap, la capture n'étant plus en cours, ferme.
            Key(edit, VK_ESCAPE);
            Assert.False(BancCapture.Field<bool>(window, "_visible"));
        }
        finally { BancCapture.Teardown(window); }
    }

    // ── W1 : chaque case des Paramètres s'écrit au clic ────────────────────

    [Fact]
    public void Paramètres_NotificationsEtAccueil_ÉcritsAuClic_SansFermer()
    {
        using var isolation = new BancCapture.Isolation();
        var window = OpenSettings(out IntPtr hwnd);
        try
        {
            IntPtr notif = BancCapture.Field<IntPtr>(window, "_hWndChkNotifications");
            IntPtr onboarding = BancCapture.Field<IntPtr>(window, "_hWndChkOnboarding");
            bool notifBefore = ConfigManager.NotificationsEnabled;
            bool onboardingBefore = ConfigManager.ShowOnboardingAtStartup;

            SetCheck(notif, !notifBefore);
            Click(hwnd, notif, 3104);
            SetCheck(onboarding, !onboardingBefore);
            Click(hwnd, onboarding, 3105);

            Assert.Equal(!notifBefore, ConfigManager.NotificationsEnabled);
            Assert.Equal(!onboardingBefore, ConfigManager.ShowOnboardingAtStartup);
            Assert.True(BancCapture.Field<bool>(window, "_visible"), "le clic ne doit pas fermer la fenêtre");
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void Paramètres_DémarrageAvecWindows_AppliquéAuClic()
    {
        using var isolation = new BancCapture.Isolation();
        bool registered = false;
        bool? asked = null;
        SettingsWindow.AutoStartIsRegistered = () => registered;
        SettingsWindow.AutoStartSetter = on => { asked = on; registered = on; return true; };
        var window = OpenSettings(out IntPtr hwnd);
        try
        {
            IntPtr box = BancCapture.Field<IntPtr>(window, "_hWndChkAutoStart");
            SetCheck(box, true);
            Click(hwnd, box, 3103);
            Assert.True(asked);
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void Paramètres_DémarrageAvecWindows_ÉchecRendLaCaseÀLÉtatRéel_EtLeDit()
    {
        using var isolation = new BancCapture.Isolation();
        SettingsWindow.AutoStartIsRegistered = () => false;
        SettingsWindow.AutoStartSetter = _ => false;
        int boxes = 0;
        SettingsWindow.ShowMessageBox = (_, _, _, _) => { boxes++; return 1; };
        var window = OpenSettings(out IntPtr hwnd);
        try
        {
            IntPtr box = BancCapture.Field<IntPtr>(window, "_hWndChkAutoStart");
            SetCheck(box, true);
            Click(hwnd, box, 3103);
            Assert.Equal(1, boxes);
            Assert.Equal(IntPtr.Zero, Win32.SendMessageW(box, Win32.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero));
        }
        finally { BancCapture.Teardown(window); }
    }

    // ── W19 : ajouter une application déjà listée dit le changement de mode ─

    [Fact]
    public void Compat_AjouterUneApplicationDéjàListée_DitLeModeAvantEtAprès()
    {
        using var isolation = new BancCapture.Isolation();
        var window = OpenSettings(out _);
        try
        {
            ConfigManager.SetCompatibilityOverride("jeu.exe", "forceOn");
            BancCapture.Call(window, "AddCompatApplication", "jeu.exe");

            Assert.Equal("forceOff", ConfigManager.GetCompatibilityOverride("jeu.exe"));
            string message = BancCapture.Field<string?>(window, "_validationMessage") ?? "";
            Assert.Contains("jeu.exe", message);
            Assert.NotEqual(L.Settings_CompatAdded("jeu.exe"), message);
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void Compat_AjouterUneNouvelleApplication_GardeLeMessageSimple()
    {
        using var isolation = new BancCapture.Isolation();
        var window = OpenSettings(out _);
        try
        {
            BancCapture.Call(window, "AddCompatApplication", "autre.exe");
            Assert.Equal(L.Settings_CompatAdded("autre.exe"), BancCapture.Field<string?>(window, "_validationMessage"));
        }
        finally { BancCapture.Teardown(window); }
    }

    // ── Couches : W1, W4, W7, W20, E28 ──────────────────────────────────────

    private static MaintainableLayersWindow OpenLayers(out IntPtr hwnd)
    {
        var window = new MaintainableLayersWindow();
        hwnd = BancCapture.Handle(window);
        return window;
    }

    [Fact]
    public void Couches_UneCaseCochée_ÉcriteAuClic_SansFermerLaFenêtre()
    {
        using var isolation = new BancCapture.Isolation();
        MaintainableLayersWindow.ShowMessageBox = (_, _, _, _) => 1;
        using var window = OpenLayers(out IntPtr hwnd);
        IntPtr greek = BancCapture.Field<IntPtr>(window, "_hGreek");
        IntPtr visual = BancCapture.Field<IntPtr>(window, "_hVisual");
        bool greekBefore = ConfigManager.MaintainableGreekEnabled;
        bool visualBefore = ConfigManager.MaintainableVisualFeedbackEnabled;

        SetCheck(greek, !greekBefore);
        Click(hwnd, greek, 5202);
        SetCheck(visual, !visualBefore);
        Click(hwnd, visual, 5205);

        Assert.Equal(!greekBefore, ConfigManager.MaintainableGreekEnabled);
        Assert.Equal(!visualBefore, ConfigManager.MaintainableVisualFeedbackEnabled);
    }

    [Fact]
    public void Couches_CaseMaîtreCochée_ActiveAuClic_EtAnnonceUneSeuleFois()
    {
        using var isolation = new BancCapture.Isolation();
        int boxes = 0;
        MaintainableLayersWindow.ShowMessageBox = (_, _, _, _) => { boxes++; return 1; };
        using var window = OpenLayers(out IntPtr hwnd);
        IntPtr master = BancCapture.Field<IntPtr>(window, "_hMaster");

        SetCheck(master, true);
        Click(hwnd, master, 5201);
        Assert.True(ConfigManager.MaintainableLayersEnabled);
        Assert.Equal(1, boxes);

        // Un second clic sur une case déjà active ne ré-annonce pas.
        Click(hwnd, master, 5201);
        Assert.Equal(1, boxes);
    }

    [Fact]
    public void Couches_RefreshFromConfig_RelitLesCases()
    {
        using var isolation = new BancCapture.Isolation();
        using var window = OpenLayers(out _);
        IntPtr greek = BancCapture.Field<IntPtr>(window, "_hGreek");
        ConfigManager.SetMaintainableGreekEnabled(false);

        window.RefreshFromConfig();

        Assert.Equal(IntPtr.Zero, Win32.SendMessageW(greek, Win32.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero));
    }

    [Fact]
    public void Couches_LangueChangéePendantLOuverture_LesTextesSuivent()
    {
        using var isolation = new BancCapture.Isolation();
        using var window = OpenLayers(out _);
        IntPtr master = BancCapture.Field<IntPtr>(window, "_hMaster");
        L.Language = "fr";
        ConfigManager.SetAppLanguage("en");
        L.Language = "en";
        // L'abonnement relit la langue ; on la rejoue pour ne pas dépendre de l'ordre des abonnés.
        BancCapture.Call(window, "OnLanguageChanged");
        Assert.Equal(L.Layers_MasterCheckbox, TextOf(master));
    }

    [Theory]
    [InlineData("500", 500, false)]
    [InlineData("150", 150, false)]
    [InlineData("1000", 1000, false)]
    [InlineData("149", 150, true)]
    [InlineData("50", 150, true)]
    [InlineData("1001", 1000, true)]
    [InlineData("99999", 1000, true)]
    [InlineData("", 500, false)]
    [InlineData("abc", 500, false)]
    public void Couches_Délai_RamenéDansLesBornes_EtDit(string texte, int attendu, bool message)
    {
        int value = MaintainableLayersWindow.ClampDelay(texte, out string? adjusted);
        Assert.Equal(attendu, value);
        Assert.Equal(message, adjusted != null);
    }

    [Fact]
    public void Couches_Délai_LeChampRefuseLesLettres_EtLaValeurValableEstÉcriteTouteDeSuite()
    {
        using var isolation = new BancCapture.Isolation();
        using var window = OpenLayers(out IntPtr hwnd);
        IntPtr delay = BancCapture.Field<IntPtr>(window, "_hDelay");

        Win32.SetWindowTextW(delay, "3a0b0");
        Assert.Equal("300", TextOf(delay));
        Assert.Equal(300, ConfigManager.MaintainableDoubleTapMilliseconds);
    }

    [Fact]
    public void Couches_Délai_HorsBornes_RamenéÀLaPerteDuFocus_AvecUneNote()
    {
        using var isolation = new BancCapture.Isolation();
        using var window = OpenLayers(out IntPtr hwnd);
        IntPtr delay = BancCapture.Field<IntPtr>(window, "_hDelay");
        IntPtr note = BancCapture.Field<IntPtr>(window, "_hDelayNote");

        Win32.SetWindowTextW(delay, "20");
        Win32.SendMessageW(hwnd, WM_COMMAND, (IntPtr)(5206 | (EN_KILLFOCUS << 16)), delay);

        Assert.Equal("150", TextOf(delay));
        Assert.Equal(L.Layers_DelayRaised(150), TextOf(note));
        Assert.Equal(150, ConfigManager.MaintainableDoubleTapMilliseconds);
    }

    // ── E28 : champ numérique ───────────────────────────────────────────────

    [Theory]
    [InlineData("123", "123")]
    [InlineData("1a2b3", "123")]
    [InlineData("-5", "5")]
    [InlineData("", "")]
    [InlineData("abc", "")]
    public void ChampNumérique_NeGardeQueLesChiffres(string entrée, string attendu)
    {
        Assert.Equal(attendu, NumericField.Digits(entrée));
    }

    // ── Tray : W3, W12, W13, W14, W15 ───────────────────────────────────────

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]    // un dialogue de durée est déjà ouvert
    [InlineData(false, false, false)]  // programme désactivé
    [InlineData(false, true, false)]
    public void Pause_PeutDémarrer(bool enabled, bool dialogOpen, bool attendu)
    {
        Assert.Equal(attendu, TrayApplication.CanStartPause(enabled, dialogOpen));
    }

    [Theory]
    [InlineData(3, 3, true, true)]
    [InlineData(3, 4, true, false)]   // une autre pause a démarré entre-temps
    [InlineData(3, 3, false, false)]  // désactivé entre-temps
    public void Pause_RésultatDuDialogue_NEcrasePasUneAutrePause(int ouverture, int maintenant, bool enabled, bool attendu)
    {
        Assert.Equal(attendu, TrayApplication.ShouldApplyDialogPause(ouverture, maintenant, enabled));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]  // « Notifications » décochée coupe la mini-fenêtre
    [InlineData(true, true, false)]    // anti-cheat : jamais d'overlay
    [InlineData(false, true, false)]
    public void MiniFenêtreDeBascule_SuitLesNotifications(bool notifications, bool sécurité, bool attendu)
    {
        Assert.Equal(attendu, TrayApplication.ShouldShowToggleOverlay(notifications, sécurité));
    }

    [Theory]
    [InlineData(true, false, "UserOverrideSuspension")]
    [InlineData(true, true, "UserOverrideSuspension")]
    [InlineData(false, true, "Paused")]
    [InlineData(false, false, "Disabled")]
    public void OutilIndisponible_DitPourquoi(bool override_, bool paused, string attendu)
    {
        Assert.Equal(attendu, TrayApplication.WhyToolUnavailable(override_, paused).ToString());
    }

    [Fact]
    public void MenuActivation_EnPause_DitQueDésactiverMetFinÀLaPause()
    {
        L.Language = "fr";
        Assert.Equal(L.Tray_MenuEnable, TrayApplication.ToggleMenuLabel(false, false));
        Assert.Equal(L.Tray_MenuEnable, TrayApplication.ToggleMenuLabel(false, true));
        Assert.Equal(L.Tray_MenuDisable, TrayApplication.ToggleMenuLabel(true, false));
        Assert.Equal(L.TrayClarity_MenuDisableEndsPause, TrayApplication.ToggleMenuLabel(true, true));
        Assert.NotEqual(L.Tray_MenuDisable, L.TrayClarity_MenuDisableEndsPause);
    }

    // ── W3 : l'accueil reste utilisable en pause, seul le champ d'essai s'arrête ─

    [Fact]
    public void Accueil_EnPause_LeChampDEssaiEstEnLectureSeule_PasLaFenêtre()
    {
        using var isolation = new BancCapture.Isolation();
        var window = new OnboardingWindow();
        try
        {
            window.Show();
            IntPtr trial = BancCapture.Field<IntPtr>(window, "_hWndTrial");
            const int ES_READONLY = 0x0800, GWL_STYLE = -16;

            window.SetInputPaused(true);
            Assert.True(window.TrialReadOnly);
            Assert.NotEqual(0, GetWindowLongW(trial, GWL_STYLE) & ES_READONLY);

            window.SetInputPaused(false);
            Assert.False(window.TrialReadOnly);
            Assert.Equal(0, GetWindowLongW(trial, GWL_STYLE) & ES_READONLY);
        }
        finally { BancCapture.Teardown(window); }
    }
}
