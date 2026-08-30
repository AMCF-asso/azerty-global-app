using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Contrôles de la fenêtre Paramètres peints à la charte — CH3 passe 2 de la refonte v1.2.0.
///
/// La passe 1 avait mis la fenêtre à la charte pour la couleur, mais ses neuf cases et radios,
/// ses quatre boutons et sa liste restaient peints par Windows : <c>#F0F0F0</c> sur 11,1 % de la
/// surface, dans les deux thèmes, mesuré le 2026-08-30 sur la matrice complète. Au thème sombre
/// cela donnait des boutons blancs, une liste blanche et des bandes claires derrière les cases
/// décochées, au milieu d'un fond <c>#241F17</c>.
///
/// Passer un contrôle en owner-draw lui retire ce que Windows faisait pour lui. Trois
/// conséquences, toutes visibles dans ce fichier :
///
///   1. <c>BS_AUTOCHECKBOX</c> et <c>BS_AUTORADIOBUTTON</c> disparaissent — un contrôle ne peut
///      pas être à la fois peint par nous et coché par Windows. L'état vit dans
///      <see cref="_checkedState"/>, et <see cref="SetCheck"/> / <see cref="GetCheck"/>
///      remplacent <c>BM_SETCHECK</c> et <c>BM_GETCHECK</c> partout dans la fenêtre.
///   2. Un clic ne bascule plus rien tout seul : la bascule se fait dans <c>WM_COMMAND</c>. Les
///      trois cases qui n'avaient aucun cas — leur valeur n'était lue qu'à la fermeture — en
///      gagnent un.
///   3. Le survol n'est pas rapporté dans <c>DRAWITEMSTRUCT.itemState</c> : il se piste par
///      sous-classement et <c>TrackMouseEvent</c>, le motif posé par Couches maintenables à CH2.
///
/// L'exclusion mutuelle des radios n'a rien coûté : les deux groupes étaient déjà pilotés
/// explicitement par <c>RefreshLanguageRadios</c> et <c>RefreshCompatSelectionUi</c>, qui
/// posaient les trois états à chaque changement au lieu de laisser Windows le faire.
/// </summary>
sealed partial class SettingsWindow
{
    // ═══════════════════════════════════════════════════════════════
    // Déclarations Win32 propres à cette fenêtre
    // ═══════════════════════════════════════════════════════════════

    private const uint LBS_OWNERDRAWFIXED = 0x0010;
    private const uint LBS_HASSTRINGS = 0x0040;
    private const uint LB_GETTEXT = 0x0189;
    private const uint LB_SETITEMHEIGHT = 0x01A0;

    // ═══════════════════════════════════════════════════════════════
    // L'état que Windows ne tient plus
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Case cochée, radio sélectionnée. Indexé par handle plutôt que par identifiant de
    /// contrôle : c'est le handle que <c>DRAWITEMSTRUCT</c> apporte, et le seul des deux qui
    /// n'oblige pas à une table de correspondance de plus.
    /// </summary>
    private readonly Dictionary<IntPtr, bool> _checkedState = new();

    /// <summary>Contrôle sous le curseur, ou <c>IntPtr.Zero</c>.</summary>
    private IntPtr _hoveredControl;

    private Win32.SUBCLASSPROC? _hoverSubclassProc;

    /// <summary>
    /// Les seize contrôles que cette fenêtre peint elle-même, dans un ordre stable : l'indice
    /// sert d'identifiant de sous-classe, et le détachement doit retrouver le même.
    /// </summary>
    private IntPtr[] OwnerDrawControls => new[]
    {
        _hWndChkAutoStart, _hWndChkNotifications, _hWndChkOnboarding, _hWndChkTraining,
        _hWndRadioLangFr, _hWndRadioLangEn,
        _hWndResetVirtualKeyboardWindow, _hWndResetLessonsWindow,
        _hWndCompatAdd, _hWndCompatRemove,
        _hWndRadioCompatAuto, _hWndRadioCompatForceOn, _hWndRadioCompatForceOff,
        _hWndTabs[0], _hWndTabs[1], _hWndTabs[2],
    };

