# -*- coding: utf-8 -*-
"""CH3 passe 1 : la fenetre Onboarding lit ses couleurs sur la charte.

Les couleurs seulement. Contrairement a Parametres, la typographie de cet ecran ne se separe pas
de sa geometrie : il porte dix tailles distinctes (28, 26, 21, 20, 18, 17, 16, 15, 14 et un
17/1,75) la ou la charte en gele six, sur une fenetre a hauteur fixe et a texte fluide. Les
ramener aux six roles deplace chaque retour a la ligne et chaque hauteur de carte, ce qui ne
s'ecrit pas sans voir la fenetre rendue. Les couleurs, elles, ne bougent aucune metrique.

Treize constantes mortes disparaissent, mesurees a zero usage : CLR_FEATURE_TITLE,
CLR_BANNER_BORDER, CLR_BANNER_TEXT, CLR_BANNER_TITLE, CLR_HIGHLIGHT, CLR_SECTION, CLR_NOTE_BG,
CLR_NOTE_BORDER, CLR_NOTE_ACCENT, CLR_WARNING_TEXT, CLR_SEPARATOR, ARGB_STEP_CIRCLE et
ARGB_WHITE. Avec elles disparait la seule matiere qu'aurait eue une famille « avertissement » :
aucun bloc de cet ecran n'en peint une.

⚠️ Fichier a fins de ligne mixtes (1 459 CRLF, 246 LF) et porteur d'un BOM : la terminaison se
deduit de la region de chaque ancre.
"""

import io
import os
import sys

SRC = os.path.normpath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "src")
)
TARGET = os.path.join(SRC, "OnboardingWindow.cs")

COLOR_ANCHOR = """    // ── Colors (COLORREF = 0x00BBGGRR) ───────────────────────────────
    private const uint CLR_BG = 0x00DDDDDD;
    private const uint CLR_TITLE = 0x00201C18;
    private const uint CLR_FEATURE_TITLE = 0x00D47800;
    private const uint CLR_TEXT = 0x00333333;
    private const uint CLR_LINK = 0x00D47800;
    private const uint CLR_LINK_HOVER = 0x00FF9830;
    private const uint CLR_BANNER_BG = 0x00E8E8E8;
    private const uint CLR_BANNER_BORDER = 0x000078D4;
    private const uint CLR_BANNER_TEXT = 0x00333333;
    private const uint CLR_BANNER_TITLE = 0x000078D4;
    private const uint CLR_STEP_TITLE = 0x00D47800;
    private const uint CLR_HIGHLIGHT = 0x000078D4;
    private const uint CLR_PROGRESS_ACTIVE = 0x00D47800;
    private const uint CLR_PROGRESS_INACTIVE = 0x00C8C8C8;
    private const uint CLR_SECTION = 0x00D47800;
    private const uint CLR_PANEL_BG = 0x00EEEEEE;
    private const uint CLR_PANEL_BORDER = 0x00D1D1D1;
    private const uint CLR_NOTE_BG = 0x00D8F4FF;
    private const uint CLR_NOTE_BORDER = 0x007BC2EB;
    private const uint CLR_NOTE_ACCENT = 0x002A98E2;
    private const uint CLR_BADGE_BG = 0x00D47800;
    private const uint CLR_BADGE_TEXT = 0x00FFFFFF;
    private const uint CLR_PILL_BG = 0x00FBECD8;
    private const uint CLR_PILL_TEXT = 0x00201C18;
    private const uint CLR_WARNING_TEXT = 0x00174D6E;
    private const uint CLR_INLINE_HIGHLIGHT = 0x000078D4;
    private const uint CLR_SEPARATOR = 0x00D0D0D0;
    private const uint CLR_REASSURE = 0x00666666;
    private const uint ARGB_STEP_CIRCLE = 0xFF0078D4;
    private const uint ARGB_WHITE = 0xFFFFFFFF;
"""

COLOR_REPLACEMENT = """    // ── Les jetons de la charte, relus a chaque peinture ─────────────
    // Treize noms ont disparu ici, tous mesures a zero usage : les quatre de l'encadre note et
    // du texte d'avertissement, les trois de la banniere autres que son fond, le titre de
    // fonctionnalite, la surbrillance, la section, le separateur et les deux couleurs GDI+ du
    // cercle d'etape. CLR_LINK_HOVER part avec eux : le survol d'un lien reste porte par le
    // soulignement et le curseur main, comme la fenetre Statistiques l'a tranche au 6f296f9.
    private static uint CLR_BG => Theme.Current.Paper;
    private static uint CLR_TITLE => Theme.Current.Ink;
    private static uint CLR_TEXT => Theme.Current.Ink;
    private static uint CLR_LINK => Theme.Current.Action;
    private static uint CLR_BANNER_BG => Theme.Current.Surface;
    private static uint CLR_STEP_TITLE => Theme.Current.Action;
    private static uint CLR_PROGRESS_ACTIVE => Theme.Current.Action;
    private static uint CLR_PROGRESS_INACTIVE => Theme.Current.Border;
    private static uint CLR_PANEL_BG => Theme.Current.Surface;
    private static uint CLR_PANEL_BORDER => Theme.Current.Border;
    private static uint CLR_BADGE_BG => Theme.Current.Action;
    private static uint CLR_BADGE_TEXT => Theme.Current.OnAction;
    private static uint CLR_PILL_BG => Theme.Current.ActionFill;
    private static uint CLR_PILL_TEXT => Theme.Current.Ink;
    private static uint CLR_INLINE_HIGHLIGHT => Theme.Current.Action;
    private static uint CLR_REASSURE => Theme.Current.TextSecondary;
"""

