# Changelog — Application AZERTY Global

## Version 1.2.0 — 17 août 2026

Préparée et vérifiée dans ce dépôt ; **non encore soumise au Microsoft Store**. Remplacer cette ligne par la révision Store le jour de l'acceptation. Source réconciliée le 2026-08-15.

- Report de la base interne 1.1.2 et des fonctions 1.2.0 en développement dans le dépôt public canonique : interface bilingue, statistiques locales, défi quotidien facultatif, rappels d'entraînement et activation des notifications Store.
- Extraction du modèle de disposition, du parseur JSON et de la composition des touches mortes dans `TypingEngine.Core`, projet portable partagé et couvert par sa propre suite de tests.
- Extraction du remapping, du hook clavier, de l'injection Win32 et de la compatibilité jeux dans `TypingEngine.Windows`. L'application fournit désormais sa configuration, ses journaux et ses statistiques via `IWindowsTypingHost`.
**Sollicitation d'avis — refonte (décision du 2026-08-16)**

- La sollicitation n'est plus subordonnée à l'absence de fenêtre d'accueil : elle vivait dans le `else` du test d'affichage de l'accueil, si bien qu'une partie des utilisateurs n'était **jamais** sollicitée. La condition exacte était `showOnboardingAtStartup` **et** moins de trois étapes du module d'apprentissage terminées : garder l'accueil au démarrage ne suffisait pas, il fallait aussi ne l'avoir jamais dépassé. Elle est désormais différée à la fermeture de l'accueil, via un nouveau callback `OnboardingWindow.OnClosed`.
- Déclenchement sur les jours d'usage réels au lieu du calendrier : essai 1 à 3 jours d'usage distincts (plancher de 3 jours depuis la première frappe remappée), essai 2 à 10 jours d'usage distincts (plancher de 7 jours après l'essai 1). Le J+7 calendaire de la v1.1 sollicitait de la même façon celui qui tape tous les jours et celui qui avait installé puis oublié l'application.
- Deux essais au maximum sur toute la vie de l'installation, contre un seul auparavant. Le second est abandonné si le premier a été cliqué, ou si l'application n'a plus servi depuis plus de trois jours.
- Fin du tirage 50/50 entre la fiche Store et la page feedback : en packagé la cible est toujours le Store. Une sollicitation sur deux partait vers un canal privé alors que la note publique est le seul levier qui manque à la fiche. La page feedback ne sert plus qu'aux installations hors Store, qui n'ont pas de fiche à noter.
- Aucune sollicitation dans les 48 heures qui suivent une erreur journalisée.
- `reviewPromptDone` cède la place à `reviewPromptCount` (plafonné à deux), `reviewPromptLastShown` et `reviewPromptClicked`. Migration des installations v1.1 : un `reviewPromptDone` à true vaut un essai déjà consommé, sinon la v1.2.0 enverrait deux notifications supplémentaires à quelqu'un qui a déjà été sollicité.
- Textes distincts pour chaque essai, sans aucun chiffre d'usage : les statistiques restent affaire de la fenêtre « Mes statistiques ». Le second essai ajoute le cadre associatif que le premier laisse de côté. Aucun des deux n'annonce sa propre fin : la formule « c'est la dernière fois qu'on vous le demande », prévue le 16 août pour lever la crainte du harcèlement, a été retirée le 18 août au premier smoke test — annoncer un plafond fait peser la demande au lieu de l'alléger. Les deux notifications décrivent ce que le clic ouvre au lieu de l'ordonner.

**Défi du jour — sortie de l'ombre et partage (décisions du 2026-08-16)**

- L'entrée « Défi du jour » du menu de la zone de notification est désormais **toujours visible**. Elle était conditionnée à `trainingEnabled`, qui vaut `false` par défaut : sur une installation neuve la fonction n'existait donc pas visuellement, alors que le défi commun est le seul contenu identique pour tous les utilisateurs. L'opt-in ne gouverne plus que les rappels d'entraînement, qui sont des notifications et relèvent d'un consentement distinct.
- Nouveau bouton **« Copier mon résultat »** sur le récapitulatif de fin de séance, présent uniquement après le défi commun : les cinq séances de prise en main dépendent de la progression individuelle, deux personnes n'y tapent pas le même extrait et il n'y a rien à y comparer.
- Le texte copié porte la date, la vitesse, la précision, la durée, les caractères qui ont posé problème, l'attribution de l'extrait quand il en a une, et le lien du site. Format texte et non image : il se colle dans une conversation sans capture d'écran ni téléversement. Aucune statistique d'usage n'y figure — elles restent dans « Mes statistiques ».
- Le record personnel, stocké dans `lessons-progress.json` depuis la v1.0 mais jamais affiché, est enfin lu : une séance qui bat le meilleur score antérieur est signalée comme telle.
- L'annonce unique du Défi du jour aux utilisateurs existants ouvre maintenant la séance du jour au lieu des Paramètres, et son texte a été reformulé en conséquence.

**Notation intégrée au Store (décision du 2026-08-16)**

- La sollicitation d'avis et l'entrée « Noter sur le Microsoft Store » passent par la boîte de notation **intégrée** de Windows (`StoreContext.RequestRateAndReviewAppAsync`) : la note se dépose sans quitter AZERTY Global. Le lien profond `ms-windows-store://review/` imposait une bascule vers l'application Store et l'attente de son chargement ; il reste le repli automatique hors package ou en cas d'échec de l'API.
- L'API exige Windows 10 1809, soit exactement le `MinVersion` déclaré dans le manifeste. La couche WinRT est celle qui pilote déjà `StartupTask` depuis la v1.0, et la publication AOT x64 la compile et la lie sans avertissement.
- Nouveau déclencheur : copier son résultat de défi puis refermer la fenêtre présente la boîte de notation. Le partage est le signal de promotion le plus net dont dispose l'application, et c'est le seul chemin de sollicitation qui atteigne aussi ceux qui ont coupé les notifications Windows. Les garde-fous existants restent en vigueur — deux essais au maximum sur la vie de l'installation, aucun après une réponse, aucun dans les 48 heures qui suivent une erreur journalisée, un seul par jour.

