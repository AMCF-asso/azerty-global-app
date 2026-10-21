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

    // ── Control IDs ─────────────────────────────────────────────────
    private const int IDC_LINK_SITE = 4101;
    private const int IDC_LINK_GITHUB = 4102;
    private const int IDC_LINK_LICENSE = 4103;
    private const int IDC_BTN_CLOSE = 4104;
    private const int IDC_LINK_AMCF = 4105;

    private const int BASE_WIN_W = 550;
    private const int BASE_WIN_H = 230;

    // ── Couleurs ────────────────────────────────────────────────────
    // A9 : palette papier des Leçons, ou celle du système en contraste élevé (lue à la
    // création, comme l'accueil).
    private readonly PaperPalette _pal = PaperPalette.Now();
    private uint LinkHover => _pal.HighContrast ? _pal.Action : PaperDraw.Blend(_pal.Ink, _pal.Action, 0.25f);

    private IntPtr _hWnd;
    private IntPtr _hWndLinkSite;
    private IntPtr _hWndLinkGithub;
    private IntPtr _hWndLinkLicense;
    private IntPtr _hWndLinkAmcf;
    private IntPtr _hWndBtnClose;

    private readonly Win32.WNDPROC _wndProcDelegate;
    // F-10 : survol, Entrée, Échap et Tab des quatre liens.
    private readonly LinkBehavior _links;

    // Fond de la classe : Windows le détruit au désenregistrement (NativeWindow).
    private IntPtr _hBgBrush;

    private IntPtr _gdipToken;
    private IntPtr _gdipLogo;
    private IntPtr _hIcon;

    private bool _visible;

    private float _dpiScale;
    private int S(int val) => (int)(val * _dpiScale);

    // A9 : polices du site (AG Titre, AG Texte), partagées par AppFonts : jamais détruites.
    // Seul le soulignement des liens STATIC demande une police à soi.
    private IntPtr FontTitle => FontSpec.Of(AppFonts.Title, TypeRamp.Title, TypeRamp.Semibold, _dpiScale).Handle;
    private IntPtr FontVersion => FontSpec.Of(AppFonts.Text, TypeRamp.Caption, TypeRamp.Regular, _dpiScale).Handle;
    private IntPtr FontText => FontSpec.Of(AppFonts.Text, TypeRamp.Body, TypeRamp.Regular, _dpiScale).Handle;
    private IntPtr FontButton => FontText;
    private IntPtr _hFontLink;

    public bool IsVisible => _visible;

    /// <summary>Langue de l'UI à la création : titre, liens et bouton sont figés au
    /// constructeur. Permet à TrayApplication de recréer la fenêtre si la langue a changé.</summary>
    public string UiLanguage { get; } = L.Language;

    public AboutWindow()
    {
        _wndProcDelegate = WndProc;
        _links = new LinkBehavior(() => _hWnd, Close, focusFrame: true);

        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        int dpi = Win32.GetDeviceCaps(hdcScreen, 88);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        _dpiScale = dpi / 96f;

        var gdipInput = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        Win32.GdiplusStartup(out _gdipToken, ref gdipInput, IntPtr.Zero);
        _gdipLogo = GdiImageLoader.LoadFromEmbeddedResource(typeof(AboutWindow), ProductIdentity.LogoResourceName);

        CreateFonts();
        CreateMainWindow();
        CreateControls();
        ApplyFontsToControls();

        if (NativeWindow.CorrectedScale(Win32.GetDpiForWindow(_hWnd), _dpiScale) is float windowScale)
        {
            _dpiScale = windowScale;
            // Mise en page seule : GetDpiForWindow ne lève pas (audit du 25/09, X-04).
            try
            {
                RecreateFonts();
                ResizeWindow();
                RepositionControls();
            }
            catch { }
        }
    }

    private void CreateFonts()
    {
        _hFontLink = TypeRamp.CreateIn(AppFonts.Text, TypeRamp.Body, TypeRamp.Regular, _dpiScale, underline: true);
    }

    private void DestroyFonts()
    {
        Win32.DeleteObject(_hFontLink);
    }

    private void RecreateFonts()
    {
        DestroyFonts();
        CreateFonts();
        ApplyFontsToControls();
    }

    private void ApplyFontsToControls()
    {
        Win32.SendMessageW(_hWndLinkSite, Win32.WM_SETFONT, _hFontLink, (IntPtr)1);
        Win32.SendMessageW(_hWndLinkGithub, Win32.WM_SETFONT, _hFontLink, (IntPtr)1);
        Win32.SendMessageW(_hWndLinkLicense, Win32.WM_SETFONT, _hFontLink, (IntPtr)1);
        Win32.SendMessageW(_hWndLinkAmcf, Win32.WM_SETFONT, _hFontLink, (IntPtr)1);
        Win32.SendMessageW(_hWndBtnClose, Win32.WM_SETFONT, FontButton, (IntPtr)1);
    }

    private void CreateMainWindow()
    {
        var hInstance = Win32.GetModuleHandleW(null);
        string className = ProductIdentity.WindowClass("About");
        // Audit du 25/09, X-01 : une classe refusée ne crée pas de fenêtre.
        if (!NativeWindow.RegisterClass(className, _wndProcDelegate))
            return;

        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var (windowW, windowH) = NativeWindow.OuterSize(S(BASE_WIN_W), S(BASE_WIN_H), dwStyle);
        var (x, y) = NativeWindow.CenterIn(NativeWindow.WorkArea(), windowW, windowH);

        _hWnd = Win32.CreateWindowExW(0, className, L.About_WindowTitle,
            dwStyle, x, y, windowW, windowH,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        _hBgBrush = NativeWindow.ApplyClassBackground(_hWnd, _pal.Paper);

        // AG130-40 : cette fenetre veut Tab, Maj+Tab et Entree entre ses controles.
        DialogNavigation.Register(_hWnd);
        NativeWindow.ApplyFrame(_hWnd, dark: false);
    }

    private void CreateControls()
    {
        var hInstance = Win32.GetModuleHandleW(null);

        _hWndLinkSite = Win32.CreateWindowExW(0, "STATIC", L.About_LinkSite,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.SS_NOTIFY | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LINK_SITE, hInstance, IntPtr.Zero);
        _links.Attach(_hWndLinkSite, 1);

        _hWndLinkGithub = Win32.CreateWindowExW(0, "STATIC", L.About_LinkGithub,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.SS_NOTIFY | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LINK_GITHUB, hInstance, IntPtr.Zero);
        _links.Attach(_hWndLinkGithub, 2);

        _hWndLinkLicense = Win32.CreateWindowExW(0, "STATIC", L.About_LinkLicense,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.SS_NOTIFY | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LINK_LICENSE, hInstance, IntPtr.Zero);
        _links.Attach(_hWndLinkLicense, 3);

        // Lien inline dans la ligne AMCF (positionné dans WM_PAINT après mesure)
        _hWndLinkAmcf = Win32.CreateWindowExW(0, "STATIC", L.About_LinkAmcf,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.SS_NOTIFY | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LINK_AMCF, hInstance, IntPtr.Zero);
        _links.Attach(_hWndLinkAmcf, 4);

        _hWndBtnClose = Win32.CreateWindowExW(0, "BUTTON", L.About_Close,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | 0x0001 /* BS_DEFPUSHBUTTON */,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_CLOSE, hInstance, IntPtr.Zero);
        // Audit UX 1.3.0, #3 ; A9 : bouton papier. En contraste élevé, le dessin du système.
        if (!_pal.HighContrast)
            LightButton.Attach(_hWndBtnClose, paper: true);

        RepositionControls();
    }

    private void ResizeWindow() =>
        NativeWindow.ResizeAroundCenter(_hWnd, S(BASE_WIN_W), S(BASE_WIN_H),
            Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU);

    private void RepositionControls()
    {
        int winW = S(BASE_WIN_W);
        int winH = S(BASE_WIN_H);
        int margin = S(20);

        // Liens : ligne horizontale en bas du panel info — largeurs adaptées au texte
        int linksY = S(BASE_WIN_H - 90);
        int linkH = S(22);
        int linkGap = S(12);
        int wSite = S(70);
        int wGithub = S(140);
        int wLicense = S(220);
        int totalW = wSite + wGithub + wLicense + linkGap * 2;
        int linksX = (winW - totalW) / 2;

        Win32.MoveWindow(_hWndLinkSite, linksX, linksY, wSite, linkH, true);
        Win32.MoveWindow(_hWndLinkGithub, linksX + wSite + linkGap, linksY, wGithub, linkH, true);
        Win32.MoveWindow(_hWndLinkLicense, linksX + wSite + linkGap + wGithub + linkGap, linksY, wLicense, linkH, true);

        // Bouton Fermer en bas a droite
        int btnW = S(110);
        int btnH = S(32);
        int btnX = winW - margin - btnW;
        int btnY = winH - margin - btnH;
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
                {
                    _dpiScale = NativeWindow.DpiFromChange(_hWnd, wParam) / 96f;
                    RecreateFonts();
                    NativeWindow.MoveToSuggestedRect(_hWnd, lParam);
                    RepositionControls();
                    Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
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
                        // Revue du 2026-09-21, R5 : IsDialogMessageW traduit Entrée en IDOK et
                        // Échap en IDCANCEL ; le seul bouton étant « Fermer », les deux ferment.
                        case DialogNavigation.IDOK:
                        case DialogNavigation.IDCANCEL:
                            Close();
                            break;
                    }
                    return IntPtr.Zero;
                }

                case Win32.WM_CTLCOLORSTATIC:
                {
                    IntPtr hdcStatic = wParam;
                    IntPtr hCtrl = lParam;
                    Win32.SetBkMode(hdcStatic, 1);
                    Win32.SetBkColor(hdcStatic, _pal.Paper);
                    if (_links.Contains(hCtrl))
                        Win32.SetTextColor(hdcStatic, _links.IsActive(hCtrl) ? LinkHover : _pal.Action);
                    // A9 : le bouton papier lit ici la couleur de ses coins (LightButton.Backdrop) ;
                    // sans réponse, Windows rendait son gris (#F0F0F0).
                    return _hBgBrush;
                }

                case Win32.WM_SETCURSOR:
                    if (_links.TrySetHandCursor(wParam))
                        return (IntPtr)1;
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
        if (gfx != IntPtr.Zero)
        {
            Win32.GdipSetSmoothingMode(gfx, 4);
            Win32.GdipSetInterpolationMode(gfx, 7);
            Win32.GdipSetTextRenderingHint(gfx, 4);
        }

        int margin = S(20);

        // Logo + titre + version sur la meme ligne en haut
        int logoSize = S(48);
        int logoX = margin;
        int logoY = S(14);
        if (gfx != IntPtr.Zero && _gdipLogo != IntPtr.Zero)
            Win32.GdipDrawImageRectI(gfx, _gdipLogo, logoX, logoY, logoSize, logoSize);

        int textX = logoX + logoSize + S(14);
        int titleY = logoY + S(2);
        Win32.SelectObject(hdc, FontTitle);
        Win32.SetTextColor(hdc, _pal.Ink);
        var titleRect = new Win32.RECT { left = textX, top = titleY, right = cw - margin, bottom = titleY + S(28) };
        Win32.DrawTextW(hdc, ProductIdentity.DisplayName, -1, ref titleRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        int versionY = titleY + S(28);
        Win32.SelectObject(hdc, FontVersion);
        Win32.SetTextColor(hdc, _pal.Muted);
        var versionRect = new Win32.RECT { left = textX, top = versionY, right = cw - margin, bottom = versionY + S(20) };
        Win32.DrawTextW(hdc, "v" + Program.Version, -1, ref versionRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        // Description
        int descY = logoY + logoSize + S(14);
        Win32.SelectObject(hdc, FontText);
        Win32.SetTextColor(hdc, _pal.Ink);
        var descRect = new Win32.RECT { left = margin, top = descY, right = cw - margin, bottom = descY + S(22) };
        Win32.DrawTextW(hdc, L.About_Description, -1, ref descRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        // Ligne AMCF : « Édité par l' » (GDI) + lien STATIC + « (AMCF) » (GDI)
        int amcfY = descY + S(26);
        int amcfH = S(20);
        Win32.SelectObject(hdc, FontText);
        Win32.SetTextColor(hdc, _pal.Ink);
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
        Win32.SelectObject(hdc, FontText);
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
        PaperDraw.Fill(hdc, margin, sepY, cw - margin, sepY + 1, _pal.Border);

        if (gfx != IntPtr.Zero)
            Win32.GdipDeleteGraphics(gfx);
    }

    public void Dispose()
    {
        _links.Detach();
        if (_hWnd != IntPtr.Zero)
        {
            // AG130-40 : desinscrire AVANT de detruire — Windows recycle les HWND.
            DialogNavigation.Unregister(_hWnd);
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
        if (_hIcon != IntPtr.Zero)
        {
            Win32.DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }

        DestroyFonts();

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

        NativeWindow.UnregisterClass(ProductIdentity.WindowClass("About"));
    }
}
