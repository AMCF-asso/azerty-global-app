# Accessibilité — arbitrage 1.3.0 / 1.3.1

## Décisions d’Antoine (QCM du 2026-09-22, 23 h)

1. La case Partner Center « This product has been tested to meet accessibility guidelines » est **décochée à la soumission 1.3.0**. Elle ne sera recochée qu’après la conformité complète, mesurée au Narrateur et en contraste élevé.
2. La 1.3.0 reçoit les **correctifs rapides** ci-dessous (≈ une session). Les chantiers lourds partent en **1.3.1**.
3. Ces correctifs changent le code : le bundle `768f13fb…` n’est plus le candidat. La recette et le WACK se feront sur le bundle qui suivra.

## Critères Microsoft

Source : [product declarations](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/product-declarations), [accessibility checklist](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-checklist). Relevé complet et citations : `evidence/accessibilite-criteres-microsoft.md`.

- noms accessibles pour tous les éléments ;
- navigation complète au clavier ;
- contraste d’au moins 4,5:1 ;
- tests avec Inspect ou AccChecker ;
- tests avec le Narrateur, la Loupe, le clavier visuel, le contraste élevé et une mise à l’échelle élevée.

Microsoft écrit de ne pas cocher la case sans conception et tests dédiés. La politique Store (7.19 comme 7.20) ne vise pas la case nommément ; la règle 10.1 sur l’exactitude des métadonnées s’y applique par déduction.

## Mesure automatique : AxeWindows CLI

