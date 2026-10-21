// Mini-fenetre TOPMOST en haut a droite affichee 2s lors du toggle on/off d'AZERTY Global.
// Sert de retour visuel quand un jeu en borderless windowed couvre la barre des taches
// (icone tray invisible). En exclusive fullscreen, la fenetre n'apparait pas — angle mort
// accepte (cf. discussion smoke test 2026-05-03).
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

internal sealed class ToggleNotification : IDisposable
{
    private const uint TIMER_AUTO_CLOSE = 0x9001;

    private const int BASE_W = 240;
    private const int BASE_H = 56;

    // Audit 1.3.0, A10 (décision d'Antoine du 20/10) : la bulle passe en papier, comme les
    // fenêtres refondues. Un filet la détache d'un document blanc ou d'un jeu clair.
    private const uint CLR_BG = PaperTheme.Paper;
    private const uint CLR_BORDER = PaperTheme.Border;
    private const uint CLR_ACTIVATED = PaperTheme.Success;
    private const uint CLR_DEACTIVATED = PaperTheme.TextSecondary;

    private IntPtr _hWnd;
    private readonly Win32.WNDPROC _wndProcDelegate;
    private readonly IntPtr _hBgBrush;

    private bool _currentActivated;
    private float _dpiScale;
    private int S(int v) => (int)(v * _dpiScale);

    public ToggleNotification()
    {
        _wndProcDelegate = WndProc;
        _hBgBrush = Win32.CreateSolidBrush(CLR_BG);

        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        int dpi = Win32.GetDeviceCaps(hdcScreen, 88);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        _dpiScale = dpi / 96f;

        CreateMainWindow();
    }

    /// <summary>Police du texte, au DPI : AppFonts garde la HFONT et la partage.</summary>
    internal static FontSpec TextFont(float scale) => FontSpec.Of(AppFonts.Text, TypeRamp.Body, TypeRamp.Semibold, scale);

    private void CreateMainWindow()
    {
        var hInstance = Win32.GetModuleHandleW(null);
        string className = ProductIdentity.WindowClass("ToggleNotif");
        // Audit du 25/09, X-01 : une classe refusée ne crée pas de fenêtre.
        if (!NativeWindow.RegisterClass(className, _wndProcDelegate))
            return;

        _hWnd = Win32.CreateWindowExW(
            Win32.WS_EX_TOPMOST | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_LAYERED,
            className, "",
            Win32.WS_POPUP,
            0, 0, S(BASE_W), S(BASE_H),
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        // Opacite ~94% — fond opaque et lisible
        Win32.SetLayeredWindowAttributes(_hWnd, 0, 240, Win32.LWA_ALPHA);
    }

    public void Show(bool activated)
    {
        _currentActivated = activated;

        // Position en haut a droite du moniteur du foreground (jeu) — pas seulement le primary.
        // Sans ca, en multi-ecran avec un jeu sur le secondaire, la fenetre TopMost s'afficherait
        // sur le primary et serait invisible.
        IntPtr fgWnd = Win32.GetForegroundWindow();
        IntPtr hMonitor = fgWnd != IntPtr.Zero
            ? Win32.MonitorFromWindow(fgWnd, Win32.MONITOR_DEFAULTTONEAREST)
            : Win32.MonitorFromWindow(_hWnd, Win32.MONITOR_DEFAULTTOPRIMARY);
        var mi = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        int x, y;
        int margin = S(16);
        if (hMonitor != IntPtr.Zero && Win32.GetMonitorInfo(hMonitor, ref mi))
        {
            x = mi.rcWork.right - S(BASE_W) - margin;
            y = mi.rcWork.top + margin;
        }
        else
        {
            // Fallback sur primary si MonitorFromWindow / GetMonitorInfo echoue
            int screenW = Win32.GetSystemMetrics(Win32.SM_CXSCREEN);
            x = screenW - S(BASE_W) - margin;
            y = margin;
        }

        // Re-affirmer le z-order au moment du toggle. ShowWindow seul peut laisser
        // une fenetre NOACTIVATE cachee derriere l'application actuellement focus.
        if (!Win32.SetWindowPos(_hWnd, Win32.HWND_TOPMOST, x, y, S(BASE_W), S(BASE_H),
            Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW))
        {
            Win32.MoveWindow(_hWnd, x, y, S(BASE_W), S(BASE_H), true);
            Win32.ShowWindow(_hWnd, Win32.SW_SHOWNOACTIVATE);
        }
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);

        // Auto-fermeture apres 2s — re-arm si appel rapide consecutif
        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_AUTO_CLOSE);
        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_AUTO_CLOSE, 2000, IntPtr.Zero);
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

                case Win32.WM_TIMER:
                    if ((uint)wParam.ToInt64() == TIMER_AUTO_CLOSE)
                    {
                        Win32.KillTimer(hWnd, (UIntPtr)TIMER_AUTO_CLOSE);
                        Win32.ShowWindow(hWnd, 0);
                    }
                    return IntPtr.Zero;

                case Win32.WM_DPICHANGED:
                {
                    // Le DPI peut changer si le moniteur change (multi-écran avec scales
                    // différents) ou si l'utilisateur modifie l'échelle système.
                    int newDpi = (wParam.ToInt32() >> 16) & 0xFFFF;
                    if (newDpi > 0)
                        _dpiScale = newDpi / 96f;
                    var suggested = Marshal.PtrToStructure<Win32.RECT>(lParam);
                    Win32.MoveWindow(hWnd, suggested.left, suggested.top,
                        suggested.right - suggested.left, suggested.bottom - suggested.top, true);
                    return IntPtr.Zero;
                }

                case Win32.WM_ERASEBKGND:
                    return (IntPtr)1;
            }
        }
        catch (Exception ex)
        {
            ConfigManager.Log("ToggleNotification WndProc", ex);
        }

        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void OnPaint(IntPtr hWnd)
    {
        using var paint = new PaintBuffer(hWnd);
        var clientRect = paint.Client;
        int cw = clientRect.right;
        int ch = clientRect.bottom;
        var hdc = paint.Hdc;

        Paint(hdc, clientRect, _currentActivated, _hBgBrush, _dpiScale);
    }

    /// <summary>Dessin de la bulle, sans fenêtre : le banc de captures l'appelle aussi.</summary>
    internal static void Paint(IntPtr hdc, Win32.RECT clientRect, bool activated, IntPtr bgBrush, float scale)
    {
        Win32.FillRect(hdc, ref clientRect, bgBrush);
        PaperDraw.Frame(hdc, clientRect, CLR_BORDER, Math.Max(1, (int)scale));
        string text = activated ? L.Toggle_Activated : L.Toggle_Deactivated;
        PaperText.DrawLine(hdc, text, TextFont(scale), clientRect,
            activated ? CLR_ACTIVATED : CLR_DEACTIVATED, TextAlign.Center);
    }

    public void Dispose()
    {
        if (_hWnd != IntPtr.Zero)
        {
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
        if (_hBgBrush != IntPtr.Zero)
        {
            Win32.DeleteObject(_hBgBrush);
        }
        NativeWindow.UnregisterClass(ProductIdentity.WindowClass("ToggleNotif"));
    }
}
