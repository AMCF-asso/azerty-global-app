using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using AZERTYGlobal;
using TypingEngine.Windows;
using TypingEngine.Windows.Testing;
using Xunit;
using Xunit.Abstractions;
using W = TypingEngine.Windows.Win32;

namespace AZERTYGlobal.Tests;

// ════════════════════════════════════════════════════════════════════════════════════════
// Audit par frappe au hasard (app 1.3.0, 16/11/2026, docs/audit-frappe-hasard.md).
//
// Le vrai rappel KeyboardHook.HookCallback (délégué privé _proc, lu par réflexion comme dans
// HookCallbackTests) reçoit des suites de touches tirées au hasard, sur la vraie disposition
// (LayoutLoader.LoadFromResource) et le vrai KeyMapper. Seuls Windows et l'hôte sont simulés :
//   - SystemeSimule : état logique des touches (événements transmis + injections), Verr. maj,
//     Verr. num, résultat de SendInput (échec partiel possible). Deux modèles de
//     GetAsyncKeyState : « physique » (celui des tests existants) et « logique » (Windows réel :
//     une touche bloquée par le hook n'y entre pas, une injection y entre).
//   - Hôte : le geste Ctrl+Maj+Verr.maj, la pause et le premier plan suivent TrayApplication
//     (ApplyHookState, OnToggleShortcut, OnForegroundChanged → RefreshForegroundContext).
// Graines : SplitMix64 maison, une graine par suite, rejouable (Rejouer).
// ════════════════════════════════════════════════════════════════════════════════════════

/// <summary>PRNG SplitMix64 : déterministe, indépendant de System.Random (qui change selon les versions).</summary>
internal sealed class SplitMix64
{
    private ulong _s;
    public SplitMix64(ulong seed) => _s = seed;
    public ulong Next()
    {
        ulong z = _s += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
    public int Below(int n) => (int)(Next() % (ulong)n);
    public bool Chance(int pourMille) => Below(1000) < pourMille;
}

internal enum Genre { Caractere, Declencheur, Controle, SansCaractere, Pave, Maj, Ctrl, Alt, AltGr, Win, VerrMaj }

internal sealed record Touche(string Nom, uint Vk, uint Sc, bool Ext, Genre Genre)
{
    public bool EstModificateur => Genre is Genre.Maj or Genre.Ctrl or Genre.Alt or Genre.AltGr or Genre.VerrMaj;
}

internal enum TypeAction { Bas, Haut, HautPerdu, Temps, PremierPlan, FenetreSansRecalcul, Securise, Pause, Sync, EchecEnvoi, Reglages }

internal readonly record struct ActionFrappe(TypeAction Type, int A = 0, int B = 0)
{
    public override string ToString() => Type switch
    {
        TypeAction.Bas => $"Bas({Clavier.Touches[A].Nom})",
        TypeAction.Haut => $"Haut({Clavier.Touches[A].Nom})",
        TypeAction.HautPerdu => $"HautPerdu({Clavier.Touches[A].Nom})",
        TypeAction.Temps => $"Temps({A} ms)",
        TypeAction.PremierPlan => $"PremierPlan({Clavier.Applis[A].Nom})",
        TypeAction.FenetreSansRecalcul => $"FenetreSansRecalcul({Clavier.Applis[A].Nom})",
        TypeAction.Securise => $"Securise({A == 1})",
        TypeAction.Pause => $"Pause({A == 1})",
        TypeAction.EchecEnvoi => $"EchecEnvoi(rend {A})",
        TypeAction.Reglages => $"Reglages(actif={A == 1}, doubleTap={B} ms)",
        _ => Type.ToString()
    };
}

internal sealed record Appli(string Nom, string Processus, uint Pid, long Debut, IntPtr Fenetre);

internal static class Clavier
{
    public static readonly Touche[] Touches =
    {
        new("a", 0x41, 0x10, false, Genre.Caractere), new("z", 0x5A, 0x11, false, Genre.Caractere),
        new("e", 0x45, 0x12, false, Genre.Caractere), new("r", 0x52, 0x13, false, Genre.Caractere),
        new("t", 0x54, 0x14, false, Genre.Caractere), new("q", 0x51, 0x1E, false, Genre.Caractere),
        new("s", 0x53, 0x1F, false, Genre.Caractere), new("d", 0x44, 0x20, false, Genre.Caractere),
        new("w", 0x57, 0x2C, false, Genre.Caractere), new("c", 0x43, 0x2E, false, Genre.Caractere),
        new("n", 0x4E, 0x31, false, Genre.Caractere), new("m", 0x4D, 0x27, false, Genre.Caractere),
        new("1", 0x31, 0x02, false, Genre.Caractere), new("2", 0x32, 0x03, false, Genre.Caractere),
        new("0", 0x30, 0x0B, false, Genre.Caractere), new("SC00C", 0xDB, 0x0C, false, Genre.Caractere),
        new("SC00D", 0xBB, 0x0D, false, Genre.Caractere), new("SC01A", 0xDD, 0x1A, false, Genre.Caractere),
        new("SC01B", 0xBA, 0x1B, false, Genre.Caractere), new("SC028", 0xC0, 0x28, false, Genre.Caractere),
        new("SC029", 0xDE, 0x29, false, Genre.Caractere), new("SC032", 0xBC, 0x32, false, Genre.Caractere),
        new("SC033", 0xBE, 0x33, false, Genre.Caractere), new("SC035", 0xDF, 0x35, false, Genre.Caractere),
        new("SC056", 0xE2, 0x56, false, Genre.Caractere), new("Espace", 0x20, 0x39, false, Genre.Caractere),
        new("SC02B", 0xDC, 0x2B, false, Genre.Declencheur),
        new("RetourArriere", 0x08, 0x0E, false, Genre.Controle), new("Tab", 0x09, 0x0F, false, Genre.Controle),
        new("Entree", 0x0D, 0x1C, false, Genre.Controle), new("Echap", 0x1B, 0x01, false, Genre.Controle),
        new("Gauche", 0x25, 0x4B, true, Genre.SansCaractere), new("Droite", 0x27, 0x4D, true, Genre.SansCaractere),
        new("Debut", 0x24, 0x47, true, Genre.SansCaractere), new("Fin", 0x23, 0x4F, true, Genre.SansCaractere),
        new("Suppr", 0x2E, 0x53, true, Genre.SansCaractere), new("F5", 0x74, 0x3F, false, Genre.SansCaractere),
        new("Pave1", 0x61, 0x4F, false, Genre.Pave), new("Pave5", 0x65, 0x4C, false, Genre.Pave),
        new("PaveDiv", 0x6F, 0x35, true, Genre.Pave), new("PaveEntree", 0x0D, 0x1C, true, Genre.Controle),
        new("MajG", 0xA0, 0x2A, false, Genre.Maj), new("MajD", 0xA1, 0x36, false, Genre.Maj),
        new("CtrlG", 0xA2, 0x1D, false, Genre.Ctrl), new("CtrlD", 0xA3, 0x1D, true, Genre.Ctrl),
        new("Alt", 0xA4, 0x38, false, Genre.Alt), new("AltGr", 0xA5, 0x38, true, Genre.AltGr),
        new("Win", 0x5B, 0x5B, true, Genre.Win), new("VerrMaj", 0x14, 0x3A, false, Genre.VerrMaj),
    };

