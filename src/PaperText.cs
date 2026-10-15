// Texte des écrans refondus en 1.3.0 : polices embarquées, secours par caractère.
namespace AZERTYGlobal;

/// <summary>Un morceau de texte enrichi : le gras dit ce qu'on tient ou frappe
/// (« Tenez <b>AltGr</b>, frappez <b>Espace</b>. »).</summary>
internal readonly record struct TextRun(string Text, bool Strong = false);

/// <summary>Alignement horizontal d'une ligne dans son cadre.</summary>
internal enum TextAlign { Left, Center, Right }

/// <summary>
/// Dessin du texte des écrans refondus. GDI ne cherche pas un glyphe absent dans une autre
/// police privée : chaque grappe (lettre et marques combinantes) va à la première police de
/// <see cref="AppFonts.Chain"/> qui l'a, et toutes les polices partagent la même ligne de
/// base. Sans cela, un « я » ou un « ◌́ » de leçon sortirait en carré vide.
/// </summary>
internal static class PaperText
{
    private readonly record struct Piece(string Text, FontSpec Font, int Width);

    private static readonly Dictionary<(FontSpec, string), bool> Coverage = new();
    private static readonly object Gate = new();

    /// <summary>Hauteurs au-dessus et au-dessous de la ligne de base de la police principale.</summary>
    internal static (int Ascent, int Descent) Metrics(IntPtr hdc, FontSpec font)
    {
        var old = Win32.SelectObject(hdc, font.Handle);
        Win32.GetTextMetricsW(hdc, out var tm);
        Win32.SelectObject(hdc, old);
        return (tm.tmAscent, tm.tmDescent);
    }

    /// <summary>Largeur de <paramref name="text"/> sur une ligne.</summary>
    internal static int Width(IntPtr hdc, string text, FontSpec font)
    {
        int width = 0;
        foreach (var piece in Split(hdc, text, font))
            width += piece.Width;
        return width;
    }

    /// <summary>Écrit <paramref name="text"/> à partir de <paramref name="x"/>, posé sur
    /// <paramref name="baseline"/>. Rend l'abscisse de fin.</summary>
    internal static int Draw(IntPtr hdc, string text, FontSpec font, int x, int baseline, uint color)
    {
        if (string.IsNullOrEmpty(text)) return x;
        Win32.SetBkMode(hdc, Win32.TRANSPARENT);
        Win32.SetTextColor(hdc, color);
        uint oldAlign = Win32.SetTextAlign(hdc, Win32.TA_LEFT | Win32.TA_BASELINE);
        var oldFont = Win32.SelectObject(hdc, font.Handle);
        foreach (var piece in Split(hdc, text, font))
        {
            Win32.SelectObject(hdc, piece.Font.Handle);
            Win32.TextOutW(hdc, x, baseline, piece.Text, piece.Text.Length);
            x += piece.Width;
        }
        Win32.SelectObject(hdc, oldFont);
        Win32.SetTextAlign(hdc, oldAlign);
        return x;
    }

    /// <summary>Une ligne dans un cadre, centrée verticalement sur la hauteur de la police
    /// principale. Rend la largeur dessinée.</summary>
    internal static int DrawLine(IntPtr hdc, string text, FontSpec font, Win32.RECT rect, uint color,
        TextAlign align = TextAlign.Left)
    {
        int width = Width(hdc, text, font);
        var (ascent, descent) = Metrics(hdc, font);
        int baseline = rect.top + (rect.bottom - rect.top - (ascent + descent)) / 2 + ascent;
        int x = align switch
        {
            TextAlign.Center => rect.left + (rect.right - rect.left - width) / 2,
            TextAlign.Right => rect.right - width,
            _ => rect.left,
        };
        Draw(hdc, text, font, x, baseline, color);
        return width;
    }

