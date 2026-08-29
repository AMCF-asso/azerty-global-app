# -*- coding: utf-8 -*-
"""Ajoute la rangee des compteurs a la planche d'etats. Fichier en LF pur."""
import sys
from pathlib import Path

PATH = Path(r"D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store"
            r"\src\AZERTYGlobal.Tests\StatesBoard.cs")

PATCHES = [
    ('            "Bouton radio",\n            "Lien",\n',
     '            "Bouton radio",\n            "Compteur — et +",\n            "Lien",\n'),

    ('''                    case 4:
                        ThemeControls.DrawRadio(hdc, cell, "Français", body,
                            state | (c == 0 ? ControlState.Checked : ControlState.None),
                            palette, Dpi);
                        break;''',
     '''                    case 4:
                        ThemeControls.DrawRadio(hdc, cell, "Français", body,
                            state | (c == 0 ? ControlState.Checked : ControlState.None),
                            palette, Dpi);
                        break;
                    case 5:
                        // Les deux compteurs partagent la cellule : ils se lisent en paire, et
                        // c'est leur taille l'un par rapport au champ qui est en jeu.
                        ThemeControls.DrawStepperButton(hdc, Square(cell, 0), state, palette,
                            Dpi, adding: false);
                        ThemeControls.DrawStepperButton(hdc, Square(cell, 1), state, palette,
                            Dpi, adding: true);
                        break;'''),

    ('''    /// <summary>L'anneau de focus déborde de 4 px : la cellule lui réserve sa marge.</summary>''',
     '''    /// <summary>Un carré de la hauteur d'un champ, place <paramref name="index"/> dans la
    /// cellule — c'est la taille réelle d'un compteur à 96 DPI.</summary>
    private static Win32.RECT Square(Win32.RECT cell, int index) => new()
    {
        left = cell.left + index * 36,
        top = cell.top,
        right = cell.left + index * 36 + 28,
        bottom = cell.top + 28,
    };

    /// <summary>L'anneau de focus déborde de 4 px : la cellule lui réserve sa marge.</summary>'''),
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
            print(f"        {old.splitlines()[0][:90]!r}")
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
