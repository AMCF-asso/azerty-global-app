using System;
using System.Reflection;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Piste papier de la liste Paramètres › Applications (A16, lot 3a), régression du 21/10 :
/// la sous-classe de la liste relisait LB_GETTOPINDEX par SendMessage, donc à travers
/// elle-même. Chaque relecture en appelait une autre, jusqu'à ce que Windows coupe la
/// récursion (31 niveaux, mesuré) et rende 0 au plus profond. Liste défilée (première ligne
/// visible 1), ce 0 différait de la ligne peinte : chaque WM_PAINT invalidait la piste, et la
/// fenêtre se repeignait sans fin (477 WM_PAINT en 3 s, CPU en boucle).
/// </summary>
public sealed class CompatListScrollTests
{
    [DllImport("user32.dll")]
    private static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetUpdateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    private const uint LB_GETTOPINDEX = 0x018E;

    [Fact]
    public void ListeDéfilée_LaPeintureNeRéinvalidePasLaFenêtre()
    {
        using var isolation = new BancCapture.Isolation();
        isolation.SetLanguage("fr");
        // Quatre entrées pour trois lignes visibles : valorant.exe, la dernière, fait défiler.
        ConfigManager.SetCompatibilityOverride("Code.exe", "forceOff");
        ConfigManager.SetCompatibilityOverride("eldenring.exe", "forceOn");
        ConfigManager.SetCompatibilityOverride("mstsc.exe", "forceOff");
        ConfigManager.SetCompatibilityOverride("valorant.exe", "forceOff");
        var window = new SettingsWindow();
        try
        {
            window.Show();
            IntPtr hwnd = BancCapture.Handle(window);
            var tabType = typeof(SettingsWindow).GetNestedType("SettingsTab", BindingFlags.NonPublic)!;
            Win32.SendMessageW(BancCapture.Field<IntPtr>(window, "_hWndTabStrip"), Win32.TCM_SETCURSEL, (IntPtr)1, IntPtr.Zero);
            BancCapture.Call(window, "SetActiveTab", Enum.ToObject(tabType, 1));
            BancCapture.Call(window, "RefreshCompatList", "valorant.exe");
            IntPtr list = BancCapture.Field<IntPtr>(window, "_hWndCompatList");
            Assert.Equal(1, (int)Win32.SendMessageW(list, LB_GETTOPINDEX, IntPtr.Zero, IntPtr.Zero));

            // Pas de pompe : avec la régression, la file ne se vide jamais. UpdateWindow peint
            // une fois, de façon synchrone, ce qui reste à peindre ; après quelques passes, plus
            // rien ne doit l'être.
            for (int i = 0; i < 5; i++)
                UpdateWindow(hwnd);
            Assert.False(GetUpdateRect(hwnd, IntPtr.Zero, false),
                "la fenêtre reste à repeindre après cinq peintures : la piste se ré-invalide");
            Assert.Equal(1, BancCapture.Field<int>(window, "_compatTopDrawn"));
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }
}
