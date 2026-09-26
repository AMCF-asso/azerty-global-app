// Méthodes GDI partagées entre les fenêtres de l'application

namespace AZERTYGlobal;

/// <summary>
/// Utilitaires de rendu GDI/DrawText partagés entre OnboardingWindow et SettingsWindow.
/// Toutes les méthodes sont statiques — les valeurs DPI-dépendantes sont passées en paramètre.
/// </summary>
static class GdiHelpers
{
    /// <summary>WM_GETFONT : police avec laquelle un contrôle peint son texte.</summary>
    private const uint WM_GETFONT = 0x0031;

    /// <summary>
    /// K5 (accessibilité 1.3.0) — un lien STATIC ne signalait son focus que par la couleur
    /// (WCAG 1.4.1 et 2.4.7). À appeler depuis la sous-classe du lien sur <c>WM_PAINT</c> : le
    /// contrôle se peint, puis le rectangle de focus système se pose autour de son texte.
    /// </summary>
    internal static IntPtr PaintLinkWithFocusRect(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        // wParam non nul : peinture dans un DC fourni par l'appelant, pas à l'écran.
        bool focused = wParam == IntPtr.Zero && Win32.GetFocus() == hWnd;
        // DrawFocusRect trace en XOR : sur un repeint partiel, il repasserait sur le cadre
        // encore à l'écran et en effacerait une partie. Un lien focalisé se repeint en entier.
        if (focused) Win32.InvalidateRect(hWnd, IntPtr.Zero, true);
        IntPtr result = Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
        if (!focused) return result;

        IntPtr hdc = Win32.GetDC(hWnd);
        if (hdc == IntPtr.Zero) return result;
        try
        {
            Win32.GetClientRect(hWnd, out var client);
            var text = new System.Text.StringBuilder(256);
            Win32.GetWindowTextW(hWnd, text, text.Capacity);
            IntPtr font = Win32.SendMessageW(hWnd, WM_GETFONT, IntPtr.Zero, IntPtr.Zero);
            IntPtr oldFont = font != IntPtr.Zero ? Win32.SelectObject(hdc, font) : IntPtr.Zero;
            // Même mise en page que le STATIC (SS_LEFT) : retour à la ligne, préfixe & traité.
            var bounds = new Win32.RECT { left = 0, top = 0, right = client.right, bottom = client.bottom };
            Win32.DrawTextW(hdc, text.ToString(), -1, ref bounds, Win32.DT_WORDBREAK | Win32.DT_CALCRECT);
            if (oldFont != IntPtr.Zero) Win32.SelectObject(hdc, oldFont);
            var focus = FocusRectForLink(bounds, client);
            Win32.DrawFocusRect(hdc, ref focus);
        }
        finally
        {
            Win32.ReleaseDC(hWnd, hdc);
        }
        return result;
    }

    /// <summary>
    /// K5 : cadre de focus d'un lien — son texte mesuré, plus 2 px à droite et 1 px en bas,
    /// borné au contrôle ; le contrôle entier quand le texte n'a pas pu être mesuré.
    /// </summary>
    internal static Win32.RECT FocusRectForLink(Win32.RECT text, Win32.RECT client)
    {
        if (text.right <= text.left || text.bottom <= text.top) return client;
        return new Win32.RECT
        {
            left = Math.Max(client.left, text.left),
            top = Math.Max(client.top, text.top),
            right = Math.Min(client.right, text.right + 2),
            bottom = Math.Min(client.bottom, text.bottom + 1)
        };
    }

