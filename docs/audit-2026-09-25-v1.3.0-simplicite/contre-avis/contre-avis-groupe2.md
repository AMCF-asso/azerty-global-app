# Contre-avis, groupe 2 : relecture adverse au commit f0a98ba

Relu le 2026-09-25, en lecture seule. `src/` est propre à f0a98ba (`git status -- src/` vide), donc les fichiers ont été lus directement. Aucun build, aucun test, application non lancée. Script : `contre-avis2/layers.py` (couches de character-index.json).

**Point transversal, qui manque à l'audit.** L'audit ne lit que la release. Or `main` contient déjà la refonte graphique CH0 à CH4b : `ThemeWindow.cs`, `Theme.cs`, `KeyboardTheme.cs`, `*.Theme.cs`. Aucun de ces fichiers n'existe à f0a98ba ni à la base commune 8775489. F-01, L-01 et V-01 proposent de recréer ce qui existe déjà sur `main`. Il faut donc décider d'abord la base de la 1.4.0 : c'est la question de V-01.

---

## F-01 : échafaudage de fenêtre recopié. **Verdict : NUANCÉ**
- Faits confirmés. OnboardingWindow.cs:424-456 et AboutWindow.cs:183-212 ne diffèrent que par 3 lignes de commentaire : c'est SetWindowIcon, recopié. AboutWindow.cs:303-332 est identique à LayoutConflictWindow.cs:233-262 (vérifié par diff). La gravité « majeur » tient.
- La proposition réinvente un socle qui existe sur `main`. `main:src/ThemeWindow.cs` (374 lignes, commits 03cc3a8 et f9e7a8e du 2026-08-28) fournit ApplyChrome, ApplyClassBackground/ForgetClassBackground (le risque de brosse de classe, AG130-48), ApplyProductIcon (remplace les clones de SetWindowIcon), DpiOf et ApplyDpiChange. Six fenêtres l'appellent déjà.
- **Correction :** partir de ThemeWindow (repris de `main` ou porté), puis y ajouter ce qui manque : RegisterOnce, PaintBuffer, FontSet. Le gain de -350 lignes dépend de la base retenue (V-01), et une partie est déjà acquise sur `main`.

## F-03 : Paramètres, 8 à 11 listes parallèles. **Verdict : CONFIRMÉ**
- `_hWndCompatAdd` apparaît bien 8 fois (SettingsWindow.cs:226, 404, 580, 873, 925, 946, 1484, 1669). Les commentaires des deux bugs sont bien aux lignes 383-386, 772-775 et 1650-1652. Gain de -150 lignes plausible.
- Deux réserves. D'abord, les deux bugs cités portent tous deux sur la bande d'onglets, dont les libellés passent par TCM_SETITEMW et non par SetWindowTextW (SettingsWindow.cs:768-775, 1650-1655) : la table doit accepter un « setter » propre à chaque contrôle. Ensuite, SetViewportOrgEx ne décale que le dessin GDI du parent : les rectangles de test de clic (CompatAddRect, l.1080 et 1156) devront recevoir le décalage inverse. C'est une nouvelle source d'écart à tester.

## L-01 : rendu du tutoriel en double. **Verdict : NUANCÉ**
- Fait confirmé : `KeyboardRenderProfile.Onboarding` n'a aucun appelant en production (git grep : KeyboardRenderer.cs:134 et :370 seulement). Son filtre, lui, reste vivant par le profil Lesson (KeyboardRenderer.cs:137).
- Le profil **ne couvre pas** ce que dessine LearningModule :
  - trois genres de surlignage (LM:1970-1988), contre un seul contour vert dans KR (KR:378-382) ;
  - le « à appuyer et activé → fond plein » (LM:2424-2431), là où KR met CLR_MOD_ACTIVE (KR:385) ;
  - les pastilles 1 et 2 (LM:2532-2543) ;
  - l'exception KeepCaps (LM:1972-1978, 2533-2535) ;
  - le Retour arrière désactivé mais rallumé quand il est surligné (LM:2414, 2517), alors que KR le laisse toujours terne (KR:376-377, 463) ;
  - l'exercice 6 : LanguageExercise* et FilterOnboardingSlot(…, languageExercise) (LM:3070-3133), paramètre absent de KR:790-795 ;
  - l'infobulle « ⌫ » propre au tutoriel (LM:583) ;
  - la géométrie : GetKeyboardGeometry et le -1 px (LM:2379-2389), contre ToRect.
