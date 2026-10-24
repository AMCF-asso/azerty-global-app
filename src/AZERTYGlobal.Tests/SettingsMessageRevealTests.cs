using System;
using System.Reflection;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// R64c (banc du 22/10, écran 1024×768 à 125 %) : le refus « Compatibilité jeu » sur le Bureau à
/// distance perdait sa dernière ligne, « Windows prend le relais. ». La fenêtre, plafonnée à la
/// zone de travail, laissait le message sous son bord, et rien ne défilait jusqu'à lui.
///
/// Le plafond ne se voit que sur un écran court : la zone de travail est donc réduite par
/// <see cref="SettingsWindow.WorkAreaHeight"/>, pour que le témoin morde sur tout poste.
/// </summary>
public sealed class SettingsMessageRevealTests : IDisposable
{
    private readonly Func<Win32.RECT, int> _zoneDOrigine = SettingsWindow.WorkAreaHeight;

    public void Dispose() => SettingsWindow.WorkAreaHeight = _zoneDOrigine;

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void RefusDeCompatibilité_FenêtrePlafonnée_LeMessageEntierEstVisible(string langue)
    {
        using var isolation = new BancCapture.Isolation();
        isolation.SetLanguage(langue);
        ConfigManager.SetCompatibilityOverride("mstsc.exe", "forceOff");
        var window = new SettingsWindow();
        try
        {
            window.Show();
            IntPtr hwnd = BancCapture.Handle(window);
            // Zone de travail de 430 px logiques : l'onglet Applications n'y tient pas.
            float échelle = BancCapture.Field<float>(window, "_dpiScale");
            SettingsWindow.WorkAreaHeight = _ => (int)(430 * échelle);

            var tabType = typeof(SettingsWindow).GetNestedType("SettingsTab", BindingFlags.NonPublic)!;
            Win32.SendMessageW(BancCapture.Field<IntPtr>(window, "_hWndTabStrip"), Win32.TCM_SETCURSEL, (IntPtr)1, IntPtr.Zero);
            BancCapture.Call(window, "SetActiveTab", Enum.ToObject(tabType, 1));
            BancCapture.Call(window, "RefreshCompatList", "mstsc.exe");
            Assert.True(BancCapture.Field<int>(window, "_contentHeight") > BancCapture.Field<int>(window, "_viewportHeight"),
                "précondition : l'onglet doit défiler, sinon le témoin ne prouve rien");

            // Le vrai chemin du bouton radio, comme le banc.
            BancCapture.Call(window, "ApplyCompatModeToSelection", "forceOn");
            Assert.True(BancCapture.Field<bool>(window, "_validationRefused"), "précondition : un refus est affiché");

            Win32.GetClientRect(hwnd, out var client);
            var message = ClientSpan(hwnd, BancCapture.Field<IntPtr>(window, "_hWndValidation"));
            Assert.True(message.Top >= 0 && message.Bottom <= client.bottom,
                $"message hors de la vue : {message.Top}-{message.Bottom} pour une zone de 0-{client.bottom}");

            // Le défilement reste minimal : le bouton qui a produit le refus reste en vue.
            var radio = ClientSpan(hwnd, BancCapture.Field<IntPtr>(window, "_hWndRadioCompatForceOn"));
            Assert.True(radio.Top >= 0, $"le bouton « Compatibilité jeu » est sorti par le haut : {radio.Top}");
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    /// <summary>Haut et bas d'un contrôle, en coordonnées clientes de la fenêtre.</summary>
    private static (int Top, int Bottom) ClientSpan(IntPtr hwnd, IntPtr control)
    {
        Win32.GetWindowRect(control, out var rect);
        var top = new Win32.POINT { x = rect.left, y = rect.top };
        var bottom = new Win32.POINT { x = rect.right, y = rect.bottom };
        Win32.ScreenToClient(hwnd, ref top);
        Win32.ScreenToClient(hwnd, ref bottom);
        return (top.y, bottom.y);
    }
}
