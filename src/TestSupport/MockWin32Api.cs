// Mock IWin32Api pour les tests d'intégration niveau 3.
//
// Limitations connues (cf. plan v0.9.7 §« Limitations du mock ») :
// - Timing exact des events Windows (debounce, ordering rapide) non reproductible
// - MapVirtualKeyExW pour HKL non installé : renvoie ce qu'on script, pas le vrai comportement
// - Ordre d'arrivée des EVENT_SYSTEM_FOREGROUND rapprochés non simulé
// - Insertion d'input physique entre deux events d'un batch SendInput impossible à mocker
// - Effet réel de KEYEVENTF_SCANCODE côté apps tierces non testable
//
// Les tests niveau 3 valident donc la LOGIQUE de construction des INPUT[],
// pas le comportement runtime de Windows.

namespace TypingEngine.Windows.Testing;

internal sealed class MockWin32Api : IWin32Api
{
    // ── Recordings ──────────────────────────────────────────────────
    /// <summary>Tous les batches SendInput appelés depuis le mock, en ordre d'arrivée.</summary>
    public List<Win32.INPUT[]> SendInputCalls { get; } = new();

    /// <summary>Aplati toutes les séquences d'INPUT en un seul tableau (pour assertions).</summary>
    public Win32.INPUT[] AllInputs => SendInputCalls.SelectMany(b => b).ToArray();

    // ── Scripted responses pour clavier/layout ──────────────────────
    /// <summary>
    /// Mappings scriptés pour VkKeyScanExW. Clé (char, hkl) → résultat short.
    /// La valeur -1 signifie « caractère inaccessible » (déclenche fallback Alt+code dans la prod).
    /// </summary>
    public Dictionary<(char, IntPtr), short> VkKeyScanScript { get; } = new();

    /// <summary>Réponses scriptées pour MapVirtualKeyExW. Clé (vk, mapType, hkl) → scancode.</summary>
    public Dictionary<(uint, uint, IntPtr), uint> MapVirtualKeyScript { get; } = new();

    /// <summary>Réponses scriptées pour GetKeyState. Clé = vk → result short.</summary>
    public Dictionary<int, short> KeyStateScript { get; } = new();

    public Dictionary<int, short> AsyncKeyStateScript { get; } = new();

    /// <summary>Layout courant retourné pour GetKeyboardLayout (peu importe le thread).</summary>
    public IntPtr CurrentHkl { get; set; } = (IntPtr)0x040C040C; // AZERTY FR par défaut

    public IntPtr ForegroundWindow { get; set; } = (IntPtr)0x12345678;

    // ── Foreground / process inspection ─────────────────────────────
    public string? ScriptedProcessName { get; set; }
    public string? ScriptedFullPath { get; set; }
    public uint ScriptedPid { get; set; } = 1234;
    public long ScriptedProcessStartTime { get; set; } = 987654321;
    public bool ScriptedSecureInput { get; set; }
    public string[]? ScriptedModules { get; set; }
    public bool ShouldFailForegroundInspection { get; set; }

    // ── WinEventHook ────────────────────────────────────────────────
    public Win32.WinEventDelegate? CapturedWinEventDelegate { get; private set; }
    public IntPtr WinEventHookHandle { get; set; } = (IntPtr)0xCAFE;
    public bool ShouldFailSetWinEventHook { get; set; }
    public bool UnhookWinEventCalled { get; private set; }

    /// <summary>
    /// Simule un changement de foreground en invoquant le delegate capturé
    /// avec les valeurs scriptées (process / hkl / modules).
    /// </summary>
    public void SimulateForegroundChange(string? processName, string? fullPath, IntPtr hkl, string[]? modules = null)
    {
        ScriptedProcessName = processName;
        ScriptedFullPath = fullPath;
        ForegroundWindow = processName == null ? IntPtr.Zero : (IntPtr)0x12345678;
        CurrentHkl = hkl;
        ScriptedModules = modules;
        CapturedWinEventDelegate?.Invoke(IntPtr.Zero, Win32.EVENT_SYSTEM_FOREGROUND,
            ForegroundWindow, 0, 0, 0, 0);
    }

    // ── IWin32Api implementation ────────────────────────────────────

    public short VkKeyScanExW(char ch, IntPtr hkl) =>
        VkKeyScanScript.TryGetValue((ch, hkl), out var v) ? v : (short)-1;

    public uint MapVirtualKeyExW(uint code, uint mapType, IntPtr hkl) =>
        MapVirtualKeyScript.TryGetValue((code, mapType, hkl), out var v) ? v : 0;

    public short GetKeyState(int vk) =>
        KeyStateScript.TryGetValue(vk, out var v) ? v : (short)0;

    public short GetAsyncKeyState(int vk) =>
        AsyncKeyStateScript.TryGetValue(vk, out var v) ? v : (short)0;

    public IntPtr GetKeyboardLayout(uint threadId) => CurrentHkl;

    public uint? SendInputResult { get; set; }

    /// <summary>Code d'erreur Win32 rendu au moteur apres un SendInput a 0 (AG130-09).</summary>
    public int LastSendInputError { get; set; }
    public Queue<uint> SendInputResults { get; } = new();
    public List<uint> ToUnicodeExFlags { get; } = new();
    public int DeadKeyState { get; set; }
    public int ToUnicode(uint vk, uint scan, byte[] state, System.Text.StringBuilder buffer, int capacity, uint flags)
    {
        int result = DeadKeyState;
        DeadKeyState = 0;
        return result;
    }

