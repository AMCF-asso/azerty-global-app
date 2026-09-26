// Socle commun des fenêtres Win32 — audit du 25/09, lot 8 (X-01, F-01).
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Ce que chaque fenêtre recopiait : enregistrement de classe, fond, DPI, placement. Les
/// copies avaient divergé (pinceau de classe détruit deux fois, retour de RegisterClassExW
/// jamais lu). Rien ici ne peint : le rendu des fenêtres reste identique au pixel près.
///
/// Modèle D1 (accessibilité 1.3.0, Durée de pause et Couches) : la fenêtre naît, son DPI se
/// lit sur elle, tailles et polices en découlent par <see cref="WindowSizing.ScaleForDpi"/>.
/// <see cref="DpiOf"/>, <see cref="ApplyDpiChange"/> et <see cref="ApplyClassBackground"/>
/// viennent de ThemeWindow (main, 5d5ada4), sans la charte.
/// </summary>
static class NativeWindow
{
    /// <summary>GetLastError de RegisterClassExW quand la classe existe déjà.</summary>
    internal const int ERROR_CLASS_ALREADY_EXISTS = 1410;
    private const int IDC_ARROW = 32512;

    private static IntPtr Module => Win32.GetModuleHandleW(null);

    // ═══════════════════════════════════════════════════════════════
    // Classe
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Enregistre la classe d'une fenêtre avec cette procédure, sans pinceau de fond (voir
    /// <see cref="ApplyClassBackground"/>). Rend faux, et le journalise, quand la classe n'a
    /// pas pu être enregistrée avec elle : la fenêtre ne doit alors pas être créée.
    ///
    /// Chaque fenêtre désenregistre sa classe à son Dispose. Si ce désenregistrement a
    /// échoué, la classe survit avec la procédure de l'instance précédente, et
    /// RegisterClassExW rend 0 (erreur 1410) : CreateWindowExW réussirait quand même et la
    /// nouvelle fenêtre partirait en silence sur l'ancienne procédure, liée à un objet
    /// disposé. La classe est donc désenregistrée puis enregistrée de nouveau ; si Windows la
    /// refuse encore (une fenêtre de l'ancienne classe vit toujours), l'échec est rendu.
    /// </summary>
    internal static bool RegisterClass(string className, Win32.WNDPROC wndProc, uint style = 0,
        bool arrowCursor = true)
    {
        var wc = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
            style = style,
            lpfnWndProc = wndProc,
            hInstance = Module,
            hCursor = arrowCursor ? Win32.LoadCursorW(IntPtr.Zero, (IntPtr)IDC_ARROW) : IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszClassName = className,
        };

        int error = 0;
        bool registered = RegisterOrReplace(
            () =>
            {
                bool ok = Win32.RegisterClassExW(ref wc) != 0;
                error = ok ? 0 : Marshal.GetLastWin32Error();
                return ok;
            },
            () => error,
            () => Win32.UnregisterClassW(className, Module));

