# Audit v1.3.0 — axe « tests et preuves »

Dépôt : `D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store`
Branche : `release/1.2.0-notation-store` — HEAD `4d5da24`
Arbre de travail : propre (2 dossiers non suivis seulement : `Archives/`, `docs/audit-2026-09-15-v1.2.0/test-results-baseline-sdk8.0.423/`)
Mesures du 2026-09-20. Aucune écriture hors `bin/`, `obj/`. Aucun `git` modifiant, aucun `dotnet publish`, aucun témoin de mutation lancé.

---

## 1. Exécution

### 1.1 SDK

```
$ dotnet --version
8.0.425
```

⚠️ Le dossier non suivi `docs/audit-2026-09-15-v1.2.0/test-results-baseline-sdk8.0.423/` (10 `.trx`) montre que la base de comparaison précédente a été prise sous **8.0.423**. Le SDK du poste a bougé depuis.

### 1.2 Les trois suites, `-c Debug --nologo -v minimal`

Lignes de résumé verbatim :

```
Passed!  - Failed:     0, Passed:    18, Skipped:     0, Total:    18, Duration: 15 ms - TypingEngine.Core.Tests.dll (net8.0)
Passed!  - Failed:     0, Passed:   157, Skipped:     0, Total:   157, Duration: 16 ms - TypingEngine.Windows.Tests.dll (net8.0)
Passed!  - Failed:     0, Passed:   326, Skipped:     0, Total:   326, Duration: 1 s - AZERTYGlobal.Tests.dll (net8.0)
```

**Trois compteurs : 18 / 157 / 326 = 501 passés, 0 échec, 0 ignoré.**

⚠️ Le message du commit `404d68a` annonce « 495 tests verts au total ». L'écart de 6 s'explique par `06208ed`, qui ajoute des tests à `DailyChallengeTests.cs` (+100 lignes). Rien d'anormal, mais **le chiffre du commit n'est plus le chiffre du HEAD** — ne pas reprendre « 495 » dans un document de release.

### 1.3 Build Release de `src/AZERTYGlobal.csproj`

Lancé une première fois en incrémental, puis en `-t:Rebuild`. **Même résultat : 2 avertissements, 0 erreur.** Liste verbatim dédoublonnée :

```
D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store\src\TrayApplication.cs(1394,88): warning CS8604: Possible null reference argument for parameter 'procName' in 'string L.Tray_MenuActiveApp(string procName)'. [.../src/AZERTYGlobal.csproj]
D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store\src\TrayApplication.cs(1407,88): warning CS8604: Possible null reference argument for parameter 'processName' in 'string? ConfigManager.GetCompatibilityOverride(string processName)'. [.../src/AZERTYGlobal.csproj]

Build succeeded.
    2 Warning(s)
    0 Error(s)
```

- **`CS86xx` (nullabilité) : 2**, les deux ci-dessus, tous deux dans `src/TrayApplication.cs`, colonne 88, sur le même argument nullable passé à deux appels consécutifs.
- **`IL2xxx` / `IL3xxx` (trimming / AOT) : 0.**

Ce zéro est mesuré, pas supposé — les analyseurs sont bien actifs sur ce projet :

```
$ dotnet build src/AZERTYGlobal.csproj -c Release -getProperty:EnableAotAnalyzer -getProperty:EnableTrimAnalyzer -getProperty:EnableSingleFileAnalyzer -getProperty:PublishAot
{
  "Properties": {
    "EnableAotAnalyzer": "true",
    "EnableTrimAnalyzer": "true",
    "EnableSingleFileAnalyzer": "true",
    "PublishAot": "true"
  }
}
```

⚠️ **Limite à ne pas franchir dans la lecture** : ces analyseurs sont des analyseurs Roslyn, ils voient le code du projet. Les avertissements émis par **ILC** (la compilation native elle-même, étape `dotnet publish -r win-x64|win-arm64`) ne sont pas dans ce périmètre, et `dotnet publish` était interdit à cette passe. « 0 IL2xxx/IL3xxx au build » ne vaut donc pas « 0 avertissement AOT au publish ».

### 1.4 Release

Contrairement au 2026-09-07 (341 échecs sur 349, `0x800711C7`), **Smart App Control n'a rien bloqué aujourd'hui, ni en Debug ni en Release.** Les trois suites en Release :

```
Passed!  - Failed:     0, Passed:    18, Skipped:     0, Total:    18, Duration: 16 ms - TypingEngine.Core.Tests.dll (net8.0)
Passed!  - Failed:     0, Passed:   157, Skipped:     0, Total:   157, Duration: 20 ms - TypingEngine.Windows.Tests.dll (net8.0)
Passed!  - Failed:     0, Passed:   326, Skipped:     0, Total:   326, Duration: 3 s - AZERTYGlobal.Tests.dll (net8.0)
```

**501 verts en Release aussi**, avec les mêmes trois compteurs.

---

## 2. Carte de couverture par fichier

Méthode : pour chaque fichier de production de `src/*.cs`, `src/TypingEngine.Core/*.cs` et `src/TypingEngine.Windows/*.cs` (hors `bin/`, `obj/`), tous les types déclarés sont extraits, et l'on compte les **fichiers** de test (`src/*Tests*/**/*.cs`) qui nomment au moins un de ces types.

⚠️ Ce que cette mesure ne dit pas : nommer un type n'est pas l'éprouver. Elle borne le **haut**, jamais le bas. Trois distorsions connues, listées pour que personne ne lise la colonne de droite comme une couverture :
- `ForegroundMonitor.cs` sort à 44 parce qu'il déclare aussi `CompatibilityMode` et `CompatibilitySuspendReason`, deux énumérations citées partout ;
- `src/Win32.cs` et `src/TypingEngine.Windows/Win32.cs` déclarent **tous deux** un type `Win32` — leurs 7 sont le même 7, indiscernable ;
- `src/Localization/L.*.cs` sont 16 fichiers d'une seule classe partielle `L` : ils partagent mécaniquement le même 11.

### 2.1 Fichiers de production sans aucun test qui les nomme (en tête, par taille)

