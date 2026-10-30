# Recette locale 1.3.0 — rapport du 2026-10-30

Worktree `lot130-integration-locale-f81958` (branche `claude/lot130-integration-locale-f81958`). Rien n'est commité, poussé ni soumis.

## Résumé

- Intégration lots B, F, G : faite, sans conflit.
- `scripts/verify.ps1` : exit 0, 9 étapes ok. Avant Bravo : tests-core 18/18, tests-windows 366/366, tests-app 1388/1388. Après Bravo : 18/18, 366/366, 1390/1390.
- Compilation locale (`Pack-MSIX.ps1`, `Verify-Release.ps1`, `check-binary-hardening.py`) : ok, bundle `msix/AZERTYGlobal-1.3.0.0.msixbundle` (6 891 324 o, non signé), SHA-256 `31aae230458f7516675d70ac3168bda233a958b8fda4a10c6eea203026814a03`. ⚠️ Il est antérieur au changement de l'écran Bravo : il ne le contient pas.
- Sandbox automatique : ECHEC, 2 essais sur 2. L'app ne tourne pas 8 s après son lancement (A1). Les 32 autres ECHEC en découlent. Cause inconnue (voir plus bas).
- Restes de la PR 14 : (a) défaut, (b) défaut partiel, (c) ok.
- Écran Bravo refait (hors orchestre, demande d'Antoine) : non commité, s'ajoute à l'intégration.

## Sandbox (lancer-recette.ps1 -Auto)

- Essai 1 (14:58) et essai 2 (15:06) : `installation.txt` note « ECHEC : l'app ne tourne pas 8 s apres son lancement ». Lancement par `shell:AppsFolder\…!AZERTYGlobal`, puis `Get-Process 'AZERTY Global'` après 8 s (`installer-candidat.ps1`).
- Les trois recettes antérieures du même Sandbox (candidats CI) ont lancé l'app : « App lancee (pid …) ; shadow stack ON ; CFG ON ».
- Verdict global : ECHEC. Conformes 1 (A12 : arrêt et désinstallation), A VOIR 1 (B12), ECHEC 32 (A1, A2, A3, A5, A10, R20 à R26, R37, R39 à R41, R45 à R49, R57, R57b, R58, R60, R70 à R75, A11).
- Diagnostic (décision d'Antoine, 15h27) : la même chaîne (publish x64 et arm64, `Pack-MSIX.ps1`, Sandbox `-Auto`) sur deux autres commits, construits dans un worktree jetable `C:\Users\antoi\AppData\Local\Temp\agbase` :
  - base `0f4873f` (sans lots B, F, G) : A1 ECHEC, « l'app ne tourne pas 8 s apres son lancement » (15:18). Journal : `diagnostic-sandbox-base.log`.
  - `0babba6` (commit du candidat CI `ba587cce`, 23/09, dernière recette qui avait marché) : A1 ECHEC, même message (15:26). Journal : `diagnostic-sandbox-0babba6.log`, preuves `evidence/recette-c601af2ee820/`.
  - Conclusion : le défaut ne vient ni des lots B, F, G ni d'un commit de la branche depuis le 23/09. Il vient du Sandbox du jour ou de la façon de construire en local (les trois builds sont locaux, les recettes qui avaient marché utilisaient des bundles de la CI : ces deux causes ne sont pas séparées).
  - Bundle CI de `main` (run 40126504075, commit `8064646`, 24/10, SHA-256 `22f4b3612563…d20d5`, téléchargé avec `gh run download`) : A1 ECHEC, même message (15:34). Journal : `diagnostic-sandbox-ci-8064646.log`, preuves `evidence/recette-22f4b3612563/`. Le bundle CI du 23/09 n'existe plus (artefacts expirés).
  - Verdict final : même un bundle construit par la CI échoue. La cause est le Sandbox du jour, pas la construction locale ni un commit. Elle n'est pas nommée plus précisément : le Sandbox ne rend aucun journal de l'app. Hypothèse non testée : lancement plus lent que les 8 s d'attente de `installer-candidat.ps1` (`Start-Sleep -Seconds 8`).
  - Aucun journal d'erreur de l'app : le dossier de données de l'app n'existe pas dans le Sandbox (`LocalCache\Local\AZERTY Global` introuvable), donc elle n'a pas démarré jusqu'à créer sa config.
  - Pour séparer les deux causes : passer au même Sandbox le bundle de la CI du 23/09 (artefact du run 35851382703) ; s'il marche, c'est la construction locale, sinon le Sandbox.
- Cause (avant ce diagnostic) : non établie. Pistes à départager, sans avoir rien corrigé : plantage au démarrage du build local (régression possible des lots B, F, G), problème propre au bundle signé par le Sandbox avec un certificat jetable, ou nom de processus. Aucun journal d'erreur de l'app n'a été relevé dans le Sandbox.
- Suite à donner : lancer l'app à la main dans le Sandbox (`lancer-recette.ps1` sans `-Auto`) pour lire le message ou le journal d'erreur ; comparer avec le bundle de la CI.
- Fichiers : `sandbox-essai1.log`, `sandbox-essai1-resume-auto.txt`, `sandbox-essai1-installation.txt`, `sandbox-auto-essai2.log` ; résultats complets dans `docs/audit-2026-09-22-v1.3.0/feu-vert/evidence/recette-31aae230458f/auto/` (le dossier `auto-20261030-150539/` est l'essai 1).

## Points de recette sautés

- R01 à R03, R83 : CI et attestation, bundle local non signé.
- R05 : couverte par A1.
- R08 à R125, R142 : gestes humains (clavier, Narrateur, Loupe).
- R131, R143 : vraie mise à jour depuis la 1.1.0.
- R133 à R141 : poste physique, dispositions US et arabe, Windows 10.
- R80 à R82 : WACK, poste en administrateur.
- R77 à R79 : deux écrans.
- R85 à R89, R144 : Partner Center.
- R84, R90 à R96 : optionnels.
- Lignes « couvertes par tests » : couvertes par les tests verts.
- `recette-p1.ps1` est absent du dossier `sandbox/` de ce worktree.

## Restes de la PR 14 (rien corrigé)

- (a) DÉFAUT. Page bas, Fin, Page haut, Début, Bas envoyés en WM_KEYDOWN à Conflit (contenu 803, vue 689) et Couches (874 / 689) à 192 DPI sur une zone de 1024x728 : la position reste 0 → 0. `src/WindowScroll.cs` ne traite que WM_VSCROLL et WM_MOUSEWHEEL ; `LayoutConflictWindow.cs` (~l.418) et `MaintainableLayersWindow.cs` (~l.371, 407) n'ont aucun traitement de ces touches. Tab, molette et barre fonctionnent.
- (b) DÉFAUT partiel, mesuré. `OnboardingWindow.FittedScale` (`src/OnboardingWindow.cs:355-364`) appelle `NativeWindow.OuterSize` sans dpi : cadre 16x39 px au DPI système contre 22x56 à 144 et 26x71 à 192 en réel. Jusqu'à 32 px de hauteur en moins à 200 % si l'écran n'a pas le DPI système. Le banc ne peut pas le montrer.
- (c) OK. À propos à 200 % en FR et EN, sur 1080p (1102x492) et sur 1024x728 (1010x492) : « Fermer » visible, rien de coupé.

## Captures

- Banc 100/150/200 % (DPI 96/144/192), 1080p, FR et EN : `docs/recette-locale-2026-10-30/captures/` (27 sujets x 6 = 162 PNG, `banc-fenetres.tsv`, `position-message.tsv`).
- Contexte (Leçons, recherche, clavier virtuel, tutoriel) : `captures-contexte/` (218 fichiers).
- Zone simulée 1024x728 à 200 % : `captures-1024x728/` (À propos, Conflit, Couches avec variantes `-bas`, Accueil).
- Écran Bravo refait : `captures-bravo/` (`tuto-final-{fr,en}-{100,150,200}.png`).
- Relecture visuelle partielle seulement : parametres-general-message-fr-200, accueil-etape4-en-200, tuto-final-fr-200, a-propos FR/EN, couches-fr-200. Rien de coupé ; le message du lot G est visible en bas à 200 %.

## Écran Bravo (changement ajouté, non commité)

Deux colonnes. Gauche : « Bravo ! » et la liste tapée, avec sous chaque ligne le geste de son premier caractère particulier (« É : Verr. Maj., puis é », « @ : la touche en haut à gauche »). Droite : carte « Prochaines étapes » (Aujourd'hui, Votre carte, Un tour d'Afrique) et carte « Raccourcis à retenir » (Ctrl + Maj + Q et W, lus depuis les réglages). Boutons : « Continuer les leçons » (principal, ouvre Aujourd'hui) et « Terminer ».

- Fichiers : `src/LessonsWindow.Tutorial.cs`, `src/TutorialSteps.cs` (`RecapGesture`), `src/Localization/L.Learning.cs`, `TutorialStepsTests.cs`, `ParcoursAccueilTests.cs`.
- Les gestes tombent d'abord quand la fenêtre est trop basse, les cartes sont omises si elles ne tiennent pas, une colonne sous 700 px de large.
- Non vérifié : clic réel sur les liens des cartes (testé par réflexion pour « Continuer »), fenêtres de moins de 700 px.
- Le candidat MSIX n'a pas été reconstruit après ce changement.
