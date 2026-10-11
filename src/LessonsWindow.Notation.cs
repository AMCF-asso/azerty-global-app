// Notation des touches sous le caractère courant (QCM d'Antoine du 11/10, maquettes
// lecons-notation2-*.html, journal DECISIONS-NOTATION.md dans Keyboard Layouts).
namespace AZERTYGlobal;

internal sealed partial class LessonsWindow
{
    private static bool HideMasteredSetting => ConfigManager.LessonHideMastered;

    /// <summary>La notation de l'état courant du clavier, mémorisée par état (QCM du 11/10).</summary>
    private static LessonNotation CurrentNotation()
    {
        bool keyboard = ConfigManager.LessonKeyboardVisible;
        return LessonNotations.Valid(ConfigManager.LessonNotationFor(keyboard), keyboard);
    }

    /// <summary>Hauteur réservée sous la ligne : fixe pour une notation, quel que soit le
    /// caractère, pour que la ligne ne saute pas d'un caractère à l'autre.</summary>
    private int NotationAreaHeight() => CurrentNotation() switch
    {
        LessonNotation.Recipe => S(40),
        LessonNotation.NumberedKeyboard => S(36),
        LessonNotation.MiniKeyboard or LessonNotation.Frames => S(172),
        // Mots : deux pavés, la case et sa note (26 + 5) × 2 + 34 + 5 + 14 = 115 à 100 %.
        _ => S(118),
    };

    /// <summary>Unités de touche des mini-claviers (rapport de lot/notation-mini) : 24 sous le
    /// caractère, 17 par vignette, 13 quand trois vignettes ne tiennent pas.</summary>
    private const float MiniUnit = 24f, FrameUnit = 17f, FrameUnitSmall = 13f;

    private bool HintActive => _hintBackspace || _hintMethod != null;

    /// <summary>
    /// Geste maîtrisé masqué (décidé le 11/10) : case cochée, caractère suivi, boîte 4 ou plus
    /// et pas échu. L'Indice le remontre. La boîte ne bouge qu'en fin d'exercice : une faute
    /// sur ce caractère dans l'exercice en cours le fait réapparaître tout de suite, comme le
    /// promet KeyNotation_MasteredPanel.
    /// </summary>
    private bool CurrentGestureHidden(char current)
        => HideMasteredSetting && !HintActive && GestureMastery.IsTracked(current) &&
           LessonNotations.IsMastered(_progress.Gestures.GetValueOrDefault(current.ToString()), DateTimeOffset.UtcNow) &&
           !_session.Stats.GetCharacterCounts().Any(c => c.Expected == current && c.Errors > 0);

    /// <summary>
    /// Le geste à montrer pour <paramref name="current"/>, ou null quand il n'y a rien à lire :
    /// une espace, ou un temps unique sans Maj ni AltGr qui frappe la touche du caractère.
    /// « U » montre Maj + U.
    /// </summary>
    private (List<GestureStep> Steps, KeyNotationNote[] Notes, int Done)? CurrentGesture(char current, char? following)
    {
        if (current == ' ') return null;
        string glyph = current.ToString();
        var gesture = Gesture.Of(glyph);
        if (gesture.Count == 0) return null;
        if (gesture.Count == 1 && !gesture[0].Shift && !gesture[0].AltGr && Gesture.StrikesItsOwnKey(glyph, gesture))
            return null;
        return KeyNotation.ForLesson(gesture, following, _mapper.CapsLockActive, _mapper.ActiveDeadKey != null);
    }

