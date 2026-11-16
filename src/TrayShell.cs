namespace AZERTYGlobal;

/// <summary>
/// Appels Win32 à effet de <see cref="TrayApplication"/> : minuteries, icône de la zone de
/// notification, messages postés, boîtes de message, ouverture de liens, menu contextuel,
/// horloge des messages. Couture d'état décidée par Antoine le 16 novembre 2026 (audit par
/// frappe au hasard, docs/audit-frappe-hasard.md § 7) : un test remplace ce port par un faux
/// et observe ce que l'application demande à Windows (minuterie armée, icône ajoutée...) au
/// lieu de le déduire du code. En production, <see cref="Win32TrayShell"/> appelle Windows.
/// </summary>
internal interface ITrayShell
{
    UIntPtr SetTimer(IntPtr hWnd, UIntPtr id, uint elapseMs, IntPtr timerProc);
    bool KillTimer(IntPtr hWnd, UIntPtr id);
    bool Shell_NotifyIconW(uint message, ref Win32.NOTIFYICONDATAW data);
    bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
    IntPtr ShellExecuteW(IntPtr hWnd, string operation, string file, string? parameters, string? directory, int showCommand);
    bool TrackPopupMenuEx(IntPtr hMenu, uint flags, int x, int y, IntPtr hWnd, IntPtr parameters);

    /// <summary>Vrai si le message en cours a été envoyé par un autre fil (<c>InSendMessageEx</c>).</summary>
    bool InSendMessage();

    /// <summary>Heure du dernier message posté retiré de la file (<c>GetMessageTime</c>).</summary>
    int GetMessageTime();

    /// <summary>Horloge de <c>KBDLLHOOKSTRUCT.time</c> (<c>GetTickCount</c>).</summary>
    uint GetTickCount();
}

/// <summary>Implémentation réelle : chaque méthode appelle la fonction Win32 de même nom.</summary>
internal sealed class Win32TrayShell : ITrayShell
{
    public static readonly Win32TrayShell Instance = new();
    private Win32TrayShell() { }

    public UIntPtr SetTimer(IntPtr hWnd, UIntPtr id, uint elapseMs, IntPtr timerProc)
        => Win32.SetTimer(hWnd, id, elapseMs, timerProc);
    public bool KillTimer(IntPtr hWnd, UIntPtr id) => Win32.KillTimer(hWnd, id);
    public bool Shell_NotifyIconW(uint message, ref Win32.NOTIFYICONDATAW data)
        => Win32.Shell_NotifyIconW(message, ref data);
    public bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        => Win32.PostMessageW(hWnd, msg, wParam, lParam);
    public int MessageBoxW(IntPtr hWnd, string text, string caption, uint type)
        => Win32.MessageBoxW(hWnd, text, caption, type);
    public IntPtr ShellExecuteW(IntPtr hWnd, string operation, string file, string? parameters, string? directory, int showCommand)
        => Win32.ShellExecuteW(hWnd, operation, file, parameters, directory, showCommand);
    public bool TrackPopupMenuEx(IntPtr hMenu, uint flags, int x, int y, IntPtr hWnd, IntPtr parameters)
        => Win32.TrackPopupMenuEx(hMenu, flags, x, y, hWnd, parameters);
    public bool InSendMessage() => Win32.InSendMessageEx(IntPtr.Zero) != Win32.ISMEX_NOSEND;
    public int GetMessageTime() => Win32.GetMessageTime();
    public uint GetTickCount() => Win32.GetTickCount();
}