- -650 lignes : plausible, mais c'est un plafond. La plage 2370-3196 contient 111 lignes de boutons d'en-tête qui restent (LM:2825-2935), plus le voile et la pastille (2553-2613). Environ 655 lignes y sont supprimables, auxquelles s'ajoutent l'infobulle (577-650) et les tables (134-155). Il faut en retrancher les ajouts à KR et l'adaptateur d'état. Estimation : **-500 à -650**.
- `main` a déjà l'enum KeyHighlight {Direct, DeadKeyActivation, Step1, Step2} et HighlightPaint (main:KeyboardTheme.cs:34-48, :106-112, décision S4-3). CH4b y a supprimé la pastille de rang du clavier virtuel (« table C écartée », message de fb0b854). Ajouter les pastilles 1 et 2 à KR va contre une décision déjà prise.
- **Risque omis :** LessonCoreTests.cs:588-606 lit par réflexion `_highlightedScancodes`, `_currentCharError` et `OnBackspace`. Si l'état de surlignage est déplacé, le test casse à l'exécution.
- **Correction :** reformuler en « profil à étendre », partir du modèle KeyHighlight de `main`, gain -500 à -650, et ajouter ce test au risque.

## L-02 : deux moteurs de guidage. **Verdict : NUANCÉ**
- Confirmé : les branches "AltGr+Shift" (LM:1919-1920) et "CapsShift" (LM:1862) sont mortes, avec 0 occurrence. "Caps+Shift" figure bien dans 549 méthodes : 500 deadkey, 45 direct, 4 activations (layers.py).
- **Faux :** l'audit affirme que le tutoriel ne tire aucune Maj de "Caps+Shift" (l.1919). En réalité, les méthodes directes Caps* passent par la branche Caps (LM:1852-1865), qui ajoute Maj dès que Verr. Maj. est active (l.1862). La ligne 1919 ne reçoit "Caps+Shift" ou "Caps+Shift+AltGr" que pour des méthodes deadkey ou d'activation, tirées de `_charMethodsCaps` (LM:702, 725, exercices KeepCaps 1839-1843). Le trou est latent, et seulement là.
- La vraie divergence est sémantique. Le tutoriel guide pas à pas, selon l'état (Caps, touche morte active, KeepCaps, remplacement de l'exercice 6 en 1845-1849). Les Leçons surlignent tout d'un coup, sans état (LW:2450-2457). Une fonction unique oblige à choisir, donc à changer le comportement pédagogique de l'une des deux fenêtres.
- **Correction :** remplacer l'exemple erroné par cette divergence, garder la confiance « moyenne » et ajouter au risque le test LessonCoreTests.cs:588.

## L-07 : ~135 lignes mortes. **Verdict : CONFIRMÉ**
- Tout est vérifié. Aucun `_inTransition = true` (supprimé en d89d358). Aucun SetTimer de 8002 ni de 8302, même par littéral. PaintLegend n'a pas d'appelant. IsStep2Key rend false, et l'étape 2 reste atteinte par `_highlightType` (LM:1985). `replayMode: true` a disparu en b20c42c. CLR_CHAR_ACTIVE, CLR_CHAR_ALTGR_ACCENT et MB_ICONINFORMATION ne sont que déclarés. `_consecutiveErrors` n'est jamais lu.
- Réflexion : les tests ne visent que `_highlightedScancodes`, `_currentCharError` et OnBackspace. FrenchTypographyTests.cs:27 énumère les propriétés de L, donc retirer Learning_LegendDeadKey est sans effet. Le constructeur à 4 arguments (ExcludedTypingLifetimeTests.cs:49, 62) reste compatible.
- **Correction mineure :** TIMER_AUTO_HINT est tué 10 fois, pas 12 (12 occurrences au total). Ajouter `_hFontTransition` et CLR_TRANSITION, qui meurent avec PaintTransition.

## V-01 : deux moteurs de clavier en 1.3.0. **Verdict : NUANCÉ**
- Confirmé : `merge-base --is-ancestor` rend 1 pour fb0b854 et pour b8dcdda. La base commune est 8775489 ; 98 commits côté release, 49 côté main. KeyboardRenderer.Draw n'a qu'un appelant : LessonsWindow.cs:1497.
- **Sous-estimé :** CH4b ne se reprend pas en deux commits. b8dcdda:VirtualKeyboard.cs:1018 lit `Theme.Current`, et la ligne 18 cite KeyboardTheme.HighlightPaint. Theme.cs et KeyboardTheme.cs sont absents de f0a98ba et de 8775489 (ils arrivent sur `main` le 2026-08-28 avec 1eae109, 51d9645 et 8e89e1d). Reprendre CH4b revient donc à reprendre la refonte graphique CH0 à CH4b. Les -160 lignes ne mesurent que VirtualKeyboard.cs sur `main`, pas le solde sur la release.
- **Correction :** poser la décision ainsi : « la 1.4.0 part de `main`, refonte graphique comprise, ou reste sur la release ». Préciser que F-01 et L-01 en dépendent.