- Version 2.4.2 de [microsoft/axe-windows](https://github.com/microsoft/axe-windows), licence MIT, téléchargée avec l’accord d’Antoine. Archive `AxeWindowsCLI-2.4.2.zip`, SHA-256 `aeca43f41c89b3ffb1db84011539e609ecd7cb3badd6e78fada2ada327d10a64`.
- `AxeWindowsCLI.exe` est signé par Microsoft Corporation (signature valide). Il est autonome, sans runtime à installer. Copie de travail : scratchpad de la session, hors dépôt.
- Contrôlé sur l’hôte : sur le Bloc-notes, il rend « 0 errors were found » et écrit un `.a11ytest`.
- Intégré au kit : `sandbox\lancer-recette.ps1 -Bundle <bundle> -AxeDir <dossier de la CLI>` installe le candidat, puis `sandbox\axe-scan.ps1` ouvre chaque fenêtre et l’analyse avec `--processid`, une seule fenêtre visible à la fois. Les fenêtres s’ouvrent par un `WM_COMMAND` envoyé à `AZERTYGlobal_Wnd`, identifiants `IDM_*` de `TrayApplication.cs:28-63`.
- **Scanner validé le 2026-09-23.** Référence sur le bundle `768f13fb` (run CI 35779820283, Windows Sandbox) : 8 analyses, 0 erreur chacune. Preuves dans `evidence/recette-768f13fbe9e7/axe/`, synthèse `resume.json`.
  - ⚠️ 8 analyses, mais 6 fenêtres distinctes seulement (lu dans `resume.json`) : les étapes « lancement », « accueil » et « lecons » portent le même HWND 196660, classe `AZERTYGlobal_Onboarding`. L’étape « lecons » a donc analysé l’accueil, pas les Leçons, qui ne le sont que par l’étape « defi » (`AZERTYGlobal_Lessons`). À corriger dans `axe-scan.ps1` avant le scan du prochain candidat.
  - En 2.4.2, `--scanrootwindowhandle` est inopérant : sortie 2 sans message, mesuré aussi sur le Bloc-notes. `axe-scan.ps1` passe donc par `--processid`, avec une seule fenêtre visible à la fois. Les essais ratés restent dans `axe-essai1/` et `axe-essai2/`.
  - ⚠️ 0 erreur ne prouve pas l’accessibilité : AxeWindows ne contrôle que ce qui est exposé à UI Automation, et les surfaces dessinées en GDI (Leçons, textes de l’accueil, résultats de la Recherche, clavier virtuel) n’y sont pas exposées du tout (N1 à N12 ci-dessous). C’est une base de comparaison avant/après, pas une preuve.

## Lot 1.3.0 — correctifs rapides (faits le 2026-09-23, recette à jouer)

Les identifiants et les lignes viennent de l’inventaire du 2026-09-22 (lecture du code, section suivante).

| # | Correctif | Fichier:ligne (inventaire) | État au 2026-09-23 |
|---|---|---|---|
| K1 | Couches : inscription à `DialogNavigation` et focus initial | `MaintainableLayersWindow.cs:53-59, 97-127` | Fait, `44cd7e0`. Inscrite le temps d’être visible, focus initial sur la case principale, Entrée et Échap ferment en enregistrant. Pas de témoin automatique (il faudrait une fenêtre réelle) ; recette B6. |
| K2 | Conflit : `IDOK` et `IDCANCEL` traités, pour qu’Échap et Entrée agissent | `LayoutConflictWindow.cs:259-276` | Fait, `4ab1a2c`. Échap garde l’app ; Entrée presse le bouton focalisé et lui seul, jamais « Quitter » par défaut, car la fenêtre peut surgir pendant une frappe. Témoins existants de `ButtonToPressOnEnter` ; recette B7. |
| K3 | Exercices : « Passer » atteignable au clavier, focus visible sur les boutons owner-draw | `LearningModule.cs:898-904, 1425, 2760-2782` | Fait, `356cbcd`. Tab et Maj+Tab entre la surface de frappe, « Quitter » et « Passer » ; Entrée et Échap sur ces boutons ; cadre de focus owner-draw. Témoin `NextFocusStop`, 5 tests ; recette B8. |
| K4 | Leçons : libellé des boutons-icônes affiché aussi au focus clavier | `LessonsWindow.cs:1839-1849, 2017` | Fait, `6155517`. Témoin `FindHoverAreaFor`, 3 tests ; recette B9. |
| K5 | Liens de l’accueil et d’À propos : focus signalé autrement que par la seule couleur | `OnboardingWindow.cs:818, 825`, `AboutWindow.cs:366` | Fait, `770750f`. Cadre de focus autour du texte du lien. À propos ne repeignait pas le lien quand le focus changeait, si bien que même la couleur ne suivait pas : corrigé aussi. Témoin `FocusRectForLink`, 3 tests ; recette B10. |
| K6 | Accueil : drapeau de langue activable au clavier | `OnboardingWindow.cs:857-869` | Fait, `b81fa3f`. Bouton owner-draw posé sur le drapeau, dernier arrêt de tabulation, nommé par l’endonyme de la langue cible, masqué quand la langue est imposée. Témoin `FlagButtonName`, 1 test ; recette B11. |
| N8 | Noms des champs (Paramètres, Recherche, Pause) ; le champ Heures serait nommé « Minutes » (supposé) | `SettingsWindow.cs:453-467, 551`, `CharacterSearch.cs:750`, `PauseDurationDialog.cs:160-164` | Fait, `af2df9a`. Soupçon confirmé par une sonde hors dépôt : le champ Heures s’annonçait « Minutes », le champ Minutes n’avait aucun nom. Étiquette STATIC juste avant chaque champ, cachée quand le libellé visible est dessiné ; la liste des apps suspendues est ajoutée. Pas de témoin automatique ; recette B12. |
| N10 | Boutons ▲▼ de la Pause nommés autrement que par leur glyphe | `PauseDurationDialog.cs:165-168` | Fait, `669444b`. Dynamic Annotation (`SetHwndPropStr`, `PROPID_ACC_NAME`), lue en MSAA et en UIA. Témoin `SpinButtons`, 3 tests ; recette B12. |
| D1 | Pause, Couches, Indicateur : mise à l’échelle DPI | `PauseDurationDialog.cs:100-118`, `MaintainableLayersWindow.cs:85-127`, `LayerIndicatorWindow.cs:25, 40` | Fait, `1b1338d`. Témoins `ScaleForDpi`, 4 tests, et `IndicatorSize`, 3 tests ; recette B13. |
| D3 | Leçons : taille minimale bornée à la zone de travail (à mesurer d’abord) | `LessonsWindow.cs:840-852` | Mesuré, puis fait, `af84b17`. Windows impose `ptMinTrackSize` jusqu’à `CreateWindowEx` et `MoveWindow`, et le minimum dépassait la zone de travail d’un 1920×1080 dès 175 %, annulant le plafond d’AG130-42. Témoin `MinimumClientSize`, 6 tests ; recette B13. |

Vérifié en local le 2026-09-23 : build Release et publish win-x64 sans nouvel avertissement, 147 tests Python, et les trois suites .NET à 18, 235 et 507 tests, 0 échec. Application Control a laissé passer la suite application ce jour-là ; rien ne garantit qu’il le fera au run suivant, la CI reste la preuve. Mutations jouées : 9 sur `WindowSizing` (copie hors dépôt) et 10 sur les autres décisions pures, toutes rouges sur les bons tests, fichiers restaurés à l’identique.

Non vérifié : le rendu à l’écran, la lecture par le Narrateur, et la Dynamic Annotation sous NativeAOT. Le banc AOT a été refusé par Application Control ; le même code tourne en JIT, et un autre processus y lit bien le nom posé, en MSAA comme en UIA.

Règles du dépôt : jamais `Edit` sur un `.cs`, patch Python en `newline=''`, fin de ligne mesurée sur le fichier. Chaque correctif porte son témoin. Ensuite : CI par `ci/verif`, nouveau bundle attesté, scan AxeWindows avant/après, recette partie A.

## Lot 1.3.1 — conformité complète avant de recocher la case

- **N1-N7, N9, N11, N12** : exposer à UI Automation (`WM_GETOBJECT` → `UiaReturnRawElementProvider`) les textes et cibles dessinés. Concerne l’accueil, la Recherche, le Conflit, les Statistiques, À propos, les Exercices, les notifications d’état et le clavier virtuel, puis **les Leçons**, entièrement dessinées (effort L).
- **C1-C5** : contraste élevé (`SPI_GETHIGHCONTRAST`, `GetSysColor`, réaction à `WM_SYSCOLORCHANGE`). Concerne 167 constantes `CLR_*`, 2 `ARGB_*` et 22 littéraux, la barre de titre sombre forcée, les ratios sous 4,5:1 et l’information portée par la seule couleur.
- **D2, D4** : hauteurs non bornées à 200 % (accueil, Statistiques) ; réglage Windows « Taille du texte » et polices de 9 à 10 px.

## Inventaire du 2026-09-22 (lecture du code, rien mesuré à l’écran)

- 0 fournisseur UIA côté serveur dans `src/` : aucun `WM_GETOBJECT`, `UiaReturnRawElementProvider`, `NotifyWinEvent` ni `IAccessible`. L’UIA de `TypingEngine.Windows` est un client.
- Contraste élevé ignoré : 0 `SPI_GETHIGHCONTRAST`, `GetSysColor` ou `WM_SETTINGCHANGE`.
- 11 fenêtres peignent en GDI ; les Leçons n’ont aucun contrôle natif. 6 fenêtres sur 13 passent par `DialogNavigation`. Aucun `DrawFocusRect`.
- PerMonitorV2 déclaré ; `WM_DPICHANGED` traité dans 10 fenêtres. `TextScaleFactor` absent.
- Aucun test ne porte sur l’UIA, le contraste élevé ou le rendu DPI.

| Écart | Fenêtre | Fichier:ligne | Effort |
|---|---|---|---|
| N1 résultats et « Copié » dessinés sans UIA | Recherche | `CharacterSearch.cs:1274-1330, 1388-1407` | M |
| N2 texte explicatif et de consentement dessiné | Accueil | `OnboardingWindow.cs:1180-1278, 1377-1447` | M |
| N3 texte d’aide dessiné | Conflit | `LayoutConflictWindow.cs:291-389` | S |
| N4 aucune cible ni aucun texte exposé | Leçons | `LessonsWindow.cs:997-1116, 1179-1199, 1398-1424, 1926` | L |
| N5 consigne, cible et erreurs non annoncées | Exercices | `LearningModule.cs:1957-2060` | M |
| N6 statistiques dessinées | Statistiques | `UsageStatsWindow.cs:487-656` | S-M |
| N7 états non annoncés | Bascule, Indicateur, Paramètres | `ToggleNotification.cs:66, 181`, `LayerIndicatorWindow.cs:39`, `SettingsWindow.cs:1512` | S |
| N9 11 liens exposés comme texte | About, Accueil, Statistiques | `AboutWindow.cs:214-237`, `OnboardingWindow.cs:484-516`, `UsageStatsWindow.cs:183-199` | S-M |
| N11 clavier virtuel non exposé | Clavier virtuel | `VirtualKeyboard.cs:993-1209` | L |
| N12 version et description dessinées | À propos | `AboutWindow.cs:464-565` | S |
| C1 couleurs en dur | 12 fichiers | 167 `CLR_*`, 2 `ARGB_*`, 22 littéraux (dont `LearningModule.cs` 36, `OnboardingWindow.cs` 28) | L |
| C3 barre de titre sombre forcée | 10 fenêtres | `Win32.cs:544-550` | S |
| C4 ratios calculés sous 4,5:1 (2,4 à 3,8) | Palette claire, Recherche, Exercices | constantes `CLR_*` | S |
| C5 information portée par la seule couleur | Exercices, clavier virtuel | `LearningModule.cs:2025-2047`, `VirtualKeyboard.cs:761-770` | M |
| D2 hauteur non bornée à 200 % | Accueil, Statistiques | `OnboardingWindow.cs:388-408`, `UsageStatsWindow.cs:153-172` | S-M |
| D4 « Taille du texte » ignorée | Toutes | `SettingsWindow.cs:338, 346`, `OnboardingWindow.cs:44, 276` | M-L |
