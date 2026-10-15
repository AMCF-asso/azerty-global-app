// Pièces de dessin communes aux écrans refondus en 1.3.0 (direction Tablature).
namespace AZERTYGlobal;

/// <summary>Rectangles, contours et cases des écrans « papier ». Chaque appel crée et
/// libère ses objets GDI : rien ne fuit d'un WM_PAINT au suivant.</summary>
internal static class PaperDraw
{
    internal static void Fill(IntPtr hdc, Win32.RECT rect, uint color) => GdiHelpers.FillSolidRect(hdc, rect, color);

    internal static void Fill(IntPtr hdc, int left, int top, int right, int bottom, uint color)
        => GdiHelpers.FillSolidRect(hdc, new Win32.RECT { left = left, top = top, right = right, bottom = bottom }, color);

    /// <summary>Contour plein de <paramref name="width"/> pixels, tracé à l'intérieur du cadre.</summary>
    internal static void Frame(IntPtr hdc, Win32.RECT r, uint color, int width = 1)
    {
        Fill(hdc, r.left, r.top, r.right, r.top + width, color);
        Fill(hdc, r.left, r.bottom - width, r.right, r.bottom, color);
        Fill(hdc, r.left, r.top, r.left + width, r.bottom, color);
        Fill(hdc, r.right - width, r.top, r.right, r.bottom, color);
    }

    /// <summary>Rectangle aux coins arrondis (rayon <paramref name="radius"/>), rempli et
    /// cerné. <paramref name="penStyle"/> <see cref="Win32.PS_DASH"/> donne la touche morte.</summary>
    internal static void RoundBox(IntPtr hdc, Win32.RECT r, uint fill, uint border, int radius, int borderWidth = 1,
        int penStyle = Win32.PS_SOLID)
    {
        var brush = Win32.CreateSolidBrush(fill);
        var pen = Win32.CreatePen(penStyle, penStyle == Win32.PS_SOLID ? borderWidth : 1, border);
        var oldBrush = Win32.SelectObject(hdc, brush);
        var oldPen = Win32.SelectObject(hdc, pen);
        // RoundRect exclut le bord droit et le bas : +1 pour couvrir le cadre entier.
        Win32.RoundRect(hdc, r.left, r.top, r.right + 1, r.bottom + 1, radius * 2, radius * 2);
        if (penStyle == Win32.PS_SOLID && borderWidth > 1)
        {
            // Un stylo épais déborde de moitié : on le reprend à l'intérieur.
            Win32.SelectObject(hdc, Win32.GetStockObject(Win32.NULL_BRUSH));
            for (int i = 1; i < borderWidth; i++)
                Win32.RoundRect(hdc, r.left + i, r.top + i, r.right + 1 - i, r.bottom + 1 - i, radius * 2, radius * 2);
        }
        Win32.SelectObject(hdc, oldPen);
        Win32.SelectObject(hdc, oldBrush);
        Win32.DeleteObject(pen);
        Win32.DeleteObject(brush);
    }

    /// <summary>Mélange <paramref name="color"/> à <paramref name="opacity"/> sur
    /// <paramref name="backdrop"/> (COLORREF).</summary>
    internal static uint Blend(uint color, uint backdrop, float opacity)
    {
        static uint C(uint c, int shift) => (c >> shift) & 0xFF;
        uint Mix(int shift) => (uint)Math.Round(C(color, shift) * opacity + C(backdrop, shift) * (1 - opacity));
        return Mix(0) | (Mix(8) << 8) | (Mix(16) << 16);
    }

    /// <summary>
    /// Repère d'une espace insécable, dessiné comme dans l'épreuve d'un correcteur : un
    /// crochet ouvert vers le haut, posé sur la ligne de base. L'espace fine est plus étroite
    /// et d'un trait plus fin (maquette Tablature).
    /// </summary>
    internal static void SpaceMark(IntPtr hdc, char ch, int left, int width, int baseline, int height, float scale, uint color)
    {
        bool thin = ch == ' ';
        int stroke = Math.Max(1, (int)((thin ? 1.5f : 2f) * scale));
        int markWidth = width;
        int x = left;
        int bottom = baseline + Math.Max(1, height / 3);
        int top = bottom - height;
        Fill(hdc, x, bottom - stroke, x + markWidth, bottom, color);
        Fill(hdc, x, top, x + stroke, bottom, color);
        Fill(hdc, x + markWidth - stroke, top, x + markWidth, bottom, color);
    }
}