## V-04 : cinq chargeurs de character-index.json. **Verdict : NUANCÉ**
- Les cinq lecteurs sont confirmés (grep). Au démarrage, il n'y a normalement qu'un parse : CharacterSearch (TrayApplication.cs:397). Le clavier virtuel reçoit les noms par GetCharacterNames (l.414), et son propre chargeur n'est qu'un repli.
- Gain gonflé. Chaque lecteur garde sa projection : la première variante Caps* dans LM:702, GetBoolean dans LM:704, la normalisation dans CS:353-355. Estimation réaliste : **-100 à -150**. L'application publiée est NativeAOT, donc les 27-32 ms « JIT » ne s'y appliquent pas : il reste ≈17 ms au démarrage.
- Déplacer MethodData touche aussi les tests (LessonCoreTests.cs:547-560). La gravité « majeur » est limite ; elle devient « moyen » si le gain réel reste sous 100 lignes.

## F-10 : clavier des liens et Entrée. **Verdict : NUANCÉ (constat mixte)**
- La duplication est confirmée : trois sous-classes de lien, et deux décodages de WM_GETDLGCODE (base 0 à OnboardingWindow.cs:1229, contre DefSubclassProc à AboutWindow.cs:445).
- **Échap sur un lien de l'Accueil : bug réel probable, confirmé par lecture.**
  - Le lien répond DLGC_WANTALLKEYS pour toute touche autre que Tab (OnboardingWindow.cs:1226-1229, DialogNavigation.cs:75-80).
  - IsDialogMessageW ne convertit donc pas Échap en IDCANCEL : il transmet WM_KEYDOWN au lien.
  - Or la sous-classe ne traite que VK_RETURN (l.1231-1237), et le STATIC ignore le reste.
  - À propos traite ce cas explicitement (AboutWindow.cs:463-467).
  - Liens concernés : Leçons, Guide, Avis et Discord (l.503-525), plus le bandeau (l.493).
- **Entrée sur « Annuler » de la Pause : non démontré, probablement faux.**
  - La seule source est le commentaire DialogNavigation.cs:90-95. Il vient d'une revue de code (3adbe37), sans mesure.
  - Dans une fenêtre qui n'est pas un dialogue, IsDialogMessageW donne au bouton atteint par Tab le style par défaut. C'est ce que fait Wine (user32 dialog.c, DIALOG_FixChildrenOnChangeFocus → BM_SETSTYLE BS_DEFPUSHBUTTON), et Raymond Chen note qu'IsDialogMessage envoie DM_GETDEFID et DM_SETDEFID à de telles fenêtres.
  - Entrée envoie alors l'identifiant du bouton focalisé. Tab vers Annuler puis Entrée annulerait donc bien.
  - Seul cas restant : clic souris sur ▲▼ puis Entrée envoie IDOK, ce qui valide la pause. Ce comportement se défend.
- **Recette :**
  - (a) Accueil, étape 3 : Tab jusqu'à « Guide » (le cadre K5 apparaît), puis Échap. Attendu : fermeture. Prédit : rien. Témoin : le même geste ferme À propos.
  - (b) Pause : Tab jusqu'à « Annuler », en regardant si le cadre épais du bouton par défaut passe à Annuler, puis Entrée. Vérifier ensuite l'état de pause dans la zone de notification.
  - (c) Pause : clic sur ▲, puis Entrée.
- **Correction :** garder Échap comme bug à signaler à Antoine. Ramener la Pause à « à vérifier par (b) ». Ne pas imposer PressFocusedButton à la Pause avant cette recette.

## V-07 : clic décalé dans la liste de résultats. **Verdict : CONFIRMÉ (lecture), bug réel mais mineur**
- Au dessin, le haut de la ligne visible k vaut searchAreaH + 1 + k + Σ rowH (CharacterSearch.cs:1325, 1379, 1386). Le test de clic, lui, part de searchH + Σ rowH (l.1162-1178), et searchH vaut searchAreaH (l.1162 et 1304).
- Le décalage est de k+1 px, fixe, non mis à l'échelle. Les 1, 2 et 3 derniers pixels des lignes 1 à 3 insèrent le caractère suivant ; les 4 derniers de la ligne 4 n'insèrent rien. Une ligne fait au moins Scale(44) (l.1532), soit 2 à 7 % de sa hauteur à 100 %.
- ResizeToFitResults ignore `_scrollOffset`, mais cela ne compte qu'après un défilement suivi d'un nouvel affichage (l.892) ou d'un changement de DPI (l.1030) : Search remet le décalage à 0 (l.456).
- **Recette :** échelle 100 %, Bloc-notes comme cible, requête « fleche » (6 résultats). Avec la Loupe à 400 %, cliquer sur la dernière rangée de pixels de la ligne 2, juste au-dessus du séparateur. Le caractère inséré devrait être celui de la ligne 3.
- **Correction :** écrire « 1 à 3 px au bas des lignes » au lieu de « le bas d'une ligne ». Extraire le test de clic en fonction pure testée (c'est le LayoutRows proposé).

