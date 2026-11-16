using System.Runtime.InteropServices;
using TypingEngine.Windows.Testing;
using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Audit des accès concurrents 1.3.0, constat R1 : ré-entrée dans <see cref="ForegroundMonitor.Recompute"/>.
///
/// Recompute tourne sur le fil d'interface (STA). Il appelle la sonde du champ mot de passe, qui
/// attend son worker par <c>WaitOne</c> (≤ 30 ms, <see cref="SecureInputProbe.Query"/>). Sur un fil
/// STA, cette attente pompe les messages envoyés : un WinEvent hors contexte arrivé pendant
/// l'attente lance un Recompute imbriqué. Le snapshot de la nouvelle fenêtre est écrit, puis
/// l'appel externe reprend et l'écrase avec l'ancien.
///
/// Rien n'est simulé sur le chemin en cause : vrai hook WinEvent hors contexte (événement
/// applicatif <see cref="EvenementTest"/>, levé par <c>NotifyWinEvent</c>), vraie
/// <see cref="SecureInputProbe"/> et son vrai worker. Seuls le premier plan et le processus
/// sont scriptés par <see cref="MockWin32Api"/>.
/// </summary>
public sealed class ForegroundMonitorReentreeTests
{
    private const uint EvenementTest = 0xA001; // plage applicative (EVENT_AIA_START)
    private const int MarqueurEnfant = 0x41414331;
    private static readonly IntPtr W0 = (IntPtr)0x1000, W1 = (IntPtr)0x1111, W2 = (IntPtr)0x2222;

    [Fact]
    public void WinEventPendantLAttenteDeLaSonde_LeSnapshotFinalDecritLaFenetreAuPremierPlan()
    {
        var bilan = SurUnFilSta(leveePendantLaSonde: true);

        // La ré-entrée a bien eu lieu : le Recompute imbriqué a sondé W2 pendant que l'externe sondait W1.
        Assert.Equal(2, bilan.ProfondeurMax);
        Assert.Equal(W2, bilan.PremierPlan);
        // Attendu : le snapshot décrit le premier plan réel. Constaté (défaut) : il décrit W1.
        Assert.Equal("w2.exe", bilan.Processus);
        Assert.False(bilan.Perime, "le snapshot final décrit une autre fenêtre que le premier plan");
    }

    [Fact]
    public void Temoin_WinEventApresLeRecompute_LeSnapshotSuitLePremierPlan()
    {
        // Même chaîne (vrai hook, vraie sonde), mais l'événement arrive hors de toute attente :
        // le hook et le gestionnaire mènent bien à W2, sans imbrication.
        var bilan = SurUnFilSta(leveePendantLaSonde: false);

        Assert.Equal(1, bilan.ProfondeurMax);
        Assert.Equal("w2.exe", bilan.Processus);
        Assert.False(bilan.Perime);
    }

    /// <summary>
    /// Même ré-entrée, déterministe : la sonde de W1 lance elle-même le Recompute imbriqué (ce
    /// que fait le WinEvent distribué par l'attente), puis rend la main ou lève une exception.
    /// Les deux sorties de l'appel externe (succès et repli du catch) ne doivent rien écrire.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecomputeImbrique_LAppelExterneNEcrasePasLEtatPlusRecent(bool sondeEnEchec)
    {
        var mock = new MockWin32Api { ForegroundWindow = W0, ScriptedProcessName = "w0.exe" };
        ForegroundMonitor? monitor = null;
        int changements = 0;
        var api = new ApiSondeScriptee(mock, window =>
        {
            if (window != W1) return false;
            mock.ScriptedProcessName = "w2.exe";
            mock.ForegroundWindow = W2;
            monitor!.Recompute();
            if (sondeEnEchec) throw new InvalidOperationException("UIA indisponible (simulé)");
            return false;
        });
        using (monitor = new ForegroundMonitor(api, IntPtr.Zero, new TestWindowsTypingHost()))
        {
            monitor.ForegroundChanged += () => changements++;
            mock.ScriptedProcessName = "w1.exe";
            mock.ForegroundWindow = W1;
            monitor.Recompute();

            Assert.Equal("w2.exe", monitor.CurrentProcessName);
            Assert.False(monitor.IsWindowStale);
            Assert.Equal(CompatibilityMode.Default, monitor.CurrentMode);
            Assert.Equal("w2.exe", monitor.LastApplicationProcessName);
            Assert.Equal(1, changements); // celui de l'appel imbriqué seulement
        }
    }

    private sealed record Bilan(int ProfondeurMax, IntPtr PremierPlan, string? Processus, bool Perime);

