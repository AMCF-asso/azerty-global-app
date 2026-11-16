using System.Reflection;
using System.Runtime.CompilerServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit par frappe au hasard (docs/audit-frappe-hasard.md, § 4) : constat C3.1 de
/// docs/machines-a-etats.md (ME-13). Vrai <c>WndProcCallback</c> sur un tray non initialisé,
/// fenêtre-message créée sur le fil du test ; la sonde est <c>KillTimer</c>, qui rend vrai
/// si et seulement si la minuterie de simple clic (9010) existe. Le <c>WM_TIMER</c> n'arrive
/// jamais : aucun message n'est pompé, et la sonde détruit la minuterie qu'elle trouve.
/// Prémisse non prouvée ici : le shell envoie bien UP, DBLCLK, UP pour un double clic sur
/// l'icône (messages souris historiques, l'application n'appelle pas NIM_SETVERSION).
/// </summary>
public sealed class ConstatsZoneNotificationTests
{
    private const uint WmTrayIcon = 0x8000 + 1;   // WM_APP + 1, TrayApplication.cs:15
    private const uint TimerSimpleClic = 9010;   // TrayApplication.cs:525
    private static readonly IntPtr HwndMessage = new(-3);

    [Fact]
    public void DoubleClicSurLIcone_LeRelachementFinal_NeRearmePasLeSimpleClic()
    {
        using var iso = new BancCapture.Isolation();
        IntPtr hwnd = Win32.CreateWindowExW(0, "STATIC", "c3.1", 0, 0, 0, 0, 0,
            HwndMessage, IntPtr.Zero, Win32.GetModuleHandleW(null), IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, hwnd);
        try
        {
            var tray = (TrayApplication)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplication));
            BancCapture.SetField(tray, "_hWnd", hwnd);
            BancCapture.SetField(tray, "_activationConsent", true);
            BancCapture.SetField(tray, "_enabled", true);   // double clic = bascule du clavier virtuel, absent ici
            var wndProc = (Win32.WNDPROC)Delegate.CreateDelegate(typeof(Win32.WNDPROC), tray,
                typeof(TrayApplication).GetMethod("WndProcCallback", BindingFlags.Instance | BindingFlags.NonPublic)!);

            void Souris(uint message) => wndProc(hwnd, WmTrayIcon, (IntPtr)1, (IntPtr)message);
            bool MinuterieArmee() => Win32.KillTimer(hwnd, (UIntPtr)TimerSimpleClic);

            Souris(Win32.WM_LBUTTONUP);
            Assert.True(MinuterieArmee(), "témoin : un simple clic arme la minuterie de simple clic");

            Souris(Win32.WM_LBUTTONUP);
            Souris(Win32.WM_LBUTTONDBLCLK);
            Assert.False(MinuterieArmee(), "témoin : le double clic désarme la minuterie");

            Souris(Win32.WM_LBUTTONUP);   // relâchement du second clic, après WM_LBUTTONDBLCLK
            Assert.False(MinuterieArmee(),
                "C3.1 : le relâchement final d'un double clic réarme la minuterie : le menu du simple clic s'ouvrira");

            Souris(Win32.WM_LBUTTONDOWN);
            Souris(Win32.WM_LBUTTONUP);
            Assert.True(MinuterieArmee(), "après le double clic, un simple clic arme de nouveau la minuterie");

            Souris(Win32.WM_LBUTTONDOWN);
            Souris(Win32.WM_LBUTTONUP);
            Souris(Win32.WM_LBUTTONDBLCLK);   // shell qui n'enverrait pas le dernier relâchement
            Souris(Win32.WM_LBUTTONDOWN);
            Souris(Win32.WM_LBUTTONUP);
            Assert.True(MinuterieArmee(), "un relâchement manquant ne mange pas le simple clic suivant");
        }
        finally
        {
            Win32.DestroyWindow(hwnd);
        }
    }
}
