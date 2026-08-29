# -*- coding: utf-8 -*-
"""Met a jour les deux lignes periemees du bloc memoire-projet de Keyboard Layouts."""
import sys
from pathlib import Path

PATH = Path(r"D:\My files\Keyboard Layouts\CLAUDE.md")

OLD_ABOUT = (
    "- **⚠️ Seconde `AboutWindow` = handle nul** — seule la première d'un processus crée sa "
    "fenêtre ; échelle, délai et classe de fenêtre écartés ; `TrayApplication` la recrée à chaque "
    "changement de langue — à reproduire sur l'app packagée · "
    "`aboutwindow-seconde-instance-handle-nul`"
)
NEW_ABOUT = (
    "- **⚠️ Une brosse de fond de classe appartient au système** — il la détruit au "
    "`UnregisterClassW`, donc en tirer une d'un cache partagé laisse un handle mort à toutes les "
    "fenêtres suivantes : fond blanc, étiquettes grises, `CreateWindowExW` nul ; ✅ corrigé le "
    "2026-08-29, l'instrument est `GetObjectType` · "
    "`brosse-de-classe-detruite-au-desenregistrement`"
)

OLD_TAIL = (
    "⛔ le contrôle visuel passe par le banc de captures, jamais par le lancement de l'exe ; "
    "⏸ gris système résiduel dans Durée de pause · `refonte-app-azerty-v120-plan`"
)
NEW_TAIL = (
    "⛔ le contrôle visuel passe par les bancs `CaptureBench` (fenêtres) et `StatesBoard` (états "
    "des contrôles), jamais par le lancement de l'exe ; ✅ fond blanc de Durée de pause corrigé le "
    "2026-08-29 (§12 du plan), 18/108/354 verts ; ⏭️ arrêt visuel des 3 points candidats · "
    "`refonte-app-azerty-v120-plan`"
)


def main():
    text = PATH.read_text(encoding="utf-8")
    for label, old in (("About", OLD_ABOUT), ("plan", OLD_TAIL)):
        if text.count(old) != 1:
            print(f"REFUS : ancre {label} trouvee {text.count(old)} fois")
            return 1
    text = text.replace(OLD_ABOUT, NEW_ABOUT, 1).replace(OLD_TAIL, NEW_TAIL, 1)
    PATH.write_text(text, encoding="utf-8")
    print(f"ecrit : {PATH.stat().st_size} octets")
    return 0


if __name__ == "__main__":
    sys.exit(main())