    public static readonly Appli[] Applis =
    {
        new("Bloc-notes", "notepad.exe", 4242, 111, (IntPtr)0x1001),
        new("Word", "winword.exe", 5150, 222, (IntPtr)0x1002),
        new("Bloc-notes PID réutilisé", "notepad.exe", 4242, 333, (IntPtr)0x1003),
        new("Explorateur", "explorer.exe", 600, 444, (IntPtr)0x1004),
    };

    public static int Index(string nom) => Array.FindIndex(Touches, t => t.Nom == nom);
}

/// <summary>Hôte qui garde les exceptions avalées par le try/catch du rappel (SEV-A2-04).</summary>
internal sealed class HoteEspion : IWindowsTypingHost
{
    public uint ShortcutCharacterSearchVk => 0x57;     // Ctrl+Maj+W, réglage par défaut
    public uint ShortcutVirtualKeyboardVk => 0x51;     // Ctrl+Maj+Q
    public bool CompatibilityDebugLog => false;
    public List<string> Exceptions { get; } = new();
    public string? GetCompatibilityOverride(string processName) => null;
    public string AnonymizeProcessName(string? processName) => processName ?? "unknown";
    public void RecordEmittedText(string text) { }
    public void Log(string context, Exception exception) => Exceptions.Add($"{context}: {exception.GetType().Name}: {exception.Message}");
    public void LogCompatibilityEvent(string eventName, string details) { }
    public void LogCompatibilityCriticalEvent(string eventName, string details) { }
}

/// <summary>
/// Windows simulé autour de MockWin32Api : l'état logique des touches suit les événements que
/// le hook transmet et les injections ; Verr. maj et Verr. num basculent à l'appui logique.
/// </summary>
internal sealed class SystemeSimule : IWin32Api
{
    private const uint KEYEVENTF_KEYUP = 0x2, KEYEVENTF_UNICODE = 0x4, KEYEVENTF_SCANCODE = 0x8;
    public readonly MockWin32Api Mock;
    public readonly bool AsyncLogique;
    public readonly HashSet<uint> Logique = new();
    public readonly HashSet<uint> Physique = new();
    public bool Caps, NumLock = true;
    public readonly Queue<uint> ResultatsForces = new();
    public int Envois;
    public int ToucheEnvois;

    public SystemeSimule(MockWin32Api mock, bool asyncLogique) { Mock = mock; AsyncLogique = asyncLogique; }

    public static uint Normaliser(uint vk) => vk switch { 0x10 => 0xA0, 0x11 => 0xA2, 0x12 => 0xA4, _ => vk };

    public void AppliquerLogique(uint vk, bool bas)
    {
        vk = Normaliser(vk);
        if (bas)
        {
            if (vk == 0x14 && !Logique.Contains(0x14)) Caps = !Caps;
            if (vk == 0x90 && !Logique.Contains(0x90)) NumLock = !NumLock;
            Logique.Add(vk);
        }
        else Logique.Remove(vk);
    }

    private bool Tenue(HashSet<uint> e, int vk) => vk switch
    {
        0x10 => e.Contains(0xA0) || e.Contains(0xA1),
        0x11 => e.Contains(0xA2) || e.Contains(0xA3),
        0x12 => e.Contains(0xA4) || e.Contains(0xA5),
        _ => e.Contains((uint)vk)
    };

    public short GetAsyncKeyState(int vk) => Tenue(AsyncLogique ? Logique : Physique, vk) ? unchecked((short)0x8000) : (short)0;

    public short GetKeyState(int vk)
    {
        int v = Tenue(Logique, vk) ? 0x8000 : 0;
        if (vk == 0x14 && Caps) v |= 1;
        if (vk == 0x90 && NumLock) v |= 1;
        return unchecked((short)v);
    }

    public uint SendInput(W.INPUT[] inputs)
    {
        Envois++;
        uint n = ResultatsForces.Count > 0 ? Math.Min(ResultatsForces.Dequeue(), (uint)inputs.Length) : (uint)inputs.Length;
        Mock.SendInputCalls.Add(inputs.ToArray());
        for (int i = 0; i < n; i++)
        {
            if (inputs[i].type != 1) continue;
            var ki = inputs[i].u.ki;
            if ((ki.dwFlags & KEYEVENTF_UNICODE) != 0) continue;
            ToucheEnvois++;
            uint vk = ki.wVk;
            if (vk == 0 && (ki.dwFlags & KEYEVENTF_SCANCODE) != 0)
            {
                var t = Array.Find(Clavier.Touches, k => k.Sc == ki.wScan);
                vk = t?.Vk ?? (0x10000u | ki.wScan);
            }
            AppliquerLogique(vk, (ki.dwFlags & KEYEVENTF_KEYUP) == 0);
        }
        return n;
    }

    public int LastSendInputError => 0;
    public short VkKeyScanExW(char ch, IntPtr hkl) => Mock.VkKeyScanExW(ch, hkl);
    public uint MapVirtualKeyExW(uint code, uint mapType, IntPtr hkl) => Mock.MapVirtualKeyExW(code, mapType, hkl);
    public IntPtr GetKeyboardLayout(uint threadId) => Mock.GetKeyboardLayout(threadId);
    public int ToUnicode(uint vk, uint scan, byte[] state, StringBuilder buffer, int capacity, uint flags) =>
        Mock.ToUnicode(vk, scan, state, buffer, capacity, flags);
    public int ToUnicodeEx(uint vk, uint scan, byte[] state, StringBuilder buffer, int capacity, uint flags, IntPtr hkl) =>
        Mock.ToUnicodeEx(vk, scan, state, buffer, capacity, flags, hkl);
    public IntPtr GetForegroundWindow() => Mock.GetForegroundWindow();
    public bool TryGetWindowProcess(IntPtr window, out string? processName, out string? fullPath, out IntPtr hkl, out uint pid) =>
        Mock.TryGetWindowProcess(window, out processName, out fullPath, out hkl, out pid);
    public bool TryGetProcessStartTime(uint pid, out long startTimeTicks) => Mock.TryGetProcessStartTime(pid, out startTimeTicks);
    public bool IsWindowPasswordField(IntPtr window) => Mock.IsWindowPasswordField(window);
    public bool TryEnumProcessModules(uint pid, out string[] moduleFileNames) => Mock.TryEnumProcessModules(pid, out moduleFileNames);
    public IntPtr SetWinEventHook(uint eventMin, uint eventMax, W.WinEventDelegate cb) => Mock.SetWinEventHook(eventMin, eventMax, cb);
    public bool UnhookWinEvent(IntPtr hook) => Mock.UnhookWinEvent(hook);
}

internal readonly record struct Violation(string Regle, int Etape, string Detail);

/// <summary>Une suite jouée : moteur réel, Windows et hôte simulés, oracle vérifié à chaque pas.</summary>
internal sealed class BancFrappe : IDisposable
{
    private const uint WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;

    public readonly MockWin32Api Mock;
    public readonly SystemeSimule Sys;
    public readonly HoteEspion Hote = new();
    public readonly ForegroundMonitor Moniteur;
    public readonly KeyMapper Mapper;
    public readonly KeyboardHook Hook;
    public int RaccourcisServis { get; private set; }
    private readonly W.LowLevelKeyboardProc _proc;
    private readonly IntPtr _lParam = Marshal.AllocHGlobal(Marshal.SizeOf<W.KBDLLHOOKSTRUCT>());
    private readonly object _composition;
    private readonly PropertyInfo _compositionArmee;
    private readonly FieldInfo _reparation;