    /// <summary>
    /// Sous la colonne du caractère courant, centré sur <paramref name="center"/> et borné à la
    /// partition : les Mots, la Recette, ou le renvoi au grand clavier numéroté.
    /// </summary>
    private void DrawCurrentNotation(IntPtr hdc, Win32.RECT part, int top, int center, char? following)
    {
        if (_session.NeedsBackspaceCorrection || _session.GetNextExpectedCharacter() is not char current) return;
        int minLeft = part.left + S(16), maxRight = part.right - S(16);
        int areaBottom = top + NotationAreaHeight();
        int saved = Win32.SaveDC(hdc);
        Win32.IntersectClipRect(hdc, part.left + 1, top, part.right - 1, areaBottom);
        try
        {
            if (CurrentGestureHidden(current))
            {
                DrawCenteredLine(hdc, L.KeyNotation_Mastered, F(AppFonts.Text, 13), top, S(20), center, minLeft, maxRight, PaperTheme.Pale);
                return;
            }
            if (CurrentGesture(current, following) is not { } g) return;
            switch (CurrentNotation())
            {
                case LessonNotation.Recipe:
                    DrawCenteredRuns(hdc, KeyNotation.Recipe(g.Steps, g.Notes), top + S(6), center, minLeft, maxRight);
                    break;
                case LessonNotation.NumberedKeyboard:
                    // Le clavier ne porte qu'un numéro par touche : Verr. Maj. éteint se dit en mots.
                    int numbered = NumberedSteps(g);
                    var runs = new List<TextRun>
                    {
                        new(L.KeyNotation_FollowKeyboard),
                        new(numbered == 1 ? L.KeyNotation_AllAtOnce : L.KeyNotation_StepsInOrder(numbered), true),
                    };
                    if (numbered < g.Steps.Count)
                    {
                        runs.Add(new TextRun(L.KeyNotation_RecipeThenLast));
                        runs.Add(new TextRun(g.Steps[^1].KeyLabel, true));
                        runs.Add(new TextRun(L.KeyNotation_RecipeTurnsOff));
                    }
                    DrawCenteredRuns(hdc, runs, top + S(6), center, minLeft, maxRight);
                    break;
                case LessonNotation.MiniKeyboard:
                {
                    float unit = MiniUnit * PaperScale;
                    var (mw, _) = MiniKeyboardRenderer.MeasureNumbered(hdc, unit, g.Steps, PaperScale);
                    int ml = Math.Clamp(center - mw / 2, minLeft, Math.Max(minLeft, maxRight - mw));
                    MiniKeyboardRenderer.DrawNumbered(hdc, ml, top, unit, g.Steps, PaperScale);
                    break;
                }
                case LessonNotation.Frames:
                {
                    float unit = FrameUnit * PaperScale;
                    var (fw, _) = MiniKeyboardRenderer.MeasureFrames(hdc, unit, g.Steps, PaperScale);
                    if (fw > maxRight - minLeft)
                    {
                        unit = FrameUnitSmall * PaperScale;
                        (fw, _) = MiniKeyboardRenderer.MeasureFrames(hdc, unit, g.Steps, PaperScale);
                    }
                    int fl = Math.Clamp(center - fw / 2, minLeft, Math.Max(minLeft, maxRight - fw));
                    MiniKeyboardRenderer.DrawFrames(hdc, fl, top, unit, g.Steps, PaperScale);
                    break;
                }
                default:
                    var style = KeyNotationStyle.Lessons(PaperScale);
                    var (w, _) = KeyNotation.Measure(hdc, g.Steps, style, g.Notes);
                    int left = Math.Clamp(center - w / 2, minLeft, Math.Max(minLeft, maxRight - w));
                    // Collée sous le caractère : une réserve pour deux pavés la détachait de
                    // la lettre de 60 px quand le geste n'en a pas (capture CI du 11/10).
                    KeyNotation.Draw(hdc, g.Steps, style, left, top, CLR_ACCENT, g.Notes, g.Done, CLR_PANEL);
                    break;
            }
        }
        finally
        {
            Win32.RestoreDC(hdc, saved);
        }
    }

    /// <summary>
    /// Réglages « Touches à taper » de la page Réglages des Leçons : une notation par état du
    /// clavier, puis le masquage des gestes maîtrisés. Mêmes clés et libellés que Paramètres
    /// (lot/notation-config). Rend l'ordonnée sous le dernier réglage.
    /// </summary>
    private int DrawNotationSettings(IntPtr hdc, int x, int y, int right)
    {
        var sectionFont = F(AppFonts.Text, 13, TypeRamp.Semibold);
        var subFont = F(AppFonts.Text, 13);
        PaperText.DrawLine(hdc, L.Notation_GroupTitle.ToUpperInvariant(), sectionFont,
            new Win32.RECT { left = x, top = y, right = right, bottom = y + S(20) }, CLR_MUTED, TextAlign.Left);
        y += S(24);
        foreach (bool keyboard in new[] { true, false })
        {
            string group = keyboard ? L.Notation_WithKeyboard : L.Notation_WithoutKeyboard;
            PaperText.DrawLine(hdc, group, subFont, new Win32.RECT { left = x, top = y, right = right, bottom = y + S(20) },
                CLR_MUTED, TextAlign.Left);
            y += S(22);
            var chosen = LessonNotations.Valid(ConfigManager.LessonNotationFor(keyboard), keyboard);
            foreach (var notation in keyboard ? LessonNotations.WithKeyboard : LessonNotations.WithoutKeyboard)
            {
                var row = new Win32.RECT { left = x - S(8), top = y, right = right, bottom = y + S(32) };
                bool forKeyboard = keyboard;
                var value = notation;
                DrawRadioRow(hdc, row, L.Notation_Name(notation), $"{group} : {L.Notation_Name(notation)}", notation == chosen,
                    () => SetNotation(forKeyboard, value));
                y += S(32);
            }
            y += S(10);
        }
        bool hide = ConfigManager.LessonHideMastered;
        DrawCheckRow(hdc, new Win32.RECT { left = x - S(8), top = y, right = right, bottom = y + S(34) }, L.Notation_HideMastered, hide,
            () => ToggleBool(hide, ConfigManager.SetLessonHideMastered));
        return y + S(36);
    }

