using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Lot 130 A (D1 d'Antoine, 29/10) — défilement vertical d'une fenêtre Win32 dont les contrôles
/// enfants sont posés d'un seul tenant : la barre de défilement du cadre, la molette, le clavier
/// (<see cref="EnsureVisible"/>, pour Tab) et le glissé du curseur.
///
/// Même mécanique que les Paramètres (<c>WM_VSCROLL</c>, <c>WM_MOUSEWHEEL</c>,
/// <see cref="SettingsScrollState"/>), mais sans repasser par la mise en page : la fenêtre pose
/// ses contrôles à l'origine, appelle <see cref="Reset"/>, et chaque défilement décale tous les
/// enfants directs du même nombre de pixels. La fenêtre doit porter <c>WS_VSCROLL</c> dès sa
/// création, comme les Paramètres : la barre existe alors, et <see cref="Reset"/> la masque quand
/// le contenu tient.
/// </summary>
internal sealed class WindowScroll
{
    private const uint GW_HWNDNEXT = 2;
    private const uint GW_CHILD = 5;

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    private readonly SettingsScrollState _input = new();

    /// <summary>Décalage actuel du contenu, en pixels (0 : le haut).</summary>
    public int Position { get; private set; }

    public int ContentHeight { get; private set; }

    public int ViewHeight { get; private set; }

    public bool Needed => ContentHeight > ViewHeight;

    /// <summary>
    /// Nouvelle mise en page : les contrôles viennent d'être posés à l'origine (défilement nul).
    /// Règle la plage de la barre, et la masque quand le contenu tient.
    /// </summary>
    public void Reset(IntPtr hwnd, int contentHeight, int viewHeight)
    {
        Position = 0;
        ContentHeight = Math.Max(0, contentHeight);
        ViewHeight = Math.Max(1, viewHeight);
        UpdateBar(hwnd);
    }

    /// <summary>Position bornée à [0, contenu − visible], sans effet de bord.</summary>
    public int Clamp(int target) => Math.Clamp(target, 0, Math.Max(0, ContentHeight - ViewHeight));

    /// <summary>Défile jusqu'à <paramref name="target"/> (borné) : décale les enfants et la barre.</summary>
    public void ScrollTo(IntPtr hwnd, int target)
    {
        target = Clamp(target);
        int shift = Position - target;
        if (shift == 0) return;

        Position = target;
        ShiftChildren(hwnd, shift);
        UpdateBar(hwnd);
    }

    private const int VK_PRIOR = 0x21, VK_NEXT = 0x22, VK_END = 0x23, VK_HOME = 0x24, VK_UP = 0x26, VK_DOWN = 0x28;

    /// <summary>WM_VSCROLL, WM_MOUSEWHEEL et WM_KEYDOWN, que les deux fenêtres traitent de la même
    /// façon. Rend vrai quand le message est traité ; tout autre message, ou toute autre touche
    /// (Tab, Échap, lettres), rend faux.</summary>
    public bool TryHandle(IntPtr hwnd, uint msg, IntPtr wParam, int line) => msg switch
    {
        Win32.WM_VSCROLL => OnVScroll(hwnd, wParam, line),
        Win32.WM_MOUSEWHEEL => OnMouseWheel(hwnd, wParam, line),
        Win32.WM_KEYDOWN => OnKeyDown(hwnd, wParam, line),
        _ => false,
    };

    /// <summary>WM_KEYDOWN reçu par la fenêtre elle-même : Page haut et bas, Début, Fin, flèches.</summary>
    public bool OnKeyDown(IntPtr hwnd, IntPtr wParam, int line)
    {
        int command = (int)(wParam.ToInt64() & 0xFFFF) switch
        {
            VK_PRIOR => Win32.SB_PAGEUP,
            VK_NEXT => Win32.SB_PAGEDOWN,
            VK_HOME => Win32.SB_TOP,
            VK_END => Win32.SB_BOTTOM,
            VK_UP => Win32.SB_LINEUP,
            VK_DOWN => Win32.SB_LINEDOWN,
            _ => -1,
        };
        return command >= 0 && OnVScroll(hwnd, (IntPtr)command, line);
    }

    /// <summary>
    /// Le focus est presque toujours sur un contrôle, et le WM_KEYDOWN va alors à lui, jamais à la
    /// fenêtre. Branché sur <see cref="DialogNavigation.Register"/> : Page haut et bas, Début et Fin
    /// défilent quand le contrôle est un bouton ou une case, qui n'en font rien. Un champ, une liste
    /// ou une liste déroulante gardent leurs touches ; les flèches restent à la navigation de
    /// dialogue, qui déplace le focus (<see cref="EnsureVisible"/> le suit).
    /// </summary>
    public bool OnControlKey(IntPtr hwnd, in Win32.MSG msg, int line)
    {
        if (msg.message != Win32.WM_KEYDOWN || msg.hwnd == hwnd || !Needed) return false;

        int vk = (int)(msg.wParam.ToInt64() & 0xFFFF);
        if (vk is not (VK_PRIOR or VK_NEXT or VK_HOME or VK_END)) return false;

        var name = new System.Text.StringBuilder(16);
        // GetClassNameW vient du moteur (Win32 de TypingEngine.Windows) : nom qualifié, comme
        // CharacterSearch, la classe Win32 de l'application le masque dans ce namespace.
        if (TypingEngine.Windows.Win32.GetClassNameW(msg.hwnd, name, name.Capacity) <= 0
            || !name.ToString().Equals("Button", StringComparison.OrdinalIgnoreCase))
            return false;
        return OnKeyDown(hwnd, msg.wParam, line);
    }

    /// <summary>WM_VSCROLL. Rend vrai quand le message est traité.</summary>
    public bool OnVScroll(IntPtr hwnd, IntPtr wParam, int line)
    {
        if (!Needed) return false;

        int page = Math.Max(line, ViewHeight - line);
        int target = Position;
        switch (wParam.ToInt32() & 0xFFFF)
        {
            case Win32.SB_LINEUP: target -= line; break;
            case Win32.SB_LINEDOWN: target += line; break;
            case Win32.SB_PAGEUP: target -= page; break;
            case Win32.SB_PAGEDOWN: target += page; break;
            case Win32.SB_TOP: target = 0; break;
            case Win32.SB_BOTTOM: target = ContentHeight; break;
            case Win32.SB_THUMBTRACK:
            {
                // La position du curseur dépasse 16 bits sur un contenu long : la lire dans
                // SCROLLINFO, jamais dans le mot haut de wParam.
                var si = new Win32.SCROLLINFO
                {
                    cbSize = (uint)Marshal.SizeOf<Win32.SCROLLINFO>(),
                    fMask = Win32.SIF_TRACKPOS
                };
                if (Win32.GetScrollInfo(hwnd, Win32.SB_VERT, ref si))
                    target = si.nTrackPos;
                break;
            }
        }
        ScrollTo(hwnd, target);
        return true;
    }

    /// <summary>WM_MOUSEWHEEL : trois lignes par cran, comme le défilement par défaut de Windows.</summary>
    public bool OnMouseWheel(IntPtr hwnd, IntPtr wParam, int line)
    {
        if (!Needed) return false;

        int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
        ScrollTo(hwnd, Position - _input.ConsumeWheel(delta) * line * 3);
        return true;
    }

    /// <summary>
    /// Le contrôle qui prend le focus au clavier doit se voir : sans cela, Tab vers « Fermer »
    /// ou « Quitter » sous le pli restait invisible (WCAG 2.4.7). Branché sur
    /// <see cref="DialogNavigation.Register"/>.
    /// </summary>
    public void EnsureVisible(IntPtr hwnd, IntPtr control, int margin)
    {
        if (control == IntPtr.Zero || control == hwnd || !Needed) return;
        if (!Win32.GetWindowRect(control, out var rect)) return;

        var top = new Win32.POINT { x = rect.left, y = rect.top };
        var bottom = new Win32.POINT { x = rect.right, y = rect.bottom };
        if (!Win32.ScreenToClient(hwnd, ref top) || !Win32.ScreenToClient(hwnd, ref bottom)) return;
        ScrollTo(hwnd, SettingsScrollState.EnsureVisible(Position, top.y, bottom.y, ViewHeight, margin));
    }

    private void UpdateBar(IntPtr hwnd)
    {
        bool needed = Needed;
        Win32.ShowScrollBar(hwnd, Win32.SB_VERT, needed);
        if (!needed) return;

        var si = new Win32.SCROLLINFO
        {
            cbSize = (uint)Marshal.SizeOf<Win32.SCROLLINFO>(),
            fMask = Win32.SIF_RANGE | Win32.SIF_PAGE | Win32.SIF_POS,
            nMin = 0,
            nMax = Math.Max(0, ContentHeight - 1),
            nPage = (uint)ViewHeight,
            nPos = Position
        };
        Win32.SetScrollInfo(hwnd, Win32.SB_VERT, ref si, true);
    }

    /// <summary>Décale de <paramref name="dy"/> px chaque enfant direct, sans toucher à sa taille.</summary>
    private static void ShiftChildren(IntPtr hwnd, int dy)
    {
        for (IntPtr child = GetWindow(hwnd, GW_CHILD); child != IntPtr.Zero; child = GetWindow(child, GW_HWNDNEXT))
        {
            if (!Win32.GetWindowRect(child, out var rect)) continue;
            var origin = new Win32.POINT { x = rect.left, y = rect.top };
            if (!Win32.ScreenToClient(hwnd, ref origin)) continue;
            Win32.SetWindowPos(child, IntPtr.Zero, origin.x, origin.y + dy, 0, 0,
                Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        }
    }
}
