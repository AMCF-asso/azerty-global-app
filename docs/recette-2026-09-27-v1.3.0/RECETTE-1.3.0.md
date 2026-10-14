# Recette 1.3.0 — fiche unique (candidat du 14/10)

> **En bref**
> 1. Ce soir : recette complète du pré-candidat de `lot/tutoriel-textes`, 2 h 30 environ (estimation, jamais chronométrée). Vendredi de 16 h à 18 h : seulement les cases que touchent les commits ajoutés depuis (`CASES-A-REJOUER.md`), plus R01-R03 et R81-R83.
> 2. Tri par priorité. **P0** bloque la soumission : R01-R03, R81-R83. **P1** : les nouveautés du candidat, jamais recettées. **P2** : les essais exigés le 05/10. **P3** : la non-régression. Si le temps manque, couper par le bas.
> 3. L’ordre de jeu n’est pas l’ordre de priorité : le WACK passe en dernier, car il installe puis retire le paquet sur l’hôte (§ 0).
> 4. La recette automatique a été alignée le 14/10 sur l’accueil en 4 étapes, le Défi réactivé et les Paramètres en 4 onglets, sans être rejouée (aucun bundle n’existait). Ses lignes R20, R22-R26, R37, R39, R40, R47, R49, R58 et R70-R73 ont donc chacune un geste de repli (§ 2).
> 5. Quatre lignes décident des textes publics de jeudi : R09 et R81-R82 (marqueurs des notes de certification), R135 (consigne « Pause » sous l’arabe), R134 (Ctrl droit + Alt gauche).

- **Candidat** : le pré-candidat du 14/10, soit `lot/tutoriel-textes` après le lot 5 d’A1. Le bundle est construit par `ci.yml`, lancé à la main sur la branche. Pour le run et son commit, voir la commande ci-dessous. La recette du 06/10 sur `ac4ff72` est annulée, et `113ad17` (bundle `FBD052CC…`) est remplacé. Depuis `113ad17`, 76 commits : Leçons « Tablature », notation des touches, Aujourd’hui, Votre carte, Votre texte, recherche par langue, Défi du jour réactivé, tutoriel par usage, accueil en 4 étapes, Paramètres en 4 onglets, clavier virtuel « papier », bulles de la zone de notification, consoles de VM et Sandbox.
- **Bundle** : `AZERTYGlobal-1.3.0.0.msixbundle` du run. **SHA-256 : `________________`**, lue par `verifier-candidat.ps1` (R02) puis reportée ici. Une recompilation change l’empreinte et annule la recette.
- **Durée estimée** (estimation, pas une mesure) : P0 hors WACK 5 min ; recette automatique 6 à 8 min sans surveillance (mesuré le 28/09) ; P1 55 min ; P2 45 min ; WACK 25 min ; P3 selon le reste. Total visé : 2 h 30.
- **Avant tout** : quitter AZERTY Global sur l’hôte pendant le travail dans le Sandbox. Depuis `d2a9ce8` et `1b342f6`, une app de l’hôte se suspend quand une fenêtre de VM ou du Sandbox est au premier plan (comportement voulu, jamais observé : ligne R126).
- **Noter** : ✅, ❌ + observation, ⏭️ + raison. Sources : *kit* = `feu-vert/recette.md` ; *05/10* = `docs/audit-2026-10-04-v1.3.0-final/decisions-qcm.md` (lots 2 et 7) et ses axes ; *L1* à *L9* = lots ; *PC* = Partner Center.
- **Lignes automatisées** (« lire le verdict ») : reporter le niveau lu (OK, OK (partiel), A VOIR ou ECHEC). Sur A VOIR ou ECHEC, jouer le geste de repli de la ligne. Lignes « couverte par … » : rien à jouer, le test cité tourne dans la CI du candidat.

Chemins, dans un PowerShell ouvert à la racine du dépôt (`components\microsoft-store` ou un worktree du même dépôt) :

```powershell
gh run list -R AMCF-asso/azerty-global-app -w ci.yml -b lot/tutoriel-textes -e workflow_dispatch -L 1
$kit = "docs\audit-2026-09-22-v1.3.0\feu-vert"
$run = "<databaseId lu ci-dessus>"
$b   = "msix\ci-$run\AZERTYGlobal-1.3.0.0.msixbundle"
```

## 0. Préparer avant 18 h 15, puis l’ordre de la soirée

À préparer (gestes système, à faire soi-même) :

