# -*- coding: utf-8 -*-
"""Migre UsageStatsWindow sur la charte, et ajoute le rôle BodyStrong.

Theme.cs et UsageStatsWindow.cs sont en LF pur (verifie avant ecriture).
"""
import sys
from pathlib import Path

SRC = Path(r"D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store\src")

PATCHES = [
    # ── Theme : un corps gras, sans taille nouvelle ──────────────────────────
    ("Theme.cs",
     "    /// <summary>Secondaire, légendes, sous-étiquettes. 13 pt, graisse 400.</summary>\n"
     "    Secondary,\n",

     "    /// <summary>Secondaire, légendes, sous-étiquettes. 13 pt, graisse 400.</summary>\n"
     "    Secondary,\n"
     "\n"
     "    /// <summary>Corps mis en avant. 15 pt, graisse 600 — la même taille que le corps, ce\n"
     "    /// qui n'ajoute rien à l'échelle typographique arrêtée à CH1 : seule la graisse\n"
     "    /// change. Les statistiques en ont besoin pour leurs lignes saillantes, que la charte\n"
     "    /// rendait jusque-là en titre de section, deux crans trop gros.</summary>\n"
     "    BodyStrong,\n"),

    ("Theme.cs",
     "        FontRole.Secondary => (13, 400, SegoeUi),\n",
     "        FontRole.Secondary => (13, 400, SegoeUi),\n"
     "        FontRole.BodyStrong => (15, 600, SegoeUi),\n"),

    # ── Statistiques : les couleurs deviennent des jetons ────────────────────
    ("UsageStatsWindow.cs",
     """    // ── Colors (COLORREF = 0x00BBGGRR) ──────────────────────────────
    private const uint CLR_BG = 0x00DDDDDD;
    private const uint CLR_TITLE = 0x00201C18;
    private const uint CLR_TEXT = 0x00333333;
    private const uint CLR_MUTED = 0x00888888;
    private const uint CLR_ACCENT = 0x00D47800;
    private const uint CLR_SEPARATOR = 0x00D7D7D7;
    private const uint CLR_LINK = 0x00D47800;
    private const uint CLR_LINK_HOVER = 0x000078D4;""",

     """    // ── Couleurs : les jetons de la charte, relus à chaque bascule de thème ──────
    // L'accent et la couleur de lien de cette fenêtre étaient l'orange 0x00D47800, qui est le
    // bleu 0x000078D4 à octets inversés — le piège COLORREF dont l'orange fantôme du parc est
    // né. Les deux disparaissent au profit du seul accent de la charte.
    private static uint CLR_BG => Theme.Current.Paper;
    private static uint CLR_TITLE => Theme.Current.Ink;
    private static uint CLR_TEXT => Theme.Current.Ink;
    private static uint CLR_MUTED => Theme.Current.TextSecondary;
    private static uint CLR_ACCENT => Theme.Current.Action;
    private static uint CLR_SEPARATOR => Theme.Current.Border;
    private static uint CLR_LINK => Theme.Current.Action;"""),

    # ── Polices : le cache de Theme, plus de cycle de vie local ──────────────
    ("UsageStatsWindow.cs",
     """    private IntPtr _hFontTitle;
    private IntPtr _hFontText;
    private IntPtr _hFontMuted;
    private IntPtr _hFontBold;
    private IntPtr _hFontButton;
    private IntPtr _hFontLink;""",

     """    /// <summary>L'échelle en points par pouce, dont Theme a besoin pour ses polices.
    /// _dpiScale reste la mesure de travail de cette fenêtre, qui multiplie des dizaines de
    /// coordonnées : les deux disent la même chose.</summary>
    private int _dpi => (int)Math.Round(96 * _dpiScale);

    private IntPtr _hFontTitle => Theme.Font(FontRole.WindowTitle, _dpi);
    private IntPtr _hFontText => Theme.Font(FontRole.Body, _dpi);
    private IntPtr _hFontMuted => Theme.Font(FontRole.Secondary, _dpi);
    private IntPtr _hFontBold => Theme.Font(FontRole.BodyStrong, _dpi);
    private IntPtr _hFontButton => Theme.Font(FontRole.Body, _dpi);
    private IntPtr _hFontLink => Theme.Font(FontRole.Body, _dpi);
    private IntPtr _hFontLinkHovered => Theme.Font(FontRole.Body, _dpi, underlined: true);"""),

    ("UsageStatsWindow.cs",
     """    private void CreateFonts()
    {
        _hFontTitle = Win32.CreateFontW(-S(20), 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontText = Win32.CreateFontW(-S(14), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontMuted = Win32.CreateFontW(-S(12), 0, 0, 0, 400, 1, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontBold = Win32.CreateFontW(-S(14), 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontButton = Win32.CreateFontW(-S(13), 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontLink = Win32.CreateFontW(-S(13), 0, 0, 0, 600, 0, 1, 0, 0, 0, 0, 5, 0, "Segoe UI");
    }

    private void DestroyFonts()
    {
        Win32.DeleteObject(_hFontTitle);
        Win32.DeleteObject(_hFontText);
        Win32.DeleteObject(_hFontMuted);
        Win32.DeleteObject(_hFontBold);
        Win32.DeleteObject(_hFontButton);
        Win32.DeleteObject(_hFontLink);
    }

    private void RecreateFonts()
    {
        DestroyFonts();
        CreateFonts();
        ApplyFontsToControls();
    }

""",
     ""),

    ("UsageStatsWindow.cs",
     "        CreateFonts();\n        CreateMainWindow();",
     "        CreateMainWindow();"),

    ("UsageStatsWindow.cs",
     "                _dpiScale = realDpi / 96f;\n                RecreateFonts();\n                ResizeWindow();",
     "                _dpiScale = realDpi / 96f;\n                ApplyFontsToControls();\n                ResizeWindow();"),

    # ── Classe de fenêtre et chrome ──────────────────────────────────────────
    ("UsageStatsWindow.cs",
     "            hbrBackground = _hBgBrush,\n            lpszClassName = className",
     "            // hbrBackground = IntPtr.Zero : une brosse inscrite ici appartient au système,\n"
     "            // qui la détruit au désenregistrement de la classe. ApplyClassBackground en\n"
     "            // pose une dédiée, que ce helper est seul à libérer.\n"
     "            hbrBackground = IntPtr.Zero,\n            lpszClassName = className"),

    ("UsageStatsWindow.cs",
     "            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);\n        Win32.EnableDarkTitleBar(_hWnd);",
     "            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);\n"
     "        ThemeWindow.ApplyChrome(_hWnd);\n"
     "        ThemeWindow.ApplyProductIcon(_hWnd);\n"
     "        ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);"),

    # ── Boutons owner-draw ───────────────────────────────────────────────────
    ("UsageStatsWindow.cs",
     """        _hWndBtnCopy = Win32.CreateWindowExW(0, "BUTTON", L.Stats_BtnCopy,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_COPY, hInstance, IntPtr.Zero);

        _hWndBtnClose = Win32.CreateWindowExW(0, "BUTTON", L.Common_Close,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | 0x0001 /* BS_DEFPUSHBUTTON */,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_CLOSE, hInstance, IntPtr.Zero);""",

     """        // BS_OWNERDRAW remplace le bouton à relief du système : c'est le grand écart entre
        // ces deux générations de contrôles que la refonte supprime.
        _hWndBtnCopy = Win32.CreateWindowExW(0, "BUTTON", L.Stats_BtnCopy,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_OWNERDRAW,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_COPY, hInstance, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndBtnCopy, _linkSubclassProc, (UIntPtr)3, IntPtr.Zero);

        _hWndBtnClose = Win32.CreateWindowExW(0, "BUTTON", L.Common_Close,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_OWNERDRAW,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_CLOSE, hInstance, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndBtnClose, _linkSubclassProc, (UIntPtr)4, IntPtr.Zero);"""),
]


def main():
    files = {}
    for rel, old, _new in PATCHES:
        if rel not in files:
            data = (SRC / rel).read_bytes()
            if data.count(b"\r\n"):
                print(f"REFUS {rel} : CRLF present")
                return 1
            files[rel] = data.decode("utf-8")
        if files[rel].count(old) != 1:
            print(f"REFUS {rel} : ancre trouvee {files[rel].count(old)} fois")
            print(f"        {old.splitlines()[0][:95]!r}")
            return 1

    for rel, old, new in PATCHES:
        files[rel] = files[rel].replace(old, new, 1)

    for rel, text in files.items():
        out = text.encode("utf-8")
        assert b"\r\n" not in out, rel
        (SRC / rel).write_bytes(out)
        print(f"ecrit {rel} : {len(out)} octets, 0 CRLF")
    return 0


if __name__ == "__main__":
    sys.exit(main())
