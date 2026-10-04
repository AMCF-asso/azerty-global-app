# Recette 1.3.0 — fiche unique

- **Candidat** : le candidat final — commit `86c5efb` et run CI `37209657043` (CI du 04/10, sur `main` = `release/1.2.0-notation-store`) ; branche `release/1.2.0-notation-store`, CI verte (Pack MSIX, Verify Release, SHA256 artefacts, attestation). Les scripts ont été rodés le 28/09 sur `913067a` (run 36437681572), qui n’est pas le candidat final.
- **Bundle** : `AZERTYGlobal-1.3.0.0.msixbundle` du candidat final — **SHA-256 : `826877B2016F570F88656B23FB2CD673BD4F335A0970A87275A8B97B3204696D`** (troisième ligne `Get-FileHash` de l’étape « SHA256 artefacts » du run, que `verifier-candidat.ps1` relit). Une recompilation change l’empreinte et annule la recette.
- **Durée estimée** (estimation : les gestes n’ont jamais été chronométrés) : 2 h 30 environ d’attention, contre 3 h 45 annoncées avant l’automatisation du 28/09 (bundle et Verify 5 min, lecture de la recette automatique 5 min, gestes 1 h 20, revue des images du banc 10 min, WACK 25 min, Partner Center 25 min). La recette automatique tourne en plus 6 à 8 min sans surveillance (mesuré le 28/09 sur `913067a`, du lancement du Sandbox à `done.txt`). Optionnels : 1 h 30 de plus.
- **Matériel** : le poste, devant la machine ; Parsec exclu (la ligne E7 du kit n’est pas jouée). Windows Sandbox activé, SDK 10.0.26100 (`signtool`), App Certification Kit, `gh` connecté à un compte qui lit `AMCF-asso/azerty-global-app`. Deux écrans d’échelles différentes (100 % et 150 %) si possible, pour la section 3.18 : le Sandbox n’a qu’un écran. Une VM remplace le Sandbox seulement si le certificat de test y est approuvé, comme sur le poste (`installer-poste.ps1` l’exige).
- **Avant tout** : quitter AZERTY Global sur l’hôte. Les consoles de VM ne sont pas reconnues comme accès distant (report 1.3.1).
- **Noter** : ✅, ❌ + observation, ⏭️ + raison. Sources : *kit* = `feu-vert/recette.md` (A, B, E) ; *24/09* et *25/09* = lignes de recette des rapports d’audit ; *L1* à *L9* = lots ; *26/09* = correctifs des Paramètres ; *règle* = règle 1.3.0 ; *PC* = Partner Center.
- **Lignes automatisées** (« lire le verdict ») : reporter le niveau lu, OK, OK (partiel), A VOIR ou ECHEC ; l’observation dit ce qu’un OK (partiel) laisse à l’œil, au banc ou à un test. Sur A VOIR ou ECHEC, rejouer le geste d’origine, décrit par la version `8a7b7cb` de cette fiche (`git show 8a7b7cb:docs/recette-2026-09-27-v1.3.0/RECETTE-1.3.0.md`). Lignes « couverte par … » : rien à jouer, le test cité tourne dans la CI du candidat.

Chemins, dans un PowerShell ouvert à la racine du dépôt `D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store` :

```powershell
$kit = "docs\audit-2026-09-22-v1.3.0\feu-vert"
$run = "37209657043"
$b   = "msix\ci-$run\AZERTYGlobal-1.3.0.0.msixbundle"
```

## 1. Installation et vérification du bundle

