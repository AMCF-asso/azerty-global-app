using System.Runtime.InteropServices;
using System.Text;
namespace AZERTYGlobal;

/// <summary>
/// Fenêtre Paramètres. Refonte 1.3.0 (maquette « onglets en tête », validée par Antoine le
/// 07/10) : papier, polices AG, bande d'onglets Win32 dont l'onglet ouvert porte le trait
/// épais bleu, raccourcis écrits en geste. Les contrôles restent natifs (onglets, cases,
/// boutons radio, boutons, liste) : ils gardent rôle, état, focus et clavier ; seul leur
/// dessin change, dans <c>SettingsWindow.Paper.cs</c>.
/// </summary>
sealed partial class SettingsWindow : IDisposable
{
    private const uint WS_GROUP = 0x00020000;
    private const uint EM_SETREADONLY = 0x00CF;

    private const int VK_TAB = 0x09;
    private const int VK_ESCAPE = 0x1B;
    // Lot « clarté » (07/10) : l'invite dit quelles touches sont acceptées et qu'Échap annule.
    private static string ShortcutCaptureHint => L.SettingsClarity_CaptureHint;

    private const int IDC_TAB_STRIP = 3120;
    private const int IDC_VALIDATION = 3121;
    private const int IDC_EDIT_KEYBOARD = 3101;
    private const int IDC_EDIT_SEARCH = 3102;
    private const int IDC_CHK_AUTOSTART = 3103;
    private const int IDC_CHK_NOTIFICATIONS = 3104;
    private const int IDC_CHK_ONBOARDING = 3105;
    private const int IDC_LINK_RESET = 3107;
    private const int IDC_RESET_VIRTUAL_KEYBOARD_WINDOW = 3108;
    private const int IDC_RESET_LESSONS_WINDOW = 3109;
    private const int IDC_RADIO_LANG_FR = 3110;
    private const int IDC_RADIO_LANG_EN = 3111;
    private const int IDC_CHK_TRAINING = 3118; // opt-in Défi du jour (v1.2.0)
    // Section « Apps suspendues » (v1.2.0)
    private const int IDC_LIST_COMPAT = 3112;
    private const int IDC_BTN_COMPAT_ADD = 3113;
    private const int IDC_BTN_COMPAT_REMOVE = 3114;
    private const int IDC_RADIO_COMPAT_AUTO = 3115;
    private const int IDC_RADIO_COMPAT_FORCEON = 3116;
    private const int IDC_RADIO_COMPAT_FORCEOFF = 3117;

    // Listbox Win32 (section Apps suspendues), dessinée par la fenêtre : LBS_HASSTRINGS garde
    // le texte de chaque ligne pour les lecteurs d'écran.
    private const uint LBS_NOTIFY = 0x0001;
    private const uint LBS_OWNERDRAWFIXED = 0x0010;
    private const uint LBS_HASSTRINGS = 0x0040;
    private const uint LBS_NOINTEGRALHEIGHT = 0x0100;
    private const uint LB_ADDSTRING = 0x0180;
    private const uint LB_RESETCONTENT = 0x0184;
    private const uint LB_SETCURSEL = 0x0186;
    private const uint LB_GETCURSEL = 0x0188;
    private const uint LB_GETCOUNT = 0x018B;
    private const uint LB_SETITEMHEIGHT = 0x01A0;
    private const int LBN_SELCHANGE = 1;
    private const uint WS_VSCROLL = 0x00200000;
    private const uint SS_OWNERDRAW = 0x000D;

    // 240 → 300 le 2026-07-16, 300 → 340 le 2026-09-28, 340 → 380 le 2026-10-07 (refonte
    // 1.3.0) : la largeur de la maquette « onglets en tête ».
    private const int BASE_WIN_W = 380;
    private const int BASE_WIN_H = 480;

    // Jetons de la maquette (PaperTheme), un rôle par constante.
    private const uint CLR_BG = PaperTheme.Paper;
    private const uint CLR_SURFACE = PaperTheme.Surface;
    private const uint CLR_TEXT = PaperTheme.Ink;
    private const uint CLR_MUTED = PaperTheme.TextSecondary;
    private const uint CLR_BORDER = PaperTheme.Border;
    private const uint CLR_ACTION = PaperTheme.Action;
    private const uint CLR_PALE = PaperTheme.Pale;
    private const uint CLR_INVALID = PaperTheme.Error;

    /// <summary>Où s'écrit le message du dernier geste. Chaque nature a sa place dans la
    /// maquette : l'invite de capture et un refus de raccourci sous les raccourcis, un refus de
    /// compatibilité sous les boutons radio, une confirmation dans le pied.</summary>
    private enum MessagePlace { None, Shortcuts, Compat, Footer }

    private struct LayoutInfo
    {
        public int Margin;
        public Win32.RECT TabStripRect;
        public Win32.RECT ShortcutsTitleRect;
        public Win32.RECT KeyboardRowRect;
        public Win32.RECT SearchRowRect;
        // La case dessinée, et le contrôle qui la porte avec son contour de focus.
        public Win32.RECT KeyboardBoxRect;
        public Win32.RECT SearchBoxRect;
        public Win32.RECT KeyboardEditRect;
        public Win32.RECT SearchEditRect;
        public MessagePlace MessagePlace;
        public Win32.RECT ValidationRect;
        public Win32.RECT ResetRect;
        public Win32.RECT PreferencesTitleRect;
        public Win32.RECT AutoStartRect;
        public Win32.RECT NotificationsRect;
        public Win32.RECT ManagedNotificationsRect;
        public Win32.RECT OnboardingRect;
        public Win32.RECT ManagedOnboardingRect;
        public Win32.RECT TrainingRect;
        public Win32.RECT LanguageTitleRect;
        public Win32.RECT LanguageFrRect;
        public Win32.RECT LanguageEnRect;
        public Win32.RECT ManagedLanguageRect;
        public Win32.RECT WindowsTitleRect;
        public Win32.RECT ResetVirtualKeyboardWindowRect;
        public Win32.RECT ResetLessonsWindowRect;
        public Win32.RECT CompatTitleRect;
        public Win32.RECT CompatListFrame;
        public Win32.RECT CompatListRect;
        public Win32.RECT CompatAddRect;
        public Win32.RECT CompatRemoveRect;
        public Win32.RECT CompatModeLabelRect;
        public Win32.RECT CompatAutoHintRect;
        public Win32.RECT CompatForceOnHintRect;
        public Win32.RECT CompatForceOffHintRect;
        public Win32.RECT CompatAutoRect;
        public Win32.RECT CompatForceOnRect;
        public Win32.RECT CompatForceOffRect;
        // Sous-titres des onglets « Général » et « Langue » (lot « clarté », 07/10).
        public Win32.RECT KeyboardHintRect;
        public Win32.RECT SearchHintRect;
        public Win32.RECT AutoStartHintRect;
        public Win32.RECT NotificationsHintRect;
        public Win32.RECT OnboardingHintRect;
        public Win32.RECT ResetVirtualKeyboardHintRect;
        public Win32.RECT ResetLessonsHintRect;
        // Hauteur totale du contenu, marge basse comprise, avant tout défilement.
        // C'est elle qui décide de la hauteur de la fenêtre : le modèle v2.0.0 veut
        // une fenêtre qui mesure son contenu, jamais une constante (audit du
        // 2026-08-28, § 17 — BASE_WIN_H était devenue fausse à chaque section ajoutée).
        public int ContentHeight;
    }

    /// <summary>Les trois onglets de la fenêtre. L'ordre est celui de la bande.</summary>
    private enum SettingsTab { General = 0, Applications = 1, LanguageMaintenance = 2 }

    // Onglet affiché. Volontairement NON persisté : la fenêtre rouvre toujours sur
    // « Général », qui porte ce qu'on vient changer neuf fois sur dix. Mémoriser le
    // dernier onglet ferait rouvrir sur « Langue » après un unique passage.
    private SettingsTab _activeTab = SettingsTab.General;
    private IntPtr _hWndTabStrip;
    private IntPtr _hWndLabelTabs;

    private IntPtr _hWnd;
    private IntPtr _hWndEditKeyboard;
    private IntPtr _hWndEditSearch;
    // N8 (accessibilité 1.3.0) : étiquettes jamais affichées qui nomment les deux champs de
    // raccourci et la liste des apps suspendues, dont les libellés visibles sont dessinés.
    private IntPtr _hWndLabelKeyboard;
    private IntPtr _hWndLabelSearch;
    private IntPtr _hWndLabelCompat;
    private IntPtr _hWndChkAutoStart;
    private IntPtr _hWndChkNotifications;
    private IntPtr _hWndChkOnboarding;
    private IntPtr _hWndChkTraining;
    private IntPtr _hWndResetVirtualKeyboardWindow;
    private IntPtr _hWndResetLessonsWindow;
    private IntPtr _hWndRadioLangFr;
    private IntPtr _hWndRadioLangEn;
    private IntPtr _hWndLinkReset;
    private IntPtr _hWndValidation;
    // Lignes « Géré par votre organisation » (lot C) : une par réglage sous politique.
    private IntPtr _hWndManagedNotifications;
    private IntPtr _hWndManagedOnboarding;
    private IntPtr _hWndManagedLanguage;

    // Politiques d'entreprise, lues une fois pour la vie du processus : ce qui est imposé
    // ne change pas tant que l'application tourne, la mise en page peut donc s'y fier.
    private readonly bool _managedNotifications =
        PolicyManager.IsManaged(PolicyManager.Current.Notifications);
    private readonly bool _managedOnboarding =
        PolicyManager.IsOnboardingManaged(PolicyManager.Current.ShowOnboarding);
    private readonly bool _managedLanguage =
        PolicyManager.IsLanguageManaged(PolicyManager.Current.Language);
    // Section « Apps suspendues » (v1.2.0)
    private IntPtr _hWndCompatList;
    private IntPtr _hWndCompatAdd;
    private IntPtr _hWndCompatRemove;
    private IntPtr _hWndRadioCompatAuto;
    private IntPtr _hWndRadioCompatForceOn;
    private IntPtr _hWndRadioCompatForceOff;
    // Noms de process affichés dans la listbox, dans l'ordre des index de la liste, et
    // leur mode (« forceOn » ou « forceOff »), que la ligne écrit à droite.
    private readonly List<string> _compatProcesses = new();
    private readonly List<string> _compatModes = new();

    private readonly Win32.WNDPROC _wndProcDelegate;
    private readonly Win32.SUBCLASSPROC _shortcutSubclassProc;
    private readonly Win32.SUBCLASSPROC _tabSubclassProc;
    private IntPtr _focusedShortcut;

    // Fond de la classe : Windows le détruit au désenregistrement (NativeWindow).
    private IntPtr _hBgBrush;
    // Fond blanc de la liste des applications (WM_CTLCOLORLISTBOX).
    private readonly IntPtr _hSurfaceBrush;

    // GDI+ : cercles des boutons radio et coche des cases, lissés.
    private IntPtr _gdipToken;

    private bool _visible;
    private bool _inputPaused;
    private bool _keyboardValid = true;
    private bool _searchValid = true;
    private uint _keyboardVk;
    private uint _searchVk;
    // Touche montrée par chaque case : celle en place, ou celle qu'un refus vient de
    // nommer (0 : rien de montré encore, la touche en place).
    private uint _keyboardShownVk;
    private uint _searchShownVk;
    private bool _showCaptureHint;
    private string _validationMessage = string.Empty;
    private bool _validationRefused;

    private float _dpiScale;
    private int S(int val) => (int)(val * _dpiScale);

    // Défilement vertical (Écart 4). _scrollY est la position courante, en pixels de
    // contenu ; _contentHeight la hauteur mesurée du contenu ; _viewportHeight la
    // hauteur de la zone cliente réellement obtenue après plafonnement à la zone de
    // travail. Quand le contenu tient, les trois restent cohérents et la barre de
    // défilement est masquée : la fenêtre se comporte exactement comme avant.
    private int _scrollY;
    private readonly SettingsScrollState _scrollInput = new();
    private int _contentHeight;
    private int _viewportHeight;
    // Barre réservée sur tous les onglets dès que l'un d'eux défile : affichée grisée
    // quand l'onglet courant tient, pour que la largeur de la fenêtre ne bouge pas (C4).
    private bool _reserveScrollBar;
    private const uint SIF_DISABLENOSCROLL = 0x0008;