## L-14 : fichier de progression illisible → sauvegardes refusées en silence. **Verdict : CONFIRMÉ, cadrage à corriger**
- La chaîne est confirmée : le catch de Load pose `_loadFailed` (LessonProgressStore.cs:229-238), Save refuse (248-252), CommitOrRollback annule en mémoire (169-173). LessonCoreTests.cs:447-468 vérifie exactement ce refus silencieux. Aucun signal à l'écran : `_loadFailed` est privé, seul le journal le voit.
- Nuance : c'est une politique voulue, identique à celle de ConfigManager (ConfigManager.cs:1016-1019 et 1055-1057, test ConfigManagerCompatTests.cs:188). Ce n'est pas une régression, mais un trou de robustesse et d'expérience. L'absence de Flush(true) est, elle, un vrai écart avec ConfigManager.Save.
- Déclencheur absent du constat : LessonsWindow vit tout le processus (TrayApplication.cs:1395-1407 ; magasin créé en LessonsWindow.cs:152). Une IOException passagère au premier chargement bloque donc les sauvegardes jusqu'au redémarrage, par exemple pendant le File.Replace d'une seconde instance (le cas RDP + console d'AG130-11).
- **Défaut réel, déclenchement rare.** À signaler à Antoine comme robustesse, sans caractère bloquant.
- **Recette :** quitter l'application. Remplacer lessons-progress.json (dossier LocalApplicationData de l'application, virtualisé en MSIX) par le texte « { invalid ». Relancer et réussir un exercice. Attendu : exercice non coché, aucun message, et « Sauvegarde ignoree » dans le journal à chaque navigation.

## V-03 : StartsWith culturel sur des chaînes normalisées. **Verdict : NUANCÉ**
- Les faits sont exacts. Les lignes 597, 602, 657 et 701 appellent StartsWith sans StringComparison. InvariantGlobalization=false (AZERTYGlobal.csproj:24-27), donc la culture ICU s'applique réellement. Mesure sur réplique : 2,19 ms contre 0,26 ms, avec le même top 20 sur 120 requêtes. Les lignes 258 et 277 ne jouent qu'au chargement.
- La gravité est gonflée : 2 ms par frappe dans une zone de recherche, hors du chemin du hook, c'est imperceptible. **Mineur.** La correction en ordinal est sans risque, et c'est la sémantique voulue, puisque NormalizeForSearch passe en FormD, retire les marques et met en minuscules.

---

## Récapitulatif

| Id | Verdict | Correction principale | Bug réel ? |
|---|---|---|---|
| F-01 | NUANCÉ | Partir de ThemeWindow (`main`, CH0) ; gain lié à la base de la 1.4.0 | — |
| F-03 | CONFIRMÉ | Setter propre à chaque contrôle (onglets TCM_SETITEMW) ; décaler aussi les rectangles de clic | — |
| L-01 | NUANCÉ | Le profil ne couvre pas le tutoriel (8 écarts) ; partir de KeyHighlight (`main`) ; -500 à -650 ; test LessonCoreTests:588 | — |
| L-02 | NUANCÉ | Exemple faux (LM:1862 tire bien Maj) ; divergence réelle : progressif ou simultané | latent (LM:1919, variantes Caps en touche morte) |
| L-07 | CONFIRMÉ | KillTimer ×10 et non ×12 ; ajouter `_hFontTransition` et CLR_TRANSITION | — |
| V-01 | NUANCÉ | CH4b dépend de Theme et KeyboardTheme : décision « base `main` ou release » | — |
| V-04 | NUANCÉ | Gain -100 à -150 ; ≈17 ms en AOT ; majeur limite | — |
| F-10 | NUANCÉ | Échap sur lien de l'Accueil = bug ; Pause = à vérifier | **Oui, probable** (Échap Accueil) ; Pause improbable |
| V-07 | CONFIRMÉ | Quantifier (1 à 3 px) ; test de clic en fonction pure | **Oui, mineur** |
| L-14 | CONFIRMÉ | Politique voulue (comme ConfigManager) ; ajouter Flush(true) ; blocage passager pour la session | **Défaut réel, rare** |
| V-03 | NUANCÉ | Gravité mineure ; ordinal sans risque | — |

## Non vérifié
- Comportement réel d'IsDialogMessageW sous Windows (Wine lu comme approximation) : la recette (b) tranche.
- Aucun build ni test lancé, application non lancée, conformément au mandat. Bancs non rejoués. Comptes de `lecons-doublons.py` et de `fenetres_cover.py` non recomptés.
- Le trou de LM:1919 sur les variantes Caps en touche morte n'a pas été relié à un caractère réel des exercices 1 et 2.
