// Fenêtre d'accueil — wizard 4 étapes affiché au premier lancement
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Fenêtre Win32 d'onboarding en 4 étapes (<see cref="OnboardingStep"/>) :
///   1. Les 5 améliorations
///   2. Écrire le peul, le wolof, le bambara (direction C, 08/10), avec un essai
///   3. Comment utiliser l'application
///   4. Ressources, communauté et préférences
/// DPI-aware per-monitor v2 : recalcule polices et layout sur WM_DPICHANGED.
/// </summary>
sealed class OnboardingWindow : IDisposable
{
    // ── Window constants ─────────────────────────────────────────────

    // ── Control IDs ──────────────────────────────────────────────────
    private const int IDC_CHK_DONT_SHOW = 2001;
    private const int IDC_BTN_NEXT = 2002;
    private const int IDC_BTN_PREV = 2003;
    private const int IDC_LINK_GUIDE = 2004;
    private const int IDC_CHK_AUTOSTART = 2006;
    private const int IDC_LINK_FEEDBACK = 2008;
    private const int IDC_LINK_DISCORD = 2010;
    private const int IDC_BTN_TRY = 2011;       // « Essayer maintenant » — etape 1 uniquement
    private const int IDC_LINK_LESSONS = 2012;
    private const int IDC_CHK_TRAINING = 2013;  // opt-in Défi du jour (v1.2.0) — étape 3 uniquement
    private const int IDC_BTN_FLAG = 2014;      // K6 : bouton du drapeau de langue (accessibilité 1.3.0)
    private const int IDC_EDIT_TRIAL = 2015;    // essai ù puis b → ɓ — étape « Vos langues » uniquement

    // Notifications et messages du champ d'essai.
    private const uint EN_SETFOCUS = 0x0100;
    private const uint EN_KILLFOCUS = 0x0200;
    private const uint EM_SETSEL = 0x00B1;

    // Dimensions de base (96 DPI)
    // Refonte du 15/10 : fenêtre en largeur, 720 × 540 à 100 % (× ONBOARDING_UI_SCALE), pour
    // montrer le clavier entier et ses notes à l'étape 1 (vagues de maquettes, choix d'Antoine).
    private const int BASE_WIN_W = 960;
    private const int BASE_WIN_H = 720;
    // Drapeau de bascule de langue dans le header (proportions 3:2 du site, +50 % : 45×30)
    private const int BASE_FLAG_W = 45;
    private const int BASE_FLAG_H = 30;
    private const float ONBOARDING_UI_SCALE = 0.75f;
    // Marges et boutons de la maquette du 15/10 : bord de 28 px, bouton de 34 px posé à 22 px
    // du bas (valeurs de base × ONBOARDING_UI_SCALE).
    private const int BASE_MARGIN = 38;
    private const int BASE_BOTTOM_MARGIN = 75;
    private const int BASE_BTN_H = 45;
    // Étapes 2 à 4 : une colonne centrée, la largeur de lecture de l'ancienne fenêtre.
    private const int BASE_COLUMN_W = 560;
    private const int BASE_BTN_W_NEXT_MIN = 140;
    private const int BASE_BTN_W_PREV = 120;
    private const int BASE_BTN_TEXT_PAD = 28;
    private const int STEP_CARD_MIN_H = 78;

    // ── Colors (COLORREF = 0x00BBGGRR) ───────────────────────────────
    // Refonte du 15/10 : palette papier des Leçons et du clavier virtuel ; le bleu marque ce
    // qui change ou ce qui agit, les titres restent à l'encre.
    private const uint CLR_BG = PaperTheme.Paper;
    private const uint CLR_TITLE = PaperTheme.Ink;
    private const uint CLR_TEXT = PaperTheme.Ink;
    private const uint CLR_LINK = PaperTheme.Action;
    private const uint CLR_LINK_HOVER = PaperTheme.Ink;
    private const uint CLR_STEP_TITLE = PaperTheme.Ink;
    private const uint CLR_PROGRESS_ACTIVE = PaperTheme.Action;
    private const uint CLR_PROGRESS_INACTIVE = PaperTheme.Border;
    private const uint CLR_PANEL_BG = PaperTheme.Surface;
    private const uint CLR_PANEL_BORDER = PaperTheme.Border;
    private const uint CLR_BADGE_BG = PaperTheme.Action;
    private const uint CLR_BADGE_TEXT = PaperTheme.OnAction;
    private const uint CLR_INLINE_HIGHLIGHT = PaperTheme.Action;
    private const uint CLR_REASSURE = PaperTheme.TextSecondary;

    // ── Colors ARGB pour GDI+ (0xAARRGGBB) ──────────────────────────

    // ═══════════════════════════════════════════════════════════════
    // Champs d'instance
    // ═══════════════════════════════════════════════════════════════
    private IntPtr _hWnd;
    private int _currentStep;
    private bool _learningModuleDone;
    // Vrai si l'utilisateur a vu l'etape 3 (la ou la checkbox « Ne plus afficher » est presente).
    // Sans ce flag, fermer le wizard via la croix a l'etape 1 sans avoir vu l'etape 3 persisterait
    // la valeur DEFAULT de la checkbox (case par defaut UNchecked en v0.9.7.1) -> cas reasonnable,
    // mais on respecte explicitement le principe : ne persister un opt-out que si l'utilisateur
    // a effectivement vu et touche a l'option. Avant v0.9.7.1, la default-checked + persist
    // inconditionnel rendait l'opt-out implicite des l'ouverture de la fenetre.
    private bool _step3Reached;
    // État de la case « Lancer au démarrage » au dernier Show() : décocher la case cochée
    // par défaut est un choix, alors même que l'état réel ne change pas.
    private bool _autoStartShownChecked;
    // Vrai des qu'on lance le LM une premiere fois. Distincte de _learningModuleDone (qui
    // n'est mis a true qu'a la complete reussite des exercices du parcours). Etat B (essaye non complete)
    // sert a afficher « Suivant » a cote de « Essayer maintenant » sur l'etape 1.
    private bool _learningModuleAttempted;
    private bool _inputPaused;
    private LearningModule? _learningModule;

    // Références passées par TrayApplication pour le LearningModule
    public KeyMapper? Mapper { get; set; }
    public KeyboardHook? Hook { get; set; }
    public Layout? AppLayout { get; set; }
    public Action? OpenLessonsRequested { get; set; }

    /// <summary>
    /// Appelé après chaque fermeture de la fenêtre. Permet à l'appelant de reprendre la
    /// main sur ce qu'il avait différé pendant que la fenêtre occupait l'écran — la
    /// sollicitation d'avis notamment, qui ne doit pas s'afficher par-dessus l'accueil.
    /// </summary>
    public Action? OnClosed { get; set; }
    public Func<bool>? ActivationRequested { get; set; }
    private bool _activationAccepted = ConfigManager.ActivationConsent;
    private string TryButtonText => _activationAccepted ? L.Onboarding_TryNow : L.Onboarding_ActivateAndTry;
    private bool RequestActivation()
    {
        if (_activationAccepted) return true;
        if (ActivationRequested?.Invoke() != true) return false;
        _activationAccepted = true;
        Win32.SetWindowTextW(_hWndBtnTry, TryButtonText);
        UpdateStepVisibility();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
        return true;
    }

    public void SetInputPaused(bool paused)
    {
        _inputPaused = paused;
        _learningModule?.SetInputPaused(paused);
    }

    // Y du contenu (après le header) — recalculé à chaque OnPaint
    private int _contentY;

    // Contrôles — Navigation
    private IntPtr _hWndBtnNext;
    private IntPtr _hWndBtnPrev;
    private IntPtr _hWndBtnTry; // visible uniquement etape 1 + !_learningModuleDone

    // Drapeau de bascule de langue (langue cible), dessiné en GDI+ dans le header —
    // pas un contrôle enfant. Le rect (coordonnées client) sert au hit-test souris.
    private Win32.RECT _flagRect;
    // K6 (accessibilité 1.3.0) : bouton owner-draw posé sur ce rect, pour que le drapeau soit
    // atteignable et activable au clavier. Il redessine le même drapeau (DrawFlagButton).
    private IntPtr _hWndBtnFlag;
    private Win32.RECT _flagButtonRect;
    private bool _flagButtonShown;

    // Abonnement AppLanguageChanged (bascule initiée depuis le tray ou les Paramètres) —
    // désabonné dans Dispose (événement statique, sinon référence pendante).
    private readonly Action<string>? _onAppLanguageChanged;

    // Contrôles — Étape 3
    private IntPtr _hWndLinkLessons;
    private IntPtr _hWndLinkGuide;
    private IntPtr _hWndLinkFeedback;
    private IntPtr _hWndLinkDiscord;
    private IntPtr _hWndChkAutoStart;
    private IntPtr _hWndChkDontShow;
    private IntPtr _hWndChkTraining; // opt-in Défi du jour (v1.2.0)

    // Contrôles — Étape « Vos langues » : le champ d'essai, sans bordure ; son cadre est peint
    // dans _trialBox (accent épaissi au focus, comme la maquette).
    private IntPtr _hWndTrial;
    private Win32.RECT _trialBox;
    private bool _trialFocused;
    private bool _trialUpdating; // garde contre la réentrée d'EN_CHANGE quand on réduit le texte
    private readonly IntPtr _hFieldBrush;

    // Delegates (prevent GC)
    private readonly Win32.WNDPROC _wndProcDelegate;
    private readonly Win32.SUBCLASSPROC _buttonArrowSubclassProc;
    // F-10 : survol, Entrée, Échap et Tab des liens de l'étape 3.
    private readonly LinkBehavior _links;

    // GDI resources
    // Fond de la classe : Windows le détruit au désenregistrement (NativeWindow).
    private IntPtr _hBgBrush;
    private readonly IntPtr _hPanelBrush;

    // GDI+ resources
    private IntPtr _gdipToken;
    private IntPtr _gdipLogo;
    private IntPtr _gdipFlagEn;
    private IntPtr _gdipFlagFr;
    private IntPtr _hIcon;

    private bool _visible;

    // DPI scaling — mutable, recalculé sur WM_DPICHANGED
    private float _dpiScale;
    private int S(int val) => (int)(val * _dpiScale * ONBOARDING_UI_SCALE);
    // Marge des étapes 2 à 4, qui centre leur colonne dans la fenêtre en largeur.
    private int ColumnMargin => (S(BASE_WIN_W) - S(BASE_COLUMN_W)) / 2;

    // Fonts — recréés sur changement de DPI
    private IntPtr _hFontTitle;
    private IntPtr _hFontSubtitle;
    private IntPtr _hFontText;
    private IntPtr _hFontBold;
    private IntPtr _hFontSmall;
    private IntPtr _hFontReassure; // mention vie privée étape 1 — plus petite que _hFontSmall pour tenir sur une ligne en 175% DPI
    private IntPtr _hFontVersion; // numéro de version sous le drapeau de langue (header)
    private IntPtr _hFontButton;
    private IntPtr _hFontStepSummary;
    private IntPtr _hFontPageTitle;
    private IntPtr _hFontLinkStrong;
    // Étape « Vos langues » : lettres (22 px), lettre de l'essai (28 px), modificateur tenu.
    private IntPtr _hFontLetters;
    private IntPtr _hFontTrial;
    private IntPtr _hFontTag;

    public bool IsVisible => _visible;

    public OnboardingWindow()
    {
        _wndProcDelegate = WndProc;
        _links = new LinkBehavior(() => _hWnd, () => Close(validated: false), focusFrame: true,
            staticDialogCode: false, swallow: msg => _inputPaused && IsPausedInputMessage(msg));
        _buttonArrowSubclassProc = ButtonArrowSubclassProc;
        _hPanelBrush = Win32.CreateSolidBrush(CLR_PANEL_BG);
        _hFieldBrush = Win32.CreateSolidBrush(PaperTheme.Surface);

        // DPI initial (moniteur principal — sera corrigé par GetDpiForWindow après création)
        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        int dpi = Win32.GetDeviceCaps(hdcScreen, 88);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        float screenScale = dpi / 96f;
        _dpiScale = FittedScale(screenScale);

        // GDI+
        var gdipInput = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        Win32.GdiplusStartup(out _gdipToken, ref gdipInput, IntPtr.Zero);

        _gdipLogo = GdiImageLoader.LoadFromEmbeddedResource(typeof(OnboardingWindow), ProductIdentity.LogoResourceName);
        _gdipFlagEn = GdiImageLoader.LoadFromEmbeddedResource(typeof(OnboardingWindow), "flag-en.png");
        _gdipFlagFr = GdiImageLoader.LoadFromEmbeddedResource(typeof(OnboardingWindow), "flag-fr.png");
        CreateFonts();
        CreateMainWindow();
        ApplyFontsToControls();

        // Bascule de langue initiée ailleurs (menu tray, Paramètres) pendant que cette
        // fenêtre existe : se rafraîchir. Événement statique → désabonné dans Dispose.
        _onAppLanguageChanged = _ => OnLanguageChanged();
        ConfigManager.AppLanguageChanged += _onAppLanguageChanged;

        // Corriger le DPI avec le vrai DPI du moniteur où la fenêtre est apparue, puis le
        // plafond de sa zone de travail.
        float windowScale = NativeWindow.CorrectedScale(Win32.GetDpiForWindow(_hWnd), screenScale) ?? screenScale;
        float fitted = FittedScale(windowScale);
        if (Math.Abs(fitted - _dpiScale) > 0.001f)
        {
            _dpiScale = fitted;
            // Mise en page seule : GetDpiForWindow ne lève pas (audit du 25/09, X-04).
            try
            {
                RecreateFonts();
                RepositionControls();
                ResizeWindow();
                NativeWindow.KeepInWorkArea(_hWnd);
            }
            catch { }
        }
    }

    /// <summary>
    /// Plafond de l'Accueil (1.3.0) : l'échelle de l'écran, réduite pour que la fenêtre tienne
    /// dans la zone de travail de son écran (celui du curseur tant qu'elle n'existe pas). La
    /// hauteur est fixe : sans lui, les boutons du bas sortaient de l'écran en 1366 × 768 à
    /// 125 % et en 1920 × 1080 à 175 %.
    /// </summary>
    private float FittedScale(float screenScale)
    {
        int clientW = (int)(BASE_WIN_W * screenScale * ONBOARDING_UI_SCALE);
        int clientH = (int)(BASE_WIN_H * screenScale * ONBOARDING_UI_SCALE);
        var (outerW, outerH) = NativeWindow.OuterSize(clientW, clientH,
            Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU, Win32.WS_EX_TOPMOST);
        var work = NativeWindow.WorkArea(_hWnd);
        return WindowSizing.FitScale(screenScale, clientW, clientH, outerW - clientW, outerH - clientH,
            work.right - work.left, work.bottom - work.top);
    }