**Identité produit — moitié non localisée (décision du 2026-08-17)**

- Nouvelle classe `ProductIdentity`. 78 sites qui nommaient le produit en dur passent par une source unique : les 20 URL du site, le lien Discord, le dépôt GitHub, l'identifiant Store, le nom du binaire, le raccourci de démarrage, le dossier de configuration, la ressource du logo, les 12 noms de classes fenêtre, le mutex d'instance unique et 31 titres affichés. 77 lignes remplacées pour 77 ajoutées : substitution ligne pour ligne, aucun changement de comportement.
- Deux formes distinguées là où le code n'en avait qu'une : `DisplayName` « AZERTY Global », ce que l'utilisateur lit, et `Namespace` « AZERTYGlobal », qui est déjà le `RootNamespace` du csproj, l'`Identity Name` du MSIX et l'`Application Id`. `ConfigFolderName` reste un littéral distinct à dessein : renommer le produit ne doit pas déplacer la configuration et la progression de tout le monde.
- Le `TaskId` du `StartupTask` reste lui aussi un littéral hors de `ProductIdentity` : `AutoStart.cs` garde `"AZERTYGlobalStartup"` en dur, à l'identique du manifeste de la 1.1.0.0 servie par le Store. C'était le seul point de cette refonte capable de casser quelque chose — un `TaskId` dérivé du `Namespace` mais différent de celui qu'a enregistré la v1.1 rend la tâche orpheline à la mise à jour, et le lancement automatique cesse sans rien dire.
- `WindowClass(suffixe)` supprime une paire de littéraux que sept fenêtres dupliquaient entre `RegisterClassEx` et `UnregisterClass` — About, CharSearch, Onboarding, Settings, ToggleNotif, UsageStats et VK répétaient leur nom de classe à deux endroits, et n'en renommer qu'un laissait la classe enregistrée.
- Vérifié avant d'y toucher : rien ne dépendait de ces chaînes. Aucun appel à `FindWindow` dans le code, et les clés de position de fenêtre sont des constantes propres (`lessonsWindowBounds`, `VirtualKeyboardBoundsKey`), pas les noms de classe.
- Hors périmètre, différé après la soumission Store : les ~65 occurrences du nom enchâssées dans les phrases traduites de `Localization/`, ainsi que l'`AssemblyDescription`. Elles réécrivent du texte visible en français et en anglais.

**Sollicitation d'avis — arbitrages du 2026-08-17**

- Le partage d'un résultat n'incrémente plus que le compteur d'essais : il ne pose plus `reviewPromptClicked`. Windows ne dit jamais si l'utilisateur a déposé sa note, si bien qu'un partage suivi d'une boîte refermée aussitôt sans rien noter éteignait toute sollicitation ultérieure — le second essai était perdu pour quelqu'un qui n'avait rien donné. Le plafond de deux essais, la limite d'une sollicitation par jour et le silence de 48 heures après une erreur journalisée restent en vigueur.
- L'entrée « Noter sur le Microsoft Store » du menu de la zone de notification pose désormais `reviewPromptClicked` : y aller de soi-même est le signal d'intention le plus net dont dispose l'application, et relancer quelqu'un qui vient de jouer le jeu serait le pire des cas. Elle ne consomme pas d'essai pour autant — ce n'est pas une sollicitation que l'application s'est accordée, c'est une action de l'utilisateur.
- Le bouton de copie de « Mes statistiques », présent depuis la v1.1 sans avoir jamais rien armé, déclenche la même sollicitation différée que le partage d'un résultat de défi : même geste de promotion, mêmes garde-fous, et tir à la fermeture de la fenêtre plutôt qu'au clic, pour ne pas couper le geste en deux.

**Lancement automatique — rattrapage hors accueil (décision du 2026-08-17)**

- Nouvelle entrée « Lancer au démarrage de Windows » au premier niveau du menu de la zone de notification, cochée quand la tâche est réellement enregistrée. Hors Paramètres, le lancement automatique n'avait aucune affordance permanente.
- Relance unique lorsque l'application a servi deux jours distincts sans démarrage automatique. Deux jours d'usage sans autostart signifient que l'utilisateur l'a relancée lui-même : l'intention est déjà démontrée, on ne lui épargne que le geste. Une seule proposition sur la vie de l'installation, jamais réémise, et rien ne s'active sans un clic.
- Motif : le manifeste déclare le `StartupTask` à `Enabled="false"`, et `OnboardingWindow.Close()` ne persistait la case pré-cochée que si l'utilisateur avait atteint l'étape 3, c'est-à-dire après deux clics sur « Suivant ». Qui refermait l'accueil plus tôt — croix, Échap, « Quitter » — n'obtenait jamais le lancement automatique et ne revoyait pas l'application au démarrage suivant.
- La décision de la v0.9.7.1 reste intacte : aucune case jamais vue n'est persistée en silence. Un choix fait à la main dans le menu éteint la relance, dans un sens comme dans l'autre.
- La règle de déclenchement vit dans `AutoStartNudge`, pure et testable sans fenêtre, sur le modèle de `TrainingReminders.ShouldRemind`.

**Correctifs de l'audit de release (2026-08-18)**

