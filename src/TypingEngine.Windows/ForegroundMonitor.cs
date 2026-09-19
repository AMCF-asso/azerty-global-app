// Détection du process foreground et du mode de compatibilité courant.
//
// Mécanique :
// - SetWinEventHook(EVENT_SYSTEM_FOREGROUND) au démarrage (après création de la fenêtre tray)
// - Le callback est routé vers le thread de la boucle de messages (WINEVENT_OUTOFCONTEXT
//   sur le thread qui a appelé SetWinEventHook, soit notre thread principal)
// - Recalcul immédiat sur le thread UI à chaque changement de fenêtre ou de contrôle.
// - Recompute() :
//   1. lit le process foreground via IWin32Api.TryGetForegroundProcess
//   2. énumère les modules du process via IWin32Api.TryEnumProcessModules
//   3. calcule CurrentMode selon la liste anti-cheat / DLL signatures / overrides utilisateur
//   4. déclenche ForegroundChanged
//
// Si le suivi foreground/focus n'est pas disponible, suspendre les émissions jusqu'au redémarrage.

namespace TypingEngine.Windows;

public enum CompatibilityMode
{
    /// <summary>Comportement v0.9.6 — injection KEYEVENTF_UNICODE.</summary>
    Default,
    /// <summary>Process avec framework gaming détecté — injection combo native + Alt+code fallback.</summary>
    NativeCombo,
    /// <summary>Process protégé ou explicitement désactivé — désactivation totale.</summary>
    DisabledAntiCheat
}

public enum CompatibilitySuspendReason
{
    None,
    AntiCheat,
    RemoteAccess,
    UserOverride,
    UnknownForeground
}

public sealed class ForegroundMonitor : IDisposable
{
    private readonly IWin32Api _api;
    private readonly IWindowsTypingHost _host;

    /// <summary>ID du timer Win32 utilisé pour le debounce. Doit être unique côté wndproc.</summary>
    public const uint TIMER_FOREGROUND_DEBOUNCE = 0xF00100;

    // Anti-GC : le delegate doit rester rooté tant que le hook est installé
    private Win32.WinEventDelegate? _winEventDelegate;
    private IntPtr _winEventHook = IntPtr.Zero;
    private IntPtr _focusEventHook = IntPtr.Zero;

    // Snapshot immuable atomique pour cohérence cross-thread. Tous les champs sont
    // mis à jour en une seule écriture de référence (atomique CLR pour les types ref).
    // Évite que le hook thread lise des combinaisons mixtes (ex. nouveau
    // processName / ancien hkl) pendant que le tray thread écrit séquentiellement.
    private sealed record class Snapshot(
        IntPtr Window,
        string? ProcessName,
        string? FullPath,
        IntPtr Hkl,
        CompatibilityMode Mode,
        CompatibilitySuspendReason SuspendReason,
        ForegroundProcessIdentity Identity,
        bool SecureInput);
    private Snapshot? _snapshot;
    private Snapshot? _lastApplication;

    /// <summary>Dernière application hors shell, destinée uniquement au menu de compatibilité.</summary>
    public string? LastApplicationProcessName => _lastApplication?.ProcessName;
    public string? LastApplicationFullPath => _lastApplication?.FullPath;

    /// <summary>Nom court du process foreground (ex: "Minecraft.Windows.exe"). Null si pas de fenêtre foreground.</summary>
    public string? CurrentProcessName => _snapshot?.ProcessName;

    /// <summary>Chemin complet du process foreground.</summary>
    public string? CurrentFullPath => _snapshot?.FullPath;

    /// <summary>HKL du layout natif du thread foreground.</summary>
    public IntPtr CurrentHkl => _snapshot?.Hkl ?? IntPtr.Zero;

    /// <summary>Mode de compatibilité résolu pour le process foreground actuel.</summary>
    public CompatibilityMode CurrentMode => _snapshot?.Mode ?? CompatibilityMode.Default;

    /// <summary>Motif précis de la suspension, indépendant du mode d'injection.</summary>
    public CompatibilitySuspendReason CurrentSuspendReason =>
        _snapshot?.SuspendReason ?? CompatibilitySuspendReason.None;

    /// <summary>Instance exacte du processus foreground (PID + création).</summary>
    public ForegroundProcessIdentity CurrentIdentity => _snapshot?.Identity ?? default;

    /// <summary>True lorsqu'un contrôle de mot de passe identifié possède le focus.</summary>
    public bool IsSecureInput => _snapshot?.SecureInput == true;

