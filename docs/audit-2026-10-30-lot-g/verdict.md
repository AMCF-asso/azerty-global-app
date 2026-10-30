# Lot G — le message de Paramètres sous le pli à 200 %

**Verdict : hypothèse confirmée.** Mesuré au banc (`AZERTY_CAPTURE_DPI=96,144,192`,
`AZERTY_CAPTURE_ECRAN=1920x1040`, soit un écran 1080p moins la barre des tâches), langue fr.
À 200 %, la zone visible fait 993 px de haut.

| Onglet, message affiché, 200 % | Avant (haut–bas) | Après (haut–bas) |
|---|---|---|
| Général | 1120–1196, 203 px sous le pli | 917–993, visible |
| Applications | 1026–1102, hors zone | 917–993, visible |
| Langue | 917–993, visible | 917–993, visible |
| Leçons | 923–999, 6 px sous le pli | 917–993, visible |

À 100 % (visible 601 px) et à 150 % (902 px) : message visible avant et après, aux mêmes positions.

Cause : le pied de confirmation était ancré au bas du **contenu** (`max(contentEnd, viewport − pied)`),
donc il défilait avec lui. Dès qu'un onglet dépasse la zone visible, le pied est hors de vue.

Correction (`src/SettingsWindow.cs`) : le pied reste collé au bas de la zone visible, au premier
plan, et les contrôles sont écrêtés par leurs voisins pour que son trait de séparation reste entier.
Le défilement vers un contrôle focalisé retire la hauteur du pied.

Preuves : `avant/` et `apres/` (images `parametres-*-message-fr-{100,150,200}.png`,
`position-message.tsv` mesuré par `RecordMessagePosition` dans `CaptureBench.cs`).
