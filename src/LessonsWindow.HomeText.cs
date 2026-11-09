// Accueil des Leçons, « Votre texte » (maquette lecons-innov-b-texte, étape 3 de la direction B) :
// un champ où l'on colle ou tape un texte, qui marque déjà les caractères à geste (soulignés) et
// ceux que la disposition ne tape pas (cadre en tirets) ; à droite, ce que la leçon fera taper ;
// dessous, la Tablature de la première ligne. « Taper ce texte » lance la séance composée tirée
// de LessonCustomText. Le texte vit le temps de la fenêtre : rien n'est gardé sur le disque, et
// la fermeture des Leçons l'oublie (HomeOnHide).
//
// Le champ est un TextEditBuffer, le même que celui du mode Libre (audit 09/11, E22) : sélection
// au clavier et à la souris, Ctrl+A/C/X/V/Z/Y, mots, et un message quand un collage dépasse
// LessonsHomeLogic.MaxTextLength. Il garde son texte déjà normalisé (forme composée, fins de
// ligne en \n, tabulations en espaces) : les grappes de LessonsHomeLogic.AnalyzeText se recollent
// alors exactement en lui, et le curseur, un index UTF-16, tombe toujours sur une limite de grappe.
using System.Globalization;

namespace AZERTYGlobal;

internal sealed partial class LessonsWindow
{
    private readonly TextEditBuffer _homeEdit = new(LessonsHomeLogic.MaxTextLength, text => LessonsHomeLogic.NormalizeText(text));
    // Le dernier collage ou la dernière frappe a buté sur la limite : le pied du champ le dit.
    private bool _homeTextTruncated;
    // Moitié haute d'un caractère hors du plan de base (émoji) : il arrive en deux WM_CHAR.
    private char? _homeTextHighSurrogate;
    // Première ligne visuelle du champ : le champ défile pour garder le curseur visible.
    private int _homeTextFirstLine;
    private Win32.RECT _homeTextField;
    // Zone du texte dans le champ, au dernier repeint : la mise en page s'y refait à la demande.
    private Win32.RECT _homeTextArea;
    private HomeTextLayout? _homeTextLayout;
    private (string Text, string Language, LessonCustomTextResult Result)? _homeTextBuilt;

    /// <summary>Mise en page du champ : chaque grappe, son début dans le texte et sa largeur, et
    /// les lignes visuelles [début, fin) en index de grappe, coupées à la dernière espace.</summary>
    private sealed record HomeTextLayout(string Text, int Scale, Win32.RECT Area, IReadOnlyList<HomeTextItem> Items,
        int[] Starts, int[] Widths, IReadOnlyList<(int From, int To)> Lines, int LineHeight);

    private static bool HomeTextTypable(string character) => CharacterIndex.Shared.ByCharacter.ContainsKey(character);

    private static bool HomeTextTracked(char c) => HomeKeyCatalog.Shared.TryGet(c.ToString(), out _);

    /// <summary>« Coller un texte » : la page « Votre texte ». Un champ vide reçoit le texte du
    /// presse-papiers ; un champ déjà rempli reste tel quel (Ctrl+V y ajoute).</summary>
    private void HomeOpenText()
    {
        if (_homeEdit.Text.Length == 0) HomeTextPaste();
        HomeShowPage(HomePage.Text);
    }

    private void HomeTextForget()
    {
        _homeEdit.Clear();
        _homeTextTruncated = false;
        _homeTextHighSurrogate = null;
        _homeTextFirstLine = 0;
        _homeTextLayout = null;
        _homeTextBuilt = null;
    }

    /// <summary>Le texte du presse-papiers, inséré au curseur (à la place de la sélection).</summary>
    private void HomeTextPaste()
    {
        _homeTextTruncated = _homeEdit.Paste(Clipboard).Truncated;
        HomeTextChanged();
    }

