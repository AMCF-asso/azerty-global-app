// Cases à cocher et boutons radio papier — audit 1.3.0, A9 (lot 3a « fenêtres »).
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Le dessin papier d'une case à cocher ou d'un bouton radio natif, peint par la fenêtre
/// parente sur NM_CUSTOMDRAW : indicateur de 18 px lissé en GDI+, libellé à 10 px. Il vivait
/// dans les Paramètres ; les Couches maintenables le reprennent (A9), où la case native se
/// remplissait de l'accent de Windows. Le contrôle reste natif : Espace, le rôle et l'état
/// accessibles ne changent pas. En contraste élevé, l'appelant laisse dessiner le système.
/// Le libellé se dessine sur une ligne : une case dont le texte revient à la ligne ne
/// convient pas.
/// </summary>
internal static class PaperChoice
{
    [DllImport("gdiplus.dll")]
    private static extern int GdipCreateSolidFill(uint argb, out IntPtr brush);

    [DllImport("gdiplus.dll")]
    private static extern int GdipDeleteBrush(IntPtr brush);

    [DllImport("gdiplus.dll")]
    private static extern int GdipCreatePen1(uint argb, float width, int unit, out IntPtr pen);

    [DllImport("gdiplus.dll")]
    private static extern int GdipDeletePen(IntPtr pen);

    [DllImport("gdiplus.dll")]
    private static extern int GdipSetPenLineCap197819(IntPtr pen, int startCap, int endCap, int dashCap);

    [DllImport("gdiplus.dll")]
    private static extern int GdipSetPenLineJoin(IntPtr pen, int lineJoin);

    [DllImport("gdiplus.dll")]
    private static extern int GdipFillEllipse(IntPtr graphics, IntPtr brush, float x, float y, float width, float height);

    [DllImport("gdiplus.dll")]
    private static extern int GdipDrawEllipse(IntPtr graphics, IntPtr pen, float x, float y, float width, float height);

    [DllImport("gdiplus.dll")]
    private static extern int GdipDrawLine(IntPtr graphics, IntPtr pen, float x1, float y1, float x2, float y2);

    [DllImport("gdiplus.dll")]
    private static extern int GdipSetPixelOffsetMode(IntPtr graphics, int mode);

    [DllImport("gdiplus.dll")]
    private static extern int GdipCreatePath(int fillMode, out IntPtr path);

    [DllImport("gdiplus.dll")]
    private static extern int GdipDeletePath(IntPtr path);

    [DllImport("gdiplus.dll")]
    private static extern int GdipAddPathArc(IntPtr path, float x, float y, float width, float height, float startAngle, float sweepAngle);

    [DllImport("gdiplus.dll")]
    private static extern int GdipClosePathFigure(IntPtr path);

    [DllImport("gdiplus.dll")]
    private static extern int GdipFillPath(IntPtr graphics, IntPtr brush, IntPtr path);

    [DllImport("gdiplus.dll")]
    private static extern int GdipDrawPath(IntPtr graphics, IntPtr pen, IntPtr path);

    private const int UnitPixel = 2;
    private const int LineCapRound = 2;
    private const int LineJoinRound = 2;

    /// <summary>Code de notification de NM_CUSTOMDRAW.</summary>
    internal const int NM_CUSTOMDRAW = -12;
    internal const uint CDDS_PREPAINT = 0x0001;
    internal const int CDRF_DODEFAULT = 0x0000;
    internal const int CDRF_SKIPDEFAULT = 0x0004;
    internal const uint CDIS_SELECTED = 0x0001;
    internal const uint CDIS_DISABLED = 0x0004;
    internal const uint CDIS_FOCUS = 0x0010;
    internal const uint CDIS_HOT = 0x0040;
    private const uint WM_QUERYUISTATE = 0x0129;
    private const int UISF_HIDEFOCUS = 0x1;
    private const int GWL_STYLE = -16;
    private const uint BS_TYPEMASK = 0x000F;
    private const uint BS_RADIOBUTTON = 0x0004;
    private const uint BS_AUTORADIOBUTTON = 0x0009;

    [StructLayout(LayoutKind.Sequential)]
    internal struct NMCUSTOMDRAW
    {
        public Win32.NMHDR hdr;
        public uint dwDrawStage;
        public IntPtr hdc;
        public Win32.RECT rc;
        public UIntPtr dwItemSpec;
        public uint uItemState;
        public IntPtr lItemlParam;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLongW(IntPtr hWnd, int nIndex);

    /// <summary>Le contour de focus se montre quand le clavier sert (état d'interface de
    /// Windows), comme pour les contrôles du système.</summary>
    internal static bool FocusCuesShown(IntPtr hWnd)
        => ((int)Win32.SendMessageW(hWnd, WM_QUERYUISTATE, IntPtr.Zero, IntPtr.Zero) & UISF_HIDEFOCUS) == 0;

