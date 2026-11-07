// Parcours de l'accueil : agencement de l'exercice et bilan avec les explications « Pourquoi ici »
// (remontée 13b d'Antoine, 07/11). Décision du 07/11 nuit : aucune explication pendant
// l'exercice, elles passent au bilan, « le moment où l'utilisateur peut retenir l'information ».
// Agencement choisi sur maquettes (QCM « Quelle option ? », option A « Frappe compacte »,
// operations/2026-10-14-sortie-v2-130/maquettes-13b/agencement.dossier-revue.md) : ligne à taper
// en 44 px, réserve du geste mesurée par exercice, clavier entier en pleine largeur ; au bilan,
// une explication par texte différent, en onglets, la touche de l'onglet actif encadrée.
namespace AZERTYGlobal;

internal sealed partial class LessonsWindow
{
    /// <summary>Onglet d'explication ouvert dans le bilan du parcours, et l'exercice auquel il
    /// se rapporte : un nouvel exercice repart du premier onglet.</summary>
    private int _summaryTab;
    private string? _summaryTabTarget;

    /// <summary>Hauteur réservée sous la ligne pour le geste, calculée au début du repeint.</summary>
    private int _notationReserve;

    /// <summary>
    /// Les explications d'un exercice du parcours, une par texte différent (« et » partagent la
    /// leur, í et ò aussi, les deux espaces insécables aussi), dans l'ordre où le texte les
    /// rencontre.
    /// </summary>
    internal static List<(string Characters, CharacterExplanation Explanation)> TutorialSummaryExplanations(string target)
    {
        var groups = new List<(List<char> Chars, CharacterExplanation Explanation)>();
        foreach (char c in target)
        {
            if (L.TutorialExplanation(c) is not { } explanation) continue;
            int found = groups.FindIndex(g => g.Explanation.Why == explanation.Why);
            if (found < 0) groups.Add((new List<char> { c }, explanation));
            else if (!groups[found].Chars.Contains(c)) groups[found].Chars.Add(c);
        }
        return groups.Select(g => (new string(g.Chars.ToArray()), g.Explanation)).ToList();
    }

    /// <summary>Nom d'un onglet : ses caractères séparés par une espace ; les espaces
    /// insécables, invisibles, par leur nom.</summary>
    internal static string TutorialSummaryTabLabel(string characters)
        => characters.All(c => c is ' ' or ' ')
            ? L.Explanation_NbspTab
            : string.Join(" ", characters.Select(c => c.ToString()));

    private List<(string Characters, CharacterExplanation Explanation)> CurrentSummaryExplanations()
    {
        string target = CurrentExercise.Content;
        if (_summaryTabTarget != target)
        {
            _summaryTabTarget = target;
            _summaryTab = 0;
        }
        var groups = TutorialSummaryExplanations(target);
        _summaryTab = Math.Clamp(_summaryTab, 0, Math.Max(0, groups.Count - 1));
        return groups;
    }

    /// <summary>Caractères de l'onglet ouvert au bilan du parcours, pour encadrer leurs touches.</summary>
    private string? SummaryTabCharacters()
    {
        if (!IsTutorialActive || !_showSummary) return null;
        var groups = CurrentSummaryExplanations();
        return groups.Count == 0 ? null : groups[_summaryTab].Characters;
    }