        if (!registered)
            ConfigManager.Log($"NativeWindow.RegisterClass {className}", new Win32Exception(error));
        return registered;
    }

    /// <summary>
    /// La règle de <see cref="RegisterClass"/>, sans Win32 : une classe déjà enregistrée est
    /// remplacée, jamais réutilisée ; toute autre erreur est un échec.
    /// </summary>
    internal static bool RegisterOrReplace(Func<bool> register, Func<int> lastError, Func<bool> unregister)
    {
        if (register())
            return true;
        if (lastError() != ERROR_CLASS_ALREADY_EXISTS)
            return false;
        return unregister() && register();
    }

    /// <summary>À appeler au Dispose, après DestroyWindow : la classe ne se libère qu'une
    /// fois sa dernière fenêtre détruite. Windows détruit alors le pinceau de fond.</summary>
    internal static void UnregisterClass(string className) =>
        Win32.UnregisterClassW(className, Module);

    // ═══════════════════════════════════════════════════════════════
    // Fond
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Pose un pinceau neuf en fond de la classe de la fenêtre et le rend, pour les
    /// WM_CTLCOLOR* et les FillRect de la fenêtre. C'est avec lui que Windows efface le fond
    /// avant WM_PAINT, comme avec un hbrBackground donné à l'enregistrement.
    ///
    /// ⛔ Ne jamais détruire ce pinceau : il appartient à la classe, et Windows le détruit au
    /// désenregistrement (<see cref="UnregisterClass"/>). Huit fenêtres le détruisaient
    /// aussi, soit deux DeleteObject pour un pinceau (audit du 25/09, X-01). Un appel
    /// suivant remplace et détruit le précédent, posé par ce même socle.
    /// </summary>
    internal static IntPtr ApplyClassBackground(IntPtr hwnd, uint color)
    {
        if (hwnd == IntPtr.Zero)
            return IntPtr.Zero;

        IntPtr brush = Win32.CreateSolidBrush(color);
        IntPtr previous = Win32.SetClassLongPtrW(hwnd, Win32.GCLP_HBRBACKGROUND, brush);
        if (previous != IntPtr.Zero && previous != brush)
            Win32.DeleteObject(previous);
        return brush;
    }

    // ═══════════════════════════════════════════════════════════════
    // DPI
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// D1 : DPI de l'écran qui porte la fenêtre, 96 quand la mesure échoue (HWND nul ou
    /// détruit : GetDpiForWindow rend 0).
    /// </summary>
    internal static int DpiOf(IntPtr hwnd)
    {
        int dpi = hwnd != IntPtr.Zero ? Win32.GetDpiForWindow(hwnd) : 0;
        return dpi > 0 ? dpi : 96;
    }

    /// <summary>Nouveau DPI d'un WM_DPICHANGED (HIWORD de wParam) ; celui de la fenêtre
    /// quand il est illisible.</summary>
    internal static int DpiFromChange(IntPtr hwnd, IntPtr wParam)
    {
        int dpi = (int)((wParam.ToInt64() >> 16) & 0xFFFF);
        return dpi > 0 ? dpi : DpiOf(hwnd);
    }

    /// <summary>
    /// Applique le rectangle que Windows suggère avec WM_DPICHANGED. Il n'est pas facultatif :
    /// c'est lui qui garde la taille apparente d'une fenêtre qui passe d'un écran à un autre
    /// d'une autre échelle.
    /// </summary>
    internal static void MoveToSuggestedRect(IntPtr hwnd, IntPtr lParam)
    {
        if (hwnd == IntPtr.Zero || lParam == IntPtr.Zero)
            return;

        var suggested = Marshal.PtrToStructure<Win32.RECT>(lParam);
        Win32.MoveWindow(hwnd, suggested.left, suggested.top,
            suggested.right - suggested.left, suggested.bottom - suggested.top, true);
    }

    /// <summary>WM_DPICHANGED : fenêtre au rectangle suggéré, et le nouveau DPI, à charge pour
    /// l'appelant de refaire polices et mise en page.</summary>
    internal static int ApplyDpiChange(IntPtr hwnd, IntPtr wParam, IntPtr lParam)
    {
        int dpi = DpiFromChange(hwnd, wParam);
        MoveToSuggestedRect(hwnd, lParam);
        return dpi;
    }

    /// <summary>
    /// Fenêtres à échelle tronquée (À propos, Conflit, Statistiques, Accueil, Paramètres) :
    /// elles naissent à l'échelle de l'écran principal, puis se corrigent sur celle de leur
    /// écran. Rend la nouvelle échelle, ou null quand elle ne change pas.
    /// </summary>
    internal static float? CorrectedScale(int windowDpi, float currentScale) =>
        windowDpi > 0 && Math.Abs(windowDpi / 96f - currentScale) > 0.01f ? windowDpi / 96f : null;

    // ═══════════════════════════════════════════════════════════════
    // Placement
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Zone de travail de l'écran du propriétaire, à défaut de celui du curseur, où
    /// l'utilisateur vient de cliquer ; 1024×768 quand aucun écran ne répond.
    /// </summary>
    internal static Win32.RECT WorkArea(IntPtr owner = default)
    {
        IntPtr monitor = owner != IntPtr.Zero
            ? Win32.MonitorFromWindow(owner, Win32.MONITOR_DEFAULTTONEAREST)
            : IntPtr.Zero;

        if (monitor == IntPtr.Zero && Win32.GetCursorPos(out var cursor))
            monitor = Win32.MonitorFromPoint(cursor, Win32.MONITOR_DEFAULTTONEAREST);

        var info = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        if (monitor != IntPtr.Zero && Win32.GetMonitorInfo(monitor, ref info))
            return info.rcWork;

        return new Win32.RECT { left = 0, top = 0, right = 1024, bottom = 768 };
    }

    /// <summary>Taille extérieure d'une fenêtre pour une zone cliente donnée.</summary>
    internal static (int Width, int Height) OuterSize(int clientW, int clientH, uint style, uint exStyle = 0)
    {
        var rect = new Win32.RECT { left = 0, top = 0, right = clientW, bottom = clientH };
        Win32.AdjustWindowRectEx(ref rect, style, false, exStyle);
        return (rect.right - rect.left, rect.bottom - rect.top);
    }

    /// <summary>
    /// Coin haut-gauche d'une fenêtre centrée dans la zone de travail. Plus grande qu'elle, la
    /// fenêtre part de son coin : centrée, elle aurait sa barre de titre hors de l'écran.
    /// </summary>
    internal static (int X, int Y) CenterIn(Win32.RECT work, int width, int height) =>
        (work.left + Math.Max(0, (work.right - work.left - width) / 2),
         work.top + Math.Max(0, (work.bottom - work.top - height) / 2));

    /// <summary>D1 : fenêtre à sa zone cliente de référence (96 DPI) portée au DPI donné,
    /// centrée dans la zone de travail. Sans repeint : la fenêtre n'est pas encore affichée.</summary>
    internal static void FitToDpi(IntPtr hwnd, int clientW96, int clientH96, int dpi, uint style,
        uint exStyle, Win32.RECT work)
    {
        var (width, height) = OuterSize(WindowSizing.ScaleForDpi(clientW96, dpi),
            WindowSizing.ScaleForDpi(clientH96, dpi), style, exStyle);
        var (x, y) = CenterIn(work, width, height);
        Win32.MoveWindow(hwnd, x, y, width, height, false);
    }

    /// <summary>Nouvelle zone cliente, même centre : la fenêtre grandit ou rétrécit sur place.</summary>
    internal static void ResizeAroundCenter(IntPtr hwnd, int clientW, int clientH, uint style, uint exStyle = 0)
    {
        var (width, height) = OuterSize(clientW, clientH, style, exStyle);
        Win32.GetWindowRect(hwnd, out var current);
        int cx = (current.left + current.right) / 2;
        int cy = (current.top + current.bottom) / 2;
        Win32.MoveWindow(hwnd, cx - width / 2, cy - height / 2, width, height, true);
    }
}

