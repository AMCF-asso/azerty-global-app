using System.Runtime.InteropServices;
using System.Text;
namespace AZERTYGlobal;

sealed class SettingsWindow : IDisposable
{
    private const uint BS_AUTOCHECKBOX = 0x0003;
    private const uint BS_AUTORADIOBUTTON = 0x0009;
    private const uint WS_GROUP = 0x00020000;
    private const uint BM_GETCHECK = 0x00F0;
    private const uint BM_SETCHECK = 0x00F1;
    private const uint BM_CLICK = 0x00F5;
    private const uint BST_CHECKED = 0x0001;
    private const uint ES_AUTOHSCROLL = 0x0080;
    private const uint ES_CENTER = 0x0001;
    private const uint ES_UPPERCASE = 0x0008;
    private const uint EM_SETREADONLY = 0x00CF;

    private const int VK_TAB = 0x09;
    private const int VK_ESCAPE = 0x1B;
    private const uint CLR_KEY_BORDER_FOCUS = 0x000078D4;
    private static string ShortcutCaptureHint => L.Settings_ShortcutCaptureHint;

    private const int IDC_TAB_STRIP = 3120;
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

    // Listbox Win32 (section Apps suspendues)
    private const uint LBS_NOTIFY = 0x0001;
    private const uint LBS_NOINTEGRALHEIGHT = 0x0100;
    private const uint LB_ADDSTRING = 0x0180;
    private const uint LB_RESETCONTENT = 0x0184;
    private const uint LB_SETCURSEL = 0x0186;
    private const uint LB_GETCURSEL = 0x0188;
    private const int LBN_SELCHANGE = 1;
    private const uint WS_VSCROLL = 0x00200000;
    private const uint WS_BORDER = 0x00800000;

    // 240 → 300 le 2026-07-16 (smoke test) : « Virtual keyboard » était tronqué en EN
    // et la fenêtre était disproportionnée (deux fois plus haute que large).
    // 470 → 680 le 2026-07-30 : section « Apps suspendues » + opt-in Défi du jour (v1.2.0).
    private const int BASE_WIN_W = 300;
    private const int BASE_WIN_H = 680;

    private const uint CLR_BG = 0x00DDDDDD;
    private const uint CLR_TITLE = 0x00201C18;
    private const uint CLR_TEXT = 0x00333333;
    private const uint CLR_MUTED = 0x00666666;
    private const uint CLR_VERSION = 0x00888888;
    private const uint CLR_PANEL_BG = 0x00EEEEEE;
    private const uint CLR_PANEL_BORDER = 0x00D1D1D1;
    private const uint CLR_LINK = 0x00D47800;
    private const uint CLR_INLINE_HIGHLIGHT = 0x000078D4;
    private const uint CLR_VALID = 0x00228B22;
    private const uint CLR_INVALID = 0x000000CC;
    private const uint CLR_KEY_BG = 0x00FAFAFA;
    private const uint CLR_KEY_BORDER = 0x00CBCBCB;
    private const uint CLR_KEY_BORDER_INVALID = 0x00A8A8FF;
    private const uint CLR_SEPARATOR = 0x00D7D7D7;

    private struct LayoutInfo
    {
        public int Margin;
        public Win32.RECT TabStripRect;
        public int HeaderTitleX;
        public int HeaderTitleY;
        public int HeaderDividerY;
        public Win32.RECT LogoRect;
        public Win32.RECT ShortcutsPanel;
        public int ShortcutsLabelX;
        public int ShortcutsLabelWidth;
        public int ShortcutsShortcutX;
        public int ShortcutsShortcutWidth;
        public int KeyboardRowY;
        public int SearchRowY;
        public Win32.RECT KeyboardBoxRect;
        public Win32.RECT SearchBoxRect;
        public Win32.RECT KeyboardEditRect;
        public Win32.RECT SearchEditRect;
        public Win32.RECT ValidationRect;
        public Win32.RECT ResetRect;
        public Win32.RECT PreferencesPanel;
        public Win32.RECT AutoStartRect;
        public Win32.RECT NotificationsRect;
        public Win32.RECT ManagedNotificationsRect;
        public Win32.RECT OnboardingRect;
        public Win32.RECT ManagedOnboardingRect;
        public Win32.RECT TrainingRect;
        public Win32.RECT LanguagePanel;
        public Win32.RECT LanguageFrRect;
        public Win32.RECT LanguageEnRect;
        public Win32.RECT ManagedLanguageRect;
        public Win32.RECT WindowsPanel;
        public Win32.RECT ResetVirtualKeyboardWindowRect;
        public Win32.RECT ResetLessonsWindowRect;
        public Win32.RECT CompatPanel;
        public Win32.RECT CompatListRect;
        public Win32.RECT CompatAddRect;
        public Win32.RECT CompatRemoveRect;
        public Win32.RECT CompatAutoRect;
        public Win32.RECT CompatForceOnRect;
        public Win32.RECT CompatForceOffRect;
        // Hauteur totale du contenu, marge basse comprise, avant tout défilement.
        // C'est elle qui décide de la hauteur de la fenêtre : le modèle v2.0.0 veut
        // une fenêtre qui mesure son contenu, jamais une constante (audit du
        // 2026-08-28, § 17 — BASE_WIN_H était devenue fausse à chaque section ajoutée).
        public int ContentHeight;
        // GuideRect et CloseButtonRect retirés — la croix système suffit
    }

    /// <summary>Les trois onglets de la fenêtre. L'ordre est celui de la bande.</summary>
    private enum SettingsTab { General = 0, Applications = 1, LanguageMaintenance = 2 }

    // Onglet affiché. Volontairement NON persisté : la fenêtre rouvre toujours sur
    // « Général », qui porte ce qu'on vient changer neuf fois sur dix. Mémoriser le
    // dernier onglet ferait rouvrir sur « Langue » après un unique passage.
    private SettingsTab _activeTab = SettingsTab.General;
    private IntPtr _hWndTabStrip;

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
    // Noms de process affichés dans la listbox, dans l'ordre des index de la liste
    private readonly List<string> _compatProcesses = new();

    private readonly Win32.WNDPROC _wndProcDelegate;
    private readonly Win32.SUBCLASSPROC _shortcutSubclassProc;
    private IntPtr _focusedShortcut;

    private readonly IntPtr _hBgBrush;
    private readonly IntPtr _hPanelBrush;
    private readonly IntPtr _hKeyBrush;

    private IntPtr _gdipToken;
    private IntPtr _gdipLogo;

    private bool _visible;
    private bool _inputPaused;
    private bool _keyboardValid = true;
    private bool _searchValid = true;
    private uint _keyboardVk;
    private uint _searchVk;
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

    private IntPtr _hFontTitle;
    private IntPtr _hFontVersion;
    private IntPtr _hFontPanelTitle;
    private IntPtr _hFontText;
    private IntPtr _hFontBold;
    private IntPtr _hFontEdit;
    private IntPtr _hFontLinkStrong;
    private IntPtr _hFontSmall;
    private IntPtr _hFontButton;

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
        _hBgBrush = Win32.CreateSolidBrush(CLR_BG);
        _hPanelBrush = Win32.CreateSolidBrush(CLR_PANEL_BG);
        _hKeyBrush = Win32.CreateSolidBrush(CLR_KEY_BG);

        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        int dpi = Win32.GetDeviceCaps(hdcScreen, 88);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        _dpiScale = dpi / 96f;

