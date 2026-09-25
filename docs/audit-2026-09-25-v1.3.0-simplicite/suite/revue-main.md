# Revue de `main` (5ee46c8) pour la 1.3.0 — 2026-09-25

Revue en lecture seule du dépôt `components/microsoft-store` : `git show`, `log`, `cherry`, `grep` sur les refs, aucune écriture dans le dépôt. Base commune : `8775489`. Release : `release/1.2.0-notation-store` à `4e0687b`. Les fichiers exportés pour la lecture sont dans `src-main/` et `src-rel/`, à côté de ce fichier.

**Limite.** Deux vérifications déléguées (équivalences fines des correctifs, portabilité détaillée du banc) n'avaient pas rendu leurs résultats à la remise. Les statuts ci-dessous marqués **(vérifié)** viennent de mes propres commandes. Les autres sont des lectures de messages de commit et de code, marquées **(à confirmer)**.

**Preuve de départ.** `git cherry -v release main 8775489` marque les 47 commits non fusionnés de `main` « + » : aucun n'a de patch identique sur la release. `git cherry release origin/main main` marque `c06d6c1` et `7a3a3ef` « - » : ils ont un équivalent sur la release. Il marque `a148f26` « + ».

## 1. Les 49 commits de `main` depuis 8775489

Légende : **a** = correctif ou test neutre ; **b** = charte ou visuel ; **c** = docs, CI, outils, données ; **m** = fusion sans contenu propre (comptée en c).

