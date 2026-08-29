# -*- coding: utf-8 -*-
"""Statistiques, seconde passe : brosse, liens, boutons owner-draw, bascule a chaud."""
import sys
from pathlib import Path

PATH = Path(r"D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store"
            r"\src\UsageStatsWindow.cs")

PATCHES = [
    ("                    RecreateFonts();\n",
     "                    ApplyFontsToControls();\n"),

    # La charte : un lien garde sa couleur dans tous ses etats, c'est la police qui souligne.
    ("""                        Win32.SetBkMode(hdcStatic, 1);
                        bool isActive = _hoveredLink == hCtrl || Win32.GetFocus() == hCtrl;
                        Win32.SetTextColor(hdcStatic, isActive ? CLR_LINK_HOVER : CLR_LINK);
                        return _hBgBrush;""",

     """                        Win32.SetBkMode(hdcStatic, 1);
                        // La même couleur dans tous les états : c'est la police qui souligne.
                        // Deux teintes de lien demanderaient une seconde nuance d'accent, que la
                        // charte n'a pas — et c'est de ce survol qu'était né l'orange fantôme.
                        Win32.SetTextColor(hdcStatic, CLR_LINK);
                        return Theme.Brush(CLR_BG);"""),

    ("        Win32.FillRect(hdc, ref clientRect, _hBgBrush);",
     "        Win32.FillRect(hdc, ref clientRect, Theme.Brush(CLR_BG));"),

    ("    private readonly IntPtr _hBgBrush;\n", ""),

    ("        _hBgBrush = Win32.CreateSolidBrush(CLR_BG);\n", ""),

    # Le survol pose la police soulignee plutot qu'une seconde couleur.
    ("""            case Win32.WM_MOUSEMOVE:
                if (_hoveredLink != hWnd)
                {
                    _hoveredLink = hWnd;
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);""",

     """            case Win32.WM_MOUSEMOVE:
                if (_hoveredLink != hWnd)
                {
                    _hoveredLink = hWnd;
                    ApplyLinkFont(hWnd, underlined: true);
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);"""),

    ("""            case Win32.WM_MOUSELEAVE:
                if (_hoveredLink == hWnd)
                {
                    _hoveredLink = IntPtr.Zero;
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);
                }
                break;""",

     """            case Win32.WM_MOUSELEAVE:
                if (_hoveredLink == hWnd)
                {
                    _hoveredLink = IntPtr.Zero;
                    ApplyLinkFont(hWnd, underlined: false);
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);
                }
                break;"""),

    # Rendu des deux boutons, et bascule a chaud.
    ("""    private IntPtr LinkSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)""",

     """    /// <summary>Un contrôle STATIC est peint par le système : on ne peut pas lui dessiner de
    /// filet, seule sa police peut le souligner. Les boutons, eux, ne sont pas des liens et
    /// gardent la leur.</summary>
    private void ApplyLinkFont(IntPtr hWnd, bool underlined)
    {
        if (hWnd != _hWndLinkFeedback && hWnd != _hWndLinkDiscord)
            return;
        Win32.SendMessageW(hWnd, Win32.WM_SETFONT,
            underlined ? _hFontLinkHovered : _hFontLink, (IntPtr)1);
    }

    /// <summary>
    /// Les deux boutons de la fenêtre. « Copier » est l'action qu'elle propose, donc le seul
    /// primaire de l'écran ; « Fermer » est un congé, comme dans À propos.
    /// </summary>
    private bool TryDrawItem(IntPtr lParam)
    {
        var dis = Marshal.PtrToStructure<Win32.DRAWITEMSTRUCT>(lParam);
        if (dis.hwndItem != _hWndBtnCopy && dis.hwndItem != _hWndBtnClose)
            return false;

        var state = ControlState.None;
        if ((dis.itemState & Win32.ODS_DISABLED) != 0) state |= ControlState.Disabled;
        if ((dis.itemState & Win32.ODS_SELECTED) != 0) state |= ControlState.Pressed;
        if ((dis.itemState & Win32.ODS_FOCUS) != 0) state |= ControlState.Focused;
        if (_hoveredLink == dis.hwndItem) state |= ControlState.Hovered;

        var full = dis.rcItem;
        Win32.FillRect(dis.hDC, ref full, Theme.Brush(CLR_BG));

        bool copy = dis.hwndItem == _hWndBtnCopy;
        ThemeControls.DrawButton(dis.hDC, dis.rcItem, copy ? L.Stats_BtnCopy : L.Common_Close,
            _hFontButton, copy ? ButtonKind.Primary : ButtonKind.Secondary,
            state, Theme.Current, _dpi);
        return true;
    }

    private IntPtr LinkSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)"""),

    ("""                case Win32.WM_SETCURSOR:""",
     """                case Win32.WM_CTLCOLORBTN:
                    // Windows efface le fond d'un bouton owner-draw avec la brosse rendue ici,
                    // avant d'envoyer WM_DRAWITEM.
                    Win32.SetBkMode(wParam, Win32.TRANSPARENT);
                    Win32.SetTextColor(wParam, CLR_TEXT);
                    return Theme.Brush(CLR_BG);

                case Win32.WM_DRAWITEM:
                    if (TryDrawItem(lParam))
                        return (IntPtr)1;
                    break;

                case Win32.WM_SETCURSOR:"""),

    # Dispose
    ("""        if (_hWndLinkDiscord != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndLinkDiscord, _linkSubclassProc, (UIntPtr)2);
        if (_hWnd != IntPtr.Zero)
        {
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }

        DestroyFonts();
        Win32.DeleteObject(_hBgBrush);
""",

     """        if (_hWndLinkDiscord != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndLinkDiscord, _linkSubclassProc, (UIntPtr)2);
        if (_hWndBtnCopy != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndBtnCopy, _linkSubclassProc, (UIntPtr)3);
        if (_hWndBtnClose != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndBtnClose, _linkSubclassProc, (UIntPtr)4);

        if (_themeChanged != null)
        {
            Theme.Changed -= _themeChanged;
            _themeChanged = null;
        }

        if (_hWnd != IntPtr.Zero)
        {
            ThemeWindow.ForgetClassBackground(_hWnd);
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }

        // Ni police ni brosse à libérer : les unes appartiennent au cache de Theme, l'autre au
        // système, qui la détruit avec la classe.
"""),

    # Abonnement a la bascule de theme
    ("        ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);",
     """        ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);

        _themeChanged = () =>
        {
            if (_hWnd == IntPtr.Zero)
                return;
            ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);
            ThemeWindow.ApplyChrome(_hWnd);
        };
        Theme.Changed += _themeChanged;"""),

    ("    private bool _visible;\n    private bool _showCopiedFeedback;",
     "    private bool _visible;\n    private bool _showCopiedFeedback;\n    private Action? _themeChanged;"),
]


def main():
    data = PATH.read_bytes()
    if data.count(b"\r\n"):
        print("REFUS : CRLF present")
        return 1
    text = data.decode("utf-8")
    for old, _new in PATCHES:
        if text.count(old) != 1:
            print(f"REFUS : ancre trouvee {text.count(old)} fois")
            print(f"        {old.splitlines()[0][:95]!r}")
            return 1
    for old, new in PATCHES:
        text = text.replace(old, new, 1)
    out = text.encode("utf-8")
    assert b"\r\n" not in out
    PATH.write_bytes(out)
    print(f"ecrit : {len(out)} octets, 0 CRLF")
    return 0


if __name__ == "__main__":
    sys.exit(main())