    /// <summary>
    /// Réponse complète à NM_CUSTOMDRAW pour une case ou un bouton radio : à l'étape
    /// PREPAINT, fond <see cref="PaperPalette.Paper"/> puis <see cref="Draw"/> dans une
    /// surface tampon, et CDRF_SKIPDEFAULT ; aux autres étapes, le dessin du système.
    /// L'appelant ne l'appelle pas en contraste élevé.
    /// </summary>
    internal static IntPtr CustomDraw(IntPtr lParam, FontSpec font, float scale, PaperPalette palette)
    {
        var cd = Marshal.PtrToStructure<NMCUSTOMDRAW>(lParam);
        if (cd.dwDrawStage != CDDS_PREPAINT)
            return (IntPtr)CDRF_DODEFAULT;

        IntPtr hWnd = cd.hdr.hwndFrom;
        var rc = cd.rc;
        int w = rc.right - rc.left, h = rc.bottom - rc.top;
        if (w <= 0 || h <= 0) return (IntPtr)CDRF_SKIPDEFAULT;

        var (dc, bitmap, previous) = PaintBuffer.CreateSurface(w, h);
        IntPtr hdc = dc != IntPtr.Zero ? dc : cd.hdc;
        var r = dc != IntPtr.Zero ? new Win32.RECT { right = w, bottom = h } : rc;
        try
        {
            PaperDraw.Fill(hdc, r, palette.Paper);
            uint type = (uint)GetWindowLongW(hWnd, GWL_STYLE) & BS_TYPEMASK;
            bool radio = type is BS_RADIOBUTTON or BS_AUTORADIOBUTTON;
            bool isChecked = Win32.SendMessageW(hWnd, Win32.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)Win32.BST_CHECKED;
            bool enabled = Win32.IsWindowEnabled(hWnd) && (cd.uItemState & CDIS_DISABLED) == 0;
            bool focused = (cd.uItemState & CDIS_FOCUS) != 0 && FocusCuesShown(hWnd);
            var text = new System.Text.StringBuilder(256);
            Win32.GetWindowTextW(hWnd, text, text.Capacity);
            Draw(hdc, r, text.ToString(), font, radio, isChecked, enabled, focused, scale, palette);
        }
        finally
        {
            if (dc != IntPtr.Zero)
            {
                Win32.BitBlt(cd.hdc, rc.left, rc.top, w, h, dc, 0, 0, Win32.SRCCOPY);
                PaintBuffer.ReleaseSurface(dc, bitmap, previous);
            }
        }
        return (IntPtr)CDRF_SKIPDEFAULT;
    }

    /// <summary>
    /// Case à cocher ou bouton radio dans <paramref name="r"/>, à l'échelle
    /// <paramref name="scale"/> (1 à 96 DPI), aux couleurs de <paramref name="palette"/>.
    /// </summary>
    internal static void Draw(IntPtr hdc, Win32.RECT r, string text, FontSpec font, bool radio, bool isChecked,
        bool enabled, bool focused, float scale, PaperPalette palette)
    {
        int S(int value) => (int)(value * scale);
        int size = S(18);
        int left = r.left + S(4);
        int top = r.top + (r.bottom - r.top - size) / 2;
        var indicator = new Win32.RECT { left = left, top = top, right = left + size, bottom = top + size };
        uint ink = enabled ? palette.Ink : palette.Pale;
        uint action = enabled ? palette.Action : palette.Pale;
        float stroke = 1.5f * scale;

        IntPtr gfx = IntPtr.Zero;
        Win32.GdipCreateFromHDC(hdc, out gfx);
        try
        {
            if (gfx != IntPtr.Zero)
            {
                Win32.GdipSetSmoothingMode(gfx, 4);
                GdipSetPixelOffsetMode(gfx, 4);
            }

            if (focused)
            {
                float ring = 2f * scale;
                if (radio)
                    StrokeEllipse(gfx, indicator.left - 3f * scale + ring / 2, indicator.top - 3f * scale + ring / 2,
                        size + 6f * scale - ring, palette.Action, ring);
                else
                    SmoothRoundBox(gfx, indicator.left - 3f * scale, indicator.top - 3f * scale,
                        size + 6f * scale, 5f * scale, palette.Paper, palette.Action, ring);
            }

            if (radio)
            {
                float x = indicator.left + stroke / 2, y = indicator.top + stroke / 2, d = size - stroke;
                FillEllipse(gfx, indicator.left, indicator.top, size, palette.Surface);
                StrokeEllipse(gfx, x, y, d, isChecked ? action : ink, stroke);
                if (isChecked)
                {
                    float inset = stroke + 3f * scale;
                    FillEllipse(gfx, indicator.left + inset, indicator.top + inset, size - 2 * inset, action);
                }
            }
            else
            {
                float radius = 3f * scale;
                if (isChecked)
                {
                    SmoothRoundBox(gfx, indicator.left, indicator.top, size, radius, action, action, stroke);
                    DrawTick(gfx, indicator, scale, palette.OnAction);
                }
                else
                {
                    SmoothRoundBox(gfx, indicator.left, indicator.top, size, radius, palette.Surface, ink, stroke);
                }
            }
        }
        finally
        {
            if (gfx != IntPtr.Zero) Win32.GdipDeleteGraphics(gfx);
        }

        var textRect = new Win32.RECT
        {
            left = indicator.right + S(10), top = r.top,
            right = indicator.right + S(10) + Math.Max(0, r.right - indicator.right - S(10)), bottom = r.bottom,
        };
        int save = Win32.SaveDC(hdc);
        Win32.IntersectClipRect(hdc, textRect.left, textRect.top, textRect.right, textRect.bottom);
        PaperText.DrawLine(hdc, text, font, textRect, enabled ? palette.Ink : palette.Pale);
        Win32.RestoreDC(hdc, save);
    }

