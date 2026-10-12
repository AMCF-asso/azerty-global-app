// Accueil des Leçons, « Votre texte » (maquette lecons-innov-b-texte, étape 3 de la direction B) :
// un champ où l'on colle ou tape un texte, qui marque déjà les caractères à geste (soulignés) et
// ceux que la disposition ne tape pas (cadre en tirets) ; à droite, ce que la leçon fera taper ;
// dessous, la Tablature de la première ligne. « Taper ce texte » lance la séance composée tirée
// de LessonCustomText. Le texte vit le temps de la fenêtre : rien n'est gardé sur le disque, et
// la fermeture des Leçons l'oublie (HomeOnHide).
//
// Le champ garde son texte déjà normalisé (forme composée, fins de ligne en \n, tabulations en
// espaces) : les grappes de LessonsHomeLogic.AnalyzeText se recollent alors exactement en lui,
// et le curseur, un index UTF-16, tombe toujours sur une limite de grappe.
using System.Globalization;

namespace AZERTYGlobal;

internal sealed partial class LessonsWindow
{
    private string _homeText = "";
    private int _homeTextCursor;
    // Moitié haute d'un caractère hors du plan de base (émoji) : il arrive en deux WM_CHAR.
    private char? _homeTextHighSurrogate;
    // Première ligne visuelle du champ : le champ défile pour garder le curseur visible.
    private int _homeTextFirstLine;
    // Les grappes visibles du dernier repeint et leur début dans le texte : un clic y pose le curseur.
    private readonly List<(Win32.RECT Rect, int Start, int Length)> _homeTextHits = new();
    private Win32.RECT _homeTextField;
    private (string Text, string Language, LessonCustomTextResult Result)? _homeTextBuilt;

    private static bool HomeTextTypable(string character) => CharacterIndex.Shared.ByCharacter.ContainsKey(character);

    private static bool HomeTextTracked(char c) => HomeKeyCatalog.Shared.TryGet(c.ToString(), out _);

    /// <summary>« Coller un texte » : la page « Votre texte ». Un champ vide reçoit le texte du
    /// presse-papiers ; un champ déjà rempli reste tel quel (Ctrl+V y ajoute).</summary>
    private void HomeOpenText()
    {
        if (_homeText.Length == 0) HomeTextPaste();
        HomeShowPage(HomePage.Text);
    }

    private void HomeTextForget()
    {
        _homeText = "";
        _homeTextCursor = 0;
        _homeTextHighSurrogate = null;
        _homeTextFirstLine = 0;
        _homeTextHits.Clear();
        _homeTextBuilt = null;
    }

    /// <summary>Le texte du presse-papiers, inséré au curseur.</summary>
    private void HomeTextPaste()
    {
        if (!Win32.OpenClipboard(_hWnd)) return;
        string? text;
        try { text = CharacterSearch.ReadClipboardText(); }
        finally { Win32.CloseClipboard(); }
        if (!string.IsNullOrEmpty(text)) HomeTextInsert(text);
    }

    /// <summary>Insère au curseur, sous la forme que garde le champ, dans la limite de
    /// <see cref="LessonsHomeLogic.MaxTextLength"/> caractères (coupé à une limite de grappe).</summary>
    internal void HomeTextInsert(string text)
    {
        _homeTextCursor = Math.Clamp(_homeTextCursor, 0, _homeText.Length);
        string added = LessonsHomeLogic.NormalizeText(text.Replace('\t', ' '));
        int room = LessonsHomeLogic.MaxTextLength - _homeText.Length;
        if (room <= 0 || added.Length == 0) return;
        if (added.Length > room)
        {
            int cut = 0;
            foreach (int start in StringInfo.ParseCombiningCharacters(added))
            {
                if (start > room) break;
                cut = start;
            }
            added = added[..cut];
            if (added.Length == 0) return;
        }
        // Une marque combinante tapée après sa lettre se compose avec elle : le curseur se place
        // après ce qui précède la suite, recomposée elle aussi.
        string before = _homeText[.._homeTextCursor] + added;
        string after = _homeText[_homeTextCursor..];
        string merged = LessonsHomeLogic.NormalizeText(before + after);
        _homeText = merged;
        _homeTextCursor = Math.Clamp(merged.Length - LessonsHomeLogic.NormalizeText(after).Length, 0, merged.Length);
        HomeTextChanged();
    }

