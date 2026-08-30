using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Habillage de la fenêtre des couches maintenables — chantier CH2 de la refonte v1.2.0.
///
/// La moitié de cette fenêtre était en pixels bruts dans une application qui se déclare
/// PerMonitorV2 : à 150 % elle rendait aux deux tiers de sa taille, et rien ne rejouait sa mise
/// en page quand on la traînait d'un écran à l'autre. Tout passe désormais par l'échelle, et
/// <c>WM_DPICHANGED</c> rejoue la même mise en page plutôt qu'une seconde table qui finirait par
/// diverger de la première — le motif retenu à CH1 pour Durée de pause.
///
/// Les cases à cocher perdent <c>BS_AUTOCHECKBOX</c> : un contrôle owner-draw ne peut pas être
/// à la fois peint par nous et coché par Windows. Leur état vit donc ici, dans
/// <see cref="_checked"/>, et <c>BM_GETCHECK</c> n'est plus interrogé nulle part.
/// </summary>
internal sealed partial class MaintainableLayersWindow
{
    // ═══════════════════════════════════════════════════════════════
    // Géométrie — en pixels à 96 DPI, passés par S()
    // ═══════════════════════════════════════════════════════════════

    private const int Margin = 24;
    private const int Indent = 48;
    private const int ContentW = 472;
    private const int TitleY = 24;
    private const int ExplainerY = 64;
    private const int ExplainerH = 66;
    private const int MasterY = 140;
    private const int AvailableY = 184;
    private const int RowAY = 212;
    private const int RowBY = 244;
    private const int ColumnBX = 272;
    private const int VisualY = 288;
    private const int DelayY = 324;
    private const int DelayLabelW = 185;
    private const int DelayFieldX = 216;
    private const int DelayFieldW = 72;
    private const int DelayUnitX = 296;
    private const int FieldH = 28;
    private const int CheckH = 28;
    private const int SaveW = 140;
    private const int SaveH = 32;
    private const int SaveY = 364;
    internal const int ClientW = 520;
    internal const int ClientH = 420;

    /// <summary>Pour le banc de captures : la fenêtre est visible, il ne la pilote pas.</summary>
    internal IntPtr Handle => _hWnd;

    private int _dpi = 96;

    /// <summary>Les contrôles que la case maîtresse commande. Windows le sait par EnableWindow,
    /// mais un contrôle owner-draw a besoin de l'état pour choisir sa couleur, et les étiquettes
    /// STATIC ne le rapportent nulle part.</summary>
    private bool _secondaryEnabled = true;

    private int S(int value) => (int)(value * _dpi * ThemeControls.Density) / 96;

    // ═══════════════════════════════════════════════════════════════
    // État des contrôles owner-draw
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Coché ou non, par identifiant de case. Windows ne le tient plus pour nous.</summary>
    private readonly Dictionary<int, bool> _checked = new();

    private IntPtr _hSave;
    private IntPtr _hoveredButton;
    private Win32.SUBCLASSPROC? _buttonSubclassProc;
    private Action? _themeChanged;

    private IntPtr[] Checkboxes => new[] { _hMaster, _hGreek, _hCyrillic, _hScientific, _hVisual };

    private IntPtr[] Buttons => new[] { _hMaster, _hGreek, _hCyrillic, _hScientific, _hVisual, _hSave };

    // ═══════════════════════════════════════════════════════════════
    // Mise en page
    // ═══════════════════════════════════════════════════════════════

    private void AdoptWindowDpi()
    {
        _dpi = ThemeWindow.DpiOf(_hWnd);
    }

