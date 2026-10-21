using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>Indicateur non interactif placé près du caret, avec le tray en secours.</summary>
internal sealed class LayerIndicatorWindow : IDisposable
{
    private readonly Win32.WNDPROC _wndProcDelegate;
    private IntPtr _hWnd;
    private IntPtr _hBrush;
    private string _label = string.Empty;
    private MaintainableLayerMode _mode;
    private bool _visible;
    // D1 (accessibilité 1.3.0) : DPI de l'écran qui porte l'indicateur.
    private int _dpi = 96;

    public LayerIndicatorWindow()
    {
        _wndProcDelegate = WndProc;
        IntPtr instance = Win32.GetModuleHandleW(null);
        // Audit du 25/09, X-01 : une classe refusée ne crée pas de fenêtre.
        if (!NativeWindow.RegisterClass(ProductIdentity.WindowClass("LayerIndicator"), _wndProcDelegate))
            return;

        uint exStyle = Win32.WS_EX_TOPMOST | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE;
        _hWnd = Win32.CreateWindowExW(exStyle, ProductIdentity.WindowClass("LayerIndicator"), string.Empty,
            Win32.WS_POPUP, 0, 0, 112, 32,
            IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
        // Fond de la classe : Windows le détruit au désenregistrement. Audit 1.3.0, A10
        // (décision d'Antoine du 20/10) : papier, comme les fenêtres refondues.
        _hBrush = NativeWindow.ApplyClassBackground(_hWnd, PaperTheme.Paper);

        // D1 (accessibilité 1.3.0) : police au DPI de l'écran de la fenêtre, et non plus
        // 15 px fixes ; la taille suit dans RefreshPosition, WM_DPICHANGED suit le caret
        // d'un écran à l'autre.
        _dpi = NativeWindow.DpiOf(_hWnd);
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

    /// <summary>Police du libellé au DPI de l'écran : AppFonts garde la HFONT et la partage.</summary>
    internal static FontSpec LabelFont(int dpi) => FontSpec.Of(AppFonts.Text, TypeRamp.Body, TypeRamp.Semibold, dpi / 96f);

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
        var info = new TypingEngine.Windows.Win32.GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<TypingEngine.Windows.Win32.GUITHREADINFO>() };
        if (!TypingEngine.Windows.Win32.GetGUIThreadInfo(threadId, ref info)) return false;

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
            Paint(hdc, rect, _label, _mode, _hBrush, _dpi);
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
                var (width, height) = IndicatorSize(_label.Length, _dpi);
                Win32.SetWindowPos(_hWnd, IntPtr.Zero, 0, 0, width, height,
                    Win32.SWP_NOMOVE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
            }
            return IntPtr.Zero;
        }
        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    /// <summary>Dessin de l'indicateur, sans fenêtre : le banc de captures l'appelle aussi.</summary>
    internal static void Paint(IntPtr hdc, Win32.RECT rect, string label, MaintainableLayerMode mode, IntPtr brush, int dpi)
    {
        Win32.FillRect(hdc, ref rect, brush);
        // Un filet détache le papier d'un document blanc.
        PaperDraw.Frame(hdc, rect, PaperTheme.Border, Math.Max(1, dpi / 96));
        // Couche verrouillée : l'action (6,6:1 sur le papier) plutôt que l'ambre de l'étape 1,
        // trop juste (4,6:1) pour un libellé de 14 px.
        uint color = mode == MaintainableLayerMode.Locked ? PaperTheme.Action : PaperTheme.Ink;
        PaperText.DrawLine(hdc, label, LabelFont(dpi), rect, color, TextAlign.Center);
    }

    public void Dispose()
    {
        if (_hWnd != IntPtr.Zero)
        {
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
        NativeWindow.UnregisterClass(ProductIdentity.WindowClass("LayerIndicator"));
    }
}
