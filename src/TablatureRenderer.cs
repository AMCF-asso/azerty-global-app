// La Tablature : chaque caractère a sa colonne ; sous lui, ce que font les doigts
// (refonte 1.3.0, maquettes Leçons F, recherche B et clavier virtuel A du 07/10).
namespace AZERTYGlobal;

/// <summary>Où en est une colonne de la portée.</summary>
internal enum TabState
{
    /// <summary>Déjà joué : encre.</summary>
    Done,
    /// <summary>À jouer maintenant : action, bande teintée qui traverse la portée.</summary>
    Current,
    /// <summary>Pas encore joué : pâle.</summary>
    Upcoming,
    /// <summary>Geste montré seul (recherche, bandeau) : action, sans bande.</summary>
    Chosen,
}

/// <summary>
/// Une colonne : le caractère (ligne des glyphes des Leçons), les modificateurs tenus (traits
/// épais) et la touche frappée (case). <see cref="IsThen"/> est la colonne « puis » qui sépare
/// deux temps d'une touche morte.
/// </summary>
internal readonly record struct TabColumn(string? Glyph, bool Shift, bool AltGr, string? Key, TabState State, bool IsThen = false)
{
    /// <summary>Les colonnes d'un geste seul : un temps par colonne, « puis » entre deux.</summary>
    internal static List<TabColumn> FromGesture(IReadOnlyList<GestureStep> steps, TabState state = TabState.Chosen)
    {
        var columns = new List<TabColumn>(steps.Count * 2);
        for (int i = 0; i < steps.Count; i++)
        {
            if (i > 0) columns.Add(new TabColumn(null, false, false, null, state, IsThen: true));
            columns.Add(new TabColumn(null, steps[i].Shift, steps[i].AltGr, steps[i].ShortLabel, state));
        }
        return columns;
    }
}

/// <summary>Les mesures d'une portée, en pixels déjà mis à l'échelle. Une par usage :
/// <see cref="Lessons"/>, <see cref="Search"/>, <see cref="Banner"/>.</summary>
internal sealed record TablatureStyle
{
    internal int LabelWidth { get; init; }
    internal int LabelInset { get; init; }
    internal bool LabelsRight { get; init; }
    internal FontSpec LabelFont { get; init; }
    internal uint LabelColor { get; init; } = PaperTheme.TextSecondary;
    /// <summary>Hauteur de la ligne des glyphes ; 0 quand la portée n'a pas de texte.</summary>
    internal int GlyphRowHeight { get; init; }
    internal FontSpec GlyphFont { get; init; }
    internal int GlyphPadding { get; init; }
    internal int NbspWidth { get; init; }
    internal int ThinNbspWidth { get; init; }
    internal int SpaceMarkHeight { get; init; }
    /// <summary>Hauteurs des lignes Maj, AltGr et Touche.</summary>
    internal int ShiftRowHeight { get; init; }
    internal int AltGrRowHeight { get; init; }
    internal int KeyRowHeight { get; init; }
    internal int CellPadding { get; init; }
    internal int MinColumnWidth { get; init; }
    internal int BarHeight { get; init; }
    internal int BarMinWidth { get; init; }
    /// <summary>Retrait du trait de part et d'autre (« width: calc(100% - 4px) »).</summary>
    internal int BarInset { get; init; }
    internal int CaseHeight { get; init; }
    internal int CaseMinWidth { get; init; }
    internal int CasePaddingX { get; init; }
    internal int CaseRadius { get; init; }
    internal FontSpec CaseFont { get; init; }
    internal FontSpec ThenFont { get; init; }
    /// <summary>Débord de la bande de la colonne en cours au-dessus et au-dessous de la grille.</summary>
    internal int BandOverflowTop { get; init; }
    internal int BandOverflowBottom { get; init; }
    internal int BandBorder { get; init; }
    /// <summary>Fond sur lequel la portée est posée (pour la teinte de la bande).</summary>
    internal uint Backdrop { get; init; } = PaperTheme.Surface;

    internal int Height => GlyphRowHeight + ShiftRowHeight + AltGrRowHeight + KeyRowHeight;

