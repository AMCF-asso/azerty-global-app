// Notation des touches sous le caractère courant (QCM d'Antoine du 11/10, maquettes
// lecons-notation2-*.html, journal DECISIONS-NOTATION.md dans Keyboard Layouts).
namespace AZERTYGlobal;

internal sealed partial class LessonsWindow
{
    // Réglages servis par lot/notation-config (ConfigManager) ; d'ici sa fusion, les défauts
    // décidés le 11/10 : Mots dans les deux états du clavier, geste maîtrisé masqué.
    private static LessonNotation NotationSetting(bool keyboardVisible) => LessonNotation.Words;
    private static bool HideMasteredSetting => true;

    /// <summary>La notation de l'état courant du clavier. Mini-clavier et vignettes montrent
    /// les Mots tant que MiniKeyboardRenderer (lot/notation-mini) n'est pas fusionné.</summary>
    private static LessonNotation CurrentNotation()
    {
        bool keyboard = ConfigManager.LessonKeyboardVisible;
        var notation = LessonNotations.Valid(NotationSetting(keyboard), keyboard);
        return notation is LessonNotation.MiniKeyboard or LessonNotation.Frames ? LessonNotation.Words : notation;
    }

    /// <summary>Hauteur réservée sous la ligne : fixe pour une notation, quel que soit le
    /// caractère, pour que la ligne ne saute pas d'un caractère à l'autre.</summary>
    private int NotationAreaHeight() => CurrentNotation() switch
    {
        LessonNotation.Recipe => S(40),
        LessonNotation.NumberedKeyboard => S(36),
        _ => S(128),
    };

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
                default:
                    var style = KeyNotationStyle.Lessons(PaperScale);
                    var (w, _) = KeyNotation.Measure(hdc, g.Steps, style, g.Notes);
                    int left = Math.Clamp(center - w / 2, minLeft, Math.Max(minLeft, maxRight - w));
                    // Les pavés montent au-dessus des cases : la ligne des cases reste à la même
                    // hauteur pour « e », « U » ou « Ǹ ».
                    int maxAbove = 2 * (style.TagHeight + style.Gap);
                    int drawTop = top + maxAbove - KeyNotation.HeightAboveCases(g.Steps, style);
                    KeyNotation.Draw(hdc, g.Steps, style, left, drawTop, CLR_ACCENT, g.Notes, g.Done, CLR_PANEL);
                    break;
            }
        }
        finally
        {
            Win32.RestoreDC(hdc, saved);
        }
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