| sha | date | sujet | cl. | Dans la 1.3.0 ? / remarque |
|---|---|---|---|---|
| b06edfe | 08-03 | Documente que conversionCount n'est jamais renseigné | c | README store-analytics ; à confirmer |
| 0b145c3 | 08-03 | Documente que l'archivage Azure n'est possible que depuis main | c | idem |
| 1eae109 | 08-28 | Every colour… one table (Theme.cs, CH0) | b | absent (Theme.cs n'existe pas sur la release) |
| f75409b | 08-28 | The witness reads both counters… | c | outil de CH0 ; inutile sans la charte |
| 51d9645 | 08-28 | Buttons, boxes, spinners and links… (ThemeControls.cs) | b | absent |
| 03cc3a8 | 08-28 | Every window can now ask for the same chrome, icon and scale (ThemeWindow.cs) | b | absent ; parties neutres réutilisables, voir §2 |
| ece8e1b | 08-28 | The tray window listens for the theme switch… | b | n'a de sens qu'avec la charte |
| 8e89e1d | 08-28 | The About window… charter | b | absent |
| 7fdf0ff | 08-28 | The pause dialog stops measuring itself in raw pixels | b | l'absence d'échelle est déjà corrigée autrement sur la release (modèle D1, `PauseDurationDialog.cs:135-199`) |
| 5d5ada4 | 08-28 | A bench renders the windows… (CaptureBench.cs) | c | banc absent de la release (vérifié : aucun `AZERTY_CAPTURE` ni `PrintWindow`) ; voir §4 |
| f9e7a8e | 08-29 | A window's background brush no longer comes from the shared cache | a* | corrige le propre helper de 03cc3a8 : sans objet sur la release, qui n'a pas de cache. À prendre seulement avec `ApplyClassBackground`. La release a un défaut voisin, le double `DeleteObject` relevé par X-01. |
| c27b9a9 | 08-29 | A board renders the control states… (StatesBoard.cs) | c | dépend de ThemeControls : non portable seul |
| 86e0741 | 08-29 | The pause dialog counts with a minus and a plus… | b | change l'apparence (−/+ pleine hauteur, anneau de focus) ; la release garde ▲▼, nommés par N10 |
| 65b5baf | 08-29 | The maintainable layers window… charter | b | l'échelle est déjà traitée par D1 sur la release |
| 6f296f9 | 08-29 | The statistics window loses the phantom orange… | b | absent |
| fa22664 | 08-29 | The layout conflict window… charter | b | absent |
| 6c43c9d | 08-29 | Two windows take their DPI from the bench override… | c | crochet du banc (passe par `ThemeWindow.DpiOf`) |
| 8ed71e1 | 08-29 | The bench renders the two windows of CH3 | c | banc et scripts de patch |
| 870163d | 08-29 | The settings window… colours and fonts off the charter | b | absent |
| 507bfff | 08-29 | The onboarding window… charter | b | absent |
| 3d158aa | 08-30 | The settings window sizes itself on its content… | b | la release a sa propre version de Paramètres (54ef49e, trois onglets 1.3.0) |
| d3da063 | 08-30 | The settings window paints its own controls… | b | absent |
| 475dc36 | 08-30 | The settings window splits into three tabs… | b | déjà fait autrement sur la release (54ef49e, « Paramètres en trois onglets ») |
| bea44be | 08-30 | The welcome window drops its own scale… | b | absent |
| ca374e0 | 08-30 | The welcome window paints its own buttons and boxes | b | absent |
| 4599e5a | 08-30 | The seven windows fit on a 1920x1080 screen up to 175 percent | b | la release borne autrement (`WindowSizing.cs`, AG130-42, D3) |
| dca2cc8 | 08-31 | The keyboard engine reads the charter's tokens… | b | absent |
| 1911581 | 09-01 | fix: preserve every fast lesson keystroke | **a** | **absent (vérifié : pas de `PhysicalTextInputBuffer` sur la release)**. Correctif réel à reporter. Conflits probables avec LessonsWindow et LearningModule, modifiés de 237 et 537 lignes sur la release. |
| 885d340 | 09-02 | Merge remote-tracking branch 'origin/main' | m | fusionne b06edfe et 0b145c3 |
| 1ed26cc | 09-02 | ci: run the pipeline on ci/** branches | c | déclencheur pour `ci/verif` (travail 2.0.0) ; à confirmer sur la release, utile seulement si ce banc CI sert à la 1.3.0 |
| f3b4dc7 | 09-02 | fix: solde la dette A1 avant CH5 | b (en partie a) | surtout la charte : jetons de l'Accueil, `S()` via `ThemeControls.Scale`, qui change la géométrie de 1 à 20 px. Les parties neutres (WS_CLIPCHILDREN sur À propos, Conflit et Stats, DrawFieldFrame appelé deux fois, deux bools morts) visent le code de `main` ; à refaire à la main si utile, pas à reporter tel quel. |
| ffb66b9 | 09-02 | feat: CH4a — profils d'affichage… et banc | b | absent |
| bbc8404 | 09-02 | feat: CH4a arbitré — police par couche active… | b | absent |
| 1b025d0 | 09-03 | fix: priorité de l'avis J+7… survit au redémarrage | **a** | **déjà dans la release sous une autre forme (vérifié)** : `ReviewPromptLastShown` persisté, lu par `TrainingReminders.cs:29,63` et `ReviewSharePrompt.cs:92`. Rien à reporter. |
| 89b021f | 09-03 | docs: recale les étapes 4, 5 et 6 du smoke test v1.2.0 | c | document v1.2.0, périmé pour la 1.3.0 |
| fb0ca3b | 09-03 | RepositoryUrl : AMCF-asso/azerty-global-app | **a** | **déjà dans la release (vérifié : `ProductIdentity.cs:49`)** |
| 1013d2b | 09-05 | Keep Store analytics archives in private storage only | c | à confirmer ; c06d6c1 et 7a3a3ef (origin/main) ont un équivalent sur la release d'après `git cherry` |
| 949fec8 | 09-05 | docs: cahier des charges au niveau du programme v2 | c | document 2.0.0 ; hors 1.3.0 |
| 38b49d7 | 09-05 | docs: exigences du lot 1.2.0 dans le §7 de la 2.0.0 | c | idem |
| 073fc7f | 09-07 | merge: absorbe 1013d2b | m | fusionne 1013d2b |
| 4542a28 | 09-07 | chore: ignore Archives/ | c | **absent de la release (vérifié : `git status` montre `?? Archives/` sur la release)**. Une ligne, sans risque. |
| fb0b854 | 09-07 | feat: CH4b — le clavier virtuel passe sur le moteur unifié | b | absent ; voir §3 |
| 159a6b2 | 09-07 | test: le banc rend le clavier virtuel… et la CI remonte les PNG | c | banc de CH4b et job CI « maquettes » ; dépend de CH4b |
| 81c9d6c | 09-07 | fix: les polices du clavier virtuel suivent le profil | b | propre à CH4b |
| c8b9eb4 | 09-07 | test: planche de calibrage du glyphe principal | b | propre à CH4b, retiré par b8dcdda |
| cd62d07 | 09-07 | test: planche de calibrage des sous-glyphes | b | idem |
| b8dcdda | 09-07 | feat: CH4b clos — la carte des trois couches | b | absent |
| 66602db | 09-07 | sync: src/lessons.json… « course aux 30 millions » | c (données) | présent sur la release sous une autre forme : `src/lessons.json` +94 lignes depuis 8775489, comme 66602db, et témoin à 34/73/79 (point suivant). Équivalent de a148f26 d'origin/main. Blob identique non vérifié. |
| 5ee46c8 | 09-07 | test: le témoin du catalogue compte le module | a | **déjà dans la release (vérifié : `LessonCoreTests.cs:15-17` = 34, 73, 79)** |

**Décompte : a = 5** (1911581, 1b025d0, fb0ca3b, 5ee46c8, f9e7a8e conditionnel) ; **b = 27** ; **c = 17** (dont 2 fusions). Total 49.

**Réellement à reporter parmi les a et c** : 1911581 (frappes rapides des leçons, correctif utilisateur réel) et 4542a28 (.gitignore). 1b025d0, fb0ca3b, 5ee46c8 et le module de lessons.json y sont déjà. f9e7a8e ne vaut qu'avec ThemeWindow.

**Commits d'origin/main absents de main** : a148f26 (lessons.json, même contenu que 66602db d'après son sujet ; le module est déjà sur la release) ; c06d6c1 et 7a3a3ef (store-analytics : équivalents sur la release d'après `git cherry`, sha non relevé).

## 2. Socle de fenêtre : reprendre ThemeWindow sans changer un pixel ?

### Contenu de `main:src/ThemeWindow.cs` (374 lignes : 03cc3a8, f9e7a8e, 5d5ada4)

| Fonction | Dépend de la charte ? | Pixel identique sur la release ? |
|---|---|---|
| `ApplyChrome` / `CaptionColors` | **oui** : `Palette`, `Theme.Current`, `Theme.Variant`, `Theme.IsHighContrast` | **non**. La release force la barre sombre partout (`Win32.cs:550-556`), y compris sur les six fenêtres à fond `#DDDDDD` (À propos, Paramètres, Stats, Conflit, Accueil, Couches). `ApplyChrome` suit le thème Windows et pose `DWMWA_CAPTION_COLOR` = `Paper` sous Windows 11. Le `SWP_FRAMECHANGED` peut aussi rendre sombre la barre des Leçons, décrite claire sous OS sombre (message de 03cc3a8). |
| `ApplyClassBackground` / `ForgetClassBackground` | non (prend un COLORREF) | **oui** avec le `CLR_BG` actuel. C'est la règle de X-01 : classe sans brosse, brosse par fenêtre, que Windows détruit au désenregistrement. |
| `ApplyProductIcon` (+ `MaskByteCount`) | non | **non strictement**. La release pose l'icône sur 2 fenêtres, en 32 px unique (`SetWindowIcon`). ThemeWindow rend les tailles système et la pose partout : les 8 autres fenêtres gagneraient une icône visible. |
| `DpiOf` + `OverrideDpiForTests` | non | oui. Équivaut à `Win32.GetDpiForWindowOrDefault` (`Win32.cs:677`), plus le crochet du banc. |
| `ApplyDpiChange` | non | oui. Équivaut au bloc `WM_DPICHANGED` D1 (Pause:350-361, Couches:311-322). |
| `ReadImmersiveDarkMode` | non | sans effet (instrument) |

**Dépendances.**
- **Version complète** : `Theme.cs` (586 lignes), plus 115 lignes de Win32 et `ThemeTests`, pour la seule `ApplyChrome`.
- **Version sans charte** : aucune dépendance à Theme.cs. Il faut ajouter à `release:Win32.cs` : `SetClassLongPtrW`, `GCLP_HBRBACKGROUND`, `RedrawWindow` et ses `RDW_*`, `SM_CXICON`/`SM_CXSMICON`, `WM_SETICON`/`ICON_*` (vérifié : absents). GDI+, `CreateIconIndirect`, `ICONINFO`, `GdiImageLoader` et `LogoResourceName` y sont déjà.
- **Tests** : `ThemeWindowTests` se porte, sauf les deux tests de barre et le témoin du cache, qui lisent `Theme`.

**Part du squelette X-01 couverte : environ 11 % (estimation).** X-01 compte 463 lignes en trop : création et CreateControls 171, en-tête de WndProc 98, WM_GETDLGCODE 92, LinkSubclassProc 50, DPI initial 28, SetWindowIcon 24. ThemeWindow ne couvre que SetWindowIcon, une partie du DPI et la règle de brosse, soit environ 50 lignes. La preuve est sur `main` : `AboutWindow.cs` y garde `GetDeviceCaps(…, 88)` (l. 98), `GdiplusStartup` (l. 103), `RegisterClassExW` (l. 190) et `UnregisterClassW` (l. 690). La barre sombre est déjà factorisée sur la release (`EnableDarkTitleBar`). Les polices partagées de `main` viennent de `Theme.Font(FontRole)` : ce sont des tailles de la charte, donc des pixels changés.

**Recommandation : un socle neuf, plus petit, sur le modèle D1, qui reprend quatre fonctions neutres de ThemeWindow.**
- **Base D1**, déjà dans la release et déjà dupliquée par Pause et Couches : `_dpi`, `_layout` à 96 DPI, `S()` = `WindowSizing.ScaleForDpi`, `FitWindowToDpi`, `ApplyDpiToControls`, `GetWorkArea`.
- **À copier de ThemeWindow** : `DpiOf` + `OverrideDpiForTests` (indispensable au banc), `ApplyDpiChange`, `ApplyClassBackground`/`Forget`. `ApplyProductIcon` seulement si Antoine accepte l'icône sur les 8 fenêtres qui n'en ont pas.
- **À ajouter pour couvrir X-01** : `RegisterOnce` (hbrBackground nul et retour vérifié), `CreateCentered`, `PaintBuffer`, `FontSet`, sous-classe de lien partagée, désinscription avant `DestroyWindow`.
- **À laisser dans l'archive** : `ApplyChrome`, `CaptionColors`, Theme.cs.

## 3. Rendu du clavier

### 3.1 Ce que CH4b change visuellement (messages, `main:VirtualKeyboard.cs:260-360, 1016-1070`, captures `operations/refonte-app/captures/ch4b/`)

1. **Contenu** : un glyphe centré par touche devient la **carte des trois couches** (Base et Maj en haut à gauche, AltGr en bas à droite, Maj+AltGr en haut à droite). La couche active est en grande police (S4-2).
2. **Tailles** : glyphe principal à 0,62 × l'échelle au lieu de 0,72 (planche c8b9eb4 : 0,40 jugé « trop timide », puis 0,52 et 0,62). Sous-glyphes à 0,42 (planche cd62d07 : 0,26 trop petit pour les symboles AltGr ; 0,34, 0,40, 0,46 ; 0,42 retenu). Libellés 0,30 → 0,24, touches contextuelles 0,35 → 0,30.
3. **Couleurs** : les 18 COLORREF d'un thème sombre unique (fond `#181C20`, touches `#384048`) cèdent la place aux jetons de la charte, et le clavier **suit le thème Windows** (clair : ivoire et touches blanches ; sombre : brun `#3E382F`). Deux couleurs de texte : l'orange des touches mortes disparaît au profit du cercle pointillé.
4. **Surlignage** : les 4 couleurs avec fond (direct, armement, étape 1, étape 2) deviennent la table B (succès, avertissement, action). La pastille 1/2 est supprimée.
5. **Forme et états** : touches rectangulaires au lieu d'arrondies à 6 px. Touche enfoncée en ActionFill au lieu de bleu clair. Verr. Maj. passe du fond orange à « modifieur actif » avec barre. Libellé jaune de touche morte → texte-2. Nom de la touche morte aligné à droite au lieu de centré en orange.
6. **Géométrie** : cadre du moteur (`ToRect`, −1 px). Le survol passe par `BuildHitTestRects`.
7. **Reste ouvert** (Antoine, 07/09) : cercles pointillés mal placés.
8. **Constat de relecture** : le profil « glyphe unique » de fb0b854, censé rendre l'ancien clavier, alignait le glyphe à droite (`alignLeft: false`, donc `DT_RIGHT`). La capture `apres/clavier-virtuel-actif-*.png` le montre décalé. CH4b n'a jamais reproduit l'ancien rendu à l'identique.

La carte a été choisie le 07/09 sous la charte. S6-1 (23/09) rouvre la direction visuelle : **demander à Antoine avant toute carte en 1.3.0.**

### 3.2 Le clavier virtuel de la release sur `KeyboardRenderer`, sans CH4b ni charte

`release:KeyboardRenderer.cs` est celui de 8775489, plus `L.Keyboard_KeyCap`. Il ne dessine que des cartes en quadrants : palette grise, actif bleu, touche morte rouge, touches rectangulaires, surlignage en contour vert seul.

- **Rendu identique : possible, mais le gain s'annule.** Il faut ajouter au moteur :
  - un profil glyphe unique centré ;
  - une palette en paramètre (les 18 couleurs) ;
  - `RoundRect` ;
  - 4 surlignages avec fond et la pastille ronde ;
  - Verr. Maj. plein ;
  - le texte sombre sur touche enfoncée ;
  - le libellé jaune et la sortie orange en touche morte ;
  - `DisplayGlyph.ForStandaloneMark` ;
  - la ligne d'état centrée ;
  - la géométrie `GetKeyboardGeometry`.

  Estimation : environ +200 lignes dans le moteur, environ −300 dans la fenêtre, **solde d'environ −100**. Des écarts d'un pixel restent, car la formule d'échelle et les arrondis diffèrent.
- **Rendu quasi identique avec un profil existant : non.** Ce serait la carte.
- **Verdict** : en 1.3.0, garder deux moteurs et le documenter (option prévue par V-01). Tout au plus, partager la géométrie entre dessin et survol.

### 3.3 Le tutoriel (`LearningModule.PaintKeyboard`) sur `KeyboardRenderer` de la release

**Faisable, et c'est le bon candidat.** Le tutoriel dessine déjà la carte avec les mêmes 11 couleurs que le moteur (`LM:109-132` contre `KR:31-44` ; capture `avant/module-essai-*.png`). L'enum `KeyHighlight` de `main` (`KeyboardTheme.cs:34-49`) est portable tel quel. En revanche, `HighlightPaint` et `KeyboardTheme.Paint` lisent la charte : il faut une table de couleurs du tutoriel, avec ses couleurs actuelles.

Écarts à combler pour ne rien changer à l'écran :

1. **Surlignage** : trois genres (direct vert, étape 1 orange, étape 2 vert), en contour seul, sauf « à appuyer et activé » en fond plein (LM:2414-2447). Le moteur n'a qu'un contour vert (KR:378-382). Remède : `KeyHighlight` par touche, table de couleurs et règle de fond plein.
2. **Pastilles carrées 1 et 2** (LM:2532-2543, 2593) : à ajouter au moteur.
3. **KeepCaps** (LM:1972-1978, 2533) : côté appelant.
4. **Retour arrière** terne mais rallumé quand il est surligné (LM:2414, 2517). Le moteur le laisse toujours terne (KR:370-377, 463).
5. **Exercice 6** (`LanguageExercise*`, `FilterOnboardingSlot`, LM:3070-3133) : paramètre de visibilité à ajouter.
6. **Touche morte inactive** : grise dans le tutoriel (LM:2948), toujours `#FF6666` dans le moteur (KR:738-744).
7. **Marge** : `max(4, 0,14 × échelle)` (LM:2619) contre `clamp(kw/10, 4, 14)` (KR:484).
8. **Touche morte active** : résultat vert, libellé blanc cassé en police de contexte, recherche Base/Maj/majuscule sans AltGr (LM:2641-2692). Le moteur fait `GetOutput(shift, altGr, caps)`, avec libellé jaune et ligne d'état (KR:503-580). Différence de sens à trancher.
9. **Barre de Verr. Maj.** : `0,08 × échelle` contre `h/18`.
10. **Géométrie** : `GetKeyboardGeometry` avec −1 px (LM:2379-2389), contre `Draw` et `ToRect`.
11. **« ◌/ » de dk_stroke à l'exercice 6** : deux passes en Segoe UI dans le tutoriel (LM:2957-2976), une passe Consolas dans le moteur.
12. **Infobulle « ⌫ »** propre au tutoriel (LM:577-650).
13. **Surcharges `learning-tweaks.json`** : fichier de développement dans LocalAppData, non livré, donc invisible pour l'utilisateur.

**Écarts inévitables**, si l'on supprime vraiment du code : arrondis d'un pixel avec `ToRect` ; le « ◌/ » Segoe UI ; les surcharges de développement ; le point 8, s'il est unifié. Tout le reste se paramètre, au prix de lignes dans le moteur.

**Gain** : environ 750 lignes du tutoriel concernées, moins environ 230 d'ajouts et d'adaptateur, soit **environ −450 à −500 à parité pixel** (estimation ; le contre-avis donne −500 à −650 sans parité). À garder : les membres lus par réflexion dans `LessonCoreTests.cs:588-606`. Aucun test visuel n'existe : le banc du §4 est un préalable.

## 4. Banc de rendu : reportable seul ?

**Verdict : oui, à condition. Ce n'est pas un simple cherry-pick** (lecture des messages et de ThemeWindow ; le détail fichier par fichier n'était pas revenu à la remise).

- **Mécanique** (5d5ada4, f9e7a8e, 6c43c9d, 8ed71e1) : le banc tourne dans le processus de test (`AZERTYGlobal.Tests`). Il ne se lance que si `AZERTY_CAPTURE` désigne un dossier : hors CI, sans effet sur les compteurs. Il crée les vraies fenêtres, force l'échelle par `ThemeWindow.OverrideDpiForTests` et écrit un PNG par cellule (fenêtre × thème × 100/125/150 %).
- **Fichiers à reporter** :
  - `src/AZERTYGlobal.Tests/CaptureBench.cs`, à expurger de `Theme.OverrideForTests` et des fenêtres ou onglets propres à la charte ;
  - le crochet de DPI : `DpiOf` + `OverrideDpiForTests` (copie de ThemeWindow, §2) ;
  - les ouvertures pour capture ajoutées aux fenêtres (About +11 lignes et Pause +6 dans 5d5ada4, Onboarding dans 8ed71e1, `LessonsWindow.OpenForCapture` dans ffb66b9, `VirtualKeyboard.Handle` interne).

  Ces crochets ne peignent rien : ils ne changent pas un pixel.
- **Condition clé, le DPI.** 6c43c9d prouve que sans point d'entrée unique, les cellules sortent toutes à l'échelle du poste (« douze cellules = quatre rendus »). Sur la release, le DPI se lit par `GetDeviceCaps(…, 88)` dans 8 fenêtres (About, Conflit, Tutoriel, Leçons, Accueil, Paramètres, Notification, Stats), par `GetDpiForWindow` direct ou par `GetDpiForWindowOrDefault` dans 3 (vérifié par grep). Il faut router ces lectures vers `DpiOf` : environ 2 lignes par fenêtre, neutres en production.
- **Le thème** : la release n'a qu'un thème. Une seule colonne suffit, sans Theme.cs.
- **Hors report** : `StatesBoard`, `KeyboardPlank`, `KeyboardStatesBoard` et le banc de clavier de CH4a/CH4b dépendent de ThemeControls, KeyboardTheme ou CH4b. `KeyboardContextBench` se réécrit en petit pour Leçons, clavier virtuel et tutoriel tels qu'ils sont.
- **Risques** :
  - Smart App Control bloque `AZERTY Global.dll` reconstruite sur le poste (0x800711C7, messages du 29/08 et du 07/09) : le banc local peut ne pas tourner. D'où le job CI « maquettes » de 159a6b2 (continue-on-error, compte les PNG écrits), à reprendre sans ses entrées CH4b.
  - Non-déterminisme connu : 2 captures sur 80 diffèrent entre deux passes (f3b4dc7), à cause du curseur qui clignote et d'un clavier sombre instable (bbc8404). Il faut deux passes et lancer le banc seul.
- **Ordre** : le banc avant tout chantier visuel, puis une capture « avant » de la release telle quelle, qui sert de référence pixel.

## 5. Recommandation (10 lignes)

1. Reporter 1911581 (frappes rapides des leçons) : c'est le seul correctif utilisateur de `main` absent de la 1.3.0. Conflits attendus dans LessonsWindow et LearningModule.
2. Reporter 4542a28 (.gitignore Archives/) ; 1b025d0, fb0ca3b, 5ee46c8 et lessons.json y sont déjà.
3. Porter ensuite le banc de capture en version réduite : CaptureBench, `DpiOf` + override, crochets d'ouverture, job CI, sans Theme ni StatesBoard.
4. Router le DPI des 11 fenêtres vers `DpiOf` et produire les images « avant » de la release, qui serviront de référence.
5. Socle de fenêtre : écrire un socle neuf sur le modèle D1 et y copier `DpiOf`, `ApplyDpiChange` et `ApplyClassBackground` de ThemeWindow, sans `ApplyChrome` ni Theme.cs.
6. Migrer d'abord À propos et Conflit, en comparant les images avant/après au pixel.
7. Tutoriel : le passer sur `KeyboardRenderer` avec un enum `KeyHighlight` et les couleurs du tutoriel. Parité pixel, sauf les 4 écarts listés, à valider sur images.
8. Clavier virtuel : le laisser sur son moteur en 1.3.0 et documenter les deux moteurs, car la bascule sans changement visible ne rapporte rien.
9. Laisser dans l'archive la charte CH0 à CH4b (27 commits b), ThemeControls, KeyboardTheme.Paint/HighlightPaint, la carte et les planches : S6-1 les a rouverts.
10. À faire trancher par Antoine : l'icône produit sur 8 fenêtres, la carte au clavier virtuel, le sens de la touche morte active dans le tutoriel.