    /// <summary>
    /// Paragraphe enrichi, coupé aux espaces sécables dans la largeur de
    /// <paramref name="rect"/>. Le gras prend <paramref name="strongColor"/>. Rend la hauteur
    /// occupée ; <paramref name="draw"/> faux ne fait que mesurer.
    /// </summary>
    internal static int DrawParagraph(IntPtr hdc, IReadOnlyList<TextRun> runs, FontSpec regular, FontSpec strong,
        Win32.RECT rect, uint color, uint strongColor, int lineHeight, bool draw = true)
    {
        var (ascent, descent) = Metrics(hdc, regular);
        int maxWidth = rect.right - rect.left;
        int x = 0, line = 0;
        int spaceWidth = Width(hdc, " ", regular);
        bool lineHasWord = false;

        foreach (var (word, isStrong, precededBySpace) in Words(runs))
        {
            var font = isStrong ? strong : regular;
            int wordWidth = Width(hdc, word, font);
            int gap = precededBySpace && lineHasWord ? spaceWidth : 0;
            if (lineHasWord && x + gap + wordWidth > maxWidth)
            {
                line++;
                x = 0;
                gap = 0;
            }
            x += gap;
            if (draw)
            {
                int baseline = rect.top + line * lineHeight + (lineHeight - (ascent + descent)) / 2 + ascent;
                Draw(hdc, word, font, rect.left + x, baseline, isStrong ? strongColor : color);
            }
            x += wordWidth;
            lineHasWord = true;
        }
        return lineHasWord ? (line + 1) * lineHeight : 0;
    }

    /// <summary>Les mots des morceaux, avec leur graisse et l'espace sécable qui les précède.
    /// Une ponctuation collée à un mot gras (« Espace. ») reste son voisin sans espace.</summary>
    private static IEnumerable<(string Word, bool Strong, bool PrecededBySpace)> Words(IReadOnlyList<TextRun> runs)
    {
        bool pendingSpace = false;
        foreach (var run in runs)
        {
            int start = 0;
            var text = run.Text;
            for (int i = 0; i <= text.Length; i++)
            {
                bool end = i == text.Length;
                if (!end && !GdiHelpers.IsBreakingSpace(text[i]))
                    continue;
                if (i > start)
                {
                    yield return (text[start..i], run.Strong, pendingSpace);
                    pendingSpace = false;
                }
                if (!end)
                    pendingSpace = true;
                start = i + 1;
            }
        }
    }

    /// <summary>Découpe <paramref name="text"/> en morceaux d'une même police de secours.</summary>
    private static List<Piece> Split(IntPtr hdc, string text, FontSpec font)
    {
        var pieces = new List<Piece>();
        if (string.IsNullOrEmpty(text)) return pieces;
        var chain = AppFonts.Chain(font);
        var current = new System.Text.StringBuilder();
        FontSpec? currentFont = null;

        int i = 0;
        while (i < text.Length)
        {
            int start = i;
            i += char.IsHighSurrogate(text[i]) && i + 1 < text.Length ? 2 : 1;
            while (i < text.Length && KeyboardRenderer.IsCombiningMark(text[i]))
                i++;
            string cluster = text[start..i];
            var chosen = chain[^1];
            foreach (var candidate in chain)
            {
                if (Covers(hdc, candidate, cluster)) { chosen = candidate; break; }
            }
            if (currentFont is { } f && f != chosen)
            {
                pieces.Add(Measure(hdc, current.ToString(), f));
                current.Clear();
            }
            currentFont = chosen;
            current.Append(cluster);
        }
        if (currentFont is { } last && current.Length > 0)
            pieces.Add(Measure(hdc, current.ToString(), last));
        return pieces;
    }

    private static Piece Measure(IntPtr hdc, string text, FontSpec font)
    {
        var old = Win32.SelectObject(hdc, font.Handle);
        Win32.GetTextExtentPoint32W(hdc, text, text.Length, out var size);
        Win32.SelectObject(hdc, old);
        return new Piece(text, font, size.cx);
    }

    /// <summary>La police a-t-elle tous les glyphes de la grappe ? Les espaces ne comptent pas :
    /// toutes les polices en ont une.</summary>
    private static bool Covers(IntPtr hdc, FontSpec font, string cluster)
    {
        if (font.Family == AppFonts.System) return true;
        lock (Gate)
        {
            if (Coverage.TryGetValue((font, cluster), out bool known))
                return known;
        }
        var old = Win32.SelectObject(hdc, font.Handle);
        var indices = new ushort[cluster.Length];
        Win32.GetGlyphIndicesW(hdc, cluster, cluster.Length, indices, Win32.GGI_MARK_NONEXISTING_GLYPHS);
        Win32.SelectObject(hdc, old);
        bool covers = Array.TrueForAll(indices, index => index != 0xFFFF);
        lock (Gate)
            Coverage[(font, cluster)] = covers;
        return covers;
    }
}
