using AZERTYGlobal;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Faux de <see cref="ITrayShell"/> : garde ce que <c>TrayApplication</c> demande à Windows.
/// Minuteries actives, icône présente, messages postés, boîtes, liens ouverts, menus ; horloge
/// des messages réglable. Couture d'état du 16 novembre 2026 (docs/audit-frappe-hasard.md § 7).
/// </summary>
internal sealed class FauxTrayShell : ITrayShell
{
    private const uint NimAdd = 0, NimDelete = 2;

    public HashSet<uint> Minuteries { get; } = new();
    public List<uint> Icone { get; } = new();                 // NIM_* reçus, dans l'ordre
    public bool IconePresente { get; private set; }
    /// <summary>Appels de Shell_NotifyIconW refusés par <see cref="ResultatIcone"/>.</summary>
    public int Refus { get; private set; }
    /// <summary>Résultat de Shell_NotifyIconW selon le NIM_* (défaut : vrai).</summary>
    public Func<uint, bool> ResultatIcone { get; set; } = _ => true;
    public List<uint> Postes { get; } = new();
    public List<string> Boites { get; } = new();
    public List<string> Liens { get; } = new();
    public int Menus { get; private set; }

    public bool Envoye { get; set; }
    public int HeureMessage { get; set; }
    public uint Maintenant { get; set; }

    public UIntPtr SetTimer(IntPtr hWnd, UIntPtr id, uint elapseMs, IntPtr timerProc)
    {
        Minuteries.Add((uint)id);
        return id;
    }

    public bool KillTimer(IntPtr hWnd, UIntPtr id) => Minuteries.Remove((uint)id);

    public bool Shell_NotifyIconW(uint message, ref Win32.NOTIFYICONDATAW data)
    {
        Icone.Add(message);
        bool ok = ResultatIcone(message);
        if (!ok) Refus++;
        if (ok && message == NimAdd) IconePresente = true;
        if (ok && message == NimDelete) IconePresente = false;
        return ok;
    }

    public bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        Postes.Add(msg);
        return true;
    }

    public int MessageBoxW(IntPtr hWnd, string text, string caption, uint type)
    {
        Boites.Add(caption + " | " + text);
        return 1;   // IDOK
    }

    public IntPtr ShellExecuteW(IntPtr hWnd, string operation, string file, string? parameters, string? directory, int showCommand)
    {
        Liens.Add(file);
        return (IntPtr)42;   // > 32 : succès
    }

    public bool TrackPopupMenuEx(IntPtr hMenu, uint flags, int x, int y, IntPtr hWnd, IntPtr parameters)
    {
        Menus++;
        return true;
    }

    public bool InSendMessage() => Envoye;
    public int GetMessageTime() => HeureMessage;
    public uint GetTickCount() => Maintenant;

    /// <summary>Nombre de NIM_ADD reçus à partir du rang <paramref name="index"/> de <see cref="Icone"/>.</summary>
    public int AjoutsDepuis(int index) => Icone.Skip(index).Count(m => m == NimAdd);
}