- [ ] **R01** — `powershell -ExecutionPolicy Bypass -File "$kit\sandbox\verifier-candidat.ps1" -Run $run -Sha <empreinte de l’en-tête> -Commit <commit de l’en-tête>` → lire le verdict R01 dans `$kit\evidence\recette-<12>\hote\resultats.json` : artefact `msixbundle` téléchargé dans `msix\ci-$run\`, un seul fichier, `AZERTYGlobal-1.3.0.0.msixbundle`. Le script prend le compte `gh` actif ; `-GhUser <compte>` utilise le jeton d’un autre compte pour lui seul, sans changer le compte actif. (README feu-vert, étape 1 ; ci.yml)
- [ ] **R02** — lire le verdict R02 dans `hote\resultats.json` → OK : empreinte du fichier égale à `-Sha` (l’en-tête) et à la 3ᵉ ligne `Get-FileHash` de l’étape « SHA256 artefacts » du run ; OK (partiel) si `-Sha` manquait. (rapport 25/09, « Candidat »)
- [ ] **R03** — lire le verdict R03 dans `hote\resultats.json` → OK : `gh attestation verify` réussi, commit source de l’attestation égal au commit du run et à `-Commit`. (README feu-vert, étape 1)

## 2. Recette automatique (Sandbox)

- [ ] **R04** — `powershell -ExecutionPolicy Bypass -File "$kit\sandbox\lancer-recette.ps1" -Bundle $b -Auto` → le Sandbox installe, joue, désinstalle et s’éteint seul ; la console attend `done.txt` dans `$kit\evidence\recette-<12>\auto\`, puis affiche le verdict global. `-Garder` laisse le Sandbox ouvert ; un dossier `auto\` d’un passage précédent est renommé, jamais effacé. (lancer-recette.ps1)
- **R05** — couverte par A1 de `recette-auto.ps1` : empreinte, `…_1.3.0.0_x64__w9kghr08zmhbg` et `shadow stack ON ; CFG ON` lus dans `installation.txt`. (kit A1)
- [ ] **R06** — lire le verdict global dans `auto\resume-auto.txt` → OK : `lire-resultats.ps1` y compare chaque ligne de `resultats.json` (A1 à A12, B12 et les lignes Rxx « lire le verdict ») à son niveau attendu. Une ligne sous son attendu donne A VOIR ; un ECHEC ou une ligne absente donne ECHEC. La frappe remappée n’y est pas jugée. B12 peut rendre A VOIR quand la Recherche ne s’ouvre pas (instable, vu au passage du 29/09) : la juger alors à la main en R33 et R69. (kit A2, A3, A5, A10, A12, B12)
- [ ] **R07** — lire le verdict A11 dans `resultats.json` → OK (partiel) : copie `config.json.illisible-…` qui contient `[1]`, `config.json` qui n’est plus `[1]` ; accueil, app inactive jusqu’à l’accord et relance active sont jugés en R75. (recette-auto.ps1, section A11 ; L4)

## 3. Gestes manuels

Ouvrir un Sandbox neuf, sans `-Auto` : `powershell -ExecutionPolicy Bypass -File "$kit\sandbox\lancer-recette.ps1" -Bundle $b`. Premier lancement : ne pas toucher l’accueil avant R08. Le Sandbox n’a pas de Bloc-notes : Win+R, la barre d’adresse et un champ de texte d’Edge le remplacent (kit D). Dans un PowerShell du Sandbox, coller ces outils ; toujours quitter l’app (menu ▸ Quitter) avant d’écrire un fichier :

```powershell
$d = "$env:LOCALAPPDATA\Packages\AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg\LocalCache\Local\AZERTY Global"   # sinon "$env:LOCALAPPDATA\AZERTY Global"
$j = Get-Date -Format yyyy-MM-dd; $j3 = (Get-Date).AddDays(-3).ToString('yyyy-MM-dd'); $j8 = (Get-Date).AddDays(-8).ToString('yyyy-MM-dd')
function Cle($k, $v) { $o = Get-Content "$d\config.json" -Raw | ConvertFrom-Json; if ($null -eq $v) { $o.PSObject.Properties.Remove($k) } else { $o | Add-Member -NotePropertyName $k -NotePropertyValue $v -Force }; [IO.File]::WriteAllText("$d\config.json", ($o | ConvertTo-Json -Depth 10)) }
function Usage($jours) { [IO.File]::WriteAllText("$d\usage-stats.json", ('{{"firstRemapDate":"{0}","lastActiveDate":"{0}","activeDaysCount":{1},"totalActiveMinutes":10,"accentedUppercaseCount":20}}' -f $j, $jours)) }
```

### 3.0 Revue des images du banc (sur l’hôte, 10 min)

Artefact `captures-<12>` du workflow « Captures des fenêtres » sur le commit du candidat final : le lancer à la main par `gh workflow run captures.yml -R AMCF-asso/azerty-global-app -f ref=<sha>` (sha complet ou abrégé, branche ou tag). Un commit absent du dépôt se pousse d’abord sur `captures/<sha>`, qui ne déclenche plus rien, et cette branche se supprime une fois l’artefact récupéré. Depuis le 28/09, le banc rend par défaut 96, 120, 144, 168 et 192 DPI (100 à 200 %), en FR et en EN ; nom des images : `<sujet>-<fr|en>-<échelle>.png`. Le banc rend sans rien affirmer : ces lignes se jugent à l’œil. Il simule le DPI par `WM_DPICHANGED` sur un runner à 96 DPI, d’où le contrôle réel de R64b.

- [ ] **R12** — `tuto-ex2-etape1`, `tuto-ex6-etape1`, `tuto-ex6-etape2` → surlignage direct ; étape 1 en orange avec la pastille « 1 », étape 2 en vert avec la pastille « 2 ». Logique : `TutorielClavierTests.L_etape_1_se_dit_en_orange_avec_sa_pastille`. (L9)
- [ ] **R13** — `tuto-ex1-verrmaj`, `tuto-ex2-espace` → Verr. Maj. pleine. Logique : `GuidageTests.Les_exercices_1_et_2_gardent_Verr_Maj_surlignee`. (L9)
- [ ] **R14** — `tuto-ex6-etape2`, `tuto-ex4-altgr-tenue` → les touches montrent le résultat avec les touches tenues, AltGr compris ; leur nom passe en jaune. (L9)
- [ ] **R15** — `tuto-ex6-retour` → Retour arrière surligné ; « ◌ » barré visible, cercle et barre superposés. (L9)
- [ ] **R64** — À 125, 150, 175 et 200 % : `parametres-*` (trois onglets), `lecons-*`, `duree-de-pause*`, `couches`, `accueil-etape*`, `a-propos`, `statistiques` → textes et contrôles à l’échelle, rien de coupé ni de superposé ; à 150 %, largeurs justes. (kit B1, B13 ; L1 ; L3 ; L6)
- [ ] **R64c** — Textes 1.3.0, en français puis en anglais : `accueil-etape1` (titres du site, carte 4 sur deux lignes, « touches mortes. » ensemble), `a-propos`, `couches`, `parametres-*`, `statistiques`, `lecons-*` → formulations validées le 04/10, rien de coupé. (décisions `operations/2026-09-29-textes-app-130`)
- [ ] **R65** — `lecons-*` à 150 et 175 % → le clavier dessiné ne recouvre ni la ligne cible ni la saisie ; commandes du bas visibles. Logique : `LessonsWindowScaleTests.Plafonnée_LÉchelleDescendSous1_EtLeClavierNeCouvrePlusLaSaisie`, `WindowSizingTests.LeCasMesuré_175Pourcent_RentreDansLÉcran`. (24/09 l. 5 ; kit B13)
- [ ] **R66** — `duree-de-pause`, `duree-de-pause-invalide`, `duree-de-pause-fond` à 175 % → mêmes attendus que R57 : fond clair, flèches à droite des champs, ligne rouge. (L8.1)
- [ ] **Compléments des verdicts partiels** — `accueil-etape3` : deux cases, sans vide (R20) ; `parametres-general` : pas de trou (R40) ; `parametres-*-message` : refus en rouge, confirmation en vert (R49) ; `duree-de-pause-invalide` : ligne rouge (R57).

### 3.1 Accueil, avant l’accord

- [ ] **R08** — Icône avant l’accord → grise, infobulle « Désactivé ». (kit E4)
- [ ] **R09** — Taper dans Win+R, fermer l’accueil par la croix, relancer depuis Démarrer ; refaire en fermant par Échap → clavier inchangé, l’accueil revient à chaque relance, aucun accord mémorisé. (kit A2 ; 24/09 l. 3)
- [ ] **R10** — Tab et Maj+Tab dans l’accueil → aucun lien fantôme ; drapeau focalisé avec un cadre, puis Entrée, Espace et clic → la langue bascule à chaque geste, le focus reste sur le drapeau. (kit B11 ; L1)
- [ ] **R11** — Tab jusqu’à « Activer et essayer », Entrée → tutoriel ouvert, remappage actif ; icône bleue, infobulle et bulle « Actif ». (kit A3, E4 ; 24/09 l. 3)

### 3.2 Tutoriel

- **R16** — couverte par `TutorielClavierTests.Les_infobulles_du_tutoriel_gardent_leurs_textes` et `LessonKeyboardTooltipTests`. (L9)
- [ ] **R17** — Tab puis Espace → le tutoriel reste ouvert. Au 5ᵉ ou 6ᵉ exercice (« Passer » affiché) : Tab, Tab, Entrée → l’exercice passe, la frappe reprend sans clic ; au suivant, Tab puis Maj+Tab → cadre sur « Quitter » puis « Passer », aucune faute comptée. (kit B8 ; 24/09 l. 6)
- [ ] **R18** — Roulement rapide sur deux touches → validées dans l’ordre ; fin : « Bravo ! », écran final et badges. (L1 ; L2)
- [ ] **R19** — Tab puis Entrée sur « Quitter les exercices » → le tutoriel se ferme. (24/09 l. 6)

### 3.3 Accueil, étape 3 et démarrage automatique

- [ ] **R20** — lire le verdict R20 dans `resultats.json` → OK (partiel) : premier accueil rejoué, case cochée à l’affichage, deux cases visibles à l’étape 3 ; « sans vide » sur l’image `accueil-etape3` (3.0). (kit E1 ; 24/09 l. 10)
- [ ] **R21** — Liens de l’étape 3 : survol → main et couleur ; Tab → cadre pointillé autour du texte, sans trace une fois le focus parti ; Entrée ouvre le lien. (kit B10 ; L8.3)
- [ ] **R22** — lire le verdict R22 dans `resultats.json` → OK : Tab jusqu’au lien « Guide », Échap, case laissée cochée → démarrage non activé, état lu par `StartupTask` dans le paquet. (25/09 l. 14)
- [ ] **R23** — lire le verdict R23 dans `resultats.json` → OK : croix, Échap (R22) et Alt+F4, case laissée cochée → le démarrage reste désactivé. (24/09 l. 2)
- [ ] **R24** — lire le verdict R24 dans `resultats.json` → OK : « C’est parti ! » → démarrage activé. (24/09 l. 2 ; kit E1)
- [ ] **R25** — lire le verdict R25 dans `resultats.json` → OK : rouvert, décoché, validé → désactivé ; `autoStartNudgeDone` posé, donc aucune relance ; état relu après relance. (kit E2)
- [ ] **R26** — lire le verdict R26 dans `resultats.json` → OK : refus « désactivé par l’utilisateur » posé par la valeur `State` = 1 de la clé `SystemAppData` du paquet, que `StartupTask` lit `DisabledByUser` → premier accueil avec la case décochée. Sur A VOIR (la tâche ne lit pas ce refus) : désactiver l’app dans Paramètres Windows › Applications › Démarrage, rouvrir l’accueil, étape 3 → case décochée. (kit E3)
- [ ] **R27** — 3 exercices faits, rouvrir l’accueil → « Essayer maintenant » n’est plus proposé ; tutoriel et accueil fermés, 15 s → aucune demande d’avis. (L9.5 ; 24/09 l. 13)

### 3.4 Frappe (Win+R, Edge)

- [ ] **R28** — `é è à ç ù`, Verr. Maj. + é → É, AltGr+E → €, AltGr+D/F → { }, ^ puis e → ê, ¨ puis i → ï ; Verr. Maj. intelligent ; NativeCombo → caractères attendus, aucun modificateur coincé. (kit A4 ; L3 ; Cahier § 7)
- [ ] **R29** — Alt+Tab rapide entre deux fenêtres en tapant, retour en moins d’1 s → chaque caractère dans la bonne fenêtre, remappage immédiat, pas de bulle en rafale. (kit A6 ; L2)
- [ ] **R30** — Edge, adresse `data:text/html,<input type=password><input>` : champ mot de passe puis champ normal → dans le premier, fonctions avancées suspendues, Ctrl+Maj+W et Ctrl+Maj+Q sans effet, aucune couche ; dans le second, disponibles. (kit A7 ; L3)
- [ ] **R31** — Maj+* puis a → α ; double appui → verrou et indicateur ; Échap déverrouille. (L1 ; Cahier § 7)
- [ ] **R32** — Quitter par l’icône, relancer depuis Démarrer → clavier système dès la sortie ; accord conservé, remappage revenu. (kit A5)

### 3.5 Recherche (juste après la relance de R32)

- [ ] **R33** — Ctrl+Maj+W, première requête de la session (l’index se charge alors), puis « é », « É », « flèche dr », « U+20AC », « espace insecable », une touche morte → bons résultats ; Entrée insère le bon symbole dans la fenêtre d’origine. (kit A8 ; L3 ; L5)
- [ ] **R34** — Clic droit sur l’icône › Rechercher un caractère › Entrée → caractère copié avec notification, rien dans la barre des tâches. (24/09 l. 4)
- **R35** — couverte par `SearchResultListTests.LePiedDitCombienDeCorrespondancesDepassentLePlafond` et `SousLePlafondLePiedGardeSesTextes`. (25/09 l. 16)
- **R36** — couverte par `SearchResultListTests.LeClicTombeSurLaLigneDessinee` et `UnSeparateurOuLeVideNeSelectionneRien`. (25/09 l. 15)

### 3.6 Menu de l’icône et Défi invisible

- [ ] **R37** — lire le verdict R37 dans `resultats.json` → OK (partiel) : Apprendre ▸ ne contient que Leçons et Revoir l’accueil, aucune entrée Défi ni Challenge, en FR et en EN ; ouverture des sous-menus à la souris non jugée (coup d’œil pendant R38). (kit A10 ; 24/09 l. 8 ; règle)
- [ ] **R38** — Désactiver (Ctrl+Maj+Verr. Maj.), réactiver, mettre en pause → l’infobulle de l’icône suit chaque état. (L3)
- [ ] **R39** — lire le verdict R39 dans `resultats.json` → OK (partiel) : opt-in coché, annonce effacée, relance → annonce non marquée, Mes statistiques sans section Défi ; module Défi des Leçons : `DefiDuJourMasqueTests.Le_module_Defi_n_entre_pas_dans_les_Lecons_meme_avec_l_opt_in`. (24/09 l. 9, 11 ; règle)
- [ ] **R40** — lire le verdict R40 dans `resultats.json` → OK (partiel) : case « Rappels d’entraînement » masquée, en FR et en EN (relevés MSAA) ; pas de trou : image `parametres-general` (3.0). (24/09 l. 10 ; règle)

### 3.7 Avis, essai 1 (config préparée, sans toucher l’horloge)

Si un toast d’avis est déjà apparu pendant la frappe réelle, c’est l’essai 1 légitime : le noter pour R44, puis, app quittée, `Cle reviewPromptCount 0; Cle reviewPromptLastShown $null; Cle reviewPromptClicked $false`.

- [ ] **R41** — lire le verdict R41 dans `resultats.json` → OK : `Usage 1`, relance → aucun avis au démarrage. (L7 ; ReviewPromptGate.cs)
- **R42** — couverte par `AvisApresSeanceTests.Refusee_pendant_une_seance` et `Les_frappes_d_exercice_seules_ne_declenchent_pas`. (24/09 l. 12 ; L7)
- **R43** — couverte par `AvisApresSeanceTests.Refusee_dans_les_dix_minutes_apres_la_fin` : plus d’attente de 10 min. (24/09 l. 12 ; L7)
- [ ] **R44** — App quittée, `Usage 1`, relancer ; plus de 10 min après la dernière fermeture du tutoriel ou des Leçons, majuscule accentuée dans Edge, puis 15 s sans frappe → toast d’avis (essai 1) ; clic → page d’avis du Store (Sandbox sans Store : noter ce qui s’ouvre) ; `reviewPromptCount` passe à 1. (L7 ; 24/09 l. 12)

### 3.8 Paramètres

- [ ] **R45** — lire le verdict R45 dans `resultats.json` → OK (partiel) : trois onglets au clavier, « Valeurs par défaut » par Entrée puis par Espace, réponse Non → `config.json` inchangé. Reste à l’œil (1 min) : focus visible sur chaque bouton poussoir, Tab puis Entrée, et retour du focus sur le bouton après l’annulation (il ne revenait pas seul au passage du 29/09). (kit A9 ; L6)
- [ ] **R46** — « Réinitialiser les raccourcis » : survol, puis clic et annulation → survol visible, geste pris en compte. (L1)
- [ ] **R47** — lire le verdict R47 dans `resultats.json` → OK : onglets 0, 1, 2, 1, 0 aux flèches, rectangle de la fenêtre inchangé, libellés traduits, en FR puis en EN. (kit E6 ; L6)
- [ ] **R48** — Applications : « Ajouter… » `C:\Windows\System32\mstsc.exe` → message visible ; « Forcer compatibilité » → refus **en rouge**, « Compatibilité jeu refusée : application protégée ou de connexion à distance », sur deux lignes en FR à 100 %, la seconde finissant par « distance » ; retirer l’app → message visible. (26/09 ; L6)
- [ ] **R49** — lire le verdict R49 dans `resultats.json` → OK (partiel) : « Touche réservée (conflit applications) » sur T, « Fenêtre clavier virtuel réinitialisée ✓ » dans Langue, message effacé à chaque changement d’onglet ; rouge et vert : `SettingsValidationColorTests` et images `parametres-*-message` (3.0). (26/09 ; L6)

### 3.9 Leçons

- **R50** — couverte par `Accessibilite130Tests.K4_BoutonIcône_RetrouveSonInfobulle` et `LessonsFocusCaptureTests.FenetreVisibleSansLeFocus_RienNEntreEnFile`. (kit B9 ; L3)
- [ ] **R51** — Fin de ligne et premier caractère de la suivante d’un trait, en strict puis en souple → rien de perdu, pas de pause ; indice immédiat. (L2)
- [ ] **R52** — Indices et infobulles de noms (caractères, touches mortes) ; lettre à touche morte → indice : armement puis touche finale ; avec une autre touche morte armée → l’indice revient à l’armement. (L1 ; L5 ; L9.4)
- [ ] **R53** — Leçons, Mes statistiques, puis remise à zéro → progression et compteurs cohérents. (kit B5)
- [ ] **R53a** — Initiation, exercice 4 → la ligne se colore à mesure, aucune barre de curseur, la lettre à taper soulignée (apostrophe comprise) ; une faute en strict puis en souple → la faute en rouge, l’attendu dessous. Tab : ligne → boutons → ligne ; une lettre ou un clic rend le cadre à la ligne. (lot surface ; images `lecons-surface-*`)
- [ ] **R53b** — Loupe de Windows (Windows + +), réglée pour suivre le curseur de texte : en tapant dans les Leçons, puis en mode Libre → la loupe suit la lettre à taper. (lot surface : caret caché, TextPattern2)
- [ ] **R53c** — Narrateur (Ctrl + Windows + Entrée), Leçons ouvertes → il annonce le champ « Texte à taper » et la consigne, et lit la ligne à la demande ; en mode Libre → « Mode libre ». Tab → chaque bouton annoncé par son nom (« Précédent », « Indice »…), les modules et leçons avec « sélectionné » sur celui affiché ; Espace ou Entrée de Narrateur sur « Paramètres » les ouvre, et un interrupteur y est lu coché ou non. Pas de blocage ni de lenteur de frappe pendant qu’il tourne. (lot surface : UIA)

### 3.10 Mes statistiques et À propos

- [ ] **R54** — Mes statistiques en FR puis EN → dates et milliers au format de la langue. (L3)
- **R55** — couverte par `LinkBehaviorTests` (survol, Entrée, `Échap_FermeLaFenêtre`) et `Accessibilite130Tests.K5_*`. (L8.3 ; kit B10)
- [ ] **R56** — À propos et Accueil → icône 32 px inchangée, dans la barre de titre et dans Alt+Tab. (L8.5)

### 3.11 Pause, Couches, Conflit

- [ ] **R57** — lire le verdict R57 dans `resultats.json` → OK (partiel) : fond clair, deux flèches Windows collées aux champs, Haut, Bas et molette de 5 en 5, « Reprise … » qui suit la saisie, Tab sur les deux champs et les boutons, 0 h 0 → bouton grisé et Entrée sans effet, Échap ferme ; ligne rouge : image `duree-de-pause-invalide` (3.0). (refonte du 28/09 ; L8.1 ; L4)
- [ ] **R57b** — lire le verdict R57b dans `resultats.json` → OK (partiel) : six entrées (« Jusqu’à demain 8 h », ou « Jusqu’à 8 h » avant 8 h), « Reprendre maintenant (reprise auto …) » pendant une pause, sous-menu grisé quand l’application est désactivée. Reste à l’œil, pendant R38 : la bulle « En pause pour … ». (refonte du 28/09)
- [ ] **R58** — lire le verdict R58 dans `resultats.json` → OK : focus initial sur la case principale, Espace, Tab et Maj+Tab jusqu’à « Enregistrer », Entrée puis Échap ferment en enregistrant. ECHEC au passage du 29/09 (enregistrement non relu dans `config.json`, écrit par lots) ; script retouché ensuite, non rejoué : sur ECHEC, geste d’origine. (kit B6)
- **R59** — couverte par `DialogNavigationKeyboardTrapTests.Entree_PresseLeBoutonFocalise`, `Entree_SurUnAutreControle_NePresseRien` et `Echap_ArriveCommeIdCancel` ; de toute façon ⏭️ dans le Sandbox, sans disposition système. (kit B7)

### 3.12 Toutes les fenêtres

- [ ] **R60** — lire le verdict R60 dans `resultats.json` → OK (partiel) : À propos, Mes statistiques, Paramètres, Accueil, Couches et Pause ouverts trois fois, rendus et répondants, `error.log` sans `RegisterClass` ; Conflit non ouvrable dans le Sandbox (voir R59). (L8.4)
- [ ] **R61** — Barre de titre sombre des fenêtres → inchangée. (L1)

### 3.13 Anglais

- [ ] **R62** — App en anglais : clavier des Leçons, des exercices et clavier virtuel → « Enter », « Caps Lock », « Shift ⇧ », « Space » ; bascule FR ↔ EN fenêtre ouverte → redessin immédiat, rien ne déborde. Revenir en FR. (kit E5)
- **R63** — couverte par `SearchResultListTests.LesMotsDeLaMethodeSeColorentDansLaLangueCourante`. (25/09 l. 17)

### 3.14 Échelle d’affichage réelle (Paramètres Windows › Écran, dans le Sandbox)

- [ ] **R64b** — À 150 % seulement, les autres échelles étant jugées sur les images (R64, 3.0) : Paramètres (trois onglets), Leçons, indicateur de couche (couche active dans Edge) et Recherche, puis passage de 100 à 150 % fenêtre ouverte → textes et contrôles à l’échelle, rien de coupé ni de superposé ; infobulles et largeurs justes. (kit B1, B13 ; L1 ; L3 ; L6)
- [ ] **R67** — Paramètres à 175 %, fenêtre du Sandbox réduite vers 1366×768 → logo, titres et contrôles défilent ensemble ; Tab fait défiler vers les contrôles masqués. (L6 ; kit E6)
- **R68** — couverte par `NativeWindowTests.Centrage_FenêtrePlusGrandeQueLaZone_PartDeSonCoin` et `WindowSizingTests.D3_200Pourcent_TientDansLÉcran`. (L8.6)

### 3.15 Narrateur

- [ ] **R69** — Narrateur (Ctrl+Win+Entrée) sur l’accueil, Paramètres, Pause et Recherche, en FR puis en EN → boutons, onglets et champs annoncés (« Heures », « Augmenter les heures », « Apps suspendues », « Rechercher un caractère »…) ; limites des surfaces dessinées notées, sans score. (kit B2, B12)

### 3.16 Avis, config 1.2 existante et essai 2

- [ ] **R70** — lire le verdict R70 dans `resultats.json` → OK : config 1.2 simulée (accord effacé, `reviewPromptCount` 1, date à J-3, `cleRecette` 42, raccourci F9, `Usage 12`) → accueil ouvert au lancement, accord à l’étape 3 ; relance : accord et réglages gardés. (kit B4 ; L7)
- [ ] **R71** — lire le verdict R71 dans `resultats.json` → OK (partiel) : après l’accord et 20 s, `reviewPromptCount` 1, date et `cleRecette` conservés, aucun avis ; branche « après frappe » : `Essai2_six_jours_apres_le_premier_refuse`. (L7)
- [ ] **R72** — lire le verdict R72 dans `resultats.json` → OK (partiel) : date à J-8, accueil fermé par l’accord → `reviewPromptCount` 2 et date du jour, sans erreur d’avis dans `error.log` ; affichage du toast non observé (coup d’œil facultatif). (L7)
- [ ] **R73** — lire le verdict R73 dans `resultats.json` → OK : compteur 0, seuils de l’essai 1 atteints, accueil fermé par l’accord → aucun avis après 20 s : l’essai 1 ne part jamais du démarrage. (L7)

### 3.17 Fichiers illisibles et sortie du Sandbox

- [ ] **R74** — lire le verdict R74 dans `resultats.json` → OK (partiel) : `{ invalid`, relance, Leçons ouvertes → une copie `lessons-progress.json.illisible-…` intacte, app vivante ; bulle unique : `QuarantaineTests.ProgressionCorrompue_MessageDemandéUneSeuleFois`. (L4)
- [ ] **R75** — lire le verdict R75 dans `resultats.json` → OK (partiel) : après A11, accueil ouvert, app inactive (menu « Activer »), accord au clavier, relance active (menu « Désactiver ») ; bulle unique : `QuarantaineTests.ConfigCorrompu_MessageDemandéUneSeuleFois`. ECHEC aux deux passages des 28 et 29/09 (premier plan refusé à l’accueil après le relevé du menu, voir `journal.txt`) : jouer le geste d’origine tant que ce verdict n’est pas OK. (L4 ; kit A11)
- **R76** — couverte par A12 (désinstallation, processus arrêté). Fin du Sandbox manuel : copier ce qui doit rester dans `C:\resultats\`, puis le fermer. (kit A12)

### 3.18 Deux écrans, sur le poste

Si la 1.1.0 du Store est installée sur le poste, l’installation est une vraie mise à jour qui garde ses données : sauvegarder `%LOCALAPPDATA%\Packages\AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg\` avant. Sans second écran : ⏭️ sur R77 à R79.

- [ ] **R77** — `powershell -ExecutionPolicy Bypass -File "$kit\installer-poste.ps1" -Bundle $b` → `installation-poste.txt` finit par « Pret », `shadow stack ON ; CFG ON` ; accepter l’accueil ; puis déplacer la Durée de pause, les Couches et les Paramètres d’un écran à 100 % vers un écran à 150 % → même taille apparente, textes nets. (installer-poste.ps1 ; L8.2 ; L6)
- [ ] **R78** — Tutoriel sur l’écran secondaire à 150 %, Windows à 100 % → « Quitter les exercices » aligné au bord droit. (L9.1)
- [ ] **R79** — Recherche ouverte sur chaque écran → elle reste sur l’écran où elle s’est ouverte. (L3)
- [ ] **R80** — Désinstaller du poste (Paramètres › Applications) avant le WACK → clavier système utilisable. (installer-poste.ps1 ; kit A12)

## 4. WACK (sur l’hôte)

- [ ] **R81** — PowerShell administrateur, AZERTY Global quitté, à la racine du dépôt (`$kit` et `$b` redéfinis) : `powershell -ExecutionPolicy Bypass -File "$kit\wack.ps1" -Bundle $b` → `$kit\evidence\wack-<12>\resume.txt` : empreinte de l’en-tête, `OVERALL_RESULT : PASS`. (kit A13 ; wack.ps1)
- [ ] **R82** — Lignes non PASS de `resume.txt` → seule « Blocked executables » (optionnelle, `ShellExecuteW`) admise. (Publication Microsoft Store.md ; Fiche Store, notes de certification)

## 5. Verify-Release

- [ ] **R83** — lire le verdict R83 dans `hote\resultats.json` (joué par `verifier-candidat.ps1` en R01) → OK : `SHA256 x64` et `SHA256 arm64` à `publish = bundle`, « Release vérifiée: version 1.3.0 / package 1.3.0.0 », run conclu en succès. Ne pas lancer `scripts\Verify-Release.ps1` en local : il compare le publish local au bundle de `msix\` et échouerait sur le bundle CI. (Verify-Release.ps1 ; ci.yml:85)

## 6. App Installer sur machine propre (seulement si demandé)

- [ ] **R84** — Par défaut ⏭️ : ce canal ne concerne pas le Store, et `gen-appinstaller.py` refuse le bundle Store. Si demandé : copie signée AMCF (msix/README.md, étape 8), `python scripts/gen-appinstaller.py --bundle <signé> --out msix\AZERTY_Global.appinstaller`, puis `--check`, installation sur une VM neuve → installation, puis montée de version constatée. (msix/README.md, étape 9)

## 7. Partner Center (arrêt humain : Antoine soumet)

- [ ] **R85** — Nouvelle soumission, Paquets → téléverser **ce** bundle (empreinte de l’en-tête) ; x64 et ARM64, ARM64 publié avec l’écart documenté (compilé et analysé, jamais exécuté nativement). (PC ; knowledge/status/microsoft-store.md ; kit C)
- [ ] **R86** — Déclaration « tested to meet accessibility guidelines » → **décochée** (décision du 22/09). (PC ; accessibilite-1.3.0.md)
- [ ] **R87** — Fiche FR et EN : variante B de `msix/Fiche Store.md` (descriptions, points forts, Nouveautés et What’s new) collée telle quelle ; les textes collés ne contiennent ni « Défi » ni « challenge ». Captures : hors de cette recette. (PC ; règle)
- [ ] **R88** — Notes de certification (Additional Testing Info) : texte anglais de la fiche, les deux marqueurs « [à vérifier …] » retirés une fois R09 et R81 vérifiées. (PC ; Fiche Store)
- [ ] **R89** — Propriétés, IARC et confidentialité inchangés ; Publication « Dès la certification » conservée ; soumettre. Tag `v1.3.0` et fusion après la certification. (PC ; README feu-vert)

## Optionnels ou longs

- [ ] **R90** — Essai 1 hors package : `AZERTY Global.exe` extrait du `.msix` x64 et lancé hors package, seuils de R41 posés dans `%LOCALAPPDATA%\AZERTY Global\` → bulle au lieu du toast ; clic → page feedback au lieu du Store. (L7)
- [ ] **R91** — Mise à jour, sur le poste : `installer-poste.ps1` avec l’ancien candidat (`msix\ci-35987701131\…`), 3 exercices d’Initiation réussis, copier `config.json` et `lessons-progress.json`, installer ce candidat, app quittée, remettre les deux fichiers, relancer → exercices toujours cochés, pas d’« Essayer maintenant ». (L9.5)
- [ ] **R92** — Hôte distant simulé : `Copy-Item C:\Windows\System32\PING.EXE $env:TEMP\parsecd.exe; Start-Process $env:TEMP\parsecd.exe '-t 127.0.0.1'`, puis AutoHotkey v2 `SendText "(3) x"` dans un champ, Verr. Maj. éteint puis allumé → texte intact. (24/09 l. 1)
- [ ] **R93** — Jeu anti-triche réel, sur le poste : avant l’accord, aucune bulle ; après l’activation, retour dans le jeu → bulle de suspension et frappe non remappée. (24/09 l. 7)
- [ ] **R94** — `usage-stats.json`, puis `config.json`, ouverts en exclusif (`$f = [IO.File]::Open("<chemin>", 'Open', 'Read', 'None')`), taper, quitter, `$f.Close()`, relancer → chiffres et fichier inchangés, aucune copie `.illisible-…`. (25/09 l. 18 ; L4 b)
- [ ] **R95** — VM ou poste : changer un réglage puis se déconnecter aussitôt, se reconnecter → réglage gardé. (L4 c)
- [ ] **R96** — Couches dans Word ou Excel, Chrome, Edge, Firefox et VS Code : les cinq gestes de la liste « Couches maintenables ». (Cahier § 7)
- **R97** — couverte par `DefiDuJourMasqueTests.Le_rappel_d_entrainement_ne_part_pas_quand_le_Defi_est_masque` et `Le_rappel_lit_l_interrupteur_meme_opt_in_coche`. (24/09 l. 9)
- [ ] **R98** — Contraste élevé → état noté ; dette connue, non bloquante. (kit B3)

## Si ça échoue

- **Dans la case** : ❌, le geste exact, la langue, l’échelle et l’écran. Les lignes issues de la partie A du kit, R01 à R03 et R81 bloquent la soumission ; les autres ❌ se trient avant de soumettre.
- **Capture** : Win+Maj+S, enregistrée sous `C:\resultats\manuel\R<nn>.png` dans le Sandbox (c’est `$kit\evidence\recette-<12>\` sur l’hôte) ; sur le poste, directement dans `$kit\evidence\recette-<12>\manuel\`.
- **Fichiers** : copier `$d\error.log`, `config.json` et le fichier en cause (`usage-stats.json`, `lessons-progress.json`, `*.illisible-*`) dans le même dossier, préfixés `R<nn>-`, **avant** de fermer le Sandbox, qui efface tout.
- **Ligne automatisée** : copier `auto\journal.txt`, `resultats.json` et `resume-auto.txt` (ou `hote\journal.txt`) dans la note, avec l’observation de la ligne.
- **Empreinte** : la reporter en tête de la note, lue dans `installation.txt` ou `resume.txt`. Si elle diffère de l’en-tête, toute la recette est à refaire.
- **Fiche cochée** : la copier dans `$kit\evidence\recette-<12>\RECETTE-1.3.0.md`.
