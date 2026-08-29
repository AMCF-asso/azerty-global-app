# -*- coding: utf-8 -*-
"""CH3 passe 1 : la fenetre Parametres lit ses couleurs et ses polices sur la charte.

La geometrie n'est pas touchee (positions, tailles, marges, BASE_WIN_W/H restent tels quels) :
elle fait l'objet d'une seconde passe, qui exige de voir la fenetre rendue et attend donc que
Smart App Control rouvre le banc de captures.

Deux constantes mortes disparaissent, mesurees a zero usage : CLR_PANEL_ACCENT et CLR_SUBTITLE.
CLR_LINK_HOVER disparait aussi : sa valeur etait l'orange fantome, et le survol d'un lien reste
porte par le soulignement permanent et le curseur main, comme la fenetre Statistiques l'a tranche
au commit 6f296f9.

Fichier mesure LF pur, sans BOM. Le script le reverifie region par region avant d'ecrire.
"""

import io
import os
import sys

SRC = os.path.normpath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "src")
)
TARGET = os.path.join(SRC, "SettingsWindow.cs")

COLOR_ANCHOR = """    private const uint CLR_BG = 0x00DDDDDD;
    private const uint CLR_TITLE = 0x00201C18;
    private const uint CLR_TEXT = 0x00333333;
    private const uint CLR_MUTED = 0x00666666;
    private const uint CLR_VERSION = 0x00888888;
    private const uint CLR_PANEL_BG = 0x00EEEEEE;
    private const uint CLR_PANEL_BORDER = 0x00D1D1D1;
    private const uint CLR_PANEL_ACCENT = 0x00D47800;
    private const uint CLR_LINK = 0x00D47800;
    private const uint CLR_LINK_HOVER = 0x000078D4;
    private const uint CLR_INLINE_HIGHLIGHT = 0x000078D4;
    private const uint CLR_VALID = 0x00228B22;
    private const uint CLR_INVALID = 0x000000CC;
    private const uint CLR_KEY_BG = 0x00FAFAFA;
    private const uint CLR_KEY_BORDER = 0x00CBCBCB;
    private const uint CLR_KEY_BORDER_INVALID = 0x00A8A8FF;
    private const uint CLR_SEPARATOR = 0x00D7D7D7;
    private const uint CLR_SUBTITLE = 0x003A342E;
"""

COLOR_REPLACEMENT = """    // Les jetons de la charte, relus a chaque peinture : une bascule de theme n'a donc rien a
    // recalculer ici. Trois noms ont disparu — CLR_PANEL_ACCENT et CLR_SUBTITLE ne peignaient
    // rien (zero usage mesure), et CLR_LINK_HOVER portait l'orange fantome, le survol restant
    // marque par le soulignement du lien et le curseur main.
    private static uint CLR_BG => Theme.Current.Paper;
    private static uint CLR_TITLE => Theme.Current.Ink;
    private static uint CLR_TEXT => Theme.Current.Ink;
    private static uint CLR_MUTED => Theme.Current.TextSecondary;
    private static uint CLR_VERSION => Theme.Current.TextSecondary;
    private static uint CLR_PANEL_BG => Theme.Current.Surface;
    private static uint CLR_PANEL_BORDER => Theme.Current.Border;
    private static uint CLR_LINK => Theme.Current.Action;
    private static uint CLR_INLINE_HIGHLIGHT => Theme.Current.Action;
    private static uint CLR_VALID => Theme.Current.Success;
    private static uint CLR_INVALID => Theme.Current.Error;
    private static uint CLR_KEY_BG => Theme.Current.Surface;
    private static uint CLR_KEY_BORDER => Theme.Current.Border;
    private static uint CLR_KEY_BORDER_INVALID => Theme.Current.Error;
    private static uint CLR_SEPARATOR => Theme.Current.Border;
"""

FONT_FIELDS_ANCHOR = """    private IntPtr _hFontTitle;
    private IntPtr _hFontVersion;
    private IntPtr _hFontSubtitle;
    private IntPtr _hFontPanelTitle;
    private IntPtr _hFontText;
    private IntPtr _hFontBold;
    private IntPtr _hFontEdit;
    private IntPtr _hFontLink;
    private IntPtr _hFontLinkStrong;
    private IntPtr _hFontSmall;
    private IntPtr _hFontButton;
"""