| # | fichier | type(s) principal(aux) | lignes |
|---|---|---|---|
| 1 | `src/LessonsWindow.cs` | `LessonsWindow` | 2 714 |
| 2 | `src/SettingsWindow.cs` | `SettingsWindow`, `LayoutInfo` | 2 013 |
| 3 | `src/OnboardingWindow.cs` | `OnboardingWindow` | 1 703 |
| 4 | `src/UsageStatsWindow.cs` | `UsageStatsWindow` | 648 |
| 5 | `src/AboutWindow.cs` | `AboutWindow` | 581 |
| 6 | `src/LayoutConflictWindow.cs` | `LayoutConflictWindow` | 413 |
| 7 | `src/PauseDurationDialog.cs` | `PauseDurationDialog` | 338 |
| 8 | `src/MaintainableLayersWindow.cs` | `MaintainableLayersWindow` | 278 |
| 9 | `src/TypingEngine.Windows/SecureInputDetector.cs` | `SecureInputDetector` | 218 |
| 10 | `src/ToggleNotification.cs` | `ToggleNotification` | 213 |
| 11 | `src/LayerIndicatorWindow.cs` | `LayerIndicatorWindow` | 138 |
| 12 | `src/StoreReview.cs` | `StoreReview` | 99 |
| 13 | `src/ClipboardText.cs` | `ClipboardText` | 67 |
| 14 | `src/GdiImageLoader.cs` | `GdiImageLoader` | 60 |
| 15 | `src/TypingEngine.Windows/IWindowsTypingHost.cs` | `IWindowsTypingHost`, `NullWindowsTypingHost` | 38 |
| 16 | `src/AzertyGlobalWindowsTypingHost.cs` | `AzertyGlobalWindowsTypingHost` | 26 |
| 17 | `src/AssemblyAttributes.cs` | (attributs seuls) | 8 |
| 18 | `src/TypingEngine.Windows/TextEmissionResult.cs` | `TextEmissionResult` (record struct) | 8 |
| 19 | `src/TypingEngine.Windows/AssemblyAttributes.cs` | (attributs seuls) | 4 |
| 20 | `src/GlobalUsings.cs` | (usings seuls) | 3 |
| 21 | `src/TypingEngine.Windows/GlobalUsings.cs` | (usings seuls) | 2 |

**21 fichiers sur 58, soit 9 572 lignes sur 27 653 (34,6 %) du périmètre demandé.**

Deux constats qui ne sont pas de la surface d'UI :

- **`src/StoreReview.cs` — 0 test.** C'est la boîte de notation Store (`StoreReview.TryShow`, ligne 37), c'est-à-dire l'objet même de la 1.3.0. Aucun fichier de test du dépôt ne contient la chaîne `StoreReview`. Ce qui est testé de la notation, ce sont ses **gardes amont** (`ReviewSharePrompt`, `ConfigManager.ReviewPrompt*`, `PolicyManager`), jamais l'appel qui ouvre la boîte.
- **`src/TypingEngine.Windows/SecureInputDetector.cs` — 0 test**, 218 lignes, `public static unsafe class`. C'est la détection de champ sécurisé (vtables COM UIA en pointeurs de fonction non managés). Vérifié par grep : `SecureInputDetector` n'apparaît dans aucune source de test, seulement dans les binaires compilés de `bin/` et `obj/`.

Hors périmètre demandé mais du même tonneau : `src/TypingEngine.Windows/Win32Api/IWin32Api.cs` (54) et `RealWin32Api.cs` (167) sont à 0 eux aussi — c'est attendu pour `RealWin32Api`, dont le double testable est `src/TestSupport/MockWin32Api.cs`.

### 2.2 Le reste du périmètre (1 fichier de test et plus)

| fichier | type(s) | lignes | nb fichiers de test |
|---|---|---|---|
| `src/LearningModule.cs` | `LearningModule` | 3 105 | 1 (`LessonCoreTests.cs`) |
| `src/VirtualKeyboard.cs` | `VirtualKeyboard` | 1 377 | 1 (`TrayUxRegressionTests.cs`) |
| `src/PolicyManager.cs` | `PolicySet`, `PolicyManager` | 329 | 1 |
| `src/AutoStart.cs` | `AutoStart` | 304 | 1 |
| `src/TypingEngine.Windows/KeyboardHook.cs` | `KeyboardHook` | 275 | 1 (`Audit120ResumeTests.cs`) |
| `src/DailyChallenge.cs` | `DailyChallenge` | 256 | 1 |
| `src/ToastActivation.cs` | `ToastActivation` | 218 | 1 |
| `src/GdiHelpers.cs` | `GdiHelpers` | 213 | 1 |
| `src/TypingEngine.Windows/GameRegistry.cs` | `GameRegistry` | 180 | 1 |
| `src/LessonHintProvider.cs` | `LessonHintProvider` | 147 | 1 |
| `src/ProductIdentity.cs` | `ProductIdentity` | 88 | 1 |
| `src/TypingEngine.Windows/InputRecovery.cs` | `InputRecovery` | 70 | 1 |
| `src/ChallengeShare.cs` | `ChallengeShare` | 57 | 1 |
| `src/TypingEngine.Core/LayoutFormatException.cs` | `LayoutFormatException` | 24 | 1 |
| `src/CharacterSearch.cs` | `CharacterSearch` | 1 541 | 2 |
| `src/KeyboardRenderer.cs` | `KeyboardRenderer` | 863 | 2 |
| `src/LessonProgressStore.cs` | `LessonProgressStore` | 400 | 2 |
| `src/LessonCatalog.cs` | `LessonCatalog`, `LessonCatalogLoader` | 265 | 2 |
| `src/TypingEngine.Core/LayoutJsonParser.cs` | `LayoutJsonParser` | 137 | 2 |
| `src/TrainingReminders.cs` | `TrainingReminders` | 129 | 2 |
| `src/ReviewSharePrompt.cs` | `ReviewSharePrompt` | 99 | 2 |
| `src/TextInsertionService.cs` | `TextInsertionService` | 65 | 2 |
| `src/AutoStartNudge.cs` | `AutoStartNudge` | 61 | 2 |
| `src/LessonTypingSession.cs` | `LessonTypingSession` | 346 | 3 |
| `src/TypingEngine.Core/CompositionEngine.cs` | `CompositionEngine` | 55 | 3 |
| `src/TypingEngine.Windows/MaintainableLayerManager.cs` | `MaintainableLayerManager` | 365 | 4 |
| `src/Program.cs` | `Program` | 83 | 4 |
| `src/UsageStats.cs` | `UsageStats` | 565 | 5 |
| `src/AppChannel.cs` | `AppChannel` | 135 | 5 |
| `src/LayoutLoader.cs` | `LayoutLoader` | 15 | 6 |
| `src/Win32.cs` | `Win32` | 905 | 7 ⚠️ homonyme |
| `src/TypingEngine.Windows/Win32.cs` | `Win32` | 188 | 7 ⚠️ homonyme |
| `src/TrayApplication.cs` | `TrayApplication` | 2 409 | 8 |
| `src/TypingEngine.Windows/KeyMapper.cs` | `KeyMapper` | 1 411 | 14 |
| `src/ConfigManager.cs` | `ConfigManager` | 1 031 | 14 |
| `src/TypingEngine.Core/Layout.cs` | `Layout` | 51 | 15 |
| `src/TypingEngine.Windows/ForegroundMonitor.cs` | `ForegroundMonitor` | 319 | 44 ⚠️ gonflé |