    private long _maintenant = 10_000_000;
    /// <summary>Heure simulée, aussi portée par KBDLLHOOKSTRUCT.time de chaque rappel.</summary>
    public uint Heure => unchecked((uint)_maintenant);
    private bool _utilisateurActif = true, _pause;
    private int _epoque;
    private int _appli;
    private readonly HashSet<int> _tenues = new();
    private readonly HashSet<int> _repetitionCoupee = new();   // tenues pendant un passage : relâche seule
    private readonly Dictionary<(uint, uint, bool), (long Decision, int Epoque, bool Normal)> _decisions = new();
    private readonly HashSet<(uint Pid, long Debut, string Couche)> _verrousVus = new();
    private bool _modifsDesynchro;
    private int _etape;
    public readonly List<Violation> Violations = new();
    public readonly HashSet<string> Desactivees = new();

    public BancFrappe(bool asyncLogique)
    {
        var a = Clavier.Applis[0];
        Mock = new MockWin32Api
        {
            ScriptedProcessName = a.Processus, ScriptedFullPath = @"C:\Apps\" + a.Processus,
            ScriptedModules = new[] { "kernel32.dll" }, ScriptedPid = a.Pid, ScriptedProcessStartTime = a.Debut,
            ForegroundWindow = a.Fenetre
        };
        Sys = new SystemeSimule(Mock, asyncLogique);
        Moniteur = new ForegroundMonitor(Sys, IntPtr.Zero, Hote, () => _maintenant);
        Mapper = new KeyMapper(LayoutLoader.LoadFromResource(), Sys, Hote);
        // Horloge du gestionnaire de couches : celle du banc (double appui, accord tenu).
        var couches = typeof(KeyMapper).GetField("_maintainableLayers", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Mapper)!;
        couches.GetType().GetField("_clockMilliseconds", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(couches, (Func<long>)(() => _maintenant));
        Mapper.SetForegroundMonitor(Moniteur);
        Moniteur.ForegroundChanged += Mapper.RefreshForegroundContext;     // OnForegroundChanged
        Mapper.ToggleRequested += SurBascule;                              // OnToggleShortcut
        Mapper.ApplyMaintainableLayerSettings(true, new[] { "dk_greek", "dk_cyrillic", "dk_scientific" }, 500);
        Hook = new KeyboardHook(Mapper, Hote);
        Hook.SearchRequested += () => RaccourcisServis++;                    // fenêtres servies par le hook
        Hook.VirtualKeyboardRequested += () => RaccourcisServis++;
        _proc = (W.LowLevelKeyboardProc)typeof(KeyboardHook)
            .GetField("_proc", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Hook)!;
        _composition = typeof(KeyMapper).GetField("_composition", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Mapper)!;
        _compositionArmee = _composition.GetType().GetProperty("ActiveDeadKey")!;
        _reparation = typeof(KeyMapper).GetField("_pendingInputRecovery", BindingFlags.NonPublic | BindingFlags.Instance)!;
        AppliquerEtatHook(sync: true, preserve: false);
    }

    public void Dispose()
    {
        Hook.Dispose();
        Moniteur.Dispose();
        Marshal.FreeHGlobal(_lParam);
    }

    public string? Composition => (string?)_compositionArmee.GetValue(_composition);
    private bool ReparationEnAttente => _reparation.GetValue(Mapper) != null;
    // Hors suspension (pause, hook coupé, fenêtre non recalculée) : M1.4 I4 et M1.3 I3 l'exceptent.
    private bool Normal => Hook.Enabled && !Hook.PassThroughAll && !Mapper.EmissionPaused
        && Moniteur.GetEmitContext().Mode != CompatibilityMode.DisabledAntiCheat;
    // État physique au niveau des VK vus du hook (Ctrl fantôme d'AltGr compris) : c'est ce que
    // Windows suit, et un Ctrl gauche relâché pendant AltGr est relâché pour lui aussi.
    private bool P(uint vk) => Sys.Physique.Contains(vk);
    // Relâchements possédés en attente de la reprise sûre (M1.4 I2 : « ou à la reprise sûre »).
    private bool RelachesEnAttente =>
        ((System.Collections.ICollection)typeof(KeyMapper).GetField("_syntheticVirtualKeyDowns", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Mapper)!).Count > 0
        || ((HashSet<uint>)typeof(KeyMapper).GetField("_passedThroughKeys", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Mapper)!).Count > 0;
    private bool Tenue(Genre g) => _tenues.Any(i => Clavier.Touches[i].Genre == g);
    private bool TenueNom(string nom) => _tenues.Contains(Clavier.Index(nom));

    // ── Hôte (TrayApplication.ApplyHookState, OnToggleShortcut) ──────────────────────────
    private void AppliquerEtatHook(bool sync, bool preserve)
    {
        Mapper.EmissionPaused = _pause;
        Hook.PassThroughAll = _pause;
        Hook.ShortcutsWhilePassThrough = _pause;
        Hook.Enabled = _utilisateurActif && !_pause;
        if (sync && Hook.Enabled) Mapper.SyncState(preserve);
        _epoque++;
    }

    private void SurBascule()
    {
        if (_pause) { _pause = false; AppliquerEtatHook(sync: true, preserve: true); return; }
        _utilisateurActif = !_utilisateurActif;
        AppliquerEtatHook(sync: true, preserve: false);
    }

    // ── Un événement physique dans le rappel ─────────────────────────────────────────────
    private long Rappel(uint vk, uint sc, bool ext, bool bas)
    {
        bool altSeul = (Sys.Physique.Contains(0xA4)) && !Sys.Physique.Contains(0xA2) && !Sys.Physique.Contains(0xA3);
        uint msg = bas ? (altSeul ? WM_SYSKEYDOWN : WM_KEYDOWN) : (altSeul ? WM_SYSKEYUP : WM_KEYUP);
        uint flags = (ext ? 1u : 0u) | (bas ? 0u : 0x80u) | (altSeul ? 0x20u : 0u);
        Marshal.StructureToPtr(new W.KBDLLHOOKSTRUCT { vkCode = vk, scanCode = sc, flags = flags, time = unchecked((uint)_maintenant) }, _lParam, false);
        long r = _proc(0, (IntPtr)msg, _lParam).ToInt64();
        if (bas) Sys.Physique.Add(vk); else Sys.Physique.Remove(vk);
        if (r == 0) Sys.AppliquerLogique(vk, bas);
        _maintenant += 15;
        return r;
    }

    /// <summary>Un événement physique simple (pas AltGr), pour les tests nommés : rend la décision du hook.</summary>
    public long Frapper(string nom, bool bas)
    {
        int i = Clavier.Index(nom);
        var t = Clavier.Touches[i];
        if (bas) _tenues.Add(i); else _tenues.Remove(i);
        return Rappel(t.Vk, t.Sc, t.Ext, bas);
    }

    private void Evenement(uint vk, uint sc, bool ext, bool bas, bool repetition)
    {
        var cle = (vk, sc, ext);
        bool normal = Normal;
        int epoque = _epoque;
        long r = Rappel(vk, sc, ext, bas);
        // Une répétition pendant une suspension passe (TrackPassThroughKey) : la cible a reçu ces appuis,
        // la suite doit rester cohérente pour elle, plus avec la décision initiale (exception de suspension).
        if (bas && (!repetition || !normal)) { _decisions[cle] = (r, epoque, normal && !repetition); return; }
        if (!_decisions.TryGetValue(cle, out var d)) return;
        // M1.4 I1 (répétition) et I3/I4 (relâche) : même décision que l'appui, à hôte inchangé.
        if (d.Epoque == epoque && d.Epoque == _epoque && d.Normal && normal && d.Decision != r)
        {
            bool ctrl = P(0xA2) || P(0xA3), maj = P(0xA0) || P(0xA1);
            string regle = bas ? "M1.4-I1" : (d.Decision == 0 ? "M1.4-I3" : "M1.4-I4");
            // Signatures des constats démontrés par un [Fact] nommé (voir FrappeHasardTests).
            if (bas && ctrl && maj && vk is 0x51 or 0x57) regle += "·AFH-3";
            else if (!bas && d.Decision == 1) regle += "·AFH-1";
            Signaler(regle, $"{(bas ? "répétition" : "relâche")} de VK {vk:X2}/SC {sc:X2} rend {r}, l'appui rendait {d.Decision}");
        }
        if (!bas) _decisions.Remove(cle);
    }

    private void Signaler(string regle, string detail)
    {
        if (!Desactivees.Contains(regle)) Violations.Add(new Violation(regle, _etape, detail));
    }

    // ── Jouer une action et vérifier l'oracle ────────────────────────────────────────────
    public void Jouer(ActionFrappe act)
    {
        var modeAvant = Mapper.MaintainableLayerState.Mode;
        string? compAvant = Composition;
        bool declencheurTenu = TenueNom("SC02B");
        bool normalAvant = Normal;
        int envoisAvant = Sys.Envois;
        int raccourcisAvant = RaccourcisServis;
        // Raccourci au sens du moteur (KeyMapper.cs:737-740) : Ctrl sans AltGr, Alt gauche sans Ctrl droit, Win.
        bool altGrTenu = P(0xA5) || (P(0xA3) && P(0xA4));
        bool ctrlTenu = ((P(0xA2) || P(0xA3)) && !altGrTenu) || (P(0xA4) && !P(0xA3)) || P(0x5B);
        Touche? frappee = null;
        bool fraiche = false;

        try
        {
            switch (act.Type)
            {
                case TypeAction.Bas:
                {
                    var t = Clavier.Touches[act.A];
                    // Touche tenue pendant un passage par l'écran sécurisé : les touches tapées
                    // là-bas (Ctrl+Alt+Suppr, Win+L, mot de passe, Échap) ont coupé sa répétition.
                    if (_repetitionCoupee.Contains(act.A)) break;
                    bool rep = _tenues.Contains(act.A);
                    fraiche = !rep;
                    frappee = t;
                    if (t.Genre == Genre.AltGr)
                    {
                        Evenement(0xA2, 0x21D, false, true, rep);   // Ctrl gauche fantôme de AltGr
                        Evenement(0xA5, 0x38, true, true, rep);
                    }
                    else Evenement(t.Vk, t.Sc, t.Ext, true, rep);
                    _tenues.Add(act.A);
                    break;
                }
                case TypeAction.Haut:
                {
                    if (!_tenues.Remove(act.A)) break;
                    _repetitionCoupee.Remove(act.A);   // relâche tardive éventuelle : émise
                    var t = Clavier.Touches[act.A];
                    if (t.Genre == Genre.AltGr)
                    {
                        Evenement(0xA2, 0x21D, false, false, false);
                        Evenement(0xA5, 0x38, true, false, false);
                    }
                    else Evenement(t.Vk, t.Sc, t.Ext, false, false);
                    break;
                }
                case TypeAction.HautPerdu:
                {
                    if (!_tenues.Remove(act.A)) break;
                    var t = Clavier.Touches[act.A];
                    // Relâche jamais vue du hook, faite sur un écran sécurisé (verrouillage, UAC,
                    // Ctrl+Alt+Suppr) : Windows la connaît, et l'app apprend le passage par
                    // EVENT_SYSTEM_DESKTOPSWITCH ou WM_WTSSESSION_CHANGE (TrayApplication.ForgetHeldKeys).
                    // Une relâche perdue sans aucun signal n'est pas modélisée (AFH-4, § 3 du doc),
                    // ni un passage fait seulement à la souris (UAC validé d'un clic), où la
                    // dernière touche tenue continuerait de se répéter au retour.
                    _repetitionCoupee.UnionWith(_tenues);
                    Hook.ForgetHeldKeys(unchecked((uint)_maintenant));
                    foreach (var vk in t.Genre == Genre.AltGr ? new uint[] { 0xA2, 0xA5 } : new[] { t.Vk })
                    {
                        Sys.Physique.Remove(vk);
                        Sys.AppliquerLogique(vk, false);
                        _decisions.Remove((vk, t.Genre == Genre.AltGr && vk == 0xA2 ? 0x21Du : t.Sc, vk == 0xA5 || t.Ext));
                    }
                    if (t.EstModificateur || t.Genre == Genre.Win) _modifsDesynchro = true;
                    break;
                }
                case TypeAction.Temps: _maintenant += act.A; break;
                case TypeAction.PremierPlan:
                case TypeAction.FenetreSansRecalcul:
                {
                    var a = Clavier.Applis[act.A];
                    Mock.ScriptedProcessName = a.Processus;
                    Mock.ScriptedFullPath = @"C:\Apps\" + a.Processus;
                    Mock.ScriptedPid = a.Pid;
                    Mock.ScriptedProcessStartTime = a.Debut;
                    Mock.ForegroundWindow = a.Fenetre;
                    if (act.Type == TypeAction.PremierPlan) { Moniteur.Recompute(); _appli = act.A; }
                    break;
                }
                case TypeAction.Securise:
                    Mock.ScriptedSecureInput = act.A == 1;
                    Moniteur.Recompute();
                    break;
                case TypeAction.Pause:
                    if ((act.A == 1) == _pause) break;
                    _pause = act.A == 1;
                    AppliquerEtatHook(sync: !_pause, preserve: true);
                    break;
                case TypeAction.Sync:
                    if (Hook.Enabled) { Mapper.SyncState(); _modifsDesynchro = false; }
                    break;
                case TypeAction.EchecEnvoi:
                    Sys.ResultatsForces.Enqueue((uint)act.A);
                    break;
                case TypeAction.Reglages:
                    Mapper.ApplyMaintainableLayerSettings(act.A == 1, new[] { "dk_greek", "dk_cyrillic", "dk_scientific" }, act.B);
                    break;
            }
        }
        catch (Exception ex)
        {
            Signaler("EXCEPTION", $"{act}: {ex.GetType().Name}: {ex.Message}");
        }

        foreach (var e in Hote.Exceptions) Signaler("EXCEPTION", e);
        Hote.Exceptions.Clear();
        Verifier(act, modeAvant, compAvant, declencheurTenu, normalAvant, envoisAvant, raccourcisAvant, ctrlTenu, frappee, fraiche);
        _etape++;
    }

    private void Verifier(ActionFrappe act, MaintainableLayerMode modeAvant, string? compAvant, bool declencheurTenu,
        bool normalAvant, int envoisAvant, int raccourcisAvant, bool ctrlTenu, Touche? frappee, bool fraiche)
    {
        var etat = Mapper.MaintainableLayerState;
        string? comp = Composition;

        // M1.2 I6 : un coup et une composition ne coexistent jamais.
        if (comp != null && etat.Mode == MaintainableLayerMode.OneShot)
            Signaler("M1.2-I6", $"après {act} : composition {comp} et coup {etat.LayerId} armés ensemble");

        // M1.2 I4 : en saisie sécurisée, aucune couche active, rien d'armé.
        if (Moniteur.IsSecureInput && etat.IsActive)
            Signaler("M1.2-I4", $"après {act} : couche {etat.LayerId} ({etat.Mode}) en saisie sécurisée");

        // M1.2 I2 : un coup ou un verrou ne suit pas l'utilisateur dans un autre processus.
        var id = Moniteur.CurrentIdentity;
        if (etat.Mode == MaintainableLayerMode.Locked && act.Type is TypeAction.Bas or TypeAction.Haut)
            _verrousVus.Add((id.ProcessId, id.StartTimeTicks, etat.LayerId!));
        if (act.Type == TypeAction.PremierPlan && id != _identiteCourante)
        {
            if (etat.Mode == MaintainableLayerMode.OneShot && modeAvant == MaintainableLayerMode.OneShot)
                Signaler("M1.2-I2", $"après {act} : le coup {etat.LayerId} a suivi le changement de processus");
            if (etat.Mode == MaintainableLayerMode.Locked && !_verrousVus.Contains((id.ProcessId, id.StartTimeTicks, etat.LayerId!)))
                Signaler("M1.2-I2", $"après {act} : verrou {etat.LayerId} jamais posé dans ce processus");
        }

        if (act.Type == TypeAction.Bas && fraiche && frappee != null && normalAvant)
        {
            // M1.2 I5 : toute touche autre que Maj, Ctrl, Alt, AltGr, Verr. maj consomme ou annule le coup.
            if (modeAvant == MaintainableLayerMode.OneShot && !declencheurTenu && !frappee.EstModificateur
                && frappee.Genre != Genre.Declencheur && etat.Mode == MaintainableLayerMode.OneShot)
                Signaler(Sys.Envois == envoisAvant && frappee.Genre == Genre.Caractere ? "M1.2-I5·AFH-5" : "M1.2-I5", $"{act} laisse le coup {etat.LayerId} armé");

            // M1.1 I3 : Retour arrière, Entrée, Tab, Échap, raccourci abandonnent la touche morte.
            bool abandonne = frappee.Genre == Genre.Controle || (ctrlTenu && !frappee.EstModificateur && frappee.Genre != Genre.Win);
            if (compAvant != null && abandonne && comp != null)
                Signaler(ctrlTenu && (P(0xA0) || P(0xA1)) && frappee.Vk is 0x51 or 0x57 ? "M1.1-I3·AFH-2" : "M1.1-I3", $"{act}{(ctrlTenu ? " (raccourci)" : "")} laisse la touche morte {comp} armée");

            // M1.3 I3 : Verr. maj suivi = Verr. maj système avant tout choix de caractère.
            // Un raccourci servi par le hook (recherche, clavier virtuel) ne choisit aucun caractère.
            if (frappee.Genre is Genre.Caractere && RaccourcisServis == raccourcisAvant && Mapper.CapsLockActive != Sys.Caps)
                Signaler("M1.3-I3", $"après {act} : Verr. maj suivi {Mapper.CapsLockActive}, système {Sys.Caps}");

            if (!frappee.EstModificateur && frappee.Genre != Genre.Win && !ReparationEnAttente) _modifsDesynchro = false;
        }

        // M1.1 I3 / M1.5 I3 : l'arrivée dans une autre application abandonne la touche morte.
        if (act.Type == TypeAction.PremierPlan && compAvant != null && comp != null
            && Clavier.Applis[act.A].Processus != "explorer.exe" && Mapper.EmissionPaused == false)
        {
            // L'identité suivie est la dernière application utile (l'Explorateur n'en est pas une).
            var derniere = Moniteur.LastApplicationIdentity;
            if (derniere.ProcessId == Clavier.Applis[act.A].Pid && derniere.StartTimeTicks == Clavier.Applis[act.A].Debut
                && _identiteAvantPremierPlan != derniere)
                Signaler("M1.1-I3", $"après {act} : touche morte {comp} gardée dans une autre application");
        }
        _identiteAvantPremierPlan = Moniteur.LastApplicationIdentity;
        _identiteCourante = Moniteur.CurrentIdentity;

        // M1.3 I1 : le suivi des modificateurs reflète le clavier physique (hors relâche perdue
        // non encore resynchronisée, M1.3 I2).
        if (!_modifsDesynchro)
        {
            bool maj = P(0xA0) || P(0xA1), ctrl = P(0xA2) || P(0xA3), alt = P(0xA4);
            bool altGr = P(0xA5) || (P(0xA3) && alt);
            if (Mapper.ShiftDown != maj) Signaler("M1.3-I1", $"après {act} : Maj suivi {Mapper.ShiftDown}, physique {maj}");
            if (Mapper.CtrlDown != ctrl) Signaler("M1.3-I1", $"après {act} : Ctrl suivi {Mapper.CtrlDown}, physique {ctrl}");
            if (Mapper.AltDown != alt) Signaler("M1.3-I1", $"après {act} : Alt suivi {Mapper.AltDown}, physique {alt}");
            if (Mapper.AltGrDown != altGr) Signaler("M1.3-I1", $"après {act} : AltGr suivi {Mapper.AltGrDown}, physique {altGr}");
        }

        // M1.5 I1 : rien n'est émis pendant une suspension (pause, ou fenêtre non encore recalculée).
        if (Sys.Envois != envoisAvant && (Mapper.EmissionPaused || Moniteur.GetEmitContext().Mode == CompatibilityMode.DisabledAntiCheat))
            Signaler("M1.5-I1", $"{act} émet {Sys.Envois - envoisAvant} lot(s) pendant une suspension");

        // Touche collée : clavier physique au repos, hôte actif, aucune réparation en attente,
        // aucune touche ne doit rester enfoncée pour Windows (M1.4 I2, I3, M1.6 I3).
        if (_tenues.Count == 0 && Normal && !ReparationEnAttente && !RelachesEnAttente && Sys.Logique.Count > 0)
            Signaler("TOUCHE-COLLEE", $"après {act} : Windows croit enfoncées {string.Join(",", Sys.Logique.Select(v => v.ToString("X2")))}");
    }

    private ForegroundProcessIdentity _identiteAvantPremierPlan, _identiteCourante;

    /// <summary>Fin de suite : tout relâcher, reprendre, resynchroniser ; puis plus rien d'enfoncé.</summary>
    public void Terminer()
    {
        foreach (var i in _tenues.ToArray()) Jouer(new ActionFrappe(TypeAction.Haut, i));
        if (_pause) Jouer(new ActionFrappe(TypeAction.Pause, 0));
        Jouer(new ActionFrappe(TypeAction.PremierPlan, _appli));
        Jouer(new ActionFrappe(TypeAction.Sync));
    }
}

/// <summary>Génère une suite d'actions d'une graine ; la suite reste jouable après suppressions (réduction).</summary>
internal static class Generateur
{
    private static readonly int[] Caracteres = Enumerable.Range(0, Clavier.Touches.Length)
        .Where(i => Clavier.Touches[i].Genre == Genre.Caractere).ToArray();
    private static readonly int[] Autres = Enumerable.Range(0, Clavier.Touches.Length)
        .Where(i => Clavier.Touches[i].Genre is Genre.Controle or Genre.SansCaractere or Genre.Pave or Genre.Win or Genre.VerrMaj).ToArray();
    private static readonly int[] Modifs = Enumerable.Range(0, Clavier.Touches.Length)
        .Where(i => Clavier.Touches[i].Genre is Genre.Maj or Genre.Ctrl or Genre.Alt or Genre.AltGr).ToArray();
    private static readonly int Declencheur = Clavier.Index("SC02B");