    // Polices de la maquette (px à 96 DPI), portées à l'échelle courante. Les HFONT sont
    // partagées et gardées par AppFonts : ne jamais les détruire.
    private FontSpec FontTab => FontSpec.Of(AppFonts.Text, 15, TypeRamp.Semibold, _dpiScale);
    private FontSpec FontTabIdle => FontSpec.Of(AppFonts.Text, 15, TypeRamp.Regular, _dpiScale);
    private FontSpec FontVersion => FontSpec.Of(AppFonts.Text, 12, TypeRamp.Regular, _dpiScale);
    private FontSpec FontSectionTitle => FontSpec.Of(AppFonts.Text, 15, TypeRamp.Semibold, _dpiScale);
    private FontSpec FontBody => FontSpec.Of(AppFonts.Text, 15, TypeRamp.Regular, _dpiScale);
    private FontSpec FontSmall => FontSpec.Of(AppFonts.Text, 13, TypeRamp.Regular, _dpiScale);
    private FontSpec FontSmallStrong => FontSpec.Of(AppFonts.Text, 13, TypeRamp.Semibold, _dpiScale);
    private FontSpec FontButton => FontSpec.Of(AppFonts.Text, 14, TypeRamp.Regular, _dpiScale);
    private FontSpec FontListName => FontSpec.Of(AppFonts.Text, 14, TypeRamp.Regular, _dpiScale);
    private FontSpec FontListNameChosen => FontSpec.Of(AppFonts.Text, 14, TypeRamp.Semibold, _dpiScale);
    private FontSpec FontHeld => FontSpec.Of(AppFonts.Text, 12, TypeRamp.Semibold, _dpiScale);
    private FontSpec FontKey => FontSpec.Of(AppFonts.Mono, 15, TypeRamp.Semibold, _dpiScale);

    public bool IsVisible => _visible;
    public Action? ShortcutChanged;
    /// <summary>Déclenché après tout changement d'override de compatibilité depuis cette
    /// fenêtre — TrayApplication doit relancer ForegroundMonitor.Recompute().</summary>
    public Action? CompatibilityOverridesChanged;

    // Abonnement AppLanguageChanged (bascule initiée depuis le tray ou l'onboarding) —
    // désabonné dans Dispose (événement statique, sinon référence pendante).
    private readonly Action<string>? _onAppLanguageChanged;

    public SettingsWindow()
    {
        _wndProcDelegate = WndProc;
        _shortcutSubclassProc = ShortcutSubclassProc;
        _tabSubclassProc = TabStripSubclassProc;
        _hSurfaceBrush = Win32.CreateSolidBrush(CLR_SURFACE);

        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        int dpi = Win32.GetDeviceCaps(hdcScreen, 88);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        _dpiScale = dpi / 96f;

        var gdipInput = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        Win32.GdiplusStartup(out _gdipToken, ref gdipInput, IntPtr.Zero);
        LoadShortcutStateFromConfig();

        CreateMainWindow();
        CreateControls();
        ApplyFontsToControls();
        // Mesurer avant de positionner : la fenêtre prend la hauteur de son contenu et
        // arme le défilement si la zone de travail l'a plafonnée (Écart 4).
        FitWindowToContent();
        RepositionControls();

        // Bascule de langue initiée ailleurs (menu tray, fenêtre de bienvenue) pendant que
        // cette fenêtre existe : se rafraîchir. Événement statique → désabonné dans Dispose.
        _onAppLanguageChanged = _ => OnLanguageChanged();
        ConfigManager.AppLanguageChanged += _onAppLanguageChanged;

        if (NativeWindow.CorrectedScale(Win32.GetDpiForWindow(_hWnd), _dpiScale) is float windowScale)
        {
            _dpiScale = windowScale;
            // Mise en page seule : GetDpiForWindow ne lève pas (audit du 25/09, X-04).
            try
            {
                RecreateFonts();
                FitWindowToContent();
                RepositionControls();
            }
            catch { }
        }
    }

    /// <summary>Mesures de texte de la mise en page (audit du 25/09, F-12). Elles ne changent
    /// qu'avec l'échelle et avec la langue : GetLayout, appelé plusieurs fois par remesure,
    /// n'ouvre plus de DC pour les reprendre. Hauteurs de ligne (montant + jambage) et
    /// largeurs des libellés traduits.</summary>
    private struct LineMetrics
    {
        public int SectionTitle, Body, Small, Button, Version, Tab;
        public int ResetText, AddText, RemoveText, ResetKeyboardText, ResetLessonsText;
        public int KeyAdvance;
        public int TabStripLeft;
    }

    private LineMetrics? _metrics; // remis à zéro par RecreateFonts et RefreshLanguageTexts

    private LineMetrics Metrics => _metrics ??= MeasureLineMetrics();

    private LineMetrics MeasureLineMetrics()
    {
        IntPtr hdc = Win32.GetDC(_hWnd);
        try
        {
            int Line(FontSpec font)
            {
                var (ascent, descent) = PaperText.Metrics(hdc, font);
                return ascent + descent;
            }
            return new LineMetrics
            {
                SectionTitle = Line(FontSectionTitle),
                Body = Line(FontBody),
                Small = Line(FontSmall),
                Button = Line(FontButton),
                Version = Line(FontVersion),
                Tab = Line(FontTab),
                ResetText = PaperText.Width(hdc, L.Settings_LinkResetDefaults, FontButton),
                AddText = PaperText.Width(hdc, L.Settings_CompatAdd, FontButton),
                RemoveText = PaperText.Width(hdc, L.Settings_CompatRemove, FontButton),
                ResetKeyboardText = PaperText.Width(hdc, L.Settings_ResetVirtualKeyboard, FontButton),
                ResetLessonsText = PaperText.Width(hdc, L.Settings_ResetLessonsModule, FontButton),
                KeyAdvance = PaperText.Width(hdc, "0", FontKey),
                TabStripLeft = MeasureTabStripLeft(hdc),
            };
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }
    }

    /// <summary>Les polices n'ont rien à créer ni à détruire (AppFonts les garde) : changer
    /// d'échelle, c'est oublier les mesures et rendre aux contrôles les polices nouvelles.</summary>
    private void RecreateFonts()
    {
        _metrics = null;
        ApplyFontsToControls();
    }

    private void ApplyFontsToControls()
    {
        // Chaque contrôle de la table reçoit la police de l'échelle courante, bande
        // d'onglets comprise : elle mesure ses onglets avec.
        ApplyTabStripPadding();
        foreach (var control in _controls)
            if (control.Font != null)
                Win32.SendMessageW(control.Handle, Win32.WM_SETFONT, control.Font(), (IntPtr)1);
        if (_hWndCompatList != IntPtr.Zero)
            Win32.SendMessageW(_hWndCompatList, LB_SETITEMHEIGHT, IntPtr.Zero, (IntPtr)CompatRowHeight);
    }

    /// <summary>Hauteur d'une ligne de la liste des applications.</summary>
    private int CompatRowHeight => S(36);

