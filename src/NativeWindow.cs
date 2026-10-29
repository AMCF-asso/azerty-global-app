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
    // Cadre : barre de titre et icône
    // ═══════════════════════════════════════════════════════════════

    private const uint WM_SETICON = 0x0080;
    private const uint DWMWA_CAPTION_COLOR = 35;
    private const uint DWMWA_TEXT_COLOR = 36;
    /// <summary>DWMWA_COLOR_DEFAULT : la couleur que Windows choisit lui-même.</summary>
    private const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFF;
    private static readonly Dictionary<int, IntPtr> LogoIcons = new();

    /// <summary>
    /// Lot visuel 1.3.0 : barre de titre de la palette de la fenêtre (claire ou sombre) et
    /// logo en icône, petite et grande, à la taille de l'écran de la fenêtre. Les fenêtres
    /// sans icône montraient celle, générique, de Windows.
    ///
    /// A12 (audit 1.3.0) : une fenêtre claire est une fenêtre papier, sa barre prend la couleur
    /// du papier, comme celle du clavier virtuel ; la barre gris-bleu du système (#F0F4F7)
    /// tranchait sur le fond #FAF8F1. En contraste élevé, la barre reste celle du système.
    /// Windows 10 ignore ces attributs (HRESULT d'erreur, sans lever).
    /// </summary>
    internal static void ApplyFrame(IntPtr hwnd, bool dark)
    {
        if (hwnd == IntPtr.Zero)
            return;
        Win32.SetTitleBarTheme(hwnd, dark);
        var (caption, text) = CaptionColors(dark, HighContrast.IsOn());
        int captionValue = unchecked((int)caption), textValue = unchecked((int)text);
        Win32.DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionValue, sizeof(int));
        Win32.DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref textValue, sizeof(int));
        int dpi = DpiOf(hwnd);
        IntPtr small = LogoIcon(WindowSizing.ScaleForDpi(16, dpi));
        IntPtr big = LogoIcon(WindowSizing.ScaleForDpi(32, dpi));
        if (small != IntPtr.Zero)
            Win32.SendMessageW(hwnd, WM_SETICON, IntPtr.Zero, small);
        if (big != IntPtr.Zero)
            Win32.SendMessageW(hwnd, WM_SETICON, (IntPtr)1, big);
    }

    /// <summary>A12 : fond et texte de la barre de titre (COLORREF, ou
    /// <see cref="DWMWA_COLOR_DEFAULT"/> pour laisser Windows décider). Papier pour une
    /// fenêtre claire ; système pour une fenêtre sombre et en contraste élevé.</summary>
    internal static (uint Caption, uint Text) CaptionColors(bool dark, bool highContrast) =>
        dark || highContrast
            ? (DWMWA_COLOR_DEFAULT, DWMWA_COLOR_DEFAULT)
            : (PaperTheme.Paper, PaperTheme.TextSecondary);

    /// <summary>
    /// Le logo en icône carrée de <paramref name="size"/> px, rendue une fois par taille et
    /// gardée pour la vie du processus : les fenêtres la partagent, aucune ne la détruit.
    /// IntPtr.Zero quand GDI+ ou la ressource manque ; la fenêtre garde alors l'icône de Windows.
    /// </summary>
    private static IntPtr LogoIcon(int size)
    {
        lock (LogoIcons)
        {
            if (LogoIcons.TryGetValue(size, out IntPtr cached))
                return cached;

            IntPtr icon = IntPtr.Zero;
            var input = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
            if (Win32.GdiplusStartup(out IntPtr token, ref input, IntPtr.Zero) == 0)
            {
                IntPtr logo = GdiImageLoader.LoadFromEmbeddedResource(typeof(NativeWindow), ProductIdentity.LogoResourceName);
                if (logo != IntPtr.Zero)
                {
                    icon = GdiHelpers.CreateLogoIcon(logo, size);
                    Win32.GdipDisposeImage(logo);
                }
                Win32.GdiplusShutdown(token);
            }
            if (icon != IntPtr.Zero)
                LogoIcons[size] = icon;
            return icon;
        }
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

    [ThreadStatic] private static Win32.RECT? t_workAreaOverride;

    /// <summary>
    /// Lot 130 A — zone de travail simulée pour le fil courant, jusqu'au Dispose. Réservé au
    /// banc de capture : les troncatures de la CI viennent d'un écran 1024×768, que le poste
    /// d'Antoine n'a pas. Même principe que <see cref="CursorSource.OverrideForTests"/>.
    /// </summary>
    internal static IDisposable OverrideWorkAreaForTests(Win32.RECT work)
    {
        var previous = t_workAreaOverride;
        t_workAreaOverride = work;
        return new WorkAreaScope(previous);
    }

    private sealed class WorkAreaScope : IDisposable
    {
        private readonly Win32.RECT? _previous;
        private bool _disposed;

        internal WorkAreaScope(Win32.RECT? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            t_workAreaOverride = _previous;
        }
    }

    /// <summary>
    /// Zone de travail de l'écran du propriétaire, à défaut de celui du curseur, où
    /// l'utilisateur vient de cliquer ; 1024×768 quand aucun écran ne répond.
    /// </summary>
    internal static Win32.RECT WorkArea(IntPtr owner = default)
    {
        if (t_workAreaOverride is { } simulated)
            return simulated;

        IntPtr monitor = owner != IntPtr.Zero
            ? Win32.MonitorFromWindow(owner, Win32.MONITOR_DEFAULTTONEAREST)
            : IntPtr.Zero;

        if (monitor == IntPtr.Zero && CursorSource.TryGet(out var cursor))
            monitor = Win32.MonitorFromPoint(cursor, Win32.MONITOR_DEFAULTTONEAREST);

        var info = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        if (monitor != IntPtr.Zero && Win32.GetMonitorInfo(monitor, ref info))
            return info.rcWork;

        return new Win32.RECT { left = 0, top = 0, right = 1024, bottom = 768 };
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);

    /// <summary>Largeur de la barre de défilement verticale au DPI de la fenêtre : au DPI réel de
    /// l'écran, comme le cadre, que la fenêtre en défile ou non.</summary>
    internal static int ScrollBarWidth(IntPtr hwnd)
    {
        int width = GetSystemMetricsForDpi(Win32.SM_CXVSCROLL, (uint)DpiOf(hwnd));
        return width > 0 ? width : Win32.GetSystemMetrics(Win32.SM_CXVSCROLL);
    }

    /// <summary>Cadre et barre de titre d'une fenêtre : largeur et hauteur à ajouter à sa zone cliente.</summary>
    internal static (int Width, int Height) NonClientSize(uint style, uint exStyle = 0)
    {
        var (width, height) = OuterSize(100, 100, style, exStyle);
        return (width - 100, height - 100);
    }

    /// <summary>Hauteur d'un texte qui revient à la ligne dans <paramref name="width"/> px, dans
    /// la police donnée (DT_CALCRECT).</summary>
    internal static int MeasureWrapped(IntPtr hdc, IntPtr hFont, string text, int width)
    {
        IntPtr previous = Win32.SelectObject(hdc, hFont);
        var rect = new Win32.RECT { left = 0, top = 0, right = Math.Max(1, width), bottom = 9999 };
        Win32.DrawTextW(hdc, text, -1, ref rect,
            Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX | Win32.DT_CALCRECT);
        Win32.SelectObject(hdc, previous);
        return rect.bottom - rect.top;
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

    /// <summary>Ramène dans la zone de travail de son écran une fenêtre qui en dépasse, sans la
    /// redimensionner. Plus grande que la zone, elle se cale sur son coin haut-gauche, comme
    /// <see cref="CenterIn"/>, pour garder sa barre de titre à l'écran.</summary>
    internal static void KeepInWorkArea(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Win32.GetWindowRect(hwnd, out var current))
            return;

        var work = WorkArea(hwnd);
        int width = current.right - current.left;
        int height = current.bottom - current.top;
        int x = Math.Max(work.left, Math.Min(current.left, work.right - width));
        int y = Math.Max(work.top, Math.Min(current.top, work.bottom - height));
        if (x != current.left || y != current.top)
            Win32.MoveWindow(hwnd, x, y, width, height, true);
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
    private readonly IntPtr _redrawOnFont;

    /// <param name="redrawOnFont">WM_SETFONT avec repeint immédiat, avant le déplacement.
    /// Sur une fenêtre qui n'efface pas son fond, le contrôle repeint à son ancienne place
    /// y laisse sa trace (mesuré au banc le 26/09) : la Durée de pause ne le demande pas.</param>
    public ControlLayout(bool redrawOnFont = true) => _redrawOnFont = redrawOnFont ? (IntPtr)1 : IntPtr.Zero;

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
            Win32.SendMessageW(control, Win32.WM_SETFONT, fonts[font], _redrawOnFont);
            Win32.MoveWindow(control, x, y, w, h, true);
        }
    }
}
