# Décors IA — fiche Microsoft Store AZERTY Global v1.2.0

Décidé le 2026-09-18. Modèle retenu : **GPT Image 2.5** (sorti le 2026-09-08, inclus dans ChatGPT).

## Règle de partage

L'IA ne produit **que le décor**. Elle ne produit jamais :

- de texte, de lettre, de chiffre, de signe de ponctuation ;
- de clavier, de touche, de glyphe ;
- d'interface, de fenêtre, de bouton, d'icône ;
- de logo, de marque, de visage.

Tout le contenu — titres, légendes, É, Ç, « », captures de l'app — est composé **par-dessus**, en HTML, rendu par Edge. C'est ce qui garantit que les diacritiques sont exactes : aucune source ne documente le rendu des accents français par ces modèles, donc on ne parie pas dessus.

## Direction artistique commune

Une seule matière sur les 6 images, une seule lumière. C'est la cohérence qui distingue une direction artistique d'une série de générations.

- **Matière** : papier mat épais, légèrement texturé, grain visible de près, aucun motif régulier.
- **Teinte** : blanc cassé chaud virant très légèrement au gris sable. Pas de blanc pur, pas de bleu.
- **Lumière** : rasante, venant du haut à gauche, douce, une seule source. Ombres longues et très peu contrastées.
- **Profondeur** : léger flou dans les angles les plus éloignés, comme une vraie prise de vue à grande ouverture.
- **Interdit** : dégradé synthétique, halo lumineux, particules, formes géométriques flottantes, bokeh coloré, effet « abstrait tech ». Ce sont les marqueurs qui font « IA ».

## Prompt de base (à coller tel quel, puis compléter par la variante)

```
A photograph of a sheet of thick matte paper filling the entire frame, shot
straight down from above. Warm off-white paper, slightly sandy undertone, fine
visible grain and subtle fibre texture. Single soft light source from the upper
left, grazing across the surface, creating very low-contrast shading. Shallow
depth of field: the far corners fall very slightly out of focus. Natural,
restrained, editorial still-life photography. Landscape orientation, 16:9.

Absolutely no text, no letters, no numbers, no punctuation marks. No keyboard,
no keys, no keycaps. No screen, no window, no user interface, no buttons, no
icons. No logo. No people. No abstract glowing shapes, no particles, no
gradient overlays, no lens flare. Nothing floating.
```

## Les 6 variantes

L'ordre est celui de la fiche : planche, capture, planche, capture, capture, planche.

### 1 — Planche « le geste plutôt que le résultat »
Ajouter au prompt de base :
```
The paper is completely bare and even, with a very faint diagonal crease running
from the lower left towards the upper right, catching the light.
```
Zone de composition : deux colonnes. Garder la moitié droite la plus unie possible.

### 2 — Capture « clavier virtuel, 4 couches »
```
The sheet of paper rests on a dark walnut desk surface; a narrow band of the
wood is visible along the bottom edge of the frame, softly out of focus.
```
La fenêtre de l'app se pose au centre, ombre portée douce. La bande de bois donne l'assise.

### 3 — Planche « cinq changements »
```
The paper is bare and even. Its upper left corner is lifted a few millimetres,
casting a small soft shadow.
```
Zone de composition : liste à gauche, encart à droite.

### 4 — Capture « recherche de caractères »
```
The sheet of paper rests on a dark walnut desk surface; a narrow band of the
wood is visible along the right edge of the frame, softly out of focus.
```

### 5 — Capture « défi du jour »
```
The sheet of paper rests on a dark walnut desk surface; a narrow band of the
wood is visible along the left edge of the frame, softly out of focus.
```

### 6 — Planche « vos frappes ne quittent jamais votre machine »
```
The paper is bare, even, and slightly darker overall, lit more softly, as if
photographed later in the day.
```
Assombrir légèrement ferme la série.

## Chaîne de production

1. Générer les 6 décors dans ChatGPT, format paysage, qualité la plus haute.
2. Recadrer / agrandir en **1920×1080** exact. ⚠️ À contrôler au premier rendu : la taille native de sortie du modèle n'est pas documentée sur sa page officielle ; si le décor sort en 1536 de large, l'agrandissement est acceptable **parce qu'il n'y a aucun détail fin** — un décor chargé ne passerait pas.
3. Déposer les PNG ici sous `decor-1.png` … `decor-6.png`.
4. `Build-StoreScreenshots.ps1` compose par-dessus : capture de l'app, titres, légendes, puis rend en PNG par Edge headless.
5. Jeux FR et EN : le décor est **le même**, seule la couche HTML change. 12 fichiers, 6 décors.

## Garde-fou Microsoft Store

Policy v7.20, clause 10.1 (vérifiée le 2026-09-18) : les captures doivent refléter fidèlement le logiciel. Le décor généré est un habillage, la fenêtre montrée est une vraie capture — la clause est respectée. Aucune obligation d'étiquetage « généré par IA » ne s'applique aux visuels de fiche.