    private void LayoutControls()
    {
        int focus = ThemeControls.FocusMargin(_dpi);

        Move(_hTitle, Margin, TitleY, ContentW, 28);
        Move(_hExplainer, Margin, ExplainerY, ContentW, ExplainerH);

        MoveButton(_hMaster, Margin, MasterY, ContentW, CheckH, focus);
        Move(_hAvailable, Indent, AvailableY, 250, 24);
        MoveButton(_hGreek, Indent, RowAY, 210, 26, focus);
        MoveButton(_hCyrillic, ColumnBX, RowAY, 220, 26, focus);
        MoveButton(_hScientific, Indent, RowBY, 260, 26, focus);
        MoveButton(_hVisual, Margin, VisualY, 360, 26, focus);

        Move(_hDelayLabel, Margin, DelayY + 4, DelayLabelW, 26);
        Move(_hDelay, DelayFieldX, DelayY, DelayFieldW, FieldH);
        Move(_hDelayUnit, DelayUnitX, DelayY + 4, 145, 26);

        MoveButton(_hSave, ClientW - Margin - SaveW, SaveY, SaveW, SaveH, focus);
    }

    private void Move(IntPtr hwnd, int x, int y, int w, int h)
    {
        if (hwnd != IntPtr.Zero)
            Win32.MoveWindow(hwnd, S(x), S(y), S(w), S(h), true);
    }

    /// <summary>L'anneau de focus se dessine dans le DC du contrôle : tout ce qui déborde de son
    /// rectangle client est écrêté, donc le contrôle est agrandi de la marge de chaque côté.</summary>
    private void MoveButton(IntPtr hwnd, int x, int y, int w, int h, int focus)
    {
        if (hwnd != IntPtr.Zero)
            Win32.MoveWindow(hwnd, S(x) - focus, S(y) - focus,
                S(w) + 2 * focus, S(h) + 2 * focus, true);
    }

    private void ResizeToClient()
    {
        if (_hWnd == IntPtr.Zero || !Win32.GetWindowRect(_hWnd, out var current))
            return;

        uint style = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var rect = new Win32.RECT { left = 0, top = 0, right = S(ClientW), bottom = S(ClientH) };
        Win32.AdjustWindowRectEx(ref rect, style, false, 0);

        int w = rect.right - rect.left;
        int h = rect.bottom - rect.top;
        int cx = (current.left + current.right) / 2;
        int cy = (current.top + current.bottom) / 2;
        Win32.MoveWindow(_hWnd, cx - w / 2, cy - h / 2, w, h, true);
    }

