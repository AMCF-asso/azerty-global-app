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
        var placed = Layout(hdc, runs, regular, strong, rect.right - rect.left);
        if (draw)
        {
            foreach (var word in placed)
            {
                int baseline = rect.top + word.Line * lineHeight + (lineHeight - (ascent + descent)) / 2 + ascent;
                Draw(hdc, word.Text, word.Strong ? strong : regular, rect.left + word.X, baseline,
                    word.Strong ? strongColor : color);
            }
        }
        return placed.Count == 0 ? 0 : (placed[^1].Line + 1) * lineHeight;
    }

    /// <summary>Un mot du paragraphe à sa place : ligne et abscisse dans le cadre.</summary>
    internal readonly record struct PlacedWord(string Text, bool Strong, int Line, int X);

    /// <summary>Les mots de <paramref name="runs"/> placés dans <paramref name="maxWidth"/>.</summary>
    internal static List<PlacedWord> Layout(IntPtr hdc, IReadOnlyList<TextRun> runs, FontSpec regular, FontSpec strong,
        int maxWidth)
    {
        var words = new List<(string Word, bool Strong, bool PrecededBySpace, bool CanBreakBefore)>();
        foreach (var w in Words(runs))
        {
            var font = w.Strong ? strong : regular;
            if (Width(hdc, w.Word, font) <= maxWidth)
            {
                words.Add(w);
                continue;
            }
            // Mot seul plus large que le cadre : seul cas où l'on coupe au milieu d'un mot.
            bool first = true;
            foreach (var chunk in ChunksThatFit(hdc, w.Word, font, maxWidth))
            {
                words.Add((chunk, w.Strong, first && w.PrecededBySpace, !first || w.CanBreakBefore));
                first = false;
            }
        }
        var measured = words.Select(w => (Width(hdc, w.Word, w.Strong ? strong : regular), w.PrecededBySpace,
            w.CanBreakBefore)).ToList();
        var positions = BreakLines(measured, Width(hdc, " ", regular), maxWidth);
        var placed = new List<PlacedWord>(words.Count);
        for (int i = 0; i < words.Count; i++)
            placed.Add(new PlacedWord(words[i].Word, words[i].Strong, positions[i].Line, positions[i].X));
        return placed;
    }

    /// <summary>Découpe <paramref name="word"/> en tronçons de grappes entières, chacun aussi
    /// large que possible sans dépasser <paramref name="maxWidth"/> (une grappe seule peut le
    /// dépasser : on ne la coupe pas).</summary>
    private static IEnumerable<string> ChunksThatFit(IntPtr hdc, string word, FontSpec font, int maxWidth)
    {
        int start = 0, end = 0;
        while (end < word.Length)
        {
            int next = end + (char.IsHighSurrogate(word[end]) && end + 1 < word.Length ? 2 : 1);
            while (next < word.Length && KeyboardRenderer.IsCombiningMark(word[next]))
                next++;
            if (end > start && Width(hdc, word[start..next], font) > maxWidth)
            {
                yield return word[start..end];
                start = end;
            }
            end = next;
        }
        if (end > start)
            yield return word[start..end];
    }

    /// <summary>
    /// Ligne et abscisse de chaque mot, de largeur et d'espace précédent donnés. La coupure
    /// ne se fait qu'à une espace : un mot collé au précédent (« Espace » puis « . ») passe à
    /// la ligne avec lui. Audit 1.3.0, A19 : le point final partait seul à la ligne. Un groupe
    /// collé plus large que le cadre se coupe encore entre ses mots, faute de mieux. Fonction
    /// pure : les tests l'éprouvent sans police.
    /// </summary>
    internal static (int Line, int X)[] BreakLines(IReadOnlyList<(int Width, bool PrecededBySpace)> words,
        int spaceWidth, int maxWidth)
        => BreakLines(words.Select(w => (w.Width, w.PrecededBySpace, false)).ToList(), spaceWidth, maxWidth);

    /// <summary>
    /// Idem, avec <c>CanBreakBefore</c> : le mot peut commencer une ligne sans espace avant
    /// lui (après un tiret, ou tronçon d'un mot plus large que le cadre). Il ouvre alors un
    /// nouveau groupe collé.
    /// </summary>
    internal static (int Line, int X)[] BreakLines(
        IReadOnlyList<(int Width, bool PrecededBySpace, bool CanBreakBefore)> words, int spaceWidth, int maxWidth)
    {
        var result = new (int Line, int X)[words.Count];
        int x = 0, line = 0;
        bool lineHasWord = false;
        int i = 0;
        while (i < words.Count)
        {
            // Le groupe : ce mot et ceux qui le suivent sans espace.
            int end = i + 1;
            int groupWidth = words[i].Width;
            while (end < words.Count && !words[end].PrecededBySpace && !words[end].CanBreakBefore)
                groupWidth += words[end++].Width;
            if (groupWidth > maxWidth)
            {
                end = i + 1;
                groupWidth = words[i].Width;
            }

            int gap = words[i].PrecededBySpace && lineHasWord ? spaceWidth : 0;
            if (lineHasWord && x + gap + groupWidth > maxWidth)
            {
                line++;
                x = 0;
                gap = 0;
            }
            x += gap;
            for (; i < end; i++)
            {
                result[i] = (line, x);
                x += words[i].Width;
            }
            lineHasWord = true;
        }
        return result;
    }

    /// <summary>Les mots des morceaux, avec leur graisse et l'espace sécable qui les précède.
    /// Une ponctuation collée à un mot gras (« Espace. ») reste son voisin sans espace.
    /// Un mot à tiret se coupe après le tiret quand les deux côtés ont au moins
    /// <see cref="MinHyphenSide"/> caractères alphanumériques (« peut-être », « well-known » ;
    /// pas « va-t-il », « dis-le », « est-ce »).</summary>
    private static IEnumerable<(string Word, bool Strong, bool PrecededBySpace, bool CanBreakBefore)> Words(
        IReadOnlyList<TextRun> runs)
    {
        foreach (var w in WordsBySpace(runs))
        {
            int start = 0;
            for (int i = 0; i < w.Word.Length; i++)
            {
                if (w.Word[i] != '-' || !HyphenBreaks(w.Word, i)) continue;
                yield return (w.Word[start..(i + 1)], w.Strong, start == 0 && w.PrecededBySpace, start > 0);
                start = i + 1;
            }
            yield return start == 0 ? (w.Word, w.Strong, w.PrecededBySpace, false)
                : (w.Word[start..], w.Strong, false, true);
        }
    }

    private const int MinHyphenSide = 3;

    private static bool HyphenBreaks(string word, int hyphen)
    {
        int left = 0, right = 0;
        for (int i = hyphen - 1; i >= 0 && char.IsLetterOrDigit(word[i]); i--) left++;
        for (int i = hyphen + 1; i < word.Length && char.IsLetterOrDigit(word[i]); i++) right++;
        return left >= MinHyphenSide && right >= MinHyphenSide;
    }

    private static IEnumerable<(string Word, bool Strong, bool PrecededBySpace)> WordsBySpace(
        IReadOnlyList<TextRun> runs)
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
