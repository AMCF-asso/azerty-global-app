# Recette du candidat Store 1.3.0 — à cocher dans Windows Sandbox

Candidat : bundle CI du run [35851382703](https://github.com/AMCF-asso/azerty-global-app/actions/runs/35851382703), commit `585672f` sur `ci/verif` (sources de `34feb04` sur `release/1.2.0-notation-store`, lot d’accessibilité compris), SHA-256 `ba587ccee0597a96d63b99052b0fd26dfabfb3830a81a07cb463cb3e1bb0a5c4`, attesté. Le précédent, `768f13fb…`, ne contient pas le lot. **Une recompilation change l’empreinte et annule cette recette.**

Lancement : `powershell -ExecutionPolicy Bypass -File sandbox\lancer-recette.ps1 -Bundle <bundle>`. Le journal `evidence\recette-ba587ccee059\installation.txt` doit finir par « Pret » avec **shadow stack ON ; CFG ON**. Le Sandbox applique réellement le shadow stack sur ce poste (mesuré le 22/09).

Noter pour chaque ligne : ✅, ❌ + observation, ou ⏭️ + raison.

**Recette automatique du 2026-09-23** : `sandbox\lancer-recette.ps1 -Bundle <bundle> -Auto` joue `sandbox\recette-auto.ps1` sans humain (A1, A2, A3, A5, A10, A11, A12, B12), puis désinstalle l'app. Les gestes passent par des messages Windows et des touches simulées ; le hook ne remappe pas ces touches (AG130-07), donc rien de ce qui dépend de la frappe remappée n'est jugé. Preuves de référence : `evidence\recette-ba587ccee059\auto-essai2\` (essai 1 : script erroné ; essai 3 : variante abandonnée). Les lignes marquées « auto, partiel » gardent leur **Reste** à jouer à la main, avec A4, A6 à A9, A13, B1 à B11 et B13.

## A. Bloquant — sans ces lignes, pas de soumission (≈ 45 min)

| # | Geste | Attendu | Résultat |
|---|---|---|---|
| A1 | Journal d’installation | SHA-256 du candidat, `…_1.3.0.0_x64__w9kghr08zmhbg`, app lancée, shadow stack ON, CFG ON | ✅ auto : empreinte, paquet 1.3.0.0 x64, shadow stack ON, CFG ON |
| A2 | Accueil : fermer par la croix, relancer ; refaire avec Échap | Clavier inchangé dans le Bloc-notes, aucun accord mémorisé | ✅ auto, partiel : fermé par la croix puis par Échap, app vivante, aucun accord mémorisé. **Reste** : clavier inchangé dans le Bloc-notes. ✅ main (23/09, 18:37, Win+R faute de Bloc-notes dans le Sandbox 24H2) : Verr. Maj + é → `2`, après la croix puis après Échap. ⚠️ Icône bleue alors que l'infobulle dit « Off » : l'icône du démarrage est créée active (`TrayApplication.cs:236`) et seule l'infobulle est recalculée (`:290`). Mineur, candidat 1.3.1 |
| A3 | Accueil au clavier seul : Tab/Maj+Tab, puis Entrée sur « Activer et essayer » | Leçon ouverte, remappage actif | ✅ auto, partiel : focus initial sur « Activer et essayer », Entrée → accord enregistré, leçon ouverte. **Reste** : Tab/Maj+Tab, remappage actif |
| A4 | Bloc-notes : `é è à ç ù`, majuscules accentuées, AltGr, deux touches mortes | Caractères attendus, aucun modificateur coincé | |
| A5 | Quitter par l’icône, relancer depuis Démarrer | Clavier système dès la sortie ; accord conservé, remappage revenu | ✅ auto, partiel : sortie par Quitter, relance, accord conservé. **Reste** : clavier système à la sortie, remappage revenu |
| A6 | Alt+Tab rapide Bloc-notes ↔ Edge en tapant | Chaque caractère dans la bonne fenêtre, pas de bulle en rafale | |
| A7 | Edge : champ mot de passe puis champ normal | Fonctions avancées suspendues dans le premier, disponibles dans le second | |
| A8 | Recherche de caractère, insertion | Bon symbole, dans la fenêtre d’origine | |
| A9 | Paramètres : trois onglets au clavier, « Réinitialiser » par Entrée puis Espace, annuler | Focus visible, confirmation, rien ne change à l’annulation | |
| A10 | Menu de l’icône : Couches ▸, Apprendre ▸, À propos et aide ▸, bascule FR/EN | Douze lignes, sous-menus ouverts, textes basculés | ✅ auto : 12 lignes en FR et en EN ; sous-menus Couches, Apprendre, Compatibilité des applications, À propos et aide ; aucun texte identique entre FR et EN. **Reste** : ouverture des sous-menus |
| A11 | **Chemin d’erreur** : quitter l’app, remplacer le contenu de `config.json` par `[1]` (sous MSIX, chercher d’abord `%LOCALAPPDATA%\Packages\AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg\LocalCache\Local\AZERTY Global\`, sinon `%LOCALAPPDATA%\AZERTY Global\`), relancer | L’app démarre (pas d’arrêt brutal) ; `error.log` porte `JsonException` ; le fichier n’est pas réécrit | ✅ auto : l'app démarre, `JsonException` dans `error.log` (0 → 1), `config.json` non réécrit |
| A12 | Désinstaller (Paramètres > Applications) | Processus arrêté, clavier système utilisable | ✅ auto, partiel : processus arrêté, paquet retiré. **Reste** : clavier système utilisable |
| A13 | Hors Sandbox : `wack.ps1 -Bundle <bundle>` en administrateur | `OVERALL_RESULT : PASS` dans `evidence\wack-ba587ccee059\resume.txt` | |

## B. Recommandé si le temps le permet

| # | Geste | Attendu | Résultat |
|---|---|---|---|
| B1 | Affichage du Sandbox à 175 % puis 200 %, ouvrir Paramètres et Leçons | Textes lisibles, le focus fait défiler vers les contrôles masqués | |
| B2 | Narrateur (Ctrl+Win+Entrée) sur l’accueil et Paramètres | Boutons et onglets annoncés ; limites des surfaces dessinées notées, sans score | |
| B3 | Contraste élevé | Noter l’état : dette connue, non bloquante | |
| B4 | Ancienne config sans `activationConsent` placée avant le 1er lancement | Demande unique, réglages conservés | |
| B5 | Leçons, défi, statistiques puis remise à zéro | Progression et compteurs cohérents | |
| B6 | Fenêtre des couches maintenables au clavier seul : Tab, Maj+Tab, Espace sur une case, Entrée ; rouvrir, Échap | Focus initial sur la case principale, puis circulation entre cases, champ et « Enregistrer » ; Entrée et Échap ferment en enregistrant | |
| B7 | Conflit de disposition (disposition système AZERTY Global active au lancement, si elle s’installe dans le Sandbox, sinon ⏭️) : Tab jusqu’à « Garder l’application », Entrée ; rouvrir, Échap ; rouvrir, Entrée sans Tab | Entrée presse le bouton focalisé ; Échap garde l’app ; Entrée sans bouton focalisé ne fait rien | |
| B8 | Exercices (5ᵉ ou 6ᵉ, « Passer » affiché) : Tab, Tab, Entrée ; au suivant, Tab puis Maj+Tab, et taper la suite | Cadre de focus sur « Quitter » puis « Passer » ; Entrée passe l’exercice et la frappe reprend sans clic ; Tab ne compte aucune faute | |
| B9 | Leçons au clavier seul : Tab jusqu’aux boutons-icônes (indice, réglages…) | Chaque icône focalisée affiche la même infobulle qu’au survol ; un mouvement de souris rend l’infobulle de survol | |
| B10 | Accueil étape 3, puis À propos : Tab sur chaque lien | Cadre pointillé autour du texte du lien focalisé, en plus de la couleur (À propos : la couleur suit aussi le focus, ce qu’elle ne faisait pas) ; aucune trace une fois le focus parti | |
| B11 | Accueil : Tab jusqu’au drapeau, Entrée, puis Espace, puis un clic | Cadre de focus autour du drapeau ; la langue bascule à chaque geste et le focus reste sur le drapeau | |
| B12 | Narrateur (Ctrl+Win+Entrée) ou Inspect : Pause (deux champs, quatre ▲▼), Paramètres (deux raccourcis, liste des apps suspendues), Recherche ; refaire en anglais | « Heures », « Minutes », « Augmenter les heures »…, « Clavier virtuel », « Recherche », « Apps suspendues », « Rechercher un caractère » ; noms anglais après la bascule | ⚠️ auto, partiel (noms MSAA, pas le Narrateur) : Pause et Paramètres complets en FR et en EN, Recherche complète en EN ; Recherche FR non ouverte par le script (non expliqué). Onglets des Paramètres sans nom. **Reste** : Narrateur, Recherche en FR |
| B13 | Affichage à 175 % puis 200 %, puis changement d’échelle fenêtre ouverte : Pause, couches maintenables, indicateur de couche (couche active dans le Bloc-notes), Leçons | Textes et contrôles à l’échelle, rien de coupé ni de superposé ; Leçons entièrement dans l’écran, commandes du bas atteignables | |

Lignes B6 à B13 : lot d’accessibilité 1.3.0 (`accessibilite-1.3.0.md`), à jouer sur le candidat qui le contient, pas sur `768f13fb`. Les décisions pures du lot ont leurs témoins automatiques ; ces lignes couvrent ce qu’aucun test ne voit : le rendu à l’écran, les messages Windows et la lecture par le Narrateur.

## C. Écarts assumés (décisions datées, ne pas rouvrir ici)

- **ARM64** : binaire compilé et analysé (BinSkim, 122 objets C/C++ du runtime Microsoft) mais pas exécuté nativement. Publié avec l’écart documenté (décision d’Antoine du 2026-09-22).
- **Windows 10 minimum (`10.0.17763.0`)** : non vérifié, le Sandbox suit l’OS de l’hôte (26100).
- **Mise à jour depuis la 1.1.0 Store** : le Sandbox n’a pas de Store. Preuve antérieure : 1.1.0 Store → 1.2.0 signée localement acceptée le 2026-08-18, identité remplacée le 2026-09-19 (VM-02).
- **Anti-cheat réel, veille/reprise, verrouillage** : non exerçables dans le Sandbox.
- **Reports 1.3.1** déjà arbitrés : R7 (repli Alt+code, scan codes droits), reprise d’un `SendInput` refusé.

## D. Relevé pendant la recette à la main du 2026-09-23 (soir)

- **Pas de Bloc-notes dans le Sandbox** (Windows 24H2, Bloc-notes livré par le Store) : Win+R le remplace, la barre d’adresse d’Edge pour A6.
- **1.3.1, décision d’Antoine du 23/09** : noms des touches du clavier affiché en anglais quand l’app est en anglais (« Verr. Maj. », « Entrée », « Maj ⇧ » restent en français). Le libellé sert aussi d’identifiant (`VirtualKeyboard.cs:175-203`, `KeyboardRenderer.cs`, `LearningModule.cs`, `LessonsWindow.cs:2445`) : séparer identifiant et texte affiché.
- **1.3.1** : icône bleue alors que l’app est « Off » avant l’accord (voir A2).
- **Observé, non reproduit** : pendant l’exercice 2/6, la touche `.` a mis en surbrillance la touche C, et d’autres touches d’autres cases, sans lien avec la lettre attendue. Au même moment, Verr. Maj s’est retrouvé désactivé en plein mot. Hypothèse non démontrée : resynchronisation de Verr. Maj par le Sandbox au changement de fenêtre.
