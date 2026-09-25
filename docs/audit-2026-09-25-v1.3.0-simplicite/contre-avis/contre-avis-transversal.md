# Contre-avis — zone transversale (X-01, X-03, X-04, X-05, X-08)

Relecture adverse, en lecture seule, du dépôt `microsoft-store` au commit `f0a98ba`. Les scripts sont dans `audit/contre-avis-transversal/` : `tirage.py`, `balayage69.py`, `pinceaux.py`, `pinceau_classe.py`, `catch_recompte.py`, `wc.py` et `x08c.py`. Le relecteur appartient à la même famille de modèles que l'auteur. Pour X-01 et X-04, un second avis d'un autre fournisseur reste utile.

## X-01 — Squelette de fenêtre recopié

**Verdict : NUANCÉ**

- La duplication est confirmée par recomptage. `RegisterClassExW` apparaît dans 14 fichiers et `UnregisterClassW` dans 13 (TrayApplication n'en a pas). On compte aussi `GetDeviceCaps(…, 88)` dans 8 fichiers, `WM_DPICHANGED` dans 12, `EnableDarkTitleBar` dans 10, `GdiplusStartup` dans 4 et `LinkSubclassProc` dans 4.
- **Erreur de calcul.** Les familles 1 à 6 donnent 171 + 98 + 92 + 50 + 28 + 24 = **463** lignes, et non 413. Le chiffre 413 correspond au total sans la famille 4 (`LinkSubclassProc`). Sortie de `outils/familles.py A 6`.
- **La divergence ne cache pas de plantage.** Presque toutes les fenêtres peuvent s'ouvrir deux fois : About et UsageStats (`TrayApplication.cs:849-861`), Lessons (`:1400-1405`), CharacterSearch et LayerIndicator (`:397-429`), LayoutConflict (`:1214-1215`), LearningModule (`OnboardingWindow.cs:1171-1173`). Les 8 fenêtres qui mettent un pinceau dans la classe appellent toutes `UnregisterClassW` après `DestroyWindow`, par exemple About (`AboutWindow.cs:590` puis `614`) et Settings (`2299` puis `2320`). La classe disparaît donc avec la fenêtre.
- **Expérience hors app** (`pinceau_classe.py`, sans montrer de fenêtre). J'ai recréé une fenêtre sur une classe restée enregistrée dont le pinceau avait été supprimé, puis forcé l'effacement du fond : aucun plantage. [CONFIRMÉ]
- **Le vrai vecteur de plantage est ailleurs.** Quand la classe survit, un second `RegisterClassExW` échoue avec l'erreur 1410, et l'app ne teste jamais ce retour. La nouvelle fenêtre tourne alors sur le `lpfnWndProc` de l'ancienne instance, dont le délégué peut avoir été collecté. Le commentaire de `LearningModule.cs:816-817` le dit lui-même.
- **Défaut latent réel.** La documentation Microsoft sur WNDCLASSEXW dit : « The system automatically deletes class background brushes when the class is unregistered… An application should not delete these brushes. » L'expérience le confirme : après `UnregisterClassW`, `GetObjectType` rend 0. Or les 8 fenêtres suppriment elles-mêmes ce pinceau, donc il est supprimé deux fois à chaque `Dispose` : `AboutWindow.cs:600`, `MaintainableLayersWindow.cs:364`, `LessonsWindow.cs:342` (dans `DestroyFonts`). CharacterSearch le supprime même avant `DestroyWindow` (`CharacterSearch.cs:1589`, puis `1592`). C'est sans effet aujourd'hui.
- **Correction.** Remplacer 413 par 463. Retirer l'idée d'un « crash à la 2e instance » pour les 8 fenêtres. La base NativeWindow doit fixer une seule règle : `hbrBackground = IntPtr.Zero` avec un pinceau d'instance, ce qui tient même si `UnregisterClassW` échoue. Elle doit aussi vérifier le retour de `RegisterClassExW`.

## X-03 — 69 membres morts

**Verdict : CONFIRMÉ**

- **Tirage.** 15 membres tirés au hasard (graine 20260925) parmi les 49 qui n'avaient pas été vérifiés à la main : `BASE_LINK_BANNER_W`, `CLR_BANNER_BORDER`, `CLR_BANNER_TEXT`, `ARGB_STEP_CIRCLE`, `ARGB_WHITE`, `CurrentFullPath`, `TTM_TRACKPOSITION`, `TCM_ADJUSTRECT`, `WM_SYSCOMMAND`, `DT_BOTTOM`, `Rectangle`, `GdipGetImageHeight`, `GdipFillEllipseI`, `GdipCreateFont`, `GdipDeleteFont`. `git grep -w` sur tout l'arbre, docs comprises, ne trouve rien d'autre que la déclaration. **Taux de faux positifs : 0/15.**
- **Balayage des 69.** Aucun n'est utilisé en production ni dans les tests. Quatre noms apparaissent seulement dans `docs/` ou le Changelog.
- **Usages cachés cherchés, aucun trouvé.**
  - Les réflexions des tests passent des noms littéraux, que le compteur voit bien : `Audit120InsertionTests.cs:62`, `LessonCoreTests.cs:592-604`, `QuickWinsTests.cs`.
  - Aucun `nameof`, `DynamicDependency` ou `UnmanagedCallersOnly` ne vise ces membres.
  - Aucun `JsonSerializer` en production : la config passe par des clés écrites à la main, comme `"maintainableTutorialCompleted"` en camelCase.
- **Précisions, sans effet sur le verdict.**
  - `AppChannel.CurrentIsSober` sert à la mutation 7 du témoin `docs/audit-v1.2.0/witness-lot-c.py:79`. Si on le supprime, ce mutant ne compile plus et devient un « rouge » de compilation : il faut réécrire la mutation avec `AppChannel.IsSober(AppChannel.Current)`.
  - On compte 14 P/Invoke GDI+, et non 13, et 15 constantes dans OnboardingWindow, et non 14.
  - Le getter `MaintainableTutorialCompleted` est mort, mais son setter écrit encore la clé (`MaintainableLayersWindow.cs:241`). Personne ne la relit.

## X-04 — catch muets et repli « Windows 8.1 »

**Verdict : NUANCÉ**

- **Le repli est bien inatteignable.** `MinVersion="10.0.17763.0"` figure dans `msix/AppxManifest.xml:24`. `Pack-MSIX.ps1:130-150` utilise ce seul modèle de manifeste, pour le Store comme pour le bundle AMCF. L'assembly déclare `SupportedOSPlatform("Windows10.0.17763.0")` (`AssemblyInfo.cs:14`).
- **`entreprise/` n'est pas un canal.** Le dossier contient l'ADMX, les ADML, un `.reg` et une note RGPD ; les établissements déploient le MSIX AMCF.
- **Le mode hors package existe à l'exécution** (`AppChannel.cs:31`), mais aucun script du dépôt ne produit de version hors package. La note RGPD cite pourtant un « installeur classique » (`entreprise/Note RGPD - Établissements.md:36`) qui n'existe pas ici. Le composant `windows-installer` installe la disposition, pas l'app. À corriger dans la note.
- **Seul cas où le repli servirait.** .NET 8 prend en charge Windows Server 2012 R2 (avec ESU), et un exe hors package y lèverait `EntryPointNotFoundException`. Mais le repli est déjà incohérent : **4** appels, et non 3, se font sans try, ce qui ferait planter l'app de toute façon (`LessonsWindow.cs:506`, `LayerIndicatorWindow.cs:47`, `MaintainableLayersWindow.cs:120`, `PauseDurationDialog.cs:139`). Les autres appels visés rendent un code d'erreur et ne lèvent jamais (`Win32.cs:542`, `919`, `922`). [CONFIRMÉ]
- **Le risque est mal décrit.** L'exception ne remonte au catch journalisé de `WndProcCallback` que pour About et UsageStats (`TrayApplication.cs:852`, `861`). Partout, elle sort du constructeur après `CreateWindowExW`. L'instance est perdue, mais le HWND et la classe restent, avec un délégué collectable. Le `new` suivant retombe alors sur le cas 1410 décrit en X-01. De plus, pour LayoutConflict, `_layoutPopupOpen` reste à `true` (`TrayApplication.cs:1212`) : la fenêtre ne réapparaît plus. [PLAUSIBLE, surface faible]
- **Correction.** Garder la proposition, qui passe à `GetDpiForWindowOrDefault`. Réécrire le risque. Prévoir un nettoyage en cas d'échec du constructeur (`DestroyWindow` puis `UnregisterClassW`) dans la future base.

## X-05 — catch attrape-tout

**Verdict : NUANCÉ**

- **Les chiffres tiennent.** Recompte avec le lexer de l'audit (`catch_recompte.py`) : 112 catch, dont 54 `catch (Exception)` et 41 catch nus, soit 95 attrape-tout ; 16 sont filtrés par `when` et 1 est typé. L'écart avec l'audit est de ±1.
- **6 catch « non défendables » vérifiés, dont un seul fait vraiment perdre de l'information :**
  - `AutoStart.cs:340` : c'est un catch imbriqué dans un catch qui journalise déjà l'exception d'origine (`:331`). **Aucune perte.**
  - `AutoStart.cs:400` : il entoure un P/Invoke qui rend un code (`RegGetValueBinary`) et ne peut pas se déclencher. Il relève de X-04. **Aucune perte.**
  - `TrayApplication.cs:1255` : `Save` journalise déjà les erreurs IO et d'accès (`ConfigManager.cs:1103-1106`). Le même appel se fait sans try en `:1441`. Seules des exceptions exotiques seraient perdues.
  - `SecureInputProbe.cs:74` : le seul abonné poste un message (`TrayApplication.cs:2066-2070`) et ne peut pas lever. `SecureInputDetector` est une classe statique du moteur, sans hôte, et n'a pas accès à `ConfigManager.Log` : la règle proposée ne s'y applique pas. Ce catch protège le worker de saisie sécurisée, un garde-fou selon la grille.
  - `AutoStart.cs:352` et `:370` : **perte réelle mais faible.** La lecture d'état retombe sur `false` ou `Unknown` sans trace. La même cause est journalisée quand l'utilisateur agit (`:331`), et elle est passagère pendant une mise à jour MSIX (`:307-312`).
- **Correction.** Reclasser 340, 400, 1255 et 74 comme défendables ou redondants. Ne garder qu'AutoStart 352/370, à journaliser une fois par session. Dans `TypingEngine.*`, écrire la règle via `IWindowsTypingHost.Log`, pas via `ConfigManager.Log`.

## X-08 — Concentration du code

**Verdict : CONFIRMÉ**

- **Recompte `wc -l` au commit** (`wc.py`) : 87 fichiers de production pour 32 476 lignes et 12 469 lignes de tests, identiques aux chiffres de l'audit.
- **Les cinq fichiers font 13 444 lignes, soit 41,4 %** : 3 245 + 3 076 + 2 874 + 2 322 + 1 927. L'audit compte les portées de classe, 13 403 lignes et 41,3 %, ce qui explique le léger écart.
- **En lignes de code seules, la part monte à 43,0 %** (10 138 sur 23 587) : le titre « 41 % du code » sous-estime même un peu.
- **Les étendues des méthodes citées concordent** : `WndProcCallback` 681-1049 = 369 lignes, `SettingsWindow.WndProc` 1263-1542 = 280, `ProcessKeyCore` 519-838 = 320.

## Récapitulatif

| Id | Verdict | Correction |
|---|---|---|
| X-01 | NUANCÉ | Remplacer 413 par 463. Aucun plantage pour les 8 fenêtres, car la classe est désinscrite. Défaut réel : pinceau de classe supprimé deux fois (doc MS, expérience). Vrai vecteur : retour de `RegisterClassExW` non testé et délégué périmé. |
| X-03 | CONFIRMÉ | 0/15 faux positif dans le tirage, 0/69 en production et en tests. Réécrire la mutation 7 de `witness-lot-c.py` si `CurrentIsSober` disparaît. 14 GDI+, 15 constantes Onboarding. |
| X-04 | NUANCÉ | Repli bien inatteignable (MinVersion 17763 pour tous les MSIX ; `entreprise/` = ADMX, pas un canal ; 4 appels déjà sans try). Réécrire le risque : l'instance est perdue après `CreateWindowExW`. |
| X-05 | NUANCÉ | Chiffres justes. Sur 6 catch « non défendables », 1 seul fait vraiment perdre de l'information (AutoStart 352/370). 340, 400, 1255 et 74 sont redondants ou défendables. Le moteur ne peut pas appeler `ConfigManager.Log`. |
| X-08 | CONFIRMÉ | Aucune : `wc -l` donne 41,4 %, et 43,0 % en lignes de code. |

**Non vérifié.**
- Je n'ai pas lancé l'app, conformément à la grille. L'expérience sur le pinceau passe par Python/ctypes : le comportement vient de user32/gdi32 et ne dépend pas du runtime, mais elle n'a pas été rejouée en NativeAOT.
- Je n'ai pas tracé le chemin d'appel de LayoutConflict au démarrage.
- Je n'ai pas cherché hors de ce dépôt l'« installeur classique » cité par la note RGPD.