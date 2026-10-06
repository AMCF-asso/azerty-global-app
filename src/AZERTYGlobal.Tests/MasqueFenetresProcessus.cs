using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Rend invisibles toutes les fenêtres de premier niveau du processus de test, quel que soit le
/// fil qui les crée. <see cref="FenetresInvisibles"/> ne couvre que le fil qui l'installe : les
/// tests ordinaires et leurs fils STA affichaient donc leurs fenêtres (mesuré le 07/10 : fenêtre
/// de conflit opaque pendant les 943 tests). Demande d'Antoine : aucun test ne montre de fenêtre.
///
/// Deux voies, sans DLL, posées par un fil dédié :
/// - un crochet WH_CALLWNDPROCRET sur chaque fil du processus, posé dès que le fil a une file de messages (balayage toutes
///   les 10 ms) : synchrone, la fenêtre est masquée au retour de WM_CREATE, avant d'être montrée ;
/// - pour un fil né entre deux balayages, l'écoute d'EVENT_OBJECT_CREATE / SHOW de ce processus.
///   Mesuré : l'événement n'arrive qu'au premier traitement de messages du fil créateur, donc au
///   plus tard quand la fenêtre se peint.
///
/// <c>AZERTY_FENETRES_VISIBLES=1</c> le désactive, pour regarder un test tourner.
/// </summary>
internal static class MasqueFenetresProcessus
{
    private const uint EVENT_OBJECT_CREATE = 0x8000;
    private const uint EVENT_OBJECT_SHOW = 0x8002;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const int OBJID_WINDOW = 0;
    private const uint GA_ROOT = 2;
    private const int WH_CALLWNDPROCRET = 12;
    private const uint WM_CREATE = 0x0001;
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const long WS_CHILD = 0x40000000;
    private const long WS_EX_TRANSPARENT = 0x00000020;
    private const long WS_EX_LAYERED = 0x00080000;
    private const uint LWA_ALPHA = 0x2;
    private const byte Alpha = 1;
    private const uint ScanMilliseconds = 10;
    private const uint QS_ALLINPUT = 0x04FF;
    private const uint PM_REMOVE = 0x1;

    // Gardés vivants : Windows garde un pointeur vers les délégués.
    private static WinEventProc? _eventProc;
    private static HookProc? _hookProc;
    private static readonly Dictionary<int, IntPtr> ThreadHooks = new();
    private static long _scans;

    /// <summary>Balayages faits : s'il n'avance plus, le fil est bloqué et les nouveaux fils ne sont plus couverts.</summary>
    internal static long Scans => Interlocked.Read(ref _scans);

    [ModuleInitializer]
    internal static void Start()
    {
        string? visible = Environment.GetEnvironmentVariable(FenetresInvisibles.VisibleVariable);
        if (visible == "1" || string.Equals(visible, "true", StringComparison.OrdinalIgnoreCase))
            return;

        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "MasqueFenetres" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait(TimeSpan.FromSeconds(5));
    }

    private static void Run(ManualResetEventSlim ready)
    {
        _eventProc = OnEvent;
        _hookProc = OnCallWndProcRet;
        SetWinEventHook(EVENT_OBJECT_CREATE, EVENT_OBJECT_SHOW, IntPtr.Zero, _eventProc,
            (uint)Environment.ProcessId, 0, WINEVENT_OUTOFCONTEXT);
        HookThreads();
        ready.Set();

        while (true)
        {
            MsgWaitForMultipleObjects(0, null, false, ScanMilliseconds, QS_ALLINPUT);
            while (PeekMessageW(out MSG msg, IntPtr.Zero, 0, 0, PM_REMOVE))
                DispatchMessageW(ref msg);
            HookThreads();
            Interlocked.Increment(ref _scans);
        }
    }

    /// <summary>Pose le crochet sur chaque fil vivant qui ne l'a pas encore.</summary>
    private static void HookThreads()
    {
        var alive = new HashSet<int>();
        using var process = Process.GetCurrentProcess();
        foreach (ProcessThread thread in process.Threads)
        {
            int id = thread.Id;
            alive.Add(id);
            // Un fil sans file de messages refuse le crochet : on retente au balayage suivant.
            if (ThreadHooks.TryGetValue(id, out IntPtr posé) && posé != IntPtr.Zero)
                continue;
            ThreadHooks[id] = SetWindowsHookExW(WH_CALLWNDPROCRET, _hookProc!, IntPtr.Zero, (uint)id);
        }

        foreach (int id in new List<int>(ThreadHooks.Keys))
        {
            if (alive.Contains(id))
                continue;
            if (ThreadHooks[id] != IntPtr.Zero)
                UnhookWindowsHookEx(ThreadHooks[id]);
            ThreadHooks.Remove(id);
        }
    }

    /// <summary>
    /// Au retour de WM_CREATE : la fenêtre existe, CreateWindowEx ne l'a pas encore montrée.
    /// Le style étendu posé à HCBT_CREATEWND, lui, était écrasé ensuite (mesuré le 07/10 : la
    /// fenêtre ressortait opaque).
    /// </summary>
    private static IntPtr OnCallWndProcRet(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var ret = Marshal.PtrToStructure<CWPRETSTRUCT>(lParam);
            if (ret.message == WM_CREATE)
                Mask(ret.hwnd);
        }
        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    private static void OnEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        // Hors du fil de balayage : changer le style d'une fenêtre d'un autre fil lui envoie
        // WM_STYLECHANGING et attend sa réponse. Un fil de test qui ne pompe pas bloquait ainsi le
        // balayage, et les fils suivants n'étaient plus couverts (mesuré le 07/10, 2 runs sur 5).
        if (idObject == OBJID_WINDOW && idChild == 0)
            ThreadPool.UnsafeQueueUserWorkItem(static h => Mask(h), hwnd, preferLocal: false);
    }

    /// <summary>Opacité 1/255 et clics traversants sur une fenêtre de premier niveau.</summary>
    internal static void Mask(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || GetAncestor(hwnd, GA_ROOT) != hwnd
            || (GetWindowLongPtrW(hwnd, GWL_STYLE).ToInt64() & WS_CHILD) != 0)
        {
            return;
        }

        long exStyle = GetWindowLongPtrW(hwnd, GWL_EXSTYLE).ToInt64();
        if ((exStyle & WS_EX_LAYERED) != 0
            && GetLayeredWindowAttributes(hwnd, out _, out byte alpha, out uint flags)
            && (flags & LWA_ALPHA) != 0 && alpha <= Alpha)
        {
            return;
        }

        SetWindowLongPtrW(hwnd, GWL_EXSTYLE, new IntPtr(exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT));
        SetLayeredWindowAttributes(hwnd, 0, Alpha, LWA_ALPHA);
    }

    private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
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

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int x;
        public int y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmod, WinEventProc proc, uint idProcess, uint idThread, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int idHook, HookProc lpfn, IntPtr hmod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint MsgWaitForMultipleObjects(uint count, IntPtr[]? handles, bool waitAll, uint milliseconds, uint wakeMask);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessageW(out MSG msg, IntPtr hWnd, uint min, uint max, uint remove);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG msg);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLayeredWindowAttributes(IntPtr hwnd, out uint crKey, out byte bAlpha, out uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
}
