using System.Text;

namespace AZERTYGlobal;

/// <summary>
/// « Personnaliser… » du sous-menu Pause : une durée en heures et minutes. Refaite le
/// 2026-09-28 sur décision d'Antoine : les durées courantes sont dans le menu de l'icône,
/// et cette fenêtre n'a plus de boutons ▲▼ au-dessus et au-dessous des champs, mais les
/// flèches Windows (Up-down) collées à droite de chacun. La ligne « Reprise à … » dit
/// l'heure de fin à mesure de la saisie, et le bouton reste grisé tant que la durée est
/// hors bornes : plus de boîte d'erreur après coup.
/// </summary>
sealed class PauseDurationDialog : IDisposable
{
    private static readonly string ClassName = ProductIdentity.WindowClass("PauseDuration");
    private const int IDOK = 1;
    private const int IDCANCEL = 2;
    private const int IDC_EDIT_HOURS = 4301;
    private const int IDC_EDIT_MINUTES = 4302;

    internal const int MaxHours = 23;
    internal const int MaxMinutes = 59;
    internal const int MinutesStep = 5;
    private const int DefaultMinutes = 5;

    // Zone client de référence, à 96 DPI.
    private const int BASE_CLIENT_W = 330;
    private const int BASE_CLIENT_H = 166;
    private const int SPIN_W = 18;
    private const uint Style = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;

    private readonly Win32.WNDPROC _wndProcDelegate;
    private IntPtr _hWnd;
    private IntPtr _hLabel;
    private IntPtr _hEditHours;
    private IntPtr _hSpinHours;
    private IntPtr _hHours;
    private IntPtr _hEditMinutes;
    private IntPtr _hSpinMinutes;
    private IntPtr _hMinutes;
    private IntPtr _hResume;
    private IntPtr _hBtnOk;
    private IntPtr _hBtnCancel;
    private IntPtr _hFont;
    // Fond de la classe : Windows le détruit au désenregistrement (NativeWindow).
    private IntPtr _hBgBrush;
    // D1 (accessibilité 1.3.0) : DPI de l'écran de la fenêtre, et géométrie de référence à
    // 96 DPI de chaque contrôle, remise à l'échelle sur WM_DPICHANGED.
    private int _dpi = 96;
    private readonly ControlLayout _layout = new(redrawOnFont: false);
    private Action<string>? _onAppLanguageChanged;
    private bool _durationValid = true;
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
        // Audit du 25/09, X-01 : une classe refusée ne crée pas de fenêtre.
        if (!NativeWindow.RegisterClass(ClassName, _wndProcDelegate))
            return;

        var hInstance = Win32.GetModuleHandleW(null);
        var work = NativeWindow.WorkArea(owner);
        var (windowW, windowH) = NativeWindow.OuterSize(BASE_CLIENT_W, BASE_CLIENT_H, Style);
        var (x, y) = NativeWindow.CenterIn(work, windowW, windowH);

        _hWnd = Win32.CreateWindowExW(0, ClassName, L.Pause_WindowTitle,
            Style, x, y, windowW, windowH, owner, IntPtr.Zero, hInstance, IntPtr.Zero);
        // La classe n'avait aucun fond : rien n'effaçait la fenêtre, que DWM montrait en
        // noir sous des étiquettes grises (vu par Antoine le 26/09). Même fond que les
        // autres fenêtres à contrôles.
        _hBgBrush = NativeWindow.ApplyClassBackground(_hWnd, LightTheme.Background);

        // D1 (accessibilité 1.3.0) : la fenêtre existe, son DPI est celui de l'écran qui
        // l'accueille. Taille, positions et police en découlent. Invisible jusqu'à
        // ShowModal, le redimensionnement ne se voit pas.
        _dpi = NativeWindow.DpiOf(_hWnd);
        NativeWindow.FitToDpi(_hWnd, BASE_CLIENT_W, BASE_CLIENT_H, _dpi, Style, 0, work);

        // AG130-40 : cette fenetre veut Tab, Maj+Tab et Entree entre ses controles.
        DialogNavigation.Register(_hWnd);