    private void CreateMainWindow()
    {
        var hInstance = Win32.GetModuleHandleW(null);
        string className = ProductIdentity.WindowClass("Settings");
        // Audit du 25/09, X-01 : une classe refusée ne crée pas de fenêtre.
        if (!NativeWindow.RegisterClass(className, _wndProcDelegate))
            return;

        // WS_VSCROLL est présent dès la création pour que la barre existe quand le
        // contenu dépasse la zone de travail ; FitWindowToContent la masque aussitôt
        // lorsque tout tient, ce qui est le cas courant (Écart 4).
        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU | WS_VSCROLL;
        var (windowW, windowH) = NativeWindow.OuterSize(S(BASE_WIN_W), S(BASE_WIN_H), dwStyle);
        var (x, y) = NativeWindow.CenterIn(NativeWindow.WorkArea(), windowW, windowH);

        _hWnd = Win32.CreateWindowExW(0, className, L.Settings_WindowTitle,
            dwStyle, x, y, windowW, windowH,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        _hBgBrush = NativeWindow.ApplyClassBackground(_hWnd, CLR_BG);

        // AG130-40 : cette fenetre veut Tab, Maj+Tab et Entree entre ses controles.
        DialogNavigation.Register(_hWnd, EnsureFocusVisible);
        // Barre de titre claire, sur le papier.
        NativeWindow.ApplyFrame(_hWnd, dark: false);
    }

    /// <summary>
    /// N8 (accessibilité 1.3.0) : étiquette STATIC jamais affichée, à créer juste avant le
    /// contrôle qu'elle nomme. MSAA et UI Automation nomment un EDIT ou une LISTBOX par le
    /// STATIC qui le précède dans l'ordre Z, visible ou non (mesuré le 2026-09-23) : sans
    /// elle, ces contrôles, dont le libellé est dessiné en GDI, n'avaient aucun nom. Aucune
    /// interop COM, rien que NativeAOT puisse élaguer.
    /// </summary>
    private IntPtr CreateHiddenLabel(IntPtr hInstance, string text) =>
        Win32.CreateWindowExW(0, "STATIC", text, Win32.WS_CHILD,
            0, 0, 0, 0, _hWnd, IntPtr.Zero, hInstance, IntPtr.Zero);

    private void CreateControls()
    {
        var hInstance = Win32.GetModuleHandleW(null);

        // La bande d'onglets n'avait pas de nom accessible (suivi 1.3.1) : même étiquette N8.
        _hWndLabelTabs = CreateHiddenLabel(hInstance, L.SettingsPaper_TabsName);
        CreateTabStrip(hInstance);

        _hWndLabelKeyboard = CreateHiddenLabel(hInstance, L.Settings_ShortcutLabelKeyboard);
        _hWndEditKeyboard = Win32.CreateWindowExW(0, "EDIT",
            ConfigManager.GetShortcutDisplayName(_keyboardVk),
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.ES_AUTOHSCROLL | Win32.ES_CENTER | Win32.ES_UPPERCASE | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_EDIT_KEYBOARD, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndEditKeyboard, EM_SETREADONLY, (IntPtr)1, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndEditKeyboard, _shortcutSubclassProc, (UIntPtr)3, IntPtr.Zero);

        _hWndLabelSearch = CreateHiddenLabel(hInstance, L.Settings_ShortcutLabelSearch);
        _hWndEditSearch = Win32.CreateWindowExW(0, "EDIT",
            ConfigManager.GetShortcutDisplayName(_searchVk),
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.ES_AUTOHSCROLL | Win32.ES_CENTER | Win32.ES_UPPERCASE | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_EDIT_SEARCH, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndEditSearch, EM_SETREADONLY, (IntPtr)1, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndEditSearch, _shortcutSubclassProc, (UIntPtr)4, IntPtr.Zero);

        // Le message du dernier geste : un STATIC dont le texte reste lisible par les
        // lecteurs d'écran, dessiné par la fenêtre (SS_OWNERDRAW) à la place que lui donne
        // sa nature (invite, refus, confirmation dans le pied).
        _hWndValidation = Win32.CreateWindowExW(0, "STATIC", "",
            Win32.WS_CHILD | Win32.WS_VISIBLE | SS_OWNERDRAW,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_VALIDATION, hInstance, IntPtr.Zero);

        _hWndLinkReset = Win32.CreateWindowExW(0, "BUTTON", L.Settings_LinkResetDefaults,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LINK_RESET, hInstance, IntPtr.Zero);

        _hWndChkAutoStart = Win32.CreateWindowExW(0, "BUTTON", L.Settings_AutoStart,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_AUTOCHECKBOX | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_CHK_AUTOSTART, hInstance, IntPtr.Zero);
        RefreshAutoStartCheckbox();

        _hWndChkNotifications = Win32.CreateWindowExW(0, "BUTTON", L.Settings_Notifications,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_AUTOCHECKBOX | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_CHK_NOTIFICATIONS, hInstance, IntPtr.Zero);
        if (ConfigManager.NotificationsEnabled)
            Win32.SendMessageW(_hWndChkNotifications, Win32.BM_SETCHECK, (IntPtr)Win32.BST_CHECKED, IntPtr.Zero);

        _hWndChkOnboarding = Win32.CreateWindowExW(0, "BUTTON", L.Settings_OnboardingWindow,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_AUTOCHECKBOX | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_CHK_ONBOARDING, hInstance, IntPtr.Zero);
        if (ConfigManager.ShowOnboardingAtStartup)
            Win32.SendMessageW(_hWndChkOnboarding, Win32.BM_SETCHECK, (IntPtr)Win32.BST_CHECKED, IntPtr.Zero);

        // Opt-in Défi du jour (v1.2.0) — décoché par défaut, appliqué immédiatement au
        // clic (pas à la fermeture) : l'entrée du menu tray et le module des leçons
        // dépendent de cet état.
        _hWndChkTraining = Win32.CreateWindowExW(0, "BUTTON", L.Challenge_OptIn,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_AUTOCHECKBOX | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_CHK_TRAINING, hInstance, IntPtr.Zero);
        if (ConfigManager.TrainingEnabled)
            Win32.SendMessageW(_hWndChkTraining, Win32.BM_SETCHECK, (IntPtr)Win32.BST_CHECKED, IntPtr.Zero);

        // Réglages sous politique d'entreprise (lot C) : le contrôle reste en place, grisé,
        // et porte sous lui la ligne qui dit pourquoi. Le retirer se lirait comme une
        // fonctionnalité disparue plutôt que comme une décision de la structure.
        _hWndManagedNotifications = CreateManagedNotice(hInstance, _managedNotifications);
        _hWndManagedOnboarding = CreateManagedNotice(hInstance, _managedOnboarding);
        if (_managedNotifications)
            Win32.EnableWindow(_hWndChkNotifications, false);
        if (_managedOnboarding)
            Win32.EnableWindow(_hWndChkOnboarding, false);

        // Noms de langue = endonymes, jamais traduits (un sélecteur de langue affiche
        // chaque langue dans elle-même : "Français" et "English" quelle que soit la langue active).
        _hWndRadioLangFr = Win32.CreateWindowExW(0, "BUTTON", "Français",
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_AUTORADIOBUTTON | WS_GROUP | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RADIO_LANG_FR, hInstance, IntPtr.Zero);

        _hWndRadioLangEn = Win32.CreateWindowExW(0, "BUTTON", "English",
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_AUTORADIOBUTTON | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RADIO_LANG_EN, hInstance, IntPtr.Zero);
        _hWndManagedLanguage = CreateManagedNotice(hInstance, _managedLanguage);
        if (_managedLanguage)
        {
            Win32.EnableWindow(_hWndRadioLangFr, false);
            Win32.EnableWindow(_hWndRadioLangEn, false);
        }

        RefreshLanguageRadios();

        _hWndResetVirtualKeyboardWindow = Win32.CreateWindowExW(0, "BUTTON", L.Settings_ResetVirtualKeyboard,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RESET_VIRTUAL_KEYBOARD_WINDOW, hInstance, IntPtr.Zero);

        _hWndResetLessonsWindow = Win32.CreateWindowExW(0, "BUTTON", L.Settings_ResetLessonsModule,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RESET_LESSONS_WINDOW, hInstance, IntPtr.Zero);

        // ── Section « Apps suspendues » (v1.2.0) ─────────────────────
        // La liste se dessine à la manière de la recherche : nom à gauche, mode à droite,
        // ligne choisie teintée et marquée du trait d'action. Le cadre est peint par la
        // fenêtre (pas de WS_BORDER, dont le trait système est noir).
        _hWndLabelCompat = CreateHiddenLabel(hInstance, L.Settings_SectionCompat);
        _hWndCompatList = Win32.CreateWindowExW(0, "LISTBOX", "",
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP |
            LBS_NOTIFY | LBS_NOINTEGRALHEIGHT | LBS_OWNERDRAWFIXED | LBS_HASSTRINGS | WS_VSCROLL,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LIST_COMPAT, hInstance, IntPtr.Zero);

        _hWndCompatAdd = Win32.CreateWindowExW(0, "BUTTON", L.Settings_CompatAdd,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_COMPAT_ADD, hInstance, IntPtr.Zero);

        _hWndCompatRemove = Win32.CreateWindowExW(0, "BUTTON", L.Settings_CompatRemove,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_COMPAT_REMOVE, hInstance, IntPtr.Zero);

        // WS_GROUP obligatoire sur le premier radio : termine le groupe Langue,
        // sinon cocher un mode décocherait Français/English.
        _hWndRadioCompatAuto = Win32.CreateWindowExW(0, "BUTTON", L.Settings_CompatModeAuto,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_AUTORADIOBUTTON | WS_GROUP | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RADIO_COMPAT_AUTO, hInstance, IntPtr.Zero);

        _hWndRadioCompatForceOn = Win32.CreateWindowExW(0, "BUTTON", L.Settings_CompatModeForceOn,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_AUTORADIOBUTTON | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RADIO_COMPAT_FORCEON, hInstance, IntPtr.Zero);

        _hWndRadioCompatForceOff = Win32.CreateWindowExW(0, "BUTTON", L.Settings_CompatModeForceOff,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_AUTORADIOBUTTON | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RADIO_COMPAT_FORCEOFF, hInstance, IntPtr.Zero);

        RefreshCompatList(selectProcess: null);
        _controls = DescribeControls();
        PreparePaperButtons();
        SetCompatDescriptions();
        SetSettingDescriptions();
    }

    /// <summary>
    /// Un contrôle enfant, déclaré une seule fois (audit du 25/09, F-03) : son onglet, sa
    /// police, son rectangle, son libellé traduit et s'il est un bouton poussoir. Polices,
    /// placement, visibilité par onglet, traduction et touche Entrée parcourent cette table.
    /// Auparavant chaque contrôle figurait dans huit à onze listes parallèles, et deux oublis
    /// avaient fait des bugs (police de la bande d'onglets, libellés des onglets).
    /// </summary>
    /// <param name="Tab">Onglet qui le montre ; null : jamais masqué par onglet.</param>
    /// <param name="Shown">Condition d'affichage en plus de l'onglet (politique, Défi).</param>
    private sealed record ControlSlot(
        IntPtr Handle,
        SettingsTab? Tab,
        Func<IntPtr>? Font,
        Func<LayoutInfo, Win32.RECT>? Bounds,
        Func<string>? Text = null,
        Func<bool>? Shown = null,
        bool PushButton = false);

    private ControlSlot[] _controls = Array.Empty<ControlSlot>();

    private ControlSlot[] DescribeControls()
    {
        const SettingsTab General = SettingsTab.General;
        const SettingsTab Apps = SettingsTab.Applications;
        const SettingsTab LangMaint = SettingsTab.LanguageMaintenance;
        return new ControlSlot[]
        {
            // ⛔ La bande d'onglets doit rester dans la table : elle mesure ses onglets avec
            // sa police, qui change avec l'échelle (geste 40 de la recette VM). Ses libellés,
            // eux, passent par TCM_SETITEMW (SetTabText), pas par le texte de fenêtre.
            new(_hWndTabStrip, null, () => FontTab.Handle, l => l.TabStripRect),
            // Étiquettes N8 jamais affichées : un texte, ni police ni place.
            new(_hWndLabelTabs, null, null, null, () => L.SettingsPaper_TabsName),
            new(_hWndLabelKeyboard, null, null, null, () => L.Settings_ShortcutLabelKeyboard),
            new(_hWndLabelSearch, null, null, null, () => L.Settings_ShortcutLabelSearch),
            new(_hWndLabelCompat, null, null, null, () => L.Settings_SectionCompat),

            new(_hWndEditKeyboard, General, () => FontKey.Handle, l => l.KeyboardEditRect),
            new(_hWndEditSearch, General, () => FontKey.Handle, l => l.SearchEditRect),
            // La ligne de validation sert les trois onglets : jamais masquée par onglet.
            new(_hWndValidation, null, () => FontSmall.Handle, l => l.ValidationRect),
            new(_hWndLinkReset, General, () => FontButton.Handle, l => l.ResetRect,
                () => L.Settings_LinkResetDefaults, PushButton: true),
            new(_hWndChkAutoStart, General, () => FontBody.Handle, l => l.AutoStartRect, () => L.Settings_AutoStart),
            new(_hWndChkNotifications, General, () => FontBody.Handle, l => l.NotificationsRect, () => L.Settings_Notifications),
            new(_hWndManagedNotifications, General, () => FontSmall.Handle, l => l.ManagedNotificationsRect,
                () => L.Settings_ManagedByOrganization, () => _managedNotifications),
            new(_hWndChkOnboarding, General, () => FontBody.Handle, l => l.OnboardingRect, () => L.Settings_OnboardingWindow),
            new(_hWndManagedOnboarding, General, () => FontSmall.Handle, l => l.ManagedOnboardingRect,
                () => L.Settings_ManagedByOrganization, () => _managedOnboarding),
            new(_hWndChkTraining, General, () => FontBody.Handle, l => l.TrainingRect,
                () => L.Challenge_OptIn, () => DailyChallenge.Enabled),

            new(_hWndCompatList, Apps, () => FontListName.Handle, l => l.CompatListRect),
            new(_hWndCompatAdd, Apps, () => FontButton.Handle, l => l.CompatAddRect, () => L.Settings_CompatAdd, PushButton: true),
            new(_hWndCompatRemove, Apps, () => FontButton.Handle, l => l.CompatRemoveRect, () => L.Settings_CompatRemove, PushButton: true),
            new(_hWndRadioCompatAuto, Apps, () => FontBody.Handle, l => l.CompatAutoRect, () => L.Settings_CompatModeAuto),
            new(_hWndRadioCompatForceOn, Apps, () => FontBody.Handle, l => l.CompatForceOnRect, () => L.Settings_CompatModeForceOn),
            new(_hWndRadioCompatForceOff, Apps, () => FontBody.Handle, l => l.CompatForceOffRect, () => L.Settings_CompatModeForceOff),

            // Noms de langue : endonymes, jamais retraduits.
            new(_hWndRadioLangFr, LangMaint, () => FontBody.Handle, l => l.LanguageFrRect),
            new(_hWndRadioLangEn, LangMaint, () => FontBody.Handle, l => l.LanguageEnRect),
            new(_hWndManagedLanguage, LangMaint, () => FontSmall.Handle, l => l.ManagedLanguageRect,
                () => L.Settings_ManagedByOrganization, () => _managedLanguage),
            new(_hWndResetVirtualKeyboardWindow, LangMaint, () => FontButton.Handle, l => l.ResetVirtualKeyboardWindowRect,
                () => L.Settings_ResetVirtualKeyboard, PushButton: true),
            new(_hWndResetLessonsWindow, LangMaint, () => FontButton.Handle, l => l.ResetLessonsWindowRect,
                () => L.Settings_ResetLessonsModule, PushButton: true),
        };
    }

    /// <summary>
    /// Donne à la fenêtre la hauteur de son contenu, plafonnée à la zone de travail de
    /// l'écran qui la porte, et arme le défilement si le plafond a mordu.
    ///
    /// Écart 4 de la recette VM du 2026-09-19 : en 1366×768 à 150 %, la fenêtre dépassait
    /// par le bas et le troisième bouton radio de compatibilité était inatteignable. Le
    /// modèle v2.0.0 (audit du 2026-08-28, § 17) pose la règle : aucune fenêtre ne dépasse
    /// la zone de travail, et une fenêtre mesure son contenu plutôt que de le supposer.
    ///
    /// C4, décision d'Antoine du 2026-09-24 : la fenêtre ne change pas de taille d'un onglet
    /// à l'autre, ni quand un message s'affiche. Refonte 1.3.0 : la référence est l'onglet le
    /// plus haut, message compris — « Applications » l'est devenu avec sa liste de trois
    /// lignes, et le prendre pour référence évite qu'il défile à 100 %. Un onglet plus court
    /// laisse du papier en bas, où le pied s'ancre. La mesure est refaite à chaque appel,
    /// donc à l'échelle courante : un changement de DPI ou de langue la recalcule.
    /// </summary>
    private void FitWindowToContent()
    {
        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;

        // Mesures sans défilement : la hauteur du contenu ne dépend pas de la position.
        int generalHeight = MeasureContentHeight(SettingsTab.General);
        int applicationsHeight = MeasureContentHeight(SettingsTab.Applications);
        int languageHeight = MeasureContentHeight(SettingsTab.LanguageMaintenance);
        int referenceHeight = Math.Max(generalHeight, Math.Max(applicationsHeight, languageHeight));

        Win32.GetWindowRect(_hWnd, out var currentRect);
        int cx = (currentRect.left + currentRect.right) / 2;
        int cy = (currentRect.top + currentRect.bottom) / 2;
        var hMonitor = Win32.MonitorFromWindow(_hWnd, 0x00000002);
        var monInfo = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        Win32.GetMonitorInfo(hMonitor, ref monInfo);
        int workH = monInfo.rcWork.bottom - monInfo.rcWork.top;
        int workW = monInfo.rcWork.right - monInfo.rcWork.left;

        // Hauteur de client maximale : la zone de travail moins les bordures et le
        // titre, mesurés par AdjustWindowRectEx plutôt que devinés.
        var frame = new Win32.RECT { left = 0, top = 0, right = S(BASE_WIN_W), bottom = referenceHeight };
        Win32.AdjustWindowRectEx(ref frame, dwStyle, false, 0);
        int chromeH = (frame.bottom - frame.top) - referenceHeight;
        int chromeW = (frame.right - frame.left) - S(BASE_WIN_W);
        int maxClientH = Math.Max(S(200), workH - chromeH);

        var (clientH, reserveScrollBar) = SettingsScrollState.FixedViewport(
            referenceHeight, maxClientH, generalHeight, applicationsHeight, languageHeight);
        _viewportHeight = clientH;
        _reserveScrollBar = reserveScrollBar;
        // Le contenu affiché, pied ancré au bas de la zone obtenue.
        _contentHeight = GetLayout(S(BASE_WIN_W)).ContentHeight;

        // La barre de défilement mange de la largeur du client : l'ajouter à la
        // fenêtre pour que le contenu garde sa largeur de mise en page. Réservée pour
        // tous les onglets dès que l'un d'eux défile, sinon la largeur varierait.
        int clientW = S(BASE_WIN_W) + (reserveScrollBar ? Win32.GetSystemMetrics(Win32.SM_CXVSCROLL) : 0);
        int windowW = Math.Min(clientW + chromeW, workW);
        int windowH = clientH + chromeH;

        // Replacer dans la zone de travail : centrer puis ramener si un bord sort. À
        // taille inchangée (changement d'onglet), garder la position : recentrer par
        // division entière décalerait la fenêtre d'un pixel en coordonnées négatives.
        bool sameSize = currentRect.right - currentRect.left == windowW
            && currentRect.bottom - currentRect.top == windowH;
        int x = sameSize ? currentRect.left : cx - windowW / 2;
        int y = sameSize ? currentRect.top : cy - windowH / 2;
        x = Math.Max(monInfo.rcWork.left, Math.Min(x, monInfo.rcWork.right - windowW));
        y = Math.Max(monInfo.rcWork.top, Math.Min(y, monInfo.rcWork.bottom - windowH));
        if (!sameSize || x != currentRect.left || y != currentRect.top)
            Win32.MoveWindow(_hWnd, x, y, windowW, windowH, true);

        ClampScroll();
        UpdateScrollBar();
    }

    /// <summary>Hauteur d'un onglet, sans défilement, à l'échelle courante, avec la plus haute
    /// des places que peut prendre un message sur cet onglet : le pied partout, l'invite ou
    /// un refus de raccourci sur « Général », le refus de compatibilité (mesuré sur son vrai
    /// texte, deux lignes en français) sur « Applications ».</summary>
    private int MeasureContentHeight(SettingsTab tab)
    {
        int width = S(BASE_WIN_W);
        int inner = width - 2 * S(20);
        int height = Layout(width, tab, MessagePlace.Footer, 0, 0).ContentHeight;
        if (tab == SettingsTab.General)
        {
            int message = 0;
            foreach (string text in ShortcutMessages())
                message = Math.Max(message, MessageHeight(text, inner));
            height = Math.Max(height, Layout(width, tab, MessagePlace.Shortcuts, message, 0).ContentHeight);
        }
        else if (tab == SettingsTab.Applications)
        {
            int message = Math.Max(MessageHeight(L.SettingsClarity_CompatRefusedAntiCheat, inner),
                MessageHeight(L.SettingsClarity_CompatRefusedRemote, inner));
            height = Math.Max(height, Layout(width, tab, MessagePlace.Compat, message, 0).ContentHeight);
        }
        return height;
    }

    /// <summary>Tous les messages que peut porter la place sous les raccourcis : l'invite et
    /// chaque refus possible. La taille fixe (C4) se mesure sur le plus haut.</summary>
    private static IEnumerable<string> ShortcutMessages()
    {
        yield return ShortcutCaptureHint;
        yield return L.SettingsClarity_ShortcutKeypadDigit;
        yield return L.SettingsClarity_ShortcutOtherKey;
        yield return L.SettingsClarity_ShortcutTakenByKeyboard("F12");
        yield return L.SettingsClarity_ShortcutTakenBySearch("F12");
        for (char letter = 'A'; letter <= 'Z'; letter++)
            if (!ConfigManager.IsShortcutAllowedVk(letter))
                yield return L.SettingsClarity_ShortcutReservedLetter(letter);
    }

    /// <summary>Le refus d'une touche, avec sa raison : lettre réservée (ce qu'elle fait déjà
    /// ailleurs), chiffre du pavé numérique, ou autre touche. Null si la touche est acceptée.</summary>
    internal static string? ShortcutRefusal(uint vk)
    {
        if (ConfigManager.IsShortcutAllowedVk(vk)) return null;
        if (vk >= 'A' && vk <= 'Z') return L.SettingsClarity_ShortcutReservedLetter((char)vk);
        if (vk >= 0x60 && vk <= 0x69) return L.SettingsClarity_ShortcutKeypadDigit; // VK_NUMPAD0-9
        return L.SettingsClarity_ShortcutOtherKey;
    }

    private void ClampScroll()
    {
        int maxScroll = Math.Max(0, _contentHeight - _viewportHeight);
        _scrollY = Math.Max(0, Math.Min(_scrollY, maxScroll));
    }

    private void UpdateScrollBar()
    {
        // Onglet qui tient dans une fenêtre dont la barre est réservée : barre grisée
        // (SIF_DISABLENOSCROLL) plutôt que masquée, la largeur du client ne bouge pas.
        bool needed = _contentHeight > _viewportHeight;
        bool visible = needed || _reserveScrollBar;
        Win32.ShowScrollBar(_hWnd, Win32.SB_VERT, visible);
        if (!visible) return;
        var si = new Win32.SCROLLINFO
        {
            cbSize = (uint)Marshal.SizeOf<Win32.SCROLLINFO>(),
            fMask = Win32.SIF_RANGE | Win32.SIF_PAGE | Win32.SIF_POS | (needed ? 0u : SIF_DISABLENOSCROLL),
            nMin = 0,
            nMax = Math.Max(0, _contentHeight - 1),
            nPage = (uint)Math.Max(1, _viewportHeight),
            nPos = _scrollY
        };
        Win32.SetScrollInfo(_hWnd, Win32.SB_VERT, ref si, true);
    }

    /// <summary>Applique une nouvelle position de défilement et redessine si elle a bougé.</summary>
    private void EnsureFocusVisible(IntPtr control)
    {
        if (control == IntPtr.Zero || control == _hWnd || _contentHeight <= _viewportHeight) return;
        if (!Win32.GetWindowRect(control, out var rect)) return;
        var top = new Win32.POINT { x = rect.left, y = rect.top };
        var bottom = new Win32.POINT { x = rect.right, y = rect.bottom };
        if (!Win32.ScreenToClient(_hWnd, ref top) || !Win32.ScreenToClient(_hWnd, ref bottom)) return;
        ScrollTo(SettingsScrollState.EnsureVisible(_scrollY, top.y, bottom.y, _viewportHeight, S(8)));
    }

    private void ScrollTo(int newScroll)
    {
        int before = _scrollY;
        _scrollY = newScroll;
        ClampScroll();
        if (_scrollY == before) return;
        UpdateScrollBar();
        RepositionControls();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    /// <summary>
    /// Cree la bande de trois onglets. Un vrai SysTabControl32 et non trois libelles
    /// peints : lui seul est annonce « onglet 1 sur 3 » par les lecteurs d'ecran et
    /// repond aux fleches gauche/droite sans une ligne de notre part. Refonte 1.3.0 : il
    /// garde tout cela, seul son dessin change (sous-classe, <see cref="PaintTabStrip"/>).
    /// ⛔ Pas de TCS_FOCUSNEVER (revue du 2026-09-21, R1) : la bande est un arrêt de
    /// tabulation comme les autres. Tab y arrive, les flèches changent d'onglet, Tab en
    /// repart vers le premier contrôle de l'onglet actif. Sans ce focus, rien ne permettait
    /// au clavier seul d'atteindre « Applications » ni « Langue » : IsDialogMessageW ne
    /// traite pas Ctrl+Tab, et Tab sur une bande focalisée ne change pas d'onglet — la
    /// crainte qui avait motivé TCS_FOCUSNEVER était fausse.
    /// </summary>
    private void CreateTabStrip(IntPtr hInstance)
    {
        var icc = new Win32.INITCOMMONCONTROLSEX
        {
            dwSize = (uint)Marshal.SizeOf<Win32.INITCOMMONCONTROLSEX>(),
            dwICC = Win32.ICC_TAB_CLASSES
        };
        Win32.InitCommonControlsEx(ref icc);

        _hWndTabStrip = Win32.CreateWindowExW(0, Win32.WC_TABCONTROL, string.Empty,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_TAB_STRIP, hInstance, IntPtr.Zero);
        if (_hWndTabStrip == IntPtr.Zero) return;

        ApplyTabStripPadding();
        Win32.SendMessageW(_hWndTabStrip, Win32.WM_SETFONT, FontTab.Handle, (IntPtr)1);
        InsertTab(0, L.Settings_TabGeneral);
        InsertTab(1, L.Settings_TabApplications);
        InsertTab(2, L.Settings_TabLanguageMaintenance);
        Win32.SendMessageW(_hWndTabStrip, Win32.TCM_SETCURSEL, (IntPtr)(int)_activeTab, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndTabStrip, _tabSubclassProc, (UIntPtr)5, IntPtr.Zero);
    }

    private void InsertTab(int index, string text)
        => SendTabText(Win32.TCM_INSERTITEMW, index, text);

    /// <summary>Réécrit le libellé d'un onglet déjà posé. Appelé par
    /// <see cref="RefreshLanguageTexts"/> : sans lui, la bande gardait les libellés de la
    /// langue d'origine sur une fenêtre entièrement retraduite — « Général / Applications /
    /// Langue » sur une fenêtre anglaise (R10 de la revue du 2026-09-21).</summary>
    private void SetTabText(int index, string text)
        => SendTabText(Win32.TCM_SETITEMW, index, text);

    /// <summary>Envoi commun à l'insertion et à la mise à jour : même <c>TCITEMW</c>, même
    /// masque, même libération. Les deux chemins partagent ce corps pour qu'un libellé
    /// posé et un libellé retraduit ne puissent pas diverger de forme.</summary>
    private void SendTabText(uint message, int index, string text)
    {
        if (_hWndTabStrip == IntPtr.Zero) return;

        IntPtr pText = Marshal.StringToHGlobalUni(text);
        try
        {
            var item = new Win32.TCITEMW { mask = Win32.TCIF_TEXT, pszText = pText };
            IntPtr pItem = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.TCITEMW>());
            try
            {
                Marshal.StructureToPtr(item, pItem, false);
                Win32.SendMessageW(_hWndTabStrip, message, (IntPtr)index, pItem);
            }
            finally { Marshal.FreeHGlobal(pItem); }
        }
        finally { Marshal.FreeHGlobal(pText); }
    }

    private void RepositionControls()
    {
        LayoutInfo layout = GetLayout(S(BASE_WIN_W));
        foreach (var control in _controls)
            if (control.Bounds != null)
                Place(control.Handle, control.Bounds(layout));
        ApplyTabVisibility();
        if (_hWndValidation != IntPtr.Zero)
            Win32.InvalidateRect(_hWndValidation, IntPtr.Zero, false);
    }

    /// <summary>Place un contrôle. La mise en page est en coordonnées de contenu : c'est ici,
    /// et au tracé, que le défilement s'applique.</summary>
    private void Place(IntPtr hWnd, Win32.RECT r) =>
        Win32.MoveWindow(hWnd, r.left, r.top - _scrollY, r.right - r.left, r.bottom - r.top, true);

    /// <summary>
    /// Masque les contrôles qui n'appartiennent pas à l'onglet actif. ⛔ Masquer et
    /// non déplacer hors écran : un contrôle visible hors du cadre reste dans l'ordre
    /// de tabulation et le focus disparaîtrait de la vue (AG130-40, geste 41).
    /// </summary>
    private void ApplyTabVisibility()
    {
        foreach (var control in _controls)
        {
            if (control.Tab is not { } tab || control.Handle == IntPtr.Zero) continue;
            bool visible = tab == _activeTab && (control.Shown?.Invoke() ?? true);
            Win32.ShowWindow(control.Handle, visible ? 5 : 0); // SW_SHOW / SW_HIDE
        }
    }

    /// <summary>Les boutons poussoirs de la fenêtre — ceux qu'Entrée doit presser quand ils ont le focus.</summary>
    private IntPtr[] PushButtons() => _controls.Where(c => c.PushButton).Select(c => c.Handle).ToArray();

    /// <summary>Change d'onglet et le redessine. La taille de la fenêtre ne change pas (C4) :
    /// FitWindowToContent ne fait que remesurer le contenu du nouvel onglet et armer ou
    /// griser le défilement.</summary>
    private void SetActiveTab(SettingsTab tab)
    {
        if (_activeTab == tab) return;
        _activeTab = tab;
        // Un message répond à un geste de l'onglet quitté : il ne le suit pas.
        _validationMessage = string.Empty;
        _validationRefused = false;
        _showCaptureHint = false;
        Win32.SetWindowTextW(_hWndValidation, string.Empty);
        // Le défilement d'un onglet n'a pas de sens dans le suivant : il est plus
        // court, et un décalage hérité laisserait la fenêtre ouverte sur du vide.
        _scrollY = 0;
        FitWindowToContent();
        RepositionControls();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
        if (_hWndTabStrip != IntPtr.Zero)
            Win32.InvalidateRect(_hWndTabStrip, IntPtr.Zero, false);
    }

    /// <summary>Place du message affiché : sa nature et l'onglet qui l'a produit.</summary>
    private MessagePlace CurrentMessagePlace()
    {
        if (string.IsNullOrEmpty(_validationMessage)) return MessagePlace.None;
        if (_activeTab == SettingsTab.General && (_showCaptureHint || _validationRefused))
            return MessagePlace.Shortcuts;
        if (_activeTab == SettingsTab.Applications && _validationRefused)
            return MessagePlace.Compat;
        return MessagePlace.Footer;
    }

    /// <summary>Hauteur du pied de confirmation.</summary>
    private int FooterHeight => S(38);

    /// <summary>Interligne des messages sur deux lignes (13 px × 1,4).</summary>
    private int MessageLineHeight => S(18);

    private LayoutInfo GetLayout(int winW)
    {
        var place = CurrentMessagePlace();
        int inner = winW - 2 * S(20);
        int height = place is MessagePlace.Shortcuts or MessagePlace.Compat
            ? ValidationTextHeight(_activeTab, inner, MessageLineHeight)
            : 0;
        return Layout(winW, _activeTab, place, height, _viewportHeight);
    }

    /// <summary>Mise en page d'un onglet, aux mesures de la maquette (px à 96 DPI portés à
    /// l'échelle). Les paramètres explicites permettent de mesurer un autre onglet que
    /// l'onglet affiché, et une autre place de message (taille fixe, C4). Seuls la bande et
    /// l'onglet demandé sont calculés : les rectangles des autres onglets restent vides, leurs
    /// contrôles étant masqués par ApplyTabVisibility, jamais déplacés hors écran (audit du
    /// 25/09, F-04). Coordonnées de contenu : le défilement s'applique au placement des
    /// contrôles (Place) et au tracé (SetViewportOrgEx).</summary>
    /// <param name="messageHeight">Hauteur du message sous les raccourcis ou les modes.</param>
    /// <param name="viewport">Hauteur de la zone cliente, où le pied s'ancre ; 0 en mesure.</param>
    private LayoutInfo Layout(int winW, SettingsTab tab, MessagePlace place, int messageHeight, int viewport)
    {
        var m = Metrics;
        int margin = S(20);
        int inner = winW - margin * 2;
        int bandHeight = S(44);
        int titleGap = S(10);
        int sectionGap = S(22);
        int choiceH = S(32);
        int buttonH = S(32);
        int focusPad = S(4); // place du contour de focus autour d'une case ou d'une coche

        var layout = new LayoutInfo
        {
            Margin = margin,
            MessagePlace = place,
            TabStripRect = Rect(m.TabStripLeft, 0, winW - m.TabStripLeft, bandHeight),
        };

        int top = bandHeight + S(18);
        int contentEnd = tab switch
        {
            SettingsTab.General => LayoutGeneral(),
            SettingsTab.Applications => LayoutApplications(),
            _ => LayoutLanguageMaintenance(),
        } + S(20);

        if (place == MessagePlace.Footer)
        {
            int footerTop = Math.Max(contentEnd, viewport - FooterHeight);
            layout.ValidationRect = Rect(0, footerTop, winW, FooterHeight);
            layout.ContentHeight = footerTop + FooterHeight;
        }
        else
        {
            layout.ContentHeight = contentEnd;
        }
        return layout;

        Win32.RECT Title(ref int y)
        {
            var r = Rect(margin, y, inner, m.SectionTitle);
            y += m.SectionTitle + titleGap;
            return r;
        }

        // Une case à cocher ou un bouton radio : ligne de 32 px, le contrôle déborde de la
        // marge de quoi porter son contour de focus.
        Win32.RECT Choice(ref int y, bool shown = true)
        {
            var r = Rect(margin - focusPad, y, inner + 2 * focusPad, shown ? choiceH : 0);
            if (shown) y += choiceH;
            return r;
        }

        // La ligne « Géré par votre organisation », alignée sur le libellé de la case.
        Win32.RECT Managed(ref int y, bool shown)
        {
            int indent = S(18) + S(10);
            var r = Rect(margin + indent, y, inner - indent, shown ? m.Small : 0);
            if (shown) y += m.Small + S(4);
            return r;
        }

        // Ce que fait un mode, sous son bouton radio, aligné sur le libellé et coupé en lignes.
        // Sans retrait (indented: false) sous une ligne de raccourci, dont le libellé part de la marge.
        Win32.RECT Hint(ref int y, string text, bool indented = true)
        {
            int indent = indented ? S(18) + S(10) : 0;
            int h = MessageHeight(text, inner - indent);
            // Le bouton radio est une fenêtre fille : le texte commence sous elle.
            var r = Rect(margin + indent, y, inner - indent, h);
            y += h + S(6);
            return r;
        }

        int ButtonWidth(int textWidth) => Math.Min(inner, textWidth + 2 * S(14) + 2);

        // ── Onglet « Général » : Raccourcis puis Préférences ────────────
        int LayoutGeneral()
        {
            int y = top;
            layout.ShortcutsTitleRect = Title(ref y);
            // Chaque raccourci porte sous sa ligne ce qu'il ouvre (lot « clarté »). La ligne
            // de 40 px centre son libellé : le sous-titre remonte de 4 px pour s'y rattacher.
            layout.KeyboardRowRect = Rect(margin, y, inner, S(40));
            y += S(40) - S(4);
            layout.KeyboardHintRect = Hint(ref y, L.SettingsClarity_HintKeyboardShortcut, indented: false);
            layout.SearchRowRect = Rect(margin, y, inner, S(40));
            y += S(40) - S(4);
            layout.SearchHintRect = Hint(ref y, L.SettingsClarity_HintSearchShortcut, indented: false);
            layout.KeyboardBoxRect = KeyBox(layout.KeyboardRowRect, ShownVk(keyboard: true));
            layout.SearchBoxRect = KeyBox(layout.SearchRowRect, ShownVk(keyboard: false));
            layout.KeyboardEditRect = Inflate(layout.KeyboardBoxRect, focusPad);
            layout.SearchEditRect = Inflate(layout.SearchBoxRect, focusPad);

            // L'invite de capture et les refus de raccourci, sous les deux lignes, à droite.
            if (place == MessagePlace.Shortcuts)
            {
                layout.ValidationRect = Rect(margin, y + S(4), inner, messageHeight);
                y = layout.ValidationRect.bottom + S(2);
            }

            // « Réinitialiser les raccourcis », écrit comme un lien : le contrôle déborde du
            // texte de quoi porter son contour de focus.
            int linkTop = y + S(8);
            layout.ResetRect = Rect(margin - S(3), linkTop - S(3), m.ResetText + S(6), m.Button + S(6));
            y = linkTop + m.Button + sectionGap;

            layout.PreferencesTitleRect = Title(ref y);
            layout.AutoStartRect = Choice(ref y);
            layout.AutoStartHintRect = Hint(ref y, L.SettingsClarity_HintAutoStart);
            layout.NotificationsRect = Choice(ref y);
            layout.NotificationsHintRect = Hint(ref y, L.SettingsClarity_HintNotifications);
            // Lignes « Géré par votre organisation » : sous le sous-titre de la case. Hauteur
            // nulle quand rien n'est imposé.
            layout.ManagedNotificationsRect = Managed(ref y, _managedNotifications);
            layout.OnboardingRect = Choice(ref y);
            layout.OnboardingHintRect = Hint(ref y, L.SettingsClarity_HintOnboarding);
            layout.ManagedOnboardingRect = Managed(ref y, _managedOnboarding);
            // Opt-in des rappels du Défi : ligne repliée tant que le Défi est masqué (1.3.0).
            layout.TrainingRect = Choice(ref y, DailyChallenge.Enabled);
            return y;
        }

        // ── Onglet « Applications » : apps suspendues ───────────────────
        int LayoutApplications()
        {
            int y = top;
            layout.CompatTitleRect = Title(ref y);
            // Trois lignes visibles, cadre d'un pixel peint par la fenêtre.
            layout.CompatListFrame = Rect(margin, y, inner, 3 * CompatRowHeight + 2);
            layout.CompatListRect = Inflate(layout.CompatListFrame, -1);
            y = layout.CompatListFrame.bottom + S(8);
            layout.CompatAddRect = Rect(margin, y, ButtonWidth(m.AddText), buttonH);
            layout.CompatRemoveRect = Rect(layout.CompatAddRect.right + S(8), y, ButtonWidth(m.RemoveText), buttonH);
            y += buttonH + S(14);
            layout.CompatModeLabelRect = Rect(margin, y, inner, m.Small);
            y += m.Small + S(2);
            layout.CompatAutoRect = Choice(ref y);
            layout.CompatAutoHintRect = Hint(ref y, L.SettingsPaper_CompatHintAuto);
            layout.CompatForceOnRect = Choice(ref y);
            layout.CompatForceOnHintRect = Hint(ref y, L.SettingsPaper_CompatHintForceOn);
            layout.CompatForceOffRect = Choice(ref y);
            layout.CompatForceOffHintRect = Hint(ref y, L.SettingsPaper_CompatHintForceOff);
            if (place == MessagePlace.Compat)
            {
                layout.ValidationRect = Rect(margin, y + S(6), inner, messageHeight);
                y = layout.ValidationRect.bottom;
            }
            return y;
        }

        // ── Onglet « Langue » : langue, puis fenêtres ───────────────────
        int LayoutLanguageMaintenance()
        {
            int y = top;
            layout.LanguageTitleRect = Title(ref y);
            layout.LanguageFrRect = Choice(ref y);
            layout.LanguageEnRect = Choice(ref y);
            layout.ManagedLanguageRect = Managed(ref y, _managedLanguage);
            y += sectionGap;
            layout.WindowsTitleRect = Title(ref y);
            // Sous chaque bouton, ce que fait la réinitialisation (lot « clarté »), sans retrait :
            // le bouton part de la marge.
            layout.ResetVirtualKeyboardWindowRect = Rect(margin, y, ButtonWidth(m.ResetKeyboardText), buttonH);
            y += buttonH + S(6);
            layout.ResetVirtualKeyboardHintRect = Hint(ref y, L.SettingsClarity_HintResetWindow, indented: false);
            y += S(6);
            layout.ResetLessonsWindowRect = Rect(margin, y, ButtonWidth(m.ResetLessonsText), buttonH);
            y += buttonH + S(6);
            layout.ResetLessonsHintRect = Hint(ref y, L.SettingsClarity_HintResetWindow, indented: false);
            return y;
        }

        // La case à frapper d'un raccourci, au bord droit de sa ligne : 34 px au moins, plus
        // large pour « F12 ».
        Win32.RECT KeyBox(Win32.RECT row, uint vk)
        {
            int size = S(34);
            int width = Math.Max(size, ConfigManager.GetShortcutDisplayName(vk).Length * m.KeyAdvance + S(16));
            return Rect(row.right - width, row.top + (row.bottom - row.top - size) / 2, width, size);
        }
    }

    private static Win32.RECT Rect(int left, int top, int width, int height)
    {
        return new Win32.RECT
        {
            left = left,
            top = top,
            right = left + width,
            bottom = top + height
        };
    }

    private static Win32.RECT Inflate(Win32.RECT r, int by) => new()
    {
        left = r.left - by,
        top = r.top - by,
        right = r.right + by,
        bottom = r.bottom + by,
    };

    public void Show()
    {
        LoadShortcutStateFromConfig();
        RefreshShortcutTexts();
        RefreshAutoStartCheckbox();
        // Les overrides ont pu changer via le sous-menu tray Compatibilité depuis la
        // dernière ouverture : resynchroniser la section Apps suspendues.
        RefreshCompatList(SelectedCompatProcess());
        // L'opt-in Défi du jour a pu changer via l'onboarding.
        Win32.SendMessageW(_hWndChkTraining, Win32.BM_SETCHECK,
            ConfigManager.TrainingEnabled ? (IntPtr)Win32.BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
        Win32.SendMessageW(_hWndChkNotifications, Win32.BM_SETCHECK,
            ConfigManager.NotificationsEnabled ? (IntPtr)Win32.BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
        // Re-synchroniser la checkbox onboarding a chaque ouverture : l'utilisateur a pu
        // modifier l'etat via la case « Ne plus afficher » du wizard depuis la derniere
        // fermeture des Settings.
        Win32.SendMessageW(_hWndChkOnboarding, Win32.BM_SETCHECK,
            ConfigManager.ShowOnboardingAtStartup ? (IntPtr)Win32.BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
        RefreshLanguageRadios();
        SetValidationMessage(string.Empty);
        _keyboardValid = true;
        _searchValid = true;
        _focusedShortcut = IntPtr.Zero;

        RepositionControls();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
        Win32.ShowWindow(_hWnd, 1);
        Win32.SetForegroundWindow(_hWnd);
        _visible = true;
    }

    public void SetInputPaused(bool paused)
    {
        if (_inputPaused == paused) return;
        _inputPaused = paused;

        if (paused && _focusedShortcut != IntPtr.Zero)
        {
            CancelShortcutCapture(_focusedShortcut);
            _focusedShortcut = IntPtr.Zero;
            if (_hWnd != IntPtr.Zero)
                Win32.SetFocus(_hWnd);
        }
    }

    public void Close()
    {
        bool autoStart = Win32.SendMessageW(_hWndChkAutoStart, Win32.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)Win32.BST_CHECKED;
        bool autoStartWasRegistered = AutoStart.IsRegistered;
        bool autoStartSaved = AutoStart.Set(autoStart);
        RefreshAutoStartCheckbox();
        if (!autoStartSaved)
            ShowAutoStartError();
        // Un choix fait à la main éteint la relance, dans un sens comme dans l'autre —
        // même doctrine que l'accueil et que l'ancienne entrée du menu tray (R2 de l'audit v1.2.0).
        // Seul un changement réel compte : refermer Paramètres sans toucher la case
        // n'est pas un choix, et ne doit pas consommer la proposition.
        else if (autoStart != autoStartWasRegistered)
            AutoStartNudge.MarkPromptShown();

        // Un réglage imposé ne se réécrit pas dans config.json : la case affiche la valeur de
        // la politique, la persister écraserait le choix de l'utilisateur, qui doit reprendre
        // effet le jour où la politique est retirée.
        if (!_managedNotifications)
        {
            bool notifications = Win32.SendMessageW(_hWndChkNotifications, Win32.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)Win32.BST_CHECKED;
            ConfigManager.SetNotifications(notifications);
        }

        if (!_managedOnboarding)
        {
            bool showOnboarding = Win32.SendMessageW(_hWndChkOnboarding, Win32.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)Win32.BST_CHECKED;
            ConfigManager.SetShowOnboardingAtStartup(showOnboarding);
        }

        Win32.ShowWindow(_hWnd, 0);
        _visible = false;
    }

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

            // Les contrôles thémés demandent le fond de leur parent (DrawThemeParentBackground) :
            // la réponse est la fenêtre elle-même, peinte dans leur DC déjà décalé. SaveDC : nos
            // objets GDI ne restent pas dans leur DC.
            case Win32.WM_PRINTCLIENT:
            {
                Win32.GetClientRect(hWnd, out var client);
                int saved = Win32.SaveDC(wParam);
                PaintClient(wParam, client);
                Win32.RestoreDC(wParam, saved);
                return IntPtr.Zero;
            }

            case Win32.WM_VSCROLL:
            {
                int code = wParam.ToInt32() & 0xFFFF;
                int line = S(24);
                int page = Math.Max(line, _viewportHeight - line);
                int target = _scrollY;
                switch (code)
                {
                    case Win32.SB_LINEUP: target -= line; break;
                    case Win32.SB_LINEDOWN: target += line; break;
                    case Win32.SB_PAGEUP: target -= page; break;
                    case Win32.SB_PAGEDOWN: target += page; break;
                    case Win32.SB_TOP: target = 0; break;
                    case Win32.SB_BOTTOM: target = _contentHeight; break;
                    case Win32.SB_THUMBTRACK:
                    {
                        // La position du curseur dépasse 16 bits sur un contenu long :
                        // la lire dans SCROLLINFO, jamais dans le mot haut de wParam.
                        var si = new Win32.SCROLLINFO
                        {
                            cbSize = (uint)Marshal.SizeOf<Win32.SCROLLINFO>(),
                            fMask = Win32.SIF_TRACKPOS
                        };
                        if (Win32.GetScrollInfo(_hWnd, Win32.SB_VERT, ref si))
                            target = si.nTrackPos;
                        break;
                    }
                }
                ScrollTo(target);
                return IntPtr.Zero;
            }

            case Win32.WM_MOUSEWHEEL:
            {
                if (_contentHeight <= _viewportHeight) break;
                int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                // Un cran de molette vaut WHEEL_DELTA (120) ; trois lignes par cran,
                // comme le défilement par défaut de Windows.
                ScrollTo(_scrollY - _scrollInput.ConsumeWheel(delta) * S(24) * 3);
                return IntPtr.Zero;
            }

            case Win32.WM_DPICHANGED:
            {
                _dpiScale = NativeWindow.DpiFromChange(_hWnd, wParam) / 96f;
                RecreateFonts();
                NativeWindow.MoveToSuggestedRect(_hWnd, lParam);
                // La taille suggérée par Windows applique le nouveau facteur à l'ancienne
                // hauteur : elle peut dépasser la zone de travail du nouvel écran. On
                // remesure derrière, sinon l'Écart 4 revient dès qu'on déplace la fenêtre
                // d'un écran 100 % vers un écran 150 %.
                FitWindowToContent();
                RepositionControls();
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                return IntPtr.Zero;
            }

            case Win32.WM_NOTIFY:
            {
                var nm = Marshal.PtrToStructure<Win32.NMHDR>(lParam);
                if (nm.hwndFrom == _hWndTabStrip && nm.code == Win32.TCN_SELCHANGE)
                {
                    int sel = (int)Win32.SendMessageW(_hWndTabStrip, Win32.TCM_GETCURSEL,
                        IntPtr.Zero, IntPtr.Zero);
                    if (sel >= 0 && sel <= 2) SetActiveTab((SettingsTab)sel);
                }
                else if (nm.code == NM_CUSTOMDRAW && IsPaperButton(nm.hwndFrom))
                {
                    return (IntPtr)OnButtonCustomDraw(lParam);
                }
                break;
            }

            case Win32.WM_DRAWITEM:
            {
                var dis = Marshal.PtrToStructure<Win32.DRAWITEMSTRUCT>(lParam);
                if (dis.CtlID == IDC_LIST_COMPAT)
                {
                    DrawCompatItem(dis);
                    return (IntPtr)1;
                }
                if (dis.CtlID == IDC_VALIDATION)
                {
                    DrawValidation(dis);
                    return (IntPtr)1;
                }
                break;
            }

            case WM_MEASUREITEM:
            {
                var mis = Marshal.PtrToStructure<MEASUREITEMSTRUCT>(lParam);
                if (mis.CtlID == IDC_LIST_COMPAT)
                {
                    mis.itemHeight = (uint)CompatRowHeight;
                    Marshal.StructureToPtr(mis, lParam, false);
                    return (IntPtr)1;
                }
                break;
            }

            case WM_CTLCOLORLISTBOX:
                if (lParam == _hWndCompatList)
                {
                    Win32.SetBkColor(wParam, CLR_SURFACE);
                    return _hSurfaceBrush;
                }
                break;

            case Win32.WM_COMMAND:
            {
                int id = wParam.ToInt32() & 0xFFFF;
                int code = (wParam.ToInt32() >> 16) & 0xFFFF;
                switch (id)
                {
                    // Revue du 2026-09-21, R5 : Entrée et Échap arrivent d'IsDialogMessageW
                    // sous forme de WM_COMMAND, jamais en WM_KEYDOWN. Entrée presse le bouton
                    // focalisé s'il y en a un ; Échap ferme. Les cases de raccourci gardent
                    // leur Échap à elles (DLGC_WANTALLKEYS), il n'arrive pas ici.
                    case DialogNavigation.IDOK:
                    {
                        IntPtr target = DialogNavigation.ButtonToPressOnEnter(id, Win32.GetFocus(), PushButtons());
                        if (target != IntPtr.Zero) Win32.SendMessageW(target, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                        break;
                    }
                    case DialogNavigation.IDCANCEL:
                        Close();
                        break;
                    case IDC_LINK_RESET:
                        if (code == 0)
                        {
                            int confirmResult = ShowMessageBox(_hWnd,
                                L.Settings_ConfirmResetShortcuts,
                                L.Settings_WindowTitle,
                                0x4 | 0x20); // MB_YESNO | MB_ICONQUESTION
                            // R45 : cette fenêtre n'est pas une boîte de dialogue. À la
                            // fermeture de la confirmation, Windows la réactive et
                            // DefWindowProc met le focus sur la fenêtre elle-même : rien
                            // n'était plus annoncé. Le rendre au bouton, Oui comme Non.
                            Win32.SetFocus(_hWndLinkReset);
                            if (confirmResult != 6) break; // IDYES = 6
                            _keyboardVk = 0x51;
                            _searchVk = 0x57;
                            ConfigManager.ShortcutVirtualKeyboardVk = _keyboardVk;
                            ConfigManager.ShortcutCharacterSearchVk = _searchVk;
                            RefreshShortcutTexts();
                            _keyboardValid = true;
                            _searchValid = true;
                            SetValidationMessage(L.Settings_ShortcutsReset);
                            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                            ShortcutChanged?.Invoke();
                        }
                        break;
                    case IDC_RESET_VIRTUAL_KEYBOARD_WINDOW:
                        if (code == 0)
                        {
                            ConfigManager.ClearWindowBounds(ConfigManager.VirtualKeyboardBoundsKey);
                            SetValidationMessage(L.Settings_VirtualKeyboardWindowReset);
                            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                        }
                        break;
                    case IDC_RESET_LESSONS_WINDOW:
                        if (code == 0)
                        {
                            ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
                            SetValidationMessage(L.Settings_LessonsWindowReset);
                            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                        }
                        break;
                    case IDC_RADIO_LANG_FR:
                        if (code == 0) ApplyLanguageChange("fr");
                        break;
                    case IDC_RADIO_LANG_EN:
                        if (code == 0) ApplyLanguageChange("en");
                        break;
                    case IDC_CHK_TRAINING:
                        if (code == 0)
                        {
                            bool enabled = Win32.SendMessageW(_hWndChkTraining, Win32.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)Win32.BST_CHECKED;
                            ConfigManager.SetTrainingEnabled(enabled);
                        }
                        break;
                    case IDC_LIST_COMPAT:
                        if (code == LBN_SELCHANGE) RefreshCompatSelectionUi();
                        break;
                    case IDC_BTN_COMPAT_ADD:
                        if (code == 0) OnCompatAdd();
                        break;
                    case IDC_BTN_COMPAT_REMOVE:
                        if (code == 0) ApplyCompatModeToSelection(null);
                        break;
                    case IDC_RADIO_COMPAT_AUTO:
                        if (code == 0) ApplyCompatModeToSelection(null);
                        break;
                    case IDC_RADIO_COMPAT_FORCEON:
                        if (code == 0) ApplyCompatModeToSelection("forceOn");
                        break;
                    case IDC_RADIO_COMPAT_FORCEOFF:
                        if (code == 0) ApplyCompatModeToSelection("forceOff");
                        break;
                }
                return IntPtr.Zero;
            }

            case Win32.WM_CTLCOLORSTATIC:
            {
                // Lignes « Géré par votre organisation » : texte secondaire sur le papier.
                IntPtr hdcStatic = wParam;
                IntPtr hCtrl = lParam;
                if (hCtrl == _hWndManagedNotifications || hCtrl == _hWndManagedOnboarding ||
                    hCtrl == _hWndManagedLanguage)
                {
                    Win32.SetBkMode(hdcStatic, 1);
                    Win32.SetTextColor(hdcStatic, CLR_MUTED);
                    return _hBgBrush;
                }
                break;
            }

            case Win32.WM_SETCURSOR:
                if (wParam == _hWndLinkReset)
                {
                    Win32.SetCursor(Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32649));
                    return (IntPtr)1;
                }
                break;

            case Win32.WM_KEYDOWN:
            case Win32.WM_SYSKEYDOWN:
                if (_focusedShortcut == IntPtr.Zero && wParam == (IntPtr)VK_ESCAPE)
                {
                    Close();
                    return IntPtr.Zero;
                }
                break;

            case Win32.WM_CLOSE:
                Close();
                return IntPtr.Zero;
        }
        }
        catch (Exception ex)
        {
            ConfigManager.Log("Settings WndProc", ex);
        }

        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void LoadShortcutStateFromConfig()
    {
        _keyboardVk = ConfigManager.ShortcutVirtualKeyboardVk;
        _searchVk = ConfigManager.ShortcutCharacterSearchVk;
    }

    private void RefreshShortcutTexts()
    {
        RefreshShortcutText(_hWndEditKeyboard, _keyboardVk);
        RefreshShortcutText(_hWndEditSearch, _searchVk);
    }

    /// <summary>Écrit la touche dans sa case. « F12 » élargit la case : la mise en page suit.</summary>
    private void RefreshShortcutText(IntPtr hWndShortcut, uint vk)
    {
        if (hWndShortcut == _hWndEditKeyboard)
            _keyboardShownVk = vk;
        else if (hWndShortcut == _hWndEditSearch)
            _searchShownVk = vk;
        Win32.SetWindowTextW(hWndShortcut, ConfigManager.GetShortcutDisplayName(vk));
        if (_controls.Length > 0)
            RepositionControls();
        Win32.InvalidateRect(hWndShortcut, IntPtr.Zero, false);
    }

    /// <summary>Couleur du message : rouge pour un refus, texte secondaire pour une
    /// confirmation (le ✓ du pied prend la couleur de l'action). Elle ne dépend que du message
    /// affiché : un raccourci resté invalide ne teint plus en rouge le message d'un autre
    /// geste, et un refus ne s'affiche plus comme une confirmation.</summary>
    internal static uint ValidationTextColor(bool refused) => refused ? CLR_INVALID : CLR_MUTED;

    private void SetRefusalMessage(string text) => ShowValidationMessage(text, false, true);

    private void SetValidationMessage(string text, bool captureHint = false)
        => ShowValidationMessage(text, captureHint, false);

    /// <summary>Hauteur du message sous les raccourcis ou les modes, sur autant de lignes qu'il
    /// en faut (décision d'Antoine du 26/09 : le refus de compatibilité jeu tient sur deux
    /// lignes en français à 100 %). Seul l'onglet affiché porte un message.</summary>
    private int ValidationTextHeight(SettingsTab tab, int width, int oneLine)
    {
        if (tab != _activeTab || string.IsNullOrEmpty(_validationMessage) || _hWnd == IntPtr.Zero)
            return oneLine;
        return Math.Max(oneLine, MessageHeight(_validationMessage, width));
    }

    /// <summary>Hauteur d'un message de 13 px coupé dans <paramref name="width"/>.</summary>
    private int MessageHeight(string text, int width)
    {
        if (_hWnd == IntPtr.Zero || string.IsNullOrEmpty(text)) return MessageLineHeight;
        IntPtr hdc = Win32.GetDC(_hWnd);
        try
        {
            return Math.Max(MessageLineHeight, PaperText.DrawParagraph(hdc, new[] { new TextRun(text) },
                FontSmall, FontSmall, Rect(0, 0, width, 0), CLR_MUTED, CLR_MUTED, MessageLineHeight, draw: false));
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }
    }

    /// <summary>Un message neuf peut changer le nombre de lignes : il remesure. Seul le
    /// même texte réaffiché, ou un effacement sur une ligne déjà vide, ne remesure rien.</summary>
    internal static bool ValidationNeedsRelayout(string current, string text)
        => string.IsNullOrEmpty(current) != string.IsNullOrEmpty(text)
           || (!string.IsNullOrEmpty(text) && text != current);

    private void ShowValidationMessage(string text, bool captureHint, bool refused)
    {
        var placeBefore = CurrentMessagePlace();
        bool rowChanged = ValidationNeedsRelayout(_validationMessage, text);
        _validationMessage = text;
        _validationRefused = refused;
        _showCaptureHint = captureHint;
        rowChanged |= placeBefore != CurrentMessagePlace();
        Win32.SetWindowTextW(_hWndValidation, text);
        if (rowChanged && _hWnd != IntPtr.Zero && _hWndValidation != IntPtr.Zero)
        {
            // Le message fait grandir le contenu, parfois de deux lignes. Sans remesure,
            // le message débordait sans que le défilement s'arme : le bas du contenu
            // devenait inatteignable (R12 de la revue du 2026-09-21). Un message qui en
            // remplace un autre peut changer de place ou de nombre de lignes (26/09).
            FitWindowToContent();
            RepositionControls();
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
        }
        if (_hWndValidation != IntPtr.Zero)
            Win32.InvalidateRect(_hWndValidation, IntPtr.Zero, false);
    }

    private void ClearCaptureHintIfVisible()
    {
        if (_showCaptureHint)
            SetValidationMessage(string.Empty);
    }

    private uint ShownVk(bool keyboard) => keyboard
        ? (_keyboardShownVk != 0 ? _keyboardShownVk : _keyboardVk)
        : (_searchShownVk != 0 ? _searchShownVk : _searchVk);

    private void SetShortcutValidity(IntPtr hWndShortcut, bool valid)
    {
        if (hWndShortcut == _hWndEditKeyboard)
            _keyboardValid = valid;
        else if (hWndShortcut == _hWndEditSearch)
            _searchValid = valid;
        if (hWndShortcut != IntPtr.Zero)
            Win32.InvalidateRect(hWndShortcut, IntPtr.Zero, false);
    }

    /// <summary>Crée la ligne « Géré par votre organisation » d'un réglage. Toujours créée,
    /// visible seulement si le réglage est effectivement imposé : le contrôle existe donc
    /// dans les deux cas, et la mise en page lui donne une hauteur nulle quand il est
    /// masqué.</summary>
    private IntPtr CreateManagedNotice(IntPtr hInstance, bool visible)
    {
        uint style = Win32.WS_CHILD;
        if (visible)
            style |= Win32.WS_VISIBLE;

        return Win32.CreateWindowExW(0, "STATIC", L.Settings_ManagedByOrganization,
            style, 0, 0, 0, 0,
            _hWnd, IntPtr.Zero, hInstance, IntPtr.Zero);
    }

    private void RefreshAutoStartCheckbox()
    {
        Win32.SendMessageW(_hWndChkAutoStart, Win32.BM_SETCHECK,
            AutoStart.IsRegistered ? (IntPtr)Win32.BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
    }

    private void RefreshLanguageRadios()
    {
        bool isEnglish = ConfigManager.AppLanguage == "en";
        Win32.SendMessageW(_hWndRadioLangFr, Win32.BM_SETCHECK, isEnglish ? IntPtr.Zero : (IntPtr)Win32.BST_CHECKED, IntPtr.Zero);
        Win32.SendMessageW(_hWndRadioLangEn, Win32.BM_SETCHECK, isEnglish ? (IntPtr)Win32.BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// Applique un changement de langue depuis les radios : persiste le choix et met à jour
    /// L.Language. Le rafraîchissement de cette fenêtre passe par l'abonnement à
    /// AppLanguageChanged (OnLanguageChanged), déclenché par SetAppLanguage — même chemin
    /// que pour une bascule initiée depuis le menu tray ou la fenêtre de bienvenue.
    /// </summary>
    private void ApplyLanguageChange(string lang)
    {
        if (ConfigManager.AppLanguage == lang) return;
        L.Language = lang; // avant SetAppLanguage : les abonnés à AppLanguageChanged lisent L.*
        ConfigManager.SetAppLanguage(lang);
    }

    /// <summary>Rafraîchit libellés et radios après un changement de langue, quelle qu'en
    /// soit l'origine (radios de cette fenêtre, menu tray, fenêtre de bienvenue).</summary>
    private void OnLanguageChanged()
    {
        RefreshLanguageTexts();
        RefreshLanguageRadios();
        // Les libellés traduits changent de largeur et de hauteur : remesurer, sinon la
        // fenêtre garde la hauteur de l'autre langue et le bas du contenu redevient
        // inatteignable.
        FitWindowToContent();
        RepositionControls();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
        if (_hWndTabStrip != IntPtr.Zero)
            Win32.InvalidateRect(_hWndTabStrip, IntPtr.Zero, false);
    }

    /// <summary>Le sous-titre peint sous chaque mode de compatibilité devient la description
    /// accessible de son bouton radio : un lecteur d'écran le lit après le libellé.</summary>
    private void SetCompatDescriptions()
    {
        AccessibleName.TrySetDescription(_hWndRadioCompatAuto, L.SettingsPaper_CompatHintAuto);
        AccessibleName.TrySetDescription(_hWndRadioCompatForceOn, L.SettingsPaper_CompatHintForceOn);
        AccessibleName.TrySetDescription(_hWndRadioCompatForceOff, L.SettingsPaper_CompatHintForceOff);
    }

    /// <summary>Même motif pour les sous-titres des onglets « Général » et « Langue » (lot
    /// « clarté ») : chaque réglage décrit est lu avec ce qu'il change.</summary>
    private void SetSettingDescriptions()
    {
        AccessibleName.TrySetDescription(_hWndEditKeyboard, L.SettingsClarity_HintKeyboardShortcut);
        AccessibleName.TrySetDescription(_hWndEditSearch, L.SettingsClarity_HintSearchShortcut);
        AccessibleName.TrySetDescription(_hWndChkAutoStart, L.SettingsClarity_HintAutoStart);
        AccessibleName.TrySetDescription(_hWndChkNotifications, L.SettingsClarity_HintNotifications);
        AccessibleName.TrySetDescription(_hWndChkOnboarding, L.SettingsClarity_HintOnboarding);
        AccessibleName.TrySetDescription(_hWndResetVirtualKeyboardWindow, L.SettingsClarity_HintResetWindow);
        AccessibleName.TrySetDescription(_hWndResetLessonsWindow, L.SettingsClarity_HintResetWindow);
    }

    /// <summary>Réapplique tous les libellés traduits de cette fenêtre (appelé après un changement de langue).</summary>
    private void RefreshLanguageTexts()
    {
        Win32.SetWindowTextW(_hWnd, L.Settings_WindowTitle);
        // La bande d'onglets n'est pas un contrôle à texte de fenêtre : ses trois libellés
        // vivent dans le contrôle onglet et ne se réécrivent qu'avec TCM_SETITEMW. Ils
        // étaient les seuls oubliés de cette fonction.
        SetTabText(0, L.Settings_TabGeneral);
        SetTabText(1, L.Settings_TabApplications);
        SetTabText(2, L.Settings_TabLanguageMaintenance);
        _metrics = null; // largeurs des libellés et de la bande, dans la nouvelle langue
        foreach (var control in _controls)
            if (control.Text != null)
                Win32.SetWindowTextW(control.Handle, control.Text());
        SetCompatDescriptions();
        SetSettingDescriptions();
        RefreshCompatList(SelectedCompatProcess()); // libellés de mode traduits dans la liste
        RefreshShortcutTexts();
        SetValidationMessage(string.Empty);
    }

    // ═══════════════════════════════════════════════════════════════
    // Section « Apps suspendues » (v1.2.0) — overrides de compatibilité
    // ═══════════════════════════════════════════════════════════════

    private string? SelectedCompatProcess()
    {
        int sel = (int)Win32.SendMessageW(_hWndCompatList, LB_GETCURSEL, IntPtr.Zero, IntPtr.Zero);
        return sel >= 0 && sel < _compatProcesses.Count ? _compatProcesses[sel] : null;
    }

    /// <summary>Libellé court d'un mode dans la liste, à droite du nom du process.</summary>
    private static string CompatListLabel(string mode)
        => mode == "forceOn" ? L.Settings_CompatListForceOn : L.Settings_CompatListForceOff;

    /// <summary>
    /// Repeuple la listbox depuis config.json (tri alphabétique), restaure la sélection
    /// sur <paramref name="selectProcess"/> si présent, puis synchronise radios et boutons.
    /// Appelé à la création, à Show() (le tray a pu changer un override), après chaque
    /// action de la section et au changement de langue (libellés de mode).
    /// </summary>
    private void RefreshCompatList(string? selectProcess)
    {
        var overrides = ConfigManager.GetAllCompatibilityOverrides();
        _compatProcesses.Clear();
        _compatModes.Clear();
        _compatProcesses.AddRange(overrides.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));

        Win32.SendMessageW(_hWndCompatList, LB_RESETCONTENT, IntPtr.Zero, IntPtr.Zero);
        int selIndex = -1;
        for (int i = 0; i < _compatProcesses.Count; i++)
        {
            string name = _compatProcesses[i];
            _compatModes.Add(overrides[name]);
            // Le texte de la ligne reste entier pour les lecteurs d'écran ; le dessin écrit
            // le nom à gauche et le mode à droite.
            SendListBoxAddString(_hWndCompatList, $"{name} — {CompatListLabel(overrides[name])}");
            if (string.Equals(name, selectProcess, StringComparison.OrdinalIgnoreCase))
                selIndex = i;
        }
        if (selIndex >= 0)
            Win32.SendMessageW(_hWndCompatList, LB_SETCURSEL, (IntPtr)selIndex, IntPtr.Zero);
        RefreshCompatSelectionUi();
    }

    private static void SendListBoxAddString(IntPtr hList, string text)
    {
        IntPtr ptr = Marshal.StringToHGlobalUni(text);
        try { Win32.SendMessageW(hList, LB_ADDSTRING, IntPtr.Zero, ptr); }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    /// <summary>Synchronise radios et bouton Retirer avec l'entrée sélectionnée
    /// (radios décochées et grisées quand rien n'est sélectionné), et le libellé
    /// « Mode pour … » qui les surmonte.</summary>
    private void RefreshCompatSelectionUi()
    {
        string? proc = SelectedCompatProcess();
        string? mode = proc != null ? ConfigManager.GetCompatibilityOverride(proc) : null;
        bool hasSelection = proc != null;

        Win32.EnableWindow(_hWndCompatRemove, hasSelection);
        Win32.EnableWindow(_hWndRadioCompatAuto, hasSelection);
        Win32.EnableWindow(_hWndRadioCompatForceOn, hasSelection);
        Win32.EnableWindow(_hWndRadioCompatForceOff, hasSelection);

        Win32.SendMessageW(_hWndRadioCompatAuto, Win32.BM_SETCHECK,
            hasSelection && mode == null ? (IntPtr)Win32.BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
        Win32.SendMessageW(_hWndRadioCompatForceOn, Win32.BM_SETCHECK,
            mode == "forceOn" ? (IntPtr)Win32.BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
        Win32.SendMessageW(_hWndRadioCompatForceOff, Win32.BM_SETCHECK,
            mode == "forceOff" ? (IntPtr)Win32.BST_CHECKED : IntPtr.Zero, IntPtr.Zero);

        if (_hWnd != IntPtr.Zero && _activeTab == SettingsTab.Applications)
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    /// <summary>
    /// Applique un mode à l'entrée sélectionnée. Auto (mode null) retire l'override —
    /// l'entrée disparaît de la liste (elle n'existe que par son override). Même garde
    /// sécurité que le sous-menu tray : jamais de forceOn sur un process anti-cheat ou
    /// de connexion à distance (chemin complet inconnu ici → garde par nom seul).
    /// </summary>
    private void ApplyCompatModeToSelection(string? mode)
    {
        string? proc = SelectedCompatProcess();
        if (proc == null) return;
        if (ConfigManager.GetCompatibilityOverride(proc) == mode && mode != null) return;

        if (mode == "forceOn" &&
            (GameRegistry.IsAntiCheatProcess(proc, null) || GameRegistry.IsRemoteAccessProcess(proc)))
        {
            // Le refus dit pourquoi et ce qui se passe à la place (lot « clarté ») : la
            // connexion à distance est testée la première, comme dans ForegroundMonitor.
            SetRefusalMessage(GameRegistry.IsRemoteAccessProcess(proc)
                ? L.SettingsClarity_CompatRefusedRemote
                : L.SettingsClarity_CompatRefusedAntiCheat);
            RefreshCompatSelectionUi(); // re-cocher le radio du mode réel
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
            return;
        }

        ConfigManager.SetCompatibilityOverride(proc, mode);
        RefreshCompatList(mode == null ? null : proc);
        SetValidationMessage(mode == null
            ? L.Settings_CompatRemoved(proc)
            : L.Settings_CompatUpdated(proc));
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
        CompatibilityOverridesChanged?.Invoke();
    }

    /// <summary>
    /// Bouton Ajouter… : sélecteur de fichier .exe → l'override est créé sur le NOM du
    /// process (la détection foreground compare des noms, pas des chemins), en mode
    /// « désactivée » par défaut (c'est la section Apps suspendues) — ajustable ensuite
    /// via les radios. forceOff est toujours sûr, y compris pour un process anti-cheat.
    /// </summary>
    private void OnCompatAdd()
    {
        const int bufferChars = 1024;
        IntPtr buffer = Marshal.AllocHGlobal(bufferChars * 2);
        try
        {
            Marshal.WriteInt16(buffer, 0);
            var ofn = new Win32.OPENFILENAMEW
            {
                lStructSize = Marshal.SizeOf<Win32.OPENFILENAMEW>(),
                hwndOwner = _hWnd,
                lpstrFilter = L.Settings_CompatFilterExe + "\0*.exe\0\0",
                nFilterIndex = 1,
                lpstrFile = buffer,
                nMaxFile = bufferChars,
                lpstrTitle = L.Settings_CompatPickerTitle,
                Flags = Win32.OFN_FILEMUSTEXIST | Win32.OFN_PATHMUSTEXIST |
                        Win32.OFN_HIDEREADONLY | Win32.OFN_NOCHANGEDIR,
            };
            if (!Win32.GetOpenFileNameW(ref ofn)) return; // annulé

            string? path = Marshal.PtrToStringUni(buffer);
            if (string.IsNullOrEmpty(path)) return;
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(name)) return;

            ConfigManager.SetCompatibilityOverride(name, "forceOff");
            RefreshCompatList(name);
            SetValidationMessage(L.Settings_CompatAdded(name));
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
            CompatibilityOverridesChanged?.Invoke();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>Boîte modale de Windows. Les témoins la remplacent : aucun test n'ouvre de
    /// vraie boîte sur le poste.</summary>
    internal static Func<IntPtr, string, string, uint, int> ShowMessageBox = Win32.MessageBoxW;

    private void ShowAutoStartError()
    {
        Win32.MessageBoxW(_hWnd,
            AutoStart.GetFailureMessage(),
            L.Common_ErrorTitle, 0x10);
    }

    private bool IsModifierVirtualKey(uint vk)
    {
        return vk is 0x10 or 0x11 or 0x12 or 0x14 or 0x5B or 0x5C or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;
    }

    private static bool IsPausedInputMessage(uint msg)
    {
        return msg is Win32.WM_KEYDOWN or Win32.WM_KEYUP or Win32.WM_SYSKEYDOWN or Win32.WM_SYSKEYUP
            or Win32.WM_CHAR or Win32.WM_SYSCHAR or Win32.WM_SYSDEADCHAR
            or Win32.WM_COMMAND or Win32.WM_PASTE or Win32.WM_CUT or Win32.WM_CLEAR or Win32.WM_UNDO;
    }

    private void CancelShortcutCapture(IntPtr hWndShortcut)
    {
        if (hWndShortcut == _hWndEditKeyboard)
        {
            _keyboardValid = true;
            RefreshShortcutText(hWndShortcut, _keyboardVk);
        }
        else
        {
            _searchValid = true;
            RefreshShortcutText(hWndShortcut, _searchVk);
        }

        // L'invite ou le refus de raccourci disparaît : la touche d'avant est rendue.
        if (ShortcutCaptureMessageShown)
            SetValidationMessage(string.Empty);
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    /// <summary>L'invite de capture ou un refus de raccourci est affiché sous les raccourcis :
    /// Échap annule alors la saisie au lieu de fermer la fenêtre. Avant le lot « clarté », un
    /// refus effaçait l'invite, et Échap fermait la fenêtre au lieu d'annuler.</summary>
    private bool ShortcutCaptureMessageShown =>
        _showCaptureHint || (_validationRefused && CurrentMessagePlace() == MessagePlace.Shortcuts);

    private void ApplyCapturedShortcut(IntPtr hWndShortcut, uint vk)
    {
        bool isKeyboard = hWndShortcut == _hWndEditKeyboard;
        uint otherVk = isKeyboard ? _searchVk : _keyboardVk;

        // Lot « clarté » : chaque refus dit pourquoi, à la place de l'invite. La case montre
        // la touche refusée, en rouge, celle que nomme le message (QCM du 07/10) ; la touche
        // en place revient à Échap ou à la perte du focus.
        if (ShortcutRefusal(vk) is string refusal)
        {
            SetShortcutValidity(hWndShortcut, false);
            RefreshShortcutText(hWndShortcut, vk);
            SetRefusalMessage(refusal);
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
            return;
        }

        if (vk == otherVk)
        {
            SetShortcutValidity(hWndShortcut, false);
            RefreshShortcutText(hWndShortcut, vk);
            string key = ConfigManager.GetShortcutDisplayName(vk);
            SetRefusalMessage(isKeyboard
                ? L.SettingsClarity_ShortcutTakenBySearch(key)
                : L.SettingsClarity_ShortcutTakenByKeyboard(key));
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
            return;
        }

        SetShortcutValidity(hWndShortcut, true);

        if (isKeyboard)
        {
            _keyboardVk = vk;
            ConfigManager.ShortcutVirtualKeyboardVk = vk;
            RefreshShortcutText(hWndShortcut, _keyboardVk);
            SetValidationMessage(L.Settings_ShortcutKeyboardUpdated);
        }
        else
        {
            _searchVk = vk;
            ConfigManager.ShortcutCharacterSearchVk = vk;
            RefreshShortcutText(hWndShortcut, _searchVk);
            SetValidationMessage(L.Settings_ShortcutSearchUpdated);
        }

        ShortcutChanged?.Invoke();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    private IntPtr ShortcutSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (_inputPaused && IsPausedInputMessage(msg))
            return IntPtr.Zero;

        switch (msg)
        {
            case Win32.WM_GETDLGCODE:
            {
                IntPtr baseResult = Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
                return (IntPtr)DialogNavigation.DialogCodeKeepingTab(baseResult.ToInt64(), lParam);
            }

            // La case est dessinée par la fenêtre (PaintShortcutBox) : ni fond système, ni
            // sélection, ni curseur texte, que l'EDIT dessinerait par-dessus.
            case Win32.WM_PAINT:
                PaintShortcutBox(hWnd);
                return IntPtr.Zero;

            case Win32.WM_ERASEBKGND:
                return (IntPtr)1;

            case EM_SETSEL:
            case Win32.WM_LBUTTONDBLCLK:
            case Win32.WM_LBUTTONUP:
            case Win32.WM_MOUSEMOVE:
                return IntPtr.Zero;

            case Win32.WM_LBUTTONDOWN:
                Win32.SetFocus(hWnd);
                return IntPtr.Zero;

            case Win32.WM_SETFOCUS:
            {
                _focusedShortcut = hWnd;
                SetShortcutValidity(hWnd, true);
                SetValidationMessage(ShortcutCaptureHint, true);
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                IntPtr result = Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
                HideCaret(hWnd);
                Win32.InvalidateRect(hWnd, IntPtr.Zero, false);
                return result;
            }

            case Win32.WM_KILLFOCUS:
            {
                if (_focusedShortcut == hWnd)
                    _focusedShortcut = IntPtr.Zero;
                SetShortcutValidity(hWnd, true);
                RefreshShortcutText(hWnd, hWnd == _hWndEditKeyboard ? _keyboardVk : _searchVk);
                ClearCaptureHintIfVisible();
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                IntPtr result = Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
                Win32.InvalidateRect(hWnd, IntPtr.Zero, false);
                return result;
            }

            case Win32.WM_CHAR:
            case Win32.WM_PASTE:
            case Win32.WM_CUT:
            case Win32.WM_CLEAR:
            case Win32.WM_UNDO:
            case Win32.WM_CONTEXTMENU:
                return IntPtr.Zero;

            case Win32.WM_KEYDOWN:
            case Win32.WM_SYSKEYDOWN:
            {
                int vk = wParam.ToInt32();
                if ((lParam.ToInt64() & 0x40000000L) != 0)
                    return IntPtr.Zero;

                if (vk == VK_TAB)
                    return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);

                if (vk == VK_ESCAPE)
                {
                    if (ShortcutCaptureMessageShown)
                        CancelShortcutCapture(hWnd);
                    else
                        Close();
                    return IntPtr.Zero;
                }

                if (IsModifierVirtualKey((uint)vk))
                    return IntPtr.Zero;

                ApplyCapturedShortcut(hWnd, (uint)vk);
                return IntPtr.Zero;
            }
        }

        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_onAppLanguageChanged != null)
            ConfigManager.AppLanguageChanged -= _onAppLanguageChanged;
        if (_hWndEditKeyboard != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndEditKeyboard, _shortcutSubclassProc, (UIntPtr)3);
        if (_hWndEditSearch != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndEditSearch, _shortcutSubclassProc, (UIntPtr)4);
        if (_hWndTabStrip != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndTabStrip, _tabSubclassProc, (UIntPtr)5);
        if (_hWnd != IntPtr.Zero)
        {
            // AG130-40 : desinscrire AVANT de detruire — Windows recycle les HWND.
            DialogNavigation.Unregister(_hWnd);
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }

        Win32.DeleteObject(_hSurfaceBrush);

        if (_gdipToken != IntPtr.Zero)
        {
            Win32.GdiplusShutdown(_gdipToken);
            _gdipToken = IntPtr.Zero;
        }

        NativeWindow.UnregisterClass(ProductIdentity.WindowClass("Settings"));
    }
}
