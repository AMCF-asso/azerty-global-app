using System.Text;

namespace AZERTYGlobal;

/// <summary>
/// Parcours d'activation volontaire et réglages des couches maintenables.
/// Fenêtre Win32 autonome pour conserver l'application sans WinForms/WPF.
/// </summary>
internal sealed class MaintainableLayersWindow : IDisposable
{
    // A9 : palette papier des Leçons et des Paramètres, ou celle du système en contraste
    // élevé (lue à la création, comme l'accueil).
    private readonly PaperPalette _pal = PaperPalette.Now();

    private const int IDC_MASTER = 5201;
    private const int IDC_GREEK = 5202;
    private const int IDC_CYRILLIC = 5203;
    private const int IDC_SCIENTIFIC = 5204;
    private const int IDC_VISUAL = 5205;
    private const int IDC_DELAY = 5206;
    private const int IDC_SAVE = 5208;

    private static readonly string ClassName = ProductIdentity.WindowClass("MaintainableLayers");
    // Lot 130 A : WS_VSCROLL dès la création (la barre existe, FitWindow la masque quand tout
    // tient), comme les Paramètres.
    private const uint Style = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU | Win32.WS_VSCROLL;
    private readonly WindowScroll _scroll = new();

    private readonly Win32.WNDPROC _wndProcDelegate;
    private IntPtr _hWnd;
    private IntPtr _hMaster;
    private IntPtr _hGreek;
    private IntPtr _hCyrillic;
    private IntPtr _hScientific;
    private IntPtr _hVisual;
    private IntPtr _hDelay;
    private IntPtr _hClose;
    // Libellés liés à l'interrupteur général : grisés avec leurs contrôles.
    private IntPtr _hAvailableLabel;
    private IntPtr _hDelayLabel;
    private IntPtr _hDelayUnit;
    private IntPtr _hFont;
    private IntPtr _hFontTitle;
    // D1 (accessibilité 1.3.0) : DPI de l'écran de la fenêtre, et géométrie de référence à
    // 96 DPI de chaque contrôle, remise à l'échelle sur WM_DPICHANGED.
    private int _dpi = 96;

    /// <summary>Ce que la mise en page fait d'un contrôle quand la largeur manque (lot 130 A) :
    /// les textes à retour à la ligne grandissent et poussent ce qui est dessous, le bouton
    /// reste ancré à droite.</summary>
    private enum Role { Plain, Title, Wrapped, Anchored }

    // Géométrie de référence à 96 DPI, dans l'ordre vertical : un texte qui grandit pousse
    // seulement les contrôles qui le suivent.
    private readonly List<(IntPtr Control, int X, int Y, int W, int H, Role Role)> _items = new();
    private const int BASE_CLIENT_W = 520;
    private const int BASE_CLIENT_H = 408;
    private IntPtr _hBgBrush;
    private IntPtr _hFieldBrush;
    private IntPtr[] _checkboxes = Array.Empty<IntPtr>();
    private bool _visible;

    public event Action? SettingsChanged;

    public bool IsVisible => _visible;

    public MaintainableLayersWindow()
    {
        _wndProcDelegate = WndProc;
        CreateWindow();
    }

    public void Show()
    {
        LoadFromConfig();
        // La fenêtre se rouvre en haut : l'instance est gardée d'une ouverture à l'autre, et
        // le défilement de la fermeture précédente masquerait le titre et la case générale.
        _scroll.ScrollTo(_hWnd, 0);
        // K1 (accessibilité 1.3.0) : Tab, Maj+Tab, Entrée et Échap entre les contrôles, comme
        // les autres fenêtres à contrôles. Inscrite le temps d'être visible, comme l'accueil :
        // inscrite en permanence, elle ferait passer chaque message du processus par
        // GetAncestor (voir DialogNavigation.TryRoute).
        // Lot 130 A : Tab vers un contrôle sous le pli le fait défiler jusqu'à lui.
        DialogNavigation.Register(_hWnd, control => _scroll.EnsureVisible(_hWnd, control, S(8)),
            (in Win32.MSG key) => _scroll.OnControlKey(_hWnd, key, S(24)));
        Win32.ShowWindow(_hWnd, 5);
        Win32.SetForegroundWindow(_hWnd);
        // Focus initial sur le premier contrôle : sans lui, le focus restait sur la fenêtre
        // elle-même, et rien n'était annoncé avant la première tabulation.
        Win32.SetFocus(_hMaster);
        _scroll.EnsureVisible(_hWnd, _hMaster, S(8));
        _visible = true;
    }

