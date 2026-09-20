# Vérification 02-coquille-donnees.md
Citations : 133 total · EXACT 130 · DÉCALÉ 0 · ABSENT 0 · NON VÉRIFIABLE 3

> Unité de compte = un couple (ligne de table × fichier:ligne). Les trois NON VÉRIFIABLE sont **3.8** et **10.4** (colonne citation « — », grep ou raisonnement) et **5.4** (octets `ff fe`, mesure binaire).
> Chemins relatifs à la racine du dépôt, comme le rapport. Colonne « test cité » recopiée du rapport.

## Décalées et absentes (détail)

| section | fichier:ligne cité | verdict | vraie ligne ou « nulle part » | citation (≤ 60 car.) |
|---|---|---|---|---|
| — | — | — | **aucune** : 0 DÉCALÉ, 0 ABSENT | — |

## Constats à citation confirmée (EXACT ou DÉCALÉ), condensés

| section | fichier:ligne | constat en une phrase | test cité |
|---|---|---|---|
| 1.1 | Program.cs:30, :27-28 | Mutex en espace de noms `Local\` : deux sessions du même compte lancent deux instances partageant les mêmes fichiers. | aucun trouvé |
| 1.2-1.3 | Program.cs:12-13, :38, :60 | Arguments bruts jamais journalisés ; la seconde instance écrit puis sort avant la rotation. | QuickWinsTests:57 |
| 1.4 | Program.cs:63-77 ; TrayApplication.cs:313-323 | Exception non gérée : ligne `FATAL:` assainie, message, terminaison ; échec de ctor traité à part. | aucun trouvé |
| 1.5 | ConfigManager.cs:691-693 ; recette-vm-resultats.md:51 | Chemin demandé `%LocalAppData%\AZERTY Global`, redirigé sous MSIX vers `LocalCache\Local`. | recette VM-01 |
| 1.6 | ConfigManager.cs:719-720 | L'assainissement masque le profil utilisateur mais pas un chemin hors profil, en slashs ou UNC. | aucun trouvé |
| 1.7 | ConfigManager.cs:758-765, :771-777 | Noms de processus jamais en clair dans `error.log` : HMAC tronqué, sel persisté dans `config.json`. | aucun trouvé |
| 1.8 | ConfigManager.cs:805, :812-827 | Rotation une seule fois au démarrage, une génération conservée, ~10 Mo au total. | aucun trouvé |
| 1.9 | ConfigManager.cs:742 ; ForegroundMonitor.cs:235 | Aucun caractère ni titre de fenêtre journalisé ; le PID n'apparaît qu'en mode debug compatibilité. | ConfigManagerCompatTests:146 |
| 2.1 | ConfigManager.cs:980, :1013-1016 ; UsageStats.cs:551-554 ; LessonProgressStore.cs:286-289 | Écriture atomique par fichier temporaire puis `File.Replace`, même motif dans trois stores. | UsageStatsTests:219 |
| 2.2 | ConfigManager.cs:942-946 | JSON invalide : le cache reste vide et la session tourne entièrement sur les défauts. | ConfigManagerCompatTests:188 |
| 2.3 | ConfigManager.cs:970-974 | Le fichier illisible n'est jamais écrasé, mais toute modification de la session est perdue. | ConfigManagerCompatTests:188 |
| 2.4 | ConfigManager.cs:938 | Les clés inconnues de premier niveau sont conservées et réécrites. | ConfigManagerCompatTests:85 |
| 2.5 | ConfigManager.cs:991, :993 | Deux transformations silencieuses : tout nombre repasse par un `double`, toute valeur non scalaire devient une chaîne. | aucun trouvé |
| 2.6 | ConfigManager.cs:335-358 | Migration v1.1→v1.3 : `reviewPromptDone` n'est plus lu du tout. | ReviewPromptConfigTests:141, :163, :174 |
| 2.7 | ConfigManager.cs:400 | `reviewPromptDone` continue d'être écrit alors que plus aucun code ne le lit. | ReviewPromptConfigTests:84 |
| 2.8 | ConfigManager.cs:130-153 | Un getter écrit sur disque : la première lecture sans la clé déclenche un `Save()` complet. | LocalizationTests:31, :47 |
| 2.9 | ConfigManager.cs:967 | Le chemin temporaire est fixe et partagé : deux instances se marchent dessus, l'échec est silencieux. | aucun trouvé |
| 2.10 | ConfigManager.cs:561-575 | `config.json` contient en clair les noms de processus ayant un override de compatibilité. | ConfigManagerCompatTests:35 |
| 3.1 | ProductIdentity.cs:46-49, :54-55 | Quatre URL littérales, dont `ms-windows-store://review/?ProductId=`. | AppChannelTests:23 (identité seule) |
| 3.2 | TrayApplication.cs:681-692 | Huit ouvertures de menu, toutes concaténées à partir de constantes. | SoberChannelTests:25-87 (présence) |
| 3.3 | TrayApplication.cs:2036 | Seul paramètre de requête émis : `source=app-notification`, constante. | aucun trouvé |
| 3.4 | StoreReview.cs:44, :49 | Seul appel sortant non déclenché par un lien : l'API Store de Windows. | ReviewSharePromptTests (décision) |
| 3.5, 3.7 | StoreReview.cs:81, :90 ; UsageStatsWindow.cs:334, :337 | Repli Store sur thread COM ; la fenêtre de statistiques ouvre `/feedback` et Discord. | aucun trouvé |
| 3.6 | AboutWindow.cs:340, :343 | Deux URL hors `ProductIdentity` ; HelloAsso n'est pas gardé par la politique de liens externes. | aucun trouvé |
| 4.1 | TrayApplication.cs:94-106 | Chemin notification : seuils de 3 et 10 jours d'usage, plancher calendaire, silence de 48 h. | DailyChallengeTests (indirect) |
| 4.2 | TrayApplication.cs:1943-1956 | Ce chemin tombe avec les liens externes et avec le réglage de notifications, et ne tire qu'au démarrage. | PolicyTests:236 |
| 4.3 | ReviewSharePrompt.cs:48, :54, :64-83 | Chemin partage : canal Store seul, seuil de 2 jours, et il ne consulte pas `NotificationsEnabled`. | ReviewSharePromptTests ×12, PolicyTests:367 |
| 4.4 | ConfigManager.cs:358 | Une installation 1.1.0 qui avait vu sa sollicitation repart avec deux essais. | ReviewPromptConfigTests:141 |
| 4.5 | TrayApplication.cs:1984-1987 | Sur une installation migrée, le plancher de 7 jours entre essais est sauté au premier passage. | aucun trouvé |
| 4.6 | TrainingReminders.cs:57 | La priorité de l'avis sur le rappel Défi est lue sur la date persistée. | DailyChallengeTests:133, :212, :223 |
| 4.7 | AppChannel.cs:96-97 ; TrayApplication.cs:1997 | Canal AMCF sobre : aucune sollicitation ; hors package la cible est `/feedback`, pas le Store. | SoberChannelTests:87, ReviewSharePromptTests:75 |
| 4.8 | TrayApplication.cs:2110, :2125 | Le chemin partage ouvre une boîte modale du Store sans aucune garde de premier plan. | aucun trouvé |
| 4.9 | TrayApplication.cs:2060-2064 | « Noter sur le Microsoft Store » pose `reviewPromptClicked` sans consommer d'essai. | SoberChannelTests:63, :71 |
| 4.10 | UsageStats.cs:426-427 ; TrayApplication.cs:1971 | Avec `UsageStatsEnabled=0`, les seuils de jours actifs ne sont jamais atteints : plus aucune sollicitation. | StatsCollectionTests:98 (mécanisme seul) |
| §5 table | PolicyManager.cs:73, :76, :78-81, :86, :278 | Les cinq noms de valeur et la racine HKLM 64 bits correspondent à l'ADMX et au `.reg`. | test_admx_agreement.py |
| 5.1 | PolicyManager.cs:134-139, :154-155 | Un DWORD non nul vaut vrai ; un type inattendu rend `null` sans trace, sauf pour `Language`. | PolicyTests:133, :140 |
| 5.2 | PolicyManager.cs:230-231 ; adml fr:28 | Seule stratégie à ne pas verrouiller à 1 : l'ADML dit exactement cela. | PolicyTests:253, :260, :288 |
| 5.3 | entreprise/fr-FR/AZERTYGlobal.adml:22 ; Note RGPD:27 ; adml en-US:26 | Le chemin annoncé à la DSI est le chemin non redirigé, contredit par la note RGPD. | test_admx_agreement.py (noms seuls) |
| 5.5 | PolicyManager.cs:110-118 | Registre illisible : aucune politique, comportement de poste non géré. | PolicyTests:452, :493 |
| 6.1 | AutoStart.cs:98-111, :161-207 | Packagé : `StartupTask` WinRT ; hors package : raccourci `.lnk` par P/Invoke pur. | QuickWinsTests:42 |
| 6.2 | AutoStart.cs:238 ; AppxManifest.xml:54-55 | `TaskId` identique des deux côtés, `Enabled="false"` à l'installation. | aucun test ne compare ces littéraux |
| 6.3 | AutoStart.cs:128-129 ; ConfigManager.cs:177 | L'état est lu à la source, pas dans `config.json` ; toute exception rend faux. | aucun trouvé |
| 6.4 | AutoStart.cs:240-242 | `DisabledByUser` et `DisabledByPolicy` comptent comme inactif et renvoient l'utilisateur à Windows. | QuickWinsTests:42 |
| 6.5-6.6 | AutoStart.cs:113-117, :257-263 | L'état final est relu avant mise en cache ; idempotence voulue après un faux échec MSIX. | aucun trouvé |
| 7.1 | ToastActivation.cs:54 ; AppxManifest.xml:71, :79 | Les trois valeurs de CLSID sont identiques, et un test lit réellement le manifeste. | ToastActivationTests:89, :118 |
| 7.2 | TrayApplication.cs:252-257 | Deux sous-chaînes seulement sont cherchées dans les arguments, réduites aussitôt à un entier. | aucun test de l'analyse |
| 7.3 | TrayApplication.cs:599-607 | Les trois cibles atteignables sont des constantes de compilation, aucune ne vient de l'argument. | aucun trouvé |
| 7.4 | ToastActivation.cs:76-80, :122-124 | `appUserModelId` n'est jamais vérifié et la fabrique est `REGCLS_MULTIPLEUSE`. | aucun trouvé |
| 7.5 | ToastActivation.cs:184-196, :211-216 | Toutes les valeurs interpolées du XML passent par `EscapeXml` ; `data`/`count` sont ignorés. | ToastActivationTests:17 |
| 7.6 | Program.cs:41-56 | Sans activateur COM, la seconde instance meurt en silence et l'action du toast est perdue. | QuickWinsTests:57 |
| 8.1 | Localization/L.cs:25 ; ConfigManager.cs:280 | Aucun repli : la langue vaut « fr » ou « en », toute autre valeur est refusée en amont. | LocalizationTests:89, :97 |
| 8.2 | L.LessonsWindow.cs:21, :23 | Deux couples dont la version française est de l'anglais ou du franglais. | aucun trouvé |
| 8.3 | Program.cs:23 ; ConfigManager.cs:266-267 | Ordre : stratégie, puis réglage utilisateur, puis défaut ; lu avant tout message affichable. | PolicyTests:266, :396 |
| 8.4 | ConfigManager.cs:287-288, :954-962 | La langue système n'est consultée que sur une installation neuve ; une mise à jour ne bascule jamais. | LocalizationTests:31, :47, :65 |
| 8.5 | ConfigManager.cs:299-309 ; TrayApplication.cs:1461 | Porte unique : le verrou de stratégie est dans le setter, le menu grise en plus son entrée. | PolicyTests:396 |
| 8.6-8.7 | ConfigManager.cs:920, :972 ; LessonProgressStore.cs:190, :248 ; UsageStats.cs:353-354 ; L.cs:28-39 | Quatre chaînes FR hors tables, toutes vers `error.log` ; heure formatée à la main, repli `yyyy-MM-dd`. | UsageStatsTests:306 |
| 9.1 | UsageStats.cs:532-545 | Contenu exhaustif du fichier : trois dates au jour près et onze compteurs. | UsageStatsTests:219, :290 |
| 9.2 | UsageStats.cs:182-192 | Seul le compteur de catégorie est incrémenté : ni caractère, ni position, ni séquence. | UsageStatsTests:111, :125-160 |
| 9.3 | UsageStats.cs:33-37 | Seule la minute comptée est persistée, et seulement en total : aucune distribution horaire. | UsageStatsTests:278 |
| 9.4 | UsageStats.cs:162-167 ; TrayApplication.cs:1787 | Aucune écriture sur le chemin du hook ; une fermeture brutale perd au plus cinq minutes. | UsageStatsTests:243 |
| 9.5-9.6 | UsageStats.cs:363-405 ; ChallengeShare.cs:42-45 | Les deux partages sont du texte brut sans identifiant ; le défi ne porte aucune statistique. | UsageStatsTests:341, ChallengeShareTests:96 |
| 9.7 | LessonProgressStore.cs:220-227, :269-279 | La matrice d'erreurs héritée est détectée puis supprimée ; huit champs par exercice restent. | aucun test ne nomme la suppression |
| 9.8 | UsageStats.cs:439-448, :519 | Collecte éteinte : le fichier n'est ni lu ni créé, les compteurs meurent avec la session. | StatsCollectionTests:66, :111, :124 |
| 10.1 | TrayApplication.cs:461-462, :818-825 | Le rappel d'entraînement est évalué toutes les cinq minutes, sur le timer des statistiques. | DailyChallengeTests:117-186 (décision) |
| 10.2 | TrainingReminders.cs:29, :33, :36, :40 | Opt-in, un rappel par jour, jamais avant 17 h, arrêt définitif après trois ignorés. | DailyChallengeTests:117, :125, :143 |
| 10.3 | TrainingReminders.cs:109, :113-114 ; TrayApplication.cs:593-594, :1265 | Le compteur d'ignorés ne fonctionne que sur le canal balloon, qui est celui du rappel. | DailyChallengeTests:151 |
| 10.5 | TrayApplication.cs:2144, :2214-2221 | Les bulles de sécurité contournent délibérément le réglage et la stratégie. | PolicyTests:202 |
| 10.6 | L.Tray.cs:117-119 ; AppxManifest.xml:38 | Sur le canal toast, l'attribution Windows ajoute le nom du produit une seconde fois. | aucun trouvé |
| 10.7-10.8 | ToastActivation.cs:168-172 ; TrayApplication.cs:1150-1156, :1183 | Corps du toast tronqué à sa première ligne ; annonce et relance marquées avant affichage, une seule fois. | ToastActivationTests:158, AutoStartNudgeTests:101 |
| 10.9 | TrayApplication.cs:307-311 | Au démarrage, au plus une notification, l'ordre étant fixé par le court-circuit. | aucun test trouvé |
| 11.1 | AppChannel.cs:55-57 | Détection par deux appels Windows indépendants, une seule fois, non rafraîchie. | AppChannelTests:39-72 |
| 11.2 | AppChannel.cs:75-83 | Direction d'échec : tout paquet inconnu est traité comme le canal sobre. | AppChannelTests:23, :63, :72 |
| 11.3 | TrayApplication.cs:1286-1299 | Le menu est asservi à une liste que les tests lisent ; deux entrées disparaissent hors Store. | SoberChannelTests ×8, PolicyTests:340, :350 |
| 11.4 | AppChannel.cs:96-97 ; PolicyManager.cs:217-225 ; ConfigManager.cs:183-187 | Statistiques éteintes par défaut sur AMCF ; les notifications de confort restent actives partout. | StatsCollectionTests:51, PolicyTests:307 |
| 11.5 | AppChannel.cs:31-32, :91-94 | Le canal hors package garde le comportement d'avant la v1.2.0, et le code le dit contre le plan. | SoberChannelTests:80, AppChannelTests:33 |
| 11.6 | AppChannel.cs:111-116 ; UsageStats.cs:422-424 | Le canal est forçable en test par un scope restaurant même sur exception. | AppChannelTests:99 |