    private void ApplyFontsToControls()
    {
        IntPtr body = Theme.Font(FontRole.Body, _dpi);
        IntPtr title = Theme.Font(FontRole.SectionTitle, _dpi);

        Win32.SendMessageW(_hTitle, Win32.WM_SETFONT, title, (IntPtr)1);
        foreach (var hwnd in new[] { _hExplainer, _hAvailable, _hDelayLabel, _hDelayUnit, _hDelay })
        {
            if (hwnd != IntPtr.Zero)
                Win32.SendMessageW(hwnd, Win32.WM_SETFONT, body, (IntPtr)1);
        }
        foreach (var hwnd in Buttons)
        {
            if (hwnd != IntPtr.Zero)
                Win32.SendMessageW(hwnd, Win32.WM_SETFONT, body, (IntPtr)1);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Thème
    // ═══════════════════════════════════════════════════════════════

    private void ApplyThemeToWindow()
    {
        ThemeWindow.ApplyChrome(_hWnd);
        ThemeWindow.ApplyProductIcon(_hWnd);
        ThemeWindow.ApplyClassBackground(_hWnd, Theme.Current.Paper);

        _buttonSubclassProc = ButtonSubclassProc;
        for (int i = 0; i < Buttons.Length; i++)
        {
            if (Buttons[i] != IntPtr.Zero)
                Win32.SetWindowSubclass(Buttons[i], _buttonSubclassProc, (UIntPtr)(uint)(i + 1), IntPtr.Zero);
        }

        ApplyFontsToControls();
        ResizeToClient();
        LayoutControls();

        _themeChanged = OnThemeChanged;
        Theme.Changed += _themeChanged;
    }

    private void OnThemeChanged()
    {
        if (_hWnd == IntPtr.Zero)
            return;

        ThemeWindow.ApplyClassBackground(_hWnd, Theme.Current.Paper);
        ThemeWindow.ApplyChrome(_hWnd);
    }

    private void DisposeTheme()
    {
        if (_themeChanged != null)
        {
            Theme.Changed -= _themeChanged;
            _themeChanged = null;
        }

        if (_buttonSubclassProc == null)
            return;

        for (int i = 0; i < Buttons.Length; i++)
        {
            if (Buttons[i] != IntPtr.Zero)
                Win32.RemoveWindowSubclass(Buttons[i], _buttonSubclassProc, (UIntPtr)(uint)(i + 1));
        }
        _buttonSubclassProc = null;
    }

    /// <summary>Windows ne rapporte pas le survol d'un bouton owner-draw : il faut le suivre.</summary>
    private IntPtr ButtonSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        switch (msg)
        {
            case Win32.WM_MOUSEMOVE:
                if (_hoveredButton != hWnd)
                {
                    _hoveredButton = hWnd;
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);
                    var tme = new Win32.TRACKMOUSEEVENT
                    {
                        cbSize = (uint)Marshal.SizeOf<Win32.TRACKMOUSEEVENT>(),
                        dwFlags = Win32.TME_LEAVE,
                        hwndTrack = hWnd
                    };
                    Win32.TrackMouseEvent(ref tme);
                }
                break;

            case Win32.WM_MOUSELEAVE:
                if (_hoveredButton == hWnd)
                {
                    _hoveredButton = IntPtr.Zero;
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);
                }
                break;
        }

        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    // ═══════════════════════════════════════════════════════════════
    // Peinture
    // ═══════════════════════════════════════════════════════════════

    private IntPtr OnPaint()
    {
        var hdc = Win32.BeginPaint(_hWnd, out var ps);
        Win32.GetClientRect(_hWnd, out var client);
        Win32.FillRect(hdc, ref client, Theme.Brush(Theme.Current.Paper));
        DrawFieldFrame(hdc, _hDelay);
        Win32.EndPaint(_hWnd, ref ps);
        return IntPtr.Zero;
    }

    private void DrawFieldFrame(IntPtr hdc, IntPtr hEdit)
    {
        if (hEdit == IntPtr.Zero || !Win32.GetWindowRect(hEdit, out var rect))
            return;

        var topLeft = new Win32.POINT { x = rect.left, y = rect.top };
        var bottomRight = new Win32.POINT { x = rect.right, y = rect.bottom };
        Win32.ScreenToClient(_hWnd, ref topLeft);
        Win32.ScreenToClient(_hWnd, ref bottomRight);

        var box = new Win32.RECT
        {
            left = topLeft.x,
            top = topLeft.y,
            right = bottomRight.x,
            bottom = bottomRight.y,
        };

        var state = !_secondaryEnabled ? ControlState.Disabled
            : Win32.GetFocus() == hEdit ? ControlState.Focused
            : ControlState.None;
        ThemeControls.DrawFieldFrame(hdc, box, state, Theme.Current, _dpi);
    }

    /// <summary>Le titre porte l'encre, le reste le texte secondaire — sauf un contrôle inactif,
    /// que la charte range sur son propre jeton.</summary>
    private IntPtr OnCtlColorStatic(IntPtr hdc, IntPtr control)
    {
        Win32.SetBkMode(hdc, Win32.TRANSPARENT);
        bool greyed = !_secondaryEnabled && (control == _hDelayLabel || control == _hDelayUnit
            || control == _hAvailable);
        uint color = greyed ? Theme.Current.Disabled : Theme.Current.Ink;
        Win32.SetTextColor(hdc, color);
        return Theme.Brush(Theme.Current.Paper);
    }

