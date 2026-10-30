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
    // Hauteur de départ seulement : PlaceControls la recalcule sous les deux liens.
    private const int BASE_WIN_H = 440;

    // Lot 130 A : WS_VSCROLL dès la création (la barre existe, FitWindow la masque quand tout
    // tient), comme les Paramètres.
    private const uint WindowStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU | Win32.WS_VSCROLL;
    private const uint WindowExStyle = Win32.WS_EX_TOPMOST;
    private readonly WindowScroll _scroll = new();

    // A9 : palette papier des Leçons, ou celle du système en contraste élevé (lue à la
    // création, comme l'accueil). Audit UX 1.3.0, #8 : l'intro passe en texte secondaire,
    // la question garde le texte plein.
    private readonly PaperPalette _pal = PaperPalette.Now();
    private const uint SS_NOPREFIX = 0x0080;
    private const int BS_TYPEMASK = 0x000F;
    private const int GWL_STYLE = -16;

    [DllImport("user32.dll")]
    private static extern int GetWindowLongW(IntPtr hWnd, int nIndex);

    private IntPtr _hWnd;
    private IntPtr _hWndBtnQuit;
    private IntPtr _hWndBtnKeep;
    // #8 : titre, intro et question étaient peints en GDI, invisibles du Narrateur. Ce sont
    // des STATIC, que la fenêtre place (LayoutText) et colore (WM_CTLCOLORSTATIC).
    private IntPtr _hTitle;
    private IntPtr _hIntro;
    private IntPtr _hQuestion;

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

    // A9 : les liens de commande se dessinent en cartes papier (NM_CUSTOMDRAW) ; leur note,
    // posée par BCM_SETNOTE pour le Narrateur, est gardée ici pour le dessin.
    private string _keepNote = "";
    private string _quitNote = "";
    private FontSpec LinkTitleFont => FontSpec.Of(AppFonts.Text, 15, TypeRamp.Semibold, _dpiScale);
    private FontSpec LinkNoteFont => FontSpec.Of(AppFonts.Text, TypeRamp.Body, TypeRamp.Regular, _dpiScale);

    /// <param name="isAtStartup">
    /// true : detection au demarrage de l'app (« est deja installee »).
    /// false : detection apres switch Ctrl+Shift (« vient d'etre activee »).
    /// </param>
    /// <param name="onQuit">Callback appele si l'utilisateur choisit « Quitter l'application ».</param>
    /// <param name="onKeep">Callback appele si l'utilisateur choisit « Garder l'app » (ou ferme la fenetre).</param>
    public LayoutConflictWindow(bool isAtStartup, Action onQuit, Action onKeep)
    {
        _wndProcDelegate = WndProc;
        _hBgBrush = Win32.CreateSolidBrush(_pal.Paper);
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
        FitWindow();

        if (NativeWindow.CorrectedScale(Win32.GetDpiForWindow(_hWnd), _dpiScale) is float windowScale)
        {
            _dpiScale = windowScale;
            // Mise en page seule : GetDpiForWindow ne lève pas (audit du 25/09, X-04).
            try
            {
                RecreateFonts();
                FitWindow();
            }
            catch { }
        }
    }

    // A9 : AG Titre et AG Texte, les polices du papier.
    private void CreateFonts()
    {
        _hFontTitle = TypeRamp.CreateIn(AppFonts.Title, TypeRamp.Title, TypeRamp.Semibold, _dpiScale);
        _hFontText = TypeRamp.CreateIn(AppFonts.Text, TypeRamp.Body, TypeRamp.Regular, _dpiScale);
        _hFontBold = TypeRamp.CreateIn(AppFonts.Text, TypeRamp.Body, TypeRamp.Semibold, _dpiScale);
        _hFontButton = TypeRamp.CreateIn(AppFonts.Text, TypeRamp.Body, TypeRamp.Regular, _dpiScale);
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
        Win32.SendMessageW(_hTitle, Win32.WM_SETFONT, _hFontTitle, (IntPtr)1);
        Win32.SendMessageW(_hIntro, Win32.WM_SETFONT, _hFontText, (IntPtr)1);
        Win32.SendMessageW(_hQuestion, Win32.WM_SETFONT, _hFontBold, (IntPtr)1);
    }

    private void CreateMainWindow()
    {
        var hInstance = Win32.GetModuleHandleW(null);
        // Sans fond de classe : WM_ERASEBKGND rend 1 et OnPaint remplit tout le fond.
        // Audit du 25/09, X-01 : une classe refusée ne crée pas de fenêtre.
        if (!NativeWindow.RegisterClass(WND_CLASS_NAME, _wndProcDelegate))
            return;

        uint dwStyle = WindowStyle;
        uint dwExStyle = WindowExStyle;
        var (windowW, windowH) = NativeWindow.OuterSize(S(BASE_WIN_W), _clientH, dwStyle, dwExStyle);
        var (x, y) = NativeWindow.CenterIn(NativeWindow.WorkArea(), windowW, windowH);

        _hWnd = Win32.CreateWindowExW(dwExStyle, WND_CLASS_NAME,
            L.LayoutConflict_WindowTitle,
            dwStyle, x, y, windowW, windowH,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        // AG130-40 : cette fenetre veut Tab, Maj+Tab et Entree entre ses controles.
        // Lot 130 A : Tab vers un lien sous le pli le fait défiler jusqu'à lui.
        DialogNavigation.Register(_hWnd, control => _scroll.EnsureVisible(_hWnd, control, S(8)));
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

        // Avant les liens dans l'ordre Z : le Narrateur les lit dans l'ordre de lecture.
        IntPtr CreateText(string text) => Win32.CreateWindowExW(0, "STATIC", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE | SS_NOPREFIX, 0, 0, 0, 0, _hWnd, IntPtr.Zero, hInstance, IntPtr.Zero);
        _hTitle = CreateText(L.LayoutConflict_Title);
        _hIntro = CreateText(_isAtStartup ? L.LayoutConflict_IntroAtStartup : L.LayoutConflict_IntroAfterSwitch);
        _hQuestion = CreateText(L.LayoutConflict_Question);

        _hWndBtnKeep = Win32.CreateWindowExW(0, "BUTTON", L.LayoutConflict_BtnKeep,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_DEFCOMMANDLINK,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_KEEP, hInstance, IntPtr.Zero);

        _hWndBtnQuit = Win32.CreateWindowExW(0, "BUTTON", L.LayoutConflict_BtnQuit,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_COMMANDLINK,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_QUIT, hInstance, IntPtr.Zero);

        _keepNote = L.LayoutConflict_KeepNote;
        _quitNote = L.LayoutConflict_QuitNote;
        SetNote(_hWndBtnKeep, _keepNote);
        SetNote(_hWndBtnQuit, _quitNote);
    }

    // ── A9 : liens de commande en cartes papier ─────────────────────

    private int LinkPadding => S(12);
    private int LinkTextLeft => S(40);
    private int LinkTitleHeight => S(24);
    private int LinkNoteLineHeight => S(20);

    /// <summary>Hauteur de la carte dessinée à la largeur donnée : titre, note repliée et
    /// marges. En contraste élevé, celle du lien natif.</summary>
    private int LinkHeight(IntPtr button, string note, int width, IntPtr hdc)
    {
        if (_pal.HighContrast)
            return IdealHeight(button, width, S(72));
        var noteRect = new Win32.RECT { left = 0, top = 0, right = Math.Max(1, width - LinkTextLeft - LinkPadding), bottom = 0 };
        int noteH = PaperText.DrawParagraph(hdc, new[] { new TextRun(note) }, LinkNoteFont, LinkNoteFont,
            noteRect, _pal.Muted, _pal.Muted, LinkNoteLineHeight, draw: false);
        return LinkPadding + LinkTitleHeight + noteH + LinkPadding;
    }

    /// <summary>
    /// Le lien de commande à l'étape PREPAINT : carte blanche cernée (le choix par défaut
    /// cerné d'action), flèche d'action, titre en encre semi-grasse, note en texte secondaire,
    /// contour de focus au clavier. Remplace le bleu #0078D7 du thème (audit 1.3.0, A9).
    /// </summary>
    private IntPtr OnCommandLinkCustomDraw(IntPtr lParam)
    {
        var cd = System.Runtime.InteropServices.Marshal.PtrToStructure<PaperChoice.NMCUSTOMDRAW>(lParam);
        if (cd.dwDrawStage != PaperChoice.CDDS_PREPAINT)
            return (IntPtr)PaperChoice.CDRF_DODEFAULT;

        IntPtr hWnd = cd.hdr.hwndFrom;
        var rc = cd.rc;
        int w = rc.right - rc.left, h = rc.bottom - rc.top;
        if (w <= 0 || h <= 0) return (IntPtr)PaperChoice.CDRF_SKIPDEFAULT;

        var (dc, bitmap, previous) = PaintBuffer.CreateSurface(w, h);
        IntPtr hdc = dc != IntPtr.Zero ? dc : cd.hdc;
        int ox = dc != IntPtr.Zero ? 0 : rc.left, oy = dc != IntPtr.Zero ? 0 : rc.top;
        try
        {
            bool primary = (GetWindowLongW(hWnd, GWL_STYLE) & BS_TYPEMASK) == (int)Win32.BS_DEFCOMMANDLINK;
            bool hot = (cd.uItemState & (PaperChoice.CDIS_HOT | PaperChoice.CDIS_SELECTED)) != 0;
            bool focused = (cd.uItemState & PaperChoice.CDIS_FOCUS) != 0 && PaperChoice.FocusCuesShown(hWnd);
            var card = new Win32.RECT { left = ox, top = oy, right = ox + w, bottom = oy + h };

            PaperDraw.Fill(hdc, card, _pal.Paper);
            var box = card;
            box.right -= 1;
            box.bottom -= 1;
            PaperDraw.RoundBox(hdc, box, hot ? _pal.FunctionKey : _pal.Surface, primary ? _pal.Action : _pal.Border,
                S(4), primary ? Math.Max(2, S(2)) : Math.Max(1, S(1)));
            if (focused)
            {
                var ring = new Win32.RECT { left = card.left + S(4), top = card.top + S(4), right = card.right - S(4), bottom = card.bottom - S(4) };
                PaperDraw.FocusOutline(hdc, ring, _pal.Action, 0, Math.Max(2, S(2)), S(6));
            }

            var titleRect = new Win32.RECT
            {
                left = card.left + LinkTextLeft, top = card.top + LinkPadding,
                right = card.right - LinkPadding, bottom = card.top + LinkPadding + LinkTitleHeight,
            };
            var arrowRect = new Win32.RECT { left = card.left + S(14), top = titleRect.top, right = titleRect.left, bottom = titleRect.bottom };
            PaperText.DrawLine(hdc, "→", LinkTitleFont, arrowRect, _pal.Action);
            var text = new System.Text.StringBuilder(256);
            Win32.GetWindowTextW(hWnd, text, text.Capacity);
            PaperText.DrawLine(hdc, text.ToString(), LinkTitleFont, titleRect, _pal.Ink);

            string note = hWnd == _hWndBtnKeep ? _keepNote : _quitNote;
            var noteRect = new Win32.RECT { left = titleRect.left, top = titleRect.bottom, right = titleRect.right, bottom = card.bottom - LinkPadding };
            PaperText.DrawParagraph(hdc, new[] { new TextRun(note) }, LinkNoteFont, LinkNoteFont,
                noteRect, _pal.Muted, _pal.Muted, LinkNoteLineHeight);
        }
        finally
        {
            if (dc != IntPtr.Zero)
            {
                Win32.BitBlt(cd.hdc, rc.left, rc.top, w, h, dc, 0, 0, Win32.SRCCOPY);
                PaintBuffer.ReleaseSurface(dc, bitmap, previous);
            }
        }
        return (IntPtr)PaperChoice.CDRF_SKIPDEFAULT;
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

    /// <summary>
    /// Lot 130 A (D1) : la fenêtre à sa largeur de référence, plafonnée à la zone de travail de
    /// son écran, et à la hauteur de son contenu, plafonnée de même ; au-delà, le contenu défile.
    /// À 200 % sur 1024×768, la référence (1120 px) dépassait l'écran : les cartes étaient
    /// coupées à droite et en bas. Le texte revient à la ligne à la largeur obtenue.
    /// </summary>
    private void FitWindow()
    {
        var work = NativeWindow.WorkArea(_hWnd);
        var (frameW, frameH) = NativeWindow.NonClientSize(_hWnd, WindowStyle, WindowExStyle);
        // La dernière mise en page de FitScrollable est celle de fit.LayoutWidth : les contrôles
        // sont déjà à leur place.
        var fit = WindowSizing.FitScrollable(S(BASE_WIN_W), PlaceControls, frameW, frameH,
            work.right - work.left, work.bottom - work.top, NativeWindow.ScrollBarWidth(_hWnd));

        _clientH = fit.ClientHeight;
        NativeWindow.ResizeAroundCenter(_hWnd, fit.ClientWidth, fit.ClientHeight, WindowStyle, WindowExStyle);
        NativeWindow.KeepInWorkArea(_hWnd);
        _scroll.Reset(_hWnd, fit.ContentHeight, fit.ClientHeight);
    }

    /// <summary>Les deux liens sous la question, pleine largeur, posés à l'origine (sans
    /// défilement) ; rend la hauteur du contenu qui en découle.</summary>
    private int PlaceControls(int layoutW)
    {
        int margin = S(24);
        int contentW = Math.Max(S(120), layoutW - margin * 2);
        int gap = S(8);

        IntPtr hdc = Win32.GetDC(_hWnd);
        int y, keepH, quitH;
        try
        {
            y = LayoutText(hdc, contentW) + S(4);
            keepH = LinkHeight(_hWndBtnKeep, _keepNote, contentW, hdc);
            quitH = LinkHeight(_hWndBtnQuit, _quitNote, contentW, hdc);
        }
        finally { Win32.ReleaseDC(_hWnd, hdc); }

        Win32.MoveWindow(_hWndBtnKeep, margin, y, contentW, keepH, true);
        y += keepH + gap;
        Win32.MoveWindow(_hWndBtnQuit, margin, y, contentW, quitH, true);
        return y + quitH + S(20);
    }

    public void Show()
    {
        // Tray crée une fenêtre neuve à chaque conflit : la remise à zéro ne sert qu'à une
        // instance rouverte. Le focus, lui, est posé par programme, hors de Tab : sans
        // EnsureVisible, « Garder » restait sous le pli quand le texte occupe la hauteur.
        _scroll.ScrollTo(_hWnd, 0);
        Win32.ShowWindow(_hWnd, 1);
        Win32.SetForegroundWindow(_hWnd);
        Win32.SetFocus(_hWndBtnKeep);
        _scroll.EnsureVisible(_hWnd, _hWndBtnKeep, S(8));
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
                    // Le rectangle suggéré applique l'échelle à l'ancienne taille : il peut
                    // dépasser l'écran. La fenêtre se remesure et se plafonne derrière.
                    FitWindow();
                    Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
                }

                case Win32.WM_VSCROLL:
                case Win32.WM_MOUSEWHEEL:
                    if (_scroll.TryHandle(_hWnd, msg, wParam, S(24)))
                        return IntPtr.Zero;
                    break;

                case Win32.WM_COMMAND:
                {
                    int id = wParam.ToInt32() & 0xFFFF;
                    switch (id)
                    {
                        case IDC_BTN_QUIT: Close(true); break;
                        case IDC_BTN_KEEP: Close(false); break;
                        // K2 (accessibilite 1.3.0) : IsDialogMessageW ne livre jamais Entree ni
                        // Echap en WM_KEYDOWN, il les convertit en IDOK et IDCANCEL (voir
                        // DialogNavigation.IsEscapeCommand). L'ancien gestionnaire VK_ESCAPE
                        // etait donc mort (retire, audit UX #8), et Entree ne pressait rien. Entree presse le bouton
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

                case Win32.WM_CTLCOLORSTATIC:
                    Win32.SetTextColor(wParam, lParam == _hIntro ? _pal.Muted : _pal.Ink);
                    Win32.SetBkColor(wParam, _pal.Paper);
                    return _hBgBrush;

                case Win32.WM_NOTIFY:
                {
                    // A9 : cartes papier ; en contraste élevé, le lien natif du système.
                    var nm = Marshal.PtrToStructure<Win32.NMHDR>(lParam);
                    if (nm.code == PaperChoice.NM_CUSTOMDRAW && !_pal.HighContrast
                        && (nm.hwndFrom == _hWndBtnKeep || nm.hwndFrom == _hWndBtnQuit))
                        return OnCommandLinkCustomDraw(lParam);
                    break;
                }

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
    }

    /// <summary>Place titre, intro et question (STATIC) ; rend le bas de la question, sous
    /// lequel viennent les liens.</summary>
    private int LayoutText(IntPtr hdc, int contentW)
    {
        int x = S(24);
        int y = S(20);

        // #8 : le titre revient à la ligne au lieu d'être coupé (DT_SINGLELINE sur S(28)).
        int titleH = MeasureWrapped(hdc, _hFontTitle, L.LayoutConflict_Title, contentW);
        Win32.MoveWindow(_hTitle, x, y, contentW, titleH, true);
        y += titleH + S(8);

        string introText = _isAtStartup
            ? L.LayoutConflict_IntroAtStartup
            : L.LayoutConflict_IntroAfterSwitch;
        int introH = MeasureWrapped(hdc, _hFontText, introText, contentW);
        Win32.MoveWindow(_hIntro, x, y, contentW, introH, true);
        y += introH + S(14);

        int qH = MeasureWrapped(hdc, _hFontBold, L.LayoutConflict_Question, contentW);
        Win32.MoveWindow(_hQuestion, x, y, contentW, qH, true);
        return y + qH + S(8);
    }

    private static int MeasureWrapped(IntPtr hdc, IntPtr hFont, string text, int width) =>
        NativeWindow.MeasureWrapped(hdc, hFont, text, width);

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