    /// <summary>
    /// Touches mortes du layout natif sous-jacent, par code virtuel. Windows fait répondre
    /// -1 à <c>ToUnicodeEx</c> pour celles-là, et c'est ce -1 que lit
    /// <c>KeyMapper.IsDeadKeyOnLayout</c> pour renoncer à la combo native et retomber sur
    /// Alt+code (« ^ », « ¨ », « ~ », « ` » sur un AZERTY traditionnel).
    ///
    /// ⛔ Ajouté le 2026-09-22 (§ 3.4 de la revue du 2026-09-21). Ce mock rendait 1 en toutes
    /// circonstances : <c>IsDeadKeyOnLayout</c> ne pouvait **jamais** valoir vrai sous test,
    /// et le repli avait zéro couverture dans sa branche utile. Un mock qui ne sait répondre
    /// qu'une seule chose ne mesure pas une décision binaire, il en cache la moitié.
    ///
    /// Vide par défaut : le comportement des témoins écrits avant cette date est inchangé.
    /// </summary>
    public HashSet<byte> NativeDeadKeys { get; } = new();

    public int ToUnicodeEx(uint vk, uint scan, byte[] state, System.Text.StringBuilder buffer, int capacity, uint flags, IntPtr hkl)
    {
        ToUnicodeExFlags.Add(flags);
        if ((flags & 4) == 0) DeadKeyState = 0;
        buffer.Clear();
        if (NativeDeadKeys.Contains((byte)vk))
        {
            buffer.Append('^'); // Windows écrit le caractère mort, puis signale par -1.
            return -1;
        }
        buffer.Append(state[0x14] == 0 ? 'a' : 'A');
        return 1;
    }

    public uint SendInput(Win32.INPUT[] inputs)
    {
        SendInputCalls.Add(inputs.ToArray()); // copy défensif
        return SendInputResults.Count > 0 ? SendInputResults.Dequeue() : SendInputResult ?? (uint)inputs.Length;
    }

    /// <summary>Fenêtres rendues par les prochains appels à GetForegroundWindow, dans l'ordre.
    /// Vide, le mock rend <see cref="ForegroundWindow"/>. Permet de reproduire une fenêtre
    /// qui change entre deux lectures d'un même Recompute.</summary>
    public Queue<IntPtr> ForegroundWindowScript { get; } = new();

    /// <summary>Nombre de lectures de GetForegroundWindow depuis la création du mock.
    /// Un témoin qui scripte une file de fenêtres doit vérifier qu'elle a été **consommée** :
    /// sans ce compteur, un test peut empiler des fenêtres que le code ne lit jamais et
    /// passer avec ou sans le correctif qu'il prétend prouver (constat du 2026-09-21).</summary>
    public int ForegroundWindowReads { get; private set; }

    public IntPtr GetForegroundWindow()
    {
        ForegroundWindowReads++;
        return ForegroundWindowScript.Count > 0 ? ForegroundWindowScript.Dequeue() : ForegroundWindow;
    }

    public bool TryGetWindowProcess(IntPtr window, out string? processName, out string? fullPath, out IntPtr hkl, out uint pid)
    {
        if (ShouldFailForegroundInspection)
        {
            processName = null;
            fullPath = null;
            hkl = CurrentHkl;
            pid = ScriptedPid;
            return false;
        }

        processName = ScriptedProcessName;
        fullPath = ScriptedFullPath;
        hkl = CurrentHkl;
        pid = processName != null ? ScriptedPid : 0;
        return processName != null;
    }

    public bool TryEnumProcessModules(uint pid, out string[] moduleFileNames)
    {
        moduleFileNames = ScriptedModules ?? Array.Empty<string>();
        return ScriptedModules != null;
    }

    public bool TryGetProcessStartTime(uint pid, out long startTimeTicks)
    {
        startTimeTicks = ScriptedProcessStartTime;
        return pid != 0 && startTimeTicks != 0;
    }

    public bool IsWindowPasswordField(IntPtr window) => ScriptedSecureInput;

    public IntPtr SetWinEventHook(uint eventMin, uint eventMax, Win32.WinEventDelegate cb)
    {
        if (ShouldFailSetWinEventHook) return IntPtr.Zero;
        CapturedWinEventDelegate = cb;
        return WinEventHookHandle;
    }

    public bool UnhookWinEvent(IntPtr hook)
    {
        UnhookWinEventCalled = true;
        return true;
    }
}

internal sealed class TestWindowsTypingHost : IWindowsTypingHost
{
    private readonly Dictionary<string, string> _overrides =
        new(StringComparer.OrdinalIgnoreCase);

    public uint ShortcutCharacterSearchVk { get; set; }
    public uint ShortcutVirtualKeyboardVk { get; set; }
    public bool CompatibilityDebugLog { get; set; }
    public List<string> EmittedTexts { get; } = new();

    public void SetCompatibilityOverride(string processName, string mode) =>
        _overrides[processName] = mode;

    public string? GetCompatibilityOverride(string processName) =>
        _overrides.TryGetValue(processName, out var mode) ? mode : null;

    public string AnonymizeProcessName(string? processName) => processName ?? "unknown";
    public void RecordEmittedText(string text) => EmittedTexts.Add(text);
    public void Log(string context, Exception exception) { }
    public void LogCompatibilityEvent(string eventName, string details) { }

    /// <summary>Evenements critiques recus, dans l'ordre (AG130-09).</summary>
    public List<(string EventName, string Details)> CriticalEvents { get; } = new();

    public void LogCompatibilityCriticalEvent(string eventName, string details) =>
        CriticalEvents.Add((eventName, details));
}
