using System.Runtime.InteropServices;
using System.Text;

namespace AZERTYGlobal;

sealed class PauseDurationDialog : IDisposable
{
    private static readonly string ClassName = ProductIdentity.WindowClass("PauseDuration");
    private const int IDOK = 1;
    private const int IDCANCEL = 2;
    private const int IDC_EDIT_HOURS = 4301;
    private const int IDC_EDIT_MINUTES = 4302;
    private const int IDC_HOURS_UP = 4303;
    private const int IDC_HOURS_DOWN = 4304;
    private const int IDC_MINUTES_UP = 4305;
    private const int IDC_MINUTES_DOWN = 4306;

    private const uint ES_AUTOHSCROLL = 0x0080;
    private const uint ES_CENTER = 0x0001;
    private const uint ES_NUMBER = 0x2000;
    private const uint BS_DEFPUSHBUTTON = 0x0001;
    private const uint BS_PUSHBUTTON = 0x0000;

    private readonly Win32.WNDPROC _wndProcDelegate;
    private IntPtr _hWnd;
    private IntPtr _hEditHours;
    private IntPtr _hEditMinutes;
    private IntPtr _hLabel;
    private IntPtr _hHours;
    private IntPtr _hMinutes;
    private IntPtr _hBtnOk;
    private IntPtr _hBtnCancel;
    private IntPtr _hFont;
    private Action<string>? _onAppLanguageChanged;
    private bool _done;
    private TimeSpan? _result;

    public PauseDurationDialog()
    {
        _wndProcDelegate = WndProc;
    }

    public static TimeSpan? Show(IntPtr owner)
    {
        using var dialog = new PauseDurationDialog();
        return dialog.ShowModal(owner);
    }

    private TimeSpan? ShowModal(IntPtr owner)
    {
        CreateWindow(owner);
        if (_hWnd == IntPtr.Zero)
            return null;

        if (owner != IntPtr.Zero)
            Win32.EnableWindow(owner, false);

        Win32.ShowWindow(_hWnd, 1);
        Win32.SetForegroundWindow(_hWnd);
        Win32.SetFocus(_hEditMinutes);

        try
        {
            while (!_done)
            {
                int ret = Win32.GetMessageW(out var msg, IntPtr.Zero, 0, 0);
                if (ret <= 0)
                    break;
                Win32.TranslateMessage(ref msg);
                Win32.DispatchMessageW(ref msg);
            }
        }
        finally
        {
            if (owner != IntPtr.Zero)
            {
                Win32.EnableWindow(owner, true);
                Win32.SetForegroundWindow(owner);
            }
        }

        return _result;
    }