    /// <summary>
    /// Pour le banc de captures : la fenêtre montre l'onglet demandé. Le banc ne clique pas,
    /// et sans ce point d'entrée son contrôle visuel ne verrait jamais que le premier onglet —
    /// soit un tiers de la fenêtre depuis le 2026-08-30.
    /// </summary>
    internal void ShowTabForCapture(int tab) => SetActiveTab(tab);

    /// <summary>Nom de fichier de l'onglet, pour le banc.</summary>
    internal static string TabSlug(int tab) => tab switch
    {
        TabShortcuts => "raccourcis",
        TabCompat => "apps",
        _ => "preferences",
    };

    /// <summary>Nombre d'onglets, pour le banc.</summary>
    internal static int CaptureTabCount => TabCount;

    /// <summary>Rang de l'onglet que porte ce handle, ou -1 si ce n'en est pas un.</summary>
    private int TabIndexOf(IntPtr control)
    {
        for (int i = 0; i < TabCount; i++)
        {
            if (control != IntPtr.Zero && control == _hWndTabs[i])
                return i;
        }
        return -1;
    }

    private bool GetCheck(IntPtr control) =>
        control != IntPtr.Zero && _checkedState.TryGetValue(control, out bool value) && value;

    private void SetCheck(IntPtr control, bool value)
    {
        if (control == IntPtr.Zero)
            return;
        if (_checkedState.TryGetValue(control, out bool current) && current == value)
            return;

        _checkedState[control] = value;
        Win32.InvalidateRect(control, IntPtr.Zero, true);
    }

    private void ToggleCheck(IntPtr control) => SetCheck(control, !GetCheck(control));

    /// <summary>
    /// Rafraîchit tous les contrôles que la fenêtre peint elle-même.
    ///
    /// <c>InvalidateRect</c> sur la fenêtre n'atteint pas ses enfants — et depuis que
    /// <c>WS_CLIPCHILDREN</c> est posé, la peinture du parent ne passe même plus dessus. Deux
    /// changements les concernent tous et ne viennent d'aucun d'eux : la bascule d'onglet, qui
    /// déplace le trait d'accent, et la bascule de thème de Windows, qui change chaque jeton.
    /// Sans ce rafraîchissement, l'onglet actif restait souligné à son ancienne place et les
    /// contrôles gardaient les couleurs du thème précédent.
    /// </summary>
    private void InvalidateOwnerDrawControls()
    {
        foreach (var control in OwnerDrawControls)
        {
            if (control != IntPtr.Zero)
                Win32.InvalidateRect(control, IntPtr.Zero, true);
        }

        if (_hWndCompatList != IntPtr.Zero)
            Win32.InvalidateRect(_hWndCompatList, IntPtr.Zero, true);
    }

    // ═══════════════════════════════════════════════════════════════
    // Survol
    // ═══════════════════════════════════════════════════════════════

    private void AttachHoverTracking()
    {
        _hoverSubclassProc = HoverSubclassProc;
        var controls = OwnerDrawControls;
        for (int i = 0; i < controls.Length; i++)
        {
            if (controls[i] != IntPtr.Zero)
                Win32.SetWindowSubclass(controls[i], _hoverSubclassProc, (UIntPtr)(uint)(i + 1), IntPtr.Zero);
        }
    }

    private void DetachHoverTracking()
    {
        if (_hoverSubclassProc == null)
            return;

        var controls = OwnerDrawControls;
        for (int i = 0; i < controls.Length; i++)
        {
            if (controls[i] != IntPtr.Zero)
                Win32.RemoveWindowSubclass(controls[i], _hoverSubclassProc, (UIntPtr)(uint)(i + 1));
        }
        _hoverSubclassProc = null;
    }

    /// <summary>Windows ne rapporte pas le survol d'un contrôle owner-draw : il faut le suivre.
    /// <c>TrackMouseEvent</c> est réarmé à chaque entrée, il ne vaut que pour une sortie.</summary>
    private IntPtr HoverSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        switch (msg)
        {
            case Win32.WM_MOUSEMOVE:
                if (_hoveredControl != hWnd)
                {
                    IntPtr previous = _hoveredControl;
                    _hoveredControl = hWnd;
                    if (previous != IntPtr.Zero)
                        Win32.InvalidateRect(previous, IntPtr.Zero, true);
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
                if (_hoveredControl == hWnd)
                {
                    _hoveredControl = IntPtr.Zero;
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);
                }
                break;
        }

        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    // ═══════════════════════════════════════════════════════════════
    // Peinture des contrôles
    // ═══════════════════════════════════════════════════════════════