    private static Bilan SurUnFilSta(bool leveePendantLaSonde)
    {
        Bilan? bilan = null;
        Exception? echec = null;
        var fil = new Thread(() =>
        {
            try { bilan = Jouer(leveePendantLaSonde); }
            catch (Exception ex) { echec = ex; }
        }) { IsBackground = true, Name = "UiSta" };
        fil.SetApartmentState(ApartmentState.STA);
        fil.Start();
        Assert.True(fil.Join(TimeSpan.FromSeconds(20)), "le fil STA du test ne s'est pas terminé");
        if (echec != null) throw new InvalidOperationException($"mesure impossible : {echec.Message}", echec);
        return bilan!;
    }

    private static Bilan Jouer(bool leveePendantLaSonde)
    {
        PeekMessageW(out _, IntPtr.Zero, 0, 0, 0); // crée la file de messages du fil

        var mock = new MockWin32Api { ForegroundWindow = W0, ScriptedProcessName = "w0.exe" };
        bool basculeFaite = false;

        void BasculerVersW2()
        {
            mock.ScriptedProcessName = "w2.exe";
            mock.ForegroundWindow = W2;
            // Une vraie fenêtre : Windows ignore un événement sans fenêtre.
            NotifyWinEvent(EvenementTest, GetDesktopWindow(), 0, MarqueurEnfant);
        }

        using var sonde = new SecureInputProbe(() => true, window =>
        {
            if (leveePendantLaSonde && window == W1 && !basculeFaite)
            {
                basculeFaite = true;
                BasculerVersW2();
                Thread.Sleep(150); // le worker reste occupé au-delà des deux délais de 30 ms
            }
            return false;
        });

        var api = new ApiAvecVraiWinEvent(mock, sonde);
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero, new TestWindowsTypingHost());
        Assert.True(monitor.IsTrackingAvailable);
        Pomper(TimeSpan.FromMilliseconds(100));

        mock.ScriptedProcessName = "w1.exe";
        mock.ForegroundWindow = W1;
        api.ProfondeurMax = 0;
        monitor.Recompute();

        if (!leveePendantLaSonde) BasculerVersW2();
        Pomper(TimeSpan.FromMilliseconds(300));