    // ═══════════════════════════════════════════════════════════════
    // Polices
    // ═══════════════════════════════════════════════════════════════
    private void CreateFonts()
    {
        _hFontTitle = TypeRamp.Create(TypeRamp.Title, TypeRamp.Semibold, _dpiScale);
        _hFontSubtitle = TypeRamp.Create(TypeRamp.Body, TypeRamp.Semibold, _dpiScale);
        _hFontText = TypeRamp.Create(TypeRamp.Body, TypeRamp.Regular, _dpiScale);
        _hFontBold = TypeRamp.Create(TypeRamp.Body, TypeRamp.Semibold, _dpiScale);
        _hFontSmall = TypeRamp.Create(TypeRamp.Caption, TypeRamp.Regular, _dpiScale, italic: true);
        _hFontVersion = TypeRamp.Create(TypeRamp.Caption, TypeRamp.Regular, _dpiScale);
        // Légende de la rampe (lot visuel 1.3.0) ; la mention revient à la ligne au besoin.
        _hFontReassure = TypeRamp.Create(TypeRamp.Caption, TypeRamp.Regular, _dpiScale, italic: true);
        _hFontButton = TypeRamp.Create(TypeRamp.Body, TypeRamp.Regular, _dpiScale);
        _hFontStepSummary = TypeRamp.Create(TypeRamp.Body, TypeRamp.Semibold, _dpiScale);
        // Titres des étapes 2 à 4 dans la police de titre de l'étape 1 (AG Titre, refonte du 15/10).
        // Handle du cache d'AppFonts : jamais détruit ici.
        _hFontPageTitle = FontSpec.Of(AppFonts.Title, 22, TypeRamp.Semibold, _dpiScale).Handle;
        _hFontLinkStrong = TypeRamp.Create(TypeRamp.Body, TypeRamp.Regular, _dpiScale, underline: true);
        // Hors rampe, comme les glyphes du clavier : ce sont des lettres montrées, pas du texte
        // d'interface (tailles de la maquette accueil-c-etape-vos-langues).
        _hFontLetters = TypeRamp.Create(22, TypeRamp.Semibold, _dpiScale);
        _hFontTrial = TypeRamp.Create(28, TypeRamp.Semibold, _dpiScale);
        _hFontTag = TypeRamp.Create(TypeRamp.Caption, TypeRamp.Semibold, _dpiScale);
    }

    private void DestroyFonts()
    {
        Win32.DeleteObject(_hFontTitle);
        Win32.DeleteObject(_hFontSubtitle);
        Win32.DeleteObject(_hFontText);
        Win32.DeleteObject(_hFontBold);
        Win32.DeleteObject(_hFontSmall);
        Win32.DeleteObject(_hFontVersion);
        Win32.DeleteObject(_hFontReassure);
        Win32.DeleteObject(_hFontButton);
        Win32.DeleteObject(_hFontStepSummary);
        Win32.DeleteObject(_hFontLinkStrong);
        Win32.DeleteObject(_hFontLetters);
        Win32.DeleteObject(_hFontTrial);
        Win32.DeleteObject(_hFontTag);
    }

    private void RecreateFonts()
    {
        DestroyFonts();
        CreateFonts();
        ApplyFontsToControls();
    }

