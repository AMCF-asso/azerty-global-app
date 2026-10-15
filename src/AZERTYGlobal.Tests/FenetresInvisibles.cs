using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tant qu'il vit, rend invisibles les fenêtres de premier niveau que crée le fil courant :
/// opacité 1/255 et clics qui les traversent. DWM les compose toujours : elles se peignent,
/// et le DC comme PrintWindow les lisent comme avant. Les tests et les bancs n'affichent donc
/// plus leurs fenêtres devant l'utilisateur (demande du 04/10). Les clics les traversent, mais
/// une fenêtre qui lit GetCursorPos (les Leçons) voit toujours le curseur : elle le lit par
/// <c>CursorSource</c>, que <c>BancCapture.Isolation</c> remplace par une position simulée hors
/// fenêtre (15/10) ; la vraie souris ne bouge plus.
///
/// Crochet WH_CALLWNDPROCRET local au fil, posé sans attendre le balayage de
/// <see cref="MasqueFenetresProcessus"/> : aucune DLL, rien hors du processus de test. La fenêtre que le produit rend lui-même translucide (la
/// notification de bascule) garde son réglage : son SetLayeredWindowAttributes passe après.
///
/// <c>AZERTY_FENETRES_VISIBLES=1</c> désactive le crochet, pour regarder un test tourner.
/// </summary>
internal sealed class FenetresInvisibles : IDisposable
{
    internal const string VisibleVariable = "AZERTY_FENETRES_VISIBLES";

    private const int WH_CALLWNDPROCRET = 12;
    private const uint WM_CREATE = 0x0001;

    private readonly HookProc? _proc; // gardé vivant : Windows garde un pointeur vers le délégué
    private readonly IntPtr _hook;
    private bool _disposed;

    public FenetresInvisibles()
    {
        string? visible = Environment.GetEnvironmentVariable(VisibleVariable);
        if (visible == "1" || string.Equals(visible, "true", StringComparison.OrdinalIgnoreCase))
            return;

        _proc = OnCallWndProcRet;
        _hook = SetWindowsHookExW(WH_CALLWNDPROCRET, _proc, IntPtr.Zero, GetCurrentThreadId());
        if (_hook == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookExW(WH_CALLWNDPROCRET)");
    }

    /// <summary>
    /// Au retour de WM_CREATE, avant que CreateWindowEx ne montre la fenêtre. L'ancien crochet
    /// CBT posait le style à HCBT_CREATEWND, où il était écrasé : les fenêtres restaient opaques
    /// (mesuré le 07/10).
    /// </summary>
    private IntPtr OnCallWndProcRet(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var ret = Marshal.PtrToStructure<CWPRETSTRUCT>(lParam);
            if (ret.message == WM_CREATE)
                MasqueFenetresProcessus.Mask(ret.hwnd);
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed || _hook == IntPtr.Zero)
            return;
        _disposed = true;
        UnhookWindowsHookEx(_hook);
    }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct CWPRETSTRUCT
    {
        public IntPtr lResult;
        public IntPtr lParam;
        public IntPtr wParam;
        public uint message;
        public IntPtr hwnd;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int idHook, HookProc lpfn, IntPtr hmod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
