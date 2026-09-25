// Micro-benchmarks du moteur AZERTY Global 1.3.0 (audit simplicité 2026-09-25).
//
// Ce qui est mesuré : le rappel réel KeyboardHook.HookCallback (délégué privé _proc, obtenu
// par réflexion) -> KeyMapper -> ForegroundMonitor -> IWin32Api, pour des séquences de frappe
// typiques. AUCUN hook n'est installé (SetWindowsHookEx n'est jamais appelé), AUCUN SendInput
// réel n'est émis (l'API de banc rend le nombre d'événements demandés).
//
// Deux passes par scénario :
//  - « mock »  : toutes les fonctions Win32 sont simulées (coût managé pur du moteur) ;
//  - « ombre » : chaque fonction Win32 EN LECTURE SEULE appelée par le moteur est aussi appelée
//    réellement (mêmes P/Invoke que la production, TypingEngine.Windows.Win32), mais son résultat
//    est ignoré au profit du script, pour garder une logique déterministe. ToUnicode (drapeau 0,
//    qui modifie l'état des touches mortes du système) est remplacé par ToUnicodeEx drapeau 4
//    (sans effet d'état), GetAsyncKeyState vise VK_F24 (ne consomme pas le bit d'une vraie touche).
//    SendInput n'est jamais appelé réellement.
//
// CallNextHookEx est appelé réellement par HookCallback pour les touches laissées passer :
// hors d'une chaîne de hooks il rend 0 sans effet (le hhk est ignoré depuis Windows NT).
using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;
using TypingEngine.Core;
using TypingEngine.Windows;

namespace BenchMoteur;

internal static unsafe class Program
{
    private const uint WM_KEYDOWN = 0x100, WM_KEYUP = 0x101;
    private const uint EXT = 0x01, UP = 0x80;
    private const int Warmup = 50_000;
    private const int Iterations = 200_000;

    private sealed record Scenario(string Name, string Expected, (uint Vk, uint Scan, uint Flags, uint Msg)[] Events,
        bool Caps = false, string[]? Modules = null);

    private static readonly string LayoutPath =
        @"D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store\src\AZERTY Global 2026.json";

    private static int Main(string[] args)
    {
        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High; } catch { }
        Console.OutputEncoding = Encoding.UTF8;

        Header();
        if (args.Contains("--hors-rappel"))
        {
            Console.WriteLine("=== 3. (seule) Travaux hors rappel ===");
            OffPathCosts();
            return 0;
        }
        var layout = LayoutJsonParser.Parse(File.OpenRead(LayoutPath));

        var scenarios = new[]
        {
            new Scenario("A lettre simple 'a' (pass-through natif)", "",
                new[] { (0x41u, 0x10u, 0u, WM_KEYDOWN), (0x41u, 0x10u, UP, WM_KEYUP) }),
            new Scenario("B caractère émis '.' (Unicode, SC 0x33)", ".",
                new[] { (0xBEu, 0x33u, 0u, WM_KEYDOWN), (0xBEu, 0x33u, UP, WM_KEYUP) }),
            new Scenario("C AltGr + e -> '€' (6 rappels)", "€",
                new[]
                {
                    (0xA2u, 0x21Du, 0u, WM_KEYDOWN), (0xA5u, 0x38u, EXT, WM_KEYDOWN),
                    (0x45u, 0x12u, 0u, WM_KEYDOWN), (0x45u, 0x12u, UP, WM_KEYUP),
                    (0xA5u, 0x38u, EXT | UP, WM_KEYUP), (0xA2u, 0x21Du, UP, WM_KEYUP)
                }),
            new Scenario("D touche morte ^ puis e -> 'ê' (4 rappels)", "ê",
                new[]
                {
                    (0xDDu, 0x1Au, 0u, WM_KEYDOWN), (0xDDu, 0x1Au, UP, WM_KEYUP),
                    (0x45u, 0x12u, 0u, WM_KEYDOWN), (0x45u, 0x12u, UP, WM_KEYUP)
                }),
            new Scenario("E Verr. Maj. intelligent : é -> 'É'", "É",
                new[] { (0x32u, 0x03u, 0u, WM_KEYDOWN), (0x32u, 0x03u, UP, WM_KEYUP) }, Caps: true),
            new Scenario("F mode NativeCombo (glfw3.dll) '.'", ".",
                new[] { (0xBEu, 0x33u, 0u, WM_KEYDOWN), (0xBEu, 0x33u, UP, WM_KEYUP) },
                Modules: new[] { "kernel32.dll", "glfw3.dll" }),
        };

