# Prompts ChatGPT — visuels de la fiche Microsoft Store v1.3.0

> Remplacé le 2026-09-23 par `prompt-visuels.md` : A0 + A1 testés dans Gemini, résultat rejeté par Antoine. Conservé comme contre-exemple.

Rédigé le 2026-09-23. Modèle : GPT Image 2.5 dans ChatGPT.
Plan suivi : celui du 2026-09-18 (`../store-v120/prompts-decors.md`), 6 visuels dans l’ordre planche, capture, planche, capture, capture, planche.

## Deux méthodes, à comparer

- **Méthode A — décor seul.** L’IA ne produit que la matière et la lumière. Les textes et les captures sont ajoutés ensuite en HTML. C’est la règle du 2026-09-18.
- **Méthode B — image complète.** L’IA écrit aussi les titres. Test ajouté le 2026-09-23 : on vérifie si elle écrit correctement É, Ç, « » et les symboles.

Utilise **une conversation ChatGPT neuve par méthode**, pour qu’une méthode n’influence pas l’autre.

Dans les deux cas, les images 2, 4 et 5 gardent un **emplacement vide** : la fenêtre de l’app sera une vraie capture, ajoutée par-dessus (clause 10.1 du Store). Aucune IA ne dessine l’interface.

## Obtenir plusieurs choix

Chaque prompt demande 4 variantes (image 1) ou 3 variantes (images 2 à 6), une image par variante.
Si ChatGPT n’en produit qu’une, réponds `Variante suivante.` jusqu’à les avoir toutes.

1. **Image 1 d’abord.** Ses 4 variantes proposent 4 directions artistiques différentes. Tu en choisis une.
2. **Images 2 à 6 ensuite**, dans la direction choisie. Leurs variantes changent la composition, pas la matière : les 6 images restent cohérentes entre elles.

## Où déposer les images retenues

- Méthode A : `store-v130/A/decor-1.png` … `decor-6.png`
- Méthode B : `store-v130/B/complet-1.png` … `complet-6.png`

Le nom de fichier suffit, peu importe la taille de sortie. Le recadrage en 1920 × 1080 se fait après.

---

## Méthode A — décor seul

### A0 — Message d’ouverture (à coller en premier)

```
Je prépare les 6 visuels de la fiche Microsoft Store d’un logiciel Windows : AZERTY Global, une disposition de clavier française améliorée, gratuite et open source, publiée par une association.

Tu vas produire uniquement des DÉCORS : la matière, le fond, la lumière. Les titres, les légendes et les captures d’écran du logiciel seront posés par-dessus plus tard, en dehors de ChatGPT.

Règles pour toutes les images de cette conversation :
- Format paysage 16:9, le plus grand disponible, qualité maximale.
- Jamais de texte, de lettre, de chiffre ni de signe de ponctuation.
- Jamais de clavier, de touche, d’écran, de fenêtre, d’interface, de bouton, d’icône, de logo, de personne.
- Jamais de dégradé synthétique, de halo lumineux, de particules, de formes géométriques flottantes, de bokeh coloré, d’effet « abstrait tech ». Rien ne flotte.
- Le rendu doit ressembler à une vraie photographie de nature morte éditoriale, pas à une image générée ni à un modèle de présentation.
- De grandes zones calmes et unies : du texte y sera posé.
- Quand je demande plusieurs variantes, produis une image par variante et annonce la lettre de chaque variante dans ta réponse, jamais dans l’image.

Réponds seulement « Prêt » et attends mon premier prompt.
```

### A1 — Planche « le geste plutôt que le résultat »

```
Image 1 sur 6. C’est la vignette de la fiche : elle doit donner envie de cliquer sans rien crier.

Composition : deux colonnes. La moitié droite doit rester la plus unie possible, un tableau comparatif y sera posé.

Propose 4 variantes, chacune dans une direction artistique différente :
A — Papier mat épais, blanc cassé chaud tirant sur le sable, grain visible. Lumière rasante douce venant du haut à gauche. Un pli diagonal très léger accroche la lumière.
B — Toile de lin écru tendue, trame fine. Lumière naturelle de fenêtre, latérale, ombres très douces.
C — Plâtre ou enduit mat, gris chaud très clair, surface légèrement irrégulière. Lumière de fin d’après-midi.
D — Bois clair (chêne ou frêne) finement veiné, vu de dessus, avec une feuille de papier posée qui occupe les deux tiers droits du cadre.

Vue de dessus pour toutes. Aucune de ces variantes ne doit contenir le moindre élément de la liste interdite.
```

