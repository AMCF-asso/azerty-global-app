// Fenetre custom affichee quand l'app detecte qu'une disposition systeme AZERTY Global
// est deja active. Propose un choix eclaire entre garder l'app (post-login user-friendly)
// ou garder la disposition systeme (avant login : mot de passe Windows, etc.).
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Mini-fenetre modale topmost qui explique le trade-off app vs disposition systeme,
/// puis demande a l'utilisateur de choisir : Quitter l'application | Garder l'app.
/// Remplace l'ancien MessageBox de TrayApplication.ShowLayoutConflictPopup pour permettre
/// un texte plus dense et un choix plus eclaire.
/// </summary>
sealed class LayoutConflictWindow : IDisposable
{
    private const int IDC_BTN_QUIT = 5101;
    private const int IDC_BTN_KEEP = 5102;

    // Nom de classe Win32. Defini en const pour partage entre CreateMainWindow et Dispose
    // (UnregisterClassW au Dispose pour eviter que la classe survive l'instance et garde
    // un pointeur vers _wndProcDelegate collecte par GC). Sans cela, une 2e instance creee
    // apres dispose de la 1ere (cas : conflit detecte au demarrage puis re-detecte apres
    // Ctrl+Shift) crashe au prochain WM_PAINT/WM_COMMAND. Pattern documente dans
    // LearningModule.cs:740 (bug Reset->Essayer post-1ere completion fixe en v0.9.7).
    private static readonly string WND_CLASS_NAME = ProductIdentity.WindowClass("LayoutConflict");

    private const int BASE_WIN_W = 560;
    private const int BASE_WIN_H = 440;

    // Couleurs alignees sur AboutWindow / SettingsWindow
    // Les jetons de la charte, relus à chaque bascule de thème. CLR_HIGHLIGHT était le bleu
    // Windows 0x000078D4, dont l'orange fantôme du parc est l'exact inverse d'octets.
    private static uint CLR_BG => Theme.Current.Paper;
    private static uint CLR_TITLE => Theme.Current.Ink;
    private static uint CLR_TEXT => Theme.Current.Ink;
    private static uint CLR_HIGHLIGHT => Theme.Current.Action;
    private static uint CLR_SUBTLE => Theme.Current.TextSecondary;

    private IntPtr _hWnd;
    private IntPtr _hWndBtnQuit;
    private IntPtr _hWndBtnKeep;

    private readonly Win32.WNDPROC _wndProcDelegate;
    private Action? _themeChanged;
    private Win32.SUBCLASSPROC? _buttonSubclassProc;
    private IntPtr _hoveredButton;

    private readonly bool _isAtStartup;
    private readonly Action _onQuit;
    private readonly Action _onKeep;

    private float _dpiScale;
    private int S(int val) => (int)(val * _dpiScale);

    /// <summary>L'échelle en points par pouce, dont Theme a besoin pour ses polices.</summary>
    private int _dpi => (int)Math.Round(96 * _dpiScale);

    private IntPtr _hFontTitle => Theme.Font(FontRole.WindowTitle, _dpi);
    private IntPtr _hFontText => Theme.Font(FontRole.Body, _dpi);
    private IntPtr _hFontBold => Theme.Font(FontRole.BodyStrong, _dpi);
    private IntPtr _hFontButton => Theme.Font(FontRole.Body, _dpi);

    /// <param name="isAtStartup">
    /// true : detection au demarrage de l'app (« est deja installee »).
    /// false : detection apres switch Ctrl+Shift (« vient d'etre activee »).
    /// </param>
    /// <param name="onQuit">Callback appele si l'utilisateur choisit « Quitter l'application ».</param>
    /// <param name="onKeep">Callback appele si l'utilisateur choisit « Garder l'app » (ou ferme la fenetre).</param>
    public LayoutConflictWindow(bool isAtStartup, Action onQuit, Action onKeep)
    {
        _wndProcDelegate = WndProc;
        _isAtStartup = isAtStartup;
        _onQuit = onQuit;
        _onKeep = onKeep;

        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        int dpi = Win32.GetDeviceCaps(hdcScreen, 88);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        _dpiScale = dpi / 96f;

        CreateMainWindow();
        CreateControls();
        ApplyFontsToControls();

        try
        {
            // Le DPI passe par ThemeWindow : seul ce point honore l'override du banc
            // de captures. Lu en direct, la fenêtre rendait toujours à l'échelle du
            // poste, et ses six cellules n'étaient qu'un rendu répété trois fois.
            int realDpi = ThemeWindow.DpiOf(_hWnd);
            if (realDpi > 0 && Math.Abs(realDpi / 96f - _dpiScale) > 0.01f)
            {
                _dpiScale = realDpi / 96f;
                ApplyFontsToControls();
                ResizeWindow();
                RepositionControls();
            }
        }
        catch { }
    }