    private void SelectSummaryTab(int index)
    {
        _summaryTab = index;
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    /// <summary>Rejouer ou revenir en arrière rouvre le bilan suivant sur son premier onglet.</summary>
    private void ResetSummaryTab()
    {
        _summaryTab = 0;
        _summaryTabTarget = null;
    }

    /// <summary>
    /// Réserve du geste dans le parcours : le plus haut des gestes de la ligne, Verr. Maj.
    /// allumée ou non, pour que rien ne saute d'un caractère à l'autre. Un exercice sans Maj ni
    /// AltGr (É, « María… ») garde une ligne de cases ; un exercice qui empile Maj et AltGr
    /// (cadratin, espaces insécables) réserve ses deux pastilles.
    /// </summary>
    private int TutorialNotationHeight(IntPtr hdc)
    {
        var notation = CurrentNotation();
        if (notation is LessonNotation.Recipe or LessonNotation.NumberedKeyboard or LessonNotation.MiniKeyboard or LessonNotation.Frames)
            return NotationAreaHeight();
        var style = KeyNotationStyle.Lessons(PaperScale);
        int height = S(20); // l'avis de geste maîtrisé
        var line = _session.GetCurrentLineSnapshot();
        for (int i = 0; i < line.Count; i++)
        {
            char current = line[i].Expected;
            if (current == ' ') continue;
            char? following = i + 1 < line.Count ? line[i + 1].Expected : null;
            foreach (bool caps in new[] { false, true })
            {
                var gesture = Gesture.Of(current.ToString(), following, caps, CapsLockExercise);
                if (gesture.Count == 0) continue;
                var g = KeyNotation.ForLesson(gesture, following, caps, false);
                height = Math.Max(height, KeyNotation.Measure(hdc, g.Steps, style, g.Notes).Height);
            }
        }
        return Math.Min(height, NotationAreaHeight());
    }

    /// <summary>
    /// Le clavier du parcours sous la barre du parcours : aussi grand que la largeur et la
    /// hauteur libres le permettent (au plus 1,5 fois sa taille de base), centré, jamais coupé.
    /// L'ancien plancher à 0,75 le faisait déborder en bas de la fenêtre (capture d'Antoine, 07/11).
    /// </summary>
    internal static (float Unit, int Left, int Top) TutorialKeyboardLayout(Win32.RECT body, int top, float baseUnit)
    {
        var (baseW, baseH) = PaperKeyboard.Size(baseUnit);
        float fitW = (body.right - body.left) / (float)baseW;
        float fitH = Math.Max(0, body.bottom - top) / (float)baseH;
        float unit = baseUnit * Math.Min(1.5f, Math.Min(fitW, fitH));
        var (kbW, _) = PaperKeyboard.Size(unit);
        return (unit, body.left + Math.Max(0, (body.right - body.left - kbW) / 2), top);
    }

    // ── Bilan du parcours ────────────────────────────────────────────

    private const int SummaryPad = 28, SummaryTitleTop = 18, SummaryMessageTop = 58, SummaryTabsTop = 92, SummaryTabsHeight = 30;

    /// <summary>Hauteur du bilan du parcours : celle de la zone de frappe et de la consigne,
    /// davantage si la plus longue explication de l'exercice le demande.</summary>
    private int TutorialSummaryHeight(IntPtr hdc, int width, int minimum)
    {
        var groups = CurrentSummaryExplanations();
        int inner = width - 2 * S(SummaryPad);
        int top = groups.Count > 1 ? S(SummaryTabsTop + SummaryTabsHeight + 14) : S(SummaryTabsTop);
        int tallest = groups.Count == 0 ? 0 : groups.Max(g => DrawSummaryExplanation(hdc, g.Explanation, 0, 0, inner, draw: false));
        return Math.Max(minimum, top + tallest + S(22));
    }

    /// <summary>Bilan du parcours : « Exercice réussi », l'encouragement, Rejouer et Suivant
    /// sur la ligne du titre, puis les explications (onglets dès qu'il y en a deux).</summary>
    private void DrawTutorialSummary(IntPtr hdc, Win32.RECT box)
    {
        int x = box.left + S(SummaryPad), right = box.right - S(SummaryPad);
        int titleTop = box.top + S(SummaryTitleTop);

        string nextText = HomeIsComposedSession ? L.LessonsHome_Finish : L.LessonsWin_IconNext;
        int nextW = Math.Max(S(120), OutlineButtonWidth(hdc, nextText));
        var nextRect = new Win32.RECT { left = right - nextW, top = titleTop, right = right, bottom = titleTop + S(36) };
        DrawOutlineButton(hdc, nextRect, nextText, NextExercise, primary: true);
        int replayW = Math.Max(S(120), OutlineButtonWidth(hdc, L.LessonsWin_Replay));
        var replayRect = new Win32.RECT { left = nextRect.left - S(12) - replayW, top = titleTop, right = nextRect.left - S(12), bottom = nextRect.bottom };
        DrawOutlineButton(hdc, replayRect, L.LessonsWin_Replay, TutorialRestart);

        var titleFont = F(AppFonts.Title, 26, TypeRamp.Semibold);
        int titleRight = replayRect.left - S(16);
        PaperText.DrawLine(hdc, Ellipsize(hdc, L.LessonsWin_ExerciseSuccess, titleFont, titleRight - x), titleFont,
            new Win32.RECT { left = x, top = titleTop, right = titleRight, bottom = titleTop + S(36) }, CLR_TEXT, TextAlign.Left);
        var messageFont = F(AppFonts.Text, 16);
        PaperText.DrawLine(hdc, Ellipsize(hdc, L.LessonsWin_TutorialEncouragement, messageFont, titleRight - x), messageFont,
            new Win32.RECT { left = x, top = box.top + S(SummaryMessageTop), right = titleRight, bottom = box.top + S(SummaryMessageTop + 24) },
            CLR_MUTED, TextAlign.Left);

        var groups = CurrentSummaryExplanations();
        if (groups.Count == 0) return;
        int y = box.top + S(SummaryTabsTop);
        if (groups.Count > 1)
        {
            DrawSummaryTabs(hdc, groups, x, right, y);
            y += S(SummaryTabsHeight + 14);
        }
        else
        {
            PaperDraw.Fill(hdc, x, y - S(10), right, y - S(10) + 1, CLR_BORDER);
        }
        DrawSummaryExplanation(hdc, groups[_summaryTab].Explanation, x, y, right - x, draw: true);
    }

    /// <summary>Un onglet par explication, le caractère en Literata ; l'onglet ouvert souligné
    /// d'un filet d'action, sur un filet pleine largeur.</summary>
    private void DrawSummaryTabs(IntPtr hdc, List<(string Characters, CharacterExplanation Explanation)> groups,
        int left, int right, int top)
    {
        var glyphFont = F(AppFonts.Title, 17, TypeRamp.Semibold);
        var wordFont = F(AppFonts.Text, 15, TypeRamp.Semibold);
        int bottom = top + S(SummaryTabsHeight);
        PaperDraw.Fill(hdc, left, bottom - 1, right, bottom, CLR_BORDER);
        int x = left;
        for (int i = 0; i < groups.Count; i++)
        {
            string label = TutorialSummaryTabLabel(groups[i].Characters);
            bool words = label == L.Explanation_NbspTab;
            var font = words ? wordFont : glyphFont;
            int w = PaperText.Width(hdc, label, font) + 2 * S(12);
            var tab = new Win32.RECT { left = x, top = top, right = x + w, bottom = bottom };
            bool open = i == _summaryTab;
            if (IsHot(tab) && !open)
                PaperDraw.Fill(hdc, tab, PaperDraw.Blend(CLR_ACCENT, CLR_PANEL, 0.07f));
            PaperText.DrawLine(hdc, label, font, tab, open ? CLR_TEXT : CLR_MUTED, TextAlign.Center);
            if (open)
                PaperDraw.Fill(hdc, tab.left, bottom - Math.Max(2, S(3)), tab.right, bottom, CLR_ACCENT);
            int captured = i;
            AddClick(tab, () => SelectSummaryTab(captured), L.Explanation_TabName(words ? label : groups[i].Characters),
                LessonControlKind.ListItem, selected: open);
            x += w + S(4);
        }
    }

    /// <summary>« Pourquoi ici : » et la raison, puis « Pour retenir : » s'il y a un indice.
    /// <paramref name="draw"/> faux ne fait que mesurer.</summary>
    private int DrawSummaryExplanation(IntPtr hdc, CharacterExplanation explanation, int x, int y, int width, bool draw)
    {
        var body = F(AppFonts.Text, 15);
        var strong = F(AppFonts.Text, 15, TypeRamp.Semibold);
        int line = S(22);
        var why = new List<TextRun> { new(L.LessonsPaper_WhyHereLabel, true), new(explanation.Why) };
        var remember = new List<TextRun> { new(L.Explanation_RememberLabel, true), new(explanation.Remember ?? "") };
        var measure = new Win32.RECT { left = 0, top = 0, right = width, bottom = 0 };
        int hWhy = PaperText.DrawParagraph(hdc, why, body, strong, measure, CLR_TEXT, CLR_TEXT, line, draw: false);
        int hRem = explanation.HasRemember ? PaperText.DrawParagraph(hdc, remember, body, strong, measure, CLR_MUTED, CLR_TEXT, line, draw: false) : 0;
        int gap = explanation.HasRemember ? S(6) : 0;
        if (draw)
        {
            PaperText.DrawParagraph(hdc, why, body, strong, new Win32.RECT { left = x, top = y, right = x + width, bottom = y + hWhy },
                CLR_TEXT, CLR_TEXT, line);
            if (explanation.HasRemember)
                PaperText.DrawParagraph(hdc, remember, body, strong,
                    new Win32.RECT { left = x, top = y + hWhy + gap, right = x + width, bottom = y + hWhy + gap + hRem },
                    CLR_MUTED, CLR_TEXT, line);
        }
        return hWhy + gap + hRem;
    }

    /// <summary>Au bilan du parcours, les touches de l'onglet ouvert, encadrées comme un geste.</summary>
    private void GuideSummaryTab(KeyboardRenderState state)
    {
        if (SummaryTabCharacters() is not { } characters) return;
        foreach (char c in characters)
        {
            if (_hints.GetRecommendedMethod(c, null, false) is not { } method) continue;
            if (!string.IsNullOrEmpty(method.DeadKeyToken))
                state.LessonVisibleCharacters.Add(method.DeadKeyToken);
            LessonHintProvider.Guide(state, method, c.ToString(), null, false, LessonGuideOptions);
        }
    }
}