    private bool IsCheckBox(IntPtr control) =>
        control == _hWndChkAutoStart || control == _hWndChkNotifications
        || control == _hWndChkOnboarding || control == _hWndChkTraining;

    private bool IsRadio(IntPtr control) =>
        control == _hWndRadioLangFr || control == _hWndRadioLangEn
        || control == _hWndRadioCompatAuto || control == _hWndRadioCompatForceOn
        || control == _hWndRadioCompatForceOff;

    /// <summary>
    /// Libellé d'un contrôle owner-draw. Lu dans <c>L</c> à chaque peinture plutôt que retenu :
    /// la langue change en cours de vie de la fenêtre, et un libellé mis en cache resterait
    /// dans l'ancienne.
    /// </summary>
    private string ControlText(IntPtr control)
    {
        if (control == _hWndChkAutoStart) return L.Settings_AutoStart;
        if (control == _hWndChkNotifications) return L.Settings_Notifications;
        if (control == _hWndChkOnboarding) return L.Settings_OnboardingWindow;
        if (control == _hWndChkTraining) return L.Challenge_OptIn;
        if (control == _hWndRadioLangFr) return "Français";
        if (control == _hWndRadioLangEn) return "English";
        if (control == _hWndResetVirtualKeyboardWindow) return L.Settings_ResetVirtualKeyboard;
        if (control == _hWndResetLessonsWindow) return L.Settings_ResetLessonsModule;
        if (control == _hWndCompatAdd) return L.Settings_CompatAdd;
        if (control == _hWndCompatRemove) return L.Settings_CompatRemove;
        if (control == _hWndRadioCompatAuto) return L.Settings_CompatModeAuto;
        if (control == _hWndRadioCompatForceOn) return L.Settings_CompatModeForceOn;
        if (control == _hWndRadioCompatForceOff) return L.Settings_CompatModeForceOff;
        int tab = TabIndexOf(control);
        return tab >= 0 ? TabLabel(tab) : string.Empty;
    }

    private bool TryDrawItem(IntPtr lParam)
    {
        var dis = Marshal.PtrToStructure<Win32.DRAWITEMSTRUCT>(lParam);

        if (dis.CtlType == Win32.ODT_LISTBOX)
            return DrawCompatItem(dis);

        if (dis.CtlType != Win32.ODT_BUTTON || dis.hwndItem == IntPtr.Zero)
            return false;

        var state = ControlState.None;
        if ((dis.itemState & Win32.ODS_DISABLED) != 0) state |= ControlState.Disabled;
        if ((dis.itemState & Win32.ODS_SELECTED) != 0) state |= ControlState.Pressed;
        if ((dis.itemState & Win32.ODS_FOCUS) != 0) state |= ControlState.Focused;
        if (_hoveredControl == dis.hwndItem) state |= ControlState.Hovered;
        if (GetCheck(dis.hwndItem)) state |= ControlState.Checked;

        // Un onglet occupe tout son rectangle et pose son propre fond : contrairement aux
        // autres contrôles, il n'est pas posé sur le panneau, il est ce qui le surmonte.
        int tabIndex = TabIndexOf(dis.hwndItem);
        if (tabIndex >= 0)
        {
            ThemeControls.DrawTab(dis.hDC, dis.rcItem, TabLabel(tabIndex), _hFontBold,
                tabIndex == _activeTab, state, Theme.Current, _dpi);
            return true;
        }

        // La marge de focus appartient au fond du panneau : l'effacer avant de rendre le
        // contrôle, faute de quoi l'anneau se dessinerait sur un fond non peint. Surface et
        // non Paper — dans cette fenêtre les contrôles sont posés sur les panneaux.
        var full = dis.rcItem;
        Win32.FillRect(dis.hDC, ref full, Theme.Brush(CLR_PANEL_BG));

        int focus = ThemeControls.FocusMargin(_dpi);
        var rect = new Win32.RECT
        {
            left = full.left + focus,
            top = full.top + focus,
            right = full.right - focus,
            bottom = full.bottom - focus,
        };

        string text = ControlText(dis.hwndItem);

        if (IsCheckBox(dis.hwndItem))
            ThemeControls.DrawCheckBox(dis.hDC, rect, text, _hFontBold, state, Theme.Current, _dpi);
        else if (IsRadio(dis.hwndItem))
            ThemeControls.DrawRadio(dis.hDC, rect, text, _hFontBold, state, Theme.Current, _dpi);
        else
            ThemeControls.DrawButton(dis.hDC, rect, text, _hFontButton,
                ButtonKind.Secondary, state, Theme.Current, _dpi);

        return true;
    }