REPLACEMENTS = [
    ("couleurs", 1, COLOR_ANCHOR, COLOR_REPLACEMENT),
    (
        "brosses",
        1,
        "        _hBgBrush = Win32.CreateSolidBrush(CLR_BG);\n"
        "        _hBannerBgBrush = Win32.CreateSolidBrush(CLR_BANNER_BG);\n"
        "        _hPanelBrush = Win32.CreateSolidBrush(CLR_PANEL_BG);\n",
        "        // Les brosses viennent du cache de Theme et lui appartiennent : cette fenetre\n"
        "        // n'en detruit aucune, et aucune n'est inscrite en fond de classe.\n",
    ),
    (
        "champs de brosses",
        1,
        "    private readonly IntPtr _hBgBrush;\n"
        "    private readonly IntPtr _hBannerBgBrush;\n"
        "    private readonly IntPtr _hPanelBrush;\n",
        "    private static IntPtr _hBgBrush => Theme.Brush(CLR_BG);\n"
        "    private static IntPtr _hBannerBgBrush => Theme.Brush(CLR_BANNER_BG);\n"
        "    private static IntPtr _hPanelBrush => Theme.Brush(CLR_PANEL_BG);\n",
    ),
    (
        "fond de classe",
        1,
        "            hbrBackground = _hBgBrush,\n",
        "            // hbrBackground = IntPtr.Zero : une brosse inscrite ici appartient au systeme,\n"
        "            // qui la detruit au desenregistrement de la classe. ApplyClassBackground en\n"
        "            // pose une apres coup, que Theme garde dans son cache.\n"
        "            hbrBackground = IntPtr.Zero,\n",
    ),
    (
        "chrome et bascule de theme",
        1,
        "        Win32.EnableDarkTitleBar(_hWnd);\n",
        "        ThemeWindow.ApplyChrome(_hWnd);\n"
        "        ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);\n"
        "\n"
        "        _themeChanged = () =>\n"
        "        {\n"
        "            if (_hWnd == IntPtr.Zero)\n"
        "                return;\n"
        "            ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);\n"
        "            ThemeWindow.ApplyChrome(_hWnd);\n"
        "        };\n"
        "        Theme.Changed += _themeChanged;\n",
    ),
    (
        "champ de bascule de theme",
        1,
        "    // DPI scaling — mutable, recalculé sur WM_DPICHANGED\n",
        "    private Action? _themeChanged;\n"
        "\n"
        "    // DPI scaling — mutable, recalculé sur WM_DPICHANGED\n",
    ),
    (
        "separateur du header",
        1,
        "        var sepBrush = Win32.CreateSolidBrush(0x00D0D0D0);\n",
        "        var sepBrush = Theme.Brush(Theme.Current.Border);\n",
    ),
    (
        "separateur non detruit",
        1,
        "        Win32.FillRect(hdc, ref sepRect, sepBrush);\n"
        "        Win32.DeleteObject(sepBrush);\n",
        "        // La brosse appartient au cache de Theme : la detruire laisserait un handle mort\n"
        "        // a toutes les fenetres qui demanderont la meme couleur.\n"
        "        Win32.FillRect(hdc, ref sepRect, sepBrush);\n",
    ),
    (
        "survol de lien",
        2,
        "                    Win32.SetTextColor(hdcStatic, isActive ? CLR_LINK_HOVER : CLR_LINK);\n",
        "                    Win32.SetTextColor(hdcStatic, CLR_LINK);\n",
    ),
    (
        "liberation",
        1,
        "        DestroyFonts();\n"
        "        Win32.DeleteObject(_hBgBrush);\n"
        "        Win32.DeleteObject(_hBannerBgBrush);\n"
        "        Win32.DeleteObject(_hPanelBrush);\n",
        "        DestroyFonts();\n"
        "        if (_themeChanged != null)\n"
        "        {\n"
        "            Theme.Changed -= _themeChanged;\n"
        "            _themeChanged = null;\n"
        "        }\n",
    ),
]


def apply(label, expected, anchor, replacement):
    with open(TARGET, "rb") as handle:
        data = handle.read()

    lf_anchor = anchor.encode("utf-8")
    crlf_anchor = anchor.replace("\n", "\r\n").encode("utf-8")
    lf_count = data.count(lf_anchor)
    crlf_count = data.count(crlf_anchor)

    if lf_count + crlf_count != expected:
        sys.exit(
            "%s : ancre trouvee %d fois en LF et %d fois en CRLF, attendu %d au total"
            % (label, lf_count, crlf_count, expected)
        )
    if lf_count and crlf_count:
        sys.exit("%s : ancre presente dans les deux terminaisons, patch refuse" % label)

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
    lf = patched.count(b"\n") - crlf
    print(
        "%-28s %-4s x%d | CRLF %d LF %d | %+d octets"
        % (label, ending, expected, crlf, lf, len(patched) - len(data))
    )


def main():
    for label, expected, anchor, replacement in REPLACEMENTS:
        apply(label, expected, anchor, replacement)


if __name__ == "__main__":
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
    main()