        Console.WriteLine();
        Console.WriteLine("=== 1. Rappel complet HookCallback, par séquence (une « frappe » = tous ses rappels) ===");
        Console.WriteLine($"    échauffement {Warmup:N0} x2, mesure {Iterations:N0} séquences ; temps en µs ; octets GC par séquence");
        foreach (var shadow in new[] { false, true })
        {
            Console.WriteLine();
            Console.WriteLine(shadow ? "--- passe « ombre » (Win32 lecture seule réellement appelé, SendInput simulé) ---"
                                     : "--- passe « mock » (Win32 entièrement simulé) ---");
            foreach (var sc in scenarios)
                RunScenario(layout, sc, shadow);
        }

        Console.WriteLine();
        Console.WriteLine("=== 2. Coûts unitaires (attribution) ===");
        UnitCosts(layout);

        Console.WriteLine();
        Console.WriteLine("=== 3. Travaux hors rappel mais sur le fil du hook ou périodiques ===");
        OffPathCosts();

        return 0;
    }

    private static void Header()
    {
        Console.WriteLine("Bench moteur AZERTY Global 1.3.0 — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Console.WriteLine($"Runtime : {RuntimeInformation.FrameworkDescription} ; {RuntimeInformation.ProcessArchitecture} ; " +
                          $"OS {Environment.OSVersion.VersionString}");
        Console.WriteLine($"CPU logiques : {Environment.ProcessorCount} ; ServerGC={GCSettings.IsServerGC} ; " +
                          $"LatencyMode={GCSettings.LatencyMode}");
        Console.WriteLine($"Stopwatch : Frequency={Stopwatch.Frequency:N0} Hz (résolution {1e9 / Stopwatch.Frequency:F0} ns), " +
                          $"HighResolution={Stopwatch.IsHighResolution}");
#if DEBUG
        Console.WriteLine("Configuration : DEBUG");
#else
        Console.WriteLine(System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled
            ? "Configuration : RELEASE JIT (TieredPGO par défaut) — la production est NativeAOT OptimizationPreference=Size"
            : "Configuration : RELEASE NativeAOT (publié avec OptimizationPreference=Size, comme la production)");
#endif
        try { Console.WriteLine($"Priorité processus : {Process.GetCurrentProcess().PriorityClass}"); } catch { }
    }

    private static void RunScenario(Layout layout, Scenario sc, bool shadow)
    {
        var api = new BenchApi
        {
            Shadow = shadow,
            CapsOn = sc.Caps,
            Modules = sc.Modules,
            RealHkl = Win32.GetKeyboardLayout(0),
        };
        var host = new BenchHost();
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero, host);
        var mapper = new KeyMapper(layout, api, host);
        mapper.SetForegroundMonitor(monitor);
        var hook = new KeyboardHook(mapper, host);
        var proc = (Win32.LowLevelKeyboardProc)typeof(KeyboardHook)
            .GetField("_proc", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hook)!;

        int n = sc.Events.Length;
        var ptrs = new IntPtr[n];
        var wps = new IntPtr[n];
        for (int i = 0; i < n; i++)
        {
            ptrs[i] = Marshal.AllocHGlobal(sizeof(Win32.KBDLLHOOKSTRUCT));
            *(Win32.KBDLLHOOKSTRUCT*)ptrs[i] = new Win32.KBDLLHOOKSTRUCT
            {
                vkCode = sc.Events[i].Vk,
                scanCode = sc.Events[i].Scan,
                flags = sc.Events[i].Flags,
                time = 0,
                dwExtraInfo = IntPtr.Zero,
            };
            wps[i] = (IntPtr)sc.Events[i].Msg;
        }

        // Vérification fonctionnelle sur une séquence, avec comptage des appels Win32.
        api.ResetCounts();
        host.Texts.Clear();
        var returns = new long[n];
        for (int j = 0; j < n; j++) returns[j] = proc(0, wps[j], ptrs[j]).ToInt64();
        string emitted = string.Concat(host.Texts);
        bool ok = emitted == sc.Expected;
        string counts = api.Counts();

        host.Record = false; // n'accumule plus de texte pendant la mesure
        for (int i = 0; i < Warmup; i++) RunOnce(proc, wps, ptrs);
        Thread.Sleep(300); // laisser le JIT promouvoir en tier 1
        for (int i = 0; i < Warmup; i++) RunOnce(proc, wps, ptrs);

        var samples = new long[Iterations];
        int gc0 = GC.CollectionCount(0);
        long alloc0 = GC.GetAllocatedBytesForCurrentThread();
        long total0 = Stopwatch.GetTimestamp();
        for (int i = 0; i < Iterations; i++)
        {
            long t0 = Stopwatch.GetTimestamp();
            RunOnce(proc, wps, ptrs);
            samples[i] = Stopwatch.GetTimestamp() - t0;
        }
        long total1 = Stopwatch.GetTimestamp();
        long alloc1 = GC.GetAllocatedBytesForCurrentThread();
        int gc1 = GC.CollectionCount(0);

        Array.Sort(samples);
        double f = 1e6 / Stopwatch.Frequency; // ticks -> µs
        double mean = (total1 - total0) * f / Iterations;
        Console.WriteLine($"{sc.Name} [{(shadow ? "ombre" : "mock")}]");
        Console.WriteLine($"   contrôle : texte émis « {emitted} » attendu « {sc.Expected} » -> {(ok ? "OK" : "ÉCART")} ; " +
                          $"retours du rappel [{string.Join(",", returns)}] (1 = touche bloquée)");
        Console.WriteLine($"   appels par séquence : {counts}");
        Console.WriteLine($"   médiane {samples[Iterations / 2] * f:F3} µs ; p99 {samples[(int)(Iterations * 0.99)] * f:F3} µs ; " +
                          $"p99.9 {samples[(int)(Iterations * 0.999)] * f:F3} µs ; max {samples[^1] * f:F1} µs ; " +
                          $"moyenne {mean:F3} µs ; par rappel ~{mean / n:F3} µs");
        Console.WriteLine($"   allocations : {(alloc1 - alloc0) / (double)Iterations:F1} octets/séquence " +
                          $"({(alloc1 - alloc0) / (double)(Iterations * n):F1} octets/rappel) ; GC gen0 pendant la mesure : {gc1 - gc0}");

        foreach (var p in ptrs) Marshal.FreeHGlobal(p);
    }

    private static void RunOnce(Win32.LowLevelKeyboardProc proc, IntPtr[] wps, IntPtr[] ptrs)
    {
        for (int j = 0; j < ptrs.Length; j++) proc(0, wps[j], ptrs[j]);
    }

    // ---------------------------------------------------------------- coûts unitaires

    private static void UnitCosts(Layout layout)
    {
        IntPtr p = Marshal.AllocHGlobal(sizeof(Win32.KBDLLHOOKSTRUCT));
        *(Win32.KBDLLHOOKSTRUCT*)p = new Win32.KBDLLHOOKSTRUCT { vkCode = 0x41, scanCode = 0x10 };
        long sink = 0;
        Micro("Marshal.PtrToStructure<KBDLLHOOKSTRUCT> (production, KeyboardHook.cs:275)", 2_000_000,
            () => sink += Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(p).vkCode);
        Micro("lecture directe *(KBDLLHOOKSTRUCT*)lParam (alternative)", 2_000_000,
            () => sink += ((Win32.KBDLLHOOKSTRUCT*)p)->vkCode);

        var api = new BenchApi { RealHkl = Win32.GetKeyboardLayout(0) };
        var host = new BenchHost();
        var mapper = new KeyMapper(layout, api, host);
        Micro("KeyMapper.MatchesShortcutKey(W, 'a') — 1 des 2 appels faits à CHAQUE rappel", 2_000_000,
            () => sink += mapper.MatchesShortcutKey(0x57, 0x41, 0x10) ? 1 : 0);
        Micro("KeyMapper.MatchesShortcutKey(Q, 'a') — 2e appel", 2_000_000,
            () => sink += mapper.MatchesShortcutKey(0x51, 0x41, 0x10) ? 1 : 0);

        // Réplique exacte de KeyMapper.cs:743 (MaintainableLayerManager.SupportedLayers est internal).
        string[] supported = { "dk_greek", "dk_cyrillic", "dk_scientific" };
        string probe = "e";
        Micro("réplique KeyMapper.cs:743 : string[3].Contains(x, StringComparer.Ordinal) (LINQ)", 2_000_000,
            () => sink += supported.Contains(probe, StringComparer.Ordinal) ? 1 : 0);
        Micro("alternative : Array.IndexOf(string[3], x) >= 0", 2_000_000,
            () => sink += Array.IndexOf(supported, probe) >= 0 ? 1 : 0);

        // Réplique des allocations de CompensateSystemDeadKey (KeyMapper.cs:309-310), à chaque caractère.
        Micro("réplique KeyMapper.cs:309-310 : new StringBuilder(8) + new byte[256]", 2_000_000,
            () => { var b = new StringBuilder(8); var k = new byte[256]; sink += b.Capacity + k.Length; });

        // Réplique de TryEmitText (KeyMapper.cs:974 + 1007) pour un caractère BMP en Unicode.
        Micro("réplique KeyMapper.cs:974+1007 : new List<INPUT>(16) + 2 Add + ToArray()", 2_000_000,
            () =>
            {
                var l = new List<Win32.INPUT>(1 * 16);
                l.Add(default); l.Add(default);
                sink += l.ToArray().Length;
            });

        IntPtr hkl = Win32.GetKeyboardLayout(0);
        var state = new byte[256];
        var sb = new StringBuilder(8);
        Micro("Win32.GetForegroundWindow() réel", 1_000_000, () => sink += Win32.GetForegroundWindow().ToInt64());
        Micro("Win32.GetKeyState(VK_CAPITAL) réel", 1_000_000, () => sink += Win32.GetKeyState(0x14));
        Micro("Win32.GetAsyncKeyState(VK_F24) réel", 1_000_000, () => sink += Win32.GetAsyncKeyState(0x87));
        Micro("Win32.GetKeyboardLayout(0) réel", 1_000_000, () => sink += Win32.GetKeyboardLayout(0).ToInt64());
        Micro("Win32.MapVirtualKeyExW(0x33, VSC_TO_VK) réel", 1_000_000, () => sink += Win32.MapVirtualKeyExW(0x33, 1, hkl));
        Micro("Win32.VkKeyScanExW('.') réel", 1_000_000, () => sink += Win32.VkKeyScanExW('.', hkl));
        Micro("Win32.ToUnicodeEx(espace, drapeau 4) réel, StringBuilder marshalé", 1_000_000,
            () => sink += Win32.ToUnicodeEx(0x20, 0x39, state, sb, sb.Capacity, 4, hkl));
        Micro("sonde AG130-10 : 247 x GetAsyncKeyState (sur VK_F24) — par tick de 2 s", 20_000,
            () => { for (int vk = 0x08; vk <= 0xFE; vk++) sink += Win32.GetAsyncKeyState(0x87); });
        GC.KeepAlive(sink);
        Marshal.FreeHGlobal(p);
    }

    private static void Micro(string name, int n, Action body)
    {
        for (int i = 0; i < Math.Min(n, 200_000); i++) body();
        Thread.Sleep(100);
        for (int i = 0; i < Math.Min(n, 200_000); i++) body();
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        long t0 = Stopwatch.GetTimestamp();
        for (int i = 0; i < n; i++) body();
        long t1 = Stopwatch.GetTimestamp();
        long a1 = GC.GetAllocatedBytesForCurrentThread();
        double ns = (t1 - t0) * 1e9 / Stopwatch.Frequency / n;
        Console.WriteLine($"   {name,-92} {ns,9:F1} ns/op   {(a1 - a0) / (double)n,6:F1} o/op");
    }

    // ---------------------------------------------------------------- hors rappel

    private static void OffPathCosts()
    {
        // Chargement de la disposition au démarrage (LayoutLoader -> LayoutJsonParser).
        byte[] json = File.ReadAllBytes(LayoutPath);
        Heavy("LayoutJsonParser.Parse(33 Ko) — démarrage", 200, () =>
        {
            using var ms = new MemoryStream(json, writable: false);
            return LayoutJsonParser.Parse(ms).Keys.Count;
        });

        // ForegroundMonitor.Recompute : parties non-UIA de RealWin32Api sur la vraie fenêtre de
        // premier plan (lecture seule). IsWindowPasswordField n'est pas appelé : sa requête UIA
        // activerait l'arbre d'accessibilité d'une application Chromium de l'utilisateur.
        var real = new RealWin32Api();
        IntPtr fg = Win32.GetForegroundWindow();
        bool okProc = real.TryGetWindowProcess(fg, out var name, out _, out _, out uint pid);
        int moduleCount = 0;
        if (okProc && real.TryEnumProcessModules(pid, out var mods)) moduleCount = mods.Length;
        Console.WriteLine($"   premier plan au moment du banc : {name ?? "(inaccessible)"} ; modules : {moduleCount}");
        if (okProc)
        {
            Heavy("RealWin32Api.TryGetWindowProcess (OpenProcess + QueryFullProcessImageName)", 200,
                () => real.TryGetWindowProcess(fg, out _, out _, out _, out _) ? 1 : 0);
            Heavy("RealWin32Api.TryGetProcessStartTime", 200,
                () => real.TryGetProcessStartTime(pid, out _) ? 1 : 0);
            Heavy($"RealWin32Api.TryEnumProcessModules ({moduleCount} modules) — à chaque Recompute non surclassé", 100,
                () => real.TryEnumProcessModules(pid, out var m) ? m.Length : 0);
        }

        int procCount = Process.GetProcesses().Length;
        Heavy($"GameRegistry.IsRemoteAccessHostRunning ({procCount} processus) — toutes les 5 s, pool de threads", 30,
            () => GameRegistry.IsRemoteAccessHostRunning() ? 1 : 0);
        Heavy("alternative : Toolhelp32 (CreateToolhelp32Snapshot + Process32NextW, comparaison sans chaîne)", 30,
            () => Toolhelp.AnyRemoteHost() ? 1 : 0);
        Console.WriteLine($"   contrôle : GameRegistry={GameRegistry.IsRemoteAccessHostRunning()} ; Toolhelp32={Toolhelp.AnyRemoteHost()}");
    }

    private static void Heavy(string name, int n, Func<int> body)
    {
        for (int i = 0; i < 5; i++) body();
        var s = new double[n];
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < n; i++)
        {
            long t0 = Stopwatch.GetTimestamp();
            body();
            s[i] = (Stopwatch.GetTimestamp() - t0) * 1e3 / Stopwatch.Frequency;
        }
        long a1 = GC.GetAllocatedBytesForCurrentThread();
        Array.Sort(s);
        Console.WriteLine($"   {name,-92} médiane {s[n / 2],8:F3} ms ; p95 {s[(int)(n * 0.95)],8:F3} ms ; " +
                          $"{(a1 - a0) / (double)n / 1024.0,8:F1} Kio/op");
    }
}

