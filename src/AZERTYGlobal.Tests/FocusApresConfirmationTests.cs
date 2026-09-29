using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// R45 (recette Sandbox du 2026-09-29) : après la confirmation de « Valeurs par défaut », le
/// focus ne revenait pas sur le bouton. Les Paramètres ne sont pas une boîte de dialogue : à la
/// fermeture de la MessageBox, Windows réactive la fenêtre et DefWindowProc met le focus sur la
/// fenêtre elle-même, où rien n'est annoncé et d'où Espace ne presse plus rien.
///
/// Aucune vraie boîte ici : la confirmation passe par <c>SettingsWindow.ShowMessageBox</c>,
/// que le test remplace par une réponse qui, comme la vraie boîte, emporte le focus. La
/// fenêtre n'est jamais affichée.
/// </summary>
public class FocusApresConfirmationTests : IDisposable
{
    private const int IDC_LINK_RESET = 3107;
    private const int IDYES = 6;
    private const int IDNO = 7;
    private const uint MB_YESNO = 0x4;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AZGFOCUS_" + Guid.NewGuid().ToString("N"));
    private readonly Func<IntPtr, string, string, uint, int> _boîteDOrigine = SettingsWindow.ShowMessageBox;

    public FocusApresConfirmationTests()
    {
        Directory.CreateDirectory(_dir);
        string config = Path.Combine(_dir, "config.json");
        File.WriteAllText(config, "{\"activationConsent\":true,\"appLanguage\":\"fr\"}");
        ConfigManager.OverrideConfigPathForTests(config);
    }

    public void Dispose()
    {
        SettingsWindow.ShowMessageBox = _boîteDOrigine;
        for (int essai = 0; essai < 20; essai++)
        {
            try { Directory.Delete(_dir, true); return; }
            catch (IOException) { Thread.Sleep(25); }
            catch (UnauthorizedAccessException) { Thread.Sleep(25); }
        }
    }

    [Theory]
    [InlineData(IDNO)]
    [InlineData(IDYES)]
    public void ValeursParDéfaut_AprèsLaConfirmation_LeFocusRevientSurLeBouton(int réponse)
    {
        using var fenêtre = new SettingsWindow();
        IntPtr hwnd = BancCapture.Handle(fenêtre);
        IntPtr bouton = BancCapture.Field<IntPtr>(fenêtre, "_hWndLinkReset");
        Win32.SetFocus(bouton);
        Assert.Equal(bouton, Win32.GetFocus());

        int confirmations = 0;
        SettingsWindow.ShowMessageBox = (owner, text, caption, type) =>
        {
            confirmations++;
            Assert.Equal(hwnd, owner);
            Assert.Equal(MB_YESNO, type & 0xF);
            // Ce que fait la vraie boîte : elle prend le focus, et ne le rend pas au bouton.
            Win32.SetFocus(IntPtr.Zero);
            return réponse;
        };

        // Même message qu'Entrée ou Espace sur le bouton : WM_COMMAND (BN_CLICKED, 3107).
        Win32.SendMessageW(hwnd, Win32.WM_COMMAND, (IntPtr)IDC_LINK_RESET, bouton);

        Assert.Equal(1, confirmations);
        Assert.Equal(bouton, Win32.GetFocus());
    }
}