    /// <summary>
    /// Audit sécu 2026-05 SEV-A2-05 : lecture atomique des deux champs critiques
    /// pour EmitText. Évite la race entre lecture séquentielle de Mode puis Hkl
    /// (un OnWinEvent pouvait remplacer _snapshot entre les deux lectures, donnant
    /// mode/hkl discordants → faux caractère pendant alt-tab rapide).
    /// Un seul accès à _snapshot (déréf. atomique CLR sur ref types).
    /// </summary>
    public (CompatibilityMode Mode, IntPtr Hkl) GetEmitContext()
    {
        var snap = _snapshot;  // capture atomique
        if (!IsTrackingAvailable || snap == null || snap.Window != _api.GetForegroundWindow())
            return (CompatibilityMode.DisabledAntiCheat, IntPtr.Zero);
        return (snap?.Mode ?? CompatibilityMode.Default, snap?.Hkl ?? IntPtr.Zero);
    }

    /// <summary>Indique si le suivi des changements de fenêtre a pu être installé.</summary>
    public bool IsHookInstalled => _winEventHook != IntPtr.Zero;
    public bool IsTrackingAvailable => IsHookInstalled && _focusEventHook != IntPtr.Zero;

    /// <summary>Déclenché à chaque changement effectif de mode (pas à chaque event foreground).</summary>
    public event Action? ForegroundChanged;

    /// <summary>
    /// Crée le monitor et installe le WinEventHook foreground.
    /// trayHwnd est conservé pour compatibilité avec les hôtes existants.
    /// Le recalcul se fait immédiatement, hors callback clavier.
    /// </summary>
    public ForegroundMonitor(IWin32Api api, IntPtr trayHwnd, IWindowsTypingHost? host = null)
    {
        _api = api;
        _host = host ?? NullWindowsTypingHost.Instance;

        try
        {
            _winEventDelegate = OnWinEvent;
            _winEventHook = _api.SetWinEventHook(
                Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND, _winEventDelegate);
            _focusEventHook = _api.SetWinEventHook(
                Win32.EVENT_OBJECT_FOCUS, Win32.EVENT_OBJECT_FOCUS, _winEventDelegate);
            // Un échec de l'un des deux suivis impose une suspension de précaution.
        }
        catch (Exception ex)
        {
            _host.Log("ForegroundMonitor.ctor", ex);
            // Conserver tout handle déjà installé pour le libérer dans Dispose.
        }

        // Premier calcul initial (synchrone)
        Recompute();
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // Le snapshot de frappe suit aussi le focus dans une même fenêtre
        // (champ de mot de passe), sans garder l'identité précédente pendant 100 ms.
        Recompute();
    }

