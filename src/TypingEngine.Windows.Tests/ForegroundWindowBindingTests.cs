using System.Text;
using TypingEngine.Windows.Testing;
using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>Le HWND capturé doit porter sa propre identité, même après A-B-A.</summary>
public sealed class ForegroundWindowBindingTests
{
    private static readonly IntPtr AntiCheatWindow = (IntPtr)0xA001;
    private static readonly IntPtr NormalWindow = (IntPtr)0xB002;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Anti_cheat_reste_suspendu_pendant_un_aller_retour(bool race)
    {
        var api = CreateApi(AntiCheatWindow);
        api.Race = race;
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);
        Assert.Equal(new[] { AntiCheatWindow, AntiCheatWindow }, api.Inspections);
        Assert.Equal(race ? new[] { NormalWindow, AntiCheatWindow } : Array.Empty<IntPtr>(), api.Transitions);
        Assert.Equal("valorant.exe", monitor.CurrentProcessName);
        Assert.Equal(CompatibilitySuspendReason.AntiCheat, monitor.CurrentSuspendReason);
        Assert.Equal(CompatibilityMode.DisabledAntiCheat, monitor.GetEmitContext().Mode);
    }

    [Fact]
    public void Fenetre_normale_stable_reste_utilisable()
    {
        var api = CreateApi(NormalWindow);
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);
        Assert.Equal(new[] { NormalWindow, NormalWindow }, api.Inspections);
        Assert.Equal(CompatibilityMode.Default, monitor.GetEmitContext().Mode);
        Assert.False(monitor.IsSecureInput);
    }

    private static WindowBoundApi CreateApi(IntPtr current)
    {
        var api = new WindowBoundApi(current);
        api.AddWindow(NormalWindow, "notepad.exe", @"C:\Windows\notepad.exe", (IntPtr)0x040C040C, false);
        api.AddWindow(AntiCheatWindow, "valorant.exe", @"C:\Games\valorant.exe", (IntPtr)0x040C040C, true);
        return api;
    }

    private sealed class WindowBoundApi : IWin32Api
    {
        private readonly MockWin32Api _inner = new();
        private readonly Dictionary<IntPtr, WindowState> _windows = new();
        private readonly Queue<IntPtr> _script = new();
        private IntPtr _current;

        internal List<IntPtr> Reads { get; } = new();
        internal List<IntPtr> Inspections { get; } = new();
        internal List<IntPtr> Transitions { get; } = new();
        internal bool Race { get; set; }

        internal WindowBoundApi(IntPtr current) => _current = current;

        internal void AddWindow(IntPtr hwnd, string process, string path, IntPtr hkl, bool secure) =>
            _windows[hwnd] = new(process, path, hkl, secure);

        internal void Replay(params IntPtr[] windows)
        {
            _script.Clear();
            foreach (var window in windows) _script.Enqueue(window);
            _current = windows[^1];
            Reads.Clear();
        }

        public IntPtr GetForegroundWindow()
        {
            IntPtr result = _script.Count > 0 ? _script.Dequeue() : _current;
            Reads.Add(result);
            return result;
        }

        public bool TryGetWindowProcess(IntPtr hwnd, out string? processName, out string? fullPath, out IntPtr hkl, out uint pid)
        {
            Inspections.Add(hwnd);
            if (Race) { _current = NormalWindow; Transitions.Add(_current); }
            if (!_windows.TryGetValue(hwnd, out var state))
            {
                processName = null;
                fullPath = null;
                hkl = IntPtr.Zero;
                pid = 0;
                return false;
            }

            processName = state.Process;
            fullPath = state.Path;
            hkl = state.Hkl;
            pid = hwnd == AntiCheatWindow ? 1001u : 2002u;
            return true;
        }

        public bool IsWindowPasswordField(IntPtr hwnd)
        {
            Inspections.Add(hwnd);
            if (Race) { _current = AntiCheatWindow; Transitions.Add(_current); }
            return _windows.TryGetValue(hwnd, out var state) && state.Secure;
        }

        public short VkKeyScanExW(char ch, IntPtr hkl) => _inner.VkKeyScanExW(ch, hkl);
        public uint MapVirtualKeyExW(uint code, uint mapType, IntPtr hkl) => _inner.MapVirtualKeyExW(code, mapType, hkl);
        public short GetKeyState(int vk) => _inner.GetKeyState(vk);
        public short GetAsyncKeyState(int vk) => _inner.GetAsyncKeyState(vk);
        public IntPtr GetKeyboardLayout(uint threadId) => _inner.GetKeyboardLayout(threadId);
        public int ToUnicode(uint vk, uint scan, byte[] state, StringBuilder buffer, int capacity, uint flags) =>
            _inner.ToUnicode(vk, scan, state, buffer, capacity, flags);
        public int ToUnicodeEx(uint vk, uint scan, byte[] state, StringBuilder buffer, int capacity, uint flags, IntPtr hkl) =>
            _inner.ToUnicodeEx(vk, scan, state, buffer, capacity, flags, hkl);
        public uint SendInput(Win32.INPUT[] inputs) => _inner.SendInput(inputs);
        public int LastSendInputError => _inner.LastSendInputError;
        public bool TryGetProcessStartTime(uint pid, out long startTimeTicks)
        {
            startTimeTicks = pid == 0 ? 0 : pid * 10_000L;
            return pid != 0;
        }
        public bool TryEnumProcessModules(uint pid, out string[] moduleFileNames)
        {
            moduleFileNames = Array.Empty<string>();
            return true;
        }
        public IntPtr SetWinEventHook(uint eventMin, uint eventMax, Win32.WinEventDelegate cb) => (IntPtr)0xCAFE;
        public bool UnhookWinEvent(IntPtr hook) => true;

        private sealed record WindowState(string Process, string Path, IntPtr Hkl, bool Secure);
    }
}