    public void Hide()
    {
        SaveToConfig();
        DialogNavigation.Unregister(_hWnd);
        Win32.ShowWindow(_hWnd, 0);
        _visible = false;
    }

    private void CreateWindow()
    {
        // Audit du 25/09, X-01 : une classe refusée ne crée pas de fenêtre.
        if (!NativeWindow.RegisterClass(ClassName, _wndProcDelegate))
            return;

        IntPtr instance = Win32.GetModuleHandleW(null);
        var work = NativeWindow.WorkArea();
        var (width, height) = NativeWindow.OuterSize(BASE_CLIENT_W, BASE_CLIENT_H, Style);
        var (x, y) = NativeWindow.CenterIn(work, width, height);

        _hWnd = Win32.CreateWindowExW(0, ClassName,
            $"{ProductIdentity.DisplayName} — {L.Layers_WindowTitleSuffix}",
            Style, x, y, width, height, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
        // Sans fond de classe, le fond n'est jamais effacé : au-dessus d'un jeu plein
        // écran, la fenêtre laissait voir la scène et ses contrôles en double (constaté
        // au smoke v1.2.0 du 2026-08-24, sous Trackmania).
        _hBgBrush = NativeWindow.ApplyClassBackground(_hWnd, _pal.Paper);
        _hFieldBrush = Win32.CreateSolidBrush(_pal.Surface);
        NativeWindow.ApplyFrame(_hWnd, dark: false);

        // D1 (accessibilité 1.3.0) : la fenêtre existe, son DPI est celui de l'écran qui
        // l'accueille. Taille, positions et polices en découlent : elles étaient fixes en
        // pixels, et la fenêtre gardait sa taille 100 % à 200 %.
        _dpi = NativeWindow.DpiOf(_hWnd);
        CreateFonts();

        // Le titre et l'explication reviennent à la ligne quand la largeur manque (lot 130 A).
        CreateStatic(instance, L.Layers_Title, 24, 18, 480, 30, _hFontTitle, Role.Title);
        // Le second groupe de phrases wrappe sur deux lignes dans 470 px :
        // réserver trois lignes pleines pour ne rien tronquer.
        CreateStatic(instance, L.Layers_Explainer, 24, 56, 470, 66, _hFont, Role.Wrapped);

        _hMaster = CreateCheckbox(instance, IDC_MASTER, L.Layers_MasterCheckbox, 24, 134, 455, 28);
        _hAvailableLabel = CreateStatic(instance, L.Layers_AvailableLabel, 45, 175, 250, 24, _hFont);
        _hGreek = CreateCheckbox(instance, IDC_GREEK, L.Layers_GreekCheckbox, 45, 202, 210, 26);
        _hCyrillic = CreateCheckbox(instance, IDC_CYRILLIC, L.Layers_CyrillicCheckbox, 270, 202, 220, 26);
        _hScientific = CreateCheckbox(instance, IDC_SCIENTIFIC, L.Layers_ScientificCheckbox, 45, 234, 260, 26);

        _hVisual = CreateCheckbox(instance, IDC_VISUAL, L.Layers_VisualCheckbox, 24, 282, 360, 26);
        _hDelayLabel = CreateStatic(instance, L.Layers_DelayLabel, 24, 322, 185, 26, _hFont);
        _hDelay = Win32.CreateWindowExW(0, "EDIT", "500",
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_BORDER | Win32.WS_TABSTOP | Win32.ES_NUMBER | Win32.ES_CENTER,
            210, 317, 70, 28, _hWnd, (IntPtr)IDC_DELAY, instance, IntPtr.Zero);
        SetFont(_hDelay, _hFont);
        Track(_hDelay, 210, 317, 70, 28, Role.Plain);
        _hDelayUnit = CreateStatic(instance, L.Layers_DelayUnit, 290, 322, 145, 26, _hFont);

        // « Fermer » : la fenêtre enregistre à toute fermeture (croix, Échap, Entrée) et n'a
        // pas d'annulation ; « Enregistrer » laissait croire que les autres sorties perdaient.
        IntPtr save = _hClose = Win32.CreateWindowExW(0, "BUTTON", L.Common_Close,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_DEFPUSHBUTTON,
            350, 357, 140, 34, _hWnd, (IntPtr)IDC_SAVE, instance, IntPtr.Zero);
        SetFont(save, _hFont);
        // Lot 130 A et C : « Fermer » reste à 30 px du bord droit de ce qui est affiché.
        Track(save, 350, 357, 140, 34, Role.Anchored);
        // Audit UX 1.3.0, #3 ; A9 : bouton papier. En contraste élevé, le dessin du système.
        if (!_pal.HighContrast)
            LightButton.Attach(save, paper: true);
        _checkboxes = new[] { _hMaster, _hGreek, _hCyrillic, _hScientific, _hVisual };

        FitWindow();
    }

    private IntPtr CreateCheckbox(IntPtr instance, int id, string text, int x, int y, int w, int h)
    {
        IntPtr control = Win32.CreateWindowExW(0, "BUTTON", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_AUTOCHECKBOX,
            x, y, w, h, _hWnd, (IntPtr)id, instance, IntPtr.Zero);
        SetFont(control, _hFont);
        Track(control, x, y, w, h, Role.Plain);
        return control;
    }

    private IntPtr CreateStatic(IntPtr instance, string text, int x, int y, int w, int h, IntPtr font,
        Role role = Role.Plain)
    {
        IntPtr control = Win32.CreateWindowExW(0, "STATIC", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE,
            x, y, w, h, _hWnd, IntPtr.Zero, instance, IntPtr.Zero);
        SetFont(control, font);
        Track(control, x, y, w, h, role);
        return control;
    }

    private int S(int value) => WindowSizing.ScaleForDpi(value, _dpi);

    // A9 : AG Texte et AG Titre, les polices du papier.
    private void CreateFonts()
    {
        AppFonts.EnsureLoaded();
        _hFont = Win32.CreateFontW(S(-TypeRamp.Body), 0, 0, 0, TypeRamp.Regular, 0, 0, 0, 0, 0, 0, 5, 0, AppFonts.Text);
        // -18 : le titre complet tient sur une seule ligne dans 470 px.
        _hFontTitle = Win32.CreateFontW(S(-TypeRamp.Title), 0, 0, 0, TypeRamp.Semibold, 0, 0, 0, 0, 0, 0, 5, 0, AppFonts.Title);
    }

    /// <summary>A9 : police des libellés de case, dessinés par <see cref="PaperChoice"/>.</summary>
    private FontSpec ChoiceFont => FontSpec.Of(AppFonts.Text, TypeRamp.Body, TypeRamp.Regular, _dpi / 96f);

    /// <summary>D1 : retient la géométrie de référence du contrôle ; <see cref="FitWindow"/> la
    /// pose au DPI et à la largeur courants.</summary>
    private void Track(IntPtr control, int x, int y, int w, int h, Role role) =>
        _items.Add((control, x, y, w, h, role));

    /// <summary>
    /// Lot 130 A (D1) : la fenêtre à sa largeur de référence, plafonnée à la zone de travail de
    /// son écran, et à la hauteur de son contenu, plafonnée de même ; au-delà, le contenu défile.
    /// À 200 % sur 1024×768, la référence (1040 px de zone cliente) dépassait l'écran : le bouton
    /// « Fermer » était coupé en bas. À 100 % comme à 175 %, la fenêtre garde sa taille.
    /// </summary>
    private void FitWindow()
    {
        var work = NativeWindow.WorkArea(_hWnd);
        var (frameW, frameH) = NativeWindow.NonClientSize(_hWnd, Style);
        // La dernière mise en page de FitScrollable est celle de fit.LayoutWidth : les contrôles
        // sont déjà à leur place.
        var fit = WindowSizing.FitScrollable(S(BASE_CLIENT_W), PlaceControls, frameW, frameH,
            work.right - work.left, work.bottom - work.top, NativeWindow.ScrollBarWidth(_hWnd));

        NativeWindow.ResizeAroundCenter(_hWnd, fit.ClientWidth, fit.ClientHeight, Style);
        NativeWindow.KeepInWorkArea(_hWnd);
        _scroll.Reset(_hWnd, fit.ContentHeight, fit.ClientHeight);
    }

    /// <summary>
    /// Les contrôles à la largeur de mise en page donnée, posés à l'origine (sans défilement) ;
    /// rend la hauteur du contenu. Aucun ne dépasse la marge de droite de la référence (16 px) ;
    /// le titre et l'explication, qui reviennent à la ligne, grandissent au besoin et poussent
    /// les contrôles suivants ; « Fermer » suit le bord droit.
    /// </summary>
    private int PlaceControls(int layoutW)
    {
        int rightLimit = layoutW - S(BASE_CLIENT_W - 504);
        int extra = 0;
        IntPtr hdc = Win32.GetDC(_hWnd);
        try
        {
            foreach (var (control, refX, refY, refW, refH, role) in _items)
            {
                int x = S(refX), y = S(refY) + extra, w = S(refW), h = S(refH);
                if (role == Role.Anchored)
                    x += layoutW - S(BASE_CLIENT_W);
                else
                    w = Math.Max(S(40), Math.Min(w, rightLimit - x));

                if (role is Role.Title or Role.Wrapped)
                {
                    // La taille se lit sur le contrôle : un texte plus long ne se tronque pas.
                    var text = new StringBuilder(Win32.GetWindowTextLengthW(control) + 1);
                    Win32.GetWindowTextW(control, text, text.Capacity);
                    int wanted = NativeWindow.MeasureWrapped(hdc, role == Role.Title ? _hFontTitle : _hFont, text.ToString(), w);
                    if (wanted > h)
                    {
                        extra += wanted - h;
                        h = wanted;
                    }
                }
                Win32.MoveWindow(control, x, y, w, h, true);
            }
        }
        finally { Win32.ReleaseDC(_hWnd, hdc); }
        return S(BASE_CLIENT_H) + extra;
    }

    /// <summary>D1 : polices et contrôles au nouveau DPI (WM_DPICHANGED).</summary>
    private void ApplyDpiToControls()
    {
        IntPtr oldFont = _hFont;
        IntPtr oldTitle = _hFontTitle;
        CreateFonts();
        foreach (var (control, _, _, _, _, role) in _items)
            Win32.SendMessageW(control, Win32.WM_SETFONT, role == Role.Title ? _hFontTitle : _hFont, (IntPtr)1);
        FitWindow();
        if (oldFont != IntPtr.Zero) Win32.DeleteObject(oldFont);
        if (oldTitle != IntPtr.Zero) Win32.DeleteObject(oldTitle);
    }

    private static void SetFont(IntPtr control, IntPtr font) =>
        Win32.SendMessageW(control, Win32.WM_SETFONT, font, (IntPtr)1);

    private void LoadFromConfig()
    {
        SetChecked(_hMaster, ConfigManager.MaintainableLayersEnabled);
        SetChecked(_hGreek, ConfigManager.MaintainableGreekEnabled);
        SetChecked(_hCyrillic, ConfigManager.MaintainableCyrillicEnabled);
        SetChecked(_hScientific, ConfigManager.MaintainableScientificEnabled);
        SetChecked(_hVisual, ConfigManager.MaintainableVisualFeedbackEnabled);
        Win32.SetWindowTextW(_hDelay, ConfigManager.MaintainableDoubleTapMilliseconds.ToString());
        UpdateEnabledState();
    }

    /// <summary>Boîte modale de Windows. Les témoins la remplacent : aucun test n'ouvre de
    /// vraie boîte sur le poste.</summary>
    internal static Func<IntPtr, string, string, uint, int> ShowMessageBox = Win32.MessageBoxW;

    private void SaveToConfig()
    {
        bool wasEnabled = ConfigManager.MaintainableLayersEnabled;
        bool enabled = IsChecked(_hMaster);
        int delay = ReadDelay();

        ConfigManager.SetMaintainableGreekEnabled(IsChecked(_hGreek));
        ConfigManager.SetMaintainableCyrillicEnabled(IsChecked(_hCyrillic));
        ConfigManager.SetMaintainableScientificEnabled(IsChecked(_hScientific));
        ConfigManager.SetMaintainableVisualFeedbackEnabled(IsChecked(_hVisual));
        ConfigManager.SetMaintainableDoubleTapMilliseconds(delay);
        ConfigManager.SetMaintainableLayersEnabled(enabled);
        if (enabled)
            ConfigManager.SetMaintainableTutorialCompleted(true);

        SettingsChanged?.Invoke();

        if (enabled && !wasEnabled)
        {
            ShowMessageBox(_hWnd, L.Layers_ActivatedBody, ProductIdentity.DisplayName, 0x40);
        }
    }

    private int ReadDelay()
    {
        var text = new StringBuilder(16);
        Win32.GetWindowTextW(_hDelay, text, text.Capacity);
        int value = int.TryParse(text.ToString(), out int parsed) ? parsed : 500;
        value = Math.Clamp(value, 150, 1000);
        Win32.SetWindowTextW(_hDelay, value.ToString());
        return value;
    }

    private void UpdateEnabledState()
    {
        bool enabled = IsChecked(_hMaster);
        Win32.EnableWindow(_hGreek, enabled);
        Win32.EnableWindow(_hCyrillic, enabled);
        Win32.EnableWindow(_hScientific, enabled);
        Win32.EnableWindow(_hVisual, enabled);
        Win32.EnableWindow(_hDelay, enabled);
        Win32.EnableWindow(_hAvailableLabel, enabled);
        Win32.EnableWindow(_hDelayLabel, enabled);
        Win32.EnableWindow(_hDelayUnit, enabled);
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (msg)
            {
                case Win32.WM_COMMAND:
                    int id = wParam.ToInt32() & 0xFFFF;
                    // K1 : Entrée et Échap arrivent d'IsDialogMessageW en IDOK et IDCANCEL,
                    // jamais en WM_KEYDOWN (DialogNavigation, revue R5). Les deux ferment en
                    // enregistrant, comme « Enregistrer », la croix et l'ancien Échap : cette
                    // fenêtre n'a pas d'annulation.
                    if (id == DialogNavigation.IDOK || DialogNavigation.IsEscapeCommand(id))
                    {
                        Hide();
                        return IntPtr.Zero;
                    }
                    if (id == IDC_MASTER)
                    {
                        UpdateEnabledState();
                        return IntPtr.Zero;
                    }
                    if (id == IDC_SAVE)
                    {
                        Hide();
                        return IntPtr.Zero;
                    }
                    break;
                case Win32.WM_KEYDOWN:
                    if (_scroll.TryHandle(_hWnd, msg, wParam, S(24)))
                        return IntPtr.Zero;
                    if (wParam == (IntPtr)0x1B)
                    {
                        Hide();
                        return IntPtr.Zero;
                    }
                    break;
                case Win32.WM_CTLCOLORSTATIC:
                case Win32.WM_CTLCOLORBTN:
                    // Statics et cases posent leur texte directement sur le fond de classe.
                    Win32.SetBkMode(wParam, 1);
                    Win32.SetBkColor(wParam, _pal.Paper);
                    Win32.SetTextColor(wParam, _pal.Ink);
                    return _hBgBrush;
                case Win32.WM_CTLCOLOREDIT:
                    // A9 : le délai, encre sur la surface du papier.
                    Win32.SetTextColor(wParam, _pal.Ink);
                    Win32.SetBkColor(wParam, _pal.Surface);
                    return _hFieldBrush;
                case Win32.WM_NOTIFY:
                {
                    // A9 : les cases natives se remplissaient de l'accent de Windows ; elles
                    // prennent le dessin papier des Paramètres. En contraste élevé, le système.
                    var nm = System.Runtime.InteropServices.Marshal.PtrToStructure<Win32.NMHDR>(lParam);
                    if (nm.code == PaperChoice.NM_CUSTOMDRAW && !_pal.HighContrast
                        && Array.IndexOf(_checkboxes, nm.hwndFrom) >= 0)
                        return PaperChoice.CustomDraw(lParam, ChoiceFont, _dpi / 96f, _pal);
                    break;
                }
                case Win32.WM_DPICHANGED:
                    // D1 : fenêtre au rectangle suggéré, polices et contrôles au nouveau DPI.
                    _dpi = NativeWindow.ApplyDpiChange(_hWnd, wParam, lParam);
                    ApplyDpiToControls();
                    return IntPtr.Zero;
                case Win32.WM_VSCROLL:
                case Win32.WM_MOUSEWHEEL:
                    if (_scroll.TryHandle(_hWnd, msg, wParam, S(24)))
                        return IntPtr.Zero;
                    break;
                case Win32.WM_CLOSE:
                    Hide();
                    return IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            ConfigManager.Log("MaintainableLayersWindow.WndProc", ex);
        }
        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private static bool IsChecked(IntPtr control) =>
        Win32.SendMessageW(control, Win32.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)Win32.BST_CHECKED;

    private static void SetChecked(IntPtr control, bool value) =>
        Win32.SendMessageW(control, Win32.BM_SETCHECK, value ? (IntPtr)Win32.BST_CHECKED : IntPtr.Zero, IntPtr.Zero);

    public void Dispose()
    {
        if (_hWnd != IntPtr.Zero)
        {
            // Désinscrire AVANT de détruire : Windows recycle les HWND.
            DialogNavigation.Unregister(_hWnd);
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
        if (_hFont != IntPtr.Zero) Win32.DeleteObject(_hFont);
        if (_hFontTitle != IntPtr.Zero) Win32.DeleteObject(_hFontTitle);
        if (_hFieldBrush != IntPtr.Zero) Win32.DeleteObject(_hFieldBrush);
        _hFieldBrush = IntPtr.Zero;
        // Le fond est celui de la classe : Windows le détruit ici.
        NativeWindow.UnregisterClass(ClassName);
    }
}