- [ ] Sauvegarder `%LOCALAPPDATA%\Packages\AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg\` : la 1.1.0 du Store y garde ta configuration réelle. Relevé du 14/10 à 14 h 40 : la 1.1.0.0 (Store) est installée et tourne.
- [ ] Ajouter la disposition « Arabe (102) AZERTY » (Paramètres › Heure et langue › Langue › ajouter l’arabe, puis la disposition), pour R135. Relevé du 14/10 : seules les dispositions US et Français sont installées.
- [ ] Démarrer la VM ou le poste Windows 10 22H2, pour R140. Sans Windows 10, R140 passe ⏭️.
- [ ] Vérifier que le run du candidat est vert (`gh run view <run>`).

Ordre de jeu (durées estimées) :

| Quand | Où | Lignes |
|---|---|---|
| 18 h 15 | hôte | P0 : R01-R03 et R83 (5 min), puis R04 (recette automatique, sans surveillance) |
| 18 h 25 | hôte | R05-R07 : lecture des verdicts automatiques |
| 18 h 30 | Sandbox manuel | P1 : R08 à R125 (55 min) |
| 19 h 25 | poste | P2 : R131 (vraie mise à jour, 30 min), puis R133 à R139 (25 min) |
| 20 h 20 | poste | R80 (désinstaller), puis R81-R82 (WACK, 25 min). R140 sur Windows 10 pendant le WACK |
| 20 h 45 | — | P3 s’il reste du temps, sinon vendredi |

La vraie mise à jour (R131) part de la 1.1.0 déjà installée : elle passe donc avant toute autre installation du candidat sur le poste. Le WACK vient après R80, car il installe puis retire le paquet. Il pourrait sinon emporter les données de la 1.1.0.

## 1. P0 — Bundle et vérification (bloque la soumission)

- [ ] **R01** — `powershell -ExecutionPolicy Bypass -File "$kit\sandbox\verifier-candidat.ps1" -Run $run -Commit <headSha du run>`. Lire le verdict R01 dans `$kit\evidence\recette-<12>\hote\resultats.json` : artefact téléchargé dans `msix\ci-$run\`, un seul fichier, `AZERTYGlobal-1.3.0.0.msixbundle`. `-GhUser <compte>` prend le jeton d’un autre compte pour ce seul processus. (README feu-vert ; ci.yml)
- [ ] **R02** — Verdict R02 : la ligne `Get-FileHash` du bundle est lue dans le job build, par son chemin ou par l’empreinte attestée (A10-03, plus par sa position). Reporter l’empreinte dans l’en-tête. Pour un OK complet, rejouer avec `-Sha <empreinte>`. (A10-03)
- [ ] **R03** — Verdict R03 : `gh attestation verify` réussi, commit source = commit du run = `-Commit`. (README feu-vert)
- [ ] **R83** — Verdict R83 : `SHA256 x64` et `SHA256 arm64` à `publish = bundle`, « Release vérifiée: version 1.3.0 / package 1.3.0.0 », **job build** en succès. Les jobs provenance et actionlint sont notés, pas exigés (A10-03). Ne pas lancer `scripts\Verify-Release.ps1` en local. (ci.yml)

Le WACK (R81-R82) est aussi en P0. Il se joue en fin de soirée (§ 5).

## 2. Recette automatique (Sandbox, sans surveillance)

- [ ] **R04** — `powershell -ExecutionPolicy Bypass -File "$kit\sandbox\lancer-recette.ps1" -Bundle $b -Auto` : le Sandbox installe, joue, désinstalle et s’éteint seul. La console attend `done.txt` dans `$kit\evidence\recette-<12>\auto\`. (lancer-recette.ps1)
- **R05** — couverte par A1 de `recette-auto.ps1` : empreinte, `…_1.3.0.0_x64__w9kghr08zmhbg`, `shadow stack ON ; CFG ON`. (kit A1)
- [ ] **R06** — Verdict global dans `auto\resume-auto.txt` → OK attendu. Les lignes du tableau ci-dessous viennent d’être réécrites sans passage réel : sur ECHEC, lire leur observation dans `resultats.json` et jouer le repli avant de conclure à un défaut. Pour toute autre ligne sous son attendu, rejouer son geste. Si A3 ou R75 rend A VOIR, le bouton 2011 a été cliqué faute d’Entrée : juger le clavier en R11. (kit A2-A12, B12)
- [ ] **R07** — Verdict A11 → OK (partiel) : copie `config.json.illisible-…` contenant `[1]`, `config.json` qui n’est plus `[1]`. (recette-auto.ps1, A11 ; L4)

Lignes réécrites le 14/10 sans passage réel, avec leur repli :

| Ligne | Ce qui a changé | Repli |
|---|---|---|
| R20, R22-R26, R70-R73 | l’accueil a 4 étapes : Suivant jusqu’à la dernière, 3 cases attendues | R101 et R27 ; démarrage : R24 à la main (« C’est parti ! » coché → Paramètres › Général le montre activé) |
| R37, R39, R40 | Défi du jour réactivé (`1ec1173`) : 3 entrées, section Défi, case visible | R116, R117 |
| R47 | 4 onglets | R118 |
| R49 | nouveau refus de raccourci | R119 |
| R58 | le bouton des Couches s’appelle « Fermer » | R58 à la main : Espace, Tab jusqu’à « Fermer », Entrée → réglage gardé à la réouverture |

## 3. P1 — Nouveautés du candidat (Sandbox manuel)

Ouvrir un Sandbox neuf, sans `-Auto` : `powershell -ExecutionPolicy Bypass -File "$kit\sandbox\lancer-recette.ps1" -Bundle $b`. Au premier lancement, ne pas toucher l’accueil avant R08. Le Sandbox n’a pas de Bloc-notes : Win+R, la barre d’adresse et un champ de texte d’Edge le remplacent. Outils à coller dans un PowerShell du Sandbox ; toujours quitter l’app (menu ▸ Quitter) avant d’écrire un fichier :

```powershell
$d = "$env:LOCALAPPDATA\Packages\AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg\LocalCache\Local\AZERTY Global"   # sinon "$env:LOCALAPPDATA\AZERTY Global"
$j = Get-Date -Format yyyy-MM-dd; $j3 = (Get-Date).AddDays(-3).ToString('yyyy-MM-dd'); $j8 = (Get-Date).AddDays(-8).ToString('yyyy-MM-dd')
function Cle($k, $v) { $o = Get-Content "$d\config.json" -Raw | ConvertFrom-Json; if ($null -eq $v) { $o.PSObject.Properties.Remove($k) } else { $o | Add-Member -NotePropertyName $k -NotePropertyValue $v -Force }; [IO.File]::WriteAllText("$d\config.json", ($o | ConvertTo-Json -Depth 10)) }
function Usage($jours) { [IO.File]::WriteAllText("$d\usage-stats.json", ('{{"firstRemapDate":"{0}","lastActiveDate":"{0}","activeDaysCount":{1},"totalActiveMinutes":10,"accentedUppercaseCount":20}}' -f $j, $jours)) }
```

### 3.1 Accueil en 4 étapes, avant l’accord

- [ ] **R08** — Icône avant l’accord → grise ; l’infobulle commence par « Désactivé » et dit comment réactiver. (kit E4 ; `761817e`)
- [ ] **R09** — Taper dans Win+R, fermer l’accueil par la croix, relancer depuis Démarrer ; refaire en fermant par Échap → clavier inchangé, l’accueil revient à chaque relance, aucun accord mémorisé. **Décide** du marqueur l. 293 des notes de certification. (kit A2 ; 24/09 l. 3)
- [ ] **R101** — Parcourir les 4 étapes au bouton « Suivant », en FR puis en EN (drapeau). « Étape n sur 4 » s’affiche. L’étape 2 s’intitule « Écrire le peul, le wolof, le bambara » / « Write Fula, Wolof and Bambara ». Dans son champ « Essayez ici », taper ù puis b → « C’est bien ɓ. » ; une autre lettre → « Ce n’est pas ɓ. Réessayez… ». Noter si l’essai marche avant l’accord. L’étape 4 montre trois cases : le démarrage (cochée), « Ne plus afficher » et « Rappels d’entraînement « Défi du jour » » (décochée). Rien n’est coupé ni superposé. (`cadab2c` ; `AccueilVosLanguesTests`)
- [ ] **R10** — Tab et Maj+Tab dans l’accueil → aucun lien fantôme. Drapeau focalisé avec un cadre, puis Entrée, Espace et clic → la langue bascule à chaque geste, le focus reste sur le drapeau. (kit B11 ; L1)
- [ ] **R11** — Revenir à l’étape 1. Tab jusqu’à « Activer et essayer », Entrée → tutoriel ouvert sur « Vous écrivez surtout… », remappage actif ; icône bleue, infobulle et bulle « Actif ». (kit A3, E4)

### 3.2 Tutoriel par usage

- [ ] **R102** — Première page : 5 grands boutons-bascules (Documents, Messages, Code, Langues, Afrique), « Plusieurs réponses possibles. » Tab puis Espace cochent et décochent, un cadre suit le focus. « Continuer » sans rien cocher → parcours Documents. (`3e9dbbb`, `86dfad9`)
- [ ] **R103** — Rouvrir le tutoriel (Apprendre ▸ Revoir l’accueil ▸ Essayer maintenant), cocher Messages et Afrique : 3 exercices communs, puis l’exercice de l’usage (Messages : « À table ! Ça refroidit ! »), les autres en bonus avec « Passer ». Une faute → « Faute : x au lieu de y. Retour arrière pour corriger. », la touche cerclée de rouge. Fin : « Bravo ! », écran final. (`5e82614`, `7add4cd` ; `TutorialStepsTests`)
- [ ] **R17** — Tab puis Espace → le tutoriel reste ouvert. Sur un exercice bonus (« Passer » affiché) : Tab, Tab, Entrée → l’exercice passe, la frappe reprend sans clic. (kit B8 ; 24/09 l. 6)
- [ ] **R27** — 3 exercices faits, rouvrir l’accueil → « Essayer maintenant » n’est plus proposé ; tutoriel et accueil fermés, 15 s → aucune demande d’avis. (L9.5 ; 24/09 l. 13)

### 3.3 Leçons : Aujourd’hui, Tablature, notation

- [ ] **R104** — Apprendre ▸ Leçons → la fenêtre s’ouvre sur « Aujourd’hui ». Trois onglets : Aujourd’hui, Votre carte, Toutes les leçons. Cartes : « Reprendre » ou « Commencer », « Votre texte » (« Coller un texte »), maîtrise. Les gestes du tutoriel y comptent déjà. Échap ramène à Aujourd’hui. FR puis EN. (`1682671`, `98d037b` ; `LessonsHomeWindowTests`)
- [ ] **R105** — Toutes les leçons › Initiation › exercice 1. En-tête : titre, « Choisir une leçon », ‹ ›, « Exercice n sur N », liens Aujourd’hui, Indice, Recommencer, Écriture libre, Paramètres ; le lien « ‹ Aujourd’hui » ne passe pas sous l’anneau. La ligne n’a pas de portée : sous le caractère courant, son geste en mots. Clavier papier : cibles cernées de bleu et numérotées, plein bleu seulement pour une touche tenue. Une faute → anneau rouge sur la touche fautive, geste réaffiché. (`73a1620`, `cdc0214`, `d309c84`, `7418b75`)
- [ ] **R106** — Lien « Paramètres » des Leçons, puis Paramètres › onglet Leçons › « Touches à taper ». Avec le clavier : Mots, Recette écrite, Grand clavier numéroté. Sans le clavier : Mots, Recette écrite, Mini-clavier sous le caractère, Une vignette par temps. Essayer chaque notation, clavier affiché puis masqué → la ligne suit, et chaque état garde son choix après fermeture et réouverture. Les deux fenêtres montrent le même réglage. (`061b099`, `5e6d1f5` ; `NotationConfigTests`)
- [ ] **R107** — Leçon des capitales (É) : un mot qui commence par une capitale accentuée suivie d’une minuscule (« Été ») s’enseigne par ^ puis é ; une lettre seule ou un mot en capitales, par Verr. Maj. La frappe accepte les deux méthodes. (`1e7574b`, `6eedab4` ; `RegleCapitalesAccentueesTests`)
- [ ] **R108** — Votre carte : un clic choisit une touche, ← et → passent à la voisine, « Séance sur cette touche » lance une séance courte sur ses signes. (`c4ed687`)
- [ ] **R109** — Aujourd’hui › Coller un texte : coller (Ctrl+V) une phrase avec É, « », ɓ et un signe que la disposition ne tape pas (par exemple 😀). Légende « caractère à geste » et « la disposition ne le tape pas ». Bilan « Ce que vous allez taper », Aperçu, puis « Taper ce texte » → séance, « Terminer » à la fin. Fermer puis rouvrir la fenêtre → le texte est oublié. (`7138a81`, `1d8de54`, `f33a5cc` ; `LessonCustomTextTests`)
- [ ] **R110** — Faire une faute sur un caractère, revenir à Aujourd’hui → « Révision du jour » le propose. « Plus tard » la range ; « Réviser » lance la séance. (`d740cd4`)
- [ ] **R111** — Toutes les leçons › « Un tour d’Afrique » → l’exercice se joue (€, ɓ, ŋ, Ŋ, ɔ), indices compris. (`11ebd84`)

### 3.4 Recherche refaite

- [ ] **R112** — Ctrl+Maj+W : champ « Caractère, nom ou langue… ». Taper « wolof », puis « bambara », puis « ton haut » → les lettres de la langue en tête, puis les signes de ton. Pied « 20 résultats affichés sur N », « Entrée insère « X » » à droite. La ligne choisie est teintée avec un filet bleu et montre le caractère en grand, son geste et une phrase. Entrée insère le bon signe dans la fenêtre d’origine. (`99be17e`, `ffd6f04` ; `LanguageSearchTests`)
- [ ] **R33** — Première requête de la session (l’index se charge), puis « é », « É », « flèche dr », « U+20AC », « espace insecable », une touche morte → bons résultats, noms en casse de phrase. (kit A8 ; `785db72`)

### 3.5 Défi du jour réactivé

- [ ] **R116** — Menu Apprendre ▸ Leçons, Défi du jour, Revoir l’accueil, en FR et en EN. Sans opt-in, « Défi du jour » ouvre les Leçons sur Aujourd’hui. L’annonce « Nouveau : le Défi du jour » apparaît une seule fois (notifications actives, pas d’opt-in), puis plus après une relance. Remplace R37 et R39. (`1ec1173` ; `DefiDuJourMasqueTests`, sens inversé)
- [ ] **R117** — Paramètres › Général › « Rappels d’entraînement « Défi du jour » » : décochée par défaut. La cocher → Apprendre ▸ Défi du jour ouvre le défi dans les Leçons, et Mes statistiques montre la section Défi. Remplace R40. (`SettingsWindow.cs:747`)

### 3.6 Paramètres en 4 onglets

- [ ] **R118** — Onglets Général, Applications, Langue, Leçons (General, Applications, Language, Lessons), aux flèches puis à la souris ; fenêtre stable. « Version 1.3.0 » au pied de Langue. Applications › « Compatibilité des applications » › Ajouter… `C:\Windows\System32\mstsc.exe` → « Mode pour » : Auto, Forcer la compatibilité jeu, Forcer la désactivation. Forcer la compatibilité jeu → refus **en rouge**, qui commence par « Refusé : si AZERTY Global tourne aussi sur l’ordinateur distant… ». Retirer l’app. Remplace R47 et R48. (`981d1ed`, `cd49ead`, `8fa73b9` ; `SettingsClarityTests`)
- [ ] **R119** — Raccourci du clavier virtuel : l’invite dit « Appuyez sur une lettre, un chiffre ou F1 à F12… ». T → « Ctrl + Maj + T rouvre l’onglet fermé dans les navigateurs. Choisissez une autre touche. », en rouge, et la case montre T. Échap annule. Remplace R49. (`0fd3c1d`)

### 3.7 Clavier virtuel, zone de notification

- [ ] **R120** — Ctrl+Maj+Q : fenêtre « AZERTY Global, Clavier virtuel », papier clair, un signe par touche, touches mortes en tirets, bandeau avec phrase. AltGr tenu, puis une touche morte armée → le clavier passe à la couche active. (`5f9860a`, `1a5a9dd`)
- [ ] **R121** — Désactiver (Ctrl+Maj+Verr. Maj.) → infobulle « Désactivé (Ctrl + Maj + Verr. Maj. pour réactiver) », ligne grisée du motif en tête du menu. Mettre en pause → la bulle dit pourquoi et jusqu’à quand. Réactiver → l’infobulle suit. Remplace R38. (`761817e`, `d4326b0` ; `TrayClarityTests`)

### 3.8 Lot 5 (⏭️ si le correctif est absent du candidat : `git log` du run)

- [ ] **R122** — A11-02 : menu ▸ Mettre en pause ▸ Personnaliser…, taper « 30 » dans les minutes sans cliquer, Entrée → « Reprise à <maintenant + 30 min> » (avant : 5 h 05). Les minutes s’arrêtent à 59. (A11-02 ; `PauseTests.Duree_BorneeDe1minA23h59`)
- [ ] **R123** — A12-01 : Initiation, exercice 4, en strict, taper m au lieu de l → le caractère tapé reste entier, l’attendu lisible dessous. (A12-01)
- [ ] **R124** — A02-02 : rechercher « ∉ », « ѓ », « ǣ », en FR puis en EN → le geste affiché, tapé dans Edge, donne bien ce caractère (avant : « AltGr + = » puis « E » donnait ∈). (A02-02)
- [ ] **R125** — A08-02 (relance après une mise à jour) : ⏭️ ce soir. Le geste exige un paquet 1.3.0.1 posé plus d’une minute après le lancement (CE2). Noter le test xUnit qu’A1 cite pour ce correctif.

## 4. P2 — Essais exigés le 05/10 (sur le poste)

- [ ] **R131** — Vraie mise à jour (A10-01, A08-01). La 1.1.0 du Store, ouverte et utilisée, contient tes vrais réglages ; le dossier est sauvegardé (§ 0). Avant : noter un raccourci, une app en compatibilité, l’état du démarrage automatique, la place du clavier virtuel, et la progression des Leçons (Initiation et 3 exercices réussis, sinon les faire). Puis, la 1.1.0 restant ouverte : `powershell -ExecutionPolicy Bypass -File "$kit\installer-poste.ps1" -Bundle $b -MiseAJour`. Attendu, dans `installation-poste.txt` : « avant 1.1.0.0 (Store), app ouverte : oui » ; « après une seule 1.3.0.0, app ouverte : non » (fermée, pas relancée : attendu) ; 0 copie `.illisible`. Relancer depuis Démarrer → l’accueil redemande l’accord une fois. Après l’accord : réglages notés retrouvés, `firstRemapDate` inchangée dans `usage-stats.json`, exercices toujours réussis, clavier virtuel à sa place, démarrage automatique conservé, aucun avis ; une seule icône à la session suivante. Une casse de migration est **bloquante**. (A10-01 ; CE3 l. 260-300 ; `installer-poste.ps1 -MiseAJour`)
- [ ] **R132** — A08-02, condition de CE2 : la fermeture par `-ForceApplicationShutdown` est lue dans R131. Le vrai Store reste non observé jusqu’à sa première mise à jour réelle : le noter. (CE2 l. 118-125)
- [ ] **R133** — A01-01 : Bloc-notes, Alt+Maj vers US sans changer de fenêtre, taper « azerty » puis la touche ² → « qwerty@ » confirme le défaut (prévu en 1.3.1), « azerty@ » le réfute. Cliquer une autre fenêtre et revenir → « azerty ». Refaire dans Edge : Ctrl+Z ne ferme pas l’onglet. Observation, non bloquante. (A01-01 ; CE1 l. 111-116)
- [ ] **R134** — A01-03 : disposition US seule active. Dans le Bloc-notes puis dans Edge et Chrome : AltGr+O → œ, AltGr+E → €, AltGr+D → {, puis Ctrl droit + Alt gauche + O. Noter les caractères perdus et les bips. **Décide** : Ctrl droit + Alt gauche entre dans la fiche Store et la FAQ seulement s’il donne œ dans Chrome et Edge. (A01-03 ; CE1 l. 195-198)
- [ ] **R135** — A01-02 : disposition « Arabe (102) AZERTY » active dans le Bloc-notes, app active, taper « azerty » → défaut prédit : « az » puis quatre lettres arabes (prévu en 1.3.1). Mettre AZERTY Global en pause (menu de l’icône) et retaper → six lettres arabes, celles de Windows. **Décide** de la consigne « Pause » de la fiche Store. (A01-02 ; CE1 l. 150-151)
- [ ] **R136** — A09-01 : Explorateur, `C:\Windows\System32` en vue Détails, flèche bas maintenue 3 s, puis relâcher ; filmer au téléphone. Refaire app quittée, puis dans le volet de navigation. Seuil : au moins deux éléments de plus après le relâchement, ou une vitesse inférieure de 20 % à celle de l’app quittée. (A09-01 ; CE2 l. 81-91)
- [ ] **R137** — A09-03 : app quittée, Edge fermé pour de bon (aucun `msedge.exe`). Ouvrir `edge://accessibility` et capturer les cases. Lancer l’app, rouvrir Edge, cliquer dans un champ, Tab deux fois, recharger `edge://accessibility` et capturer. Des cases passées à actif confirment A09-03. Observation seulement : ses deux attendus se contredisent (CE2 l. 269-272). (A09-03 ; CE2 l. 299-319)
- [ ] **R138** — A10-08, 2026.1 : (a) AltGr+Maj+* (touche à droite de ù), puis o → ө ; un double appui ne verrouille rien. (b) Clavier virtuel en FR puis en EN, Ponctuation armée → les six espaces portent un libellé lisible (esp. fine, ultrafine, ½ cadr., cadr., chiffre, nulle). (c) Le nom « CYRILLIQUE ÉTENDU » / « EXTENDED CYRILLIC » s’affiche, jamais `dk_cyrillic_ext`. (A10-08)
- [ ] **R139** — A05-04 : contraste élevé Aquatique (Alt gauche + Maj gauche + Impr. écran). Ouvrir Paramètres (Général, Applications), l’accueil à l’étape 4, les Couches, Pause › Personnaliser…, les Leçons → chaque libellé se lit. Retour au thème normal sans relancer l’app. Remplace R98. (A05-04)
- [ ] **R140** — A04-06, Windows 10 22H2 : installer le candidat signé (certificat de test approuvé, comme R77), puis accueil, « Activer et essayer », Verr. Maj. + é → É, clavier virtuel, Paramètres, Quitter → `error.log` vide. Noter le chemin « Langue et région » affiché par le Conflit. Sans Windows 10 : ⏭️, et l’écart « Windows 10 non vérifié avec .NET 10 » reste à arbitrer pour la configuration requise de la fiche. (A04-06)
- [ ] **R141** — Clavier US, notes de certification (CE3) : disposition US active, app active. Ctrl+Maj+A → clavier virtuel ; Ctrl+Maj+Z → recherche ; Verr. Maj. puis la touche 2 → É ; les deux entrées du menu de l’icône ouvrent les mêmes fenêtres. **Décide** du texte de test des notes de certification. (A04-02 ; CE3 l. 180-181)

