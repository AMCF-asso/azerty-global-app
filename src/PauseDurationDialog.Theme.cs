// Habillage de la fenêtre « Durée de pause » — CH1 de la refonte graphique v1.2.0.
//
// L'audit du 2026-08-28 la désigne comme le contrôle le plus daté de l'application : boutons
// « ▲ »/« ▼ » système en guise de spinners, bandeau gris, et surtout des pixels bruts avec une
// police fixe — aucune mise à l'échelle, sur une application par ailleurs déclarée PerMonitorV2.
// À 150 %, elle s'affichait donc aux deux tiers de la taille attendue.
//
// Le fichier d'origine porte des fins de ligne mixtes (297 CRLF pour 40 LF, mesuré le
// 2026-08-28), et ses lignes LF sont éparpillées jusque dans le corps des méthodes à retoucher.
// D'où cette moitié partielle : tout ce qui est neuf vit ici, en LF pur, et le fichier d'origine
// ne reçoit que des remplacements d'une seule ligne, qui ne peuvent par construction pas changer
// une terminaison.

using System.Runtime.InteropServices;

namespace AZERTYGlobal;

sealed partial class PauseDurationDialog
{
    // ═══════════════════════════════════════════════════════════════
    // Échelle
    // ═══════════════════════════════════════════════════════════════

    private int _dpi = 96;

    /// <summary>Met une dimension de la charte à l'échelle de l'écran. Toutes les coordonnées de
    /// cette fenêtre y passent — c'est ce qui manquait entièrement.</summary>
    private int S(int value) => ThemeControls.Scale(value, _dpi);

    /// <summary>Échelle du bureau, lue avant que la fenêtre n'existe : elle sert à dimensionner
    /// la fenêtre elle-même.</summary>
    private void InitDpi()
    {
        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        _dpi = Win32.GetDeviceCaps(hdcScreen, 88);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        if (_dpi <= 0)
            _dpi = 96;
    }

    /// <summary>Échelle de l'écran où la fenêtre est réellement née, qui n'est pas celle du
    /// bureau dès qu'un poste a deux écrans d'échelles distinctes.</summary>
    private void AdoptWindowDpi() => _dpi = ThemeWindow.DpiOf(_hWnd);

    // ═══════════════════════════════════════════════════════════════
    // Contrôles
    // ═══════════════════════════════════════════════════════════════

    private IntPtr _hHoursUp;
    private IntPtr _hHoursDown;
    private IntPtr _hMinutesUp;
    private IntPtr _hMinutesDown;

    private Win32.SUBCLASSPROC? _buttonSubclassProc;
    private Action? _themeChanged;
    private IntPtr _hoveredButton;

    private IntPtr[] Buttons => new[] { _hHoursUp, _hHoursDown, _hMinutesUp, _hMinutesDown, _hBtnOk, _hBtnCancel };

    // Géométrie à 96 DPI. Deux rangées plutôt qu'une : les spinners passent à droite de leur
    // champ, comme tout compteur de Windows, au lieu de l'encadrer par le haut et par le bas.
    private const int Margin = 24;
    private const int LabelW = 76;
    private const int FieldX = 108;
    private const int FieldW = 64;
    private const int FieldH = 28;
    private const int SpinX = 176;
    private const int SpinW = 26;
    private const int Row1Y = 58;
    private const int Row2Y = 102;
    private const int ButtonW = 120;
    private const int CancelW = 100;
    private const int ButtonH = 32;
    private const int ButtonsY = 148;
    internal const int ClientW = 340;
    internal const int ClientH = 210;