        return new Bilan(api.ProfondeurMax, mock.ForegroundWindow, monitor.CurrentProcessName, monitor.IsWindowStale);
    }

    private static void Pomper(TimeSpan duree)
    {
        var fin = DateTime.UtcNow + duree;
        while (DateTime.UtcNow < fin)
        {
            while (PeekMessageW(out var msg, IntPtr.Zero, 0, 0, PM_REMOVE)) DispatchMessageW(ref msg);
            Thread.Sleep(5);
        }
    }

    /// <summary>Délègue au mock, sauf le suivi des fenêtres (un vrai hook hors contexte sur
    /// <see cref="EvenementTest"/>) et la sonde du mot de passe (une vraie <see cref="SecureInputProbe"/>,
    /// comme <c>RealWin32Api</c> via <c>SecureInputDetector</c>).</summary>
    private sealed class ApiAvecVraiWinEvent(MockWin32Api mock, SecureInputProbe sonde) : IWin32Api
    {
        private IntPtr _vraiHook;
        private Win32.WinEventDelegate? _filtre;
        private int _profondeur;
        public int ProfondeurMax;

        public IntPtr SetWinEventHook(uint eventMin, uint eventMax, Win32.WinEventDelegate cb)
        {
            if (_vraiHook != IntPtr.Zero) return (IntPtr)0xBEEF; // les suivis suivants : posés, muets
            _filtre = (h, ev, hwnd, idObject, idChild, thread, time) =>
            {
                if (ev == EvenementTest && idChild == MarqueurEnfant) cb(h, ev, hwnd, idObject, idChild, thread, time);
            };
            _vraiHook = Win32.SetWinEventHook(EvenementTest, EvenementTest, IntPtr.Zero, _filtre,
                0, 0, Win32.WINEVENT_OUTOFCONTEXT);
            if (_vraiHook == IntPtr.Zero) throw new InvalidOperationException("SetWinEventHook réel a échoué");
            return _vraiHook;
        }

        public bool UnhookWinEvent(IntPtr hook) => hook != _vraiHook || Win32.UnhookWinEvent(hook);

        public bool IsWindowPasswordField(IntPtr window)
        {
            _profondeur++;
            ProfondeurMax = Math.Max(ProfondeurMax, _profondeur);
            try { return sonde.Query(window); }
            finally { _profondeur--; }
        }

        public short VkKeyScanExW(char ch, IntPtr hkl) => mock.VkKeyScanExW(ch, hkl);
        public uint MapVirtualKeyExW(uint code, uint mapType, IntPtr hkl) => mock.MapVirtualKeyExW(code, mapType, hkl);
        public short GetKeyState(int vk) => mock.GetKeyState(vk);
        public short GetAsyncKeyState(int vk) => mock.GetAsyncKeyState(vk);
        public IntPtr GetKeyboardLayout(uint threadId) => mock.GetKeyboardLayout(threadId);
        public int ToUnicode(uint vk, uint scan, byte[] state, System.Text.StringBuilder buffer, int capacity, uint flags) =>
            mock.ToUnicode(vk, scan, state, buffer, capacity, flags);
        public int ToUnicodeEx(uint vk, uint scan, byte[] state, System.Text.StringBuilder buffer, int capacity, uint flags, IntPtr hkl) =>
            mock.ToUnicodeEx(vk, scan, state, buffer, capacity, flags, hkl);
        public uint SendInput(Win32.INPUT[] inputs) => mock.SendInput(inputs);
        public IntPtr GetForegroundWindow() => mock.GetForegroundWindow();
        public bool TryGetWindowProcess(IntPtr window, out string? processName, out string? fullPath, out IntPtr hkl, out uint pid) =>
            mock.TryGetWindowProcess(window, out processName, out fullPath, out hkl, out pid);
        public bool TryGetProcessStartTime(uint pid, out long startTimeTicks) => mock.TryGetProcessStartTime(pid, out startTimeTicks);
        public bool TryEnumProcessModules(uint pid, out string[] moduleFileNames) => mock.TryEnumProcessModules(pid, out moduleFileNames);
    }

    /// <summary>Délègue tout au mock, sauf la sonde du mot de passe, scriptée par le test.</summary>
    private sealed class ApiSondeScriptee(MockWin32Api mock, Func<IntPtr, bool> sonde) : IWin32Api
    {
        public bool IsWindowPasswordField(IntPtr window) => sonde(window);
        public IntPtr SetWinEventHook(uint eventMin, uint eventMax, Win32.WinEventDelegate cb) => mock.SetWinEventHook(eventMin, eventMax, cb);
        public bool UnhookWinEvent(IntPtr hook) => mock.UnhookWinEvent(hook);
        public short VkKeyScanExW(char ch, IntPtr hkl) => mock.VkKeyScanExW(ch, hkl);
        public uint MapVirtualKeyExW(uint code, uint mapType, IntPtr hkl) => mock.MapVirtualKeyExW(code, mapType, hkl);
        public short GetKeyState(int vk) => mock.GetKeyState(vk);
        public short GetAsyncKeyState(int vk) => mock.GetAsyncKeyState(vk);
        public IntPtr GetKeyboardLayout(uint threadId) => mock.GetKeyboardLayout(threadId);
        public int ToUnicode(uint vk, uint scan, byte[] state, System.Text.StringBuilder buffer, int capacity, uint flags) =>
            mock.ToUnicode(vk, scan, state, buffer, capacity, flags);
        public int ToUnicodeEx(uint vk, uint scan, byte[] state, System.Text.StringBuilder buffer, int capacity, uint flags, IntPtr hkl) =>
            mock.ToUnicodeEx(vk, scan, state, buffer, capacity, flags, hkl);
        public uint SendInput(Win32.INPUT[] inputs) => mock.SendInput(inputs);
        public IntPtr GetForegroundWindow() => mock.GetForegroundWindow();
        public bool TryGetWindowProcess(IntPtr window, out string? processName, out string? fullPath, out IntPtr hkl, out uint pid) =>
            mock.TryGetWindowProcess(window, out processName, out fullPath, out hkl, out pid);
        public bool TryGetProcessStartTime(uint pid, out long startTimeTicks) => mock.TryGetProcessStartTime(pid, out startTimeTicks);
        public bool TryEnumProcessModules(uint pid, out string[] moduleFileNames) => mock.TryEnumProcessModules(pid, out moduleFileNames);
    }

    private const uint PM_REMOVE = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd; public uint message; public UIntPtr wParam; public IntPtr lParam;
        public uint time; public int x, y;
    }

    [DllImport("user32.dll")]
    private static extern bool PeekMessageW(out MSG msg, IntPtr hwnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG msg);
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")]
    private static extern void NotifyWinEvent(uint ev, IntPtr hwnd, int idObject, int idChild);
}