    /// <summary>
    /// Une ligne de la liste des apps suspendues. <c>itemID</c> vaut <c>-1</c> quand la liste
    /// est vide et que Windows ne demande que le rectangle de focus : il n'y a alors aucun
    /// texte à lire, et <c>LB_GETTEXT</c> sur cet indice rendrait <c>LB_ERR</c>.
    /// </summary>
    private bool DrawCompatItem(Win32.DRAWITEMSTRUCT dis)
    {
        bool selected = (dis.itemState & Win32.ODS_SELECTED) != 0;
        var rect = dis.rcItem;
        Win32.FillRect(dis.hDC, ref rect,
            Theme.Brush(selected ? Theme.Current.ActionFill : Theme.Current.Surface));

        if (unchecked((int)dis.itemID) < 0)
            return true;

        string text = CompatItemText(dis.hwndItem, unchecked((int)dis.itemID));
        if (text.Length == 0)
            return true;

        Win32.SelectObject(dis.hDC, _hFontText);
        Win32.SetBkMode(dis.hDC, Win32.TRANSPARENT);
        Win32.SetTextColor(dis.hDC, selected ? Theme.Current.OnActionFill : Theme.Current.Ink);

        var textRect = new Win32.RECT
        {
            left = rect.left + ThemeControls.Scale(8, _dpi),
            top = rect.top,
            right = rect.right - ThemeControls.Scale(8, _dpi),
            bottom = rect.bottom,
        };
        Win32.DrawTextW(dis.hDC, text, -1, ref textRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_VCENTER | Win32.DT_NOPREFIX);
        return true;
    }

    /// <summary>Texte d'une ligne, relu dans la liste : <c>LBS_HASSTRINGS</c> est posé pour
    /// que Windows continue de les détenir, et non pour que la fenêtre en tienne une copie.</summary>
    private static string CompatItemText(IntPtr list, int index)
    {
        const int bufferChars = 260;
        IntPtr buffer = Marshal.AllocHGlobal(bufferChars * 2);
        try
        {
            IntPtr length = Win32.SendMessageW(list, LB_GETTEXT, (IntPtr)index, buffer);
            int chars = length.ToInt32();
            if (chars <= 0 || chars >= bufferChars)
                return string.Empty;
            return Marshal.PtrToStringUni(buffer, chars) ?? string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Hauteur d'une ligne de la liste. <c>LBS_OWNERDRAWFIXED</c> ne pose la question qu'une
    /// fois, à la création : sur changement de DPI la réponse serait périmée, et c'est
    /// <c>LB_SETITEMHEIGHT</c> qui la remet à jour depuis <c>ApplyFontsToControls</c>.
    /// </summary>
    private int CompatItemHeight()
    {
        IntPtr hdc = Win32.GetDC(_hWnd);
        try
        {
            return Math.Max(ThemeControls.Scale(20, _dpi),
                MeasureSingleLineHeight(hdc, _hFontText) + ThemeControls.Scale(4, _dpi));
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }
    }

    private bool TryMeasureItem(IntPtr lParam)
    {
        var mis = Marshal.PtrToStructure<Win32.MEASUREITEMSTRUCT>(lParam);
        if (mis.CtlType != Win32.ODT_LISTBOX)
            return false;

        mis.itemHeight = (uint)CompatItemHeight();
        Marshal.StructureToPtr(mis, lParam, false);
        return true;
    }
}