    private void ApplyFontsToControls()
    {
        Win32.SendMessageW(_hWndBtnNext, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
        Win32.SendMessageW(_hWndBtnPrev, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
        // « Activer et essayer » : sans ce renvoi, il gardait la police détruite au WM_DPICHANGED.
        Win32.SendMessageW(_hWndBtnTry, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
        Win32.SendMessageW(_hWndLinkLessons, Win32.WM_SETFONT, _hFontLinkStrong, (IntPtr)1);
        Win32.SendMessageW(_hWndLinkGuide, Win32.WM_SETFONT, _hFontLinkStrong, (IntPtr)1);
        Win32.SendMessageW(_hWndLinkFeedback, Win32.WM_SETFONT, _hFontLinkStrong, (IntPtr)1);
        Win32.SendMessageW(_hWndLinkDiscord, Win32.WM_SETFONT, _hFontLinkStrong, (IntPtr)1);
        Win32.SetWindowTextW(_hWndLinkDiscord, L.Onboarding_LinkDiscord);
        Win32.SendMessageW(_hWndChkAutoStart, Win32.WM_SETFONT, _hFontText, (IntPtr)1);
        Win32.SendMessageW(_hWndChkDontShow, Win32.WM_SETFONT, _hFontText, (IntPtr)1);
        Win32.SendMessageW(_hWndChkTraining, Win32.WM_SETFONT, _hFontText, (IntPtr)1);
        Win32.SendMessageW(_hWndTrial, Win32.WM_SETFONT, _hFontTrial, (IntPtr)1);
    }

    // ═══════════════════════════════════════════════════════════════
    // Redimensionnement et repositionnement
    // ═══════════════════════════════════════════════════════════════
    private void ResizeWindow() =>
        NativeWindow.ResizeAroundCenter(_hWnd, S(BASE_WIN_W), S(BASE_WIN_H),
            Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU, Win32.WS_EX_TOPMOST);

    private void RepositionControls()
    {
        int margin = S(BASE_MARGIN);
        int winW = S(BASE_WIN_W);
        int bottomY = S(BASE_WIN_H) - S(BASE_BOTTOM_MARGIN);
        GetStep3Layout(_contentY, winW, out _, out _,
            out int linksX, out int linksWidth, out int linkStartY, out int linkRowH, out int linkControlHeight,
            out int checkboxX, out int checkboxWidth, out int checkboxY, out int checkboxSpacing, out int checkboxHeight,
            out int checkboxTrainingY, out _, out _);

        // Navigation : btnPrev seulement ici (position fixe a gauche).
        // btnNext et btnTry sont positionnes dans UpdateStepVisibility selon les 3 etats
        // possibles a l'etape 1 (jamais essaye / essaye non complete / complete).
        Win32.MoveWindow(_hWndBtnPrev, margin, bottomY, S(BASE_BTN_W_PREV), S(BASE_BTN_H), true);

        // Étape 3 — liens dans une grille fixe
        Win32.MoveWindow(_hWndLinkLessons, linksX, linkStartY, linksWidth, linkControlHeight, true);
        Win32.MoveWindow(_hWndLinkGuide, linksX, linkStartY + linkRowH, linksWidth, linkControlHeight, true);
        Win32.MoveWindow(_hWndLinkFeedback, linksX, linkStartY + linkRowH * 2, linksWidth, linkControlHeight, true);
        Win32.MoveWindow(_hWndLinkDiscord, linksX, linkStartY + linkRowH * 3, linksWidth, linkControlHeight, true);

        // Préférences
        Win32.MoveWindow(_hWndChkAutoStart, checkboxX, checkboxY, checkboxWidth, checkboxHeight, true);
        Win32.MoveWindow(_hWndChkDontShow, checkboxX, checkboxY + checkboxSpacing, checkboxWidth, checkboxHeight, true);
        Win32.MoveWindow(_hWndChkTraining, checkboxX, checkboxTrainingY, checkboxWidth, checkboxHeight, true);
    }

    private void CreateMainWindow()
    {
        var hInstance = Win32.GetModuleHandleW(null);
        var className = ProductIdentity.WindowClass("Onboarding");
        // Audit du 25/09, X-01 : une classe refusée ne crée pas de fenêtre.
        if (!NativeWindow.RegisterClass(className, _wndProcDelegate))
            return;

        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        uint dwExStyle = Win32.WS_EX_TOPMOST;
        var (windowW, windowH) = NativeWindow.OuterSize(S(BASE_WIN_W), S(BASE_WIN_H), dwStyle, dwExStyle);
        var (x, y) = NativeWindow.CenterIn(NativeWindow.WorkArea(), windowW, windowH);

        _hWnd = Win32.CreateWindowExW(dwExStyle, className, ProductIdentity.DisplayName,
            dwStyle, x, y, windowW, windowH,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        _hBgBrush = NativeWindow.ApplyClassBackground(_hWnd, CLR_BG);
        NativeWindow.ApplyFrame(_hWnd, dark: false);

        CreateControls();
    }

    // ═══════════════════════════════════════════════════════════════
    // Création des contrôles
    // ═══════════════════════════════════════════════════════════════
    private void CreateControls()
    {
        var hInstance = Win32.GetModuleHandleW(null);
        int margin = S(BASE_MARGIN);
        int winW = S(BASE_WIN_W);
        int bottomY = S(BASE_WIN_H) - S(BASE_BOTTOM_MARGIN);
        int linkH = S(28);

        // ══ Navigation ══ (largeur initiale = minimum ; ajustée dynamiquement dans UpdateStepVisibility)
        _hWndBtnNext = Win32.CreateWindowExW(0, "BUTTON", L.Onboarding_Next,
            Win32.WS_CHILD | Win32.WS_VISIBLE | 0x0001 | Win32.WS_TABSTOP,
            winW - margin - S(BASE_BTN_W_NEXT_MIN), bottomY, S(BASE_BTN_W_NEXT_MIN), S(BASE_BTN_H),
            _hWnd, (IntPtr)IDC_BTN_NEXT, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndBtnNext, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);

        _hWndBtnPrev = Win32.CreateWindowExW(0, "BUTTON", L.Onboarding_Prev,
            Win32.WS_CHILD | Win32.WS_TABSTOP,
            margin, bottomY, S(BASE_BTN_W_PREV), S(BASE_BTN_H),
            _hWnd, (IntPtr)IDC_BTN_PREV, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndBtnPrev, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);

        // Bouton « Essayer maintenant » — visible uniquement etape 1 tant que les exercices
        // n'ont pas ete completes une fois. Positionne dans UpdateStepVisibility a gauche du
        // bouton « Suivant », pour que l'utilisateur puisse choisir : essayer maintenant OU
        // passer directement a l'etape 2.
        _hWndBtnTry = Win32.CreateWindowExW(0, "BUTTON", TryButtonText,
            Win32.WS_CHILD | Win32.WS_TABSTOP,
            0, bottomY, S(BASE_BTN_W_NEXT_MIN), S(BASE_BTN_H),
            _hWnd, (IntPtr)IDC_BTN_TRY, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndBtnTry, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);

        // Sous-classer les boutons pour relayer les flèches au WndProc parent (sinon Windows
        // les intercepte pour la navigation entre boutons et le WM_KEYDOWN principal n'arrive jamais).
        Win32.SetWindowSubclass(_hWndBtnNext, _buttonArrowSubclassProc, (UIntPtr)20, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndBtnPrev, _buttonArrowSubclassProc, (UIntPtr)21, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndBtnTry, _buttonArrowSubclassProc, (UIntPtr)22, IntPtr.Zero);
        // Audit UX 1.3.0, #3 : boutons clairs de Windows 11, « Suivant » porte l'accent.
        // Refonte du 15/10 : palette papier, le bleu des cadres de l'étape 1.
        LightButton.Attach(_hWndBtnNext, paper: true);
        LightButton.Attach(_hWndBtnPrev, paper: true);
        LightButton.Attach(_hWndBtnTry, paper: true);

        // ══ Étape 3 — Liens et checkboxes ══
        // Positions initiales temporaires — repositionnés dans RepositionControls
        int y = 0;
        _hWndLinkLessons = Win32.CreateWindowExW(0, "STATIC", L.Onboarding_LinkLessons,
            Win32.WS_CHILD | Win32.SS_NOTIFY | Win32.WS_TABSTOP, margin, y, S(240), linkH,
            _hWnd, (IntPtr)IDC_LINK_LESSONS, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndLinkLessons, Win32.WM_SETFONT, _hFontLinkStrong, (IntPtr)1);
        _links.Attach(_hWndLinkLessons, 6);

        _hWndLinkGuide = Win32.CreateWindowExW(0, "STATIC", L.Onboarding_LinkGuide,
            Win32.WS_CHILD | Win32.SS_NOTIFY | Win32.WS_TABSTOP, margin, y, S(200), linkH,
            _hWnd, (IntPtr)IDC_LINK_GUIDE, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndLinkGuide, Win32.WM_SETFONT, _hFontLinkStrong, (IntPtr)1);
        _links.Attach(_hWndLinkGuide, 1);

        _hWndLinkFeedback = Win32.CreateWindowExW(0, "STATIC", L.Tray_MenuGiveFeedback,
            Win32.WS_CHILD | Win32.SS_NOTIFY | Win32.WS_TABSTOP, margin, y, S(280), linkH,
            _hWnd, (IntPtr)IDC_LINK_FEEDBACK, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndLinkFeedback, Win32.WM_SETFONT, _hFontLinkStrong, (IntPtr)1);
        _links.Attach(_hWndLinkFeedback, 3);

        _hWndLinkDiscord = Win32.CreateWindowExW(0, "STATIC", L.Onboarding_LinkDiscord,
            Win32.WS_CHILD | Win32.SS_NOTIFY | Win32.WS_TABSTOP, margin, y, S(380), linkH,
            _hWnd, (IntPtr)IDC_LINK_DISCORD, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndLinkDiscord, Win32.WM_SETFONT, _hFontLinkStrong, (IntPtr)1);
        _links.Attach(_hWndLinkDiscord, 5);

        _hWndChkAutoStart = Win32.CreateWindowExW(0, "BUTTON", L.Onboarding_ChkAutoStart,
            Win32.WS_CHILD | Win32.BS_AUTOCHECKBOX | Win32.WS_TABSTOP,
            margin, y, S(320), S(26),
            _hWnd, (IntPtr)IDC_CHK_AUTOSTART, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndChkAutoStart, Win32.WM_SETFONT, _hFontText, (IntPtr)1);
        RefreshAutoStartCheckbox();

        _hWndChkDontShow = Win32.CreateWindowExW(0, "BUTTON", L.Onboarding_ChkDontShow,
            Win32.WS_CHILD | Win32.BS_AUTOCHECKBOX | Win32.WS_TABSTOP,
            margin, y, S(280), S(26),
            _hWnd, (IntPtr)IDC_CHK_DONT_SHOW, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndChkDontShow, Win32.WM_SETFONT, _hFontText, (IntPtr)1);

        // Opt-in Défi du jour (v1.2.0) — décochée par défaut ; resynchronisée sur
        // ConfigManager.TrainingEnabled à chaque affichage de l'étape 3 (cf. UpdateStepVisibility),
        // au cas où l'utilisateur l'aurait déjà activée depuis les Paramètres.
        _hWndChkTraining = Win32.CreateWindowExW(0, "BUTTON", L.Onboarding_ChkTraining,
            Win32.WS_CHILD | Win32.BS_AUTOCHECKBOX | Win32.WS_TABSTOP,
            margin, y, S(320), S(26),
            _hWnd, (IntPtr)IDC_CHK_TRAINING, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndChkTraining, Win32.WM_SETFONT, _hFontText, (IntPtr)1);

        // ══ Étape « Vos langues » — champ d'essai ══
        // Un vrai EDIT : la frappe y arrive comme dans n'importe quelle application, après la
        // traduction du crochet clavier (ù puis b → ɓ). Placé dans UpdateStepVisibility.
        _hWndTrial = Win32.CreateWindowExW(0, "EDIT", "",
            Win32.WS_CHILD | Win32.WS_TABSTOP | Win32.ES_CENTER | Win32.ES_AUTOHSCROLL,
            margin, y, S(80), S(40),
            _hWnd, (IntPtr)IDC_EDIT_TRIAL, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndTrial, Win32.WM_SETFONT, _hFontTrial, (IntPtr)1);
        RefreshTrialAccessibility(announce: false);

        // K6 (accessibilité 1.3.0) : le drapeau de langue n'était qu'une zone GDI du header,
        // hors d'atteinte au clavier. Ce bouton se pose dessus (SyncFlagButton), se tabule,
        // s'active par Entrée ou Espace et dessine son focus. Son texte, jamais affiché, est
        // son nom accessible. Créé masqué : le premier dessin du header le place et l'affiche.
        _hWndBtnFlag = Win32.CreateWindowExW(0, "BUTTON", FlagButtonName,
            Win32.WS_CHILD | Win32.WS_TABSTOP | Win32.BS_OWNERDRAW,
            0, 0, S(BASE_FLAG_W), S(BASE_FLAG_H),
            _hWnd, (IntPtr)IDC_BTN_FLAG, hInstance, IntPtr.Zero);
    }

    // ═══════════════════════════════════════════════════════════════
    // Visibilité des contrôles selon l'étape
    // ═══════════════════════════════════════════════════════════════
    private void UpdateStepVisibility()
    {
        // Topmost uniquement sur l'etape 1 (presentation des features). Etapes 2 et 3 :
        // laisser l'utilisateur consulter les ressources mentionnees (guide, Discord, feedback)
        // sans que le wizard ne masque le navigateur. Cf. plan UX 2026-05-02 Q3.
        // Exception : quand le mini-tutoriel (LearningModule) est ouvert, ne pas se remettre
        // topmost — sinon un rafraichissement (ex. bascule de langue depuis le tray) ferait
        // repasser l'onboarding devant le tutoriel actif (constat smoke test 2026-07-17).
        IntPtr insertAfter = (_currentStep == 0 && _learningModule == null)
            ? Win32.HWND_TOPMOST : Win32.HWND_NOTOPMOST;
        Win32.SetWindowPos(_hWnd, insertAfter, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);

        // Champ d'essai : visible à l'étape « Vos langues » seulement. Le masquer alors qu'il a
        // le focus le laisserait sur un contrôle invisible : le focus passe à « Suivant ».
        bool trialVisible = _currentStep == OnboardingStep.Languages;
        if (!trialVisible && Win32.GetFocus() == _hWndTrial)
            Win32.SetFocus(_hWndBtnNext);
        Win32.ShowWindow(_hWndTrial, trialVisible ? 1 : 0);
        if (trialVisible)
            PositionTrialField();

        int step3Vis = _currentStep == OnboardingStep.Last ? 1 : 0;
        Win32.ShowWindow(_hWndLinkLessons, step3Vis);
        Win32.ShowWindow(_hWndLinkGuide, step3Vis);
        Win32.ShowWindow(_hWndLinkFeedback, step3Vis);
        // Canal sobre : aucune invitation Discord, décision D3 du 2026-08-19. C'est la
        // dernière ligne de la grille de liens de l'étape 3 : rien ne remonte, aucune place
        // ne reste vide. Depuis le lot C, une politique d'entreprise peut l'éteindre aussi
        // sur les autres canaux.
        Win32.ShowWindow(_hWndLinkDiscord, PolicyManager.ExternalLinksEnabledNow ? step3Vis : 0);
        Win32.ShowWindow(_hWndChkAutoStart, step3Vis);
        Win32.ShowWindow(_hWndChkDontShow, step3Vis);
        // Opt-in des rappels du Défi : masqué avec le Défi en 1.3.0 (DailyChallenge.Enabled).
        Win32.ShowWindow(_hWndChkTraining, DailyChallenge.Enabled ? step3Vis : 0);
        if (step3Vis == 1)
        {
            // Resynchronisation à chaque affichage de l'étape 3 : l'utilisateur a pu
            // activer/désactiver l'opt-in depuis les Paramètres pendant que l'onboarding
            // est ouvert (même principe que RefreshAutoStartCheckbox pour l'autostart).
            Win32.SendMessageW(_hWndChkTraining, Win32.BM_SETCHECK,
                ConfigManager.TrainingEnabled ? (IntPtr)Win32.BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
        }

        Win32.ShowWindow(_hWndBtnPrev, _currentStep > 0 ? 1 : 0);

        // 3 etats sur l'etape 1 :
        //   A — jamais essaye        : seul « Essayer maintenant » a la position de droite habituelle
        //   B — essaye, non complete : « Essayer maintenant » (gauche) + « Suivant » (droite)
        //   C — complete             : seul « Suivant » a la position de droite habituelle
        // Etapes 2/3 : seul « Suivant » / « C'est parti ! » a la position habituelle.
        bool isStep1NotAttempted = _currentStep == 0 && !_learningModuleAttempted && !_learningModuleDone; // etat A
        // Audit UX 1.3.0, #3 : à l'état A, « Essayer maintenant » est la seule action et porte
        // l'accent ; sinon « Suivant » le porte. Entrée presse toujours le bouton focalisé.
        Win32.SendMessageW(_hWndBtnTry, 0x00F4 /* BM_SETSTYLE */,
            (IntPtr)(isStep1NotAttempted ? Win32.BS_DEFPUSHBUTTON : Win32.BS_PUSHBUTTON), (IntPtr)1);
        bool isStep1Attempted    = _currentStep == 0 &&  _learningModuleAttempted && !_learningModuleDone; // etat B
        // etat C = _currentStep == 0 && _learningModuleDone (ou etapes 2/3) → comportement standard

        string nextText = _currentStep == OnboardingStep.Last
            ? (_activationAccepted ? L.Onboarding_LetsGo : L.Onboarding_Activate) : L.Onboarding_Next;
        Win32.SetWindowTextW(_hWndBtnNext, nextText);

        int btnWinW = S(BASE_WIN_W);
        int btnMargin = S(BASE_MARGIN);
        int btnBottomY = S(BASE_WIN_H) - S(BASE_BOTTOM_MARGIN);

        if (isStep1NotAttempted)
        {
            // Etat A : seul « Essayer maintenant », position droite. « Suivant » cache.
            Win32.ShowWindow(_hWndBtnNext, 0);
            Win32.ShowWindow(_hWndBtnTry, 1);
            string tryText = TryButtonText;
            IntPtr hdc = Win32.GetDC(_hWnd);
            int tryTextW;
            try { tryTextW = MeasureSingleLineWidth(hdc, _hFontButton, tryText); }
            finally { Win32.ReleaseDC(_hWnd, hdc); }
            int tryWidth = Math.Max(S(BASE_BTN_W_NEXT_MIN), tryTextW + S(BASE_BTN_TEXT_PAD * 2));
            int tryX = btnWinW - btnMargin - tryWidth;
            Win32.MoveWindow(_hWndBtnTry, tryX, btnBottomY, tryWidth, S(BASE_BTN_H), true);
            _step1ButtonsLeft = tryX;
        }
        else if (isStep1Attempted)
        {
            // Etat B : « Essayer maintenant » a gauche, « Suivant » a droite (gap 12px).
            Win32.ShowWindow(_hWndBtnNext, 1);
            Win32.ShowWindow(_hWndBtnTry, 1);
            var nextGeomB = ComputeNextButtonGeometry(nextText, btnWinW, btnMargin);
            Win32.MoveWindow(_hWndBtnNext, nextGeomB.x, btnBottomY, nextGeomB.width, S(BASE_BTN_H), true);

            string tryText = TryButtonText;
            IntPtr hdc = Win32.GetDC(_hWnd);
            int tryTextW;
            try { tryTextW = MeasureSingleLineWidth(hdc, _hFontButton, tryText); }
            finally { Win32.ReleaseDC(_hWnd, hdc); }
            int tryWidth = Math.Max(S(BASE_BTN_W_NEXT_MIN), tryTextW + S(BASE_BTN_TEXT_PAD * 2));
            int tryX = nextGeomB.x - S(12) - tryWidth;
            Win32.MoveWindow(_hWndBtnTry, tryX, btnBottomY, tryWidth, S(BASE_BTN_H), true);
            _step1ButtonsLeft = tryX;
        }
        else
        {
            // Etat C ou etapes 2/3 : seul « Suivant »/« C'est parti ! » visible, position droite.
            Win32.ShowWindow(_hWndBtnTry, 0);
            Win32.ShowWindow(_hWndBtnNext, 1);
            var nextGeomC = ComputeNextButtonGeometry(nextText, btnWinW, btnMargin);
            Win32.MoveWindow(_hWndBtnNext, nextGeomC.x, btnBottomY, nextGeomC.width, S(BASE_BTN_H), true);
            _step1ButtonsLeft = nextGeomC.x;
        }

        if (_currentStep == OnboardingStep.Last)
            RepositionControls();

        // Le Narrateur ne lit pas le dessin de l'étape 1 (clavier et notes) : son contenu passe
        // en description du bouton « Activer et essayer », qui n'existe qu'à cette étape.
        if (_currentStep == OnboardingStep.Changes)
            AccessibleName.TrySetDescription(_hWndBtnTry, Step1AccessibleSummary());

        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    private static string Step1AccessibleSummary() => string.Join(" ",
        L.Onboarding_Step1Title + ".", L.Onboarding_Step1Subtitle,
        "@ # : " + L.Onboarding_NoteAt + ".", "É È Ç À : " + L.Onboarding_NoteCaps + ".",
        "{ } \\ | [ ] : " + L.Onboarding_NoteCoding + ".", ". : " + L.Onboarding_NotePeriod + ".",
        "´ ` ~ : " + L.Onboarding_NoteAccents + ".", L.Onboarding_PrivacyReassurance, L.Onboarding_TurnOffAnyTime);

    /// <summary>Avance d'une étape (bouton Suivant ou flèche) ; la dernière arme
    /// <c>_step3Reached</c>, qui autorise l'enregistrement des préférences à la fermeture.</summary>
    private void GoToNextStep()
    {
        if (_currentStep >= OnboardingStep.Last) return;
        _currentStep++;
        if (_currentStep == OnboardingStep.Last) _step3Reached = true;
        UpdateStepVisibility();
    }

    public void Show()
    {
        _currentStep = 0;
        _step3Reached = false; // sera mis a true a la 1ere transition vers l'etape 3
        // Étape 3 : « Lancer au démarrage » cochée par défaut au premier accueil, sinon l'état
        // réel ; un refus dans Windows n'est jamais contourné (AutoStart.DefaultOnboardingCheck,
        // décision du 2026-09-23). Rien n'est appliqué ici : Close() ne persiste la case que si
        // l'étape 3 a été vue, l'activation acceptée, et la case validée par le bouton
        // final ou changée par l'utilisateur (audit 24/09, B2).
        // « Ne plus afficher »
        // UNCHECKED par defaut (v0.9.7.1) -> l'opt-out doit etre explicite. Avant, la default-checked
        // combinee a la persistance dans Close() faisait que tout fermeture (X, Esc, Quit, C'est parti!)
        // declenchait un opt-out permanent, meme si l'utilisateur n'avait jamais atteint l'etape 3.
        _autoStartShownChecked = AutoStart.DefaultOnboardingCheck();
        Win32.SendMessageW(_hWndChkAutoStart, Win32.BM_SETCHECK, _autoStartShownChecked ? (IntPtr)Win32.BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
        Win32.SendMessageW(_hWndChkDontShow, Win32.BM_SETCHECK, IntPtr.Zero, IntPtr.Zero);
        // L'essai de l'étape « Vos langues » repart vide à chaque accueil.
        Win32.SetWindowTextW(_hWndTrial, "");
        // Sync bidirectionnel des flags avec la progression persistee. Seuils alignes avec
        // la condition d'auto-show ([TrayApplication.cs] : LearningMaxStepCompleted < 3) :
        //   - 0 exo               -> etat A (« Essayer maintenant » seul)
        //   - 1-2 exos            -> etat B (« Essayer maintenant » + « Suivant »)
        //   - 3+ exos             -> etat C (« Suivant » seul, l'utilisateur a fait l'essentiel)
        int completed = ConfigManager.LearningMaxStepCompleted;
        _learningModuleAttempted = completed >= 1;
        _learningModuleDone = completed >= TutorialSteps.DoneThreshold;
        UpdateStepVisibility();
        Win32.ShowWindow(_hWnd, 1);
        Win32.SetForegroundWindow(_hWnd);
        _visible = true;
        DialogNavigation.Register(_hWnd);
        Win32.SetFocus(_currentStep == 0 && !_learningModuleDone ? _hWndBtnTry : _hWndBtnNext);
    }

    /// <summary>Remet l'onboarding à zéro (étape 1, exercice non fait) et réautorise son affichage automatique.</summary>
    public void ResetState()
    {
        // Reset les flags AVANT Close() : le callback OnClosed declenche UpdateStepVisibility,
        // qui dessinerait sinon brievement l'etat B (essaye non complete, 2 boutons) avant
        // que le Show suivant repositionne en etat A (1 bouton). Avec les flags reset d'abord,
        // UpdateStepVisibility passe directement en etat A.
        _currentStep = 0;
        _learningModuleDone = false;
        _learningModuleAttempted = false;
        _step3Reached = false;
        ConfigManager.SetShowOnboardingAtStartup(true);

        // Si la fenetre des exercices est ouverte, la fermer proprement.
        // Close() declenche le callback OnClosed qui Dispose et met _learningModule = null.
        _learningModule?.Close();
    }

    /// <summary>
    /// Faut-il persister une case de l'étape 3 à la fermeture de l'accueil ? Décision pure
    /// (audit 24/09, B2). Rien tant que l'étape 3 n'a pas été vue ou que l'activation n'a
    /// pas été acceptée (décision v0.9.7.1). Ensuite, seul le bouton final vaut validation :
    /// la case « Lancer au démarrage » est cochée par défaut depuis la 1.3.0, et la croix,
    /// Échap ou un WM_CLOSE l'appliquaient comme un accord. Sur ces gestes, une case n'est
    /// persistée que si l'utilisateur l'a lui-même changée depuis son affichage.
    /// </summary>
    internal static bool ShouldApplyPreferenceOnClose(bool validated, bool step3Reached,
        bool activationAccepted, bool checkedNow, bool shownChecked)
    {
        if (!step3Reached || !activationAccepted) return false;
        return validated || checkedNow != shownChecked;
    }

    /// <summary>
    /// Ramène au premier plan la fenêtre déjà affichée, sans la remettre à l'étape 1
    /// comme le ferait <see cref="Show"/> (relance depuis Démarrer, audit 24/09).
    /// </summary>
    public void BringToFront()
    {
        if (!_visible || _hWnd == IntPtr.Zero) return;
        Win32.ShowWindow(_hWnd, 9); // SW_RESTORE : réaffiche aussi une fenêtre réduite
        Win32.SetForegroundWindow(_hWnd);
    }

    /// <param name="validated">Vrai seulement depuis le bouton final de l'étape 3 (ou la
    /// flèche qui l'actionne), activation acceptée. Faux pour la croix, Échap, IDCANCEL et
    /// tout WM_CLOSE, y compris celui qu'un updater MSIX envoie à la fenêtre masquée.</param>
    public void Close(bool validated)
    {
        // Ne persister les preferences visibles QUE si l'utilisateur a vu l'etape 3.
        // Sinon, fermer le wizard a l'etape 1 ou 2 (croix, Esc, Quitter) ne touche ni
        // ShowOnboardingAtStartup ni l'autostart. Les checkboxes sont initialisees par
        // defaut dans Show(), mais ne deviennent un choix utilisateur qu'une fois visibles.
        //
        // Audit 24/09 (B2) : l'étape 3 vue ne suffit plus, voir ShouldApplyPreferenceOnClose.
        // Le drapeau est consommé dès l'entrée : un WM_CLOSE ultérieur sur la fenêtre
        // masquée (updater MSIX, cf. AutoStart.SetStartupTask), ou reçu pendant la boîte
        // d'erreur ci-dessous, ne réapplique plus rien.
        bool step3Reached = _step3Reached;
        _step3Reached = false;
        if (step3Reached && _activationAccepted)
        {
            // « Ne plus afficher » est toujours affichée décochée (Show) : sur croix ou
            // Échap, seule une case cochée par l'utilisateur est un choix. Avant, un
            // simple Échap à l'étape 3 réécrivait « afficher au démarrage » et défaisait
            // en silence un refus posé dans les Paramètres ou hérité de la v1.1.
            bool dontShow = Win32.SendMessageW(_hWndChkDontShow, Win32.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)Win32.BST_CHECKED;
            if (ShouldApplyPreferenceOnClose(validated, step3Reached, _activationAccepted, dontShow, shownChecked: false))
                ConfigManager.SetShowOnboardingAtStartup(!dontShow);

            var autoStartState = Win32.SendMessageW(_hWndChkAutoStart, Win32.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero);
            bool autoStart = autoStartState == (IntPtr)Win32.BST_CHECKED;
            if (ShouldApplyPreferenceOnClose(validated, step3Reached, _activationAccepted, autoStart, _autoStartShownChecked))
            {
                bool autoStartWasRegistered = AutoStart.IsRegistered;
                bool autoStartSaved = AutoStart.Set(autoStart);
                RefreshAutoStartCheckbox();
                if (!autoStartSaved)
                    ShowAutoStartError();
                // Ce bloc n'est atteint que si _step3Reached : la case a donc été vue, et la
                // modifier est un choix, qui éteint la relance dans un sens comme dans
                // l'autre (R2 de l'audit v1.2.0). Une case jamais vue reste hors de ce
                // chemin, la décision v0.9.7.1 est intacte.
                // Depuis la case cochée par défaut (v1.3.0), la décocher est aussi un choix.
                else if (autoStart != autoStartWasRegistered || autoStart != _autoStartShownChecked)
                    AutoStartNudge.MarkPromptShown();
            }
            // Croix ou Échap sans toucher la case : rien n'est enregistré, la relance
            // unique (AutoStartNudge) reste due, comme pour qui ferme avant l'étape 3.
        }

        Win32.ShowWindow(_hWnd, 0);
        _visible = false;
        // Après le masquage : l'appelant peut afficher ce qu'il avait différé sans que la
        // notification se retrouve derrière la fenêtre d'accueil.
        try { OnClosed?.Invoke(); } catch (Exception ex) { ConfigManager.Log("OnboardingWindow.OnClosed", ex); }
    }

    // ═══════════════════════════════════════════════════════════════
    // WndProc
    // ═══════════════════════════════════════════════════════════════
    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
        if (_inputPaused && IsPausedInputMessage(msg))
            return IntPtr.Zero;

        switch (msg)
        {
            case Win32.WM_PAINT:
                OnPaint(hWnd);
                return IntPtr.Zero;

            case Win32.WM_ERASEBKGND:
                return (IntPtr)1;

            case Win32.WM_DPICHANGED:
            {
                float screenScale = NativeWindow.DpiFromChange(_hWnd, wParam) / 96f;
                NativeWindow.MoveToSuggestedRect(_hWnd, lParam);
                // Après le déplacement : le plafond se prend sur la zone de travail du nouvel écran.
                _dpiScale = FittedScale(screenScale);
                RecreateFonts();
                if (_dpiScale < screenScale)
                {
                    ResizeWindow();
                    NativeWindow.KeepInWorkArea(_hWnd);
                }
                RepositionControls();
                UpdateStepVisibility(); // repositionne btnNext + btnTry selon l'etat (3 etats sur etape 1)
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                return IntPtr.Zero;
            }

            case Win32.WM_DRAWITEM:
            {
                var dis = Marshal.PtrToStructure<Win32.DRAWITEMSTRUCT>(lParam);
                if (dis.CtlID == IDC_BTN_FLAG)
                {
                    DrawFlagButton(in dis);
                    return (IntPtr)1;
                }
                break;
            }

            case Win32.WM_COMMAND:
                int id = wParam.ToInt32() & 0xFFFF;
                int code = (wParam.ToInt32() >> 16) & 0xFFFF;
                switch (id)
                {
                    case DialogNavigation.IDOK:
                    {
                        IntPtr focused = Win32.GetFocus();
                        IntPtr button = DialogNavigation.ButtonToPressOnEnter(id, focused,
                            new[] { _hWndBtnNext, _hWndBtnPrev, _hWndBtnTry, _hWndBtnFlag });
                        if (button != IntPtr.Zero) Win32.SendMessageW(button, 0x00F5, IntPtr.Zero, IntPtr.Zero);
                        break;
                    }
                    case DialogNavigation.IDCANCEL:
                        Close(validated: false);
                        break;
                    case IDC_BTN_NEXT:
                        // Avant la dernière étape : passer a la suivante. Dernière : fermer l'onboarding.
                        // (Le bouton « Essayer maintenant » est un controle distinct IDC_BTN_TRY.)
                        if (_currentStep < OnboardingStep.Last) GoToNextStep();
                        else if (RequestActivation()) Close(validated: true);
                        break;
                    case IDC_EDIT_TRIAL:
                        if (code == Win32.EN_CHANGE) OnTrialChanged();
                        else if (code == EN_SETFOCUS || code == EN_KILLFOCUS)
                        {
                            _trialFocused = code == EN_SETFOCUS;
                            InvalidateTrialBox();
                        }
                        break;
                    case IDC_BTN_TRY:
                        // Etape 1 uniquement (visible seulement si !_learningModuleDone).
                        // L'utilisateur reste sur l'etape 1 a la fermeture du module ; les deux
                        // boutons restent presents tant qu'il n'a pas vraiment complete les exercices.
                        if (RequestActivation()) LaunchLearningModule();
                        break;
                    case IDC_BTN_PREV:
                        if (_currentStep > 0) { _currentStep--; UpdateStepVisibility(); }
                        break;
                    case IDC_LINK_FEEDBACK:
                        if (code == 0) OpenLink(ProductIdentity.DiagnosticUrl(ProductIdentity.FeedbackPath,
                            Program.Version, ProductIdentity.OsDescription(), "onboarding")); break;
                    case IDC_LINK_GUIDE:
                        if (code == 0) OpenLink(ProductIdentity.Url("/guide")); break;
                    case IDC_LINK_DISCORD:
                        if (code == 0) OpenLink(ProductIdentity.DiscordInviteUrl); break;
                    case IDC_LINK_LESSONS:
                        if (code == 0) OpenLessonsRequested?.Invoke(); break;
                    case IDC_BTN_FLAG:
                        if (code == 0) ApplyLanguageChange(L.IsEnglish ? "fr" : "en"); break;
                    case IDC_CHK_TRAINING:
                        // Applique immédiatement (pas à la fermeture du wizard) — même pattern
                        // que IDC_CHK_TRAINING dans SettingsWindow.cs.
                        if (code == 0)
                        {
                            bool trainingEnabled = Win32.SendMessageW(_hWndChkTraining, Win32.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)Win32.BST_CHECKED;
                            ConfigManager.SetTrainingEnabled(trainingEnabled);
                        }
                        break;
                }
                return IntPtr.Zero;

            case Win32.WM_CTLCOLOREDIT:
                if (lParam == _hWndTrial)
                {
                    Win32.SetTextColor(wParam, CLR_TEXT);
                    Win32.SetBkColor(wParam, PaperTheme.Surface);
                    return _hFieldBrush;
                }
                break;

            case Win32.WM_CTLCOLORSTATIC:
            {
                IntPtr hdcStatic = wParam;
                IntPtr hCtrl = lParam;
                if (_links.Contains(hCtrl))
                {
                    Win32.SetBkMode(hdcStatic, 1);
                    Win32.SetTextColor(hdcStatic, _links.IsActive(hCtrl) ? CLR_LINK_HOVER : CLR_LINK);
                    return _hPanelBrush;
                }
                if (hCtrl == _hWndChkAutoStart || hCtrl == _hWndChkDontShow || hCtrl == _hWndChkTraining)
                {
                    Win32.SetBkMode(hdcStatic, 1);
                    Win32.SetTextColor(hdcStatic, CLR_TEXT);
                    return _hPanelBrush;
                }
                Win32.SetBkMode(hdcStatic, 1);
                Win32.SetTextColor(hdcStatic, PaperTheme.TextSecondary);
                return _hBgBrush;
            }

            case Win32.WM_SETCURSOR:
                if (_links.Contains(wParam) || wParam == _hWndBtnFlag)
                {
                    Win32.SetCursor(Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32649));
                    return (IntPtr)1;
                }
                // Drapeau de langue : zone GDI du header (pas un contrôle), curseur main au survol.
                if (wParam == _hWnd && IsCursorOverFlag())
                {
                    Win32.SetCursor(Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32649));
                    return (IntPtr)1;
                }
                break;

            case Win32.WM_LBUTTONUP:
            {
                // Clic sur le drapeau de bascule de langue (hit-test sur le rect GDI du header).
                int mx = unchecked((short)(lParam.ToInt64() & 0xFFFF));
                int my = unchecked((short)((lParam.ToInt64() >> 16) & 0xFFFF));
                if (mx >= _flagRect.left && mx < _flagRect.right &&
                    my >= _flagRect.top && my < _flagRect.bottom)
                {
                    ApplyLanguageChange(L.IsEnglish ? "fr" : "en");
                    return IntPtr.Zero;
                }
                break;
            }

            case Win32.WM_KEYDOWN:
            {
                int vk = wParam.ToInt32();
                if (vk == 0x1B) // VK_ESCAPE
                {
                    Close(validated: false);
                    return IntPtr.Zero;
                }
                // Navigation par flèches : ↓/→ = bouton principal, ↑/← = bouton Précédent.
                if (vk == 0x27 || vk == 0x28) // VK_RIGHT, VK_DOWN
                {
                    if (_currentStep == 0 && !_learningModuleDone)
                    {
                        if (_activationAccepted) LaunchLearningModule();
                        else Win32.SetFocus(_hWndBtnTry);
                    }
                    else if (_currentStep < OnboardingStep.Last)
                    {
                        GoToNextStep();
                    }
                    else if (_activationAccepted)
                        Close(validated: true); // flèche = bouton principal (« C'est parti ! »)
                    else
                        Win32.SetFocus(_hWndBtnNext);
                    return IntPtr.Zero;
                }
                if (vk == 0x25 || vk == 0x26) // VK_LEFT, VK_UP
                {
                    if (_currentStep > 0)
                    {
                        _currentStep--;
                        UpdateStepVisibility();
                    }
                    return IntPtr.Zero;
                }
                break;
            }

            case Win32.WM_CLOSE:
                Close(validated: false);
                return IntPtr.Zero;
        }
        }
        catch (Exception ex)
        {
            ConfigManager.Log("Onboarding WndProc", ex);
        }

        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void RefreshAutoStartCheckbox()
    {
        Win32.SendMessageW(_hWndChkAutoStart, Win32.BM_SETCHECK,
            AutoStart.IsRegistered ? (IntPtr)Win32.BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// K6 : nom accessible du bouton du drapeau — l'endonyme de la langue cible, comme les
    /// boutons radio des Paramètres (un nom de langue s'écrit dans sa propre langue).
    /// </summary>
    internal static string FlagButtonName => L.IsEnglish ? "Français" : "English";

    /// <summary>
    /// K6 : cale le bouton du drapeau sur le rect que le header vient de calculer, avec S(3)
    /// de marge pour le cadre de focus, ou le retire de la tabulation quand la langue est
    /// imposée (rect vide). Appelé depuis le dessin du header, seul endroit où ce rect est
    /// connu ; ne touche à rien quand rien n'a changé, pour ne pas relancer de repeint.
    /// </summary>
    private void SyncFlagButton()
    {
        if (_hWndBtnFlag == IntPtr.Zero) return;
        bool show = _flagRect.right > _flagRect.left;
        if (show)
        {
            int pad = S(3);
            var wanted = new Win32.RECT
            {
                left = _flagRect.left - pad,
                top = _flagRect.top - pad,
                right = _flagRect.right + pad,
                bottom = _flagRect.bottom + pad
            };
            if (wanted.left != _flagButtonRect.left || wanted.top != _flagButtonRect.top ||
                wanted.right != _flagButtonRect.right || wanted.bottom != _flagButtonRect.bottom)
            {
                _flagButtonRect = wanted;
                Win32.MoveWindow(_hWndBtnFlag, wanted.left, wanted.top,
                    wanted.right - wanted.left, wanted.bottom - wanted.top, true);
            }
        }
        if (show != _flagButtonShown)
        {
            _flagButtonShown = show;
            Win32.ShowWindow(_hWndBtnFlag, show ? 1 : 0);
        }
    }

    /// <summary>K6 : le même drapeau que le header, plus le cadre de focus clavier.</summary>
    private void DrawFlagButton(in Win32.DRAWITEMSTRUCT dis)
    {
        var rc = dis.rcItem;
        Win32.FillRect(dis.hDC, ref rc, _hBgBrush);
        int pad = S(3);
        var inner = new Win32.RECT
        {
            left = rc.left + pad,
            top = rc.top + pad,
            right = rc.right - pad,
            bottom = rc.bottom - pad
        };
        IntPtr flag = L.IsEnglish ? _gdipFlagFr : _gdipFlagEn;
        IntPtr gfx = IntPtr.Zero;
        if (flag != IntPtr.Zero)
            Win32.GdipCreateFromHDC(dis.hDC, out gfx);
        if (gfx != IntPtr.Zero)
        {
            Win32.GdipSetInterpolationMode(gfx, 7);
            Win32.GdipDrawImageRectI(gfx, flag, inner.left, inner.top,
                inner.right - inner.left, inner.bottom - inner.top);
            Win32.GdipDeleteGraphics(gfx);
            // Filet du site autour du drapeau : le blanc du drapeau anglais se détache du papier.
            PaperDraw.Frame(dis.hDC, new Win32.RECT { left = inner.left - 1, top = inner.top - 1, right = inner.right + 1, bottom = inner.bottom + 1 },
                PaperTheme.Border);
        }
        else
        {
            // Repli si la ressource manque : l'endonyme court, comme le header.
            Win32.SelectObject(dis.hDC, _hFontSmall);
            Win32.SetBkMode(dis.hDC, 1);
            Win32.SetTextColor(dis.hDC, CLR_TEXT);
            Win32.DrawTextW(dis.hDC, L.IsEnglish ? "FR" : "EN", -1, ref inner,
                Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        }
        if ((dis.itemState & Win32.ODS_FOCUS) != 0)
        {
            var focus = new Win32.RECT { left = rc.left + 1, top = rc.top + 1, right = rc.right - 1, bottom = rc.bottom - 1 };
            Win32.DrawFocusRect(dis.hDC, ref focus);
        }
    }

    /// <summary>Curseur au-dessus du drapeau de langue du header (coordonnées client) ?</summary>
    private bool IsCursorOverFlag()
    {
        if (!Win32.GetCursorPos(out var pt)) return false;
        if (!Win32.ScreenToClient(_hWnd, ref pt)) return false;
        return pt.x >= _flagRect.left && pt.x < _flagRect.right &&
               pt.y >= _flagRect.top && pt.y < _flagRect.bottom;
    }

    /// <summary>
    /// Applique un changement de langue depuis le drapeau du header : persiste le choix et
    /// met à jour L.Language. Le rafraîchissement de cette fenêtre passe par l'abonnement à
    /// AppLanguageChanged (OnLanguageChanged) — même chemin que pour une bascule initiée
    /// depuis le menu tray ou les Paramètres.
    /// </summary>
    private void ApplyLanguageChange(string lang)
    {
        if (ConfigManager.AppLanguage == lang) return;
        L.Language = lang; // avant SetAppLanguage : les abonnés à AppLanguageChanged lisent L.*
        ConfigManager.SetAppLanguage(lang);
    }

    /// <summary>Rafraîchit les libellés des contrôles Win32 fixes après un changement de
    /// langue, quelle qu'en soit l'origine (les textes GDI se mettent à jour au repaint).</summary>
    private void OnLanguageChanged()
    {
        RefreshLanguageTexts();
        UpdateStepVisibility();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    /// <summary>Réapplique les libellés des contrôles Win32 dont le texte est fixé une fois à la création.</summary>
    private void RefreshLanguageTexts()
    {
        Win32.SetWindowTextW(_hWndBtnPrev, L.Onboarding_Prev);
        Win32.SetWindowTextW(_hWndBtnTry, TryButtonText);
        Win32.SetWindowTextW(_hWndLinkLessons, L.Onboarding_LinkLessons);
        Win32.SetWindowTextW(_hWndLinkGuide, L.Onboarding_LinkGuide);
        Win32.SetWindowTextW(_hWndLinkFeedback, L.Tray_MenuGiveFeedback);
        Win32.SetWindowTextW(_hWndLinkDiscord, L.Onboarding_LinkDiscord);
        Win32.SetWindowTextW(_hWndChkAutoStart, L.Onboarding_ChkAutoStart);
        Win32.SetWindowTextW(_hWndChkDontShow, L.Onboarding_ChkDontShow);
        Win32.SetWindowTextW(_hWndChkTraining, L.Onboarding_ChkTraining);
        Win32.SetWindowTextW(_hWndBtnFlag, FlagButtonName);
        RefreshTrialAccessibility(announce: false);
    }

    // ═══════════════════════════════════════════════════════════════
    // Étape « Vos langues » — l'essai
    // ═══════════════════════════════════════════════════════════════

    private string ReadTrialText()
    {
        var buffer = new System.Text.StringBuilder(64);
        Win32.GetWindowTextW(_hWndTrial, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    /// <summary>
    /// Le champ ne garde que la dernière lettre tapée, affichée en grand : une nouvelle
    /// frappe remplace la précédente, sans qu'il faille effacer. Puis le message et
    /// l'annonce suivent.
    /// </summary>
    private void OnTrialChanged()
    {
        if (_trialUpdating) return;
        string text = ReadTrialText();
        string letter = LetterTrial.LastLetter(text);
        if (letter != text)
        {
            _trialUpdating = true;
            try
            {
                Win32.SetWindowTextW(_hWndTrial, letter);
                Win32.SendMessageW(_hWndTrial, EM_SETSEL, (IntPtr)letter.Length, (IntPtr)letter.Length);
            }
            finally { _trialUpdating = false; }
        }
        RefreshTrialAccessibility(announce: true);
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    /// <summary>
    /// Nom accessible du champ : la consigne, suivie du résultat quand une lettre est tapée ;
    /// description : le tableau des langues et l'astuce, que le lecteur d'écran ne voit pas
    /// autrement (peints en GDI). <paramref name="announce"/> lève EVENT_OBJECT_NAMECHANGE,
    /// que le lecteur lit sur l'élément focalisé, comme la faute de frappe du tutoriel.
    /// </summary>
    private void RefreshTrialAccessibility(bool announce)
    {
        if (_hWndTrial == IntPtr.Zero) return;
        string message = LetterTrial.Message(ReadTrialText());
        string name = message.Length == 0 ? L.Onboarding_TrialFieldName : L.Onboarding_TrialFieldName + " " + message;
        if (announce) AccessibleName.TrySetAndNotify(_hWndTrial, name);
        else AccessibleName.TrySet(_hWndTrial, name);
        AccessibleName.TrySetDescription(_hWndTrial, OnboardingLanguages.AccessibleSummary() + " "
            + L.Onboarding_LanguagesTipPrefix + $"Ctrl + {L.Settings_ShortcutModifier2} + W" + L.Onboarding_LanguagesTipSuffix);
    }

    private void InvalidateTrialBox() => Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);

    /// <summary>Pose le champ au centre de son cadre peint ; ne bouge rien si rien n'a changé.</summary>
    private void PositionTrialField()
    {
        if (_hWndTrial == IntPtr.Zero || _contentY <= 0) return;
        IntPtr hdc = Win32.GetDC(_hWnd);
        LanguagesLayout layout;
        try { layout = GetLanguagesLayout(hdc, S(BASE_WIN_W), _contentY); }
        finally { Win32.ReleaseDC(_hWnd, hdc); }
        PlaceTrialField(layout);
    }

    private void PlaceTrialField(in LanguagesLayout layout)
    {
        _trialBox = layout.TrialBox;
        var edit = layout.TrialEdit;
        Win32.GetWindowRect(_hWndTrial, out var current);
        var topLeft = new Win32.POINT { x = current.left, y = current.top };
        Win32.ScreenToClient(_hWnd, ref topLeft);
        if (topLeft.x != edit.left || topLeft.y != edit.top
            || current.right - current.left != edit.right - edit.left
            || current.bottom - current.top != edit.bottom - edit.top)
            Win32.MoveWindow(_hWndTrial, edit.left, edit.top, edit.right - edit.left, edit.bottom - edit.top, true);
    }

    private void ShowAutoStartError()
    {
        Win32.MessageBoxW(_hWnd,
            AutoStart.GetFailureMessage(),
            L.Common_ErrorTitle, 0x10);
    }

    private static bool IsPausedInputMessage(uint msg)
    {
        return msg is Win32.WM_KEYDOWN or Win32.WM_KEYUP or Win32.WM_SYSKEYDOWN or Win32.WM_SYSKEYUP
            or Win32.WM_CHAR or Win32.WM_SYSCHAR or Win32.WM_SYSDEADCHAR
            or Win32.WM_COMMAND or Win32.WM_PASTE or Win32.WM_CUT or Win32.WM_CLEAR or Win32.WM_UNDO;
    }

    private void OpenLink(string url)
    {
        Win32.ShellExecuteW(IntPtr.Zero, "open", url, null, null, 1);
        Win32.SetWindowPos(_hWnd, Win32.HWND_NOTOPMOST, 0, 0, 0, 0,
            Win32.SWP_NOSIZE | Win32.SWP_NOMOVE | Win32.SWP_SHOWWINDOW);
    }

    private void LaunchLearningModule()
    {
        ConfigManager.LogCrashTraceDebug($"LaunchLearningModule: enter (Mapper={Mapper != null}, Hook={Hook != null}, AppLayout={AppLayout != null}, _learningModule existing={_learningModule != null})");
        if (Mapper == null || Hook == null || AppLayout == null)
        {
            ConfigManager.LogCrashTraceDebug("LaunchLearningModule: early return — null deps");
            return;
        }

        _learningModule?.Dispose();
        ConfigManager.LogCrashTraceDebug("LaunchLearningModule: about to create new LearningModule");
        _learningModule = new LearningModule(_hWnd, Mapper, Hook, AppLayout);
        _learningModule.SetInputPaused(_inputPaused);
        _learningModuleAttempted = true; // declenche l'apparition du bouton « Suivant » sur l'etape 1
        UpdateStepVisibility();          // rafraichit la disposition des boutons AVANT le Show du LM
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
        ConfigManager.LogCrashTraceDebug("LaunchLearningModule: ctor returned, attaching OnClosed");
        _learningModule.OnClosed = (completed) =>
        {
            // Ne marquer comme "done" que si l'utilisateur a vraiment terminé son parcours
            // (page « Bravo ! » + bouton « Terminer »). Une fermeture précoce (croix X, bouton
            // « Quitter les exercices », Esc) laisse le bouton à « Essayer maintenant ».
            if (completed) _learningModuleDone = true;
            _learningModule?.Dispose();
            _learningModule = null;
            UpdateStepVisibility();
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
        };
        ConfigManager.LogCrashTraceDebug("LaunchLearningModule: about to call Show");
        _learningModule.Show();
        ConfigManager.LogCrashTraceDebug("LaunchLearningModule: Show returned, exiting");
    }

    // Sous-classe pour les boutons Next/Prev/Try : relaie les flèches et Esc au WndProc parent.
    // Sans ce subclass, Windows intercepte les flèches pour la navigation entre boutons
    // tabulables et le WM_KEYDOWN principal n'est jamais appelé.
    private IntPtr ButtonArrowSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (_inputPaused && IsPausedInputMessage(msg))
            return IntPtr.Zero;

        // Les flèches restent gérées par le wizard ; Tab/Entrée/Échap sont routées
        // par IsDialogMessage, comme dans les paramètres.
        if (msg == 0x0087) // WM_GETDLGCODE / DLGC_WANTARROWS
            return (IntPtr)(Win32.DefSubclassProc(hWnd, msg, wParam, lParam).ToInt64() | 0x0001);

        if (msg == Win32.WM_KEYDOWN)
        {
            int vk = wParam.ToInt32();
            if (vk == 0x25 || vk == 0x26 || vk == 0x27 || vk == 0x28 || vk == 0x1B)
            {
                Win32.SendMessageW(_hWnd, Win32.WM_KEYDOWN, wParam, lParam);
                return IntPtr.Zero;
            }
        }
        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    // ═══════════════════════════════════════════════════════════════
    // Rendu — Dispatcher
    // ═══════════════════════════════════════════════════════════════
    private void OnPaint(IntPtr hWnd)
    {
        using var paint = new PaintBuffer(hWnd);
        var clientRect = paint.Client;
        int cw = clientRect.right;
        int ch = clientRect.bottom;
        var hdc = paint.Hdc;

        Win32.FillRect(hdc, ref clientRect, _hBgBrush);
        Win32.SetBkMode(hdc, 1);

        Win32.GdipCreateFromHDC(hdc, out IntPtr gfx);
        Win32.GdipSetSmoothingMode(gfx, 4);
        Win32.GdipSetInterpolationMode(gfx, 7);
        Win32.GdipSetTextRenderingHint(gfx, 5);


        int y = 0;
        DrawHeader(hdc, gfx, cw, ref y);
        _contentY = y; // Stocker pour le positionnement des contrôles

        switch (_currentStep)
        {
            case OnboardingStep.Changes: PaintStep1(hdc, gfx, cw, y); break;
            case OnboardingStep.Languages: PaintLanguages(hdc, cw, y); break;
            case OnboardingStep.HowTo: PaintStep2(hdc, gfx, cw, ch, y); break;
            case OnboardingStep.Resources: PaintStep3(hdc, gfx, cw, ch, y); break;
        }

        Win32.GdipDeleteGraphics(gfx);
    }

    // ═══════════════════════════════════════════════════════════════
    // En-tête (refonte du 15/10) : marque, jalons des 4 étapes, version et drapeau sur une ligne
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Pixels de la maquette (fenêtre de 720 × 540 à 100 %) à l'échelle courante.</summary>
    private int Px(float px) => (int)Math.Round(px * _dpiScale);

    /// <summary>Écrit <paramref name="text"/> centré verticalement sur <paramref name="midY"/> ; rend l'abscisse de fin.</summary>
    private static int DrawMiddle(IntPtr hdc, string text, FontSpec font, int x, int midY, uint color)
    {
        var (ascent, descent) = PaperText.Metrics(hdc, font);
        return PaperText.Draw(hdc, text, font, x, midY + (ascent - descent) / 2, color);
    }

    private void DrawHeader(IntPtr hdc, IntPtr gfx, int cw, ref int y)
    {
        int margin = Px(28);
        int rowTop = Px(20), rowH = Px(26);
        int midY = rowTop + rowH / 2;
        int x = margin;
        if (_gdipLogo != IntPtr.Zero)
        {
            Win32.GdipDrawImageRectI(gfx, _gdipLogo, x, rowTop, rowH, rowH);
            x += rowH + Px(9);
        }
        x = DrawMiddle(hdc, ProductIdentity.DisplayName, FontSpec.Of(AppFonts.Title, 17, TypeRamp.Semibold, _dpiScale),
            x, midY, CLR_TITLE) + Px(12);

        // Jalons : étapes passées à l'encre, étape courante à l'action, suivantes au filet. La
        // couleur n'est pas seule porteuse : le libellé dit l'étape en toutes lettres.
        int segW = Px(22), segH = Math.Max(2, Px(3)), segGap = Px(4);
        for (int i = 0; i < OnboardingStep.Count; i++)
        {
            var seg = new Win32.RECT { left = x, top = midY - segH / 2, right = x + segW, bottom = midY - segH / 2 + segH };
            uint color = i < _currentStep ? CLR_TEXT : i == _currentStep ? CLR_PROGRESS_ACTIVE : CLR_PROGRESS_INACTIVE;
            GdiHelpers.FillSolidRect(hdc, seg, color);
            x += segW + segGap;
        }
        var caption = FontSpec.Of(AppFonts.Text, 12, TypeRamp.Regular, _dpiScale);
        DrawMiddle(hdc, OnboardingStep.Label(_currentStep), caption, x - segGap + Px(10), midY, CLR_REASSURE);

        // Drapeau de bascule de langue (langue cible : anglais si l'interface est en français,
        // français sinon), mêmes assets que le bandeau du site ; la version à sa gauche. Le
        // bouton K6 se pose sur _flagRect (SyncFlagButton) et dessine le drapeau.
        int flagW = Px(27), flagH = Px(18);
        int flagLeft = cw - margin - flagW;
        int versionRight;
        // Langue imposée par une politique d'entreprise (lot C) : ni drapeau dessiné, ni zone
        // cliquable. Un drapeau qui ne bascule rien se lit comme une panne. Le rect vide
        // ferme aussi le hit-test du survol, qui lit le même champ.
        if (PolicyManager.LanguageIsManagedNow)
        {
            _flagRect = default;
            versionRight = cw - margin;
        }
        else
        {
            _flagRect = new Win32.RECT { left = flagLeft, top = midY - flagH / 2, right = flagLeft + flagW, bottom = midY - flagH / 2 + flagH };
            versionRight = flagLeft - Px(8);
        }
        SyncFlagButton();

        string versionText = "v" + Program.Version;
        DrawMiddle(hdc, versionText, caption, versionRight - PaperText.Width(hdc, versionText, caption), midY, CLR_REASSURE);

        y = rowTop + rowH + Px(18);
    }
    // ═══════════════════════════════════════════════════════════════
    // Mise en page de l'étape Ressources, mesures
    // ═══════════════════════════════════════════════════════════════
    private void GetStep3Layout(int topY, int winW,
        out Win32.RECT resourcesPanel, out Win32.RECT prefsPanel,
        out int linksX, out int linksWidth, out int linkStartY, out int linkRowH, out int linkControlHeight,
        out int checkboxX, out int checkboxWidth, out int checkboxY, out int checkboxSpacing, out int checkboxHeight,
        out int checkboxTrainingY, out int trainingDescY, out int trainingDescHeight)
    {
        int margin = ColumnMargin;
        int panelWidth = winW - margin * 2;
        int panelPaddingX = S(18);
        int resourcePaddingTop = S(16);
        int resourcePaddingBottom = S(12);
        int prefsPaddingTop = S(16);
        int prefsPaddingBottom = S(16);
        int panelGap = S(14);

        IntPtr hdc = Win32.GetDC(_hWnd);
        try
        {
            int pageTitleHeight = MeasureSingleLineHeight(hdc, _hFontPageTitle);
            linkControlHeight = Math.Max(S(28), MeasureSingleLineHeight(hdc, _hFontLinkStrong) + S(6));
            linkRowH = linkControlHeight + S(2);
            int resourcesHeight = resourcePaddingTop + linkRowH * 4 + resourcePaddingBottom;
            int panelTop = topY + pageTitleHeight + S(12);

            resourcesPanel = new Win32.RECT
            {
                left = margin,
                top = panelTop,
                right = margin + panelWidth,
                bottom = panelTop + resourcesHeight
            };

            linksX = resourcesPanel.left + panelPaddingX;
            linksWidth = panelWidth - panelPaddingX * 2;
            linkStartY = resourcesPanel.top + resourcePaddingTop;

            checkboxX = margin + panelPaddingX;
            checkboxWidth = panelWidth - panelPaddingX * 2;
            checkboxHeight = Math.Max(S(26), MeasureSingleLineHeight(hdc, _hFontBold) + S(10));
            int prefsTitleHeight = MeasureSingleLineHeight(hdc, _hFontPageTitle);
            int prefsTop = resourcesPanel.bottom + panelGap + prefsTitleHeight + S(8);
            checkboxY = prefsTop + prefsPaddingTop;
            checkboxSpacing = checkboxHeight + S(10);
            // 3e case « Défi du jour » (v1.2.0) + son texte descriptif sur 1-2 lignes,
            // sous les 2 cases existantes (même colonne, même largeur).
            checkboxTrainingY = checkboxY + checkboxSpacing * 2;
            trainingDescY = checkboxTrainingY + checkboxHeight + S(2);
            int prefsHeight;
            if (DailyChallenge.Enabled)
            {
                trainingDescHeight = MeasureTextHeight(hdc, _hFontReassure, L.Onboarding_ChkTrainingDesc, checkboxWidth);
                prefsHeight = prefsPaddingTop + checkboxHeight * 3 + S(10) * 2 + S(2) + trainingDescHeight + prefsPaddingBottom;
            }
            else
            {
                // 1.3.0 : Défi masqué, le panneau ne garde que deux cases.
                trainingDescHeight = 0;
                prefsHeight = prefsPaddingTop + checkboxHeight * 2 + S(10) + prefsPaddingBottom;
            }
            prefsPanel = new Win32.RECT
            {
                left = margin,
                top = prefsTop,
                right = margin + panelWidth,
                bottom = prefsTop + prefsHeight
            };
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }
    }

    // Méthodes GDI factorisées dans GdiHelpers.cs — wrappers d'instance pour le DPI scaling
    private int MeasureTextHeight(IntPtr hdc, IntPtr hFont, string text, int width,
        uint format = Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX)
        => GdiHelpers.MeasureTextHeight(hdc, hFont, text, width, format);

    /// <summary>
    /// Hauteur du titre d'une carte : une ligne (<c>S(24)</c>), plus les lignes en trop quand
    /// le titre passe à la ligne. Les titres du site (1.3.0) dépassent la carte 4 en français.
    /// </summary>
    private int CardTitleHeight(IntPtr hdc, string title, int width)
    {
        int extra = MeasureTextHeight(hdc, _hFontBold, title, width)
            - MeasureTextHeight(hdc, _hFontBold, "A", width);
        return S(24) + Math.Max(0, extra);
    }

    private int MeasureSingleLineWidth(IntPtr hdc, IntPtr hFont, string text)
        => GdiHelpers.MeasureSingleLineWidth(hdc, hFont, text);

    /// <summary>
    /// Mesure le texte du bouton « Suivant / Essayer maintenant / C'est parti ! » et
    /// retourne sa position X (alignée à droite) et sa largeur (≥ BASE_BTN_W_NEXT_MIN).
    /// </summary>
    private (int x, int width) ComputeNextButtonGeometry(string text, int winW, int margin)
    {
        IntPtr hdc = Win32.GetDC(_hWnd);
        try
        {
            int textWidth = MeasureSingleLineWidth(hdc, _hFontButton, text);
            int width = Math.Max(S(BASE_BTN_W_NEXT_MIN), textWidth + S(BASE_BTN_TEXT_PAD * 2));
            int x = winW - margin - width;
            return (x, width);
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }
    }

    private int MeasureSingleLineHeight(IntPtr hdc, IntPtr hFont)
        => GdiHelpers.MeasureSingleLineHeight(hdc, hFont);

    // Étape 1 (refonte du 15/10, maquette v4 « Cadres » choisie par Antoine) : le clavier entier,
    // gravure de base centrée ; les touches qui changent ont leur signe encadré de bleu, et cinq
    // notes au trait, hors du clavier, disent ce qui change.

    /// <summary>Les 13 touches concernées : ², é è ç à, d à k, la touche du point et ù.</summary>
    private static readonly uint[] Step1ChangedKeys =
        { 0x29, 0x03, 0x08, 0x0A, 0x0B, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x33, 0x28 };

    /// <summary>Touches de fonction citées par les notes : les autres restent muettes.</summary>
    private static readonly string[] Step1NamedKeys = { "Verr. Maj.", "Maj ⇧", "AltGr" };

    // Disposition du clavier de l'étape 1 : celle de l'application ; sans elle (banc de
    // captures), celle embarquée, chargée une fois.
    private Layout? _paintLayout;
    private Layout? PaintLayout
    {
        get
        {
            if (AppLayout != null) return AppLayout;
            if (_paintLayout == null)
            {
                try { _paintLayout = LayoutLoader.LoadFromResource(); }
                catch { }
            }
            return _paintLayout;
        }
    }

    // Bord gauche du premier bouton du bas à l'étape 1 (états A, B, C) : la mention s'arrête avant.
    private int _step1ButtonsLeft;

    private void PaintStep1(IntPtr hdc, IntPtr gfx, int cw, int y)
    {
        int margin = Px(28);
        var titleRect = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = y + Px(30) };
        PaperText.DrawLine(hdc, L.Onboarding_Step1Title, FontSpec.Of(AppFonts.Title, 26, TypeRamp.Semibold, _dpiScale),
            titleRect, CLR_TITLE);
        var subtitleRect = new Win32.RECT { left = margin, top = titleRect.bottom + Px(4), right = cw - margin, bottom = titleRect.bottom + Px(24) };
        PaperText.DrawLine(hdc, L.Onboarding_Step1Subtitle, FontSpec.Of(AppFonts.Text, 14, TypeRamp.Regular, _dpiScale),
            subtitleRect, CLR_REASSURE);

        var layout = PaintLayout;
        if (layout != null)
        {
            float unit = 38f * _dpiScale;
            var (kbW, kbH) = PaperKeyboard.Size(unit);
            var placement = PaperKeyboard.PlaceAt((cw - kbW) / 2, subtitleRect.bottom + Px(70), unit);
            var state = new PaperKeyboardState { CenteredBase = true };
            state.FramedScancodes.UnionWith(Step1ChangedKeys);
            state.NamedFunctionLabels.UnionWith(Step1NamedKeys);
            PaperKeyboard.Draw(hdc, placement, layout, PaperKeyboardMode.Map, state);
            DrawStep1Notes(hdc, gfx, placement, kbH, cw - margin);
        }

        DrawStep1Footer(hdc, margin);
    }

    /// <summary>Les cinq notes au trait : vers le haut pour la rangée des chiffres ; vers le bas,
    /// en escalier, par un interstice puis la barre d'espace ou une touche muette.</summary>
    private void DrawStep1Notes(IntPtr hdc, IntPtr gfx, KeyboardPlacement p, int kbH, int right)
    {
        var signFont = FontSpec.Of(AppFonts.Title, 15, TypeRamp.Semibold, _dpiScale);
        var textFont = FontSpec.Of(AppFonts.Text, 13, TypeRamp.Regular, _dpiScale);
        int line = Math.Max(1, Px(1));
        int kbTop = p.OriginY, kbBottom = p.OriginY + kbH;
        int space = PaperText.Width(hdc, " ", textFont);

        int NoteWidth(string sign, string text) => PaperText.Width(hdc, sign, signFont) + space + PaperText.Width(hdc, text, textFont);
        void Note(string sign, string text, int x, int baseline)
        {
            x = PaperText.Draw(hdc, sign, signFont, x, baseline, PaperTheme.Action);
            PaperText.Draw(hdc, text, textFont, x + space, baseline, CLR_REASSURE);
        }
        void VLine(int x, int y0, int y1) => GdiHelpers.FillSolidRect(hdc,
            new Win32.RECT { left = x, top = Math.Min(y0, y1), right = x + line, bottom = Math.Max(y0, y1) + line }, PaperTheme.Action);
        void HLine(int x0, int x1, int y) => GdiHelpers.FillSolidRect(hdc,
            new Win32.RECT { left = Math.Min(x0, x1), top = y, right = Math.Max(x0, x1) + line, bottom = y + line }, PaperTheme.Action);
        void Dot(int x, int y) => FillDot(gfx, x + line / 2f, y, 2.25f * _dpiScale, PaperTheme.Action);

        // @ # : drapeau vers le haut depuis la touche ².
        var grave = KeyRect(p, 0x29);
        int gx = CenterX(grave), flagY = kbTop - Px(38);
        VLine(gx, grave.top, flagY);
        HLine(gx, gx + Px(8), flagY);
        Dot(gx, grave.top);
        int atX = gx + Px(16);
        Note("@ #", L.Onboarding_NoteAt, atX, kbTop - Px(34));
        int atEnd = atX + NoteWidth("@ #", L.Onboarding_NoteAt);

        // É È Ç À : peigne au-dessus de é è ç à. Centrée sur le peigne, la note se pousse à droite
        // si elle touche celle de @ # (les métriques GDI ne sont pas celles du navigateur).
        uint[] comb = { 0x03, 0x08, 0x0A, 0x0B };
        int combY = kbTop - Px(12);
        int first = CenterX(KeyRect(p, comb[0])), last = CenterX(KeyRect(p, comb[^1]));
        HLine(first, last, combY);
        foreach (uint scancode in comb)
        {
            var key = KeyRect(p, scancode);
            VLine(CenterX(key), key.top, combY);
            Dot(CenterX(key), key.top);
        }
        int capsW = NoteWidth("É È Ç À", L.Onboarding_NoteCaps);
        int capsX = Math.Min(Math.Max((first + last - capsW) / 2, atEnd + Px(12)), right - capsW);
        Note("É È Ç À", L.Onboarding_NoteCaps, capsX, combY - Px(13));

        void Down(float units, uint scancode, int depth, string sign, string text)
        {
            int x = p.OriginX + (int)Math.Round(units * p.Scale);
            int y0 = KeyRect(p, scancode).bottom + 1;
            int yb = kbBottom + Px(depth);
            VLine(x, y0, yb);
            HLine(x - Px(12), x, yb);
            Dot(x, y0);
            Note(sign, text, x - Px(20) - NoteWidth(sign, text), yb + Px(4));
        }
        // d à k par l'interstice b/n ; le point par l'interstice Espace/AltGr ; ù par l'interstice
        // !/Maj, au-dessus de la touche Menu muette.
        Down(7.9f, 0x23, 18, "{ } \\ | [ ]", L.Onboarding_NoteCoding);
        Down(10.35f, 0x33, 42, ".", L.Onboarding_NotePeriod);
        Down(13.4f, 0x28, 66, "´ ` ~", L.Onboarding_NoteAccents);
    }

    private static Win32.RECT KeyRect(KeyboardPlacement p, uint scancode)
    {
        foreach (var key in KeyboardRenderer.VisualKeys)
            if (key.Scancode == scancode && !key.IsContextual)
                return PaperKeyboard.ToRect(key, p);
        return default;
    }

    private static int CenterX(Win32.RECT r) => (r.left + r.right + 1) / 2;

    [DllImport("gdiplus.dll")]
    private static extern int GdipCreateSolidFill(uint argb, out IntPtr brush);

    [DllImport("gdiplus.dll")]
    private static extern int GdipFillEllipse(IntPtr graphics, IntPtr brush, float x, float y, float width, float height);

    [DllImport("gdiplus.dll")]
    private static extern int GdipDeleteBrush(IntPtr brush);

    /// <summary>Point lissé (GDI+) de rayon <paramref name="radius"/>, centré sur (x, y).</summary>
    private static void FillDot(IntPtr gfx, float x, float y, float radius, uint colorRef)
    {
        if (gfx == IntPtr.Zero) return;
        uint argb = 0xFF000000u | ((colorRef & 0xFF) << 16) | (colorRef & 0xFF00) | ((colorRef >> 16) & 0xFF);
        if (GdipCreateSolidFill(argb, out IntPtr brush) != 0) return;
        GdipFillEllipse(gfx, brush, x - radius, y - radius, radius * 2, radius * 2);
        GdipDeleteBrush(brush);
    }

    /// <summary>Pied de l'étape 1 : la mention de confidentialité, puis la ligne de retour, à
    /// gauche des boutons et centrées sur eux.</summary>
    private void DrawStep1Footer(IntPtr hdc, int margin)
    {
        var font = FontSpec.Of(AppFonts.Text, 12, TypeRamp.Regular, _dpiScale);
        int lineH = Px(16);
        int buttonsTop = S(BASE_WIN_H) - S(BASE_BOTTOM_MARGIN);
        var rect = new Win32.RECT { left = margin, right = Math.Max(margin + Px(160), _step1ButtonsLeft - Px(16)) };
        var mention = new[] { new TextRun(L.Onboarding_PrivacyReassurance) };
        var undo = new[] { new TextRun(L.Onboarding_TurnOffAnyTime) };
        int mentionH = PaperText.DrawParagraph(hdc, mention, font, font, rect, CLR_REASSURE, CLR_REASSURE, lineH, draw: false);
        int undoH = PaperText.DrawParagraph(hdc, undo, font, font, rect, CLR_REASSURE, CLR_REASSURE, lineH, draw: false);
        rect.top = buttonsTop + (S(BASE_BTN_H) - mentionH - undoH) / 2;
        rect.bottom = rect.top + mentionH;
        PaperText.DrawParagraph(hdc, mention, font, font, rect, CLR_REASSURE, CLR_REASSURE, lineH);
        rect.top = rect.bottom;
        rect.bottom = rect.top + undoH;
        PaperText.DrawParagraph(hdc, undo, font, font, rect, CLR_REASSURE, CLR_REASSURE, lineH);
    }
    // ═══════════════════════════════════════════════════════════════
    // Étape 2
    // ═══════════════════════════════════════════════════════════════
    private void PaintStep2(IntPtr hdc, IntPtr gfx, int cw, int ch, int y)
    {
        int margin = ColumnMargin;

        // Même titre de page que les étapes « Vos langues » et « Ressources » (refonte du 15/10).
        Win32.SelectObject(hdc, _hFontPageTitle);
        Win32.SetTextColor(hdc, CLR_TITLE);
        int pageTitleHeight = MeasureSingleLineHeight(hdc, _hFontPageTitle);
        var stepTitleRect = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = y + pageTitleHeight };
        Win32.DrawTextW(hdc, L.Onboarding_Step2Title, -1, ref stepTitleRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        y += pageTitleHeight + S(12);

        DrawStepCard(hdc, margin, cw, ref y, "1",
            L.Onboarding_Card1Title,
            L.Onboarding_Card1Desc);

        DrawToggleStepCard(hdc, margin, cw, ref y);

        DrawStepCardWithRuns(hdc, margin, cw, ref y, "3",
            L.Onboarding_Card3Title,
            GetShortcutRuns(null, $"Ctrl + {L.Settings_ShortcutModifier2} + Q", L.Onboarding_Card3Suffix));

        DrawStepCardWithRuns(hdc, margin, cw, ref y, "4",
            L.Onboarding_Card4Title,
            GetShortcutRuns(null, $"Ctrl + {L.Settings_ShortcutModifier2} + W", L.Onboarding_Card4Suffix));
    }

    // ═══════════════════════════════════════════════════════════════
    // Étape « Vos langues » (direction C, 08/10)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Géométrie de l'étape, commune au dessin et au placement du champ d'essai.</summary>
    private struct LanguagesLayout
    {
        public Win32.RECT Title, Subtitle, Panel, TrialText, TrialBox, TrialEdit, Result, Tip;
        public int RowHeight, RowPadX, RowPadY, NameHeight;
    }

    private (string Text, uint Color, IntPtr Font)[] TrialRuns() => new[]
    {
        (L.Onboarding_TrialPrefix, CLR_TEXT, _hFontText),
        (L.Onboarding_TrialDeadKey, CLR_TEXT, _hFontBold),
        (L.Onboarding_TrialThen, CLR_TEXT, _hFontText),
        (L.Onboarding_TrialLetter, CLR_TEXT, _hFontBold),
        (L.Onboarding_TrialSuffix, CLR_TEXT, _hFontText),
    };

    /// <summary>« Une autre lettre : Ctrl + Maj + W, puis tapez « peul » ou « bambara ». » :
    /// texte secondaire, touches à l'encre en semi-gras.</summary>
    private (string Text, uint Color, IntPtr Font)[] TipRuns()
    {
        var runs = new List<(string Text, uint Color, IntPtr Font)> { (L.Onboarding_LanguagesTipPrefix, CLR_REASSURE, _hFontText) };
        string[] keys = { "Ctrl", L.Settings_ShortcutModifier2, "W" };
        for (int i = 0; i < keys.Length; i++)
        {
            if (i > 0) runs.Add((" + ", CLR_TEXT, _hFontBold));
            runs.Add((keys[i], CLR_TEXT, _hFontBold));
        }
        runs.Add((L.Onboarding_LanguagesTipSuffix, CLR_REASSURE, _hFontText));
        return runs.ToArray();
    }

    private LanguagesLayout GetLanguagesLayout(IntPtr hdc, int cw, int topY)
    {
        int margin = ColumnMargin;
        int contentW = cw - margin * 2;
        var layout = new LanguagesLayout { RowPadX = S(16), RowPadY = S(11) };

        int y = topY + S(6);
        int titleH = MeasureTextHeight(hdc, _hFontPageTitle, L.Onboarding_LanguagesTitle, contentW);
        layout.Title = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = y + titleH };
        y += titleH + S(2);

        int subtitleH = MeasureTextHeight(hdc, _hFontText, L.Onboarding_LanguagesSubtitle, contentW);
        layout.Subtitle = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = y + subtitleH };
        y += subtitleH + S(12);

        layout.NameHeight = MeasureSingleLineHeight(hdc, _hFontBold);
        int lettersH = MeasureSingleLineHeight(hdc, _hFontLetters);
        layout.RowHeight = layout.RowPadY * 2 + layout.NameHeight + lettersH;
        int panelH = layout.RowHeight * OnboardingLanguages.All.Length + 2;
        layout.Panel = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = y + panelH };
        y += panelH + S(16);

        // L'essai : consigne à gauche, champ à droite (64 × 48 px à 100 % dans la maquette).
        int boxW = S(88);
        int boxH = S(64);
        int textW = contentW - boxW - S(16);
        int trialTextH = GdiHelpers.MeasureColoredRunsHeight(hdc, textW, S(22), TrialRuns());
        int trialH = Math.Max(boxH, trialTextH);
        layout.TrialBox = new Win32.RECT { left = cw - margin - boxW, top = y + (trialH - boxH) / 2, right = cw - margin, bottom = y + (trialH - boxH) / 2 + boxH };
        int textTop = y + (trialH - trialTextH) / 2;
        layout.TrialText = new Win32.RECT { left = margin, top = textTop, right = margin + textW, bottom = textTop + trialTextH };
        int editH = MeasureSingleLineHeight(hdc, _hFontTrial) + S(2);
        int editInset = S(6);
        int editTop = layout.TrialBox.top + (boxH - editH) / 2;
        layout.TrialEdit = new Win32.RECT
        {
            left = layout.TrialBox.left + editInset,
            top = editTop,
            right = layout.TrialBox.right - editInset,
            bottom = editTop + editH
        };
        y += trialH + S(6);

        // Le message occupe toujours sa ligne : l'astuce ne saute pas quand il apparaît.
        int resultH = MeasureSingleLineHeight(hdc, _hFontBold);
        layout.Result = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = y + resultH };
        y += resultH + S(10);

        int tipH = GdiHelpers.MeasureColoredRunsHeight(hdc, contentW, S(22), TipRuns());
        layout.Tip = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = y + tipH };
        return layout;
    }

    private void PaintLanguages(IntPtr hdc, int cw, int y)
    {
        var layout = GetLanguagesLayout(hdc, cw, y);
        PlaceTrialField(layout);

        Win32.SelectObject(hdc, _hFontPageTitle);
        Win32.SetTextColor(hdc, CLR_TITLE);
        var titleRect = layout.Title;
        Win32.DrawTextW(hdc, L.Onboarding_LanguagesTitle, -1, ref titleRect, Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX);

        Win32.SelectObject(hdc, _hFontText);
        Win32.SetTextColor(hdc, CLR_REASSURE);
        var subtitleRect = layout.Subtitle;
        Win32.DrawTextW(hdc, L.Onboarding_LanguagesSubtitle, -1, ref subtitleRect, Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX);

        // Les trois langues : nom et lettres à gauche, un geste à droite.
        GdiHelpers.DrawPanel(hdc, layout.Panel, CLR_PANEL_BG, CLR_PANEL_BORDER, CLR_BADGE_BG, S(4));
        int rowLeft = layout.Panel.left + layout.RowPadX;
        int rowRight = layout.Panel.right - S(12);
        for (int i = 0; i < OnboardingLanguages.All.Length; i++)
        {
            var language = OnboardingLanguages.All[i];
            int rowTop = layout.Panel.top + 1 + i * layout.RowHeight;
            if (i > 0)
            {
                var sep = new Win32.RECT { left = layout.Panel.left + S(4), top = rowTop - 1, right = layout.Panel.right - 1, bottom = rowTop };
                GdiHelpers.FillSolidRect(hdc, sep, PaperTheme.Border);
            }

            int gestureLeft = DrawLanguageGesture(hdc, language.Example, rowRight, rowTop + layout.RowHeight / 2);

            Win32.SelectObject(hdc, _hFontBold);
            Win32.SetTextColor(hdc, CLR_TEXT);
            int textTop = rowTop + layout.RowPadY;
            var nameRect = new Win32.RECT { left = rowLeft, top = textTop, right = gestureLeft - S(8), bottom = textTop + layout.NameHeight };
            Win32.DrawTextW(hdc, language.Name(), -1, ref nameRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

            Win32.SelectObject(hdc, _hFontLetters);
            var lettersRect = new Win32.RECT { left = rowLeft, top = nameRect.bottom, right = gestureLeft - S(8), bottom = rowTop + layout.RowHeight - layout.RowPadY };
            Win32.DrawTextW(hdc, language.Letters, -1, ref lettersRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        }

        // L'essai : consigne, cadre du champ (le contrôle EDIT est posé dedans), message.
        GdiHelpers.DrawColoredRuns(hdc, layout.TrialText.left, layout.TrialText.top,
            layout.TrialText.right - layout.TrialText.left, S(22), TrialRuns());
        DrawTrialBox(hdc, layout.TrialBox);

        string trialText = ReadTrialText();
        string message = LetterTrial.Message(trialText);
        if (message.Length > 0)
        {
            bool match = LetterTrial.Evaluate(trialText) == LetterTrialResult.Match;
            Win32.SelectObject(hdc, match ? _hFontBold : _hFontText);
            Win32.SetTextColor(hdc, match ? CLR_LINK : CLR_TEXT);
            var resultRect = layout.Result;
            Win32.DrawTextW(hdc, message, -1, ref resultRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX | Win32.DT_END_ELLIPSIS);
        }

        GdiHelpers.DrawColoredRuns(hdc, layout.Tip.left, layout.Tip.top, layout.Tip.right - layout.Tip.left, S(22), TipRuns());
    }

    /// <summary>Cadre du champ d'essai : gris au repos ; au focus, bord et anneau d'accent.</summary>
    private void DrawTrialBox(IntPtr hdc, Win32.RECT box)
    {
        if (_trialFocused)
        {
            int ring = Math.Max(2, S(3));
            int gap = Math.Max(1, S(1));
            var outer = new Win32.RECT { left = box.left - ring - gap, top = box.top - ring - gap, right = box.right + ring + gap, bottom = box.bottom + ring + gap };
            GdiHelpers.FillSolidRect(hdc, outer, CLR_LINK);
            var spacer = new Win32.RECT { left = box.left - gap, top = box.top - gap, right = box.right + gap, bottom = box.bottom + gap };
            GdiHelpers.FillSolidRect(hdc, spacer, CLR_BG);
        }
        GdiHelpers.FillSolidRect(hdc, box, _trialFocused ? CLR_LINK : PaperTheme.Pale);
        var inner = new Win32.RECT { left = box.left + 1, top = box.top + 1, right = box.right - 1, bottom = box.bottom - 1 };
        GdiHelpers.FillSolidRect(hdc, inner, PaperTheme.Surface);
    }

    /// <summary>
    /// Geste de <paramref name="example"/>, aligné à droite sur <paramref name="right"/> et
    /// centré sur <paramref name="centerY"/> : la lettre, « = », puis chaque temps (modificateur
    /// tenu en étiquette pleine, touche frappée en case), séparés par « puis ». Lu dans
    /// character-index.json (<see cref="Gesture.Of(string)"/>). Rend le bord gauche dessiné.
    /// </summary>
    private int DrawLanguageGesture(IntPtr hdc, string example, int right, int centerY)
    {
        var parts = new List<(int Kind, string Text, IntPtr Font, int Width)>(); // 0 lettre, 1 texte, 2 tenu, 3 case
        int keyH = S(32);
        int tagH = S(27);
        int pad = S(14);
        parts.Add((0, example, _hFontLetters, MeasureSingleLineWidth(hdc, _hFontLetters, example)));
        parts.Add((1, "=", _hFontVersion, MeasureSingleLineWidth(hdc, _hFontVersion, "=")));
        var steps = Gesture.Of(example);
        for (int i = 0; i < steps.Count; i++)
        {
            if (i > 0)
                parts.Add((1, L.Gesture_ThenShort, _hFontVersion, MeasureSingleLineWidth(hdc, _hFontVersion, L.Gesture_ThenShort)));
            if (steps[i].Shift)
                parts.Add((2, L.Settings_ShortcutModifier2, _hFontTag, MeasureSingleLineWidth(hdc, _hFontTag, L.Settings_ShortcutModifier2) + pad));
            if (steps[i].AltGr)
                parts.Add((2, "AltGr", _hFontTag, MeasureSingleLineWidth(hdc, _hFontTag, "AltGr") + pad));
            string label = steps[i].ShortLabel;
            parts.Add((3, label, _hFontBold, Math.Max(keyH, MeasureSingleLineWidth(hdc, _hFontBold, label) + pad)));
        }

        int gap = S(6);
        int total = 0;
        for (int i = 0; i < parts.Count; i++)
            total += parts[i].Width + (i > 0 ? (i == 1 ? S(10) : gap) : 0);
        int x = right - total;
        int left = x;

        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0) x += i == 1 ? S(10) : gap;
            var part = parts[i];
            switch (part.Kind)
            {
                case 2:
                {
                    var tag = new Win32.RECT { left = x, top = centerY - tagH / 2, right = x + part.Width, bottom = centerY - tagH / 2 + tagH };
                    GdiHelpers.FillSolidRect(hdc, tag, CLR_TEXT);
                    Win32.SelectObject(hdc, part.Font);
                    Win32.SetTextColor(hdc, PaperTheme.Surface);
                    Win32.DrawTextW(hdc, part.Text, -1, ref tag, Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
                    break;
                }
                case 3:
                {
                    var key = new Win32.RECT { left = x, top = centerY - keyH / 2, right = x + part.Width, bottom = centerY - keyH / 2 + keyH };
                    GdiHelpers.FillSolidRect(hdc, key, CLR_TEXT);
                    var face = new Win32.RECT { left = key.left + 1, top = key.top + 1, right = key.right - 1, bottom = key.bottom - 1 };
                    GdiHelpers.FillSolidRect(hdc, face, PaperTheme.Surface);
                    Win32.SelectObject(hdc, part.Font);
                    Win32.SetTextColor(hdc, CLR_TEXT);
                    Win32.DrawTextW(hdc, part.Text, -1, ref key, Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
                    break;
                }
                default:
                {
                    int h = MeasureSingleLineHeight(hdc, part.Font);
                    var rect = new Win32.RECT { left = x, top = centerY - h / 2, right = x + part.Width, bottom = centerY - h / 2 + h };
                    Win32.SelectObject(hdc, part.Font);
                    Win32.SetTextColor(hdc, part.Kind == 0 ? CLR_LINK : CLR_REASSURE);
                    Win32.DrawTextW(hdc, part.Text, -1, ref rect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
                    break;
                }
            }
            x += part.Width;
        }
        return left;
    }

    // ═══════════════════════════════════════════════════════════════
    // Étape 3
    // ═══════════════════════════════════════════════════════════════
    private void DrawBadge(IntPtr hdc, int x, int y, string number)
    {
        int badgeW = S(34);
        int badgeH = S(24);
        var badgeRect = new Win32.RECT { left = x, top = y, right = x + badgeW, bottom = y + badgeH };
        GdiHelpers.FillSolidRect(hdc, badgeRect, CLR_BADGE_BG);

        Win32.SelectObject(hdc, _hFontSmall);
        Win32.SetTextColor(hdc, CLR_BADGE_TEXT);
        Win32.DrawTextW(hdc, number, -1, ref badgeRect,
            Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
    }

    private (string Text, uint Color, IntPtr Font)[] GetShortcutRuns(string? prefix, string shortcut, string? suffix = null)
    {
        var runs = new System.Collections.Generic.List<(string Text, uint Color, IntPtr Font)>();

        if (!string.IsNullOrEmpty(prefix))
            runs.Add((prefix, CLR_TEXT, _hFontText));

        string[] keys = shortcut.Split(" + ");
        for (int i = 0; i < keys.Length; i++)
        {
            if (i > 0)
                runs.Add((" + ", CLR_TEXT, _hFontText));

            runs.Add((keys[i], CLR_INLINE_HIGHLIGHT, _hFontBold));
        }

        if (!string.IsNullOrEmpty(suffix))
            runs.Add((suffix, CLR_TEXT, _hFontText));

        return runs.ToArray();
    }

    private void DrawStepCardWithRuns(IntPtr hdc, int margin, int cw, ref int y, string number, string title,
        params (string Text, uint Color, IntPtr Font)[] descriptionRuns)
    {
        DrawStepCardWithRuns(hdc, margin, cw, ref y, number, title, STEP_CARD_MIN_H, descriptionRuns);
    }

    private void DrawStepCardWithRuns(IntPtr hdc, int margin, int cw, ref int y, string number, string title,
        int minCardHeight, params (string Text, uint Color, IntPtr Font)[] descriptionRuns)
    {
        int cardTop = y;
        int cardPaddingX = S(16);
        int cardPaddingY = S(12);
        int badgeW = S(34);
        int badgeGap = S(16);
        int contentWidth = cw - margin * 2;
        int textX = margin + cardPaddingX + badgeW + badgeGap;
        int textWidth = contentWidth - cardPaddingX * 2 - badgeW - badgeGap;
        int titleHeight = CardTitleHeight(hdc, title, textWidth);
        int descHeight = GdiHelpers.MeasureColoredRunsHeight(hdc, textWidth, S(22), descriptionRuns);
        int cardHeight = Math.Max(S(minCardHeight), cardPaddingY * 2 + titleHeight + descHeight + S(4));

        var cardRect = new Win32.RECT { left = margin, top = cardTop, right = cw - margin, bottom = cardTop + cardHeight };
        GdiHelpers.DrawPanel(hdc, cardRect, CLR_PANEL_BG, CLR_PANEL_BORDER, CLR_BADGE_BG, S(4));
        DrawBadge(hdc, margin + cardPaddingX, cardTop + cardPaddingY, number);

        Win32.SelectObject(hdc, _hFontBold);
        Win32.SetTextColor(hdc, CLR_STEP_TITLE);
        var titleRect = new Win32.RECT
        {
            left = textX,
            top = cardTop + cardPaddingY - S(1),
            right = textX + textWidth,
            bottom = cardTop + cardPaddingY + titleHeight
        };
        Win32.DrawTextW(hdc, title, -1, ref titleRect, Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX);

        GdiHelpers.DrawColoredRuns(hdc, textX, cardTop + cardPaddingY + titleHeight, textWidth, S(22), descriptionRuns);

        y += cardHeight + S(10);
    }

    private void DrawStepCard(IntPtr hdc, int margin, int cw, ref int y, string number, string title, string description, int minCardHeight = STEP_CARD_MIN_H)
    {
        int cardTop = y;
        int cardPaddingX = S(16);
        int cardPaddingY = S(12);
        int badgeW = S(34);
        int badgeGap = S(16);
        int contentWidth = cw - margin * 2;
        int textX = margin + cardPaddingX + badgeW + badgeGap;
        int textWidth = contentWidth - cardPaddingX * 2 - badgeW - badgeGap;
        int titleHeight = CardTitleHeight(hdc, title, textWidth);
        int descHeight = MeasureTextHeight(hdc, _hFontText, description, textWidth);
        int cardHeight = Math.Max(S(minCardHeight), cardPaddingY * 2 + titleHeight + descHeight + S(4));

        var cardRect = new Win32.RECT { left = margin, top = cardTop, right = cw - margin, bottom = cardTop + cardHeight };
        GdiHelpers.DrawPanel(hdc, cardRect, CLR_PANEL_BG, CLR_PANEL_BORDER, CLR_BADGE_BG, S(4));
        DrawBadge(hdc, margin + cardPaddingX, cardTop + cardPaddingY, number);

        Win32.SelectObject(hdc, _hFontBold);
        Win32.SetTextColor(hdc, CLR_STEP_TITLE);
        var titleRect = new Win32.RECT
        {
            left = textX,
            top = cardTop + cardPaddingY - S(1),
            right = textX + textWidth,
            bottom = cardTop + cardPaddingY + titleHeight
        };
        Win32.DrawTextW(hdc, title, -1, ref titleRect, Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX);

        Win32.SelectObject(hdc, _hFontText);
        Win32.SetTextColor(hdc, CLR_TEXT);
        var descRect = new Win32.RECT
        {
            left = textX,
            top = cardTop + cardPaddingY + titleHeight,
            right = textX + textWidth,
            bottom = cardTop + cardHeight - cardPaddingY
        };
        Win32.DrawTextW(hdc, description, -1, ref descRect, Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX);

        y += cardHeight + S(10);
    }

    private void DrawToggleStepCard(IntPtr hdc, int margin, int cw, ref int y)
    {
        int cardTop = y;
        int cardPaddingX = S(16);
        int cardPaddingY = S(12);
        int badgeW = S(34);
        int badgeGap = S(16);
        int contentWidth = cw - margin * 2;
        int textX = margin + cardPaddingX + badgeW + badgeGap;
        int textWidth = contentWidth - cardPaddingX * 2 - badgeW - badgeGap;
        int titleHeight = S(24);
        var shortcutRuns = GetShortcutRuns(L.Onboarding_Card2ShortcutPrefix, $"Ctrl + {L.Settings_ShortcutModifier2} + {L.Onboarding_CapsLockWord}");
        int shortcutHeight = GdiHelpers.MeasureColoredRunsHeight(hdc, textWidth, S(22), shortcutRuns);
        int cardHeight = Math.Max(S(78), cardPaddingY * 2 + titleHeight + shortcutHeight + S(10));

        var cardRect = new Win32.RECT { left = margin, top = cardTop, right = cw - margin, bottom = cardTop + cardHeight };
        GdiHelpers.DrawPanel(hdc, cardRect, CLR_PANEL_BG, CLR_PANEL_BORDER, CLR_BADGE_BG, S(4));
        DrawBadge(hdc, margin + cardPaddingX, cardTop + cardPaddingY, "2");

        Win32.SelectObject(hdc, _hFontBold);
        Win32.SetTextColor(hdc, CLR_STEP_TITLE);
        var titleRect = new Win32.RECT
        {
            left = textX,
            top = cardTop + cardPaddingY - S(1),
            right = textX + textWidth,
            bottom = cardTop + cardPaddingY + titleHeight
        };
        Win32.DrawTextW(hdc, L.Onboarding_Card2Title, -1, ref titleRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        int lineY = cardTop + cardPaddingY + titleHeight + S(4);
        GdiHelpers.DrawColoredRuns(hdc, textX, lineY, textWidth, S(22), shortcutRuns);

        y += cardHeight + S(10);
    }

    private void PaintStep3(IntPtr hdc, IntPtr gfx, int cw, int ch, int y)
    {
        int margin = ColumnMargin;
        GetStep3Layout(y, cw, out var resourcesPanel, out var prefsPanel,
            out int linksX, out int linksWidth, out int linkStartY, out int linkRowH, out _,
            out int checkboxX, out int checkboxWidth, out int checkboxY, out int checkboxSpacing, out _,
            out _, out int trainingDescY, out int trainingDescHeight);

        Win32.SelectObject(hdc, _hFontPageTitle);
        Win32.SetTextColor(hdc, CLR_TITLE);
        var stepTitleRect = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = resourcesPanel.top - S(12) };
        Win32.DrawTextW(hdc, L.Onboarding_SectionResources, -1, ref stepTitleRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        var prefsTitleRect = new Win32.RECT
        {
            left = margin,
            top = resourcesPanel.bottom + S(14),
            right = cw - margin,
            bottom = prefsPanel.top - S(8)
        };
        Win32.DrawTextW(hdc, L.Settings_SectionPreferences, -1, ref prefsTitleRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        GdiHelpers.DrawPanel(hdc, resourcesPanel, CLR_PANEL_BG, CLR_PANEL_BORDER, CLR_BADGE_BG, S(4));
        GdiHelpers.DrawPanel(hdc, prefsPanel, CLR_PANEL_BG, CLR_PANEL_BORDER, CLR_BADGE_BG, S(4));

        // Séparateurs entre les liens
        for (int row = 1; row < 4; row++)
        {
            var rowSep = new Win32.RECT
            {
                left = linksX,
                top = linkStartY + row * linkRowH - S(6),
                right = linksX + linksWidth,
                bottom = linkStartY + row * linkRowH - S(5)
            };
            GdiHelpers.FillSolidRect(hdc, rowSep, PaperTheme.Border);
        }

        // Texte descriptif sous la case « Défi du jour » (3e case du panneau Préférences) —
        // simple texte dessiné (comme la mention vie privée de l'étape 1), pas de contrôle
        // STATIC dédié : pas besoin d'interaction, juste une précision sous le libellé de la case.
        if (!DailyChallenge.Enabled) return; // 1.3.0 : Défi masqué, ni case ni texte
        Win32.SelectObject(hdc, _hFontReassure);
        Win32.SetTextColor(hdc, CLR_REASSURE);
        var trainingDescRect = new Win32.RECT
        {
            left = checkboxX,
            top = trainingDescY,
            right = checkboxX + checkboxWidth,
            bottom = trainingDescY + trainingDescHeight
        };
        Win32.DrawTextW(hdc, L.Onboarding_ChkTrainingDesc, -1, ref trainingDescRect,
            Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX);
    }

    // ═══════════════════════════════════════════════════════════════
    // Dispose
    // ═══════════════════════════════════════════════════════════════
    public void Dispose()
    {
        if (_onAppLanguageChanged != null)
            ConfigManager.AppLanguageChanged -= _onAppLanguageChanged;

        _learningModule?.Dispose();
        _learningModule = null;

        _links.Detach();
        if (_hWndBtnNext != IntPtr.Zero) Win32.RemoveWindowSubclass(_hWndBtnNext, _buttonArrowSubclassProc, (UIntPtr)20);
        if (_hWndBtnPrev != IntPtr.Zero) Win32.RemoveWindowSubclass(_hWndBtnPrev, _buttonArrowSubclassProc, (UIntPtr)21);
        if (_hWndBtnTry != IntPtr.Zero) Win32.RemoveWindowSubclass(_hWndBtnTry, _buttonArrowSubclassProc, (UIntPtr)22);

        DialogNavigation.Unregister(_hWnd);
        if (_hWnd != IntPtr.Zero) { Win32.DestroyWindow(_hWnd); _hWnd = IntPtr.Zero; }
        if (_hIcon != IntPtr.Zero) { Win32.DestroyIcon(_hIcon); _hIcon = IntPtr.Zero; }
        if (_gdipLogo != IntPtr.Zero) { Win32.GdipDisposeImage(_gdipLogo); _gdipLogo = IntPtr.Zero; }
        if (_gdipFlagEn != IntPtr.Zero) { Win32.GdipDisposeImage(_gdipFlagEn); _gdipFlagEn = IntPtr.Zero; }
        if (_gdipFlagFr != IntPtr.Zero) { Win32.GdipDisposeImage(_gdipFlagFr); _gdipFlagFr = IntPtr.Zero; }
        if (_gdipToken != IntPtr.Zero) { Win32.GdiplusShutdown(_gdipToken); _gdipToken = IntPtr.Zero; }
        DestroyFonts();
        Win32.DeleteObject(_hPanelBrush);
        Win32.DeleteObject(_hFieldBrush);
        NativeWindow.UnregisterClass(ProductIdentity.WindowClass("Onboarding"));
    }
}
