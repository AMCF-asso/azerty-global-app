// Mini-module d'apprentissage — exercices guidés avec clavier virtuel intégré
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Fenêtre d'exercices de frappe lancée depuis l'onboarding (étape 1 → "Essayer maintenant").
/// Affiche un texte cible, compare caractère par caractère, et guide l'utilisateur
/// via un clavier virtuel avec highlight du prochain caractère à taper.
/// </summary>
sealed class LearningModule : IDisposable
{
    // ── Étapes ──────────────────────────────────────────────────────
    // Textes et réglages des six exercices : TutorialSteps, partagé avec le module
    // Initiation des Leçons. Ici, les titres et consignes propres au tutoriel.
    // KeepCapsHighlight : Verr. Maj. reste surlignée tant que l'exercice tourne.
    private readonly record struct LearningStep(string Title, string Instruction, TutorialSteps.Step Content)
    {
        public string Target => Content.Target;
        public bool Skippable => Content.Skippable;
        public bool KeepCapsHighlight => Content.KeepCapsLock;
    }

    // Propriété (pas un champ static readonly) : reconstruite à chaque accès pour refléter
    // la langue courante — LearningModule est recréé à chaque lancement (pas un singleton),
    // mais un tableau static readonly ne serait initialisé qu'une seule fois par processus.
    private static LearningStep[] Steps => new LearningStep[]
    {
        new(L.Learning_Step0Title, L.Learning_Step0Instruction, TutorialSteps.All[0]),
        new(L.Learning_Step1Title, L.Learning_Step1Instruction, TutorialSteps.All[1]),
        new(L.Learning_Step2Title, L.Learning_Step2Instruction, TutorialSteps.All[2]),
        new(L.Learning_Step3Title, L.Learning_Step3Instruction, TutorialSteps.All[3]),
        new(L.Learning_Step4Title, L.Learning_Step4Instruction, TutorialSteps.All[4]),
        new(L.Learning_Step5Title, L.Learning_Step5Instruction, TutorialSteps.All[5]),
    };

    // ── Window constants ────────────────────────────────────────────
    // Nom de classe Win32. Defini en const partagee entre CreateMainWindow et Dispose
    // (UnregisterClassW au Dispose pour eviter que la classe survive l'instance et garde
    // un delegate WndProc collecte par GC).
    private static readonly string WND_CLASS_NAME = ProductIdentity.WindowClass("Learning");

    // Dimensions de référence à 96 DPI. Bouton « Quitter » placé en haut à droite,
    // pas de légende footer, donc on peut réduire la hauteur.
    private const int BASE_WIN_W = 920;
    private const int BASE_WIN_H = 470;  // -25 (suppression BASE_STATUS_H) -15 (BASE_FOOTER_H reduit)

    // Layout vertical (base 96 DPI)
    private const int BASE_HEADER_H = 54;       // dots progression (~22) + titre (~32) sans overlap
    private const int BASE_INSTRUCTION_H = 32;
    private const int BASE_TARGET_H = 60;
    // BASE_STATUS_H supprime — l'ancienne ligne "Verr.Maj : ... — Touche morte : ..." est
    // remplacee par un bloc droit (PaintRightStatusBlock) dessine au-dessus du clavier.
    private const int BASE_FOOTER_H = 3;        // Marge basse minimale sous le clavier (avant 18, gain de 15px)
    private const int BASE_MARGIN = 20;

    // ── Control IDs ─────────────────────────────────────────────────
    private const int IDC_BTN_QUIT = 4001;
    private const int IDC_BTN_SKIP = 4002;
    private const int IDC_BTN_FINISH = 4003;
    // Boutons affiches a la fin de chaque exercice (page de choix Reessayer / Suivant)
    private const int IDC_BTN_RETRY = 4004;
    private const int IDC_BTN_CONTINUE = 4005;

    // ── Timer IDs ───────────────────────────────────────────────────
    private const uint TIMER_KEYPRESS = 8001;
    private const uint TIMER_REFOCUS = 8003;
    private const uint TIMER_FOCUS_LOST_CONFIRM = 8004;
    private const uint TIMER_CAPS_RESYNC = 8005;
    private const uint FOCUS_LOSS_DEBOUNCE_MS = 250;
    private const uint KEYPRESS_DURATION_MS = 120;
    private const int REFOCUS_MAX_ATTEMPTS = 6;     // ~6 × 80ms = 480ms total

    // ── Colors (COLORREF = 0x00BBGGRR) ──────────────────────────────
    // Fond unifié sombre (cohérent avec le testeur web). Toutes les zones (titre, instruction,
    // target, status, footer, clavier) partagent le même fond. Couleurs de texte adaptées
    // au contraste sur fond sombre (~#1A1A1A → texte clair #E0E0E0 = ratio 14:1).
    private const uint CLR_BG = 0x001A1A1A;                  // Fond fenêtre — sombre quasi-noir
    private const uint CLR_HEADER_TITLE = 0x00E0E0E0;        // Titre — blanc cassé
    private const uint CLR_INSTRUCTION = 0x00CCCCCC;         // Instruction — gris clair sur fond sombre
    private const uint CLR_TARGET_PENDING = 0x00808080;      // Caractères cible non encore tapés — gris moyen
    private const uint CLR_TARGET_CURRENT = 0x00FFFFFF;      // Caractère courant — blanc pur
    private const uint CLR_TARGET_CORRECT = 0x005EC522;      // Caractère validé — vert
    private const uint CLR_TARGET_ERROR = 0x004444EF;        // Erreur de frappe — rouge
    private const uint CLR_STATUS = 0x00AAAAAA;              // Barre de statut — gris clair
    private const uint CLR_PROGRESS_DONE = 0x00D47800;       // Dots progression terminés — orange brand
    private const uint CLR_PROGRESS_TODO = 0x00606060;       // Dots progression à faire — gris foncé (sur fond sombre)
    private const uint CLR_PROGRESS_CURRENT = 0x005EC522;    // Dot exercice en cours — vert (BGR, = #22C55E)
    private const uint CLR_TRANSITION = 0x005EC522;          // Animation transition entre exercices — vert
    private const uint CLR_BTN_QUIT_TEXT = 0x00AAAAAA;       // Bouton « Quitter les exercices » — gris clair

    // Suffixe « (Bonus) » a la suite du titre pour les exos optionnels — dore-orange.
    private const uint CLR_BONUS_TEXT = 0x000094E2;          // BGR ≈ #E29400 (orange ambré, lisible sur fond sombre)

    // Fond de la zone clavier et cadre des boutons d'en-tête. Les couleurs des touches et du
    // surlignage sont celles de KeyboardRenderer, profil Onboarding (lot 9, audit du 25/09 L-01).
    private const uint CLR_KB_BG = 0x001A1A1A;               // Fond zone clavier — identique à CLR_BG (unifié)
    private const uint CLR_KEY_BORDER = 0x00555555;          // Bordure des boutons d'en-tête

    // Polices des touches, en pixels à 96 DPI. Le réglage à chaud par learning-tweaks.json,
    // fichier de développement jamais livré, a disparu avec le rendu propre du tutoriel.
    private const int FONT_CHAR_MAIN = 28;
    private const int FONT_CHAR_DEAD_KEY = 24;  // 28 × 0,85, arrondi
    private const int FONT_CHAR_SMALL = 25;
    private const int FONT_CTX = 22;

    // Exercice 6 : les aides des mots étrangers, que le clavier simplifié cache ailleurs.
    private static readonly string[] LanguageExerciseCharacters = { "dk:stroke", "¿", "¡" };

    // ═══════════════════════════════════════════════════════════════
    // Champs d'instance
    // ═══════════════════════════════════════════════════════════════
    private IntPtr _hWnd;
    private readonly Win32.WNDPROC _wndProcDelegate;
    private readonly IntPtr _hWndOnboarding;

    // Références app
    private readonly KeyMapper _mapper;
    private readonly KeyboardHook _hook;
    private readonly Layout _layout;

    // Données de highlight et noms des infobulles : index partagé (audit du 25/09, V-04 et L-03).
    private static CharacterIndex Index => CharacterIndex.Shared;

    // Place du clavier au dernier repeint : le survol y cherche la touche sous la souris.
    private KeyboardPlacement? _keyboardPlacement;

    private int _hoveredKeyIndex = -1;
    private IntPtr _hTooltip;
    private bool _trackingMouse;

    // Pause visuelle quand la fenêtre n'a plus le focus clavier (option A user 2026-05-01) :
    // dim overlay sur le clavier + message « Cliquez pour reprendre ». Le clic restaure
    // automatiquement le focus via Windows et renvoie un WM_SETFOCUS qui repasse à true.
    // _hasFocus reflete l'etat reel ; _focusLostConfirmed est mis a true seulement apres
    // un debounce de 250ms (FOCUS_LOSS_DEBOUNCE_MS) pour eviter d'afficher l'overlay sur
    // des "blinks" focus rapides causes par MoveWindow / ShowWindow / repaint forces.
    private bool _hasFocus = true;
    private bool _focusLostConfirmed;
    private bool _inputPaused;

    // État
    private int _currentStep;
    private int _cursorPosition;
    private bool _currentCharError;
    // Page affichée : l'exercice, le choix après un succès (Recommencer / Suivant, à la
    // place de l'ancienne transition automatique), la page finale après le sixième. Une
    // seule valeur au lieu de deux booléens croisés (audit du 25/09, L-09).
    internal enum TutorialPage { Exercise, Choice, Final }
    private TutorialPage _page = TutorialPage.Exercise;
    // Compteur de succes pour l'exercice courant. Reset a chaque AdvanceToNextStep.
    // Le titre « ✓ Bravo ! » et le sous-titre ne s'affichent qu'au 1er succes (=1).
    // Aux reussites suivantes (apres Recommencer), on n'affiche que les boutons.
    private int _currentStepSuccessCount;


    // Animation de frappe
    private uint _pressedScancode;

    // Le module d'initiation doit rester positionnel, meme si le layout Windows sous-jacent
    // laisse passer ponctuellement un caractere natif (ex: QWERTY US D01 -> q). RawKeyDown
    // arrive avant l'emission de caractere : on y capture le texte AZERTY Global attendu.
    private const int PENDING_PHYSICAL_TEXT_TIMEOUT_MS = 1000;
    private readonly PhysicalTextInputBuffer _pendingPhysicalText =
        new(PENDING_PHYSICAL_TEXT_TIMEOUT_MS);

    // Surlignage du prochain caractère : touches, genre (direct, étape 1, étape 2). Calculé
    // par LessonHintProvider.Guide, le guidage des Leçons réglé pour le tutoriel (audit du
    // 25/09, L-02) ; seuls les ensembles et le genre en sont lus.
    private KeyboardRenderState _guidance = new();

    // Contrôles
    private IntPtr _hWndBtnQuit;
    private IntPtr _hWndBtnSkip;
    private IntPtr _hWndBtnFinish;
    private IntPtr _hWndBtnRetry;     // page de choix fin d'exercice
    private IntPtr _hWndBtnContinue;  // page de choix fin d'exercice (« Suivant » ou « Terminer »)

    // Abonnement à la bascule de langue en cours de tutoriel (retraduction live).
    private readonly Action<string>? _onAppLanguageChanged;

