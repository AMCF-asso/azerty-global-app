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
    // Zone client de référence, à 96 DPI.
    private const int BASE_CLIENT_W = 330;
    private const int BASE_CLIENT_H = 154;

    private readonly Win32.WNDPROC _wndProcDelegate;
    private IntPtr _hWnd;
    private IntPtr _hEditHours;
    private IntPtr _hEditMinutes;
    private IntPtr _hLabel;
    private IntPtr _hHours;
    private IntPtr _hMinutes;
    private IntPtr _hBtnOk;
    private IntPtr _hBtnCancel;
    // N10 (accessibilité 1.3.0) : boutons ▲▼, gardés pour leur poser un nom accessible.
    private IntPtr _hBtnHoursUp;
    private IntPtr _hBtnHoursDown;
    private IntPtr _hBtnMinutesUp;
    private IntPtr _hBtnMinutesDown;
    private IntPtr _hFont;
    // D1 (accessibilité 1.3.0) : DPI de l'écran de la fenêtre, et géométrie de référence à
    // 96 DPI de chaque contrôle, remise à l'échelle sur WM_DPICHANGED.
    private int _dpi = 96;
    private readonly List<(IntPtr Hwnd, int X, int Y, int W, int H)> _layout = new();
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
                // Audit 24/09 : 0 = WM_QUIT retiré de la file. Le reposter, sinon la boucle
                // principale ne le voit jamais et le processus survit sans icône. -1 (erreur)
                // reste une sortie simple.
                if (ret == 0)
                {
                    Win32.PostQuitMessage((int)msg.wParam);
                    break;
                }
                if (ret < 0)
                    break;
                // AG130-40 : cette boucle modale a la meme omission que la principale.
                if (DialogNavigation.TryRoute(ref msg)) continue;
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

        int clientW = BASE_CLIENT_W;
        int clientH = BASE_CLIENT_H;
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

        // D1 (accessibilité 1.3.0) : la fenêtre existe, son DPI est celui de l'écran qui
        // l'accueille. Taille, positions et police en découlent : elles étaient fixes en
        // pixels, et la fenêtre gardait sa taille 100 % à 200 %. Invisible jusqu'à ShowModal,
        // le redimensionnement ne se voit pas.
        _dpi = Win32.GetDpiForWindowOrDefault(_hWnd);
        FitWindowToDpi(work);

        // AG130-40 : cette fenetre veut Tab, Maj+Tab et Entree entre ses controles.
        DialogNavigation.Register(_hWnd);

        _hFont = CreateScaledFont();
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
        AnnotateSpinButtons();
    }

    private int S(int value) => WindowSizing.ScaleForDpi(value, _dpi);

    private IntPtr CreateScaledFont() =>
        Win32.CreateFontW(S(-14), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");

    /// <summary>D1 : taille de la fenêtre au DPI courant, centrée dans la zone de travail.</summary>
    private void FitWindowToDpi(Win32.RECT work)
    {
        uint style = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var rect = new Win32.RECT { left = 0, top = 0, right = S(BASE_CLIENT_W), bottom = S(BASE_CLIENT_H) };
        Win32.AdjustWindowRectEx(ref rect, style, false, 0);
        int windowW = rect.right - rect.left;
        int windowH = rect.bottom - rect.top;
        int x = work.left + Math.Max(0, (work.right - work.left - windowW) / 2);
        int y = work.top + Math.Max(0, (work.bottom - work.top - windowH) / 2);
        Win32.MoveWindow(_hWnd, x, y, windowW, windowH, false);
    }

    /// <summary>D1 : police et géométrie des contrôles au nouveau DPI (WM_DPICHANGED).</summary>
    private void ApplyDpiToControls()
    {
        IntPtr oldFont = _hFont;
        _hFont = CreateScaledFont();
        foreach (var (hwnd, x, y, w, h) in _layout)
        {
            Win32.SendMessageW(hwnd, Win32.WM_SETFONT, _hFont, IntPtr.Zero);
            Win32.MoveWindow(hwnd, S(x), S(y), S(w), S(h), true);
        }
        if (oldFont != IntPtr.Zero) Win32.DeleteObject(oldFont);
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
        // N8 (accessibilité 1.3.0) : chaque étiquette précède son champ dans l'ordre Z. MSAA
        // et UI Automation nomment un EDIT par le STATIC qui le précède ; l'ancien ordre, deux
        // étiquettes puis deux champs, nommait « Minutes » le champ des heures et laissait
        // celui des minutes sans nom (mesuré le 2026-09-23). L'ordre de tabulation, qui ne voit
        // que les champs et les boutons, est inchangé.
        _hLabel = CreateStatic(hInstance, L.Pause_Label, 18, 16, 280, 22);
        _hHours = CreateStatic(hInstance, L.Pause_Hours, 28, 60, 72, 22);

        _hEditHours = CreateEdit(hInstance, IDC_EDIT_HOURS, "0", 82, 54, 50, 26);
        _hMinutes = CreateStatic(hInstance, L.Pause_Minutes, 150, 60, 82, 22);
        _hEditMinutes = CreateEdit(hInstance, IDC_EDIT_MINUTES, "5", 218, 54, 50, 26);
        _hBtnHoursUp = CreateButton(hInstance, IDC_HOURS_UP, "▲", 82, 38, 50, 15, BS_PUSHBUTTON);
        _hBtnHoursDown = CreateButton(hInstance, IDC_HOURS_DOWN, "▼", 82, 81, 50, 15, BS_PUSHBUTTON);
        _hBtnMinutesUp = CreateButton(hInstance, IDC_MINUTES_UP, "▲", 218, 38, 50, 15, BS_PUSHBUTTON);
        _hBtnMinutesDown = CreateButton(hInstance, IDC_MINUTES_DOWN, "▼", 218, 81, 50, 15, BS_PUSHBUTTON);

        _hBtnOk = CreateButton(hInstance, IDOK, L.Pause_BtnConfirm, 96, 106, 120, 32, BS_DEFPUSHBUTTON);
        _hBtnCancel = CreateButton(hInstance, IDCANCEL, L.Pause_BtnCancel, 224, 106, 84, 32, BS_PUSHBUTTON);
        AnnotateSpinButtons();
    }

    /// <summary>
    /// N10 (accessibilité 1.3.0) : nom accessible de chaque bouton ▲▼ dans la langue
    /// courante. Le glyphe reste le texte affiché ; seul, il s'annonçait « ▲ » ou « ▼ ».
    /// </summary>
    internal static (int Id, string Glyph, string Name)[] SpinButtons() => new[]
    {
        (IDC_HOURS_UP, "▲", L.Pause_HoursUp),
        (IDC_HOURS_DOWN, "▼", L.Pause_HoursDown),
        (IDC_MINUTES_UP, "▲", L.Pause_MinutesUp),
        (IDC_MINUTES_DOWN, "▼", L.Pause_MinutesDown),
    };

    /// <summary>
    /// N10 : pose ces noms par Dynamic Annotation (<see cref="AccessibleName"/>) ; rappelé
    /// au changement de langue. Un échec laisse le glyphe pour nom, comme avant.
    /// </summary>
    private void AnnotateSpinButtons()
    {
        foreach (var (id, _, name) in SpinButtons())
        {
            IntPtr button = id switch
            {
                IDC_HOURS_UP => _hBtnHoursUp,
                IDC_HOURS_DOWN => _hBtnHoursDown,
                IDC_MINUTES_UP => _hBtnMinutesUp,
                IDC_MINUTES_DOWN => _hBtnMinutesDown,
                _ => IntPtr.Zero
            };
            AccessibleName.TrySet(button, name);
        }
    }

    private IntPtr CreateStatic(IntPtr hInstance, string text, int x, int y, int w, int h)
    {
        var hwnd = Win32.CreateWindowExW(0, "STATIC", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE,
            S(x), S(y), S(w), S(h), _hWnd, IntPtr.Zero, hInstance, IntPtr.Zero);
        _layout.Add((hwnd, x, y, w, h));
        Win32.SendMessageW(hwnd, Win32.WM_SETFONT, _hFont, (IntPtr)1);
        return hwnd;
    }

    private IntPtr CreateEdit(IntPtr hInstance, int id, string text, int x, int y, int w, int h)
    {
        var hwnd = Win32.CreateWindowExW(0, "EDIT", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_BORDER | Win32.WS_TABSTOP |
            ES_AUTOHSCROLL | ES_CENTER | ES_NUMBER,
            S(x), S(y), S(w), S(h), _hWnd, (IntPtr)id, hInstance, IntPtr.Zero);
        _layout.Add((hwnd, x, y, w, h));
        Win32.SendMessageW(hwnd, Win32.WM_SETFONT, _hFont, (IntPtr)1);
        return hwnd;
    }

    private IntPtr CreateButton(IntPtr hInstance, int id, string text, int x, int y, int w, int h, uint style)
    {
        var hwnd = Win32.CreateWindowExW(0, "BUTTON", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | style,
            S(x), S(y), S(w), S(h), _hWnd, (IntPtr)id, hInstance, IntPtr.Zero);
        _layout.Add((hwnd, x, y, w, h));
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
                case Win32.WM_DPICHANGED:
                {
                    // D1 : même traitement que LayoutConflictWindow — fenêtre au rectangle
                    // suggéré par Windows, police et contrôles au nouveau DPI.
                    int newDpi = (wParam.ToInt32() >> 16) & 0xFFFF;
                    if (newDpi > 0) _dpi = newDpi;
                    var suggested = Marshal.PtrToStructure<Win32.RECT>(lParam);
                    Win32.MoveWindow(_hWnd, suggested.left, suggested.top,
                        suggested.right - suggested.left, suggested.bottom - suggested.top, true);
                    ApplyDpiToControls();
                    return IntPtr.Zero;
                }
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
            // AG130-40 : desinscrire AVANT de detruire — Windows recycle les HWND.
            DialogNavigation.Unregister(_hWnd);
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