    private void CreateWindow(IntPtr owner)
    {
        var hInstance = Win32.GetModuleHandleW(null);
        var wc = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
            lpfnWndProc = _wndProcDelegate,
            hInstance = hInstance,
            hCursor = Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32512),
            lpszClassName = ClassName
        };
        Win32.RegisterClassExW(ref wc);

        int clientW = 330;
        int clientH = 154;
        uint style = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var windowRect = new Win32.RECT { left = 0, top = 0, right = clientW, bottom = clientH };
        Win32.AdjustWindowRectEx(ref windowRect, style, false, 0);
        int windowW = windowRect.right - windowRect.left;
        int windowH = windowRect.bottom - windowRect.top;

        var work = GetWorkArea(owner);
        int x = work.left + Math.Max(0, (work.right - work.left - windowW) / 2);
        int y = work.top + Math.Max(0, (work.bottom - work.top - windowH) / 2);

        _hWnd = Win32.CreateWindowExW(0, ClassName, L.Pause_WindowTitle,
            style, x, y, windowW, windowH, owner, IntPtr.Zero, hInstance, IntPtr.Zero);

        _hFont = Win32.CreateFontW(-14, 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        CreateControls(hInstance);
        Win32.EnableDarkTitleBar(_hWnd);

        // La pop-up est modale mais l'icône tray reste accessible : un changement de
        // langue via le menu tray pendant qu'elle est ouverte doit la retraduire sur
        // place (sinon elle garde la langue d'ouverture — constat visuel 2026-07-23).
        _onAppLanguageChanged = _ => RefreshLanguage();
        ConfigManager.AppLanguageChanged += _onAppLanguageChanged;
    }

    private void RefreshLanguage()
    {
        if (_hWnd == IntPtr.Zero)
            return;
        Win32.SetWindowTextW(_hWnd, L.Pause_WindowTitle);
        Win32.SetWindowTextW(_hLabel, L.Pause_Label);
        Win32.SetWindowTextW(_hHours, L.Pause_Hours);
        Win32.SetWindowTextW(_hMinutes, L.Pause_Minutes);
        Win32.SetWindowTextW(_hBtnOk, L.Pause_BtnConfirm);
        Win32.SetWindowTextW(_hBtnCancel, L.Pause_BtnCancel);
    }

    private static Win32.RECT GetWorkArea(IntPtr owner)
    {
        IntPtr monitor = owner != IntPtr.Zero
            ? Win32.MonitorFromWindow(owner, Win32.MONITOR_DEFAULTTONEAREST)
            : IntPtr.Zero;

        if (monitor == IntPtr.Zero && Win32.GetCursorPos(out var cursor))
            monitor = Win32.MonitorFromPoint(cursor, Win32.MONITOR_DEFAULTTONEAREST);

        var info = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        if (monitor != IntPtr.Zero && Win32.GetMonitorInfo(monitor, ref info))
            return info.rcWork;

        return new Win32.RECT { left = 0, top = 0, right = 1024, bottom = 768 };
    }

    private void CreateControls(IntPtr hInstance)
    {
        _hLabel = CreateStatic(hInstance, L.Pause_Label, 18, 16, 280, 22);
        _hHours = CreateStatic(hInstance, L.Pause_Hours, 28, 60, 72, 22);
        _hMinutes = CreateStatic(hInstance, L.Pause_Minutes, 150, 60, 82, 22);

        _hEditHours = CreateEdit(hInstance, IDC_EDIT_HOURS, "0", 82, 54, 50, 26);
        _hEditMinutes = CreateEdit(hInstance, IDC_EDIT_MINUTES, "5", 218, 54, 50, 26);
        CreateButton(hInstance, IDC_HOURS_UP, "▲", 82, 38, 50, 15, BS_PUSHBUTTON);
        CreateButton(hInstance, IDC_HOURS_DOWN, "▼", 82, 81, 50, 15, BS_PUSHBUTTON);
        CreateButton(hInstance, IDC_MINUTES_UP, "▲", 218, 38, 50, 15, BS_PUSHBUTTON);
        CreateButton(hInstance, IDC_MINUTES_DOWN, "▼", 218, 81, 50, 15, BS_PUSHBUTTON);

        _hBtnOk = CreateButton(hInstance, IDOK, L.Pause_BtnConfirm, 96, 106, 120, 32, BS_DEFPUSHBUTTON);
        _hBtnCancel = CreateButton(hInstance, IDCANCEL, L.Pause_BtnCancel, 224, 106, 84, 32, BS_PUSHBUTTON);
    }

    private IntPtr CreateStatic(IntPtr hInstance, string text, int x, int y, int w, int h)
    {
        var hwnd = Win32.CreateWindowExW(0, "STATIC", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE,
            x, y, w, h, _hWnd, IntPtr.Zero, hInstance, IntPtr.Zero);
        Win32.SendMessageW(hwnd, Win32.WM_SETFONT, _hFont, (IntPtr)1);
        return hwnd;
    }

    private IntPtr CreateEdit(IntPtr hInstance, int id, string text, int x, int y, int w, int h)
    {
        var hwnd = Win32.CreateWindowExW(0, "EDIT", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_BORDER | Win32.WS_TABSTOP |
            ES_AUTOHSCROLL | ES_CENTER | ES_NUMBER,
            x, y, w, h, _hWnd, (IntPtr)id, hInstance, IntPtr.Zero);
        Win32.SendMessageW(hwnd, Win32.WM_SETFONT, _hFont, (IntPtr)1);
        return hwnd;
    }

    private IntPtr CreateButton(IntPtr hInstance, int id, string text, int x, int y, int w, int h, uint style)
    {
        var hwnd = Win32.CreateWindowExW(0, "BUTTON", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | style,
            x, y, w, h, _hWnd, (IntPtr)id, hInstance, IntPtr.Zero);
        Win32.SendMessageW(hwnd, Win32.WM_SETFONT, _hFont, (IntPtr)1);
        return hwnd;
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (msg)
            {
                case Win32.WM_COMMAND:
                {
                    int id = wParam.ToInt32() & 0xFFFF;
                    if (id == IDOK)
                    {
                        ValidateAndClose();
                        return IntPtr.Zero;
                    }
                    if (id == IDC_HOURS_UP)
                    {
                        AdjustHours(1);
                        return IntPtr.Zero;
                    }
                    if (id == IDC_HOURS_DOWN)
                    {
                        AdjustHours(-1);
                        return IntPtr.Zero;
                    }
                    if (id == IDC_MINUTES_UP)
                    {
                        AdjustMinutes(1);
                        return IntPtr.Zero;
                    }
                    if (id == IDC_MINUTES_DOWN)
                    {
                        AdjustMinutes(-1);
                        return IntPtr.Zero;
                    }
                    if (id == IDCANCEL)
                    {
                        Close(null);
                        return IntPtr.Zero;
                    }
                    break;
                }
                case Win32.WM_KEYDOWN:
                    if (wParam == (IntPtr)0x1B)
                    {
                        Close(null);
                        return IntPtr.Zero;
                    }
                    break;
                case Win32.WM_CLOSE:
                    Close(null);
                    return IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            ConfigManager.Log("PauseDurationDialog.WndProc", ex);
            Close(null);
            return IntPtr.Zero;
        }

        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void ValidateAndClose()
    {
        int hours = ReadInt(_hEditHours);
        int minutes = ReadInt(_hEditMinutes);
        int totalMinutes = hours * 60 + minutes;

        if (hours < 0 || minutes < 0 || minutes > 59 || totalMinutes < 1 || totalMinutes > 1439)
        {
            Win32.MessageBoxW(_hWnd,
                L.Pause_InvalidDuration,
                ProductIdentity.DisplayName, 0x30);
            return;
        }

        Close(TimeSpan.FromMinutes(totalMinutes));
    }

    private void AdjustHours(int delta)
    {
        int hours = Math.Clamp(ReadInt(_hEditHours) + delta, 0, 23);
        WriteInt(_hEditHours, hours);
    }

    private void AdjustMinutes(int direction)
    {
        int minutes = Math.Clamp(ReadInt(_hEditMinutes), 0, 59);
        if (direction > 0)
        {
            minutes = minutes >= 55 ? 55 : ((minutes / 5) + 1) * 5;
        }
        else
        {
            minutes = minutes <= 0 ? 0 : minutes % 5 == 0 ? minutes - 5 : minutes - (minutes % 5);
        }

        WriteInt(_hEditMinutes, minutes);
    }

    private static int ReadInt(IntPtr hwnd)
    {
        var sb = new StringBuilder(16);
        Win32.GetWindowTextW(hwnd, sb, sb.Capacity);
        return int.TryParse(sb.ToString(), out int value) ? value : 0;
    }

    private static void WriteInt(IntPtr hwnd, int value)
    {
        Win32.SetWindowTextW(hwnd, value.ToString());
    }

    private void Close(TimeSpan? result)
    {
        _result = result;
        _done = true;
        if (_hWnd != IntPtr.Zero)
            Win32.ShowWindow(_hWnd, 0);
    }

    public void Dispose()
    {
        if (_onAppLanguageChanged != null)
        {
            ConfigManager.AppLanguageChanged -= _onAppLanguageChanged;
            _onAppLanguageChanged = null;
        }
        if (_hWnd != IntPtr.Zero)
        {
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
        if (_hFont != IntPtr.Zero)
        {
            Win32.DeleteObject(_hFont);
            _hFont = IntPtr.Zero;
        }
        Win32.UnregisterClassW(ClassName, Win32.GetModuleHandleW(null));
    }
}
