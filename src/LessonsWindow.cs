using System.Runtime.InteropServices;

namespace AZERTYGlobal;

internal sealed partial class LessonsWindow : IDisposable, ILessonsAutomationHost
{
    private static readonly string WND_CLASS_NAME = ProductIdentity.WindowClass("Lessons");
    private const int BASE_WIN_W = 1120;
    private const int BASE_WIN_H = 760;
    private const int BASE_MIN_W = 940;
    private const int BASE_MIN_H = 600;
    private const int WMSZ_LEFT = 1;
    private const int WMSZ_RIGHT = 2;
    private const int WMSZ_TOP = 3;
    private const int WMSZ_TOPLEFT = 4;
    private const int WMSZ_TOPRIGHT = 5;
    private const int WMSZ_BOTTOM = 6;
    private const int WMSZ_BOTTOMLEFT = 7;
    private const uint CS_DBLCLKS = 0x0008;
    private const uint TIMER_KEYPRESS = 8303;
    private const uint TIMER_HINT_CLEAR = 8304;
    private const uint TIMER_HINT_FLASH_CLEAR = 8305;
    private const uint TIMER_FREE_STATS = 8306;
    private const uint KEYPRESS_DURATION_MS = 120;
    private const uint HINT_DIRECT_MS = 3000;
    private const uint HINT_DEADKEY_MS = 4500;
    private const uint HINT_FLASH_MS = 1000;
    private const uint FREE_STATS_REFRESH_MS = 1000;
    private const int PENDING_PHYSICAL_TEXT_TIMEOUT_MS = 1000;
    /// <summary>Longueur du champ du mode Libre. L'ancien plafond de 220 caractères coupait le
    /// texte sans rien dire (audit 09/11, E6) ; au-delà de celui-ci, un message le dit.</summary>
    internal const int FREE_MAX_CHARS = 20_000;

    // Refonte 1.3.0 (Tablature, maquette Leçons F du 07/10) : la fenêtre passe au papier.
    // A14 : en contraste élevé, le parcours d'exercice prend la palette de l'accueil des Leçons
    // (HomePalette, « .contraste » des maquettes) au lieu du papier en dur. Réussite et erreur
    // n'y ont pas de couleur propre : elles prennent l'encre, l'état se lit au message et au
    // trait. Le clavier papier et la Tablature gardent leurs touches claires, comme à l'accueil.
    private HomePalette _pal = HomePaletteNow();
    private uint CLR_BG => _pal.Paper;
    private uint CLR_PANEL => _pal.Surface;
    private uint CLR_PANEL_2 => _pal.Surface;
    private uint CLR_BORDER => _pal.Border;
    private uint CLR_TEXT => _pal.Ink;
    private uint CLR_MUTED => _pal.Muted;
    private uint CLR_ACCENT => _pal.Action;
    private uint CLR_OK => _pal.HighContrast ? _pal.Ink : PaperTheme.Success;
    private uint CLR_BAD => _pal.HighContrast ? _pal.Ink : PaperTheme.Error;
    private uint CLR_CURRENT => _pal.Action;
    private uint CLR_PENDING => _pal.Pale;
    private uint CLR_LESSON_ACCENT => _pal.HighContrast ? _pal.Ink : PaperTheme.Error;
    private uint CLR_ON_ACCENT => _pal.OnAction;

    private const uint MB_YESNO = 0x00000004;
    private const uint MB_ICONWARNING = 0x00000030;
    private const int IDYES = 6;
    private const int HTCLIENT = 1;
    private const uint WM_CAPTURECHANGED = 0x0215;
    private const int IDC_HAND = 32649;

    private enum WindowMode { Lessons, Free }

    private readonly record struct FreeVisualLine(int Start, int Length, int Width);

    private readonly Layout _layout;
    private readonly KeyMapper _mapper;
    private readonly KeyboardHook _hook;
    // Non readonly : rechargé dans la langue courante lors d'une bascule de langue en
    // cours de session (les titres/instructions sont résolus au parse ; le texte à taper
    // reste français, donc la session de frappe en cours est préservée).
    private LessonCatalog _catalog;
    private readonly LessonProgressStore _progress;
    private readonly LessonHintProvider _hints;
    private readonly Win32.WNDPROC _wndProcDelegate;
    private readonly List<(Win32.RECT Rect, Action Action)> _clickActions = new();
    private readonly List<(Win32.RECT Rect, Action Action)> _doubleClickActions = new();
    private readonly List<(Win32.RECT Rect, string Tooltip, bool PreferAbove, bool Compact)> _hoverAreas = new();
    // Audit du 25/09 (L-06) : les infobulles des 62 touches ne sont plus construites à chaque
    // repeint. Le repeint retient le cadre du clavier et l'état dessiné ; OnMouseMove en tire,
    // pour la seule touche survolée, le texte que _hoverAreas aurait porté.
    // Refonte 1.3.0 : le clavier papier est placé à une taille de touche fixe ; on retient son
    // placement, plus son cadre.
    private (KeyboardPlacement Placement, KeyboardRenderProfile Profile, KeyboardRenderState State)? _keyboardHover;

    private IntPtr _hWnd;
    private bool _visible;
    // Report de 1911581 (lot 2) : la file du texte attendu (PhysicalTextInputBuffer) n'est
    // alimentée que si les Leçons ont le focus clavier, comme le tutoriel (_hasFocus de
    // LearningModule). Tenu par WM_SETFOCUS et WM_KILLFOCUS.
    private bool _hasFocus;
    // Caret système masqué (décision d'Antoine du 29/09) : créé et placé devant la lettre à
    // taper, jamais affiché, pour que la loupe et les lecteurs d'écran suivent la frappe. Le
    // dessin en fixe la place ; null quand rien n'est à taper.
    private Win32.RECT? _systemCaretRect;
    private (int Width, int Height)? _systemCaretSize;
    // UI Automation (étape 3 du lot) : fournisseurs créés au premier WM_GETOBJECT ; la vue de
    // la surface est relevée à chaque repeint, et ses changements signalés juste après.
    private const uint WM_APP_AUTOMATION = 0x8000 + 0x41;
    private const uint WM_APP_AUTOMATION_INVOKE = 0x8000 + 0x42;
    private LessonsAutomation? _automation;
    private LessonSurfaceView? _surfaceView;
    private bool _automationEventPending;
    // Une entrée par zone cliquable, dans l'ordre de _clickActions (AddClick tient les deux).
    private readonly List<LessonControlView> _controlViews = new();
    private (int Index, string Name)? _pendingInvoke;
    private float _dpiScale = 1f;
    private float _windowScale = 1f;
    private int _baseClientW;
    private int _baseClientH;
    private int _nonClientW;
    private int _nonClientH;
    // Audit du 30/10, M2 : surface dessinée et défilement (WindowSizing.FitCanvas). Tout se
    // dessine et se teste en coordonnées de la surface ; seuls la souris, le caret système et
    // l'UI Automation passent par _scrollX et _scrollY.
    private int _canvasW;
    private int _canvasH;
    private int _scrollX;
    private int _scrollY;
    private bool _scrollsX;
    private bool _scrollsY;
    private bool _applyingCanvas;
    private int _lastShownFocus = int.MinValue;
    private Win32.RECT? _lastShownCaret;
    private readonly SettingsScrollState _wheel = new();
    private readonly SettingsScrollState _hWheel = new();
    private bool _liveResizing;
    private IntPtr _resizeSnapshotBitmap;
    private int _resizeSnapshotW;
    private int _resizeSnapshotH;

    private IntPtr _hFontTitle;
    private IntPtr _hFontSubtitle;
    private IntPtr _hFontText;
    private IntPtr _hFontSmall;
    private IntPtr _hFontSidebarModule;
    private IntPtr _hFontSidebarLesson;
    private IntPtr _hFontButton;
    private IntPtr _hFontIcon;
    private IntPtr _hFontEmoji;
    private IntPtr _hFontKeyboard;
    private IntPtr _hFontKeyboardDeadKey;
    private IntPtr _hFontKeyboardSmall;
    private IntPtr _hFontKeyboardTiny;
    private IntPtr _hFontKeyboardContext;

    private WindowMode _mode = WindowMode.Lessons;
    private bool _settingsOpen;
    private int _moduleIndex;
    private int _lessonIndex;
    private int _exerciseIndex;
    private LessonTypingSession _session;
    private bool _showSummary;
    /// <summary>L'Espace vient d'activer un bouton : son WM_CHAR est avalé (E4).</summary>
    private bool _swallowActivationSpace;
    private LessonAttemptStats? _lastCompletedStats;
    // Métrique « Record » du récapitulatif (QW-4, 2026-08-24) : meilleur score de
    // l'exercice, tentative courante comprise, et vrai si elle vient de l'établir.
    private int? _summaryBestWpm;
    private bool _summaryIsNewBest;
    // Défi du jour : catalogue, fin de séance et partage (L-18).
    private readonly ChallengeSessionState _challenge = new();
    private char? _hintCharacter;
    private MethodData? _hintMethod;
    private bool _hintBackspace;
    private bool _hintButtonActive;
    private uint _pressedScancode;
    private readonly PhysicalTextInputBuffer _pendingPhysicalText =
        new(PENDING_PHYSICAL_TEXT_TIMEOUT_MS);
    private bool _trackingMouseLeave;
    // Audit UX 1.3.0, #10 : point survolé (int.MinValue hors fenêtre) et cible cliquable
    // dessous ; le repeint en tire l'état « hot » des boutons et des lignes.
    private int _hotX = int.MinValue, _hotY = int.MinValue;
    private int _hotActionIndex = -1;
    private int _focusedActionIndex = -1;

    /// <summary>Ce qui décide de la liste des cibles cliquables : un autre écran a d'autres
    /// boutons, et l'index focalisé n'y désigne plus le même (audit 09/11, L4 : une Entrée
    /// pouvait « Recommencer » ou ouvrir les Paramètres sur l'écran suivant).</summary>
    private readonly record struct FocusScreen(WindowMode Mode, bool Settings, bool Picker, HomePage Page,
        bool Summary, TutorialPage Tutorial, int TutorialPos, int Exercise, bool Composed);

    private FocusScreen CurrentFocusScreen => new(_mode, _settingsOpen, _pickerOpen, _homePage,
        _showSummary, _tutorialPage, _tutorialRoutePos, _exerciseIndex, _homeComposed != null);

    // L'écran dont _clickActions décrit les cibles (dernier repeint).
    private FocusScreen? _clickActionsScreen;
    // Écran pour lequel le focus vient d'être posé exprès (liste ouverte sur sa première ligne).
    private FocusScreen? _focusPlacedFor;

    /// <summary>Au repeint : un écran changé repart sans cible focalisée (la surface de frappe
    /// ou rien), sauf si le focus vient d'y être posé exprès.</summary>
    private void SyncFocusWithScreen()
    {
        var screen = CurrentFocusScreen;
        if (_clickActionsScreen is { } shown && shown != screen && _focusPlacedFor != screen)
            _focusedActionIndex = -1;
        _clickActionsScreen = screen;
        _focusPlacedFor = null;
    }
    // Remontée n° 7 d'Antoine (07/11) : comme Windows, l'anneau de focus ne se montre qu'après
    // une navigation au clavier (Tab) ; un clic le cache à nouveau. La cible focalisée reste
    // active (Entrée), seul son repère visuel attend. Réglage « toujours souligner » respecté.
    private bool _focusCuesShown;
    private bool FocusCuesVisible => _focusCuesShown || SystemKeyboardCues();
    private string? _hoverTooltip;
    private Win32.RECT _hoverTooltipAnchor;
    private bool _hoverTooltipPreferAbove;
    private bool _hoverTooltipCompact;

    private DateTimeOffset? _freeStartedAt;
    private int _freeChars;
    private int _freeBackspaces;
    /// <summary>Le champ du mode Libre (TextEditBuffer.cs). Il vit le temps de l'application :
    /// Échap ou la croix ferment la fenêtre sans effacer le texte (audit 09/11, E7) ; rien
    /// n'est écrit sur le disque.</summary>
    private readonly TextEditBuffer _free = new(FREE_MAX_CHARS);
    private string _freePreview => _free.Text;
    private int _freeCursorIndex => _free.Caret;
    private bool _fieldDragging;
    private bool _freeTruncated;
    private char? _freeHighSurrogate;
    private readonly Dictionary<string, int> _freeWidths = new(StringComparer.Ordinal);
    private int _freeWidthsScale;
    private Win32.RECT _freePreviewRect;

    /// <summary>Modificateurs tenus pendant la frappe en cours. Les tests le remplacent : le
    /// clavier réel de la machine ne doit pas compter.</summary>
    internal Func<EditModifiers> ReadEditModifiers { get; set; }

    /// <summary>Presse-papiers des champs ; les tests en passent un factice.</summary>
    internal ITextClipboard Clipboard { get; set; }

    public LessonsWindow(Layout layout, KeyMapper mapper, KeyboardHook hook)
    {
        _layout = layout;
        _mapper = mapper;
        _hook = hook;
        ReadEditModifiers = () => new EditModifiers(
            Ctrl: KeyHeld(0x11), Shift: KeyHeld(0x10), Alt: KeyHeld(0x12),
            Win: KeyHeld(0x5B) || KeyHeld(0x5C), AltGr: _mapper.AltGrDown);
        Clipboard = new WindowClipboard(() => _hWnd);
        _catalog = _challenge.LoadCatalog();
        _progress = new LessonProgressStore();
        _progress.SyncOnboardingProgress(_catalog, ConfigManager.LearningCompletedExercises);
        _hints = new LessonHintProvider();
        _wndProcDelegate = WndProc;
        _session = new LessonTypingSession(_catalog.Exercises[0]);

        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        int dpi = Win32.GetDeviceCaps(hdcScreen, 88);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        _dpiScale = dpi > 0 ? dpi / 96f : 1f;

        CreateFonts();
        CreateWindow();
        ConfigManager.WindowBoundsCleared += OnWindowBoundsCleared;
        PickInitialSelection();
        StartCurrentSession(savePosition: false);

        _mapper.StateChanged += OnMapperStateChanged;
        _hook.RawKeyDownDetailed += OnRawKeyDown;
        _mapper.KeyDecided += OnMapperKeyDecided;
        // Bascule de langue en cours de session (ex. depuis le menu tray) : recharger le
        // catalogue dans la nouvelle langue et repeindre, sans fermer la fenêtre ni perdre
        // la position (constat smoke test 2026-07-17).
        _onAppLanguageChanged = _ => RefreshLanguage();
        ConfigManager.AppLanguageChanged += _onAppLanguageChanged;
    }

    private readonly Action<string>? _onAppLanguageChanged;

    public bool IsVisible => _visible;

    /// <summary>Handle de la fenêtre — propriétaire des boîtes de dialogue système.</summary>
    public IntPtr Handle => _hWnd;

    /// <summary>Langue de l'UI au moment du parse du catalogue (les titres/instructions des
    /// leçons sont résolus au parse — cf. LessonCatalogLoader.PickText). Mise à jour au
    /// rechargement live du catalogue ; permet à TrayApplication de recréer la fenêtre si la
    /// langue a changé pendant qu'elle était masquée.</summary>
    public string CatalogLanguage { get; private set; } = L.Language;

