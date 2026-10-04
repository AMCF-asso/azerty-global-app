using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tant qu'il vit, rend invisibles les fenêtres de premier niveau que crée le fil courant :
/// opacité 1/255 et clics qui les traversent. DWM les compose toujours : elles se peignent,
/// et le DC comme PrintWindow les lisent comme avant. Les tests et les bancs n'affichent donc
/// plus leurs fenêtres devant l'utilisateur (demande du 04/10). Les clics les traversent, mais
/// une fenêtre qui lit GetCursorPos (les Leçons) voit toujours le curseur : le banc l'écarte
/// avant chaque prise (<c>BancCapture.Capture</c>).
///
/// Crochet CBT local au fil : aucune DLL, rien hors du processus de test, et aucun
/// changement dans <c>src/</c>. La fenêtre que le produit rend lui-même translucide (la
/// notification de bascule) garde son réglage : son SetLayeredWindowAttributes passe après.
///
/// <c>AZERTY_FENETRES_VISIBLES=1</c> désactive le crochet, pour regarder un test tourner.
/// </summary>
internal sealed class FenetresInvisibles : IDisposable
{
    internal const string VisibleVariable = "AZERTY_FENETRES_VISIBLES";

    /// <summary>Opacité : 1 sur 255, imperceptible. À 0, DWM pourrait ne plus rendre la fenêtre.</summary>
    private const byte Alpha = 1;

    private const int WH_CBT = 5;
    private const int HCBT_CREATEWND = 3;
    private const int GWL_EXSTYLE = -20;
    private const long WS_CHILD = 0x40000000;
    private const long WS_EX_TRANSPARENT = 0x00000020;
    private const long WS_EX_LAYERED = 0x00080000;
    private const uint LWA_ALPHA = 0x2;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private readonly HookProc? _proc; // gardé vivant : Windows garde un pointeur vers le délégué
    private readonly IntPtr _hook;
    private bool _disposed;

    public FenetresInvisibles()
    {
        string? visible = Environment.GetEnvironmentVariable(VisibleVariable);
        if (visible == "1" || string.Equals(visible, "true", StringComparison.OrdinalIgnoreCase))
            return;

        _proc = OnCbt;
        _hook = SetWindowsHookExW(WH_CBT, _proc, IntPtr.Zero, GetCurrentThreadId());
        if (_hook == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookExW(WH_CBT)");
    }

    private IntPtr OnCbt(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code == HCBT_CREATEWND)
            Hide(wParam, lParam);
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    /// <summary>
    /// À HCBT_CREATEWND, la fenêtre existe mais n'a reçu aucun message : le style étendu est
    /// posé sur elle et dans son CREATESTRUCT, pour que WM_NCCREATE et WM_CREATE le voient.
    /// </summary>
    private static void Hide(IntPtr hwnd, IntPtr cbtCreate)
    {
        IntPtr lpcs = Marshal.ReadIntPtr(cbtCreate);
        var cs = Marshal.PtrToStructure<CREATESTRUCTW>(lpcs);
        if ((cs.style & WS_CHILD) != 0 || cs.hwndParent == HWND_MESSAGE)
            return;

        cs.dwExStyle |= (uint)(WS_EX_LAYERED | WS_EX_TRANSPARENT);
        Marshal.StructureToPtr(cs, lpcs, false);
        long exStyle = GetWindowLongPtrW(hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtrW(hwnd, GWL_EXSTYLE, new IntPtr(exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT));
        SetLayeredWindowAttributes(hwnd, 0, Alpha, LWA_ALPHA);
    }

    public void Dispose()
    {
        if (_disposed || _hook == IntPtr.Zero)
            return;
        _disposed = true;
        UnhookWindowsHookEx(_hook);
    }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREATESTRUCTW
    {
        public IntPtr lpCreateParams;
        public IntPtr hInstance;
        public IntPtr hMenu;
        public IntPtr hwndParent;
        public int cy;
        public int cx;
        public int y;
        public int x;
        public int style;
        public IntPtr lpszName;
        public IntPtr lpszClass;
        public uint dwExStyle;
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

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
}