    private void HomeTextChanged()
    {
        _homeTextBuilt = null;
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    /// <summary>Les débuts de grappe du texte, suivis de sa longueur.</summary>
    private int[] HomeTextBoundaries()
    {
        var starts = StringInfo.ParseCombiningCharacters(_homeText);
        var bounds = new int[starts.Length + 1];
        starts.CopyTo(bounds, 0);
        bounds[^1] = _homeText.Length;
        return bounds;
    }

    private int HomeTextPrevious(int at)
    {
        int previous = 0;
        foreach (int bound in HomeTextBoundaries())
        {
            if (bound >= at) break;
            previous = bound;
        }
        return previous;
    }

    private int HomeTextNext(int at)
    {
        foreach (int bound in HomeTextBoundaries())
            if (bound > at) return bound;
        return _homeText.Length;
    }

    private void HomeTextDelete(bool backwards)
    {
        _homeTextCursor = Math.Clamp(_homeTextCursor, 0, _homeText.Length);
        if (backwards)
        {
            if (_homeTextCursor == 0) return;
            int start = HomeTextPrevious(_homeTextCursor);
            _homeText = _homeText.Remove(start, _homeTextCursor - start);
            _homeTextCursor = start;
        }
        else
        {
            if (_homeTextCursor >= _homeText.Length) return;
            int end = HomeTextNext(_homeTextCursor);
            _homeText = _homeText.Remove(_homeTextCursor, end - _homeTextCursor);
        }
        HomeTextChanged();
    }

    /// <summary>
    /// Un caractère sur la page « Votre texte ». OnChar l'envoie avant tout filtre : Retour
    /// arrière arrive en '\b', Entrée en '\r', Ctrl+V en 0x16. Espace et Entrée qui viennent
    /// d'activer la cible focalisée n'écrivent rien ; toute autre frappe rend le focus au champ.
    /// </summary>
    private void HomeTextChar(char c)
    {
        bool onField = _focusedActionIndex < 0;
        if (c == '\u0016') { HomeTextPaste(); return; }
        if (c == '\b')
        {
            _focusedActionIndex = -1;
            HomeTextDelete(backwards: true);
            return;
        }
        if (c == '\r' || c == '\n')
        {
            if (onField) HomeTextInsert("\n");
            return;
        }
        if (char.IsHighSurrogate(c)) { _homeTextHighSurrogate = c; return; }
        if (char.IsLowSurrogate(c))
        {
            if (_homeTextHighSurrogate is { } high)
            {
                _focusedActionIndex = -1;
                HomeTextInsert(new string(new[] { high, c }));
            }
            _homeTextHighSurrogate = null;
            return;
        }
        _homeTextHighSurrogate = null;
        if (char.IsControl(c)) return;
        if (c == ' ' && !onField) return;
        _focusedActionIndex = -1;
        HomeTextInsert(c.ToString());
    }

    /// <summary>Touches du champ : flèches gauche et droite, Origine et Fin (de la ligne),
    /// Suppr. Ctrl seul (sans AltGr) vide la file de la frappe physique : sans cela, le WM_CHAR
    /// de Ctrl+V (0x16) serait remplacé par le « v » que la touche donne d'habitude.</summary>
    private void HomeTextKey(int vk)
    {
        const int VK_CONTROL = 0x11;
        bool ctrl = (TypingEngine.Windows.Win32.GetKeyState(VK_CONTROL) & unchecked((short)0x8000)) != 0
                    && !_mapper.AltGrDown;
        if (ctrl) ClearPendingPhysicalText();
        int before = _homeTextCursor;
        switch (vk)
        {
            case 0x25: _homeTextCursor = HomeTextPrevious(_homeTextCursor); break; // Gauche
            case 0x27: _homeTextCursor = HomeTextNext(_homeTextCursor); break;     // Droite
            case 0x24: // Origine
                _homeTextCursor = _homeTextCursor == 0 ? 0 : _homeText.LastIndexOf('\n', _homeTextCursor - 1) + 1;
                break;
            case 0x23: // Fin
                int end = _homeText.IndexOf('\n', Math.Min(_homeTextCursor, _homeText.Length));
                _homeTextCursor = end < 0 ? _homeText.Length : end;
                break;
            case 0x2E: HomeTextDelete(backwards: false); return; // Suppr
            default: return;
        }
        _focusedActionIndex = -1;
        if (_homeTextCursor != before) Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    /// <summary>Un clic dans le champ pose le curseur avant ou après la grappe touchée.</summary>
    private void HomeTextClick(int x, int y)
    {
        var field = _homeTextField;
        if (x < field.left || x > field.right || y < field.top || y > field.bottom) return;
        int best = -1, bestDistance = int.MaxValue;
        foreach (var (rect, start, length) in _homeTextHits)
        {
            if (y < rect.top || y > rect.bottom) continue;
            int at = x < (rect.left + rect.right) / 2 ? start : start + length;
            int distance = x < rect.left ? rect.left - x : x > rect.right ? x - rect.right : 0;
            if (distance < bestDistance) { best = at; bestDistance = distance; }
        }
        if (best < 0) best = _homeText.Length;
        _homeTextCursor = best;
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    /// <summary>Ce que la leçon fera du texte : gardé tant que le texte et la langue restent.</summary>
    private LessonCustomTextResult HomeTextBuild()
    {
        if (_homeTextBuilt is { } built && built.Text == _homeText && built.Language == L.Language)
            return built.Result;
        var result = LessonCustomText.Build(_homeText, L.LessonsHome_TextInstruction);
        _homeTextBuilt = (_homeText, L.Language, result);
        return result;
    }

    /// <summary>La leçon a-t-elle laissé de côté une part du texte tapable (au-delà de
    /// <see cref="LessonCustomText.MaxLines"/> lignes) ?</summary>
    private static bool HomeTextWasCut(IReadOnlyList<HomeTextItem> items, LessonExercise exercise)
    {
        int typable = items.Count(item => item.Kind is HomeTextKind.Plain or HomeTextKind.Gesture && !string.IsNullOrWhiteSpace(item.Element));
        int kept = 0;
        var elements = StringInfo.GetTextElementEnumerator(exercise.Content);
        while (elements.MoveNext())
            if (!string.IsNullOrWhiteSpace(elements.GetTextElement())) kept++;
        return kept < typable;
    }

    private void HomeStartText()
    {
        var result = HomeTextBuild();
        if (result.Exercise is not { } exercise) return;
        var characters = LessonsHomeLogic.TextBilan(exercise.Content, _progress.Gestures, DateTimeOffset.UtcNow, HomeTextTracked)
            .Select(line => line.Character).ToList();
        HomeStartComposed(exercise, L.LessonsHome_YourText, L.LessonsHome_YourText, characters);
    }

    // ── Dessin ───────────────────────────────────────────────────────────

    private void DrawHomeTextPage(IntPtr hdc, Win32.RECT body, in HomePalette pal, HomeSnapshot snapshot)
    {
        _homeTextHits.Clear();
        var items = LessonsHomeLogic.AnalyzeText(_homeText, HomeTextTypable, HomeTextTracked);
        var result = HomeTextBuild();

        int gap = S(18);
        int bilanW = Math.Min(S(330), (body.right - body.left) * 2 / 5);
        int leftRight = body.right - bilanW - gap;

        // Fil : « Aujourd'hui › Votre texte · collez… ».
        int crumbMid = body.top + S(11);
        var crumbFont = HomeFont(AppFonts.Text, 14);
        var crumbBold = HomeFont(AppFonts.Text, 14, TypeRamp.Semibold);
        var link = HomeLink(hdc, body.left + S(4), crumbMid, L.LessonsHome_Today, L.LessonsHome_BackToTodayTooltip,
            () => HomeShowPage(HomePage.Today), pal, half: 10);
        int cx = link.right + S(2);
        var (ascent, descent) = PaperText.Metrics(hdc, crumbFont);
        int crumbBase = crumbMid - (ascent + descent) / 2 + ascent;
        cx = PaperText.Draw(hdc, " › ", crumbFont, cx, crumbBase, pal.Muted);
        cx = PaperText.Draw(hdc, L.LessonsHome_YourText, crumbBold, cx, crumbBase, pal.Ink);
        string hint = " · " + L.LessonsHome_TextCrumbHint;
        PaperText.Draw(hdc, HomeEllipsize(hdc, hint, crumbFont, leftRight - cx), crumbFont, cx, crumbBase, pal.Muted);

        // Champ et bilan, à la même hauteur ; l'aperçu prend le reste.
        int fieldTop = body.top + S(28);
        int bodyH = body.bottom - fieldTop;
        int fieldH = Math.Clamp(bodyH - S(250), S(150), S(262));
        var field = new Win32.RECT { left = body.left, top = fieldTop, right = leftRight, bottom = fieldTop + fieldH };
        var bilan = new Win32.RECT { left = leftRight + gap, top = fieldTop, right = body.right, bottom = field.bottom };
        DrawHomeTextField(hdc, field, pal, items);
        DrawHomeTextBilan(hdc, bilan, pal, snapshot, items, result);

        var preview = new Win32.RECT { left = body.left, top = field.bottom + S(20), right = body.right, bottom = body.bottom };
        DrawHomeTextPreview(hdc, preview, pal, result.Exercise);

        _surfaceView = new LessonSurfaceView(L.LessonsHome_YourText, L.LessonsHome_TextSurfaceHelp, _homeText,
            Math.Clamp(_homeTextCursor, 0, _homeText.Length), field, Array.Empty<Win32.RECT>(), _systemCaretRect);
    }

    /// <summary>Le champ : le texte en grand, coupé aux mots ; le pied dit les deux marques et le
    /// nombre de signes. Bordure d'action épaisse quand il a le focus.</summary>
    private void DrawHomeTextField(IntPtr hdc, Win32.RECT field, in HomePalette pal, IReadOnlyList<HomeTextItem> items)
    {
        _homeTextField = field;
        bool focused = _focusedActionIndex < 0;
        int border = focused ? Math.Max(2, S(2)) : 1;
        PaperDraw.RoundBox(hdc, field, pal.Surface, focused ? pal.Action : pal.Border, S(3), border);

        int padX = S(20), padTop = S(14), footH = S(26);
        var text = new Win32.RECT { left = field.left + padX, top = field.top + padTop, right = field.right - S(14), bottom = field.bottom - footH };
        var font = HomeFont(AppFonts.Title, 30, TypeRamp.Semibold);
        var skippedFont = HomeFont(AppFonts.Text, 24);
        var (ascent, descent) = PaperText.Metrics(hdc, font);
        int lineH = (ascent + descent) * 3 / 2;
        int width = Math.Max(S(40), text.right - text.left);

        // Les grappes, leur début dans le texte et leur largeur.
        var starts = new int[items.Count];
        var widths = new int[items.Count];
        int offset = 0;
        for (int i = 0; i < items.Count; i++)
        {
            starts[i] = offset;
            offset += items[i].Element.Length;
            widths[i] = items[i].Kind switch
            {
                HomeTextKind.Break => 0,
                HomeTextKind.Skipped => PaperText.Width(hdc, items[i].Element, skippedFont) + S(12),
                _ => PaperText.Width(hdc, items[i].Element, font),
            };
        }

        // Lignes visuelles : [début, fin) en index de grappe, coupées à la dernière espace.
        var lines = new List<(int From, int To)>();
        int from = 0, x = 0, lastSpace = -1;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Kind == HomeTextKind.Break)
            {
                lines.Add((from, i + 1));
                from = i + 1; x = 0; lastSpace = -1;
                continue;
            }
            if (x + widths[i] > width && i > from)
            {
                int cut = lastSpace >= from ? lastSpace + 1 : i;
                lines.Add((from, cut));
                from = cut; lastSpace = -1;
                x = 0;
                for (int k = from; k < i; k++) x += widths[k];
            }
            if (items[i].Element == " ") lastSpace = i;
            x += widths[i];
        }
        lines.Add((from, items.Count));

        // La ligne du curseur, puis le défilement qui la garde visible.
        int cursor = Math.Clamp(_homeTextCursor, 0, _homeText.Length);
        int cursorItem = Array.FindIndex(starts, s => s >= cursor);
        if (cursorItem < 0) cursorItem = items.Count;
        int cursorLine = lines.Count - 1;
        for (int l = 0; l < lines.Count; l++)
        {
            var (lineFrom, lineTo) = lines[l];
            bool endsWithBreak = lineTo > lineFrom && items[lineTo - 1].Kind == HomeTextKind.Break;
            int lastPosition = endsWithBreak ? lineTo - 1 : lineTo;
            if (cursorItem >= lineFrom && (cursorItem < lastPosition || (cursorItem == lastPosition && (endsWithBreak || l == lines.Count - 1))))
            { cursorLine = l; break; }
        }
        int visible = Math.Max(1, (text.bottom - text.top) / lineH);
        _homeTextFirstLine = Math.Clamp(_homeTextFirstLine, Math.Max(0, cursorLine - visible + 1), cursorLine);

        int saved = Win32.SaveDC(hdc);
        Win32.IntersectClipRect(hdc, field.left + border, field.top + border, field.right - border, text.bottom);
        if (items.Count == 0)
        {
            var placeholder = HomeFont(AppFonts.Text, 17);
            PaperText.DrawLine(hdc, HomeEllipsize(hdc, L.LessonsHome_TextPlaceholder, placeholder, width), placeholder,
                new Win32.RECT { left = text.left, top = text.top, right = text.right, bottom = text.top + lineH }, pal.Muted, TextAlign.Left);
        }
        int underline = Math.Max(2, S(3));
        int? caretX = null;
        int caretTop = 0;
        for (int l = _homeTextFirstLine; l < lines.Count && l < _homeTextFirstLine + visible; l++)
        {
            var (lineFrom, lineTo) = lines[l];
            int top = text.top + (l - _homeTextFirstLine) * lineH;
            int baseline = top + (lineH - (ascent + descent)) / 2 + ascent;
            int lx = text.left;
            for (int i = lineFrom; i < lineTo; i++)
            {
                var item = items[i];
                if (l == cursorLine && i == cursorItem) caretX = lx;
                if (item.Kind == HomeTextKind.Break) continue;
                var cell = new Win32.RECT { left = lx, top = top, right = lx + widths[i], bottom = top + lineH };
                if (item.Kind == HomeTextKind.Skipped)
                {
                    var box = new Win32.RECT { left = lx + S(3), top = top + S(8), right = lx + widths[i] - S(3), bottom = top + lineH - S(8) };
                    PaperDraw.RoundBox(hdc, box, pal.Surface, pal.Muted, S(4), Math.Max(1, S(1)), Win32.PS_DASH);
                    PaperText.DrawLine(hdc, item.Element, skippedFont, box, pal.Ink, TextAlign.Center);
                }
                else
                {
                    PaperText.Draw(hdc, item.Element, font, lx, baseline, pal.Ink);
                    if (item.Kind == HomeTextKind.Gesture)
                        PaperDraw.Fill(hdc, lx, baseline + S(6), lx + widths[i], baseline + S(6) + underline, pal.Action);
                }
                _homeTextHits.Add((cell, starts[i], item.Element.Length));
                lx += widths[i];
            }
            if (l == cursorLine && caretX == null) caretX = lx;
            if (l == cursorLine) caretTop = top + (lineH - (ascent + descent)) / 2;
        }
        _systemCaretRect = null;
        if (focused && caretX is { } cxPos && cursorLine >= _homeTextFirstLine && cursorLine < _homeTextFirstLine + visible)
        {
            var caret = new Win32.RECT { left = cxPos + S(1), top = caretTop, right = cxPos + S(1) + Math.Max(2, S(2)), bottom = caretTop + ascent + descent };
            PaperDraw.Fill(hdc, caret, pal.Ink);
            _systemCaretRect = caret;
        }
        Win32.RestoreDC(hdc, saved);

        // Pied : les deux marques, puis le compte à droite.
        var small = HomeFont(AppFonts.Text, 12);
        int footTop = field.bottom - footH;
        int mid = footTop + footH / 2 - S(1);
        int fx = field.left + padX;
        PaperDraw.Fill(hdc, fx, mid + S(2), fx + S(16), mid + S(2) + underline, pal.Action);
        fx += S(22);
        fx += PaperText.DrawLine(hdc, L.LessonsHome_TextLegendGesture, small,
            new Win32.RECT { left = fx, top = footTop, right = field.right, bottom = footTop + footH }, pal.Muted, TextAlign.Left) + S(18);
        PaperDraw.RoundBox(hdc, new Win32.RECT { left = fx, top = mid - S(5), right = fx + S(14), bottom = mid + S(7) }, pal.Surface, pal.Muted, S(3),
            Math.Max(1, S(1)), Win32.PS_DASH);
        fx += S(20);
        int countLeft = fx + PaperText.Width(hdc, L.LessonsHome_TextLegendSkipped, small) + S(18);
        PaperText.DrawLine(hdc, L.LessonsHome_TextLegendSkipped, small,
            new Win32.RECT { left = fx, top = footTop, right = field.right - S(14), bottom = footTop + footH }, pal.Muted, TextAlign.Left);
        string count = L.LessonsHome_TextCount(items.Count(item => item.Kind != HomeTextKind.Break));
        int countW = PaperText.Width(hdc, count, small);
        if (field.right - S(14) - countW > countLeft)
            PaperText.DrawLine(hdc, count, small,
                new Win32.RECT { left = field.right - S(14) - countW, top = footTop, right = field.right - S(14), bottom = footTop + footH }, pal.Muted, TextAlign.Left);
    }

    /// <summary>« Ce que vous allez taper » : chaque caractère à geste que la leçon gardera, avec
    /// son nombre de passages (« nouveau » s'il n'a jamais été tapé), « à revoir » s'il est échu
    /// et sa boîte en cinq crans ; puis les caractères sautés. Le bouton lance la séance.</summary>
    private void DrawHomeTextBilan(IntPtr hdc, Win32.RECT box, in HomePalette pal, HomeSnapshot snapshot,
        IReadOnlyList<HomeTextItem> items, LessonCustomTextResult result)
    {
        PaperDraw.RoundBox(hdc, box, pal.Surface, pal.Ink, S(3));
        int pad = S(14);
        int x0 = box.left + pad, x1 = box.right - pad;
        PaperText.DrawLine(hdc, L.LessonsHome_TextBilanTitle, HomeFont(AppFonts.Text, 14, TypeRamp.Semibold),
            new Win32.RECT { left = x0, top = box.top + S(10), right = x1, bottom = box.top + S(30) }, pal.Ink, TextAlign.Left);

        var lines = result.Exercise is { } exercise
            ? LessonsHomeLogic.TextBilan(exercise.Content, snapshot.Entries, snapshot.Now, HomeTextTracked)
            : Array.Empty<HomeTextBilanLine>();
        var skipped = LessonsHomeLogic.SkippedElements(items);

        // Pied : le bouton s'il y a une séance, et une note s'il y en a une (sans note, les
        // lignes gardent sa hauteur).
        string? note = null;
        if (result.Exercise == null)
            note = _homeText.Trim().Length == 0 ? L.LessonsHome_TextEmpty : L.LessonsHome_TextNothingTypable;
        else if (HomeTextWasCut(items, result.Exercise))
            note = L.LessonsHome_TextCut(LessonCustomText.MaxLines);
        int buttonH = S(34);
        int noteH = S(32);
        int footTop = box.bottom - pad - buttonH - (note != null ? S(6) + noteH : 0);
        if (result.Exercise != null)
        {
            string label = L.LessonsHome_TextStart;
            var button = new Win32.RECT { left = x0, top = footTop, right = x0 + HomeButtonWidth(hdc, label), bottom = footTop + buttonH };
            HomeButton(hdc, button, label, L.LessonsHome_TextStartName(lines.Count, skipped.Count), HomeStartText, primary: true, pal);
        }
        if (note != null)
        {
            int noteTop = result.Exercise != null ? footTop + buttonH + S(6) : footTop;
            PaperText.DrawParagraph(hdc, new[] { new TextRun(note) }, HomeFont(AppFonts.Text, 12), HomeFont(AppFonts.Text, 12, TypeRamp.Semibold),
                new Win32.RECT { left = x0, top = noteTop, right = x1, bottom = noteTop + noteH + (result.Exercise != null ? 0 : buttonH) },
                pal.Muted, pal.Ink, S(16));
        }

        // Les lignes, tant qu'elles tiennent au-dessus du pied.
        int rowH = S(27);
        int y = box.top + S(36);
        int bottom = footTop - S(8);
        int rows = lines.Count + skipped.Count;
        if (rows == 0)
        {
            if (result.Exercise != null)
                HomeParagraph(hdc, new Win32.RECT { left = x0, top = y, right = x1, bottom = bottom },
                    new[] { new TextRun(L.LessonsHome_TextBilanNone) }, pal);
            return;
        }
        int fits = Math.Max(0, (bottom - y) / rowH);
        int shown = rows <= fits ? rows : Math.Max(0, fits - 1);
        var glyphFont = HomeFont(AppFonts.Title, 19, TypeRamp.Semibold);
        var small = HomeFont(AppFonts.Text, 12);
        var smallBold = HomeFont(AppFonts.Text, 12, TypeRamp.Semibold);
        int notchW = S(5), notchH = S(10), notchGap = Math.Max(1, S(1));
        int gaugeW = GestureMastery.MaxBox * notchW + (GestureMastery.MaxBox - 1) * notchGap;
        PaperDraw.Fill(hdc, x0, y, x1, y + 1, pal.Border);
        for (int r = 0; r < shown; r++)
        {
            var row = new Win32.RECT { left = x0, top = y, right = x1, bottom = y + rowH };
            int textLeft = x0 + S(34);
            if (r < lines.Count)
            {
                var line = lines[r];
                PaperText.DrawLine(hdc, HomeGlyph(line.Character), glyphFont,
                    new Win32.RECT { left = x0, top = y, right = x0 + S(26), bottom = y + rowH }, pal.Ink, TextAlign.Center);
                int gx = x1 - gaugeW, gy = y + (rowH - notchH) / 2;
                for (int k = 0; k < GestureMastery.MaxBox; k++)
                {
                    var notch = new Win32.RECT { left = gx + k * (notchW + notchGap), top = gy, right = gx + k * (notchW + notchGap) + notchW, bottom = gy + notchH };
                    if (k < line.Box) PaperDraw.Fill(hdc, notch, pal.Action);
                    else PaperDraw.Frame(hdc, notch, pal.Pale);
                }
                string what = line.Box < 0 ? L.LessonsHome_TextNew : L.LessonsHome_TextTimes(line.Count);
                int used = PaperText.DrawLine(hdc, what, small,
                    new Win32.RECT { left = textLeft, top = y, right = gx - S(6), bottom = y + rowH }, pal.Muted, TextAlign.Left);
                if (line.Due)
                    PaperText.DrawLine(hdc, HomeEllipsize(hdc, L.LessonsHome_DueTag, smallBold, gx - S(6) - textLeft - used), smallBold,
                        new Win32.RECT { left = textLeft + used, top = y, right = gx - S(6), bottom = y + rowH }, pal.Ink, TextAlign.Left);
                AddHover(row, L.LessonsHome_TextBilanSpoken(line.Character, what, line.Due, HomeLevelText(line.Box)), preferAbove: false, compact: true);
            }
            else
            {
                string element = skipped[r - lines.Count];
                var mark = new Win32.RECT { left = x0 + S(2), top = y + S(4), right = x0 + S(26), bottom = y + rowH - S(4) };
                PaperDraw.RoundBox(hdc, mark, pal.Surface, pal.Muted, S(3), Math.Max(1, S(1)), Win32.PS_DASH);
                PaperText.DrawLine(hdc, element, HomeFont(AppFonts.Text, 13), mark, pal.Ink, TextAlign.Center);
                PaperText.DrawLine(hdc, L.LessonsHome_TextSkipped, small,
                    new Win32.RECT { left = textLeft, top = y, right = x1, bottom = y + rowH }, pal.Muted, TextAlign.Left);
            }
            y += rowH;
            PaperDraw.Fill(hdc, x0, y - 1, x1, y, pal.Border);
        }
        if (shown < rows)
            PaperText.DrawLine(hdc, L.LessonsHome_LayerMore(rows - shown), small,
                new Win32.RECT { left = x0, top = y + S(4), right = x1, bottom = y + S(20) }, pal.Muted, TextAlign.Left);
    }

    /// <summary>L'aperçu : la première ligne que la leçon fera taper, en Tablature (ce que font
    /// les doigts sous chaque caractère). Les caractères à geste sont à la couleur d'action. La
    /// portée se réduit pour tenir dans la hauteur qui reste ; trop basse, elle n'est pas dessinée.</summary>
    private void DrawHomeTextPreview(IntPtr hdc, Win32.RECT area, in HomePalette pal, LessonExercise? exercise)
    {
        if (exercise == null || area.bottom - area.top < S(120)) return;
        var titleFont = HomeFont(AppFonts.Title, 19, TypeRamp.Semibold);
        int titleW = PaperText.DrawLine(hdc, L.LessonsHome_TextPreviewTitle, titleFont,
            new Win32.RECT { left = area.left, top = area.top, right = area.right, bottom = area.top + S(26) }, pal.Ink, TextAlign.Left);
        var subFont = HomeFont(AppFonts.Text, 13);
        PaperText.DrawLine(hdc, HomeEllipsize(hdc, L.LessonsHome_TextPreviewSub, subFont, area.right - area.left - titleW - S(12)), subFont,
            new Win32.RECT { left = area.left + titleW + S(12), top = area.top + S(3), right = area.right, bottom = area.top + S(26) }, pal.Muted, TextAlign.Left);

        int boxTop = area.top + S(34);
        int padTop = S(14), padBottom = S(10);
        float natural = TablatureStyle.Lessons(HomeScale).Height + padTop + padBottom;
        float fit = Math.Min(1f, (area.bottom - boxTop) / natural);
        var style = TablatureStyle.Lessons(HomeScale * fit);
        var box = new Win32.RECT { left = area.left, top = boxTop, right = area.right, bottom = boxTop + style.Height + (int)((padTop + padBottom) * fit) };
        PaperDraw.RoundBox(hdc, box, PaperTheme.Surface, pal.Border, 0);

        var columns = new List<TabColumn>();
        foreach (char c in exercise.Lines[0])
        {
            string glyph = HomeGlyph(c.ToString());
            var steps = Gesture.Of(c.ToString());
            if (HomeTextTracked(c) && steps.Count > 0)
            {
                var gesture = TabColumn.FromGesture(steps, TabState.Chosen);
                gesture[0] = gesture[0] with { Glyph = glyph };
                columns.AddRange(gesture);
            }
            else
            {
                var first = steps.Count > 0 ? steps[0] : default;
                columns.Add(new TabColumn(c == ' ' ? " " : glyph, steps.Count > 0 && first.Shift, steps.Count > 0 && first.AltGr, null, TabState.Done));
            }
        }
        int[] widths = TablatureRenderer.MeasureColumns(hdc, columns, style);
        var grid = new Win32.RECT { left = box.left + 1, top = box.top + (int)(padTop * fit), right = box.right - S(12), bottom = box.bottom };
        TablatureRenderer.Draw(hdc, columns, widths, style, grid);
    }
}