    /// <summary>Carré arrondi lissé (indicateur de case) : fond puis trait de
    /// <paramref name="stroke"/> px, posé à l'intérieur du cadre.</summary>
    private static void SmoothRoundBox(IntPtr gfx, float x, float y, float size, float radius, uint fill, uint border, float stroke)
    {
        if (gfx == IntPtr.Zero || GdipCreatePath(0, out IntPtr path) != 0) return;
        try
        {
            float half = stroke / 2;
            float left = x + half, top = y + half, side = size - stroke, d = radius * 2;
            GdipAddPathArc(path, left, top, d, d, 180, 90);
            GdipAddPathArc(path, left + side - d, top, d, d, 270, 90);
            GdipAddPathArc(path, left + side - d, top + side - d, d, d, 0, 90);
            GdipAddPathArc(path, left, top + side - d, d, d, 90, 90);
            GdipClosePathFigure(path);
            if (GdipCreateSolidFill(Argb(fill), out IntPtr brush) == 0)
            {
                GdipFillPath(gfx, brush, path);
                GdipDeleteBrush(brush);
            }
            if (GdipCreatePen1(Argb(border), stroke, UnitPixel, out IntPtr pen) == 0)
            {
                GdipDrawPath(gfx, pen, path);
                GdipDeletePen(pen);
            }
        }
        finally { GdipDeletePath(path); }
    }

    /// <summary>COLORREF (0x00BBGGRR) → ARGB opaque de GDI+.</summary>
    private static uint Argb(uint colorRef)
        => 0xFF000000u | ((colorRef & 0xFF) << 16) | (colorRef & 0xFF00) | ((colorRef >> 16) & 0xFF);

    private static void FillEllipse(IntPtr gfx, float x, float y, float d, uint color)
    {
        if (gfx == IntPtr.Zero) return;
        if (GdipCreateSolidFill(Argb(color), out IntPtr brush) != 0) return;
        GdipFillEllipse(gfx, brush, x, y, d, d);
        GdipDeleteBrush(brush);
    }

    private static void StrokeEllipse(IntPtr gfx, float x, float y, float d, uint color, float width)
    {
        if (gfx == IntPtr.Zero) return;
        if (GdipCreatePen1(Argb(color), width, UnitPixel, out IntPtr pen) != 0) return;
        GdipDrawEllipse(gfx, pen, x, y, d, d);
        GdipDeletePen(pen);
    }

    /// <summary>La coche d'une case cochée, trait arrondi de 2 px.</summary>
    private static void DrawTick(IntPtr gfx, Win32.RECT box, float scale, uint color)
    {
        if (gfx == IntPtr.Zero) return;
        if (GdipCreatePen1(Argb(color), 2f * scale, UnitPixel, out IntPtr pen) != 0) return;
        GdipSetPenLineCap197819(pen, LineCapRound, LineCapRound, 0);
        GdipSetPenLineJoin(pen, LineJoinRound);
        float u = scale;
        float x = box.left, y = box.top;
        GdipDrawLine(gfx, pen, x + 4.5f * u, y + 9.5f * u, x + 7.5f * u, y + 12.5f * u);
        GdipDrawLine(gfx, pen, x + 7.5f * u, y + 12.5f * u, x + 13.5f * u, y + 5.5f * u);
        GdipDeletePen(pen);
    }
}