        var gdipInput = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        Win32.GdiplusStartup(out _gdipToken, ref gdipInput, IntPtr.Zero);
        _gdipLogo = GdiImageLoader.LoadFromEmbeddedResource(typeof(SettingsWindow), ProductIdentity.LogoResourceName);
        LoadShortcutStateFromConfig();

        CreateFonts();
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

        int realDpi = Win32.GetDpiForWindow(_hWnd);
        if (realDpi > 0 && Math.Abs(realDpi / 96f - _dpiScale) > 0.01f)
        {
            _dpiScale = realDpi / 96f;
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
    /// qu'avec les polices, et avec la langue pour le bouton de réinitialisation : GetLayout,
    /// appelé cinq fois par remesure, n'ouvre plus de DC pour les reprendre.</summary>
    private struct LineMetrics
    {
        public int Title, Version, PanelTitle, Text, Bold, Link, Small, ResetText;
    }

    private LineMetrics? _metrics; // remis à zéro par CreateFonts et RefreshLanguageTexts

    private LineMetrics Metrics => _metrics ??= MeasureLineMetrics();

    private LineMetrics MeasureLineMetrics()
    {
        IntPtr hdc = Win32.GetDC(_hWnd);
        try
        {
            return new LineMetrics
            {
                Title = MeasureSingleLineHeight(hdc, _hFontTitle),
                Version = MeasureSingleLineHeight(hdc, _hFontVersion),
                PanelTitle = MeasureSingleLineHeight(hdc, _hFontPanelTitle),
                Text = MeasureSingleLineHeight(hdc, _hFontText),
                Bold = MeasureSingleLineHeight(hdc, _hFontBold),
                Link = MeasureSingleLineHeight(hdc, _hFontLinkStrong),
                Small = MeasureSingleLineHeight(hdc, _hFontSmall),
                ResetText = MeasureSingleLineWidth(hdc, _hFontButton, L.Settings_LinkResetDefaults),
            };
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }
    }

    private void CreateFonts()
    {
        _metrics = null;
        _hFontTitle = Win32.CreateFontW(-S(18), 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontVersion = Win32.CreateFontW(-S(9), 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontPanelTitle = Win32.CreateFontW(-S(13), 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontText = Win32.CreateFontW(-S(11), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontBold = Win32.CreateFontW(-S(11), 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontEdit = Win32.CreateFontW(-S(13), 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontLinkStrong = Win32.CreateFontW(-S(11), 0, 0, 0, 700, 0, 1, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontSmall = Win32.CreateFontW(-S(9), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontButton = Win32.CreateFontW(-S(11), 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
    }

    private void DestroyFonts()
    {
        Win32.DeleteObject(_hFontTitle);
        Win32.DeleteObject(_hFontVersion);
        Win32.DeleteObject(_hFontPanelTitle);
        Win32.DeleteObject(_hFontText);
        Win32.DeleteObject(_hFontBold);
        Win32.DeleteObject(_hFontEdit);
        Win32.DeleteObject(_hFontLinkStrong);
        Win32.DeleteObject(_hFontSmall);
        Win32.DeleteObject(_hFontButton);
    }

    private void RecreateFonts()
    {
        DestroyFonts();
        CreateFonts();
        ApplyFontsToControls();
    }

    private void ApplyFontsToControls()
    {
        // RecreateFonts supprime les anciennes polices : chaque contrôle de la table reçoit
        // la nouvelle, bande d'onglets comprise.
        foreach (var control in _controls)
            if (control.Font != null)
                Win32.SendMessageW(control.Handle, Win32.WM_SETFONT, control.Font(), (IntPtr)1);
    }

    private void CreateMainWindow()
    {
        var hInstance = Win32.GetModuleHandleW(null);
        string className = ProductIdentity.WindowClass("Settings");

        var wc = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
            lpfnWndProc = _wndProcDelegate,
            hInstance = hInstance,
            hCursor = Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32512),
            hbrBackground = _hBgBrush,
            lpszClassName = className
        };
        Win32.RegisterClassExW(ref wc);

        int winW = S(BASE_WIN_W);
        int winH = S(BASE_WIN_H);
        // WS_VSCROLL est présent dès la création pour que la barre existe quand le
        // contenu dépasse la zone de travail ; FitWindowToContent la masque aussitôt
        // lorsque tout tient, ce qui est le cas courant (Écart 4).
        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU | WS_VSCROLL;
        var adjustRect = new Win32.RECT { left = 0, top = 0, right = winW, bottom = winH };
        Win32.AdjustWindowRectEx(ref adjustRect, dwStyle, false, 0);
        int windowW = adjustRect.right - adjustRect.left;
        int windowH = adjustRect.bottom - adjustRect.top;

        Win32.GetCursorPos(out var cursorPt);
        var hMonitor = Win32.MonitorFromPoint(cursorPt, 0x00000001);
        var monInfo = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        Win32.GetMonitorInfo(hMonitor, ref monInfo);
        int screenX = monInfo.rcWork.left;
        int screenY = monInfo.rcWork.top;
        int screenW = monInfo.rcWork.right - monInfo.rcWork.left;
        int screenH = monInfo.rcWork.bottom - monInfo.rcWork.top;

        _hWnd = Win32.CreateWindowExW(0, className, L.Settings_WindowTitle,
            dwStyle, screenX + (screenW - windowW) / 2, screenY + (screenH - windowH) / 2, windowW, windowH,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        // AG130-40 : cette fenetre veut Tab, Maj+Tab et Entree entre ses controles.
        DialogNavigation.Register(_hWnd, EnsureFocusVisible);
        Win32.EnableDarkTitleBar(_hWnd);
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

        CreateTabStrip(hInstance);

        _hWndLabelKeyboard = CreateHiddenLabel(hInstance, L.Settings_ShortcutLabelKeyboard);
        _hWndEditKeyboard = Win32.CreateWindowExW(0, "EDIT",
            ConfigManager.GetShortcutDisplayName(_keyboardVk),
            Win32.WS_CHILD | Win32.WS_VISIBLE | ES_AUTOHSCROLL | ES_CENTER | ES_UPPERCASE | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_EDIT_KEYBOARD, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndEditKeyboard, EM_SETREADONLY, (IntPtr)1, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndEditKeyboard, _shortcutSubclassProc, (UIntPtr)3, IntPtr.Zero);

        _hWndLabelSearch = CreateHiddenLabel(hInstance, L.Settings_ShortcutLabelSearch);
        _hWndEditSearch = Win32.CreateWindowExW(0, "EDIT",
            ConfigManager.GetShortcutDisplayName(_searchVk),
            Win32.WS_CHILD | Win32.WS_VISIBLE | ES_AUTOHSCROLL | ES_CENTER | ES_UPPERCASE | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_EDIT_SEARCH, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndEditSearch, EM_SETREADONLY, (IntPtr)1, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndEditSearch, _shortcutSubclassProc, (UIntPtr)4, IntPtr.Zero);

        _hWndValidation = Win32.CreateWindowExW(0, "STATIC", "",
            Win32.WS_CHILD | Win32.WS_VISIBLE,
            0, 0, 0, 0,
            _hWnd, IntPtr.Zero, hInstance, IntPtr.Zero);

        _hWndLinkReset = Win32.CreateWindowExW(0, "BUTTON", L.Settings_LinkResetDefaults,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LINK_RESET, hInstance, IntPtr.Zero);

        _hWndChkAutoStart = Win32.CreateWindowExW(0, "BUTTON", L.Settings_AutoStart,
            Win32.WS_CHILD | Win32.WS_VISIBLE | BS_AUTOCHECKBOX | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_CHK_AUTOSTART, hInstance, IntPtr.Zero);
        RefreshAutoStartCheckbox();

        _hWndChkNotifications = Win32.CreateWindowExW(0, "BUTTON", L.Settings_Notifications,
            Win32.WS_CHILD | Win32.WS_VISIBLE | BS_AUTOCHECKBOX | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_CHK_NOTIFICATIONS, hInstance, IntPtr.Zero);
        if (ConfigManager.NotificationsEnabled)
            Win32.SendMessageW(_hWndChkNotifications, BM_SETCHECK, (IntPtr)BST_CHECKED, IntPtr.Zero);

        _hWndChkOnboarding = Win32.CreateWindowExW(0, "BUTTON", L.Settings_OnboardingWindow,
            Win32.WS_CHILD | Win32.WS_VISIBLE | BS_AUTOCHECKBOX | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_CHK_ONBOARDING, hInstance, IntPtr.Zero);
        if (ConfigManager.ShowOnboardingAtStartup)
            Win32.SendMessageW(_hWndChkOnboarding, BM_SETCHECK, (IntPtr)BST_CHECKED, IntPtr.Zero);

        // Opt-in Défi du jour (v1.2.0) — décoché par défaut, appliqué immédiatement au
        // clic (pas à la fermeture) : l'entrée du menu tray et le module des leçons
        // dépendent de cet état.
        _hWndChkTraining = Win32.CreateWindowExW(0, "BUTTON", L.Challenge_OptIn,
            Win32.WS_CHILD | Win32.WS_VISIBLE | BS_AUTOCHECKBOX | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_CHK_TRAINING, hInstance, IntPtr.Zero);
        if (ConfigManager.TrainingEnabled)
            Win32.SendMessageW(_hWndChkTraining, BM_SETCHECK, (IntPtr)BST_CHECKED, IntPtr.Zero);

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
            Win32.WS_CHILD | Win32.WS_VISIBLE | BS_AUTORADIOBUTTON | WS_GROUP | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RADIO_LANG_FR, hInstance, IntPtr.Zero);

        _hWndRadioLangEn = Win32.CreateWindowExW(0, "BUTTON", "English",
            Win32.WS_CHILD | Win32.WS_VISIBLE | BS_AUTORADIOBUTTON | Win32.WS_TABSTOP,
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
        _hWndLabelCompat = CreateHiddenLabel(hInstance, L.Settings_SectionCompat);
        _hWndCompatList = Win32.CreateWindowExW(0, "LISTBOX", "",
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP |
            LBS_NOTIFY | LBS_NOINTEGRALHEIGHT | WS_VSCROLL | WS_BORDER,
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
            Win32.WS_CHILD | Win32.WS_VISIBLE | BS_AUTORADIOBUTTON | WS_GROUP | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RADIO_COMPAT_AUTO, hInstance, IntPtr.Zero);

        _hWndRadioCompatForceOn = Win32.CreateWindowExW(0, "BUTTON", L.Settings_CompatModeForceOn,
            Win32.WS_CHILD | Win32.WS_VISIBLE | BS_AUTORADIOBUTTON | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RADIO_COMPAT_FORCEON, hInstance, IntPtr.Zero);

        _hWndRadioCompatForceOff = Win32.CreateWindowExW(0, "BUTTON", L.Settings_CompatModeForceOff,
            Win32.WS_CHILD | Win32.WS_VISIBLE | BS_AUTORADIOBUTTON | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RADIO_COMPAT_FORCEOFF, hInstance, IntPtr.Zero);

        RefreshCompatList(selectProcess: null);
        _controls = DescribeControls();
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
            // ⛔ La bande d'onglets doit rester dans la table : RecreateFonts supprime les
            // anciennes polices, et un contrôle oublié garderait un HFONT détruit après un
            // changement de DPI (geste 40 de la recette VM). Ses libellés, eux, passent par
            // TCM_SETITEMW (SetTabText), pas par le texte de fenêtre.
            new(_hWndTabStrip, null, () => _hFontText, l => l.TabStripRect),
            // Étiquettes N8 jamais affichées : un texte, ni police ni place.
            new(_hWndLabelKeyboard, null, null, null, () => L.Settings_ShortcutLabelKeyboard),
            new(_hWndLabelSearch, null, null, null, () => L.Settings_ShortcutLabelSearch),
            new(_hWndLabelCompat, null, null, null, () => L.Settings_SectionCompat),

            new(_hWndEditKeyboard, General, () => _hFontEdit, l => l.KeyboardEditRect),
            new(_hWndEditSearch, General, () => _hFontEdit, l => l.SearchEditRect),
            // La ligne de validation sert les trois onglets : jamais masquée par onglet.
            new(_hWndValidation, null, () => _hFontSmall, l => l.ValidationRect),
            new(_hWndLinkReset, General, () => _hFontButton, l => l.ResetRect,
                () => L.Settings_LinkResetDefaults, PushButton: true),
            new(_hWndChkAutoStart, General, () => _hFontBold, l => l.AutoStartRect, () => L.Settings_AutoStart),
            new(_hWndChkNotifications, General, () => _hFontBold, l => l.NotificationsRect, () => L.Settings_Notifications),
            new(_hWndManagedNotifications, General, () => _hFontSmall, l => l.ManagedNotificationsRect,
                () => L.Settings_ManagedByOrganization, () => _managedNotifications),
            new(_hWndChkOnboarding, General, () => _hFontBold, l => l.OnboardingRect, () => L.Settings_OnboardingWindow),
            new(_hWndManagedOnboarding, General, () => _hFontSmall, l => l.ManagedOnboardingRect,
                () => L.Settings_ManagedByOrganization, () => _managedOnboarding),
            new(_hWndChkTraining, General, () => _hFontBold, l => l.TrainingRect,
                () => L.Challenge_OptIn, () => DailyChallenge.Enabled),

            new(_hWndCompatList, Apps, () => _hFontText, l => l.CompatListRect),
            new(_hWndCompatAdd, Apps, () => _hFontButton, l => l.CompatAddRect, () => L.Settings_CompatAdd, PushButton: true),
            new(_hWndCompatRemove, Apps, () => _hFontButton, l => l.CompatRemoveRect, () => L.Settings_CompatRemove, PushButton: true),
            new(_hWndRadioCompatAuto, Apps, () => _hFontBold, l => l.CompatAutoRect, () => L.Settings_CompatModeAuto),
            new(_hWndRadioCompatForceOn, Apps, () => _hFontBold, l => l.CompatForceOnRect, () => L.Settings_CompatModeForceOn),
            new(_hWndRadioCompatForceOff, Apps, () => _hFontBold, l => l.CompatForceOffRect, () => L.Settings_CompatModeForceOff),

            // Noms de langue : endonymes, jamais retraduits.
            new(_hWndRadioLangFr, LangMaint, () => _hFontBold, l => l.LanguageFrRect),
            new(_hWndRadioLangEn, LangMaint, () => _hFontBold, l => l.LanguageEnRect),
            new(_hWndManagedLanguage, LangMaint, () => _hFontSmall, l => l.ManagedLanguageRect,
                () => L.Settings_ManagedByOrganization, () => _managedLanguage),
            new(_hWndResetVirtualKeyboardWindow, LangMaint, () => _hFontButton, l => l.ResetVirtualKeyboardWindowRect,
                () => L.Settings_ResetVirtualKeyboard, PushButton: true),
            new(_hWndResetLessonsWindow, LangMaint, () => _hFontButton, l => l.ResetLessonsWindowRect,
                () => L.Settings_ResetLessonsModule, PushButton: true),
        };
    }

    /// <summary>
    /// Donne à la fenêtre la hauteur de son contenu, plafonnée à la zone de travail de
    /// l'écran qui la porte, et arme le défilement si le plafond a mordu.
    ///
    /// Écart 4 de la recette VM du 2026-09-19 : en 1366×768 à 150 %, BASE_WIN_H valait
    /// 680 × 1,5 = 1 020 px de client pour 728 px utiles — la fenêtre dépassait par le
    /// bas et le troisième bouton radio de compatibilité était inatteignable, sans
    /// aucun moyen de l'atteindre puisque la fenêtre n'est pas redimensionnable.
    /// Le modèle v2.0.0 (audit du 2026-08-28, § 17) pose la règle : aucune fenêtre ne
    /// dépasse la zone de travail, et une fenêtre mesure son contenu plutôt que de le
    /// supposer. BASE_WIN_H ne sert donc plus que d'amorce avant la première mesure.
    ///
    /// C4, décision d'Antoine du 2026-09-24 : la fenêtre changeait de taille à chaque
    /// changement d'onglet, ce qui dérangeait. Elle prend désormais toujours la taille de
    /// l'onglet « Général », ligne du message de validation comprise, pour que ce message
    /// ne la fasse pas grandir non plus. Un onglet plus court laisse du vide en bas ; un
    /// onglet plus haut défile. La mesure est refaite à chaque appel, donc à l'échelle
    /// courante : un changement de DPI ou de langue la recalcule, toujours sur « Général ».
    /// </summary>
    private void FitWindowToContent()
    {
        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;

        // Mesures sans défilement : la hauteur du contenu ne dépend pas de la position.
        int referenceHeight = MeasureContentHeight(SettingsTab.General, showValidationRow: true);
        int applicationsHeight = MeasureContentHeight(SettingsTab.Applications, showValidationRow: true);
        int languageHeight = MeasureContentHeight(SettingsTab.LanguageMaintenance, showValidationRow: true);
        _contentHeight = MeasureContentHeight(_activeTab, !string.IsNullOrEmpty(_validationMessage));

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
            referenceHeight, maxClientH, referenceHeight, applicationsHeight, languageHeight);
        _viewportHeight = clientH;
        _reserveScrollBar = reserveScrollBar;

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

    /// <summary>Hauteur de contenu d'un onglet, sans défilement, à l'échelle courante.</summary>
    private int MeasureContentHeight(SettingsTab tab, bool showValidationRow) =>
        GetLayout(S(BASE_WIN_W), tab, showValidationRow).ContentHeight;

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
    /// repond aux fleches gauche/droite sans une ligne de notre part.
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

        Win32.SendMessageW(_hWndTabStrip, Win32.WM_SETFONT, _hFontText, (IntPtr)1);
        InsertTab(0, L.Settings_TabGeneral);
        InsertTab(1, L.Settings_TabApplications);
        InsertTab(2, L.Settings_TabLanguageMaintenance);
        Win32.SendMessageW(_hWndTabStrip, Win32.TCM_SETCURSEL, (IntPtr)(int)_activeTab, IntPtr.Zero);
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
        _showCaptureHint = false;
        Win32.SetWindowTextW(_hWndValidation, string.Empty);
        // Le défilement d'un onglet n'a pas de sens dans le suivant : il est plus
        // court, et un décalage hérité laisserait la fenêtre ouverte sur du vide.
        _scrollY = 0;
        FitWindowToContent();
        RepositionControls();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    private LayoutInfo GetLayout(int winW) =>
        GetLayout(winW, _activeTab, !string.IsNullOrEmpty(_validationMessage));

    /// <summary>Mise en page d'un onglet donné. Les paramètres explicites permettent de
    /// mesurer un autre onglet que l'onglet affiché (taille fixe, C4). Seuls l'en-tête et
    /// l'onglet demandé sont calculés, chacun par sa fonction : les rectangles des autres
    /// onglets restent vides, leurs contrôles étant masqués par ApplyTabVisibility, jamais
    /// déplacés hors écran (audit du 25/09, F-04). Coordonnées de contenu : le défilement
    /// s'applique au placement des contrôles (Place) et au tracé (SetViewportOrgEx).</summary>
    private LayoutInfo GetLayout(int winW, SettingsTab tab, bool showValidationRow)
    {
        var m = Metrics;
        int margin = S(8);
        int contentWidth = winW - margin * 2;
        int headerTop = S(8);
        int logoSize = S(24);
        int panelPadX = S(9);
        int headerLineHeight = Math.Max(logoSize, Math.Max(m.Title, m.Version) + S(4));
        int headerBottom = headerTop + headerLineHeight + S(9);

        int labelX = margin + panelPadX;
        int labelWidth = S(120); // assez pour « Virtual keyboard » sans troncature
        int keyOuterW = S(28);
        int keyOuterH = S(24);
        int keyOuterX = margin + contentWidth - panelPadX - keyOuterW;
        int shortcutX = labelX + labelWidth + S(6);
        int innerWidth = contentWidth - panelPadX * 2;

        // Espacements communs aux trois onglets.
        int sectionTitleH = m.PanelTitle + S(9);
        int rowH = Math.Max(S(18), m.Bold);
        int rowGap = S(6);
        int managedHeight = Math.Max(S(13), m.Small);
        int managedIndent = S(18);
        int managedGap = S(2);
        int managedWidth = innerWidth - managedIndent;
        int buttonHeight = S(28);

        // ── Bande d'onglets ─────────────────────────────────────────────
        // Trois onglets au lieu d'une colonne de cinq sections. La fenêtre se cale
        // alors sur l'onglet le plus haut au lieu de la somme de tout. Découpage
        // décidé par Antoine le 2026-09-21 après mesure des hauteurs : Général
        // ~224 px, Applications ~196 px, Langue et maintenance ~175 px, contre
        // ~631 px cumulés. ⛔ « Langue » appartient au troisième onglet et non au
        // premier : avec elle, « Général » culminait à ~310 px et commandait seul
        // la hauteur, ce qui ramenait le défilement à 175 % sur 1366×768 — le cas
        // exact du geste 36 de la recette VM.
        var layout = new LayoutInfo
        {
            Margin = margin,
            TabStripRect = Rect(margin, headerBottom + S(4), contentWidth, S(24)),
            HeaderTitleX = margin + logoSize + S(6),
            HeaderTitleY = headerTop + Math.Max(0, (headerLineHeight - m.Title) / 2),
            HeaderDividerY = headerBottom,
            LogoRect = Rect(margin, headerTop + Math.Max(0, (headerLineHeight - logoSize) / 2), logoSize, logoSize),
            ShortcutsLabelX = labelX,
            ShortcutsLabelWidth = labelWidth,
            ShortcutsShortcutX = shortcutX,
            ShortcutsShortcutWidth = keyOuterX - shortcutX - S(8),
        };

        int contentTop = layout.TabStripRect.bottom + S(7);
        int contentBottom = tab switch
        {
            SettingsTab.General => LayoutGeneral(),
            SettingsTab.Applications => LayoutApplications(),
            _ => LayoutLanguageMaintenance(),
        };

        // Le panneau de fond couvre tout l'onglet ; chaque section en garde le bas.
        int panelBottom = contentBottom + S(12);
        layout.ShortcutsPanel = Rect(margin, contentTop, contentWidth, panelBottom - contentTop);
        CloseSection(ref layout.PreferencesPanel);
        CloseSection(ref layout.CompatPanel);
        CloseSection(ref layout.LanguagePanel);
        CloseSection(ref layout.WindowsPanel);
        layout.ContentHeight = panelBottom + margin;
        return layout;

        void CloseSection(ref Win32.RECT section)
        {
            if (section.right > section.left) section.bottom = panelBottom;
        }

        Win32.RECT Section(int top) => Rect(margin, top, contentWidth, 0);

        // Ligne du message de validation, sous le contrôle du geste qui l'a produit. Chaque
        // onglet a la sienne : les messages des onglets Applications et Langue s'écrivaient
        // dans une ligne que seul « Général » montrait (suite de l'audit du 25/09).
        int ValidationRow(int top)
        {
            int rowTop = showValidationRow ? top + S(5) : top;
            layout.ValidationRect = Rect(labelX, rowTop, innerWidth, showValidationRow ? Math.Max(S(15), m.Small) : 0);
            return layout.ValidationRect.bottom;
        }

        // ── Onglet « Général » : Raccourcis puis Préférences ────────────
        int LayoutGeneral()
        {
            int keyboardRowY = contentTop + S(30);
            int searchRowY = keyboardRowY + Math.Max(S(28), m.Text + S(11));
            layout.KeyboardRowY = keyboardRowY;
            layout.SearchRowY = searchRowY;
            layout.KeyboardBoxRect = Rect(keyOuterX, keyboardRowY - S(4), keyOuterW, keyOuterH);
            layout.SearchBoxRect = Rect(keyOuterX, searchRowY - S(4), keyOuterW, keyOuterH);
            layout.KeyboardEditRect = Rect(keyOuterX + 1, keyboardRowY - S(3), keyOuterW - 2, Math.Max(0, keyOuterH - 2));
            layout.SearchEditRect = Rect(keyOuterX + 1, searchRowY - S(3), keyOuterW - 2, Math.Max(0, keyOuterH - 2));
            int resetY = searchRowY + Math.Max(S(20), m.Text + S(7));
            int resetWidth = Math.Max(S(150), m.ResetText + S(24));
            layout.ResetRect = Rect(labelX, resetY, resetWidth, Math.Max(S(28), m.Link + S(10)));

            int prefsTitleTop = ValidationRow(layout.ResetRect.bottom) + S(10);
            layout.PreferencesPanel = Section(prefsTitleTop);
            layout.AutoStartRect = Rect(labelX, prefsTitleTop + sectionTitleH, innerWidth, rowH);
            layout.NotificationsRect = Rect(labelX, layout.AutoStartRect.bottom + rowGap, innerWidth, rowH);
            // Lignes « Géré par votre organisation » : sous la case, décalées de la largeur
            // de la coche pour s'aligner sur son libellé. Hauteur nulle quand rien n'est
            // imposé — la fenêtre est alors exactement celle d'avant le lot C.
            layout.ManagedNotificationsRect = Rect(labelX + managedIndent,
                layout.NotificationsRect.bottom + managedGap, managedWidth,
                _managedNotifications ? managedHeight : 0);
            layout.OnboardingRect = Rect(labelX,
                (_managedNotifications ? layout.ManagedNotificationsRect.bottom : layout.NotificationsRect.bottom) + rowGap,
                innerWidth, rowH);
            layout.ManagedOnboardingRect = Rect(labelX + managedIndent,
                layout.OnboardingRect.bottom + managedGap, managedWidth,
                _managedOnboarding ? managedHeight : 0);
            // Opt-in des rappels du Défi : ligne repliée tant que le Défi est masqué (1.3.0).
            layout.TrainingRect = Rect(labelX,
                (_managedOnboarding ? layout.ManagedOnboardingRect.bottom : layout.OnboardingRect.bottom)
                    + (DailyChallenge.Enabled ? rowGap : 0),
                innerWidth, DailyChallenge.Enabled ? rowH : 0);
            return layout.TrainingRect.bottom;
        }

        // ── Onglet « Applications » : apps suspendues ───────────────────
        int LayoutApplications()
        {
            layout.CompatPanel = Section(contentTop);
            layout.CompatListRect = Rect(labelX, contentTop + sectionTitleH, innerWidth, S(58));
            int compatBtnW = (innerWidth - S(6)) / 2;
            layout.CompatAddRect = Rect(labelX, layout.CompatListRect.bottom + S(6), compatBtnW, S(24));
            layout.CompatRemoveRect = Rect(labelX + compatBtnW + S(6), layout.CompatAddRect.top,
                innerWidth - compatBtnW - S(6), S(24));
            layout.CompatAutoRect = Rect(labelX, layout.CompatAddRect.bottom + S(8), innerWidth, rowH);
            layout.CompatForceOnRect = Rect(labelX, layout.CompatAutoRect.bottom + S(4), innerWidth, rowH);
            layout.CompatForceOffRect = Rect(labelX, layout.CompatForceOnRect.bottom + S(4), innerWidth, rowH);
            return ValidationRow(layout.CompatForceOffRect.bottom);
        }

        // ── Onglet « Langue et maintenance » ────────────────────────────
        int LayoutLanguageMaintenance()
        {
            layout.LanguagePanel = Section(contentTop);
            layout.LanguageFrRect = Rect(labelX, contentTop + sectionTitleH, innerWidth, rowH);
            layout.LanguageEnRect = Rect(labelX, layout.LanguageFrRect.bottom + rowGap, innerWidth, rowH);
            layout.ManagedLanguageRect = Rect(labelX + managedIndent,
                layout.LanguageEnRect.bottom + managedGap, managedWidth,
                _managedLanguage ? managedHeight : 0);

            int windowsTitleTop =
                (_managedLanguage ? layout.ManagedLanguageRect.bottom : layout.LanguageEnRect.bottom) + S(18);
            layout.WindowsPanel = Section(windowsTitleTop);
            layout.ResetVirtualKeyboardWindowRect = Rect(labelX, windowsTitleTop + sectionTitleH, innerWidth, buttonHeight);
            layout.ResetLessonsWindowRect = Rect(labelX,
                layout.ResetVirtualKeyboardWindowRect.bottom + S(7), innerWidth, buttonHeight);
            return ValidationRow(layout.ResetLessonsWindowRect.bottom);
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

    public void Show()
    {
        LoadShortcutStateFromConfig();
        RefreshShortcutTexts();
        RefreshAutoStartCheckbox();
        // Les overrides ont pu changer via le sous-menu tray Compatibilité depuis la
        // dernière ouverture : resynchroniser la section Apps suspendues.
        RefreshCompatList(SelectedCompatProcess());
        // L'opt-in Défi du jour a pu changer via l'onboarding.
        Win32.SendMessageW(_hWndChkTraining, BM_SETCHECK,
            ConfigManager.TrainingEnabled ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
        Win32.SendMessageW(_hWndChkNotifications, BM_SETCHECK,
            ConfigManager.NotificationsEnabled ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
        // Re-synchroniser la checkbox onboarding a chaque ouverture : l'utilisateur a pu
        // modifier l'etat via la case « Ne plus afficher » du wizard depuis la derniere
        // fermeture des Settings.
        Win32.SendMessageW(_hWndChkOnboarding, BM_SETCHECK,
            ConfigManager.ShowOnboardingAtStartup ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
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
        bool autoStart = Win32.SendMessageW(_hWndChkAutoStart, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)BST_CHECKED;
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
            bool notifications = Win32.SendMessageW(_hWndChkNotifications, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)BST_CHECKED;
            ConfigManager.SetNotifications(notifications);
        }

        if (!_managedOnboarding)
        {
            bool showOnboarding = Win32.SendMessageW(_hWndChkOnboarding, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)BST_CHECKED;
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
                int newDpi = (wParam.ToInt32() >> 16) & 0xFFFF;
                if (newDpi > 0)
                    _dpiScale = newDpi / 96f;
                RecreateFonts();
                var suggested = Marshal.PtrToStructure<Win32.RECT>(lParam);
                Win32.MoveWindow(_hWnd, suggested.left, suggested.top,
                    suggested.right - suggested.left, suggested.bottom - suggested.top, true);
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
                break;
            }

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
                        if (target != IntPtr.Zero) Win32.SendMessageW(target, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                        break;
                    }
                    case DialogNavigation.IDCANCEL:
                        Close();
                        break;
                    case IDC_LINK_RESET:
                        if (code == 0)
                        {
                            int confirmResult = Win32.MessageBoxW(_hWnd,
                                L.Settings_ConfirmResetShortcuts,
                                L.Settings_WindowTitle,
                                0x4 | 0x20); // MB_YESNO | MB_ICONQUESTION
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
                            bool enabled = Win32.SendMessageW(_hWndChkTraining, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)BST_CHECKED;
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
                IntPtr hdcStatic = wParam;
                IntPtr hCtrl = lParam;
                if (hCtrl == _hWndValidation)
                {
                    Win32.SetBkMode(hdcStatic, 1);
                    Win32.SetTextColor(hdcStatic, ValidationTextColor(_validationRefused));
                    return _hPanelBrush;
                }

                // Lignes de politique, et cases ou radios grisées : Windows adresse
                // WM_CTLCOLORSTATIC — et non WM_CTLCOLORBTN — à un bouton désactivé. Sans
                // cette branche, ces contrôles reprendraient le fond système au milieu du
                // panneau.
                if (hCtrl == _hWndManagedNotifications || hCtrl == _hWndManagedOnboarding ||
                    hCtrl == _hWndManagedLanguage ||
                    hCtrl == _hWndChkNotifications || hCtrl == _hWndChkOnboarding ||
                    hCtrl == _hWndRadioLangFr || hCtrl == _hWndRadioLangEn)
                {
                    Win32.SetBkMode(hdcStatic, 1);
                    Win32.SetTextColor(hdcStatic, CLR_MUTED);
                    return _hPanelBrush;
                }
                break;
            }

            case Win32.WM_CTLCOLORBTN:
            {
                IntPtr hdcButton = wParam;
                IntPtr hCtrl = lParam;
                if (hCtrl == _hWndChkAutoStart || hCtrl == _hWndChkNotifications || hCtrl == _hWndChkOnboarding ||
                    hCtrl == _hWndChkTraining ||
                    hCtrl == _hWndRadioLangFr || hCtrl == _hWndRadioLangEn ||
                    hCtrl == _hWndResetVirtualKeyboardWindow || hCtrl == _hWndResetLessonsWindow ||
                    hCtrl == _hWndCompatAdd || hCtrl == _hWndCompatRemove ||
                    hCtrl == _hWndRadioCompatAuto || hCtrl == _hWndRadioCompatForceOn || hCtrl == _hWndRadioCompatForceOff)
                {
                    Win32.SetBkMode(hdcButton, 1);
                    Win32.SetTextColor(hdcButton, CLR_TEXT);
                    return _hPanelBrush;
                }
                break;
            }

            case Win32.WM_CTLCOLOREDIT:
            {
                IntPtr hdcEdit = wParam;
                IntPtr hCtrlEdit = lParam;
                if (hCtrlEdit == _hWndEditKeyboard)
                {
                    Win32.SetTextColor(hdcEdit, _keyboardValid ? CLR_TEXT : CLR_INVALID);
                    Win32.SetBkColor(hdcEdit, CLR_KEY_BG);
                    return _hKeyBrush;
                }

                if (hCtrlEdit == _hWndEditSearch)
                {
                    Win32.SetTextColor(hdcEdit, _searchValid ? CLR_TEXT : CLR_INVALID);
                    Win32.SetBkColor(hdcEdit, CLR_KEY_BG);
                    return _hKeyBrush;
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

    private void RefreshShortcutText(IntPtr hWndShortcut, uint vk)
    {
        Win32.SetWindowTextW(hWndShortcut, ConfigManager.GetShortcutDisplayName(vk));
    }

    /// <summary>Couleur de la ligne de validation : rouge pour un refus, vert sinon. Elle ne
    /// dépend que du message affiché : un raccourci resté invalide ne teint plus en rouge
    /// le message d'un autre geste, et un refus ne s'affiche plus en vert.</summary>
    internal static uint ValidationTextColor(bool refused) => refused ? CLR_INVALID : CLR_VALID;

    private void SetRefusalMessage(string text) => ShowValidationMessage(text, false, true);

    private void SetValidationMessage(string text, bool captureHint = false)
        => ShowValidationMessage(text, captureHint, false);

    private void ShowValidationMessage(string text, bool captureHint, bool refused)
    {
        bool rowChanged = string.IsNullOrEmpty(_validationMessage) != string.IsNullOrEmpty(text);
        _validationMessage = text;
        _validationRefused = refused;
        _showCaptureHint = captureHint;
        Win32.SetWindowTextW(_hWndValidation, text);
        if (rowChanged && _hWnd != IntPtr.Zero && _hWndValidation != IntPtr.Zero)
        {
            // Le message fait grandir le contenu, parfois de deux lignes. Sans remesure,
            // la fenêtre gardait sa hauteur et le message débordait sans que le défilement
            // s'arme : le bas du contenu devenait inatteignable (R12 de la revue du
            // 2026-09-21). Même enchaînement que OnLanguageChanged, pour la même raison.
            // Seule l'apparition ou la disparition de la ligne change la mise en page : un
            // message qui en remplace un autre ne remesure plus rien (audit du 25/09, F-12).
            FitWindowToContent();
            RepositionControls();
        }
    }

    private void ClearCaptureHintIfVisible()
    {
        if (_showCaptureHint)
            SetValidationMessage(string.Empty);
    }

    private void SetShortcutValidity(IntPtr hWndShortcut, bool valid)
    {
        if (hWndShortcut == _hWndEditKeyboard)
            _keyboardValid = valid;
        else if (hWndShortcut == _hWndEditSearch)
            _searchValid = valid;
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
        Win32.SendMessageW(_hWndChkAutoStart, BM_SETCHECK,
            AutoStart.IsRegistered ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
    }

    private void RefreshLanguageRadios()
    {
        bool isEnglish = ConfigManager.AppLanguage == "en";
        Win32.SendMessageW(_hWndRadioLangFr, BM_SETCHECK, isEnglish ? IntPtr.Zero : (IntPtr)BST_CHECKED, IntPtr.Zero);
        Win32.SendMessageW(_hWndRadioLangEn, BM_SETCHECK, isEnglish ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
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
        // Les libellés traduits changent de hauteur (les textes anglais tiennent parfois
        // sur une ligne de moins) : remesurer, sinon la fenêtre garde la hauteur de
        // l'autre langue et le bas du contenu redevient inatteignable.
        FitWindowToContent();
        RepositionControls();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    /// <summary>R\u00e9applique tous les libell\u00e9s traduits de cette fen\u00eatre (appel\u00e9 apr\u00e8s un changement de langue).</summary>
    private void RefreshLanguageTexts()
    {
        _metrics = null; // largeur du bouton de réinitialisation, dans la nouvelle langue
        Win32.SetWindowTextW(_hWnd, L.Settings_WindowTitle);
        // La bande d'onglets n'est pas un contrôle à texte de fenêtre : ses trois libellés
        // vivent dans le contrôle onglet et ne se réécrivent qu'avec TCM_SETITEMW. Ils
        // étaient les seuls oubliés de cette fonction.
        SetTabText(0, L.Settings_TabGeneral);
        SetTabText(1, L.Settings_TabApplications);
        SetTabText(2, L.Settings_TabLanguageMaintenance);
        foreach (var control in _controls)
            if (control.Text != null)
                Win32.SetWindowTextW(control.Handle, control.Text());
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
        _compatProcesses.AddRange(overrides.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));

        Win32.SendMessageW(_hWndCompatList, LB_RESETCONTENT, IntPtr.Zero, IntPtr.Zero);
        int selIndex = -1;
        for (int i = 0; i < _compatProcesses.Count; i++)
        {
            string name = _compatProcesses[i];
            string modeLabel = overrides[name] == "forceOn"
                ? L.Settings_CompatListForceOn
                : L.Settings_CompatListForceOff;
            SendListBoxAddString(_hWndCompatList, $"{name} — {modeLabel}");
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
    /// (radios décochées et grisées quand rien n'est sélectionné).</summary>
    private void RefreshCompatSelectionUi()
    {
        string? proc = SelectedCompatProcess();
        string? mode = proc != null ? ConfigManager.GetCompatibilityOverride(proc) : null;
        bool hasSelection = proc != null;

        Win32.EnableWindow(_hWndCompatRemove, hasSelection);
        Win32.EnableWindow(_hWndRadioCompatAuto, hasSelection);
        Win32.EnableWindow(_hWndRadioCompatForceOn, hasSelection);
        Win32.EnableWindow(_hWndRadioCompatForceOff, hasSelection);

        Win32.SendMessageW(_hWndRadioCompatAuto, BM_SETCHECK,
            hasSelection && mode == null ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
        Win32.SendMessageW(_hWndRadioCompatForceOn, BM_SETCHECK,
            mode == "forceOn" ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
        Win32.SendMessageW(_hWndRadioCompatForceOff, BM_SETCHECK,
            mode == "forceOff" ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
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
            SetRefusalMessage(L.Settings_CompatForceOnRefused);
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

        ClearCaptureHintIfVisible();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    private void ApplyCapturedShortcut(IntPtr hWndShortcut, uint vk)
    {
        bool isKeyboard = hWndShortcut == _hWndEditKeyboard;
        uint otherVk = isKeyboard ? _searchVk : _keyboardVk;

        if (!ConfigManager.IsShortcutAllowedVk(vk))
        {
            SetShortcutValidity(hWndShortcut, false);
            SetRefusalMessage(L.Settings_ShortcutReserved);
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
            return;
        }

        if (vk == otherVk)
        {
            SetShortcutValidity(hWndShortcut, false);
            SetRefusalMessage(L.Settings_ShortcutAlreadyUsed);
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
                uint inputMessage = 0;
                long inputVk = 0;
                if (lParam != IntPtr.Zero)
                {
                    var inputMsg = Marshal.PtrToStructure<Win32.MSG>(lParam);
                    inputMessage = inputMsg.message;
                    inputVk = inputMsg.wParam.ToInt64();
                }

                return (IntPtr)DialogNavigation.DialogCodeKeepingTab(baseResult.ToInt64(), inputMessage, inputVk);
            }

            case Win32.WM_SETFOCUS:
                _focusedShortcut = hWnd;
                SetShortcutValidity(hWnd, true);
                SetValidationMessage(ShortcutCaptureHint, true);
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                break;

            case Win32.WM_KILLFOCUS:
                if (_focusedShortcut == hWnd)
                    _focusedShortcut = IntPtr.Zero;
                SetShortcutValidity(hWnd, true);
                ClearCaptureHintIfVisible();
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                break;

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
                    if (_showCaptureHint)
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

    private void OnPaint(IntPtr hWnd)
    {
        var hdcPaint = Win32.BeginPaint(hWnd, out var ps);
        Win32.GetClientRect(hWnd, out var clientRect);
        int cw = clientRect.right;
        int ch = clientRect.bottom;
        LayoutInfo layout = GetLayout(cw);

        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        var hdc = Win32.CreateCompatibleDC(hdcScreen);
        var hBmp = Win32.CreateCompatibleBitmap(hdcScreen, cw, ch);
        var hBmpOld = Win32.SelectObject(hdc, hBmp);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);

        Win32.FillRect(hdc, ref clientRect, _hBgBrush);
        Win32.SetBkMode(hdc, 1);
        // Le contenu se dessine en coordonnées de contenu, décalées du défilement. GDI+,
        // créé après, suit la même origine (mesuré le 26/09).
        Win32.SetViewportOrgEx(hdc, 0, -_scrollY, IntPtr.Zero);

        Win32.GdipCreateFromHDC(hdc, out IntPtr gfx);
        if (gfx != IntPtr.Zero)
        {
            Win32.GdipSetSmoothingMode(gfx, 4);
            Win32.GdipSetInterpolationMode(gfx, 7);
            Win32.GdipSetTextRenderingHint(gfx, 4);
        }

        DrawHeader(hdc, gfx, layout, cw);
        GdiHelpers.DrawPanel(hdc, layout.ShortcutsPanel, CLR_PANEL_BG, CLR_PANEL_BORDER, 0, 0);
        // Seules les sections de l'onglet actif se dessinent. Les autres ont des
        // rectangles de hauteur nulle : les tracer ne planterait pas, mais leurs
        // titres de panneau, eux, se dessineraient les uns sur les autres.
        if (_activeTab == SettingsTab.General)
        {
            PaintShortcutPanel(hdc, layout);
            DrawSectionTitle(hdc, layout.PreferencesPanel, L.Settings_SectionPreferences);
        }
        else if (_activeTab == SettingsTab.Applications)
        {
            DrawSectionTitle(hdc, layout.CompatPanel, L.Settings_SectionCompat);
        }
        else
        {
            DrawSectionTitle(hdc, layout.LanguagePanel, L.Settings_SectionLanguage);
            DrawSectionTitle(hdc, layout.WindowsPanel, L.Settings_SectionWindows);
        }

        if (gfx != IntPtr.Zero)
            Win32.GdipDeleteGraphics(gfx);

        Win32.SetViewportOrgEx(hdc, 0, 0, IntPtr.Zero);
        Win32.BitBlt(hdcPaint, 0, 0, cw, ch, hdc, 0, 0, Win32.SRCCOPY);
        Win32.SelectObject(hdc, hBmpOld);
        Win32.DeleteObject(hBmp);
        Win32.DeleteDC(hdc);
        Win32.EndPaint(hWnd, ref ps);
    }

    private void DrawHeader(IntPtr hdc, IntPtr gfx, LayoutInfo layout, int cw)
    {
        if (gfx != IntPtr.Zero && _gdipLogo != IntPtr.Zero)
        {
            Win32.GdipDrawImageRectI(gfx, _gdipLogo,
                layout.LogoRect.left, layout.LogoRect.top,
                layout.LogoRect.right - layout.LogoRect.left,
                layout.LogoRect.bottom - layout.LogoRect.top);
        }

        string version = $"v{Program.Version}";
        Win32.SelectObject(hdc, _hFontVersion);
        int versionHeight = MeasureSingleLineHeight(hdc, _hFontVersion);
        int titleHeight = MeasureSingleLineHeight(hdc, _hFontTitle);
        int versionTextWidth = MeasureSingleLineWidth(hdc, _hFontVersion, version);
        int versionWidth = versionTextWidth + S(24);
        int versionRight = cw - layout.Margin - S(6);
        int versionLeft = versionRight - versionWidth;
        int headerLineTop = Math.Min(layout.LogoRect.top, layout.HeaderTitleY);
        int headerLineBottom = Math.Max(layout.LogoRect.bottom, layout.HeaderTitleY + titleHeight);

        string title = ProductIdentity.DisplayName;
        int titleRight = versionLeft - S(8);
        Win32.SelectObject(hdc, _hFontTitle);
        Win32.SetTextColor(hdc, CLR_TITLE);
        var titleRect = new Win32.RECT
        {
            left = layout.HeaderTitleX,
            top = layout.HeaderTitleY,
            right = titleRight,
            bottom = layout.HeaderTitleY + S(20)
        };
        Win32.DrawTextW(hdc, title, -1, ref titleRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        Win32.SetTextColor(hdc, CLR_VERSION);
        var versionRect = new Win32.RECT
        {
            left = versionLeft,
            top = headerLineTop - S(1),
            right = versionRight,
            bottom = headerLineBottom + S(3)
        };
        Win32.DrawTextW(hdc, version, -1, ref versionRect,
            Win32.DT_LEFT | Win32.DT_VCENTER | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        GdiHelpers.FillSolidRect(hdc, Rect(layout.Margin, layout.HeaderDividerY, cw - layout.Margin * 2, 1), CLR_SEPARATOR);
    }

    private void PaintShortcutPanel(IntPtr hdc, LayoutInfo layout)
    {
        int titleX = layout.ShortcutsPanel.left + S(12);
        DrawPanelTitle(hdc, titleX, layout.ShortcutsPanel.top + S(8),
            layout.ShortcutsPanel.right - S(12) - titleX, L.Settings_SectionShortcuts);

        DrawShortcutRow(hdc, layout.ShortcutsLabelX, layout.ShortcutsLabelWidth, layout.KeyboardRowY,
            L.Settings_ShortcutLabelKeyboard, GetShortcutPrefixRuns(), layout.ShortcutsShortcutX, layout.ShortcutsShortcutWidth);
        DrawKeyBox(hdc, layout.KeyboardBoxRect, _keyboardValid, _focusedShortcut == _hWndEditKeyboard);

        DrawShortcutRow(hdc, layout.ShortcutsLabelX, layout.ShortcutsLabelWidth, layout.SearchRowY,
            L.Settings_ShortcutLabelSearch, GetShortcutPrefixRuns(), layout.ShortcutsShortcutX, layout.ShortcutsShortcutWidth);
        DrawKeyBox(hdc, layout.SearchBoxRect, _searchValid, _focusedShortcut == _hWndEditSearch);
    }

    /// <summary>Titre d'une section et, au-dessus, son séparateur (audit du 25/09, F-06 :
    /// quatre méthodes recopiaient ce bloc à côté de DrawPanelTitle, qui ne servait pas).</summary>
    private void DrawSectionTitle(IntPtr hdc, Win32.RECT section, string title)
    {
        int x = section.left + S(12);
        GdiHelpers.FillSolidRect(hdc, Rect(x, section.top - S(8), section.right - section.left - S(24), 1), CLR_SEPARATOR);
        DrawPanelTitle(hdc, x, section.top, section.right - S(12) - x, title);
    }

    private void DrawShortcutRow(IntPtr hdc, int labelX, int labelWidth, int rowY,
        string label, (string Text, uint Color, IntPtr Font)[] shortcutRuns, int shortcutX, int shortcutWidth)
    {
        Win32.SelectObject(hdc, _hFontText);
        Win32.SetTextColor(hdc, CLR_TEXT);
        var labelRect = new Win32.RECT
        {
            left = labelX,
            top = rowY + S(1),
            right = labelX + labelWidth,
            bottom = rowY + S(22)
        };
        Win32.DrawTextW(hdc, label, -1, ref labelRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_VCENTER | Win32.DT_NOPREFIX);

        GdiHelpers.DrawColoredRuns(hdc, shortcutX, rowY + S(2), shortcutWidth, S(20), shortcutRuns);
    }

    private void DrawKeyBox(IntPtr hdc, Win32.RECT rect, bool valid, bool focused)
    {
        uint borderColor = !valid ? CLR_KEY_BORDER_INVALID : focused ? CLR_KEY_BORDER_FOCUS : CLR_KEY_BORDER;
        GdiHelpers.FillSolidRect(hdc, rect, borderColor);
        var innerRect = new Win32.RECT
        {
            left = rect.left + 1,
            top = rect.top + 1,
            right = rect.right - 1,
            bottom = rect.bottom - 1
        };
        GdiHelpers.FillSolidRect(hdc, innerRect, CLR_KEY_BG);
    }

    private void DrawPanelTitle(IntPtr hdc, int x, int y, int width, string title)
    {
        Win32.SelectObject(hdc, _hFontPanelTitle);
        Win32.SetTextColor(hdc, CLR_LINK);
        var rect = new Win32.RECT { left = x, top = y, right = x + width, bottom = y + S(20) };
        Win32.DrawTextW(hdc, title, -1, ref rect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
    }

    // Méthodes GDI factorisées dans GdiHelpers.cs — wrappers d'instance pour le DPI scaling
    private int MeasureSingleLineWidth(IntPtr hdc, IntPtr hFont, string text)
        => GdiHelpers.MeasureSingleLineWidth(hdc, hFont, text);

    private int MeasureSingleLineHeight(IntPtr hdc, IntPtr hFont)
        => GdiHelpers.MeasureSingleLineHeight(hdc, hFont);

    private (string Text, uint Color, IntPtr Font)[] GetShortcutPrefixRuns()
    {
        return new[]
        {
            ("Ctrl", CLR_INLINE_HIGHLIGHT, _hFontBold),
            (" + ", CLR_TEXT, _hFontText),
            (L.Settings_ShortcutModifier2, CLR_INLINE_HIGHLIGHT, _hFontBold),
            (" + ", CLR_TEXT, _hFontText)
        };
    }

    public void Dispose()
    {
        if (_onAppLanguageChanged != null)
            ConfigManager.AppLanguageChanged -= _onAppLanguageChanged;
        if (_hWndEditKeyboard != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndEditKeyboard, _shortcutSubclassProc, (UIntPtr)3);
        if (_hWndEditSearch != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndEditSearch, _shortcutSubclassProc, (UIntPtr)4);
        if (_hWnd != IntPtr.Zero)
        {
            // AG130-40 : desinscrire AVANT de detruire — Windows recycle les HWND.
            DialogNavigation.Unregister(_hWnd);
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }

        DestroyFonts();
        Win32.DeleteObject(_hBgBrush);
        Win32.DeleteObject(_hPanelBrush);
        Win32.DeleteObject(_hKeyBrush);

        if (_gdipLogo != IntPtr.Zero)
        {
            Win32.GdipDisposeImage(_gdipLogo);
            _gdipLogo = IntPtr.Zero;
        }
        if (_gdipToken != IntPtr.Zero)
        {
            Win32.GdiplusShutdown(_gdipToken);
            _gdipToken = IntPtr.Zero;
        }

        // UnregisterClassW pour permettre une 2e instance avec un delegate WndProc frais.
        Win32.UnregisterClassW(ProductIdentity.WindowClass("Settings"), Win32.GetModuleHandleW(null));
    }
}