    /// <summary>Windows ne rapporte pas le survol d'un bouton owner-draw : il faut le suivre.</summary>
    private IntPtr ButtonSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        switch (msg)
        {
            case Win32.WM_MOUSEMOVE:
                if (_hoveredButton != hWnd)
                {
                    _hoveredButton = hWnd;
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
                if (_hoveredButton == hWnd)
                {
                    _hoveredButton = IntPtr.Zero;
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);
                }
                break;
        }

        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private bool TryDrawItem(IntPtr lParam)
    {
        var dis = Marshal.PtrToStructure<Win32.DRAWITEMSTRUCT>(lParam);
        if (dis.hwndItem != _hWndBtnQuit && dis.hwndItem != _hWndBtnKeep)
            return false;

        var state = ControlState.None;
        if ((dis.itemState & Win32.ODS_DISABLED) != 0) state |= ControlState.Disabled;
        if ((dis.itemState & Win32.ODS_SELECTED) != 0) state |= ControlState.Pressed;
        if ((dis.itemState & Win32.ODS_FOCUS) != 0) state |= ControlState.Focused;
        if (_hoveredButton == dis.hwndItem) state |= ControlState.Hovered;

        var full = dis.rcItem;
        Win32.FillRect(dis.hDC, ref full, Theme.Brush(CLR_BG));

        bool quit = dis.hwndItem == _hWndBtnQuit;
        ThemeControls.DrawButton(dis.hDC, dis.rcItem,
            quit ? L.LayoutConflict_BtnQuit : L.LayoutConflict_BtnKeep, _hFontButton,
            quit ? ButtonKind.Primary : ButtonKind.Secondary, state, Theme.Current, _dpi);
        return true;
    }

    private void ApplyFontsToControls()
    {
        Win32.SendMessageW(_hWndBtnQuit, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
        Win32.SendMessageW(_hWndBtnKeep, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
    }

    private void CreateMainWindow()
    {
        var hInstance = Win32.GetModuleHandleW(null);

        // hbrBackground = IntPtr.Zero : NE PAS reference _hBgBrush dans la WNDCLASSEXW.
        // La classe Win32 reste enregistree au-dela de la duree de vie de l'instance ;
        // si on libere _hBgBrush au Dispose, la classe garde un pointeur invalide → crash
        // a la 2e instance. L'effacement du fond est gere via WM_ERASEBKGND (return 1) +
        // FillRect dans OnPaint. Couple avec UnregisterClassW au Dispose pour permettre
        // la 2e instance avec un delegate WndProc frais.
        var wc = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
            lpfnWndProc = _wndProcDelegate,
            hInstance = hInstance,
            hCursor = Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32512),
            hbrBackground = IntPtr.Zero,
            lpszClassName = WND_CLASS_NAME
        };
        Win32.RegisterClassExW(ref wc);

        int winW = S(BASE_WIN_W);
        int winH = S(BASE_WIN_H);
        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        uint dwExStyle = Win32.WS_EX_TOPMOST;
        var adjustRect = new Win32.RECT { left = 0, top = 0, right = winW, bottom = winH };
        Win32.AdjustWindowRectEx(ref adjustRect, dwStyle, false, dwExStyle);
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

        _hWnd = Win32.CreateWindowExW(dwExStyle, WND_CLASS_NAME,
            L.LayoutConflict_WindowTitle,
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
        };
        Theme.Changed += _themeChanged;
    }

