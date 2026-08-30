"""CH3 passe 2, lot A2 bis — la mise en page réserve l'anneau de focus.

Défaut mesuré sur captures/ch3/a2/ juste après le passage en owner-draw : **tous** les libellés
de case, de radio et de bouton sont amputés verticalement, et le texte des boutons touche leur
bordure.

Cause : ThemeControls dessine un anneau de focus de 4 px (2 d'écart, 2 de trait) à l'extérieur
du contrôle, et TryDrawItem lui passe donc un rectangle rentré de cette marge sur les quatre
côtés — le motif de Couches maintenables. Or la mise en page dimensionnait encore ses lignes sur
la seule hauteur du texte : une case de 20 px offrait 12 px à un glyphe qui en réclame 20. Windows
n'avait pas ce problème parce que son rectangle pointillé se dessine *dans* le contrôle.

MeasureBoxRowWidth comptait déjà cette marge en largeur ; c'est la hauteur qui manquait, et la
largeur des boutons, dont MeasureButtonWidth ne connaît que le rembourrage du texte.
"""

import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]
SETTINGS = ROOT / "src" / "SettingsWindow.cs"

data = SETTINGS.read_bytes()
if data[:3] == b"\xef\xbb\xbf" or data.count(b"\r\n"):
    sys.exit("SettingsWindow.cs n'est plus en LF pur sans BOM")


def replace(name, old, new, expected=1):
    global data
    old_b = old.encode("utf-8")
    found = data.count(old_b)
    if found != expected:
        sys.exit(f"{name} : ancre attendue {expected} fois, trouvée {found}\n---\n{old}\n---")
    data = data.replace(old_b, new.encode("utf-8"))
    print(f"  {name:34s} {expected}×")


replace(
    "hauteur des cases et radios",
    """            int checkboxHeight = Math.Max(S(18), MeasureSingleLineHeight(hdc, _hFontBold));""",
    """            // + la marge de focus des deux côtés : l'anneau se dessine à l'extérieur du
            // contrôle, donc TryDrawItem rend le libellé dans un rectangle rentré d'autant.
            int focusMargin = ThemeControls.FocusMargin(_dpi);
            int checkboxHeight = Math.Max(S(18), MeasureSingleLineHeight(hdc, _hFontBold))
                + focusMargin * 2;""",
)

replace(
    "hauteur des boutons de section",
    """            int buttonHeight = S(28);""",
    """            int buttonHeight = ThemeControls.MeasureButtonHeight(hdc, _hFontButton, _dpi)
                + focusMargin * 2;""",
)

replace(
    "hauteur des boutons de la liste",
    """            var compatAddRect = Rect(labelX, compatListRect.bottom + S(6), compatBtnW, S(24));
            var compatRemoveRect = Rect(labelX + compatBtnW + S(6), compatListRect.bottom + S(6),
                innerWidth - compatBtnW - S(6), S(24));""",
    """            var compatAddRect = Rect(labelX, compatListRect.bottom + S(6), compatBtnW, buttonHeight);
            var compatRemoveRect = Rect(labelX + compatBtnW + S(6), compatListRect.bottom + S(6),
                innerWidth - compatBtnW - S(6), buttonHeight);""",
)

replace(
    "largeur des boutons mesurée",
    """            int Button(string text) =>
                ThemeControls.MeasureButtonWidth(hdc, _hFontButton, text, _dpi);""",
    """            int Button(string text) =>
                ThemeControls.MeasureButtonWidth(hdc, _hFontButton, text, _dpi)
                    + ThemeControls.FocusMargin(_dpi) * 2;""",
)

if data.count(b"\r\n"):
    sys.exit("des CRLF sont apparus — rien n'est écrit")
SETTINGS.write_bytes(data)
print(f"SettingsWindow.cs  {len(data)} octets, {data.count(b'\n')} LF")