    public static List<ActionFrappe> Suite(ulong graine, int longueur)
    {
        var r = new SplitMix64(graine);
        var tenues = new List<int>();
        var suite = new List<ActionFrappe>(longueur);
        while (suite.Count < longueur)
        {
            int x = r.Below(1000);
            if (x < 430)
            {
                int y = r.Below(100);
                int k = y < 50 ? Caracteres[r.Below(Caracteres.Length)]
                    : y < 62 ? Declencheur
                    : y < 82 ? Modifs[r.Below(Modifs.Length)]
                    : Autres[r.Below(Autres.Length)];
                suite.Add(new ActionFrappe(TypeAction.Bas, k));
                if (!tenues.Contains(k)) tenues.Add(k);
            }
            else if (x < 800)
            {
                if (tenues.Count == 0) continue;
                // Le plus souvent la dernière touche appuyée (frappe ordinaire), parfois une autre.
                int j = r.Chance(650) ? tenues.Count - 1 : r.Below(tenues.Count);
                suite.Add(new ActionFrappe(TypeAction.Haut, tenues[j]));
                tenues.RemoveAt(j);
            }
            else if (x < 900) suite.Add(new ActionFrappe(TypeAction.Temps, r.Chance(500) ? 1 + r.Below(200) : 300 + r.Below(1200)));
            else if (x < 930) suite.Add(new ActionFrappe(TypeAction.PremierPlan, r.Below(Clavier.Applis.Length)));
            else if (x < 940) suite.Add(new ActionFrappe(TypeAction.FenetreSansRecalcul, r.Below(Clavier.Applis.Length)));
            else if (x < 955) suite.Add(new ActionFrappe(TypeAction.Securise, r.Below(2)));
            else if (x < 970) suite.Add(new ActionFrappe(TypeAction.Pause, r.Below(2)));
            else if (x < 978) suite.Add(new ActionFrappe(TypeAction.Sync));
            else if (x < 986)
            {
                if (tenues.Count == 0) continue;
                int j = r.Below(tenues.Count);
                suite.Add(new ActionFrappe(TypeAction.HautPerdu, tenues[j]));
                tenues.RemoveAt(j);
            }
            else if (x < 994) suite.Add(new ActionFrappe(TypeAction.EchecEnvoi, r.Below(2)));
            else suite.Add(new ActionFrappe(TypeAction.Reglages, r.Chance(800) ? 1 : 0, 200 + r.Below(800)));
        }
        return suite;
    }
}

/// <summary>Exécution, réduction et rapport.</summary>
internal static class Execution
{
    public static List<Violation> Jouer(IReadOnlyList<ActionFrappe> suite, bool asyncLogique, IEnumerable<string>? desactivees = null)
    {
        using var banc = new BancFrappe(asyncLogique);
        if (desactivees != null) foreach (var d in desactivees) banc.Desactivees.Add(d);
        foreach (var a in suite) banc.Jouer(a);
        banc.Terminer();
        return banc.Violations;
    }