    private void CreateControls()
    {
        var hInstance = Win32.GetModuleHandleW(null);
        _buttonSubclassProc = ButtonSubclassProc;

        // BS_OWNERDRAW : le relief du système disparaît. « Quitter l'autre disposition » est
        // ce que la fenêtre propose de faire, donc le seul primaire ; « garder » est le refus.
        _hWndBtnQuit = Win32.CreateWindowExW(0, "BUTTON", L.LayoutConflict_BtnQuit,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_OWNERDRAW,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_QUIT, hInstance, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndBtnQuit, _buttonSubclassProc, (UIntPtr)1, IntPtr.Zero);

        _hWndBtnKeep = Win32.CreateWindowExW(0, "BUTTON", L.LayoutConflict_BtnKeep,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_OWNERDRAW,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_KEEP, hInstance, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndBtnKeep, _buttonSubclassProc, (UIntPtr)2, IntPtr.Zero);

        RepositionControls();
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

    private void RepositionControls()
    {
        int winW = S(BASE_WIN_W);
        int winH = S(BASE_WIN_H);
        int margin = S(20);
        int btnW = S(180);
        int btnH = S(34);
        int btnGap = S(12);
        int totalBtnW = btnW * 2 + btnGap;
        int btnX = (winW - totalBtnW) / 2;
        int btnY = winH - margin - btnH;
        Win32.MoveWindow(_hWndBtnQuit, btnX, btnY, btnW, btnH, true);
        Win32.MoveWindow(_hWndBtnKeep, btnX + btnW + btnGap, btnY, btnW, btnH, true);
    }

    /// <summary>Pour le banc de captures : la fenêtre est rendue, elle n'est pas pilotée.</summary>
    internal IntPtr Handle => _hWnd;

    public void Show()
    {
        Win32.ShowWindow(_hWnd, 1);
        Win32.SetForegroundWindow(_hWnd);
    }

    private void Close(bool quit)
    {
        Win32.ShowWindow(_hWnd, 0);
        if (quit) _onQuit(); else _onKeep();
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

                case Win32.WM_CTLCOLORBTN:
                    // Windows efface le fond d'un bouton owner-draw avec la brosse rendue ici,
                    // avant d'envoyer WM_DRAWITEM.
                    Win32.SetBkMode(wParam, Win32.TRANSPARENT);
                    Win32.SetTextColor(wParam, CLR_TEXT);
                    return Theme.Brush(CLR_BG);

                case Win32.WM_DRAWITEM:
                    if (TryDrawItem(lParam))
                        return (IntPtr)1;
                    break;

                case Win32.WM_COMMAND:
                {
                    int id = wParam.ToInt32() & 0xFFFF;
                    switch (id)
                    {
                        case IDC_BTN_QUIT: Close(true); break;
                        case IDC_BTN_KEEP: Close(false); break;
                    }
                    return IntPtr.Zero;
                }

                case Win32.WM_KEYDOWN:
                    if (wParam == (IntPtr)0x1B) // VK_ESCAPE → choix non-destructif (garder)
                    {
                        Close(false);
                        return IntPtr.Zero;
                    }
                    break;

                case Win32.WM_CLOSE:
                    Close(false); // croix X = garder l'app
                    return IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            ConfigManager.Log("LayoutConflictWindow WndProc", ex);
        }

        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
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

        Win32.FillRect(hdc, ref clientRect, Theme.Brush(CLR_BG));
        Win32.SetBkMode(hdc, 1);

        int margin = S(24);
        int x = margin;
        int y = S(20);
        int contentW = cw - margin * 2;

        // Titre
        Win32.SelectObject(hdc, _hFontTitle);
        Win32.SetTextColor(hdc, CLR_TITLE);
        var titleRect = new Win32.RECT { left = x, top = y, right = x + contentW, bottom = y + S(28) };
        Win32.DrawTextW(hdc, L.LayoutConflict_Title, -1, ref titleRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        y += S(36);

        // Intro variable selon origine
        Win32.SelectObject(hdc, _hFontText);
        Win32.SetTextColor(hdc, CLR_TEXT);
        string introText = _isAtStartup
            ? L.LayoutConflict_IntroAtStartup
            : L.LayoutConflict_IntroAfterSwitch;
        int introH = MeasureWrapped(hdc, _hFontText, introText, contentW);
        var introRect = new Win32.RECT { left = x, top = y, right = x + contentW, bottom = y + introH };
        Win32.DrawTextW(hdc, introText, -1, ref introRect,
            Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX);
        y += introH + S(14);

        // Question
        Win32.SelectObject(hdc, _hFontBold);
        Win32.SetTextColor(hdc, CLR_TITLE);
        var qRect = new Win32.RECT { left = x, top = y, right = x + contentW, bottom = y + S(20) };
        Win32.DrawTextW(hdc, L.LayoutConflict_Question, -1, ref qRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        y += S(28);

        // Section 1 — avant login
        DrawOption(hdc, ref y, x, contentW,
            L.LayoutConflict_Option1Heading,
            L.LayoutConflict_Option1Subline,
            L.LayoutConflict_Option1Body);
        y += S(10);

        // Section 2 — confort post-login
        DrawOption(hdc, ref y, x, contentW,
            L.LayoutConflict_Option2Heading,
            null,
            L.LayoutConflict_Option2Body);

        Win32.BitBlt(hdcPaint, 0, 0, cw, ch, hdc, 0, 0, Win32.SRCCOPY);
        Win32.SelectObject(hdc, hBmpOld);
        Win32.DeleteObject(hBmp);
        Win32.DeleteDC(hdc);
        Win32.EndPaint(hWnd, ref ps);
    }

    private void DrawOption(IntPtr hdc, ref int y, int x, int contentW,
        string heading, string? subline, string body)
    {
        Win32.SelectObject(hdc, _hFontBold);
        Win32.SetTextColor(hdc, CLR_HIGHLIGHT);
        int headH = MeasureWrapped(hdc, _hFontBold, heading, contentW);
        var headRect = new Win32.RECT { left = x, top = y, right = x + contentW, bottom = y + headH };
        Win32.DrawTextW(hdc, heading, -1, ref headRect,
            Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX);
        y += headH + S(2);

        if (!string.IsNullOrEmpty(subline))
        {
            Win32.SelectObject(hdc, _hFontText);
            Win32.SetTextColor(hdc, CLR_SUBTLE);
            int subH = MeasureWrapped(hdc, _hFontText, subline, contentW - S(16));
            var subRect = new Win32.RECT { left = x + S(16), top = y, right = x + contentW, bottom = y + subH };
            Win32.DrawTextW(hdc, subline, -1, ref subRect,
                Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX);
            y += subH + S(4);
        }

        Win32.SelectObject(hdc, _hFontText);
        Win32.SetTextColor(hdc, CLR_TEXT);
        int bodyH = MeasureWrapped(hdc, _hFontText, body, contentW - S(16));
        var bodyRect = new Win32.RECT { left = x + S(16), top = y, right = x + contentW, bottom = y + bodyH };
        Win32.DrawTextW(hdc, body, -1, ref bodyRect,
            Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX);
        y += bodyH;
    }

    private static int MeasureWrapped(IntPtr hdc, IntPtr hFont, string text, int width)
    {
        Win32.SelectObject(hdc, hFont);
        var rect = new Win32.RECT { left = 0, top = 0, right = width, bottom = 9999 };
        Win32.DrawTextW(hdc, text, -1, ref rect,
            Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX | Win32.DT_CALCRECT);
        return rect.bottom - rect.top;
    }

    public void Dispose()
    {
        if (_themeChanged != null)
        {
            Theme.Changed -= _themeChanged;
            _themeChanged = null;
        }
        if (_hWndBtnQuit != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndBtnQuit, _buttonSubclassProc, (UIntPtr)1);
        if (_hWndBtnKeep != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndBtnKeep, _buttonSubclassProc, (UIntPtr)2);
        if (_hWnd != IntPtr.Zero)
        {
            ThemeWindow.ForgetClassBackground(_hWnd);
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
        // Ni police ni brosse à libérer : les unes appartiennent au cache de Theme, l'autre au
        // système, qui la détruit avec la classe.

        // UnregisterClassW pour permettre une 2e instance avec un delegate WndProc frais.
        // Sans cela, la classe garde un pointeur vers _wndProcDelegate de cette instance
        // (potentiellement collecte par GC apres ce Dispose) → crash a la 2e instanciation.
        Win32.UnregisterClassW(WND_CLASS_NAME, Win32.GetModuleHandleW(null));
    }
}