/// <summary>Alternative mesurée à Process.GetProcesses pour la sonde C5 (lecture seule).</summary>
internal static unsafe class Toolhelp
{
    private const uint TH32CS_SNAPPROCESS = 0x2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32W
    {
        public uint dwSize, cntUsage, th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID, cntThreads, th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        public fixed char szExeFile[260];
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32FirstW(IntPtr snap, PROCESSENTRY32W* entry);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32NextW(IntPtr snap, PROCESSENTRY32W* entry);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr h);

    public static bool AnyRemoteHost()
    {
        IntPtr snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snap == IntPtr.Zero || snap == (IntPtr)(-1)) return false;
        try
        {
            PROCESSENTRY32W e = default;
            e.dwSize = (uint)sizeof(PROCESSENTRY32W);
            for (bool ok = Process32FirstW(snap, &e); ok; ok = Process32NextW(snap, &e))
            {
                var name = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(e.szExeFile);
                foreach (var host in GameRegistry.RemoteAccessHostProcesses)
                    if (name.Equals(host, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
        finally { CloseHandle(snap); }
    }
}

/// <summary>Hôte neutre : raccourcis par défaut du produit (Ctrl+Maj+W et Ctrl+Maj+Q).</summary>
internal sealed class BenchHost : IWindowsTypingHost
{
    public bool Record = true;
    public List<string> Texts { get; } = new();
    public uint ShortcutCharacterSearchVk => 0x57;
    public uint ShortcutVirtualKeyboardVk => 0x51;
    public bool CompatibilityDebugLog => false;
    public string? GetCompatibilityOverride(string processName) => null;
    public string AnonymizeProcessName(string? processName) => processName ?? "unknown";
    public void RecordEmittedText(string text) { if (Record) Texts.Add(text); }
    public void Log(string context, Exception exception) => Console.WriteLine($"   !! Log {context}: {exception.Message}");
    public void LogCompatibilityEvent(string eventName, string details) { }
    public void LogCompatibilityCriticalEvent(string eventName, string details) =>
        Console.WriteLine($"   !! critique {eventName}: {details}");
}

/// <summary>
/// Équivalent de MockWin32Api sans enregistrement (MockWin32Api copie chaque lot SendInput dans
/// une liste : il fausserait la mesure des allocations). Réponses scriptées pour un AZERTY
/// français natif ; en mode Shadow, appelle aussi la vraie fonction Win32 en lecture seule.
/// </summary>
internal sealed class BenchApi : IWin32Api
{
    public bool Shadow;
    public bool CapsOn;
    public string[]? Modules;
    public IntPtr RealHkl;
    private static readonly IntPtr Hkl = (IntPtr)0x040C040C;
    private static readonly IntPtr Window = (IntPtr)0x12345678;
    private readonly byte[] _shadowState = new byte[256];
    private readonly StringBuilder _shadowBuf = new(8);

    public int VkScan, MapVk, KeyState, AsyncKey, KbdLayout, ToUni, ToUniEx, Send, SendEvents, Fg;
    public void ResetCounts() => VkScan = MapVk = KeyState = AsyncKey = KbdLayout = ToUni = ToUniEx = Send = SendEvents = Fg = 0;
    public string Counts() =>
        $"GetForegroundWindow {Fg}, GetKeyState {KeyState}, GetAsyncKeyState {AsyncKey}, ToUnicode {ToUni}, " +
        $"ToUnicodeEx {ToUniEx}, MapVirtualKeyExW {MapVk}, VkKeyScanExW {VkScan}, GetKeyboardLayout {KbdLayout}, " +
        $"SendInput {Send} ({SendEvents} évén.)";

    public short VkKeyScanExW(char ch, IntPtr hkl)
    {
        VkScan++;
        if (Shadow) Win32.VkKeyScanExW(ch, RealHkl);
        return ch switch { '.' => 0x1BE, _ => -1 };
    }

    public uint MapVirtualKeyExW(uint code, uint mapType, IntPtr hkl)
    {
        MapVk++;
        if (Shadow) Win32.MapVirtualKeyExW(code, mapType, RealHkl);
        return (code, mapType) switch
        {
            (0x33, 1) => 0xBE,
            (0xBE, 0) => 0x33,
            (0x10, 1) => 0x41,
            _ => 0
        };
    }

    public short GetKeyState(int vk)
    {
        KeyState++;
        if (Shadow) Win32.GetKeyState(vk);
        if (vk == 0x14) return (short)(CapsOn ? 1 : 0);
        if (vk == 0x90) return 1;
        return 0;
    }

    public short GetAsyncKeyState(int vk)
    {
        AsyncKey++;
        if (Shadow) Win32.GetAsyncKeyState(0x87); // VK_F24 : même coût, aucun bit de vraie touche consommé
        return unchecked((short)0x8000); // modificateurs tenus : aucune resynchronisation parasite
    }

    public IntPtr GetKeyboardLayout(uint threadId)
    {
        KbdLayout++;
        if (Shadow) Win32.GetKeyboardLayout(threadId);
        return Hkl;
    }

    public int ToUnicode(uint vk, uint scan, byte[] state, StringBuilder buffer, int capacity, uint flags)
    {
        ToUni++;
        // Production : ToUnicode drapeau 0 (modifie l'état DK système). Ombre : même coût d'appel
        // et de marshaling, drapeau 4 pour ne rien modifier.
        if (Shadow) Win32.ToUnicodeEx(vk, scan, state, buffer, capacity, flags | 4, RealHkl);
        return 0;
    }

    public int ToUnicodeEx(uint vk, uint scan, byte[] state, StringBuilder buffer, int capacity, uint flags, IntPtr hkl)
    {
        ToUniEx++;
        if (Shadow) Win32.ToUnicodeEx(vk, scan, state, buffer, capacity, flags | 4, RealHkl);
        buffer.Clear();
        buffer.Append(state[0x14] == 0 ? 'a' : 'A');
        return 1;
    }

    public uint SendInput(Win32.INPUT[] inputs)
    {
        Send++;
        SendEvents += inputs.Length;
        return (uint)inputs.Length; // jamais de SendInput réel
    }

    public IntPtr GetForegroundWindow()
    {
        Fg++;
        if (Shadow) Win32.GetForegroundWindow();
        return Window;
    }

    public bool TryGetWindowProcess(IntPtr window, out string? processName, out string? fullPath, out IntPtr hkl, out uint pid)
    {
        processName = "notepad.exe";
        fullPath = @"C:\Windows\notepad.exe";
        hkl = Hkl;
        pid = 4242;
        return true;
    }

    public bool TryGetProcessStartTime(uint pid, out long startTimeTicks)
    {
        startTimeTicks = 133_000_000_000_000_000;
        return true;
    }

    public bool IsWindowPasswordField(IntPtr window) => false;

    public bool TryEnumProcessModules(uint pid, out string[] moduleFileNames)
    {
        moduleFileNames = Modules ?? Array.Empty<string>();
        return Modules != null;
    }

    public IntPtr SetWinEventHook(uint eventMin, uint eventMax, Win32.WinEventDelegate cb) => (IntPtr)0xCAFE;
    public bool UnhookWinEvent(IntPtr hook) => true;
}
