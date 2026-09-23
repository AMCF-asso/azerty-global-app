using System.Runtime.InteropServices;
using System.Text;

namespace AZERTYGlobal;

/// <summary>
/// Parcours d'activation volontaire et réglages des couches maintenables.
/// Fenêtre Win32 autonome pour conserver l'application sans WinForms/WPF.
/// </summary>
internal sealed class MaintainableLayersWindow : IDisposable
{
    private const uint BS_AUTOCHECKBOX = 0x0003;
    private const uint BS_DEFPUSHBUTTON = 0x0001;
    private const uint BM_GETCHECK = 0x00F0;
    private const uint BM_SETCHECK = 0x00F1;
    private const uint BST_CHECKED = 1;
    private const uint ES_NUMBER = 0x2000;
    private const uint ES_CENTER = 0x0001;
    // Même fond que SettingsWindow et UsageStatsWindow.
    private const uint CLR_BG = 0x00DDDDDD;

    private const int IDC_MASTER = 5201;
    private const int IDC_GREEK = 5202;
    private const int IDC_CYRILLIC = 5203;
    private const int IDC_SCIENTIFIC = 5204;
    private const int IDC_VISUAL = 5205;
    private const int IDC_DELAY = 5206;
    private const int IDC_SAVE = 5208;

    private readonly Win32.WNDPROC _wndProcDelegate;
    private IntPtr _hWnd;
    private IntPtr _hMaster;
    private IntPtr _hGreek;
    private IntPtr _hCyrillic;
    private IntPtr _hScientific;
    private IntPtr _hVisual;
    private IntPtr _hDelay;
    private IntPtr _hFont;
    private IntPtr _hFontTitle;
    // D1 (accessibilité 1.3.0) : DPI de l'écran de la fenêtre, et géométrie de référence à
    // 96 DPI de chaque contrôle, remise à l'échelle sur WM_DPICHANGED.
    private int _dpi = 96;
    private readonly List<(IntPtr Control, int X, int Y, int W, int H, bool Title)> _layout = new();
    private const int BASE_CLIENT_W = 520;
    private const int BASE_CLIENT_H = 408;
    private IntPtr _hBgBrush;
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
        // K1 (accessibilité 1.3.0) : Tab, Maj+Tab, Entrée et Échap entre les contrôles, comme
        // les autres fenêtres à contrôles. Inscrite le temps d'être visible, comme l'accueil :
        // inscrite en permanence, elle ferait passer chaque message du processus par
        // GetAncestor (voir DialogNavigation.TryRoute).
        DialogNavigation.Register(_hWnd);
        Win32.ShowWindow(_hWnd, 5);
        Win32.SetForegroundWindow(_hWnd);
        // Focus initial sur le premier contrôle : sans lui, le focus restait sur la fenêtre
        // elle-même, et rien n'était annoncé avant la première tabulation.
        Win32.SetFocus(_hMaster);
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
        IntPtr instance = Win32.GetModuleHandleW(null);
        var wc = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
            lpfnWndProc = _wndProcDelegate,
            hInstance = instance,
            hCursor = Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32512),
            // Sans brosse de classe, le fond n'est jamais effacé : au-dessus d'un jeu
            // plein écran, la fenêtre laissait voir la scène et ses contrôles en double
            // (constaté au smoke v1.2.0 du 2026-08-24, sous Trackmania).
            hbrBackground = _hBgBrush = Win32.CreateSolidBrush(CLR_BG),
            lpszClassName = ProductIdentity.WindowClass("MaintainableLayers")
        };
        Win32.RegisterClassExW(ref wc);

        const int clientW = BASE_CLIENT_W;
        const int clientH = BASE_CLIENT_H;
        uint style = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var rect = new Win32.RECT { left = 0, top = 0, right = clientW, bottom = clientH };
        Win32.AdjustWindowRectEx(ref rect, style, false, 0);

        var work = GetWorkArea();
        int width = rect.right - rect.left;
        int height = rect.bottom - rect.top;
        int x = work.left + Math.Max(0, (work.right - work.left - width) / 2);
        int y = work.top + Math.Max(0, (work.bottom - work.top - height) / 2);

        _hWnd = Win32.CreateWindowExW(0, ProductIdentity.WindowClass("MaintainableLayers"),
            $"{ProductIdentity.DisplayName} — {L.Layers_WindowTitleSuffix}",
            style, x, y, width, height, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
        Win32.EnableDarkTitleBar(_hWnd);

        // D1 (accessibilité 1.3.0) : la fenêtre existe, son DPI est celui de l'écran qui
        // l'accueille. Taille, positions et polices en découlent : elles étaient fixes en
        // pixels, et la fenêtre gardait sa taille 100 % à 200 %.
        _dpi = Win32.GetDpiForWindowOrDefault(_hWnd);
        FitWindowToDpi(work);
        CreateFonts();

        CreateStatic(instance, L.Layers_Title, 24, 20, 470, 26, _hFontTitle);
        // Le second groupe de phrases wrappe sur deux lignes dans 470 px :
        // réserver trois lignes pleines pour ne rien tronquer.
        CreateStatic(instance, L.Layers_Explainer, 24, 56, 470, 66, _hFont);

        _hMaster = CreateCheckbox(instance, IDC_MASTER, L.Layers_MasterCheckbox, 24, 134, 455, 28);
        CreateStatic(instance, L.Layers_AvailableLabel, 45, 175, 250, 24, _hFont);
        _hGreek = CreateCheckbox(instance, IDC_GREEK, L.Layers_GreekCheckbox, 45, 202, 210, 26);
        _hCyrillic = CreateCheckbox(instance, IDC_CYRILLIC, L.Layers_CyrillicCheckbox, 270, 202, 220, 26);
        _hScientific = CreateCheckbox(instance, IDC_SCIENTIFIC, L.Layers_ScientificCheckbox, 45, 234, 260, 26);

        _hVisual = CreateCheckbox(instance, IDC_VISUAL, L.Layers_VisualCheckbox, 24, 282, 360, 26);
        CreateStatic(instance, L.Layers_DelayLabel, 24, 322, 185, 26, _hFont);
        _hDelay = Win32.CreateWindowExW(0, "EDIT", "500",
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_BORDER | Win32.WS_TABSTOP | ES_NUMBER | ES_CENTER,
            210, 317, 70, 28, _hWnd, (IntPtr)IDC_DELAY, instance, IntPtr.Zero);
        SetFont(_hDelay, _hFont);
        Track(_hDelay, 210, 317, 70, 28);
        CreateStatic(instance, L.Layers_DelayUnit, 290, 322, 145, 26, _hFont);

        IntPtr save = Win32.CreateWindowExW(0, "BUTTON", L.Layers_SaveButton,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | BS_DEFPUSHBUTTON,
            350, 357, 140, 34, _hWnd, (IntPtr)IDC_SAVE, instance, IntPtr.Zero);
        SetFont(save, _hFont);
        Track(save, 350, 357, 140, 34);
    }

    private IntPtr CreateCheckbox(IntPtr instance, int id, string text, int x, int y, int w, int h)
    {
        IntPtr control = Win32.CreateWindowExW(0, "BUTTON", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | BS_AUTOCHECKBOX,
            x, y, w, h, _hWnd, (IntPtr)id, instance, IntPtr.Zero);
        SetFont(control, _hFont);
        Track(control, x, y, w, h);
        return control;
    }

    private void CreateStatic(IntPtr instance, string text, int x, int y, int w, int h, IntPtr font)
    {
        IntPtr control = Win32.CreateWindowExW(0, "STATIC", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE,
            x, y, w, h, _hWnd, IntPtr.Zero, instance, IntPtr.Zero);
        SetFont(control, font);
        Track(control, x, y, w, h, font == _hFontTitle);
    }

    private int S(int value) => WindowSizing.ScaleForDpi(value, _dpi);

    private void CreateFonts()
    {
        _hFont = Win32.CreateFontW(S(-16), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        // -18 : le titre complet tient sur une seule ligne dans 470 px.
        _hFontTitle = Win32.CreateFontW(S(-18), 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
    }

    /// <summary>D1 : retient la géométrie de référence du contrôle et l'applique au DPI courant.</summary>
    private void Track(IntPtr control, int x, int y, int w, int h, bool title = false)
    {
        _layout.Add((control, x, y, w, h, title));
        Win32.MoveWindow(control, S(x), S(y), S(w), S(h), false);
    }

    /// <summary>D1 : taille de la fenêtre au DPI courant, centrée dans la zone de travail.</summary>
    private void FitWindowToDpi(Win32.RECT work)
    {
        uint style = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var rect = new Win32.RECT { left = 0, top = 0, right = S(BASE_CLIENT_W), bottom = S(BASE_CLIENT_H) };
        Win32.AdjustWindowRectEx(ref rect, style, false, 0);
        int width = rect.right - rect.left;
        int height = rect.bottom - rect.top;
        int x = work.left + Math.Max(0, (work.right - work.left - width) / 2);
        int y = work.top + Math.Max(0, (work.bottom - work.top - height) / 2);
        Win32.MoveWindow(_hWnd, x, y, width, height, false);
    }

    /// <summary>D1 : polices et contrôles au nouveau DPI (WM_DPICHANGED).</summary>
    private void ApplyDpiToControls()
    {
        IntPtr oldFont = _hFont;
        IntPtr oldTitle = _hFontTitle;
        CreateFonts();
        foreach (var (control, x, y, w, h, title) in _layout)
        {
            SetFont(control, title ? _hFontTitle : _hFont);
            Win32.MoveWindow(control, S(x), S(y), S(w), S(h), true);
        }
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
            Win32.MessageBoxW(_hWnd, L.Layers_ActivatedBody, ProductIdentity.DisplayName, 0x40);
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
                    return _hBgBrush;
                case Win32.WM_DPICHANGED:
                {
                    // D1 : même traitement que LayoutConflictWindow — fenêtre au rectangle
                    // suggéré par Windows, polices et contrôles au nouveau DPI.
                    int newDpi = (wParam.ToInt32() >> 16) & 0xFFFF;
                    if (newDpi > 0) _dpi = newDpi;
                    var suggested = Marshal.PtrToStructure<Win32.RECT>(lParam);
                    Win32.MoveWindow(_hWnd, suggested.left, suggested.top,
                        suggested.right - suggested.left, suggested.bottom - suggested.top, true);
                    ApplyDpiToControls();
                    return IntPtr.Zero;
                }
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
        Win32.SendMessageW(control, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)BST_CHECKED;

    private static void SetChecked(IntPtr control, bool value) =>
        Win32.SendMessageW(control, BM_SETCHECK, value ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);

    private static Win32.RECT GetWorkArea()
    {
        IntPtr monitor = Win32.GetCursorPos(out var cursor)
            ? Win32.MonitorFromPoint(cursor, Win32.MONITOR_DEFAULTTONEAREST)
            : IntPtr.Zero;
        var info = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        return monitor != IntPtr.Zero && Win32.GetMonitorInfo(monitor, ref info)
            ? info.rcWork
            : new Win32.RECT { left = 0, top = 0, right = 1024, bottom = 768 };
    }

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
        Win32.UnregisterClassW(ProductIdentity.WindowClass("MaintainableLayers"), Win32.GetModuleHandleW(null));
        if (_hBgBrush != IntPtr.Zero) Win32.DeleteObject(_hBgBrush);
    }
}