Deux fichiers portent 1 seul fichier de test pour plus de 1 300 lignes : `LearningModule.cs` (3 105) et `VirtualKeyboard.cs` (1 377).

---

## 3. Tests qui ne prouvent rien

Passes systématiques d'abord, résultats **négatifs** (c'est-à-dire : sains) :

| Motif cherché | Occurrences |
|---|---|
| `[Fact(Skip = …)]` / `[Theory(Skip = …)]` | **0** |
| `Assert.True(true)`, `Assert.False(false)`, `Assert.Equal(1, 1)` | **0** |
| Tests commentés (`// [Fact]`, `// [Theory]`) | **0** |
| Itération sur les clés d'un seul côté d'une comparaison d'ensembles | **0** (le seul `foreach … .Keys`, `KeyMapperMaintainableLayerCoverageTests.cs:443`, est une recherche dans un helper, pas une comparaison) |

Restent cinq cas réels.

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 1 | `src/AZERTYGlobal.Tests/Placeholder.cs:8` | `public void ProjectCompilesAndXunitRuns() { }` | Test à **corps vide**, zéro assertion. Il est l'un des 326 « passés » d'`AZERTYGlobal.Tests`. Son en-tête l'annonce : « Placeholder pour valider la compilation initiale du projet de tests. Sera remplacé par les vrais tests au fur et à mesure des phases » (lignes 1-2) — le remplacement n'a pas eu lieu. |
| 2 | `src/AZERTYGlobal.Tests/LessonCoreTests.cs:472-479` | `string script = File.ReadAllText(Path.Combine(root, "scripts", "Sync-LayoutResources.ps1"));` puis `Assert.Contains("[switch]$AllowCreate", script);` et `Assert.Contains("src\\lessons.json') -AllowCreate", script);` | Deux `Assert.Contains` sur le **texte source** d'un `.ps1` qui n'est jamais exécuté : le test garde une chaîne de caractères, pas un comportement. Déjà nommé par `.claude/rules/app-repo-guard-blind-spots.md` § « Les trois autres, non traités au 2026-08-18 » ; **toujours présent au HEAD**, inchangé. |
| 3 | `src/AZERTYGlobal.Tests/FrenchTypographyTests.cs:53-54` | `try { value = (string?)method.Invoke(null, args); }` / `catch { /* méthode non évaluable avec des arguments factices : ignorée */ }` | Les deux tests de ce fichier énumèrent par réflexion les membres `string` publics de `L` (mesuré : **395** déclarations `public static string` dans `src/Localization/*.cs`) et n'assertent que sur les violations trouvées (`Assert.True(violations.Count == 0, …)`, l. 73 et 91). **Aucune assertion ne contrôle que l'énumération rend quelque chose** : si `AllFrenchStrings()` rendait zéro élément — un `catch` qui avale tout, un filtre de ligne 51 élargi, un changement de visibilité — les deux tests resteraient verts en ne regardant rien. C'est exactement le motif que la suite Python du même dépôt, elle, garde explicitement (`scripts/tests/test_schema_parser_agreement.py`, `test_le_parseur_lit_bien_quelque_chose` : « Un motif qui ne trouve plus rien passerait tous les autres tests de ce fichier »). L'asymétrie est le constat. |
| 4 | `src/AZERTYGlobal.Tests/ReviewPromptConfigTests.cs:146` | `Assert.Equal(0, ConfigManager.ReviewPromptCount);` | **Assertion sur une valeur par défaut** au sens de la règle du 2026-08-19 : 0 est ce que rend `ReviewPromptCount` quand rien n'est lu. Sa non-vacuité ne tient pas sur ce test mais sur un voisin, `ReviewPromptCount_ExplicitCountWinsOverLegacyFlag` (l. 174-181), qui prouve que le chemin de lecture du fichier est vivant en rendant 2. Le commentaire du test (l. 132-134) affirme la distinction — « cette assertion vaut 0 sous la règle du 2026-09-20 et valait 1 sous la règle de la v1.2.0 » — sans que le témoin de mutation qui l'a établie soit versionné. Même remarque pour `ReviewPromptCount_ResetDoesNotRevivePromptsForAClickedInstall:170`, dont le signal réel est le second `Assert.True(ConfigManager.ReviewPromptClicked)`. |
| 5 | `src/AZERTYGlobal.Tests/LocalizationTests.cs:89-94` | `File.WriteAllText(_configPath, "{\"appLanguage\": \"xx\"}");` … `Assert.Equal("fr", ConfigManager.AppLanguage);` | Même famille : « fr » est la valeur de repli **et** le défaut d'une config absente sur un poste francophone. Un `AppLanguage` devenu totalement inerte rendrait « fr » et passerait. Cas nettement moins grave que le 4 : le test voisin `SetAppLanguage_En_PersistsAndSurvivesReload:71` prouve que la lecture fonctionne. À noter, pas à corriger à l'aveugle. |

### 3.1 Contre-exemples — ce qui, dans ce dépôt, prouve vraiment quelque chose

À citer parce que l'axe « preuves » n'a de sens que différentiel :

