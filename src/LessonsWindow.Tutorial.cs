// Parcours de l'accueil dans les Leçons (audit 1.3.0, B2 voie (b), décision d'Antoine du
// 20/10) : « Activer et essayer » ouvre Leçons › Initiation, déjà en papier, à la place de
// l'ancienne fenêtre d'exercices sombre (LearningModule, retirée). Le parcours en garde
// l'écran des usages, les exercices choisis (TutorialSteps.BuildRoute), l'écran final et le
// retour vers l'accueil ; il reprend au premier exercice non réussi (A1).
namespace AZERTYGlobal;

internal sealed partial class LessonsWindow
{
    private enum TutorialPage { None, Usages, Final }

    // Ordre de l'écran des usages, du plus courant au plus particulier.
    private static readonly string[] TutorialUsageIds =
    {
        TutorialSteps.UsageDocuments, TutorialSteps.UsageMessages, TutorialSteps.UsageCode,
        TutorialSteps.UsageLanguages, TutorialSteps.UsageAfrica,
    };

    // « Un tour d'Afrique » de l'écran final : une leçon de l'édition spéciale du catalogue.
    private const string TutorialAfricaModuleId = "vingt-millions";
    private const string TutorialAfricaLessonId = "course-20-millions-afrique";

    private TutorialPage _tutorialPage;
    // Exercices du parcours, null hors parcours ou avant l'écran des usages validé.
    private TutorialSteps.RouteStep[]? _tutorialRoute;
    private int _tutorialRoutePos;
    private readonly bool[] _tutorialUsages = new bool[TutorialUsageIds.Length];
    // Frappe du parcours hors des statistiques d'usage, comme dans l'ancienne fenêtre : les
    // exercices de l'accueil franchissaient à eux seuls le seuil de la demande d'avis
    // (UsageStats.EnrichedCharsReviewThreshold, constat du 2026-09-21).
    private bool _tutorialExcludesTyping;
    // Au prochain repeint, le focus clavier va au premier contrôle de la page (après ceux de
    // l'en-tête, dessinés avant elle) : l'écran se règle au clavier dès l'ouverture.
    private bool _tutorialFocusPending;

    /// <summary>Fin du parcours ouvert par l'accueil (fenêtre fermée, « Passer pour
    /// l'instant » ou « Terminer ») : vrai si l'écran final a été atteint.</summary>
    public Action<bool>? TutorialClosed;

    internal bool IsTutorialActive => _tutorialPage != TutorialPage.None || _tutorialRoute != null;

    /// <summary>Écran des usages ou écran final à la place de l'exercice.</summary>
    private bool TutorialPageShown =>
        _tutorialPage != TutorialPage.None && _mode == WindowMode.Lessons && !_settingsOpen && !HomeShown;

    /// <summary>Exercice de l'Initiation qui se tape en Verr. Maj. (« ÇA GÈLE… ») : une capitale
    /// s'y enseigne Verr. Maj. + lettre, jamais Maj + lettre (remontée d'Antoine, 07/11).
    /// Vrai dans le parcours comme dans les Leçons : la consigne est celle de l'exercice.</summary>
    private bool CapsLockExercise =>
        _mode == WindowMode.Lessons && _homeComposed == null &&
        CurrentModule.Id == LessonCatalogLoader.InitiationModuleId &&
        _exerciseIndex >= 0 && _exerciseIndex < TutorialSteps.All.Length &&
        TutorialSteps.All[_exerciseIndex].KeepCapsLock;

    private GuideOptions LessonGuideOptions => CapsLockExercise ? GuideOptions.CapsLockExercise : GuideOptions.Lessons;

    /// <summary>L'exercice affiché est l'étape courante du parcours.</summary>
    private bool TutorialOnRouteStep =>
        _tutorialRoute != null && _tutorialPage == TutorialPage.None && _homeComposed == null &&
        _tutorialRoutePos >= 0 && _tutorialRoutePos < _tutorialRoute.Length &&
        CurrentModule.Id == LessonCatalogLoader.InitiationModuleId &&
        _exerciseIndex == _tutorialRoute[_tutorialRoutePos].Index;

    /// <summary>Le parcours que l'en-tête compte à la place de la leçon : ses exercices (les
    /// quatre communs et les bonus choisis), pas les huit de l'Initiation, comme la ligne du
    /// parcours sous la consigne. Null hors parcours, ou sur un exercice qui n'en est pas
    /// l'étape.</summary>
    private TutorialSteps.RouteStep[]? TutorialRouteForHeader =>
        TutorialOnRouteStep || (_tutorialPage == TutorialPage.Final && _tutorialRoute != null) ? _tutorialRoute : null;

