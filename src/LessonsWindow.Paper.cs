// Rendu « papier » des Leçons (refonte 1.3.0, maquette Leçons F « Tablature », 07/10) :
// en-tête d'une ligne avec sélecteur de leçon, partition (Tablature) de la ligne à taper,
// clavier papier en carte et panneau du geste. La logique (saisie, focus, UIA, persistance)
// reste dans LessonsWindow.cs.
namespace AZERTYGlobal;

internal sealed partial class LessonsWindow
{
    // Sélecteur de leçon déroulé (remplace la barre latérale).
    private bool _pickerOpen;
    private Win32.RECT _pickerButtonRect;
    // Touche de la dernière faute, cerclée sur le clavier jusqu'à la correction.
    private uint _errorScancode;
    // Décalage horizontal de la partition, gardé d'un repeint à l'autre (ScrollToKeep).
    private int _tabScroll;

    private float PaperScale => _dpiScale * _windowScale;

    private FontSpec F(string family, int size, int weight = TypeRamp.Regular) =>
        FontSpec.Of(family, size, weight, PaperScale);

    private void ClosePicker()
    {
        if (!_pickerOpen) return;
        _pickerOpen = false;
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    private void TogglePicker()
    {
        _pickerOpen = !_pickerOpen;
        _focusedActionIndex = _pickerOpen ? 0 : -1;
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    /// <summary>Une case de la ligne en cours est-elle encore fausse (mode strict) ?</summary>
    private bool CurrentLineHasWrongCell()
    {
        foreach (var cell in _session.GetCurrentLineSnapshot())
            if (cell.State == LessonCharacterState.Wrong)
                return true;
        return false;
    }

    private static string Ellipsize(IntPtr hdc, string text, FontSpec font, int maxWidth)
    {
        if (maxWidth <= 0) return "";
        if (PaperText.Width(hdc, text, font) <= maxWidth) return text;
        for (int n = text.Length - 1; n > 0; n--)
        {
            string candidate = text[..n].TrimEnd() + "…";
            if (PaperText.Width(hdc, candidate, font) <= maxWidth)
                return candidate;
        }
        return "…";
    }

    // ── Contrôles papier ─────────────────────────────────────────────

    /// <summary>Lien souligné en couleur d'action (« Indice », « Recommencer »…), semi-gras
    /// quand il désigne l'état en cours. Rend son cadre cliquable.</summary>
    private Win32.RECT DrawLink(IntPtr hdc, int x, int midY, string text, bool active, Action action,
        string? tooltip = null, Action? doubleClick = null, bool measureOnly = false)
    {
        var font = F(AppFonts.Text, 15, active ? TypeRamp.Semibold : TypeRamp.Regular);
        int w = PaperText.Width(hdc, text, font);
        var rect = new Win32.RECT { left = x - S(4), top = midY - S(13), right = x + w + S(4), bottom = midY + S(13) };
        if (measureOnly) return rect;
        var (ascent, descent) = PaperText.Metrics(hdc, font);
        int baseline = midY - (ascent + descent) / 2 + ascent;
        PaperText.Draw(hdc, text, font, x, baseline, CLR_ACCENT);
        int thick = IsHot(rect) ? Math.Max(1, S(2)) : Math.Max(1, S(1));
        PaperDraw.Fill(hdc, x, baseline + S(3), x + w, baseline + S(3) + thick, CLR_ACCENT);
        AddClick(rect, action, text);
        if (doubleClick != null)
            AddDoubleClick(rect, doubleClick);
        if (!string.IsNullOrEmpty(tooltip))
            AddLinkTooltip(rect, tooltip);
        return rect;
    }

    /// <summary>Infobulle d'une cible : même rectangle que son clic, pour que la cible
    /// focalisée au clavier la montre aussi (K4, <see cref="FindHoverAreaFor"/>).</summary>
    private void AddLinkTooltip(Win32.RECT rect, string tooltip) => AddHover(rect, tooltip, preferAbove: false, compact: true);

    private int OutlineButtonWidth(IntPtr hdc, string text) =>
        PaperText.Width(hdc, text, F(AppFonts.Text, 15)) + S(28);

    /// <summary>Bouton à contour, de la famille du sélecteur (surface, filet, rayon 3).</summary>
    private void DrawOutlineButton(IntPtr hdc, Win32.RECT rect, string text, Action action, bool primary = false)
    {
        bool hot = IsHot(rect);
        uint fill = primary ? CLR_ACCENT : hot ? PaperDraw.Blend(PaperTheme.Action, PaperTheme.Surface, 0.07f) : CLR_PANEL;
        uint border = primary ? CLR_ACCENT : hot ? CLR_ACCENT : CLR_BORDER;
        PaperDraw.RoundBox(hdc, rect, fill, border, S(3));
        PaperText.DrawLine(hdc, text, F(AppFonts.Text, 15, primary ? TypeRamp.Semibold : TypeRamp.Regular), rect,
            primary ? PaperTheme.OnAction : CLR_TEXT, TextAlign.Center);
        AddClick(rect, action, text);
    }

    /// <summary>Case à cocher papier : carré de 18 px, coché en couleur d'action.</summary>
    private void DrawCheckRow(IntPtr hdc, Win32.RECT row, string label, bool enabled, Action toggle)
    {
        if (IsHot(row))
            PaperDraw.Fill(hdc, row, PaperDraw.Blend(PaperTheme.Action, PaperTheme.Surface, 0.05f));
        int box = S(18);
        int top = row.top + (row.bottom - row.top - box) / 2;
        var square = new Win32.RECT { left = row.left + S(8), top = top, right = row.left + S(8) + box, bottom = top + box };
        PaperDraw.RoundBox(hdc, square, enabled ? CLR_ACCENT : CLR_PANEL, enabled ? CLR_ACCENT : PaperTheme.Pale, S(3));
        if (enabled)
        {
            // Coche en deux traits.
            var pen = Win32.CreatePen(0, Math.Max(2, S(2)), PaperTheme.OnAction);
            var old = Win32.SelectObject(hdc, pen);
            Win32.MoveToEx(hdc, square.left + box * 4 / 18, square.top + box * 9 / 18, IntPtr.Zero);
            Win32.LineTo(hdc, square.left + box * 8 / 18, square.top + box * 13 / 18);
            Win32.LineTo(hdc, square.left + box * 14 / 18, square.top + box * 5 / 18);
            Win32.SelectObject(hdc, old);
            Win32.DeleteObject(pen);
        }
        var font = F(AppFonts.Text, 15);
        var labelRect = new Win32.RECT { left = square.right + S(12), top = row.top, right = row.right - S(8), bottom = row.bottom };
        PaperText.DrawLine(hdc, Ellipsize(hdc, label, font, labelRect.right - labelRect.left), font, labelRect, CLR_TEXT, TextAlign.Left);
        AddClick(row, toggle, label, LessonControlKind.CheckBox, toggled: enabled);
    }

    // ── En-tête ──────────────────────────────────────────────────────

    private void DrawPaperHeader(IntPtr hdc, Win32.RECT rc)
    {
        int mid = S(29);
        int x = S(24);
        var titleFont = F(AppFonts.Title, 24, TypeRamp.Semibold);
        x += PaperText.DrawLine(hdc, L.LessonsWin_Title, titleFont,
            new Win32.RECT { left = x, top = 0, right = rc.right, bottom = S(58) }, CLR_TEXT, TextAlign.Left);
        x += S(20);

        // Liens à droite, mesurés d'abord : le reste de l'en-tête prend la place qui reste.
        var links = new List<(string Text, bool Active, Action Action, string? Tooltip, Action? DoubleClick)>();
        if (_mode == WindowMode.Lessons && !_settingsOpen)
        {
            string hintTooltip = (!_hintBackspace && _hintMethod == null) ? L.LessonsWin_IconHint : FormatHintButtonText();
            links.Add((L.LessonsWin_IconHint, _hintButtonActive || ConfigManager.LessonAutoHintsEnabled, ShowHint, hintTooltip, ToggleAutoHints));
            links.Add((L.LessonsWin_IconRestart, false, () => StartCurrentSession(savePosition: true), null, null));
            links.Add((L.LessonsPaper_FreeTyping, false, () => SwitchMode(WindowMode.Free), null, null));
        }
        else
        {
            links.Add((L.LessonsWin_Title, !_settingsOpen && _mode == WindowMode.Lessons, () => SwitchMode(WindowMode.Lessons), null, null));
            links.Add((L.LessonsPaper_FreeTyping, !_settingsOpen && _mode == WindowMode.Free, () => SwitchMode(WindowMode.Free), null, null));
        }
        links.Add((L.LessonsWin_SettingsTab, _settingsOpen, ToggleSettings, null, null));

        int gap = S(22);
        int linksWidth = 0;
        foreach (var link in links)
        {
            var font = F(AppFonts.Text, 15, link.Active ? TypeRamp.Semibold : TypeRamp.Regular);
            linksWidth += PaperText.Width(hdc, link.Text, font);
        }
        linksWidth += gap * (links.Count - 1);
        int linksLeft = rc.right - S(24) - linksWidth;

        int available = linksLeft - S(24) - x;
        if (_mode == WindowMode.Lessons)
            DrawHeaderLessonPart(hdc, x, mid, available);
        else
            PaperText.DrawLine(hdc, Ellipsize(hdc, L.LessonsWin_FreeTitle, F(AppFonts.Text, 15, TypeRamp.Semibold), available),
                F(AppFonts.Text, 15, TypeRamp.Semibold), new Win32.RECT { left = x, top = 0, right = x + available, bottom = S(58) },
                CLR_TEXT, TextAlign.Left);

        int lx = linksLeft;
        foreach (var link in links)
        {
            var rect = DrawLink(hdc, lx, mid, link.Text, link.Active, link.Action, link.Tooltip, link.DoubleClick);
            lx = rect.right - S(4) + gap;
        }
    }

    /// <summary>Sélecteur « Module Leçon ▾ », jalons des exercices entre ‹ et ›, « Exercice n
    /// sur N ». Ce qui ne tient pas cède dans l'ordre : le texte, puis la largeur du sélecteur.</summary>
    private void DrawHeaderLessonPart(IntPtr hdc, int x, int mid, int available)
    {
        var moduleFont = F(AppFonts.Text, 15);
        var lessonFont = F(AppFonts.Text, 15, TypeRamp.Semibold);
        var arrowFont = F(AppFonts.Text, 11);
        var countFont = F(AppFonts.Text, 14);

        int exercises = CurrentLesson.Exercises.Count;
        int segment = S(18), segGap = S(3), chevron = S(22);
        int jalonsMax = S(220);
        if (exercises * (segment + segGap) > jalonsMax)
            segment = Math.Max(S(4), jalonsMax / Math.Max(1, exercises) - segGap);
        int jalonsW = exercises * (segment + segGap) - segGap;
        int stepperW = chevron + S(6) + jalonsW + S(6) + chevron;
        string countText = L.LessonsPaper_ExerciseOf(_exerciseIndex + 1, exercises);
        int countW = PaperText.Width(hdc, countText, countFont);

        string module = CurrentModule.Title;
        string lesson = CurrentLesson.Title;
        int pickerNatural = S(12) + PaperText.Width(hdc, module, moduleFont) + S(8) + PaperText.Width(hdc, lesson, lessonFont)
                            + S(12) + PaperText.Width(hdc, "▾", arrowFont) + S(12);
        int pickerW = Math.Min(pickerNatural, S(340));
        bool showCount = pickerW + S(20) + stepperW + S(10) + countW <= available;
        int rest = available - S(20) - stepperW - (showCount ? S(10) + countW : 0);
        pickerW = Math.Max(S(120), Math.Min(pickerW, rest));

        // Sélecteur.
        var picker = new Win32.RECT { left = x, top = S(12), right = x + pickerW, bottom = S(46) };
        _pickerButtonRect = picker;
        bool hot = IsHot(picker) || _pickerOpen;
        PaperDraw.RoundBox(hdc, picker, CLR_PANEL, hot ? CLR_ACCENT : CLR_BORDER, S(3));
        int arrowW = PaperText.Width(hdc, "▾", arrowFont);
        int textRight = picker.right - S(12) - arrowW - S(8);
        int tx = picker.left + S(12);
        int moduleW = PaperText.Width(hdc, module, moduleFont);
        int lessonW = PaperText.Width(hdc, lesson, lessonFont);
        int room = textRight - tx;
        if (moduleW + S(8) + lessonW > room)
        {
            // La leçon passe d'abord : le module cède jusqu'à un tiers.
            int moduleRoom = Math.Max(Math.Min(moduleW, room / 3), room - S(8) - lessonW);
            module = Ellipsize(hdc, module, moduleFont, moduleRoom);
            moduleW = PaperText.Width(hdc, module, moduleFont);
            lesson = Ellipsize(hdc, lesson, lessonFont, room - moduleW - S(8));
        }
        tx += PaperText.DrawLine(hdc, module, moduleFont, new Win32.RECT { left = tx, top = picker.top, right = textRight, bottom = picker.bottom },
            CLR_MUTED, TextAlign.Left) + S(8);
        PaperText.DrawLine(hdc, lesson, lessonFont, new Win32.RECT { left = tx, top = picker.top, right = textRight, bottom = picker.bottom },
            CLR_TEXT, TextAlign.Left);
        PaperText.DrawLine(hdc, "▾", arrowFont, new Win32.RECT { left = picker.right - S(12) - arrowW, top = picker.top, right = picker.right - S(12), bottom = picker.bottom },
            CLR_MUTED, TextAlign.Left);
        AddClick(picker, TogglePicker, $"{CurrentModule.Title} {CurrentLesson.Title}");
        AddLinkTooltip(picker, L.LessonsPaper_ChooseLesson);

        // Jalons entre les chevrons.
        int sx = picker.right + S(20);
        var chevronFont = F(AppFonts.Text, 20);
        var prev = new Win32.RECT { left = sx, top = mid - S(13), right = sx + chevron, bottom = mid + S(13) };
        PaperText.DrawLine(hdc, "‹", chevronFont, prev, IsHot(prev) ? CLR_ACCENT : CLR_MUTED, TextAlign.Center);
        AddClick(prev, PreviousExercise, L.LessonsWin_IconPrevious);
        AddLinkTooltip(prev, L.LessonsWin_IconPrevious);
        int jx = prev.right + S(6);
        for (int i = 0; i < exercises; i++)
        {
            var ex = CurrentLesson.Exercises[i];
            uint color = i == _exerciseIndex ? CLR_ACCENT : _progress.IsCompleted(ex) ? CLR_TEXT : CLR_BORDER;
            PaperDraw.Fill(hdc, jx, mid - S(1), jx + segment, mid - S(1) + Math.Max(1, S(3)), color);
            jx += segment + segGap;
        }
        jx = prev.right + S(6) + jalonsW + S(6);
        var next = new Win32.RECT { left = jx, top = mid - S(13), right = jx + chevron, bottom = mid + S(13) };
        PaperText.DrawLine(hdc, "›", chevronFont, next, IsHot(next) ? CLR_ACCENT : CLR_MUTED, TextAlign.Center);
        AddClick(next, NextExercise, L.LessonsWin_IconNext);
        AddLinkTooltip(next, L.LessonsWin_IconNext);

        if (showCount)
            PaperText.DrawLine(hdc, countText, countFont,
                new Win32.RECT { left = next.right + S(10), top = 0, right = next.right + S(10) + countW, bottom = S(58) }, CLR_MUTED, TextAlign.Left);
    }

    // ── Sélecteur déroulé ────────────────────────────────────────────

    /// <summary>
    /// Liste des modules et, sous le module affiché, de ses leçons (l'ancienne barre
    /// latérale). Dessinée en dernier : tant qu'elle est ouverte, seuls le sélecteur et ses
    /// lignes répondent aux clics et à Tab.
    /// </summary>
    private void DrawPaperPicker(IntPtr hdc, Win32.RECT rc)
    {
        _clickActions.Clear();
        _controlViews.Clear();
        _doubleClickActions.Clear();
        _hoverAreas.Clear();
        _keyboardHover = null;
        AddClick(_pickerButtonRect, TogglePicker, $"{CurrentModule.Title} {CurrentLesson.Title}");

        var moduleFont = F(AppFonts.Text, 15, TypeRamp.Semibold);
        var lessonFont = F(AppFonts.Text, 15);
        var countFont = F(AppFonts.Text, 13);
        int width = Math.Max(S(420), _pickerButtonRect.right - _pickerButtonRect.left);
        int left = _pickerButtonRect.left;
        int right = Math.Min(rc.right - S(16), left + width);
        int top = _pickerButtonRect.bottom + S(4);
        int pad = S(8);

        // Hauteur totale, pour le cadre.
        int moduleH = S(34), lessonH = S(30), headH = S(30);
        int height = pad + headH + _catalog.Modules.Count * moduleH + CurrentModule.Lessons.Count * lessonH + pad;
        int bottom = Math.Min(rc.bottom - S(12), top + height);
        var panel = new Win32.RECT { left = left, top = top, right = right, bottom = bottom };
        // Ombre légère, puis le panneau.
        PaperDraw.Fill(hdc, panel.left + S(2), panel.top + S(3), panel.right + S(2), panel.bottom + S(3),
            PaperDraw.Blend(PaperTheme.Ink, PaperTheme.Paper, 0.08f));
        PaperDraw.RoundBox(hdc, panel, CLR_PANEL, CLR_BORDER, S(3));

        int saved = Win32.SaveDC(hdc);
        Win32.IntersectClipRect(hdc, panel.left + 1, panel.top + 1, panel.right - 1, panel.bottom - 1);
        int y = panel.top + pad;
        int done = _progress.CountCompleted(_catalog);
        PaperText.DrawLine(hdc, L.LessonsWin_ExerciseCount(done, _catalog.TotalExerciseCount), countFont,
            new Win32.RECT { left = panel.left + S(16), top = y, right = panel.right - S(16), bottom = y + headH }, CLR_MUTED, TextAlign.Left);
        y += headH;

        for (int i = 0; i < _catalog.Modules.Count && y < panel.bottom - pad; i++)
        {
            var module = _catalog.Modules[i];
            int completed = module.Lessons.SelectMany(l => l.Exercises).Count(_progress.IsCompleted);
            int total = module.Lessons.SelectMany(l => l.Exercises).Count();
            var item = new Win32.RECT { left = panel.left + 1, top = y, right = panel.right - 1, bottom = y + moduleH };
            bool selected = i == _moduleIndex;
            DrawPickerRow(hdc, item, module.Title, $"{completed}/{total}", moduleFont, countFont, CLR_TEXT, selected: false, S(16));
            int captured = i;
            AddClick(item, () => SelectModule(captured), $"{module.Title} {completed}/{total}", LessonControlKind.ListItem, selected: selected);
            y += moduleH;
            if (!selected) continue;

            for (int j = 0; j < module.Lessons.Count && y < panel.bottom - pad; j++)
            {
                var lesson = module.Lessons[j];
                int lessonDone = lesson.Exercises.Count(_progress.IsCompleted);
                var row = new Win32.RECT { left = panel.left + 1, top = y, right = panel.right - 1, bottom = y + lessonH };
                bool current = j == _lessonIndex;
                DrawPickerRow(hdc, row, lesson.Title, $"{lessonDone}/{lesson.Exercises.Count}", lessonFont, countFont,
                    current ? CLR_ACCENT : CLR_TEXT, current, S(32));
                int capturedLesson = j;
                AddClick(row, () => SelectLesson(capturedLesson), $"{lesson.Title} {lessonDone}/{lesson.Exercises.Count}",
                    LessonControlKind.ListItem, selected: current);
                y += lessonH;
            }
        }
        Win32.RestoreDC(hdc, saved);
    }

    private void DrawPickerRow(IntPtr hdc, Win32.RECT row, string title, string count, FontSpec font, FontSpec countFont,
        uint color, bool selected, int indent)
    {
        if (IsHot(row))
            PaperDraw.Fill(hdc, row, PaperDraw.Blend(PaperTheme.Action, PaperTheme.Surface, 0.07f));
        if (selected)
            PaperDraw.Fill(hdc, row.left, row.top + S(4), row.left + Math.Max(2, S(2)), row.bottom - S(4), CLR_ACCENT);
        int countW = PaperText.Width(hdc, count, countFont);
        var countRect = new Win32.RECT { left = row.right - S(16) - countW, top = row.top, right = row.right - S(16), bottom = row.bottom };
        PaperText.DrawLine(hdc, count, countFont, countRect, CLR_MUTED, TextAlign.Right);
        var titleRect = new Win32.RECT { left = row.left + indent, top = row.top, right = countRect.left - S(12), bottom = row.bottom };
        PaperText.DrawLine(hdc, Ellipsize(hdc, title, font, titleRect.right - titleRect.left), font, titleRect, color, TextAlign.Left);
    }

    // ── Leçon : partition, consigne, clavier, geste ──────────────────

    private void DrawPaperLesson(IntPtr hdc, Win32.RECT rc, Win32.RECT body)
    {
        var style = TablatureStyle.LessonsText(PaperScale);
        int partH = 1 + S(22) + style.Height + S(10) + NotationAreaHeight() + S(14) + 1;
        var part = new Win32.RECT { left = body.left, top = body.top, right = body.right, bottom = body.top + partH };

        int textTop = part.bottom + S(14);
        if (_showSummary && _lastCompletedStats != null)
        {
            DrawPaperSummary(hdc, part, _lastCompletedStats);
        }
        else
        {
            DrawPartition(hdc, part, style);
            DrawLessonTextLines(hdc, part, textTop);
        }
        int belowText = textTop + S(44);

        if (ConfigManager.LessonKeyboardVisible)
        {
            float unit = 52f * PaperScale;
            var (kbW, kbH) = PaperKeyboard.Size(unit);
            int free = body.bottom - belowText;
            int kbTop = belowText + Math.Max(S(4), (free - kbH) / 2);
            var placement = PaperKeyboard.PlaceAt(body.left, kbTop, unit);
            DrawPaperKeyboard(hdc, placement, lessons: true);
            if (!_showSummary)
            {
                var panel = new Win32.RECT { left = body.left + kbW + S(36), top = kbTop, right = body.right, bottom = kbTop + kbH };
                DrawGesturePanel(hdc, panel, centered: true);
            }
        }
        else if (!_showSummary)
        {
            var panel = new Win32.RECT { left = body.left, top = belowText + S(16), right = Math.Min(body.right, body.left + S(520)), bottom = body.bottom };
            DrawGesturePanel(hdc, panel, centered: false);
        }
    }

    /// <summary>Consigne de l'exercice et numéro de ligne, puis le mode de frappe.</summary>
    private void DrawLessonTextLines(IntPtr hdc, Win32.RECT part, int top)
    {
        var font = F(AppFonts.Text, 15);
        var small = F(AppFonts.Text, 13);
        string line = L.LessonsWin_LineLabel(_session.LineIndex + 1, _session.TotalLines);
        int lineW = PaperText.Width(hdc, line, font);
        var lineRect = new Win32.RECT { left = part.right - lineW, top = top, right = part.right, bottom = top + S(22) };
        PaperText.DrawLine(hdc, line, font, lineRect, CLR_MUTED, TextAlign.Right);
        string instruction = FormatLessonInstruction(CurrentExercise.Instruction);
        var instrRect = new Win32.RECT { left = part.left, top = top, right = lineRect.left - S(24), bottom = top + S(22) };
        PaperText.DrawLine(hdc, Ellipsize(hdc, instruction, font, instrRect.right - instrRect.left), font, instrRect, CLR_MUTED, TextAlign.Left);
        var statusRect = new Win32.RECT { left = part.left, top = top + S(22), right = part.right, bottom = top + S(42) };
        PaperText.DrawLine(hdc, Ellipsize(hdc, BuildTypingStatus(), small, statusRect.right - statusRect.left), small, statusRect,
            PaperTheme.Pale, TextAlign.Left);
    }

    /// <summary>
    /// La ligne à taper : une colonne par caractère attendu, sans portée (QCM du 11/10) ; le
    /// geste du seul caractère courant se lit dessous (<see cref="DrawCurrentNotation"/>). Le
    /// socle n'a pas d'état « faute » : la colonne fautive est tracée sans glyphe, puis le
    /// caractère tapé y est repeint en rouge, souligné.
    /// </summary>
    private void DrawPartition(IntPtr hdc, Win32.RECT part, TablatureStyle style)
    {
        PaperDraw.RoundBox(hdc, part, CLR_PANEL, CLR_BORDER, 0);
        var expected = _session.GetCurrentLineSnapshot();
        var typed = _session.GetTypedLineSnapshot();
        bool strict = CurrentExercise.TypingMode == LessonTypingMode.Strict;
        int cursor = Math.Clamp(_session.CursorPosition, 0, Math.Max(expected.Count, typed.Count));

        var measured = new List<TabColumn>(expected.Count);
        var drawn = new List<TabColumn>(expected.Count);
        var glyphColumn = new int[expected.Count];
        var wrong = new List<(int Column, char Actual)>();
        for (int i = 0; i < expected.Count; i++)
        {
            var cell = expected[i];
            var state = cell.State switch
            {
                LessonCharacterState.Correct => TabState.Done,
                LessonCharacterState.Current => TabState.Current,
                LessonCharacterState.Wrong => strict && i == cursor ? TabState.Current : TabState.Done,
                _ => TabState.Upcoming,
            };
            string glyph = cell.Expected.ToString();
            glyphColumn[i] = measured.Count;
            measured.Add(new TabColumn(glyph, false, false, null, state));
            bool isWrong = cell.State == LessonCharacterState.Wrong && TypedCharacterAt(typed, i) is char;
            drawn.Add(new TabColumn(isWrong ? null : glyph, false, false, null, state));
            if (isWrong)
                wrong.Add((glyphColumn[i], typed[i].Actual));
        }
        // Mode souple : ce qui dépasse la fin du modèle reste écrit, en faute.
        for (int i = expected.Count; i < typed.Count; i++)
        {
            measured.Add(new TabColumn(typed[i].Actual == ' ' ? "·" : typed[i].Actual.ToString(), false, false, null, TabState.Done));
            drawn.Add(new TabColumn(null, false, false, null, TabState.Done));
            wrong.Add((measured.Count - 1, typed[i].Actual));
        }

        int[] widths = TablatureRenderer.MeasureColumns(hdc, measured, style);
        var grid = new Win32.RECT { left = part.left + 1, top = part.top + 1 + S(22), right = part.right - 1, bottom = part.top + 1 + S(22) + style.Height };
        int columnsLeft = grid.left + style.LabelWidth;
        int viewport = Math.Max(1, grid.right - columnsLeft - S(16));
        int currentColumn = cursor < expected.Count ? glyphColumn[cursor] : measured.Count - 1;
        _tabScroll = TablatureRenderer.ScrollToKeep(widths, currentColumn, viewport, _tabScroll);
        TablatureRenderer.Draw(hdc, drawn, widths, style, grid, _tabScroll);

        // Abscisse de chaque colonne.
        var lefts = new int[widths.Length + 1];
        lefts[0] = columnsLeft - _tabScroll;
        for (int c = 0; c < widths.Length; c++)
            lefts[c + 1] = lefts[c] + widths[c];

        // Le geste du caractère courant, sous sa colonne.
        if (cursor < expected.Count)
        {
            int c = glyphColumn[cursor];
            int center = (lefts[c] + lefts[c + 1]) / 2;
            char? following = cursor + 1 < expected.Count ? expected[cursor + 1].Expected : null;
            DrawCurrentNotation(hdc, part, grid.bottom + S(10), center, following);
        }

        // Fautes repeintes : le caractère tapé en rouge, souligné.
        int saved = Win32.SaveDC(hdc);
        Win32.IntersectClipRect(hdc, columnsLeft, grid.top, grid.right, grid.top + style.GlyphRowHeight);
        foreach (var (column, actual) in wrong)
        {
            var cellRect = new Win32.RECT { left = lefts[column], top = grid.top, right = lefts[column + 1], bottom = grid.top + style.GlyphRowHeight };
            string shown = actual == ' ' || actual == ' ' || actual == ' ' ? "·" : actual.ToString();
            PaperText.DrawLine(hdc, shown, style.GlyphFont, cellRect, CLR_BAD, TextAlign.Center);
            var (ul, ur) = UnderlineSpan(cellRect.left, cellRect.right, S(2), S(6));
            int uy = cellRect.bottom - S(6);
            PaperDraw.Fill(hdc, ul, uy, ur, uy + Math.Max(1, S(2)), CLR_BAD);
        }
        Win32.RestoreDC(hdc, saved);

        // Caret système masqué, devant la colonne du caractère à taper.
        bool active = !_session.IsLineComplete && !_session.IsExerciseComplete;
        int caretColumn = cursor < expected.Count ? glyphColumn[cursor] : widths.Length;
        int caretX = lefts[Math.Clamp(caretColumn, 0, widths.Length)];
        int caretTop = grid.top + S(14);
        if (active && caretX >= columnsLeft && caretX <= grid.right)
            _systemCaretRect = new Win32.RECT { left = caretX, top = caretTop, right = caretX + Math.Max(1, S(2)), bottom = caretTop + S(50) };

        // UI Automation : la ligne attendue et la colonne de chacun de ses caractères.
        var text = new char[expected.Count];
        var cells = new Win32.RECT[expected.Count];
        for (int i = 0; i < expected.Count; i++)
        {
            text[i] = expected[i].Expected;
            int c = glyphColumn[i];
            cells[i] = new Win32.RECT { left = lefts[c], top = grid.top, right = lefts[c + 1], bottom = grid.top + style.GlyphRowHeight };
        }
        _surfaceView = new LessonSurfaceView(L.LessonsWin_AutomationLineName,
            $"{CurrentLesson.Title}. {FormatLessonInstruction(CurrentExercise.Instruction)}", new string(text),
            Math.Clamp(_session.CursorPosition, 0, text.Length), part, cells, _systemCaretRect);

        if (_hasFocus && _focusedActionIndex < 0)
            DrawFocusOutline(hdc, part, CLR_ACCENT);
    }

    /// <summary>Nom du caractère en casse de phrase (l'index les donne en capitales). Un mot
    /// d'une lettre reste en capitale dans le nom d'une majuscule.</summary>
    internal static string CharacterDisplayName(char ch)
    {
        if (!CharacterIndex.Shared.Names.TryGetValue(ch.ToString(), out var names))
            return L.LessonsPaper_CharacterFallback(ch.ToString());
        string raw = L.IsEnglish ? names.En : names.Fr;
        if (string.IsNullOrWhiteSpace(raw))
            return L.LessonsPaper_CharacterFallback(ch.ToString());
        bool capital = raw.Contains("MAJUSCULE", StringComparison.OrdinalIgnoreCase) ||
                       raw.Contains("CAPITAL", StringComparison.OrdinalIgnoreCase);
        var words = raw.Split(' ');
        for (int w = 0; w < words.Length; w++)
        {
            string word = words[w];
            if (word.Length == 0) continue;
            if (word.Length == 1 && capital && char.IsLetter(word[0]))
                continue;
            string lower = word.ToLowerInvariant();
            words[w] = w == 0 ? char.ToUpperInvariant(lower[0]) + lower[1..] : lower;
        }
        return string.Join(' ', words);
    }

    /// <summary>Panneau du geste : nom du caractère à taper, puis la phrase du geste (ou l'avis
    /// de geste masqué) ; « Frappez Retour arrière. » quand une faute attend.</summary>
    private void DrawGesturePanel(IntPtr hdc, Win32.RECT panel, bool centered)
    {
        if (panel.right - panel.left < S(120)) return;
        string title;
        var runs = new List<TextRun>();
        if (_session.NeedsBackspaceCorrection)
        {
            title = L.LessonsPaper_CorrectionTitle;
            runs.Add(new TextRun(L.Gesture_StrikeCapital));
            runs.Add(new TextRun(L.LessonsPaper_BackspaceKey, true));
            runs.Add(new TextRun("."));
        }
        else
        {
            char? next = _session.GetNextExpectedCharacter();
            if (!next.HasValue) return;
            title = CharacterDisplayName(next.Value);
            if (CurrentGestureHidden(next.Value))
            {
                runs.Add(new TextRun(L.KeyNotation_MasteredPanel));
            }
            else if (CurrentNotation() != LessonNotation.Recipe)
            {
                // La Recette se lit déjà sous le caractère : pas deux fois la même phrase.
                var steps = Gesture.Of(next.Value.ToString(), FollowingExpectedCharacter(), _mapper.CapsLockActive);
                if (steps.Count > 0)
                    runs.AddRange(Gesture.Sentence(steps));
            }
        }

        var regular = F(AppFonts.Text, 15);
        var strong = F(AppFonts.Text, 15, TypeRamp.Semibold);
        int width = Math.Min(panel.right - panel.left, S(300));
        // Le paragraphe ne coupe pas les mots : un nom trop long (« Guillemet-apostrophe »)
        // prend un corps plus petit plutôt que de déborder.
        var titleFont = F(AppFonts.Title, 26, TypeRamp.Semibold);
        foreach (int size in new[] { 22, 19, 16 })
        {
            int widest = title.Split(' ').Max(word => PaperText.Width(hdc, word, titleFont));
            if (widest <= width) break;
            titleFont = F(AppFonts.Title, size, TypeRamp.Semibold);
        }
        var measureRect = new Win32.RECT { left = panel.left, top = 0, right = panel.left + width, bottom = 0 };
        var titleRuns = new List<TextRun> { new(title) };
        int titleH = PaperText.DrawParagraph(hdc, titleRuns, titleFont, titleFont, measureRect, CLR_TEXT, CLR_TEXT, S(30), draw: false);
        int bodyH = runs.Count == 0 ? 0 : PaperText.DrawParagraph(hdc, runs, regular, strong, measureRect, CLR_MUTED, CLR_TEXT, S(22), draw: false);
        int total = titleH + (bodyH > 0 ? S(14) + bodyH : 0);
        int top = centered ? panel.top + Math.Max(0, (panel.bottom - panel.top - total) / 2) : panel.top;

        PaperText.DrawParagraph(hdc, titleRuns, titleFont, titleFont,
            new Win32.RECT { left = panel.left, top = top, right = panel.left + width, bottom = top + titleH }, CLR_TEXT, CLR_TEXT, S(30));
        if (bodyH > 0)
        {
            int by = top + titleH + S(14);
            PaperText.DrawParagraph(hdc, runs, regular, strong,
                new Win32.RECT { left = panel.left, top = by, right = panel.left + width, bottom = by + bodyH }, CLR_MUTED, CLR_TEXT, S(22));
        }
    }

    /// <summary>
    /// Clavier papier en carte : touches cibles du prochain caractère toujours montrées ;
    /// l'Indice y ajoute le caractère et l'ordre des temps. Retient de quoi construire
    /// l'infobulle d'une touche au survol (L-06).
    /// </summary>
    private void DrawPaperKeyboard(IntPtr hdc, KeyboardPlacement placement, bool lessons)
    {
        var state = new KeyboardRenderState
        {
            Shift = _mapper.ShiftDown,
            AltGr = _mapper.AltGrDown,
            Ctrl = _mapper.CtrlDown,
            Alt = _mapper.AltDown,
            CapsLock = _mapper.CapsLockActive,
            ActiveDeadKey = _mapper.ActiveDeadKey,
            PressedScancode = _pressedScancode,
            ErrorScancode = lessons ? _errorScancode : 0,
            HintCharacter = _hintCharacter?.ToString(),
            ShowInvisibleMarkers = ConfigManager.LessonInvisibleMarkersVisible
        };
        var profile = KeyboardRenderProfile.Full;
        var stepBadges = new Dictionary<uint, int>();
        if (lessons)
        {
            if (CurrentLesson.Characters.Count > 0)
                profile = KeyboardRenderProfile.Lesson;
            _hints.AddRequiredCharacters(CurrentLesson.Characters, state.LessonVisibleCharacters);
            _hints.AddRequiredCharacters(CurrentExercise.Content, state.LessonVisibleCharacters);
            if (_hintCharacter.HasValue)
            {
                state.LessonVisibleCharacters.Add(_hintCharacter.Value.ToString());
                _hints.AddRequiredDeadKey(_hintCharacter.Value, state.LessonVisibleCharacters);
            }

            if (!_showSummary)
            {
                if (_hintBackspace || _hintMethod != null)
                {
                    ApplyHintHighlight(state);
                    if (_hintMethod is { } method && method.IsDeadKey && method.DkActivationKey.Length > 0 &&
                        VirtualKeyboard.KeyCodeToScancode.TryGetValue(method.DkActivationKey, out var first) &&
                        VirtualKeyboard.KeyCodeToScancode.TryGetValue(method.Key, out var second))
                    {
                        stepBadges[first] = 1;
                        stepBadges[second] = 2;
                    }
                }
                else if (_session.NeedsBackspaceCorrection)
                {
                    state.HighlightedScancodes.Add(0x0E);
                }
                else if (_session.GetNextExpectedCharacter() is char next && !CurrentGestureHidden(next) &&
                         _hints.GetRecommendedMethod(next, FollowingExpectedCharacter(), _mapper.CapsLockActive) is { } method)
                {
                    if (!string.IsNullOrEmpty(method.DeadKeyToken))
                        state.LessonVisibleCharacters.Add(method.DeadKeyToken);
                    LessonHintProvider.Guide(state, method, next.ToString(), _mapper.ActiveDeadKey,
                        _mapper.CapsLockActive, GuideOptions.Lessons);
                    if (CurrentNotation() == LessonNotation.NumberedKeyboard)
                        AddNumberedBadges(stepBadges, next);
                }
            }
        }

        var paper = new PaperKeyboardState
        {
            Shift = state.Shift,
            AltGr = state.AltGr,
            CapsLock = state.CapsLock,
            Ctrl = state.Ctrl,
            Alt = state.Alt,
            ActiveDeadKey = state.ActiveDeadKey,
            PressedScancode = state.PressedScancode,
            ErrorScancode = state.ErrorScancode,
            Profile = profile,
            HintCharacter = state.HintCharacter,
        };
        foreach (var sc in state.HighlightedScancodes) paper.TargetScancodes.Add(sc);
        foreach (var id in state.HighlightedContextIds) paper.TargetContextLabels.Add(id);
        foreach (var label in state.HighlightedLabels) paper.TargetContextLabels.Add(label);
        foreach (var ch in state.LessonVisibleCharacters) paper.LessonVisibleCharacters.Add(ch);
        foreach (var (sc, step) in stepBadges) paper.StepBadges[sc] = step;

        PaperKeyboard.Draw(hdc, placement, _layout, PaperKeyboardMode.Map, paper);
        _keyboardHover = (placement, profile, state);
    }

    // ── Récapitulatif ────────────────────────────────────────────────

    private void DrawPaperSummary(IntPtr hdc, Win32.RECT box, LessonAttemptStats stats)
    {
        PaperDraw.RoundBox(hdc, box, CLR_PANEL, CLR_BORDER, 0);
        int pad = S(28);
        int x = box.left + pad;
        int right = box.right - pad;
        var titleFont = F(AppFonts.Title, 26, TypeRamp.Semibold);

        // Suivant, en bas à droite ; le partage du défi, lien sur la ligne du titre.
        string nextText = L.LessonsWin_IconNext;
        int nextW = Math.Max(S(120), OutlineButtonWidth(hdc, nextText));
        var nextRect = new Win32.RECT { left = right - nextW, top = box.bottom - S(22) - S(36), right = right, bottom = box.bottom - S(22) };
        DrawOutlineButton(hdc, nextRect, nextText, NextExercise, primary: true);

        int titleRight = right;
        if (_challenge.ShareText != null)
        {
            string share = _challenge.ShareCopied ? L.Challenge_ShareButtonCopied : L.Challenge_ShareButton;
            var measure = DrawLink(hdc, 0, 0, share, _challenge.ShareCopied, OnCopyChallengeResult, measureOnly: true);
            int w = measure.right - measure.left - S(8);
            var link = DrawLink(hdc, right - w, box.top + S(22) + S(18), share, _challenge.ShareCopied, OnCopyChallengeResult, L.Challenge_ShareTooltip);
            titleRight = link.left - S(16);
        }
        PaperText.DrawLine(hdc, Ellipsize(hdc, L.LessonsWin_ExerciseSuccess, titleFont, titleRight - x), titleFont,
            new Win32.RECT { left = x, top = box.top + S(22), right = titleRight, bottom = box.top + S(58) }, CLR_TEXT, TextAlign.Left);

        if (!ConfigManager.LessonSummaryVisible)
            return;

        int metricTop = box.top + S(84);
        int columns = _summaryBestWpm.HasValue ? 4 : 3;
        int gap = S(32);
        int metricW = Math.Max(S(90), Math.Min(S(190), (right - x - gap * (columns - 1)) / columns));
        DrawPaperMetric(hdc, x, metricTop, metricW, L.LessonsWin_MetricSpeed, FormatWpm(stats.Wpm), CLR_TEXT);
        DrawPaperMetric(hdc, x + (metricW + gap), metricTop, metricW, L.LessonsWin_MetricAccuracy,
            FormatNullable(stats.AccuracyPercent, " %"), GetAccuracyColor(stats.AccuracyPercent));
        DrawPaperMetric(hdc, x + (metricW + gap) * 2, metricTop, metricW, L.LessonsWin_MetricErrors,
            stats.ErrorCount.ToString(), GetErrorColor(stats.ErrorCount));
        if (_summaryBestWpm.HasValue)
            DrawPaperMetric(hdc, x + (metricW + gap) * 3, metricTop, metricW, L.LessonsWin_MetricBest,
                FormatWpm(_summaryBestWpm), _summaryIsNewBest ? CLR_ACCENT : CLR_TEXT);

        var hard = stats.GetHardestCharacters(3);
        int seconds = (int)Math.Round(stats.ElapsedSeconds);
        string detail = hard.Count == 0
            ? L.LessonsWin_DetailNoHardChar(seconds)
            : L.LessonsWin_DetailHardChars(seconds, string.Join("   ", hard.Select(ch => FormatVisibleCharacter(ch.ToString()))));
        var small = F(AppFonts.Text, 14);
        var detailRect = new Win32.RECT { left = x, top = nextRect.top, right = nextRect.left - S(24), bottom = nextRect.bottom };
        PaperText.DrawLine(hdc, Ellipsize(hdc, detail, small, detailRect.right - detailRect.left), small, detailRect, CLR_MUTED, TextAlign.Left);
    }

    private void DrawPaperMetric(IntPtr hdc, int x, int y, int width, string label, string value, uint color)
    {
        var labelFont = F(AppFonts.Text, 13);
        var valueFont = F(AppFonts.Title, 26, TypeRamp.Semibold);
        PaperText.DrawLine(hdc, Ellipsize(hdc, label, labelFont, width), labelFont,
            new Win32.RECT { left = x, top = y, right = x + width, bottom = y + S(18) }, CLR_MUTED, TextAlign.Left);
        PaperText.DrawLine(hdc, Ellipsize(hdc, value, valueFont, width), valueFont,
            new Win32.RECT { left = x, top = y + S(20), right = x + width, bottom = y + S(56) }, color, TextAlign.Left);
    }

    // ── Paramètres ───────────────────────────────────────────────────

    private void DrawPaperSettings(IntPtr hdc, Win32.RECT body)
    {
        var box = new Win32.RECT { left = body.left, top = body.top, right = body.right, bottom = body.bottom };
        PaperDraw.RoundBox(hdc, box, CLR_PANEL, CLR_BORDER, 0);
        int pad = S(28);
        int x = box.left + pad;
        int right = Math.Min(box.right - pad, x + S(560));
        int y = box.top + S(22);
        var titleFont = F(AppFonts.Title, 24, TypeRamp.Semibold);
        var sectionFont = F(AppFonts.Text, 13, TypeRamp.Semibold);

        PaperText.DrawLine(hdc, L.LessonsWin_SettingsTab, titleFont, new Win32.RECT { left = x, top = y, right = right, bottom = y + S(34) }, CLR_TEXT, TextAlign.Left);
        y += S(50);

        void Section(string name)
        {
            PaperText.DrawLine(hdc, name.ToUpperInvariant(), sectionFont, new Win32.RECT { left = x, top = y, right = right, bottom = y + S(20) }, CLR_MUTED, TextAlign.Left);
            y += S(24);
        }
        void Row(string label, bool value, Action toggle)
        {
            DrawCheckRow(hdc, new Win32.RECT { left = x - S(8), top = y, right = right, bottom = y + S(34) }, label, value, toggle);
            y += S(36);
        }

        Section(L.LessonsWin_Title);
        Row(L.LessonsWin_ToggleAutoHints, ConfigManager.LessonAutoHintsEnabled, ToggleAutoHints);
        Row(L.LessonsWin_ToggleSummary, ConfigManager.LessonSummaryVisible,
            () => ToggleBool(ConfigManager.LessonSummaryVisible, ConfigManager.SetLessonSummaryVisible));
        y += S(14);
        Section(L.LessonsWin_SectionDisplay);
        Row(L.LessonsWin_ToggleFreeStats, ConfigManager.LessonFreeStatsVisible,
            () => ToggleBool(ConfigManager.LessonFreeStatsVisible, ConfigManager.SetLessonFreeStatsVisible));
        Row(L.LessonsWin_ToggleKeyboard, ConfigManager.LessonKeyboardVisible,
            () => ToggleBool(ConfigManager.LessonKeyboardVisible, ConfigManager.SetLessonKeyboardVisible));
        Row(L.LessonsWin_ToggleInvisibleMarkers, ConfigManager.LessonInvisibleMarkersVisible,
            () => ToggleBool(ConfigManager.LessonInvisibleMarkersVisible, ConfigManager.SetLessonInvisibleMarkersVisible));
        y += S(14);
        Section(L.LessonsWin_SectionActions);
        int w1 = OutlineButtonWidth(hdc, L.LessonsWin_BtnResetFreeStats);
        var reset1 = new Win32.RECT { left = x, top = y, right = x + w1, bottom = y + S(34) };
        DrawOutlineButton(hdc, reset1, L.LessonsWin_BtnResetFreeStats, ResetFreeStats);
        int w2 = OutlineButtonWidth(hdc, L.LessonsWin_BtnResetProgress);
        var reset2 = new Win32.RECT { left = reset1.right + S(12), top = y, right = reset1.right + S(12) + w2, bottom = y + S(34) };
        DrawOutlineButton(hdc, reset2, L.LessonsWin_BtnResetProgress, ResetProgress);

        // Touches à taper : en seconde colonne quand elle tient, sinon sous les actions.
        int column = right + S(48);
        if (box.right - pad - column >= S(300))
            DrawNotationSettings(hdc, column, box.top + S(22) + S(50), box.right - pad);
        else
            DrawNotationSettings(hdc, x, reset1.bottom + S(28), right);
    }

    // ── Écriture libre ───────────────────────────────────────────────

    private void DrawPaperFree(IntPtr hdc, Win32.RECT rc, Win32.RECT body)
    {
        var preview = new Win32.RECT { left = body.left, top = body.top, right = body.right, bottom = body.top + S(150) };
        _freePreviewRect = preview;
        PaperDraw.RoundBox(hdc, preview, CLR_PANEL, CLR_BORDER, 0);
        DrawFreePreviewText(hdc, preview);
        if (_hasFocus && _focusedActionIndex < 0)
            DrawFocusOutline(hdc, preview, CLR_ACCENT);
        // Géométrie des caractères non relevée ici : l'aperçu entier en tient lieu.
        _surfaceView = new LessonSurfaceView(L.LessonsWin_FreeTitle, L.LessonsWin_FreeDescription, _freePreview,
            Math.Clamp(_freeCursorIndex, 0, _freePreview.Length), preview, Array.Empty<Win32.RECT>(), _systemCaretRect);

        var font = F(AppFonts.Text, 15);
        int y = preview.bottom + S(14);
        PaperText.DrawLine(hdc, Ellipsize(hdc, L.LessonsWin_FreeDescription, font, body.right - body.left), font,
            new Win32.RECT { left = body.left, top = y, right = body.right, bottom = y + S(22) }, CLR_MUTED, TextAlign.Left);
        y += S(26);
        if (ConfigManager.LessonFreeStatsVisible)
        {
            var linkRect = DrawLink(hdc, 0, 0, L.LessonsWin_BtnResetStats, false, ResetFreeStats, measureOnly: true);
            int linkW = linkRect.right - linkRect.left - S(8);
            int linkX = body.right - linkW;
            DrawLink(hdc, linkX, y + S(11), L.LessonsWin_BtnResetStats, false, ResetFreeStats);
            var strong = F(AppFonts.Text, 15, TypeRamp.Semibold);
            PaperText.DrawLine(hdc, Ellipsize(hdc, BuildFreeStatsText(), strong, linkX - S(24) - body.left), strong,
                new Win32.RECT { left = body.left, top = y, right = linkX - S(24), bottom = y + S(22) }, CLR_TEXT, TextAlign.Left);
            y += S(26);
        }

        if (ConfigManager.LessonKeyboardVisible)
        {
            float unit = 52f * PaperScale;
            var (_, kbH) = PaperKeyboard.Size(unit);
            int top = y + S(16);
            int kbTop = top + Math.Max(0, (body.bottom - top - kbH) / 2);
            DrawPaperKeyboard(hdc, PaperKeyboard.PlaceAt(body.left, kbTop, unit), lessons: false);
        }
    }
}