- Sollicitation d'avis après un partage : la limite d'une par jour se lisait sur un champ en mémoire, remis à zéro à chaque démarrage du processus. Elle ne valait donc qu'une par session — partager un résultat, redémarrer l'application, repartager le même jour consommait les deux essais de la vie de l'installation en quelques minutes. La garde s'appuie désormais sur la date persistée.
- Le partage exige à son tour un plancher d'usage : une première frappe remappée et deux jours d'usage distincts. Ce chemin n'appliquait aucun seuil, si bien qu'un utilisateur du jour 1 qui terminait le Défi du jour et copiait son résultat recevait la boîte de notation dans l'heure. Les seuils de 3 et 10 jours de la notification ne s'y appliquent toujours pas — un partage est un geste volontaire, pas une interruption — mais l'application doit avoir servi.
- La date du dernier essai est écrite en heure locale, comme toutes ses comparaisons. En UTC, elle enregistrait la veille pour toute sollicitation affichée entre minuit et 2 heures locales, et le plancher de 7 jours du second essai s'ouvrait un jour trop tôt. Elle est maintenant fournie par l'appelant, qui a déjà calculé sa date.
- La relance du lancement automatique s'éteint aussi depuis Paramètres et depuis l'accueil, et plus seulement depuis le menu de la zone de notification. Activer puis désactiver le lancement automatique ailleurs que dans le menu laissait la relance armée, et l'application proposait deux jours plus tard d'activer ce que l'utilisateur venait de couper. Seul un changement réel de la case compte : refermer Paramètres sans y toucher n'est pas un choix.
- Le manifeste de packaging déclare enfin l'activateur COM de toast — `com:ComServer` et `windows.toastNotificationActivation` — que `ToastActivation.cs` exigeait en commentaire depuis sa création sans qu'aucune déclaration n'existe. Sans elle, Windows relance l'exécutable au clic sur un toast au lieu de livrer l'activation au processus vivant.
- Le test censé garantir cette déclaration comparait deux chaînes codées en dur sans jamais ouvrir le manifeste : il est resté vert pendant toute l'absence. Il lit désormais le fichier livré au packaging, et deux témoins prouvent qu'il échoue sur un manifeste sans déclaration comme sur un CLSID divergent.
- La règle de décision du chemin partage vit dans `ReviewSharePrompt`, pure et testable sans fenêtre, sur le modèle d'`AutoStartNudge`.

**Revue de release 1.2.0**

- Version applicative portée de `1.1.2` à `1.2.0`, manifeste de packaging de `1.0.0.0` à `1.2.0.0` (le Store sert `1.1.0.0`). `Program.cs` et `AssemblyInfo.cs` étaient restés en `1.1.2` : l'infobulle du tray et les rapports de bug se seraient annoncés en 1.1.2, et `Verify-Release.ps1` bloquait dessus.
- 282 tests xUnit passent sur les trois suites — 169 applicatifs, 95 moteur Windows, 18 moteur portable. Le chiffre de 250 annoncé le 17 août avait été figé avant huit tests ajoutés le même jour au parseur de disposition : le total réel était déjà de 258. Les 24 suivants viennent de l'audit de release et de ses correctifs — lots d'émission des touches mortes, touche morte orpheline, décision de sollicitation après partage, et lecture réelle du manifeste par le test de l'activateur COM. Build Release : 0 avertissement, 0 erreur.
- Quatre tests comparent `Program.Version` aux attributs d'assembly à chaque exécution de la suite. La dérive qui a bloqué les portes de release ce jour-là ne se voyait que dans `Verify-Release.ps1`, une levée à la fois et au moment du packaging ; elle apparaît désormais en CI, avant. Le csproj et `AppxManifest.xml` restent couverts par le script, hors de portée d'un test qui ne connaît que l'assembly compilé.
- Aucun package MSIX n'a été produit et aucune publication n'a été effectuée.

## Version 1.1.0 — 23 juillet 2026

Publiée sur le Microsoft Store en `1.1.0.0` (révision Store `2026-07-23T22:11:18Z`). Le package a été produit hors de ce dépôt : aucun tag ni commit de release ne lui correspond ici, et le code n'a rejoint la branche canonique qu'avec le commit `452aab0` du 2026-08-15. Entrée reconstituée le 2026-08-16 à partir des notes de version publiées (FR et EN), du code réconcilié et de l'inspection de l'application installée depuis le Store, pas d'un diff de release.

**Interface bilingue**

- Interface complète en anglais, en plus du français.
- Changement de langue à chaud depuis la fenêtre de bienvenue, le menu de la zone de notification ou les Paramètres.

**Statistiques locales**

- Nouvelle fenêtre « Mes statistiques » : jours d'utilisation, séries, temps de frappe actif et caractères spéciaux produits avec AZERTY Global.
- Calcul et stockage exclusivement sur l'appareil, sans télémétrie réseau. Aucune frappe ni aucun texte n'est enregistré ni transmis.
- Bouton de copie volontaire d'un résumé lisible des statistiques dans le presse-papiers.

**Avis et retours**

- Accès direct aux avis, aux retours et à la communauté depuis l'application.
- Sollicitation d'avis unique 7 jours après le premier lancement (`MaybeShowReviewPrompt`), cible tirée à 50/50 entre le volet d'avis du Store et la page feedback du site, marquée comme faite dès l'affichage. Vérifié sur une installation Store 1.1.0.0 : `firstRunTimestamp` et `reviewPromptDone` sont écrits dans `config.json`.

**Recherche de caractères**

- Prise en charge des noms anglais et de nombreux alias supplémentaires liés aux langues.

**Fiabilité**

- Comportement fiabilisé après une mise en veille ou une session de bureau à distance.
- Messages d'erreur plus clairs et correctifs d'interface divers.

## Version 1.0.0 — 29 juin 2026

Version stable de lancement Microsoft Store. Cette version reprend la RC interne `0.12.0`, validée le 2026-06-26, puis reconstruite et publiée en `1.0.0` le 2026-06-29. Le MSIX hors Store signé AMCF reste un livrable séparé.

**Module Leçons**

- Ajout d'une fenêtre `Leçons` accessible depuis le tray, avec catalogue embarqué `lessons.json`, progression locale et mode libre non persistant.
- Ajout d'un renderer clavier commun utilisé par le clavier virtuel, l'onboarding et les leçons.
- Ajout du moteur `LessonTypingSession`, du chargement de catalogue, des indices et du stockage `lessons-progress.json`.

**Correctifs pré-test manuel**