    /// <summary>Réduction gloutonne (suppression de blocs) tant que la même règle échoue.</summary>
    public static List<ActionFrappe> Reduire(List<ActionFrappe> suite, string regle, bool asyncLogique)
    {
        bool Echoue(List<ActionFrappe> s) => Jouer(s, asyncLogique).Any(v => v.Regle == regle);
        var courante = new List<ActionFrappe>(suite);
        var premier = Jouer(courante, asyncLogique).FirstOrDefault(v => v.Regle == regle);
        if (premier.Regle != null && premier.Etape < courante.Count)
        {
            var tronquee = courante.Take(premier.Etape + 1).ToList();
            if (Echoue(tronquee)) courante = tronquee;
        }
        for (int bloc = Math.Max(1, courante.Count / 2); bloc >= 1; bloc /= 2)
        {
            for (int i = 0; i + bloc <= courante.Count;)
            {
                var essai = new List<ActionFrappe>(courante);
                essai.RemoveRange(i, bloc);
                if (Echoue(essai)) courante = essai; else i += bloc;
            }
            if (bloc == 1) break;
        }
        return courante;
    }
}

public sealed class FrappeHasardTests
{
    private readonly ITestOutputHelper _sortie;
    public FrappeHasardTests(ITestOutputHelper sortie) => _sortie = sortie;