    /// <summary>Leçons F : glyphes Literata 50 px, lignes de 34 px, étiquettes à gauche.</summary>
    internal static TablatureStyle Lessons(float s) => new()
    {
        LabelWidth = Px(74, s), LabelInset = Px(18, s),
        LabelFont = FontSpec.Of(AppFonts.Text, 13, TypeRamp.Regular, s),
        GlyphRowHeight = Px(78, s), GlyphFont = FontSpec.Of(AppFonts.Title, 50, TypeRamp.Semibold, s),
        GlyphPadding = Px(1, s), NbspWidth = Px(22, s), ThinNbspWidth = Px(13, s), SpaceMarkHeight = Px(30, s),
        ShiftRowHeight = Px(34, s), AltGrRowHeight = Px(34, s), KeyRowHeight = Px(34, s),
        MinColumnWidth = Px(10, s),
        BarHeight = Px(8, s), BarMinWidth = Px(22, s), BarInset = Px(2, s),
        CaseHeight = Px(24, s), CaseMinWidth = Px(22, s), CasePaddingX = Px(3, s), CaseRadius = Px(3, s),
        CaseFont = FontSpec.Of(AppFonts.Mono, 12, TypeRamp.Semibold, s),
        ThenFont = FontSpec.Of(AppFonts.Text, 12, TypeRamp.Regular, s),
        BandOverflowTop = Px(26, s), BandOverflowBottom = Px(22, s), BandBorder = Px(2, s),
    };

    /// <summary>Recherche B : la partition du caractère choisi, grandes cases de 38 px.</summary>
    internal static TablatureStyle Search(float s) => new()
    {
        LabelWidth = Px(56, s), LabelInset = Px(8, s), LabelsRight = true,
        LabelFont = FontSpec.Of(AppFonts.Text, 13, TypeRamp.Regular, s),
        ShiftRowHeight = Px(20, s), AltGrRowHeight = Px(20, s), KeyRowHeight = Px(42, s),
        CellPadding = Px(6, s),
        BarHeight = Px(9, s), BarMinWidth = Px(38, s),
        CaseHeight = Px(38, s), CaseMinWidth = Px(38, s), CasePaddingX = Px(5, s), CaseRadius = Px(3, s),
        CaseFont = FontSpec.Of(AppFonts.Mono, 18, TypeRamp.Semibold, s),
        ThenFont = FontSpec.Of(AppFonts.Text, 14, TypeRamp.Regular, s),
        Backdrop = PaperTheme.Paper,
    };

    /// <summary>Clavier virtuel A : le geste venu de la recherche, dans le bandeau d'état.</summary>
    internal static TablatureStyle Banner(float s) => new()
    {
        LabelWidth = Px(52, s),
        LabelFont = FontSpec.Of(AppFonts.Text, 12, TypeRamp.Regular, s),
        ShiftRowHeight = Px(16, s), AltGrRowHeight = Px(16, s), KeyRowHeight = Px(30, s),
        CellPadding = Px(4, s),
        BarHeight = Px(6, s), BarMinWidth = Px(24, s),
        CaseHeight = Px(26, s), CaseMinWidth = Px(26, s), CasePaddingX = Px(5, s), CaseRadius = Px(3, s),
        CaseFont = FontSpec.Of(AppFonts.Mono, 13, TypeRamp.Semibold, s),
        ThenFont = FontSpec.Of(AppFonts.Text, 12, TypeRamp.Regular, s),
        Backdrop = PaperTheme.Paper,
    };

    internal static int Px(float value, float scale) => Math.Max(1, (int)Math.Round(value * scale));
}

/// <summary>
/// Dessine la Tablature : trois lignes Maj, AltGr et Touche, le trait épais se tient, la case
/// se frappe ; aux Leçons, la phrase au-dessus, chaque caractère sur sa colonne.
/// </summary>
internal static class TablatureRenderer
{
    private const char Nbsp = ' ';
    private const char ThinNbsp = ' ';

    /// <summary>Largeur de chaque colonne : la chasse du glyphe, élargie au besoin par le
    /// trait ou la case qu'elle porte.</summary>
    internal static int[] MeasureColumns(IntPtr hdc, IReadOnlyList<TabColumn> columns, TablatureStyle style)
    {
        var widths = new int[columns.Count];
        for (int i = 0; i < columns.Count; i++)
        {
            var c = columns[i];
            int content = style.MinColumnWidth;
            if (style.GlyphRowHeight > 0 && !string.IsNullOrEmpty(c.Glyph))
                content = Math.Max(content, GlyphWidth(hdc, c.Glyph, style) + 2 * style.GlyphPadding);
            if (c.Shift || c.AltGr)
                content = Math.Max(content, style.BarMinWidth + 2 * style.BarInset);
            if (c.IsThen)
                content = Math.Max(content, PaperText.Width(hdc, L.Gesture_ThenShort, style.ThenFont));
            else if (!string.IsNullOrEmpty(c.Key))
                content = Math.Max(content, CaseWidth(hdc, c.Key, style));
            widths[i] = content + 2 * style.CellPadding;
        }
        return widths;
    }