- Les leçons reprennent la gestion `WM_SYSCHAR` / `WM_SYSKEYDOWN` et la résolution positionnelle déjà utilisée par l'onboarding pour les dispositions sous-jacentes non-AZERTY.
- Le pass-through des lettres identiques reste actif même avec Verr. Maj. afin de préserver les raccourcis applicatifs comme `K` sur YouTube.
- Les échecs de sauvegarde de `config.json` sont maintenant journalisés.

**Validation Store 1.0.0**

- Bundle Store 1.0.0.0 reconstruit le 2026-06-29 (`msix/AZERTYGlobal-1.0.0.0.msixbundle`) pour x64 + ARM64.
- `scripts/Verify-Release.ps1` PASS : hashes des exécutables publiés identiques aux exécutables embarqués dans le bundle.
- WACK 1.0.0 PASS ; `DPIAwarenessValidation` PASS, optional `Blocked executables` non bloquant conservé.
- Package accepté et publié par Microsoft le 2026-06-29.
- SHA-256 du bundle Store : `E6BC370052CDFF26F8F3C6BD2526C338A749B67A2F48BE24B175C71C672C9855`.

## Version 0.11.2 — 3 juin 2026

**Exercice de typographie**

- Phrase de l'exercice 4 remplacée par : `Lætitia demande « d'où vient ce chef-d'œuvre… » — elle l'approuve à 100 %.`

**Correctifs pré-publication Store**

- Mode compatibilité jeux : les combos natives utilisent désormais de vrais événements scancode (`KEYEVENTF_SCANCODE`) pour les applications qui bindent les touches physiquement.
- Désactivation anti-cheat : la notification de sécurité reste affichée même si les notifications standard sont désactivées.
- Journaux locaux : anonymisation du nom de process dans le log debug compat et suppression du chemin complet `learning-tweaks.json`.

## Version 0.11.1 — 28 mai 2026

**Correctif dispositions système non-AZERTY**

- Correction du pass-through clavier quand la fenêtre cible utilise une disposition système non-AZERTY, notamment QWERTY US.
- Les touches physiques restent pilotées par scancode : `D01` produit bien `a` au lieu de laisser passer `q`, et `E01` produit bien `&` au lieu de laisser passer `1`.
- Le pass-through reste conservé quand la disposition de la fenêtre cible produit déjà le bon caractère.
- Correction associée pour les raccourcis `Ctrl+touche` : `Ctrl+D01` sous QWERTY envoie bien `Ctrl+A`, pas `Ctrl+Q`.

## Version 0.11.0 — 20 mai 2026

**Synchronisation avec la disposition actuelle**

- Ressources embarquées synchronisées avec la disposition actuelle : `AZERTY Global 2026.json` et `character-index.json`.
- Mise à jour des raccourcis : `#` en alternative développeur sur AltGr + :, `^` sur AltGr + I, backtick vif sur AltGr + L, Latin étendu sur AltGr + 6, tiret insécable sur Maj + AltGr + 6.
- Espaces insécables alignées : espace fine insécable sur AltGr + Espace, espace insécable sur Maj + AltGr + Espace.
- Recherche de caractères mise à jour avec 1034 entrées d'index, dont 1005 caractères Unicode et 29 touches mortes.
- Ajout d'un script durable de synchronisation des ressources depuis le site, avec validation des raccourcis critiques.

## Version 0.10.0 — 8 mai 2026

**Audit sécurité indépendant**

- Hardening binaire : Control Flow Guard (CFG) activé sur les binaires AOT x64 et ARM64. Build déterministe explicite.
- Robustesse renforcée : gestion d'erreurs défensive sur le hook clavier (try/catch sur le callback bas niveau) et les allocations mémoire natives (try/finally sur 5 sites `Marshal.AllocHGlobal`).
- Privacy : logs locaux désormais limités (pas de stack traces complètes ni de paths utilisateur dans `error.log`) et noms de process anonymisés via HMAC-SHA256 dans les events critiques de compatibilité.
- Isolation hook : marker d'injection randomisé au démarrage (au lieu d'une valeur fixe), mutex d'instance unique préfixé `Local\` + suffixé SID utilisateur (anti-squat).
- CI GitHub Actions ajoutée (build reproductible x64+ARM64 + tests + Pack-MSIX + Verify-Release + BinSkim hardening + attestation SLSA L1).
- Hygiène repo : suppression d'un fichier doublon `OnboardingWindow (# Name clash...)` issu d'un conflit de sync Proton Drive.

Aucun changement fonctionnel utilisateur visible. Audit complet : `Archives/audits/2026-05/reports/AUDIT-SECURITY-v0.10.0.md`.

## Version 0.9.8 — 5 mai 2026

**Menu tray — entrée « Exercices »**

- Nouvelle entrée `Exercices` dans le menu de la zone de notification (entre `Rechercher un caractère` et le séparateur). Ouvre le `LearningModule` en mode replay : démarre toujours à l'exercice 1, parcourt les 4 exercices normaux puis la page de choix avant les 2 exercices bonus, comme l'onboarding initial.
- Mode replay sans side-effect sur la progression : la valeur `learningMaxStepCompleted` du fichier de configuration n'est jamais modifiée par cette voie. La progression sauvegardée reste celle du premier passage onboarding.
- Garde-fou : si la fenêtre d'onboarding est en cours d'utilisation OU si une autre instance de `LearningModule` est déjà ouverte, le clic est ignoré (no-op) — pas de doublon d'instance.

**Notification toggle — suppression du doublon**

- Suppression de la balloon Windows (zone de notification, en bas à droite) lors des bascules `Ctrl+Maj+Verr.Maj` : elle faisait doublon avec la mini-fenêtre flottante en haut à droite (`ToggleNotification`, ajoutée en v0.9.7) qui était déjà plus visible et plus lisible. Reste désormais seule la fenêtre flottante.
- La balloon de démarrage de l'app (rappel du raccourci `Ctrl+Maj+Verr.Maj` au lancement) est conservée — elle a un rôle pédagogique différent.

