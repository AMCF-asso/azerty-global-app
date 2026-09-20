# Vérification 03-fenetres-rendu

Citations : 74 total · EXACT 55 · DÉCALÉ 8 · ABSENT 0 · NON VÉRIFIABLE 11

63 ancres `fichier:ligne` + 11 lignes sans ancre (F2.3, F4.3, F4.6-F4.8, F5.1, F5.3, F5.7, F6.5,
F6.7, F8.3 → NON VÉRIFIABLE ; comptages refaits). Méthode : `sed -n` ±3 lignes, `grep -n -F`, puis
`grep -rn -F` sur `src/` ; une citation répartie sur 2-3 lignes consécutives **dans la plage
citée** compte EXACT. HEAD `4d5da24`, lecture seule.

## Comptages refaits

`X` = `--exclude-dir` sur `*Tests*`, `bin`, `obj`, `TestSupport` ; exécuté depuis `src/`.

| constat du rapport | commande | résultat | accord |
|---|---|---|---|
| §5 F5.3 « 164 constantes `CLR_*` », 12 fichiers | `grep -rn $X --include="*.cs" -E "const +uint +CLR_[A-Z0-9_]+ *="` | **167** sur 13 fichiers ; les 12 listés font 164, chiffre par fichier exact ; `ToggleNotification.cs` (3) hors liste | partiel |
| §5 F5.3 « `CLR_BG` 11 fois, 5 valeurs » | `grep -rn $X -E "const +uint +CLR_BG *="` | 11 définitions ; 5 valeurs : `0x00DDDDDD` ×6, `0x00201C18` ×2, `0x001A1A1A`, `0x00282828`, `0x00302D2A` | oui |
| §4 F4.1 « 48 `WS_TABSTOP` » | `grep -rno $X "WS_TABSTOP" \| wc -l` | **49** : 48 dans les 8 fenêtres (répartition exacte), la 49ᵉ est la définition `Win32.cs` | oui |
| §4 F4.3 « 0 `WM_GETOBJECT`/`IAccessible`/`UiaRaiseAutomationEvent`/`NotifyWinEvent` » | `grep -rn $X -E "WM_GETOBJECT\|IAccessible\|Uia…\|NotifyWinEvent"` | 0 | oui |
| §2 F2.3 « `WM_DPICHANGED` dans **7** fenêtres » puis liste de 9 | `grep -rn $X "WM_DPICHANGED"` | **9** fichiers, et les 9 `fichier:ligne` cités sont tous exacts | non (9≠7) |
| §2 F2.4 `WM_DPICHANGED` absent de LessonsWindow, `_dpiScale` absent des 3 autres | `grep -c` sur les 4 fichiers | `WM_DPICHANGED` 0/0/0/0 ; `_dpiScale` : Lessons 4, Maintainable 0, Pause 0, LayerIndicator 0 | oui |
| §6 F6.2 « 29 noms de touches mortes » | `sed -n '98,128p' VirtualKeyboard.cs \| grep -c '\["dk_'` | 29, lignes **99-127** (le rapport écrit 100-126) | oui |
| §6 F6.5 « Zéro U+202F dans toute l'application » | parcours Python de `src/**/*.cs` hors tests | 0 dans `Localization/`, **1** dans `src/` : `UsageStats.cs:232` (`case` de parsing, non affiché) | non (1) |
| §6 F6.5 table NBSP / apostrophe par fichier `L.*` | comptage Python U+00A0 et U+2019 (aucun échappement hexa dans le source) | apostrophe `’` : **16/16 exactes** ; NBSP : 12 fichiers sur 16 en écart — Tray 24 annoncé/12 mesuré, Settings 16/8, Challenge 9/6, Onboarding 7/4, Lessons 2/0, L.cs 3/0, Startup 1/0, et 5 autres à −1 | non |
| §3-§8, les 15 autres « 0 occurrence » : `IsDialogMessageW`, `TranslateAcceleratorW`, `ODS_FOCUS`, `DrawFocusRect`, `GetSysColor`, `SystemParametersInfoW`, `SPI_GETHIGHCONTRAST`, `SPI_GETCLIENTAREAANIMATION`, `WM_SETTINGCHANGE`, `RGB(`, `RedrawWindow`, `RDW_ALLCHILDREN`, `MonitorFromRect`, `CreateCaret`, `GetCaretBlinkTime` | `grep -rno $X -F` | 0 pour les 15 | oui |
| §8 F8.3 « ni `Thread.Sleep`, ni `.Wait()`, ni `.Result` » | `grep -rn $X -E "\.Wait\(\)\|\.Result"` | `Thread.Sleep` 0 ; 2 correspondances = l'identifiant `ResultChangedLate` (TrayApplication 397, 1790) | oui |
| §8 F8.4 « 28 `KillTimer` / 6 `SetTimer` » ; §6 F6.6 « habitude » ×1 ; F6.7 « quelques heures »/« a few hours » ×0 ; §5 aucun fichier ne contient « Theme » ; §8 F8.1 605 678 octets ; §9 F9.1 29 `dead_keys` | `grep -c`, `grep -rl $X`, `getsize`, `json.load` | 6 et 28 ; 1 (`ConfigManager.cs:217`) ; 0 et 0 ; 0 fichier ; 605678 ; 29 | oui |
| §5 F5.2 10 appels `EnableDarkTitleBar` + `CLR_BG=0x00DDDDDD` dans 6 ; F5.5 `WS_CLIPCHILDREN` dans 3 | `grep -rn $X "EnableDarkTitleBar\|WS_CLIPCHILDREN"` | 10 appels et 6 fichiers clairs, aux lignes citées ; CharacterSearch 731, LearningModule 809, LessonsWindow 392 | oui |
| §6 F6.3/F6.4 « vérifié octet par octet » | lecture Python des 4 lignes | F6.3 : 3× U+0027, 0 U+2019/U+00A0/U+202F, identique dans les 2 fichiers ; F6.4 : 0 apostrophe, 0 NBSP | oui |