    private IntPtr OnCtlColorEdit(IntPtr hdc)
    {
        Win32.SetBkMode(hdc, Win32.TRANSPARENT);
        Win32.SetTextColor(hdc, Theme.Current.Ink);
        Win32.SetBkColor(hdc, Theme.Current.Surface);
        return Theme.Brush(Theme.Current.Surface);
    }

    private bool TryDrawItem(IntPtr lParam)
    {
        var dis = Marshal.PtrToStructure<Win32.DRAWITEMSTRUCT>(lParam);
        if (dis.hwndItem == IntPtr.Zero)
            return false;

        var state = ControlState.None;
        if ((dis.itemState & Win32.ODS_DISABLED) != 0) state |= ControlState.Disabled;
        if ((dis.itemState & Win32.ODS_SELECTED) != 0) state |= ControlState.Pressed;
        if ((dis.itemState & Win32.ODS_FOCUS) != 0) state |= ControlState.Focused;
        if (_hoveredButton == dis.hwndItem) state |= ControlState.Hovered;

        // L'état inactif vient de l'application plutôt que d'ODS_DISABLED : les cases que la
        // case maîtresse commande sont bien passées par EnableWindow, mais le drapeau n'est pas
        // arrivé jusqu'ici, et une case cochée continuait de se peindre à l'accent une fois
        // grisée. Ce que l'application sait, elle le dit.
        if (!_secondaryEnabled && dis.hwndItem != _hMaster && dis.hwndItem != _hSave)
            state |= ControlState.Disabled;

        int focus = ThemeControls.FocusMargin(_dpi);
        var rect = new Win32.RECT
        {
            left = dis.rcItem.left + focus,
            top = dis.rcItem.top + focus,
            right = dis.rcItem.right - focus,
            bottom = dis.rcItem.bottom - focus,
        };

        // La marge de focus appartient au fond de la fenêtre : l'effacer avant de rendre le
        // contrôle, faute de quoi l'anneau se dessinerait sur un fond non peint.
        var full = dis.rcItem;
        Win32.FillRect(dis.hDC, ref full, Theme.Brush(Theme.Current.Paper));

        if (dis.hwndItem == _hSave)
        {
            ThemeControls.DrawButton(dis.hDC, rect, L.Layers_SaveButton,
                Theme.Font(FontRole.Body, _dpi), ButtonKind.Primary, state, Theme.Current, _dpi);
            return true;
        }

        int id = CheckboxId(dis.hwndItem);
        if (id == 0)
            return false;

        if (_checked.TryGetValue(id, out bool ticked) && ticked)
            state |= ControlState.Checked;

        ThemeControls.DrawCheckBox(dis.hDC, rect, CheckboxLabel(id),
            Theme.Font(FontRole.Body, _dpi), state, Theme.Current, _dpi);
        return true;
    }

    private int CheckboxId(IntPtr hwnd)
    {
        if (hwnd == _hMaster) return IDC_MASTER;
        if (hwnd == _hGreek) return IDC_GREEK;
        if (hwnd == _hCyrillic) return IDC_CYRILLIC;
        if (hwnd == _hScientific) return IDC_SCIENTIFIC;
        if (hwnd == _hVisual) return IDC_VISUAL;
        return 0;
    }

    private static string CheckboxLabel(int id) => id switch
    {
        IDC_MASTER => L.Layers_MasterCheckbox,
        IDC_GREEK => L.Layers_GreekCheckbox,
        IDC_CYRILLIC => L.Layers_CyrillicCheckbox,
        IDC_SCIENTIFIC => L.Layers_ScientificCheckbox,
        _ => L.Layers_VisualCheckbox,
    };

    private void OnDpiChanged(IntPtr wParam, IntPtr lParam)
    {
        _dpi = ThemeWindow.ApplyDpiChange(_hWnd, wParam, lParam);
        ApplyFontsToControls();
        LayoutControls();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }
}
