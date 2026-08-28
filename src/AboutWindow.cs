// Fenetre « A propos » — informations sur l'application, licence et liens.
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Mini-fenetre custom GDI affichant version, licence EUPL 1.2, mention AMCF
/// et 3 liens cliquables (site, code source, licence). Calque sur SettingsWindow
/// pour le style (fond clair, double buffering, hover liens, DPI-aware).
/// </summary>
sealed class AboutWindow : IDisposable
{
    // ── Window constants ────────────────────────────────────────────
    private const uint SS_NOTIFY = 0x0100;

    // ── Control IDs ─────────────────────────────────────────────────
    private const int IDC_LINK_SITE = 4101;
    private const int IDC_LINK_GITHUB = 4102;
    private const int IDC_LINK_LICENSE = 4103;
    private const int IDC_BTN_CLOSE = 4104;
    private const int IDC_LINK_AMCF = 4105;

    private const int BASE_WIN_W = 560;
    private const int BASE_WIN_H = 260;

    // ── Couleurs, jetons de la charte ──────────────────────────────
    //
    // Propriétés et non constantes : la palette suit le thème de Windows et bascule à chaud,
    // donc une valeur figée à la compilation peindrait le thème du démarrage jusqu'à la
    // fermeture. Les noms CLR_* ne changent pas, pour que les sites d'appel ne bougent pas.
    //
    // Ce que cette fenêtre perd au passage : le gris #333333 de sa description, qui devient
    // l'encre, et surtout ses deux couleurs de lien — l'orange #D47800 au repos et le bleu
    // #0078D4 au survol, qui sont la même couleur à octets inversés. C'est ici qu'est né
    // l'accent fantôme de l'application.
    private static uint CLR_BG => Theme.Current.Paper;
    private static uint CLR_TITLE => Theme.Current.Ink;
    private static uint CLR_TEXT => Theme.Current.Ink;
    private static uint CLR_VERSION => Theme.Current.TextSecondary;
    private static uint CLR_LINK => Theme.Current.Action;
    private static uint CLR_SEPARATOR => Theme.Current.Border;

    private IntPtr _hWnd;
    private IntPtr _hWndLinkSite;
    private IntPtr _hWndLinkGithub;
    private IntPtr _hWndLinkLicense;
    private IntPtr _hWndLinkAmcf;
    private IntPtr _hWndBtnClose;

    private readonly Win32.WNDPROC _wndProcDelegate;
    private readonly Win32.SUBCLASSPROC _linkSubclassProc;
    private readonly Win32.SUBCLASSPROC _buttonSubclassProc;
    private readonly Action _themeChanged;
    private IntPtr _hoveredLink;
    private bool _buttonHovered;

    /// <summary>Fond de fenêtre. La brosse vient du cache de Theme : elle est partagée, elle
    /// suit la bascule de thème, et ⛔ elle ne se détruit pas ici.</summary>
    private static IntPtr BgBrush => Theme.Brush(CLR_BG);

    private IntPtr _gdipToken;
    private IntPtr _gdipLogo;

    private bool _visible;

    private int _dpi = 96;
    private int S(int val) => ThemeControls.Scale(val, _dpi);

    // Aucune police n'appartient plus à cette fenêtre : Theme les partage, indexées par rôle
    // et par échelle d'écran. La version passe en Consolas, la charte rangeant les numéros de
    // version avec le technique. Le gras qui n'était employé nulle part disparaît.
    private IntPtr _hFontTitle => Theme.Font(FontRole.WindowTitle, _dpi);
    private IntPtr _hFontVersion => Theme.Font(FontRole.Mono, _dpi);
    private IntPtr _hFontText => Theme.Font(FontRole.Body, _dpi);
    private IntPtr _hFontLink => Theme.Font(FontRole.Body, _dpi);
    private IntPtr _hFontLinkHovered => Theme.Font(FontRole.Body, _dpi, underlined: true);
    private IntPtr _hFontButton => Theme.Font(FontRole.Body, _dpi);