- `src/AZERTYGlobal.Tests/ToastActivationTests.cs:88-126` : le test du CLSID ouvre réellement `msix/AppxManifest.xml` (`FindAppxManifest()`, l. 37-48) **et** porte ses deux témoins réciproques sur manifestes fabriqués (`…_ManifesteSansServeurCom_NeTrouveRien`, `…_ClsidDivergent_EstRapporteTelQuel`). C'est le seul garde-fou du dépôt qui porte son propre témoin **dans la suite**, sans script externe.
- `src/AZERTYGlobal.Tests/SoberChannelTests.cs` : chaque entrée de menu est éprouvée dans les deux sens (absente sur AMCF / présente sur Store), et `HorsPackage_GardeLeMenuDAujourdhui:80-84` rougit précisément sur l'élargissement `== Amcf` → `!= Store`.
- `src/AZERTYGlobal.Tests/VersionAlignmentTests.cs` : lit les attributs de l'assembly compilé, donc compare vraiment `src/Program.cs` à `src/Properties/AssemblyInfo.cs`. ⚠️ **Il ne voit ni le csproj ni le manifeste**, et le dit (l. 22-23). Vérifié à la main ce jour : les quatre valent bien 1.3.0 — `src/Program.cs:10` `internal const string Version = "1.3.0";`, `src/Properties/AssemblyInfo.cs:8-12` `1.3.0.0`, `src/AZERTYGlobal.csproj:10` `<Version>1.3.0</Version>`, `msix/AppxManifest.xml:13` `Version="1.3.0.0"`.
- `src/AZERTYGlobal.Tests/ResourceAlignmentTests.cs:46` : `Assert.Equal(characters.EnumerateObject().Count(), root.GetProperty("totalCharacters").GetInt32())` — une **relation** recalculée, pas deux constantes.

⚠️ Compteurs figés, à surveiller sans être un défaut de preuve : `LessonCoreTests.cs:13-17` (`9`, `8`, `34`, `73`, `79`). Ils sont lus depuis la ressource embarquée, donc ils distinguent bien deux états, mais ils tombent à chaque évolution de `src/lessons.json` — c'est ce qui s'est produit et que `5ee46c8` a rattrapé dans `06208ed`.

---

## 4. Témoins de mutation

Sept fichiers portent `witness` dans leur nom. **Aucun n'a été lancé** : les six premiers réécrivent des fichiers de `src/` ou d'`entreprise/`, ce qui est hors du périmètre d'écriture autorisé à cette passe, même avec restauration.

