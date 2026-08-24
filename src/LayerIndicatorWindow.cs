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

    public LayerIndicatorWindow()
    {
        _wndProcDelegate = WndProc;
        IntPtr instance = Win32.GetModuleHandleW(null);
        _hBrush = Win32.CreateSolidBrush(0x00352A20);
        _hFont = Win32.CreateFontW(-15, 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");

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

        // Largeur adaptée au libellé, bornée pour rester discrète.
        int width = Math.Clamp(42 + _label.Length * 7, 92, 180);
        Win32.SetWindowPos(_hWnd, Win32.HWND_TOPMOST, point.x + 8, point.y + 8,
            width, 32, Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
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