    /// <summary>
    /// Audit du 25/09, F-15 — le logo en icône carrée, rendu par GDI+ (démarré par l'appelant)
    /// depuis une image déjà chargée. À propos, l'accueil et le tray en tenaient trois
    /// copies ; celle du tray lisait les retours et alignait le masque, c'est elle qui reste.
    /// Rend IntPtr.Zero quand l'image manque ou que GDI+ refuse une étape.
    /// </summary>
    internal static IntPtr CreateLogoIcon(IntPtr logo, int size)
    {
        if (logo == IntPtr.Zero || size <= 0) return IntPtr.Zero;
        IntPtr bmp32 = IntPtr.Zero;
        try
        {
            // 0x0026200A = PixelFormat32bppARGB.
            if (Win32.GdipCreateBitmapFromScan0(size, size, 0, 0x0026200A, IntPtr.Zero, out bmp32) != 0) return IntPtr.Zero;
            if (Win32.GdipGetImageGraphicsContext(bmp32, out IntPtr g) != 0) return IntPtr.Zero;
            Win32.GdipSetSmoothingMode(g, 4);
            Win32.GdipSetInterpolationMode(g, 7);
            Win32.GdipDrawImageRectI(g, logo, 0, 0, size, size);
            Win32.GdipDeleteGraphics(g);
            if (Win32.GdipCreateHBITMAPFromBitmap(bmp32, out IntPtr hBmp, 0x00000000) != 0) return IntPtr.Zero;
            // Lignes du masque 1 bpp alignées au mot : 6 octets par ligne à 40 px, pas 5.
            var maskBits = new byte[MonochromeMaskByteCount(size, size)];
            IntPtr hMask = Win32.CreateBitmap(size, size, 1, 1, maskBits);
            var iconInfo = new Win32.ICONINFO { fIcon = true, hbmMask = hMask, hbmColor = hBmp };
            IntPtr hIcon = Win32.CreateIconIndirect(ref iconInfo);
            Win32.DeleteObject(hMask);
            Win32.DeleteObject(hBmp);
            return hIcon;
        }
        finally
        {
            if (bmp32 != IntPtr.Zero) Win32.GdipDisposeImage(bmp32);
        }
    }

    /// <summary>Taille du masque 1 bpp : chaque ligne est alignée sur un mot de 16 bits.</summary>
    internal static int MonochromeMaskByteCount(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return checked(((width + 15) / 16) * 2 * height);
    }

    internal static void FillSolidRect(IntPtr hdc, Win32.RECT rect, uint color)
    {
        var brush = Win32.CreateSolidBrush(color);
        Win32.FillRect(hdc, ref rect, brush);
        Win32.DeleteObject(brush);
    }

    /// <summary>
    /// Dessine un panneau avec bordure 1px et accent coloré à gauche.
    /// <paramref name="accentWidth"/> est la largeur de l'accent en pixels (typiquement S(4)).
    /// Si <paramref name="accentColor"/> vaut 0, l'accent n'est pas dessiné.
    /// </summary>
    internal static void DrawPanel(IntPtr hdc, Win32.RECT rect, uint backgroundColor, uint borderColor, uint accentColor, int accentWidth)
    {
        FillSolidRect(hdc, rect, borderColor);
        var innerRect = new Win32.RECT
        {
            left = rect.left + 1,
            top = rect.top + 1,
            right = rect.right - 1,
            bottom = rect.bottom - 1
        };
        FillSolidRect(hdc, innerRect, backgroundColor);
        if (accentColor != 0)
        {
            var accentRect = new Win32.RECT
            {
                left = rect.left,
                top = rect.top,
                right = rect.left + accentWidth,
                bottom = rect.bottom
            };
            FillSolidRect(hdc, accentRect, accentColor);
        }
    }

    internal static int MeasureTextHeight(IntPtr hdc, IntPtr hFont, string text, int width,
        uint format = Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX)
    {
        Win32.SelectObject(hdc, hFont);
        var measureRect = new Win32.RECT { left = 0, top = 0, right = width, bottom = 9999 };
        Win32.DrawTextW(hdc, text, -1, ref measureRect, format | Win32.DT_CALCRECT);
        return measureRect.bottom;
    }

    internal static int MeasureSingleLineWidth(IntPtr hdc, IntPtr hFont, string text)
    {
        Win32.SelectObject(hdc, hFont);
        var measureRect = new Win32.RECT { left = 0, top = 0, right = 9999, bottom = 9999 };
        Win32.DrawTextW(hdc, text, -1, ref measureRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX | Win32.DT_CALCRECT);
        return measureRect.right;
    }

