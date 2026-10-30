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
        if (_tutorialExcludesTyping)
        {
            UsageStats.EndExcludedTyping();
            _tutorialExcludesTyping = false;
        }
        if (!wasActive) return;
        try { TutorialClosed?.Invoke(completed); }
        catch (Exception ex) { ConfigManager.Log("LessonsWindow.TutorialClosed", ex); }
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

    /// <summary>Écran des usages ou écran final, dans le cadre de l'exercice. Faux s'il n'y a
    /// rien à dessiner : l'exercice se dessine alors comme d'habitude.</summary>
    private bool DrawTutorialPage(IntPtr hdc, Win32.RECT body)
    {
        if (!TutorialPageShown) return false;
        int firstControl = _clickActions.Count;
        PaperDraw.RoundBox(hdc, body, CLR_PANEL, CLR_BORDER, 0);
        int pad = S(28);
        int x = body.left + pad;
        int right = body.right - pad;
        var titleFont = F(AppFonts.Title, 26, TypeRamp.Semibold);
        var textFont = F(AppFonts.Text, 15);
        var titleRect = new Win32.RECT { left = x, top = body.top + S(22), right = right, bottom = body.top + S(58) };

        if (_tutorialPage == TutorialPage.Usages)
        {
            PaperText.DrawLine(hdc, Ellipsize(hdc, L.Learning_UsagesQuestion, titleFont, right - x), titleFont, titleRect,
                CLR_TEXT, TextAlign.Left);
            var help = new Win32.RECT { left = x, top = titleRect.bottom + S(4), right = right, bottom = titleRect.bottom + S(26) };
            PaperText.DrawLine(hdc, Ellipsize(hdc, L.Learning_UsagesHelp, textFont, right - x), textFont, help,
                CLR_MUTED, TextAlign.Left);

            int rowW = Math.Min(right - x, S(560));
            int top = help.bottom + S(16);
            for (int i = 0; i < TutorialUsageIds.Length; i++)
            {
                int index = i;
                var row = new Win32.RECT { left = x - S(8), top = top, right = x - S(8) + rowW, bottom = top + S(36) };
                DrawCheckRow(hdc, row, TutorialUsageLabel(i), _tutorialUsages[i], () => ToggleTutorialUsage(index));
                top = row.bottom + S(4);
            }

            DrawTutorialPrimaryButton(hdc, body, right, L.Learning_BtnContinue, StartTutorialRoute);
        }
        else
        {
            PaperText.DrawLine(hdc, Ellipsize(hdc, L.Learning_FinalTitle, titleFont, right - x), titleFont, titleRect,
                CLR_TEXT, TextAlign.Left);
            var sub = new Win32.RECT { left = x, top = titleRect.bottom + S(8), right = right, bottom = titleRect.bottom + S(32) };
            PaperText.DrawLine(hdc, Ellipsize(hdc, L.Learning_FinalSubtitle, textFont, right - x), textFont, sub,
                CLR_MUTED, TextAlign.Left);
            DrawTutorialPrimaryButton(hdc, body, right, L.Learning_BtnFinish, FinishTutorial);
        }
        if (_tutorialFocusPending)
        {
            _focusedActionIndex = firstControl;
            _tutorialFocusPending = false;
        }
        return true;
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
}