    /// <summary>Largeur totale, étiquettes comprises.</summary>
    internal static int TotalWidth(int[] widths, TablatureStyle style)
    {
        int total = style.LabelWidth;
        foreach (int w in widths) total += w;
        return total;
    }

    /// <summary>
    /// Décalage horizontal qui garde la colonne <paramref name="current"/> lisible dans
    /// <paramref name="viewport"/> pixels de colonnes : on ne bouge que si elle sort, et on la
    /// replace alors au tiers gauche pour laisser voir la suite.
    /// </summary>
    internal static int ScrollToKeep(int[] widths, int current, int viewport, int scroll)
    {
        int total = 0;
        foreach (int w in widths) total += w;
        int maxScroll = Math.Max(0, total - viewport);
        if (current < 0 || current >= widths.Length) return Math.Clamp(scroll, 0, maxScroll);
        int left = 0;
        for (int i = 0; i < current; i++) left += widths[i];
        int right = left + widths[current];
        int margin = viewport / 6;
        if (left - scroll < margin || right - scroll > viewport - margin)
            scroll = left - viewport / 3;
        return Math.Clamp(scroll, 0, maxScroll);
    }

    /// <summary>
    /// Dessine la portée dans <paramref name="bounds"/> (coin haut gauche de la grille). Les
    /// étiquettes restent en place ; les colonnes défilent de <paramref name="scroll"/> et sont
    /// coupées au bord droit de <paramref name="bounds"/>. La bande de la colonne en cours
    /// déborde de la grille selon le style.
    /// </summary>
    internal static void Draw(IntPtr hdc, IReadOnlyList<TabColumn> columns, int[] widths, TablatureStyle style,
        Win32.RECT bounds, int scroll = 0)
    {
        int top = bounds.top;
        int glyphTop = top;
        int shiftTop = glyphTop + style.GlyphRowHeight;
        int altGrTop = shiftTop + style.ShiftRowHeight;
        int keyTop = altGrTop + style.AltGrRowHeight;
        int bottom = keyTop + style.KeyRowHeight;
        int columnsLeft = bounds.left + style.LabelWidth;

        // Étiquettes, fixes.
        DrawLabel(hdc, L.Gesture_LineShift, style, bounds.left, shiftTop, style.ShiftRowHeight);
        DrawLabel(hdc, L.Gesture_LineAltGr, style, bounds.left, altGrTop, style.AltGrRowHeight);
        DrawLabel(hdc, L.Gesture_LineKey, style, bounds.left, keyTop, style.KeyRowHeight);

        int saved = Win32.SaveDC(hdc);
        Win32.IntersectClipRect(hdc, columnsLeft, top - style.BandOverflowTop, bounds.right, bottom + style.BandOverflowBottom);

        // Bande de la colonne en cours, sous tout le reste.
        int x = columnsLeft - scroll;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].State == TabState.Current)
            {
                PaperDraw.Fill(hdc, x, top - style.BandOverflowTop, x + widths[i], bottom + style.BandOverflowBottom,
                    PaperDraw.Blend(PaperTheme.Action, style.Backdrop, 0.07f));
                PaperDraw.Fill(hdc, x, top - style.BandOverflowTop, x + style.BandBorder, bottom + style.BandOverflowBottom,
                    PaperTheme.Action);
            }
            x += widths[i];
        }

        // Filets des trois lignes, interrompus par « puis » sur la ligne des touches.
        int rulesRight = columnsLeft - scroll;
        foreach (int w in widths) rulesRight += w;
        DrawRule(hdc, columnsLeft - scroll, rulesRight, shiftTop + style.ShiftRowHeight / 2);
        DrawRule(hdc, columnsLeft - scroll, rulesRight, altGrTop + style.AltGrRowHeight / 2);
        x = columnsLeft - scroll;
        int keyMid = keyTop + style.KeyRowHeight / 2;
        for (int i = 0; i < columns.Count; i++)
        {
            if (!columns[i].IsThen)
                DrawRule(hdc, x, x + widths[i], keyMid);
            x += widths[i];
        }

        // Colonnes.
        x = columnsLeft - scroll;
        for (int i = 0; i < columns.Count; i++)
        {
            int w = widths[i];
            if (x + w >= columnsLeft && x <= bounds.right)
            {
                var c = columns[i];
                var (ink, onCase, caseFill) = Colors(c.State);
                if (style.GlyphRowHeight > 0 && !string.IsNullOrEmpty(c.Glyph))
                    DrawGlyph(hdc, c.Glyph, style, x, w, glyphTop, ink);
                if (c.Shift) DrawBar(hdc, style, x, w, shiftTop, style.ShiftRowHeight, ink);
                if (c.AltGr) DrawBar(hdc, style, x, w, altGrTop, style.AltGrRowHeight, ink);
                if (c.IsThen)
                {
                    PaperText.DrawLine(hdc, L.Gesture_ThenShort, style.ThenFont,
                        new Win32.RECT { left = x, top = keyTop, right = x + w, bottom = keyTop + style.KeyRowHeight },
                        PaperTheme.TextSecondary, TextAlign.Center);
                }
                else if (!string.IsNullOrEmpty(c.Key))
                {
                    int cw = CaseWidth(hdc, c.Key, style);
                    int cx = x + (w - cw) / 2;
                    int cy = keyTop + (style.KeyRowHeight - style.CaseHeight) / 2;
                    var box = new Win32.RECT { left = cx, top = cy, right = cx + cw, bottom = cy + style.CaseHeight };
                    PaperDraw.RoundBox(hdc, box, caseFill, ink, style.CaseRadius);
                    PaperText.DrawLine(hdc, c.Key, style.CaseFont, box, onCase, TextAlign.Center);
                }
            }
            x += w;
        }
        Win32.RestoreDC(hdc, saved);
    }

    /// <summary>
    /// Geste abrégé d'une ligne de résultats (recherche B) : barres pleines nommées pour ce qui
    /// se tient, case pour ce qui se frappe, « puis » entre deux temps. Mesure seulement quand
    /// <paramref name="draw"/> est faux. Rend la largeur ; <paramref name="right"/> est le bord
    /// droit, <paramref name="midY"/> le milieu vertical.
    /// </summary>
    internal static int DrawShortGesture(IntPtr hdc, IReadOnlyList<GestureStep> steps, bool chosen, float s,
        int right, int midY, bool draw = true)
    {
        var tagFont = FontSpec.Of(AppFonts.Text, 11, TypeRamp.Semibold, s);
        var caseFont = FontSpec.Of(AppFonts.Mono, 12, TypeRamp.Semibold, s);
        var thenFont = FontSpec.Of(AppFonts.Text, 12, TypeRamp.Regular, s);
        int gap = TablatureStyle.Px(5, s), tagPad = TablatureStyle.Px(6, s), tagH = TablatureStyle.Px(20, s);
        int caseH = TablatureStyle.Px(22, s), caseMin = TablatureStyle.Px(22, s), casePad = TablatureStyle.Px(5, s);
        int thenMargin = TablatureStyle.Px(3, s), radius = TablatureStyle.Px(2, s);

        // Les morceaux de gauche à droite : (texte, genre) ; genre 0 tenu, 1 case, 2 « puis ».
        var parts = new List<(string Text, int Kind, int Width)>();
        for (int i = 0; i < steps.Count; i++)
        {
            if (i > 0) parts.Add((L.Gesture_ThenShort, 2, PaperText.Width(hdc, L.Gesture_ThenShort, thenFont) + 2 * thenMargin));
            if (steps[i].Shift) parts.Add((L.Gesture_LineShift, 0, PaperText.Width(hdc, L.Gesture_LineShift, tagFont) + 2 * tagPad));
            if (steps[i].AltGr) parts.Add((L.Gesture_LineAltGr, 0, PaperText.Width(hdc, L.Gesture_LineAltGr, tagFont) + 2 * tagPad));
            string key = steps[i].ShortLabel;
            parts.Add((key, 1, Math.Max(caseMin, PaperText.Width(hdc, key, caseFont) + 2 * casePad + 2)));
        }
        int total = 0;
        foreach (var p in parts) total += p.Width;
        total += Math.Max(0, parts.Count - 1) * gap;
        if (!draw) return total;

        uint ink = chosen ? PaperTheme.Action : PaperTheme.Ink;
        int x = right - total;
        foreach (var (text, kind, width) in parts)
        {
            switch (kind)
            {
                case 0:
                {
                    var r = new Win32.RECT { left = x, top = midY - tagH / 2, right = x + width, bottom = midY - tagH / 2 + tagH };
                    PaperDraw.RoundBox(hdc, r, ink, ink, radius);
                    PaperText.DrawLine(hdc, text, tagFont, r, chosen ? PaperTheme.OnAction : PaperTheme.Paper, TextAlign.Center);
                    break;
                }
                case 1:
                {
                    var r = new Win32.RECT { left = x, top = midY - caseH / 2, right = x + width, bottom = midY - caseH / 2 + caseH };
                    PaperDraw.RoundBox(hdc, r, chosen ? PaperTheme.Action : PaperTheme.Surface, ink, TablatureStyle.Px(3, s));
                    PaperText.DrawLine(hdc, text, caseFont, r, chosen ? PaperTheme.OnAction : PaperTheme.Ink, TextAlign.Center);
                    break;
                }
                default:
                    PaperText.DrawLine(hdc, text, thenFont,
                        new Win32.RECT { left = x, top = midY - caseH / 2, right = x + width, bottom = midY - caseH / 2 + caseH },
                        PaperTheme.TextSecondary, TextAlign.Center);
                    break;
            }
            x += width + gap;
        }
        return total;
    }

    /// <summary>Encre des traits et contours, texte de la case, fond de la case.</summary>
    private static (uint Ink, uint OnCase, uint CaseFill) Colors(TabState state) => state switch
    {
        TabState.Done => (PaperTheme.Ink, PaperTheme.Ink, PaperTheme.Surface),
        TabState.Upcoming => (PaperTheme.Pale, PaperTheme.Pale, PaperTheme.Surface),
        _ => (PaperTheme.Action, PaperTheme.OnAction, PaperTheme.Action),
    };

    private static void DrawLabel(IntPtr hdc, string text, TablatureStyle style, int left, int top, int height)
    {
        var rect = style.LabelsRight
            ? new Win32.RECT { left = left, top = top, right = left + style.LabelWidth - style.LabelInset, bottom = top + height }
            : new Win32.RECT { left = left + style.LabelInset, top = top, right = left + style.LabelWidth, bottom = top + height };
        PaperText.DrawLine(hdc, text, style.LabelFont, rect, style.LabelColor, style.LabelsRight ? TextAlign.Right : TextAlign.Left);
    }

    private static void DrawRule(IntPtr hdc, int left, int right, int y)
        => PaperDraw.Fill(hdc, left, y, right, y + 1, PaperTheme.Border);

    private static void DrawBar(IntPtr hdc, TablatureStyle style, int x, int w, int rowTop, int rowHeight, uint color)
    {
        int y = rowTop + (rowHeight - style.BarHeight) / 2;
        int inner = Math.Max(style.BarMinWidth, w - 2 * style.CellPadding - 2 * style.BarInset);
        int left = x + (w - inner) / 2;
        PaperDraw.Fill(hdc, left, y, left + inner, y + style.BarHeight, color);
    }

    private static int CaseWidth(IntPtr hdc, string key, TablatureStyle style)
        => Math.Max(style.CaseMinWidth, PaperText.Width(hdc, key, style.CaseFont) + 2 * style.CasePaddingX + 2);

    private static int GlyphWidth(IntPtr hdc, string glyph, TablatureStyle style) => glyph switch
    {
        " " => style.NbspWidth,
        " " => style.ThinNbspWidth,
        _ => PaperText.Width(hdc, glyph, style.GlyphFont),
    };

    private static void DrawGlyph(IntPtr hdc, string glyph, TablatureStyle style, int x, int w, int top, uint color)
    {
        var (ascent, descent) = PaperText.Metrics(hdc, style.GlyphFont);
        int baseline = top + (style.GlyphRowHeight - (ascent + descent)) / 2 + ascent;
        if (glyph.Length == 1 && (glyph[0] == Nbsp || glyph[0] == ThinNbsp))
        {
            // Le repère reste discret : la couleur de l'état à 55 % sur la surface.
            int markWidth = glyph[0] == Nbsp ? style.NbspWidth : style.ThinNbspWidth;
            PaperDraw.SpaceMark(hdc, glyph[0], x + (w - markWidth) / 2, markWidth, baseline,
                style.SpaceMarkHeight / 2, style.GlyphFont.Height / 50f,
                PaperDraw.Blend(color, PaperTheme.Surface, 0.55f));
            return;
        }
        int width = PaperText.Width(hdc, glyph, style.GlyphFont);
        PaperText.Draw(hdc, glyph, style.GlyphFont, x + (w - width) / 2, baseline, color);
    }
}