    public bool IsVisible => _visible;

    /// <summary>Handle de la fenêtre. Interne, et pour le seul banc de captures : Smart App
    /// Control refusant de lancer l'exécutable, le contrôle visuel des chantiers passe par le
    /// processus de test, qui a besoin de savoir quoi rendre.</summary>
    internal IntPtr Handle => _hWnd;

    /// <summary>Langue de l'UI à la création : titre, liens et bouton sont figés au
    /// constructeur. Permet à TrayApplication de recréer la fenêtre si la langue a changé.</summary>
    public string UiLanguage { get; } = L.Language;

    public AboutWindow()
    {
        _wndProcDelegate = WndProc;
        _linkSubclassProc = LinkSubclassProc;
        _buttonSubclassProc = ButtonSubclassProc;
        _themeChanged = OnThemeChanged;

        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        _dpi = Win32.GetDeviceCaps(hdcScreen, 88);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        if (_dpi <= 0)
            _dpi = 96;

        var gdipInput = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        Win32.GdiplusStartup(out _gdipToken, ref gdipInput, IntPtr.Zero);
        _gdipLogo = GdiImageLoader.LoadFromEmbeddedResource(typeof(AboutWindow), ProductIdentity.LogoResourceName);

        CreateMainWindow();
        CreateControls();
        ApplyFontsToControls();
        ThemeWindow.ApplyProductIcon(_hWnd);

        // L'échelle du bureau et celle de l'écran où la fenêtre est née diffèrent dès qu'un
        // poste a deux écrans d'échelles distinctes. Rien à recréer désormais : les polices se
        // redemandent au nouveau DPI.
        int realDpi = ThemeWindow.DpiOf(_hWnd);
        if (realDpi != _dpi)
        {
            _dpi = realDpi;
            ApplyFontsToControls();
            ResizeWindow();
            RepositionControls();
        }

        Theme.Changed += _themeChanged;
    }

    /// <summary>
    /// Repose les polices sur les contrôles système. Rien n'est créé ni détruit : après un
    /// changement d'échelle, il suffit de redemander les mêmes rôles au nouveau DPI.
    /// </summary>
    private void ApplyFontsToControls()
    {
        RefreshLinkFont(_hWndLinkSite);
        RefreshLinkFont(_hWndLinkGithub);
        RefreshLinkFont(_hWndLinkLicense);
        RefreshLinkFont(_hWndLinkAmcf);
        Win32.SendMessageW(_hWndBtnClose, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
    }

    /// <summary>
    /// Un lien est souligné quand il est survolé ou focalisé, et pas autrement. Sa couleur, elle,
    /// ne bouge dans aucun état : la charte réserve l'accent au lien, et c'est précisément une
    /// couleur de survol de lien qui a fait entrer l'orange fantôme dans l'application.
    ///
    /// Le soulignement passe par la police parce qu'un STATIC est peint par le système : la
    /// fenêtre ne peut pas lui dessiner de filet comme le fait ThemeControls.DrawLink.
    /// </summary>
    private IntPtr LinkFont(IntPtr hLink) =>
        (_hoveredLink == hLink || Win32.GetFocus() == hLink) ? _hFontLinkHovered : _hFontLink;

    private void RefreshLinkFont(IntPtr hLink)
    {
        if (hLink == IntPtr.Zero)
            return;

        Win32.SendMessageW(hLink, Win32.WM_SETFONT, LinkFont(hLink), (IntPtr)1);
        Win32.InvalidateRect(hLink, IntPtr.Zero, true);
    }

    /// <summary>Bascule de thème de Windows : la brosse de classe doit être reposée, sans quoi
    /// le fond serait effacé à l'ancienne couleur avant chaque peinture.</summary>
    private void OnThemeChanged()
    {
        if (_hWnd == IntPtr.Zero)
            return;

        ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);
        ThemeWindow.ApplyChrome(_hWnd);
    }