    /// <summary>Graine de base et volume : AG_FRAPPE_GRAINE, AG_FRAPPE_SUITES, AG_FRAPPE_PAS.</summary>
    private static ulong GraineBase => ulong.TryParse(Environment.GetEnvironmentVariable("AG_FRAPPE_GRAINE"), out var g) ? g : 0x41470130UL;
    private static int NombreSuites => int.TryParse(Environment.GetEnvironmentVariable("AG_FRAPPE_SUITES"), out var n) ? n : 400;
    private static int LongueurSuite => int.TryParse(Environment.GetEnvironmentVariable("AG_FRAPPE_PAS"), out var n) ? n : 250;

    /// <summary>
    /// Règles dont un constat est déjà démontré par un [Fact] nommé (voir plus bas) : le banc ne
    /// les recompte pas, pour rester vert sur toutes les autres et servir de garde de régression.
    /// </summary>
    // Retirer une signature quand son correctif est livré : le banc redevient garde de toute la règle.
    // AFH-2, AFH-3 (KeyboardHook : touches servies comme raccourci), AFH-1, AFH-5 (KeyMapper) et
    // AFH-4 (ForgetHeldKeys aux transitions) corrigés le 16/11/2026 : plus aucune signature exclue.
    internal static readonly string[] ConstatsDemontres = Array.Empty<string>();

    // Constat AFH-1, seconde forme (graine 0x4147013A) : AltGr+Espace absorbé (espace insécable
    // émise), AltGr relâché avant Espace, qui se répète : la relâche d'Espace part à l'application.
    [Fact]
    public void ToucheAbsorbeeSousAltGr_AltGrRelacheAvant_LaRelacheResteAbsorbee()
    {
        using var banc = new BancFrappe(asyncLogique: false);
        int altGr = Clavier.Index("AltGr");
        banc.Jouer(new ActionFrappe(TypeAction.Bas, altGr));
        Assert.Equal(1, banc.Frapper("Espace", bas: true));    // témoin : appui absorbé
        banc.Jouer(new ActionFrappe(TypeAction.Haut, altGr));
        // Répétition automatique d'Espace, toujours tenue : ProcessKeyCore réévalue la touche sans
        // AltGr (réserve de M1.4 I1) ; la répétition reste absorbée, mais la relâche ne l'est plus.
        Assert.Equal(1, banc.Frapper("Espace", bas: true));
        Assert.Equal(1, banc.Frapper("Espace", bas: false));
    }

