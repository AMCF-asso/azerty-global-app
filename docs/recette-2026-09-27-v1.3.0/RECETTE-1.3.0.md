# Recette 1.3.0 — fiche unique

- **Candidat** : commit `2752799` (`2752799d6c62de9739a9d674cfa9958e795e3e55`), branche `release/1.2.0-notation-store`, CI [36271106009](https://github.com/AMCF-asso/azerty-global-app/actions/runs/36271106009) verte (Pack MSIX, Verify Release, SHA256 artefacts, attestation). `34f35af`, au-dessus, n’ajoute que le Changelog (`[skip ci]`).
- **Bundle** : `AZERTYGlobal-1.3.0.0.msixbundle` — **SHA-256 : D6D8A420794D0C9C17BAFD01B90FF361D9818F5DBAF24D054C4028D3728974C6** (mesurée le 26/09 sur le fichier téléchargé, identique au journal CI) (troisième ligne `Get-FileHash` du journal CI). Une recompilation change l’empreinte et annule la recette.
- **Durée estimée** : 3 h 45 environ (bundle 10 min, automatique 15 min, gestes 2 h 30, WACK 20 min, Verify 5 min, Partner Center 25 min). Optionnels : 1 h 30 de plus.
- **Matériel** : le poste, devant la machine ; Parsec exclu (la ligne E7 du kit n’est pas jouée). Windows Sandbox activé, SDK 10.0.26100 (`signtool`), App Certification Kit, `gh` connecté à un compte qui lit `AMCF-asso/azerty-global-app`. Deux écrans d’échelles différentes (100 % et 150 %) si possible, pour la section 3.18 : le Sandbox n’a qu’un écran. Une VM remplace le Sandbox seulement si le certificat de test y est approuvé, comme sur le poste (`installer-poste.ps1` l’exige).
- **Avant tout** : quitter AZERTY Global sur l’hôte. Les consoles de VM ne sont pas reconnues comme accès distant (report 1.3.1).
- **Noter** : ✅, ❌ + observation, ⏭️ + raison. Sources : *kit* = `feu-vert/recette.md` (A, B, E) ; *24/09* et *25/09* = lignes de recette des rapports d’audit ; *L1* à *L9* = lots ; *26/09* = correctifs des Paramètres ; *règle* = règle 1.3.0 ; *PC* = Partner Center.

Chemins, dans un PowerShell ouvert à la racine du dépôt `D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store` :

```powershell
$kit = "docs\audit-2026-09-22-v1.3.0\feu-vert"
$b   = "msix\ci-36271106009\AZERTYGlobal-1.3.0.0.msixbundle"
```

## 1. Installation et vérification du bundle

- [ ] **R01** — `gh run download 36271106009 -R AMCF-asso/azerty-global-app -n msixbundle -D msix\ci-36271106009` → un seul fichier, `AZERTYGlobal-1.3.0.0.msixbundle`. (README feu-vert, étape 1 ; ci.yml)
- [ ] **R02** — `(Get-FileHash $b -Algorithm SHA256).Hash` → égale à l’empreinte de l’en-tête et à la 3ᵉ ligne `Get-FileHash` de l’étape « SHA256 artefacts » du run. (rapport 25/09, « Candidat »)
- [ ] **R03** — `gh attestation verify $b -R AMCF-asso/azerty-global-app` → vérification réussie, dépôt source à `2752799d…`. (README feu-vert, étape 1)

## 2. Recette automatique (Sandbox)

- [ ] **R04** — `powershell -ExecutionPolicy Bypass -File "$kit\sandbox\lancer-recette.ps1" -Bundle $b -Auto` → le Sandbox installe, joue, désinstalle ; `done.txt` paraît dans `$kit\evidence\recette-<12>\auto\`. Fermer ensuite le Sandbox. (lancer-recette.ps1)
- [ ] **R05** — `$kit\evidence\recette-<12>\installation.txt` → SHA-256 du candidat, `…_1.3.0.0_x64__w9kghr08zmhbg`, app lancée, `shadow stack ON ; CFG ON`. (kit A1)
- [ ] **R06** — `auto\resultats.json` → A1, A2, A3, A10 (12 lignes FR et EN), A12 et B12 à OK, A5 à « OK (partiel) ». La frappe remappée n’y est pas jugée. (kit A2, A3, A5, A10, A12, B12)
- [ ] **R07** — A11 dans `resultats.json` → **OK (partiel) attendu** : depuis le 28/09, le script vérifie la copie `config.json.illisible-…` qui contient `[1]`, et un `config.json` qui n’est plus `[1]` (lot 4). L’accueil, l’app inactive jusqu’à l’accord et la bulle unique se jugent à la main en R75. (recette-auto.ps1, section A11 ; L4)

## 3. Gestes manuels

Ouvrir un Sandbox neuf, sans `-Auto` : `powershell -ExecutionPolicy Bypass -File "$kit\sandbox\lancer-recette.ps1" -Bundle $b`. Premier lancement : ne pas toucher l’accueil avant R08. Le Sandbox n’a pas de Bloc-notes : Win+R, la barre d’adresse et un champ de texte d’Edge le remplacent (kit D). Dans un PowerShell du Sandbox, coller ces outils ; toujours quitter l’app (menu ▸ Quitter) avant d’écrire un fichier :

```powershell
$d = "$env:LOCALAPPDATA\Packages\AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg\LocalCache\Local\AZERTY Global"   # sinon "$env:LOCALAPPDATA\AZERTY Global"
$j = Get-Date -Format yyyy-MM-dd; $j3 = (Get-Date).AddDays(-3).ToString('yyyy-MM-dd'); $j8 = (Get-Date).AddDays(-8).ToString('yyyy-MM-dd')
function Cle($k, $v) { $o = Get-Content "$d\config.json" -Raw | ConvertFrom-Json; if ($null -eq $v) { $o.PSObject.Properties.Remove($k) } else { $o | Add-Member -NotePropertyName $k -NotePropertyValue $v -Force }; [IO.File]::WriteAllText("$d\config.json", ($o | ConvertTo-Json -Depth 10)) }
function Usage($jours) { [IO.File]::WriteAllText("$d\usage-stats.json", ('{{"firstRemapDate":"{0}","lastActiveDate":"{0}","activeDaysCount":{1},"totalActiveMinutes":10,"accentedUppercaseCount":20}}' -f $j, $jours)) }
```

### 3.1 Accueil, avant l’accord

- [ ] **R08** — Icône avant l’accord → grise, infobulle « Désactivé ». (kit E4)
- [ ] **R09** — Taper dans Win+R, fermer l’accueil par la croix, relancer depuis Démarrer ; refaire en fermant par Échap → clavier inchangé, l’accueil revient à chaque relance, aucun accord mémorisé. (kit A2 ; 24/09 l. 3)
- [ ] **R10** — Tab et Maj+Tab dans l’accueil → aucun lien fantôme ; drapeau focalisé avec un cadre, puis Entrée, Espace et clic → la langue bascule à chaque geste, le focus reste sur le drapeau. (kit B11 ; L1)
- [ ] **R11** — Tab jusqu’à « Activer et essayer », Entrée → tutoriel ouvert, remappage actif ; icône bleue, infobulle et bulle « Actif ». (kit A3, E4 ; 24/09 l. 3)

### 3.2 Tutoriel

- [ ] **R12** — Les 6 exercices → surlignage direct ; étape 1 en orange avec la pastille « 1 », étape 2 en vert avec la pastille « 2 ». (L9)
- [ ] **R13** — Exercices 1 et 2 → Verr. Maj. pleine. (L9)
- [ ] **R14** — Armer une touche morte → les touches montrent le résultat avec les touches tenues, AltGr compris ; leur nom passe en jaune. (L9)
- [ ] **R15** — Exercice 6 : ã, puis armer l’accent aigu → Retour arrière surligné ; « ◌ » barré visible, cercle et barre superposés. (L9)
- [ ] **R16** — Survol → infobulle de Retour arrière (désactivé) et « — TOUCHE MORTE ». (L9)
- [ ] **R17** — Tab puis Espace → le tutoriel reste ouvert. Au 5ᵉ ou 6ᵉ exercice (« Passer » affiché) : Tab, Tab, Entrée → l’exercice passe, la frappe reprend sans clic ; au suivant, Tab puis Maj+Tab → cadre sur « Quitter » puis « Passer », aucune faute comptée. (kit B8 ; 24/09 l. 6)
- [ ] **R18** — Roulement rapide sur deux touches → validées dans l’ordre ; fin : « Bravo ! », écran final et badges. (L1 ; L2)
- [ ] **R19** — Tab puis Entrée sur « Quitter les exercices » → le tutoriel se ferme. (24/09 l. 6)

### 3.3 Accueil, étape 3 et démarrage automatique

- [ ] **R20** — Étape 3 → case « Lancer au démarrage de Windows » cochée ; deux cases seulement, sans vide. (kit E1 ; 24/09 l. 10)
- [ ] **R21** — Liens de l’étape 3 : survol → main et couleur ; Tab → cadre pointillé autour du texte, sans trace une fois le focus parti ; Entrée ouvre le lien. (kit B10 ; L8.3)
- [ ] **R22** — Tab jusqu’au lien « Guide », Échap → l’accueil se ferme ; case laissée cochée sans y toucher, donc démarrage non activé. (25/09 l. 14)
- [ ] **R23** — Apprendre ▸ Revoir l’accueil, étape 3, case laissée cochée : fermer par la croix, puis par Échap, puis par Alt+F4 → le démarrage reste désactivé dans Paramètres Windows › Applications › Démarrage. (24/09 l. 2)
- [ ] **R24** — Rouvrir, étape 3, « C’est parti ! » → démarrage activé dans Windows. (24/09 l. 2 ; kit E1)
- [ ] **R25** — Rouvrir, décocher, valider, relancer l’app → démarrage désactivé, aucune relance qui propose de l’activer. (kit E2)
- [ ] **R26** — Désactiver l’app dans Paramètres Windows › Démarrage, rouvrir l’accueil, étape 3 → case décochée. (kit E3)
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
- [ ] **R35** — « cyrillique » → pied « 20 sur 98 résultats — Entrée pour insérer » ; une requête à moins de 20 résultats → « N résultats ». (25/09 l. 16)
- [ ] **R36** — « cyrillique », à 100 %, avec la Loupe : clic sur la dernière rangée de pixels de la 2ᵉ ligne → caractère de la 2ᵉ ligne ; clic sur un séparateur → rien. (25/09 l. 15)

### 3.6 Menu de l’icône et Défi invisible

- [ ] **R37** — Menu : Couches ▸, Apprendre ▸, À propos et aide ▸ s’ouvrent à la souris ; Apprendre ▸ ne contient que Leçons et Revoir l’accueil ; aucune entrée ne parle du Défi. (kit A10 ; 24/09 l. 8 ; règle)
- [ ] **R38** — Désactiver (Ctrl+Maj+Verr. Maj.), réactiver, mettre en pause → l’infobulle de l’icône suit chaque état. (L3)
- [ ] **R39** — App quittée : `Cle trainingEnabled $true; Cle challengeAnnounceDone $null`, relancer → aucune bulle « Nouveau : le Défi du jour », pas de module Défi dans les Leçons, pas de section Défi dans Mes statistiques. (24/09 l. 9, 11 ; règle)
- [ ] **R40** — Paramètres › Général → pas de case « Rappels d’entraînement », pas de trou. (24/09 l. 10 ; règle)

### 3.7 Avis, essai 1 (config préparée, sans toucher l’horloge)

Si un toast d’avis est déjà apparu pendant la frappe réelle, c’est l’essai 1 légitime : le noter pour R44, puis, app quittée, `Cle reviewPromptCount 0; Cle reviewPromptLastShown $null; Cle reviewPromptClicked $false`.

- [ ] **R41** — App quittée, `Usage 1` (20 caractères enrichis, 10 min actives ; `config.json` de ce premier lancement gardé), relancer → aucun avis au démarrage. (L7 ; ReviewPromptGate.cs)
- [ ] **R42** — Apprendre ▸ Leçons : taper plus de 20 caractères enrichis, fermer, noter l’heure → aucune demande après 15 s. (24/09 l. 12 ; L7)
- [ ] **R43** — Pendant les 10 min qui suivent, jouer 3.8 en tapant (sans valider de réinitialisation) → aucune demande d’avis. (24/09 l. 12 ; L7)
- [ ] **R44** — Plus de 10 min après, majuscule accentuée dans Edge, puis 15 s sans frappe → toast d’avis (essai 1) ; clic → page d’avis du Store (Sandbox sans Store : noter ce qui s’ouvre) ; `reviewPromptCount` passe à 1. (L7 ; 24/09 l. 12)

### 3.8 Paramètres (pendant l’attente de R43)

- [ ] **R45** — Trois onglets au clavier, « Réinitialiser » par Entrée puis Espace, annuler ; Entrée sur chaque bouton poussoir → focus visible, confirmation, rien ne change à l’annulation. (kit A9 ; L6)
- [ ] **R46** — « Réinitialiser les raccourcis » : survol, puis clic et annulation → survol visible, geste pris en compte. (L1)
- [ ] **R47** — Passer d’un onglet à l’autre, en FR puis en EN → la fenêtre ne change ni de taille ni de place, libellés des onglets traduits. (kit E6 ; L6)
- [ ] **R48** — Applications : « Ajouter… » `C:\Windows\System32\mstsc.exe` → message visible ; « Forcer compatibilité » → refus **en rouge**, « Compatibilité jeu refusée : application protégée ou de connexion à distance », sur deux lignes en FR à 100 %, la seconde finissant par « distance » ; retirer l’app → message visible. (26/09 ; L6)
- [ ] **R49** — Raccourci réservé (Ctrl+Maj+T) → « Touche réservée (conflit applications) » en rouge ; puis Langue › « Réinitialiser clavier virtuel » → confirmation **en vert** ; changer d’onglet efface le message. (26/09 ; L6)

### 3.9 Leçons

- [ ] **R50** — Au clavier seul, Tab jusqu’aux boutons-icônes → même infobulle qu’au survol ; un mouvement de souris rend l’infobulle de survol ; Leçons sans le focus → aucune frappe captée. (kit B9 ; L3)
- [ ] **R51** — Fin de ligne et premier caractère de la suivante d’un trait, en strict puis en souple → rien de perdu, pas de pause ; indice immédiat. (L2)
- [ ] **R52** — Indices et infobulles de noms (caractères, touches mortes) ; lettre à touche morte → indice : armement puis touche finale ; avec une autre touche morte armée → l’indice revient à l’armement. (L1 ; L5 ; L9.4)
- [ ] **R53** — Leçons, Mes statistiques, puis remise à zéro → progression et compteurs cohérents. (kit B5)

### 3.10 Mes statistiques et À propos

- [ ] **R54** — Mes statistiques en FR puis EN → dates et milliers au format de la langue. (L3)
- [ ] **R55** — À propos et Mes statistiques : survol d’un lien → main et couleur ; Tab jusqu’au lien, Entrée ouvre, Échap ferme ; cadre de focus dans À propos. (L8.3 ; kit B10)
- [ ] **R56** — À propos et Accueil → icône 32 px inchangée, dans la barre de titre et dans Alt+Tab. (L8.5)

### 3.11 Pause, Couches, Conflit

- [ ] **R57** — Menu ▸ Mettre en pause ▸ « Personnaliser… », à 100 % → fond clair, pas noir ; flèches Windows à droite de chaque champ ; flèches du clavier et molette changent la valeur (minutes de 5 en 5) ; « Reprise à … » suit la saisie ; 0 h 0 → ligne rouge et bouton grisé, Entrée sans effet ; Tab parcourt les deux champs et les boutons ; Échap ferme. (refonte du 28/09 ; L8.1 ; L4)
- [ ] **R57b** — Menu ▸ Mettre en pause : 15 minutes, 30 minutes, 1 heure, 2 heures, « Jusqu’à demain 8 h » (avant 8 h : « Jusqu’à 8 h »), Personnaliser… ; un choix → bulle « En pause pour … » ; le menu montre alors « Reprendre maintenant (reprise auto à …) » ; application désactivée → sous-menu grisé. (refonte du 28/09)
- [ ] **R58** — Couches ▸ Configurer… : Tab, Maj+Tab, Espace sur une case, Entrée ; rouvrir, Échap → focus initial sur la case principale, circulation entre cases, champ et « Enregistrer » ; Entrée et Échap ferment en enregistrant. (kit B6)
- [ ] **R59** — Conflit (disposition système AZERTY Global active au lancement, sinon ⏭️) : Tab jusqu’à « Garder l’application », Entrée ; rouvrir, Échap ; rouvrir, Entrée sans Tab → Entrée presse le bouton focalisé, Échap garde l’app, Entrée sans focus ne fait rien. (kit B7)

### 3.12 Toutes les fenêtres

- [ ] **R60** — Ouvrir, fermer, puis rouvrir deux fois : À propos, Mes statistiques, Paramètres, Accueil, Conflit, Couches, Pause → aucune fenêtre vide ou figée ; `Select-String -Path "$d\error.log" -Pattern "NativeWindow.RegisterClass"` → rien. (L8.4)
- [ ] **R61** — Barre de titre sombre des fenêtres → inchangée. (L1)

### 3.13 Anglais

- [ ] **R62** — App en anglais : clavier des Leçons, des exercices et clavier virtuel → « Enter », « Caps Lock », « Shift ⇧ », « Space » ; bascule FR ↔ EN fenêtre ouverte → redessin immédiat, rien ne déborde. (kit E5)
- [ ] **R63** — Recherche en anglais d’un caractère tapé avec Maj ou après une touche morte → « Shift » et « then » ont les couleurs de « Maj » et « puis » en français. Revenir en FR. (25/09 l. 17)

### 3.14 Échelle d’affichage (Paramètres Windows › Écran, dans le Sandbox)

- [ ] **R64** — À 125, 150, 175 puis 200 % : Paramètres (trois onglets), Leçons, Pause, Couches, indicateur de couche (couche active dans Edge), Accueil, À propos, puis changement d’échelle fenêtre ouverte → textes et contrôles à l’échelle, rien de coupé ni de superposé ; à 150 %, infobulles et largeurs justes. (kit B1, B13 ; L1 ; L3 ; L6)
- [ ] **R65** — Sandbox plein écran sur 1920×1080, Leçons à 150 % puis 175 % → le clavier dessiné ne recouvre ni la ligne cible ni la saisie ; Leçons entièrement dans l’écran, commandes du bas atteignables. (24/09 l. 5 ; kit B13)
- [ ] **R66** — Pause à 175 % → mêmes attendus que R57. (L8.1)
- [ ] **R67** — Paramètres à 175 %, fenêtre du Sandbox réduite vers 1366×768 → logo, titres et contrôles défilent ensemble ; Tab fait défiler vers les contrôles masqués. (L6 ; kit E6)
- [ ] **R68** — À 200 % en 1366×768, fenêtre plus grande que la zone de travail (Leçons ou Accueil) → elle s’ouvre depuis son coin, barre de titre visible. Revenir à 100 %. (L8.6)

### 3.15 Narrateur

- [ ] **R69** — Narrateur (Ctrl+Win+Entrée) sur l’accueil, Paramètres, Pause et Recherche, en FR puis en EN → boutons, onglets et champs annoncés (« Heures », « Augmenter les heures », « Apps suspendues », « Rechercher un caractère »…) ; limites des surfaces dessinées notées, sans score. (kit B2, B12)

### 3.16 Avis, config 1.2 existante et essai 2

- [ ] **R70** — App quittée, config 1.2 simulée : `Cle activationConsent $null; Cle currentVersionFirstRunDate $null; Cle reviewPromptCount 1; Cle reviewPromptLastShown $j3; Cle reviewPromptClicked $false; Cle cleRecette 42`, un raccourci changé, `Usage 12` ; relancer → accord demandé une fois, réglages conservés. (kit B4 ; L7)
- [ ] **R71** — Après l’accord, relire `config.json` → `reviewPromptCount` 1, la date et `cleRecette` conservés ; aucun avis au démarrage, à la fermeture de l’accueil, ni après frappe et 15 s (3 jours, moins de 7). (L7)
- [ ] **R72** — App quittée : `Cle activationConsent $null; Cle reviewPromptLastShown $j8`, relancer, fermer l’accueil → toast de l’essai 2. (L7)
- [ ] **R73** — App quittée : `Cle activationConsent $null; Cle reviewPromptCount 0; Cle reviewPromptLastShown $null; Cle reviewPromptClicked $false`, relancer, fermer l’accueil → aucun avis : l’essai 1 ne part jamais du démarrage. (L7)

### 3.17 Fichiers illisibles et sortie du Sandbox

- [ ] **R74** — App quittée, `[IO.File]::WriteAllText("$d\lessons-progress.json", '{ invalid')`, relancer, ouvrir les Leçons → bulle « Progression remise à zéro » une seule fois, copie `.illisible-…` gardée. (L4)
- [ ] **R75** — A11 : app quittée, `[IO.File]::WriteAllText("$d\config.json", '[1]')`, relancer → l’app démarre, `error.log` porte `JsonException`, `config.json.illisible-…` contient `[1]`, l’accueil s’ouvre, app inactive jusqu’à l’accord, bulle « Réglages remis à zéro » une fois ; après l’accord et une relance, app active sans bulle. (L4 ; kit A11)
- [ ] **R76** — Désinstaller par Paramètres › Applications → processus arrêté, clavier système utilisable. Copier ce qui doit rester dans `C:\resultats\`, puis fermer le Sandbox. (kit A12)

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

- [ ] **R83** — `gh run view 36271106009 -R AMCF-asso/azerty-global-app --log | Select-String "publish = bundle|package 1\.3\.0\.0"` → `SHA256 x64` et `SHA256 arm64` à `publish = bundle`, puis « Release vérifiée: version 1.3.0 / package 1.3.0.0 ». Ne pas lancer `scripts\Verify-Release.ps1` en local : il compare le publish local au bundle de `msix\` et échouerait sur le bundle CI. (Verify-Release.ps1 ; ci.yml:85)

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
- [ ] **R97** — Recette jouée après 17 h, Défi masqué (R39) → aucun rappel d’entraînement. (24/09 l. 9)
- [ ] **R98** — Contraste élevé → état noté ; dette connue, non bloquante. (kit B3)

## Si ça échoue

- **Dans la case** : ❌, le geste exact, la langue, l’échelle et l’écran. Les lignes issues de la partie A du kit, R01 à R03 et R81 bloquent la soumission ; les autres ❌ se trient avant de soumettre.
- **Capture** : Win+Maj+S, enregistrée sous `C:\resultats\manuel\R<nn>.png` dans le Sandbox (c’est `$kit\evidence\recette-<12>\` sur l’hôte) ; sur le poste, directement dans `$kit\evidence\recette-<12>\manuel\`.
- **Fichiers** : copier `$d\error.log`, `config.json` et le fichier en cause (`usage-stats.json`, `lessons-progress.json`, `*.illisible-*`) dans le même dossier, préfixés `R<nn>-`, **avant** de fermer le Sandbox, qui efface tout.
- **Empreinte** : la reporter en tête de la note, lue dans `installation.txt` ou `resume.txt`. Si elle diffère de l’en-tête, toute la recette est à refaire.
- **Fiche cochée** : la copier dans `$kit\evidence\recette-<12>\RECETTE-1.3.0.md`.