    /// <summary>Insère au curseur, sous la forme que garde le champ, dans la limite de
    /// <see cref="LessonsHomeLogic.MaxTextLength"/> caractères (coupé à une limite de grappe,
    /// et dit au pied du champ).</summary>
    internal void HomeTextInsert(string text)
    {
        _homeTextTruncated = _homeEdit.Insert(text).Truncated;
        HomeTextChanged();
    }

    private void HomeTextChanged() => Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);

    /// <summary>
    /// Un caractère sur la page « Votre texte ». OnChar l'envoie avant tout filtre. Les caractères
    /// de contrôle (Retour arrière, Entrée, Ctrl+lettre) n'écrivent rien : leur touche a déjà agi
    /// au keydown (HomeTextKey). Espace qui vient d'activer la cible focalisée n'écrit rien ;
    /// toute autre frappe rend le focus au champ.
    /// </summary>
    private void HomeTextChar(char c)
    {
        string typed;
        if (char.IsHighSurrogate(c)) { _homeTextHighSurrogate = c; return; }
        if (char.IsLowSurrogate(c))
        {
            if (_homeTextHighSurrogate is not { } high) return;
            typed = new string(new[] { high, c });
        }
        else
        {
            _homeTextHighSurrogate = null;
            if (char.IsControl(c)) return;
            if (c == ' ' && _focusedActionIndex >= 0) return;
            typed = c.ToString();
        }
        _homeTextHighSurrogate = null;
        _focusedActionIndex = -1;
        _homeTextTruncated = _homeEdit.Insert(typed, typed: true).Truncated;
        HomeTextChanged();
    }

    /// <summary>Touches du champ, comme dans un champ Windows : flèches (Maj : sélection, Ctrl :
    /// mot), Origine et Fin (de la ligne visuelle ; Ctrl : du texte), Retour arrière et Suppr
    /// (Ctrl : un mot), Entrée, Ctrl+A/C/X/V/Z/Y. Alt Gr tenu n'est jamais un raccourci.</summary>
    private void HomeTextKey(int vk)
    {
        var key = TextEditKeys.Map(vk, ReadEditModifiers());
        switch (key.Command)
        {
            case TextEditCommand.None:
                return;
            case TextEditCommand.Left:
            case TextEditCommand.Right:
            case TextEditCommand.WordLeft:
            case TextEditCommand.WordRight:
                _homeEdit.MoveHorizontal(key.Command is TextEditCommand.Left or TextEditCommand.WordLeft ? -1 : 1,
                    word: key.Command is TextEditCommand.WordLeft or TextEditCommand.WordRight, key.Extend);
                break;
            case TextEditCommand.Up:
            case TextEditCommand.Down:
                HomeTextMoveVertical(key.Command == TextEditCommand.Up ? -1 : 1, key.Extend);
                break;
            case TextEditCommand.LineStart:
            case TextEditCommand.LineEnd:
                HomeTextMoveToLineBoundary(key.Command == TextEditCommand.LineEnd, key.Extend);
                break;
            case TextEditCommand.DocumentStart:
                _homeEdit.MoveTo(0, key.Extend);
                break;
            case TextEditCommand.DocumentEnd:
                _homeEdit.MoveTo(_homeEdit.Text.Length, key.Extend);
                break;
            case TextEditCommand.Backspace:
            case TextEditCommand.Delete:
            case TextEditCommand.WordBackspace:
            case TextEditCommand.WordDelete:
                if (_homeEdit.Delete(backwards: key.Command is TextEditCommand.Backspace or TextEditCommand.WordBackspace,
                        word: key.Command is TextEditCommand.WordBackspace or TextEditCommand.WordDelete))
                    _homeTextTruncated = false;
                break;
            case TextEditCommand.NewLine:
                // Entrée sur une cible focalisée l'a déjà activée (OnKeyDown) ; ici, le champ a le focus.
                if (_focusedActionIndex >= 0) return;
                _homeTextTruncated = _homeEdit.Insert("\n", typed: true).Truncated;
                break;
            case TextEditCommand.SelectAll:
                _homeEdit.SelectAll();
                break;
            case TextEditCommand.Copy:
                _homeEdit.Copy(Clipboard);
                break;
            case TextEditCommand.Cut:
                if (_homeEdit.Cut(Clipboard)) _homeTextTruncated = false;
                break;
            case TextEditCommand.Paste:
                _homeTextTruncated = _homeEdit.Paste(Clipboard).Truncated;
                break;
            case TextEditCommand.Undo:
                if (_homeEdit.Undo()) _homeTextTruncated = false;
                break;
            case TextEditCommand.Redo:
                if (_homeEdit.Redo()) _homeTextTruncated = false;
                break;
        }
        _focusedActionIndex = -1;
        HomeTextChanged();
    }

    // ── Mise en page, curseur et souris ─────────────────────────────────

    /// <summary>Coupe le texte en lignes visuelles dans <paramref name="area"/>, avec les polices
    /// du champ ; le repeint et les touches (Haut, Bas, Origine, Fin, la souris) s'en servent.</summary>
    private HomeTextLayout BuildHomeTextLayout(IntPtr hdc, Win32.RECT area, IReadOnlyList<HomeTextItem> items)
    {
        var font = HomeFont(AppFonts.Title, 30, TypeRamp.Semibold);
        var skippedFont = HomeFont(AppFonts.Text, 24);
        var (ascent, descent) = PaperText.Metrics(hdc, font);
        int lineH = Math.Max(1, (ascent + descent) * 3 / 2);
        int width = Math.Max(S(40), area.right - area.left);

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
        var layout = new HomeTextLayout(_homeEdit.Text, S(1000), area, items, starts, widths, lines, lineH);
        _homeTextLayout = layout;
        return layout;
    }

    /// <summary>La mise en page du texte actuel : celle du dernier repeint, refaite si le texte a
    /// changé depuis (une touche peut arriver avant le repeint). Nulle tant que le champ n'a
    /// jamais été dessiné.</summary>
    private HomeTextLayout? CurrentHomeTextLayout()
    {
        if (_homeTextLayout is { } kept && kept.Text == _homeEdit.Text && kept.Scale == S(1000)) return kept;
        if (_homeTextArea.right <= _homeTextArea.left) return null;
        var items = LessonsHomeLogic.AnalyzeText(_homeEdit.Text, HomeTextTypable, HomeTextTracked);
        var hdc = Win32.GetDC(_hWnd);
        try { return BuildHomeTextLayout(hdc, _homeTextArea, items); }
        finally { Win32.ReleaseDC(_hWnd, hdc); }
    }

    private int HomeVisibleLines(HomeTextLayout layout) =>
        Math.Max(1, (layout.Area.bottom - layout.Area.top) / layout.LineHeight);

    /// <summary>La ligne visuelle d'une position : une position de fin de ligne coupée au mot
    /// appartient à la ligne suivante, sauf en fin de texte ou avant une fin de ligne.</summary>
    private static int HomeLineOf(HomeTextLayout layout, int index)
    {
        int item = Array.FindIndex(layout.Starts, s => s >= index);
        if (item < 0) item = layout.Items.Count;
        for (int l = 0; l < layout.Lines.Count; l++)
        {
            var (from, to) = layout.Lines[l];
            bool endsWithBreak = to > from && layout.Items[to - 1].Kind == HomeTextKind.Break;
            int last = endsWithBreak ? to - 1 : to;
            if (item >= from && (item < last || (item == last && (endsWithBreak || l == layout.Lines.Count - 1))))
                return l;
        }
        return layout.Lines.Count - 1;
    }

    /// <summary>L'abscisse d'une position dans sa ligne, depuis le bord gauche du texte.</summary>
    private static int HomeXOf(HomeTextLayout layout, int line, int index)
    {
        var (from, to) = layout.Lines[line];
        int x = 0;
        for (int i = from; i < to && layout.Starts[i] < index; i++)
            if (layout.Items[i].Kind != HomeTextKind.Break) x += layout.Widths[i];
        return x;
    }

    /// <summary>La limite de grappe la plus proche de l'abscisse dans la ligne. Au-delà de la fin
    /// d'une ligne coupée au mot, avant son espace final, pour rester sur la ligne.</summary>
    private int HomeIndexAtX(HomeTextLayout layout, int line, int x)
    {
        var (from, to) = layout.Lines[line];
        int acc = 0;
        for (int i = from; i < to; i++)
        {
            if (layout.Items[i].Kind == HomeTextKind.Break) return layout.Starts[i];
            int w = layout.Widths[i];
            if (x < acc + w / 2) return layout.Starts[i];
            acc += w;
        }
        if (to >= layout.Items.Count) return layout.Text.Length;
        return to > from && layout.Items[to - 1].Element == " " ? layout.Starts[to - 1] : layout.Starts[to];
    }

    /// <summary>La position sous un point du canevas ; au-dessus ou au-dessous du champ, la ligne
    /// d'avant ou d'après (le glisser fait défiler).</summary>
    private int HomeIndexAtPoint(HomeTextLayout layout, int x, int y)
    {
        int rel = y - layout.Area.top;
        int offset = rel >= 0 ? rel / layout.LineHeight : (rel - layout.LineHeight + 1) / layout.LineHeight;
        int line = Math.Clamp(_homeTextFirstLine + offset, 0, layout.Lines.Count - 1);
        return HomeIndexAtX(layout, line, Math.Max(0, x - layout.Area.left));
    }

    private void HomeTextMoveVertical(int direction, bool extend)
    {
        if (CurrentHomeTextLayout() is not { } layout)
        {
            _homeEdit.MoveTo(direction < 0 ? 0 : _homeEdit.Text.Length, extend);
            return;
        }
        int line = HomeLineOf(layout, _homeEdit.Caret);
        int target = line + direction;
        if (target < 0) _homeEdit.MoveTo(0, extend); // Haut sur la première ligne : début du texte, comme Windows
        else if (target >= layout.Lines.Count) _homeEdit.MoveTo(_homeEdit.Text.Length, extend);
        else _homeEdit.MoveTo(HomeIndexAtX(layout, target, HomeXOf(layout, line, _homeEdit.Caret)), extend);
    }

    private void HomeTextMoveToLineBoundary(bool toEnd, bool extend)
    {
        if (CurrentHomeTextLayout() is not { } layout)
        {
            _homeEdit.MoveTo(toEnd ? _homeEdit.Text.Length : 0, extend);
            return;
        }
        int line = HomeLineOf(layout, _homeEdit.Caret);
        var (from, _) = layout.Lines[line];
        int start = from < layout.Starts.Length ? layout.Starts[from] : layout.Text.Length;
        _homeEdit.MoveTo(toEnd ? HomeIndexAtX(layout, line, int.MaxValue) : start, extend);
    }

    /// <summary>Le point est-il dans le champ de « Votre texte » affiché, hors d'une cible ?</summary>
    private bool HomeTextFieldAt(int x, int y) =>
        HomeShown && _homePage == HomePage.Text && IsPointInRect(_homeTextField, x, y) && HitClickAction(x, y) < 0;

    /// <summary>Bouton gauche enfoncé dans le champ : pose le curseur (Maj : étend la sélection).</summary>
    private bool HomeTextMouseDown(int x, int y, bool extend)
    {
        if (!HomeTextFieldAt(x, y) || CurrentHomeTextLayout() is not { } layout) return false;
        _focusedActionIndex = -1;
        _homeEdit.MoveTo(HomeIndexAtPoint(layout, x, y), extend);
        HomeTextChanged();
        return true;
    }

    private void HomeTextMouseDrag(int x, int y)
    {
        if (CurrentHomeTextLayout() is not { } layout) return;
        int before = _homeEdit.Caret;
        _homeEdit.MoveTo(HomeIndexAtPoint(layout, x, y), extend: true);
        if (_homeEdit.Caret != before) HomeTextChanged();
    }

    /// <summary>Double-clic dans le champ : le mot sous le pointeur.</summary>
    private bool HomeTextSelectWordAt(int x, int y)
    {
        if (!HomeTextFieldAt(x, y) || CurrentHomeTextLayout() is not { } layout) return false;
        _homeEdit.SelectWordAt(HomeIndexAtPoint(layout, x, y));
        HomeTextChanged();
        return true;
    }

    /// <summary>Ce que la leçon fera du texte : gardé tant que le texte et la langue restent.</summary>
    private LessonCustomTextResult HomeTextBuild()
    {
        if (_homeTextBuilt is { } built && built.Text == _homeEdit.Text && built.Language == L.Language)
            return built.Result;
        var result = LessonCustomText.Build(_homeEdit.Text, L.LessonsHome_TextInstruction);
        _homeTextBuilt = (_homeEdit.Text, L.Language, result);
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
        var items = LessonsHomeLogic.AnalyzeText(_homeEdit.Text, HomeTextTypable, HomeTextTracked);
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

        _surfaceView = new LessonSurfaceView(L.LessonsHome_YourText, L.LessonsHome_TextSurfaceHelp, _homeEdit.Text,
            _homeEdit.Caret, field, Array.Empty<Win32.RECT>(), _systemCaretRect, SelectionAnchor: _homeEdit.Anchor);
    }

    /// <summary>Le champ : le texte en grand, coupé aux mots, sa sélection sur fond d'action
    /// pâle ; le pied dit les deux marques et le nombre de signes, ou que le champ est plein.
    /// Bordure d'action épaisse quand il a le focus.</summary>
    private void DrawHomeTextField(IntPtr hdc, Win32.RECT field, in HomePalette pal, IReadOnlyList<HomeTextItem> items)
    {
        _homeTextField = field;
        bool focused = _focusedActionIndex < 0;
        int border = focused ? Math.Max(2, S(2)) : 1;
        PaperDraw.RoundBox(hdc, field, pal.Surface, focused ? pal.Action : pal.Border, S(3), border);

        int padX = S(20), padTop = S(14), footH = S(26);
        var area = new Win32.RECT { left = field.left + padX, top = field.top + padTop, right = field.right - S(14), bottom = field.bottom - footH };
        _homeTextArea = area;
        var layout = BuildHomeTextLayout(hdc, area, items);
        var font = HomeFont(AppFonts.Title, 30, TypeRamp.Semibold);
        var skippedFont = HomeFont(AppFonts.Text, 24);
        var (ascent, descent) = PaperText.Metrics(hdc, font);
        int lineH = layout.LineHeight;
        int width = Math.Max(S(40), area.right - area.left);

        // La ligne du curseur, puis le défilement qui la garde visible.
        int cursorLine = HomeLineOf(layout, _homeEdit.Caret);
        int visible = HomeVisibleLines(layout);
        _homeTextFirstLine = Math.Clamp(_homeTextFirstLine, Math.Max(0, cursorLine - visible + 1), cursorLine);

        int selStart = _homeEdit.SelectionStart, selEnd = _homeEdit.SelectionEnd;
        uint selFill = pal.HighContrast ? pal.Action : PaperTheme.SelectionFill;
        uint selInk = pal.HighContrast ? pal.OnAction : pal.Ink;

        int saved = Win32.SaveDC(hdc);
        Win32.IntersectClipRect(hdc, field.left + border, field.top + border, field.right - border, area.bottom);
        if (items.Count == 0)
        {
            var placeholder = HomeFont(AppFonts.Text, 17);
            PaperText.DrawLine(hdc, HomeEllipsize(hdc, L.LessonsHome_TextPlaceholder, placeholder, width), placeholder,
                new Win32.RECT { left = area.left, top = area.top, right = area.right, bottom = area.top + lineH }, pal.Muted, TextAlign.Left);
        }
        int underline = Math.Max(2, S(3));
        int caretTop = 0;
        for (int l = _homeTextFirstLine; l < layout.Lines.Count && l < _homeTextFirstLine + visible; l++)
        {
            var (lineFrom, lineTo) = layout.Lines[l];
            int top = area.top + (l - _homeTextFirstLine) * lineH;
            int baseline = top + (lineH - (ascent + descent)) / 2 + ascent;
            int lx = area.left;
            for (int i = lineFrom; i < lineTo; i++)
            {
                var item = items[i];
                if (item.Kind == HomeTextKind.Break) continue;
                int start = layout.Starts[i];
                var cell = new Win32.RECT { left = lx, top = top, right = lx + layout.Widths[i], bottom = top + lineH };
                bool selected = start >= selStart && start + item.Element.Length <= selEnd && selEnd > selStart;
                if (selected) PaperDraw.Fill(hdc, cell, selFill);
                uint ink = selected ? selInk : pal.Ink;
                if (item.Kind == HomeTextKind.Skipped)
                {
                    var box = new Win32.RECT { left = lx + S(3), top = top + S(8), right = lx + layout.Widths[i] - S(3), bottom = top + lineH - S(8) };
                    PaperDraw.RoundBox(hdc, box, pal.Surface, pal.Muted, S(4), Math.Max(1, S(1)), Win32.PS_DASH);
                    PaperText.DrawLine(hdc, item.Element, skippedFont, box, pal.Ink, TextAlign.Center);
                }
                else
                {
                    PaperText.Draw(hdc, item.Element, font, lx, baseline, ink);
                    if (item.Kind == HomeTextKind.Gesture)
                        PaperDraw.Fill(hdc, lx, baseline + S(6), lx + layout.Widths[i], baseline + S(6) + underline, selected ? selInk : pal.Action);
                }
                lx += layout.Widths[i];
            }
            if (l == cursorLine) caretTop = top + (lineH - (ascent + descent)) / 2;
        }
        _systemCaretRect = null;
        if (focused && cursorLine >= _homeTextFirstLine && cursorLine < _homeTextFirstLine + visible)
        {
            int cxPos = area.left + HomeXOf(layout, cursorLine, _homeEdit.Caret);
            var caret = new Win32.RECT { left = cxPos + S(1), top = caretTop, right = cxPos + S(1) + Math.Max(2, S(2)), bottom = caretTop + ascent + descent };
            PaperDraw.Fill(hdc, caret, pal.Ink);
            _systemCaretRect = caret;
        }
        Win32.RestoreDC(hdc, saved);

        // Pied : les deux marques, puis le compte à droite (ou « Champ plein »).
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
        string count = _homeTextTruncated
            ? L.LessonsWin_FreeFull(LessonsHomeLogic.MaxTextLength)
            : L.LessonsHome_TextCount(items.Count(item => item.Kind != HomeTextKind.Break));
        var countFont = _homeTextTruncated ? HomeFont(AppFonts.Text, 12, TypeRamp.Semibold) : small;
        uint countInk = _homeTextTruncated ? (pal.HighContrast ? pal.Ink : PaperTheme.Error) : pal.Muted;
        int countW = PaperText.Width(hdc, count, countFont);
        // Le message « champ plein » passe avant la légende : il prend sa place s'il le faut.
        int countX = Math.Max(_homeTextTruncated ? field.left + padX : countLeft, field.right - S(14) - countW);
        if (_homeTextTruncated && countX < countLeft)
            PaperDraw.Fill(hdc, field.left + border, footTop, field.right - border, field.bottom - border, pal.Surface);
        if (_homeTextTruncated || field.right - S(14) - countW > countLeft)
            PaperText.DrawLine(hdc, count, countFont,
                new Win32.RECT { left = countX, top = footTop, right = field.right - S(14), bottom = footTop + footH }, countInk, TextAlign.Left);
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
            note = _homeEdit.Text.Trim().Length == 0 ? L.LessonsHome_TextEmpty : L.LessonsHome_TextNothingTypable;
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
