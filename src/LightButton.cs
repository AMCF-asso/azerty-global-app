using System.Runtime.InteropServices;
using System.Text;

namespace AZERTYGlobal;

/// <summary>
/// Audit UX 1.3.0, #3 — dans les fenêtres claires, le bouton principal (BS_DEFPUSHBUTTON) ne
/// se distinguait des autres que par un liseré bleu. Cette sous-classe repeint un bouton
/// natif avec <see cref="GdiHelpers.DrawLightButton"/> : le principal porte l'accent.
///
/// Le bouton reste natif (pas de BS_OWNERDRAW) : Entrée, le bouton par défaut, BM_CLICK et
/// le rôle accessible ne changent pas. Seul son dessin est remplacé ; l'état vient de
/// BM_GETSTATE (survol, enfoncé, focus), que Common Controls 6 tient à jour.
/// </summary>
static class LightButton
{
    private const uint WM_ENABLE = 0x000A;
    private const uint WM_SETTEXT = 0x000C;
    private const uint WM_GETFONT = 0x0031;
    private const uint WM_NCDESTROY = 0x0082;
    private const uint WM_UPDATEUISTATE = 0x0128;
    private const uint WM_QUERYUISTATE = 0x0129;
    private const uint WM_CTLCOLORSTATIC = 0x0138;
    private const uint BM_GETSTATE = 0x00F2;
    private const uint BM_SETSTATE = 0x00F3;
    private const uint BM_SETSTYLE = 0x00F4;
    private const int BST_PUSHED = 0x0004;
    private const int BST_FOCUS = 0x0008;
    private const int BST_HOT = 0x0200;
    private const int UISF_HIDEFOCUS = 0x1;
    private const int GWL_STYLE = -16;
    private const uint BS_TYPEMASK = 0x000F;
    private static readonly UIntPtr SubclassId = (UIntPtr)0x4C42; // « LB »

    // Garde le délégué en vie tant que des boutons l'utilisent.
    private static readonly Win32.SUBCLASSPROC Proc = SubclassProc;

    [DllImport("user32.dll")]
    private static extern int GetWindowLongW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    /// <summary>Repeint <paramref name="button"/> au style clair ; sans effet sur un handle nul.
    /// <paramref name="paper"/> : palette papier des Leçons (Accueil, refonte du 15/10).</summary>
    public static void Attach(IntPtr button, bool paper = false)
    {
        if (button == IntPtr.Zero) return;
        Win32.SetWindowSubclass(button, Proc, SubclassId, paper ? (IntPtr)1 : IntPtr.Zero);
        Win32.InvalidateRect(button, IntPtr.Zero, true);
    }

    private static IntPtr SubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        switch (msg)
        {
            case Win32.WM_PAINT:
            {
                IntPtr hdc = Win32.BeginPaint(hWnd, out var ps);
                Paint(hWnd, hdc, dwRefData != IntPtr.Zero);
                Win32.EndPaint(hWnd, ref ps);
                return IntPtr.Zero;
            }
            case Win32.WM_PRINTCLIENT:
                Paint(hWnd, wParam, dwRefData != IntPtr.Zero);
                return IntPtr.Zero;
            case Win32.WM_ERASEBKGND:
                return (IntPtr)1;
            case WM_NCDESTROY:
                Win32.RemoveWindowSubclass(hWnd, Proc, uIdSubclass);
                break;
            case Win32.WM_SETFOCUS:
            case Win32.WM_KILLFOCUS:
            case WM_ENABLE:
            case WM_SETTEXT:
            case WM_UPDATEUISTATE:
            case BM_SETSTATE:
            case BM_SETSTYLE:
            {
                // Le contrôle natif peut se peindre lui-même sur ces changements d'état : on
                // laisse faire, puis on repeint par-dessus.
                IntPtr result = Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
                Win32.InvalidateRect(hWnd, IntPtr.Zero, false);
                return result;
            }
        }
        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private static void Paint(IntPtr hWnd, IntPtr hdc, bool paper)
    {
        Win32.GetClientRect(hWnd, out var rect);
        int dpi = Math.Max(96, Win32.GetDpiForWindow(hWnd));
        int S(int v) => (int)Math.Round(v * dpi / 96.0);

        int state = (int)Win32.SendMessageW(hWnd, BM_GETSTATE, IntPtr.Zero, IntPtr.Zero);
        bool primary = ((uint)GetWindowLongW(hWnd, GWL_STYLE) & BS_TYPEMASK) == Win32.BS_DEFPUSHBUTTON;
        bool enabled = Win32.IsWindowEnabled(hWnd);
        var text = new StringBuilder(256);
        Win32.GetWindowTextW(hWnd, text, text.Capacity);
        IntPtr font = Win32.SendMessageW(hWnd, WM_GETFONT, IntPtr.Zero, IntPtr.Zero);

        GdiHelpers.DrawLightButton(hdc, rect, text.ToString(), font, primary,
            hot: (state & BST_HOT) != 0, pressed: (state & BST_PUSHED) != 0, enabled, S(4), Backdrop(hWnd, hdc, paper), paper);

        bool hideFocus = ((int)Win32.SendMessageW(hWnd, WM_QUERYUISTATE, IntPtr.Zero, IntPtr.Zero) & UISF_HIDEFOCUS) != 0;
        if ((state & BST_FOCUS) != 0 && !hideFocus)
        {
            // Anneau intérieur arrondi : le rectangle pointillé en XOR virait à l'orange sur l'accent.
            int inset = S(3);
            // A13 : en papier, l'anneau prend l'encre du papier, plus celle de la palette Ardoise.
            uint ring = primary && enabled
                ? (paper ? PaperTheme.OnAction : LightTheme.OnAccent)
                : (paper ? PaperTheme.Ink : LightTheme.Text);
            var pen = Win32.CreatePen(0, Math.Max(1, S(1)), ring);
            var oldPen = Win32.SelectObject(hdc, pen);
            var oldBrush = Win32.SelectObject(hdc, Win32.GetStockObject(Win32.NULL_BRUSH));
            Win32.RoundRect(hdc, rect.left + inset, rect.top + inset, rect.right - inset, rect.bottom - inset, S(4), S(4));
            Win32.SelectObject(hdc, oldBrush);
            Win32.SelectObject(hdc, oldPen);
            Win32.DeleteObject(pen);
        }
    }

    /// <summary>
    /// Couleur des coins arrondis : le fond que la fenêtre donne à ses étiquettes
    /// (WM_CTLCOLORSTATIC), sinon le fond clair commun.
    /// </summary>
    private static uint Backdrop(IntPtr hWnd, IntPtr hdc, bool paper)
    {
        IntPtr parent = GetParent(hWnd);
        IntPtr brush = parent == IntPtr.Zero ? IntPtr.Zero
            : Win32.SendMessageW(parent, WM_CTLCOLORSTATIC, hdc, hWnd);
        return brush != IntPtr.Zero && GetObjectW(brush, Marshal.SizeOf<LOGBRUSH>(), out var lb) != 0 && lb.lbStyle == 0
            ? lb.lbColor
            : paper ? PaperTheme.Paper : LightTheme.Background;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LOGBRUSH { public uint lbStyle; public uint lbColor; public UIntPtr lbHatch; }

    [DllImport("gdi32.dll")]
    private static extern int GetObjectW(IntPtr h, int c, out LOGBRUSH pv);
}