## 5. P0 — WACK (sur l’hôte, en fin de soirée)

- [ ] **R80** — Désinstaller AZERTY Global du poste (Paramètres › Applications) → clavier système utilisable. Après le WACK, réinstaller la 1.1.0 depuis le Store pour l’usage courant. (installer-poste.ps1 ; kit A12)
- [ ] **R81** — PowerShell administrateur, aucune AZERTY Global installée, à la racine du dépôt (`$kit` et `$b` redéfinis) : `powershell -ExecutionPolicy Bypass -File "$kit\wack.ps1" -Bundle $b` → `$kit\evidence\wack-<12>\resume.txt` : empreinte de l’en-tête, `OVERALL_RESULT : PASS`. **Décide** du marqueur l. 300 des notes de certification. (kit A13 ; wack.ps1)
- [ ] **R82** — Lignes non PASS de `resume.txt` → seule « Blocked executables » (optionnelle, `ShellExecuteW`) est admise. (Fiche Store, notes de certification)

## 6. P3 — Non-régression

Dans le Sandbox manuel, après P1, puis sur le poste pour 6.6.

### 6.1 Frappe (Win+R, Edge)

- [ ] **R28** — `é è à ç ù`, Verr. Maj. + é → É, AltGr+E → €, AltGr+D/F → { }, ^ puis e → ê, ¨ puis i → ï ; Verr. Maj. intelligent ; aucun modificateur coincé. (kit A4 ; L3)
- [ ] **R29** — Alt+Tab rapide entre deux fenêtres en tapant, retour en moins d’1 s → chaque caractère dans la bonne fenêtre, pas de bulle en rafale. (kit A6 ; L2)
- [ ] **R30** — Edge, adresse `data:text/html,<input type=password><input>` : dans le champ mot de passe, fonctions avancées suspendues, Ctrl+Maj+W et Ctrl+Maj+Q sans effet, aucune couche ; dans le second, disponibles. (kit A7 ; L3)
- [ ] **R31** — Maj+* puis a → α ; double appui → verrou et indicateur ; Échap déverrouille. (L1)
- [ ] **R32** — Quitter par l’icône, relancer depuis Démarrer → clavier système dès la sortie ; accord conservé, remappage revenu. (kit A5)
- [ ] **R34** — Clic droit sur l’icône › Rechercher un caractère › Entrée → caractère copié avec notification, rien dans la barre des tâches. (24/09 l. 4)
- **R35, R36** — couvertes par `SearchResultListTests.LePiedDitCombienDeCorrespondancesDepassentLePlafond`, `SousLePlafondLePiedCompteSimplement`, `LeClicTombeSurLaLigneDessinee` et `UnSeparateurOuLeVideNeSelectionneRien`.