    // Constat AFH-5 (M1.2 I5, graine 0x41470517) : coup grec armé, puis une touche de la
    // disposition qui n'écrit rien à ce niveau (Maj+AltGr+S). ProcessKeyCore l'absorbe
    // (« output == null → return true », KeyMapper.cs:892-893) sans annuler le coup, alors
    // que la règle « Toutes annulent » vaut pour toute touche sans caractère.
    [Fact]
    public void UnCoupDeCouche_UneToucheSansCaractereACeNiveau_AnnuleLAttente()
    {
        using var banc = new BancFrappe(asyncLogique: false);
        int maj = Clavier.Index("MajD"), declencheur = Clavier.Index("SC02B"), altGr = Clavier.Index("AltGr"), s = Clavier.Index("s");
        banc.Jouer(new ActionFrappe(TypeAction.Bas, maj));
        banc.Jouer(new ActionFrappe(TypeAction.Bas, declencheur));
        banc.Jouer(new ActionFrappe(TypeAction.Haut, declencheur));
        banc.Jouer(new ActionFrappe(TypeAction.Bas, altGr));
        Assert.Equal(MaintainableLayerMode.OneShot, banc.Mapper.MaintainableLayerState.Mode);   // témoin
        int envois = banc.Sys.Envois;
        banc.Jouer(new ActionFrappe(TypeAction.Bas, s));
        Assert.Equal(envois, banc.Sys.Envois);                                                  // témoin : rien n'est écrit
        Assert.NotEqual(MaintainableLayerMode.OneShot, banc.Mapper.MaintainableLayerState.Mode);
    }

    // Constat AFH-2 (M1.1 I3, graine 0x41470789) : « ^ » armé, puis Ctrl+Maj+W (recherche de
    // caractères). Le hook sert le raccourci avant ProcessKey (KeyboardHook.cs:362-368) : la touche
    // morte n'est pas abandonnée, alors que le document promet l'abandon « après … raccourci ».
    // Ctrl+Maj+Q (clavier virtuel) suit le même chemin.
    [Theory]
    [InlineData("w")]
    [InlineData("q")]
    public void ToucheMorteArmee_PuisRaccourciDeLApplication_EstAbandonnee(string lettre)
    {
        using var banc = new BancFrappe(asyncLogique: false);
        banc.Frapper("SC01A", bas: true);
        banc.Frapper("SC01A", bas: false);
        Assert.NotNull(banc.Composition);                      // témoin : l'accent est bien armé
        banc.Frapper("CtrlG", bas: true);
        banc.Frapper("MajG", bas: true);
        Assert.Equal(1, banc.Frapper(lettre, bas: true));     // raccourci servi par le hook
        Assert.Null(banc.Composition);
    }

    // Constat AFH-3 (M1.4 I1, graine 0x4147015D) : Ctrl+Maj+Q tenu. Les répétitions automatiques
    // de Q repassent par la détection du raccourci (KeyboardHook.cs:362-379, « isKeyDown » sans
    // distinguer l'appui de la répétition) : le clavier virtuel bascule à chaque répétition,
    // soit ~30 fois par seconde après le délai de répétition. Même chose pour Ctrl+Maj+W.
    [Theory]
    [InlineData("q")]
    [InlineData("w")]
    public void RaccourciTenu_LaRepetitionNeLeRedeclenchePas(string lettre)
    {
        using var banc = new BancFrappe(asyncLogique: false);
        int declenchements = 0;
        banc.Hook.VirtualKeyboardRequested += () => declenchements++;
        banc.Hook.SearchRequested += () => declenchements++;
        banc.Frapper("CtrlG", bas: true);
        banc.Frapper("MajG", bas: true);
        banc.Frapper(lettre, bas: true);
        banc.Frapper(lettre, bas: true);                       // répétition automatique de Windows
        banc.Frapper(lettre, bas: true);
        Assert.Equal(1, declenchements);
    }

    // Constat AFH-4 (M1.2 I5, graine 0x414703C0) : une relâche jamais vue du hook, faite sur un
    // écran sécurisé (verrouillage, UAC, Ctrl+Alt+Suppr ; M1.3 I2 ne traite que les modificateurs).
    // L'appui suivant de la même touche passait pour une répétition (_keyDownOwnership gardait
    // l'ancien appui) : il rejouait l'ancienne décision, et un coup armé entre-temps n'était ni
    // consommé ni annulé. HautPerdu modélise le passage : il appelle KeyboardHook.ForgetHeldKeys,
    // comme TrayApplication sur EVENT_SYSTEM_DESKTOPSWITCH et WM_WTSSESSION_CHANGE.
    [Fact]
    public void RelachePerdue_LAppuiSuivantNEstPasUneRepetition_EtConsommeLeCoup()
    {
        using var banc = new BancFrappe(asyncLogique: false);
        int espace = Clavier.Index("Espace"), maj = Clavier.Index("MajD"), declencheur = Clavier.Index("SC02B");
        banc.Jouer(new ActionFrappe(TypeAction.Bas, maj));
        banc.Jouer(new ActionFrappe(TypeAction.Bas, espace));
        banc.Jouer(new ActionFrappe(TypeAction.HautPerdu, espace));
        banc.Jouer(new ActionFrappe(TypeAction.Bas, declencheur));
        banc.Jouer(new ActionFrappe(TypeAction.Haut, declencheur));
        banc.Jouer(new ActionFrappe(TypeAction.Haut, maj));
        Assert.Equal(MaintainableLayerMode.OneShot, banc.Mapper.MaintainableLayerState.Mode);   // témoin
        banc.Jouer(new ActionFrappe(TypeAction.Bas, espace));
        Assert.NotEqual(MaintainableLayerMode.OneShot, banc.Mapper.MaintainableLayerState.Mode);
    }

    // Témoin d'AFH-4 : la touche reste tenue pendant le passage par l'écran sécurisé et sa relâche
    // arrive au retour. Oublier l'appui ne doit pas libérer la relâche d'un appui absorbé (M1.4 I4).
    [Fact]
    public void ToucheAbsorbeeTenuePendantLEcranSecurise_SaRelacheTardiveResteAbsorbee()
    {
        using var banc = new BancFrappe(asyncLogique: false);
        Assert.Equal(1, banc.Frapper("SC032", bas: true));   // « , » émise par le moteur, appui absorbé
        banc.Hook.ForgetHeldKeys();
        Assert.Equal(1, banc.Frapper("SC032", bas: false));
        Assert.Equal(1, banc.Frapper("SC032", bas: true));   // appui neuf : de nouveau émis et absorbé
        Assert.Equal(1, banc.Frapper("SC032", bas: false));
    }

    // Témoin d'AFH-4 et d'AFH-3 : Ctrl+Maj+Q tenu pendant le passage. La relâche tardive reste au
    // raccourci ; l'appui suivant est neuf et déclenche une fois, ses répétitions jamais.
    [Fact]
    public void RaccourciTenuPendantLEcranSecurise_RelacheAbsorbee_AppuiSuivantNeuf()
    {
        using var banc = new BancFrappe(asyncLogique: false);
        int declenchements = 0;
        banc.Hook.VirtualKeyboardRequested += () => declenchements++;
        banc.Hook.SearchRequested += () => declenchements++;
        banc.Frapper("CtrlG", bas: true);
        banc.Frapper("MajG", bas: true);
        Assert.Equal(1, banc.Frapper("q", bas: true));
        banc.Hook.ForgetHeldKeys();
        Assert.Equal(1, banc.Frapper("q", bas: false));      // relâche tardive : absorbée
        Assert.Equal(1, declenchements);
        Assert.Equal(1, banc.Frapper("q", bas: true));       // appui neuf
        Assert.Equal(1, banc.Frapper("q", bas: true));       // répétition
        Assert.Equal(1, banc.Frapper("q", bas: false));
        Assert.Equal(2, declenchements);
    }

