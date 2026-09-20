# Base — ce que les deux audits précédents ont établi

Lecture seule, le 2026-09-20. Dépôt : `D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store\`.
Branche lue : `release/1.2.0-notation-store`, HEAD `4d5da24` (`docs(changelog): consigner le rattrapage d avis et les quatre ports de la 1.3.0`).
`git status --porcelain` : deux entrées non suivies seulement (`?? Archives/`, `?? docs/audit-2026-09-15-v1.2.0/test-results-baseline-sdk8.0.423/`). Aucune écriture, aucune commande git mutante.

Tous les chemins ci-dessous sont relatifs à cette racine, sauf indication contraire.

---

## A. Findings de l'audit du 2026-09-15

Source primaire : `docs/audit-2026-09-15-v1.2.0/rapport.md` (268 lignes, révision auditée `8775489fc1170e31e7e08a2a2d9a9fa77f5a21ea`).
Source du statut : `docs/audit-2026-09-15-v1.2.0/corrections.md` (82 lignes, 15/09), puis la campagne VM et les quatre documents du 19/09.

### A.1 — Les onze constats numérotés

| # | id / titre | fichier:ligne de la source | citation (≤ 2 lignes) | statut déclaré et où |
|---|---|---|---|---|
| 1 | AG120-01 — Les sondes de touches mortes utilisent un mauvais paramètre Windows | `docs/audit-2026-09-15-v1.2.0/rapport.md:39` puis `:43` | « `IsDeadKeyOnLayout` appelle `ToUnicodeEx` avec `0x01`. Les deux sondes de `DoesCapsLockAffectVk` utilisent le même paramètre. » | **Corrigé** — `corrections.md:11` : « Les trois sondes de touches mortes et de Verr. Maj. utilisent le paramètre non mutateur 0x04. » Vérifié en VM partiellement : `recette-vm-resultats.md:55` (VM-05 **PARTIEL**) |
| 2 | AG120-02 — Une touche remappée peut perdre son relâchement pendant un raccourci | `rapport.md:50` puis `:54` | « Les sorties anticipées pour Alt et Windows précèdent `TryReleaseSyntheticVirtualKey`. » | **Corrigé** — `corrections.md:12` : « Le moteur relâche les touches virtuelles qu'il a pressées avant les sorties Alt/Windows. » VM-06 **VERT**, `recette-vm-resultats.md:56` |
| 3 | AG120-03 — Le mode de compatibilité neutralise Maj gauche même si Maj droite est tenue | `rapport.md:61` puis `:65` | « `hasShift` agrège les deux touches Maj, mais `BuildVkComboInputs` et `BuildAltCodeInputs` relâchent puis restaurent uniquement `VK_LSHIFT`. » | **Corrigé** — `corrections.md:13` : « Les émissions natives et Alt+code neutralisent puis restaurent Maj gauche et Maj droite séparément. » VM-07 **PARTIEL**, `recette-vm-resultats.md:57` |
| 4 | AG120-04 — Décocher une couche ne supprime pas ses verrous existants | `rapport.md:72` puis `:76` | « `ApplySettings` reconstruit la liste des couches autorisées, mais ne vide les verrous que lorsque l'interrupteur général passe à faux. » | **Corrigé** — `corrections.md:14` : « Décocher une couche purge ses verrous, états ponctuels, appuis en attente et historique de double appui ». VM-09 **VERT**, `recette-vm-resultats.md:59` |
| 5 | AG120-05 — Les fenêtres du shell héritent du contexte de l'application précédente | `rapport.md:83` puis `:87` | « `ForegroundMonitor.Recompute` ignore notamment `explorer.exe` et `SearchHost.exe`. Il conserve tout le contexte » | **Corrigé** — `corrections.md:15` : « Le contexte de frappe suit la vraie fenêtre, y compris l'Explorateur et la recherche Windows. » VM-10 **VERT**, `recette-vm-resultats.md:60` |
| 6 | AG120-06 — La suspension ne couvre pas toutes les sorties synthétiques | `rapport.md:94` puis `:104` | « Le code ne démontre donc pas l'absence de toute injection vers une cible suspendue. » | **Corrigé** — `corrections.md:16` : « La pause et la suspension bloquent aussi l'insertion depuis la recherche, Verr. Maj. et le nettoyage synthétique. » VM-11 **VERT** (`:61`), VM-12 **PARTIEL** (`:62`) |
| 7 | AG120-07 — Un fichier de réglages JSON valide peut empêcher le démarrage | `rapport.md:110` puis `:114` | « `ConfigManager.EnsureLoaded` appelle `EnumerateObject()` sans vérifier que la racine est un objet. » | **Corrigé** — `corrections.md:17` : « Une configuration JSON dont la racine est un tableau, une valeur simple ou null est traitée comme illisible ; le fichier original est préservé. » ⛔ VM-14 **NON TESTÉ**, `recette-vm-resultats.md:64` |
| 8 | AG120-08 — Une insertion refusée peut être annoncée comme réussie | `rapport.md:121` puis `:125` | « `EmitText` compte le texte avant émission et ignore la valeur retournée par `SendInput`. » | **Corrigé** — `corrections.md:18` : « Le résultat distingue émission complète, nulle, partielle et bloquée. » VM-13 **VERT** (`:63`) ; ⛔ VM-23 (retours partiels) **NON TESTÉ** (`:73`) |
| 9 | AG120-09 — Une version inconnue du fichier de progression peut être écrasée | `rapport.md:132` puis `:136` | « `LessonProgressStore.Load` sort lorsqu'il rencontre une version différente de `CurrentVersion`, tout en laissant `_loadFailed` à faux. » | **Corrigé** — `corrections.md:19` : « Une progression de version absente, invalide ou inconnue reste intacte ». ⛔ VM-15 **NON TESTÉ**, `recette-vm-resultats.md:65` |
| 10 | AG120-10 — L'ancien paquet stable est archivé sous la version à construire | `rapport.md:231` puis `:235` | « Une fabrication 1.2.0 peut donc ranger un paquet 1.1.0 dans `by-version/1.2.0.0`, sous un nom 1.2.0.0. » | **Corrigé** — `corrections.md:20` : « L'archivage lit la vraie version du bundle précédent, crée une copie au nom unique, vérifie son SHA-256 et écrit sa preuve. » Test : `scripts/tests/test_archive_stable_bundle.py` |
| 11 | AG120-11 — Le masque d'icône de secours est trop court pour certaines tailles | `rapport.md:179` puis `:183` | « `CreateTextIcon` alloue `size * size / 8` octets pour son masque 1 bit. À 40 pixels, cela fait 200 octets. » | **Corrigé** — `corrections.md:21` : « Les deux chemins d'icône partagent un calcul de masque monochrome aligné sur 16 bits, notamment à 40 pixels. » ⛔ VM-19 **NON TESTÉ**, `recette-vm-resultats.md:69` |

### A.2 — Constats non numérotés du même rapport

| # | titre | fichier:ligne | citation (≤ 2 lignes) | statut déclaré et où |
|---|---|---|---|---|
| 12 | Porte supplémentaire — Vérifier les manifestes du paquet final | `rapport.md:239` puis `:241` | « `Verify-Release.ps1` … Il ne lit pas le manifeste du bundle ni les manifestes des MSIX internes. » | **Traité manuellement, contrôle non automatisé** — `manifestes-bundle-2026-09-19.md:112` : « `Verify-Release.ps1` n'ouvre toujours aucun manifeste : les contrôles ci-dessus sont manuels et ne se rejoueront pas tout seuls » |
| 13 | Sollicitation d'avis non réévaluée pendant une longue session | `rapport.md:142` puis `:144` | « `MaybeShowReviewPrompt` est appelé au démarrage ou à la fermeture de la fenêtre d'accueil. Il n'est pas réévalué périodiquement » | **Reporté, choix produit** — `corrections.md:67` : « L'amélioration de la sollicitation d'avis pendant une session longue reste un choix produit distinct ». ⛔ RET-09 **NON TESTÉ**, `recette-vm-resultats.md:87` |
| 14 | Un avis Store ne remplace pas un retour exploitable pour la v2 | `rapport.md:150` puis `:156` | « La campagne auprès des utilisateurs actuels doit être préparée séparément, après validation du paquet. » | **Ouvert / hors périmètre** — `corrections.md:69` : « L'application n'a pas été lancée interactivement ni installée ». RET-01 à RET-09 : toutes **NON TESTÉ**, `recette-vm-resultats.md:79-87` |
| 15 | Fiche Store — « éviter tout risque de bannissement » | `rapport.md:172` | « La fiche Store source emploie pourtant « éviter tout risque de bannissement » : remplacer cette garantie par une description limitée aux applications reconnues. » | **Corrigé** — `corrections.md:23` : « La promesse de détection générale du plein écran et celle d'absence de risque de bannissement ont été retirées. » |
| 16 | README — « détection des applications fullscreen » | `rapport.md:173` | « Le README parle de « détection des applications fullscreen » ; la lecture n'a pas trouvé de détecteur général de plein écran dans ce chemin. » | **Corrigé** — `corrections.md:23` (même phrase que le n°15) |
| 17 | Générateur App Installer — porte trop faible pour le canal AMCF | `rapport.md:257` | « Le générateur App Installer rejette l'éditeur Store, mais ne vérifie pas la signature ni l'éditeur AMCF attendu. » | **Ouvert** — aucune mention dans `corrections.md` ; `corrections.md:67` : « Le canal entreprise AMCF conserve ses contrôles propres de signature, d'éditeur et de publication. » |
| 18 | `msix/README.md` annonce un contrôle `.appinstaller` non implémenté | `rapport.md:258` | « La documentation dit que la vérification de release compare également le fichier `.appinstaller` au bundle ; ce n'est pas implémenté dans `Verify-Release.ps1`. » | **Corrigé (documentation)** — `corrections.md:23` : « La documentation AMCF indique le contrôle explicite réellement disponible pour comparer le fichier App Installer au bundle. » |
| 19 | CI — tests non exécutés sur ARM64, BinSkim en `continue-on-error` | `rapport.md:249` | « La CI publie x64 et ARM64 mais exécute ses trois projets de tests sur le runner Windows x64, sans exécution ARM64. » | **Ouvert** — `rebuild-sdk-8.0.425-2026-09-19.md:98` : « ⛔ Rappel : compiler pour ARM64 ne prouve pas l'exécution ARM64. » VM-21 **PARTIEL**, `recette-vm-resultats.md:71` |
| 20 | Aucun scan de vulnérabilités des dépendances | `rapport.md:251` | « Aucun scan de vulnérabilités du binaire ou de ses dépendances résolues n'a été exécuté ; l'audit ne certifie pas leur absence. » | **Levé le 19/09** — `scan-vulnerabilites-2026-09-19.md:3` : « Lève le point 2 de `corrections.md` § 4 » (détail en F) |
| 21 | SDK à actualiser — .NET 8 en maintenance, fin de support 10/11/2026 | `rapport.md:253` | « Microsoft classe .NET 8 en maintenance, avec fin de support au **10 novembre 2026** ; la page consultée indique le correctif **8.0.31 du 8 septembre 2026**. » | **Levé le 19/09** — `rebuild-sdk-8.0.425-2026-09-19.md:3` : « Lève le point 1 de `corrections.md` § 4 » |
| 22 | Hypothèses à observer (5 items : recherche qui se ferme, GDI+, DPI/multiécran/a11y, API Store et toast, cycle Windows) | `rapport.md:189` puis `:191` | « Ces points ne sont pas comptés comme des bugs Windows reproduits » | **Ouverts, renvoyés à la recette** — le n°1 (recherche qui se ferme) est **mesuré fermé par construction** : `recette-vm-resultats.md:61` « la fenêtre de recherche **se ferme avec l'application** ». Les autres : VM-18 **PARTIEL** (`:68`), VM-19 / RET-04 à RET-08 **NON TESTÉ** |

### A.3 — Vérifications déclarées le 15/09

`corrections.md:3` : « Les onze constats numérotés de l'audit sont corrigés dans le code. Les 609 tests automatisés exécutés passent ; les compilations natives x64 et ARM64 réussissent. »
`corrections.md:39` : « **Total : 493 tests .NET + 116 tests Python = 609 réussites.** »
`corrections.md:41` : « Les témoins ont observé **33 échecs avant leur correction** : 12 dans le premier lot, 8 dans le second, 12 dans le troisième et 1 sur la quarantaine des relâchements. »
`corrections.md:59` : « Tous ses scénarios restent **NON TESTÉS** dans cette session. »

Compteurs des JSON (survol) :
- `verification-bilan.json:3-6` : `"tests_passed": 609`, `"source_hashes_verified": 32`, `"native_hashes_verified": 2`, `"local_links_verified": 30`.
- `verification-finale.json:8` : `"links_checked": 44`, `"source_files_unchanged": 171`, `"issues": []`, head `8775489f…`.
- `verification-corrections.json` (19/09, 07:43 UTC) : 8 `checks`, `tests_dotnet` à 3 entrées, `changed_from_audit` = 19 fichiers, `unchanged_from_audit` = 152, `native_outputs` = 2 (x64 `0x8664` 8 172 544 o, arm64 `0xAA64`), `resolved_product_libraries` = 6 dont `Microsoft.DotNet.ILCompiler/8.0.31`.
- `controles-statiques.json` : `"method": "Contrôles de sources uniquement. Aucun build ni test applicatif Windows exécuté."`, 3 `checks` (`validate-layout.py` exit 0, `list-identity-literals.py`, `check-doc-versions.py --release`).
- `inventaire.json` : `runtime_cs_files` 75 / `runtime_cs_lines` 28098 ; `test_cs_files` 41 / `test_cs_lines` 6602 ; `syntax_checks` 9 ; `files` 171 ; `projects` 6 ; `data_invariants` 5 clés.

---

## B. Recette VM

### B.1 — Scénarios VM-01 à VM-23

Protocole : `docs/audit-2026-09-15-v1.2.0/recette-vm.md`. Compte rendu : `docs/audit-2026-09-15-v1.2.0/recette-vm-resultats.md` (511 lignes).
Avertissement de tête, `recette-vm-resultats.md:3` : « ⚠️ **La campagne porte désormais sur la 1.3.0**, pas sur la 1.2.0 ».
`recette-vm-resultats.md:13` : « ⛔ Un scénario non renseigné vaut **NON TESTÉ**, jamais « probablement bon ». »

| ID | Statut littéral | ligne | citation (≤ 2 lignes) |
|---|---|---|---|
| VM-01 | **VERT** | `:51` | « `Add-AppxPackage` du sideload 1.3.0.0 accepté. Lancement depuis le menu Démarrer : **une seule** instance » |
| VM-02 | **PARTIEL** | `:52` | « ⛔ Reste dû : conservation des réglages, raccourcis, statistiques et progression, et **un seul** démarrage actif » |
| VM-03 | **VERT** | `:53` | « ✅ L'état réel Windows correspond au choix dans les quatre cas, aucun consentement refusé contourné » |
| VM-04 | **VERT** | `:54` | « Les cinq changements saisis d'affilée rendent `éèçàÉÈÇÀ.;@#(){}[]|\ˆ¨~` — **23 caractères** » |
| VM-05 | **PARTIEL** | `:55` | « ⛔ **Volet « compatibilité native » (forceOn) : non concluant, voir Écart 5** » |
| VM-06 | **VERT** | `:56` | « **aucune touche virtuelle laissée enfoncée**, aucun menu ouvert, aucun modificateur collé (AG120-02) » |
| VM-07 | **PARTIEL** | `:57` | « ⛔ **Volet « compatibilité native » (mode `NativeCombo`, réglage `forceOn`) non exécuté** » |
| VM-08 | NON TESTÉ | `:58` | « ⛔ **Non exécutable hors session de débogage de l'Écart 5.** » |
| VM-09 | **VERT** | `:59` | « aucun ancien verrou ne ressuscite (AG120-04) » |
| VM-10 | **VERT** | `:60` | « **grec, latin, latin, grec**. Le verrou appartient à la cible, pas à l'application (AG120-05) » |
| VM-11 | **VERT** | `:61` | « la fenêtre de recherche **se ferme avec l'application** — aucun clic sur un résultat n'est possible pendant l'inactivité » |
| VM-12 | **PARTIEL** | `:62` | « ⛔ **Écart 8 ouvert dans ce scénario** : `Ctrl+Maj+W` dans une fenêtre désactivée ferme la fenêtre » |
| VM-13 | **VERT** | `:63` | « le remappage **traverse** la barrière d'intégrité. … Aucun faux succès, aucun échec muet (AG120-08) » |
| VM-14 | NON TESTÉ | `:64` | (ligne vide : `| VM-14 | NON TESTÉ | | |`) |
| VM-15 | NON TESTÉ | `:65` | (ligne vide : `| VM-15 | NON TESTÉ | | |`) |
| VM-16 | **VERT** | `:66` | « chaque répétition garde la décision de l'appui initial, donc **aucune sortie mixte ni touche bloquée** à la transition » |
| VM-17 | **VERT** | `:67` | « Remappage et raccourcis suivent donc la disposition réelle de la cible » |
| VM-18 | **PARTIEL** | `:68` | « ⛔ **Volet non testé : veille / reprise** — l'invité Hyper-V n'offre pas la mise en veille ordinaire » |
| VM-19 | NON TESTÉ | `:69` | (ligne vide) |
| VM-20 | NON TESTÉ | `:70` | « priorité : couche sécurisée sur champ de mot de passe » |
| VM-21 | **PARTIEL** | `:71` | « ⛔ Reste dû : le volet ARM64 exécuté, cf. limite 2 » |
| VM-22 | NON TESTÉ | `:72` | (ligne vide) |
| VM-23 | NON TESTÉ | `:73` | « **priorité** : retours partiels de `SendInput` » |

