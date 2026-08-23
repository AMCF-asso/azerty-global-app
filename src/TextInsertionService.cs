namespace AZERTYGlobal;

/// <summary>
/// Restaure une fenêtre cible puis délègue l'émission au même moteur Unicode que
/// le clavier. Les delegates injectables rendent les échecs de focus testables.
/// </summary>
internal sealed class TextInsertionService
{
    private readonly Action<string> _emitText;
    private readonly Func<IntPtr, bool> _activateTarget;

    public TextInsertionService(Action<string> emitText, Func<IntPtr, bool>? activateTarget = null)
    {
        _emitText = emitText;
        _activateTarget = activateTarget ?? ActivateTargetWindow;
    }

    public bool TryInsert(IntPtr targetWindow, string text)
    {
        if (targetWindow == IntPtr.Zero || string.IsNullOrEmpty(text) ||
            !_activateTarget(targetWindow))
            return false;

        _emitText(text);
        return true;
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