    private void CreateMainWindow()
    {
        var hInstance = Win32.GetModuleHandleW(null);
        string className = ProductIdentity.WindowClass("About");

        var wc = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
            lpfnWndProc = _wndProcDelegate,
            hInstance = hInstance,
            hCursor = Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32512),
            hbrBackground = BgBrush,
            lpszClassName = className
        };
        Win32.RegisterClassExW(ref wc);

        int winW = S(BASE_WIN_W);
        int winH = S(BASE_WIN_H);
        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
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

        _hWnd = Win32.CreateWindowExW(0, className, L.About_WindowTitle,
            dwStyle, screenX + (screenW - windowW) / 2, screenY + (screenH - windowH) / 2, windowW, windowH,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        ThemeWindow.ApplyChrome(_hWnd);
    }

    private void CreateControls()
    {
        var hInstance = Win32.GetModuleHandleW(null);

        _hWndLinkSite = Win32.CreateWindowExW(0, "STATIC", L.About_LinkSite,
            Win32.WS_CHILD | Win32.WS_VISIBLE | SS_NOTIFY | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LINK_SITE, hInstance, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndLinkSite, _linkSubclassProc, (UIntPtr)1, IntPtr.Zero);

        _hWndLinkGithub = Win32.CreateWindowExW(0, "STATIC", L.About_LinkGithub,
            Win32.WS_CHILD | Win32.WS_VISIBLE | SS_NOTIFY | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LINK_GITHUB, hInstance, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndLinkGithub, _linkSubclassProc, (UIntPtr)2, IntPtr.Zero);

        _hWndLinkLicense = Win32.CreateWindowExW(0, "STATIC", L.About_LinkLicense,
            Win32.WS_CHILD | Win32.WS_VISIBLE | SS_NOTIFY | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LINK_LICENSE, hInstance, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndLinkLicense, _linkSubclassProc, (UIntPtr)3, IntPtr.Zero);

        // Lien inline dans la ligne AMCF (positionné dans WM_PAINT après mesure)
        _hWndLinkAmcf = Win32.CreateWindowExW(0, "STATIC", L.About_LinkAmcf,
            Win32.WS_CHILD | Win32.WS_VISIBLE | SS_NOTIFY | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LINK_AMCF, hInstance, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndLinkAmcf, _linkSubclassProc, (UIntPtr)4, IntPtr.Zero);

        // BS_OWNERDRAW remplace BS_DEFPUSHBUTTON : le bouton à relief du système est ce que
        // l'audit désigne comme le premier marqueur « années 2000 ». Le statut de bouton par
        // défaut se perd au passage — la fenêtre répond de toute façon à Échap, et le bouton
        // reste actionnable au clavier une fois focalisé.
        _hWndBtnClose = Win32.CreateWindowExW(0, "BUTTON", L.About_Close,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_OWNERDRAW,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_CLOSE, hInstance, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndBtnClose, _buttonSubclassProc, (UIntPtr)5, IntPtr.Zero);

        RepositionControls();
    }

    private void ResizeWindow()
    {
        int winW = S(BASE_WIN_W);
        int winH = S(BASE_WIN_H);
        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
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
        int winW = S(BASE_WIN_W);
        int winH = S(BASE_WIN_H);
        int margin = S(24);

        // Liens : ligne horizontale en bas du panel info. Les largeurs se mesurent au lieu
        // d'être posées en dur — l'interface est bilingue, et « Code source » n'a pas la
        // largeur de « Source code ». La police soulignée sert de mètre : c'est la plus large
        // des deux, donc celle qui déborderait.
        int linksY = S(BASE_WIN_H - 90);
        int linkH = S(26);
        int linkGap = S(16);
        var hdcMeasure = Win32.GetDC(IntPtr.Zero);
        int wSite = GdiHelpers.MeasureSingleLineWidth(hdcMeasure, _hFontLinkHovered, L.About_LinkSite) + S(4);
        int wGithub = GdiHelpers.MeasureSingleLineWidth(hdcMeasure, _hFontLinkHovered, L.About_LinkGithub) + S(4);
        int wLicense = GdiHelpers.MeasureSingleLineWidth(hdcMeasure, _hFontLinkHovered, L.About_LinkLicense) + S(4);
        Win32.ReleaseDC(IntPtr.Zero, hdcMeasure);
        int totalW = wSite + wGithub + wLicense + linkGap * 2;
        int linksX = (winW - totalW) / 2;

        Win32.MoveWindow(_hWndLinkSite, linksX, linksY, wSite, linkH, true);
        Win32.MoveWindow(_hWndLinkGithub, linksX + wSite + linkGap, linksY, wGithub, linkH, true);
        Win32.MoveWindow(_hWndLinkLicense, linksX + wSite + linkGap + wGithub + linkGap, linksY, wLicense, linkH, true);

        // Bouton Fermer en bas à droite. Le contrôle est agrandi de la marge de focus de
        // chaque côté : l'anneau se dessine dans le DC du contrôle, donc tout ce qui déborde
        // de son rectangle client serait écrêté. Le bouton visible garde sa taille et reste
        // aligné sur la marge de la fenêtre.
        int focus = ThemeControls.FocusMargin(_dpi);
        int btnW = S(110) + 2 * focus;
        int btnH = S(32) + 2 * focus;
        int btnX = winW - margin + focus - btnW;
        int btnY = winH - margin + focus - btnH;
        Win32.MoveWindow(_hWndBtnClose, btnX, btnY, btnW, btnH, true);
    }

    public void Show()
    {
        Win32.ShowWindow(_hWnd, 1);
        Win32.SetForegroundWindow(_hWnd);
        _visible = true;
    }

    public void Close()
    {
        Win32.ShowWindow(_hWnd, 0);
        _visible = false;
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (msg)
            {
                case Win32.WM_PAINT:
                    OnPaint(hWnd);
                    return IntPtr.Zero;

                case Win32.WM_ERASEBKGND:
                    return (IntPtr)1;

                case Win32.WM_DPICHANGED:
                    _dpi = ThemeWindow.ApplyDpiChange(_hWnd, wParam, lParam);
                    ApplyFontsToControls();
                    RepositionControls();
                    Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;

                case Win32.WM_DRAWITEM:
                {
                    var dis = Marshal.PtrToStructure<Win32.DRAWITEMSTRUCT>(lParam);
                    if (dis.hwndItem == _hWndBtnClose)
                    {
                        DrawCloseButton(dis);
                        return (IntPtr)1;
                    }
                    break;
                }

                case Win32.WM_COMMAND:
                {
                    int id = wParam.ToInt32() & 0xFFFF;
                    int code = (wParam.ToInt32() >> 16) & 0xFFFF;
                    switch (id)
                    {
                        case IDC_LINK_SITE:
                            if (code == 0) OpenLink(ProductIdentity.SiteBaseUrl);
                            break;
                        case IDC_LINK_GITHUB:
                            if (code == 0) OpenLink(ProductIdentity.RepositoryUrl);
                            break;
                        case IDC_LINK_LICENSE:
                            if (code == 0) OpenLink("https://eupl.eu/1.2/fr/");
                            break;
                        case IDC_LINK_AMCF:
                            if (code == 0) OpenLink("https://www.helloasso.com/associations/association-pour-la-modernisation-du-clavier-francais");
                            break;
                        case IDC_BTN_CLOSE:
                            Close();
                            break;
                    }
                    return IntPtr.Zero;
                }

                case Win32.WM_CTLCOLORSTATIC:
                {
                    IntPtr hdcStatic = wParam;
                    IntPtr hCtrl = lParam;
                    if (hCtrl == _hWndLinkSite || hCtrl == _hWndLinkGithub || hCtrl == _hWndLinkLicense || hCtrl == _hWndLinkAmcf)
                    {
                        Win32.SetBkMode(hdcStatic, 1);
                        // La même couleur dans tous les états : c'est la police qui souligne.
                        Win32.SetTextColor(hdcStatic, CLR_LINK);
                        return BgBrush;
                    }
                    break;
                }

                case Win32.WM_CTLCOLORBTN:
                    // Fond du bouton owner-draw, effacé par Windows avant WM_DRAWITEM.
                    Win32.SetBkMode(wParam, Win32.TRANSPARENT);
                    Win32.SetTextColor(wParam, Theme.Current.Ink);
                    return BgBrush;

                case Win32.WM_SETCURSOR:
                    if (wParam == _hWndLinkSite || wParam == _hWndLinkGithub || wParam == _hWndLinkLicense || wParam == _hWndLinkAmcf)
                    {
                        Win32.SetCursor(Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32649));
                        return (IntPtr)1;
                    }
                    break;

                case Win32.WM_KEYDOWN:
                    if (wParam == (IntPtr)0x1B) // VK_ESCAPE
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
            ConfigManager.Log("AboutWindow WndProc", ex);
        }

        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void OpenLink(string url)
    {
        Win32.ShellExecuteW(IntPtr.Zero, "open", url, null, null, 1);
    }

    private IntPtr LinkSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        switch (msg)
        {
            case Win32.WM_MOUSEMOVE:
                if (_hoveredLink != hWnd)
                {
                    _hoveredLink = hWnd;
                    RefreshLinkFont(hWnd);
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
                    RefreshLinkFont(hWnd);
                }
                break;
            case Win32.WM_SETFOCUS:
            case Win32.WM_KILLFOCUS:
                // Le focus clavier souligne comme le survol. Sans cela, un lien atteint au Tab
                // n'aurait aucun signal : sa couleur, elle, ne bouge dans aucun état. Pendant
                // WM_KILLFOCUS, GetFocus rend encore ce contrôle, d'où le choix explicite
                // plutôt qu'un appel à LinkFont.
                Win32.SendMessageW(hWnd, Win32.WM_SETFONT,
                    msg == Win32.WM_SETFOCUS || _hoveredLink == hWnd ? _hFontLinkHovered : _hFontLink,
                    (IntPtr)1);
                Win32.InvalidateRect(hWnd, IntPtr.Zero, true);
                break;
            case 0x0087: // WM_GETDLGCODE
                return (IntPtr)0x0004; // DLGC_WANTALLKEYS
            case Win32.WM_KEYDOWN:
                if (wParam == (IntPtr)0x0D) // VK_RETURN
                {
                    int ctrlId = Win32.GetDlgCtrlID(hWnd);
                    Win32.SendMessageW(_hWnd, Win32.WM_COMMAND, (IntPtr)ctrlId, hWnd);
                    return IntPtr.Zero;
                }
                break;
        }
        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private IntPtr ButtonSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        switch (msg)
        {
            case Win32.WM_MOUSEMOVE:
                if (!_buttonHovered)
                {
                    _buttonHovered = true;
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
                if (_buttonHovered)
                {
                    _buttonHovered = false;
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);
                }
                break;
        }
        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Bouton Fermer, redessiné au système. Windows ne suit pas le survol d'un bouton : ce
    /// signal-là vient du sous-classement ci-dessus, les trois autres de itemState.
    ///
    /// Secondaire et non primaire [candidat — arrêt visuel d'Antoine] : la charte n'accorde
    /// qu'un bouton primaire par écran, et fermer une boîte d'information est un congé, pas
    /// l'action que l'écran propose.
    /// </summary>
    private void DrawCloseButton(in Win32.DRAWITEMSTRUCT dis)
    {
        var state = ControlState.None;
        if ((dis.itemState & Win32.ODS_DISABLED) != 0) state |= ControlState.Disabled;
        if ((dis.itemState & Win32.ODS_SELECTED) != 0) state |= ControlState.Pressed;
        if ((dis.itemState & Win32.ODS_FOCUS) != 0) state |= ControlState.Focused;
        if (_buttonHovered) state |= ControlState.Hovered;

        // Le contrôle déborde du bouton visible de la marge de focus : cette marge appartient
        // au fond de la fenêtre, et c'est là que l'anneau se dessine.
        var full = dis.rcItem;
        Win32.FillRect(dis.hDC, ref full, BgBrush);

        int focus = ThemeControls.FocusMargin(_dpi);
        var rect = new Win32.RECT
        {
            left = full.left + focus,
            top = full.top + focus,
            right = full.right - focus,
            bottom = full.bottom - focus,
        };
        ThemeControls.DrawButton(dis.hDC, rect, L.About_Close, _hFontButton,
            ButtonKind.Secondary, state, Theme.Current, _dpi);
    }

    private void OnPaint(IntPtr hWnd)
    {
        var hdcPaint = Win32.BeginPaint(hWnd, out var ps);
        Win32.GetClientRect(hWnd, out var clientRect);
        int cw = clientRect.right;
        int ch = clientRect.bottom;

        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        var hdc = Win32.CreateCompatibleDC(hdcScreen);
        var hBmp = Win32.CreateCompatibleBitmap(hdcScreen, cw, ch);
        var hBmpOld = Win32.SelectObject(hdc, hBmp);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);

        Win32.FillRect(hdc, ref clientRect, BgBrush);
        Win32.SetBkMode(hdc, 1);

        Win32.GdipCreateFromHDC(hdc, out IntPtr gfx);
        if (gfx != IntPtr.Zero)
        {
            Win32.GdipSetSmoothingMode(gfx, 4);
            Win32.GdipSetInterpolationMode(gfx, 7);
            Win32.GdipSetTextRenderingHint(gfx, 4);
        }

        int margin = S(24);

        // Logo + titre + version sur la meme ligne en haut. Les écarts suivent l'échelle
        // 4/8/12/16/24/32 de la charte, et non plus des valeurs au cas par cas.
        int logoSize = S(48);
        int logoX = margin;
        int logoY = S(20);
        if (gfx != IntPtr.Zero && _gdipLogo != IntPtr.Zero)
            Win32.GdipDrawImageRectI(gfx, _gdipLogo, logoX, logoY, logoSize, logoSize);

        int textX = logoX + logoSize + S(16);
        int titleY = logoY;
        Win32.SelectObject(hdc, _hFontTitle);
        Win32.SetTextColor(hdc, CLR_TITLE);
        var titleRect = new Win32.RECT { left = textX, top = titleY, right = cw - margin, bottom = titleY + S(32) };
        Win32.DrawTextW(hdc, ProductIdentity.DisplayName, -1, ref titleRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        int versionY = titleY + S(32);
        Win32.SelectObject(hdc, _hFontVersion);
        Win32.SetTextColor(hdc, CLR_VERSION);
        var versionRect = new Win32.RECT { left = textX, top = versionY, right = cw - margin, bottom = versionY + S(20) };
        Win32.DrawTextW(hdc, "v" + Program.Version, -1, ref versionRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        // Description
        int descY = logoY + logoSize + S(24);
        Win32.SelectObject(hdc, _hFontText);
        Win32.SetTextColor(hdc, CLR_TEXT);
        var descRect = new Win32.RECT { left = margin, top = descY, right = cw - margin, bottom = descY + S(24) };
        Win32.DrawTextW(hdc, L.About_Description, -1, ref descRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        // Ligne AMCF : « Édité par l' » (GDI) + lien STATIC + « (AMCF) » (GDI)
        int amcfY = descY + S(32);
        int amcfH = S(24);
        Win32.SelectObject(hdc, _hFontText);
        Win32.SetTextColor(hdc, CLR_TEXT);
        string amcfPrefix = L.About_AmcfPrefix;
        string amcfLinkText = L.About_LinkAmcf;
        string amcfSuffix = L.About_AmcfSuffix;
        // Mesurer chaque portion via DT_CALCRECT
        var measurePrefix = new Win32.RECT { left = 0, top = 0, right = 9999, bottom = 9999 };
        Win32.DrawTextW(hdc, amcfPrefix, -1, ref measurePrefix, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX | Win32.DT_CALCRECT);
        int prefixW = measurePrefix.right;
        Win32.SelectObject(hdc, _hFontLink);
        var measureLink = new Win32.RECT { left = 0, top = 0, right = 9999, bottom = 9999 };
        Win32.DrawTextW(hdc, amcfLinkText, -1, ref measureLink, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX | Win32.DT_CALCRECT);
        int linkW = measureLink.right;
        Win32.SelectObject(hdc, _hFontText);
        var measureSuffix = new Win32.RECT { left = 0, top = 0, right = 9999, bottom = 9999 };
        Win32.DrawTextW(hdc, amcfSuffix, -1, ref measureSuffix, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX | Win32.DT_CALCRECT);
        int suffixW = measureSuffix.right;
        // Dessiner préfixe à margin
        var prefixRect = new Win32.RECT { left = margin, top = amcfY, right = margin + prefixW, bottom = amcfY + amcfH };
        Win32.DrawTextW(hdc, amcfPrefix, -1, ref prefixRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        // Repositionner le STATIC du lien
        Win32.SetWindowPos(_hWndLinkAmcf, IntPtr.Zero,
            margin + prefixW, amcfY, linkW, amcfH,
            Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        // Dessiner suffixe après le lien
        int suffixX = margin + prefixW + linkW;
        var suffixRect = new Win32.RECT { left = suffixX, top = amcfY, right = suffixX + suffixW, bottom = amcfY + amcfH };
        Win32.DrawTextW(hdc, amcfSuffix, -1, ref suffixRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        // Separateur juste au-dessus des liens
        int sepY = S(BASE_WIN_H - 100);
        var sepRect = new Win32.RECT { left = margin, top = sepY, right = cw - margin, bottom = sepY + 1 };
        Win32.FillRect(hdc, ref sepRect, Theme.Brush(CLR_SEPARATOR));

        if (gfx != IntPtr.Zero)
            Win32.GdipDeleteGraphics(gfx);

        Win32.BitBlt(hdcPaint, 0, 0, cw, ch, hdc, 0, 0, Win32.SRCCOPY);
        Win32.SelectObject(hdc, hBmpOld);
        Win32.DeleteObject(hBmp);
        Win32.DeleteDC(hdc);
        Win32.EndPaint(hWnd, ref ps);
    }

    public void Dispose()
    {
        if (_hWndLinkSite != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndLinkSite, _linkSubclassProc, (UIntPtr)1);
        if (_hWndLinkGithub != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndLinkGithub, _linkSubclassProc, (UIntPtr)2);
        if (_hWndLinkLicense != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndLinkLicense, _linkSubclassProc, (UIntPtr)3);
        if (_hWndLinkAmcf != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndLinkAmcf, _linkSubclassProc, (UIntPtr)4);
        if (_hWndBtnClose != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndBtnClose, _buttonSubclassProc, (UIntPtr)5);

        // Un abonnement laissé derrière garde en vie une fenêtre détruite et lui envoie des
        // repeints sur un handle mort.
        Theme.Changed -= _themeChanged;
        if (_hWnd != IntPtr.Zero)
        {
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }

        // Ni polices, ni brosse, ni icône à libérer : tout cela appartient aux caches de Theme
        // et de ThemeWindow, qui les partagent avec les autres fenêtres.

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
        Win32.UnregisterClassW(ProductIdentity.WindowClass("About"), Win32.GetModuleHandleW(null));
    }
}
