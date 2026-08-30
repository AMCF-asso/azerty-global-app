"""CH3 passe 2, lot A3 — la primitive d'onglet, en jetons existants seulement.

Antoine a tranché le 2026-08-30 : Paramètres passe à trois onglets, parce que la fenêtre
dimensionnée sur son contenu ne tient plus sur l'écran (1466 px de haut à 150 %, 1440
disponibles ici, ~1032 sur un portable 1080p).

Un onglet est une quatrième forme de contrôle, mais **pas une couleur de plus** : actif, il
prend le fond de surface, l'encre et un soulignement d'accent ; inactif, il prend le papier et
le texte secondaire. La charte interdit d'inventer une nuance, pas de composer une forme avec
celles qui existent.

ThemeControls.cs est en LF pur sans BOM.
"""

import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]
CONTROLS = ROOT / "src" / "ThemeControls.cs"

data = CONTROLS.read_bytes()
if data[:3] == b"\xef\xbb\xbf" or data.count(b"\r\n"):
    sys.exit("ThemeControls.cs n'est plus en LF pur sans BOM")


def replace(name, old, new, expected=1):
    global data
    old_b = old.encode("utf-8")
    found = data.count(old_b)
    if found != expected:
        sys.exit(f"{name} : ancre attendue {expected} fois, trouvée {found}\n---\n{old}\n---")
    data = data.replace(old_b, new.encode("utf-8"))
    print(f"  {name:34s} {expected}×")


replace(
    "constantes d'onglet",
    """    /// <summary>Hauteur minimale d'un bouton, et son rembourrage horizontal.</summary>
    internal const int BaseButtonHeight = 32;
    internal const int BaseButtonPadding = 16;""",
    """    /// <summary>Hauteur minimale d'un bouton, et son rembourrage horizontal.</summary>
    internal const int BaseButtonHeight = 32;
    internal const int BaseButtonPadding = 16;

    /// <summary>Rembourrage d'un onglet, et l'épaisseur du trait qui marque l'actif. Le trait
    /// vaut 2 px comme l'anneau de focus : à 1 px il disparaît sur un écran à 150 %, à 3 px il
    /// pèse plus que la bordure d'un bouton primaire.</summary>
    internal const int BaseTabPaddingX = 14;
    internal const int BaseTabPaddingY = 8;
    internal const int BaseTabUnderline = 2;""",
)

replace(
    "DrawTab et ses mesures",
    """    /// <summary>Largeur qu'une case ou une radio doit avoir pour porter son libellé sans le""",
    """    /// <summary>
    /// Un onglet. Quatrième forme de contrôle de la charte, et aucune couleur de plus :
    ///
    ///   - actif : fond de surface, encre, et un trait d'accent sur toute sa largeur en bas ;
    ///   - inactif : fond de papier, texte secondaire, pas de trait ;
    ///   - survolé et inactif : le fond passe à action-fond, la même transition qu'un bouton
    ///     secondaire survolé, pour que le clic se devine avant d'être tenté ;
    ///   - désactivé : texte à <c>Disabled</c>, jamais de survol.
    ///
    /// L'anneau de focus est celui de tous les autres contrôles : la barre d'onglets se
    /// parcourt au clavier comme le reste de la fenêtre.
    /// </summary>
    internal static void DrawTab(IntPtr hdc, Win32.RECT rect, string text, IntPtr font,
        bool active, ControlState state, Palette palette, int dpi)
    {
        bool disabled = state.HasFlag(ControlState.Disabled);
        uint fill = active ? palette.Surface
            : (!disabled && state.HasFlag(ControlState.Hovered)) ? palette.ActionFill
            : palette.Paper;
        uint ink = disabled ? palette.Disabled
            : active ? palette.Ink
            : palette.TextSecondary;

        GdiHelpers.FillSolidRect(hdc, rect, fill);
        DrawCenteredText(hdc, rect, text, font, ink);

        if (active)
        {
            int thickness = Scale(BaseTabUnderline, dpi);
            GdiHelpers.FillSolidRect(hdc, new Win32.RECT
            {
                left = rect.left,
                top = rect.bottom - thickness,
                right = rect.right,
                bottom = rect.bottom,
            }, disabled ? palette.Disabled : palette.Action);
        }

        if (state.HasFlag(ControlState.Focused) && !disabled)
        {
            int inset = FocusMargin(dpi);
            DrawFocusRing(hdc, new Win32.RECT
            {
                left = rect.left + inset,
                top = rect.top + inset,
                right = rect.right - inset,
                bottom = rect.bottom - inset,
            }, palette, dpi);
        }
    }

    /// <summary>Largeur d'un onglet : son libellé et son rembourrage. Les onglets ne sont pas
    /// à largeur égale — un intitulé court n'a aucune raison d'occuper la place du plus
    /// long.</summary>
    internal static int MeasureTabWidth(IntPtr hdc, IntPtr font, string text, int dpi) =>
        GdiHelpers.MeasureSingleLineWidth(hdc, font, text) + 2 * Scale(BaseTabPaddingX, dpi);

    /// <summary>Hauteur d'une barre d'onglets, trait de l'actif compris.</summary>
    internal static int MeasureTabHeight(IntPtr hdc, IntPtr font, int dpi) =>
        GdiHelpers.MeasureSingleLineHeight(hdc, font) + 2 * Scale(BaseTabPaddingY, dpi)
            + Scale(BaseTabUnderline, dpi);

    /// <summary>Largeur qu'une case ou une radio doit avoir pour porter son libellé sans le""",
)

if data.count(b"\r\n"):
    sys.exit("des CRLF sont apparus — rien n'est écrit")
CONTROLS.write_bytes(data)
print(f"ThemeControls.cs  {len(data)} octets, {data.count(b'\n')} LF")
