# Revue de code v1.3.0 en remplacement de la recette VM — 2026-09-21

- **Demande d'Antoine** : ne pas rejouer les 55 gestes de `docs/audit-2026-09-15-v1.2.0/recette-vm-v1.3.0-correctifs.md` en VM ; vérifier dans le code qu'il n'y a pas de souci.
- **Objet** : branche `release/1.2.0-notation-store` à `54ef49e` (2026-09-21 21:56), 7 commits devant `origin`, arbre propre.
- **Méthode** : trois relecteurs Opus en lecture seule, un par groupe de sections (A+C, B+D, E+F+G), chargés de dire ce que le code prédit geste par geste, avec `fichier:ligne`. Les constats qui fondent le verdict ont été relus à la source par la session (marqués ✔). Aucun agent n'a rendu de jugement : la gravité et le verdict sont de la session. Aucune modification du code.
- **Mesuré par la session** : trois suites Release à HEAD, **18 / 194 / 410 = 622 tests, 0 échec, 0 ignoré** (Application Control n'a rien bloqué ce soir) ; `Verify-Release.ps1` sort en 0 ; le bundle `msix/AZERTYGlobal-1.3.0.0.msixbundle` (21:49) ne contient que l'exe, le manifeste et `Assets/`, aucun fichier parasite ; aucun fichier de `src/` n'est plus récent que le publish x64 (21:46).

## 1. Verdict

**La lecture du code ne remplace pas la VM, et elle dit que la 1.3.0 n'est pas prête.** Sur 55 gestes, le code en garantit 27, en contredit 17, et 11 ne se décident qu'en réel. Quatre des contradictions portent sur ce que le Changelog 1.3.0 annonce comme corrigé, et deux sont des régressions du dernier commit `54ef49e`.

| # | Ce que le code contredit | Preuve | Gravité |
|---|---|---|---|
| R1 | **Onglets « Applications » et « Langue » inaccessibles au clavier.** `TCS_FOCUSNEVER` retire la bande de l'ordre de tabulation et rien ne traite Ctrl+Tab, F6 ni les flèches au niveau fenêtre — `IsDialogMessageW` ne le fait pas. Au clavier seul, la liste d'apps suspendues, les trois radios de compatibilité, le choix de langue et les réinitialisations sont hors d'atteinte. Régression d'AG130-40 introduite par `54ef49e`. | `SettingsWindow.cs:698` ✔, VK_TAB seulement dans le sous-classement des raccourcis `:1793,1830` ✔ | **Bloquant** (accessibilité, Store 10.4) |
| R2 | **Piège clavier dans À propos et Statistiques.** Le sous-classement des liens rend `DLGC_WANTALLKEYS` sans exception pour Tab : une fois le focus sur un lien, Tab, Maj+Tab et Échap ne font plus rien. La parade existe dans `SettingsWindow.cs:1789-1795`, non portée. | `AboutWindow.cs:428-429` ✔, `UsageStatsWindow.cs:423-424` ✔ | **Bloquant** (WCAG 2.1.2) |
| R3 | **Écart 8 corrigé à moitié.** Sous « forcer la désactivation », Ctrl+Maj+W est bien avalé (plus de Ctrl+W destructeur), mais la recherche ne s'ouvre pas : `WM_APP_SEARCH` exige `ShouldProcessHook`, faux sous suspension → bulle « Désactivé ». Le flag `ShortcutsWhilePassThrough` est juste, son consommateur l'ignore. Gestes 2, 5, 6, 7 (retour) faux ; le Changelog dit l'inverse. | `TrayApplication.cs:534,664-672` ✔, `ApplyWindowInputState:563-569` ✔ | Majeur |
| R4 | **La touche morte en attente est détruite à la reprise d'une pause.** `StopPause` → `ApplyHookState(syncWhenActive:true)` → `SyncState()` → `_composition.Cancel()`. Geste 16 faux ; la doctrine écrite dans `KeyMapper.cs:1030-1031` n'est pas tenue par le produit. Le témoin `DeadKeyWhileSuspendedTests` court-circuite `StopPause` et ne le voit pas. | `TrayApplication.cs:1702` ✔, `KeyMapper.cs:209` ✔ | Majeur |
| R5 | **Échap ne ferme plus Paramètres, À propos ni Statistiques.** `IsDialogMessageW` convertit VK_ESCAPE en `WM_COMMAND(IDCANCEL)` ; aucune des trois fenêtres ne traite l'id 2, et leurs anciens gestionnaires `WM_KEYDOWN`/VK_ESCAPE sont morts depuis AG130-40. Même mécanisme pour Entrée → `IDOK` avalé dans Paramètres. | `SettingsWindow.cs:1421` vs `:1336` ✔ ; `AboutWindow.cs`, `UsageStatsWindow.cs` idem | Majeur |
| R6 | **AG130-09 repose sur une hypothèse que Windows ne tient pas.** La doc de `SendInput` dit qu'un blocage UIPI n'est signalé ni par le retour ni par `GetLastError`. Le compteur de pertes restera à 0 en VM et sera lu comme « aucune perte ». ⚠️ VM-13 du 19/09 a mesuré que le remappage **traverse** une fenêtre élevée : la perte n'est peut-être pas réelle, mais la détection annoncée ne détecte rien. | `RealWin32Api.cs:34-39`, `KeyMapper.cs:1093-1115` | Majeur (claim du Changelog non prouvé) |
| R7 | **Écart 5 (a) non corrigé sur le repli Alt+code.** `ModifierScanCode` ne sert que `BuildVkComboInputs` ; `BuildAltCodeInputs` émet toujours RAlt/RCtrl en `wScan=0` sans bit étendu. Un test verrouille cette forme volontairement (fiche `un-test-qui-fige-une-forme-peut-encoder-une-distinction`) : le correctif exige de discriminer par ordre, pas par `wScan`. | `KeyMapper.cs:1325-1330,1342-1347` vs `1479-1495` ; `AltGrEmissionTests.cs:17-21` | Majeur (cibles qui lisent le scan code, US) |
| R8 | **Seuil de sollicitation consommé même quand la sollicitation est refusée.** `ClearEnrichedThresholdSignal()` précède `MaybeShowReviewPrompt()` ; 20 caractères enrichis à 9 minutes actives → signal désarmé pour toute la session, plancher des 10 min jamais rattrapé avant redémarrage. | `TrayApplication.cs:2192`, `UsageStats.cs:279` | Majeur |
| R9 | **Une exception du constructeur de `LearningModule` gèle tout le comptage.** `BeginExcludedTyping()` à la ligne 396, `EndExcludedTyping` seulement dans `Dispose` ; un ctor qui lève laisse `_excludedTypingDepth` à 1 : plus aucune frappe comptée jusqu'au redémarrage. | `LearningModule.cs:396,3077`, `OnboardingWindow.cs:973` | Majeur |
| R10 | **Libellés d'onglets jamais retraduits** : `RefreshLanguageTexts` omet la bande, `TCM_SETITEMW` n'est appelé nulle part. FR→EN laisse « Général / Applications / Langue » sur une fenêtre anglaise. | `SettingsWindow.cs:1537-1558` | Majeur (visible) |
| R11 | **Le clavier virtuel n'a pas la garde « saisie sécurisée »** que la recherche a : Ctrl+Maj+Q s'affiche au-dessus d'un champ mot de passe. | `KeyboardHook.cs:294` vs `:285` ✔ | Majeur |
| R12 | **Le message de validation déborde sans armer le défilement** : `SetValidationMessage` repositionne sans `FitWindowToContent`. | `SettingsWindow.cs:1458-1465` | Mineur |
| R13 | **Touche morte conservée à travers un Alt+Tab vers une app ordinaire** (AG130-17, toujours ouvert) : l'arbitrage ne s'applique qu'à `DisabledAntiCheat`. Geste 17 faux par construction. | `KeyMapper.cs:1040` ✔ | Mineur (reporté 1.3.1, mais la recette le promet) |
| R14 | **Un premier plan élevé n'est pas « inconnu »** : `OpenProcess(QUERY_LIMITED)` réussit, seul `EnumProcessModules` échoue en silence → mode `Default`. Geste 8 repose sur une hypothèse fausse. | `RealWin32Api.cs:61-72,137-139`, `ForegroundMonitor.cs:290-294` | Mineur |
| R15 | **Rafale de bulles à chaque Alt+Tab entre deux apps suspendues** : le sélecteur appartient à `explorer.exe` → `Default` → `Leave` puis `Enter` (bulle sécurité, qui ignore `NotificationsEnabled`). | `ForegroundMonitor.cs:200-205`, `TrayApplication.cs:2519-2546` | Mineur (bruit) |
| R16 | **« Donner mon avis » n'emporte pas la version**, contrairement à « Signaler un bug ». Quatre sites. | `TrayApplication.cs:742,2300`, `UsageStatsWindow.cs:337`, `OnboardingWindow.cs:761` | Mineur |
| R17 | **Menu tray : 12 lignes, mais 5 groupes et non 4** (Langue + Quitter forment un cinquième bloc). Le Changelog dit « quatre blocs ». | `TrayApplication.cs:1428,1455,1472,1551` | Mineur (texte) |

Ce que le code **garantit** (27 gestes) : 1, 3, 4, 7 (sens distant), 9 · 11 (hors `#`), 12, 13, 15, 18, 19 · 21, 24, 25 · 28, 29 · 31 (refus dit), 32, 33 · 34-36 (calcul : tient à 1366×768 jusqu'à 175 %), 37, 40, 42, 43 · 45, 46, 47, 48, 49, 50, 52, 54 (`/bug`).

Ce qui **ne se décide qu'en réel** (11 gestes) : 10 (`@` sur E00 nu : RAlt injecté sans LCtrl, dépend de win32k), 14, 20 (six relectures de `GetForegroundWindow` par frappe), 22-23 (fenêtre ≤ 250 ms où la frappe sort en natif, par construction), 26, 27 (refus non journalisé), 30 (`ReinstallHook` sans `nudgeForeground` : le commentaire du code dit que ça ne reçoit rien), 38 (inerte : plus rien ne dépasse), 41 (visibilité du focus), 44 (visuel).

## 2. Dettes de preuve du paquet lui-même

| Constat | Détail |
|---|---|
| **Le bundle sur disque n'est pas celui que les documents décrivent.** | Recette (table « Le paquet à installer ») et Changelog « Vérifié » : construit à 19:17, 6 829 529 o, SHA `AC28BAFD…`, commit `63e58e2`. Disque : **21:49**, 6 835 788 o, SHA `A7154470…`, source `54ef49e`. Le premier est archivé sous `Archives/msix-previous/`. |
| **Aucun WACK sur le bundle actuel.** | `wack-report-v1.3.0.0.xml` est de 19:20:59, donc sur le bundle de 19:17. Le bundle de 21:49 porte la fenêtre à trois onglets et n'a pas passé le WACK. |
| **« 618 tests » est périmé.** | HEAD en rend **622** (18 / 194 / 410). |
| **La section F de la recette décrit l'ancienne fenêtre.** | Gestes 35-36 attendent une barre de défilement ; avec trois onglets, plus rien ne dépasse dans les trois configurations : la barre n'apparaît pas et la molette est inerte (`:1223`). Attente à réécrire. |
| **`54ef49e` a été construit sans revue.** | 9 fichiers, +3 236 / −2 845 (dont ~1 250 lignes de fins de ligne dans `UsageStats.cs`, diff réel ≈ 30 lignes). Deux régressions d'accessibilité (R1, R2 est antérieur mais découvert ici, R5, R10) sortent de ce commit ou de son voisinage. |

Le message de `54ef49e` dit aussi que `Pack-MSIX` refusait jusque-là un sous-dossier inattendu de `msix/` « clé privée comprise » : le bundle de 21:49 a été construit **après** ce correctif (script modifié à 21:49:21, bundle à 21:49:51) et la session a vérifié qu'il ne contient que `AZERTY%20Global.exe`, `AppxManifest.xml`, `AppxBlockMap.xml`, `[Content_Types].xml` et `Assets/`. ⚠️ Le bundle de 19:17 archivé, lui, n'a pas été inspecté : à ouvrir avant tout partage.

## 3. Ce que les tests ne prouvent pas (synthèse des trois rapports)

1. `SuspensionTransitionTests` éprouve deux fonctions pures par réflexion ; **rien ne teste `WM_APP_SEARCH` ni `WM_APP_VKBD`**, là où l'écart 8 se décide et échoue (R3).
2. `ShellRaceSuspensionTests.FenetreChangeEntreDeuxLectures_NeSuspendPlus` est creux : `MockWin32Api.TryGetForegroundProcess` n'appelle pas `GetForegroundWindow`, la seconde valeur de la file n'est jamais consommée. Passe avec ou sans le correctif.
3. `DeadKeyWhileSuspendedTests.ApresUnePause_…` remet `EmissionPaused = false` sur le moteur, jamais par `StopPause` (R4). `PauseVolontaire_ConserveLaToucheMorte` exerce un chemin inatteignable en production (`PassThroughAll` sort avant `ProcessKey`).
4. `MockWin32Api.ToUnicodeEx` rend toujours 1 : `IsDeadKeyOnLayout` ne peut jamais rendre `true` sous test. Le repli touche-morte-native du geste 19 n'a **aucune** couverture dans sa branche utile.
5. `EmissionLossGuardTests` force `SendInputResult = 0` : il teste la garde, pas la condition réelle (R6).
6. `HookSilenceWatchdogTests` le dit lui-même : la décision est testée, « la tuyauterie qui l'exécute » non (geste 30).
7. Le plancher des 10 minutes (`ReviewPromptFirstMinActiveMinutes`) n'est dans aucun test ; `EnrichedThresholdTests` prouve le signal, jamais la décision (R8). Les 4 tests neufs d'`UsageStatsTests` n'éprouvent que `Begin/EndExcludedTyping`, pas leur câblage (R9).
8. Zéro test de fenêtre : onglets, `TCN_SELCHANGE`, `FitWindowToContent`, `WM_DPICHANGED`, retraduction, `IsDialogMessageW` réel. R1, R2, R5, R10, R12 sont hors de portée de la suite par construction.
9. `MockWin32Api.SetWinEventHook` ignore `eventMin/eventMax` et écrase le délégué : rien ne prouve que le hook SWITCHSTART/SWITCHEND est demandé sur la bonne plage.
10. AG130-19 toujours ouvert : `TIMER_FOREGROUND_DEBOUNCE` jamais armé, branche morte `TrayApplication.cs:898-902`, paramètre `trayHwnd` inutilisé.

## 4. Ordre de marche proposé

1. **R1, R2, R5** — une session, fenêtres seules : bande d'onglets atteignable (Ctrl+Tab / Ctrl+Maj+Tab dans le WndProc, ou retirer `TCS_FOCUSNEVER` avec `WM_GETDLGCODE` sur la bande), `DLGC_WANTALLKEYS` conditionné à `!VK_TAB` dans les deux sous-classements de liens, traitement de `IDCANCEL`/`IDOK` dans les trois `WM_COMMAND`. Une demi-journée.
2. **R3, R4, R11** — hook et tray : accepter `_suspendedForCompatibility && reason == UserOverride` dans `WM_APP_SEARCH` et ne pas mettre `_characterSearch` en `SetInputPaused` dans ce cas ; ne pas appeler `_composition.Cancel()` dans le `SyncState` de `StopPause` ; ajouter `!AdvancedFeaturesSuppressed` au raccourci du clavier virtuel. Deux heures, plus trois témoins.
3. **R8, R9** — sollicitation : réarmer le signal quand la sollicitation est refusée pour un autre seuil ; `try/catch` autour du ctor de `LearningModule` avec `EndExcludedTyping` en `finally`. Une heure.
4. **Décider** R6 (retirer le claim UIPI du Changelog, ou le prouver en VM au geste 26) et R7 (1.3.1 ou réécriture du témoin).
5. **Reconstruire**, WACK sur le nouveau bundle, réécrire la table du paquet et les gestes 34-38 de la recette.
6. **VM minimale, ~12 gestes**, ceux que le code ne décide pas : 10, 14, 20, 22, 23, 26, 30, 44, plus VM-02 (mise à jour depuis 1.1.0) et VM-22 (désinstallation). Une heure de VM au lieu des 55 gestes.

## 5. Limites de cette revue

- Trois relecteurs de la même famille de modèle ; aucun second fournisseur.
- Rendu réel des fenêtres non observé ; les hauteurs des gestes 34-36 sont calculées, pas mesurées.
- ARM64, veille/reprise, mise à jour Store→Store : hors de portée du code, comme avant.
- Les rapports complets des trois relecteurs ne sont pas versionnés ; les pointeurs retenus ici ont été relus à la source pour R1 à R5, R11 et R13 (✔) ; les autres viennent des rapports, citations vérifiées par les relecteurs.

## 6. Suite donnée le 2026-09-21, même soir

Point 1 de l'ordre de marche (§ 4) exécuté : **R1, R2, R5 corrigés**, le reste du tableau du § 1 est inchangé.

| # | Correctif | Où | Preuve |
|---|---|---|---|
| R1 | `TCS_FOCUSNEVER` retiré, `WS_TABSTOP` posé sur la bande d'onglets : Tab l'atteint, flèches gauche/droite changent d'onglet, Tab repart vers le premier contrôle de l'onglet actif. Le commentaire qui justifiait `TCS_FOCUSNEVER` (« Tab changerait d'onglet ») décrivait un comportement que SysTabControl32 n'a pas. | `SettingsWindow.cs`, `CreateTabStrip` | Style seul, hors de portée de la suite : recette VM, geste 41 |
| R2 | Les deux sous-classements de liens répondent `DialogNavigation.DialogCodeKeepingTab` : `DLGC_WANTALLKEYS` sauf pour un `WM_KEYDOWN`/`WM_SYSKEYDOWN` portant VK_TAB, qui rend le code de base. `ShortcutSubclassProc` de Paramètres passe par la même fonction. Échap sur un lien ferme la fenêtre (géré dans le sous-classement, puisque le lien garde la touche). | `AboutWindow.cs`, `UsageStatsWindow.cs`, `SettingsWindow.cs`, `DialogNavigation.cs` | `DialogNavigationKeyboardTrapTests`, 11 témoins ; **mutation** : `/* MUTANT */` à la place de l'exception Tab → **2 rouges sur 421** |
| R5 | Les trois `WM_COMMAND` traitent `IDCANCEL` (→ `Close()`) et `IDOK` : Paramètres presse le bouton poussoir focalisé (`ButtonToPressOnEnter`, liste explicite des quatre boutons), Statistiques copie si le focus est sur « Copier » sinon ferme, À propos ferme. Les anciens gestionnaires `WM_KEYDOWN`/VK_ESCAPE restent en place : ils ne coûtent rien et reprendraient du service si une fenêtre se désinscrivait. | idem | Décision pure testée ; l'arrivée réelle de `IDOK`/`IDCANCEL` est du Win32 : recette VM, geste 41 |

Suite Release après correctif : **421 tests, 0 échec** (410 + 11). Build : 0 erreur, 2 avertissements préexistants (`TrayApplication.cs:1494,1507`).

Non touché, à dessein : `OnboardingWindow.cs:1027` répond aussi `DLGC_WANTALLKEYS` inconditionnel, mais cette fenêtre n'est pas inscrite à `DialogNavigation`, donc `IsDialogMessageW` ne la voit jamais et le piège n'existe pas là. R10 (libellés d'onglets non retraduits) et R12 restent ouverts : points 2 à 6 du § 4 inchangés.

## 7. Suite donnée le 2026-09-22 : point 2 de l'ordre de marche

**R3, R4 et R11 corrigés**, avec leurs trois témoins. Le reste du tableau du § 1 est
inchangé : R6 et R7 attendent un arbitrage (point 4), R8, R9, R10 et R12 restent ouverts.

| # | Correctif | Où | Preuve |
|---|---|---|---|
| R3 | `WM_APP_SEARCH` accepte la suspension choisie par l'utilisateur, et `ApplyWindowInputState` ne gèle plus la saisie de la recherche dans ce cas — un raccourci détecté qui ouvre une fenêtre inerte était le même défaut déplacé d'un cran. La décision sort en fonction pure `ShouldServeSearchWhileBlocked`. | `TrayApplication.cs` | `SearchWhileSuspendedTests`, 12 cas dont l'égalité avec `ShouldDetectShortcutsWhileBlocked` sur les cinq motifs |
| R4 | `SyncState` prend `preservePendingDeadKey`, que seul `StopPause` passe à vrai. La bascule manuelle (activer/désactiver) garde son annulation. | `KeyMapper.cs`, `TrayApplication.cs` | `PauseResumeDeadKeyTests`, 3 cas dont la réciproque : sans le drapeau, `SyncState` annule bien |
| R11 | Les deux raccourcis qui font surgir une fenêtre passent par `KeyboardHook.ShortcutOpensWindow` : l'oubli ne peut plus porter sur une seule des deux branches. | `KeyboardHook.cs` | `SecureInputShortcutTests`, 4 cas |

⛔ **Les suites n'ont pas pu être mesurées sur ce poste** : Application Control refuse les
trois assemblies fraîchement reconstruites (`0x800711C7`, une relance confirme). Le build
Release des trois projets sort en **0 erreur, 2 avertissements préexistants**
(`TrayApplication.cs:1509,1522`). Attendu en CI : **18 / 201 / 433 = 652** (194 + 7 et 421 + 12),
à lire sur un
push de la branche `release/1.2.0-notation-store` — geste d'Antoine.

⚠️ Limite des trois témoins, écrite dans leurs `<summary>` : ils tiennent les décisions,
pas leur câblage Win32. `StopPause`, l'arrivée de `WM_APP_SEARCH` et le callback du hook
restent hors de portée de la suite — c'est le geste 16 et la VM minimale du point 6 qui les
prouvent.