    /// <summary>
    /// Force le recalcul immédiat du process foreground et du mode.
    /// Appelé : (1) au démarrage, (2) à l'expiration du timer debounce, (3) sur WM_INPUTLANGCHANGE,
    /// (4) après modification d'un override utilisateur via le menu tray.
    /// </summary>
    public void Recompute()
    {
        try
        {
            string? processName = null;
            string? fullPath = null;
            IntPtr hkl = IntPtr.Zero;
            uint pid = 0;
            IntPtr window = _api.GetForegroundWindow();
            bool hasFg = _api.TryGetForegroundProcess(out processName, out fullPath, out hkl, out pid);
            long startTimeTicks = 0;
            if (hasFg && pid != 0)
                _api.TryGetProcessStartTime(pid, out startTimeTicks);
            var identity = new ForegroundProcessIdentity(pid, startTimeTicks);
            bool secureInput = hasFg && _api.IsForegroundPasswordField();

            // Le menu conserve la dernière application utile ; la frappe suit toujours
            // la vraie cible, y compris Explorer, la recherche Windows et nos fenêtres.
            bool isTransientShell = hasFg && processName != null && (
                string.Equals(processName, "explorer.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(processName, "SearchHost.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(processName, "StartMenuExperienceHost.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(processName, "ShellExperienceHost.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(processName, "TextInputHost.exe", StringComparison.OrdinalIgnoreCase));
            // Recette VM du 2026-09-19 : une fenêtre qui change entre les deux lectures
            // n'est pas une identité inconnue, c'est un événement foreground de plus, qui
            // déclenchera son propre Recompute. Suspendre ici affichait une bulle de
            // précaution au moindre clic sur la barre des tâches (mesuré : explorer.exe
            // puis ShellExperienceHost.exe, six occurrences en trois minutes), et cette
            // bulle masque l'icône du tray. La sécurité de frappe reste entière :
            // GetEmitContext recontrôle la fenêtre à chaque émission et refuse d'émettre
            // dès qu'elle a bougé. Seul un suivi réellement indisponible suspend.
            var resolved = IsTrackingAvailable
                ? ResolveState(processName, fullPath, pid, hasFg)
                : (Mode: CompatibilityMode.DisabledAntiCheat, Reason: CompatibilitySuspendReason.UnknownForeground);
            CompatibilityMode mode = resolved.Mode;

            // Snapshot atomique : une seule écriture de référence (atomique CLR sur ref types).
            var oldSnapshot = _snapshot;
            CompatibilityMode oldMode = oldSnapshot?.Mode ?? CompatibilityMode.Default;
            _snapshot = new Snapshot(window, processName, fullPath, hkl, mode, resolved.Reason, identity, secureInput);
            if (hasFg && !isTransientShell && pid != (uint)Environment.ProcessId)
                _lastApplication = _snapshot;

            // Le motif et hasFg sont journalisés avec le mode : sans eux, deux Recompute
            // successifs se lisent comme un seul événement et le motif réellement vu par
            // le tray reste invisible (recette VM du 2026-09-19).
            var oldReason = oldSnapshot?.SuspendReason ?? CompatibilitySuspendReason.None;
            if ((oldMode != mode || oldReason != resolved.Reason) && _host.CompatibilityDebugLog)
            {
                _host.LogCompatibilityEvent("CompatMode",
                    $"{oldMode} → {mode} [{oldReason} → {resolved.Reason}] "
                    + $"(process={_host.AnonymizeProcessName(processName)}, hasFg={hasFg}, "
                    + $"pid={pid}, tracking={IsTrackingAvailable})");
            }

            if (oldMode != mode || oldSnapshot?.Identity != identity ||
                oldSnapshot?.SecureInput != secureInput ||
                (oldMode == mode && processName != null))
            {
                // Toujours notifier sur changement effectif de process pour permettre à
                // au produit hôte de mettre à jour son état UI (override changes, etc.)
                ForegroundChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            _snapshot = new Snapshot(IntPtr.Zero, null, null, IntPtr.Zero,
                CompatibilityMode.DisabledAntiCheat, CompatibilitySuspendReason.UnknownForeground, default, true);
            _host.Log("ForegroundMonitor.Recompute", ex);
            ForegroundChanged?.Invoke();
        }
    }

    private (CompatibilityMode Mode, CompatibilitySuspendReason Reason) ResolveState(
        string? processName, string? fullPath, uint pid, bool hasFg)
    {
        if (!hasFg)
        {
            // Si un PID foreground existe mais que son nom/chemin est inaccessible,
            // on privilégie la sécurité utilisateur : ne pas laisser le hook actif
            // face à un process potentiellement protégé par anti-cheat.
            if (pid != 0)
                return (CompatibilityMode.DisabledAntiCheat, CompatibilitySuspendReason.UnknownForeground);
            return (CompatibilityMode.Default, CompatibilitySuspendReason.None);
        }

        if (string.IsNullOrEmpty(processName))
            return (CompatibilityMode.Default, CompatibilitySuspendReason.None);

        // Override utilisateur lu en premier (mais l'anti-cheat le surclasse pour la sécurité)
        // Les clients de connexion à distance surclassent les overrides : si AZERTY Global
        // tourne aussi sur le poste distant, le passthrough provoquerait un double remapping.
        if (GameRegistry.IsRemoteAccessProcess(processName))
            return (CompatibilityMode.DisabledAntiCheat, CompatibilitySuspendReason.RemoteAccess);

        var userOverride = _host.GetCompatibilityOverride(processName);

        // Anti-cheat : surclasse tout override forceOn (sécurité utilisateur)
        if (GameRegistry.IsAntiCheatProcess(processName, fullPath))
            return (CompatibilityMode.DisabledAntiCheat, CompatibilitySuspendReason.AntiCheat);

        if (userOverride == "forceOn")
            return (CompatibilityMode.NativeCombo, CompatibilitySuspendReason.None);
        if (userOverride == "forceOff")
            return (CompatibilityMode.DisabledAntiCheat, CompatibilitySuspendReason.UserOverride);

        // Détection auto via modules chargés
        if (pid != 0 && _api.TryEnumProcessModules(pid, out var modules))
        {
            if (GameRegistry.HasGameFrameworkLoaded(modules))
                return (CompatibilityMode.NativeCombo, CompatibilitySuspendReason.None);
        }

        return (CompatibilityMode.Default, CompatibilitySuspendReason.None);
    }

    public void Dispose()
    {
        if (_winEventHook != IntPtr.Zero)
        {
            try { _api.UnhookWinEvent(_winEventHook); } catch { }
            _winEventHook = IntPtr.Zero;
        }
        if (_focusEventHook != IntPtr.Zero)
        {
            try { _api.UnhookWinEvent(_focusEventHook); } catch { }
            _focusEventHook = IntPtr.Zero;
        }
        _winEventDelegate = null;
    }
}
