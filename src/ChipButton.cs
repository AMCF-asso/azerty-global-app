using System.Text;

namespace AZERTYGlobal;

/// <summary>
/// Atelier du 2026-11-03 (fenêtre « Mettre en pause », variante G2) : une durée courante en
/// pastille. Chaque pastille est un bouton radio natif à aspect de bouton (BS_PUSHLIKE) :
/// Tab entre dans le groupe, les flèches passent d'une pastille à l'autre, Espace choisit, et
/// le rôle et l'état « coché » restent ceux du système pour le Narrateur. Seul son dessin est
/// remplacé, comme <see cref="LightButton"/> : la pastille choisie prend la teinte d'action
/// et un contour d'action, les autres le blanc du papier et un contour d'encre.
/// </summary>
static class ChipButton
{
    internal const uint BS_PUSHLIKE = 0x1000;
    private const uint WM_ENABLE = 0x000A;
    private const uint WM_SETTEXT = 0x000C;
    private const uint WM_GETFONT = 0x0031;
    private const uint WM_NCDESTROY = 0x0082;
    private const uint WM_UPDATEUISTATE = 0x0128;
    private const uint BM_GETSTATE = 0x00F2;
    private const uint BM_SETSTATE = 0x00F3;
    private const int BST_PUSHED = 0x0004;
    private const int BST_FOCUS = 0x0008;
    private const int BST_HOT = 0x0200;
    private static readonly UIntPtr SubclassId = (UIntPtr)0x4348; // « CH »

    // Garde le délégué en vie, et la palette de chaque pastille (retirée à sa destruction).
    private static readonly Win32.SUBCLASSPROC Proc = SubclassProc;
    private static readonly Dictionary<IntPtr, PaperPalette> Palettes = new();

    /// <summary>Peint <paramref name="chip"/> en pastille ; sans effet sur un handle nul.</summary>
    public static void Attach(IntPtr chip, PaperPalette palette)
    {
        if (chip == IntPtr.Zero) return;
        Palettes[chip] = palette;
        Win32.SetWindowSubclass(chip, Proc, SubclassId, IntPtr.Zero);
        Win32.InvalidateRect(chip, IntPtr.Zero, true);
    }

    private static IntPtr SubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        switch (msg)
        {
            case Win32.WM_PAINT:
            {
                IntPtr hdc = Win32.BeginPaint(hWnd, out var ps);
                Paint(hWnd, hdc);
                Win32.EndPaint(hWnd, ref ps);
                return IntPtr.Zero;
            }
            case Win32.WM_PRINTCLIENT:
                Paint(hWnd, wParam);
                return IntPtr.Zero;
            case Win32.WM_ERASEBKGND:
                return (IntPtr)1;
            case WM_NCDESTROY:
                Palettes.Remove(hWnd);
                Win32.RemoveWindowSubclass(hWnd, Proc, uIdSubclass);
                break;
            case Win32.WM_SETFOCUS:
            case Win32.WM_KILLFOCUS:
            case WM_ENABLE:
            case WM_SETTEXT:
            case WM_UPDATEUISTATE:
            case Win32.BM_SETCHECK:
            case BM_SETSTATE:
            {
                // Le contrôle natif peut se peindre sur ces changements d'état : on le laisse
                // faire, puis on repeint par-dessus.
                IntPtr result = Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
                Win32.InvalidateRect(hWnd, IntPtr.Zero, false);
                return result;
            }
        }
        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private static void Paint(IntPtr hWnd, IntPtr hdc)
    {
        if (!Palettes.TryGetValue(hWnd, out var pal)) return;
        Win32.GetClientRect(hWnd, out var rect);
        int dpi = Math.Max(96, Win32.GetDpiForWindow(hWnd));
        int S(int v) => (int)Math.Round(v * dpi / 96.0);

        int state = (int)Win32.SendMessageW(hWnd, BM_GETSTATE, IntPtr.Zero, IntPtr.Zero);
        bool chosen = Win32.SendMessageW(hWnd, Win32.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)Win32.BST_CHECKED;
        bool hot = (state & BST_HOT) != 0 || (state & BST_PUSHED) != 0;
        bool enabled = Win32.IsWindowEnabled(hWnd);
        var text = new StringBuilder(64);
        Win32.GetWindowTextW(hWnd, text, text.Capacity);
        IntPtr font = Win32.SendMessageW(hWnd, WM_GETFONT, IntPtr.Zero, IntPtr.Zero);

#if DEBUG
        DevRects.Element(hdc, "bouton", text.ToString(), rect.left, rect.top, rect.right, rect.bottom);
#endif
        PaperDraw.Fill(hdc, rect, pal.Paper);

        // Le contour de focus se pose à l'extérieur : on laisse S(3) de marge autour de la pastille.
        int margin = S(3);
        var pill = new Win32.RECT
        {
            left = rect.left + margin, top = rect.top + margin,
            right = rect.right - margin - 1, bottom = rect.bottom - margin - 1,
        };
        uint fill = !enabled ? pal.Paper
            : chosen ? pal.ActionTint
            : hot ? pal.FunctionKey
            : pal.Surface;
        uint border = !enabled ? pal.Border : chosen ? pal.Action : pal.Ink;
        int radius = (pill.bottom - pill.top) / 2;
        PaperDraw.RoundBox(hdc, pill, fill, border, radius, chosen ? Math.Max(2, S(2)) : Math.Max(1, S(1)));

        var oldFont = Win32.SelectObject(hdc, font);
        Win32.SetBkMode(hdc, Win32.TRANSPARENT);
        Win32.SetTextColor(hdc, enabled ? pal.Ink : pal.Pale);
        var textRect = pill;
        Win32.DrawTextW(hdc, text.ToString(), -1, ref textRect,
            Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX | Win32.DT_END_ELLIPSIS);
        Win32.SelectObject(hdc, oldFont);

        if ((state & BST_FOCUS) != 0 && PaperChoice.FocusCuesShown(hWnd))
            PaperDraw.FocusOutline(hdc, pill, pal.Ink, S(2), Math.Max(1, S(2)), radius * 2 + S(4));
    }
}