### 6.2 Avis et Paramètres

- [ ] **R41** — Verdict R41 → OK : `Usage 1`, relance → aucun avis au démarrage. (L7)
- **R42, R43** — couvertes par `AvisApresSeanceTests.Refusee_pendant_une_seance`, `Les_frappes_d_exercice_seules_ne_declenchent_pas` et `Refusee_dans_les_dix_minutes_apres_la_fin`.
- [ ] **R44** — App quittée, `Usage 1`, relancer. Plus de 10 min après la dernière fermeture du tutoriel ou des Leçons, majuscule accentuée dans Edge, puis 15 s sans frappe → toast d’avis ; clic → page d’avis du Store (Sandbox sans Store : noter ce qui s’ouvre) ; `reviewPromptCount` passe à 1. (L7)
- [ ] **R45** — Verdict R45 → OK (partiel). À l’œil : focus visible sur chaque bouton, « Réinitialiser les raccourcis » par Entrée puis Espace, Non → `config.json` inchangé, focus revenu sur le bouton. (kit A9 ; L6)
- [ ] **R46** — « Réinitialiser les raccourcis » : survol, puis clic et annulation → survol visible, geste pris en compte. (L1)

### 6.3 Leçons, statistiques, fenêtres

- **R50** — couverte par `Accessibilite130Tests.K4_BoutonIcône_RetrouveSonInfobulle` et `LessonsFocusCaptureTests.FenetreVisibleSansLeFocus_RienNEntreEnFile`.
- [ ] **R51** — Fin de ligne et premier caractère de la suivante d’un trait, en strict puis en souple → rien de perdu, pas de pause ; indice immédiat. (L2)
- [ ] **R52** — Lettre à touche morte → l’indice montre l’armement puis la touche finale ; avec une autre touche morte armée → l’indice revient à l’armement. (L5 ; L9.4)
- [ ] **R53** — Leçons, Mes statistiques, puis remise à zéro → progression et compteurs cohérents. (kit B5)
- [ ] **R53b** — Loupe de Windows (Windows + +), réglée pour suivre le curseur de texte, en tapant dans les Leçons puis en Écriture libre → elle suit la lettre à taper. (lot surface)
- [ ] **R53c** — Narrateur, Leçons ouvertes → il annonce le champ « Texte à taper » et la consigne ; Tab → chaque bouton par son nom ; un interrupteur des Paramètres est lu coché ou non ; pas de lenteur de frappe. (lot surface)
- [ ] **R54** — Mes statistiques en FR puis en EN → dates et milliers au format de la langue. (L3)
- **R55** — couverte par `LinkBehaviorTests` et `Accessibilite130Tests.K5_*`.
- [ ] **R56** — À propos et Accueil → icône 32 px, dans la barre de titre et dans Alt+Tab. (L8.5)
- [ ] **R57** — Verdict R57 → OK (partiel) : fond clair, flèches collées aux champs, molette de 5 en 5, « Reprise … » qui suit la saisie, 0 h 0 → bouton grisé, Échap ferme. (L8.1 ; L4)
- [ ] **R57b** — Verdict R57b → OK (partiel) : six entrées de pause, « Reprendre maintenant (reprise auto …) » pendant une pause, sous-menu grisé app désactivée. (refonte du 28/09)
- **R59** — couverte par `DialogNavigationKeyboardTrapTests` ; ⏭️ dans le Sandbox.
- [ ] **R60** — Verdict R60 → OK (partiel) : À propos, Mes statistiques, Paramètres, Accueil, Couches et Pause ouverts trois fois, `error.log` sans `RegisterClass`. (L8.4)
- [ ] **R61** — Barre de titre des fenêtres sombres → inchangée. (L1)
- [ ] **R62** — App en anglais : Leçons, tutoriel et clavier virtuel → « Enter », « Caps Lock », « Shift ⇧ », « Space », gestes en mots anglais ; bascule FR ↔ EN fenêtre ouverte → redessin immédiat, rien ne déborde. Remplace R63, dont le test a disparu. Revenir en FR. (kit E5)

