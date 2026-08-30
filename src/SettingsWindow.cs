using System.Runtime.InteropServices;
using System.Text;
namespace AZERTYGlobal;

sealed partial class SettingsWindow : IDisposable
{
    // BS_AUTOCHECKBOX, BS_AUTORADIOBUTTON, BM_GETCHECK, BM_SETCHECK et BST_CHECKED ont
    // disparu avec le passage en owner-draw : Windows ne tient plus l'état coché, et le
    // demander à un contrôle BS_OWNERDRAW rendrait toujours zéro. Voir SettingsWindow.Theme.cs.
    private const uint WS_GROUP = 0x00020000;
    private const uint SS_NOTIFY = 0x0100;
    private const uint ES_AUTOHSCROLL = 0x0080;
    private const uint ES_CENTER = 0x0001;
    private const uint ES_UPPERCASE = 0x0008;
    private const uint EN_CHANGE = 0x0300;
    private const uint EM_SETLIMITTEXT = 0x00C5;
    private const uint EM_SETREADONLY = 0x00CF;

    private const int DLGC_WANTALLKEYS = 0x0004;
    private const int VK_TAB = 0x09;
    private const int VK_ESCAPE = 0x1B;
    private static uint CLR_KEY_BORDER_FOCUS => Theme.Current.Action;
    private static string ShortcutCaptureHint => L.Settings_ShortcutCaptureHint;

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
    // Barre d'onglets (2026-08-30)
    private const int IDC_TAB_SHORTCUTS = 3119;
    private const int IDC_TAB_PREFERENCES = 3120;
    private const int IDC_TAB_COMPAT = 3121;

    // Listbox Win32 (section Apps suspendues)
    private const uint LBS_NOTIFY = 0x0001;
    private const uint LBS_NOINTEGRALHEIGHT = 0x0100;
    private const uint LB_ADDSTRING = 0x0180;
    private const uint LB_RESETCONTENT = 0x0184;
    private const uint LB_SETCURSEL = 0x0186;
    private const uint LB_GETCURSEL = 0x0188;
    private const int LBN_SELCHANGE = 1;
    private const uint WS_VSCROLL = 0x00200000;

    // Plancher, et non plus taille de la fenêtre : MeasureRequiredClientSize mesure ce que le
    // contenu réclame à ce DPI et dans cette langue, la fenêtre prend le plus grand des deux.
    //
    // Ces deux nombres sont l'histoire du défaut que la mesure supprime. 240 → 300 le
    // 2026-07-16 (smoke test) : « Virtual keyboard » était tronqué en EN. 470 → 680 le
    // 2026-07-30 : section « Apps suspendues » + opt-in Défi du jour (v1.2.0). Puis la charte
    // a remplacé les polices d'origine par de plus grandes, et 300 × 680 a coupé quatre
    // libellés de case, le lien « Valeurs par défaut » et la dernière ligne de la fenêtre,
    // aux trois échelles — mesuré le 2026-08-30. Une troisième constante aurait tenu jusqu'au
    // libellé suivant.
    private const int BASE_WIN_W = 300;
    private const int BASE_WIN_H = 680;

    /// <summary>Style de la fenêtre, un seul endroit pour les trois qui en avaient besoin.
    /// <c>WS_THICKFRAME</c> depuis le 2026-08-30, sur demande d'Antoine : la taille calculée
    /// n'est qu'un plancher, l'utilisateur peut agrandir. Ni minimiser ni maximiser — une
    /// fenêtre de réglages n'a rien à faire dans la barre des tâches ni en plein écran.</summary>
    /// <c>WS_CLIPCHILDREN</c> depuis le 2026-08-30, et il n'est pas décoratif : sans lui,
    /// OnPaint recopie son tampon sur tout le client, zones des enfants comprises, et efface
    /// les contrôles owner-draw qui s'étaient déjà peints. Mesuré sur la matrice — treize
    /// contrôles absents d'une cellule, présents dans la suivante, aucune règle. Les fenêtres
    /// déjà migrées le portent toutes ; celle-ci ne l'avait pas parce que Windows repeignait
    /// lui-même ses contrôles natifs après chaque WM_PAINT du parent.
    private const uint WindowStyle =
        Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU | Win32.WS_THICKFRAME
        | Win32.WS_CLIPCHILDREN;

    // Les jetons de la charte, relus a chaque peinture : une bascule de theme n'a donc rien a
    // recalculer ici. Trois noms ont disparu — CLR_PANEL_ACCENT et CLR_SUBTITLE ne peignaient
    // rien (zero usage mesure), et CLR_LINK_HOVER portait l'orange fantome, le survol restant
    // marque par le soulignement du lien et le curseur main.
    private static uint CLR_BG => Theme.Current.Paper;
    private static uint CLR_TITLE => Theme.Current.Ink;
    private static uint CLR_TEXT => Theme.Current.Ink;
    private static uint CLR_MUTED => Theme.Current.TextSecondary;
    private static uint CLR_VERSION => Theme.Current.TextSecondary;
    private static uint CLR_PANEL_BG => Theme.Current.Surface;
    private static uint CLR_PANEL_BORDER => Theme.Current.Border;
    private static uint CLR_LINK => Theme.Current.Action;
    private static uint CLR_INLINE_HIGHLIGHT => Theme.Current.Action;
    private static uint CLR_VALID => Theme.Current.Success;
    private static uint CLR_INVALID => Theme.Current.Error;
    private static uint CLR_KEY_BG => Theme.Current.Surface;
    private static uint CLR_KEY_BORDER => Theme.Current.Border;
    private static uint CLR_KEY_BORDER_INVALID => Theme.Current.Error;
    private static uint CLR_SEPARATOR => Theme.Current.Border;

    private struct LayoutInfo
    {
        public int Margin;
        public int HeaderTitleX;
        public int HeaderTitleY;
        public int HeaderDividerY;
        public Win32.RECT LogoRect;
        // Onglet visible, ses trois rectangles de barre, et le panneau qui porte son contenu.
        // Les cinq « Panel » d'avant les onglets ont disparu : ils décrivaient cinq sections
        // empilées dans un seul panneau, qui vivent désormais dans trois.
        public int Tab;
        public Win32.RECT[] TabRects;
        public int TabStripBottom;
        public Win32.RECT ContentPanel;
        public int PanelBottom;
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
        public Win32.RECT AutoStartRect;
        public Win32.RECT NotificationsRect;
        public Win32.RECT ManagedNotificationsRect;
        public Win32.RECT OnboardingRect;
        public Win32.RECT ManagedOnboardingRect;
        public Win32.RECT TrainingRect;
        public int LanguageTitleTop;
        public Win32.RECT LanguageFrRect;
        public Win32.RECT LanguageEnRect;
        public Win32.RECT ManagedLanguageRect;
        public int WindowsTitleTop;
        public Win32.RECT ResetVirtualKeyboardWindowRect;
        public Win32.RECT ResetLessonsWindowRect;
        public Win32.RECT CompatListRect;
        public Win32.RECT CompatAddRect;
        public Win32.RECT CompatRemoveRect;
        public Win32.RECT CompatAutoRect;
        public Win32.RECT CompatForceOnRect;
        public Win32.RECT CompatForceOffRect;
        // GuideRect et CloseButtonRect retirés — la croix système suffit
    }

    private IntPtr _hWnd;
    private IntPtr _hWndEditKeyboard;
    private IntPtr _hWndEditSearch;
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
    private readonly IntPtr[] _hWndTabs = new IntPtr[TabCount];
    private int _activeTab = TabShortcuts;
    private IntPtr _hWndCompatList;
    private IntPtr _hWndCompatAdd;
    private IntPtr _hWndCompatRemove;
    private IntPtr _hWndRadioCompatAuto;
    private IntPtr _hWndRadioCompatForceOn;
    private IntPtr _hWndRadioCompatForceOff;
    // Noms de process affichés dans la listbox, dans l'ordre des index de la liste
    private readonly List<string> _compatProcesses = new();

    private readonly Win32.WNDPROC _wndProcDelegate;
    private readonly Win32.SUBCLASSPROC _linkSubclassProc;
    private readonly Win32.SUBCLASSPROC _shortcutSubclassProc;
    private IntPtr _hoveredLink;
    private IntPtr _focusedShortcut;