## Décalées et absentes (détail)

| section | fichier:ligne cité | verdict | vraie ligne | citation (≤ 60 car.) |
|---|---|---|---|---|
| 1 F1.7 | LessonsWindow.cs:869-878 | DÉCALÉ | **861** (2ᵉ fragment `EndPaint` bien en 878) | `oldBitmap = Win32.SelectObject(memDc, …)` |
| 2 F2.10 | SettingsWindow.cs:601 | DÉCALÉ | **602** | `int maxClientH = Math.Max(S(200), …)` |
| 3 F3.2 | SettingsWindow.cs:1043-1049 | DÉCALÉ | **1041-1042** (le code cité est bien en 1048-1049) | `// La position du curseur dépasse 16 bits…` |
| 5 F5.4 | LearningModule.cs:2341 | DÉCALÉ | **2742** (écart 401) | `uint bgColor = hovered ? 0x003838C0u : …` |
| 7 F7.1 | LessonsWindow.cs:582 | DÉCALÉ | **455** ; 582 est la ligne de l'autre copie (VirtualKeyboard) | `var monitor = MonitorFromPoint(center, 0);` |
| 7 F7.2 | LessonsWindow.cs:445 | DÉCALÉ | **444** (VirtualKeyboard.cs:571 exact) | `…bool IsRectVisibleOnScreen(Win32.RECT rect)` |
| 7 F7.4 | SettingsWindow.cs:608-611 | DÉCALÉ | **615** et **617** | `int x = cx - windowW / 2;` |
| 9 F9.6 | KeyboardRenderer.cs:120-122 | DÉCALÉ | **117** (commentaire), **119** (cache) | `// Cache : BuildKeyLayout() alloue un tableau…` |

Aucune ABSENTE. Rattrapés à l'inspection : F6.3/F6.4 (texte bien en LessonCatalog.cs:208 et 206,
seule la queue `", false, false),` vient de LearningModule), F6.9, F6.10, F9.1, F3.2 (multi-lignes).

## Constats à citation confirmée (EXACT ou DÉCALÉ), condensés

