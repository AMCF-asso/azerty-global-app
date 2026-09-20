# Vérification 01-moteur-de-frappe.md
Citations : 103 total · EXACT 102 · DÉCALÉ 0 · ABSENT 0 · NON VÉRIFIABLE 1

> Unité de compte = un couple (ligne de table × fichier:ligne) ; les lignes qui citent plusieurs ancres comptent une fois par ancre.
> Seul NON VÉRIFIABLE : **7.5**, dont la colonne citation vaut « — » (constat d'absence de `OpenInputDesktop`/`GetThreadDesktop`, aucun texte à ancrer).
> Convention du rapport respectée : chemins relatifs à `src/` sauf mention contraire.

## Décalées et absentes (détail)

| section | fichier:ligne cité | verdict | vraie ligne ou « nulle part » | citation (≤ 60 car.) |
|---|---|---|---|---|
| — | — | — | **aucune** : 0 DÉCALÉ, 0 ABSENT | — |

## Constats à citation confirmée (EXACT ou DÉCALÉ), condensés

| section | fichier:ligne | constat en une phrase | test cité |
|---|---|---|---|
| 1.1 | KeyboardHook.cs:158 | Point d'entrée : struct blittable, aucune allocation GC dans le callback. | aucun trouvé |
| 1.2 | KeyMapper.cs:457-458, :575 | `CleanupStaleModifiers` tourne sur chaque touche non-modificateur, jusqu'à 8 `GetAsyncKeyState`. | aucun trouvé |
| 1.3 | KeyMapper.cs:306-307, :751 | Deux allocations managées et un à deux `ToUnicode` par keydown émetteur, dans le callback. | aucun trouvé |
| 1.4 | ForegroundMonitor.cs:103 ; KeyMapper.cs:1005 | Six `GetForegroundWindow` par caractère émis sur le chemin nominal. | aucun trouvé |
| 1.5 | KeyMapper.cs:996 ; UsageStats.cs:164-166 ; TrayApplication.cs:461-462, :543 | La journalisation d'usage reste en mémoire : aucune E/S fichier sur le chemin nominal. | StatsCollectionTests, UsageStatsTests |
| 1.6 | KeyboardHook.cs:255 ; ConfigManager.cs:731 | Le chemin d'exception délègue l'écriture au ThreadPool : pas d'E/S synchrone. | aucun trouvé |
| 1.7 | KeyboardHook.cs:250-257, :86 | Le corps entier du callback est sous `try` et le délégué est enraciné en champ. | aucun trouvé |
| 1.8 | TrayApplication.cs:458, :812-816 | Aucune détection d'un hook retiré : la seule parade est une réinstallation aveugle toutes les 60 s. | aucun trouvé |
| 1.9 | KeyboardHook.cs:122 | `Reinstall()` pose le nouveau hook avant de décrocher l'ancien. | aucun trouvé |
| 2.1 | KeyboardHook.cs:161-162 | Auto-reconnaissance par le seul marqueur `dwExtraInfo`. | aucun trouvé |
| 2.2 | KeyboardHook.cs:23 ; Program.cs:30-31 | Marqueur aléatoire par démarrage ; l'unicité est tenue par le mutex. | aucun trouvé |
| 2.3 | KeyMapper.cs:26 | `LLKHF_EXTENDED` est le seul bit de flags interprété : une frappe injectée est traitée comme physique. | aucun trouvé |
| 2.4 | GameRegistry.cs:24-32 ; ForegroundMonitor.cs:275-276 | La seule protection contre le double remappage distant est au niveau du processus au premier plan. | ForegroundMonitorMockedTests, GameRegistryTests |
| 3.1 | KeyMapper.cs:333-334 ; KeyboardHook.cs:187 | Suivi interne des modificateurs, alimenté avant tout traitement, même remappage désactivé. | MaintainableLayerManagerTests, Audit120ResumeTests |
| 3.2 | KeyMapper.cs:326 | AltGr reconnu sous deux formes : RAlt réel, et RCtrl+LAlt. | Audit120RegressionTests, KeyMapperBuildInputsTests |
| 3.3 | KeyMapper.cs:457-458, :213-218 | La resynchronisation ne sait qu'éteindre un modificateur collé, jamais en allumer un tenu physiquement. | aucun trouvé |
| 3.4 | KeyMapper.cs:220 | La resynchronisation est suspendue pendant une compensation d'envoi partiel en attente. | Audit120RecoveryTests |
| 3.5 | KeyboardHook.cs:144-145 | Pendant une suspension, les modificateurs restent suivis sans notification UI. | Audit120ResumeTests |
| 3.6 | TrayApplication.cs:861-863 ; Win32.cs:890 | Aucune constante `WTS_SESSION_LOCK` : rien n'est fait au verrouillage de session. | aucun trouvé |
| 4.1 | CompositionEngine.cs:20-24 ; KeyMapper.cs:803-811 | Machine à états pure, sans Win32, branchée au mapper. | CompositionEngineTests |
| 4.2 | CompositionEngine.cs:31 ; Layout.cs:49 | Combinaison inconnue : forme isolée plus caractère, rien n'est perdu. | CompositionEngineTests, DeadKeyAndSmartCapsTests |
| 4.3 | CompositionEngine.cs:43-50 | Touche morte inconnue du layout acceptée sans validation, forme isolée vide. | CompositionEngineOrphanDeadKeyTests |
| 4.4 | KeyMapper.cs:605-610 | Retour arrière annule la composition puis est laissé passer à l'application. | DeadKeyAndSmartCapsTests |
| 4.5 | KeyMapper.cs:135-145, :208 | Une touche morte en attente n'est annulée ni au changement d'application, ni à l'entrée en suspension. | aucun trouvé |
| 4.6 | KeyMapper.cs:290, :875-879 | `ToUnicode` purge le tampon du thread appelant, pas celui du `hkl` de la cible. | Audit120RegressionTests (sondes, pas la cible) |
| 5.1 | KeyMapper.cs:1136 | Mode par défaut : `KEYEVENTF_UNICODE`, down puis up par caractère. | KeyMapperEmitTextIntegrationTests, KeyMapperBuildInputsTests |
| 5.2 | KeyMapper.cs:1115-1118, :969-970 | Hors BMP, quatre événements ; substitut orphelin : rien n'est émis. | 4 tests nommés |
| 5.3 | KeyMapper.cs:959 | Un seul `SendInput` par chaîne, sans plafond de lot ni segmentation. | EmitText_MultipleChars_BatchedInSingleSendInputCall |
| 5.4 | TypingEngine.Windows/Win32.cs:64-65 | `SetLastError` n'est pas activé : un refus UIPI rend 0 sans code d'erreur lisible. | aucun trouvé |
| 5.5 | KeyMapper.cs:1012-1014 | La compensation ne se déclenche que sur lot partiel : un `sent == 0` perd le caractère en silence. | Audit120SuspensionTests (lot partiel seulement) |
| 5.6 | KeyMapper.cs:829-832 ; GameRegistry.cs:100-120 | Aucune liste d'applications sans Unicode ; le seul aiguillage hors défaut est le `NativeCombo` par DLL. | KeyMapperPassThroughLayoutTests (5) |
| 6.1 | ForegroundMonitor.cs:146-149 ; RealWin32Api.cs:162-163 | Trois WinEventHooks `OUTOFCONTEXT`, aucune réinstallation si l'un échoue en cours de vie. | ForegroundMonitorMockedTests |
| 6.2 | ForegroundMonitor.cs:214-216 | Un suivi requis manquant suspend l'émission définitivement jusqu'au redémarrage. | ForegroundMonitorMockedTests, Audit120SuspensionTests |
| 6.3 | ForegroundMonitor.cs:121-122 | `IsSnapshotStale` est une simple lecture, sans effet de bord. | ForegroundMonitorMockedTests (3) |
| 6.4 | TrayApplication.cs:831-832, :471-472 | Chien de garde à 250 ms sur le thread de la boucle de messages, donc celui du hook. | aucun trouvé |
| 6.5 | ForegroundMonitor.cs:103-104 ; KeyMapper.cs:489-492 | Le refus d'émettre n'arrête pas la frappe : la touche produit le caractère de la disposition Windows. | ShellRaceSuspensionTests, Audit120RecoveryTests |
| 6.6 | ForegroundMonitor.cs:200-205 | La liste shell ne sert qu'au menu ; la suspension « fenêtre changée » a été retirée. | ShellRaceSuspensionTests, Audit120RecoveryTests |
| 6.7 | ForegroundMonitor.cs:249-250 | Toute exception dans `Recompute` produit un snapshot fail-closed avec `SecureInput = true`. | aucun trouvé |
| 7.1 | Win32Api/RealWin32Api.cs:102-103 | Source 1 : `GetGUIThreadInfo` puis style `ES_PASSWORD`, conservateur si `GetClassNameW` échoue. | Audit120SuspensionTests (via mock) |
| 7.2 | SecureInputDetector.cs:28, :94-99 | Source 2 : UIA sur thread MTA dédié, l'appelant attend au plus 30 ms. | aucun trouvé |
| 7.3 | SecureInputDetector.cs:85 ; TrayApplication.cs:1744-1747 | Au-delà du budget, la valeur rendue est la dernière connue, globale : faux positifs et faux négatifs. | aucun trouvé |
| 7.4 | KeyMapper.cs:80 ; KeyboardHook.cs:211 | Le remappage ordinaire et les touches mortes continuent d'émettre dans un champ mot de passe. | 3 tests nommés |
| 7.6 | ForegroundMonitor.cs:196 | L'attente de 30 ms est payée sur le thread de la boucle de messages. | aucun trouvé |
| §8 listes | GameRegistry.cs:24-33, :40-61, :71-78, :84-93, :100-120 | Cinq listes littérales : accès distant, anti-cheat, exact + chemin, précaution, DLL. | GameRegistryTests |
| 8.1 | ForegroundMonitor.cs:275-287 ; TrayApplication.cs:2319 | Ordre de priorité fixé ; `forceOn` refusé à la source sur un processus anti-cheat. | ForegroundMonitorMockedTests, GameRegistryTests (17) |
| 8.2 | KeyMapper.cs:976-981 | Trois chemins d'émission par caractère en `forceOn`, contre un seul en mode par défaut. | KeyMapperEmitTextIntegrationTests (4) |
| 8.3 | KeyMapper.cs:1181, :1379-1393 | AltGr émis par `VK_RMENU` seul, sans Ctrl ni bit étendu, alors qu'il est détecté sous deux formes. | KeyMapperBuildInputsTests (figent la forme) |
| 8.4 | KeyMapper.cs:1190-1191 ; TestSupport/MockWin32Api.cs:8 | Mélange VK et scancode dans le même lot, aucun bit étendu jamais posé. | KeyMapperBuildInputsTests (6, sur la forme) |
| 8.5 | KeyMapper.cs:489-492 | Une suspension tombée entre la touche morte et la lettre fait sortir la lettre nue, sans trace. | Audit120SuspensionTests/ResumeTests (pas la composition en cours) |
| 8.6 | KeyMapper.cs:1234-1238 | Alt+code : environ 22 `INPUT` par caractère et dépendance au traitement Alt+numpad de la cible. | KeyMapperBuildInputsTests (6) |
| 9.1 | KeyMapper.cs:1086-1088 ; TrayApplication.cs:338 | Trois `Dictionary<>` non protégés reposent entièrement sur l'affinité de thread. | aucun trouvé |
| 9.2 | KeyMapper.cs:60-64 | `_passedThroughKeys` est verrouillé : protection cohérente mais asymétrique avec 9.1. | KeyMapperCtrlRegressionTests |
| 9.3 | KeyboardHook.cs:36-37 ; TrayApplication.cs:494 | Aucun `volatile` ne documente la contrainte de thread sur `_enabled` et `_passThroughAll`. | aucun trouvé |
| 9.4 | SecureInputDetector.cs:123-125 | Seul état réellement partagé entre deux threads, correctement traité. | aucun trouvé |
| 9.5 | UsageStats.cs:172 | Le verrou des statistiques est pris depuis le callback et depuis le timer, sur le même thread. | StatsCollectionTests, UsageStatsTests |
| 10.1 | KeyboardHook.cs:107 | Pose unique au démarrage ; l'échec remonte au ctor puis `PostQuitMessage(1)`. | aucun trouvé |
| 10.2 | TrayApplication.cs:532-540 | Réinstallations programmées à 500 ms, 3 s et 8 s, puis chien de garde à 60 s. | aucun trouvé |
| 10.3 | TrayApplication.cs:851-852 | Reprise de veille traitée ; rien sur `PBT_APMSUSPEND`, constante absente de `Win32.cs`. | aucun trouvé |
| 10.4 | TrayApplication.cs:861-864 | Déverrouillage et connexions réinstallent le hook ; le verrouillage n'est pas traité. | aucun trouvé |
| 10.5 | TrayApplication.cs:1789-1792 ; KeyMapper.cs:249 | Si le snapshot est périmé à la sortie, aucun keyup n'est envoyé et les touches restent collées. | KeyMapperCtrlRegressionTests, Audit120SuspensionTests |
| 10.6 | TrayApplication.cs:875-876, :1781 | `WM_ENDSESSION` et `IDM_QUIT` passent par le même `Cleanup()` idempotent. | aucun trouvé |
| 10.7 | ForegroundMonitor.cs:301-316 | Les trois WinEventHooks sont libérés ; pas de `Dispose` sur `SecureInputDetector`. | ForegroundMonitorMockedTests |
| 11.1 | KeyMapper.cs:736-740 ; MaintainableLayerManager.cs:36-41 | Déclencheur lu sur les modificateurs physiques bruts, hors touche morte active. | 23 tests nommés |
| 11.2 | KeyMapper.cs:742-743 | Le modificateur qui a servi au déclencheur est neutralisé jusqu'à son relâchement complet. | KeyMapperMaintainableLayerCoverageTests |
| 11.3 | MaintainableLayerManager.cs:192-195 | Trois modes : one-shot, verrou par identité de processus, accord. | MaintainableLayerManagerTests (13) |
| 11.4 | MaintainableLayerManager.cs:256-257, :331-340 | Verrou suspendu hors de son processus, purgé à sa mort, jamais hérité par un PID réutilisé. | 4 tests nommés |
| 11.5 | KeyMapper.cs:767-768 | Ctrl, Alt et Win sortent avant la couche ; la composition classique reste prioritaire. | 4 tests nommés |