    // Les brosses viennent du cache de Theme et lui appartiennent : cette fenetre n'en
    // detruit aucune, et aucune n'est inscrite en fond de classe (voir CreateMainWindow).
    private static IntPtr _hBgBrush => Theme.Brush(CLR_BG);
    private static IntPtr _hPanelBrush => Theme.Brush(CLR_PANEL_BG);
    private static IntPtr _hKeyBrush => Theme.Brush(CLR_KEY_BG);

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

    private Action? _themeChanged;

    private float _dpiScale;
    private int S(int val) => (int)(val * _dpiScale);

    /// <summary>L'echelle en points par pouce, dont Theme a besoin pour ses polices. _dpiScale
    /// reste la mesure de travail de cette fenetre, qui multiplie des dizaines de coordonnees :
    /// les deux disent la meme chose.</summary>
    private int _dpi => (int)Math.Round(96 * _dpiScale);

    private IntPtr _hFontTitle => Theme.Font(FontRole.WindowTitle, _dpi);
    private IntPtr _hFontVersion => Theme.Font(FontRole.Mono, _dpi);
    private IntPtr _hFontSubtitle => Theme.Font(FontRole.BodyStrong, _dpi);
    private IntPtr _hFontPanelTitle => Theme.Font(FontRole.SectionTitle, _dpi);
    private IntPtr _hFontText => Theme.Font(FontRole.Body, _dpi);
    private IntPtr _hFontBold => Theme.Font(FontRole.BodyStrong, _dpi);
    private IntPtr _hFontEdit => Theme.Font(FontRole.BodyStrong, _dpi);
    private IntPtr _hFontLink => Theme.Font(FontRole.Body, _dpi, underlined: true);
    private IntPtr _hFontLinkStrong => Theme.Font(FontRole.BodyStrong, _dpi, underlined: true);
    private IntPtr _hFontSmall => Theme.Font(FontRole.Secondary, _dpi);
    private IntPtr _hFontButton => Theme.Font(FontRole.Body, _dpi);

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
        _linkSubclassProc = LinkSubclassProc;
        _shortcutSubclassProc = ShortcutSubclassProc;
        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        int dpi = Win32.GetDeviceCaps(hdcScreen, 88);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        _dpiScale = dpi / 96f;

        var gdipInput = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        Win32.GdiplusStartup(out _gdipToken, ref gdipInput, IntPtr.Zero);
        _gdipLogo = GdiImageLoader.LoadFromEmbeddedResource(typeof(SettingsWindow), ProductIdentity.LogoResourceName);
        LoadShortcutStateFromConfig();

        CreateMainWindow();
        CreateControls();
        AttachHoverTracking();
        ApplyFontsToControls();
        RepositionControls();

        // Bascule de langue initiée ailleurs (menu tray, fenêtre de bienvenue) pendant que
        // cette fenêtre existe : se rafraîchir. Événement statique → désabonné dans Dispose.
        _onAppLanguageChanged = _ => OnLanguageChanged();
        ConfigManager.AppLanguageChanged += _onAppLanguageChanged;