    /// <summary>
    /// Pose tous les contrôles. Séparé de leur création pour que le changement d'échelle rejoue
    /// exactement la même mise en page — deux tables de coordonnées finiraient par diverger.
    /// </summary>
    private void LayoutControls()
    {
        int focus = ThemeControls.FocusMargin(_dpi);

        Move(_hLabel, Margin, 20, ClientW - 2 * Margin, 24);

        Move(_hHours, Margin, Row1Y + 4, LabelW, 24);
        Move(_hEditHours, FieldX, Row1Y, FieldW, FieldH);
        Move(_hHoursUp, SpinX, Row1Y, SpinW, FieldH / 2);
        Move(_hHoursDown, SpinX, Row1Y + FieldH / 2, SpinW, FieldH / 2);

        Move(_hMinutes, Margin, Row2Y + 4, LabelW, 24);
        Move(_hEditMinutes, FieldX, Row2Y, FieldW, FieldH);
        Move(_hMinutesUp, SpinX, Row2Y, SpinW, FieldH / 2);
        Move(_hMinutesDown, SpinX, Row2Y + FieldH / 2, SpinW, FieldH / 2);

        // Les deux boutons sont agrandis de la marge de focus de chaque côté : l'anneau se
        // dessine dans le DC du contrôle, donc tout ce qui déborde de son rectangle client est
        // écrêté. Le bouton visible, lui, garde sa taille et son alignement.
        MoveButton(_hBtnOk, Margin * 2 + 36, ButtonsY, ButtonW, ButtonH, focus);
        MoveButton(_hBtnCancel, ClientW - Margin - CancelW, ButtonsY, CancelW, ButtonH, focus);
    }

    private void Move(IntPtr hwnd, int x, int y, int w, int h)
    {
        if (hwnd != IntPtr.Zero)
            Win32.MoveWindow(hwnd, S(x), S(y), S(w), S(h), true);
    }

    private void MoveButton(IntPtr hwnd, int x, int y, int w, int h, int focus)
    {
        if (hwnd != IntPtr.Zero)
            Win32.MoveWindow(hwnd, S(x) - focus, S(y) - focus,
                S(w) + 2 * focus, S(h) + 2 * focus, true);
    }

    // ═══════════════════════════════════════════════════════════════
    // Thème
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Habille la fenêtre une fois ses contrôles créés : chrome, icône, fond de classe, mise en
    /// page à l'échelle, et le sous-classement qui donne aux six boutons l'état de survol que
    /// Windows ne rapporte pas pour un bouton owner-draw.
    /// </summary>
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

    /// <summary>Un abonnement laissé derrière garde en vie une fenêtre détruite et lui envoie des
    /// repeints sur un handle mort. La police et la brosse, elles, appartiennent aux caches de
    /// Theme et ne se détruisent pas ici.</summary>
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

    /// <summary>Réajuste la fenêtre à l'échelle réellement adoptée, la taille ayant été calculée
    /// avec celle du bureau avant que la fenêtre n'existe.</summary>
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

    // ═══════════════════════════════════════════════════════════════
    // Messages
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Ouvre la fenêtre sans sa boucle modale, pour le banc de captures uniquement. ShowModal
    /// pompe les messages jusqu'à la fermeture : un banc qui l'appellerait ne rendrait jamais la
    /// main. Rien d'autre ne diffère — c'est la même fenêtre, créée par le même chemin.
    /// </summary>
    internal IntPtr OpenForCapture()
    {
        CreateWindow(IntPtr.Zero);
        if (_hWnd == IntPtr.Zero)
            return IntPtr.Zero;

        Win32.ShowWindow(_hWnd, 1);
        return _hWnd;
    }

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

    /// <summary>
    /// Peint le fond et les cadres des deux champs. Le contrôle EDIT reste système et peint son
    /// propre intérieur par WM_CTLCOLOREDIT ; son style WS_BORDER a disparu, parce que la
    /// bordure gravée de Windows est précisément ce que la refonte supprime.
    /// </summary>
    private IntPtr OnPaint()
    {
        var hdc = Win32.BeginPaint(_hWnd, out var ps);
        Win32.GetClientRect(_hWnd, out var client);
        Win32.FillRect(hdc, ref client, Theme.Brush(Theme.Current.Paper));

        DrawFieldFrame(hdc, _hEditHours);
        DrawFieldFrame(hdc, _hEditMinutes);

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

        var client = new Win32.RECT
        {
            left = topLeft.x,
            top = topLeft.y,
            right = bottomRight.x,
            bottom = bottomRight.y,
        };

        var state = Win32.GetFocus() == hEdit ? ControlState.Focused : ControlState.None;
        ThemeControls.DrawFieldFrame(hdc, client, state, Theme.Current, _dpi);
    }

