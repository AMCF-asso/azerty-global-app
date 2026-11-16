using TypingEngine.Windows;

namespace AZERTYGlobal;

/// <summary>Issue d'une insertion. <see cref="ElevatedTarget"/> : la fenêtre cible tourne à un
/// niveau d'intégrité supérieur (fenêtre administrateur) ; Windows refuserait les frappes
/// simulées sans le signaler, rien n'est émis et la recherche copie le caractère (EW-7).</summary>
internal enum TextInsertionOutcome { Inserted, NotInserted, Partial, Blocked, ElevatedTarget }

/// <summary>
/// Restaure une fenêtre cible puis délègue l'émission au même moteur Unicode que
/// le clavier. Les delegates injectables rendent les échecs de focus testables.
/// </summary>
internal sealed class TextInsertionService
{
    private readonly Func<string, TextEmissionResult> _emitText;
    private readonly Func<bool> _canInsert;
    private readonly Action _refreshTarget;
    private readonly Func<IntPtr, bool> _activateTarget;
    private readonly Func<IntPtr, bool> _targetAboveUs;
    private readonly Func<bool> _imposedSuspension;

    public TextInsertionService(Func<string, TextEmissionResult> emitText,
        Func<IntPtr, bool>? activateTarget = null, Func<bool>? canInsert = null, Action? refreshTarget = null,
        Func<IntPtr, bool>? targetAboveUs = null, Func<bool>? imposedSuspension = null)
    {
        _imposedSuspension = imposedSuspension ?? (() => false);
        _targetAboveUs = targetAboveUs ?? TargetIntegrity.IsWindowAboveUs;
        _emitText = emitText;
        _canInsert = canInsert ?? (() => true);
        _refreshTarget = refreshTarget ?? (() => { });
        _activateTarget = activateTarget ?? ActivateTargetWindow;
    }

    public TextInsertionOutcome Insert(IntPtr targetWindow, string text)
    {
        if (!_canInsert()) return TextInsertionOutcome.Blocked;
        if (targetWindow == IntPtr.Zero || string.IsNullOrEmpty(text) ||
            !_activateTarget(targetWindow))
            return TextInsertionOutcome.NotInserted;

        // Le changement de focus peut suspendre l'app ou révéler un champ protégé.
        // Forcer une lecture synchrone avant d'émettre depuis la recherche.
        _refreshTarget();
        // A21 : un jeu anti-triche ou un client distant ne reçoit aucune requête de plus
        // (pas d'OpenProcessToken) ; la suspension imposée se décide avant le niveau.
        if (_imposedSuspension()) return TextInsertionOutcome.Blocked;
        // EW-7 : fenêtre de niveau supérieur (administrateur). Vérifiée avant la seconde
        // lecture : la sonde du champ mot de passe (UIA) y échoue et répond « protégé »,
        // ce qui bloquerait sans copie ni bulle. La copie n'injecte rien : sans risque
        // pour un champ mot de passe ou un jeu protégé. Pause et consentement sont déjà
        // vérifiés en tête. La fenêtre activée reste au premier plan, Ctrl+V y colle.
        if (_targetAboveUs(targetWindow)) return TextInsertionOutcome.ElevatedTarget;
        if (!_canInsert()) return TextInsertionOutcome.Blocked;
        var result = _emitText(text);
        if (result.Blocked) return TextInsertionOutcome.Blocked;
        if (result.IsComplete) return TextInsertionOutcome.Inserted;
        return result.SentEvents == 0 ? TextInsertionOutcome.NotInserted : TextInsertionOutcome.Partial;
    }

    private static bool ActivateTargetWindow(IntPtr targetWindow)
    {
        if (!Win32.IsWindow(targetWindow)) return false;

        uint currentThread = Win32.GetCurrentThreadId();
        uint targetThread = Win32.GetWindowThreadProcessId(targetWindow, IntPtr.Zero);
        bool attached = targetThread != 0 && targetThread != currentThread &&
            Win32.AttachThreadInput(currentThread, targetThread, true);
        try
        {
            if (!Win32.SetForegroundWindow(targetWindow))
                return false;
            return Win32.GetForegroundWindow() == targetWindow;
        }
        finally
        {
            if (attached)
                Win32.AttachThreadInput(currentThread, targetThread, false);
        }
    }
}