## Version 0.9.7 — Avril-Mai 2026

**Caps Lock — refonte complète (smoke test in-game, mai 2026)**

- **Désynchronisation entre l'état Caps Lock interne et Windows** corrigée : la frappe `Caps Lock + lettre` puis lancement d'un exercice produisait des majuscules permanentes à cause d'un état désynchros. `_capsLockState` est désormais resynchronisé avec `GetKeyState(0x14)` à chaque frappe non-modifier (`KeyMapper.ProcessKey`), et `RequestCapsLockOff` vérifie l'état Windows réel avant de toggler.
- **Modificateurs Shift/Ctrl/Alt résiduels** corrigés : si l'application est lancée pendant qu'un jeu tient des touches (ex. Maj pour sprinter), le keydown initial était manqué et des frappes ultérieures sortaient en majuscule. `SyncState` appelle désormais `CleanupStaleModifiers()` ; `SyncState` lui-même est invoqué au démarrage de l'app et au retour de focus du LearningModule.
- **Suppression du toggle Caps Lock physique** dans `BuildVkComboInputs` (mode NativeCombo) : auparavant chaque frappe en Caps Lock ON injectait `VK_CAPITAL down/up` deux fois, ce qui spammait la notification Windows « Verr. Maj. activé/désactivé » dans Minecraft, Trackmania, etc. Désormais on inverse logiquement `needsShift` (Caps Lock + Shift s'annulent côté Windows) — sans toucher physiquement à Caps Lock.
- **Détection dynamique « Caps Lock affecte ce VK ? »** via `ToUnicodeEx` (avec/sans état Caps Lock simulé, flags=1 sans consommer le dead-key state). Cache par `(vk, hkl)`. Couvre exactement les touches affectées (lettres A-Z, rangée numérique, ponctuation OEM en AZERTY) et exclut celles qui ne le sont pas (VK_OEM_102 `<>`). Bug `<` qui devenait `>` en Caps Lock corrigé.

**Touches mortes natives — fallback Alt+code**

- Les caractères qui sont eux-mêmes des dead keys sur le layout natif (`^` `¨` `~` `` ` `` en AZERTY traditionnel) faisaient entrer Windows en mode dead-key lors de l'injection en mode NativeCombo, ce qui consommait le caractère sans l'afficher (workaround Tab nécessaire dans Trackmania). `BuildNativeComboInputs` détecte désormais via `IsDeadKeyOnLayout` (`ToUnicodeEx` renvoie -1) et fait fallback automatique sur Alt+code, qui bypass complètement le système dead-key Windows.

**Compatibilité — détection foreground**

- `ForegroundMonitor.Recompute` ignore désormais les transitions vers `explorer.exe`, `SearchHost.exe`, `StartMenuExperienceHost.exe`, `ShellExperienceHost.exe`, `TextInputHost.exe` — effets de bord du clic sur l'icône tray ou de la touche Windows. Sans ce filtre, le sous-menu « Compatibilité » affichait `SearchHost.exe` ou `explorer.exe` au lieu du jeu réel.
- Le PID de notre propre application n'est plus ignoré : quand la fenêtre du LearningModule prend le focus, le mode redevient correctement `Default` (au lieu d'hériter d'un `NativeCombo` parasite d'un jeu antérieur), ce qui empêche l'AltGr+N (`~`) d'être consommé en mode dead-key dans nos exercices.
- Sous-menu « Compatibilité » dans le menu tray filtré quand le foreground est notre propre app (plus d'item « Compatibilité — AZERTY Global.exe »).

**Retour visuel pendant les jeux fullscreen — `ToggleNotification`**

- Nouvelle mini-fenêtre TOPMOST en haut à droite (240×56 px logiques, opacité ~94 %, auto-fermeture 2 s) qui affiche « AZERTY Global activé » (vert) ou « AZERTY Global désactivé » (gris) à chaque toggle via `Ctrl+Maj+Verr.Maj`. Permet de voir l'état du remapping en borderless windowed quand l'icône de la zone de notification est cachée par le jeu. Angle mort accepté en exclusive fullscreen.
- **Garde anti-cheat** : la fenêtre TOPMOST ne s'affiche **jamais** quand un process protégé par anti-cheat kernel-level est au foreground (Valorant, Fortnite, CoD, etc.). Évite tout risque qu'un anti-cheat scanne l'overlay et le flagge comme cheat tiers.

**LearningModule — finitions**

- Le module force `RequestCapsLockOff()` à l'ouverture : tous les exercices commencent désormais avec Caps Lock désactivé, peu importe l'état hérité du contexte extérieur. Empêche que l'utilisateur arrive sur l'exo 1 « Activez Verr. Maj. » avec Verr. Maj. déjà actif.
- Suffixe « (Bonus) » en doré-orangé (`#E29400`) à la suite du titre des exercices facultatifs (ex5, ex6) — remplace l'ancienne pill orange peu lisible. La couleur dorée évite la confusion avec le vert utilisé pour la progression.
- Page de choix fin d'exercice : navigation par flèches (`←` / `↑` = Recommencer ; `→` / `↓` = Suivant ; `Esc` = Quitter).
- Écran final « Bravo ! » : flèches `→` / `↓` + `Esc` ferment la fenêtre (équivalent au bouton Terminer). Bouton Terminer repositionné — aligné à droite (largeur 140 px), juste au-dessus du clavier, pour ne plus chevaucher le sous-titre « Vous maîtrisez les bases d'AZERTY Global. ».
- Tooltip de la touche Backspace désactivée passé sur 2 lignes pour la lisibilité.
- Masquage des caractères secondaires peu utilisés sur le clavier virtuel des exercices (point en chef, point souscrit, double aigu, double grave, corne, crochet, brève, brève inversée, barre oblique/horizontale, macron, latin étendu, cédille, virgule souscrite, alphabet phonétique, rond en chef, symboles scientifiques, caron, ogonek, alphabet cyrillique, symboles divers `→`, guillemet-apostrophe ouvrant, soft hyphen, arobase alternatif sur AltGr+E10, guillemets doubles `“ ”`). Ces caractères restent visibles dans le **tooltip de chaque touche** au survol.
- Tooltips uniformisés : tous les noms de caractères et de touches mortes sont en MAJUSCULES (cohérence visuelle). Format des dead keys : `TOUCHE MORTE + nom` (ex. « TOUCHE MORTE SYMBOLES DIVERS » au lieu de « FLÈCHE VERS LA DROITE »). Override pour `’` qui s'affiche désormais comme « APOSTROPHE TYPOGRAPHIQUE » (au lieu du nom Unicode officiel « GUILLEMET-APOSTROPHE FERMANT »).

**Wizard d'accueil — finitions UX**

- Étape 1 : libellé du bandeau passé de « Version bêta » à « **Phase de tests** » + point ajouté après « donnez votre avis ». Espacement entre le titre « Votre clavier est maintenant amélioré » et la barre de progression réduit (24→12 px).
- Étape 1 : la phrase rassurante « Cette application améliore votre clavier. Aucune frappe n'est enregistrée ni transmise. » est désormais sur une seule ligne avec une fonte dédiée à scaling proportionnel calibré (`-(int)Math.Round(17 * dpiScale / 1.75)` — 10 px à 100 % DPI, 17 px à 175 % DPI).
- Hauteur de la fenêtre wizard réduite de 810 → 770 px (compacité).
- Étape 3 : checkboxes « Lancer au démarrage de Windows » et « Ne plus afficher cet écran au démarrage » désormais cochées par défaut à chaque ouverture (recommandation). Lien « Donner son avis sur la bêta » renommé « Donner son avis sur AZERTY Global » (cohérence avec le menu tray).
- Navigation par flèches sur les 3 étapes (sous-classe `ButtonArrowSubclassProc` sur les boutons Next/Prev/Try) : `↓` / `→` = bouton principal de l'étape (Essayer maintenant / Suivant / C'est parti) ; `↑` / `←` = bouton Précédent (étapes 2 et 3) ; `Esc` = fermer.

**AboutWindow — refonte**

- Hauteur réduite de 320 → 230 px, largeur passée de 420 → 500 px pour faire tenir les liens.
- Description simplifiée : « Disposition clavier améliorée pour les francophones. »
- Ligne « Édité par l'AMCF » + ligne secondaire fusionnées en une seule : « Édité par l'**Association pour la Modernisation du Clavier Français (AMCF)** » avec le nom complet en lien cliquable vers la page HelloAsso de l'association.
- Lien « azerty.global » renommé « Site web ». Lien « Licence EUPL 1.2 » enrichi en « Licence EUPL 1.2 (open source) » — la mention « Licence : EUPL 1.2 (open source) » au-dessus est supprimée (redondance).

**LayoutConflictWindow — wording**

- Mention de la suppression de la disposition système reformulée pour être indépendante de la langue de Windows : « Enlève AZERTY Global de la liste des dispositions chargées dans les options de langue (Paramètres Windows → Heure et langue → Langue → Options de la langue concernée). » (au lieu de « désinstalle le pack « Français — AZERTY Global » »).

**Tutoiement / vouvoiement — stratégie**

- L'OnboardingWindow et le LearningModule (sas d'accueil) **vouvoient** l'utilisateur, en cohérence avec le site web public.
- Toutes les autres fenêtres et messages (LayoutConflictWindow, SettingsWindow, TrayApplication notifications, AutoStart erreurs) **tutoient** l'utilisateur.