Note ici la lettre retenue. Elle sert dans les prompts suivants.

### A2 — Capture « clavier virtuel, 4 couches »

```
Image 2 sur 6. Reste exactement dans la direction de la variante [LETTRE] de l’image 1 : même matière, même lumière, même teinte.

Une vraie capture d’écran du clavier virtuel sera posée au centre, avec une ombre portée douce. Le centre du cadre doit donc rester uni, sur environ 70 % de la largeur.

Propose 3 variantes de composition :
A — Une bande de bois sombre (noyer), floue, visible le long du bord inférieur.
B — La surface seule, un coin de la matière légèrement soulevé en haut à gauche.
C — La lumière tombe un peu plus fort au centre, les bords s’assombrissent très légèrement.
```

### A3 — Planche « cinq changements »

```
Image 3 sur 6. Même direction que la variante [LETTRE] de l’image 1.

Composition : une liste de cinq lignes sera posée à gauche, un encart à droite. Deux zones calmes, gauche et droite.

Propose 3 variantes de composition :
A — Surface nue et régulière, le coin supérieur gauche soulevé de quelques millimètres avec une petite ombre douce.
B — Surface nue, un pli vertical très discret au tiers droit du cadre.
C — Surface nue, lumière un peu plus rasante qui fait ressortir le grain.
```

### A4 — Capture « recherche de caractères »

```
Image 4 sur 6. Même direction que la variante [LETTRE] de l’image 1.

Une vraie capture d’écran de la recherche de caractères sera posée légèrement à gauche du centre. Garde cette zone unie.

Propose 3 variantes de composition :
A — Une bande de bois sombre, floue, le long du bord droit.
B — La surface seule, lumière venant du haut à droite cette fois.
C — Un léger décalage de matière : deux feuilles superposées, la seconde dépasse à peine en bas à droite.
```

### A5 — Capture « défi du jour »

```
Image 5 sur 6. Même direction que la variante [LETTRE] de l’image 1.

Une vraie capture d’écran du défi du jour sera posée légèrement à droite du centre. Garde cette zone unie.

Propose 3 variantes de composition :
A — Une bande de bois sombre, floue, le long du bord gauche.
B — La surface seule, lumière un peu plus vive, comme en matinée.
C — Une ombre douce et floue de feuillage traverse le coin supérieur droit, très atténuée.
```

### A6 — Planche « vos frappes ne quittent jamais votre machine »

```
Image 6 sur 6, celle qui clôt la série. Même direction que la variante [LETTRE] de l’image 1, mais légèrement plus sombre et plus douce, comme photographiée plus tard dans la journée.

Composition : un titre et quatre lignes courtes seront posés à gauche. La moitié gauche reste unie.

Propose 3 variantes :
A — Lumière de fin de journée, chaude, ombres allongées.
B — Lumière froide et diffuse, comme un ciel couvert.
C — Pénombre douce, une seule zone éclairée à gauche où le texte sera posé.
```

---

## Méthode B — image complète

Les textes ci-dessous reprennent ceux des planches du 2026-09-18. Ils sont vérifiés sur la source du logiciel (`src/character-index.json`) : É = Verr. Maj + é, Ç = Verr. Maj + ç, « = AltGr + W.

À contrôler sur chaque image reçue, caractère par caractère : É, È, Ç, À, « », ², @, #, { } [ ] | \, ´ et `. Une seule erreur disqualifie l’image : la fiche parle de typographie française.

### B0 — Message d’ouverture (à coller en premier)

```
Je prépare les 6 visuels de la fiche Microsoft Store d’un logiciel Windows : AZERTY Global, une disposition de clavier française améliorée, gratuite et open source, publiée par une association.

Tu vas produire des visuels complets, avec leurs titres. Les textes que je te donne doivent apparaître exactement, lettre pour lettre, accents et ponctuation compris. N’ajoute aucun autre texte.

Règles pour toutes les images de cette conversation :
- Format paysage 16:9, le plus grand disponible, qualité maximale.
- Typographie éditoriale : une seule famille de caractères, sobre, lisible en petite vignette. Pas de police fantaisie.
- Jamais de clavier, de touche, d’écran, de fenêtre, d’interface ni de logo dessinés. Quand je parle d’une capture, laisse un emplacement uni et vide : la vraie capture sera ajoutée ensuite.
- Interdits, parce qu’ils font « modèle de présentation » : cartes blanches à coins arrondis, accent bleu Windows, gros chiffre en couleur, séparateurs gris, pied de page répété.
- Interdits, parce qu’ils font « image générée » : dégradé synthétique, halo, particules, formes flottantes, bokeh coloré.
- Fond : une vraie matière photographiée (papier, toile, plâtre ou bois), une seule lumière douce.
- Peu d’éléments. L’image doit se lire en deux secondes.
- Quand je demande plusieurs variantes, produis une image par variante et annonce la lettre de chaque variante dans ta réponse, jamais dans l’image.

Réponds seulement « Prêt » et attends mon premier prompt.
```