    /// <summary>Ouvre le parcours de l'accueil sur l'écran des usages, cochés comme au dernier
    /// passage.</summary>
    public void ShowTutorial()
    {
        BeginTutorial();
        Show();
    }

    /// <summary>État du parcours, sans afficher la fenêtre (les tests ne prennent pas le
    /// premier plan du poste).</summary>
    internal void BeginTutorial()
    {
        var saved = new HashSet<string>(ConfigManager.LearningUsages, StringComparer.Ordinal);
        for (int i = 0; i < TutorialUsageIds.Length; i++)
            _tutorialUsages[i] = saved.Contains(TutorialUsageIds[i]);
        _tutorialRoute = null;
        _tutorialRoutePos = -1;
        _tutorialPage = TutorialPage.Usages;
        SetTutorialCaption(active: true);
        if (!_tutorialExcludesTyping)
        {
            UsageStats.BeginExcludedTyping();
            _tutorialExcludesTyping = true;
        }

        _mode = WindowMode.Lessons;
        _settingsOpen = false;
        _pickerOpen = false;
        SelectExercise(LessonCatalogLoader.InitiationModuleId, LessonCatalogLoader.InitiationLessonId, 0, savePosition: false);
        _homeOpenOnExercise = true;
        _tutorialFocusPending = true;
    }

