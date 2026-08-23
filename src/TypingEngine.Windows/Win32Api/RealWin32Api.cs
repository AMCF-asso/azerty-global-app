// Implémentation prod de IWin32Api : délègue aux P/Invoke statiques de Win32.
using System.Runtime.InteropServices;
using System.Text;

namespace TypingEngine.Windows;

public sealed class RealWin32Api : IWin32Api
{
    public short VkKeyScanExW(char ch, IntPtr hkl) => Win32.VkKeyScanExW(ch, hkl);

    public uint MapVirtualKeyExW(uint code, uint mapType, IntPtr hkl) =>
        Win32.MapVirtualKeyExW(code, mapType, hkl);

    public short GetKeyState(int vk) => Win32.GetKeyState(vk);

    public short GetAsyncKeyState(int vk) => Win32.GetAsyncKeyState(vk);

    public IntPtr GetKeyboardLayout(uint threadId) => Win32.GetKeyboardLayout(threadId);

    public uint SendInput(Win32.INPUT[] inputs) =>
        Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());

    public IntPtr GetForegroundWindow() => Win32.GetForegroundWindow();

    public bool TryGetForegroundProcess(out string? processName, out string? fullPath, out IntPtr hkl, out uint pid)
    {
        processName = null;
        fullPath = null;
        hkl = IntPtr.Zero;
        pid = 0;

        var hwnd = Win32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        uint tid = Win32.GetWindowThreadProcessIdOut(hwnd, out pid);
        if (tid == 0 || pid == 0) return false;

        // Layout natif du thread foreground
        hkl = Win32.GetKeyboardLayout(tid);

        // Nom du process via droits minimaux : PROCESS_VM_READ échoue plus souvent
        // sur les process protégés et ferait basculer le monitor en mode indéterminé.
        IntPtr hProc = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProc == IntPtr.Zero) return false;

        try
        {
            var sb = new StringBuilder(32768);
            uint len = (uint)sb.Capacity;
            if (!Win32.QueryFullProcessImageNameW(hProc, 0, sb, ref len) || len == 0)
                return false;
            fullPath = sb.ToString(0, (int)Math.Min(len, (uint)sb.Length));
            processName = System.IO.Path.GetFileName(fullPath);
            return true;
        }
        finally
        {
            Win32.CloseHandle(hProc);
        }
    }

    public bool TryGetProcessStartTime(uint pid, out long startTimeTicks)
    {
        startTimeTicks = 0;
        if (pid == 0) return false;

        IntPtr hProc = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProc == IntPtr.Zero) return false;
        try
        {
            if (!Win32.GetProcessTimes(hProc, out var creation, out _, out _, out _))
                return false;
            startTimeTicks = creation.ToLong();
            return startTimeTicks != 0;
        }
        finally
        {
            Win32.CloseHandle(hProc);
        }
    }

    public bool IsForegroundPasswordField()
    {
        IntPtr foreground = Win32.GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;

        uint threadId = Win32.GetWindowThreadProcessIdOut(foreground, out _);
        if (threadId == 0) return false;

        var info = new Win32.GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<Win32.GUITHREADINFO>() };
        if (!Win32.GetGUIThreadInfo(threadId, ref info) || info.hwndFocus == IntPtr.Zero)
            return false;

        // ES_PASSWORD est la source native fiable pour EDIT et RichEdit. Pour les
        // contrôles navigateur, SecureInputDetector complète cette détection hors hook.
        int style = Win32.GetWindowLongW(info.hwndFocus, Win32.GWL_STYLE);
        if ((style & Win32.ES_PASSWORD) != 0)
        {
            var className = new StringBuilder(64);
            if (Win32.GetClassNameW(info.hwndFocus, className, className.Capacity) == 0)
                return true; // style explicite : rester conservateur si la classe est inaccessible

            string name = className.ToString();
            if (name.Equals("Edit", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("RichEdit", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Password", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Chromium, Firefox et plusieurs applications modernes exposent le champ
        // sécurisé par UIA plutôt que par un HWND enfant avec ES_PASSWORD.
        return SecureInputDetector.IsFocusedElementPassword();
    }

    public bool TryEnumProcessModules(uint pid, out string[] moduleFileNames)
    {
        moduleFileNames = Array.Empty<string>();

        IntPtr hProc = Win32.OpenProcess(
            Win32.PROCESS_QUERY_LIMITED_INFORMATION | Win32.PROCESS_VM_READ, false, pid);
        if (hProc == IntPtr.Zero) return false;

        try
        {
            var modules = new IntPtr[256];
            uint needed;
            while (true)
            {
                uint cb = (uint)(modules.Length * IntPtr.Size);
                if (!Win32.EnumProcessModulesEx(hProc, modules, cb, out needed, Win32.LIST_MODULES_ALL))
                    return false;
                if (needed <= cb)
                    break;
                modules = new IntPtr[Math.Max(modules.Length * 2, (int)((needed + (uint)IntPtr.Size - 1) / (uint)IntPtr.Size))];
            }

            int count = (int)(needed / IntPtr.Size);
            var names = new List<string>(count);
            var sb = new StringBuilder(1024);
            for (int i = 0; i < count; i++)
            {
                sb.Clear();
                uint len = Win32.GetModuleFileNameExW(hProc, modules[i], sb, (uint)sb.Capacity);
                if (len > 0)
                    names.Add(System.IO.Path.GetFileName(sb.ToString()));
            }
            moduleFileNames = names.ToArray();
            return true;
        }
        finally
        {
            Win32.CloseHandle(hProc);
        }
    }

    public IntPtr SetWinEventHook(uint eventMin, uint eventMax, Win32.WinEventDelegate cb) =>
        Win32.SetWinEventHook(eventMin, eventMax, IntPtr.Zero, cb, 0, 0, Win32.WINEVENT_OUTOFCONTEXT);

    public bool UnhookWinEvent(IntPtr hook) => Win32.UnhookWinEvent(hook);
}