    /// <summary>Étiquettes : encre sur le fond de la fenêtre.</summary>
    private IntPtr OnCtlColorStatic(IntPtr hdcStatic)
    {
        Win32.SetBkMode(hdcStatic, Win32.TRANSPARENT);
        Win32.SetTextColor(hdcStatic, Theme.Current.Ink);
        return Theme.Brush(Theme.Current.Paper);
    }

    /// <summary>Champs de saisie : encre sur la surface. L'ascenseur et le menu contextuel
    /// restent système, comme le veut la charte.</summary>
    private IntPtr OnCtlColorEdit(IntPtr hdcEdit)
    {
        Win32.SetBkMode(hdcEdit, Win32.TRANSPARENT);
        Win32.SetTextColor(hdcEdit, Theme.Current.Ink);
        Win32.SetBkColor(hdcEdit, Theme.Current.Surface);
        return Theme.Brush(Theme.Current.Surface);
    }

    /// <summary>
    /// Rend les six boutons. Les quatre spinners remplacent les « ▲ »/« ▼ » système, et
    /// « Confirmer » est le seul bouton primaire de l'écran — c'est l'action que la fenêtre
    /// propose, quand « Annuler » n'est qu'un congé.
    /// </summary>
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

        var rect = dis.rcItem;

        if (dis.hwndItem == _hHoursUp || dis.hwndItem == _hMinutesUp)
        {
            ThemeControls.DrawSpinnerButton(dis.hDC, rect, state, Theme.Current, _dpi, pointingUp: true);
            return true;
        }

        if (dis.hwndItem == _hHoursDown || dis.hwndItem == _hMinutesDown)
        {
            ThemeControls.DrawSpinnerButton(dis.hDC, rect, state, Theme.Current, _dpi, pointingUp: false);
            return true;
        }

        if (dis.hwndItem != _hBtnOk && dis.hwndItem != _hBtnCancel)
            return false;

        // La marge de focus appartient au fond de la fenêtre : on l'efface avant de rentrer le
        // bouton visible, faute de quoi l'anneau se dessinerait sur un fond non peint.
        Win32.FillRect(dis.hDC, ref rect, Theme.Brush(Theme.Current.Paper));

        int focus = ThemeControls.FocusMargin(_dpi);
        var inner = new Win32.RECT
        {
            left = rect.left + focus,
            top = rect.top + focus,
            right = rect.right - focus,
            bottom = rect.bottom - focus,
        };

        bool primary = dis.hwndItem == _hBtnOk;
        ThemeControls.DrawButton(dis.hDC, inner,
            primary ? L.Pause_BtnConfirm : L.Pause_BtnCancel, _hFont,
            primary ? ButtonKind.Primary : ButtonKind.Secondary, state, Theme.Current, _dpi);
        return true;
    }

    /// <summary>Changement d'échelle : la fenêtre se réajuste et la mise en page se rejoue à
    /// l'identique. Rare sur une modale, mais c'est exactement ce qui manquait ici.</summary>
    private void OnDpiChanged(IntPtr wParam, IntPtr lParam)
    {
        _dpi = ThemeWindow.ApplyDpiChange(_hWnd, wParam, lParam);

        foreach (var hwnd in new[] { _hLabel, _hHours, _hMinutes, _hEditHours, _hEditMinutes })
        {
            if (hwnd != IntPtr.Zero)
                Win32.SendMessageW(hwnd, Win32.WM_SETFONT, _hFont, (IntPtr)1);
        }

        LayoutControls();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }
}