    private void ToggleTutorialUsage(int index)
    {
        _tutorialUsages[index] = !_tutorialUsages[index];
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    private void StartTutorialRoute()
    {
        var chosen = TutorialUsageIds.Where((_, i) => _tutorialUsages[i]).ToArray();
        ConfigManager.SetLearningUsages(chosen);
        _tutorialRoute = TutorialSteps.BuildRoute(chosen);
        _tutorialRoutePos = -1;
        AdvanceTutorialRoute();
    }

    /// <summary>Exercice suivant du parcours, en sautant ceux déjà réussis (A1 : reprise) ;
    /// après le dernier, l'écran final.</summary>
    private void AdvanceTutorialRoute()
    {
        if (_tutorialRoute == null) return;
        var done = ConfigManager.LearningCompletedExercises;
        do _tutorialRoutePos++;
        while (_tutorialRoutePos < _tutorialRoute.Length && done.Contains(_tutorialRoute[_tutorialRoutePos].Index));

        if (_tutorialRoutePos >= _tutorialRoute.Length)
        {
            _tutorialPage = TutorialPage.Final;
            _showSummary = false;
            _tutorialFocusPending = true;
        }
        else
        {
            _tutorialPage = TutorialPage.None;
            SelectExercise(LessonCatalogLoader.InitiationModuleId, LessonCatalogLoader.InitiationLessonId,
                _tutorialRoute[_tutorialRoutePos].Index, savePosition: true);
            _focusedActionIndex = -1;
            UpdateAutoHintIfEnabled();
        }
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    /// <summary>« Suivant » du récapitulatif : dans le parcours, son étape suivante.</summary>
    private bool TutorialNext()
    {
        if (!TutorialOnRouteStep) return false;
        AdvanceTutorialRoute();
        return true;
    }

    /// <summary>Clôt le parcours : la frappe compte de nouveau, l'accueil est prévenu.</summary>
    private void EndTutorial(bool completed)
    {
        if (!IsTutorialActive && !_tutorialExcludesTyping) return;
        bool wasActive = IsTutorialActive;
        _tutorialPage = TutorialPage.None;
        _tutorialRoute = null;
        _tutorialRoutePos = -1;
        SetTutorialCaption(active: false);
        if (_tutorialExcludesTyping)
        {
            UsageStats.EndExcludedTyping();
            _tutorialExcludesTyping = false;
        }
        if (!wasActive) return;
        try { TutorialClosed?.Invoke(completed); }
        catch (Exception ex) { ConfigManager.Log("LessonsWindow.TutorialClosed", ex); }
    }

    /// <summary>Barre de titre du parcours, « AZERTY Global — Prise en main », comme son en-tête
    /// (Antoine, QCM du 07/11) ; « — Leçons » hors parcours.</summary>
    private void SetTutorialCaption(bool active)
    {
        if (_hWnd != IntPtr.Zero)
            Win32.SetWindowTextW(_hWnd, active ? L.Learning_WindowTitle : L.LessonsWin_WindowTitle);
    }

    private void FinishTutorial()
    {
        EndTutorial(completed: true);
        Hide();
    }

    private void LeaveTutorialForNow()
    {
        EndTutorial(completed: false);
        Hide();
    }

    // ── Dessin ───────────────────────────────────────────────────────

    /// <summary>
    /// En-tête du parcours (remontée n° 6 d'Antoine, 07/11 : « Il y a des boutons de partout. Si
    /// on clique sur un bouton, ça nous fait quitter l'onboarding »). Ni onglets, ni Réglages, ni
    /// sélecteur de leçon, ni chevrons : le titre « Prise en main », les jalons du parcours sans
    /// clic, et « Indice » sur un exercice. On n'en sort que par « Passer pour l'instant »,
    /// Échap ou la fermeture, qui rendent la main à l'accueil, ou par l'écran final.
    /// </summary>
    private void DrawTutorialHeader(IntPtr hdc, Win32.RECT rc)
    {
        int mid = S(29);
        int x = S(24);
        var titleFont = F(AppFonts.Title, 24, TypeRamp.Semibold);
        x += PaperText.DrawLine(hdc, L.Learning_HeaderTitle, titleFont,
            new Win32.RECT { left = x, top = 0, right = rc.right, bottom = S(58) }, CLR_TEXT, TextAlign.Left);
        x += S(24);

        int linksLeft = rc.right - S(24);
        if (TutorialOnRouteStep)
        {
            string hintTooltip = (!_hintBackspace && _hintMethod == null) ? L.LessonsWin_IconHint : FormatHintButtonText();
            bool active = _hintButtonActive || ConfigManager.LessonAutoHintsEnabled;
            var measure = DrawLink(hdc, 0, 0, L.LessonsWin_IconHint, active, ShowHint, measureOnly: true);
            linksLeft -= measure.right - measure.left - S(8);
            DrawLink(hdc, linksLeft, mid, L.LessonsWin_IconHint, active, ShowHint, hintTooltip, ToggleAutoHints);
        }

        // Jalons : un trait par exercice du parcours, en couleur d'action pour le courant.
        var route = TutorialRouteForHeader;
        if (route == null) return;
        int segment = S(18), segGap = S(3);
        int room = Math.Min(S(220), linksLeft - S(24) - x);
        if (room <= 0) return;
        if (route.Length * (segment + segGap) > room)
            segment = Math.Max(S(4), room / Math.Max(1, route.Length) - segGap);
        for (int i = 0; i < route.Length; i++)
        {
            var ex = CurrentLesson.Exercises[route[i].Index];
            uint color = i == _tutorialRoutePos ? CLR_ACCENT : _progress.IsCompleted(ex) ? CLR_TEXT : CLR_BORDER;
            PaperDraw.Fill(hdc, x, mid - S(1), x + segment, mid - S(1) + Math.Max(1, S(3)), color);
            x += segment + segGap;
        }
    }

    /// <summary>Écran des usages ou écran final, dans le cadre de l'exercice. Faux s'il n'y a
    /// rien à dessiner : l'exercice se dessine alors comme d'habitude.</summary>
    private bool DrawTutorialPage(IntPtr hdc, Win32.RECT body)
    {
        if (!TutorialPageShown) return false;
        int firstControl = _clickActions.Count;
        int focusAt = firstControl;
        PaperDraw.RoundBox(hdc, body, CLR_PANEL, CLR_BORDER, 0);
        int pad = S(28);
        int x = body.left + pad;
        int right = body.right - pad;
        var titleFont = F(AppFonts.Title, 26, TypeRamp.Semibold);
        var textFont = F(AppFonts.Text, 15);
        var titleRect = new Win32.RECT { left = x, top = body.top + S(22), right = right, bottom = body.top + S(58) };

        if (_tutorialPage == TutorialPage.Usages)
        {
            // Deux colonnes (remontée n° 7, maquette « fusion B + A » validée le 07/11) : à gauche
            // les cases, chacune avec ce que son exercice fait taper ; à droite le parcours
            // qu'elles composent, redessiné à chaque case. Trop étroit : les cases seules.
            bool twoColumns = right - x >= S(700);
            int leftRight = twoColumns ? x + (right - x - S(40)) / 2 : right;
            PaperText.DrawLine(hdc, Ellipsize(hdc, L.Learning_UsagesQuestion, titleFont, leftRight - x), titleFont, titleRect,
                CLR_TEXT, TextAlign.Left);
            var help = new Win32.RECT { left = x, top = titleRect.bottom + S(4), right = leftRight, bottom = titleRect.bottom + S(26) };
            PaperText.DrawLine(hdc, Ellipsize(hdc, L.Learning_UsagesHelp, textFont, leftRight - x), textFont, help,
                CLR_MUTED, TextAlign.Left);

            int rowRight = x - S(8) + Math.Min(leftRight - x + S(8), S(568));
            int top = help.bottom + S(16);
            for (int i = 0; i < TutorialUsageIds.Length; i++)
            {
                int index = i;
                var row = new Win32.RECT { left = x - S(8), top = top, right = rowRight, bottom = top + S(66) };
                DrawTutorialUsageRow(hdc, row, TutorialUsageLabel(i), TutorialUsageHelp(TutorialUsageIds[i]),
                    _tutorialUsages[i], () => ToggleTutorialUsage(index));
                top = row.bottom + S(7);
            }

            int buttonTop = body.bottom - S(22) - S(36);
            if (twoColumns)
                DrawTutorialPlan(hdc, leftRight + S(40), right, titleRect.top, buttonTop - S(16));
            DrawTutorialPrimaryButton(hdc, body, right, L.Learning_BtnContinue, StartTutorialRoute);
        }
        else
        {
            // Deux colonnes dans une fenêtre assez large : à gauche le bravo et ce qui a été
            // tapé, à droite les cartes. Sinon une colonne, les cartes sous la liste si elles tiennent.
            bool twoColumns = right - x >= S(700);
            int leftRight = twoColumns ? x + (right - x) * 11 / 20 : right;
            int buttonsTop = body.bottom - S(22) - S(36);
            PaperText.DrawLine(hdc, Ellipsize(hdc, L.Learning_FinalTitle, titleFont, leftRight - x), titleFont, titleRect,
                CLR_TEXT, TextAlign.Left);
            var sub = new Win32.RECT { left = x, top = titleRect.bottom + S(8), right = leftRight, bottom = titleRect.bottom + S(32) };
            PaperText.DrawLine(hdc, Ellipsize(hdc, L.Learning_FinalSubtitle, textFont, leftRight - x), textFont, sub,
                CLR_MUTED, TextAlign.Left);
            int listBottom = DrawTutorialRecap(hdc, body, x, leftRight, sub.bottom);
            if (twoColumns)
                DrawTutorialFinalCards(hdc, leftRight + S(32), right, titleRect.top, buttonsTop - S(16));
            else
                DrawTutorialFinalCards(hdc, x, right, listBottom + S(16), buttonsTop - S(16));

            // « Terminer » à droite, « Continuer les leçons » (le bouton principal, qui a le
            // focus à l'ouverture) à sa gauche.
            int finishW = Math.Max(S(140), OutlineButtonWidth(hdc, L.Learning_BtnFinish));
            int continueW = Math.Max(S(140), OutlineButtonWidth(hdc, L.Learning_BtnContinueLessons));
            var finish = new Win32.RECT { left = right - finishW, top = buttonsTop, right = right, bottom = buttonsTop + S(36) };
            var more = new Win32.RECT { left = finish.left - S(12) - continueW, top = buttonsTop, right = finish.left - S(12), bottom = buttonsTop + S(36) };
            focusAt = _clickActions.Count;
            DrawOutlineButton(hdc, more, L.Learning_BtnContinueLessons, () => ContinueAfterTutorial(HomeShowToday), primary: true);
            DrawOutlineButton(hdc, finish, L.Learning_BtnFinish, FinishTutorial);
        }
        if (_tutorialFocusPending)
        {
            _focusedActionIndex = focusAt;
            _tutorialFocusPending = false;
        }
        return true;
    }

    /// <summary>« Continuer les leçons » et les liens des cartes : le parcours est terminé (l'accueil
    /// est prévenu), la fenêtre reste ouverte et ouvre <paramref name="open"/>.</summary>
    private void ContinueAfterTutorial(Action open)
    {
        EndTutorial(completed: true);
        open();
        Win32.SetForegroundWindow(_hWnd);
    }

    /// <summary>La leçon « Un tour d'Afrique » du catalogue, null si le catalogue ne la porte pas.</summary>
    private LessonLesson? TutorialAfricaLesson()
    {
        foreach (var module in _catalog.Modules)
            if (StringComparer.Ordinal.Equals(module.Id, TutorialAfricaModuleId))
                foreach (var lesson in module.Lessons)
                    if (StringComparer.Ordinal.Equals(lesson.Id, TutorialAfricaLessonId))
                        return lesson;
        return null;
    }

    private void OpenTutorialAfrica()
    {
        if (!SelectExercise(TutorialAfricaModuleId, TutorialAfricaLessonId, 0, savePosition: false))
            HomeShowToday();
    }

    /// <summary>Colonne de droite de l'écran final : « Prochaines étapes » puis « Raccourcis à
    /// retenir », chacune dans sa carte. Une carte qui ne tient pas avant <paramref name="limit"/>
    /// est omise.</summary>
    private void DrawTutorialFinalCards(IntPtr hdc, int left, int right, int top, int limit)
    {
        int pad = S(18);
        var headFont = F(AppFonts.Text, 15, TypeRamp.Semibold);
        var small = F(AppFonts.Text, 13);
        uint fill = PaperDraw.Blend(CLR_ACCENT, CLR_PANEL, 0.04f);

        var steps = new List<(string Label, string Help, Action Open)>
        {
            (L.LessonsHome_Today, L.Learning_FinalNextTodayHelp, () => ContinueAfterTutorial(HomeShowToday)),
            (L.LessonsHome_YourMap, L.Learning_FinalNextMapHelp, () => ContinueAfterTutorial(() => HomeShowMap())),
        };
        if (TutorialAfricaLesson() is { } africa)
            steps.Add((africa.Title, L.Learning_FinalNextAfricaHelp, () => ContinueAfterTutorial(OpenTutorialAfrica)));

        int rowH = S(50);
        int nextH = pad + S(26) + steps.Count * rowH + S(4);
        if (top + nextH <= limit)
        {
            var card = new Win32.RECT { left = left, top = top, right = right, bottom = top + nextH };
            PaperDraw.RoundBox(hdc, card, fill, CLR_BORDER, S(3));
            var head = new Win32.RECT { left = left + pad, top = top + S(14), right = right - pad, bottom = top + S(38) };
            PaperText.DrawLine(hdc, Ellipsize(hdc, L.Learning_FinalNextTitle, headFont, head.right - head.left), headFont, head,
                CLR_TEXT, TextAlign.Left);
            int y = head.bottom + S(6);
            foreach (var (label, help, open) in steps)
            {
                DrawLink(hdc, left + pad, y + S(12), label, false, open);
                var helpRect = new Win32.RECT { left = left + pad, top = y + S(24), right = right - pad, bottom = y + S(46) };
                PaperText.DrawLine(hdc, Ellipsize(hdc, help, small, helpRect.right - helpRect.left), small, helpRect,
                    CLR_MUTED, TextAlign.Left);
                y += rowH;
            }
            top = card.bottom + S(16);
        }

        var shortcuts = new (string Combo, string Label)[]
        {
            (L.SettingsClarity_Combo(ShortcutKeyName(ConfigManager.ShortcutVirtualKeyboardVk)), L.Settings_ShortcutLabelKeyboard),
            (L.SettingsClarity_Combo(ShortcutKeyName(ConfigManager.ShortcutCharacterSearchVk)), L.Search_WindowTitle),
        };
        int shortcutRowH = S(32);
        int shortcutsH = pad + S(26) + shortcuts.Length * shortcutRowH + S(4);
        if (top + shortcutsH > limit) return;
        var box = new Win32.RECT { left = left, top = top, right = right, bottom = top + shortcutsH };
        PaperDraw.RoundBox(hdc, box, fill, CLR_BORDER, S(3));
        var title = new Win32.RECT { left = left + pad, top = top + S(14), right = right - pad, bottom = top + S(38) };
        PaperText.DrawLine(hdc, Ellipsize(hdc, L.Learning_FinalShortcutsTitle, headFont, title.right - title.left), headFont, title,
            CLR_TEXT, TextAlign.Left);
        int row = title.bottom + S(6);
        var comboFont = F(AppFonts.Text, 15, TypeRamp.Semibold);
        int comboW = shortcuts.Max(s => PaperText.Width(hdc, s.Combo, comboFont));
        foreach (var (combo, label) in shortcuts)
        {
            var comboRect = new Win32.RECT { left = left + pad, top = row, right = left + pad + comboW, bottom = row + shortcutRowH };
            PaperText.DrawLine(hdc, combo, comboFont, comboRect, CLR_TEXT, TextAlign.Left);
            var labelRect = new Win32.RECT { left = comboRect.right + S(16), top = row, right = right - pad, bottom = row + shortcutRowH };
            PaperText.DrawLine(hdc, Ellipsize(hdc, label, F(AppFonts.Text, 15), labelRect.right - labelRect.left),
                F(AppFonts.Text, 15), labelRect, CLR_MUTED, TextAlign.Left);
            row += shortcutRowH;
        }
    }

    private static string ShortcutKeyName(uint vk) => ConfigManager.GetShortcutDisplayName(vk);

    /// <summary>Écran final : les textes réussis du parcours, une ligne cochée chacun, avec sous
    /// chaque ligne le geste de son premier caractère particulier tant que la hauteur le permet.
    /// Les lignes qui ne tiennent pas au-dessus du bouton sont omises. Rend le bas de la liste.</summary>
    private int DrawTutorialRecap(IntPtr hdc, Win32.RECT body, int x, int right, int top)
    {
        if (_tutorialRoute == null) return top;
        var done = ConfigManager.LearningCompletedExercises;
        var targets = TutorialSteps.RecapTargets(_tutorialRoute, done.Contains);
        if (targets.Length == 0) return top;

        var headFont = F(AppFonts.Text, 15, TypeRamp.Semibold);
        var rowFont = F(AppFonts.Text, 18);
        var gestureFont = F(AppFonts.Text, 13);
        int limit = body.bottom - S(22) - S(36) - S(16);
        int y = top + S(28);
        var head = new Win32.RECT { left = x, top = y, right = right, bottom = y + S(24) };
        PaperText.DrawLine(hdc, Ellipsize(hdc, L.Learning_FinalRecap, headFont, right - x), headFont, head,
            CLR_TEXT, TextAlign.Left);
        y = head.bottom + S(8);

        var gestures = targets.Select(TutorialSteps.RecapGesture).ToArray();
        bool withGestures = gestures.Any(g => g != null) && y + S(56) * targets.Length <= limit;
        int mainH = withGestures ? S(30) : S(36);
        int rowH = withGestures ? S(56) : S(36);

        int box = S(18);
        for (int i = 0; i < targets.Length; i++)
        {
            if (y + rowH > limit) break;
            var square = new Win32.RECT { left = x, top = y + (mainH - box) / 2, right = x + box, bottom = y + (mainH - box) / 2 + box };
            PaperDraw.RoundBox(hdc, square, CLR_ACCENT, CLR_ACCENT, S(3));
            DrawTick(hdc, square);
            var text = new Win32.RECT { left = square.right + S(12), top = y, right = right, bottom = y + mainH };
            PaperText.DrawLine(hdc, Ellipsize(hdc, targets[i], rowFont, text.right - text.left), rowFont, text,
                CLR_TEXT, TextAlign.Left);
            if (withGestures && gestures[i] is { } gesture)
            {
                var line = new Win32.RECT { left = text.left, top = y + mainH - S(4), right = right, bottom = y + mainH + S(18) };
                PaperText.DrawLine(hdc, Ellipsize(hdc, gesture, gestureFont, line.right - line.left), gestureFont, line,
                    CLR_MUTED, TextAlign.Left);
            }
            y += rowH;
        }
        return y;
    }

    /// <summary>Case d'usage sur deux lignes : le libellé, puis en gris ce que son exercice fait
    /// taper. La ligne cochée garde un fond léger.</summary>
    private void DrawTutorialUsageRow(IntPtr hdc, Win32.RECT row, string label, string help, bool enabled, Action toggle)
    {
        bool hot = IsHot(row);
        if (hot || enabled)
        {
            uint fill = PaperDraw.Blend(CLR_ACCENT, CLR_PANEL, hot ? 0.08f : 0.05f);
            PaperDraw.RoundBox(hdc, row, fill, fill, S(4));
        }
        int box = S(18);
        int lineTop = row.top + S(11), lineH = S(24);
        var square = new Win32.RECT { left = row.left + S(8), top = lineTop + (lineH - box) / 2, right = row.left + S(8) + box };
        square.bottom = square.top + box;
        PaperDraw.RoundBox(hdc, square, enabled ? CLR_ACCENT : CLR_PANEL, enabled ? CLR_ACCENT : CLR_PENDING, S(3));
        if (enabled)
            DrawTick(hdc, square);
        int textLeft = square.right + S(12), textRight = row.right - S(8);
        var labelFont = F(AppFonts.Text, 16);
        var labelRect = new Win32.RECT { left = textLeft, top = lineTop, right = textRight, bottom = lineTop + lineH };
        PaperText.DrawLine(hdc, Ellipsize(hdc, label, labelFont, textRight - textLeft), labelFont, labelRect, CLR_TEXT, TextAlign.Left);
        var helpFont = F(AppFonts.Text, 13);
        var helpRect = new Win32.RECT { left = textLeft, top = labelRect.bottom, right = textRight, bottom = labelRect.bottom + S(20) };
        PaperText.DrawLine(hdc, Ellipsize(hdc, help, helpFont, textRight - textLeft), helpFont, helpRect, CLR_MUTED, TextAlign.Left);
        AddClick(row, toggle, label + ", " + help, LessonControlKind.CheckBox, toggled: enabled);
    }

    /// <summary>Panneau « Votre parcours » de l'écran des usages : les exercices que
    /// <see cref="TutorialSteps.BuildRoute"/> tire des cases cochées, dans l'ordre où ils seront
    /// joués, ceux des usages en couleur d'action avec leur étiquette. La hauteur d'une ligne
    /// décroît avec leur nombre, sans palier (QCM du 07/11) ; sous un seuil, les lignes d'aide
    /// tombent, puis les lignes qui ne tiennent pas avant <paramref name="limit"/>.</summary>
    private void DrawTutorialPlan(IntPtr hdc, int left, int right, int top, int limit)
    {
        var route = TutorialSteps.BuildRoute(TutorialUsageIds.Where((_, i) => _tutorialUsages[i]));
        bool anyChosen = _tutorialUsages.Any(b => b);
        int pad = S(22);
        int headH = S(18) + S(22) + S(20) + S(8);
        int avail = limit - top - headH - S(14);
        int stepH = Math.Min(S(78), avail / route.Length);
        bool withWhat = stepH >= S(46);
        if (!withWhat)
            stepH = Math.Max(S(28), Math.Min(S(40), avail / route.Length));
        int shown = Math.Min(route.Length, Math.Max(0, avail / stepH));
        if (shown == 0) return;

        uint fill = PaperDraw.Blend(CLR_BG, CLR_PANEL, 0.6f);
        var panel = new Win32.RECT { left = left, top = top, right = right, bottom = top + headH + shown * stepH + S(14) };
        PaperDraw.RoundBox(hdc, panel, fill, CLR_BORDER, S(6));
        int innerLeft = left + pad, innerRight = right - pad;
        var headFont = F(AppFonts.Text, 15, TypeRamp.Semibold);
        var small = F(AppFonts.Text, 13);
        var head = new Win32.RECT { left = innerLeft, top = top + S(18), right = innerRight, bottom = top + S(40) };
        string title = L.Learning_PlanTitle(route.Length);
        PaperText.DrawLine(hdc, Ellipsize(hdc, title, headFont, innerRight - innerLeft), headFont, head, CLR_TEXT, TextAlign.Left);
        var sub = new Win32.RECT { left = innerLeft, top = head.bottom, right = innerRight, bottom = head.bottom + S(20) };
        PaperText.DrawLine(hdc, Ellipsize(hdc, L.Learning_PlanSubtitle, small, innerRight - innerLeft), small, sub, CLR_MUTED, TextAlign.Left);

        var sampleFont = F(AppFonts.Text, 17);
        var codeFont = F(AppFonts.Mono, 15);
        uint rule = PaperDraw.Blend(CLR_BORDER, fill, 0.6f);
        int y = top + headH;
        for (int i = 0; i < shown; i++)
        {
            var step = route[i];
            string? usage = TutorialSteps.UsageOf(step.Index);
            bool fresh = usage != null && anyChosen;
            string what = usage != null ? TutorialUsageHelp(usage) : step.Index switch
            {
                0 => L.Learning_PlanCapital,
                1 => L.Learning_PlanCapitals,
                _ => L.Learning_PlanAt,
            };
            string? tag = usage == null ? null
                : !anyChosen ? L.Learning_TagDefault
                : step.Skippable ? L.Learning_TagBonus(TutorialUsageTag(usage))
                : TutorialUsageTag(usage);

            int contentH = withWhat ? S(24) + S(18) : S(24);
            int lineTop = y + (stepH - contentH) / 2;
            var number = new Win32.RECT { left = innerLeft, top = lineTop, right = innerLeft + S(24), bottom = lineTop + S(24) };
            PaperText.DrawLine(hdc, (i + 1).ToString(), small, number, fresh ? CLR_ACCENT : CLR_MUTED, TextAlign.Left);
            int textLeft = innerLeft + S(36), textRight = innerRight;
            if (tag != null)
            {
                int tagW = PaperText.Width(hdc, tag, small);
                var tagRect = new Win32.RECT { left = innerRight - tagW, top = lineTop, right = innerRight, bottom = lineTop + S(24) };
                PaperText.DrawLine(hdc, tag, small, tagRect, fresh ? CLR_ACCENT : CLR_MUTED, TextAlign.Right);
                textRight = tagRect.left - S(12);
            }
            var font = usage == TutorialSteps.UsageCode ? codeFont : sampleFont;
            string sample = TutorialSteps.All[step.Index].Target;
            var sampleRect = new Win32.RECT { left = textLeft, top = lineTop, right = textRight, bottom = lineTop + S(24) };
            PaperText.DrawLine(hdc, Ellipsize(hdc, sample, font, textRight - textLeft), font, sampleRect,
                fresh ? CLR_ACCENT : CLR_TEXT, TextAlign.Left);
            if (withWhat)
            {
                var whatRect = new Win32.RECT { left = textLeft, top = lineTop + S(24), right = innerRight, bottom = lineTop + S(42) };
                PaperText.DrawLine(hdc, Ellipsize(hdc, what, small, innerRight - textLeft), small, whatRect, CLR_MUTED, TextAlign.Left);
            }
            if (i < shown - 1)
                PaperDraw.Fill(hdc, innerLeft, y + stepH - 1, innerRight, y + stepH - 1 + Math.Max(1, S(1)), rule);
            y += stepH;
        }
    }

    private void DrawTutorialPrimaryButton(IntPtr hdc, Win32.RECT body, int right, string text, Action action)
    {
        int w = Math.Max(S(140), OutlineButtonWidth(hdc, text));
        var rect = new Win32.RECT { left = right - w, top = body.bottom - S(22) - S(36), right = right, bottom = body.bottom - S(22) };
        DrawOutlineButton(hdc, rect, text, action, primary: true);
    }

    /// <summary>Ligne du parcours sous la consigne : rang de l'exercice, « Passer cet
    /// exercice » pour un bonus, « Passer pour l'instant ». Rend le haut de ce qui suit.</summary>
    private int DrawTutorialRouteBar(IntPtr hdc, int left, int right, int top)
    {
        if (!TutorialOnRouteStep) return top;
        var step = _tutorialRoute![_tutorialRoutePos];
        int mid = top + S(13);

        int x = right;
        var skipForNow = DrawLink(hdc, 0, 0, L.Learning_SkipForNow, false, LeaveTutorialForNow, measureOnly: true);
        x -= skipForNow.right - skipForNow.left - S(8);
        var placed = DrawLink(hdc, x, mid, L.Learning_SkipForNow, false, LeaveTutorialForNow, L.Learning_SkipForNowTooltip);
        x = placed.left - S(20);
        if (step.Skippable)
        {
            var skip = DrawLink(hdc, 0, 0, L.Learning_BtnSkip, false, AdvanceTutorialRoute, measureOnly: true);
            x -= skip.right - skip.left - S(8);
            placed = DrawLink(hdc, x, mid, L.Learning_BtnSkip, false, AdvanceTutorialRoute);
            x = placed.left - S(20);
        }

        var small = F(AppFonts.Text, 13);
        string progress = L.Learning_RouteProgress(_tutorialRoutePos + 1, _tutorialRoute.Length, step.Skippable);
        var progressRect = new Win32.RECT { left = left, top = top, right = Math.Max(left, x), bottom = top + S(26) };
        PaperText.DrawLine(hdc, Ellipsize(hdc, progress, small, progressRect.right - progressRect.left), small, progressRect,
            CLR_MUTED, TextAlign.Left);
        return top + S(30);
    }

    private static string TutorialUsageLabel(int index) => TutorialUsageIds[index] switch
    {
        TutorialSteps.UsageDocuments => L.Learning_UsageDocuments,
        TutorialSteps.UsageMessages => L.Learning_UsageMessages,
        TutorialSteps.UsageCode => L.Learning_UsageCode,
        TutorialSteps.UsageLanguages => L.Learning_UsageLanguages,
        _ => L.Learning_UsageAfrica,
    };

    private static string TutorialUsageHelp(string usage) => usage switch
    {
        TutorialSteps.UsageDocuments => L.Learning_UsageDocumentsHelp,
        TutorialSteps.UsageMessages => L.Learning_UsageMessagesHelp,
        TutorialSteps.UsageCode => L.Learning_UsageCodeHelp,
        TutorialSteps.UsageLanguages => L.Learning_UsageLanguagesHelp,
        _ => L.Learning_UsageAfricaHelp,
    };

    private static string TutorialUsageTag(string usage) => usage switch
    {
        TutorialSteps.UsageDocuments => L.Learning_TagDocuments,
        TutorialSteps.UsageMessages => L.Learning_TagMessages,
        TutorialSteps.UsageCode => L.Learning_TagCode,
        TutorialSteps.UsageLanguages => L.Learning_TagLanguages,
        _ => L.Learning_TagAfrica,
    };
}