    /// <summary>Recharge le catalogue dans la langue courante et repeint. Les index de
    /// position (module/leçon/exercice) et la session de frappe en cours sont conservés :
    /// le texte à taper (Content) est en français et ne change pas avec la langue, seuls
    /// les titres/descriptions/instructions sont retraduits (relus depuis _catalog au paint).</summary>
    private void RefreshLanguage()
    {
        if (CatalogLanguage == L.Language) return;
        _catalog = _challenge.LoadCatalog();
        _progress.SyncOnboardingProgress(_catalog, ConfigManager.LearningCompletedExercises);
        CatalogLanguage = L.Language;
        _challenge.RefreshShareLanguage(); // le texte à partager suit la langue (audit UX du 09/11, L14)
        // Index défensivement bornés (structure identique entre langues, mais on ne suppose rien).
        _moduleIndex = Math.Clamp(_moduleIndex, 0, _catalog.Modules.Count - 1);
        _lessonIndex = Math.Clamp(_lessonIndex, 0, _catalog.Modules[_moduleIndex].Lessons.Count - 1); // le catalogue, pas la séance composée de l'accueil
        _exerciseIndex = Math.Clamp(_exerciseIndex, 0, _catalog.Modules[_moduleIndex].Lessons[_lessonIndex].Exercises.Count - 1);
        // Audit de régression du 10/11, RA12. Le titre de la fenêtre (barre de titre, barre des
        // tâches, Alt+Tab) restait dans l'ancienne langue ; la séance composée aussi.
        SetTutorialCaption(IsTutorialActive);
        HomeRetranslateComposed();
        // Le catalogue rechargé porte le Défi du moment : fenêtre restée ouverte après minuit ou
        // étape changée, l'exercice affiché n'est plus celui de la séance en cours, dont la
        // frappe irait sous la clé et la date du nouveau. La séance repart sur l'exercice
        // affiché ; même texte (seule la langue change), elle continue telle quelle.
        if (!StringComparer.Ordinal.Equals(_session.Exercise.StableKey, CurrentExercise.StableKey))
            StartCurrentSession(savePosition: false);
        if (_hWnd != IntPtr.Zero)
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    private bool _inputPaused;

    public void SetInputPaused(bool paused)
    {
        _inputPaused = paused;
    }

    /// <summary>
    /// Audit de régression du 10/11, RA5 (suite d'E26, CharacterSearch.BlockedWhilePaused) : en
    /// pause, la fenêtre refuse la frappe de l'exercice, pas la navigation. Échap (fermer), Tab et
    /// Maj+Tab (changer de cible) passent, ainsi qu'Entrée ou Espace sur un bouton focalisé ;
    /// sinon il ne restait qu'Alt+F4 au clavier. <paramref name="spaceActivates"/> : l'écran où
    /// Espace active un bouton (ailleurs, c'est une frappe).
    /// </summary>
    internal static bool BlockedWhilePaused(int vk, bool buttonFocused, bool spaceActivates) =>
        vk != 0x1B && vk != 0x09 &&
        !(buttonFocused && (vk == 0x0D || (vk == 0x20 && spaceActivates)));

    /// <summary>
    /// Reconstruit le module Défi si la date ou l'étape a changé (fenêtre restée ouverte
    /// à cheval sur minuit, séance terminée) et repositionne les index si besoin.
    /// </summary>
    private void RefreshChallengeModuleIfStale()
    {
        if (!_challenge.IsStale()) return;
        bool wasOnChallenge = _catalog.Modules[_moduleIndex].Id == DailyChallenge.ModuleId;
        _catalog = _challenge.LoadCatalog();
        _moduleIndex = Math.Clamp(_moduleIndex, 0, _catalog.Modules.Count - 1);
        _lessonIndex = Math.Clamp(_lessonIndex, 0, _catalog.Modules[_moduleIndex].Lessons.Count - 1); // le catalogue, pas la séance composée de l'accueil
        _exerciseIndex = Math.Clamp(_exerciseIndex, 0, _catalog.Modules[_moduleIndex].Lessons[_lessonIndex].Exercises.Count - 1);
        if (wasOnChallenge && _catalog.Modules[_moduleIndex].Id != DailyChallenge.ModuleId)
        {
            // Le module a disparu (opt-out) : revenir au premier module proprement.
            _moduleIndex = 0; _lessonIndex = 0; _exerciseIndex = 0;
            StartCurrentSession(savePosition: false);
        }
    }

    /// <summary>
    /// Ouvre la fenêtre directement sur la séance « Défi du jour » (clic sur le rappel
    /// ou entrée du menu tray). Retourne false si le module n'est pas disponible.
    /// </summary>
    public bool ShowChallenge()
    {
        if (!OpenChallenge()) return false;
        Show();
        return true;
    }

    /// <summary>État du Défi demandé, sans afficher la fenêtre (les tests ne prennent pas le
    /// premier plan du poste).</summary>
    internal bool OpenChallenge()
    {
        if (!DailyChallenge.Enabled) return false; // 1.3.0 : Défi masqué
        _challenge.Request(); // offert même sans opt-in, reconstruit à la date et à l'étape du moment (A23)
        RefreshChallengeModuleIfStale();
        for (int m = 0; m < _catalog.Modules.Count; m++)
        {
            if (_catalog.Modules[m].Id != DailyChallenge.ModuleId) continue;
            var lesson = _catalog.Modules[m].Lessons[0];
            // Parcours de l'accueil en cours : il se clôt, sinon son en-tête, son bilan sans
            // chiffres et sa frappe hors statistiques restaient sur le Défi (audit UX du 09/11, L16).
            EndTutorial(completed: _tutorialPage == TutorialPage.Final);
            if (!SelectExercise(DailyChallenge.ModuleId, lesson.Id, 0, savePosition: false))
                return false;
            _homeOpenOnExercise = true; // accueil des Leçons : le défi s'ouvre sur son exercice (LessonsWindow.Home.cs)
            return true;
        }
        return false;
    }

    public void Show()
    {
        // Audit UX du 09/11, W2 et E24 : un menu de la zone de notification qui rouvre une
        // fenêtre déjà ouverte la ramène devant, sans la déplacer, sans revenir à « Aujourd'hui »
        // ni éteindre Verr. Maj. Seule une demande explicite (le Défi) change d'écran.
        if (_visible)
        {
            HomeOnShow(); // ne change d'écran que pour un exercice demandé (_homeOpenOnExercise)
            Win32.ShowWindow(_hWnd, 9); // SW_RESTORE : réaffiche aussi une fenêtre réduite
            Win32.SetForegroundWindow(_hWnd);
            Win32.SetFocus(_hWnd);
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
            return;
        }
        if (!RestoreSavedBoundsIfVisible())
            CenterOnActiveMonitor();
        UpdateRenderScaleFromCurrentClient(force: true);
        RefreshChallengeModuleIfStale();
        _progress.SyncOnboardingProgress(_catalog, ConfigManager.LearningCompletedExercises);
        _mapper.RequestCapsLockOff();
        _mapper.SyncState();
        HomeOnShow(); // accueil des Leçons : « Aujourd'hui » à l'ouverture (LessonsWindow.Home.cs)
        _visible = true;
        LearningSessionTracker.Opened(this); // aucune demande d'avis pendant la séance
        Win32.ShowWindow(_hWnd, 1);
        Win32.SetForegroundWindow(_hWnd);
        Win32.SetFocus(_hWnd);
        UpdateAutoHintIfEnabled();
        ArmFreeStatsTimerIfNeeded();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    private int D(int value) => (int)Math.Round(value * _dpiScale);
    private int S(int value) => (int)Math.Round(value * _dpiScale * _windowScale);

    // Accueil des Leçons : une séance composée (révision, touche, texte) prend la place de
    // l'exercice du catalogue sans toucher aux index (LessonsWindow.Home.cs, HomeComposed).
    private LessonModule CurrentModule => _homeComposed?.Module ?? _catalog.Modules[_moduleIndex];
    private LessonLesson CurrentLesson => _homeComposed?.Lesson ?? CurrentModule.Lessons[_lessonIndex];
    private LessonExercise CurrentExercise => _homeComposed?.Exercise ?? CurrentLesson.Exercises[_exerciseIndex];

    private void CreateFonts()
    {
        CreateScaledFonts();
    }

    private void CreateScaledFonts()
    {
        _hFontTitle = Win32.CreateFontW(-S(TypeRamp.Title), 0, 0, 0, TypeRamp.Semibold, 0, 0, 0, 0, 0, 0, 5, 0, TypeRamp.Family);
        _hFontSubtitle = Win32.CreateFontW(-S(TypeRamp.Body), 0, 0, 0, TypeRamp.Semibold, 0, 0, 0, 0, 0, 0, 5, 0, TypeRamp.Family);
        _hFontText = Win32.CreateFontW(-S(TypeRamp.Body), 0, 0, 0, TypeRamp.Regular, 0, 0, 0, 0, 0, 0, 5, 0, TypeRamp.Family);
        _hFontSmall = Win32.CreateFontW(-S(TypeRamp.Caption), 0, 0, 0, TypeRamp.Regular, 0, 0, 0, 0, 0, 0, 5, 0, TypeRamp.Family);
        _hFontSidebarModule = Win32.CreateFontW(-S(TypeRamp.Body), 0, 0, 0, TypeRamp.Semibold, 0, 0, 0, 0, 0, 0, 5, 0, TypeRamp.Family);
        _hFontSidebarLesson = Win32.CreateFontW(-S(TypeRamp.Body), 0, 0, 0, TypeRamp.Regular, 0, 0, 0, 0, 0, 0, 5, 0, TypeRamp.Family);
        _hFontButton = Win32.CreateFontW(-S(TypeRamp.Body), 0, 0, 0, TypeRamp.Regular, 0, 0, 0, 0, 0, 0, 5, 0, TypeRamp.Family);
        _hFontIcon = Win32.CreateFontW(-S(18), 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI Symbol");
        _hFontEmoji = Win32.CreateFontW(-S(17), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI Emoji");
        _hFontKeyboard = Win32.CreateFontW(S(28), 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 4, 0, "Consolas");
        _hFontKeyboardDeadKey = Win32.CreateFontW(S(24), 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 4, 0, "Consolas");
        _hFontKeyboardSmall = Win32.CreateFontW(S(20), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 4, 0, "Consolas");
        _hFontKeyboardTiny = Win32.CreateFontW(S(16), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 4, 0, "Segoe UI");
        _hFontKeyboardContext = Win32.CreateFontW(S(20), 0, 0, 0, 500, 0, 0, 0, 0, 0, 0, 4, 0, "Segoe UI");
    }

    private void DestroyFonts()
    {
        DestroyScaledFonts();
    }

    private void RecreateScaledFonts()
    {
        DestroyScaledFonts();
        CreateScaledFonts();
    }

    private void DestroyScaledFonts()
    {
        DeleteObject(ref _hFontTitle);
        DeleteObject(ref _hFontSubtitle);
        DeleteObject(ref _hFontText);
        DeleteObject(ref _hFontSmall);
        DeleteObject(ref _hFontSidebarModule);
        DeleteObject(ref _hFontSidebarLesson);
        DeleteObject(ref _hFontButton);
        DeleteObject(ref _hFontIcon);
        DeleteObject(ref _hFontEmoji);
        DeleteObject(ref _hFontKeyboard);
        DeleteObject(ref _hFontKeyboardDeadKey);
        DeleteObject(ref _hFontKeyboardSmall);
        DeleteObject(ref _hFontKeyboardTiny);
        DeleteObject(ref _hFontKeyboardContext);
    }

    private static void DeleteObject(ref IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        Win32.DeleteObject(handle);
        handle = IntPtr.Zero;
    }

    private void CreateWindow()
    {
        var hInstance = Win32.GetModuleHandleW(null);
        // Audit du 25/09, X-01 : une classe refusée ne crée pas de fenêtre.
        if (!NativeWindow.RegisterClass(WND_CLASS_NAME, _wndProcDelegate, CS_DBLCLKS))
            return;

        int winW = D(BASE_WIN_W);
        int winH = D(BASE_WIN_H);
        uint style = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU | Win32.WS_THICKFRAME | Win32.WS_CLIPCHILDREN;

        // Écran du curseur ; la zone simulée du banc de capture quand elle est posée.
        var work = NativeWindow.WorkArea();
        int screenW = work.right - work.left;
        int screenH = work.bottom - work.top;
        // AG130-42 : borner avant de creer. A 175 % sur 1920x1080, D(1120)xD(760) vaut
        // 1960x1330 pour une zone de travail de 1920x1032 : la fenetre naissait hors ecran,
        // commandes du bas inatteignables, sans que rien n'echoue.
        // Audit du 30/10, M2 : chaque dimension pour elle-même, le contenu défile au lieu de
        // rapetisser (WindowSizing.FitCanvas), et la fenêtre prend la place de ses barres.
        (winW, winH) = ScrollingWindowSize(winW, winH, screenW, screenH);

        int x = work.left + Math.Max(0, (screenW - winW) / 2);
        int y = work.top + Math.Max(0, (screenH - winH) / 2);

        _hWnd = Win32.CreateWindowExW(0, WND_CLASS_NAME, L.LessonsWin_WindowTitle,
            style, x, y, winW, winH, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        // Fond de la classe, que Windows détruit au désenregistrement.
        NativeWindow.ApplyClassBackground(_hWnd, CLR_BG);

        // AG130-42 : le constructeur a lu le DPI du moniteur PRINCIPAL, faute de fenetre
        // a interroger. Maintenant qu'elle existe, prendre le sien : sur un poste a deux
        // ecrans de densites differentes, les polices etaient calculees pour le mauvais.
        AdoptWindowDpi();
        CaptureBaseWindowMetrics();
        RestoreSavedBoundsIfVisible();
        UpdateRenderScaleFromCurrentClient(force: true);
        NativeWindow.ApplyFrame(_hWnd, dark: false); // refonte 1.3.0 : fenêtre papier, barre claire
    }

    private bool RestoreSavedBoundsIfVisible()
    {
        if (_hWnd == IntPtr.Zero) return false;
        if (!ConfigManager.TryGetWindowBounds(ConfigManager.LessonsWindowBoundsKey, out var rect))
            return false;
        if (!IsRectVisibleOnScreen(rect))
            return false;

        Win32.MoveWindow(_hWnd, rect.left, rect.top,
            rect.right - rect.left, rect.bottom - rect.top, false);
        return true;
    }

    private void CenterOnActiveMonitor()
    {
        if (_hWnd == IntPtr.Zero || !Win32.GetWindowRect(_hWnd, out var rect))
            return;

        int winW = Math.Max(1, rect.right - rect.left);
        int winH = Math.Max(1, rect.bottom - rect.top);
        CursorSource.TryGet(out var cursor);
        var monitor = Win32.MonitorFromPoint(cursor, Win32.MONITOR_DEFAULTTONEAREST);
        var monInfo = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        if (!Win32.GetMonitorInfo(monitor, ref monInfo))
            return;

        int screenW = monInfo.rcWork.right - monInfo.rcWork.left;
        int screenH = monInfo.rcWork.bottom - monInfo.rcWork.top;
        int x = monInfo.rcWork.left + Math.Max(0, (screenW - winW) / 2);
        int y = monInfo.rcWork.top + Math.Max(0, (screenH - winH) / 2);
        Win32.MoveWindow(_hWnd, x, y, winW, winH, false);
    }

    private static bool IsRectVisibleOnScreen(Win32.RECT rect)
    {
        int width = rect.right - rect.left;
        int height = rect.bottom - rect.top;
        if (width < 100 || height < 80) return false;

        var center = new Win32.POINT
        {
            x = rect.left + width / 2,
            y = rect.top + height / 2
        };
        var monitor = Win32.MonitorFromPoint(center, 0);
        if (monitor == IntPtr.Zero) return false;

        var info = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        if (!Win32.GetMonitorInfo(monitor, ref info)) return false;

        var work = info.rcWork;
        return rect.right > work.left + 40 &&
               rect.left < work.right - 40 &&
               rect.bottom > work.top + 40 &&
               rect.top < work.bottom - 40;
    }

    private void SaveWindowBounds()
    {
        if (_hWnd == IntPtr.Zero) return;
        if (Win32.GetWindowRect(_hWnd, out var rect) && IsRectVisibleOnScreen(rect))
            ConfigManager.SetWindowBounds(ConfigManager.LessonsWindowBoundsKey, rect);
    }

    private void OnWindowBoundsCleared(string key)
    {
        if (key != ConfigManager.LessonsWindowBoundsKey || _hWnd == IntPtr.Zero)
            return;
        CenterOnActiveMonitor();
        UpdateRenderScaleFromCurrentClient(force: true);
        if (_visible)
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    /// <summary>
    /// AG130-42 — aligner l'echelle sur le DPI reel de la fenetre, puis la redimensionner
    /// pour tenir dans son ecran. Rend vrai quand quelque chose a bouge.
    /// </summary>
    private bool AdoptWindowDpi()
    {
        if (_hWnd == IntPtr.Zero) return false;
        int dpi = Win32.GetDpiForWindow(_hWnd);
        if (dpi <= 0) return false;

        float scale = dpi / 96f;
        if (MathF.Abs(scale - _dpiScale) < 0.01f) return false;

        _dpiScale = scale;
        RecreateScaledFonts();
        return true;
    }

    /// <summary>
    /// AG130-42 — remettre la fenetre a la taille voulue par le DPI courant, bornee a sa
    /// zone de travail. Windows suggere, sur WM_DPICHANGED, une taille obtenue en appliquant
    /// le nouveau facteur a l'ancienne : elle peut deborder l'ecran d'arrivee.
    /// </summary>
    private void FitWindowToWorkArea()
    {
        if (_hWnd == IntPtr.Zero) return;
        if (!Win32.GetWindowRect(_hWnd, out var window)) return;

        var work = NativeWindow.WorkArea(_hWnd);
        int workW = work.right - work.left;
        int workH = work.bottom - work.top;
        int winW = Math.Max(1, window.right - window.left);
        int winH = Math.Max(1, window.bottom - window.top);
        // Audit du 30/10, M2 : une taille suggérée qui ne tient pas repart de la taille prévue au
        // nouveau DPI, bornée comme à la création ; celle qui tient reste celle de l'utilisateur.
        int maxW = (int)(workW * WindowSizing.MaxWorkAreaFraction);
        int maxH = (int)(workH * WindowSizing.MaxWorkAreaFraction);
        if (workW > 0 && workH > 0 && (winW > maxW || winH > maxH))
            (winW, winH) = ScrollingWindowSize(D(BASE_WIN_W), D(BASE_WIN_H), workW, workH);

        // Recentrer plutot que garder le coin : une fenetre retrecie qui garde son coin
        // superieur gauche laisse tout le retrecissement du meme cote.
        int x = work.left + Math.Max(0, (workW - winW) / 2);
        int y = work.top + Math.Max(0, (workH - winH) / 2);
        Win32.MoveWindow(_hWnd, x, y, winW, winH, true);
    }

    /// <summary><see cref="WindowSizing.ScrollingWindowSize"/> avec les barres au DPI de rendu,
    /// qui peut devancer celui de la fenêtre (création, WM_DPICHANGED).</summary>
    private (int Width, int Height) ScrollingWindowSize(int wantedW, int wantedH, int workW, int workH)
    {
        var (vBar, hBar) = NativeWindow.ScrollBarsForDpi((int)MathF.Round(_dpiScale * 96f));
        return WindowSizing.ScrollingWindowSize(wantedW, wantedH, workW, workH, vBar, hBar);
    }

    private void CaptureBaseWindowMetrics()
    {
        // Le cadre non client se mesure toujours sur la fenêtre réelle.
        if (Win32.GetClientRect(_hWnd, out var client) && Win32.GetWindowRect(_hWnd, out var window))
        {
            // Les barres de défilement affichées sont de la zone cliente, pas du cadre (M2).
            int clientW = Math.Max(1, client.right - client.left) + (_scrollsY ? NativeWindow.ScrollBarWidth(_hWnd) : 0);
            int clientH = Math.Max(1, client.bottom - client.top) + (_scrollsX ? NativeWindow.ScrollBarHeight(_hWnd) : 0);
            _nonClientW = Math.Max(0, Math.Max(1, window.right - window.left) - clientW);
            _nonClientH = Math.Max(0, Math.Max(1, window.bottom - window.top) - clientH);
        }

        // Audit 24/09 B3 : la référence est la zone client PRÉVUE au DPI courant, pas celle
        // qu'on mesure. AG130-42 plafonne la fenêtre avant cette mesure : prendre la zone
        // client déjà réduite donnait une échelle de 1 dans une fenêtre trop basse, et le
        // clavier (ancré en bas) recouvrait la ligne cible et la saisie à 150-175 % sur 1080p.
        (_baseClientW, _baseClientH) = ReferenceClientSize(D(BASE_WIN_W), D(BASE_WIN_H), _nonClientW, _nonClientH);
    }

    /// <summary>
    /// Audit 24/09 B3 : zone client de référence = taille de fenêtre prévue moins le cadre.
    /// Hors plafond, elle vaut la zone client mesurée (échelle 1, comme avant) ; plafonnée,
    /// elle reste plus grande et <see cref="RenderScale"/> descend sous 1.
    /// </summary>
    internal static (int Width, int Height) ReferenceClientSize(int designOuterW, int designOuterH, int nonClientW, int nonClientH)
        => (Math.Max(1, designOuterW - Math.Max(0, nonClientW)), Math.Max(1, designOuterH - Math.Max(0, nonClientH)));

    /// <summary>Échelle de rendu : le plus petit des deux rapports zone client / référence, jamais
    /// sous 1 (audit du 30/10, M2) : une fenêtre plus petite que la référence défile
    /// (<see cref="WindowSizing.FitCanvas"/>) au lieu de rendre le texte sous l'échelle de l'écran.</summary>
    internal static float RenderScale(int clientW, int clientH, int baseClientW, int baseClientH)
        => WindowSizing.FitCanvas(clientW, clientH, baseClientW, baseClientH, 0, 0).Scale;

    /// <summary>Le rapport de forme ne se tient qu'au-delà de la référence, là où le rendu grandit
    /// avec la fenêtre ; en deçà, la fenêtre défile (audit du 30/10, M2).</summary>
    internal static bool LocksAspect(int clientW, int clientH, int baseClientW, int baseClientH)
        => clientW >= baseClientW && clientH >= baseClientH;

    private void UpdateRenderScaleFromCurrentClient(bool force = false)
    {
        if (_baseClientW <= 0 || _baseClientH <= 0) return;
        if (!Win32.GetClientRect(_hWnd, out var client)) return;

        // GetClientRect retire les barres déjà affichées : FitCanvas veut la zone entière.
        int vBar = NativeWindow.ScrollBarWidth(_hWnd);
        int hBar = NativeWindow.ScrollBarHeight(_hWnd);
        int clientW = Math.Max(1, client.right - client.left) + (_scrollsY ? vBar : 0);
        int clientH = Math.Max(1, client.bottom - client.top) + (_scrollsX ? hBar : 0);
        var fit = WindowSizing.FitCanvas(clientW, clientH, _baseClientW, _baseClientH, vBar, hBar);
        ApplyCanvas(fit);
        if (!force && MathF.Abs(fit.Scale - _windowScale) < 0.01f) return;

        _windowScale = fit.Scale;
        RecreateScaledFonts();
    }

    /// <summary>Surface, barres et positions de défilement d'après <paramref name="fit"/>.
    /// Afficher une barre change la zone cliente et renvoie WM_SIZE : le drapeau l'ignore, le
    /// calcul tenant déjà compte des barres.</summary>
    private void ApplyCanvas(WindowSizing.ScrolledCanvas fit)
    {
        _canvasW = fit.CanvasWidth;
        _canvasH = fit.CanvasHeight;
        _applyingCanvas = true;
        try
        {
            if (_scrollsX != fit.ScrollsX) Win32.ShowScrollBar(_hWnd, Win32.SB_HORZ, fit.ScrollsX);
            if (_scrollsY != fit.ScrollsY) Win32.ShowScrollBar(_hWnd, Win32.SB_VERT, fit.ScrollsY);
            _scrollsX = fit.ScrollsX;
            _scrollsY = fit.ScrollsY;
        }
        finally
        {
            _applyingCanvas = false;
        }
        _scrollX = WindowSizing.ClampScroll(_scrollX, _canvasW, fit.ViewWidth);
        _scrollY = WindowSizing.ClampScroll(_scrollY, _canvasH, fit.ViewHeight);
        UpdateScrollBars();
    }

    /// <summary>Zone visible, en pixels clients (barres exclues).</summary>
    private (int Width, int Height) ViewSize()
    {
        if (!Win32.GetClientRect(_hWnd, out var rc)) return (Math.Max(1, _canvasW), Math.Max(1, _canvasH));
        return (Math.Max(1, rc.right - rc.left), Math.Max(1, rc.bottom - rc.top));
    }

    private void UpdateScrollBars()
    {
        var (viewW, viewH) = ViewSize();
        if (_scrollsX) SetScrollBar(Win32.SB_HORZ, _canvasW, viewW, _scrollX);
        if (_scrollsY) SetScrollBar(Win32.SB_VERT, _canvasH, viewH, _scrollY);
    }

    private void SetScrollBar(int bar, int canvas, int view, int position)
    {
        var si = new Win32.SCROLLINFO
        {
            cbSize = (uint)Marshal.SizeOf<Win32.SCROLLINFO>(),
            fMask = Win32.SIF_RANGE | Win32.SIF_PAGE | Win32.SIF_POS,
            nMin = 0,
            nMax = Math.Max(0, canvas - 1),
            nPage = (uint)Math.Max(1, view),
            nPos = position
        };
        Win32.SetScrollInfo(_hWnd, bar, ref si, true);
    }

    /// <summary>Défile jusqu'à (<paramref name="x"/>, <paramref name="y"/>), bornés ; rend vrai
    /// quand la vue a bougé.</summary>
    private bool ScrollTo(int x, int y)
    {
        var (viewW, viewH) = ViewSize();
        x = _scrollsX ? WindowSizing.ClampScroll(x, _canvasW, viewW) : 0;
        y = _scrollsY ? WindowSizing.ClampScroll(y, _canvasH, viewH) : 0;
        if (x == _scrollX && y == _scrollY) return false;
        _scrollX = x;
        _scrollY = y;
        UpdateScrollBars();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        return true;
    }

    /// <summary>WM_VSCROLL et WM_HSCROLL : flèches de la barre, page, glissé du curseur.</summary>
    private void OnScrollBar(int bar, IntPtr wParam)
    {
        bool vertical = bar == Win32.SB_VERT;
        var (viewW, viewH) = ViewSize();
        int view = vertical ? viewH : viewW;
        int position = vertical ? _scrollY : _scrollX;
        int line = S(40);
        int page = Math.Max(line, view - line);
        int target = position;
        switch (wParam.ToInt32() & 0xFFFF)
        {
            case Win32.SB_LINEUP: target -= line; break;
            case Win32.SB_LINEDOWN: target += line; break;
            case Win32.SB_PAGEUP: target -= page; break;
            case Win32.SB_PAGEDOWN: target += page; break;
            case Win32.SB_TOP: target = 0; break;
            case Win32.SB_BOTTOM: target = int.MaxValue / 2; break;
            case Win32.SB_THUMBTRACK:
            {
                // Position sur 32 bits dans SCROLLINFO, jamais le mot haut de wParam (16 bits).
                var si = new Win32.SCROLLINFO
                {
                    cbSize = (uint)Marshal.SizeOf<Win32.SCROLLINFO>(),
                    fMask = Win32.SIF_TRACKPOS
                };
                if (Win32.GetScrollInfo(_hWnd, bar, ref si))
                    target = si.nTrackPos;
                break;
            }
        }
        if (vertical) ScrollTo(_scrollX, target);
        else ScrollTo(target, _scrollY);
    }

    /// <summary>Molette : trois lignes par cran ; Maj + molette, ou la molette horizontale,
    /// défilent en largeur. Rend faux quand rien ne défile sur cet axe.</summary>
    private bool OnWheel(IntPtr wParam, bool horizontal)
    {
        int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
        bool shift = (wParam.ToInt64() & 0x0004) != 0; // MK_SHIFT
        if (!horizontal && shift) { horizontal = true; delta = -delta; }
        int line = S(40);
        if (horizontal)
        {
            if (!_scrollsX) return false;
            // WM_MOUSEHWHEEL : positif vers la droite, à l'inverse de la molette verticale.
            ScrollTo(_scrollX + _hWheel.ConsumeWheel(delta) * line * 3, _scrollY);
            return true;
        }
        if (!_scrollsY) return false;
        ScrollTo(_scrollX, _scrollY - _wheel.ConsumeWheel(delta) * line * 3);
        return true;
    }

    /// <summary>Point client (souris) vers la surface dessinée.</summary>
    private (int X, int Y) ClientToCanvas(IntPtr lParam)
    {
        int x = unchecked((short)((long)lParam & 0xFFFF));
        int y = unchecked((short)(((long)lParam >> 16) & 0xFFFF));
        return (x + _scrollX, y + _scrollY);
    }

    /// <summary>
    /// Ce qui vient de prendre le focus au clavier, ou le caret qui vient de bouger, doit se
    /// voir (WCAG 2.4.7) : Tab vers un bouton sous le pli, frappe en bas de la surface. Seulement
    /// sur un changement, sinon chaque repeint ramènerait la vue que la molette vient de quitter.
    /// </summary>
    private void KeepFocusVisible(int viewW, int viewH)
    {
        if (!_scrollsX && !_scrollsY) return;
        Win32.RECT? target = null;
        if (_focusedActionIndex >= 0 && _focusedActionIndex < _clickActions.Count)
        {
            if (_focusedActionIndex != _lastShownFocus)
                target = _clickActions[_focusedActionIndex].Rect;
        }
        else if (_systemCaretRect is { } caret && !SameRect(_lastShownCaret, caret))
        {
            target = caret;
        }
        _lastShownFocus = _focusedActionIndex;
        _lastShownCaret = _systemCaretRect;
        if (target is not { } r) return;

        int margin = S(16);
        int x = _scrollsX ? WindowSizing.ScrollToShow(_scrollX, r.left, r.right, viewW, _canvasW, margin) : 0;
        int y = _scrollsY ? WindowSizing.ScrollToShow(_scrollY, r.top, r.bottom, viewH, _canvasH, margin) : 0;
        if (x != _scrollX || y != _scrollY)
        {
            _scrollX = x;
            _scrollY = y;
            UpdateScrollBars();
        }
    }

    private static bool SameRect(Win32.RECT? a, Win32.RECT b)
        => a is { } r && r.left == b.left && r.top == b.top && r.right == b.right && r.bottom == b.bottom;

    private void ConstrainSizingRect(int edge, ref Win32.RECT rect)
    {
        if (_baseClientW <= 0 || _baseClientH <= 0) return;

        double ratio = _baseClientW / (double)_baseClientH;
        int clientW = Math.Max(1, rect.right - rect.left - _nonClientW);
        int clientH = Math.Max(1, rect.bottom - rect.top - _nonClientH);
        if (!LocksAspect(clientW, clientH, _baseClientW, _baseClientH))
        {
            // Audit du 30/10, M2 : sous la référence, le contenu défile à l'échelle de l'écran ;
            // tenir le rapport rétrécirait aussi la dimension qui tient. Seul le minimum reste.
            var (minW, minH) = MinimumClientSize(_hWnd, ratio);
            if (clientW < minW)
            {
                if (edge is WMSZ_LEFT or WMSZ_TOPLEFT or WMSZ_BOTTOMLEFT) rect.left = rect.right - minW - _nonClientW;
                else rect.right = rect.left + minW + _nonClientW;
            }
            if (clientH < minH)
            {
                if (edge is WMSZ_TOP or WMSZ_TOPLEFT or WMSZ_TOPRIGHT) rect.top = rect.bottom - minH - _nonClientH;
                else rect.bottom = rect.top + minH + _nonClientH;
            }
            return;
        }
        bool widthDriven = edge switch
        {
            WMSZ_LEFT or WMSZ_RIGHT => true,
            WMSZ_TOP or WMSZ_BOTTOM => false,
            _ => clientW / (double)clientH >= ratio
        };

        int newClientW;
        int newClientH;
        if (widthDriven)
        {
            newClientW = clientW;
            newClientH = (int)Math.Round(newClientW / ratio);
        }
        else
        {
            newClientH = clientH;
            newClientW = (int)Math.Round(newClientH * ratio);
        }

        EnforceMinimumClientSize(ref newClientW, ref newClientH, ratio);
        int newOuterW = newClientW + _nonClientW;
        int newOuterH = newClientH + _nonClientH;

        if (edge is WMSZ_LEFT or WMSZ_TOPLEFT or WMSZ_BOTTOMLEFT)
            rect.left = rect.right - newOuterW;
        else
            rect.right = rect.left + newOuterW;

        if (edge is WMSZ_TOP or WMSZ_TOPLEFT or WMSZ_TOPRIGHT)
            rect.top = rect.bottom - newOuterH;
        else
            rect.bottom = rect.top + newOuterH;
    }

    /// <summary>
    /// D3 (accessibilité 1.3.0) : minimum de zone client, borné à la zone de travail de l'écran
    /// de la fenêtre (<see cref="WindowSizing.MinimumClientSize"/>).
    /// </summary>
    private (int Width, int Height) MinimumClientSize(IntPtr hwnd, double ratio)
    {
        // Même zone que la création (simulée au banc de capture) : sinon le minimum d'un vrai
        // écran plus grand annulerait la taille de l'écran simulé.
        var work = NativeWindow.WorkArea(hwnd);
        int workW = work.right - work.left;
        int workH = work.bottom - work.top;
        return WindowSizing.MinimumClientSize(D(BASE_MIN_W), D(BASE_MIN_H), ratio,
            _nonClientW, _nonClientH, workW, workH);
    }

    private void EnforceMinimumClientSize(ref int clientW, ref int clientH, double ratio)
    {
        // D3 : le même minimum borné que WM_GETMINMAXINFO, sans quoi tirer un bord le
        // repousserait hors de l'écran.
        var (minW, minH) = MinimumClientSize(_hWnd, ratio);
        if (clientW < minW)
        {
            clientW = minW;
            clientH = (int)Math.Round(clientW / ratio);
        }
        if (clientH < minH)
        {
            clientH = minH;
            clientW = (int)Math.Round(clientH * ratio);
        }
    }

    private void BeginLiveResize()
    {
        _liveResizing = true;
        CaptureResizeSnapshot();
    }

    private void EndLiveResize()
    {
        _liveResizing = false;
        DisposeResizeSnapshot();
        UpdateRenderScaleFromCurrentClient(force: true);
        SaveWindowBounds();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    private void CaptureResizeSnapshot()
    {
        DisposeResizeSnapshot();
        if (!Win32.GetClientRect(_hWnd, out var rc)) return;

        int width = Math.Max(1, rc.right - rc.left);
        int height = Math.Max(1, rc.bottom - rc.top);
        IntPtr hdc = Win32.GetDC(_hWnd);
        if (hdc == IntPtr.Zero) return;

        IntPtr memDc = IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero;
        try
        {
            memDc = Win32.CreateCompatibleDC(hdc);
            _resizeSnapshotBitmap = Win32.CreateCompatibleBitmap(hdc, width, height);
            if (memDc == IntPtr.Zero || _resizeSnapshotBitmap == IntPtr.Zero)
            {
                DisposeResizeSnapshot();
                return;
            }

            oldBitmap = Win32.SelectObject(memDc, _resizeSnapshotBitmap);
            RenderView(memDc, width, height, keepFocusVisible: false);
            _resizeSnapshotW = width;
            _resizeSnapshotH = height;
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero && memDc != IntPtr.Zero)
                Win32.SelectObject(memDc, oldBitmap);
            if (memDc != IntPtr.Zero)
                Win32.DeleteDC(memDc);
            Win32.ReleaseDC(_hWnd, hdc);
        }
    }

    private void DisposeResizeSnapshot()
    {
        DeleteObject(ref _resizeSnapshotBitmap);
        _resizeSnapshotW = 0;
        _resizeSnapshotH = 0;
    }

    private bool DrawLiveResizeSnapshot(IntPtr hdc, Win32.RECT rc)
    {
        if (_resizeSnapshotBitmap == IntPtr.Zero || _resizeSnapshotW <= 0 || _resizeSnapshotH <= 0)
            return false;

        int width = Math.Max(1, rc.right - rc.left);
        int height = Math.Max(1, rc.bottom - rc.top);
        IntPtr memDc = Win32.CreateCompatibleDC(hdc);
        if (memDc == IntPtr.Zero) return false;

        IntPtr oldBitmap = IntPtr.Zero;
        try
        {
            oldBitmap = Win32.SelectObject(memDc, _resizeSnapshotBitmap);
            Win32.SetStretchBltMode(hdc, Win32.HALFTONE);
            return Win32.StretchBlt(hdc, 0, 0, width, height, memDc, 0, 0, _resizeSnapshotW, _resizeSnapshotH, Win32.SRCCOPY);
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero)
                Win32.SelectObject(memDc, oldBitmap);
            Win32.DeleteDC(memDc);
        }
    }

    private void PickInitialSelection()
    {
        if (_progress.LastModuleId != null && _progress.LastLessonId != null)
        {
            if (SelectExercise(_progress.LastModuleId, _progress.LastLessonId, _progress.LastExerciseIndex, savePosition: false))
                return;
        }

        foreach (var module in _catalog.Modules.Select((Module, Index) => (Module, Index)))
        {
            foreach (var lesson in module.Module.Lessons.Select((Lesson, Index) => (Lesson, Index)))
            {
                foreach (var exercise in lesson.Lesson.Exercises.Select((Exercise, Index) => (Exercise, Index)))
                {
                    if (!_progress.IsCompleted(exercise.Exercise))
                    {
                        _moduleIndex = module.Index;
                        _lessonIndex = lesson.Index;
                        _exerciseIndex = exercise.Index;
                        return;
                    }
                }
            }
        }
    }

    private bool SelectExercise(string moduleId, string lessonId, int exerciseIndex, bool savePosition)
    {
        for (int m = 0; m < _catalog.Modules.Count; m++)
        {
            if (!StringComparer.Ordinal.Equals(_catalog.Modules[m].Id, moduleId)) continue;
            for (int l = 0; l < _catalog.Modules[m].Lessons.Count; l++)
            {
                if (!StringComparer.Ordinal.Equals(_catalog.Modules[m].Lessons[l].Id, lessonId)) continue;
                if (exerciseIndex < 0 || exerciseIndex >= _catalog.Modules[m].Lessons[l].Exercises.Count)
                    return false;
                HomeClearComposed(); // accueil des Leçons : l'exercice du catalogue reprend la main
                _homePage = HomePage.Exercise;
                _moduleIndex = m;
                _lessonIndex = l;
                _exerciseIndex = exerciseIndex;
                StartCurrentSession(savePosition);
                return true;
            }
        }
        return false;
    }

    private void StartCurrentSession(bool savePosition)
    {
        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_FREE_STATS);
        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_HINT_CLEAR);
        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_HINT_FLASH_CLEAR);
        _session = new LessonTypingSession(CurrentExercise);
        _errorScancode = 0;
        _showSummary = false;
        _lastCompletedStats = null;
        _challenge.ClearShare();
        _hintCharacter = null;
        _hintMethod = null;
        _hintBackspace = false;
        _hintButtonActive = false;
        ClearPendingPhysicalText();
        if (savePosition && _homeComposed == null) // une séance composée n'est pas une position du catalogue
            _progress.SetLastPosition(CurrentExercise);
        UpdateAutoHintIfEnabled();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (msg)
            {
                case Win32.WM_PAINT:
                    OnPaint();
                    return IntPtr.Zero;
                case Win32.WM_ERASEBKGND:
                    return (IntPtr)1;
                case Win32.WM_ENTERSIZEMOVE:
                    BeginLiveResize();
                    return IntPtr.Zero;
                case Win32.WM_EXITSIZEMOVE:
                    EndLiveResize();
                    return IntPtr.Zero;
                case Win32.WM_SIZING:
                    var sizingRect = Marshal.PtrToStructure<Win32.RECT>(lParam);
                    ConstrainSizingRect(wParam.ToInt32(), ref sizingRect);
                    Marshal.StructureToPtr(sizingRect, lParam, false);
                    return (IntPtr)1;
                case Win32.WM_VSCROLL:
                    OnScrollBar(Win32.SB_VERT, wParam);
                    return IntPtr.Zero;
                case Win32.WM_HSCROLL:
                    OnScrollBar(Win32.SB_HORZ, wParam);
                    return IntPtr.Zero;
                case Win32.WM_MOUSEWHEEL:
                    if (OnWheel(wParam, horizontal: false)) return IntPtr.Zero;
                    break;
                case Win32.WM_MOUSEHWHEEL:
                    if (OnWheel(wParam, horizontal: true)) return IntPtr.Zero;
                    break;
                case Win32.WM_SIZE:
                    // Une barre de défilement qui apparaît ou disparaît (ApplyCanvas) : déjà comptée.
                    if (_applyingCanvas)
                        return IntPtr.Zero;
                    if (!_liveResizing)
                    {
                        UpdateRenderScaleFromCurrentClient();
                        if (_visible)
                            SaveWindowBounds();
                    }
                    Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
                // AG130-42 : les neuf autres fenetres traitaient deja ce message ; celle-ci
                // gardait les polices du DPI de son ecran d'origine en changeant d'ecran.
                // A14 : contraste élevé ou couleurs système changés, fenêtre ouverte.
                case 0x0015: // WM_SYSCOLORCHANGE
                case 0x001A: // WM_SETTINGCHANGE
                {
                    var palette = HomePaletteNow();
                    if (palette != _pal)
                    {
                        _pal = palette;
                        NativeWindow.ApplyClassBackground(hWnd, CLR_BG);
                        Win32.InvalidateRect(hWnd, IntPtr.Zero, false);
                    }
                    break;
                }

                case Win32.WM_DPICHANGED:
                {
                    int newDpi = (wParam.ToInt32() >> 16) & 0xFFFF;
                    if (newDpi > 0) _dpiScale = newDpi / 96f;
                    RecreateScaledFonts();

                    var suggested = Marshal.PtrToStructure<Win32.RECT>(lParam);
                    Win32.MoveWindow(_hWnd, suggested.left, suggested.top,
                        suggested.right - suggested.left, suggested.bottom - suggested.top, true);

                    // Puis remesurer : la taille suggeree peut deborder l'ecran d'arrivee.
                    FitWindowToWorkArea();
                    CaptureBaseWindowMetrics();
                    UpdateRenderScaleFromCurrentClient(force: true);
                    Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
                }

                case Win32.WM_GETMINMAXINFO:
                    var mmi = Marshal.PtrToStructure<Win32.MINMAXINFO>(lParam);
                    // D3 (accessibilité 1.3.0) : minimum borné à la zone de travail. Windows
                    // l'applique aussi à CreateWindowEx et MoveWindow (mesuré le 2026-09-23) ;
                    // non borné, il dépassait l'écran dès 175 % sur un 1920 × 1080 et annulait
                    // le plafond d'AG130-42. Avant la création, aucun rapport n'est mesuré :
                    // aucun n'est imposé, comme avant.
                    double minRatio = _baseClientW > 0 && _baseClientH > 0
                        ? _baseClientW / (double)_baseClientH
                        : 0;
                    var (minClientW, minClientH) = MinimumClientSize(hWnd, minRatio);
                    mmi.ptMinTrackSize.x = minClientW + _nonClientW;
                    mmi.ptMinTrackSize.y = minClientH + _nonClientH;
                    Marshal.StructureToPtr(mmi, lParam, false);
                    return IntPtr.Zero;
                case Win32.WM_SETFOCUS:
                    // Ce qui reste dans la file date d'avant : frappe traitée par le hook juste
                    // avant la perte du focus, dont le WM_CHAR est parti ailleurs. Au retour, il
                    // serait pris pour les premiers caractères tapés ici (moins d'une seconde).
                    _hasFocus = true;
                    ClearPendingPhysicalText();
                    Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
                    return IntPtr.Zero;
                case Win32.WM_KILLFOCUS:
                    _hasFocus = false;
                    DestroySystemCaret(); // avant que la fenêtre suivante crée le sien
                    break; // traitement par défaut, comme avant
                case Win32.WM_LBUTTONDOWN:
                    _focusCuesShown = false;
                    if (OnFieldMouseDown(lParam, wParam))
                        return IntPtr.Zero;
                    OnButtonMouseDown(lParam);
                    break;
                case Win32.WM_LBUTTONUP:
                    _focusCuesShown = false;
                    if (_fieldDragging)
                    {
                        EndFieldDrag(); // fin d'une sélection à la souris dans un champ
                        return IntPtr.Zero;
                    }
                    OnButtonMouseUp(lParam);
                    return IntPtr.Zero;
                case WM_CAPTURECHANGED:
                    _fieldDragging = false;
                    _clickPressed = false; // capture perdue (Alt+Tab, menu) : le clic est annulé
                    break;
                case Win32.WM_LBUTTONDBLCLK:
                    OnDoubleClick(lParam);
                    OnButtonMouseDown(lParam); // le second relâchement reste un clic, comme avant
                    return IntPtr.Zero;
                case Win32.WM_MOUSEMOVE:
                    if (_fieldDragging)
                    {
                        OnFieldMouseDrag(lParam);
                        return IntPtr.Zero;
                    }
                    OnMouseMove(lParam);
                    return IntPtr.Zero;
                case Win32.WM_MOUSELEAVE:
                    OnMouseLeave();
                    return IntPtr.Zero;
                case Win32.WM_SETCURSOR:
                    // #10 : main sur une cible cliquable, comme un lien ou un bouton web.
                    if ((lParam.ToInt64() & 0xFFFF) == HTCLIENT && CursorSource.TryGet(out var cursor)
                        && Win32.ScreenToClient(_hWnd, ref cursor)
                        && HitClickAction(cursor.x + _scrollX, cursor.y + _scrollY) >= 0)
                    {
                        Win32.SetCursor(Win32.LoadCursorW(IntPtr.Zero, (IntPtr)IDC_HAND));
                        return (IntPtr)1;
                    }
                    break;
                case Win32.WM_KEYDOWN:
                    if (_inputPaused && BlockedWhilePaused(wParam.ToInt32(), _focusedActionIndex >= 0, SpaceActivatesButtons))
                        return IntPtr.Zero;
                    OnKeyDown(wParam.ToInt32());
                    return IntPtr.Zero;
                case Win32.WM_CHAR:
                    if (_inputPaused)
                        return IntPtr.Zero;
                    OnChar((char)wParam.ToInt32());
                    return IntPtr.Zero;
                case Win32.WM_SYSCHAR:
                    if (_inputPaused)
                        return IntPtr.Zero;
                    // Alt Gr ou texte attendu au keydown : c'est une frappe. Sinon Alt+touche est
                    // un raccourci : Alt+Espace ouvre le menu système, le reste ne tape rien
                    // (audit 09/11, E2 : Alt+lettre entrait dans l'exercice).
                    if (_mapper.AltGrDown || _pendingPhysicalText.HasPending)
                    {
                        OnChar((char)wParam.ToInt32());
                        return IntPtr.Zero;
                    }
                    if (wParam.ToInt32() == ' ')
                        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
                    return IntPtr.Zero;
                case Win32.WM_SYSDEADCHAR:
                    return IntPtr.Zero;
                case Win32.WM_SYSKEYDOWN:
                {
                    int vk = wParam.ToInt32();
                    if (vk == 0x73) // VK_F4, preserve Alt+F4.
                        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
                    if (_inputPaused)
                        return IntPtr.Zero;
                    // Alt+Espace hors Alt Gr : menu système, jamais l'activation d'un bouton.
                    if (vk == 0x20 && !_mapper.AltGrDown)
                        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
                    OnKeyDown(vk);
                    return IntPtr.Zero;
                }
                case Win32.WM_SYSKEYUP:
                    return IntPtr.Zero;
                case Win32.WM_PASTE:
                case Win32.WM_CUT:
                case Win32.WM_CLEAR:
                    return IntPtr.Zero;
                case Win32.WM_TIMER:
                    OnTimer((uint)wParam.ToInt64());
                    return IntPtr.Zero;
                case Win32.WM_CLOSE:
                    Hide();
                    return IntPtr.Zero;
                case Win32.WM_GETOBJECT:
                    if (unchecked((int)(long)lParam) != LessonsAutomation.UiaRootObjectId)
                        break; // MSAA et les autres objets : traitement par défaut
                    _automation ??= new LessonsAutomation(this);
                    return _automation.OnGetObject(wParam, lParam);
                case WM_APP_AUTOMATION:
                    _automationEventPending = false;
                    _automation?.RaiseChanges();
                    return IntPtr.Zero;
                case WM_APP_AUTOMATION_INVOKE:
                    RunPendingInvoke();
                    return IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            ConfigManager.Log("LessonsWindow.WndProc", ex);
        }

        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void OnPaint()
    {
        var hdc = Win32.BeginPaint(_hWnd, out var ps);
        try
        {
            Win32.GetClientRect(_hWnd, out var rc);
            int width = Math.Max(1, rc.right - rc.left);
            int height = Math.Max(1, rc.bottom - rc.top);
            if (_liveResizing && DrawLiveResizeSnapshot(hdc, rc))
                return;

            RenderView(hdc, width, height, keepFocusVisible: true);
        }
        finally
        {
            Win32.EndPaint(_hWnd, ref ps);
        }
        // Après EndPaint : il rappelle ShowCaret pour le caret que BeginPaint a caché, et
        // rendrait visible un caret créé entre les deux (vu au banc le 01/10).
        PlaceSystemCaret();
        _automation?.OnPainted();
        RequestAutomationEvents();
    }

    /// <summary>
    /// Audit du 30/10, M2 — la page entière se dessine sur une surface de la taille de la
    /// référence (<see cref="WindowSizing.FitCanvas"/>), puis la partie visible est copiée dans
    /// <paramref name="target"/>. La mise en page, les zones cliquables et l'UI Automation
    /// restent en coordonnées de la surface ; seule la copie tient compte du défilement.
    /// </summary>
    private void RenderView(IntPtr target, int width, int height, bool keepFocusVisible)
    {
        int canvasW = Math.Max(width, _canvasW);
        int canvasH = Math.Max(height, _canvasH);
        _scrollX = _scrollsX ? WindowSizing.ClampScroll(_scrollX, canvasW, width) : 0;
        _scrollY = _scrollsY ? WindowSizing.ClampScroll(_scrollY, canvasH, height) : 0;
        var canvas = new Win32.RECT { left = 0, top = 0, right = canvasW, bottom = canvasH };

        IntPtr memDc = Win32.CreateCompatibleDC(target);
        IntPtr memBitmap = memDc != IntPtr.Zero ? Win32.CreateCompatibleBitmap(target, canvasW, canvasH) : IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero;
        try
        {
            if (memDc == IntPtr.Zero || memBitmap == IntPtr.Zero)
            {
                // Mémoire refusée : directement à l'écran, origine décalée du défilement.
                SetVisibleCanvas(width, height);
                Win32.SetViewportOrgEx(target, -_scrollX, -_scrollY, IntPtr.Zero);
                DrawWindowContents(target, canvas);
                Win32.SetViewportOrgEx(target, 0, 0, IntPtr.Zero);
                return;
            }

            oldBitmap = Win32.SelectObject(memDc, memBitmap);
            SetVisibleCanvas(width, height);
            DrawWindowContents(memDc, canvas);
            if (keepFocusVisible)
            {
                int beforeX = _scrollX, beforeY = _scrollY;
                KeepFocusVisible(width, height);
                if (beforeX != _scrollX || beforeY != _scrollY)
                {
                    // Les infobulles se bornent à la vue : la redessiner à sa nouvelle place.
                    SetVisibleCanvas(width, height);
                    DrawWindowContents(memDc, canvas);
                }
            }
            Win32.BitBlt(target, 0, 0, width, height, memDc, _scrollX, _scrollY, Win32.SRCCOPY);
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero)
                Win32.SelectObject(memDc, oldBitmap);
            if (memBitmap != IntPtr.Zero)
                Win32.DeleteObject(memBitmap);
            if (memDc != IntPtr.Zero)
                Win32.DeleteDC(memDc);
        }
    }

    /// <summary>Partie visible de la surface : les infobulles s'y bornent.</summary>
    private Win32.RECT _visibleCanvas;

    private void SetVisibleCanvas(int width, int height)
        => _visibleCanvas = new Win32.RECT { left = _scrollX, top = _scrollY, right = _scrollX + width, bottom = _scrollY + height };

    private void DrawWindowContents(IntPtr hdc, Win32.RECT rc)
    {
        GdiHelpers.FillSolidRect(hdc, rc, CLR_BG);
        Win32.SetBkMode(hdc, Win32.TRANSPARENT);
        SyncFocusWithScreen();
        _clickActions.Clear();
        _controlViews.Clear();
        _doubleClickActions.Clear();
        _hoverAreas.Clear();
        _keyboardHover = null;
        _systemCaretRect = null;
        _surfaceView = null;

        // Refonte 1.3.0 (maquette Leçons F) : en-tête d'une ligne, partition, clavier papier.
        // Le sélecteur ouvert se dessine en dernier, par-dessus, et capte seul les clics.
        var body = new Win32.RECT { left = S(24), top = S(76), right = rc.right - S(24), bottom = rc.bottom - S(20) };
        // Accueil des Leçons : la page « Aujourd'hui » a son propre en-tête, sa page et son anneau
        // de focus (LessonsWindow.Home.cs) ; le reste de la méthode sert l'écran d'exercice.
        bool home = HomeShown;
        if (home)
            DrawHome(hdc, rc);
        else
        {
            // L'en-tête d'abord : l'ordre de Tab suit celui de la page.
            DrawPaperHeader(hdc, rc);
            if (_settingsOpen)
                DrawPaperSettings(hdc, body);
            else if (_mode == WindowMode.Lessons)
            {
                if (!DrawTutorialPage(hdc, body)) // parcours de l'accueil : usages, fin (LessonsWindow.Tutorial.cs)
                    DrawPaperLesson(hdc, rc, body);
            }
            else
                DrawPaperFree(hdc, rc, body);
            if (_pickerOpen)
                DrawPaperPicker(hdc, rc);
        }

        if (!home)
            DrawFocusRing(hdc);
        // K4 (accessibilité 1.3.0) : l'infobulle de survol garde la priorité ; à défaut, celle
        // de la cible focalisée au clavier.
        // Bornées à la partie visible, pas à toute la surface (audit du 30/10, M2).
        var tooltipBounds = _visibleCanvas.right > _visibleCanvas.left ? _visibleCanvas : rc;
        if (string.IsNullOrEmpty(_hoverTooltip))
        {
            if (FocusCuesVisible)
                DrawFocusedTooltip(hdc, tooltipBounds);
        }
        else
            DrawHoverTooltip(hdc, tooltipBounds);
    }

    // Hors du repeint : un client UIA du même processus pourrait sinon y rentrer.
    private void RequestAutomationEvents()
    {
        if (_automation == null || _automationEventPending)
            return;
        _automationEventPending = true;
        Win32.PostMessageW(_hWnd, WM_APP_AUTOMATION, IntPtr.Zero, IntPtr.Zero);
    }

    LessonSurfaceView? ILessonsAutomationHost.SurfaceView => _surfaceView;

    bool ILessonsAutomationHost.WindowHasFocus => _hasFocus;

    bool ILessonsAutomationHost.SurfaceHasFocus => _hasFocus && _focusedActionIndex < 0;

    IReadOnlyList<LessonControlView> ILessonsAutomationHost.Controls => _controlViews;

    int ILessonsAutomationHost.FocusedControl => _focusedActionIndex;

    Win32.POINT ILessonsAutomationHost.ScrollOffset => new() { x = _scrollX, y = _scrollY };

    void ILessonsAutomationHost.FocusSurface() => FocusFromAutomation(-1);

    void ILessonsAutomationHost.FocusControl(int index) => FocusFromAutomation(index);

    private void FocusFromAutomation(int index)
    {
        _focusedActionIndex = index;
        _focusCuesShown = true; // un lecteur d'écran déplace le focus : le repère suit
        Win32.SetFocus(_hWnd);
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    void ILessonsAutomationHost.InvokeControl(int index, string name)
    {
        _pendingInvoke = (index, name);
        Win32.PostMessageW(_hWnd, WM_APP_AUTOMATION_INVOKE, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>Activation demandée par UIA, comme un clic sur le contrôle, s'il est toujours là.</summary>
    private void RunPendingInvoke()
    {
        if (_pendingInvoke is not { } pending)
            return;
        _pendingInvoke = null;
        var (index, name) = pending;
        if (index < _clickActions.Count && _controlViews[index].Name == name)
            _clickActions[index].Action();
    }

    private void PlaceSystemCaret()
    {
        if (!_hasFocus || _systemCaretRect is not { } caret)
        {
            DestroySystemCaret();
            return;
        }
        var size = (caret.right - caret.left, caret.bottom - caret.top);
        if (_systemCaretSize != size)
        {
            // Pas de ShowCaret : créé caché, il le reste.
            Win32.CreateCaret(_hWnd, IntPtr.Zero, size.Item1, size.Item2);
            _systemCaretSize = size;
        }
        // Le caret est en coordonnées de la surface ; la loupe suit la position cliente.
        Win32.SetCaretPos(caret.left - _scrollX, caret.top - _scrollY);
    }

    private void DestroySystemCaret()
    {
        if (_systemCaretSize == null)
            return;
        Win32.DestroyCaret();
        _systemCaretSize = null;
    }

    /// <summary>Abscisse du curseur de la ligne : l'origine plus la largeur des cases qui le
    /// précèdent.</summary>
    internal static int SurfaceCaretX(IReadOnlyList<int> widths, int cursor, int origin)
    {
        int x = origin;
        int end = Math.Clamp(cursor, 0, widths.Count);
        for (int i = 0; i < end; i++)
            x += widths[i];
        return x;
    }

    /// <summary>Trait de soulignement d'une case : la case moins les retraits, jamais moins de
    /// <paramref name="minWidth"/>, centré sur elle, pour que i, l et ' restent repérables.</summary>
    internal static (int Left, int Right) UnderlineSpan(int left, int right, int inset, int minWidth)
    {
        int width = Math.Max(right - left - 2 * inset, minWidth);
        int start = (left + right - width) / 2;
        return (start, start + width);
    }

    private static char? TypedCharacterAt(IReadOnlyList<LessonTypedCharacterSnapshot> typed, int index) =>
        index < typed.Count ? typed[index].Actual : null;

    /// <summary>
    /// L-06 : infobulle de la touche sous (<paramref name="x"/>, <paramref name="y"/>), telle
    /// que le repeint la déclarait : touches dans l'ordre de
    /// <see cref="KeyboardRenderer.BuildHitTestRects"/>, bornes incluses, touches sans texte
    /// ignorées. Fonction pure pour le témoin (LessonKeyboardTooltipTests).
    /// </summary>
    internal static bool TryGetKeyboardTooltip(Layout layout, Win32.RECT bounds, KeyboardRenderProfile profile,
        KeyboardRenderState state, int x, int y, out string tooltip, out Win32.RECT anchor)
        => TryGetKeyboardTooltip(layout, KeyboardRenderer.Place(bounds), profile, state, x, y, out tooltip, out anchor);

    /// <summary>Même recherche, pour un clavier placé à taille de touche fixe (clavier papier
    /// de la refonte 1.3.0, même géométrie que <see cref="KeyboardRenderer"/>).</summary>
    internal static bool TryGetKeyboardTooltip(Layout layout, KeyboardPlacement placement, KeyboardRenderProfile profile,
        KeyboardRenderState state, int x, int y, out string tooltip, out Win32.RECT anchor)
    {
        foreach (var hit in KeyboardRenderer.BuildHitTestRects(placement))
        {
            var rect = hit.Rect;
            if (x < rect.left || x > rect.right || y < rect.top || y > rect.bottom)
                continue;
            string text = KeyboardRenderer.BuildTooltipText(layout, profile, state, hit.Scancode, hit.Label);
            if (string.IsNullOrWhiteSpace(text))
                continue;
            tooltip = text;
            anchor = rect;
            return true;
        }
        tooltip = "";
        anchor = new Win32.RECT();
        return false;
    }

    private void DrawFreePreviewText(IntPtr hdc, Win32.RECT preview)
    {
        var lines = BuildFreeVisualLines(hdc, preview);
        int firstVisibleLine = GetFreeFirstVisibleLine(lines, preview);
        int left = FreeTextLeft(preview);
        int y = FreeTextTop(preview);
        int lineH = FreeLineHeight();
        string text = _free.Text;

        if (text.Length == 0)
        {
            PaperText.DrawLine(hdc, "...", FreeLineFont, new Win32.RECT { left = left, top = y, right = preview.right - S(12), bottom = y + lineH },
                CLR_MUTED);
        }
        else
        {
            // Sélection : fond de sélection sous la part choisie de chaque ligne, texte par-dessus ;
            // une fin de ligne sélectionnée se voit par un court bloc après la ligne.
            int selStart = _free.SelectionStart, selEnd = _free.SelectionEnd;
            uint selFill = _pal.HighContrast ? _pal.Action : PaperTheme.SelectionFill;
            uint selInk = _pal.HighContrast ? _pal.OnAction : CLR_TEXT;
            int right = preview.right - S(12);
            for (int i = firstVisibleLine; i < lines.Count; i++)
            {
                if (y + lineH > preview.bottom - S(8))
                    break;

                var line = lines[i];
                int lineEnd = line.Start + line.Length;
                int a = Math.Clamp(selStart, line.Start, lineEnd), b = Math.Clamp(selEnd, line.Start, lineEnd);
                bool breakSelected = _free.HasSelection && selStart <= lineEnd && selEnd > lineEnd &&
                                     lineEnd < text.Length && text[lineEnd] == '\n';
                int xa = left + FreeTextWidth(hdc, line.Start, a - line.Start);
                int xb = xa + FreeTextWidth(hdc, a, b - a);
                if (a < b || breakSelected)
                    GdiHelpers.FillSolidRect(hdc, new Win32.RECT { left = xa, top = y + S(3), right = xb + (breakSelected ? S(6) : 0), bottom = y + lineH - S(1) }, selFill);

                DrawFreeSegment(hdc, line.Start, a - line.Start, left, y, right, CLR_TEXT);
                DrawFreeSegment(hdc, a, b - a, xa, y, right, selInk);
                DrawFreeSegment(hdc, b, lineEnd - b, xb, y, right, CLR_TEXT);
                y += lineH;
            }
        }

        DrawFreeCaret(hdc, preview, lines);
    }

    private void DrawFreeSegment(IntPtr hdc, int start, int length, int x, int y, int right, uint color)
    {
        if (length <= 0 || x >= right) return;
        PaperText.DrawLine(hdc, _free.Text.Substring(start, length), FreeLineFont,
            new Win32.RECT { left = x, top = y, right = right, bottom = y + FreeLineHeight() }, color);
    }

    private void DrawFreeCaret(IntPtr hdc, Win32.RECT preview, IReadOnlyList<FreeVisualLine> lines)
    {
        int lineIndex = FindFreeLineIndex(lines, _free.Caret);
        int firstVisibleLine = GetFreeFirstVisibleLine(lines, preview);
        var line = lines[lineIndex];
        int offset = Math.Clamp(_free.Caret - line.Start, 0, line.Length);
        int x = FreeTextLeft(preview) + FreeTextWidth(hdc, line.Start, offset);
        int top = FreeTextTop(preview) + (lineIndex - firstVisibleLine) * FreeLineHeight() + S(4);
        int width = Math.Max(1, S(1));
        var caret = new Win32.RECT { left = x, top = top, right = x + width, bottom = top + S(18) };
        GdiHelpers.FillSolidRect(hdc, caret, CLR_CURRENT);
        _systemCaretRect = caret;
    }

    /// <summary>
    /// Lignes visuelles du champ : coupées aux fins de ligne, puis au dernier espace qui tient
    /// (le mot passe entier à la ligne, comme dans un champ Windows) ; un mot plus long que la
    /// ligne est coupé où il déborde. Les largeurs sont mesurées par grappe et gardées en cache :
    /// sans plafond de 220 caractères, chaque repeint ne doit plus coûter une mesure par lettre.
    /// </summary>
    private List<FreeVisualLine> BuildFreeVisualLines(IntPtr hdc, Win32.RECT preview)
    {
        var lines = new List<FreeVisualLine>();
        int maxWidth = Math.Max(1, preview.right - preview.left - S(24));
        string text = _free.Text;

        if (text.Length == 0)
        {
            lines.Add(new FreeVisualLine(0, 0, 0));
            return lines;
        }

        var starts = System.Globalization.StringInfo.ParseCombiningCharacters(text);
        int start = 0, width = 0;
        int breakAt = -1, widthAtBreak = 0; // après le dernier espace de la ligne
        for (int k = 0; k < starts.Length; k++)
        {
            int i = starts[k];
            int end = k + 1 < starts.Length ? starts[k + 1] : text.Length;
            if (text[i] == '\n')
            {
                lines.Add(new FreeVisualLine(start, i - start, width));
                start = end;
                width = 0;
                breakAt = -1;
                continue;
            }

            int w = MeasureFreeGrapheme(hdc, text, i, end - i);
            if (i > start && width + w > maxWidth && text[i] != ' ')
            {
                if (breakAt > start)
                {
                    lines.Add(new FreeVisualLine(start, breakAt - start, widthAtBreak));
                    width -= widthAtBreak;
                    start = breakAt;
                }
                else
                {
                    lines.Add(new FreeVisualLine(start, i - start, width));
                    start = i;
                    width = 0;
                }
                breakAt = -1;
            }

            width += w;
            if (text[i] == ' ')
            {
                breakAt = end;
                widthAtBreak = width;
            }
        }

        lines.Add(new FreeVisualLine(start, text.Length - start, width));
        return lines;
    }

    private int FindFreeLineIndex(IReadOnlyList<FreeVisualLine> lines, int cursorIndex)
    {
        int cursor = Math.Clamp(cursorIndex, 0, _free.Text.Length);
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            int lineEnd = line.Start + line.Length;
            if (cursor == line.Start ||
                (cursor > line.Start && cursor < lineEnd) ||
                (cursor == lineEnd && (i == lines.Count - 1 || lines[i + 1].Start > cursor)))
                return i;
        }
        return Math.Max(0, lines.Count - 1);
    }

    private int GetFreeVisibleLineCapacity(Win32.RECT preview)
    {
        int availableH = Math.Max(1, preview.bottom - S(8) - FreeTextTop(preview));
        return Math.Max(1, availableH / Math.Max(1, FreeLineHeight()));
    }

    private int GetFreeFirstVisibleLine(IReadOnlyList<FreeVisualLine> lines, Win32.RECT preview)
    {
        int visibleLineCount = GetFreeVisibleLineCapacity(preview);
        int cursorLine = FindFreeLineIndex(lines, _free.Caret);
        int maxFirstLine = Math.Max(0, lines.Count - visibleLineCount);
        if (cursorLine < visibleLineCount)
            return 0;
        return Math.Clamp(cursorLine - visibleLineCount + 1, 0, maxFirstLine);
    }

    /// <summary>Index de la limite de grappe la plus proche de l'abscisse, dans la ligne.</summary>
    private int FindFreeIndexAtX(IntPtr hdc, FreeVisualLine line, int x)
    {
        if (line.Length <= 0)
            return line.Start;

        string text = _free.Text;
        int width = 0;
        int i = line.Start, end = line.Start + line.Length;
        while (i < end)
        {
            int next = Math.Min(end, _free.NextBoundary(i));
            int w = MeasureFreeGrapheme(hdc, text, i, next - i);
            if (x < width + w / 2)
                return i;
            width += w;
            i = next;
        }
        // Après une coupure au dernier espace, le curseur se pose avant cet espace : sur la ligne.
        return end > line.Start && end < text.Length && text[end - 1] == ' ' ? end - 1 : end;
    }

    /// <summary>Index du texte sous un point du champ (coordonnées du canevas).</summary>
    private int FreeIndexAtPoint(int x, int y)
    {
        var hdc = Win32.GetDC(_hWnd);
        try
        {
            var lines = BuildFreeVisualLines(hdc, _freePreviewRect);
            int firstVisibleLine = GetFreeFirstVisibleLine(lines, _freePreviewRect);
            int visibleLineIndex = Math.Clamp((y - FreeTextTop(_freePreviewRect)) / FreeLineHeight(), 0, GetFreeVisibleLineCapacity(_freePreviewRect) - 1);
            int lineIndex = Math.Clamp(firstVisibleLine + visibleLineIndex, 0, lines.Count - 1);
            int relativeX = Math.Max(0, x - FreeTextLeft(_freePreviewRect));
            return FindFreeIndexAtX(hdc, lines[lineIndex], relativeX);
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }
    }

    private void SetFreeCursorFromPoint(int x, int y, bool extend)
    {
        _free.MoveTo(FreeIndexAtPoint(x, y), extend);
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    private bool MoveFreeCursorVertical(int direction, bool extend)
    {
        if (_freePreviewRect.right <= _freePreviewRect.left)
        {
            _free.MoveTo(direction < 0 ? 0 : _free.Text.Length, extend);
            return true;
        }

        var hdc = Win32.GetDC(_hWnd);
        try
        {
            var lines = BuildFreeVisualLines(hdc, _freePreviewRect);
            int currentLineIndex = FindFreeLineIndex(lines, _free.Caret);
            int targetLineIndex = currentLineIndex + direction;
            var currentLine = lines[currentLineIndex];
            if (targetLineIndex < 0)
                _free.MoveTo(0, extend); // Haut sur la première ligne : début du texte, comme Windows
            else if (targetLineIndex >= lines.Count)
                _free.MoveTo(_free.Text.Length, extend);
            else
            {
                int offset = Math.Clamp(_free.Caret - currentLine.Start, 0, currentLine.Length);
                int x = FreeTextWidth(hdc, currentLine.Start, offset);
                _free.MoveTo(FindFreeIndexAtX(hdc, lines[targetLineIndex], x), extend);
            }
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }

        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        return true;
    }

    /// <summary>Largeur d'une grappe dans la police du champ ; une fin de ligne ne mesure rien.</summary>
    private int MeasureFreeGrapheme(IntPtr hdc, string text, int start, int length)
    {
        if (length <= 0 || text[start] == '\n') return 0;
        int scale = S(1000);
        if (scale != _freeWidthsScale)
        {
            _freeWidths.Clear();
            _freeWidthsScale = scale;
        }
        string grapheme = text.Substring(start, length);
        if (!_freeWidths.TryGetValue(grapheme, out int width))
        {
            width = PaperText.Width(hdc, grapheme, FreeLineFont);
            _freeWidths[grapheme] = width;
        }
        return width;
    }

    /// <summary>Largeur d'un morceau du texte : la somme de ses grappes, mesure qui place aussi
    /// le curseur et la sélection.</summary>
    private int FreeTextWidth(IntPtr hdc, int start, int length)
    {
        string text = _free.Text;
        int width = 0;
        int i = start, end = Math.Min(text.Length, start + length);
        while (i < end)
        {
            int next = Math.Min(end, _free.NextBoundary(i));
            if (next <= i) break;
            width += MeasureFreeGrapheme(hdc, text, i, next - i);
            i = next;
        }
        return width;
    }

    /// <summary>Texte de l'Écriture libre : AG Texte demi-gras, comme le reste des Leçons
    /// (audit 1.3.0, P5, décision d'Antoine du 20/10) ; <see cref="PaperText"/> complète un
    /// caractère absent de la police (cyrillique, API…) par les polices de secours.</summary>
    private FontSpec FreeLineFont => F(AppFonts.Text, 17, TypeRamp.Semibold);

    private int FreeTextLeft(Win32.RECT preview) => preview.left + S(12);
    private int FreeTextTop(Win32.RECT preview) => preview.top + S(12);
    private int FreeLineHeight() => S(26);

    private static bool IsPointInRect(Win32.RECT rect, int x, int y)
        => rect.right > rect.left && rect.bottom > rect.top &&
           x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom;

    private bool MoveFreeCursorToLineBoundary(bool toEnd, bool extend)
    {
        if (_freePreviewRect.right <= _freePreviewRect.left)
        {
            _free.MoveTo(toEnd ? _free.Text.Length : 0, extend);
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
            return true;
        }

        var hdc = Win32.GetDC(_hWnd);
        try
        {
            var lines = BuildFreeVisualLines(hdc, _freePreviewRect);
            var line = lines[FindFreeLineIndex(lines, _free.Caret)];
            int end = line.Start + line.Length;
            // Fin d'une ligne coupée au dernier espace : avant cet espace, pour rester sur la ligne.
            if (toEnd && end > line.Start && end < _free.Text.Length && _free.Text[end - 1] == ' ')
                end--;
            _free.MoveTo(toEnd ? end : line.Start, extend);
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }

        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        return true;
    }

    private static bool KeyHeld(int vk) =>
        (TypingEngine.Windows.Win32.GetKeyState(vk) & unchecked((short)0x8000)) != 0;

    /// <summary>
    /// Touches du champ du mode Libre, comme dans un champ Windows (QCM du 09/11) : flèches,
    /// Origine et Fin (Ctrl : tout le texte), Maj pour sélectionner, Ctrl pour sauter un mot,
    /// Retour arrière et Suppr (Ctrl : un mot), Entrée, Ctrl+A/C/X/V/Z/Y. Alt Gr tenu n'est
    /// jamais un raccourci (TextEditKeys).
    /// </summary>
    private bool HandleFreeModeKeyDown(int vk) => HandleFreeModeKeyDown(vk, ReadEditModifiers());

    private bool HandleFreeModeKeyDown(int vk, EditModifiers modifiers)
    {
        var key = TextEditKeys.Map(vk, modifiers);
        switch (key.Command)
        {
            case TextEditCommand.None:
                return false;
            case TextEditCommand.Left:
            case TextEditCommand.Right:
            case TextEditCommand.WordLeft:
            case TextEditCommand.WordRight:
                _free.MoveHorizontal(key.Command is TextEditCommand.Left or TextEditCommand.WordLeft ? -1 : 1,
                    word: key.Command is TextEditCommand.WordLeft or TextEditCommand.WordRight, key.Extend);
                break;
            case TextEditCommand.Up:
                return MoveFreeCursorVertical(-1, key.Extend);
            case TextEditCommand.Down:
                return MoveFreeCursorVertical(1, key.Extend);
            case TextEditCommand.LineStart:
                return MoveFreeCursorToLineBoundary(toEnd: false, key.Extend);
            case TextEditCommand.LineEnd:
                return MoveFreeCursorToLineBoundary(toEnd: true, key.Extend);
            case TextEditCommand.DocumentStart:
                _free.MoveTo(0, key.Extend);
                break;
            case TextEditCommand.DocumentEnd:
                _free.MoveTo(_free.Text.Length, key.Extend);
                break;
            case TextEditCommand.Backspace:
            case TextEditCommand.Delete:
            case TextEditCommand.WordBackspace:
            case TextEditCommand.WordDelete:
                if (_free.Delete(backwards: key.Command is TextEditCommand.Backspace or TextEditCommand.WordBackspace,
                        word: key.Command is TextEditCommand.WordBackspace or TextEditCommand.WordDelete))
                {
                    _freeBackspaces++;
                    _freeTruncated = false;
                    ArmFreeStatsTimerIfNeeded();
                }
                break;
            case TextEditCommand.NewLine:
                InsertFreeCharacter('\n');
                return true;
            case TextEditCommand.SelectAll:
                _free.SelectAll();
                break;
            case TextEditCommand.Copy:
                _free.Copy(Clipboard);
                break;
            case TextEditCommand.Cut:
                if (_free.Cut(Clipboard)) _freeTruncated = false;
                break;
            case TextEditCommand.Paste:
                // Un collage n'est pas une frappe : il ne compte ni dans la vitesse ni dans les caractères.
                _freeTruncated = _free.Paste(Clipboard).Truncated;
                break;
            case TextEditCommand.Undo:
                _free.Undo();
                _freeTruncated = false;
                break;
            case TextEditCommand.Redo:
                _free.Redo();
                _freeTruncated = false;
                break;
        }
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        return true;
    }

    /// <summary>Un caractère tapé : il remplace la sélection. Un caractère hors du plan de base
    /// (émoji) arrive en deux WM_CHAR : sa première moitié attend la seconde.</summary>
    private void InsertFreeCharacter(char c)
    {
        string typed;
        if (char.IsHighSurrogate(c))
        {
            _freeHighSurrogate = c;
            return;
        }
        if (char.IsLowSurrogate(c))
        {
            if (_freeHighSurrogate is not { } high) return;
            typed = new string(new[] { high, c });
        }
        else
            typed = c.ToString();
        _freeHighSurrogate = null;

        _freeStartedAt ??= DateTimeOffset.UtcNow;
        var result = _free.Insert(typed, typed: true);
        _freeTruncated = result.Truncated;
        if (result.Inserted > 0)
            _freeChars++;
        ArmFreeStatsTimerIfNeeded();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    /// <summary>Double-clic dans le champ : le mot sous le pointeur.</summary>
    private bool SelectFreeWordAt(int x, int y)
    {
        if (_mode != WindowMode.Free || _settingsOpen || !IsPointInRect(_freePreviewRect, x, y)) return false;
        _free.SelectWordAt(FreeIndexAtPoint(x, y));
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        return true;
    }

    /// <summary>Bouton gauche enfoncé dans un champ (mode Libre ou « Votre texte ») : pose le
    /// curseur (Maj : étend la sélection) et suit la souris jusqu'au relâchement, pour
    /// sélectionner en glissant.</summary>
    private bool OnFieldMouseDown(IntPtr lParam, IntPtr wParam)
    {
        if (_settingsOpen || _pickerOpen) return false;
        var (x, y) = ClientToCanvas(lParam);
        const long MK_SHIFT = 0x0004;
        bool extend = (wParam.ToInt64() & MK_SHIFT) != 0;
        if (_mode == WindowMode.Free && IsPointInRect(_freePreviewRect, x, y) && HitClickAction(x, y) < 0)
        {
            _focusedActionIndex = -1;
            SetFreeCursorFromPoint(x, y, extend);
        }
        else if (!HomeTextMouseDown(x, y, extend))
            return false;
        _fieldDragging = true;
        SetCapture(_hWnd);
        Win32.SetFocus(_hWnd);
        return true;
    }

    private void OnFieldMouseDrag(IntPtr lParam)
    {
        var (x, y) = ClientToCanvas(lParam);
        if (HomeShown)
        {
            if (_homePage == HomePage.Text) HomeTextMouseDrag(x, y);
            return;
        }
        if (_mode != WindowMode.Free) return;
        int before = _free.Caret;
        _free.MoveTo(FreeIndexAtPoint(x, y), extend: true);
        if (_free.Caret != before)
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    private void EndFieldDrag()
    {
        if (!_fieldDragging) return;
        _fieldDragging = false;
        ReleaseCapture();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetCapture(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    private string BuildFreeStatsText()
    {
        if (!_freeStartedAt.HasValue || _freeChars == 0)
            return L.LessonsWin_FreeStatsEmpty;
        double seconds = Math.Max(1, (DateTimeOffset.UtcNow - _freeStartedAt.Value).TotalSeconds);
        double minutes = seconds / 60d;
        int wpm = _freeChars >= 10 ? (int)Math.Round((_freeChars / 5d) / minutes) : 0;
        int cpm = (int)Math.Round(_freeChars / minutes);
        return L.LessonsWin_FreeStatsFilled(wpm == 0 ? "—" : wpm.ToString(), cpm, (int)Math.Round(seconds), _freeBackspaces);
    }

    /// <summary>« Effacer les statistiques » : les compteurs seulement. Le texte reste (audit
    /// 09/11, L13 : le bouton effaçait aussi le texte sans le dire) ; Ctrl+A puis Suppr l'efface.</summary>
    private void ResetFreeStats()
    {
        _freeStartedAt = null;
        _freeChars = 0;
        _freeBackspaces = 0;
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    private void DrawHoverTooltip(IntPtr hdc, Win32.RECT bounds)
    {
        DrawTooltip(hdc, bounds, _hoverTooltip, _hoverTooltipAnchor, _hoverTooltipPreferAbove, _hoverTooltipCompact);
    }

    /// <summary>
    /// K4 (accessibilité 1.3.0) : un bouton-icône ne dit ce qu'il fait que par son infobulle,
    /// qui n'apparaissait qu'au survol de la souris. La cible focalisée au clavier affiche la
    /// même, au même endroit.
    /// </summary>
    private void DrawFocusedTooltip(IntPtr hdc, Win32.RECT bounds)
    {
        if (_focusedActionIndex < 0 || _focusedActionIndex >= _clickActions.Count) return;
        int index = FindHoverAreaFor(_clickActions[_focusedActionIndex].Rect, _hoverAreas);
        if (index < 0) return;
        var area = _hoverAreas[index];
        DrawTooltip(hdc, bounds, area.Tooltip, area.Rect, area.PreferAbove, area.Compact);
    }

    /// <summary>
    /// K4 : l'infobulle d'une cible cliquable est celle déclarée sur le même rectangle
    /// (<see cref="AddLinkTooltip"/> passe le même aux deux listes) ; la première s'il y en a
    /// plusieurs, -1 quand la cible n'en a pas.
    /// </summary>
    internal static int FindHoverAreaFor(Win32.RECT target,
        IReadOnlyList<(Win32.RECT Rect, string Tooltip, bool PreferAbove, bool Compact)> areas)
    {
        for (int i = 0; i < areas.Count; i++)
        {
            var rect = areas[i].Rect;
            if (rect.left == target.left && rect.top == target.top &&
                rect.right == target.right && rect.bottom == target.bottom &&
                !string.IsNullOrEmpty(areas[i].Tooltip))
                return i;
        }
        return -1;
    }

    private void DrawTooltip(IntPtr hdc, Win32.RECT bounds, string? text, Win32.RECT anchor, bool preferAbove, bool compact)
    {
        if (string.IsNullOrEmpty(text)) return;

        int pad = S(8);
        string[] lines = text.Split('\n');
        int lineH = S(18);
        var tipFont = FontSpec.Of(AppFonts.Text, 13, TypeRamp.Regular, PaperScale);
        int textWidth = lines.Max(line => PaperText.Width(hdc, line, tipFont));
        int minWidth = compact ? 0 : S(80);
        int width = Math.Max(minWidth, textWidth) + pad * 2;
        int height = Math.Max(S(26), lines.Length * lineH + pad);
        int anchorCenter = anchor.left + (anchor.right - anchor.left) / 2;
        int left = Math.Max(bounds.left + S(8), Math.Min(anchorCenter - width / 2, bounds.right - width - S(8)));
        int top = preferAbove
            ? anchor.top - height - S(6)
            : anchor.bottom + S(6);
        if (top < bounds.top + S(8))
            top = anchor.bottom + S(6);
        if (top + height > bounds.bottom - S(8))
            top = anchor.top - height - S(6);

        var rect = new Win32.RECT { left = left, top = top, right = left + width, bottom = top + height };
        PaperDraw.RoundBox(hdc, rect, CLR_PANEL, CLR_BORDER, S(3));
        int y = rect.top + Math.Max(S(4), (height - lines.Length * lineH) / 2);
        foreach (var line in lines)
        {
            PaperText.DrawLine(hdc, line, tipFont, new Win32.RECT { left = rect.left + pad, top = y, right = rect.right - pad, bottom = y + lineH },
                CLR_TEXT, TextAlign.Left);
            y += lineH;
        }
    }

    private const uint SPI_GETKEYBOARDCUES = 0x100A;

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    private static extern bool SystemParametersInfoBool(uint action, uint param, out bool value, uint winIni);

    /// <summary>Réglage Windows « souligner les raccourcis et touches d'accès » : repères toujours visibles.</summary>
    private static bool SystemKeyboardCues()
        => SystemParametersInfoBool(SPI_GETKEYBOARDCUES, 0, out bool on, 0) && on;

    private void DrawFocusRing(IntPtr hdc)
    {
        if (_focusedActionIndex < 0 || _clickActions.Count == 0 || !FocusCuesVisible)
            return;

        if (_focusedActionIndex >= _clickActions.Count)
            _focusedActionIndex = _clickActions.Count - 1;

        // #10 : l'anneau d'accent se perdait contre l'onglet actif, rempli d'accent ; une cible
        // focalisée au clavier prend l'anneau clair de Windows 11 en thème sombre.
        DrawFocusOutline(hdc, _clickActions[_focusedActionIndex].Rect, CLR_TEXT);
    }

    /// <summary>Le contour de focus commun (A13 : <see cref="PaperDraw.FocusOutline"/>).</summary>
    private void DrawFocusOutline(IntPtr hdc, Win32.RECT rect, uint? color = null)
        => PaperDraw.FocusOutline(hdc, rect, color ?? CLR_CURRENT, S(2), S(2), S(4));

    private void DrawRoundedBox(IntPtr hdc, Win32.RECT rect, uint fill, uint border, int radius)
    {
        var brush = Win32.CreateSolidBrush(fill);
        var pen = Win32.CreatePen(0, 1, border);
        var oldBrush = Win32.SelectObject(hdc, brush);
        var oldPen = Win32.SelectObject(hdc, pen);
        Win32.RoundRect(hdc, rect.left, rect.top, rect.right, rect.bottom, radius, radius);
        Win32.SelectObject(hdc, oldPen);
        Win32.SelectObject(hdc, oldBrush);
        Win32.DeleteObject(pen);
        Win32.DeleteObject(brush);
    }

    private void DrawText(IntPtr hdc, IntPtr font, string text, Win32.RECT rect, uint color, uint flags)
    {
        Win32.SelectObject(hdc, font);
        Win32.SetTextColor(hdc, color);
        Win32.DrawTextW(hdc, text, -1, ref rect, flags | Win32.DT_NOPREFIX);
    }

    /// <summary>Zone cliquable, et l'élément UIA qui la représente.</summary>
    private void AddClick(Win32.RECT rect, Action action, string name, LessonControlKind kind = LessonControlKind.Button,
        bool? toggled = null, bool selected = false)
    {
        _clickActions.Add((rect, action));
        _controlViews.Add(new LessonControlView(name, kind, rect, toggled, selected));
    }

    private void AddDoubleClick(Win32.RECT rect, Action action)
    {
        _doubleClickActions.Add((rect, action));
    }

    private void AddHover(Win32.RECT rect, string tooltip, bool preferAbove = false, bool compact = false)
    {
        _hoverAreas.Add((rect, tooltip, preferAbove, compact));
    }

    // Un clic se joue comme sur un bouton Windows (audit 09/11, E23) : enfoncé sur une cible et
    // relâché sur la même. Un relâchement sans enfoncement ici (glissé depuis une autre
    // fenêtre, double-clic de la barre de titre) ou sur une autre cible ne déclenche rien.
    private bool _clickPressed;
    private int _pressedClickAction = -1;

    private void OnButtonMouseDown(IntPtr lParam)
    {
        var (x, y) = ClientToCanvas(lParam);
        _pressedClickAction = _clickActionsScreen == CurrentFocusScreen ? HitClickAction(x, y) : -1;
        _clickPressed = true;
        SetCapture(_hWnd); // le relâchement arrive ici même hors de la fenêtre
    }

    private void OnButtonMouseUp(IntPtr lParam)
    {
        bool pressed = _clickPressed;
        int pressedAction = _pressedClickAction;
        _clickPressed = false;
        _pressedClickAction = -1;
        if (!pressed) return;
        ReleaseCapture();
        var (x, y) = ClientToCanvas(lParam);
        if (HitClickAction(x, y) != pressedAction) return; // glissé hors de la cible : annulé
        OnClick(lParam);
    }

    private void OnClick(IntPtr lParam)
    {
        var (x, y) = ClientToCanvas(lParam);
        ReturnFocusToSurface();
        HomeNoteClick(x, y); // accueil : le point sert à la carte (une zone, plusieurs touches)
        foreach (var (rect, action) in _clickActions)
        {
            if (x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom)
            {
                action();
                return;
            }
        }
        if (_pickerOpen)
        {
            // Un clic hors du sélecteur ouvert le referme, sans rien faire d'autre.
            ClosePicker();
            Win32.SetFocus(_hWnd);
            return;
        }
        // Le champ du mode Libre pose son curseur à l'enfoncement (OnFieldMouseDown) : le
        // relâchement ne doit pas défaire une sélection faite en glissant.
        Win32.SetFocus(_hWnd);
    }

    private void OnDoubleClick(IntPtr lParam)
    {
        var (x, y) = ClientToCanvas(lParam);
        if (SelectFreeWordAt(x, y) || HomeTextSelectWordAt(x, y))
            return;
        foreach (var (rect, action) in _doubleClickActions)
        {
            if (x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom)
            {
                action();
                return;
            }
        }
    }

    /// <summary>Indice de la cible cliquable sous le point, dans l'ordre d'<see cref="OnClick"/> ; -1 sinon.</summary>
    private int HitClickAction(int x, int y)
    {
        for (int i = 0; i < _clickActions.Count; i++)
        {
            var rect = _clickActions[i].Rect;
            if (x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom)
                return i;
        }
        return -1;
    }

    private bool IsHot(Win32.RECT rect) =>
        _hotX >= rect.left && _hotX <= rect.right && _hotY >= rect.top && _hotY <= rect.bottom;

    private void OnMouseMove(IntPtr lParam)
    {
        var (x, y) = ClientToCanvas(lParam);

        if (!_trackingMouseLeave)
        {
            var tme = new Win32.TRACKMOUSEEVENT
            {
                cbSize = (uint)Marshal.SizeOf<Win32.TRACKMOUSEEVENT>(),
                dwFlags = Win32.TME_LEAVE,
                hwndTrack = _hWnd,
                dwHoverTime = 0
            };
            _trackingMouseLeave = Win32.TrackMouseEvent(ref tme);
        }

        _hotX = x;
        _hotY = y;
        int hotIndex = HitClickAction(x, y);
        if (hotIndex != _hotActionIndex)
        {
            _hotActionIndex = hotIndex;
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        }

        string? tooltip = null;
        var anchor = new Win32.RECT();
        bool preferAbove = false;
        bool compact = false;
        bool found = false;
        foreach (var (rect, candidate, candidatePreferAbove, candidateCompact) in _hoverAreas)
        {
            if (x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom)
            {
                tooltip = candidate;
                anchor = rect;
                preferAbove = candidatePreferAbove;
                compact = candidateCompact;
                found = true;
                break;
            }
        }
        // L-06 : les touches du clavier après toutes les autres zones, comme au repeint, avec
        // l'état qu'il a dessiné ; ni au-dessus, ni compactes (valeurs par défaut d'AddHover).
        if (!found && _keyboardHover is { } keyboard &&
            TryGetKeyboardTooltip(_layout, keyboard.Placement, keyboard.Profile, keyboard.State, x, y,
                out var keyTooltip, out var keyAnchor))
        {
            tooltip = keyTooltip;
            anchor = keyAnchor;
        }

        if (!StringComparer.Ordinal.Equals(_hoverTooltip, tooltip) ||
            _hoverTooltipAnchor.left != anchor.left ||
            _hoverTooltipAnchor.top != anchor.top ||
            _hoverTooltipAnchor.right != anchor.right ||
            _hoverTooltipAnchor.bottom != anchor.bottom ||
            _hoverTooltipPreferAbove != preferAbove ||
            _hoverTooltipCompact != compact)
        {
            _hoverTooltip = tooltip;
            _hoverTooltipAnchor = anchor;
            _hoverTooltipPreferAbove = preferAbove;
            _hoverTooltipCompact = compact;
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        }
    }

    private void OnMouseLeave()
    {
        _trackingMouseLeave = false;
        _hotX = _hotY = int.MinValue;
        if (_hotActionIndex >= 0)
        {
            _hotActionIndex = -1;
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        }
        if (_hoverTooltip == null) return;
        _hoverTooltip = null;
        _hoverTooltipAnchor = new Win32.RECT();
        _hoverTooltipPreferAbove = false;
        _hoverTooltipCompact = false;
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    private void MoveKeyboardFocus(int direction)
    {
        _focusedActionIndex = NextFocusIndex(_focusedActionIndex, _clickActions.Count, direction, HasTypingSurface);
        _focusCuesShown = true;

        // K4 : la navigation au clavier retire l'infobulle de survol pour laisser voir celle
        // de la cible focalisée ; le prochain mouvement de souris la rétablit.
        _hoverTooltip = null;
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    /// <summary>La page affiche-t-elle une surface où l'on tape (ligne d'exercice, aperçu du
    /// mode Libre) ? Elle prend alors place dans le cycle de Tab, à l'index -1.</summary>
    private bool HasTypingSurface =>
        !_settingsOpen && !_pickerOpen && !TutorialPageShown &&
        (HomeShown ? HomePageHasSurface : _mode == WindowMode.Free || !_showSummary);

    /// <summary>
    /// Cible suivante de Tab (Maj+Tab : <paramref name="direction"/> = -1) parmi
    /// <paramref name="count"/> boutons. Avec une surface de frappe, -1 la désigne et Tab
    /// parcourt surface → boutons → surface (29/09) ; sans elle, les boutons seuls.
    /// </summary>
    internal static int NextFocusIndex(int current, int count, int direction, bool hasSurface)
    {
        int first = hasSurface ? -1 : 0;
        int slots = count - first;
        if (slots <= 0)
            return -1;
        if (current < first || current >= count)
            return direction < 0 ? count - 1 : first;
        return (current - first + direction + slots) % slots + first;
    }

    /// <summary>Une frappe ou un clic rend le focus à la surface (29/09) : le cadre accent y
    /// revient, et ce qui est tapé s'y écrit.</summary>
    private void ReturnFocusToSurface()
    {
        if (_focusedActionIndex < 0 || !HasTypingSurface)
            return;
        _focusedActionIndex = -1;
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    private bool ActivateFocusedAction()
    {
        if (_focusedActionIndex < 0 || _focusedActionIndex >= _clickActions.Count)
            return false;
        // L'écran a changé depuis le dernier repeint : les cibles connues sont celles de
        // l'écran d'avant, n'en activer aucune (L4).
        if (_clickActionsScreen != CurrentFocusScreen)
        {
            _focusedActionIndex = -1;
            return false;
        }

        _clickActions[_focusedActionIndex].Action();
        return true;
    }

    /// <summary>Le bilan d'un exercice est à l'écran.</summary>
    private bool SummaryShown => _showSummary && _mode == WindowMode.Lessons && !HomeShown && !TutorialPageShown;

    /// <summary>Écrans sans frappe d'exercice : Espace y active le bouton focalisé, comme Entrée.</summary>
    private bool SpaceActivatesButtons => _settingsOpen || HomeShown || TutorialPageShown || SummaryShown;

    private void OnKeyDown(int vk)
    {
        _swallowActivationSpace = false; // ne vaut que pour le WM_CHAR de cette touche-ci
        if (vk == 0x1B)
        {
            if (_pickerOpen)
            {
                ClosePicker();
                return;
            }
            if (_settingsOpen)
            {
                _settingsOpen = false;
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                return;
            }
            if (HomeEscape()) // accueil : de « Votre carte » ou « Votre texte » vers « Aujourd'hui »
                return;
            Hide();
            return;
        }
        if (vk == 0x09)
        {
            bool backwards = (TypingEngine.Windows.Win32.GetKeyState(0x10) & unchecked((short)0x8000)) != 0;
            bool ctrl = (TypingEngine.Windows.Win32.GetKeyState(0x11) & unchecked((short)0x8000)) != 0;
            if (ctrl)
                HomeCycleTab(backwards ? -1 : 1); // Ctrl+Tab : onglet, jamais un second Tab (E21)
            else
                MoveKeyboardFocus(backwards ? -1 : 1);
            return;
        }
        bool summaryShown = SummaryShown;
        if (vk == 0x0D || (SpaceActivatesButtons && vk == 0x20))
        {
            if (ActivateFocusedAction())
            {
                // L'Espace qui active un bouton n'est pas une frappe : son WM_CHAR ne doit pas
                // entrer dans la séance suivante (audit 09/11, E4).
                _swallowActivationSpace = vk == 0x20;
                return;
            }
        }
        if (_inputPaused) return; // en pause, rien au-delà de la navigation n'atteint l'exercice (RA5)
        if (summaryShown && !_settingsOpen && !_pickerOpen && (vk == 0x0D || vk == 0x20))
        {
            // Bilan sans bouton focalisé : Entrée et Espace valent « Suivant » ou « Terminer » (L12).
            _swallowActivationSpace = vk == 0x20;
            NextExercise();
            return;
        }
        if (HomeHandleKey(vk)) return; // accueil : rien n'atteint la séance cachée
        if (_settingsOpen || _pickerOpen) return; // liste ouverte : rien n'atteint l'exercice caché (E5)
        if (_mode == WindowMode.Free && HandleFreeModeKeyDown(vk))
        {
            ReturnFocusToSurface();
            return;
        }

        if (_mode == WindowMode.Lessons && vk == 0x08)
        {
            ReturnFocusToSurface();
            var result = _session.Backspace();
            if (result.Accepted)
            {
                if (!_session.NeedsBackspaceCorrection && !CurrentLineHasWrongCell())
                    _errorScancode = 0;
                ClearHint();
                UpdateAutoHintIfEnabled();
            }
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
            return;
        }
    }

    private void OnChar(char c)
    {
        // Paramètres ou liste des leçons ouverts : la frappe n'atteint pas l'exercice caché
        // derrière (audit 09/11, E5).
        if (_settingsOpen || _pickerOpen)
        {
            ResolveTypedCharacter(c); // consomme la frappe physique attendue, sans l'écrire
            return;
        }
        c = ResolveTypedCharacter(c);
        if (_swallowActivationSpace)
        {
            _swallowActivationSpace = false;
            if (c == ' ') return;
        }
        if (HomeHandleChar(c)) return; // accueil : après la résolution de la frappe physique, rien n'atteint la séance cachée
        if (TutorialPageShown) return; // écran des usages ou final du parcours : rien à taper
        if (char.IsControl(c)) return; // dont le '\t' qui suit Tab : le focus reste où Tab l'a mis
        ReturnFocusToSurface();
        if (_mode == WindowMode.Free)
        {
            InsertFreeCharacter(c);
            return;
        }

        if (_showSummary) return;
        var result = _session.TypeCharAndAdvanceLine(c);
        if (!result.Accepted) return;
        // La touche de la dernière faute reste cerclée sur le clavier jusqu'à sa correction.
        if (result.WasError)
        {
            _errorScancode = _pressedScancode;
            _automation?.Notify(TypingErrorAnnouncement(result.Actual ?? c, result.Expected, _session.NeedsBackspaceCorrection));
        }
        else if (!_session.NeedsBackspaceCorrection && !CurrentLineHasWrongCell())
            _errorScancode = 0;
        ClearHint();

        if (result.LineCompleted)
        {
            ClearHint();
            if (result.ExerciseCompleted)
                CompleteExercise();
            else
                UpdateAutoHintIfEnabled();
        }
        else
        {
            UpdateAutoHintIfEnabled();
        }

        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    /// <summary>
    /// Ce que le lecteur d'écran dit d'une faute (audit de régression du 10/11, RA7) : la surface
    /// expose la ligne attendue et le curseur, pas ce qui a été tapé. L'annonce vivait dans
    /// l'ancien module d'apprentissage, supprimé quand le parcours est entré dans les Leçons.
    /// </summary>
    internal static string TypingErrorAnnouncement(char typed, char? expected, bool backspaceFixes)
    {
        string fault = expected is { } wanted
            ? L.LessonsAutomation_TypingError(SpokenCharacter(typed), SpokenCharacter(wanted))
            : L.LessonsAutomation_TypingExtra(SpokenCharacter(typed));
        return fault + " " + (backspaceFixes ? L.LessonsAutomation_FixWithBackspace : L.LessonsAutomation_TypeAgain);
    }

    /// <summary>Un caractère tel qu'il se dit : les espaces par leur nom, les autres entre guillemets.</summary>
    private static string SpokenCharacter(char c) => c switch
    {
        ' ' => L.LessonsAutomation_SpaceName,
        ' ' => L.LessonsAutomation_NbspName,
        ' ' => L.LessonsAutomation_NarrowNbspName,
        _ => L.LessonsPaper_CharacterFallback(c.ToString()),
    };

    private void OnTimer(uint timerId)
    {
        if (timerId == TIMER_KEYPRESS)
        {
            Win32.KillTimer(_hWnd, (UIntPtr)TIMER_KEYPRESS);
            _pressedScancode = 0;
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        }
        else if (timerId == TIMER_HINT_CLEAR)
        {
            ClearHint();
            UpdateAutoHintIfEnabled();
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        }
        else if (timerId == TIMER_HINT_FLASH_CLEAR)
        {
            Win32.KillTimer(_hWnd, (UIntPtr)TIMER_HINT_FLASH_CLEAR);
            _hintButtonActive = false;
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        }
        else if (timerId == TIMER_FREE_STATS)
        {
            Win32.KillTimer(_hWnd, (UIntPtr)TIMER_FREE_STATS);
            if (_visible && _mode == WindowMode.Free && !_settingsOpen && _freeStartedAt.HasValue && ConfigManager.LessonFreeStatsVisible)
            {
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
                Win32.SetTimer(_hWnd, (UIntPtr)TIMER_FREE_STATS, FREE_STATS_REFRESH_MS, IntPtr.Zero);
            }
        }
    }

    private void CompleteExercise()
    {
        if (_showSummary) return;

        bool onChallenge = CurrentModule.Id == DailyChallenge.ModuleId;
        bool challengeFinale = onChallenge && _exerciseIndex == CurrentLesson.Exercises.Count - 1;
        // Le meilleur score doit être lu AVANT RecordSuccess, qui l'écrase par le résultat
        // de la tentative courante : après l'appel, toute séance est son propre record.
        // Extrait du Défi : le record est celui de tous les Défis, pas celui de l'exercice du
        // jour, neuf chaque matin (audit UX du 09/11, L10).
        int? previousBestWpm = challengeFinale
            ? ChallengeSessionState.PersonalBestWpm(_progress)
            : _progress.GetValidProgress(CurrentExercise)?.BestWpm;

        _lastCompletedStats = _session.Stats;
        _progress.RecordSuccess(CurrentExercise, _session.Stats);
        // Audit 1.3.0, B2 voie (b) : l'Initiation est le tutoriel de l'accueil. Sa réussite va
        // au compteur de la config, que lisent l'accueil (« Essayer maintenant », retour au
        // démarrage tant que 3 exercices ne sont pas faits) et l'import vers les Leçons.
        if (_homeComposed == null && CurrentModule.Id == LessonCatalogLoader.InitiationModuleId)
            ConfigManager.RecordLearningExerciseCompleted(CurrentExercise.ExerciseIndex);
        // Métrique « Record » (QW-4) : relue APRÈS RecordSuccess pour inclure la
        // tentative courante ; « nouveau » seulement si un score antérieur est battu.
        // Échauffement du Défi : cinq fois chaque caractère, différents chaque jour, pas de record.
        _summaryBestWpm = challengeFinale
            ? MaxWpm(previousBestWpm, _session.Stats.Wpm)
            : onChallenge ? null : _progress.GetValidProgress(CurrentExercise)?.BestWpm;
        _summaryIsNewBest = previousBestWpm.HasValue && _session.Stats.Wpm.HasValue &&
                            _session.Stats.Wpm.Value > previousBestWpm.Value;
        _showSummary = true;
        ClearHint();

        // Défi du jour : le dernier exercice de la leçon du jour clôt la séance —
        // date enregistrée, séquence de désapprentissage avancée, compteur local +1.
        if (challengeFinale)
            _challenge.Complete(_session.Stats, previousBestWpm);

        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    private static int? MaxWpm(int? a, int? b) => a is { } x && b is { } y ? Math.Max(x, y) : a ?? b;

    /// <summary>La séance du Défi affichée est déjà comptée : dit avant le mode de frappe
    /// (audit UX du 09/11, L10). Seulement si c'est bien elle qui a été finie, donc son dernier
    /// exercice réussi : le jour où la 5e séance de prise en main est comptée, le premier Défi
    /// commun ne compterait pas non plus, mais il n'a jamais été joué (RA9 du 10/11).</summary>
    internal bool ChallengeAlreadyCounted =>
        _homeComposed == null && CurrentModule.Id == DailyChallenge.ModuleId && _challenge.AlreadyCounted &&
        _progress.IsCompleted(CurrentLesson.Exercises[^1]);

    /// <summary>Compteur « x/N exercices » de la liste, sans le Défi du jour : ses deux
    /// exercices changent chaque jour, comptés ils faisaient baisser x le lendemain et varier N
    /// avec l'offre du Défi (audit UX du 09/11, L10).</summary>
    internal (int Done, int Total) PickerExerciseCount()
    {
        int done = 0, total = 0;
        foreach (var exercise in _catalog.Exercises)
        {
            if (exercise.ModuleId == DailyChallenge.ModuleId) continue;
            total++;
            if (_progress.IsCompleted(exercise)) done++;
        }
        return (done, total);
    }

    private void OnCopyChallengeResult()
    {
        _challenge.Copy(_hWnd);
        if (_challenge.ShareCopyFailed)
            _automation?.Notify(L.Challenge_ShareButtonFailed);
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    private void ShowHint()
    {
        ShowHintCore(automatic: false);
    }

    private void ShowHintCore(bool automatic)
    {
        if (_session.NeedsBackspaceCorrection)
        {
            if (_hintBackspace)
            {
                ArmHintTimers(clearAfterDuration: !automatic);
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
                return;
            }
            _hintBackspace = true;
            _hintCharacter = null;
            _hintMethod = null;
            _session.RecordHint();
            ArmHintTimers(clearAfterDuration: !automatic);
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
            return;
        }

        char? next = _session.GetNextExpectedCharacter();
        if (!next.HasValue) return;
        if (_hintCharacter == next.Value && _hintMethod != null)
        {
            ArmHintTimers(clearAfterDuration: !automatic);
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
            return;
        }
        var method = _hints.GetRecommendedMethod(next.Value, FollowingExpectedCharacter(), _mapper.CapsLockActive);
        if (method == null) return;
        _hintBackspace = false;
        _hintCharacter = next.Value;
        _hintMethod = method;
        _session.RecordHint();
        ArmHintTimers(clearAfterDuration: !automatic);
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    private void ClearHint()
    {
        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_HINT_CLEAR);
        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_HINT_FLASH_CLEAR);
        _hintCharacter = null;
        _hintMethod = null;
        _hintBackspace = false;
        _hintButtonActive = false;
    }

    private void ArmHintTimers(bool clearAfterDuration)
    {
        _hintButtonActive = true;
        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_HINT_FLASH_CLEAR);
        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_HINT_FLASH_CLEAR, HINT_FLASH_MS, IntPtr.Zero);

        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_HINT_CLEAR);
        if (clearAfterDuration)
        {
            uint duration = string.Equals(_hintMethod?.Type, "deadkey", StringComparison.OrdinalIgnoreCase)
                ? HINT_DEADKEY_MS
                : HINT_DIRECT_MS;
            Win32.SetTimer(_hWnd, (UIntPtr)TIMER_HINT_CLEAR, duration, IntPtr.Zero);
        }
    }

    private void ApplyHintHighlight(KeyboardRenderState state)
    {
        if (_hintBackspace)
        {
            state.HighlightedScancodes.Add(0x0E);
            return;
        }

        if (_hintMethod == null) return;

        if (!string.IsNullOrEmpty(_hintMethod.DeadKeyToken))
            state.LessonVisibleCharacters.Add(_hintMethod.DeadKeyToken);

        // Même guidage que le tutoriel, réglé pour les Leçons : la méthode recommandée,
        // armement puis touche finale (audit du 25/09, L-02).
        LessonHintProvider.Guide(state, _hintMethod, _hintCharacter?.ToString() ?? "", _mapper.ActiveDeadKey,
            _mapper.CapsLockActive, LessonGuideOptions);
    }

    private void ToggleAutoHints()
    {
        ConfigManager.SetLessonAutoHints(!ConfigManager.LessonAutoHintsEnabled);
        if (ConfigManager.LessonAutoHintsEnabled)
            UpdateAutoHintIfEnabled();
        else
            ClearHint();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    private void ToggleSettings()
    {
        _pickerOpen = false;
        _settingsOpen = !_settingsOpen;
        if (_settingsOpen)
        {
            Win32.KillTimer(_hWnd, (UIntPtr)TIMER_FREE_STATS);
        }
        else
        {
            UpdateAutoHintIfEnabled();
            ArmFreeStatsTimerIfNeeded();
        }
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    private void ToggleBool(bool currentValue, Action<bool> setter)
    {
        setter(!currentValue);
        UpdateAutoHintIfEnabled();
        ArmFreeStatsTimerIfNeeded();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    private void SwitchMode(WindowMode mode)
    {
        _settingsOpen = false;
        _pickerOpen = false;
        _mode = mode;
        if (mode == WindowMode.Lessons)
        {
            Win32.KillTimer(_hWnd, (UIntPtr)TIMER_FREE_STATS);
            UpdateAutoHintIfEnabled();
        }
        else
        {
            ClearHint();
            ArmFreeStatsTimerIfNeeded();
        }
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    private void UpdateAutoHintIfEnabled()
    {
        if (!_visible ||
            _settingsOpen ||
            HomeShown || // accueil : pas d'indice pour une séance qu'on ne voit pas
            _mode != WindowMode.Lessons ||
            !ConfigManager.LessonAutoHintsEnabled ||
            _showSummary ||
            _session.IsLineComplete ||
            _session.IsExerciseComplete)
            return;

        ShowHintCore(automatic: true);
    }

    private void ArmFreeStatsTimerIfNeeded()
    {
        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_FREE_STATS);
        if (!_visible ||
            _settingsOpen ||
            _mode != WindowMode.Free ||
            !_freeStartedAt.HasValue ||
            !ConfigManager.LessonFreeStatsVisible)
            return;

        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_FREE_STATS, FREE_STATS_REFRESH_MS, IntPtr.Zero);
    }

    private void ResetProgress()
    {
        int result = Win32.MessageBoxW(_hWnd,
            L.LessonsWin_ResetProgressConfirm,
            L.LessonsWin_WindowTitle, MB_YESNO | MB_ICONWARNING);
        if (result != IDYES) return;
        ApplyProgressReset();
    }

    /// <summary>Remise à zéro confirmée : exercices, gestes de « Votre carte » et record du
    /// Défi (audit UX du 09/11, L10 et L13).</summary>
    internal void ApplyProgressReset()
    {
        _progress.ResetAll();
        ConfigManager.SetTrainingBestWpm(0);
        StartCurrentSession(savePosition: true);
    }

    private void SelectModule(int index)
    {
        if (index < 0 || index >= _catalog.Modules.Count) return;
        HomeClearComposed(); // accueil : quitte la séance composée
        _homePage = HomePage.Exercise;
        _settingsOpen = false;
        _mode = WindowMode.Lessons;
        _moduleIndex = index;
        _lessonIndex = 0;
        _exerciseIndex = 0;
        StartCurrentSession(savePosition: true);
    }

    private void SelectLesson(int index)
    {
        HomeClearComposed(); // accueil : quitte la séance composée
        _homePage = HomePage.Exercise;
        if (index < 0 || index >= CurrentModule.Lessons.Count) return;
        _pickerOpen = false;
        _settingsOpen = false;
        _mode = WindowMode.Lessons;
        _lessonIndex = index;
        _exerciseIndex = 0;
        StartCurrentSession(savePosition: true);
    }

    /// <summary>
    /// Position du catalogue après (<paramref name="step"/> = 1) ou avant (-1) l'exercice
    /// affiché ; null en bout de parcours. On ne passe jamais d'un module ordinaire au Défi du
    /// jour ni l'inverse : le Défi s'ouvre exprès et compte la séance du jour (audit UX du
    /// 09/11, L3). Fonction pure pour les tests.
    /// </summary>
    internal static (int Module, int Lesson, int Exercise)? AdjacentPosition(
        LessonCatalog catalog, int moduleIndex, int lessonIndex, int exerciseIndex, int step)
    {
        var module = catalog.Modules[moduleIndex];
        var lesson = module.Lessons[lessonIndex];
        if (step > 0)
        {
            if (exerciseIndex < lesson.Exercises.Count - 1) return (moduleIndex, lessonIndex, exerciseIndex + 1);
            if (lessonIndex < module.Lessons.Count - 1) return (moduleIndex, lessonIndex + 1, 0);
            int next = moduleIndex + 1;
            if (next < catalog.Modules.Count && !CrossesChallenge(module, catalog.Modules[next]))
                return (next, 0, 0);
            return null;
        }
        if (exerciseIndex > 0) return (moduleIndex, lessonIndex, exerciseIndex - 1);
        if (lessonIndex > 0)
            return (moduleIndex, lessonIndex - 1, module.Lessons[lessonIndex - 1].Exercises.Count - 1);
        int previous = moduleIndex - 1;
        if (previous >= 0 && !CrossesChallenge(module, catalog.Modules[previous]))
        {
            var before = catalog.Modules[previous];
            int lastLesson = before.Lessons.Count - 1;
            return (previous, lastLesson, before.Lessons[lastLesson].Exercises.Count - 1);
        }
        return null;

        static bool CrossesChallenge(LessonModule a, LessonModule b) =>
            a.Id == DailyChallenge.ModuleId || b.Id == DailyChallenge.ModuleId;
    }

    private (int Module, int Lesson, int Exercise)? AdjacentPosition(int step) =>
        AdjacentPosition(_catalog, _moduleIndex, _lessonIndex, _exerciseIndex, step);

    /// <summary>Rien après cet exercice : fin du Défi ou du catalogue. Le bilan dit alors
    /// « Terminer », qui ramène à « Aujourd'hui » (QCM du 09/11).</summary>
    internal bool IsAtEndOfPath => !IsTutorialActive && _homeComposed == null && AdjacentPosition(1) == null;

    /// <summary>Le chevron ‹ (›) a-t-il une cible ? Sinon il n'est pas dessiné : un bouton
    /// dessiné n'est jamais mort (audit UX du 09/11, L2, L9).</summary>
    internal bool HasPreviousExercise => IsTutorialActive || _homeComposed != null || AdjacentPosition(-1) != null;

    private void PreviousExercise()
    {
        if (TutorialPrevious()) return; // parcours de l'accueil : son étape précédente (LessonsWindow.Tutorial.cs)
        if (_homeComposed != null) // séance composée : un seul exercice, on repart de son début
        {
            StartCurrentSession(savePosition: false);
            return;
        }
        if (AdjacentPosition(-1) is { } previous)
            GoTo(previous);
    }

    private void NextExercise()
    {
        if (TutorialNext()) return; // parcours de l'accueil : son étape suivante (LessonsWindow.Tutorial.cs)
        if (HomeFinishComposed()) return; // séance composée : retour à « Aujourd'hui »
        if (AdjacentPosition(1) is { } next)
        {
            GoTo(next);
            return;
        }
        FinishPath();
    }

    private void GoTo((int Module, int Lesson, int Exercise) position)
    {
        _moduleIndex = position.Module;
        _lessonIndex = position.Lesson;
        _exerciseIndex = position.Exercise;
        StartCurrentSession(savePosition: true);
    }

    /// <summary>Fin du Défi ou du catalogue : « Terminer » ramène à « Aujourd'hui ». L'exercice
    /// caché derrière repart de zéro : « Reprendre » n'ouvre jamais un exercice fini sans bilan.</summary>
    private void FinishPath()
    {
        StartCurrentSession(savePosition: false);
        HomeShowPage(HomePage.Today);
    }

    /// <summary>
    /// À la fermeture (audit 09/11, L5) : un bilan affiché vaut lu, comme « Suivant » (l'exercice
    /// suivant attend, une séance composée rend le catalogue) ; le Défi, qui ne s'ouvre que par
    /// ShowChallenge, rend la place du catalogue d'où l'on venait. Sans cela, la réouverture
    /// rendait l'ancien bilan, et le Défi périmé renvoyait à l'Initiation.
    /// </summary>
    private void SettleSessionOnHide()
    {
        if (_mode != WindowMode.Lessons || IsTutorialActive) return;
        bool onChallenge = _homeComposed == null && CurrentModule.Id == DailyChallenge.ModuleId;
        if (_showSummary)
        {
            if (HomeFinishComposed()) return;
            if (!onChallenge && AdjacentPosition(1) is { } next)
            {
                GoTo(next);
                return;
            }
            if (!onChallenge)
            {
                StartCurrentSession(savePosition: false); // fin du catalogue : comme « Terminer »
                return;
            }
        }
        if (onChallenge)
            PickInitialSelection(); // la dernière position du catalogue, sinon le premier exercice à faire
    }

    /// <summary>« Rejouer » au bilan : le même exercice, depuis le début. Un Défi rejoué ne
    /// recompte pas la séance du jour (ChallengeSessionState.Complete, A23).</summary>
    private void ReplayExercise() => StartCurrentSession(savePosition: false);

    private static string FormatLessonInstruction(string instruction)
        => LessonCatalogLoader.ExpandPlaceholders(instruction);

    private string BuildTypingStatus()
    {
        string status = CurrentExercise.TypingMode == LessonTypingMode.Strict
            ? L.LessonsWin_StatusStrict
            : L.LessonsWin_StatusFlexible;
        // Audit 1.3.0, A2 : la tolérance se dit ici, hors de la consigne, qui entre dans
        // l'empreinte de l'exercice (changer Lessons_Init* effacerait les réussites).
        if (_session.IsTolerant) status += " " + L.LessonsWin_StatusTolerated;
        return ChallengeAlreadyCounted ? L.Challenge_AlreadyDone + " " + status : status;
    }

    private string FormatHintButtonText()
    {
        if (_hintBackspace)
            return L.LessonsWin_HintBackspace;

        if (_hintMethod == null || !_hintCharacter.HasValue)
            return L.LessonsWin_IconHint;

        string target = FormatVisibleCharacter(_hintCharacter.Value.ToString());
        string key = FormatKeyName(_hintMethod.Key);
        string layer = FormatLayer(_hintMethod.Layer);

        if (string.Equals(_hintMethod.Type, "deadkey", StringComparison.OrdinalIgnoreCase))
        {
            string dead = !string.IsNullOrEmpty(_hintMethod.DeadKey)
                ? TrayApplication.GetDeadKeySymbol(_hintMethod.DeadKey)
                : "";
            string activationKey = FormatKeyName(_hintMethod.DkActivationKey);
            string activationLayer = FormatLayer(_hintMethod.DkActivationLayer);
            string activation = string.IsNullOrEmpty(activationLayer) ? activationKey : $"{activationLayer} + {activationKey}";
            string combo = string.IsNullOrEmpty(layer) ? key : $"{layer} + {key}";
            return string.IsNullOrEmpty(activation)
                ? $"{dead} {L.Search_ThenWord} {combo} -> {target}"
                : $"{activation} -> {dead} {L.Search_ThenWord} {combo} -> {target}";
        }

        string direct = string.IsNullOrEmpty(layer) ? key : $"{layer} + {key}";
        return $"{direct} -> {target}";
    }

    private string FormatVisibleCharacter(string value)
    {
        return ConfigManager.LessonInvisibleMarkersVisible
            ? KeyboardRenderer.DisplayInvisible(value)
            : value;
    }

    private static string FormatLayer(string? layer)
    {
        if (string.IsNullOrWhiteSpace(layer) || string.Equals(layer, "Base", StringComparison.OrdinalIgnoreCase))
            return "";

        return string.Join(" + ", layer.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.Equals(part, "Base", StringComparison.OrdinalIgnoreCase))
            .Select(part => part switch
            {
                "Shift" => L.Settings_ShortcutModifier2,
                "Caps" => L.Onboarding_CapsLockWord,
                "AltGr" => L.AltGrKey,
                _ => part
            }));
    }

    private static string FormatKeyName(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "";
        if (key.StartsWith("Key", StringComparison.OrdinalIgnoreCase) && key.Length == 4)
            return key[3].ToString().ToUpperInvariant();
        if (key.StartsWith("Digit", StringComparison.OrdinalIgnoreCase) && key.Length == 6)
            return key[5].ToString();

        return key switch
        {
            "Space" => L.Search_SpaceKeyLabel,
            "Minus" => "-",
            "Equal" => "=",
            "BracketLeft" => "[",
            "BracketRight" => "]",
            "Backslash" => "\\",
            "Semicolon" => ";",
            "Quote" => "´", // T3 : le signe de la touche à droite du M, plus l'apostrophe
            "Comma" => ",",
            "Period" => ".",
            "Slash" => "/",
            "Backquote" => "@",
            _ => key
        };
    }

    private static string FormatNullable(int? value, string suffix = "")
    {
        return value.HasValue ? value.Value + suffix : "—";
    }

    private static string FormatWpm(int? value)
    {
        // Audit 24/09 : « WPM » s'affichait en français. Unité inline, faute de clé dédiée.
        return value.HasValue ? L.LessonsWin_SpeedValue(value.Value) : FormatNullable(value);
    }

    private uint GetAccuracyColor(int? accuracy)
    {
        if (!accuracy.HasValue) return CLR_TEXT;
        if (accuracy.Value >= 95) return CLR_OK;
        if (accuracy.Value >= 85) return CLR_TEXT;
        return CLR_LESSON_ACCENT;
    }

    private uint GetErrorColor(int errorCount)
    {
        if (errorCount == 0) return CLR_OK;
        if (errorCount <= 3) return CLR_TEXT;
        return CLR_LESSON_ACCENT;
    }

    private void OnMapperStateChanged()
    {
        if (_visible)
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    private void OnRawKeyDown(uint scancode, bool extended)
    {
        if (_inputPaused) return;
        if (!_visible || _settingsOpen) return;
        _pressedScancode = scancode;
        // Touche étendue (pavé « / », flèches, Entrée du pavé) : jamais une touche de la
        // disposition, aucun texte attendu (audit 09/11, E3).
        if (!extended)
            CaptureExpectedTextForPhysicalKey(scancode);
        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_KEYPRESS, KEYPRESS_DURATION_MS, IntPtr.Zero);
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    /// <summary>
    /// Le texte attendu d'une frappe physique entre-t-il dans la file ? Report de 1911581 :
    /// seulement si les Leçons ont le focus, faute de quoi le WM_CHAR part dans une autre
    /// fenêtre et le texte resterait en file jusqu'à une seconde. Fonction pure pour le témoin.
    /// </summary>
    internal static bool ShouldCaptureExpectedText(bool visible, bool hasFocus, bool settingsOpen, bool showSummary) =>
        visible && hasFocus && !settingsOpen && !showSummary;

    private void CaptureExpectedTextForPhysicalKey(uint scancode)
    {
        // Accueil : le récapitulatif et la fin de la séance cachée ne concernent pas le champ de
        // « Votre texte », qui reçoit la frappe telle qu'AZERTY Global la produit.
        if (!ShouldCaptureExpectedText(_visible, _hasFocus, _settingsOpen, (_showSummary && !HomeShown) || TutorialPageShown)) return;
        if (HomeShown && !HomePageHasSurface) return; // accueil : pas de champ où le texte attendu irait
        if (_mode == WindowMode.Lessons && !HomeShown && _session.IsExerciseComplete) return;
        _pendingPhysicalText.CaptureKey(_layout, scancode, _mapper);
    }

    // Ce que le moteur a réellement écrit pour la frappe capturée (ticket ME-06, C1.2 et C1.3).
    private void OnMapperKeyDecided(KeyDecision decision) =>
        _pendingPhysicalText.ApplyEngineDecision(decision.Handled, decision.EmittedText);

    private char ResolveTypedCharacter(char received)
    {
        return _pendingPhysicalText.Resolve(received);
    }

    private void ClearPendingPhysicalText()
    {
        _pendingPhysicalText.Clear();
    }

    private void Hide()
    {
        // Parcours de l'accueil : fermer la fenêtre le clôt ; l'écran final atteint vaut réussite.
        EndTutorial(completed: _tutorialPage == TutorialPage.Final);
        // Audit 1.3.0, A3 : les exercices font allumer Verr. Maj. ; la fenêtre ne la laisse pas
        // allumée derrière elle (Show l'éteint déjà à l'ouverture).
        _mapper.RequestCapsLockOff();
        SaveWindowBounds();
        SettleSessionOnHide();
        _visible = false;
        _challenge.EndRequest();
        LearningSessionTracker.Closed(this);
        _settingsOpen = false;
        HomeOnHide(); // accueil des Leçons : oublie le clic et le texte collé (LessonsWindow.Home.cs)
        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_FREE_STATS);
        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_HINT_CLEAR);
        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_HINT_FLASH_CLEAR);
        // Mode Libre : le texte reste pour la prochaine ouverture (audit 09/11, E7 : Échap
        // l'effaçait sans prévenir) ; les statistiques mesurent une séance et repartent de zéro.
        EndFieldDrag();
        _freeHighSurrogate = null;
        _freeChars = 0;
        _freeBackspaces = 0;
        _freeStartedAt = null;
        ClearPendingPhysicalText();
        _progress.RetryPendingSave(); // AF-02 : progression gardée en mémoire après un échec d'écriture
        Win32.ShowWindow(_hWnd, 0);
    }

    public void Dispose()
    {
        EndTutorial(completed: false); // referme la plage de frappe exclue du parcours
        LearningSessionTracker.Closed(this);
        _progress.RetryPendingSave(); // AF-02 : dernier essai à la fermeture de l'application
        ConfigManager.WindowBoundsCleared -= OnWindowBoundsCleared;
        if (_onAppLanguageChanged != null)
            ConfigManager.AppLanguageChanged -= _onAppLanguageChanged;
        _mapper.StateChanged -= OnMapperStateChanged;
        _hook.RawKeyDownDetailed -= OnRawKeyDown;
        _mapper.KeyDecided -= OnMapperKeyDecided;
        _automation?.Dispose(); // tant que le HWND existe
        _automation = null;
        if (_hWnd != IntPtr.Zero)
        {
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
        NativeWindow.UnregisterClass(WND_CLASS_NAME);
        DisposeResizeSnapshot();
        DestroyFonts();
    }
}