        try
        {
            // Le DPI passe par ThemeWindow : seul ce point honore l'override du banc de
            // captures. Lu en direct, la fenêtre rend toujours à l'échelle du poste et sa
            // matrice n'est qu'un rendu répété.
            int realDpi = ThemeWindow.DpiOf(_hWnd);
            if (realDpi > 0 && Math.Abs(realDpi / 96f - _dpiScale) > 0.01f)
            {
                _dpiScale = realDpi / 96f;
                ApplyFontsToControls();
                ResizeWindow();
                RepositionControls();
            }
        }
        catch
        {
        }
    }

    private void ApplyFontsToControls()
    {
        Win32.SendMessageW(_hWndEditKeyboard, Win32.WM_SETFONT, _hFontEdit, (IntPtr)1);
        Win32.SendMessageW(_hWndEditSearch, Win32.WM_SETFONT, _hFontEdit, (IntPtr)1);
        Win32.SendMessageW(_hWndChkAutoStart, Win32.WM_SETFONT, _hFontBold, (IntPtr)1);
        Win32.SendMessageW(_hWndChkNotifications, Win32.WM_SETFONT, _hFontBold, (IntPtr)1);
        Win32.SendMessageW(_hWndChkOnboarding, Win32.WM_SETFONT, _hFontBold, (IntPtr)1);
        Win32.SendMessageW(_hWndChkTraining, Win32.WM_SETFONT, _hFontBold, (IntPtr)1);
        Win32.SendMessageW(_hWndResetVirtualKeyboardWindow, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
        Win32.SendMessageW(_hWndResetLessonsWindow, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
        Win32.SendMessageW(_hWndRadioLangFr, Win32.WM_SETFONT, _hFontBold, (IntPtr)1);
        Win32.SendMessageW(_hWndRadioLangEn, Win32.WM_SETFONT, _hFontBold, (IntPtr)1);
        Win32.SendMessageW(_hWndLinkReset, Win32.WM_SETFONT, _hFontLink, (IntPtr)1);
        Win32.SendMessageW(_hWndValidation, Win32.WM_SETFONT, _hFontSmall, (IntPtr)1);
        Win32.SendMessageW(_hWndManagedNotifications, Win32.WM_SETFONT, _hFontSmall, (IntPtr)1);
        Win32.SendMessageW(_hWndManagedOnboarding, Win32.WM_SETFONT, _hFontSmall, (IntPtr)1);
        Win32.SendMessageW(_hWndManagedLanguage, Win32.WM_SETFONT, _hFontSmall, (IntPtr)1);
        Win32.SendMessageW(_hWndCompatList, Win32.WM_SETFONT, _hFontText, (IntPtr)1);
        // LBS_OWNERDRAWFIXED ne demande sa hauteur de ligne qu'une fois, à la création : à la
        // bascule d'échelle, c'est ici qu'elle se remet au DPI courant.
        if (_hWndCompatList != IntPtr.Zero)
            Win32.SendMessageW(_hWndCompatList, LB_SETITEMHEIGHT, IntPtr.Zero, (IntPtr)CompatItemHeight());
        Win32.SendMessageW(_hWndCompatAdd, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
        Win32.SendMessageW(_hWndCompatRemove, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
        Win32.SendMessageW(_hWndRadioCompatAuto, Win32.WM_SETFONT, _hFontBold, (IntPtr)1);
        Win32.SendMessageW(_hWndRadioCompatForceOn, Win32.WM_SETFONT, _hFontBold, (IntPtr)1);
        Win32.SendMessageW(_hWndRadioCompatForceOff, Win32.WM_SETFONT, _hFontBold, (IntPtr)1);
        for (int i = 0; i < TabCount; i++)
            Win32.SendMessageW(_hWndTabs[i], Win32.WM_SETFONT, _hFontBold, (IntPtr)1);
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
            // hbrBackground = IntPtr.Zero : une brosse inscrite ici appartient au systeme,
            // qui la detruit au desenregistrement de la classe. ApplyClassBackground en
            // pose une apres coup, que Theme garde dans son cache.
            hbrBackground = IntPtr.Zero,
            lpszClassName = className
        };
        Win32.RegisterClassExW(ref wc);

        var (winW, winH) = MeasureRequiredClientSize();
        uint dwStyle = WindowStyle;
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
        ThemeWindow.ApplyChrome(_hWnd);
        ThemeWindow.ApplyProductIcon(_hWnd);
        ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);

        _themeChanged = () =>
        {
            if (_hWnd == IntPtr.Zero)
                return;
            ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);
            ThemeWindow.ApplyChrome(_hWnd);
            InvalidateOwnerDrawControls();
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
        };
        Theme.Changed += _themeChanged;
    }

    private void CreateControls()
    {
        var hInstance = Win32.GetModuleHandleW(null);

        // La barre d'onglets, créée avant tout le reste : Windows construit l'ordre de
        // tabulation dans l'ordre de création des enfants, et les onglets doivent venir en
        // premier — c'est par eux qu'on choisit ce qu'on va régler.
        //
        // Des BUTTON BS_OWNERDRAW plutôt qu'un SysTabControl32 : le contrôle système ne connaît
        // aucun jeton de la charte, et un bouton rend gratuitement le focus, l'espace, l'entrée
        // et WM_COMMAND.
        int[] tabIds = { IDC_TAB_SHORTCUTS, IDC_TAB_PREFERENCES, IDC_TAB_COMPAT };
        for (int i = 0; i < TabCount; i++)
        {
            _hWndTabs[i] = Win32.CreateWindowExW(0, "BUTTON", TabLabel(i),
                Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,
                0, 0, 0, 0,
                _hWnd, (IntPtr)tabIds[i], hInstance, IntPtr.Zero);
        }

        _hWndEditKeyboard = Win32.CreateWindowExW(0, "EDIT",
            ConfigManager.GetShortcutDisplayName(_keyboardVk),
            Win32.WS_CHILD | Win32.WS_VISIBLE | ES_AUTOHSCROLL | ES_CENTER | ES_UPPERCASE | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_EDIT_KEYBOARD, hInstance, IntPtr.Zero);
        Win32.SendMessageW(_hWndEditKeyboard, EM_SETREADONLY, (IntPtr)1, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndEditKeyboard, _shortcutSubclassProc, (UIntPtr)3, IntPtr.Zero);

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

        _hWndLinkReset = Win32.CreateWindowExW(0, "STATIC", L.Settings_LinkResetDefaults,
            Win32.WS_CHILD | Win32.WS_VISIBLE | SS_NOTIFY,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LINK_RESET, hInstance, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndLinkReset, _linkSubclassProc, (UIntPtr)2, IntPtr.Zero);

        _hWndChkAutoStart = Win32.CreateWindowExW(0, "BUTTON", L.Settings_AutoStart,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_CHK_AUTOSTART, hInstance, IntPtr.Zero);
        RefreshAutoStartCheckbox();

        _hWndChkNotifications = Win32.CreateWindowExW(0, "BUTTON", L.Settings_Notifications,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_CHK_NOTIFICATIONS, hInstance, IntPtr.Zero);
        if (ConfigManager.NotificationsEnabled)
            SetCheck(_hWndChkNotifications, true);

        _hWndChkOnboarding = Win32.CreateWindowExW(0, "BUTTON", L.Settings_OnboardingWindow,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_CHK_ONBOARDING, hInstance, IntPtr.Zero);
        if (ConfigManager.ShowOnboardingAtStartup)
            SetCheck(_hWndChkOnboarding, true);

        // Opt-in Défi du jour (v1.2.0) — décoché par défaut, appliqué immédiatement au
        // clic (pas à la fermeture) : l'entrée du menu tray et le module des leçons
        // dépendent de cet état.
        _hWndChkTraining = Win32.CreateWindowExW(0, "BUTTON", L.Challenge_OptIn,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_CHK_TRAINING, hInstance, IntPtr.Zero);
        if (ConfigManager.TrainingEnabled)
            SetCheck(_hWndChkTraining, true);

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
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | WS_GROUP | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RADIO_LANG_FR, hInstance, IntPtr.Zero);

        _hWndRadioLangEn = Win32.CreateWindowExW(0, "BUTTON", "English",
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,
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
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RESET_VIRTUAL_KEYBOARD_WINDOW, hInstance, IntPtr.Zero);

        _hWndResetLessonsWindow = Win32.CreateWindowExW(0, "BUTTON", L.Settings_ResetLessonsModule,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RESET_LESSONS_WINDOW, hInstance, IntPtr.Zero);

        // ── Section « Apps suspendues » (v1.2.0) ─────────────────────
        _hWndCompatList = Win32.CreateWindowExW(0, "LISTBOX", "",
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP |
            // WS_BORDER retiré : le cadre système ne connaît aucun jeton, et la charte le
            // remplace par celui d'un champ, peint par PaintCompatPanel.
            LBS_NOTIFY | LBS_NOINTEGRALHEIGHT | LBS_OWNERDRAWFIXED | LBS_HASSTRINGS | WS_VSCROLL,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LIST_COMPAT, hInstance, IntPtr.Zero);

        _hWndCompatAdd = Win32.CreateWindowExW(0, "BUTTON", L.Settings_CompatAdd,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_COMPAT_ADD, hInstance, IntPtr.Zero);

        _hWndCompatRemove = Win32.CreateWindowExW(0, "BUTTON", L.Settings_CompatRemove,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_COMPAT_REMOVE, hInstance, IntPtr.Zero);

        // WS_GROUP obligatoire sur le premier radio : termine le groupe Langue,
        // sinon cocher un mode décocherait Français/English.
        _hWndRadioCompatAuto = Win32.CreateWindowExW(0, "BUTTON", L.Settings_CompatModeAuto,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | WS_GROUP | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RADIO_COMPAT_AUTO, hInstance, IntPtr.Zero);

        _hWndRadioCompatForceOn = Win32.CreateWindowExW(0, "BUTTON", L.Settings_CompatModeForceOn,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RADIO_COMPAT_FORCEON, hInstance, IntPtr.Zero);

        _hWndRadioCompatForceOff = Win32.CreateWindowExW(0, "BUTTON", L.Settings_CompatModeForceOff,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_RADIO_COMPAT_FORCEOFF, hInstance, IntPtr.Zero);

        RefreshCompatList(selectProcess: null);
    }

    private void ResizeWindow()
    {
        var (winW, winH) = MeasureRequiredClientSize();
        uint dwStyle = WindowStyle;
        var adjustRect = new Win32.RECT { left = 0, top = 0, right = winW, bottom = winH };
        Win32.AdjustWindowRectEx(ref adjustRect, dwStyle, false, 0);
        int windowW = adjustRect.right - adjustRect.left;
        int windowH = adjustRect.bottom - adjustRect.top;
        Win32.GetWindowRect(_hWnd, out var currentRect);
        int cx = (currentRect.left + currentRect.right) / 2;
        int cy = (currentRect.top + currentRect.bottom) / 2;
        Win32.MoveWindow(_hWnd, cx - windowW / 2, cy - windowH / 2, windowW, windowH, true);
    }

    private void RepositionControls()
    {
        // La taille réelle fait foi depuis que la fenêtre est redimensionnable. Tant que ces
        // deux lignes lisaient les constantes, l'agrandir laissait les contrôles à leur ancienne
        // place pendant que la peinture, elle, suivait déjà le client (OnPaint lit
        // GetClientRect depuis toujours).
        Win32.GetClientRect(_hWnd, out var clientRect);
        LayoutInfo layout = GetLayout(clientRect.right, clientRect.bottom);

        Win32.MoveWindow(_hWndEditKeyboard,
            layout.KeyboardEditRect.left, layout.KeyboardEditRect.top,
            layout.KeyboardEditRect.right - layout.KeyboardEditRect.left,
            layout.KeyboardEditRect.bottom - layout.KeyboardEditRect.top, true);
        Win32.MoveWindow(_hWndEditSearch,
            layout.SearchEditRect.left, layout.SearchEditRect.top,
            layout.SearchEditRect.right - layout.SearchEditRect.left,
            layout.SearchEditRect.bottom - layout.SearchEditRect.top, true);

        Win32.MoveWindow(_hWndValidation,
            layout.ValidationRect.left, layout.ValidationRect.top,
            layout.ValidationRect.right - layout.ValidationRect.left,
            layout.ValidationRect.bottom - layout.ValidationRect.top, true);
        Win32.MoveWindow(_hWndLinkReset,
            layout.ResetRect.left, layout.ResetRect.top,
            layout.ResetRect.right - layout.ResetRect.left,
            layout.ResetRect.bottom - layout.ResetRect.top, true);

        Win32.MoveWindow(_hWndChkAutoStart,
            layout.AutoStartRect.left, layout.AutoStartRect.top,
            layout.AutoStartRect.right - layout.AutoStartRect.left,
            layout.AutoStartRect.bottom - layout.AutoStartRect.top, true);
        Win32.MoveWindow(_hWndChkNotifications,
            layout.NotificationsRect.left, layout.NotificationsRect.top,
            layout.NotificationsRect.right - layout.NotificationsRect.left,
            layout.NotificationsRect.bottom - layout.NotificationsRect.top, true);
        Win32.MoveWindow(_hWndManagedNotifications,
            layout.ManagedNotificationsRect.left, layout.ManagedNotificationsRect.top,
            layout.ManagedNotificationsRect.right - layout.ManagedNotificationsRect.left,
            layout.ManagedNotificationsRect.bottom - layout.ManagedNotificationsRect.top, true);
        Win32.MoveWindow(_hWndChkOnboarding,
            layout.OnboardingRect.left, layout.OnboardingRect.top,
            layout.OnboardingRect.right - layout.OnboardingRect.left,
            layout.OnboardingRect.bottom - layout.OnboardingRect.top, true);
        Win32.MoveWindow(_hWndManagedOnboarding,
            layout.ManagedOnboardingRect.left, layout.ManagedOnboardingRect.top,
            layout.ManagedOnboardingRect.right - layout.ManagedOnboardingRect.left,
            layout.ManagedOnboardingRect.bottom - layout.ManagedOnboardingRect.top, true);
        Win32.MoveWindow(_hWndChkTraining,
            layout.TrainingRect.left, layout.TrainingRect.top,
            layout.TrainingRect.right - layout.TrainingRect.left,
            layout.TrainingRect.bottom - layout.TrainingRect.top, true);
        Win32.MoveWindow(_hWndRadioLangFr,
            layout.LanguageFrRect.left, layout.LanguageFrRect.top,
            layout.LanguageFrRect.right - layout.LanguageFrRect.left,
            layout.LanguageFrRect.bottom - layout.LanguageFrRect.top, true);
        Win32.MoveWindow(_hWndRadioLangEn,
            layout.LanguageEnRect.left, layout.LanguageEnRect.top,
            layout.LanguageEnRect.right - layout.LanguageEnRect.left,
            layout.LanguageEnRect.bottom - layout.LanguageEnRect.top, true);
        Win32.MoveWindow(_hWndManagedLanguage,
            layout.ManagedLanguageRect.left, layout.ManagedLanguageRect.top,
            layout.ManagedLanguageRect.right - layout.ManagedLanguageRect.left,
            layout.ManagedLanguageRect.bottom - layout.ManagedLanguageRect.top, true);
        Win32.MoveWindow(_hWndResetVirtualKeyboardWindow,
            layout.ResetVirtualKeyboardWindowRect.left, layout.ResetVirtualKeyboardWindowRect.top,
            layout.ResetVirtualKeyboardWindowRect.right - layout.ResetVirtualKeyboardWindowRect.left,
            layout.ResetVirtualKeyboardWindowRect.bottom - layout.ResetVirtualKeyboardWindowRect.top, true);
        Win32.MoveWindow(_hWndResetLessonsWindow,
            layout.ResetLessonsWindowRect.left, layout.ResetLessonsWindowRect.top,
            layout.ResetLessonsWindowRect.right - layout.ResetLessonsWindowRect.left,
            layout.ResetLessonsWindowRect.bottom - layout.ResetLessonsWindowRect.top, true);
        Win32.MoveWindow(_hWndCompatList,
            layout.CompatListRect.left, layout.CompatListRect.top,
            layout.CompatListRect.right - layout.CompatListRect.left,
            layout.CompatListRect.bottom - layout.CompatListRect.top, true);
        Win32.MoveWindow(_hWndCompatAdd,
            layout.CompatAddRect.left, layout.CompatAddRect.top,
            layout.CompatAddRect.right - layout.CompatAddRect.left,
            layout.CompatAddRect.bottom - layout.CompatAddRect.top, true);
        Win32.MoveWindow(_hWndCompatRemove,
            layout.CompatRemoveRect.left, layout.CompatRemoveRect.top,
            layout.CompatRemoveRect.right - layout.CompatRemoveRect.left,
            layout.CompatRemoveRect.bottom - layout.CompatRemoveRect.top, true);
        Win32.MoveWindow(_hWndRadioCompatAuto,
            layout.CompatAutoRect.left, layout.CompatAutoRect.top,
            layout.CompatAutoRect.right - layout.CompatAutoRect.left,
            layout.CompatAutoRect.bottom - layout.CompatAutoRect.top, true);
        Win32.MoveWindow(_hWndRadioCompatForceOn,
            layout.CompatForceOnRect.left, layout.CompatForceOnRect.top,
            layout.CompatForceOnRect.right - layout.CompatForceOnRect.left,
            layout.CompatForceOnRect.bottom - layout.CompatForceOnRect.top, true);
        Win32.MoveWindow(_hWndRadioCompatForceOff,
            layout.CompatForceOffRect.left, layout.CompatForceOffRect.top,
            layout.CompatForceOffRect.right - layout.CompatForceOffRect.left,
            layout.CompatForceOffRect.bottom - layout.CompatForceOffRect.top, true);

        for (int i = 0; i < TabCount; i++)
        {
            var r = layout.TabRects[i];
            Win32.MoveWindow(_hWndTabs[i], r.left, r.top, r.right - r.left, r.bottom - r.top, true);
        }

        ApplyTabVisibility(layout.Tab);
    }

    /// <summary>
    /// Montre les contrôles de l'onglet visible et cache les autres. Cacher suffit : Windows
    /// retire un contrôle invisible du parcours de tabulation, ce qui évite d'avoir à toucher
    /// aux styles WS_TABSTOP à chaque changement d'onglet.
    /// </summary>
    private void ApplyTabVisibility(int tab)
    {
        Show(_hWndEditKeyboard, tab == TabShortcuts);
        Show(_hWndEditSearch, tab == TabShortcuts);
        Show(_hWndLinkReset, tab == TabShortcuts);
        Show(_hWndValidation, tab == TabShortcuts);

        Show(_hWndChkAutoStart, tab == TabPreferences);
        Show(_hWndChkNotifications, tab == TabPreferences);
        Show(_hWndChkOnboarding, tab == TabPreferences);
        Show(_hWndChkTraining, tab == TabPreferences);
        Show(_hWndRadioLangFr, tab == TabPreferences);
        Show(_hWndRadioLangEn, tab == TabPreferences);
        Show(_hWndResetVirtualKeyboardWindow, tab == TabPreferences);
        Show(_hWndResetLessonsWindow, tab == TabPreferences);
        Show(_hWndManagedNotifications, tab == TabPreferences && _managedNotifications);
        Show(_hWndManagedOnboarding, tab == TabPreferences && _managedOnboarding);
        Show(_hWndManagedLanguage, tab == TabPreferences && _managedLanguage);

        Show(_hWndCompatList, tab == TabCompat);
        Show(_hWndCompatAdd, tab == TabCompat);
        Show(_hWndCompatRemove, tab == TabCompat);
        Show(_hWndRadioCompatAuto, tab == TabCompat);
        Show(_hWndRadioCompatForceOn, tab == TabCompat);
        Show(_hWndRadioCompatForceOff, tab == TabCompat);
    }

    private static void Show(IntPtr control, bool visible)
    {
        if (control != IntPtr.Zero)
            Win32.ShowWindow(control, visible ? 5 : 0); // SW_SHOW / SW_HIDE
    }

    /// <summary>
    /// Change d'onglet. La fenêtre ne change pas de taille : MeasureRequiredClientSize prend
    /// déjà le plus grand des trois, pour qu'un changement d'onglet ne fasse pas sauter la
    /// fenêtre sous le curseur.
    /// </summary>
    private void SetActiveTab(int tab)
    {
        if (tab < 0 || tab >= TabCount || tab == _activeTab)
            return;

        _activeTab = tab;
        RepositionControls();
        InvalidateOwnerDrawControls();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    /// <summary>Les trois onglets, arrêtés par Antoine le 2026-08-30.</summary>
    private const int TabCount = 3;
    private const int TabShortcuts = 0;
    private const int TabPreferences = 1;
    private const int TabCompat = 2;

    private static string TabLabel(int tab) => tab switch
    {
        TabShortcuts => L.Settings_SectionShortcuts,
        TabCompat => L.Settings_SectionCompat,
        _ => L.Settings_SectionPreferences,
    };

    private LayoutInfo GetLayout(int winW, int winH) => GetLayout(winW, winH, _activeTab);

    /// <summary>
    /// Mise en page d'un onglet. Les trois flux partent du même haut de panneau : seul celui de
    /// l'onglet demandé décide de la hauteur du panneau, les deux autres sont calculés quand
    /// même — <see cref="MeasureRequiredClientSize"/> en a besoin pour que la fenêtre ne change
    /// pas de taille quand on change d'onglet.
    /// </summary>
    private LayoutInfo GetLayout(int winW, int winH, int tab)
    {
        int margin = S(8);
        int contentWidth = winW - margin * 2;
        int headerTop = S(8);
        int logoSize = S(24);
        int panelPadX = S(9);

        IntPtr hdc = Win32.GetDC(_hWnd);
        try
        {
            int titleHeight = MeasureSingleLineHeight(hdc, _hFontTitle);
            int versionHeight = MeasureSingleLineHeight(hdc, _hFontVersion);
            int headerLineHeight = Math.Max(logoSize, Math.Max(titleHeight, versionHeight) + S(4));
            int logoY = headerTop + Math.Max(0, (headerLineHeight - logoSize) / 2);
            int headerTitleY = headerTop + Math.Max(0, (headerLineHeight - titleHeight) / 2);
            int headerBottom = headerTop + headerLineHeight + S(9);

            int panelTitleHeight = MeasureSingleLineHeight(hdc, _hFontPanelTitle);
            int textLineHeight = MeasureSingleLineHeight(hdc, _hFontText);
            // + la marge de focus des deux côtés : l'anneau se dessine à l'extérieur du
            // contrôle, donc TryDrawItem rend le libellé dans un rectangle rentré d'autant.
            int focusMargin = ThemeControls.FocusMargin(_dpi);
            int checkboxHeight = Math.Max(S(18), MeasureSingleLineHeight(hdc, _hFontBold))
                + focusMargin * 2;
            int buttonHeight = ThemeControls.MeasureButtonHeight(hdc, _hFontButton, _dpi)
                + focusMargin * 2;
            int linkHeight = MeasureSingleLineHeight(hdc, _hFontLinkStrong);
            int validationHeight = MeasureSingleLineHeight(hdc, _hFontSmall);

            // ── Barre d'onglets ─────────────────────────────────────────
            // Largeurs inégales, chacune celle de son libellé : un intitulé court n'a aucune
            // raison d'occuper la place du plus long, et la traduction anglaise ne les aligne
            // pas non plus.
            int tabHeight = ThemeControls.MeasureTabHeight(hdc, _hFontBold, _dpi);
            int tabTop = headerBottom + S(6);
            var tabRects = new Win32.RECT[TabCount];
            int tabX = margin;
            for (int i = 0; i < TabCount; i++)
            {
                int tabW = ThemeControls.MeasureTabWidth(hdc, _hFontBold, TabLabel(i), _dpi);
                tabRects[i] = Rect(tabX, tabTop, tabW, tabHeight);
                tabX += tabW;
            }

            int panelTop = tabTop + tabHeight;
            int labelX = margin + panelPadX;
            int innerWidth = contentWidth - panelPadX * 2;
            int cursor = panelTop + S(12);

            // ── Onglet Raccourcis ───────────────────────────────────────
            int labelWidth = Math.Max(
                MeasureSingleLineWidth(hdc, _hFontText, L.Settings_ShortcutLabelKeyboard),
                MeasureSingleLineWidth(hdc, _hFontText, L.Settings_ShortcutLabelSearch)) + S(6);
            int keyOuterW = S(28);
            int keyOuterH = S(24);
            int shortcutX = labelX + labelWidth + S(6);
            // La boîte suit le préfixe « Ctrl + Maj + » au lieu de fuir au bord droit : depuis
            // que la fenêtre s'élargit avec son contenu, la coller à droite laissait la touche
            // à cinquante pixels du « + » qu'elle complète.
            int shortcutWidth = 0;
            foreach (var (runText, _, runFont) in GetShortcutPrefixRuns())
                shortcutWidth += MeasureSingleLineWidth(hdc, runFont, runText);
            int keyOuterX = shortcutX + shortcutWidth + S(4);

            int keyboardRowY = cursor;
            int searchRowY = keyboardRowY + Math.Max(S(28), textLineHeight + S(11));

            var keyboardBoxRect = Rect(keyOuterX, keyboardRowY - S(4), keyOuterW, keyOuterH);
            var searchBoxRect = Rect(keyOuterX, searchRowY - S(4), keyOuterW, keyOuterH);
            var keyboardEditRect = Rect(keyOuterX + 1, keyboardRowY - S(3), keyOuterW - 2, keyOuterH - 2);
            var searchEditRect = Rect(keyOuterX + 1, searchRowY - S(3), keyOuterW - 2, keyOuterH - 2);

            int resetY = searchRowY + Math.Max(S(20), textLineHeight + S(7));
            int resetWidth = MeasureSingleLineWidth(hdc, _hFontLinkStrong, L.Settings_LinkResetDefaults);
            var resetRect = Rect(labelX, resetY, resetWidth, Math.Max(S(18), linkHeight));

            bool showValidation = !string.IsNullOrEmpty(_validationMessage);
            int validationTop = showValidation ? resetRect.bottom + S(5) : resetRect.bottom;
            int currentValidationHeight = showValidation ? Math.Max(S(15), validationHeight) : 0;
            var validationRect = Rect(labelX, validationTop, innerWidth, currentValidationHeight);
            int shortcutsBottom = validationRect.bottom;

            // ── Onglet Préférences ──────────────────────────────────────
            int checkboxGap = S(6);
            int managedHeight = Math.Max(S(13), validationHeight);
            int managedIndent = S(18);
            int managedGap = S(2);
            int managedWidth = innerWidth - managedIndent;

            var autoStartRect = Rect(labelX, cursor, innerWidth, checkboxHeight);
            var notificationsRect = Rect(labelX, autoStartRect.bottom + checkboxGap,
                innerWidth, checkboxHeight);
            // Lignes « Géré par votre organisation » : sous la case, décalées de la largeur de
            // la coche pour s'aligner sur son libellé. Hauteur nulle quand rien n'est imposé.
            var managedNotificationsRect = Rect(labelX + managedIndent,
                notificationsRect.bottom + managedGap, managedWidth,
                _managedNotifications ? managedHeight : 0);
            var onboardingRect = Rect(labelX,
                (_managedNotifications ? managedNotificationsRect.bottom : notificationsRect.bottom) + checkboxGap,
                innerWidth, checkboxHeight);
            var managedOnboardingRect = Rect(labelX + managedIndent,
                onboardingRect.bottom + managedGap, managedWidth,
                _managedOnboarding ? managedHeight : 0);
            var trainingRect = Rect(labelX,
                (_managedOnboarding ? managedOnboardingRect.bottom : onboardingRect.bottom) + checkboxGap,
                innerWidth, checkboxHeight);

            int languageTitleTop = trainingRect.bottom + S(18);
            var languageFrRect = Rect(labelX, languageTitleTop + panelTitleHeight + S(9),
                innerWidth, checkboxHeight);
            var languageEnRect = Rect(labelX, languageFrRect.bottom + checkboxGap,
                innerWidth, checkboxHeight);
            var managedLanguageRect = Rect(labelX + managedIndent,
                languageEnRect.bottom + managedGap, managedWidth,
                _managedLanguage ? managedHeight : 0);

            int windowsTitleTop =
                (_managedLanguage ? managedLanguageRect.bottom : languageEnRect.bottom) + S(18);
            var resetVirtualKeyboardWindowRect = Rect(labelX, windowsTitleTop + panelTitleHeight + S(9),
                innerWidth, buttonHeight);
            var resetLessonsWindowRect = Rect(labelX, resetVirtualKeyboardWindowRect.bottom + S(7),
                innerWidth, buttonHeight);
            int preferencesBottom = resetLessonsWindowRect.bottom;

            // ── Onglet Apps suspendues ──────────────────────────────────
            var compatListRect = Rect(labelX, cursor, innerWidth, S(58));
            int compatBtnW = (innerWidth - S(6)) / 2;
            var compatAddRect = Rect(labelX, compatListRect.bottom + S(6), compatBtnW, buttonHeight);
            var compatRemoveRect = Rect(labelX + compatBtnW + S(6), compatListRect.bottom + S(6),
                innerWidth - compatBtnW - S(6), buttonHeight);
            var compatAutoRect = Rect(labelX, compatAddRect.bottom + S(8), innerWidth, checkboxHeight);
            var compatForceOnRect = Rect(labelX, compatAutoRect.bottom + S(4), innerWidth, checkboxHeight);
            var compatForceOffRect = Rect(labelX, compatForceOnRect.bottom + S(4), innerWidth, checkboxHeight);
            int compatBottom = compatForceOffRect.bottom;

            int activeBottom = tab switch
            {
                TabShortcuts => shortcutsBottom,
                TabCompat => compatBottom,
                _ => preferencesBottom,
            };
            // Le panneau descend jusqu'au bas de la fenêtre quand celle-ci est plus haute que
            // son contenu — ce qui est le cas dès qu'on ouvre un onglet court, la fenêtre étant
            // dimensionnée sur le plus haut des trois, et dès que l'utilisateur l'agrandit.
            // PanelBottom garde la hauteur mesurée : c'est elle que MeasureRequiredClientSize
            // additionne, pas celle du panneau étiré.
            int panelBottom = activeBottom + S(12);
            int panelDrawBottom = Math.Max(panelBottom, winH - margin);

            return new LayoutInfo
            {
                Margin = margin,
                HeaderTitleX = margin + logoSize + S(6),
                HeaderTitleY = headerTitleY,
                HeaderDividerY = headerBottom,
                LogoRect = Rect(margin, logoY, logoSize, logoSize),
                Tab = tab,
                TabRects = tabRects,
                TabStripBottom = panelTop,
                ContentPanel = Rect(margin, panelTop, contentWidth, panelDrawBottom - panelTop),
                PanelBottom = panelBottom,
                ShortcutsLabelX = labelX,
                ShortcutsLabelWidth = labelWidth,
                ShortcutsShortcutX = shortcutX,
                ShortcutsShortcutWidth = shortcutWidth,
                KeyboardRowY = keyboardRowY,
                SearchRowY = searchRowY,
                KeyboardBoxRect = keyboardBoxRect,
                SearchBoxRect = searchBoxRect,
                KeyboardEditRect = keyboardEditRect,
                SearchEditRect = searchEditRect,
                ValidationRect = validationRect,
                ResetRect = resetRect,
                AutoStartRect = autoStartRect,
                NotificationsRect = notificationsRect,
                ManagedNotificationsRect = managedNotificationsRect,
                OnboardingRect = onboardingRect,
                ManagedOnboardingRect = managedOnboardingRect,
                TrainingRect = trainingRect,
                LanguageTitleTop = languageTitleTop,
                LanguageFrRect = languageFrRect,
                LanguageEnRect = languageEnRect,
                ManagedLanguageRect = managedLanguageRect,
                WindowsTitleTop = windowsTitleTop,
                ResetVirtualKeyboardWindowRect = resetVirtualKeyboardWindowRect,
                ResetLessonsWindowRect = resetLessonsWindowRect,
                CompatListRect = compatListRect,
                CompatAddRect = compatAddRect,
                CompatRemoveRect = compatRemoveRect,
                CompatAutoRect = compatAutoRect,
                CompatForceOnRect = compatForceOnRect,
                CompatForceOffRect = compatForceOffRect,
            };
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }
    }

    /// <summary>
    /// Taille de client où rien n'est tronqué, à ce DPI et dans cette langue. Elle sert de
    /// taille d'ouverture et de plancher au redimensionnement (<c>WM_GETMINMAXINFO</c>).
    ///
    /// La largeur est celle du plus large bloc qui ne peut pas se replier : l'en-tête, la ligne
    /// de raccourci, le lien, les neuf cases et radios, les quatre boutons. La hauteur vient du
    /// bas du dernier contrôle que <see cref="GetLayout"/> place, majorée de la ligne de
    /// validation quand elle n'est pas affichée : sans cette réserve, saisir un raccourci
    /// invalide ferait grandir la fenêtre sous le curseur.
    ///
    /// Les cases se mesurent à l'anatomie de la charte (<see cref="ThemeControls.MeasureBoxRowWidth"/>)
    /// et non à celle du contrôle Windows qui les peint encore : c'est la cible du lot suivant,
    /// et sa boîte est la plus large des deux.
    /// </summary>
    private (int Width, int Height) MeasureRequiredClientSize()
    {
        int margin = S(8);
        int panelPadX = S(9);
        int logoSize = S(24);
        int width;
        int reserve;

        IntPtr hdc = Win32.GetDC(_hWnd);
        try
        {
            int Box(string text) =>
                ThemeControls.MeasureBoxRowWidth(hdc, _hFontBold, text, _dpi);
            int Button(string text) =>
                ThemeControls.MeasureButtonWidth(hdc, _hFontButton, text, _dpi)
                    + ThemeControls.FocusMargin(_dpi) * 2;

            // En-tête : logo, titre, numéro de version. Il occupe toute la largeur de client,
            // pas l'intérieur d'un panneau.
            string version = $"v{Program.Version}";
            int headerWidth = margin + logoSize + S(6)
                + MeasureSingleLineWidth(hdc, _hFontTitle, ProductIdentity.DisplayName) + S(8)
                + MeasureSingleLineWidth(hdc, _hFontVersion, version) + S(24) + S(6) + margin;

            // La barre d'onglets ne se replie pas : les trois intitulés bout à bout sont un
            // plancher de largeur au même titre que l'en-tête.
            int tabStripWidth = margin * 2;
            for (int i = 0; i < TabCount; i++)
                tabStripWidth += ThemeControls.MeasureTabWidth(hdc, _hFontBold, TabLabel(i), _dpi);
            headerWidth = Math.Max(headerWidth, tabStripWidth);

            // Ligne de raccourci : étiquette, préfixe « Ctrl + Maj + », boîte de touche.
            int labelWidth = Math.Max(
                MeasureSingleLineWidth(hdc, _hFontText, L.Settings_ShortcutLabelKeyboard),
                MeasureSingleLineWidth(hdc, _hFontText, L.Settings_ShortcutLabelSearch)) + S(6);
            int prefixWidth = 0;
            foreach (var (text, _, font) in GetShortcutPrefixRuns())
                prefixWidth += MeasureSingleLineWidth(hdc, font, text);
            int shortcutRow = labelWidth + S(6) + prefixWidth + S(8) + S(28);

            int inner = shortcutRow;
            inner = Math.Max(inner,
                MeasureSingleLineWidth(hdc, _hFontLinkStrong, L.Settings_LinkResetDefaults));
            inner = Math.Max(inner, Box(L.Settings_AutoStart));
            inner = Math.Max(inner, Box(L.Settings_Notifications));
            inner = Math.Max(inner, Box(L.Settings_OnboardingWindow));
            inner = Math.Max(inner, Box(L.Challenge_OptIn));
            inner = Math.Max(inner, Box("Français"));
            inner = Math.Max(inner, Box("English"));
            inner = Math.Max(inner, Box(L.Settings_CompatModeAuto));
            inner = Math.Max(inner, Box(L.Settings_CompatModeForceOn));
            inner = Math.Max(inner, Box(L.Settings_CompatModeForceOff));
            inner = Math.Max(inner, Button(L.Settings_ResetVirtualKeyboard));
            inner = Math.Max(inner, Button(L.Settings_ResetLessonsModule));
            inner = Math.Max(inner,
                Button(L.Settings_CompatAdd) + S(6) + Button(L.Settings_CompatRemove));

            // Les lignes « Géré par votre organisation » sont indentées sous leur case.
            inner = Math.Max(inner, S(18)
                + MeasureSingleLineWidth(hdc, _hFontSmall, L.Settings_ManagedByOrganization));

            width = Math.Max(S(BASE_WIN_W),
                Math.Max(headerWidth, inner + panelPadX * 2 + margin * 2));

            // Réserve de la ligne de validation, que GetLayout n'insère que lorsqu'un message
            // est en cours. La fenêtre ne doit pas changer de taille pour un message.
            reserve = string.IsNullOrEmpty(_validationMessage)
                ? S(5) + Math.Max(S(15), MeasureSingleLineHeight(hdc, _hFontSmall))
                : 0;
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }

        // GetLayout prend son propre DC : le nôtre est rendu avant de l'appeler. La hauteur
        // est celle du plus haut des trois onglets, pas celle de l'onglet visible : changer
        // d'onglet ne doit pas faire sauter la fenêtre sous le curseur.
        int height = 0;
        for (int tab = 0; tab < TabCount; tab++)
            height = Math.Max(height, GetLayout(width, 0, tab).PanelBottom);

        return (width, height + margin + reserve);
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

    /// <summary>Pour le banc de captures : la fenêtre est rendue, elle n'est pas pilotée.</summary>
    internal IntPtr Handle => _hWnd;

    public void Show()
    {
        LoadShortcutStateFromConfig();
        RefreshShortcutTexts();
        RefreshAutoStartCheckbox();
        // Les overrides ont pu changer via le sous-menu tray Compatibilité depuis la
        // dernière ouverture : resynchroniser la section Apps suspendues.
        RefreshCompatList(SelectedCompatProcess());
        // L'opt-in Défi du jour a pu changer via l'onboarding.
        SetCheck(_hWndChkTraining, ConfigManager.TrainingEnabled);
        SetCheck(_hWndChkNotifications, ConfigManager.NotificationsEnabled);
        // Re-synchroniser la checkbox onboarding a chaque ouverture : l'utilisateur a pu
        // modifier l'etat via la case « Ne plus afficher » du wizard depuis la derniere
        // fermeture des Settings.
        SetCheck(_hWndChkOnboarding, ConfigManager.ShowOnboardingAtStartup);
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
        bool autoStart = GetCheck(_hWndChkAutoStart);
        bool autoStartWasRegistered = AutoStart.IsRegistered;
        bool autoStartSaved = AutoStart.Set(autoStart);
        RefreshAutoStartCheckbox();
        if (!autoStartSaved)
            ShowAutoStartError();
        // Un choix fait à la main éteint la relance, dans un sens comme dans l'autre —
        // même doctrine que ToggleAutoStart pour le menu tray (R2 de l'audit v1.2.0).
        // Seul un changement réel compte : refermer Paramètres sans toucher la case
        // n'est pas un choix, et ne doit pas consommer la proposition.
        else if (autoStart != autoStartWasRegistered)
            AutoStartNudge.MarkPromptShown();

        // Un réglage imposé ne se réécrit pas dans config.json : la case affiche la valeur de
        // la politique, la persister écraserait le choix de l'utilisateur, qui doit reprendre
        // effet le jour où la politique est retirée.
        if (!_managedNotifications)
        {
            bool notifications = GetCheck(_hWndChkNotifications);
            ConfigManager.SetNotifications(notifications);
        }

        if (!_managedOnboarding)
        {
            bool showOnboarding = GetCheck(_hWndChkOnboarding);
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

            case Win32.WM_SIZE:
                // _hWnd est encore nul pendant CreateWindowExW, qui envoie déjà ce message :
                // les contrôles n'existent pas, il n'y a rien à replacer.
                if (_hWnd != IntPtr.Zero)
                {
                    RepositionControls();
                    Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                }
                return IntPtr.Zero;

            case Win32.WM_GETMINMAXINFO:
            {
                // Recalculé à chaque demande plutôt que mémorisé : la langue et le DPI changent
                // tous les deux en cours de vie de la fenêtre, et un plancher périmé laisserait
                // l'utilisateur réduire la fenêtre sous la taille de son propre contenu.
                var (minW, minH) = MeasureRequiredClientSize();
                var frame = new Win32.RECT { left = 0, top = 0, right = minW, bottom = minH };
                Win32.AdjustWindowRectEx(ref frame, WindowStyle, false, 0);
                var mmi = Marshal.PtrToStructure<Win32.MINMAXINFO>(lParam);
                mmi.ptMinTrackSize.x = frame.right - frame.left;
                mmi.ptMinTrackSize.y = frame.bottom - frame.top;
                Marshal.StructureToPtr(mmi, lParam, false);
                return IntPtr.Zero;
            }

            case Win32.WM_DPICHANGED:
            {
                int newDpi = (wParam.ToInt32() >> 16) & 0xFFFF;
                if (newDpi > 0)
                    _dpiScale = newDpi / 96f;
                ApplyFontsToControls();
                var suggested = Marshal.PtrToStructure<Win32.RECT>(lParam);
                Win32.MoveWindow(_hWnd, suggested.left, suggested.top,
                    suggested.right - suggested.left, suggested.bottom - suggested.top, true);
                RepositionControls();
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                return IntPtr.Zero;
            }

            case Win32.WM_DRAWITEM:
                if (TryDrawItem(lParam))
                    return (IntPtr)1;
                break;

            case Win32.WM_MEASUREITEM:
                if (TryMeasureItem(lParam))
                    return (IntPtr)1;
                break;

            case Win32.WM_CTLCOLORLISTBOX:
                // La liste peint ses lignes, pas la bande que Windows laisse sous la dernière.
                Win32.SetBkColor(wParam, CLR_PANEL_BG);
                return Theme.Brush(CLR_PANEL_BG);

            case Win32.WM_COMMAND:
            {
                int id = wParam.ToInt32() & 0xFFFF;
                int code = (wParam.ToInt32() >> 16) & 0xFFFF;
                switch (id)
                {
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
                    // Les quatre cases basculent ici depuis qu'elles sont owner-draw :
                    // BS_AUTOCHECKBOX le faisait avant d'envoyer BN_CLICKED, plus personne ne
                    // le fait à sa place. Les trois dernières n'avaient aucun cas — leur valeur
                    // n'était lue qu'à la fermeture de la fenêtre, ce qui suffisait tant que
                    // Windows la tenait.
                    case IDC_TAB_SHORTCUTS:
                        if (code == 0) SetActiveTab(TabShortcuts);
                        break;
                    case IDC_TAB_PREFERENCES:
                        if (code == 0) SetActiveTab(TabPreferences);
                        break;
                    case IDC_TAB_COMPAT:
                        if (code == 0) SetActiveTab(TabCompat);
                        break;
                    case IDC_CHK_TRAINING:
                        if (code == 0)
                        {
                            ToggleCheck(_hWndChkTraining);
                            ConfigManager.SetTrainingEnabled(GetCheck(_hWndChkTraining));
                        }
                        break;
                    case IDC_CHK_AUTOSTART:
                        if (code == 0) ToggleCheck(_hWndChkAutoStart);
                        break;
                    case IDC_CHK_NOTIFICATIONS:
                        if (code == 0) ToggleCheck(_hWndChkNotifications);
                        break;
                    case IDC_CHK_ONBOARDING:
                        if (code == 0) ToggleCheck(_hWndChkOnboarding);
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
                if (hCtrl == _hWndLinkReset)
                {
                    Win32.SetBkMode(hdcStatic, 1);
                    Win32.SetTextColor(hdcStatic, CLR_LINK);
                    return _hPanelBrush;
                }

                if (hCtrl == _hWndValidation)
                {
                    Win32.SetBkMode(hdcStatic, 1);
                    Win32.SetTextColor(hdcStatic, (_keyboardValid && _searchValid) ? CLR_VALID : CLR_INVALID);
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

    private void SetValidationMessage(string text, bool captureHint = false)
    {
        _validationMessage = text;
        _showCaptureHint = captureHint;
        Win32.SetWindowTextW(_hWndValidation, text);
        if (_hWnd != IntPtr.Zero && _hWndValidation != IntPtr.Zero)
            RepositionControls();
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
        SetCheck(_hWndChkAutoStart, AutoStart.IsRegistered);
    }

    private void RefreshLanguageRadios()
    {
        bool isEnglish = ConfigManager.AppLanguage == "en";
        SetCheck(_hWndRadioLangFr, !isEnglish);
        SetCheck(_hWndRadioLangEn, isEnglish);
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
        RepositionControls();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    /// <summary>R\u00e9applique tous les libell\u00e9s traduits de cette fen\u00eatre (appel\u00e9 apr\u00e8s un changement de langue).</summary>
    private void RefreshLanguageTexts()
    {
        Win32.SetWindowTextW(_hWnd, L.Settings_WindowTitle);
        Win32.SetWindowTextW(_hWndLinkReset, L.Settings_LinkResetDefaults);
        Win32.SetWindowTextW(_hWndChkAutoStart, L.Settings_AutoStart);
        Win32.SetWindowTextW(_hWndChkNotifications, L.Settings_Notifications);
        Win32.SetWindowTextW(_hWndChkOnboarding, L.Settings_OnboardingWindow);
        Win32.SetWindowTextW(_hWndManagedNotifications, L.Settings_ManagedByOrganization);
        Win32.SetWindowTextW(_hWndManagedOnboarding, L.Settings_ManagedByOrganization);
        Win32.SetWindowTextW(_hWndManagedLanguage, L.Settings_ManagedByOrganization);
        Win32.SetWindowTextW(_hWndChkTraining, L.Challenge_OptIn);
        Win32.SetWindowTextW(_hWndResetVirtualKeyboardWindow, L.Settings_ResetVirtualKeyboard);
        Win32.SetWindowTextW(_hWndResetLessonsWindow, L.Settings_ResetLessonsModule);
        Win32.SetWindowTextW(_hWndCompatAdd, L.Settings_CompatAdd);
        Win32.SetWindowTextW(_hWndCompatRemove, L.Settings_CompatRemove);
        Win32.SetWindowTextW(_hWndRadioCompatAuto, L.Settings_CompatModeAuto);
        Win32.SetWindowTextW(_hWndRadioCompatForceOn, L.Settings_CompatModeForceOn);
        Win32.SetWindowTextW(_hWndRadioCompatForceOff, L.Settings_CompatModeForceOff);
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

        SetCheck(_hWndRadioCompatAuto, hasSelection && mode == null);
        SetCheck(_hWndRadioCompatForceOn, mode == "forceOn");
        SetCheck(_hWndRadioCompatForceOff, mode == "forceOff");
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
            SetValidationMessage(L.Settings_CompatForceOnRefused);
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
            SetValidationMessage(L.Settings_ShortcutReserved);
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
            return;
        }

        if (vk == otherVk)
        {
            SetShortcutValidity(hWndShortcut, false);
            SetValidationMessage(L.Settings_ShortcutAlreadyUsed);
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
                if (lParam != IntPtr.Zero)
                {
                    var inputMsg = Marshal.PtrToStructure<Win32.MSG>(lParam);
                    if ((inputMsg.message == Win32.WM_KEYDOWN || inputMsg.message == Win32.WM_SYSKEYDOWN) &&
                        inputMsg.wParam == (IntPtr)VK_TAB)
                        return baseResult;
                }

                return (IntPtr)(baseResult.ToInt64() | DLGC_WANTALLKEYS);
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

    private IntPtr LinkSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        switch (msg)
        {
            case Win32.WM_MOUSEMOVE:
                if (_hoveredLink != hWnd)
                {
                    _hoveredLink = hWnd;
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);
                    var tme = new Win32.TRACKMOUSEEVENT
                    {
                        cbSize = (uint)Marshal.SizeOf<Win32.TRACKMOUSEEVENT>(),
                        dwFlags = Win32.TME_LEAVE,
                        hwndTrack = hWnd
                    };
                    Win32.TrackMouseEvent(ref tme);
                }
                break;
            case Win32.WM_MOUSELEAVE:
                if (_hoveredLink == hWnd)
                {
                    _hoveredLink = IntPtr.Zero;
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);
                }
                break;
        }

        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private void OnPaint(IntPtr hWnd)
    {
        var hdcPaint = Win32.BeginPaint(hWnd, out var ps);
        Win32.GetClientRect(hWnd, out var clientRect);
        int cw = clientRect.right;
        int ch = clientRect.bottom;
        LayoutInfo layout = GetLayout(cw, ch);

        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        var hdc = Win32.CreateCompatibleDC(hdcScreen);
        var hBmp = Win32.CreateCompatibleBitmap(hdcScreen, cw, ch);
        var hBmpOld = Win32.SelectObject(hdc, hBmp);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);

        Win32.FillRect(hdc, ref clientRect, _hBgBrush);
        Win32.SetBkMode(hdc, 1);

        Win32.GdipCreateFromHDC(hdc, out IntPtr gfx);
        if (gfx != IntPtr.Zero)
        {
            Win32.GdipSetSmoothingMode(gfx, 4);
            Win32.GdipSetInterpolationMode(gfx, 7);
            Win32.GdipSetTextRenderingHint(gfx, 4);
        }

        DrawHeader(hdc, gfx, layout, cw);
        // Le fond de la barre d'onglets appartient au papier : les trois boutons se peignent
        // eux-mêmes et WS_CLIPCHILDREN les découpe de ce tampon.
        GdiHelpers.DrawPanel(hdc, layout.ContentPanel, CLR_PANEL_BG, CLR_PANEL_BORDER, 0, 0);
        switch (layout.Tab)
        {
            case TabShortcuts:
                PaintShortcutTab(hdc, layout);
                break;
            case TabCompat:
                PaintCompatTab(hdc, layout);
                break;
            default:
                PaintPreferencesTab(hdc, layout);
                break;
        }

        if (gfx != IntPtr.Zero)
            Win32.GdipDeleteGraphics(gfx);

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
            // S(20) pour une police de 24 px : le titre était écrasé en haut et en bas.
            bottom = layout.HeaderTitleY + titleHeight
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

    /// <summary>
    /// Titre d'un bloc à l'intérieur d'un onglet, précédé de son filet. Le **premier** bloc d'un
    /// onglet n'en reçoit pas : l'onglet le nomme déjà, et répéter « Raccourcis » sous l'onglet
    /// « Raccourcis » ne dit rien de plus. Seuls Langue et Fenêtres en portent un, parce qu'ils
    /// partagent l'onglet Préférences.
    /// </summary>
    private void PaintSectionTitle(IntPtr hdc, LayoutInfo layout, int top, string title)
    {
        int left = layout.ContentPanel.left + S(12);
        int right = layout.ContentPanel.right - S(12);
        GdiHelpers.FillSolidRect(hdc, Rect(left, top - S(8), right - left, 1), CLR_SEPARATOR);

        Win32.SelectObject(hdc, _hFontPanelTitle);
        Win32.SetTextColor(hdc, CLR_LINK);
        var titleRect = new Win32.RECT
        {
            left = left,
            top = top,
            right = right,
            bottom = top + MeasureSingleLineHeight(hdc, _hFontPanelTitle)
        };
        Win32.DrawTextW(hdc, title, -1, ref titleRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
    }

    private void PaintShortcutTab(IntPtr hdc, LayoutInfo layout)
    {
        DrawShortcutRow(hdc, layout.ShortcutsLabelX, layout.ShortcutsLabelWidth, layout.KeyboardRowY,
            L.Settings_ShortcutLabelKeyboard, GetShortcutPrefixRuns(), layout.ShortcutsShortcutX, layout.ShortcutsShortcutWidth);
        DrawKeyBox(hdc, layout.KeyboardBoxRect, _keyboardValid, _focusedShortcut == _hWndEditKeyboard);

        DrawShortcutRow(hdc, layout.ShortcutsLabelX, layout.ShortcutsLabelWidth, layout.SearchRowY,
            L.Settings_ShortcutLabelSearch, GetShortcutPrefixRuns(), layout.ShortcutsShortcutX, layout.ShortcutsShortcutWidth);
        DrawKeyBox(hdc, layout.SearchBoxRect, _searchValid, _focusedShortcut == _hWndEditSearch);
    }

    private void PaintPreferencesTab(IntPtr hdc, LayoutInfo layout)
    {
        PaintSectionTitle(hdc, layout, layout.LanguageTitleTop, L.Settings_SectionLanguage);
        PaintSectionTitle(hdc, layout, layout.WindowsTitleTop, L.Settings_SectionWindows);
    }

    private void PaintCompatTab(IntPtr hdc, LayoutInfo layout)
    {
        // Le cadre de la liste, que WS_BORDER dessinait aux couleurs du système. Même primitive
        // que les champs de raccourci : une liste est un champ, du point de vue de la charte.
        ThemeControls.DrawFieldFrame(hdc, layout.CompatListRect, ControlState.None,
            Theme.Current, _dpi);
        ThemeControls.DrawFieldFrame(hdc, layout.CompatListRect, ControlState.None,
            Theme.Current, _dpi);
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
        var rect = new Win32.RECT { left = x, top = y, right = x + width, bottom = y + S(22) };
        Win32.DrawTextW(hdc, title, -1, ref rect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
    }

    // Méthodes GDI factorisées dans GdiHelpers.cs — wrappers d'instance pour le DPI scaling
    private int MeasureTextHeight(IntPtr hdc, IntPtr hFont, string text, int width,
        uint format = Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX)
        => GdiHelpers.MeasureTextHeight(hdc, hFont, text, width, format);

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

    private (string Text, uint Color, IntPtr Font)[] GetShortcutRuns(params string[] keys)
    {
        var runs = new List<(string Text, uint Color, IntPtr Font)>();
        for (int i = 0; i < keys.Length; i++)
        {
            if (i > 0)
                runs.Add((" + ", CLR_TEXT, _hFontText));
            runs.Add((keys[i], CLR_INLINE_HIGHLIGHT, _hFontBold));
        }

        return runs.ToArray();
    }

    public void Dispose()
    {
        if (_onAppLanguageChanged != null)
            ConfigManager.AppLanguageChanged -= _onAppLanguageChanged;
        if (_hWndEditKeyboard != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndEditKeyboard, _shortcutSubclassProc, (UIntPtr)3);
        if (_hWndEditSearch != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndEditSearch, _shortcutSubclassProc, (UIntPtr)4);
        if (_hWndLinkReset != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndLinkReset, _linkSubclassProc, (UIntPtr)2);
        DetachHoverTracking();
        if (_hWnd != IntPtr.Zero)
        {
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }

        if (_themeChanged != null)
        {
            Theme.Changed -= _themeChanged;
            _themeChanged = null;
        }

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