**Settings — libellés**

- « Notifications (activé / désactivé) » → « Notifications ».
- « Lancer au démarrage de Windows (recommandé) » → « Lancer au démarrage de Windows ».
- « Afficher la fenêtre de bienvenue au démarrage » → « Fenêtre de bienvenue au démarrage ».
- MessageBox de confirmation « Réinitialiser raccourcis » réécrite sur 2 lignes pour rendre la boîte plus compacte.

**Menu tray — corrections**

- L'item « Donner son avis sur AZERTY Global » pointe désormais vers `https://azerty.global/beta` (au lieu de `/feedback`) tant que la phase de retours est en cours.

**Tests automatisés**

- Le test `BuildVkComboInputs_CapsLockActive_AndShiftCombo_TogglesCapsAround` qui validait l'ancien comportement (toggle Caps Lock physique) a été remplacé par `BuildVkComboInputs_CapsLockActive_NoPhysicalToggle` qui valide l'absence d'event `VK_CAPITAL` injecté.
- 77/77 tests xUnit passent. Build Release AOT x64 + ARM64 : 0 warning, 0 error.

---

**Wizard d'accueil — affichage conditionnel et choix utilisateur**

- Le wizard d'accueil ne s'affiche plus systématiquement à chaque démarrage : il reste affiché tant que les 3 premiers exercices n'ont pas tous été complétés. Une fois ces 3 exercices validés, l'application démarre directement en arrière-plan avec une bulle de notification discrète.
- Nouvelle option dans Paramètres : « Afficher la fenêtre de bienvenue au démarrage » — permet à l'utilisateur de désactiver manuellement le wizard à tout moment, même si les exercices ne sont pas terminés.
- État de progression persisté dans la configuration utilisateur (`learningMaxStepCompleted`) pour traverser les redémarrages.
- L'étape 1 du wizard reste au premier plan (`topmost`) pour maximiser la visibilité des 5 améliorations. Les étapes 2 et 3 ne le sont plus, pour permettre de consulter les ressources mentionnées (guide, Discord, bêta) en parallèle d'un navigateur.

**Menu de la zone de notification — réorganisation**

- Nouvelle entrée « À propos » en dessous des Paramètres : ouvre une mini-fenêtre custom avec version, licence EUPL 1.2, mention de l'AMCF, et 3 liens cliquables (site, code source GitHub, licence).
- Sous-menu « Compatibilité « process » » déplacé sous « À propos ». Le séparateur qui le suivait n'apparaît plus quand aucun process foreground n'est détecté (plus de séparateur orphelin).
- Libellé « Signaler un bug » enrichi en « Signaler un bug (version + OS) » pour clarifier les données techniques transmises au support.

