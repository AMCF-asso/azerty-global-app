// Fenetre custom affichee quand l'app detecte qu'une disposition systeme AZERTY Global
// est deja active. Propose un choix eclaire entre garder l'app (post-login user-friendly)
// ou garder la disposition systeme (avant login : mot de passe Windows, etc.).
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Mini-fenetre modale topmost qui explique le trade-off app vs disposition systeme,
/// puis demande a l'utilisateur de choisir : Garder l'application | Quitter l'application.
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
    // Hauteur de départ seulement : RepositionControls la recalcule sous les deux liens.
    private const int BASE_WIN_H = 440;

    // Couleurs alignees sur AboutWindow / SettingsWindow
    private const uint CLR_BG = LightTheme.Background;
    private const uint CLR_TITLE = LightTheme.Text;
    private const uint CLR_TEXT = LightTheme.Text;

    private IntPtr _hWnd;
    private IntPtr _hWndBtnQuit;
    private IntPtr _hWndBtnKeep;

    private readonly Win32.WNDPROC _wndProcDelegate;
    private readonly IntPtr _hBgBrush;

    private readonly bool _isAtStartup;
    private readonly Action _onQuit;
    private readonly Action _onKeep;

    private float _dpiScale;
    private int S(int val) => (int)(val * _dpiScale);
    private int _clientH;

    private IntPtr _hFontTitle;
    private IntPtr _hFontText;
    private IntPtr _hFontBold;
    private IntPtr _hFontButton;

    /// <param name="isAtStartup">
    /// true : detection au demarrage de l'app (« est deja installee »).
    /// false : detection apres switch Ctrl+Shift (« vient d'etre activee »).
    /// </param>
    /// <param name="onQuit">Callback appele si l'utilisateur choisit « Quitter l'application ».</param>
    /// <param name="onKeep">Callback appele si l'utilisateur choisit « Garder l'app » (ou ferme la fenetre).</param>
    public LayoutConflictWindow(bool isAtStartup, Action onQuit, Action onKeep)
    {
        _wndProcDelegate = WndProc;
        _hBgBrush = Win32.CreateSolidBrush(CLR_BG);
        _isAtStartup = isAtStartup;
        _onQuit = onQuit;
        _onKeep = onKeep;

        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        int dpi = Win32.GetDeviceCaps(hdcScreen, 88);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        _dpiScale = dpi / 96f;
        _clientH = S(BASE_WIN_H);

        CreateFonts();
        CreateMainWindow();
        CreateControls();
        ApplyFontsToControls();
        RepositionControls();
        ResizeWindow();

        if (NativeWindow.CorrectedScale(Win32.GetDpiForWindow(_hWnd), _dpiScale) is float windowScale)
        {
            _dpiScale = windowScale;
            // Mise en page seule : GetDpiForWindow ne lève pas (audit du 25/09, X-04).
            try
            {
                RecreateFonts();
                RepositionControls();
                ResizeWindow();
            }
            catch { }
        }
    }

    private void CreateFonts()
    {
        _hFontTitle = Win32.CreateFontW(-S(TypeRamp.Title), 0, 0, 0, TypeRamp.Semibold, 0, 0, 0, 0, 0, 0, 5, 0, TypeRamp.Family);
        _hFontText = Win32.CreateFontW(-S(TypeRamp.Body), 0, 0, 0, TypeRamp.Regular, 0, 0, 0, 0, 0, 0, 5, 0, TypeRamp.Family);
        _hFontBold = Win32.CreateFontW(-S(TypeRamp.Body), 0, 0, 0, TypeRamp.Semibold, 0, 0, 0, 0, 0, 0, 5, 0, TypeRamp.Family);
        _hFontButton = Win32.CreateFontW(-S(TypeRamp.Body), 0, 0, 0, TypeRamp.Regular, 0, 0, 0, 0, 0, 0, 5, 0, TypeRamp.Family);
    }

    private void DestroyFonts()
    {
        Win32.DeleteObject(_hFontTitle);
        Win32.DeleteObject(_hFontText);
        Win32.DeleteObject(_hFontBold);
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
        Win32.SendMessageW(_hWndBtnQuit, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
        Win32.SendMessageW(_hWndBtnKeep, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
    }

    private void CreateMainWindow()
    {
        var hInstance = Win32.GetModuleHandleW(null);
        // Sans fond de classe : WM_ERASEBKGND rend 1 et OnPaint remplit tout le fond.
        // Audit du 25/09, X-01 : une classe refusée ne crée pas de fenêtre.
        if (!NativeWindow.RegisterClass(WND_CLASS_NAME, _wndProcDelegate))
            return;

        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        uint dwExStyle = Win32.WS_EX_TOPMOST;
        var (windowW, windowH) = NativeWindow.OuterSize(S(BASE_WIN_W), _clientH, dwStyle, dwExStyle);
        var (x, y) = NativeWindow.CenterIn(NativeWindow.WorkArea(), windowW, windowH);

        _hWnd = Win32.CreateWindowExW(dwExStyle, WND_CLASS_NAME,
            L.LayoutConflict_WindowTitle,
            dwStyle, x, y, windowW, windowH,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        // AG130-40 : cette fenetre veut Tab, Maj+Tab et Entree entre ses controles.
        DialogNavigation.Register(_hWnd);
        NativeWindow.ApplyFrame(_hWnd, dark: false);
    }

    /// <summary>
    /// Deux liens de commande, le choix non destructif d'abord : il est le bouton par défaut
    /// et reçoit le focus. Cette fenêtre peut surgir pendant une frappe (bascule Ctrl+Maj) ;
    /// « Quitter » en tête faisait d'une Entrée destinée au document un arrêt de l'application.
    /// </summary>
    private void CreateControls()
    {
        var hInstance = Win32.GetModuleHandleW(null);

        _hWndBtnKeep = Win32.CreateWindowExW(0, "BUTTON", L.LayoutConflict_BtnKeep,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_DEFCOMMANDLINK,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_KEEP, hInstance, IntPtr.Zero);

        _hWndBtnQuit = Win32.CreateWindowExW(0, "BUTTON", L.LayoutConflict_BtnQuit,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_COMMANDLINK,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_QUIT, hInstance, IntPtr.Zero);

        SetNote(_hWndBtnKeep, L.LayoutConflict_KeepNote);
        SetNote(_hWndBtnQuit, L.LayoutConflict_QuitNote);
    }

    private static void SetNote(IntPtr button, string note)
    {
        IntPtr pText = Marshal.StringToHGlobalUni(note);
        try { Win32.SendMessageW(button, Win32.BCM_SETNOTE, IntPtr.Zero, pText); }
        finally { Marshal.FreeHGlobal(pText); }
    }

    /// <summary>Hauteur d'un lien de commande à la largeur donnée, note repliée comprise
    /// (BCM_GETIDEALSIZE lit la largeur dans cx) ; le repli quand le contrôle ne répond pas.</summary>
    private static int IdealHeight(IntPtr button, int width, int fallback)
    {
        IntPtr pSize = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.POINT>());
        try
        {
            Marshal.StructureToPtr(new Win32.POINT { x = width, y = 0 }, pSize, false);
            if (Win32.SendMessageW(button, Win32.BCM_GETIDEALSIZE, IntPtr.Zero, pSize) == IntPtr.Zero)
                return fallback;
            int height = Marshal.PtrToStructure<Win32.POINT>(pSize).y;
            return height > 0 ? height : fallback;
        }
        finally { Marshal.FreeHGlobal(pSize); }
    }

    private void ResizeWindow() =>
        NativeWindow.ResizeAroundCenter(_hWnd, S(BASE_WIN_W), _clientH,
            Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU, Win32.WS_EX_TOPMOST);

    /// <summary>Les deux liens sous la question, pleine largeur, et la hauteur de la fenêtre
    /// qui en découle.</summary>
    private void RepositionControls()
    {
        int margin = S(24);
        int contentW = S(BASE_WIN_W) - margin * 2;
        int gap = S(8);

        IntPtr hdc = Win32.GetDC(_hWnd);
        int y;
        try { y = LayoutText(hdc, contentW, draw: false) + S(4); }
        finally { Win32.ReleaseDC(_hWnd, hdc); }

        int keepH = IdealHeight(_hWndBtnKeep, contentW, S(72));
        int quitH = IdealHeight(_hWndBtnQuit, contentW, S(72));
        Win32.MoveWindow(_hWndBtnKeep, margin, y, contentW, keepH, true);
        y += keepH + gap;
        Win32.MoveWindow(_hWndBtnQuit, margin, y, contentW, quitH, true);
        _clientH = y + quitH + S(20);
    }

    public void Show()
    {
        Win32.ShowWindow(_hWnd, 1);
        Win32.SetForegroundWindow(_hWnd);
        Win32.SetFocus(_hWndBtnKeep);
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

                // Les liens de commande thémés demandent le fond de leur parent
                // (DrawThemeParentBackground). La réponse est la fenêtre entière, peinte dans
                // leur DC déjà décalé, comme dans les Paramètres ; SaveDC : nos polices ne
                // restent pas dans leur DC.
                case Win32.WM_PRINTCLIENT:
                {
                    Win32.GetClientRect(hWnd, out var client);
                    int saved = Win32.SaveDC(wParam);
                    PaintClient(wParam, client);
                    Win32.RestoreDC(wParam, saved);
                    return IntPtr.Zero;
                }

                case Win32.WM_DPICHANGED:
                {
                    _dpiScale = NativeWindow.DpiFromChange(_hWnd, wParam) / 96f;
                    RecreateFonts();
                    NativeWindow.MoveToSuggestedRect(_hWnd, lParam);
                    RepositionControls();
                    ResizeWindow();
                    Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
                }

                case Win32.WM_COMMAND:
                {
                    int id = wParam.ToInt32() & 0xFFFF;
                    switch (id)
                    {
                        case IDC_BTN_QUIT: Close(true); break;
                        case IDC_BTN_KEEP: Close(false); break;
                        // K2 (accessibilite 1.3.0) : IsDialogMessageW ne livre jamais Entree ni
                        // Echap en WM_KEYDOWN, il les convertit en IDOK et IDCANCEL (voir
                        // DialogNavigation.IsEscapeCommand). Le gestionnaire VK_ESCAPE plus bas
                        // etait donc mort, et Entree ne pressait rien. Entree presse le bouton
                        // focalise, et lui seul : cette fenetre peut surgir pendant une frappe
                        // (bascule Ctrl+Maj), une Entree tapee pour un document ne doit pas
                        // quitter l'application. Echap garde l'app, comme la croix.
                        case DialogNavigation.IDOK:
                        {
                            IntPtr target = DialogNavigation.ButtonToPressOnEnter(id, Win32.GetFocus(),
                                new[] { _hWndBtnKeep, _hWndBtnQuit });
                            if (target != IntPtr.Zero) Win32.SendMessageW(target, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                            break;
                        }
                        case DialogNavigation.IDCANCEL: Close(false); break;
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
        using var paint = new PaintBuffer(hWnd);
        PaintClient(paint.Hdc, paint.Client);
    }

    /// <summary>Toute la zone cliente dans <paramref name="hdc"/> : (0, 0) pour WM_PAINT,
    /// l'origine d'un lien de commande pour WM_PRINTCLIENT.</summary>
    private void PaintClient(IntPtr hdc, Win32.RECT clientRect)
    {
        Win32.FillRect(hdc, ref clientRect, _hBgBrush);
        Win32.SetBkMode(hdc, 1);
        LayoutText(hdc, clientRect.right - S(24) * 2, draw: true);
    }

    /// <summary>Titre, phrase d'intro et question ; rend le bas de la question. Sert au dessin
    /// et au placement des liens, pour qu'ils ne divergent pas.</summary>
    private int LayoutText(IntPtr hdc, int contentW, bool draw)
    {
        int x = S(24);
        int y = S(20);

        // Titre
        Win32.SelectObject(hdc, _hFontTitle);
        Win32.SetTextColor(hdc, CLR_TITLE);
        var titleRect = new Win32.RECT { left = x, top = y, right = x + contentW, bottom = y + S(28) };
        if (draw)
            Win32.DrawTextW(hdc, L.LayoutConflict_Title, -1, ref titleRect,
                Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        y += S(36);

        // Intro variable selon origine
        Win32.SetTextColor(hdc, CLR_TEXT);
        string introText = _isAtStartup
            ? L.LayoutConflict_IntroAtStartup
            : L.LayoutConflict_IntroAfterSwitch;
        int introH = MeasureWrapped(hdc, _hFontText, introText, contentW);
        var introRect = new Win32.RECT { left = x, top = y, right = x + contentW, bottom = y + introH };
        if (draw)
            Win32.DrawTextW(hdc, introText, -1, ref introRect,
                Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX);
        y += introH + S(14);

        // Question
        Win32.SelectObject(hdc, _hFontBold);
        Win32.SetTextColor(hdc, CLR_TITLE);
        int qH = MeasureWrapped(hdc, _hFontBold, L.LayoutConflict_Question, contentW);
        var qRect = new Win32.RECT { left = x, top = y, right = x + contentW, bottom = y + qH };
        if (draw)
            Win32.DrawTextW(hdc, L.LayoutConflict_Question, -1, ref qRect,
                Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX);
        return y + qH + S(8);
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
        if (_hWnd != IntPtr.Zero)
        {
            // AG130-40 : desinscrire AVANT de detruire — Windows recycle les HWND.
            DialogNavigation.Unregister(_hWnd);
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
        DestroyFonts();
        Win32.DeleteObject(_hBgBrush);

        NativeWindow.UnregisterClass(WND_CLASS_NAME);
    }
}