    // États hover pour boutons owner-drawn (Quit + Skip → fond rouge clair au survol)
    private bool _quitHovered;
    private bool _skipHovered;
    private Win32.SUBCLASSPROC? _quitSubclassProc;
    private Win32.SUBCLASSPROC? _skipSubclassProc;

    // DPI
    private float _dpiScale = 1f;
    private int S(int val) => (int)(val * _dpiScale);

    // Polices
    private IntPtr _hFontTitle;
    private IntPtr _hFontInstruction;
    private IntPtr _hFontTarget;
    private IntPtr _hFontStatus;
    private IntPtr _hFontButton;
    private IntPtr _hFontCharMain;
    private IntPtr _hFontCharDeadKey;
    private IntPtr _hFontCharSmall;
    private IntPtr _hFontCharTiny;
    private IntPtr _hFontCtx;
    private IntPtr _hFontTransition;
    private IntPtr _hFontBadge;

    // Brushes
    private IntPtr _hBgBrush;
    private IntPtr _hKbBgBrush;

    // Callback de fermeture
    /// <summary>
    /// Callback invoqué à la fermeture du module. Le bool indique si l'utilisateur a
    /// vraiment complété les 6 exercices (true → page « Bravo ! » + Terminer) ou s'il
    /// a fermé prématurément (false → croix, bouton « Quitter », Esc).
    /// </summary>
    public Action<bool>? OnClosed;