Bilan littéral : 9 VERT, 6 PARTIEL, 8 NON TESTÉ.
Parcours de retour RET-01 à RET-09 (`:79-87`) : **tous NON TESTÉ**, dont RET-04 « **NON TESTÉ — décidé** » (`:82`, compte local sans compte Microsoft).

Deux limites structurelles déclarées avant la grille :
- `:36` « **La mise à jour Store→Store ne peut pas être testée ici.** La 1.1.0 servie par le Store est signée Microsoft »
- `:43` « ⛔ **Hyper-V sur ce poste x64 ne peut pas exécuter d'invité ARM64.** »

### B.2 — Les huit écarts

En-tête, `recette-vm-resultats.md:207` : « ⛔ Les écarts **4, 5, 7 et 8** sont **ouverts** au 2026-09-19 ; l'écart **6** est **non reproduit** et rattaché à l'écart 7. »

| # | description (citation) | ligne | statut | commit de correction cité | ce qui reste à rejouer |
|---|---|---|---|---|---|
| 1 | « Ouvrir la zone de notification suffit à suspendre le remapping. Six occurrences journalisées dans `error.log` » | `:209`, `:211` | **Corrigé et vérifié en VM** (`:236`, `:243`) | `edfe727` — « ✅ **Corrigé** (`edfe727`) : la course ne suspend plus » (`:236`). Commit présent : `fix(compat): ne plus suspendre sur une course de lecture du premier plan`, 3 fichiers dont `ShellRaceSuspensionTests.cs` | Rien de déclaré ; non-reproduction confirmée par VM-10 (`:60`) |
| 2 | « ouvrir la zone de notification déclenche la bulle, la bulle recouvre l'icône de l'application, **et l'utilisateur ne peut plus quitter l'app** » | `:247`, `:249` | **Clos par conséquence** (`:262`) | Aucun commit propre — clos par l'écart 1 | « ⚠️ Le défaut de conception demeure en théorie … À rouvrir si une suspension légitime se produit » (`:263`) |
| 3 | « Le titre de l'étape 1 de la fenêtre d'accueil annonçait « 5 améliorations, 99 % de vos **habitudes** préservées » » | `:268`, `:271` | **Corrigé et clos** (`:276`, `:284`) | Pas de SHA cité ; fichier : `src/Localization/L.Onboarding.cs`, `Onboarding_Step1Title` (`:276`) | « ⛔ Aucun test ne verrouille ce libellé » (`:282`) |
| 4 | « La fenêtre Paramètres ne tient pas dans la hauteur disponible, **n'a pas de défilement** » | `:290`, `:293` | **Ouvert au 19/09**, correctif écrit le 20/09 | `404d68a` — « v1.3.0 : reparer Alt+Tab, Ctrl+Maj+W et la fenetre Parametres » (`src/SettingsWindow.cs`, +227 lignes) ; grille dédiée : `recette-vm-v1.3.0-correctifs.md:29` | Gestes 10 à 15 de `recette-vm-v1.3.0-correctifs.md:60-65` (1920×1080 100 %, 1366×768 150 % et 175 %, molette/ascenseur/Page suivante, FR→EN, second écran) |
| 5 | « mode « Forcer compatibilité jeu » : caractères perdus, mesures contradictoires » | `:313` | **⛔ Ouvert. Bloquant tant qu'il n'est pas expliqué.** (`:315`) | Aucun | « ⛔ **À reprendre dans une session de débogage dédiée** … il faut une reproduction propre » (`:342`). Bloque aussi VM-05 (volet forceOn), VM-07 (volet forceOn) et VM-08 entier |
| 6 | « ⚠️ NON REPRODUIT — aucun remappage dès que la disposition Windows n'est plus française » | `:348` | **Non reproduit, non clos** (`:350`, `:371`) | Aucun — rattaché à l'écart 7 | « ⛔ **Ne pas clore cet écart** : le rattacher à l'Écart 7 dans la session de débogage et vérifier ce qui peut geler le snapshot durablement » (`:371`). Point non expliqué : « à 17:17-17:35 l'état tenait plusieurs minutes et plusieurs gestes » (`:368`) |
| 7 | « après un Alt+Tab, plus aucun remappage dans la fenêtre d'arrivée » | `:429` | **Ouvert au 19/09**, correctif écrit le 20/09 | `404d68a` (`src/TypingEngine.Windows/ForegroundMonitor.cs` +31, `ForegroundMonitorMockedTests.cs` +44) ; `recette-vm-v1.3.0-correctifs.md:27` : « un chien de garde de 250 ms recalcule le contexte » | Gestes 1 à 4 de `recette-vm-v1.3.0-correctifs.md:39-42`, dont « Répéter cinq fois d'affilée » |
| 8 | « dans une fenêtre désactivée, `Ctrl+Maj+W` ferme la fenêtre » | `:474` | **Ouvert au 19/09**, correctif écrit le 20/09 ; « le seul écart de la campagne dont l'effet secondaire est destructeur » (`:494`) | `404d68a` (`src/TypingEngine.Windows/KeyboardHook.cs`, 158 lignes touchées) ; `recette-vm-v1.3.0-correctifs.md:28` : « les raccourcis sont réarmés pour la seule désactivation *choisie* par l'utilisateur » | Gestes 5 à 9 de `recette-vm-v1.3.0-correctifs.md:48-52`, dont le geste 8 (anti-cheat) et 9 (accès distant) : « **Rien ne se passe.** L'inertie totale doit tenir ici ». Non mesuré : « le raccourci du **clavier virtuel** tombe-t-il de la même façon ? » (`:496`) |