FONT_FIELDS_REPLACEMENT = """    /// <summary>L'echelle en points par pouce, dont Theme a besoin pour ses polices. _dpiScale
    /// reste la mesure de travail de cette fenetre, qui multiplie des dizaines de coordonnees :
    /// les deux disent la meme chose.</summary>
    private int _dpi => (int)Math.Round(96 * _dpiScale);

    private IntPtr _hFontTitle => Theme.Font(FontRole.WindowTitle, _dpi);
    private IntPtr _hFontVersion => Theme.Font(FontRole.Mono, _dpi);
    private IntPtr _hFontSubtitle => Theme.Font(FontRole.BodyStrong, _dpi);
    private IntPtr _hFontPanelTitle => Theme.Font(FontRole.SectionTitle, _dpi);
    private IntPtr _hFontText => Theme.Font(FontRole.Body, _dpi);
    private IntPtr _hFontBold => Theme.Font(FontRole.BodyStrong, _dpi);
    private IntPtr _hFontEdit => Theme.Font(FontRole.BodyStrong, _dpi);
    private IntPtr _hFontLink => Theme.Font(FontRole.Body, _dpi, underlined: true);
    private IntPtr _hFontLinkStrong => Theme.Font(FontRole.BodyStrong, _dpi, underlined: true);
    private IntPtr _hFontSmall => Theme.Font(FontRole.Secondary, _dpi);
    private IntPtr _hFontButton => Theme.Font(FontRole.Body, _dpi);
"""

FONT_LIFECYCLE_ANCHOR = """    private void CreateFonts()
    {
        _hFontTitle = Win32.CreateFontW(-S(18), 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontVersion = Win32.CreateFontW(-S(9), 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontSubtitle = Win32.CreateFontW(-S(11), 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontPanelTitle = Win32.CreateFontW(-S(13), 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontText = Win32.CreateFontW(-S(11), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontBold = Win32.CreateFontW(-S(11), 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontEdit = Win32.CreateFontW(-S(13), 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontLink = Win32.CreateFontW(-S(11), 0, 0, 0, 400, 0, 1, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontLinkStrong = Win32.CreateFontW(-S(11), 0, 0, 0, 700, 0, 1, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontSmall = Win32.CreateFontW(-S(9), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        _hFontButton = Win32.CreateFontW(-S(11), 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
    }

    private void DestroyFonts()
    {
        Win32.DeleteObject(_hFontTitle);
        Win32.DeleteObject(_hFontVersion);
        Win32.DeleteObject(_hFontSubtitle);
        Win32.DeleteObject(_hFontPanelTitle);
        Win32.DeleteObject(_hFontText);
        Win32.DeleteObject(_hFontBold);
        Win32.DeleteObject(_hFontEdit);
        Win32.DeleteObject(_hFontLink);
        Win32.DeleteObject(_hFontLinkStrong);
        Win32.DeleteObject(_hFontSmall);
        Win32.DeleteObject(_hFontButton);
    }

    private void RecreateFonts()
    {
        DestroyFonts();
        CreateFonts();
        ApplyFontsToControls();
    }

"""

