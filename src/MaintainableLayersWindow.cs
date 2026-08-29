using System.Runtime.InteropServices;
using System.Text;

namespace AZERTYGlobal;

/// <summary>
/// Parcours d'activation volontaire et réglages des couches maintenables.
/// Fenêtre Win32 autonome pour conserver l'application sans WinForms/WPF.
/// </summary>
internal sealed partial class MaintainableLayersWindow : IDisposable
{
    private const uint ES_NUMBER = 0x2000;
    private const uint ES_CENTER = 0x0001;

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
    private IntPtr _hTitle;
    private IntPtr _hExplainer;
    private IntPtr _hAvailable;
    private IntPtr _hDelayLabel;
    private IntPtr _hDelayUnit;
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
        Win32.ShowWindow(_hWnd, 5);
        Win32.SetForegroundWindow(_hWnd);
        _visible = true;
    }

    public void Hide()
    {
        SaveToConfig();
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
            // hbrBackground = IntPtr.Zero : une brosse inscrite ici appartient au système, qui
            // la détruit au UnregisterClassW du Dispose — la détruire une seconde fois, comme
            // faisait ce Dispose, ou la prendre dans le cache partagé de Theme, laisse un handle
            // mort derrière soi. ApplyClassBackground pose une brosse dédiée, ce qui garde la
            // propriété qui comptait : au-dessus d'un jeu plein écran, le fond est bien effacé
            // et la scène ne transparaît plus (smoke v1.2.0 du 2026-08-24, sous Trackmania).
            hbrBackground = IntPtr.Zero,
            lpszClassName = ProductIdentity.WindowClass("MaintainableLayers")
        };
        Win32.RegisterClassExW(ref wc);

        // WS_CLIPCHILDREN : sans lui, un repeint partiel du parent n'efface et ne redessine
        // que la zone invalide, et le cadre du champ ressort amputé de deux côtés.
        uint style = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU
            | Win32.WS_CLIPCHILDREN;
        var rect = new Win32.RECT { left = 0, top = 0, right = ClientW, bottom = ClientH };
        Win32.AdjustWindowRectEx(ref rect, style, false, 0);

        var work = GetWorkArea();
        int width = rect.right - rect.left;
        int height = rect.bottom - rect.top;
        int x = work.left + Math.Max(0, (work.right - work.left - width) / 2);
        int y = work.top + Math.Max(0, (work.bottom - work.top - height) / 2);

        _hWnd = Win32.CreateWindowExW(0, ProductIdentity.WindowClass("MaintainableLayers"),
            $"{ProductIdentity.DisplayName} — {L.Layers_WindowTitleSuffix}",
            style, x, y, width, height, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
        AdoptWindowDpi();

        // L'ordre de création est l'ordre de tabulation : la fenêtre se parcourt comme elle se
        // lit. La mise en page vient après, par LayoutControls, à l'échelle réellement adoptée.
        _hTitle = CreateStatic(instance, L.Layers_Title);
        _hExplainer = CreateStatic(instance, L.Layers_Explainer);
        _hMaster = CreateCheckbox(instance, IDC_MASTER, L.Layers_MasterCheckbox);
        _hAvailable = CreateStatic(instance, L.Layers_AvailableLabel);
        _hGreek = CreateCheckbox(instance, IDC_GREEK, L.Layers_GreekCheckbox);
        _hCyrillic = CreateCheckbox(instance, IDC_CYRILLIC, L.Layers_CyrillicCheckbox);
        _hScientific = CreateCheckbox(instance, IDC_SCIENTIFIC, L.Layers_ScientificCheckbox);
        _hVisual = CreateCheckbox(instance, IDC_VISUAL, L.Layers_VisualCheckbox);

        _hDelayLabel = CreateStatic(instance, L.Layers_DelayLabel);
        // Le champ perd WS_BORDER : la bordure gravée du système est ce que la refonte supprime,
        // et c'est le parent qui dessine celle de la charte.
        _hDelay = Win32.CreateWindowExW(0, "EDIT", "500",
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | ES_NUMBER | ES_CENTER,
            0, 0, 0, 0, _hWnd, (IntPtr)IDC_DELAY, instance, IntPtr.Zero);
        _hDelayUnit = CreateStatic(instance, L.Layers_DelayUnit);

        _hSave = Win32.CreateWindowExW(0, "BUTTON", L.Layers_SaveButton,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_OWNERDRAW,
            0, 0, 0, 0, _hWnd, (IntPtr)IDC_SAVE, instance, IntPtr.Zero);

        ApplyThemeToWindow();
    }

    private IntPtr CreateCheckbox(IntPtr instance, int id, string text)
    {
        _checked[id] = false;
        return Win32.CreateWindowExW(0, "BUTTON", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_OWNERDRAW,
            0, 0, 0, 0, _hWnd, (IntPtr)id, instance, IntPtr.Zero);
    }

    private IntPtr CreateStatic(IntPtr instance, string text) =>
        Win32.CreateWindowExW(0, "STATIC", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE,
            0, 0, 0, 0, _hWnd, IntPtr.Zero, instance, IntPtr.Zero);

    private void LoadFromConfig()
    {
        SetChecked(IDC_MASTER, ConfigManager.MaintainableLayersEnabled);
        SetChecked(IDC_GREEK, ConfigManager.MaintainableGreekEnabled);
        SetChecked(IDC_CYRILLIC, ConfigManager.MaintainableCyrillicEnabled);
        SetChecked(IDC_SCIENTIFIC, ConfigManager.MaintainableScientificEnabled);
        SetChecked(IDC_VISUAL, ConfigManager.MaintainableVisualFeedbackEnabled);
        Win32.SetWindowTextW(_hDelay, ConfigManager.MaintainableDoubleTapMilliseconds.ToString());
        UpdateEnabledState();
    }

    private void SaveToConfig()
    {
        bool wasEnabled = ConfigManager.MaintainableLayersEnabled;
        bool enabled = IsChecked(IDC_MASTER);
        int delay = ReadDelay();

        ConfigManager.SetMaintainableGreekEnabled(IsChecked(IDC_GREEK));
        ConfigManager.SetMaintainableCyrillicEnabled(IsChecked(IDC_CYRILLIC));
        ConfigManager.SetMaintainableScientificEnabled(IsChecked(IDC_SCIENTIFIC));
        ConfigManager.SetMaintainableVisualFeedbackEnabled(IsChecked(IDC_VISUAL));
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
        bool enabled = IsChecked(IDC_MASTER);
        _secondaryEnabled = enabled;
        Win32.EnableWindow(_hGreek, enabled);
        Win32.EnableWindow(_hCyrillic, enabled);
        Win32.EnableWindow(_hScientific, enabled);
        Win32.EnableWindow(_hVisual, enabled);
        Win32.EnableWindow(_hDelay, enabled);
        Repaint();
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (msg)
            {
                case Win32.WM_COMMAND:
                    int id = wParam.ToInt32() & 0xFFFF;
                    if (_checked.ContainsKey(id))
                    {
                        // Une case owner-draw n'est plus cochée par Windows : c'est ce clic qui
                        // la bascule, et le contrôle se repeint sur l'état que nous tenons.
                        _checked[id] = !_checked[id];
                        Win32.InvalidateRect(lParam, IntPtr.Zero, true);
                        Repaint();
                        if (id == IDC_MASTER)
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
                case Win32.WM_PAINT:
                    return OnPaint();
                case Win32.WM_ERASEBKGND:
                    // Le fond est peint par WM_PAINT. Laisser Windows l'effacer d'abord ferait
                    // clignoter la fenêtre à chaque frappe dans le champ.
                    return (IntPtr)1;
                case Win32.WM_CTLCOLORSTATIC:
                    return OnCtlColorStatic(wParam, lParam);
                case Win32.WM_CTLCOLORBTN:
                    // Windows efface le fond d'un bouton owner-draw avec la brosse rendue ici,
                    // avant d'envoyer WM_DRAWITEM.
                    return OnCtlColorStatic(wParam, lParam);
                case Win32.WM_CTLCOLOREDIT:
                    return OnCtlColorEdit(wParam);
                case Win32.WM_DRAWITEM:
                    if (TryDrawItem(lParam))
                        return (IntPtr)1;
                    break;
                case Win32.WM_DPICHANGED:
                    OnDpiChanged(wParam, lParam);
                    return IntPtr.Zero;
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

    private bool IsChecked(int id) => _checked.TryGetValue(id, out bool value) && value;

    private void SetChecked(int id, bool value)
    {
        _checked[id] = value;
        Repaint();
    }

    /// <summary>
    /// Redessine la fenêtre et ses enfants. WS_CLIPCHILDREN découpe les enfants hors du dessin
    /// du parent, donc un InvalidateRect posé sur la fenêtre ne les atteint plus : les cases
    /// owner-draw gardaient l'état de leur dernier rendu, et le cadre du champ ressortait amputé.
    /// </summary>
    private void Repaint()
    {
        if (_hWnd == IntPtr.Zero)
            return;
        Win32.RedrawWindow(_hWnd, IntPtr.Zero, IntPtr.Zero,
            Win32.RDW_INVALIDATE | Win32.RDW_ERASE | Win32.RDW_ALLCHILDREN | Win32.RDW_UPDATENOW);
    }

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
        DisposeTheme();
        if (_hWnd != IntPtr.Zero)
        {
            ThemeWindow.ForgetClassBackground(_hWnd);
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
        // Ni police ni brosse à libérer : les polices appartiennent au cache de Theme, et la
        // brosse de fond appartient au système, qui la détruit avec la classe.
        Win32.UnregisterClassW(ProductIdentity.WindowClass("MaintainableLayers"), Win32.GetModuleHandleW(null));
    }
}
