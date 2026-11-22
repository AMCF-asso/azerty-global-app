using TypingEngine.Windows;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Témoins V1.3.0 : modale de conflit disposition système (mémoire durable, vraie détection
/// de bascule, case « Ne plus afficher », réglage dans Paramètres) et modificateurs de
/// raccourcis configurables (capture directe, persistance, coexistence).
/// </summary>
public sealed class ConflitDispositionEtModificateursTests
{
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_SYSKEYDOWN = 0x0104;
    private const uint BM_SETCHECK = 0x00F1;
    private const uint BM_CLICK = 0x00F5;
    private const int BST_CHECKED = 1;
    private const int VK_SHIFT = 0x10;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_LMENU = 0xA4;

    [Fact]
    public void GardeConflit_DemarrageEtBasculeReelle_SansSpamSiDejaActif()
    {
        var guard = new SystemLayoutConflictGuard();

        // Au démarrage : active + avertissement actif → affiche
        Assert.True(guard.EvaluateStartup(isSystemLayoutActive: true, warningEnabled: true));

        // Ctrl+Maj+clic alors que la disposition était DÉJÀ active → pas de spam
        Assert.False(guard.EvaluateAfterSwitch(isSystemLayoutActive: true, warningEnabled: true));
        Assert.False(guard.EvaluateAfterSwitch(isSystemLayoutActive: true, warningEnabled: true));

        // L'utilisateur bascule vers une autre disposition, puis revient sur AZERTY Global → avertit
        Assert.False(guard.EvaluateAfterSwitch(isSystemLayoutActive: false, warningEnabled: true));
        Assert.True(guard.EvaluateAfterSwitch(isSystemLayoutActive: true, warningEnabled: true));

        // Si l'utilisateur a désactivé l'avertissement → jamais réaffiché, même sur vraie bascule
        Assert.False(guard.EvaluateAfterSwitch(isSystemLayoutActive: false, warningEnabled: false));
        Assert.False(guard.EvaluateAfterSwitch(isSystemLayoutActive: true, warningEnabled: false));
        Assert.False(guard.EvaluateStartup(isSystemLayoutActive: true, warningEnabled: false));
    }

    [Fact]
    public void Config_WarnSystemLayout_PersisteEtSeReactive()
    {
        using var isolation = new BancCapture.Isolation();
        Assert.True(ConfigManager.WarnSystemLayout);

        ConfigManager.SetWarnSystemLayout(false);
        Assert.False(ConfigManager.WarnSystemLayout);

        ConfigManager.SetWarnSystemLayout(true);
        Assert.True(ConfigManager.WarnSystemLayout);
    }

    [Fact]
    public void Config_ModificateursRaccourcis_PersisteEtAutoriseLettresHorsCtrlMaj()
    {
        using var isolation = new BancCapture.Isolation();
        Assert.Equal(ShortcutModifierRules.Default, ConfigManager.ShortcutVirtualKeyboardModifiers);

        // Sous Ctrl+Maj, 'T' est bloquée ; sous Alt+Maj, 'T' est autorisée
        Assert.False(ConfigManager.IsShortcutAllowed(ShortcutModifiers.Ctrl | ShortcutModifiers.Shift, 'T'));
        Assert.True(ConfigManager.IsShortcutAllowed(ShortcutModifiers.Shift | ShortcutModifiers.Alt, 'T'));

        // Persistance des nouveaux modificateurs
        ConfigManager.ShortcutVirtualKeyboardModifiers = ShortcutModifiers.Shift | ShortcutModifiers.Alt;
        ConfigManager.ShortcutVirtualKeyboardVk = 'T';
        Assert.Equal(ShortcutModifiers.Shift | ShortcutModifiers.Alt, ConfigManager.ShortcutVirtualKeyboardModifiers);
        Assert.Equal((uint)'T', ConfigManager.ShortcutVirtualKeyboardVk);

        // Un seul modificateur est ignoré
        ConfigManager.ShortcutVirtualKeyboardModifiers = ShortcutModifiers.Ctrl;
        Assert.Equal(ShortcutModifiers.Shift | ShortcutModifiers.Alt, ConfigManager.ShortcutVirtualKeyboardModifiers);
    }

    [Fact]
    public void ModaleConflit_CaseNePlusAfficher_DeclencheCallbackAuGarderEtPasAuQuitter()
    {
        using var isolation = new BancCapture.Isolation();
        bool dontShowCalled = false;
        bool kept = false;
        using var fenetre = new LayoutConflictWindow(
            isAtStartup: true,
            onQuit: () => { },
            onKeep: () => kept = true,
            onDontShowChanged: v => dontShowCalled = v);
        fenetre.Show();

        IntPtr chk = BancCapture.Field<IntPtr>(fenetre, "_hWndChkDontShow");
        IntPtr btnKeep = BancCapture.Field<IntPtr>(fenetre, "_hWndBtnKeep");
        Assert.NotEqual(IntPtr.Zero, chk);

        Win32.SendMessageW(chk, BM_SETCHECK, (IntPtr)BST_CHECKED, IntPtr.Zero);
        Win32.SendMessageW(btnKeep, BM_CLICK, IntPtr.Zero, IntPtr.Zero);

        Assert.True(kept);
        Assert.True(dontShowCalled);
    }

    [Fact]
    public void ModaleConflit_CaseDecochee_NeDesactivePasLAvertissement()
    {
        using var isolation = new BancCapture.Isolation();
        bool dontShowCalled = false;
        using var fenetre = new LayoutConflictWindow(
            isAtStartup: false,
            onQuit: () => { },
            onKeep: () => { },
            onDontShowChanged: v => dontShowCalled = v);
        fenetre.Show();

        IntPtr btnKeep = BancCapture.Field<IntPtr>(fenetre, "_hWndBtnKeep");
        Win32.SendMessageW(btnKeep, BM_CLICK, IntPtr.Zero, IntPtr.Zero);

        Assert.False(dontShowCalled);
    }

    [Fact]
    public void Parametres_CaptureDirecteAltMajX_MetAJourModificateursEtTouche()
    {
        using var isolation = new BancCapture.Isolation();
        var window = new SettingsWindow();
        window.Show();
        try
        {
            IntPtr edit = BancCapture.Field<IntPtr>(window, "_hWndEditKeyboard");
            SettingsWindow.KeyHeld = vk => vk == VK_LMENU || vk == VK_SHIFT;
            Win32.SendMessageW(edit, WM_SYSKEYDOWN, (IntPtr)'X', IntPtr.Zero);

            Assert.Equal(ShortcutModifiers.Shift | ShortcutModifiers.Alt, ConfigManager.ShortcutVirtualKeyboardModifiers);
            Assert.Equal((uint)'X', ConfigManager.ShortcutVirtualKeyboardVk);

            // Même lettre 'X' autorisée pour la recherche si les modificateurs diffèrent (Ctrl+Alt+X)
            IntPtr editSearch = BancCapture.Field<IntPtr>(window, "_hWndEditSearch");
            SettingsWindow.KeyHeld = vk => vk == VK_LCONTROL || vk == VK_LMENU;
            Win32.SendMessageW(editSearch, WM_SYSKEYDOWN, (IntPtr)'X', IntPtr.Zero);

            Assert.Equal(ShortcutModifiers.Ctrl | ShortcutModifiers.Alt, ConfigManager.ShortcutCharacterSearchModifiers);
            Assert.Equal((uint)'X', ConfigManager.ShortcutCharacterSearchVk);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void Parametres_CaseAvertissementDispositionSysteme_BasculeConfig()
    {
        using var isolation = new BancCapture.Isolation();
        var window = new SettingsWindow();
        window.Show();
        try
        {
            IntPtr chk = BancCapture.Field<IntPtr>(window, "_hWndChkSystemLayoutWarning");
            Assert.NotEqual(IntPtr.Zero, chk);
            Assert.True(ConfigManager.WarnSystemLayout);

            Win32.SendMessageW(chk, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            Assert.False(ConfigManager.WarnSystemLayout);

            Win32.SendMessageW(chk, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            Assert.True(ConfigManager.WarnSystemLayout);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }
}