    /// <summary>Un réglage d'affichage a changé dans Paramètres : la fenêtre se redessine.</summary>
    public void RefreshDisplay()
    {
        if (_hWnd != IntPtr.Zero)
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    private void SetNotation(bool keyboardVisible, LessonNotation notation)
    {
        ConfigManager.SetLessonNotationFor(keyboardVisible, notation);
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    /// <summary>Un choix parmi plusieurs : rond cerné, point plein quand il est choisi.
    /// Exposé en élément de liste sélectionné, comme les entrées de la barre latérale.</summary>
    private void DrawRadioRow(IntPtr hdc, Win32.RECT row, string label, string accessibleName, bool selected, Action choose)
    {
        if (IsHot(row))
            PaperDraw.Fill(hdc, row, PaperDraw.Blend(PaperTheme.Action, PaperTheme.Surface, 0.05f));
        int d = S(18);
        int top = row.top + (row.bottom - row.top - d) / 2;
        var ring = new Win32.RECT { left = row.left + S(8), top = top, right = row.left + S(8) + d, bottom = top + d };
        PaperDraw.RoundBox(hdc, ring, CLR_PANEL, selected ? CLR_ACCENT : PaperTheme.Pale, d / 2, KeyNotationStyle.Px(1.5f, PaperScale));
        if (selected)
        {
            int inset = d * 5 / 18;
            var dot = new Win32.RECT { left = ring.left + inset, top = ring.top + inset, right = ring.right - inset, bottom = ring.bottom - inset };
            PaperDraw.RoundBox(hdc, dot, CLR_ACCENT, CLR_ACCENT, (dot.right - dot.left) / 2);
        }
        var font = F(AppFonts.Text, 15);
        var labelRect = new Win32.RECT { left = ring.right + S(12), top = row.top, right = row.right - S(8), bottom = row.bottom };
        PaperText.DrawLine(hdc, Ellipsize(hdc, label, font, labelRect.right - labelRect.left), font, labelRect, CLR_TEXT, TextAlign.Left);
        AddClick(row, choose, accessibleName, LessonControlKind.ListItem, selected: selected);
    }

    /// <summary>Temps numérotés sur le grand clavier : tous, sauf Verr. Maj. éteint, qui
    /// retombe sur une touche déjà numérotée.</summary>
    private static int NumberedSteps((List<GestureStep> Steps, KeyNotationNote[] Notes, int Done) g)
        => g.Notes.Length > 0 && g.Notes[^1] == KeyNotationNote.TurnsOff ? g.Steps.Count - 1 : g.Steps.Count;

    /// <summary>
    /// Grand clavier numéroté : le numéro du temps sur chaque touche frappée, comme le texte
    /// sous le caractère. Un temps déjà fait (Verr. Maj. allumée, touche morte armée) perd le sien.
    /// </summary>
    private void AddNumberedBadges(Dictionary<uint, int> badges, char current)
    {
        var line = _session.GetCurrentLineSnapshot();
        int next = _session.CursorPosition + 1;
        char? following = next >= 0 && next < line.Count ? line[next].Expected : null;
        if (CurrentGesture(current, following) is not { } g) return;
        int numbered = NumberedSteps(g);
        if (numbered < 2) return;
        for (int i = g.Done; i < numbered; i++)
        {
            if (VirtualKeyboard.KeyCodeToScancode.TryGetValue(g.Steps[i].KeyCode, out var sc))
                badges[sc] = i + 1;
        }
    }

    private static void DrawCenteredLine(IntPtr hdc, string text, FontSpec font, int top, int height, int center,
        int minLeft, int maxRight, uint color)
    {
        int w = PaperText.Width(hdc, text, font);
        int left = Math.Clamp(center - w / 2, minLeft, Math.Max(minLeft, maxRight - w));
        PaperText.DrawLine(hdc, text, font, new Win32.RECT { left = left, top = top, right = left + w, bottom = top + height },
            color, TextAlign.Left);
    }

    /// <summary>Une ligne de texte à mots en gras, centrée sous le caractère.</summary>
    private void DrawCenteredRuns(IntPtr hdc, List<TextRun> runs, int top, int center, int minLeft, int maxRight)
    {
        var regular = F(AppFonts.Text, 15);
        var strong = F(AppFonts.Text, 15, TypeRamp.Semibold);
        int w = runs.Sum(run => PaperText.Width(hdc, run.Text, run.Strong ? strong : regular));
        w = Math.Min(w, maxRight - minLeft);
        int left = Math.Clamp(center - w / 2, minLeft, Math.Max(minLeft, maxRight - w));
        PaperText.DrawParagraph(hdc, runs, regular, strong,
            new Win32.RECT { left = left, top = top, right = left + w + 1, bottom = top + S(24) }, CLR_MUTED, CLR_TEXT, S(22));
    }
}