REPLACEMENTS = [
    ("couleurs", COLOR_ANCHOR, COLOR_REPLACEMENT),
    (
        "bordure de focus",
        "    private const uint CLR_KEY_BORDER_FOCUS = 0x000078D4;\n"
        "    private static string ShortcutCaptureHint => L.Settings_ShortcutCaptureHint;\n",
        "    private static uint CLR_KEY_BORDER_FOCUS => Theme.Current.Action;\n"
        "    private static string ShortcutCaptureHint => L.Settings_ShortcutCaptureHint;\n",
    ),
    ("polices", FONT_FIELDS_ANCHOR, FONT_FIELDS_REPLACEMENT),
    ("cycle de vie des polices", FONT_LIFECYCLE_ANCHOR, ""),
    (
        "brosses",
        "    private readonly IntPtr _hBgBrush;\n"
        "    private readonly IntPtr _hPanelBrush;\n"
        "    private readonly IntPtr _hKeyBrush;\n",
        "    // Les brosses viennent du cache de Theme et lui appartiennent : cette fenetre n'en\n"
        "    // detruit aucune, et aucune n'est inscrite en fond de classe (voir CreateMainWindow).\n"
        "    private static IntPtr _hBgBrush => Theme.Brush(CLR_BG);\n"
        "    private static IntPtr _hPanelBrush => Theme.Brush(CLR_PANEL_BG);\n"
        "    private static IntPtr _hKeyBrush => Theme.Brush(CLR_KEY_BG);\n",
    ),
    (
        "construction des brosses",
        "        _hBgBrush = Win32.CreateSolidBrush(CLR_BG);\n"
        "        _hPanelBrush = Win32.CreateSolidBrush(CLR_PANEL_BG);\n"
        "        _hKeyBrush = Win32.CreateSolidBrush(CLR_KEY_BG);\n"
        "\n",
        "",
    ),
    (
        "appel CreateFonts",
        "        CreateFonts();\n        CreateMainWindow();\n",
        "        CreateMainWindow();\n",
    ),
    (
        "correction DPI a la creation",
        "                _dpiScale = realDpi / 96f;\n"
        "                RecreateFonts();\n"
        "                ResizeWindow();\n",
        "                _dpiScale = realDpi / 96f;\n"
        "                ApplyFontsToControls();\n"
        "                ResizeWindow();\n",
    ),
    (
        "fond de classe",
        "            hbrBackground = _hBgBrush,\n",
        "            // hbrBackground = IntPtr.Zero : une brosse inscrite ici appartient au systeme,\n"
        "            // qui la detruit au desenregistrement de la classe. ApplyClassBackground en\n"
        "            // pose une apres coup, que Theme garde dans son cache.\n"
        "            hbrBackground = IntPtr.Zero,\n",
    ),
    (
        "chrome et bascule de theme",
        "        Win32.EnableDarkTitleBar(_hWnd);\n    }\n",
        "        ThemeWindow.ApplyChrome(_hWnd);\n"
        "        ThemeWindow.ApplyProductIcon(_hWnd);\n"
        "        ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);\n"
        "\n"
        "        _themeChanged = () =>\n"
        "        {\n"
        "            if (_hWnd == IntPtr.Zero)\n"
        "                return;\n"
        "            ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);\n"
        "            ThemeWindow.ApplyChrome(_hWnd);\n"
        "        };\n"
        "        Theme.Changed += _themeChanged;\n"
        "    }\n",
    ),
    (
        "WM_DPICHANGED",
        "                    _dpiScale = newDpi / 96f;\n                RecreateFonts();\n",
        "                    _dpiScale = newDpi / 96f;\n                ApplyFontsToControls();\n",
    ),
    (
        "liberation",
        "        DestroyFonts();\n"
        "        Win32.DeleteObject(_hBgBrush);\n"
        "        Win32.DeleteObject(_hPanelBrush);\n"
        "        Win32.DeleteObject(_hKeyBrush);\n"
        "\n",
        "        if (_themeChanged != null)\n"
        "        {\n"
        "            Theme.Changed -= _themeChanged;\n"
        "            _themeChanged = null;\n"
        "        }\n"
        "\n",
    ),
    (
        "survol de lien",
        "                    Win32.SetTextColor(hdcStatic, _hoveredLink == hCtrl ? CLR_LINK_HOVER : CLR_LINK);\n",
        "                    Win32.SetTextColor(hdcStatic, CLR_LINK);\n",
    ),
    (
        "champ de bascule de theme",
        "    private float _dpiScale;\n",
        "    private Action? _themeChanged;\n\n    private float _dpiScale;\n",
    ),
]


def apply(label, anchor, replacement):
    with open(TARGET, "rb") as handle:
        data = handle.read()

    lf_anchor = anchor.encode("utf-8")
    crlf_anchor = anchor.replace("\n", "\r\n").encode("utf-8")
    lf_count = data.count(lf_anchor)
    crlf_count = data.count(crlf_anchor)

    if lf_count + crlf_count != 1:
        sys.exit(
            "%s : ancre trouvee %d fois en LF et %d fois en CRLF, attendu 1 au total"
            % (label, lf_count, crlf_count)
        )

    if lf_count:
        found, new, ending = lf_anchor, replacement.encode("utf-8"), "LF"
    else:
        found = crlf_anchor
        new = replacement.replace("\n", "\r\n").encode("utf-8")
        ending = "CRLF"

    patched = data.replace(found, new)
    with open(TARGET, "wb") as handle:
        handle.write(patched)

    crlf = patched.count(b"\r\n")
    print(
        "%-28s %-4s | CRLF %d | %+d octets"
        % (label, ending, crlf, len(patched) - len(data))
    )


def main():
    for label, anchor, replacement in REPLACEMENTS:
        apply(label, anchor, replacement)


if __name__ == "__main__":
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
    main()