    // Témoin d'AFH-4 : l'avis du retour (EVENT_SYSTEM_DESKTOPSWITCH, hors contexte) peut être traité
    // après le rappel du hook d'une touche tapée juste après. Cette touche, plus récente que le
    // passage, reste en cours : sa répétition ne redéclenche pas le raccourci (AFH-3).
    [Fact]
    public void AvisDuPassageEnRetard_UneToucheTapeeApresResteEnCours()
    {
        using var banc = new BancFrappe(asyncLogique: false);
        int declenchements = 0;
        banc.Hook.VirtualKeyboardRequested += () => declenchements++;
        banc.Hook.SearchRequested += () => declenchements++;
        uint passage = banc.Heure;
        banc.Jouer(new ActionFrappe(TypeAction.Temps, 40));
        banc.Frapper("CtrlG", bas: true);
        banc.Frapper("MajG", bas: true);
        Assert.Equal(1, banc.Frapper("q", bas: true));
        banc.Hook.ForgetHeldKeys(passage);                     // avis traité en retard
        Assert.Equal(1, banc.Frapper("q", bas: true));       // répétition
        Assert.Equal(1, declenchements);
        banc.Hook.ForgetHeldKeys(banc.Heure);                // témoin : un passage à l'heure de l'appui l'oublie
        Assert.Equal(1, banc.Frapper("q", bas: true));
        Assert.Equal(2, declenchements);
    }

    // AFH-4, avis de session : posté, l'heure est celle du message (GetMessageTime) ; envoyé,
    // GetMessageTime date du message posté précédent, l'heure retenue est celle du traitement.
    // Le témoin montre l'écart : un appui tenu pendant le verrouillage, plus récent que le
    // dernier message posté, n'est oublié qu'avec l'heure de traitement.
    [Fact]
    public void AvisDeSessionEnvoye_PrendLHeureDeTraitement_PasCelleDuDernierMessagePoste()
    {
        Assert.Equal(5_000u, AZERTYGlobal.TrayApplication.SessionTransitionTime(false, 5_000, 9_000));
        Assert.Equal(9_000u, AZERTYGlobal.TrayApplication.SessionTransitionTime(true, 5_000, 9_000));
        Assert.Equal(0xFFFF_FFF6u, AZERTYGlobal.TrayApplication.SessionTransitionTime(false, -10, 3));  // tour d'horloge

        using var banc = new BancFrappe(asyncLogique: false);
        int declenchements = 0;
        banc.Hook.VirtualKeyboardRequested += () => declenchements++;
        int dernierPoste = unchecked((int)banc.Heure);
        banc.Jouer(new ActionFrappe(TypeAction.Temps, 40));
        banc.Frapper("CtrlG", bas: true);
        banc.Frapper("MajG", bas: true);
        Assert.Equal(1, banc.Frapper("q", bas: true));         // tenu pendant le verrouillage
        banc.Jouer(new ActionFrappe(TypeAction.Temps, 40));

        banc.Hook.ForgetHeldKeys(AZERTYGlobal.TrayApplication.SessionTransitionTime(false, dernierPoste, banc.Heure));
        banc.Frapper("q", bas: true);                            // témoin : heure trop ancienne, rien d'oublié
        Assert.Equal(1, declenchements);

        banc.Hook.ForgetHeldKeys(AZERTYGlobal.TrayApplication.SessionTransitionTime(true, dernierPoste, banc.Heure));
        banc.Frapper("q", bas: true);                            // appui neuf
        Assert.Equal(2, declenchements);
    }

    // Témoin d'AFH-4 : le déclencheur de couche tenu pendant le passage est abandonné sans armer
    // de coup, et sa relâche tardive n'en arme pas non plus.
    [Fact]
    public void DeclencheurTenuPendantLEcranSecurise_NArmeAucunCoup()
    {
        using var banc = new BancFrappe(asyncLogique: false);
        banc.Frapper("MajD", bas: true);
        banc.Frapper("SC02B", bas: true);
        banc.Hook.ForgetHeldKeys();
        banc.Frapper("SC02B", bas: false);
        banc.Frapper("MajD", bas: false);
        Assert.Equal(MaintainableLayerMode.Inactive, banc.Mapper.MaintainableLayerState.Mode);
        banc.Frapper("MajD", bas: true);                     // témoin : le même appui sans passage arme le coup
        banc.Frapper("SC02B", bas: true);
        banc.Frapper("SC02B", bas: false);
        banc.Frapper("MajD", bas: false);
        Assert.Equal(MaintainableLayerMode.OneShot, banc.Mapper.MaintainableLayerState.Mode);
    }

    // Constat AFH-1 (M1.4 I4, réduit par le banc, graine 0x41470132) : une touche dont l'appui a été
    // absorbé voit sa relâche transmise à l'application quand Ctrl, Alt gauche ou Win a été enfoncé
    // entre les deux. KeyMapper.ProcessKeyCore rend false sur ces chemins de raccourci sans regarder
    // _keyDownOwnership (KeyMapper.cs:768-776 pour Alt et Win, chemin Ctrl pour une touche non
    // lettre). L'application reçoit un WM_KEYUP sans WM_KEYDOWN.
    [Theory]
    [InlineData("CtrlG")]
    [InlineData("Alt")]
    [InlineData("Win")]
    public void ToucheAbsorbee_PuisModificateur_LaRelacheResteAbsorbee(string modificateur)
    {
        using var banc = new BancFrappe(asyncLogique: false);
        Assert.Equal(1, banc.Frapper("SC032", bas: true));   // « , » : émise par le moteur, appui absorbé
        banc.Frapper(modificateur, bas: true);
        Assert.Equal(1, banc.Frapper("SC032", bas: false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Banc_FrappeAuHasard_AucuneRegleViolee(bool asyncLogique)
    {
        var parRegle = new SortedDictionary<string, (int Nombre, ulong Graine, Violation Premiere)>();
        long pas = 0;
        for (int i = 0; i < NombreSuites; i++)
        {
            ulong graine = GraineBase + (ulong)i;
            var suite = Generateur.Suite(graine, LongueurSuite);
            pas += suite.Count;
            foreach (var v in Execution.Jouer(suite, asyncLogique, ConstatsDemontres))
                parRegle[v.Regle] = parRegle.TryGetValue(v.Regle, out var x) ? (x.Nombre + 1, x.Graine, x.Premiere) : (1, graine, v);
        }

        var rapport = new StringBuilder();
        rapport.AppendLine($"modèle async {(asyncLogique ? "logique" : "physique")} : {NombreSuites} suites × {LongueurSuite} pas = {pas} pas, graines {GraineBase:X}..{GraineBase + (ulong)NombreSuites - 1:X}");
        foreach (var (regle, (nombre, graine, premiere)) in parRegle)
        {
            var reduite = Execution.Reduire(Generateur.Suite(graine, LongueurSuite), regle, asyncLogique);
            var v = Execution.Jouer(reduite, asyncLogique).First(x => x.Regle == regle);
            rapport.AppendLine($"[{regle}] {nombre} violation(s), première graine {graine:X} pas {premiere.Etape} : {premiere.Detail}");
            rapport.AppendLine($"    réduite ({reduite.Count} actions) : {string.Join(" ", reduite)}");
            rapport.AppendLine($"    → {v.Detail}");
        }
        _sortie.WriteLine(rapport.ToString());
        var chemin = Environment.GetEnvironmentVariable("AG_FRAPPE_RAPPORT");
        if (!string.IsNullOrEmpty(chemin)) File.AppendAllText(chemin, rapport.ToString());
        Assert.True(parRegle.Count == 0, rapport.ToString());
    }

    [Fact]
    public void Banc_LaMemeGraine_RejoueLaMemeSuite()
    {
        Assert.Equal(Generateur.Suite(42, 300), Generateur.Suite(42, 300));
    }
}
