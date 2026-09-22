using System.Text;
using TypingEngine.Windows.Testing;
using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Témoin d'audit isolé : l'API réelle relit GetForegroundWindow dans
/// TryGetForegroundProcess et IsForegroundPasswordField. Le mock de la suite principale
/// ne le fait pas, donc ShellRaceSuspensionTests ne peut pas rejouer cette course.
/// </summary>
public sealed class ForegroundAbaRaceWitnessTests
{
    private static readonly IntPtr AntiCheatWindow = (IntPtr)0xA001;
    private static readonly IntPtr NormalWindow = (IntPtr)0xB002;

    [Fact]
    public void Positive_control_stable_anti_cheat_A_is_suspended()
    {
        var (api, monitor) = CreateMonitor();
        using (monitor)
        {
            api.Replay(AntiCheatWindow, AntiCheatWindow, AntiCheatWindow);
            monitor.Recompute();

            Assert.Equal(new[] { AntiCheatWindow, AntiCheatWindow, AntiCheatWindow }, api.Reads);
            Assert.Equal("valorant.exe", monitor.CurrentProcessName);
            Assert.Equal(CompatibilityMode.DisabledAntiCheat, monitor.CurrentMode);
            Assert.Equal(CompatibilitySuspendReason.AntiCheat, monitor.CurrentSuspendReason);
            Assert.Equal(CompatibilityMode.DisabledAntiCheat, monitor.GetEmitContext().Mode);
        }
    }

    [Fact]
    public void Negative_control_stable_normal_B_remains_default()
    {
        var (api, monitor) = CreateMonitor();
        using (monitor)
        {
            api.Replay(NormalWindow, NormalWindow, NormalWindow);
            monitor.Recompute();

            Assert.Equal(new[] { NormalWindow, NormalWindow, NormalWindow }, api.Reads);
            Assert.Equal("notepad.exe", monitor.CurrentProcessName);
            Assert.Equal(CompatibilityMode.Default, monitor.CurrentMode);
            Assert.Equal(CompatibilitySuspendReason.None, monitor.CurrentSuspendReason);
            Assert.Equal(CompatibilityMode.Default, monitor.GetEmitContext().Mode);
        }
    }

    [Fact]
    public void Witness_traverse_exactly_A_then_B_then_A_during_recompute()
    {
        var (api, monitor) = CreateMonitorAndReplayRace();
        using (monitor)
        {
            Assert.Equal(new[] { AntiCheatWindow, NormalWindow, AntiCheatWindow }, api.Reads);
            Assert.Equal("notepad.exe", monitor.CurrentProcessName);
            Assert.Equal(CompatibilityMode.Default, monitor.CurrentMode);
        }
    }

    [Fact]
    public void Security_property_anti_cheat_A_must_remain_suspended_after_ABA()
    {
        var (api, monitor) = CreateMonitorAndReplayRace();
        using (monitor)
        {
            Assert.Equal(new[] { AntiCheatWindow, NormalWindow, AntiCheatWindow }, api.Reads);

            // GetEmitContext relit A. Le snapshot porte aussi le HWND A, donc la garde
            // d'obsolescence accepte le mode Default calculé depuis le processus B.
            var emit = monitor.GetEmitContext();
            Assert.Equal(AntiCheatWindow, api.Reads[^1]);

            // Propriété de sécurité attendue : A est une cible anti-cheat connue, donc
            // aucune émission ne doit être permise. Cette assertion échoue au HEAD audité.
            Assert.Equal(CompatibilityMode.DisabledAntiCheat, emit.Mode);
        }
    }

    private static (ReReadingForegroundApi Api, ForegroundMonitor Monitor) CreateMonitorAndReplayRace()
    {
        var (api, monitor) = CreateMonitor();
        api.Replay(AntiCheatWindow, NormalWindow, AntiCheatWindow);
        monitor.Recompute();
        return (api, monitor);
    }

    private static (ReReadingForegroundApi Api, ForegroundMonitor Monitor) CreateMonitor()
    {
        var api = new ReReadingForegroundApi(NormalWindow);
        api.AddWindow(NormalWindow, "notepad.exe", @"C:\Windows\notepad.exe", (IntPtr)0x040C040C, secure: false);
        // Ce témoin porte uniquement sur le mode anti-cheat. Garder Secure=false évite
        // d'en tirer à tort une conclusion sur la détection des champs de mot de passe.
        api.AddWindow(AntiCheatWindow, "valorant.exe", @"C:\Games\VALORANT\valorant.exe", (IntPtr)0x040C040C, secure: false);

        // Le constructeur fait son propre Recompute : le laisser se stabiliser sur B.
        // Chaque test pose ensuite son propre script et efface ces lectures initiales.
        var monitor = new ForegroundMonitor(api, IntPtr.Zero);
        return (api, monitor);
    }

    /// <summary>
    /// Décorateur du mock existant qui reproduit la sémantique de RealWin32Api : les
    /// méthodes d'inspection relisent elles-mêmes la fenêtre de premier plan.
    /// </summary>
    private sealed class ReReadingForegroundApi : IWin32Api
    {
        private readonly MockWin32Api _inner = new();
        private readonly Dictionary<IntPtr, WindowState> _windows = new();
        private readonly Queue<IntPtr> _script = new();
        private IntPtr _current;

        internal List<IntPtr> Reads { get; } = new();

        internal ReReadingForegroundApi(IntPtr current) => _current = current;

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

        public bool TryGetForegroundProcess(out string? processName, out string? fullPath, out IntPtr hkl, out uint pid)
        {
            IntPtr hwnd = GetForegroundWindow();
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

        public bool IsForegroundPasswordField()
        {
            IntPtr hwnd = GetForegroundWindow();
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
