# Banc de capture : report depuis main (synthèse de la revue du 25/09)

Source : sous-agent de la revue de main, en lecture seule. Les lignes citées valent pour la release `4e0687b` ; le lot 1 a ensuite retiré du code mort (`12f0758`, `c964cac`, `f93efc8`, …), à recaler.

## Décision de conception (option A, recommandée)

- **Aucun crochet dans `src/`.** Tout passe par réflexion depuis `AZERTYGlobal.Tests`, qui voit les membres `internal` (`src/AssemblyAttributes.cs:7`) ; les membres privés sont atteints par réflexion.
  - Champs `_hWnd` : About :35, Conflit :38, Stats :44, Couches :31, Paramètres :193, Accueil :95, Clavier virtuel :230, Pause :28, Module d'essai :245. Lessons expose `public IntPtr Handle` (:183).
  - Conséquence : `src/` reste intact, donc aucun pixel ne peut changer par construction. Le fichier du banc peut aussi être déposé sur un commit « avant » pour une comparaison.
- **Fichiers à porter** depuis `main` (`git show main:<chemin>`, main = `5ee46c8` = `origin/archive/main-charte-2026-09`) :
  - `src/AZERTYGlobal.Tests/CaptureBench.cs` (371 l.) ;
  - `src/AZERTYGlobal.Tests/KeyboardContextBench.cs` (260 l.) ;
  - `KeyboardPlank.cs`, facultatif : remplacer `Theme.Brush/Font/palette` par `CreateSolidBrush`/`CreateFontW`/constante.
  - **Ne pas porter** : `StatesBoard.cs`, `KeyboardStatesBoard.cs` (charte pure).
- **Retouches de `CaptureBench`** :
  - supprimer Density/TypeScale (43-85), la boucle de thème (124-131) et les `using` d'override (137-140) ;
  - ajouter 168 (175 %) aux échelles ;
  - `window.Handle` → réflexion `_hWnd` ;
  - Pause : `OpenForCapture` → réflexion sur `CreateWindow(IntPtr)` privé ;
  - Paramètres : 3 onglets, `SetActiveTab(SettingsTab)` privé (SettingsWindow.cs:952, enum privé :185) ;
  - Accueil : 3 étapes, `_currentStep` (:96) + `UpdateStepVisibility()` privé (:562) ;
  - P/Invoke manquants **déclarés dans le projet de test** : `PrintWindow`, `PW_RENDERFULLCONTENT`, `PeekMessageW`, `PM_REMOVE`, `GdipCreateBitmapFromHBITMAP`, `GdipSaveImageToFile`, `PngEncoderClsid`.
- **Retouches de `KeyboardContextBench`** :
  - supprimer la boucle de thème ;
  - Lessons : `OpenForCapture` → réflexion (`SelectExercise` :777, `CenterOnActiveMonitor` :438, `UpdateRenderScaleFromCurrentClient(true)` :575, `ShowHintCore` :2358, `_visible` :75) ;
  - clavier virtuel : `_hWnd` par réflexion ;
  - module d'essai : `_hWnd` au lieu de `FindWindowW`, dont le nom de classe est partagé avec l'app installée ;
  - supprimer les suffixes `-clair`/`-sombre`.
- **DPI sans toucher `src/`** : le banc envoie `SendMessageW(hwnd, WM_DPICHANGED, (dpi<<16)|dpi, &rectProportionnel)`. Les 10 gestionnaires lisent le HIWORD et appliquent le rectangle. L'arrondi peut différer d'1 px d'une fenêtre « née » à ce DPI, mais il s'annule dans un avant/après rendu par la même voie.
- **Thème** : la release n'en a qu'un (couleurs en dur, barre de titre sombre forcée) : un seul rendu par fenêtre.
- **Isolation** :
  - `ConfigManager.OverrideConfigPathForTests` (redirige aussi la progression) et `UsageStats.OverrideStatsPathForTests` vers un dossier temporaire ;
  - langue fixée (`appLanguage` fr, puis en si utile), sinon le runner en-US rendrait en anglais ;
  - clavier virtuel : `ShowWindow(h, 8)` au lieu de `Show()`, pour ne pas compter d'ouverture ;
  - mapper sur `MockWin32Api`, hook jamais installé.
- **Portes** : variables `AZERTY_CAPTURE` et `AZERTY_CONTEXTE`. Sans elles, le [Fact] sort tout de suite (compte comme réussi, +2 au compteur de la suite application). Sortie PNG dans un dossier donné par variable (ex. `AZERTY_CAPTURE_DIR`).
- **Mécanique** reprise de main :
  - PrintWindow(PW_RENDERFULLCONTENT), puis GDI+ plat vers PNG ;
  - 20 tours de pompe PeekMessage/Sleep(15) avant chaque capture ;
  - Dispose, puis 5 tours de pompe ;
  - un seul Assert qui ramène les échecs.

## Risques

- **Application Control** peut bloquer `AZERTY Global.dll` en local. Compter les PNG, jamais se fier au code de retour. Voie fiable : la CI.
- **CI** : un workflow séparé `workflow_dispatch` (entrées `avant` et `apres`, deux refs) avec, pour chaque ref :
  - checkout ;
  - dépose des fichiers du banc depuis la tête ;
  - deux passes ;
  - comptage des PNG, puis upload-artifact.
  - Runner à 96 DPI et 1024×768 : Lessons et Paramètres y seront réduits.
- **Non-déterminisme** mesuré sur main : 1 à 2 captures sur 54 à 80 diffèrent d'une passe à l'autre. Parade : deux passes par état, lancées seules, et un comparateur qui signale les différences stables seulement.
- **Common-Controls v6** n'est pas déclaré dans `app.manifest` : vérifier la première image contre une capture réelle.
