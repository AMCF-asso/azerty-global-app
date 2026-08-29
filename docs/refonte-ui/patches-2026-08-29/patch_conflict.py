# -*- coding: utf-8 -*-
"""Migre LayoutConflictWindow sur la charte. Fichier en LF pur (verifie)."""
import sys
from pathlib import Path

PATH = Path(r"D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store"
            r"\src\LayoutConflictWindow.cs")

PATCHES = [
    ("""    private const uint CLR_BG = 0x00DDDDDD;
    private const uint CLR_TITLE = 0x00201C18;
    private const uint CLR_TEXT = 0x00333333;
    private const uint CLR_HIGHLIGHT = 0x000078D4;
    private const uint CLR_SUBTLE = 0x00666666;""",

     """    // Les jetons de la charte, relus à chaque bascule de thème. CLR_HIGHLIGHT était le bleu
    // Windows 0x000078D4, dont l'orange fantôme du parc est l'exact inverse d'octets.
    private static uint CLR_BG => Theme.Current.Paper;
    private static uint CLR_TITLE => Theme.Current.Ink;
    private static uint CLR_TEXT => Theme.Current.Ink;
    private static uint CLR_HIGHLIGHT => Theme.Current.Action;
    private static uint CLR_SUBTLE => Theme.Current.TextSecondary;"""),

    ("""    private IntPtr _hFontTitle;
    private IntPtr _hFontText;
    private IntPtr _hFontBold;
    private IntPtr _hFontButton;""",

     """    /// <summary>L'échelle en points par pouce, dont Theme a besoin pour ses polices.</summary>
    private int _dpi => (int)Math.Round(96 * _dpiScale);

    private IntPtr _hFontTitle => Theme.Font(FontRole.WindowTitle, _dpi);
    private IntPtr _hFontText => Theme.Font(FontRole.Body, _dpi);
    private IntPtr _hFontBold => Theme.Font(FontRole.BodyStrong, _dpi);
    private IntPtr _hFontButton => Theme.Font(FontRole.Body, _dpi);"""),

    ("    private readonly IntPtr _hBgBrush;\n", "    private Action? _themeChanged;\n"),

    ("        _hBgBrush = Win32.CreateSolidBrush(CLR_BG);\n", ""),

    ("        Win32.FillRect(hdc, ref clientRect, _hBgBrush);",
     "        Win32.FillRect(hdc, ref clientRect, Theme.Brush(CLR_BG));"),

    ("        Win32.EnableDarkTitleBar(_hWnd);\n    }",
     """        ThemeWindow.ApplyChrome(_hWnd);
        ThemeWindow.ApplyProductIcon(_hWnd);
        ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);

        _themeChanged = () =>
        {
            if (_hWnd == IntPtr.Zero)
                return;
            ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);
            ThemeWindow.ApplyChrome(_hWnd);
        };
        Theme.Changed += _themeChanged;
    }"""),

    ("""        _hWndBtnQuit = Win32.CreateWindowExW(0, "BUTTON", L.LayoutConflict_BtnQuit,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | 0x0001 /* BS_DEFPUSHBUTTON */,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_QUIT, hInstance, IntPtr.Zero);

        _hWndBtnKeep = Win32.CreateWindowExW(0, "BUTTON", L.LayoutConflict_BtnKeep,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_KEEP, hInstance, IntPtr.Zero);""",

     """        // BS_OWNERDRAW : le relief du système disparaît. « Quitter l'autre disposition » est
        // ce que la fenêtre propose de faire, donc le seul primaire ; « garder » est le refus.
        _hWndBtnQuit = Win32.CreateWindowExW(0, "BUTTON", L.LayoutConflict_BtnQuit,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_OWNERDRAW,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_QUIT, hInstance, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndBtnQuit, _buttonSubclassProc, (UIntPtr)1, IntPtr.Zero);

        _hWndBtnKeep = Win32.CreateWindowExW(0, "BUTTON", L.LayoutConflict_BtnKeep,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_OWNERDRAW,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_KEEP, hInstance, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndBtnKeep, _buttonSubclassProc, (UIntPtr)2, IntPtr.Zero);"""),

    ("""    public void Dispose()
    {
        if (_hWnd != IntPtr.Zero)
        {
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
        DestroyFonts();
        Win32.DeleteObject(_hBgBrush);
""",

     """    public void Dispose()
    {
        if (_themeChanged != null)
        {
            Theme.Changed -= _themeChanged;
            _themeChanged = null;
        }
        if (_hWndBtnQuit != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndBtnQuit, _buttonSubclassProc, (UIntPtr)1);
        if (_hWndBtnKeep != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndBtnKeep, _buttonSubclassProc, (UIntPtr)2);
        if (_hWnd != IntPtr.Zero)
        {
            ThemeWindow.ForgetClassBackground(_hWnd);
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
        // Ni police ni brosse à libérer : les unes appartiennent au cache de Theme, l'autre au
        // système, qui la détruit avec la classe.
"""),
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