| section | fichier:ligne | constat | test |
|---|---|---|---|
| 1 F1.1 | SettingsWindow.cs:1709 | `OnPaint` hors `try/finally` : une exception perd bitmap, DC, `EndPaint`. | aucun |
| 1 F1.2 | OnboardingWindow.cs:1069 | Même structure : `PaintStep1/2/3` hors `try/finally`. | aucun |
| 1 F1.3 | LearningModule.cs:1933 | `EndPaint` hors du `finally` qui libère bitmap et DC. | aucun |
| 1 F1.4 | VirtualKeyboard.cs:1054 | 5 à 7 objets GDI hors `try/finally`, exception avalée par l'appelant. | aucun |
| 1 F1.5 | SettingsWindow.cs:1994 | `Dispose()` sans garde ni remise à zéro des handles ; 3 fenêtres idem. | aucun |
| 1 F1.6 | CharacterSearch.cs:1239 | Brosses créées/détruites dans la boucle : ≈20 allers-retours GDI/WM_PAINT. | aucun |
| 1 F1.7 | LessonsWindow.cs:861-878 | Contre-exemple : tout le cycle sous `try/finally`, `EndPaint` inclus. | aucun |
| 1 F1.8 | LearningModule.cs:762 | Cache de fontes par caractère vidé dans `DestroyFonts`. | aucun |
| 1 F1.9 | VirtualKeyboard.cs:302 | `EnsureFonts` mémorise (cw, ch) : recréation au changement de taille. | aucun |
| 1 F1.10 | GdiHelpers.cs:12 | Seul point GDI testé ; aucune durée de vie de handle. | `Audit120BitmapTests.MasqueIcone…` (l.13) |
| 2 F2.1 | app.manifest:14 | PerMonitorV2 déclaré, plus `dpiAware True/PM`. | aucun |
| 2 F2.2 | Program.cs:19 | Doublon runtime PMv2 avec repli, cohérent. | aucun |
| 2 F2.4 | LessonsWindow.cs:159-161 | DPI lu sur le DC écran et figé : 0 `WM_DPICHANGED` ici. | aucun |
| 2 F2.5 | MaintainableLayersWindow.cs:102 | Tailles de fonte en dur, non multipliées par le DPI (3 fenêtres). | aucun |
| 2 F2.6 | SettingsWindow.cs:240 | `S()` tronque sans plancher ; deux variantes coexistent. | aucun |
| 2 F2.7 | LessonsWindow.cs:390 | Taille jamais bornée à `rcWork` : 1960×1330 calculés à 175 %. | aucun |
| 2 F2.8 | OnboardingWindow.cs:375 | Aucun plafonnement à `rcWork` : ~1060 px de haut à 175 %. | aucun |
| 2 F2.9 | LearningModule.cs:819 | Seule fenêtre à plafonner sa taille à 90 % de `rcWork`. | aucun |
| 2 F2.10 | SettingsWindow.cs:602 | Le plancher `S(200)` prime sur `rcWork` : bas hors écran. | aucun |
| 3 F3.1 | SettingsWindow.cs:640-641 | Plage cohérente avec `ClampScroll` : pouce juste. | aucun |
| 3 F3.2 | SettingsWindow.cs:1041-1049 | `SB_THUMBTRACK` lit `nTrackPos` via `SCROLLINFO` : bon au-delà de 65 535 px. | aucun |
| 3 F3.3 | SettingsWindow.cs:1028-1029 | Ligne et page bornées ; `SB_BOTTOM` puis `ClampScroll`. | aucun |
| 3 F3.4 | SettingsWindow.cs:654-656 | Point de passage unique : enfants suivis par `RepositionControls`. | aucun |
| 3 F3.5 | SettingsWindow.cs:1617 | Tab passe à `DefSubclassProc`, mais nulle part `IsDialogMessageW`. | aucun |
| 3 F3.6 | SettingsWindow.cs:1243-1245 | Aucun défilement clavier : seul Échap est traité. | aucun |
| 3 F3.7 | SettingsWindow.cs:405 | Fenêtre non redimensionnable ; seule `FitWindowToContent` remesure. | aucun |
| 3 F3.8 | SettingsWindow.cs:1080 | Sur `WM_DPICHANGED`, taille suggérée puis remesurée. | aucun |
| 3 F3.9 | SettingsWindow.cs:1059 | Roulette consommée seulement si le contenu déborde. | aucun |
| 4 F4.1 | TrayApplication.cs:546-550 | Boucle sans `IsDialogMessageW` : 48 `WS_TABSTOP` inertes. | aucun |
| 4 F4.2 | PauseDurationDialog.cs:66-70 | La boucle modale du dialogue répète l'omission. | aucun |
| 4 F4.4 | LearningModule.cs:2739 | Boutons `BS_OWNERDRAW` peints au survol seul : `itemState` jamais lu. | aucun |
| 4 F4.5 | SettingsWindow.cs:24 | Seul indicateur de focus dessiné de l'application. | aucun |
| 5 F5.2 | Win32.cs:496-497 | Barre de titre sombre forcée pour 10 fenêtres, 6 à fond clair. | aucun |
| 5 F5.4 | LearningModule.cs:2742 | Couleurs en clair dans le code de rendu, hors de toute constante. | aucun |
| 5 F5.5 | SettingsWindow.cs:405 | `WS_CLIPCHILDREN` absent : le parent peint sous 15 enfants. | aucun |
| 5 F5.6 | SettingsWindow.cs:395 | Brosse d'instance servant de brosse de classe (8 fenêtres). | aucun |
| 6 F6.1 | SettingsWindow.cs:499 | Libellé « Français » en dur hors `Localization/`. | aucun |
| 6 F6.2 | VirtualKeyboard.cs:100-126 | 29 noms de touches mortes en français seulement. | `KeyboardRendererI18nTests` (tooltip seul) |
| 6 F6.3 | LearningModule.cs:36, LessonCatalog.cs:208 | Dupliqué : 3 apostrophes droites, espaces ordinaires aux guillemets. | non (`FrenchTypographyTests` : `typeof(L)`, l.25) |
| 6 F6.4 | LearningModule.cs:30, LessonCatalog.cs:206 | Espace ordinaire avant `!`, même duplication. | non (idem) |
| 6 F6.6 | ConfigManager.cs:217 | « habitude » n'existe que dans ce commentaire. | aucun |
| 6 F6.8 | L.Startup.cs:19 | Les 3 « impossible » visent le démarrage auto et la fenêtre. | aucun |
| 6 F6.9 | L.Tray.cs:117-118 | Le nom du produit apparaît une fois par couple titre+corps. | aucun |
| 6 F6.10 | ToastActivation.cs:168-171 | Corps du toast tronqué à la première `\n`, exprès. | `ToastBodyTests` (l.165, réciproque 177) |
| 7 F7.1 | LessonsWindow.cs:455 | `MONITOR_DEFAULTTONULL` : position hors écran → centrage. | `ConfigManagerCompatTests` 124-136 |
| 7 F7.2 | LessonsWindow.cs:444, VirtualKeyboard.cs:571 | `IsRectVisibleOnScreen` dupliquée à l'identique. | aucun |
| 7 F7.3 | SettingsWindow.cs:412 | Deux conventions de moniteur coexistent (7 contre 2). | aucun |
| 7 F7.4 | SettingsWindow.cs:615-617 | Recentrage puis rappel dans `rcWork`. | aucun |
| 7 F7.5 | LessonsWindow.cs:764-773 | Taille minimale seule posée ; `WM_SIZING` contraint le ratio. | aucun |
| 8 F8.1 | CharacterSearch.cs:223 | 605 678 octets de JSON analysés sur le thread UI. | aucun |
| 8 F8.2 | LearningModule.cs:202 | Lecture disque synchrone sur le thread UI, sous `try`. | aucun |
| 8 F8.4 | LessonsWindow.cs:2382 | 6 minuteries, 28 `KillTimer` : aucune orpheline. | aucun |
| 8 F8.5 | LearningModule.cs:1034-1039 | Deux minuteries courtes au `Show`, tuées ; pas de caret maison. | aucun |
| 9 F9.1 | KeyboardRenderer.cs:83-91 | 7 touches mortes à cercle pointillé en dur sur 29. | aucun |
| 9 F9.2 | LearningModule.cs:2893 | Liste de 7 dupliquée, avec sa copie d'`IsCombiningMark`. | aucun |
| 9 F9.3 | VirtualKeyboard.cs:1157-1159 | Glyphes combinants nus, sans le préfixe `◌` des 2 autres rendus. | aucun |
| 9 F9.4 | KeyboardRenderer.cs:845-850 | Les 5 plages couvrent les marques combinantes du layout. | aucun |
| 9 F9.5 | KeyboardRenderer.cs:190-197 | Hit-test sur la formule du rendu : zones 1 px sous la touche. | aucun |
| 9 F9.6 | KeyboardRenderer.cs:117-119 | Layout en cache, mais `Max(…)` refait à chaque `BuildHitTestRects`. | aucun |
| 9 F9.7 | VirtualKeyboard.cs:314 | Planchers de police sous ~20 px ; `DT_NOCLIP` laisse déborder. | aucun |