### B1 — Planche « le geste plutôt que le résultat »

```
Image 1 sur 6, la vignette de la fiche.

Texte exact :
Titre : Les mêmes lettres. Moins de gestes.
Puis trois lignes de comparaison :
É   Alt + 0201   →   Verr. Maj + é
Ç   Alt + 0199   →   Verr. Maj + ç
«   Alt + 0171   →   AltGr + W

Propose 4 variantes, chacune dans une direction artistique différente :
A — Papier mat blanc cassé chaud, lumière rasante du haut à gauche.
B — Toile de lin écru, lumière de fenêtre latérale.
C — Plâtre mat gris chaud très clair, lumière de fin d’après-midi.
D — Bois clair vu de dessus, une feuille de papier posée porte le texte.
```

Note ici la lettre retenue.

### B2 — Capture « clavier virtuel, 4 couches »

```
Image 2 sur 6. Même direction que la variante [LETTRE] de l’image 1.

Texte exact, en haut, sur une ligne :
Les quatre couches, d’un coup d’œil

Sous le texte, un grand emplacement uni et vide, environ 70 % de la largeur, pour une capture d’écran ajoutée ensuite. N’y dessine rien.

Propose 3 variantes de composition : texte centré, texte aligné à gauche, texte en bas.
```

### B3 — Planche « cinq changements »

```
Image 3 sur 6. Même direction que la variante [LETTRE] de l’image 1.

Texte exact :
Titre : Cinq changements, c’est tout.
Puis cinq lignes :
1. É È Ç À en un appui
2. Le point en accès direct
3. @ et # sur la touche ²
4. { } [ ] | \ sur la rangée de repos
5. ´ et ` sur la touche ù

Propose 3 variantes de composition : liste à gauche et grand vide à droite, liste centrée, titre en grand à gauche et liste à droite.
```

### B4 — Capture « recherche de caractères »

```
Image 4 sur 6. Même direction que la variante [LETTRE] de l’image 1.

Texte exact, sur une ligne :
Tapez un nom, trouvez le symbole

Laisse un grand emplacement uni et vide, légèrement à gauche du centre, pour une capture d’écran ajoutée ensuite. N’y dessine rien.

Propose 3 variantes de composition : texte en haut, texte à droite de l’emplacement, texte en bas.
```

### B5 — Capture « défi du jour »

```
Image 5 sur 6. Même direction que la variante [LETTRE] de l’image 1.

Texte exact, sur une ligne :
Chaque jour, un nouveau défi

Laisse un grand emplacement uni et vide, légèrement à droite du centre, pour une capture d’écran ajoutée ensuite. N’y dessine rien.

Propose 3 variantes de composition : texte en haut, texte à gauche de l’emplacement, texte en bas.
```

### B6 — Planche « vos frappes ne quittent jamais votre machine »

```
Image 6 sur 6, celle qui clôt la série. Même direction que la variante [LETTRE] de l’image 1, légèrement plus sombre, comme photographiée plus tard dans la journée.

Texte exact :
Titre : Vos frappes ne quittent jamais votre machine.
Puis quatre lignes courtes :
Aucune télémétrie
Aucun compte
Aucun envoi réseau automatique
Aucune frappe journalisée

Propose 3 variantes : lumière chaude de fin de journée, lumière froide de ciel couvert, pénombre avec le texte seul éclairé.
```

---

## Après la génération

1. Déposer les images retenues dans `A/` et `B/`.
2. Refaire les vraies captures de la 1.3.0 : clavier virtuel, recherche de caractères, défi du jour. Fenêtre seule, fond uni. Les captures actuelles datent de mars à juin 2026 (point AG130-50).
3. Composer la méthode A en HTML, poser les captures dans les emplacements de la méthode B, puis comparer les deux jeux côte à côte avant de choisir.
4. Jeu anglais : pour la méthode A, même décor et seule la couche HTML change. Pour la méthode B, il faut une nouvelle génération avec les textes anglais.
