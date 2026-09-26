using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>Indicateur non interactif placé près du caret, avec le tray en secours.</summary>
internal sealed class LayerIndicatorWindow : IDisposable
{
    private const uint DT_CENTER = 0x00000001;
    private const uint DT_VCENTER = 0x00000004;
    private const uint DT_SINGLELINE = 0x00000020;

    private readonly Win32.WNDPROC _wndProcDelegate;
    private IntPtr _hWnd;
    private IntPtr _hBrush;
    private IntPtr _hFont;
    private string _label = string.Empty;
    private MaintainableLayerMode _mode;
    private bool _visible;
    // D1 (accessibilité 1.3.0) : DPI de l'écran qui porte l'indicateur.
    private int _dpi = 96;

    public LayerIndicatorWindow()
    {
        _wndProcDelegate = WndProc;
        IntPtr instance = Win32.GetModuleHandleW(null);
        _hBrush = Win32.CreateSolidBrush(0x00352A20);

        var wc = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
            lpfnWndProc = _wndProcDelegate,
            hInstance = instance,
            hCursor = Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32512),
            hbrBackground = _hBrush,
            lpszClassName = ProductIdentity.WindowClass("LayerIndicator")
        };
        Win32.RegisterClassExW(ref wc);

        uint exStyle = Win32.WS_EX_TOPMOST | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE;
        _hWnd = Win32.CreateWindowExW(exStyle, ProductIdentity.WindowClass("LayerIndicator"), string.Empty,
            Win32.WS_POPUP, 0, 0, 112, 32,
            IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);

        // D1 (accessibilité 1.3.0) : police au DPI de l'écran de la fenêtre, et non plus
        // 15 px fixes ; la taille suit dans RefreshPosition, WM_DPICHANGED suit le caret
        // d'un écran à l'autre.
        _dpi = NativeWindow.DpiOf(_hWnd);
        RecreateFont();
    }

    /// <summary>
    /// D1 : taille de l'indicateur pour un libellé de <paramref name="labelLength"/>
    /// caractères. La formule et ses bornes (92 à 180 px de large, 32 de haut) valent à
    /// 96 DPI ; le tout suit l'échelle. Fixes en pixels, la boîte et la police restaient à
    /// la taille 100 % : à 200 %, un libellé moitié plus petit que voulu.
    /// </summary>
    internal static (int Width, int Height) IndicatorSize(int labelLength, int dpi)
    {
        int baseWidth = Math.Clamp(42 + labelLength * 7, 92, 180);
        return (WindowSizing.ScaleForDpi(baseWidth, dpi), WindowSizing.ScaleForDpi(32, dpi));
    }

    private void RecreateFont()
    {
        if (_hFont != IntPtr.Zero) Win32.DeleteObject(_hFont);
        _hFont = Win32.CreateFontW(WindowSizing.ScaleForDpi(-15, _dpi), 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
    }

    public void Update(MaintainableLayerState state, bool secureInput)
    {
        if (!ConfigManager.MaintainableLayersEnabled ||
            !ConfigManager.MaintainableVisualFeedbackEnabled ||
            secureInput || !state.IsActive)
        {
            Hide();
            return;
        }

        _mode = state.Mode;
        string suffix = state.Mode switch
        {
            MaintainableLayerMode.OneShot => " · 1",
            MaintainableLayerMode.Locked => L.Layers_IndicatorLockedSuffix,
            _ => ""
        };
        _label = TrayApplication.GetMaintainableLayerLabel(state.LayerId) + suffix;
        RefreshPosition();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    public void RefreshPosition()
    {
        if (string.IsNullOrEmpty(_label)) return;
        if (!TryGetCaretPoint(out var point))
        {
            Hide();
            return;
        }

        // Largeur adaptée au libellé, bornée pour rester discrète ; D1 : au DPI de l'écran.
        var (width, height) = IndicatorSize(_label.Length, _dpi);
        int offset = WindowSizing.ScaleForDpi(8, _dpi);
        Win32.SetWindowPos(_hWnd, Win32.HWND_TOPMOST, point.x + offset, point.y + offset,
            width, height, Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
        _visible = true;
    }

    public void Hide()
    {
        _label = string.Empty;
        if (_visible)
            Win32.ShowWindow(_hWnd, 0);
        _visible = false;
    }

    private static bool TryGetCaretPoint(out Win32.POINT point)
    {
        point = default;
        IntPtr foreground = Win32.GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;

        uint threadId = Win32.GetWindowThreadProcessId(foreground, IntPtr.Zero);
        if (threadId == 0) return false;
        var info = new Win32.GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<Win32.GUITHREADINFO>() };
        if (!Win32.GetGUIThreadInfo(threadId, ref info)) return false;

        IntPtr caretWindow = info.hwndCaret != IntPtr.Zero ? info.hwndCaret : info.hwndFocus;
        if (caretWindow == IntPtr.Zero) return false;
        point = new Win32.POINT { x = info.rcCaret.left, y = info.rcCaret.bottom };
        return Win32.ClientToScreen(caretWindow, ref point);
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == Win32.WM_PAINT)
        {
            IntPtr hdc = Win32.BeginPaint(hWnd, out var ps);
            Win32.GetClientRect(hWnd, out var rect);
            Win32.FillRect(hdc, ref rect, _hBrush);
            IntPtr oldFont = Win32.SelectObject(hdc, _hFont);
            Win32.SetBkMode(hdc, 1);
            uint color = _mode == MaintainableLayerMode.Locked ? 0x0060D8FFu : 0x00FFFFFFu;
            Win32.SetTextColor(hdc, color);
            Win32.DrawTextW(hdc, _label, -1, ref rect, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
            Win32.SelectObject(hdc, oldFont);
            Win32.EndPaint(hWnd, ref ps);
            return IntPtr.Zero;
        }
        if (msg == Win32.WM_DPICHANGED)
        {
            // D1 : le caret a changé d'écran. Police et taille au nouveau DPI ; la position
            // reste celle que RefreshPosition vient de donner.
            int newDpi = (wParam.ToInt32() >> 16) & 0xFFFF;
            if (newDpi > 0 && newDpi != _dpi)
            {
                _dpi = newDpi;
                RecreateFont();
                var (width, height) = IndicatorSize(_label.Length, _dpi);
                Win32.SetWindowPos(_hWnd, IntPtr.Zero, 0, 0, width, height,
                    Win32.SWP_NOMOVE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
            }
            return IntPtr.Zero;
        }
        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hWnd != IntPtr.Zero)
        {
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
        if (_hFont != IntPtr.Zero) Win32.DeleteObject(_hFont);
        if (_hBrush != IntPtr.Zero) Win32.DeleteObject(_hBrush);
        Win32.UnregisterClassW(ProductIdentity.WindowClass("LayerIndicator"), Win32.GetModuleHandleW(null));
    }
}