    public LearningModule(IntPtr hWndOnboarding, KeyMapper? mapper, KeyboardHook? hook, Layout? layout)
    {
        ConfigManager.LogCrashTraceDebug("LM.ctor: enter");
        ConfigManager.LogCrashTraceDebug($"LM.ctor: params hWndOnb={hWndOnboarding}, mapper={mapper != null}, hook={hook != null}, layout={layout != null}");
        // Validation explicite après le log diagnostic : si l'un est null, on aura logué
        // l'état exact des params avant de lever (utile pour le bug crash post-Reset).
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(hook);
        ArgumentNullException.ThrowIfNull(layout);
        // Frappe non comptabilisée tant que ce module vit. Les 6 exercices produisent
        // à eux seuls 21 caractères enrichis, pour un seuil de sollicitation d'avis de 20
        // (UsageStats.EnrichedCharsReviewThreshold) : sans cette exclusion, terminer
        // l'onboarding déclenchait la demande de notation, constaté en VM le 2026-09-21.
        // Ouverte après les ThrowIfNull ci-dessus, et refermée par le catch ci-dessous
        // si la suite du constructeur lève : dans ce cas l'instance n'existe pas, Dispose
        // ne passera jamais, et la plage resterait ouverte pour tout le processus — plus
        // une seule frappe comptée jusqu'au redémarrage (R9 de la revue du 2026-09-21).
        // Sur le chemin nominal elle est refermée dans Dispose, protégée par _disposed
        // contre une double fermeture.
        UsageStats.BeginExcludedTyping();
        // Séance d'exercices : aucune demande d'avis tant qu'elle vit, ni dans les dix
        // minutes qui suivent (décision d'Antoine du 2026-09-24).
        LearningSessionTracker.Opened(this);
        try
        {
            _hWndOnboarding = hWndOnboarding;
            ConfigManager.LogCrashTraceDebug("LM.ctor: A1 _hWndOnboarding assigned");
            _mapper = mapper;
            ConfigManager.LogCrashTraceDebug("LM.ctor: A2 _mapper assigned");
            _hook = hook;
            ConfigManager.LogCrashTraceDebug("LM.ctor: A3 _hook assigned");
            _layout = layout;
            ConfigManager.LogCrashTraceDebug("LM.ctor: A4 _layout assigned");
            _wndProcDelegate = WndProc;
            ConfigManager.LogCrashTraceDebug("LM.ctor: A5 _wndProcDelegate created");

            _hBgBrush = Win32.CreateSolidBrush(CLR_BG);
            _hKbBgBrush = Win32.CreateSolidBrush(CLR_KB_BG);

            // DPI initial
            var hdcScreen = Win32.GetDC(IntPtr.Zero);
            int dpi = Win32.GetDeviceCaps(hdcScreen, 88);
            Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
            _dpiScale = dpi / 96f;
            ConfigManager.LogCrashTraceDebug($"LM.ctor: dpi={dpi}, scale={_dpiScale}");

            CreateFonts();
            ConfigManager.LogCrashTraceDebug("LM.ctor: CreateFonts done");
            CreateMainWindow();
            ConfigManager.LogCrashTraceDebug($"LM.ctor: CreateMainWindow done, _hWnd={_hWnd}");
            CreateControls();
            ConfigManager.LogCrashTraceDebug("LM.ctor: CreateControls done");
            UpdateControlVisibility();
            ConfigManager.LogCrashTraceDebug("LM.ctor: UpdateControlVisibility done");

            // Corriger le DPI avec le vrai DPI du moniteur
            int realDpi = Win32.GetDpiForWindow(_hWnd);
            if (realDpi > 0 && Math.Abs(realDpi / 96f - _dpiScale) > 0.01f)
                AdoptWindowDpi(realDpi);

            // S'abonner aux événements
            _mapper.StateChanged += OnStateChanged;
            _hook.RawKeyDown += OnRawKeyDown;
            // Bascule de langue en cours de tutoriel (ex. depuis le menu tray) : retraduire
            // en direct sans fermer le tutoriel ni perdre la progression (constat smoke test
            // 2026-07-17). Les libellés de boutons Win32 sont fixés à la création ; le titre,
            // l'instruction et le texte cible se relisent depuis Steps (dynamique) au repaint.
            _onAppLanguageChanged = _ => RefreshLanguage();
            ConfigManager.AppLanguageChanged += _onAppLanguageChanged;
            ConfigManager.LogCrashTraceDebug("LM.ctor: events subscribed");

            // Highlight initial
            UpdateHighlight();

            // Tooltip pour les touches du clavier (au survol). Comportement aligné sur le
            // testeur web : affiche le caractère + son nom Unicode FR pour les 4 couches.
            CreateTooltip();
            ConfigManager.LogCrashTraceDebug("LM.ctor: UpdateHighlight done — exit");
        }
        catch
        {
            // Le constructeur a levé : personne ne tient l'instance, donc personne
            // n'appellera Dispose. On referme ici avant de relancer.
            UsageStats.EndExcludedTyping();
            LearningSessionTracker.Closed(this);
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Tooltip
    // ═══════════════════════════════════════════════════════════════
    private const uint TTS_ALWAYSTIP = 0x01;
    private const uint TTS_NOPREFIX = 0x02;
    private const uint TTF_SUBCLASS = 0x0010;
    private const uint TTF_TRANSPARENT = 0x0100;
    private const uint TTM_ADDTOOLW = 0x0432;
    private const uint TTM_UPDATETIPTEXTW = 0x0439;
    private const uint TTM_NEWTOOLRECTW = 0x0434;
    private const uint TTM_SETMAXTIPWIDTH = 0x0418;
    private const uint TTM_SETDELAYTIME = 0x0403;
    private const uint TTDT_INITIAL = 3;
    private const uint TTDT_RESHOW = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TOOLINFOW
    {
        public uint cbSize;
        public uint uFlags;
        public IntPtr hwnd;
        public UIntPtr uId;
        public Win32.RECT rect;
        public IntPtr hinst;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszText;
        public IntPtr lParam;
    }

    private void CreateTooltip()
    {
        _hTooltip = Win32.CreateWindowExW(
            Win32.WS_EX_TOPMOST,
            "tooltips_class32", "",
            Win32.WS_POPUP | TTS_ALWAYSTIP | TTS_NOPREFIX,
            0, 0, 0, 0,
            _hWnd, IntPtr.Zero, Win32.GetModuleHandleW(null), IntPtr.Zero);

        if (_hTooltip == IntPtr.Zero) return;

        Win32.SendMessageW(_hTooltip, TTM_SETMAXTIPWIDTH, IntPtr.Zero, (IntPtr)420);
        Win32.SendMessageW(_hTooltip, TTM_SETDELAYTIME, (IntPtr)TTDT_INITIAL, (IntPtr)200);
        Win32.SendMessageW(_hTooltip, TTM_SETDELAYTIME, (IntPtr)TTDT_RESHOW, (IntPtr)50);

        // Outil unique couvrant la fenêtre — on actualise sa zone à chaque hover.
        var ti = new TOOLINFOW
        {
            cbSize = (uint)Marshal.SizeOf<TOOLINFOW>(),
            uFlags = TTF_SUBCLASS | TTF_TRANSPARENT,
            hwnd = _hWnd,
            uId = (UIntPtr)1,
            rect = new Win32.RECT(),
            lpszText = ""
        };
        // Audit sécu 2026-05 SEV-A2-01 : try/finally pour éviter memory leak si exception.
        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<TOOLINFOW>());
        try
        {
            Marshal.StructureToPtr(ti, ptr, false);
            Win32.SendMessageW(_hTooltip, TTM_ADDTOOLW, IntPtr.Zero, ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private void SetTooltip(string text, Win32.RECT rect)
    {
        if (_hTooltip == IntPtr.Zero) return;
        var ti = new TOOLINFOW
        {
            cbSize = (uint)Marshal.SizeOf<TOOLINFOW>(),
            hwnd = _hWnd,
            uId = (UIntPtr)1,
            rect = rect,
            lpszText = text
        };
        // Audit sécu 2026-05 SEV-A2-01 : try/finally pour éviter memory leak si exception.
        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<TOOLINFOW>());
        try
        {
            Marshal.StructureToPtr(ti, ptr, false);
            Win32.SendMessageW(_hTooltip, TTM_UPDATETIPTEXTW, IntPtr.Zero, ptr);
            Win32.SendMessageW(_hTooltip, TTM_NEWTOOLRECTW, IntPtr.Zero, ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Polices
    // ═══════════════════════════════════════════════════════════════
    private void CreateFonts()
    {
        _hFontTitle = Win32.CreateFontW(-S(22), 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontInstruction = Win32.CreateFontW(-S(16), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontTarget = Win32.CreateFontW(-S(20), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontStatus = Win32.CreateFontW(-S(15), 0, 0, 0, 500, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontButton = Win32.CreateFontW(-S(14), 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        // Caractères dans les touches.
        _hFontCharMain = Win32.CreateFontW(S(FONT_CHAR_MAIN), 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 4, 0, "Consolas");
        _hFontCharDeadKey = Win32.CreateFontW(S(FONT_CHAR_DEAD_KEY), 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 4, 0, "Consolas");
        _hFontCharSmall = Win32.CreateFontW(S(FONT_CHAR_SMALL), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 4, 0, "Consolas");
        // Nom des touches sous le résultat d'une touche morte armée : la taille des Leçons.
        _hFontCharTiny = Win32.CreateFontW(S(16), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 4, 0, "Segoe UI");
        _hFontCtx = Win32.CreateFontW(S(FONT_CTX), 0, 0, 0, 500, 0, 0, 0, 0, 0, 0, 4, 0, "Segoe UI");
        _hFontTransition = Win32.CreateFontW(-S(28), 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontBadge = Win32.CreateFontW(S(9), 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 4, 0, "Segoe UI");
    }

    private void DestroyFonts()
    {
        Win32.DeleteObject(_hFontTitle);
        Win32.DeleteObject(_hFontInstruction);
        Win32.DeleteObject(_hFontTarget);
        Win32.DeleteObject(_hFontStatus);
        Win32.DeleteObject(_hFontButton);
        Win32.DeleteObject(_hFontCharMain);
        Win32.DeleteObject(_hFontCharDeadKey);
        Win32.DeleteObject(_hFontCharSmall);
        Win32.DeleteObject(_hFontCharTiny);
        Win32.DeleteObject(_hFontCtx);
        Win32.DeleteObject(_hFontTransition);
        Win32.DeleteObject(_hFontBadge);
    }

    private void RecreateFonts()
    {
        DestroyFonts();
        CreateFonts();
        Win32.SendMessageW(_hWndBtnQuit, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
        Win32.SendMessageW(_hWndBtnSkip, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
        Win32.SendMessageW(_hWndBtnFinish, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
    }

    // ═══════════════════════════════════════════════════════════════
    // Fenêtre
    // ═══════════════════════════════════════════════════════════════
    private void CreateMainWindow()
    {
        var hInstance = Win32.GetModuleHandleW(null);

        // Sans fond de classe : WM_ERASEBKGND rend 1 et OnPaint remplit le fond avec
        // _hBgBrush, qui reste à cette instance (bug Reset → Essayer du 2026-05-01).
        // Audit du 25/09, X-01 : une classe refusée ne crée pas de fenêtre.
        if (!NativeWindow.RegisterClass(WND_CLASS_NAME, _wndProcDelegate))
            return;

        int winW = S(BASE_WIN_W);
        int winH = S(BASE_WIN_H);
        // WS_CLIPCHILDREN : empeche la fenetre parent de peindre dans les zones des
        // boutons enfants (Quitter, Passer, Recommencer, etc.). Sans ce flag, OnPaint
        // peut briévement peindre par-dessus les boutons lors d'un repaint frequent
        // (ex. frappe rapide en exo), causant un flicker visible.
        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU | Win32.WS_CLIPCHILDREN;
        uint dwExStyle = Win32.WS_EX_TOPMOST;

        // Adapter aux petits écrans
        Win32.GetCursorPos(out var cursorPt);
        var hMonitor = Win32.MonitorFromPoint(cursorPt, 0x00000001);
        var monInfo = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        Win32.GetMonitorInfo(hMonitor, ref monInfo);
        int screenW = monInfo.rcWork.right - monInfo.rcWork.left;
        int screenH = monInfo.rcWork.bottom - monInfo.rcWork.top;
        int maxW = (int)(screenW * 0.9f);
        int maxH = (int)(screenH * 0.9f);
        if (winW > maxW || winH > maxH)
        {
            float ratio = 1100f / 600f;
            if ((float)maxW / maxH > ratio)
            { winH = maxH; winW = (int)(winH * ratio); }
            else
            { winW = maxW; winH = (int)(winW / ratio); }
        }

        var adjustRect = new Win32.RECT { left = 0, top = 0, right = winW, bottom = winH };
        Win32.AdjustWindowRectEx(ref adjustRect, dwStyle, false, dwExStyle);
        int windowW = adjustRect.right - adjustRect.left;
        int windowH = adjustRect.bottom - adjustRect.top;

        int screenX = monInfo.rcWork.left;
        int screenY = monInfo.rcWork.top;

        _hWnd = Win32.CreateWindowExW(dwExStyle, WND_CLASS_NAME, L.Learning_WindowTitle,
            dwStyle,
            screenX + (screenW - windowW) / 2, screenY + (screenH - windowH) / 2,
            windowW, windowH,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        Win32.EnableDarkTitleBar(_hWnd);
    }

    /// <summary>
    /// Passe la fenêtre, née au DPI du système, au DPI de son écran : polices, taille, puis
    /// place des boutons. Sans ce dernier pas, « Quitter les exercices » gardait sa place
    /// calculée au DPI du système et sortait du bord quand l'écran en avait un autre
    /// (lot 9 du 26/09) ; WM_DPICHANGED, lui, replaçait déjà les boutons.
    /// </summary>
    internal void AdoptWindowDpi(int dpi)
    {
        _dpiScale = dpi / 96f;
        // Mise en page seule : GetDpiForWindow ne lève pas (audit du 25/09, X-04).
        try
        {
            RecreateFonts();
            ResizeWindow();
            RepositionControls();
        }
        catch { }
    }

    private void ResizeWindow()
    {
        int winW = S(BASE_WIN_W);
        int winH = S(BASE_WIN_H);
        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        uint dwExStyle = Win32.WS_EX_TOPMOST;
        var adjustRect = new Win32.RECT { left = 0, top = 0, right = winW, bottom = winH };
        Win32.AdjustWindowRectEx(ref adjustRect, dwStyle, false, dwExStyle);
        int windowW = adjustRect.right - adjustRect.left;
        int windowH = adjustRect.bottom - adjustRect.top;
        Win32.GetWindowRect(_hWnd, out var currentRect);
        int cx = (currentRect.left + currentRect.right) / 2;
        int cy = (currentRect.top + currentRect.bottom) / 2;
        Win32.MoveWindow(_hWnd, cx - windowW / 2, cy - windowH / 2, windowW, windowH, true);
    }

    private void CreateControls()
    {
        var hInstance = Win32.GetModuleHandleW(null);
        int margin = S(BASE_MARGIN);

        // Bouton Quit en owner-draw pour gérer le hover (fond rouge clair).
        _hWndBtnQuit = Win32.CreateWindowExW(0, "BUTTON", L.Learning_BtnQuit,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_OWNERDRAW,
            margin, 0, S(180), S(30),
            _hWnd, (IntPtr)IDC_BTN_QUIT, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndBtnQuit, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
        _quitSubclassProc = QuitButtonSubclassProc;
        Win32.SetWindowSubclass(_hWndBtnQuit, _quitSubclassProc, (UIntPtr)1, IntPtr.Zero);

        // Bouton Skip en owner-draw avec hover rouge clair (même style que Quit).
        _hWndBtnSkip = Win32.CreateWindowExW(0, "BUTTON", L.Learning_BtnSkip,
            Win32.WS_CHILD | Win32.WS_TABSTOP | Win32.BS_OWNERDRAW,
            0, 0, S(160), S(30),
            _hWnd, (IntPtr)IDC_BTN_SKIP, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndBtnSkip, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
        _skipSubclassProc = SkipButtonSubclassProc;
        Win32.SetWindowSubclass(_hWndBtnSkip, _skipSubclassProc, (UIntPtr)2, IntPtr.Zero);

        _hWndBtnFinish = Win32.CreateWindowExW(0, "BUTTON", L.Learning_BtnFinish,
            Win32.WS_CHILD | Win32.WS_TABSTOP,
            0, 0, S(140), S(30),
            _hWnd, (IntPtr)IDC_BTN_FINISH, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndBtnFinish, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);

        // Page de choix fin d'exercice : Reessayer + Suivant (cote a cote, centres sous le titre)
        _hWndBtnRetry = Win32.CreateWindowExW(0, "BUTTON", L.Learning_BtnRetry,
            Win32.WS_CHILD | Win32.WS_TABSTOP,
            0, 0, S(200), S(36),
            _hWnd, (IntPtr)IDC_BTN_RETRY, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndBtnRetry, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);

        _hWndBtnContinue = Win32.CreateWindowExW(0, "BUTTON", L.Learning_BtnNext,
            Win32.WS_CHILD | Win32.WS_TABSTOP,
            0, 0, S(180), S(36),
            _hWnd, (IntPtr)IDC_BTN_CONTINUE, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndBtnContinue, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);

        RepositionControls();
    }

    private void RepositionControls()
    {
        Win32.GetClientRect(_hWnd, out var cr);
        int cw = cr.right;
        int ch = cr.bottom;
        int margin = S(BASE_MARGIN);

        // Quitter et Skip restent en haut à droite. Terminer (page finale Bravo !) est
        // descendu juste au-dessus du clavier, centré horizontalement, pour rester aligne
        // avec la position du bouton « Suivant » de la page de choix precedente.
        int btnW = S(180);
        int btnH = S(30);
        int btnX = cw - margin - btnW;
        Win32.MoveWindow(_hWndBtnQuit, btnX, S(10), btnW, btnH, true);
        Win32.MoveWindow(_hWndBtnSkip, btnX, S(10) + btnH + S(6), btnW, btnH, true);

        // Page de choix fin d'exercice : Reessayer + Suivant centres horizontalement.
        // Position verticale :
        //   1er succes (avec « ✓ Bravo ! ») → block Bravo + boutons centre verticalement
        //                                      dans la zone superieure (boutons descendus
        //                                      pour laisser place au titre au-dessus)
        //   n-ieme succes (sans Bravo)     → boutons seuls centres verticalement
        int retryW = S(200);
        int continueW = S(180);
        int choiceBtnH = S(36);
        int choiceGap = S(16);
        int totalW = retryW + choiceGap + continueW;
        int choiceX = (cw - totalW) / 2;
        int kbTop = S(BASE_HEADER_H + BASE_INSTRUCTION_H + BASE_TARGET_H);

        int choiceY;
        if (_page == TutorialPage.Choice && _currentStepSuccessCount <= 1)
        {
            // Block Bravo + gap + boutons, centre verticalement
            int titleH = S(40);
            int gapTitleButtons = S(14);
            int blockH = titleH + gapTitleButtons + choiceBtnH;
            int blockTop = (kbTop - blockH) / 2;
            choiceY = blockTop + titleH + gapTitleButtons;
        }
        else
        {
            choiceY = (kbTop - choiceBtnH) / 2;
        }
        Win32.MoveWindow(_hWndBtnRetry, choiceX, choiceY, retryW, choiceBtnH, true);
        Win32.MoveWindow(_hWndBtnContinue, choiceX + retryW + choiceGap, choiceY, continueW, choiceBtnH, true);

        // Bouton Terminer (page finale « Bravo ! ») : aligné à droite, juste au-dessus du
        // clavier — laisse de la place au sous-titre « Vous maîtrisez... » qui est centré.
        int finishW = S(140);
        int finishH = S(36);
        int finishY = kbTop - finishH - S(16);
        int finishX = cw - margin - finishW;
        Win32.MoveWindow(_hWndBtnFinish, finishX, finishY, finishW, finishH, true);
    }

    private void UpdateControlVisibility()
    {
        if (_page == TutorialPage.Final)
        {
            // Page « Bravo ! » finale (apres les 6 exercices) : seul Terminer
            Win32.ShowWindow(_hWndBtnQuit, 0);
            Win32.ShowWindow(_hWndBtnSkip, 0);
            Win32.ShowWindow(_hWndBtnFinish, 1);
            Win32.ShowWindow(_hWndBtnRetry, 0);
            Win32.ShowWindow(_hWndBtnContinue, 0);
        }
        else if (_page == TutorialPage.Choice)
        {
            // Page de choix fin d'exercice : Reessayer + Suivant. On masque tout le reste.
            Win32.ShowWindow(_hWndBtnQuit, 0);
            Win32.ShowWindow(_hWndBtnSkip, 0);
            Win32.ShowWindow(_hWndBtnFinish, 0);
            Win32.ShowWindow(_hWndBtnRetry, 1);
            Win32.ShowWindow(_hWndBtnContinue, 1);
            // « Suivant » → « Terminer » au dernier exercice (on basculera ensuite sur la page Bravo finale)
            bool isLast = _currentStep >= Steps.Length - 1;
            Win32.SetWindowTextW(_hWndBtnContinue, isLast ? L.Learning_BtnFinishAll : L.Learning_BtnNext);
        }
        else
        {
            Win32.ShowWindow(_hWndBtnQuit, 1);
            bool skippable = _currentStep < Steps.Length && Steps[_currentStep].Skippable;
            Win32.ShowWindow(_hWndBtnSkip, skippable ? 1 : 0);
            Win32.ShowWindow(_hWndBtnFinish, 0);
            Win32.ShowWindow(_hWndBtnRetry, 0);
            Win32.ShowWindow(_hWndBtnContinue, 0);
        }
    }

    /// <summary>Retraduit le tutoriel en direct après une bascule de langue (menu tray) :
    /// libellés des boutons Win32 fixés à la création + repaint (titre/instruction/cible
    /// se relisent depuis Steps). La progression et l'étape courante sont préservées.</summary>
    private void RefreshLanguage()
    {
        if (_hWnd == IntPtr.Zero) return;
        Win32.SetWindowTextW(_hWnd, L.Learning_WindowTitle);
        Win32.SetWindowTextW(_hWndBtnQuit, L.Learning_BtnQuit);
        Win32.SetWindowTextW(_hWndBtnSkip, L.Learning_BtnSkip);
        Win32.SetWindowTextW(_hWndBtnFinish, L.Learning_BtnFinish);
        Win32.SetWindowTextW(_hWndBtnRetry, L.Learning_BtnRetry);
        Win32.SetWindowTextW(_hWndBtnContinue, L.Learning_BtnNext);
        UpdateControlVisibility(); // réapplique le libellé dynamique de « Suivant »/« Terminer »
        RepositionControls();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    // ═══════════════════════════════════════════════════════════════
    // Show / Close
    // ═══════════════════════════════════════════════════════════════
    public void Show()
    {
        ConfigManager.LogCrashTraceDebug("LM.Show: enter");
        // Tous les exos demarrent avec Caps Lock OFF — l'utilisateur l'active explicitement
        // si l'exo le requiert (ex1, ex2). Sans ca, un Verr.Maj. herite du contexte
        // exterieur (avant le clic Essayer maintenant) brise la pedagogie de l'exo 1
        // (« Activez Verr. Maj. puis tapez sur la lettre é »).
        if (!_inputPaused)
            _mapper.RequestCapsLockOff();
        Win32.EnableWindow(_hWndOnboarding, false);
        Win32.ShowWindow(_hWnd, 1);
        ConfigManager.LogCrashTraceDebug("LM.Show: ShowWindow done");
        // Le synthetic VK_CAPITAL inject par RequestCapsLockOff() est traite par Windows
        // de maniere asynchrone : entre ShowWindow et le 1er WM_PAINT, _mapper._capsLockState
        // peut etre desaligne avec Windows reel. Sans cette resync timeree, la touche
        // Verr. Maj. peut etre rendue comme « activee » jusqu'a la 1ere frappe utilisateur.
        // 50ms suffisent largement pour que Windows ait traite le toggle.
        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_CAPS_RESYNC, 50, IntPtr.Zero);
        TakeFocus();
        // Windows bloque souvent SetForegroundWindow au 1er appel (anti-vol de focus).
        // On retry plusieurs fois via timer jusqu'à ce que GetForegroundWindow() == _hWnd.
        _refocusAttempts = 0;
        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_REFOCUS, 80, IntPtr.Zero);
        ConfigManager.LogCrashTraceDebug("LM.Show: focus done — exit");
    }

    public void SetInputPaused(bool paused)
    {
        if (_inputPaused == paused) return;
        _inputPaused = paused;

        if (paused)
        {
            _pressedScancode = 0;
            Win32.KillTimer(_hWnd, (UIntPtr)TIMER_REFOCUS);
            Win32.KillTimer(_hWnd, (UIntPtr)TIMER_FOCUS_LOST_CONFIRM);
            if (_hWnd != IntPtr.Zero)
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        }
    }

    private static bool IsPausedInputMessage(uint msg)
    {
        return msg is Win32.WM_KEYDOWN or Win32.WM_KEYUP or Win32.WM_SYSKEYDOWN or Win32.WM_SYSKEYUP
            or Win32.WM_CHAR or Win32.WM_SYSCHAR or Win32.WM_SYSDEADCHAR
            or Win32.WM_COMMAND or Win32.WM_PASTE or Win32.WM_CUT or Win32.WM_CLEAR or Win32.WM_UNDO;
    }

    private int _refocusAttempts;

    /// <summary>
    /// Force le focus clavier sur la fenêtre LearningModule. Utilisé au lancement
    /// (Show) et après chaque transition entre exercices (AdvanceToNextStep) pour
    /// que les frappes utilisateur arrivent sans qu'il doive recliquer la fenêtre.
    /// </summary>
    private void TakeFocus()
    {
        var foreWnd = Win32.GetForegroundWindow();
        uint foreThread = Win32.GetWindowThreadProcessId(foreWnd, IntPtr.Zero);
        uint curThread = Win32.GetCurrentThreadId();
        if (foreThread != curThread)
            Win32.AttachThreadInput(curThread, foreThread, true);
        Win32.SetForegroundWindow(_hWnd);
        Win32.SetFocus(_hWnd);
        if (foreThread != curThread)
            Win32.AttachThreadInput(curThread, foreThread, false);
    }

    /// <summary>
    /// Ferme le LearningModule : unsubscribe les events, masque la fenetre, reactive
    /// l'OnboardingWindow, declenche OnClosed (qui Dispose). Internal pour permettre
    /// a OnboardingWindow.ResetState() de fermer une session en cours.
    /// </summary>
    internal void Close()
    {
        _mapper.StateChanged -= OnStateChanged;
        _hook.RawKeyDown -= OnRawKeyDown;
        Win32.ShowWindow(_hWnd, 0);
        Win32.EnableWindow(_hWndOnboarding, true);
        Win32.SetForegroundWindow(_hWndOnboarding);
        OnClosed?.Invoke(_page == TutorialPage.Final);
    }

    // ═══════════════════════════════════════════════════════════════
    // Événements KeyMapper / KeyboardHook
    // ═══════════════════════════════════════════════════════════════
    private void OnStateChanged()
    {
        // Mettre à jour le highlight (l'état des modificateurs a changé)
        UpdateHighlight();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);

        // Le rafraichissement du tooltip etait fait ici lors d'un changement d'etat
        // (touche morte, Caps Lock). Probleme : entre deux exercices, RequestCapsLockOff
        // fire StateChanged → si la souris est immobile sur une touche, le tooltip
        // re-popait pour la meme touche, ce qui surprenait l'utilisateur. Le tooltip
        // est desormais rafraichi uniquement sur mouvement souris (OnMouseMove). Le seul
        // cas qui n'est plus couvert : l'utilisateur survole une touche immobile pendant
        // une transition de touche morte → texte legerement obsolete jusqu'au prochain
        // mouvement de souris. Acceptable.
    }

    private void OnRawKeyDown(uint scancode)
    {
        _pressedScancode = scancode;
        CaptureExpectedTextForPhysicalKey(scancode);
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_KEYPRESS, KEYPRESS_DURATION_MS, IntPtr.Zero);
    }

    private void CaptureExpectedTextForPhysicalKey(uint scancode)
    {
        if (!_hasFocus || _page != TutorialPage.Exercise) return;
        _pendingPhysicalText.CaptureKey(_layout, scancode, _mapper);
    }

    private void ClearPendingPhysicalText()
    {
        // Certains tests de compatibilité construisent le module sans exécuter son
        // constructeur afin d'isoler la logique Retour arrière.
        _pendingPhysicalText?.Clear();
    }

    private void OnMouseMove(IntPtr lParam)
    {
        if (!_trackingMouse)
        {
            var tme = new Win32.TRACKMOUSEEVENT
            {
                cbSize = (uint)Marshal.SizeOf<Win32.TRACKMOUSEEVENT>(),
                dwFlags = Win32.TME_LEAVE,
                hwndTrack = _hWnd,
                dwHoverTime = 0
            };
            Win32.TrackMouseEvent(ref tme);
            _trackingMouse = true;
        }

        int mx = (short)(lParam.ToInt64() & 0xFFFF);
        int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

        int hitIndex = -1;
        KeyboardHitTestResult hit = default;
        if (_keyboardPlacement is { } placement)
        {
            int i = 0;
            foreach (var key in KeyboardRenderer.BuildHitTestRects(placement))
            {
                var rc = key.Rect;
                if (mx >= rc.left && mx < rc.right && my >= rc.top && my < rc.bottom)
                {
                    hitIndex = i;
                    hit = key;
                    break;
                }
                i++;
            }
        }

        if (hitIndex != _hoveredKeyIndex)
        {
            _hoveredKeyIndex = hitIndex;
            if (hitIndex >= 0)
            {
                string text = KeyboardRenderer.BuildTooltipText(_layout, KeyboardRenderProfile.Onboarding,
                    BuildKeyboardState(), hit.Scancode, hit.Label);
                if (string.IsNullOrEmpty(text))
                    SetTooltip("", new Win32.RECT()); // pas de correspondance avec la dk active → pas de tooltip
                else
                    SetTooltip(text, hit.Rect);
            }
            else
            {
                SetTooltip("", new Win32.RECT());
            }
        }
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

                case Win32.WM_MOUSEMOVE:
                    OnMouseMove(lParam);
                    return IntPtr.Zero;

                case Win32.WM_MOUSELEAVE:
                    _trackingMouse = false;
                    if (_hoveredKeyIndex != -1)
                    {
                        _hoveredKeyIndex = -1;
                        SetTooltip("", new Win32.RECT());
                    }
                    return IntPtr.Zero;

                case Win32.WM_KILLFOCUS:
                {
                    // wParam contient le HWND qui prend le focus. Si c'est un de nos child
                    // windows (boutons en-tete, tooltip), on n'affiche PAS l'overlay
                    // « Cliquez pour reprendre » : le focus reste de fait dans notre arborescence
                    // et les boutons d'avancement (Skip/Retry/Continue) reprennent le focus
                    // tout seuls via TakeFocus() apres leur action.
                    IntPtr nextFocus = wParam;
                    bool focusGoingToOurChild = nextFocus != IntPtr.Zero
                        && (nextFocus == _hWndBtnQuit
                         || nextFocus == _hWndBtnSkip
                         || nextFocus == _hWndBtnFinish
                         || nextFocus == _hWndBtnRetry
                         || nextFocus == _hWndBtnContinue
                         || nextFocus == _hTooltip);
                    if (!focusGoingToOurChild)
                    {
                        _hasFocus = false;
                        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_REFOCUS);
                        // Debounce : ne pas afficher l'overlay immediatement. Attendre 250ms
                        // pour ignorer les "blinks" causes par MoveWindow/ShowWindow/repaint.
                        // Si le focus revient pendant ce delai, on annule. Sinon on confirme
                        // la perte et on affiche l'overlay.
                        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_FOCUS_LOST_CONFIRM, FOCUS_LOSS_DEBOUNCE_MS, IntPtr.Zero);
                    }
                    return IntPtr.Zero;
                }

                case Win32.WM_SETFOCUS:
                    _hasFocus = true;
                    _focusLostConfirmed = false;
                    Win32.KillTimer(_hWnd, (UIntPtr)TIMER_FOCUS_LOST_CONFIRM);
                    // Resync des modificateurs et de Caps Lock au retour de focus — corrige
                    // les desynchros heritees d'un contexte exterieur (jeu en arriere-plan,
                    // touches modifs tenues lors de l'install du hook). Cf. bug 2026-05-03 :
                    // virgule bloquee dans l'exo 2 quand l'app demarrait pendant un jeu.
                    _mapper?.SyncState();
                    Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
                    return IntPtr.Zero;

                case Win32.WM_DRAWITEM:
                {
                    var dis = Marshal.PtrToStructure<Win32.DRAWITEMSTRUCT>(lParam);
                    if (dis.CtlID == IDC_BTN_QUIT)
                    {
                        DrawHoverButton(in dis, _quitHovered, L.Learning_BtnQuit);
                        return (IntPtr)1;
                    }
                    if (dis.CtlID == IDC_BTN_SKIP)
                    {
                        DrawHoverButton(in dis, _skipHovered, L.Learning_BtnSkip);
                        return (IntPtr)1;
                    }
                    break;
                }

                case 0x0102: // WM_CHAR
                    OnChar((char)wParam.ToInt32());
                    return IntPtr.Zero;

                case Win32.WM_SYSCHAR:
                    // Sous un layout sous-jacent sans AltGr natif (ex. QWERTY US), Right Alt
                    // reste un Alt systeme pour Windows. Les caracteres injectes par le hook
                    // arrivent alors parfois en WM_SYSCHAR au lieu de WM_CHAR : les traiter
                    // comme une saisie normale evite le bip DefWindowProc et valide l'exercice.
                    OnChar((char)wParam.ToInt32());
                    return IntPtr.Zero;

                case Win32.WM_SYSDEADCHAR:
                    return IntPtr.Zero;

                case Win32.WM_SYSKEYDOWN:
                {
                    int vk = wParam.ToInt32();
                    if (vk == 0x73) // VK_F4, preserve Alt+F4.
                    {
                        Close();
                        return IntPtr.Zero;
                    }
                    if (vk == 0x08) // VK_BACK
                        OnBackspace();
                    else if (vk == 0x1B) // VK_ESCAPE
                        Close();
                    return IntPtr.Zero;
                }

                case Win32.WM_SYSKEYUP:
                    return IntPtr.Zero;

                case Win32.WM_KEYDOWN:
                {
                    int vk = wParam.ToInt32();
                    if (_page == TutorialPage.Final)
                    {
                        // Page finale « Bravo ! » : flèches droite/bas ou Esc → Terminer (Close).
                        if (vk == 0x27 || vk == 0x28 || vk == 0x1B) // VK_RIGHT, VK_DOWN, VK_ESCAPE
                            Close();
                        return IntPtr.Zero;
                    }
                    if (_page == TutorialPage.Choice)
                    {
                        // Page de choix fin d'exercice : flèches gauche/haut → Recommencer,
                        // flèches droite/bas → Suivant (ou Terminer au dernier exercice).
                        if (vk == 0x25 || vk == 0x26) // VK_LEFT, VK_UP
                            RetryCurrentStep();
                        else if (vk == 0x27 || vk == 0x28) // VK_RIGHT, VK_DOWN
                            ContinueAfterChoice();
                        else if (vk == 0x1B) // VK_ESCAPE
                            Close();
                        return IntPtr.Zero;
                    }
                    if (vk == 0x09) // VK_TAB
                    {
                        // K3 (accessibilité 1.3.0) : Tab et Maj+Tab mènent aux boutons
                        // d'en-tête ; « Passer cet exercice » n'était atteignable qu'à la
                        // souris. Aucun exercice ne contient de tabulation : la touche ne
                        // faisait que compter une faute.
                        CycleHeaderFocus(_hWnd);
                        return IntPtr.Zero;
                    }
                    if (vk == 0x08) // VK_BACK
                        OnBackspace();
                    else if (vk == 0x1B) // VK_ESCAPE
                        Close();
                    return IntPtr.Zero;
                }

                case Win32.WM_COMMAND:
                    switch (wParam.ToInt32() & 0xFFFF)
                    {
                        case IDC_BTN_QUIT: Close(); break;
                        case IDC_BTN_SKIP: SkipStep(); break;
                        case IDC_BTN_FINISH: Close(); break;
                        case IDC_BTN_RETRY: RetryCurrentStep(); break;
                        case IDC_BTN_CONTINUE: ContinueAfterChoice(); break;
                    }
                    return IntPtr.Zero;

                case Win32.WM_TIMER:
                {
                    var timerId = (uint)wParam.ToInt64();
                    if (timerId == TIMER_KEYPRESS)
                    {
                        Win32.KillTimer(hWnd, (UIntPtr)TIMER_KEYPRESS);
                        _pressedScancode = 0;
                        Win32.InvalidateRect(hWnd, IntPtr.Zero, false);
                    }
                    else if (timerId == TIMER_REFOCUS)
                    {
                        Win32.KillTimer(hWnd, (UIntPtr)TIMER_REFOCUS);
                        _refocusAttempts++;
                        var fg = Win32.GetForegroundWindow();
                        if (fg != _hWnd && _refocusAttempts < REFOCUS_MAX_ATTEMPTS)
                        {
                            TakeFocus();
                            Win32.SetTimer(_hWnd, (UIntPtr)TIMER_REFOCUS, 80, IntPtr.Zero);
                        }
                    }
                    else if (timerId == TIMER_CAPS_RESYNC)
                    {
                        // Resync Caps Lock apres que Windows ait traite le synthetic VK_CAPITAL
                        // inject par Show(). Sans ca, le 1er paint peut afficher Verr. Maj.
                        // comme activee alors que Windows est OFF.
                        Win32.KillTimer(hWnd, (UIntPtr)TIMER_CAPS_RESYNC);
                        _mapper.SyncState();
                        UpdateHighlight();
                        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                    }
                    else if (timerId == TIMER_FOCUS_LOST_CONFIRM)
                    {
                        Win32.KillTimer(hWnd, (UIntPtr)TIMER_FOCUS_LOST_CONFIRM);
                        // Si le focus n'est toujours pas revenu apres le debounce, on confirme
                        // la perte et on affiche l'overlay.
                        if (!_hasFocus)
                        {
                            _focusLostConfirmed = true;
                            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
                        }
                    }
                    return IntPtr.Zero;
                }

                case Win32.WM_DPICHANGED:
                {
                    int newDpi = (wParam.ToInt32() >> 16) & 0xFFFF;
                    if (newDpi > 0) _dpiScale = newDpi / 96f;
                    RecreateFonts();
                    var suggested = Marshal.PtrToStructure<Win32.RECT>(lParam);
                    Win32.MoveWindow(_hWnd, suggested.left, suggested.top,
                        suggested.right - suggested.left, suggested.bottom - suggested.top, true);
                    RepositionControls();
                    Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
                }

                case Win32.WM_CLOSE:
                    Close();
                    return IntPtr.Zero;

                case Win32.WM_DESTROY:
                    return IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            ConfigManager.Log("LearningModule WndProc", ex);
        }

        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    // ═══════════════════════════════════════════════════════════════
    // Saisie
    // ═══════════════════════════════════════════════════════════════
    private void OnChar(char c)
    {
        // K3 : le WM_CHAR de Tab arrive encore ici après que WM_KEYDOWN a déplacé le focus
        // (TranslateMessage l'a posté à cette fenêtre) ; ce n'est pas une frappe d'exercice.
        if (c == '\t') return;
        if (_page != TutorialPage.Exercise) return;
        if (_currentStep >= Steps.Length) return;

        var target = Steps[_currentStep].Target;
        if (_cursorPosition >= target.Length) return;

        char typed = ResolveTypedCharacter(c);
        if (typed == target[_cursorPosition])
        {
            _cursorPosition++;
            _currentCharError = false;

            if (_cursorPosition >= target.Length)
            {
                // Exercice termine : afficher la page de choix Reessayer / Suivant
                // (au lieu d'enchainer automatiquement comme avant). L'utilisateur decide
                // s'il refait l'exercice ou passe au suivant.
                _page = TutorialPage.Choice;
                _currentStepSuccessCount++;
                // Persister la progression : exercice (_currentStep + 1) valide. Setter monotone,
                // donc safe meme en cas de Recommencer puis nouveau succes (no-op si deja persiste).
                ConfigManager.SetLearningMaxStepCompleted(_currentStep + 1);
                _mapper.RequestCapsLockOff(); // reinciter a appuyer sur Verr.Maj. au prochain exercice
                ClearHighlight();
                UpdateControlVisibility();
                RepositionControls();
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
            }
            else
            {
                UpdateHighlight();
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
            }
        }
        else
        {
            _currentCharError = true;
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        }
    }

    private char ResolveTypedCharacter(char received)
    {
        return _pendingPhysicalText.Resolve(received);
    }

    private void OnBackspace()
    {
        // Une mauvaise frappe ne fait pas avancer le curseur, mais reste affichée en erreur.
        // Backspace l'efface pour permettre de retaper immédiatement le caractère attendu.
        if (_currentCharError)
        {
            _currentCharError = false;
            ClearPendingPhysicalText();
            if (_hWnd != IntPtr.Zero)
            {
                UpdateHighlight();
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
            }
            return;
        }

        // Backspace peut aussi annuler une mauvaise touche morte activee dans le mapper.
        // On rafraichit alors le guidage vers l'etape 1.
        if (_guidance.HighlightedScancodes.Contains(0x0E))
        {
            UpdateHighlight();
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        }
        return;
    }

    private void AdvanceToNextStep()
    {
        _currentStep++;
        _cursorPosition = 0;
        _currentCharError = false;
        _page = TutorialPage.Exercise;
        _currentStepSuccessCount = 0; // reset pour le nouvel exercice (1er succes => « Bravo ! » s'affiche)
        ClearPendingPhysicalText();

        if (_currentStep >= Steps.Length)
        {
            _page = TutorialPage.Final;
            ClearHighlight();
        }
        else
        {
            UpdateHighlight();
        }

        UpdateControlVisibility();
        RepositionControls();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
        // Le focus est souvent perdu pendant la transition (boutons « Quitter / Passer »
        // recevant la souris, etc.) — on le reprend pour que l'utilisateur n'ait pas
        // besoin de recliquer la fenêtre avant de taper l'exercice suivant.
        TakeFocus();
    }

    private void SkipStep()
    {
        if (_currentStep < Steps.Length && Steps[_currentStep].Skippable)
            AdvanceToNextStep();
    }

    /// <summary>
    /// K3 (accessibilité 1.3.0) : cycle de tabulation surface de frappe → « Quitter » →
    /// « Passer » (s'il est affiché) → surface, à l'envers avec Maj. La fenêtre reste hors
    /// d'IsDialogMessageW, qui mangerait les frappes de l'exercice : elle tourne son focus
    /// elle-même.
    /// </summary>
    private void CycleHeaderFocus(IntPtr from)
    {
        var stops = new List<IntPtr> { _hWnd };
        if (Win32.IsWindowVisible(_hWndBtnQuit)) stops.Add(_hWndBtnQuit);
        if (Win32.IsWindowVisible(_hWndBtnSkip)) stops.Add(_hWndBtnSkip);
        bool backwards = (TypingEngine.Windows.Win32.GetKeyState(0x10) & 0x8000) != 0; // VK_SHIFT
        int next = DialogNavigation.NextFocusStop(stops.IndexOf(from), stops.Count, backwards);
        if (next >= 0) Win32.SetFocus(stops[next]);
    }

    /// <summary>Ce que fait une frappe reçue par un bouton d'en-tête (K3, revu audit 24/09).</summary>
    internal enum HeaderKeyAction { None, CycleFocus, Click, Close, Swallow, ForwardToExercise }

    /// <summary>
    /// K3 : clavier des boutons d'en-tête. Hors d'IsDialogMessageW, un BUTTON ne rend pas Tab
    /// et ne réagit pas à Entrée : Tab tourne le focus, Entrée clique, Échap ferme, comme sur
    /// la surface.
    ///
    /// Audit 24/09 (régression K3) : un Tab accidentel laissait le focus sur « Quitter », et la
    /// première espace de l'exercice fermait le tutoriel — un BUTTON s'enfonce sur WM_KEYDOWN
    /// d'espace et clique au WM_KEYUP, dans sa propre procédure. L'espace est donc avalée sur
    /// les deux, et tout caractère imprimable (espace comprise) retourne à l'exercice, où il
    /// compte comme frappe. Entrée reste la seule activation clavier du bouton.
    /// </summary>
    internal static HeaderKeyAction ClassifyHeaderButtonKey(uint msg, int wParam)
    {
        switch (msg)
        {
            case Win32.WM_KEYDOWN:
                return wParam switch
                {
                    0x09 => HeaderKeyAction.CycleFocus, // VK_TAB
                    0x0D => HeaderKeyAction.Click,      // VK_RETURN
                    0x1B => HeaderKeyAction.Close,      // VK_ESCAPE
                    0x20 => HeaderKeyAction.Swallow,    // VK_SPACE : sinon le bouton s'enfonce
                    _ => HeaderKeyAction.None,
                };
            case Win32.WM_KEYUP:
                // VK_SPACE : le relâchement est ce qui clique.
                return wParam == 0x20 ? HeaderKeyAction.Swallow : HeaderKeyAction.None;
            case Win32.WM_CHAR:
            case Win32.WM_SYSCHAR: // même traitement que la surface (AltGr sous un layout US)
                // Tab, Entrée, Échap, Retour arrière : caractères de contrôle, traités au keydown.
                return char.IsControl((char)wParam) ? HeaderKeyAction.None : HeaderKeyAction.ForwardToExercise;
        }
        return HeaderKeyAction.None;
    }

    private bool HandleHeaderButtonKey(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (_inputPaused) return false;
        switch (ClassifyHeaderButtonKey(msg, wParam.ToInt32()))
        {
            case HeaderKeyAction.CycleFocus:
                CycleHeaderFocus(hWnd);
                return true;
            case HeaderKeyAction.Click:
                Win32.SendMessageW(hWnd, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                return true;
            case HeaderKeyAction.Close:
                Close();
                return true;
            case HeaderKeyAction.Swallow:
                return true;
            case HeaderKeyAction.ForwardToExercise:
                // Le focus revient à la surface avant la frappe : les suivantes (et le
                // WM_KEYUP de cette espace) y arrivent directement.
                Win32.SetFocus(_hWnd);
                Win32.SendMessageW(_hWnd, msg, wParam, lParam);
                return true;
        }
        return false;
    }

    /// <summary>
    /// Page de choix fin d'exercice → bouton « Recommencer l'exercice ». Reset le cursor
    /// au debut, ramene les controles standards (Quitter/Passer) et le highlight du 1er
    /// caractere a taper.
    /// </summary>
    private void RetryCurrentStep()
    {
        _page = TutorialPage.Exercise;
        _cursorPosition = 0;
        _currentCharError = false;
        ClearPendingPhysicalText();
        _mapper.RequestCapsLockOff(); // forcer Verr.Maj. off au reset (re-incite a l'activation si KeepCapsHighlight)
        UpdateHighlight();
        UpdateControlVisibility();
        RepositionControls();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
        TakeFocus();
    }

    /// <summary>
    /// Page de choix fin d'exercice → bouton « Exercice suivant » / « Terminer les exercices ».
    /// Avance au prochain exercice ; au dernier, AdvanceToNextStep mettra _page = TutorialPage.Final et
    /// affichera la page « Bravo ! » finale.
    /// </summary>
    private void ContinueAfterChoice()
    {
        AdvanceToNextStep();
    }

    // ═══════════════════════════════════════════════════════════════
    // Highlight du prochain caractère
    // ═══════════════════════════════════════════════════════════════
    private void ClearHighlight()
    {
        _guidance = new KeyboardRenderState();
    }

    private MethodData? CreateDeadKeyMethod(string deadKey, string key, string layer)
    {
        if (!Index.DeadKeyActivations.TryGetValue(deadKey, out var dkAct)) return null;
        return new MethodData
        {
            Type = "deadkey",
            DeadKey = deadKey,
            Key = key,
            Layer = layer,
            DkActivationKey = dkAct.Key,
            DkActivationLayer = dkAct.Layer,
        };
    }

    private MethodData? GetLanguageExerciseMethod(string character)
    {
        return character switch
        {
            "\u00e3" => CreateDeadKeyMethod("dk_tilde", "KeyQ", "Base"),  // ã
            "\u00c3" => CreateDeadKeyMethod("dk_tilde", "KeyQ", "Shift"), // Ã
            "\u00f8" => CreateDeadKeyMethod("dk_stroke", "KeyO", "Base"), // ø
            "\u00d8" => CreateDeadKeyMethod("dk_stroke", "KeyO", "Shift"), // Ø
            "\u0142" => CreateDeadKeyMethod("dk_stroke", "KeyL", "Base"), // ł
            "\u0141" => CreateDeadKeyMethod("dk_stroke", "KeyL", "Shift"), // Ł
            _ => null,
        };
    }

    private void UpdateHighlight()
    {
        ClearHighlight();
        if (_page == TutorialPage.Final) return;
        if (_currentStep >= Steps.Length) return;

        var target = Steps[_currentStep].Target;
        if (_cursorPosition >= target.Length) return;

        var nextChar = target[_cursorPosition].ToString();
        if (!Index.ByCharacter.TryGetValue(nextChar, out var indexEntry) || indexEntry.Preferred is not { } method) return;

        // Si l'exercice demande de garder Verr.Maj activée et qu'une variante Caps existe
        // pour ce caractère, l'utiliser (= ne pas demander Maj redondant à l'utilisateur).
        if (Steps[_currentStep].KeepCapsHighlight
            && indexEntry.Caps is { } capsAlt)
        {
            method = capsAlt;
        }

        if (_currentStep == Steps.Length - 1
            && GetLanguageExerciseMethod(nextChar) is { } languageMethod)
        {
            method = languageMethod;
        }

        LessonHintProvider.Guide(_guidance, method, nextChar, _mapper.ActiveDeadKey, _mapper.CapsLockActive,
            GuideOptions.Tutorial(keepCapsLock: Steps[_currentStep].KeepCapsHighlight));
    }

    // ═══════════════════════════════════════════════════════════════
    // Rendu — WM_PAINT
    // ═══════════════════════════════════════════════════════════════
    private void OnPaint(IntPtr hWnd)
    {
        var hdcPaint = Win32.BeginPaint(hWnd, out var ps);
        Win32.GetClientRect(hWnd, out var clientRect);
        int cw = clientRect.right;
        int ch = clientRect.bottom;
        if (cw <= 0 || ch <= 0) { Win32.EndPaint(hWnd, ref ps); return; }

        // Double buffering
        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        var hdc = Win32.CreateCompatibleDC(hdcScreen);
        var hBmp = Win32.CreateCompatibleBitmap(hdcScreen, cw, ch);
        var hBmpOld = Win32.SelectObject(hdc, hBmp);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);

        try
        {
            // Fond zones supérieures (clair)
            int kbTop = S(BASE_HEADER_H + BASE_INSTRUCTION_H + BASE_TARGET_H);
            var topRect = new Win32.RECT { left = 0, top = 0, right = cw, bottom = kbTop };
            Win32.FillRect(hdc, ref topRect, _hBgBrush);

            // Fond zone clavier (sombre)
            int footerTop = ch - S(BASE_FOOTER_H);
            var kbRect = new Win32.RECT { left = 0, top = kbTop, right = cw, bottom = footerTop };
            Win32.FillRect(hdc, ref kbRect, _hKbBgBrush);

            // Fond footer (clair)
            var footerRect = new Win32.RECT { left = 0, top = footerTop, right = cw, bottom = ch };
            Win32.FillRect(hdc, ref footerRect, _hBgBrush);

            Win32.SetBkMode(hdc, Win32.TRANSPARENT);

            if (_page == TutorialPage.Final)
                PaintFinalScreen(hdc, cw, kbTop);
            else if (_page == TutorialPage.Choice)
                PaintChoiceScreen(hdc, cw, kbTop);
            else
                PaintExercise(hdc, cw, kbTop);

            // Clavier virtuel
            PaintKeyboard(hdc, cw, kbTop, footerTop);

            // (Légende clavier retirée — peu utile à un seul item ; les tooltips sur survol
            // remplacent désormais l'information par caractère.)

            // Blit
            Win32.BitBlt(hdcPaint, 0, 0, cw, ch, hdc, 0, 0, 0x00CC0020);
        }
        finally
        {
            Win32.SelectObject(hdc, hBmpOld);
            Win32.DeleteObject(hBmp);
            Win32.DeleteDC(hdc);
        }

        Win32.EndPaint(hWnd, ref ps);
    }

    private void PaintExercise(IntPtr hdc, int cw, int kbTop)
    {
        int margin = S(BASE_MARGIN);
        int y = S(8);

        // Header — dots de progression dessines via GDI Ellipse (taille uniforme, couleurs
        // distinctes orange/vert/gris). Avant on utilisait des chars Unicode ● / ○
        // via DrawTextW mais les glyphes ne rendaient pas a la meme taille dans Segoe UI.
        int dotDiam = S(12);
        int dotSpacing = S(8);
        int dotsTop = y + S(4);
        for (int i = 0; i < Steps.Length; i++)
        {
            uint dotColor = i < _currentStep ? CLR_PROGRESS_DONE
                          : i == _currentStep ? CLR_PROGRESS_CURRENT
                          : CLR_PROGRESS_TODO;
            int dotX = margin + i * (dotDiam + dotSpacing);
            var hBrush = Win32.CreateSolidBrush(dotColor);
            var hPen = Win32.CreatePen(0, 1, dotColor);
            var hOldBrush = Win32.SelectObject(hdc, hBrush);
            var hOldPen = Win32.SelectObject(hdc, hPen);
            Win32.Ellipse(hdc, dotX, dotsTop, dotX + dotDiam, dotsTop + dotDiam);
            Win32.SelectObject(hdc, hOldBrush);
            Win32.SelectObject(hdc, hOldPen);
            Win32.DeleteObject(hBrush);
            Win32.DeleteObject(hPen);
        }

        var hOldFont = Win32.SelectObject(hdc, _hFontTitle);
        y += S(22);
        Win32.SelectObject(hdc, _hFontTitle);
        Win32.SetTextColor(hdc, CLR_HEADER_TITLE);
        string title = L.Learning_ExerciseHeader(_currentStep + 1, Steps.Length, Steps[_currentStep].Title);
        var titleRect = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = y + S(32) };
        Win32.DrawTextW(hdc, title, title.Length, ref titleRect, 0);

        // Suffixe \u00ab (Bonus) \u00bb \u00e0 la suite du titre, en dor\u00e9, si l'exercice est facultatif.
        if (Steps[_currentStep].Skippable)
        {
            int titleW = GdiHelpers.MeasureSingleLineWidth(hdc, _hFontTitle, title);
            string bonusSuffix = L.Learning_BonusSuffix;
            var bonusRect = new Win32.RECT { left = margin + titleW, top = y, right = cw - margin, bottom = y + S(32) };
            Win32.SetTextColor(hdc, CLR_BONUS_TEXT);
            Win32.DrawTextW(hdc, bonusSuffix, bonusSuffix.Length, ref bonusRect, 0);
        }

        // Instruction
        y = S(BASE_HEADER_H) + S(4);
        Win32.SelectObject(hdc, _hFontInstruction);
        Win32.SetTextColor(hdc, CLR_INSTRUCTION);
        string instr = Steps[_currentStep].Instruction;
        var instrRect = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = y + S(BASE_INSTRUCTION_H) };
        Win32.DrawTextW(hdc, instr, instr.Length, ref instrRect, 0);

        // Texte cible — caractère par caractère
        y = S(BASE_HEADER_H + BASE_INSTRUCTION_H) + S(4);
        var target = Steps[_currentStep].Target;
        Win32.SelectObject(hdc, _hFontTarget);

        int textX = margin;
        for (int i = 0; i < target.Length; i++)
        {
            string ch = target[i].ToString();
            // Mesurer la largeur du caractère
            var measureRect = new Win32.RECT { left = 0, top = 0, right = 10000, bottom = 1000 };
            Win32.DrawTextW(hdc, ch, 1, ref measureRect, Win32.DT_CALCRECT);
            int charW = measureRect.right;

            if (i < _cursorPosition)
            {
                // Tapé correctement → vert
                Win32.SetTextColor(hdc, CLR_TARGET_CORRECT);
            }
            else if (i == _cursorPosition)
            {
                // Caractère en cours
                Win32.SetTextColor(hdc, _currentCharError ? CLR_TARGET_ERROR : CLR_TARGET_CURRENT);
                // Souligné — décalé sous la base des lettres sans trop s'éloigner.
                int underY = y + S(28);
                var hPen = Win32.CreatePen(0, S(2), _currentCharError ? CLR_TARGET_ERROR : CLR_TARGET_CURRENT);
                var hOldPen = Win32.SelectObject(hdc, hPen);
                Win32.MoveToEx(hdc, textX, underY, IntPtr.Zero);
                Win32.LineTo(hdc, textX + charW, underY);
                Win32.SelectObject(hdc, hOldPen);
                Win32.DeleteObject(hPen);
            }
            else
            {
                // Pas encore tapé → gris
                Win32.SetTextColor(hdc, CLR_TARGET_PENDING);
            }

            var charRect = new Win32.RECT { left = textX, top = y, right = textX + charW + S(2), bottom = y + S(30) };
            Win32.DrawTextW(hdc, ch, 1, ref charRect, 0);
            textX += charW + S(1);
        }

        Win32.SelectObject(hdc, hOldFont);

        // Bloc droit : Verrouillage Majuscule + (eventuellement) Touche morte, juste au-dessus
        // du clavier, aligne a droite. Remplace l'ancienne barre d'état sous le target text.
        int kbTopForStatus = S(BASE_HEADER_H + BASE_INSTRUCTION_H + BASE_TARGET_H);
        PaintRightStatusBlock(hdc, cw, kbTopForStatus);
    }

    /// <summary>
    /// Dessine le bloc droit avec le statut Verrouillage Majuscule (toujours affiche) et
    /// la touche morte active (uniquement si une est active). Les 2 lignes sont collees au
    /// clavier avec une petite marge ; la ligne Verrouillage Majuscule est au-dessus de la
    /// ligne Touche morte. ACTIVE en vert quand le Caps Lock est actif.
    /// </summary>
    private void PaintRightStatusBlock(IntPtr hdc, int cw, int kbTop)
    {
        int margin = S(BASE_MARGIN);
        int marginAboveKb = S(8);
        int statusH = S(20);
        int gap = S(4);
        int rightX = cw - margin;

        var hOldFont = Win32.SelectObject(hdc, _hFontStatus);

        var activeDk = _mapper.ActiveDeadKey;
        bool dkLineShown = !string.IsNullOrEmpty(activeDk);
        int bottomLineY = kbTop - marginAboveKb - statusH;
        int topLineY = dkLineShown ? bottomLineY - gap - statusH : bottomLineY;

        // Ligne Verrouillage Majuscule (toujours)
        bool capsActive = _mapper.CapsLockActive;
        string capsSuffix = capsActive ? L.Learning_CapsLockOn : L.Learning_CapsLockOff;
        uint capsSuffixColor = capsActive ? CLR_TARGET_CORRECT : CLR_STATUS;
        DrawTwoColorRightAligned(hdc, _hFontStatus,
            L.Learning_CapsLockLabel, CLR_STATUS,
            capsSuffix, capsSuffixColor,
            rightX, topLineY);

        // Ligne Touche morte (si active)
        if (dkLineShown)
        {
            var dkNamesDictStatus = L.IsEnglish ? L.DeadKeyNamesEn : VirtualKeyboard._deadKeyNamesFr;
            dkNamesDictStatus.TryGetValue(activeDk!, out var dkName);
            dkName ??= activeDk!;
            string symbol = GetDeadKeySymbolNonCombining(activeDk!);
            string dkText = L.Learning_ActiveDeadKey(dkName, symbol);
            DrawSingleColorRightAligned(hdc, _hFontStatus, dkText, CLR_STATUS, rightX, bottomLineY);
        }

        Win32.SelectObject(hdc, hOldFont);
    }

    /// <summary>Symbole isole d'une touche morte uniquement s'il est non-combinant (sinon vide).</summary>
    private string GetDeadKeySymbolNonCombining(string dkName)
    {
        if (_layout.DeadKeys.TryGetValue(dkName, out var dk))
        {
            var iso = dk.GetIsolated();
            if (!string.IsNullOrWhiteSpace(iso) && iso.Length == 1 && !KeyboardRenderer.IsCombiningMark(iso[0]))
                return iso;
        }
        string fallback = TrayApplication.GetDeadKeySymbol(dkName);
        if (!string.IsNullOrEmpty(fallback) && fallback.Length == 1 && !KeyboardRenderer.IsCombiningMark(fallback[0]))
            return fallback;
        return "";
    }

    private void DrawSingleColorRightAligned(IntPtr hdc, IntPtr hFont, string text, uint color, int rightX, int y)
    {
        int width = GdiHelpers.MeasureSingleLineWidth(hdc, hFont, text);
        int left = rightX - width;
        Win32.SetTextColor(hdc, color);
        var rc = new Win32.RECT { left = left, top = y, right = rightX, bottom = y + 100 };
        Win32.DrawTextW(hdc, text, text.Length, ref rc,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
    }

    private void DrawTwoColorRightAligned(IntPtr hdc, IntPtr hFont,
        string prefix, uint prefixColor,
        string suffix, uint suffixColor,
        int rightX, int y)
    {
        int prefixWidth = GdiHelpers.MeasureSingleLineWidth(hdc, hFont, prefix);
        int suffixWidth = GdiHelpers.MeasureSingleLineWidth(hdc, hFont, suffix);
        int leftX = rightX - prefixWidth - suffixWidth;
        Win32.SetTextColor(hdc, prefixColor);
        var rcP = new Win32.RECT { left = leftX, top = y, right = leftX + prefixWidth, bottom = y + 100 };
        Win32.DrawTextW(hdc, prefix, prefix.Length, ref rcP,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        Win32.SetTextColor(hdc, suffixColor);
        var rcS = new Win32.RECT { left = leftX + prefixWidth, top = y, right = rightX, bottom = y + 100 };
        Win32.DrawTextW(hdc, suffix, suffix.Length, ref rcS,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
    }

    /// <summary>
    /// Page de choix affichee apres chaque exercice reussi.
    /// 1er succes de l'exercice : \u00ab \u2713 Bravo ! \u00bb (gros vert) + sous-titre + 2 boutons.
    /// 2e+ succes (apres Recommencer) : seulement les 2 boutons, pas de titre/sous-titre.
    /// Les boutons eux-memes sont dessines par Windows via leurs HWND.
    /// </summary>
    private void PaintChoiceScreen(IntPtr hdc, int cw, int kbTop)
    {
        if (_currentStepSuccessCount <= 1)
        {
            // Block \u00ab \u2713 Bravo ! \u00bb + gap + boutons : centre verticalement dans la zone superieure.
            // Cette geometrie doit matcher RepositionControls (page de choix + 1er succes).
            int margin = S(BASE_MARGIN);
            int titleH = S(40);
            int gapTitleButtons = S(14);
            int choiceBtnH = S(36);
            int blockH = titleH + gapTitleButtons + choiceBtnH;
            int blockTop = (kbTop - blockH) / 2;
            int titleTop = blockTop;
            int titleBottom = titleTop + titleH;

            // Titre \u00ab \u2713 Bravo ! \u00bb centre, grande police, vert valide
            var hOldFont = Win32.SelectObject(hdc, _hFontTransition);
            Win32.SetTextColor(hdc, CLR_TRANSITION);
            string title = L.Learning_BravoShort;
            var titleRect = new Win32.RECT { left = margin, top = titleTop, right = cw - margin, bottom = titleBottom };
            Win32.DrawTextW(hdc, title, title.Length, ref titleRect,
                Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE);
            Win32.SelectObject(hdc, hOldFont);
        }
        // n-ieme succes : pas de titre, juste les boutons (positionnes par RepositionControls
        // au centre vertical de la zone superieure).
    }

    private void PaintFinalScreen(IntPtr hdc, int cw, int kbTop)
    {
        int margin = S(BASE_MARGIN);
        int titleH = S(48);
        int subtitleH = S(28);
        int gap = S(8);
        int blockH = titleH + gap + subtitleH;
        int blockTop = (kbTop - blockH) / 2;

        // Titre \u00ab Bravo ! \u00bb centr\u00e9, grande police, orange brand
        var hOldFont = Win32.SelectObject(hdc, _hFontTransition);
        Win32.SetTextColor(hdc, CLR_PROGRESS_DONE);
        string title = L.Learning_FinalTitle;
        var titleRect = new Win32.RECT { left = margin, top = blockTop, right = cw - margin, bottom = blockTop + titleH };
        Win32.DrawTextW(hdc, title, title.Length, ref titleRect,
            Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE);

        // Sous-titre \u2014 police plus petite, fonc\u00e9e
        Win32.SelectObject(hdc, _hFontTitle);
        Win32.SetTextColor(hdc, CLR_HEADER_TITLE);
        string subtitle = L.Learning_FinalSubtitle;
        var subtitleRect = new Win32.RECT { left = margin, top = blockTop + titleH + gap, right = cw - margin, bottom = blockTop + titleH + gap + subtitleH };
        Win32.DrawTextW(hdc, subtitle, subtitle.Length, ref subtitleRect,
            Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE);

        Win32.SelectObject(hdc, hOldFont);
    }

    // ═══════════════════════════════════════════════════════════════
    // Rendu du clavier virtuel
    // ═══════════════════════════════════════════════════════════════
    private void PaintKeyboard(IntPtr hdc, int cw, int kbTop, int kbBottom)
    {
        int kbW = cw;
        int kbH = kbBottom - kbTop;
        if (kbW <= 0 || kbH <= 0) return;

        var geo = VirtualKeyboard.GetKeyboardGeometry(kbW, kbH);
        var placement = new KeyboardPlacement(geo.OffsetX, kbTop + geo.OffsetY, geo.Scale);
        _keyboardPlacement = placement;
        KeyboardRenderer.DrawKeys(hdc, placement, _layout, KeyboardRenderProfile.Onboarding, BuildKeyboardState(),
            new KeyboardFonts(_hFontCharMain, _hFontCharDeadKey, _hFontCharSmall, _hFontCharTiny, _hFontCtx, _hFontBadge));

        // Overlay « pause » quand la fenêtre n'a plus le focus clavier (option A).
        // Affiche apres debounce 250ms (TIMER_FOCUS_LOST_CONFIRM) pour eviter d'apparaitre
        // sur les blinks WM_KILLFOCUS / WM_SETFOCUS rapides causes par MoveWindow / repaint.
        if (_focusLostConfirmed)
            PaintFocusLostOverlay(hdc, cw, kbTop, kbBottom);
    }

    /// <summary>
    /// Ce que le clavier montre : modificateurs tenus (Ctrl et Alt ne s'allument pas sous
    /// AltGr, qui vaut Ctrl+Alt pour Windows), Verr. Maj., touche morte armée, touche enfoncée,
    /// surlignage de l'exercice et, à l'exercice 6, les aides des mots étrangers.
    /// </summary>
    private KeyboardRenderState BuildKeyboardState()
    {
        bool altGr = _mapper.AltGrDown;
        var state = new KeyboardRenderState
        {
            Shift = _mapper.ShiftDown,
            AltGr = altGr,
            Ctrl = _mapper.CtrlDown && !altGr,
            Alt = _mapper.AltDown && !altGr,
            CapsLock = _mapper.CapsLockActive,
            ActiveDeadKey = _mapper.ActiveDeadKey,
            PressedScancode = _pressedScancode,
            ShowInvisibleMarkers = false,
            UiScale = _dpiScale,
            HighlightKind = _guidance.HighlightKind,
            KeepCapsLockHighlight = _currentStep < Steps.Length && Steps[_currentStep].KeepCapsHighlight,
        };
        state.HighlightedScancodes.UnionWith(_guidance.HighlightedScancodes);
        state.HighlightedLabels.UnionWith(_guidance.HighlightedLabels);
        state.HighlightedContextIds.UnionWith(_guidance.HighlightedContextIds);
        if (_currentStep == Steps.Length - 1)
            state.LessonVisibleCharacters.UnionWith(LanguageExerciseCharacters);
        return state;
    }

    /// <summary>
    /// Assombrit la zone clavier via AlphaBlend (alpha 160 sur fond noir) et affiche
    /// « Cliquez pour reprendre » centré. Le clic dans la fenêtre redonne le focus
    /// (Windows envoie WM_SETFOCUS) et l'overlay disparaît.
    /// </summary>
    private void PaintFocusLostOverlay(IntPtr hdc, int cw, int top, int bottom)
    {
        // Bitmap source 1×1 noir pour AlphaBlend (msimg32.dll).
        var hdcMem = Win32.CreateCompatibleDC(hdc);
        var hBmp = Win32.CreateCompatibleBitmap(hdc, 1, 1);
        var hOldBmp = Win32.SelectObject(hdcMem, hBmp);
        var oneRect = new Win32.RECT { left = 0, top = 0, right = 1, bottom = 1 };
        var hBlackBrush = Win32.CreateSolidBrush(0x00000000u);
        Win32.FillRect(hdcMem, ref oneRect, hBlackBrush);
        Win32.DeleteObject(hBlackBrush);

        var blend = new Win32.BLENDFUNCTION
        {
            BlendOp = Win32.AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = 170,  // ~67 % opacité
            AlphaFormat = 0
        };
        Win32.AlphaBlend(hdc, 0, top, cw, bottom - top, hdcMem, 0, 0, 1, 1, blend);

        Win32.SelectObject(hdcMem, hOldBmp);
        Win32.DeleteObject(hBmp);
        Win32.DeleteDC(hdcMem);

        // Texte « Cliquez pour reprendre » centré
        var hOldFont = Win32.SelectObject(hdc, _hFontTitle);
        Win32.SetTextColor(hdc, 0x00FFFFFFu);
        Win32.SetBkMode(hdc, Win32.TRANSPARENT);
        var rc = new Win32.RECT { left = 0, top = top, right = cw, bottom = bottom };
        string msg = L.Learning_ClickToResume;
        Win32.DrawTextW(hdc, msg, msg.Length, ref rc,
            Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE);
        Win32.SelectObject(hdc, hOldFont);
    }

    /// <summary>
    /// Subclass du bouton « Quitter les exercices » pour tracker hover (WM_MOUSEMOVE
    /// + WM_MOUSELEAVE via TrackMouseEvent). Met à jour _quitHovered et invalide le
    /// bouton pour redraw via WM_DRAWITEM (DrawQuitButton).
    /// </summary>
    private IntPtr QuitButtonSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (HandleHeaderButtonKey(hWnd, msg, wParam, lParam)) return IntPtr.Zero;
        switch (msg)
        {
            case Win32.WM_MOUSEMOVE:
                if (!_quitHovered)
                {
                    _quitHovered = true;
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, false);
                    var tme = new Win32.TRACKMOUSEEVENT
                    {
                        cbSize = (uint)Marshal.SizeOf<Win32.TRACKMOUSEEVENT>(),
                        dwFlags = Win32.TME_LEAVE,
                        hwndTrack = hWnd,
                        dwHoverTime = 0
                    };
                    Win32.TrackMouseEvent(ref tme);
                }
                break;
            case Win32.WM_MOUSELEAVE:
                _quitHovered = false;
                Win32.InvalidateRect(hWnd, IntPtr.Zero, false);
                break;
        }
        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Dessine un bouton owner-draw avec effet hover « rouge clair ». Utilisé pour
    /// les boutons « Quitter les exercices » et « Passer cet exercice ».
    /// Hover : fond rouge sombre, bordure rouge clair, texte blanc.
    /// Normal : fond gris foncé, texte gris CLR_BTN_QUIT_TEXT, bordure CLR_KEY_BORDER.
    /// </summary>
    private void DrawHoverButton(in Win32.DRAWITEMSTRUCT dis, bool hovered, string label)
    {
        var rc = dis.rcItem;
        uint bgColor = hovered ? 0x003838C0u : 0x002A2A2Au;       // BGR : rouge sombre / gris
        var hBrush = Win32.CreateSolidBrush(bgColor);
        Win32.FillRect(dis.hDC, ref rc, hBrush);
        Win32.DeleteObject(hBrush);

        uint borderColor = hovered ? 0x005050E0u : CLR_KEY_BORDER;
        var hPen = Win32.CreatePen(0, 1, borderColor);
        var hOldPen = Win32.SelectObject(dis.hDC, hPen);
        var hOldBrush = Win32.SelectObject(dis.hDC, Win32.GetStockObject(Win32.NULL_BRUSH));
        Win32.RoundRect(dis.hDC, rc.left, rc.top, rc.right - 1, rc.bottom - 1, 6, 6);
        Win32.SelectObject(dis.hDC, hOldPen);
        Win32.SelectObject(dis.hDC, hOldBrush);
        Win32.DeleteObject(hPen);

        Win32.SelectObject(dis.hDC, _hFontButton);
        Win32.SetBkMode(dis.hDC, Win32.TRANSPARENT);
        Win32.SetTextColor(dis.hDC, hovered ? 0x00FFFFFFu : CLR_BTN_QUIT_TEXT);
        Win32.DrawTextW(dis.hDC, label, label.Length, ref rc,
            Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE);

        // K3 (accessibilité 1.3.0) : un bouton owner-draw ne dessine que ce qu'on lui dit, et
        // le focus clavier ne se voyait pas. Rectangle système, en retrait du cadre arrondi.
        if ((dis.itemState & Win32.ODS_FOCUS) != 0)
        {
            int inset = S(3);
            var focus = new Win32.RECT
            {
                left = dis.rcItem.left + inset,
                top = dis.rcItem.top + inset,
                right = dis.rcItem.right - inset,
                bottom = dis.rcItem.bottom - inset
            };
            Win32.DrawFocusRect(dis.hDC, ref focus);
        }
    }

    /// <summary>Subclass du bouton « Passer cet exercice » — même comportement que QuitButtonSubclassProc.</summary>
    private IntPtr SkipButtonSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (HandleHeaderButtonKey(hWnd, msg, wParam, lParam)) return IntPtr.Zero;
        switch (msg)
        {
            case Win32.WM_MOUSEMOVE:
                if (!_skipHovered)
                {
                    _skipHovered = true;
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, false);
                    var tme = new Win32.TRACKMOUSEEVENT
                    {
                        cbSize = (uint)Marshal.SizeOf<Win32.TRACKMOUSEEVENT>(),
                        dwFlags = Win32.TME_LEAVE,
                        hwndTrack = hWnd,
                        dwHoverTime = 0
                    };
                    Win32.TrackMouseEvent(ref tme);
                }
                break;
            case Win32.WM_MOUSELEAVE:
                _skipHovered = false;
                Win32.InvalidateRect(hWnd, IntPtr.Zero, false);
                break;
        }
        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    // ═══════════════════════════════════════════════════════════════
    // Dispose
    // ═══════════════════════════════════════════════════════════════
    private bool _disposed;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Referme la plage ouverte par le ctor : la frappe redevient comptabilisée.
        UsageStats.EndExcludedTyping();
        LearningSessionTracker.Closed(this);

        _mapper.StateChanged -= OnStateChanged;
        _hook.RawKeyDown -= OnRawKeyDown;
        if (_onAppLanguageChanged != null)
            ConfigManager.AppLanguageChanged -= _onAppLanguageChanged;

        if (_quitSubclassProc != null && _hWndBtnQuit != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndBtnQuit, _quitSubclassProc, (UIntPtr)1);
        if (_skipSubclassProc != null && _hWndBtnSkip != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndBtnSkip, _skipSubclassProc, (UIntPtr)2);

        if (_hTooltip != IntPtr.Zero)
        {
            Win32.DestroyWindow(_hTooltip);
            _hTooltip = IntPtr.Zero;
        }

        // Detruire la fenetre AVANT de liberer fontes/brushes : les WM_DESTROY etc. doivent
        // encore pouvoir router vers _wndProcDelegate.
        if (_hWnd != IntPtr.Zero)
        {
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }

        // Desenregistrer la classe Win32 maintenant que plus aucune fenetre ne l'utilise
        // (voir NativeWindow.RegisterClass).
        NativeWindow.UnregisterClass(WND_CLASS_NAME);

        DestroyFonts();
        Win32.DeleteObject(_hBgBrush);
        Win32.DeleteObject(_hKbBgBrush);
    }
}