| Témoin | Ce qu'il mute | Ce qu'il attend | Rejouable tel quel ? |
|---|---|---|---|
| `docs/audit-v1.2.0/witness-baseline.py` | Rien (lecture seule) | Taux de recouvrement entre les chaînes `Localization/` de `452aab0^` et le binaire 1.1.0 installé | **Non.** Il lit `C:\Program Files\WindowsApps\AZERTYGlobal.AZERTYGlobal_1.1.0.0_x64__w9kghr08zmhbg\AZERTY Global.exe` (l. 22-25) — vérifié ce jour : **le fichier n'existe pas sur ce poste**. Le paquet 1.1.0 n'y est plus installé. |
| `docs/audit-v1.2.0/witness-lot-b.py` | 6 mutations : `src/AppChannel.cs`, `src/TrayApplication.cs` ×2, `src/ReviewSharePrompt.cs`, `src/UsageStatsWindow.cs`, `src/OnboardingWindow.cs` | « Un test qui reste vert sur la mutation qu'il est censé attraper ne prouve rien » (en-tête). **Mutations 5 et 6 attendues à ZÉRO rouge**, et l'en-tête le dit : « c'est le point : elles documentent l'angle mort du lot B » — les deux liens Discord vivent dans des fenêtres que la suite ne peut pas instancier. | **Oui.** Les **6 ancres sont toutes présentes exactement une fois** au HEAD (vérifié en lecture, en tenant compte des fins de ligne déclarées : LF pour `AppChannel.cs` et `ReviewSharePrompt.cs`, CRLF pour `TrayApplication.cs`). Restauration prouvée : octets gardés en mémoire, réécriture dans un `finally`, contrôle `identique`/`DIVERGENT` imprimé. |
| `docs/audit-v1.2.0/witness-lot-c.py` | 10 mutations : `src/PolicyManager.cs` ×6, `src/ConfigManager.cs`, `src/UsageStats.cs`, `src/SettingsWindow.cs`, `src/TrayApplication.cs` | Mutations **9 et 10 attendues à ZÉRO rouge** (grisage d'un réglage, grisage d'une entrée de menu) : « ils n'ont d'autre preuve que le smoke test du lot G ». | **Oui**, 10/10 ancres présentes. Même mécanisme de restauration. |
| `docs/audit-v1.2.0/witness-lot-d.py` | 4 mutations sur `src/UsageStats.cs` ×3 et `src/UsageStatsWindow.cs` | Mutation **4 attendue à 0 rouge** | ⚠️ **Non, pas en l'état.** La mutation 3 est morte : son ancre `"    internal static bool CollectionEnabled => !AppChannel.CurrentIsSober;"` (l. 41) n'existe plus — `src/UsageStats.cs:426-427` porte désormais `internal static bool CollectionEnabled =>` `PolicyManager.UsageStatsEnabled(PolicyManager.Current.UsageStats, AppChannel.Current);` (réécrit par le lot C, cf. `witness-lot-c.py` mutation 7). **Le script ne s'arrête pas** : il imprime `ECHEC : 0 occurrence(s) de l'ancre` et `continue`, puis `return 0` ; le garde-fou « `CollectionEnabled` est inversée » passerait donc silencieusement de « éprouvé » à « non couvert » pour qui lit la conclusion et non chaque ligne. |
| `docs/audit-v1.2.0/witness-lot-f.py` | 10 mutations sur `entreprise/AZERTYGlobal.admx`, `entreprise/fr-FR/AZERTYGlobal.adml`, `entreprise/en-US/AZERTYGlobal.adml`, `entreprise/politiques-exemple.reg` | « Sortie attendue sur un dépôt sain : dix mutations, dix rouges » ; juge = `python -m unittest discover -s scripts/tests` | ⚠️ **Non de façon concluante aujourd'hui.** Les 4 fichiers existent. Mais son `main()` commence par un contrôle d'état sain qui lève : `raise SystemExit("La suite est déjà rouge avant toute mutation :…\nRien n'a été touché.")` — or la suite Python **est rouge** (§ 5.5). Le témoin s'arrête proprement sans rien toucher, mais il ne rend aucune mesure tant que le point 5.5 n'est pas corrigé. |
| `docs/audit-v1.2.0/witness-lot-f-versions.py` | 10 mutations : 5 sur des documents de parc, 5 sur `scripts/check-doc-versions.py` lui-même | « dix mutations, dix rouges, six fichiers restaurés à l'identique » | ⚠️ **Non.** Les 5 chemins existent tous, y compris les trois **hors dépôt** (`projects/azerty-global/sources/legacy/AZERTY Global/2026/…`, vérifiés présents). Mais son préambule sort en **code 2 « INCONCLUANT »** : `if code != 0: print("INCONCLUANT : le scanner rougit déjà avant toute mutation.")` — et `check-doc-versions.py` rend 1 aujourd'hui (§ 5.2). Second préambule identique sur la suite Python, rouge elle aussi. |
| `scripts/witness-embedded-resources.py` | ⛔ **Ce n'est pas un contrôle statique** : il écrit (`with open(path, "w", encoding="utf-8", newline="")`, l. 86), copie par `shutil.copy2` (l. 112, 119, 132) et lance `dotnet` (l. 91, 104). Il mute les ressources embarquées et attend des rouges. | — | **Non lancé**, et il ne doit pas l'être depuis une passe en lecture seule. Son en-tête porte au passage un avertissement utile : « Restaurer les octets ne suffit pas. `shutil.copy2` rend aussi la date d'origine » — la date de modification fait partie de l'état à restaurer, parce que MSBuild s'en sert. |

### 4.1 Angle mort commun aux trois témoins `.cs` (lots B, C, D)

Leur juge est le même :

```python
def run_suite():
    out = subprocess.run(
        ["dotnet", "test", "src/AZERTYGlobal.Tests", "--nologo", "-v", "minimal"], …
```
`docs/audit-v1.2.0/witness-lot-b.py:79-82` (texte identique dans `-c.py` et `-d.py`).

Ils ne lancent que **`AZERTYGlobal.Tests` (326 tests)**, jamais `TypingEngine.Core.Tests` ni `TypingEngine.Windows.Tests` (**175 tests, 35 % de la suite**). Une mutation qui ne ferait rougir qu'un test des moteurs serait rapportée « 0 rouge », c'est-à-dire comme un angle mort — avec la bonne conclusion et la mauvaise preuve, motif exactement décrit au § « Un test qui plante ne rougit pas là où il faut » de `app-repo-guard-blind-spots.md`.

---

## 5. Contrôles statiques

Code lu avant exécution pour chacun. Les quatre premiers n'écrivent rien et ne lancent aucun sous-processus (grep sur `write_text|write_bytes|open(...,"w")|shutil|os.remove|unlink|mkdir|subprocess` : zéro occurrence dans les quatre). `scripts/witness-embedded-resources.py`, lui, écrit — il est traité au § 4 et n'a pas été lancé.

### 5.1 `scripts/check-layout-provenance.py` — exit 0

```
IDENTIQUE  src/AZERTY Global 2026.json  5f79f9e04232393c  33494 o
IDENTIQUE  src/character-index.json  b2eba45490e0e81a  605678 o
IDENTIQUE  src/lessons.json  a53bd3469ee31343  36635 o

Les 3 copies sont identiques à leur original canonique.
```

✅ Le port `66602db` (module « course aux 30 millions » repris du site) a bien refermé la dérive. ⚠️ Le script lit par le réseau sur `https://raw.githubusercontent.com/AZERTYGlobal/website/main/` (l. 36) — l'ancien compte personnel, joignable par redirection ; ce n'est pas `AMCF-asso`.

### 5.2 `scripts/check-doc-versions.py` — **exit 1, 9 erreurs**

```
Version de référence (csproj) : 1.3.0

### components/microsoft-store/Distribution Entreprises.md  (2 anomalie(s))
  ERREUR  bloc   VERSION-DECLAREE-PERIMEE version-app annonce « 1.2.0 » quand le csproj est en 1.3.0
  ERREUR  L18    VERSION-CORPS-INCONNUE   version 1.2.0 ni courante, ni en bascule, ni déclarée historique

### components/microsoft-store/entreprise/Note RGPD - Établissements.md  (1 anomalie(s))
  ERREUR  bloc   VERSION-DECLAREE-PERIMEE version-app annonce « 1.2.0 » quand le csproj est en 1.3.0

### sources/legacy/AZERTY Global/2026/Microsoft Store/Distribution Entreprises.md  (8 anomalie(s))
  ERREUR  bloc   VERSION-DECLAREE-PERIMEE version-app annonce « 1.2.0 » quand le csproj est en 1.3.0
  ERREUR  L22    VERSION-CORPS-INCONNUE   version 1.2.0 ni courante, ni en bascule, ni déclarée historique
  ATTENTE L24    VERSION-EN-BASCULE       version 1.1.0.0 déclarée remplaçable par la bascule du kit
  ATTENTE L24    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE L25    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE L26    EMPREINTE-NON-DECLAREE   empreinte 79A9C9C80CE9… absente de empreintes-attendues
  ATTENTE L36    EMPREINTE-NON-DECLAREE   empreinte 1B040DE6AE43… absente de empreintes-attendues
  ATTENTE bloc   EMPREINTE-EN-ATTENTE     empreintes-attendues vaut en-attente : document non publiable en l'état

### sources/legacy/AZERTY Global/2026/Pilotes/Note informatique.md  (8 anomalie(s))
  ERREUR  bloc   VERSION-DECLAREE-PERIMEE version-app annonce « 1.2.0 » quand le csproj est en 1.3.0
  ATTENTE L84    EMPREINTE-NON-DECLAREE   empreinte 1B040DE6AE43… absente de empreintes-attendues
  ATTENTE L85    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE L86    EMPREINTE-NON-DECLAREE   empreinte 79A9C9C80CE9… absente de empreintes-attendues
  ATTENTE L89    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE L91    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE L93    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE bloc   EMPREINTE-EN-ATTENTE     empreintes-attendues vaut en-attente : document non publiable en l'état

### sources/legacy/AZERTY Global/2026/Fichiers d'installation/Application AZERTY Global (Windows Store-MSIX)/LISEZMOI-DSI.md  (14 anomalie(s))
  ERREUR  bloc   VERSION-DECLAREE-PERIMEE version-app annonce « 1.2.0 » quand le csproj est en 1.3.0
  ATTENTE L23    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE L24    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE L32    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE L33    VERSION-EN-BASCULE       version 1.1.0.0 déclarée remplaçable par la bascule du kit
  ATTENTE L34    EMPREINTE-NON-DECLAREE   empreinte 79A9C9C80CE9… absente de empreintes-attendues
  ATTENTE L48    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE L55    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE L62    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE L73    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE L79    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE L88    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE L96    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE bloc   EMPREINTE-EN-ATTENTE     empreintes-attendues vaut en-attente : document non publiable en l'état

### sources/legacy/AZERTY Global/2026/Fichiers d'installation/Application AZERTY Global (Windows Store-MSIX)/LISEZ-MOI.md  (2 anomalie(s))
  ERREUR  bloc   VERSION-DECLAREE-PERIMEE version-app annonce « 1.2.0 » quand le csproj est en 1.3.0
  ATTENTE L33    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py

### sources/legacy/AZERTY Global/2026/Fichiers d'installation/Application AZERTY Global (Windows Store-MSIX)/SIGNATURE.md  (6 anomalie(s))
  ERREUR  bloc   VERSION-DECLAREE-PERIMEE version-app annonce « 1.2.0 » quand le csproj est en 1.3.0
  ATTENTE L12    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE L17    EMPREINTE-NON-DECLAREE   empreinte 79A9C9C80CE9… absente de empreintes-attendues
  ATTENTE L23    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE L29    NOM-BUNDLE-PERIME        le kit nomme encore le bundle 1.1.0 ; bascule par update_kit_version.py
  ATTENTE bloc   EMPREINTE-EN-ATTENTE     empreintes-attendues vaut en-attente : document non publiable en l'état

9 erreur(s), 32 attente(s) de bascule.
```

Constat : **sept documents de parc déclarent encore `version-app: 1.2.0` alors que le csproj est passé à 1.3.0.** Les 32 « attentes » sont normales avant la bascule du kit signé ; les 9 « erreurs » ne le sont pas — le scanner est écrit pour qu'elles soient corrigeables aujourd'hui, sans bundle.

### 5.3 `scripts/list-identity-literals.py` — exit 0

```
### ProductIdentity.cs  (10)
     24  public const string DisplayName = "AZERTY Global";
     33  public const string ExecutableName = "AZERTY Global.exe";
     36  public const string ShortcutFileName = "AZERTY Global.lnk";
     41  public const string ConfigFolderName = "AZERTY Global";
     43  public const string StoreProductId = "9N4BTS43SSSZ";
     46  public const string SiteDomain = "azerty.global";
     48  public const string DiscordInviteUrl = "https://discord.gg/nYknqshJz3";
     49  public const string RepositoryUrl = "https://github.com/AMCF-asso/azerty-global-app";
     50  public const string LogoResourceName = "favicon-azerty-global.png";
     71  public const string StorePackageFamilyName = "AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg";

Littéraux hors ProductIdentity : 0
```

✅ Le port `fb0ca3b` est visible : `RepositoryUrl` pointe bien `AMCF-asso/azerty-global-app`.

⛔ **`SKIP_DIRS`, citation littérale — `scripts/list-identity-literals.py:49-53`** :

```python
SKIP_DIRS = {
    "AZERTYGlobal.Tests", "TypingEngine.Core",
    "TypingEngine.Core.Tests", "TypingEngine.Windows",
    "TypingEngine.Windows.Tests", "TestSupport", "bin", "obj",
}
```

Cette liste exclut **deux dossiers de production** : `TypingEngine.Core` (4 fichiers, 263 lignes) et `TypingEngine.Windows` (14 fichiers, 3 285 lignes), qui sont du code livré depuis `452aab0`. Son exit 0 signifie « rien dans les dossiers que je lis », jamais « rien dans le dépôt ».

Vérifié ce jour que l'angle mort est encore **actif et occupé** : `src/AZERTYGlobal.Tests/ToastActivationTests.cs:26` porte `Assert.Equal("Vous aimez AZERTY Global ?", ToastActivation.EscapeXml("Vous aimez AZERTY Global ?"));` (deux occurrences du motif sur une ligne) et `:69` porte `"<com:ExeServer Executable=\"AZERTY Global.exe\">"`. Trois occurrences du propre motif du scanner, dans un dossier qu'il ne lit pas.

### 5.4 `scripts/validate-layout.py` — exit 0

Aucun argument attendu : l'en-tête du script donne son usage, `python scripts/validate-layout.py`, et il résout lui-même le chemin de la disposition.

```
Schéma : conforme
Compteurs : conforme
Références : conforme

AZERTY Global 2026.json est conforme au schéma, à ses compteurs et à ses références.
```

### 5.5 `python -m unittest discover -s scripts/tests -v` — **exit 1, 116 tests, 1 échec**

```
Ran 116 tests in 3.380s

FAILED (failures=1)
```

```
======================================================================
FAIL: test_aucune_erreur_dans_les_documents_de_parc (test_check_doc_versions.DocumentsReelsTests.test_aucune_erreur_dans_les_documents_de_parc)
----------------------------------------------------------------------
Traceback (most recent call last):
  File "D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store\scripts\tests\test_check_doc_versions.py", line 223, in test_aucune_erreur_dans_les_documents_de_parc
    self.assertEqual(erreurs, [], f"{chemin.name} : {erreurs}")
    ~~~~~~~~~~~~~~~~^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
AssertionError: Lists differ: [ERREUR VERSION-DECLAREE-PERIMEE L0 versio[148 chars]ique] != []

First list contains 2 additional elements.
First extra element 0:
ERREUR VERSION-DECLAREE-PERIMEE L0 version-app annonce « 1.2.0 » quand le csproj est en 1.3.0

+ []
- [ERREUR VERSION-DECLAREE-PERIMEE L0 version-app annonce « 1.2.0 » quand le csproj est en 1.3.0,
-  ERREUR VERSION-CORPS-INCONNUE L18 version 1.2.0 ni courante, ni en bascule, ni déclarée historique] : Collection Entreprises.md
```
*(dernière ligne tronquée ici, le nom réel imprimé est `Distribution Entreprises.md`)*

Les 115 autres passent, dont les témoins d'accord schéma / parseur et les 5 témoins de désaccord de compteurs de `validate-layout`.

### 5.6 Conséquence CI — mesurée, pas déduite

`.github/workflows/ci.yml` du HEAD contient, dans le job `build`, **avant** le moindre `dotnet` :

```yaml
      - name: Témoins du schéma et de son accord avec le parseur
        run: python -m unittest discover -s scripts/tests -v
```

Cette étape rend 1 aujourd'hui. Le job `build` — et donc les publish x64/ARM64, les trois `dotnet test -c Release`, `Pack-MSIX.ps1`, `Verify-Release.ps1`, BinSkim, l'attestation SLSA et l'upload du bundle — **ne peut pas être atteint sur cette branche en l'état**. Le déclencheur du workflow couvre bien `release/**` (`on: push: branches: [main, release/**]`).

⚠️ Pour mémoire : `check-doc-versions.py` **n'a aucune étape à lui** dans `ci.yml`. C'est son test qui le fait entrer en CI, donc corriger l'un corrige l'autre, mais un lecteur du seul workflow ne verrait pas le lien.

---

## 6. Tests des correctifs 1.3.0

### 6.1 Les trois témoins d'`IsSnapshotStale` (écart 7 — Alt+Tab)

`src/TypingEngine.Windows.Tests/ForegroundMonitorMockedTests.cs`, ajoutés par `404d68a` :

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 1 | `:171-177` | `public void IsSnapshotStale_ForegroundUnchanged_IsFalse()` … `Assert.False(fm.IsSnapshotStale);` | Cas de repos. Seul, c'est une assertion sur la valeur par défaut. |
| 2 | `:180-191` | `mock.ForegroundWindow = (IntPtr)0x5678;` puis `Assert.True(fm.IsSnapshotStale);`, précédé d'un `Assert.False` l. 184 | **C'est le témoin réel** : le même objet rend `false` puis `true` sur le seul changement du premier plan. Les deux états se distinguent dans un seul test. |
| 3 | `:194-205` | `fm.Recompute();` puis `Assert.False(fm.IsSnapshotStale);` | Réciproque : le chien de garde répare. |

✅ Le trio est bien formé au sens de la règle du dépôt : chaque assertion a sa contre-assertion dans le même test, la valeur par défaut n'est jamais le seul signal. Le commentaire l. 163-168 relie explicitement le témoin à l'écart.
✅ La garde côté production existe : `src/TypingEngine.Windows/ForegroundMonitor.cs:117-123` (`snap != null && snap.Window != _api.GetForegroundWindow()`).
⚠️ Ce qui n'est **pas** couvert : le câblage. `src/TrayApplication.cs:826-832` (`else if (timerId == TIMER_FOREGROUND_WATCHDOG) { if (_foregroundMonitor?.IsSnapshotStale == true) _foregroundMonitor.Recompute(); }`) et `src/TrayApplication.cs:540` (`Win32.SetTimer(_hWnd, (UIntPtr)TIMER_FOREGROUND_WATCHDOG, FOREGROUND_WATCHDOG_INTERVAL_MS, IntPtr.Zero)`) ne sont nommés par **aucun test** — grep de `TIMER_FOREGROUND_WATCHDOG` dans les trois projets de test : 0. La propriété est prouvée, son branchement à la boucle de messages ne l'est pas. La preuve reste la recette VM.

### 6.2 `reviewPromptDone` (d356841)

`src/AZERTYGlobal.Tests/ReviewPromptConfigTests.cs`, 12 `[Fact]` :

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 1 | `:141-155` | `ReviewPromptCount_LegacyDoneFlagNoLongerConsumesAnAttempt` — `File.WriteAllText(_configPath, "{\"reviewPromptDone\": true}");` … `Assert.Equal(0, ConfigManager.ReviewPromptCount);` puis trois `RecordReviewPromptShown()` vérifiant `1`, `2`, `2` | Le témoin annoncé par le commit. Le `Assert.Equal(0, …)` est une valeur par défaut (§ 3, cas 4) ; ce qui sauve le test, c'est la suite — le plafond dur à 2 est éprouvé dans le même corps, et donc le chemin d'écriture aussi. |
| 2 | `:163-172` | `ReviewPromptCount_ResetDoesNotRevivePromptsForAClickedInstall` — `"{\"reviewPromptDone\": true, \"reviewPromptClicked\": true}"` … `Assert.True(ConfigManager.ReviewPromptClicked);` | Prouve que `reviewPromptClicked` est toujours lu, c'est-à-dire que le rattrapage est borné. |
| 3 | `:174-181` | `ReviewPromptCount_ExplicitCountWinsOverLegacyFlag` — `"{\"reviewPromptDone\": true, \"reviewPromptCount\": 2}"` … `Assert.Equal(2, …)` | **C'est lui qui rend le cas 1 non vide** : il prouve que `ReviewPromptCount` lit réellement le fichier. Sans ce voisin, un `ReviewPromptCount` inerte rendrait 0 et passerait le témoin principal. |

⚠️ Le témoin de mutation qui a établi « remettre l'ancienne ligne rougit ce test et lui seul » n'est **pas versionné** : il n'existe pas de `witness-*` pour la 1.3.0. L'affirmation vit dans un commentaire (`:132-134`) et dans le message de commit, pas dans un script rejouable. C'est la seule différence de régime entre les correctifs 1.3.0 et ceux du lot B/C/D de la 1.2.0.

### 6.3 `ShortcutsWhilePassThrough` (écart 8 — Ctrl+Maj+W)

**Aucun test trouvé.** Grep de `ShortcutsWhilePassThrough` sur tout `src/` :

```
src/TrayApplication.cs:509:        _hook.ShortcutsWhilePassThrough =
src/TypingEngine.Windows/KeyboardHook.cs:80:    public bool ShortcutsWhilePassThrough { get; set; }
src/TypingEngine.Windows/KeyboardHook.cs:171:            if (_passThroughAll && !ShortcutsWhilePassThrough)
```

Trois sites, tous en production, zéro dans les projets de test. `src/TypingEngine.Windows.Tests/KeyMapperPassThroughLayoutTests.cs` ne couvre **pas** ce chemin : il porte sur le laissez-passer d'une frappe dont la sortie native coïncide avec la disposition, pas sur `_passThroughAll`.

Le seul test voisin est `src/AZERTYGlobal.Tests/QuickWinsTests.cs:28-39`, `UserOverride_IsNotClassifiedAsSecuritySuspension`, qui éprouve `TrayApplication.IsSecuritySuspension(CompatibilitySuspendReason.UserOverride)` par réflexion. Il est **antérieur** — `404d68a` ne touche aucun test de `AZERTYGlobal.Tests` — et il ne couvre pas le prédicat réel du correctif, qui est `src/TrayApplication.cs:507-510` :

```csharp
bool userChosenSuspension = _suspendedForCompatibility
    && _foregroundMonitor?.CurrentSuspendReason == CompatibilitySuspendReason.UserOverride;
_hook.ShortcutsWhilePassThrough =
    (IsPaused && !_suspendedForCompatibility) || userChosenSuspension;
```

### 6.4 Fenêtre Paramètres (écart 4) — **les correctifs ne sont pas testés**

Réponse directe à la question posée : **non.**

| symbole | sites en production | sites en test |
|---|---|---|
| `FitWindowToContent` | `src/SettingsWindow.cs:298, 313, 403 (commentaire), 576, 1080, 1355` | **0** |
| `ShiftLayout` | `src/SettingsWindow.cs:137, 910` | **0** |
| `SettingsWindow` (le type entier) | 2 013 lignes | **0** (cf. § 2.1, ligne 2) |

`404d68a` ajoute 227 lignes à `src/SettingsWindow.cs` et **zéro ligne de test**. Les 44 lignes de test du commit vont toutes à `ForegroundMonitorMockedTests.cs`. Le message du commit l'assume implicitement en ne parlant de témoins que pour `IsSnapshotStale`. Le `Changelog.md` porte la même réserve pour le menu : « ⚠️ Aucun test ne verrouille la structure du menu — `ShowContextMenu` appelle Win32 directement. La vérification est visuelle, en VM. »

### 6.5 Les quatre ports de `06208ed`

| port | ce qu'il corrige | test qui le couvre |
|---|---|---|
| `fb0ca3b` | `RepositoryUrl` → `AMCF-asso` (`src/ProductIdentity.cs`) | ✅ Indirectement : `scripts/list-identity-literals.py` imprime la ligne 49 et exige que ce littéral soit **dans** `ProductIdentity.cs`. Aucun test C# n'assert sa valeur. |
| `1b025d0` | La priorité de la sollicitation d'avis sur le rappel du Défi du jour survit au redémarrage (`src/TrainingReminders.cs`, `src/TrayApplication.cs`) | ✅ `src/AZERTYGlobal.Tests/DailyChallengeTests.cs` (+100 lignes dans ce commit) et `src/AZERTYGlobal.Tests/AutoStartNudgeTests.cs` nomment tous deux `TrainingReminders`. C'est le seul des quatre ports qui arrive avec ses tests. |
| `66602db` | `src/lessons.json` reprend le module « course aux 30 millions » | ✅ `scripts/check-layout-provenance.py` — rejoué ce jour, exit 0, les trois copies identiques (§ 5.1). |
| `5ee46c8` | Compteurs figés de `LessonCoreTests` suivant le nouveau catalogue | ✅ C'est lui-même un test : `src/AZERTYGlobal.Tests/LessonCoreTests.cs:13-17`. ⚠️ Motif fragile par construction (cf. § 3.1). |

---

## 7. Non vérifié

| Ce qui n'a pas été fait | Pourquoi / erreur exacte |
|---|---|
| Avertissements **ILC** de la compilation native (`IL2xxx`/`IL3xxx` émis au lien AOT) | `dotnet publish` interdit à cette passe. Le zéro du § 1.3 ne porte que sur les analyseurs Roslyn. Le seul chemin qui les rendrait est `dotnet publish src/AZERTYGlobal.csproj -c Release -r win-x64` puis `-r win-arm64`. |
| Couverture de ligne réelle (`dotnet test --collect:"XPlat Code Coverage"` / coverlet) | Non lancé : produirait des `TestResults/` (toléré) mais le § 2 demandait une carte par fichier, pas un taux. Le nombre de fichiers de test nommant un type **majore** la couverture, il ne la mesure pas. |
| Les six témoins de mutation `docs/audit-v1.2.0/witness-*.py` et `scripts/witness-embedded-resources.py` | Tous écrivent sous `src/` ou `entreprise/`, hors périmètre d'écriture. Analyse statique d'ancres seulement (§ 4). |
| `docs/audit-v1.2.0/witness-baseline.py` | Impossible même sans interdit d'écriture : `ls: cannot access 'C:/Program Files/WindowsApps/AZERTYGlobal.AZERTYGlobal_1.1.0.0_x64__w9kghr08zmhbg/AZERTY Global.exe': No such file or directory`. Le paquet 1.1.0 n'est plus installé sur ce poste. |
| Rejeu effectif des lots F et F-versions | Leurs deux préambules refusent de muter tant que la suite Python est rouge — exactement le comportement attendu, mais cela veut dire qu'**aucune mesure de mutation du lot F n'est disponible** avant correction du § 5.2/5.5. |
| Structure du menu de la zone de notification (12 lignes, 4 blocs, sous-menu Couches ▸) | Aucun test n'existe ; `ShowContextMenu` appelle Win32 directement. Recette VM seulement, le `Changelog.md` le dit. |
| Comportement réel du chien de garde de 250 ms sur un vrai Alt+Tab | Non instrumentable depuis la suite (boucle de messages Win32). `FOREGROUND_WATCHDOG_INTERVAL_MS` et `TIMER_FOREGROUND_WATCHDOG` ne sont nommés par aucun test. |
| `Verify-Release.ps1` et `Pack-MSIX.ps1` | Non lancés : `Pack-MSIX.ps1` empaquette et écrit dans `msix/`, `Verify-Release.ps1` suppose un publish préalable. |

---

*Rapport du 2026-09-20 — axe « tests et preuves » de l'audit v1.3.0.*