### B.3 — Paquet de la grille du 20/09

`recette-vm-v1.3.0-correctifs.md:3` : « **État : aucun scénario ci-dessous n'a été exécuté.** »
`:9-15` : fichier `msix/AZERTYGlobal-1.3.0.0.msixbundle`, 6,48 Mo, construit le 2026-09-20 à 17:27, SHA-256 x64 `DF69897528D0171E37CD8E65D6A0E6E82BAE425B01DC546CDABBC11FAE6DED25`, « **sans** les 32 commits de `origin/main` (décision d'Antoine du 2026-09-20) ».
Déjà prouvé côté machine (`:84-87`) : « Build Release vert, 2 avertissements préexistants (`TrayApplication.cs` 1399 et 1412) » ; « 495 tests verts : 18 Core, 157 Windows, 323 application » ; « Trois témoins neufs sur `IsSnapshotStale`, **vérifiés par mutation** ».
Non couvert par cette grille (`:77-80`) : « VM-14, 15, 19, 20, 22, 23 », ARM64, veille/reprise, écart 5.

### B.4 — Piège de fabrication consigné

`recette-vm-resultats.md:149` : « ⛔ Piège : `Pack-MSIX.ps1` ne compile pas ».
`:153` : « Un publish périmé passe donc sans un mot, et le bundle produit annonce la bonne version en portant l'ancien binaire. »
`:170` : « ⛔ **La version vit à cinq endroits indépendants, pas deux.** » — tableau `:176-180` (csproj, AppxManifest.xml, Program.cs, AssemblyInfo.cs, documents).
`:187` : « ⛔ **Et rien ne vérifie l'accord csproj / AppxManifest hors de ce script.** »

---

## C. Cahier des charges §7 — exigences avant soumission

Source : `Cahier des charges.md`, section `## 7. Critères de validation` (ligne 254) → `### Tests minimaux avant publication` (ligne 256), fin de fichier ligne 293 (« *Dernière mise à jour : 2026-08-24* »).

Table des matières du fichier (titres `#`/`##`/`###`, avec leurs lignes) : `1. Objectifs` (7), `Objectif principal` (9), `Cas d'usage cibles` (12), `2. Exigences fonctionnelles` (20), `2.1 Remapping clavier complet` (22), `2.2 Verrouillage Majuscule Intelligent (Smart Caps Lock)` (30), `2.3 Compatibilité` (41), `2.4 Interface utilisateur` (52), `2.5 Première utilisation (onboarding)` (72), `Wizard d'accueil — 3 étapes` (78), `Module d'exercices intégré — 6 exercices` (86), `2.6 Clavier virtuel (visualiseur de disposition)` (95), `2.7 Recherche de caractère` (112), `2.8 Fonctionnalités supplémentaires (v1)` (125), `2.9 Compatibilité jeux (livré en v0.9.7)` (130), `2.10 Couches maintenables — grec, cyrillique, scientifique` (140), `2.11 Fonctionnalités v2+` (155), `3. Exigences non-fonctionnelles` (165), `3.1 Sécurité et confiance` (167), `3.2 Performance` (176), `3.3 Autonomie système` (183), `3.4 Distribution` (190), `4. Architecture technique — Contraintes` (199), `Ce que doit faire l'application techniquement` (201), `Contraintes Windows connues` (209), `5. Comparatif des technologies candidates` (218), `Décision : C# / .NET 8 AOT ✅` (233), `6. Données d'entrée` (243), `7. Critères de validation` (254), `Tests minimaux avant publication` (256).

⚠️ Les cases `[x]`/`[ ]` sont l'état déclaré par ce fichier au 2026-08-24, antérieur aux deux campagnes. Elles ne sont pas une preuve.

**Remapping** (`:258`)

| ligne | exigence (citation littérale) | preuve dans le dépôt |
|---|---|---|
| 259 | « - [x] Toutes les lettres a–z produisent le bon caractère (base + shift + caps) » | `src/AZERTY Global 2026.json` validé par `scripts/validate-layout.py` (exit 0, `docs/audit-2026-09-15-v1.2.0/controles-statiques.json`) ; `src/TypingEngine.Windows.Tests/KeyMapperBuildInputsTests.cs` ; VM-04 VERT, `recette-vm-resultats.md:54` |
| 260 | « - [x] É, È, Ç, À fonctionnent avec Caps Lock » | `src/AZERTYGlobal.Tests/DeadKeyAndSmartCapsTests.cs` ; VM-04, `recette-vm-resultats.md:54` (« les quatre majuscules accentuées sortent bien du Verr. Maj. intelligent ») |
| 261 | « - [x] Les 5 touches mortes principales fonctionnent (circonflexe, tréma, aigu, grave, tilde) » | `src/TypingEngine.Core.Tests/CompositionEngineTests.cs` ; VM-05 **PARTIEL**, `recette-vm-resultats.md:55` (`^`, `¨`, `` ` ``, `~` mesurés ; aigu non cité) |
| 262 | « - [x] Touche morte + caractère non reconnu → diacritique isolé + caractère (fallback) » | `src/TypingEngine.Core.Tests/CompositionEngineOrphanDeadKeyTests.cs` ; `src/AZERTYGlobal.Tests/DeadKeyEmissionBatchTests.cs` |
| 263 | « - [x] Les symboles de programmation fonctionnent (AltGr + D/F/G/H/J/K → { } \ \| [ ]) » | `src/AZERTY Global 2026.json` ; VM-04, `recette-vm-resultats.md:54` (sortie `{}[]|\`) |
| 264 | « - [x] Les guillemets français fonctionnent (AltGr + W/X → « ») » | `src/AZERTYGlobal.Tests/FrenchTypographyTests.cs` ; **aucune preuve VM trouvée** |
| 265 | « - [x] œ et æ fonctionnent (AltGr + O/A) » | `src/AZERTY Global 2026.json` ; **aucun test nommément dédié ni scénario VM trouvé** |
| 266 | « - [x] Espaces insécables : fine insécable en AltGr + Espace, insécable en Maj + AltGr + Espace » | `src/AZERTYGlobal.Tests/FrenchTypographyTests.cs` ; `recette-vm.md:52` prévoit la comparaison des points de code, **non exécutée** |
| 267 | « - [x] Les raccourcis Ctrl+C/V/Z/X ne sont pas cassés » | `src/TypingEngine.Windows.Tests/KeyMapperCtrlRegressionTests.cs` ; VM-06 VERT, `recette-vm-resultats.md:56` |
| 268 | « - [x] Compensation DK système : ^ puis e produit ê (pas ^e ou ^^e) » | `CompensateSystemDeadKey` cité dans `docs/audit-2026-09-15-v1.2.0/rapport.md:47` ; **aucun test le nommant trouvé** dans les suites ; VM-05 PARTIEL |

**Interface** (`:270`)

| ligne | exigence | preuve |
|---|---|---|
| 271 | « - [x] L'application se lance et se ferme proprement » | VM-01 VERT, `recette-vm-resultats.md:51` |
| 272 | « - [x] L'icône tray s'affiche et le menu contextuel fonctionne » | VM-01 VERT (`:51`, icône) ; ⚠️ `Changelog.md:12` : « Aucun test ne verrouille la structure du menu — `ShowContextMenu` appelle Win32 directement. » |
| 273 | « - [x] Double-clic sur l'icône ouvre le clavier virtuel » | **aucune preuve trouvée dans le dépôt** (aucun scénario VM, aucun test identifié) |
| 274 | « - [x] Le clavier virtuel réagit aux modificateurs (Shift, AltGr, Caps) » | **aucune preuve trouvée dans le dépôt** ; le plus proche est `src/AZERTYGlobal.Tests/LessonCoreTests.cs:482` (`KeyboardRenderer.IsSlotVisible`), qui porte sur la visibilité des slots en profil Leçon |
| 275 | « - [x] Le clavier virtuel réagit aux touches mortes actives » | **aucune preuve trouvée dans le dépôt** |
| 276 | « - [x] La recherche de caractère fonctionne (par caractère et par nom) » | `src/AZERTYGlobal.Tests/Audit120InsertionTests.cs`, `src/AZERTYGlobal.Tests/TextInsertionServiceTests.cs` ; VM-11 VERT (`:61`), VM-13 VERT (`:63`) ; RET-02 **NON TESTÉ** (`:80`) |
| 277 | « - [x] L'onboarding s'affiche au premier lancement uniquement » | `src/AZERTYGlobal.Tests/ConfigManagerCompatTests.cs` (`showOnboardingAtStartup`) ; `docs/audit-v1.2.0/README.md:396` mesure l'état réel du poste |

**Performance** (`:279`)

| ligne | exigence | preuve |
|---|---|---|
| 280 | « - [x] L'application ne consomme pas de CPU au repos » | **aucune preuve trouvée dans le dépôt** ; contredit par `rapport.md:174` : « Aucun temps de réponse, consommation de mémoire, taux de crash ou résultat antivirus n'est revendiqué ici. » |
| 281 | « - [x] Latence de frappe imperceptible » | **aucune preuve trouvée dans le dépôt** ; même citation `rapport.md:174` |

**Couches maintenables** (`:283`) — chapeau : « **Couches maintenables (validation manuelle avant publication de la fonctionnalité)** — à dérouler dans Word/Excel, Chrome, Edge, Firefox et VS Code : »

| ligne | exigence | preuve |
|---|---|---|
| 285 | « - [ ] Maj+* puis `a` → α ; maintien Maj+* + `abc` → αbc … double appui → verrou (indicateur « verrou »), Espace reste une espace, Échap déverrouille » | `src/AZERTYGlobal.Tests/KeyMapperMaintainableLayerTests.cs`, `src/TypingEngine.Windows.Tests/MaintainableLayerManagerTests.cs` ; VM-09 VERT mais **grec seulement** : `recette-vm-resultats.md:59` « cyrillique et scientifique non rejouées » |
| 286 | « - [ ] Pendant un verrou : Ctrl+C/V, Alt+Tab et Win+E restent intacts ; un nouveau Maj produit Α ; un appui AltGr+* rend la frappe suivante cyrillique » | `src/AZERTYGlobal.Tests/KeyMapperMaintainableLayerCoverageTests.cs` ; **aucun scénario VM** ; ⚠️ le volet Alt+Tab croise l'écart 7 (`recette-vm-resultats.md:429`) |
| 287 | « - [ ] Champ de mot de passe (login réel dans les 3 navigateurs + `<input type="password">` local) : frappes remappées normalement, aucune couche, indicateur masqué, raccourci de recherche inerte » | `src/TypingEngine.Windows.Tests/Audit120SuspensionTests.cs`, `src/TypingEngine.Windows.Tests/MaintainableLayerManagerTests.cs` (`IsSecureInput`) ; ⛔ VM-20 **NON TESTÉ** (`recette-vm-resultats.md:70`) |
| 288 | « - [ ] Recherche depuis le raccourci ET depuis le menu tray : Entrée insère au point d'insertion d'origine et la fenêtre de recherche reste ouverte » | ⚠️ **contredit par la mesure** : `recette-vm-resultats.md:61` « Elle se ferme donc à **toute perte de focus** » ; `rapport.md:193` « Clarifier le comportement souhaité ». RET-02 **NON TESTÉ** |
| 289 | « - [ ] Verrou dans Word → VS Code (pas de couche) → retour Word (verrou revenu) → fermeture puis réouverture de Word (verrou disparu) » | VM-10 VERT (`recette-vm-resultats.md:60`) mais joué sur **Bloc-notes / Explorateur / recherche Windows**, pas sur la matrice Word/Excel/Chrome/Edge/Firefox/VS Code : **aucune preuve de cette matrice dans le dépôt** |

---

## D. Procédure de publication

Source : `Publication Microsoft Store.md` (322 lignes, « *Dernière mise à jour : 2026-07-06* » `:322`).
En-tête `:5-8` : « Version cible : 1.3.0 » / « Version publiée Store : 1.1.0 » / « Package Store : 1.3.0.0 » / « Publication Store : 2026-07-23 — v1.1.0 acceptée par Microsoft et publiée ».

### D.1 — `## Séquence de release à appliquer aux prochaines versions` (`:245`)

| # | ligne | étape (une ligne) | arrêt humain |
|---|---|---|---|
| 1 | 247 | `python scripts/check-doc-versions.py --release` — aucune attente de bascule tolérée, le kit entreprise doit avoir été basculé avant | — |
| 2 | 248 | `dotnet publish -c Release` pour `win-x64` puis `win-arm64`, avec `vswhere` dans le `PATH` | — |
| 3 | 249 | Exécuter `scripts/Pack-MSIX.ps1` | — |
| 4 | 250 | Vérifier le package versionné produit dans `msix/` | — |
| 5 | 251 | Exécuter `scripts/Verify-Release.ps1` | — |
| 6 | 252 | Passer le WACK | **⚑ arrêt humain** — exécution en invite élevée par Antoine, cf. `bundle-msix-2026-09-19.md:74` et `:128` |
| 7 | 253 | Soumettre dans Partner Center | **⚑ arrêt humain** — soumission externe |
| 8 | 254 | Après publication confirmée, synchroniser le repo GitHub | **⚑ arrêt humain** — conditionné à « publication confirmée » |

### D.2 — `### Procédure à chaque release` du workflow GitHub (`:266`)

| # | ligne | étape | arrêt humain |
|---|---|---|---|
| 1 | 268 | Avant la soumission Store (ou juste après si déjà publiée) : ouvrir le clone `AZERTY Global/Microsoft Store - app repo` | — |
| 1a | 270 | `git pull origin main` | — |
| 1b | 271-276 | Reporter les changements par trois `robocopy` (`src`, `msix`, `scripts`) vers le clone | — |
| 1c | 277 | Mettre à jour le `README.md` du clone si nouvelles fonctionnalités, modules ou structure | — |
| 1d | 278 | `git -C … status` → vérifier le diff | — |
| 1e | 279 | Vérifier qu'aucun fichier sensible n'est ajouté (pas de `.env`, pas de certificat, pas de clé) | — |
| 1f | 280 | Stager explicitement les familles de fichiers réellement synchronisées | — |
| 1g | 281 | « Créer le commit de release seulement après vérification du diff » | **⚑ arrêt humain** (vérification préalable) |
| 1h | 282 | « Créer le tag annoté seulement après validation explicite d'Antoine » | **⚑ arrêt humain explicite** |
| 2 | 284 | « **Après publication Store réussie et validation explicite d'Antoine** » | **⚑ arrêt humain explicite** |
| 2a | 285 | Pousser la branche principale et le tag depuis le clone public | **⚑** (couvert par 2) |
| 2b | 286 | Vérifier sur GitHub que commit + tag sont visibles | — |
| 2c | 287 | Si changelog notable, créer une GitHub Release depuis le tag | — |

Post-publication, `:309-314` : vérifier l'expérience d'installation depuis la fiche publique ; tester le DPI 100/125/150/175 % sur la version installée ; surveiller les premiers retours ; « La politique de confidentialité dédiée et le registre RGPD restent à traiter séparément. » (`:314`)

⚠️ Écart de chemins : la procédure GitHub cite `AZERTY Global/2026/Microsoft Store/` et `AZERTY Global/Microsoft Store - app repo/` (`:264`, `:269-275`), arborescence antérieure à la migration en bundle wiki qui a descendu le composant d'un niveau (`bundle-msix-2026-09-19.md:42`). **Ces chemins n'ont pas été vérifiés sur disque dans cette lecture.**

---

## E. Garde-fous sans témoin (audit d'août)

Source : `docs/audit-v1.2.0/README.md`, `### Garde-fous sans temoin` (ligne 454).
`:456` : « Un garde-fou dont on n'a jamais vu un echec ne prouve rien. »

| # | garde-fou (citation) | ligne | témoin de mutation aujourd'hui ? |
|---|---|---|---|
| 1 | « `ActivatorClsid_MatchesAppxManifestDeclaration` (`ToastActivationTests.cs:28-35`) -- deux chaines codees en dur comparees entre elles, le manifeste n'est jamais lu. » | 458-460 | **Oui, corrigé et doté de deux témoins in-suite.** `src/AZERTYGlobal.Tests/ToastActivationTests.cs:89` charge désormais le manifeste (`XDocument.Load(FindAppxManifest())`, `:92`), et `:104` `ReadActivatorDeclarations_ManifesteSansServeurCom_NeTrouveRien` + `:118` `ReadActivatorDeclarations_ClsidDivergent_EstRapporteTelQuel` sont les témoins. Commentaire `:99` : « Témoin : un manifeste sans extension `com:` … Sans ce témoin, un lecteur muet passerait pour un garde-fou. » Confirmé côté paquet : `manifestes-bundle-2026-09-19.md:56` |
| 2 | « `SyncScript_AllowsCreatingPublicLessonsResource` (`LessonCoreTests.cs:471-479`) -- deux `Assert.Contains` sur le texte source du `.ps1`, jamais execute. » | 461-462 | **Non.** Le test est inchangé : `src/AZERTYGlobal.Tests/LessonCoreTests.cs:472-479`, toujours deux `Assert.Contains` sur le texte du script (`:477`, `:478`). Aucun témoin de mutation trouvé |
| 3 | « `scripts/list-identity-literals.py` et `scripts/check-layout-provenance.py` -- bloquants en CI, exit 0 en l'etat, aucun test ne reintroduit une regression » | 463-465 | **Non.** `scripts/tests/` ne contient que `Invoke-ArchiveTest.ps1`, `test_admx_agreement.py`, `test_archive_stable_bundle.py`, `test_check_doc_versions.py`, `test_gen_appinstaller.py`, `test_schema_parser_agreement.py`, `test_validate_layout.py` — aucun fichier pour ces deux scripts. `docs/audit-v1.2.0/` ne contient pas de `witness-*` les visant (les six sont : `witness-baseline.py`, `witness-lot-b.py`, `witness-lot-c.py`, `witness-lot-d.py`, `witness-lot-f.py`, `witness-lot-f-versions.py`) |
| 4 | « `scripts/Sync-LayoutResources.ps1` -- `-SyncPublicRepo` copie un fichier sur lui-meme en annoncant un succes, bug reconnu par `docs/keyboard-platform.md:224-227`, non corrige. » | 466-468 | **Non, et le défaut subsiste.** `scripts/Sync-LayoutResources.ps1` porte toujours `[switch]$SyncPublicRepo` (`:2`) et trois branches `if ($SyncPublicRepo)` (`:129`, `:142`, `:169`), fichier daté du 2026-08-17. `docs/keyboard-platform.md:224` : « La branche `-SyncPublicRepo` est résiduelle : le clone public qu'elle cherche n'existe pas, son second candidat est le dépôt lui-même, si bien qu'elle copie les fichiers sur eux-mêmes en annonçant un succès. » `:226` : « La supprimer casserait `LessonCoreTests.SyncScript_AllowsCreatingPublicLessonsResource` … les deux se traitent ensemble ou pas du tout » |
| 5 | « Le `SKIP_DIRS` du scanner d'identite (`list-identity-literals.py:52-56`) saute `TypingEngine.Core` et `TypingEngine.Windows`, devenus du code de production depuis `452aab0`. » | 469-471 | **Non, inchangé.** `scripts/list-identity-literals.py:49-53` : `SKIP_DIRS = {` … `"AZERTYGlobal.Tests", "TypingEngine.Core",` / `"TypingEngine.Core.Tests", "TypingEngine.Windows",` / `"TypingEngine.Windows.Tests", "TestSupport", "bin", "obj",` `}` — les deux projets de production sont toujours exclus |

Témoins existants par ailleurs (hors de cette liste), pour situer l'outillage :
- `scripts/witness-embedded-resources.py` — « Témoin des contrôles de `ResourceAlignmentTests` : ils doivent virer au rouge. » (`:1`). ⛔ Non exécuté au 15/09 : `rapport.md:225` « `witness-embedded-resources.py` n'a pas été exécuté : il modifie des sources et lance les tests ».
- `docs/audit-v1.2.0/witness-lot-f.py:12` : « Sortie attendue sur un dépôt sain : dix mutations, dix rouges. »
- `docs/audit-v1.2.0/witness-lot-b.py`, `-c.py`, `-d.py` — chacun déclare des mutations « attendues À ZÉRO ROUGE » qui documentent leurs angles morts (`witness-lot-b.py:9`, `witness-lot-c.py:9`, `witness-lot-d.py:9`).
- Témoins neufs créés depuis : `src/TypingEngine.Windows.Tests/ShellRaceSuspensionTests.cs` (écart 1, `recette-vm-resultats.md:239`) et trois témoins `IsSnapshotStale` « **vérifiés par mutation** » (`recette-vm-v1.3.0-correctifs.md:86`).
- Autre contrôle muet mesuré le 19/09 : `bundle-msix-2026-09-19.md:47` « `Assert-MatchIfExists` ne trouvait pas les fichiers, émettait un `WARNING` et **passait**. Deux contrôles de cohérence de version rendaient donc « vert » depuis des semaines sans rien vérifier. » Corrigé (`:55-61`), mais `:68` : « `TO-DO.md` est attendu à la racine du composant et n'existe nulle part dans le dépôt : le contrôle reste en `WARNING` permanent. »

---

## F. Scan de vulnérabilités du 19/09

Source : `docs/audit-2026-09-15-v1.2.0/scan-vulnerabilites-2026-09-19.md` (67 lignes).

Commandes exécutées (`:11-14`) : `dotnet restore <projet> --force` puis `dotnet list <projet> package --vulnerable --include-transitive`, « Depuis `components/microsoft-store/src`, sur les six projets » (`:9`).

Preuve que le scan est réel (`:16-19`) : « Index joignable cette fois : chaque exécution imprime `The following sources were used: https://api.nuget.org/v3/index.json`. Aucun NU1900. »

Résultat littéral (`:23-30`) :

| Projet | Livré au Store | Verdict |
|---|---|---|
| `AZERTYGlobal` | oui | aucune vulnérabilité |
| `TypingEngine.Core` | oui | aucune vulnérabilité |
| `TypingEngine.Windows` | oui | aucune vulnérabilité |
| `AZERTYGlobal.Tests` | non | 2 × High, transitives |
| `TypingEngine.Core.Tests` | non | 2 × High, transitives |
| `TypingEngine.Windows.Tests` | non | 2 × High, transitives |

`:32` : « **Les trois projets qui composent le paquet Store sont propres.** »

Paquets cités (`:38-39`) : `System.Net.Http` 4.3.0, High, [GHSA-7jgj-8wvc-jh57] ; `System.Text.RegularExpressions` 4.3.0, High, [GHSA-cmhx-cq75-c4mj]. Chaîne (`:44-49`) : `xunit (2.6.0)` → `xunit.core (2.6.0)` → `xunit.extensibility.core (2.6.0)` → `NETStandard.Library (1.6.1)` → les deux paquets.

Décision (`:56`, `:58`) : « ## Décision — reportée après la v1.2.0 » / « Pas de montée de xunit avant la soumission. » ; `:63` « À faire sur la branche v2.0.0 : passer xunit en 2.9.x ». `:66` : « ⚠️ À rejouer si la soumission glisse de plus de quelques semaines : un scan n'est valable qu'à sa date. »

**SDK utilisé** — `docs/audit-2026-09-15-v1.2.0/rebuild-sdk-8.0.425-2026-09-19.md:10-12` : SDK .NET 8.0.423 → **8.0.425** ; runtime `Microsoft.NETCore.App` 8.0.29 → **8.0.31** ; ILCompiler (Native AOT) 8.0.29 → **8.0.31**.
`:14` : « 8.0.31 est sorti le 2026-09-08 et corrige 5 CVE (CVE-2026-69439, -71328, -69522, -69304, -58649). »
`:18` : « ⚠️ **.NET 8 sort de support le 2026-11-10.** »
⚠️ Ordre d'exécution, `:62-65` : « le scan de vulnérabilités du même jour … avait tourné **avant** ce nettoyage [du cache HTTP NuGet]. Il a été **relancé après**, résultat identique ».

Confirmation indépendante dans les preuves : `verification-corrections.json` → `resolved_product_libraries` contient `Microsoft.DotNet.ILCompiler/8.0.31`.

---

## G. Non vérifié

1. **Je n'ai exécuté aucun test, build, script ni WACK.** Tout ce qui précède est une lecture de documents et de sources. Les chiffres (609, 493, 495, 116) sont cités, pas mesurés.
2. **Les TRX ne sont pas ouverts.** `docs/audit-2026-09-15-v1.2.0/test-results/` et `test-results-baseline-sdk8.0.423/` n'ont pas été lus ligne à ligne ; les totaux viennent de `corrections.md:29-32` et de `rebuild-sdk-8.0.425-2026-09-19.md:35-37`.
3. **`inventaire.json` (171 fichiers) et `controles-statiques.json` n'ont été survolés que par leur structure et leurs compteurs**, conformément à la consigne. Les 171 empreintes SHA-256 ne sont pas recoupées.
4. **Le statut « corrigé » de chaque AG120 vient de `corrections.md`, pas d'une relecture du code.** Je n'ai pas ouvert `KeyMapper.cs`, `ForegroundMonitor.cs`, `ConfigManager.cs` ni `LessonProgressStore.cs` pour confirmer que la correction décrite est bien celle qui est en place à `4d5da24`.
5. **L'écart entre la branche lue et `origin/main` n'est pas mesuré.** `recette-vm-v1.3.0-correctifs.md:14` parle de « **sans** les 32 commits de `origin/main` » ; je n'ai pas exécuté de comparaison.
6. **Les commits de correction des écarts 4, 7 et 8 sont identifiés par leur message et leur `--stat`**, pas par une lecture du diff. Je n'affirme pas que `404d68a` corrige effectivement ces trois écarts ; le document `recette-vm-v1.3.0-correctifs.md:3` dit lui-même qu'« aucun scénario ci-dessous n'a été exécuté ».
7. **Partie C** : pour plusieurs exigences (double-clic → clavier virtuel, réaction du clavier virtuel aux modificateurs et aux touches mortes, CPU au repos, latence, œ/æ), je n'ai trouvé **aucune preuve**. Cela veut dire que mes recherches (grep sur les suites de tests et sur les deux fichiers de recette) n'en ont pas trouvé — pas qu'elle n'existe nulle part. Les suites n'ont pas été lues intégralement.
8. **Partie D** : les chemins `AZERTY Global/2026/Microsoft Store/` et `AZERTY Global/Microsoft Store - app repo/` cités par la procédure GitHub n'ont pas été vérifiés sur disque.
9. **`docs/keyboard-platform.md` n'a été que survolé** (titres + la zone 215-232 sur `Sync-LayoutResources.ps1`). La liste complète de ses bugs reconnus n'est pas établie ici ; seul le bug `-SyncPublicRepo` (`:224-227`) l'est, parce que l'audit d'août le cite.
10. **Aucun fichier absent constaté parmi ceux demandés** : les 13 fichiers de la liste de lecture existent tous. Un fichier attendu ailleurs est bien absent : `TO-DO.md` à la racine du composant (`bundle-msix-2026-09-19.md:68`).
11. **La VM `AZERTY-Test` n'est pas accessible depuis cette session** ; aucun état machine n'a été contrôlé.