### 6.4 Échelle et Narrateur (dans le Sandbox)

- [ ] **R64b** — À 150 % : Paramètres (4 onglets), Leçons (Aujourd’hui, Votre texte, exercice), indicateur de couche, Recherche, clavier virtuel, puis passage de 100 à 150 % fenêtre ouverte → rien de coupé ni de superposé. (kit B1, B13 ; L6)
- [ ] **R67** — Paramètres à 175 %, fenêtre du Sandbox réduite vers 1366×768 → les contrôles défilent ensemble ; Tab fait défiler vers les contrôles masqués. (L6)
- **R68** — couverte par `NativeWindowTests.Centrage_FenêtrePlusGrandeQueLaZone_PartDeSonCoin` et `WindowSizingTests.D3_200Pourcent_TientDansLÉcran`.
- [ ] **R69** — Narrateur (Ctrl+Win+Entrée) sur l’accueil (usages : « coché »), Paramètres, Pause et Recherche, en FR puis en EN → boutons, onglets et champs annoncés (« Heures », « Compatibilité des applications », « Rechercher un caractère »…). (kit B2, B12 ; `1c718a7`)

### 6.5 Avis sur config 1.2 et fichiers illisibles (automatiques)

- [ ] **R70 à R73** — Verdicts R70 à R73 : faux ECHEC probable (accueil en 4 étapes). Repli : R131 couvre l’accord redemandé sur une vraie config ancienne ; le reste est couvert par `AvisApresSeanceTests` et `SollicitationAvisCaracterisationTests`. (L7)
- [ ] **R74** — Verdict R74 → OK (partiel) : `lessons-progress.json` illisible, copie `.illisible-…` intacte, app vivante. (L4)
- [ ] **R75** — Verdict R75 → OK (partiel) : après A11, accueil ouvert, app inactive (menu « Activer »), accord par clic sur 2011, relance active (menu « Désactiver »). (L4 ; kit A11)
- **R76** — couverte par A12 (désinstallation, processus arrêté). Fin du Sandbox manuel : copier ce qui doit rester dans `C:\resultats\`, puis le fermer.
- [ ] **R126** — Observation (`1b342f6`, jamais mesuré) : AZERTY Global lancé sur l’hôte, fenêtre du Sandbox au premier plan → suspension, avec une bulle qui dit pourquoi ; retour sur l’hôte → reprise.

### 6.6 Deux écrans, sur le poste (après R131 et avant R80)

- [ ] **R77** — `powershell -ExecutionPolicy Bypass -File "$kit\installer-poste.ps1" -Bundle $b` (sans `-MiseAJour` : repart d’un premier lancement) → `installation-poste.txt` finit par « Pret », `shadow stack ON ; CFG ON`. Accepter l’accueil ; déplacer la Pause, les Couches et les Paramètres d’un écran à 100 % vers un écran à 150 % → même taille apparente, textes nets. (L8.2 ; L6)
- [ ] **R78** — Tutoriel sur l’écran secondaire à 150 %, Windows à 100 % → « Quitter les exercices » aligné au bord droit. (L9.1)
- [ ] **R79** — Recherche ouverte sur chaque écran → elle reste sur l’écran où elle s’est ouverte. (L3)

### 6.7 Images du banc (si l’artefact existe)

Artefact `captures-<12>` du workflow « Captures des fenêtres » sur le commit du candidat : `gh workflow run captures.yml -R AMCF-asso/azerty-global-app -f ref=<sha>` (de jour : le garde refuse la nuit tout `gh` qui écrit). Échelles 100 à 200 %, FR et EN, nom `<sujet>-<fr|en>-<échelle>.png`. Le banc rend sans rien affirmer : ces lignes se jugent à l’œil.

- [ ] **R12** — `tuto-ex2-fine`, `tuto-ex6-etape1`, `tuto-ex6-etape2` → étape 1 en orange avec la pastille « 1 », étape 2 en vert avec la pastille « 2 ». (`TutorielClavierTests.L_etape_1_se_dit_en_orange_avec_sa_pastille`)
- [ ] **R13** — `tuto-ex1-verrmaj`, `tuto-ex2-espace` → Verr. Maj. pleine. (`GuidageTests.Les_exercices_1_et_2_gardent_Verr_Maj_surlignee`)
- [ ] **R14** — `tuto-ex6-etape2`, `tuto-ex4-altgr-tenue` → touches tenues montrées, AltGr compris. (L9)
- [ ] **R15** — `tuto-ex6-retour` → Retour arrière surligné ; « ◌ » barré visible. (L9)
- [ ] **R64** — À 125, 150, 175 et 200 % : `parametres-*` (4 onglets), `lecons-*` (dont `lecons-aujourdhui-*`, `lecons-carte`, `lecons-texte`), `recherche*`, `clavier-virtuel*`, `duree-de-pause*`, `couches`, `accueil-etape1` à `4`, `tuto-usages`, `a-propos`, `statistiques` → rien de coupé ni de superposé. (kit B1, B13)
- [ ] **R64c** — Textes, en FR puis en EN : `accueil-etape*`, `tuto-usages`, `tuto-messages*`, `parametres-*`, `lecons-*` → rien de coupé, aucune clé brute.

## 7. Partner Center (vendredi soir ; Antoine soumet)

- [ ] **R85** — Nouvelle soumission, Paquets → téléverser **ce** bundle (empreinte de l’en-tête du bundle final) ; x64 et ARM64, ARM64 publié avec l’écart documenté. (PC ; kit C)
- [ ] **R86** — « tested to meet accessibility guidelines » → **décochée** (décision du 22/09). (PC)
- [ ] **R87** — Fiche FR et EN collée depuis `msix/Fiche Store.md`. Compteur des notes de version sous 1 500 caractères (A07-03) ; aucun « standard » pour l’AZERTY de Windows en anglais (A07-02). La règle « ni Défi ni challenge » tombe avec le Défi réactivé : appliquer celle de la fiche validée jeudi. (PC ; A07)
- [ ] **R88** — Notes de certification : texte anglais de la fiche ; marqueur l. 293 retiré si R09 est ✅, marqueur l. 300 si R81 et R82 le sont. (PC)
- [ ] **R89** — Propriétés, IARC et confidentialité inchangés ; Publication : **Publishing hold** (plan du 14/10) ; soumettre. Tag `v1.3.0` sur le commit soumis ; release GitHub en brouillon jusqu’à J. (PC ; plan § 2)

## Optionnels

- [ ] **R84** — App Installer sur machine propre : ⏭️ par défaut, ce canal ne concerne pas le Store. (msix/README.md, étapes 8-9)
- [ ] **R90** — Essai 1 hors package : `AZERTY Global.exe` extrait du `.msix` x64, seuils de R41 posés dans `%LOCALAPPDATA%\AZERTY Global\` → bulle au lieu du toast. (L7)
- [ ] **R92** — Hôte distant simulé : `Copy-Item C:\Windows\System32\PING.EXE $env:TEMP\parsecd.exe; Start-Process $env:TEMP\parsecd.exe '-t 127.0.0.1'`, puis AutoHotkey v2 `SendText "(3) x"`, Verr. Maj. éteint puis allumé → texte intact. (24/09 l. 1)
- [ ] **R93** — Jeu anti-triche réel : avant l’accord, aucune bulle ; après, retour dans le jeu → bulle de suspension qui dit pourquoi, frappe non remappée. (24/09 l. 7)
- [ ] **R94** — `usage-stats.json`, puis `config.json`, ouverts en exclusif, taper, quitter, fermer, relancer → chiffres et fichier inchangés, aucune copie `.illisible-…`. (L4 b)
- [ ] **R95** — Changer un réglage puis se déconnecter aussitôt, se reconnecter → réglage gardé. (L4 c)
- [ ] **R96** — Couches dans Word ou Excel, Chrome, Edge, Firefox et VS Code. (Cahier § 7)

## Si ça échoue

- **Dans la case** : ❌, le geste exact, la langue, l’échelle et l’écran. R01-R03 et R81-R83, ainsi qu’une casse de migration en R131, bloquent la soumission. Les autres ❌ se trient au QCM de 21 h : corrigé avant le gel, ou 1.3.1.
- **Capture** : Win+Maj+S, sous `C:\resultats\manuel\R<nn>.png` dans le Sandbox (c’est `$kit\evidence\recette-<12>\` sur l’hôte) ; sur le poste, directement dans `$kit\evidence\recette-<12>\manuel\`.
- **Fichiers** : copier `$d\error.log`, `config.json` et le fichier en cause dans le même dossier, préfixés `R<nn>-`, **avant** de fermer le Sandbox, qui efface tout.
- **Ligne automatisée** : copier `auto\journal.txt`, `resultats.json` et `resume-auto.txt` (ou `hote\journal.txt`) dans la note.
- **Empreinte** : la reporter en tête de la note. Si elle diffère de l’en-tête, toute la recette est à refaire.
- **Fiche cochée** : la copier dans `$kit\evidence\recette-<12>\RECETTE-1.3.0.md`.
