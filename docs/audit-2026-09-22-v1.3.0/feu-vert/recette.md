# Recette du candidat Store 1.3.0 — à cocher sur le poste d’Antoine

Candidat : bundle CI du run [35987701131](https://github.com/AMCF-asso/azerty-global-app/actions/runs/35987701131), commit `ade118f` sur `release/1.2.0-notation-store` (correctifs `5b56087`, `ad05d72` et `ade118f` compris), SHA-256 `01b18e114865a57a9ae4fe06ed04249602cb080666820aac678913f31b0f2507`, attestation vérifiée le 2026-09-24 (`sourceRepositoryDigest` = `ade118f`). Tests CI : 18/18, 252/252, 536/536. **Une recompilation change l’empreinte et annule cette recette.** Les candidats précédents `ba587cce` puis `bbf64c28` sont remplacés ; les résultats du premier (A1, A2, recette automatique) restent dans l’historique git jusqu’à `a7e6151` et dans `evidence\recette-ba587ccee059\`.

Lancement, sur le poste (choix d’Antoine du 2026-09-23, le Sandbox reste possible via `sandbox\lancer-recette.ps1`) : quitter toute autre version d’AZERTY Global, puis `powershell -ExecutionPolicy Bypass -File installer-poste.ps1 -Bundle ..\..\..\msix\ci-35987701131\AZERTYGlobal-1.3.0.0.msixbundle`. Le journal `evidence\recette-01b18e114865\installation-poste.txt` doit finir par « Pret » avec **shadow stack ON ; CFG ON**. Ne pas toucher l’accueil avant A2.

Noter pour chaque ligne : ✅, ❌ + observation, ou ⏭️ + raison.

## A. Bloquant — sans ces lignes, pas de soumission (≈ 45 min)

| # | Geste | Attendu | Résultat |
|---|---|---|---|
| A1 | Journal d’installation | SHA-256 du candidat, `…_1.3.0.0_x64__w9kghr08zmhbg`, app lancée, shadow stack ON, CFG ON | |
| A2 | Accueil : fermer par la croix, relancer ; refaire avec Échap | Clavier inchangé dans le Bloc-notes, aucun accord mémorisé | |
| A3 | Accueil au clavier seul : Tab/Maj+Tab, puis Entrée sur « Activer et essayer » | Leçon ouverte, remappage actif | |
| A4 | Bloc-notes : `é è à ç ù`, majuscules accentuées, AltGr, deux touches mortes | Caractères attendus, aucun modificateur coincé | |
| A5 | Quitter par l’icône, relancer depuis Démarrer | Clavier système dès la sortie ; accord conservé, remappage revenu | |
| A6 | Alt+Tab rapide Bloc-notes ↔ Edge en tapant | Chaque caractère dans la bonne fenêtre, pas de bulle en rafale | |
| A7 | Edge : champ mot de passe puis champ normal | Fonctions avancées suspendues dans le premier, disponibles dans le second | |
| A8 | Recherche de caractère, insertion | Bon symbole, dans la fenêtre d’origine | |
| A9 | Paramètres : trois onglets au clavier, « Réinitialiser » par Entrée puis Espace, annuler | Focus visible, confirmation, rien ne change à l’annulation | |
| A10 | Menu de l’icône : Couches ▸, Apprendre ▸, À propos et aide ▸, bascule FR/EN | Douze lignes, sous-menus ouverts, textes basculés | |
| A11 | **Chemin d’erreur** : quitter l’app, remplacer le contenu de `config.json` par `[1]` (sous MSIX, chercher d’abord `%LOCALAPPDATA%\Packages\AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg\LocalCache\Local\AZERTY Global\`, sinon `%LOCALAPPDATA%\AZERTY Global\`), relancer | L’app démarre (pas d’arrêt brutal) ; `error.log` porte `JsonException` ; le fichier n’est pas réécrit | |
| A12 | Désinstaller (Paramètres > Applications) | Processus arrêté, clavier système utilisable | |
| A13 | Hors Sandbox : `wack.ps1 -Bundle <bundle>` en administrateur | `OVERALL_RESULT : PASS` dans `evidence\wack-01b18e114865\resume.txt` | |

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
| B12 | Narrateur (Ctrl+Win+Entrée) ou Inspect : Pause (deux champs, quatre ▲▼), Paramètres (deux raccourcis, liste des apps suspendues), Recherche ; refaire en anglais | « Heures », « Minutes », « Augmenter les heures »…, « Clavier virtuel », « Recherche », « Apps suspendues », « Rechercher un caractère » ; noms anglais après la bascule | |
| B13 | Affichage à 175 % puis 200 %, puis changement d’échelle fenêtre ouverte : Pause, couches maintenables, indicateur de couche (couche active dans le Bloc-notes), Leçons | Textes et contrôles à l’échelle, rien de coupé ni de superposé ; Leçons entièrement dans l’écran, commandes du bas atteignables | |

Lignes B6 à B13 : lot d’accessibilité 1.3.0 (`accessibilite-1.3.0.md`), à jouer sur le candidat qui le contient, pas sur `768f13fb`. Les décisions pures du lot ont leurs témoins automatiques ; ces lignes couvrent ce qu’aucun test ne voit : le rendu à l’écran, les messages Windows et la lecture par le Narrateur.

## E. Correctifs du candidat (à vérifier en plus)

| # | Geste | Attendu | Résultat |
|---|---|---|---|
| E1 | Accueil, premier lancement : aller jusqu’à l’étape 3 après « Activer et essayer » | Case « Lancer au démarrage de Windows » **cochée** ; après validation, l’app figure activée dans Paramètres Windows > Applications > Démarrage | |
| E2 | Rouvrir l’accueil (menu de l’icône), décocher la case, valider ; relancer l’app | Démarrage désactivé dans Windows ; pas de relance proposant de l’activer | |
| E3 | Désactiver l’app dans Paramètres Windows > Démarrage, réinitialiser l’accueil, aller à l’étape 3 | Case **décochée** : le refus Windows est respecté | |
| E4 | Avant l’accord (A2), puis après « Activer et essayer » | Icône grise avec infobulle « Désactivé/Off », puis bleue avec « Actif/Active » | |
| E5 | App en anglais : clavier des Leçons, des exercices et clavier virtuel ; basculer FR ↔ EN fenêtre ouverte | « Enter », « Caps Lock », « Shift ⇧ », « Space » en anglais, textes français en français, redessin immédiat ; rien ne déborde | |
| E6 | Paramètres : passer d’un onglet à l’autre, en FR puis en EN, à 100 % puis 175 % | La fenêtre ne change ni de taille ni de place ; aucun contrôle coupé ; Tab fait défiler si besoin | |
| E7 | **Via Parsec** depuis le portable (app suspendue côté portable quand Parsec est au premier plan) : exercice 1 de l’accueil, puis Bloc-notes : `azerty`, Verr. Maj + é, AltGr+e, `^` puis e ; le journal de compatibilité porte `RemoteAccessHost: present` | Frappes reçues et remappées **une seule fois** (É, €, ê) ; touches allumées sur le clavier de l’exercice ; rien de doublé | |

## C. Écarts assumés (décisions datées, ne pas rouvrir ici)

- **ARM64** : binaire compilé et analysé (BinSkim, 122 objets C/C++ du runtime Microsoft) mais pas exécuté nativement. Publié avec l’écart documenté (décision d’Antoine du 2026-09-22).
- **Windows 10 minimum (`10.0.17763.0`)** : non vérifié, le Sandbox suit l’OS de l’hôte (26100).
- **Mise à jour depuis la 1.1.0 Store** : le Sandbox n’a pas de Store. Preuve antérieure : 1.1.0 Store → 1.2.0 signée localement acceptée le 2026-08-18, identité remplacée le 2026-09-19 (VM-02).
- **Anti-cheat réel, veille/reprise, verrouillage** : non exerçables dans le Sandbox.
- **Reports 1.3.1** déjà arbitrés : R7 (repli Alt+code, scan codes droits), reprise d’un `SendInput` refusé.

## D. Relevé pendant la recette à la main du 2026-09-23 (soir)

- **Pas de Bloc-notes dans le Sandbox** (Windows 24H2, Bloc-notes livré par le Store) : Win+R le remplace, la barre d’adresse d’Edge pour A6.
- **Corrigé dans ce candidat** (décision d’Antoine du 23/09, `5b56087`) : noms des touches du clavier affiché en anglais quand l’app est en anglais. Voir E5.
- **Corrigé dans ce candidat** (`5b56087`) : icône bleue alors que l’app est « Off » avant l’accord. Voir E4.
- **Observé, non reproduit** : pendant l’exercice 2/6, la touche `.` a mis en surbrillance la touche C, et d’autres touches d’autres cases, sans lien avec la lettre attendue. Au même moment, Verr. Maj s’est retrouvé désactivé en plein mot. Hypothèse non démontrée : resynchronisation de Verr. Maj par le Sandbox au changement de fenêtre.