**Conflit avec disposition système AZERTY Global — popup éclairée**

- Si l'application détecte qu'une disposition système AZERTY Global est déjà installée, elle ouvre désormais une mini-fenêtre custom (au lieu d'une `MessageBox` standard) qui présente le trade-off entre les deux solutions :
  1. Garder la disposition système (nécessaire pour taper avec AZERTY Global avant le login Windows : mot de passe, écran de verrouillage, UAC, BitLocker)
  2. Garder l'application (clavier virtuel et recherche de caractère, plus user-friendly post-login)
- Si la fenêtre de bienvenue devait s'afficher au démarrage, son ouverture est différée jusqu'à ce que l'utilisateur ait choisi « Garder l'application ». Évite que le wizard recouvre la mini-fenêtre d'explication.

**Refonte du mini-onboarding**

- Bouton « Essayer maintenant » : largeur dimensionnée dynamiquement selon le texte (corrige la troncature visible « ssayer maintenar »).
- Instruction des exercices : passage à un gris foncé `#404040` (contraste ~9:1) pour une lisibilité nette sur fond clair.
- Mention de confidentialité ajoutée à l'étape 1 : « Cette application améliore votre clavier. Aucune frappe n'est enregistrée ni transmise. »
- Comportement « Essayer maintenant » revu : on reste sur l'étape 1 ; le bouton se transforme en « Suivant » à la sortie des exercices (au lieu d'avancer silencieusement à l'étape 2).
- Fenêtre wizard agrandie de 750 → 810 px de hauteur pour absorber la nouvelle mention.

**Module d'apprentissage**

- Renommage « Étape 1/6 » → « Exercice 1/6 » pour distinguer du wizard d'accueil 3 étapes.
- Exercices 5 et 6 (facultatifs) : pill « Bonus » à côté du titre pour signaler qu'ils sont skippables.
- Renommage du bouton « Passer cette étape » → « Passer cet exercice ».
- Page de fin enrichie : titre « Bravo ! » en grande police orange + sous-titre « Vous maîtrisez les bases d'AZERTY Global. ».
- Légende du clavier en bas : « Maj. — Verr. Maj. — AltGr — Touche morte » avec leurs codes couleur respectifs.
- Caractères AltGr du clavier des exercices désormais en bleu accent (cohérence avec le testeur du site web).
- Reformulation des instructions des exercices 1 et 2 pour être plus explicites :
  - Exercice 1 : « Activez Verr. Maj. puis tapez sur la lettre é »
  - Exercice 2 : « Gardez le Verrouillage Majuscule activé pour taper cette phrase »
- La touche Backspace est désormais grisée pendant les exercices avec un tooltip dédié au survol : « Désactivé pendant les exercices — continue de taper, l'erreur se corrige toute seule ». Évite la confusion quand l'utilisateur appuie par réflexe sur Retour arrière après une erreur.

**Wizard d'accueil — étape 3 simplifiée**

- Retrait du lien « S'entraîner avec les leçons de frappe » (doublon avec les exercices intégrés).
- Retrait de la note d'avertissement « Le testeur en ligne nécessite de désactiver temporairement l'application. » (jugée disruptive).
- Conservation des liens Guide, Bêta et Discord.

**Bugs corrigés**

- Couleur des touches mortes (`CLR_DK_RESULT`) : corrigée d'une valeur hex à 9 chiffres invalide vers le vert intentionné `#339900`.

**Compatibilité jeux**

Refonte majeure de la couche d'injection pour résoudre les problèmes de compatibilité avec les jeux qui filtrent les frappes synthétiques (Minecraft Java, mods comme JEI, jeux Unity, SDL, GLFW…).

- **Saut impossible en sprint** (Maj+Z+Espace dans Minecraft) : la barre d'espace est désormais en pass-through même quand Shift est maintenu, puisque sa sortie ne dépend pas du Shift. Le jeu reçoit un vrai `WM_KEYDOWN VK_SPACE`.
- **« Touche fantôme »** après usage du raccourci `Ctrl+Maj+Verr.Maj` pendant qu'une touche était maintenue (personnage continuant à avancer ou aller à gauche dans les jeux) : un keyup synthétique est désormais émis pour chaque touche en pass-through avant de purger l'état interne, évitant que l'app cible ne perçoive la touche comme toujours enfoncée.
- **`Ctrl + lettre` dans les jeux qui bindent par position physique** (Minecraft via GLFW, SDL, DirectInput) : si la touche physique correspond déjà au bon VK natif, on laisse passer la frappe d'origine au lieu d'injecter une touche synthétique. Corrige `Ctrl+A` (drop d'item dans l'inventaire Minecraft).
- **Combo native pour les caractères injectés en jeu** : quand un jeu compatible (Minecraft, Trackmania, jeux Unity, SDL, etc.) est au premier plan, les caractères AZERTY Global (`@`, `#`, accents, guillemets typo) sont désormais injectés via une combinaison de touches natives du clavier sous-jacent. Marche dans les chats et tous les champs de saisie modés (notamment la recherche d'items JEI dans Minecraft, qui était cassée auparavant).
- **Alt+code automatique pour les caractères inaccessibles** sur le layout natif (`É`, `«»`, `–`, `œ`, etc.) : injection via la séquence `Alt+0XXX` du Numpad pour permettre la frappe en jeu sans perdre la fonctionnalité Smart Caps Lock ni les guillemets typographiques.
- **Désactivation automatique sur jeux protégés par anti-cheat kernel-level** (Valorant, League of Legends, Fortnite, Apex Legends, Call of Duty, R6 Siege, PUBG, Tarkov, Genshin Impact, Honkai Star Rail, Roblox, FACEIT, Battlefield 2042, The Finals, Counter-Strike 2, Marvel Rivals, Helldivers 2, etc.) : AZERTY Global se met automatiquement en pause à l'ouverture du jeu pour éviter tout risque de bannissement, avec une bulle d'information ; réactivation automatique à la fermeture.
- **Le raccourci `Ctrl+Maj+Verr.Maj` est désormais refusé pendant la désactivation auto anti-cheat** : tant qu'un jeu protégé est au premier plan, l'utilisateur ne peut pas réactiver AZERTY Global, même via raccourci. Une bulle de sécurité explique le refus. Évite les bannissements accidentels.
- **Sous-menu de compatibilité par application** dans le menu de la zone de notification : permet de forcer la compatibilité jeu, ou la désactivation totale, pour une application précise détectée au premier plan. La désactivation utilisateur sur un process protégé par anti-cheat est refusée par sécurité.
- **Fonctionnement correct en RDP, VPN et applications qui simulent AltGr via `Ctrl+Alt`** : la séquence Alt+code utilisée pour injecter les caractères inaccessibles (`É`, `«»`, `–`, `œ`…) relâche désormais correctement les modificateurs physiques tenus dans ce mode. Auparavant l'application cible recevait `Ctrl+Alt+0XXX` au lieu de `Alt+0XXX`, ce qui pouvait déclencher des raccourcis au lieu de produire le caractère.

**Architecture interne**

- Refonte modulaire : nouvelle couche `IWin32Api` permettant l'injection de dépendances et facilitant la maintenance future.
- Suite de tests automatisés (~70 tests xUnit) couvrant la liste anti-cheat, la persistance des overrides utilisateur, la détection de mode, et la construction des séquences d'injection (combo native, Alt+code, fallback Unicode).
- Rotation automatique du journal d'erreurs à 5 Mo (au lieu de la troncature à 1 Mo précédente).

**Outils internes (build DEBUG uniquement)**

- Nouvelle entrée dans le menu tray « 🛠 Réinitialiser onboarding » pour faciliter les tests visuels du parcours.

## Version 0.9.6 — Avril 2026

**Consolidation**
- Audit complet de l'architecture et du code (16 fichiers, ~7 500 lignes).
- Aucun bug bloquant identifié — version de consolidation sans changement fonctionnel.

## Version 0.9.5 — Avril 2026

**Fiabilité de la publication Store**
- Alignement des métadonnées de release sur `0.9.5` côté application et `0.9.5.0` côté package Store.
- Ajout d'un contrôle de cohérence pour la chaîne `publish -> msix -> documentation`.

**Lancement automatique plus fiable**
- Les fenêtres de paramètres et d'accueil relisent désormais l'état réel de Windows au lieu d'un cache local.
- Les messages d'erreur distinguent correctement le mode MSIX du mode non packagé.

**Recherche de caractère**
- La copie dans le presse-papiers ne signale plus un succès sans validation réelle de `SetClipboardData`.
- La fenêtre gère maintenant les changements de DPI en recalculant polices, layout et taille.

**Robustesse interne**
- Le hook clavier peut être réinstallé sans fenêtre de coupure visible.
- Les composants auxiliaires (`CharacterSearch`, `VirtualKeyboard`) n'empêchent plus le remapping de démarrer s'ils échouent isolément.
- Nettoyage des `JsonDocument` temporaires et protection du log fatal contre les erreurs d'écriture.

## Version 0.9 — Mars 2026

**Démarrage automatique avec Windows**
- AZERTY Global peut maintenant se lancer automatiquement au démarrage de Windows, sans droits administrateur et sans modifier le registre.

**Raccourcis clavier personnalisables**
- Les raccourcis pour ouvrir le clavier virtuel et la recherche de caractère sont désormais configurables pour éviter les conflits avec vos autres applications.

**Meilleure compatibilité**
- Correction d'un problème où certaines touches mortes de l'AZERTY traditionnel pouvaient interférer avec la saisie.
- Les touches de modification (Maj, AltGr) ne restent plus « bloquées » dans de rares cas.

**Préparation Microsoft Store**
- AZERTY Global sera bientôt disponible sur le Microsoft Store pour une installation encore plus simple.

---

## Version 0.8 — Mars 2026

**Recherche de caractère**
- Nouveau : trouvez n'importe quel caractère en tapant son nom en français (« e accent aigu », « euro », « tiret cadratin »…) ou en collant directement le caractère recherché.
- Le résultat indique clairement la combinaison de touches à utiliser (ex : AltGr + E → €).
- La recherche surligne automatiquement les touches correspondantes sur le clavier virtuel.

---

## Version 0.7 — Mars 2026

**Clavier virtuel**
- Nouveau : un clavier virtuel affiche en temps réel les caractères disponibles selon les touches enfoncées (Maj, AltGr, Verrouillage Majuscule).
- Le clavier s'adapte quand vous appuyez sur une touche morte pour montrer les caractères accentués possibles.
- Fenêtre redimensionnable, repositionnable et toujours visible si vous le souhaitez.

**Écran d'accueil**
- Au premier lancement, un écran d'accueil explique les bases : AZERTY Global est actif, l'icône est dans la barre des tâches, et un raccourci clavier ouvre le clavier virtuel.

**Menu amélioré**
- L'icône dans la barre des tâches donne accès au clavier virtuel, à la recherche de caractère et au site azerty.global.

---

## Version 0.6 — Mars 2026

**Premier exécutable autonome**
- AZERTY Global est désormais un fichier unique qui fonctionne sans installation ni dépendance.

---

## Version 0.5 — Mars 2026

**Première version**
- Prise en charge complète de la disposition AZERTY Global 2026 avec ses 8 couches de caractères.
- Verrouillage Majuscule intelligent : n'affecte que les lettres, pas les chiffres ni les symboles.
- Icône dans la barre des tâches pour activer ou quitter le programme.
- Une seule instance peut tourner à la fois.

---

*Dernière mise à jour : 2026-08-18*