/// <summary>
/// D1 : géométrie de référence à 96 DPI des contrôles d'une fenêtre, et la police de chacun,
/// remises à l'échelle ensemble sur WM_DPICHANGED. La Durée de pause et les Couches en
/// tenaient chacune une copie.
/// </summary>
internal sealed class ControlLayout
{
    private readonly List<(IntPtr Control, int X, int Y, int W, int H, int Font)> _items = new();

    /// <summary>Retient un contrôle. <paramref name="font"/> est un indice dans les polices
    /// que reçoit <see cref="Apply"/>.</summary>
    public void Track(IntPtr control, int x, int y, int w, int h, int font = 0) =>
        _items.Add((control, x, y, w, h, font));

    /// <summary>Ce que <see cref="Apply"/> fera, contrôle par contrôle, au DPI donné.</summary>
    internal IEnumerable<(IntPtr Control, int Font, int X, int Y, int W, int H)> Plan(int dpi)
    {
        foreach (var (control, x, y, w, h, font) in _items)
        {
            yield return (control, font,
                WindowSizing.ScaleForDpi(x, dpi), WindowSizing.ScaleForDpi(y, dpi),
                WindowSizing.ScaleForDpi(w, dpi), WindowSizing.ScaleForDpi(h, dpi));
        }
    }

    /// <summary>Police et géométrie de chaque contrôle au DPI donné.</summary>
    public void Apply(int dpi, params IntPtr[] fonts)
    {
        foreach (var (control, font, x, y, w, h) in Plan(dpi))
        {
            Win32.SendMessageW(control, Win32.WM_SETFONT, fonts[font], (IntPtr)1);
            Win32.MoveWindow(control, x, y, w, h, true);
        }
    }
}