    internal static int MeasureSingleLineHeight(IntPtr hdc, IntPtr hFont)
    {
        return MeasureTextHeight(hdc, hFont, "Ag", 9999,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
    }

    /// <summary>
    /// Découpe des runs colorés en tokens (mot / espace) pour le word-wrap manuel.
    /// </summary>
    internal static List<(string Text, uint Color, IntPtr Font, bool IsSpace)> TokenizeColoredRuns(
        params (string Text, uint Color, IntPtr Font)[] runs)
    {
        var tokens = new List<(string Text, uint Color, IntPtr Font, bool IsSpace)>();
        foreach (var run in runs)
        {
            if (string.IsNullOrEmpty(run.Text))
                continue;

            var buffer = new System.Text.StringBuilder();
            bool currentIsSpace = char.IsWhiteSpace(run.Text[0]);

            foreach (char ch in run.Text)
            {
                bool isSpace = char.IsWhiteSpace(ch);
                if (buffer.Length > 0 && isSpace != currentIsSpace)
                {
                    tokens.Add((buffer.ToString(), run.Color, run.Font, currentIsSpace));
                    buffer.Clear();
                }

                buffer.Append(ch);
                currentIsSpace = isSpace;
            }

            if (buffer.Length > 0)
                tokens.Add((buffer.ToString(), run.Color, run.Font, currentIsSpace));
        }

        return tokens;
    }

    /// <summary>
    /// Mesure la hauteur nécessaire pour afficher des runs colorés avec word-wrap.
    /// <paramref name="fallbackLineHeight"/> est la hauteur de ligne par défaut si aucune police n'est trouvée.
    /// </summary>
    internal static int MeasureColoredRunsHeight(IntPtr hdc, int width, int fallbackLineHeight,
        params (string Text, uint Color, IntPtr Font)[] runs)
    {
        var tokens = TokenizeColoredRuns(runs);
        int lineHeight = 0;

        foreach (var run in runs)
        {
            if (run.Font == IntPtr.Zero)
                continue;

            lineHeight = Math.Max(lineHeight,
                MeasureTextHeight(hdc, run.Font, "Ag", width, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX));
        }

        if (lineHeight <= 0)
            lineHeight = fallbackLineHeight;

        int lineWidth = 0;
        int lines = 1;

        foreach (var token in tokens)
        {
            if (token.IsSpace && lineWidth == 0)
                continue;

            int tokenWidth = MeasureSingleLineWidth(hdc, token.Font, token.Text);
            if (!token.IsSpace && lineWidth > 0 && lineWidth + tokenWidth > width)
            {
                lines++;
                lineWidth = 0;
            }

            if (!(token.IsSpace && lineWidth == 0))
                lineWidth += tokenWidth;
        }

        return lines * lineHeight;
    }

    /// <summary>
    /// Dessine des runs colorés avec word-wrap manuel.
    /// <paramref name="fallbackLineHeight"/> est la hauteur de ligne par défaut si aucune police n'est trouvée.
    /// </summary>
    internal static void DrawColoredRuns(IntPtr hdc, int x, int y, int width, int fallbackLineHeight,
        params (string Text, uint Color, IntPtr Font)[] runs)
    {
        var tokens = TokenizeColoredRuns(runs);
        int lineHeight = 0;

        foreach (var run in runs)
        {
            if (run.Font == IntPtr.Zero)
                continue;

            lineHeight = Math.Max(lineHeight,
                MeasureTextHeight(hdc, run.Font, "Ag", width, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX));
        }

        if (lineHeight <= 0)
            lineHeight = fallbackLineHeight;

        int cursorX = x;
        int cursorY = y;

        foreach (var token in tokens)
        {
            if (token.IsSpace && cursorX == x)
                continue;

            int tokenWidth = MeasureSingleLineWidth(hdc, token.Font, token.Text);
            if (!token.IsSpace && cursorX > x && cursorX + tokenWidth > x + width)
            {
                cursorX = x;
                cursorY += lineHeight;
            }

            if (token.IsSpace && cursorX == x)
                continue;

            var tokenRect = new Win32.RECT
            {
                left = cursorX,
                top = cursorY,
                right = x + width,
                bottom = cursorY + lineHeight
            };
            Win32.SelectObject(hdc, token.Font);
            Win32.SetTextColor(hdc, token.Color);
            Win32.DrawTextW(hdc, token.Text, -1, ref tokenRect,
                Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
            cursorX += tokenWidth;
        }
    }
}