        _hFont = CreateScaledFont();
        CreateControls(hInstance);
        NativeWindow.ApplyFrame(_hWnd, dark: false);

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
        Win32.SetWindowTextW(_hHours, L.Pause_UnitHours);
        Win32.SetWindowTextW(_hMinutes, L.Pause_UnitMinutes);
        Win32.SetWindowTextW(_hBtnOk, L.Pause_BtnConfirm);
        Win32.SetWindowTextW(_hBtnCancel, L.Pause_BtnCancel);
        AnnotateFields();
        UpdateResumeLine();
    }

    private int S(int value) => WindowSizing.ScaleForDpi(value, _dpi);

    private IntPtr CreateScaledFont() =>
        Win32.CreateFontW(S(-TypeRamp.Body), 0, 0, 0, TypeRamp.Regular, 0, 0, 0, 0, 0, 0, 5, 0, TypeRamp.Family);

    /// <summary>D1 : police et géométrie des contrôles au nouveau DPI (WM_DPICHANGED).</summary>
    private void ApplyDpiToControls()
    {
        IntPtr oldFont = _hFont;
        _hFont = CreateScaledFont();
        _layout.Apply(_dpi, _hFont);
        // Les champs reprennent leur largeur pleine : chaque Up-down, remis à sa largeur au
        // nouveau DPI, se recolle à droite et raccourcit son champ d'autant.
        AttachSpin(_hSpinHours, _hEditHours);
        AttachSpin(_hSpinMinutes, _hEditMinutes);
        if (oldFont != IntPtr.Zero) Win32.DeleteObject(oldFont);
    }

    private void CreateControls(IntPtr hInstance)
    {
        var icc = new Win32.INITCOMMONCONTROLSEX
        {
            dwSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.INITCOMMONCONTROLSEX>(),
            dwICC = Win32.ICC_UPDOWN_CLASS
        };
        Win32.InitCommonControlsEx(ref icc);

        // L'ordre de création fait l'ordre Z : le Up-down suit son champ, et l'unité « h »
        // ou « min » le suit à son tour. Les champs portent leur propre nom accessible
        // (AnnotateFields) : sans lui, le Narrateur nommerait le champ des minutes « h ».
        _hLabel = CreateStatic(hInstance, L.Pause_Label, 18, 14, 294, 22);
        _hEditHours = CreateEdit(hInstance, IDC_EDIT_HOURS, "0", 18, 40, 64, 28);
        _hSpinHours = CreateSpin(hInstance, _hEditHours, MaxHours, 1);
        _hHours = CreateStatic(hInstance, L.Pause_UnitHours, 88, 45, 32, 22);
        _hEditMinutes = CreateEdit(hInstance, IDC_EDIT_MINUTES, DefaultMinutes.ToString(), 126, 40, 64, 28);
        _hSpinMinutes = CreateSpin(hInstance, _hEditMinutes, MaxMinutes, MinutesStep);
        _hMinutes = CreateStatic(hInstance, L.Pause_UnitMinutes, 196, 45, 60, 22);
        _hResume = CreateStatic(hInstance, string.Empty, 18, 80, 294, 22);

        _hBtnOk = CreateButton(hInstance, IDOK, L.Pause_BtnConfirm, 90, 118, 124, 32, Win32.BS_DEFPUSHBUTTON);
        _hBtnCancel = CreateButton(hInstance, IDCANCEL, L.Pause_BtnCancel, 222, 118, 90, 32, Win32.BS_PUSHBUTTON);
        AnnotateFields();
        UpdateResumeLine();
    }

    private void AnnotateFields()
    {
        AccessibleName.TrySet(_hEditHours, L.Pause_Hours);
        AccessibleName.TrySet(_hEditMinutes, L.Pause_Minutes);
        // Chaque Up-down s'annonçait « spinner » sans nom, et ses boutons « Plus » et
        // « Moins » : ni le champ ni le sens.
        AnnotateSpin(_hSpinHours, L.Pause_Hours, L.Pause_HoursUp, L.Pause_HoursDown);
        AnnotateSpin(_hSpinMinutes, L.Pause_Minutes, L.Pause_MinutesUp, L.Pause_MinutesDown);
    }

    private static void AnnotateSpin(IntPtr spin, string field, string up, string down)
    {
        AccessibleName.TrySet(spin, field);
        AccessibleName.TrySet(spin, up, AccessibleName.UpDownIncrease);
        AccessibleName.TrySet(spin, down, AccessibleName.UpDownDecrease);
    }

    private IntPtr CreateStatic(IntPtr hInstance, string text, int x, int y, int w, int h)
    {
        var hwnd = Win32.CreateWindowExW(0, "STATIC", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE,
            S(x), S(y), S(w), S(h), _hWnd, IntPtr.Zero, hInstance, IntPtr.Zero);
        _layout.Track(hwnd, x, y, w, h);
        Win32.SendMessageW(hwnd, Win32.WM_SETFONT, _hFont, (IntPtr)1);
        return hwnd;
    }

    private IntPtr CreateEdit(IntPtr hInstance, int id, string text, int x, int y, int w, int h)
    {
        var hwnd = Win32.CreateWindowExW(0, "EDIT", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_BORDER | Win32.WS_TABSTOP |
            Win32.ES_AUTOHSCROLL | Win32.ES_CENTER | Win32.ES_NUMBER,
            S(x), S(y), S(w), S(h), _hWnd, (IntPtr)id, hInstance, IntPtr.Zero);
        _layout.Track(hwnd, x, y, w, h);
        Win32.SendMessageW(hwnd, Win32.WM_SETFONT, _hFont, (IntPtr)1);
        return hwnd;
    }

    /// <summary>Flèches Windows d'un champ : bornes 0..<paramref name="max"/>, pas de
    /// <paramref name="step"/>, flèches du clavier, texte du champ tenu à jour par le
    /// contrôle lui-même (UDS_SETBUDDYINT).</summary>
    private IntPtr CreateSpin(IntPtr hInstance, IntPtr buddy, int max, uint step)
    {
        var hwnd = Win32.CreateWindowExW(0, Win32.UPDOWN_CLASS, string.Empty,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.UDS_SETBUDDYINT | Win32.UDS_ALIGNRIGHT |
            Win32.UDS_ARROWKEYS | Win32.UDS_NOTHOUSANDS | Win32.UDS_HOTTRACK,
            0, 0, S(SPIN_W), 0, _hWnd, IntPtr.Zero, hInstance, IntPtr.Zero);
        if (hwnd == IntPtr.Zero)
            return hwnd;

        Win32.SendMessageW(hwnd, Win32.UDM_SETRANGE32, IntPtr.Zero, (IntPtr)max);
        Win32.SendMessageW(hwnd, Win32.UDM_SETBUDDY, buddy, IntPtr.Zero);
        Win32.SendMessageW(hwnd, Win32.UDM_SETPOS32, IntPtr.Zero, (IntPtr)ReadInt(buddy));

        var accel = new Win32.UDACCEL { nSec = 0, nInc = step };
        IntPtr pAccel = System.Runtime.InteropServices.Marshal.AllocHGlobal(
            System.Runtime.InteropServices.Marshal.SizeOf<Win32.UDACCEL>());
        try
        {
            System.Runtime.InteropServices.Marshal.StructureToPtr(accel, pAccel, false);
            Win32.SendMessageW(hwnd, Win32.UDM_SETACCEL, (IntPtr)1, pAccel);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pAccel); }
        return hwnd;
    }

    private void AttachSpin(IntPtr spin, IntPtr buddy)
    {
        if (spin == IntPtr.Zero) return;
        Win32.MoveWindow(spin, 0, 0, S(SPIN_W), 0, false);
        Win32.SendMessageW(spin, Win32.UDM_SETBUDDY, buddy, IntPtr.Zero);
    }

    private IntPtr CreateButton(IntPtr hInstance, int id, string text, int x, int y, int w, int h, uint style)
    {
        var hwnd = Win32.CreateWindowExW(0, "BUTTON", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | style,
            S(x), S(y), S(w), S(h), _hWnd, (IntPtr)id, hInstance, IntPtr.Zero);
        _layout.Track(hwnd, x, y, w, h);
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
                    int code = (wParam.ToInt32() >> 16) & 0xFFFF;
                    if (id == IDOK)
                    {
                        ValidateAndClose();
                        return IntPtr.Zero;
                    }
                    if (id == IDCANCEL)
                    {
                        Close(null);
                        return IntPtr.Zero;
                    }
                    if ((id == IDC_EDIT_HOURS || id == IDC_EDIT_MINUTES) && code == Win32.EN_CHANGE)
                    {
                        UpdateResumeLine();
                        return IntPtr.Zero;
                    }
                    break;
                }
                case Win32.WM_MOUSEWHEEL:
                {
                    // La molette remonte du champ qui a le focus jusqu'ici (DefWindowProc
                    // la transmet au parent) : elle fait ce que font ses flèches.
                    IntPtr focus = Win32.GetFocus();
                    int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                    if (focus == _hEditHours)
                        Step(_hEditHours, delta > 0 ? 1 : -1, MaxHours);
                    else if (focus == _hEditMinutes)
                        Step(_hEditMinutes, delta > 0 ? MinutesStep : -MinutesStep, MaxMinutes);
                    else
                        break;
                    return IntPtr.Zero;
                }
                case Win32.WM_CTLCOLORSTATIC:
                    // Les étiquettes posent leur texte sur le fond de la fenêtre ; la ligne
                    // de reprise passe au rouge quand la durée est hors bornes.
                    Win32.SetBkMode(wParam, 1);
                    if (lParam == _hResume)
                        Win32.SetTextColor(wParam, ResumeLineColor(_durationValid));
                    return _hBgBrush;
                case Win32.WM_KEYDOWN:
                    if (wParam == (IntPtr)0x1B)
                    {
                        Close(null);
                        return IntPtr.Zero;
                    }
                    break;
                case Win32.WM_DPICHANGED:
                    // D1 : fenêtre au rectangle suggéré, police et contrôles au nouveau DPI.
                    _dpi = NativeWindow.ApplyDpiChange(_hWnd, wParam, lParam);
                    ApplyDpiToControls();
                    return IntPtr.Zero;
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

    /// <summary>Durée saisie, ou null hors de 1 min à 23 h 59. Les minutes peuvent dépasser
    /// 59 à la frappe : « 0 h 75 » vaut 1 h 15.</summary>
    internal static TimeSpan? ParseDuration(int hours, int minutes)
    {
        int total = hours * 60 + minutes;
        return hours < 0 || minutes < 0 || total < 1 || total > MaxHours * 60 + MaxMinutes
            ? null
            : TimeSpan.FromMinutes(total);
    }

    // Même rouge que les refus des Paramètres.
    internal static uint ResumeLineColor(bool valid) => valid ? LightTheme.Text : LightTheme.Error;

    // Heure de la ligne de reprise. Le banc de captures la fige : l'heure réelle rendait
    // la fenêtre différente d'une passe à l'autre (16 captures instables, run 36427331772).
    internal static Func<DateTime> Clock = () => DateTime.Now;

    private TimeSpan? CurrentDuration() => ParseDuration(ReadInt(_hEditHours), ReadInt(_hEditMinutes));

    private void UpdateResumeLine()
    {
        if (_hResume == IntPtr.Zero || _hBtnOk == IntPtr.Zero)
            return; // les champs se remplissent avant que la ligne et le bouton existent
        TimeSpan? duration = CurrentDuration();
        _durationValid = duration.HasValue;
        DateTime now = Clock();
        Win32.SetWindowTextW(_hResume, duration is { } d
            ? L.Pause_Resumes(PauseSchedule.DescribeResume(now, now + d))
            : L.Pause_InvalidDuration);
        Win32.InvalidateRect(_hResume, IntPtr.Zero, true);
        Win32.EnableWindow(_hBtnOk, _durationValid);
    }

    private void ValidateAndClose()
    {
        // Entrée arrive jusqu'ici même quand le bouton est grisé.
        if (CurrentDuration() is { } duration)
            Close(duration);
    }

    private static void Step(IntPtr edit, int delta, int max)
    {
        int value = Math.Clamp(ReadInt(edit) + delta, 0, max);
        Win32.SetWindowTextW(edit, value.ToString());
    }

    private static int ReadInt(IntPtr hwnd)
    {
        var sb = new StringBuilder(16);
        Win32.GetWindowTextW(hwnd, sb, sb.Capacity);
        return int.TryParse(sb.ToString(), out int value) ? value : 0;
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
        NativeWindow.UnregisterClass(ClassName);
    }
}
