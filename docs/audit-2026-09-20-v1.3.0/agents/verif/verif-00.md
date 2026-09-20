# Vérification 00-base-audits-precedents.md
Citations : 206 total · EXACT 206 · DÉCALÉ 0 · ABSENT 0 · NON VÉRIFIABLE 0

> Unité de compte = un couple (ligne de table × fichier:ligne). Une ligne de A.1 qui cite `rapport.md`, `corrections.md` et `recette-vm-resultats.md` compte trois fois.
> Trois citations désignent un fichier JSON **sans** numéro de ligne (`controles-statiques.json`, `inventaire.json`, `verification-corrections.json`) : le texte y est présent (l. 4, 14 et 232-235) et elles sont comptées EXACT, faute de ligne annoncée.
> Deux transcriptions diffèrent du fichier par des **apostrophes typographiques** (`recette-vm-resultats.md:187`) ou par l'**échappement JSON** (`verification-finale.json:8`, où les octets réels sont `\"links_checked\": 44`) : le texte est bien à la ligne citée.
> Chemins relatifs à la racine du composant. `rapport.md`, `corrections.md`, `recette-vm-resultats.md`, `recette-vm-v1.3.0-correctifs.md`, `scan-vulnerabilites-2026-09-19.md`, `rebuild-sdk-8.0.425-2026-09-19.md`, `bundle-msix-2026-09-19.md` et `manifestes-bundle-2026-09-19.md` vivent tous dans `docs/audit-2026-09-15-v1.2.0/`.

## Décalées et absentes (détail)

| section | fichier:ligne cité | verdict | vraie ligne ou « nulle part » | citation (≤ 60 car.) |
|---|---|---|---|---|
| — | — | — | **aucune** : 0 DÉCALÉ, 0 ABSENT | — |

## Constats à citation confirmée (EXACT ou DÉCALÉ), condensés

| section | fichier:ligne | constat en une phrase | test cité |
|---|---|---|---|
| A.1 n°1 | rapport.md:39, :43 ; corrections.md:11 | Les sondes de touches mortes passaient `0x01` ; corrigées en `0x04` non mutateur. | VM-05 PARTIEL (:55) |
| A.1 n°2 | rapport.md:50, :54 ; corrections.md:12 | Les sorties anticipées Alt/Windows précédaient le relâchement des touches virtuelles. | VM-06 VERT (:56) |
| A.1 n°3 | rapport.md:61, :65 ; corrections.md:13 | Seul `VK_LSHIFT` était neutralisé puis restauré ; les deux Maj le sont désormais. | VM-07 PARTIEL (:57) |
| A.1 n°4 | rapport.md:72, :76 ; corrections.md:14 | Décocher une couche ne vidait pas ses verrous existants. | VM-09 VERT (:59) |
| A.1 n°5 | rapport.md:83, :87 ; corrections.md:15 | Les fenêtres du shell héritaient du contexte de l'application précédente. | VM-10 VERT (:60) |
| A.1 n°6 | rapport.md:94, :104 ; corrections.md:16 | Le code ne démontrait pas l'absence d'injection vers une cible suspendue. | VM-11 VERT, VM-12 PARTIEL |
| A.1 n°7 | rapport.md:110, :114 ; corrections.md:17 | `EnsureLoaded` appelait `EnumerateObject()` sans vérifier que la racine est un objet. | VM-14 NON TESTÉ (:64) |
| A.1 n°8 | rapport.md:121, :125 ; corrections.md:18 | `EmitText` comptait le texte avant émission et ignorait le retour de `SendInput`. | VM-13 VERT ; VM-23 NON TESTÉ |
| A.1 n°9 | rapport.md:132, :136 ; corrections.md:19 | `LessonProgressStore.Load` sortait sur version inconnue en laissant `_loadFailed` à faux. | VM-15 NON TESTÉ (:65) |
| A.1 n°10 | rapport.md:231, :235 ; corrections.md:20 | L'archivage pouvait ranger un paquet 1.1.0 sous un nom 1.2.0.0. | test_archive_stable_bundle.py |
| A.1 n°11 | rapport.md:179, :183 ; corrections.md:21 | Le masque d'icône de secours était trop court, notamment à 40 pixels. | VM-19 NON TESTÉ (:69) |
| A.2 n°12 | rapport.md:239, :241 ; manifestes-bundle:112 | `Verify-Release.ps1` ne lit ni le manifeste du bundle ni ceux des MSIX internes. | contrôle manuel |
| A.2 n°13 | rapport.md:142, :144 ; corrections.md:67 | La sollicitation d'avis n'est pas réévaluée pendant une longue session ; reporté. | RET-09 NON TESTÉ |
| A.2 n°14 | rapport.md:150, :156 ; corrections.md:69 | Un avis Store ne remplace pas un retour exploitable ; campagne à préparer à part. | RET-01 à RET-09 NON TESTÉ |
| A.2 n°15 | rapport.md:172 ; corrections.md:23 | La garantie « éviter tout risque de bannissement » de la fiche Store a été retirée. | aucun |
| A.2 n°16 | rapport.md:173 ; corrections.md:23 | Le README annonçait une « détection des applications fullscreen » introuvable dans le code. | aucun |
| A.2 n°17 | rapport.md:257 ; corrections.md:67 | Le générateur App Installer ne vérifie ni la signature ni l'éditeur AMCF attendu. | aucun |
| A.2 n°18 | rapport.md:258 ; corrections.md:23 | La documentation annonçait un contrôle `.appinstaller` non implémenté. | aucun |
| A.2 n°19 | rapport.md:249 ; rebuild-sdk:98 | Les trois projets de test tournent sur le runner x64 ; compiler ARM64 ne prouve rien. | VM-21 PARTIEL |
| A.2 n°20 | rapport.md:251 ; scan-vulnerabilites:3 | L'absence de scan de dépendances a été levée le 19/09. | — |
| A.2 n°21 | rapport.md:253 ; rebuild-sdk:3 | Le point SDK / fin de support .NET 8 a été levé le 19/09. | — |
| A.2 n°22 | rapport.md:189, :191 ; recette:61 | Cinq hypothèses renvoyées à la recette ; seule la fenêtre de recherche est mesurée fermée. | VM-18 PARTIEL, VM-19 NON TESTÉ |
| A.3 | corrections.md:3, :39, :41, :59 ; verification-bilan.json:3-6 ; verification-finale.json:8 ; controles-statiques.json:4 ; inventaire.json:14 | 609 réussites déclarées, 33 échecs observés par les témoins, recette VM déclarée non testée le 15/09. | chiffres cités, non mesurés |
| B.1 en-têtes | recette-vm-resultats.md:3, :13 | La campagne porte sur la 1.3.0 et un scénario non renseigné vaut NON TESTÉ. | — |
| B.1 VERT | recette:51, :53, :54, :56, :59, :60, :61, :63, :66, :67 | Neuf scénarios verts, dont l'installation, les 23 caractères et le verrou lié à la cible. | recette VM |
| B.1 PARTIEL | recette:52, :55, :57, :62, :68, :71 | Six scénarios partiels : volets forceOn, mise à jour, veille/reprise, ARM64, écart 8. | recette VM |
| B.1 NON TESTÉ | recette:58, :64, :65, :69, :70, :72, :73 | Huit scénarios non testés, dont VM-14, VM-15, VM-19, VM-22 laissés en ligne vide. | aucun |
| B.1 RET | recette:82 | RET-04 est « NON TESTÉ — décidé » (compte local sans compte Microsoft). | aucun |
| B.1 limites | recette:36, :43 | Mise à jour Store→Store et invité ARM64 impossibles sur ce poste. | — |
| B.2 écart 1 | recette:209, :211, :236 | Ouvrir la zone de notification suspendait le remapping ; corrigé par `edfe727`. | ShellRaceSuspensionTests |
| B.2 écart 2 | recette:247, :249, :263 | L'utilisateur ne pouvait plus quitter l'application ; clos par conséquence, à rouvrir si besoin. | aucun |
| B.2 écart 3 | recette:268, :271, :282 | Le titre d'accueil disait « 99 % de vos habitudes » ; corrigé, sans test qui verrouille le libellé. | aucun |
| B.2 écart 4 | recette:290, :293 ; correctifs:29 | La fenêtre Paramètres n'a pas de défilement ; correctif écrit le 20/09, non rejoué. | gestes 10 à 15 |
| B.2 écart 5 | recette:313, :315, :342 | Mode « Forcer compatibilité jeu » : caractères perdus, ouvert et bloquant. | aucun |
| B.2 écart 6 | recette:348, :350, :368, :371 | Non reproduit mais non clos, rattaché à l'écart 7. | aucun |
| B.2 écart 7 | recette:429 ; correctifs:27 | Plus aucun remappage après Alt+Tab ; chien de garde de 250 ms écrit le 20/09. | gestes 1 à 4 |
| B.2 écart 8 | recette:474, :494, :496 ; correctifs:28 | `Ctrl+Maj+W` ferme la fenêtre : seul écart à effet destructeur, correctif non rejoué. | gestes 5 à 9 |
| B.3 | correctifs:3, :9-15, :77-80, :84-87 | Aucun scénario de la grille du 20/09 n'a été exécuté ; le bundle est sans les 32 commits de `main`. | 495 tests verts cités |
| B.4 | recette:149, :153, :170, :187 | `Pack-MSIX.ps1` ne compile pas, la version vit à cinq endroits, rien ne vérifie csproj ↔ manifeste. | aucun |
| C en-têtes | Cahier des charges.md:7, :9, :140, :167, :233, :254, :256, :293 | La table des matières et la section 7 sont aux lignes annoncées, fichier daté du 2026-08-24. | — |
| C remapping | Cahier des charges.md:259-268 | Dix exigences cochées ; trois sans preuve VM trouvée (guillemets, œ/æ, compensation DK). | tests nommés par exigence |
| C interface | Cahier des charges.md:271-277 | Sept exigences cochées ; trois sans aucune preuve trouvée (double-clic, modificateurs, touches mortes). | VM-01, VM-11, VM-13 |
| C performance | Cahier des charges.md:280-281 ; rapport.md:174 | Deux exigences cochées sans preuve, contredites par le rapport qui ne revendique aucune mesure. | aucun |
| C couches | Cahier des charges.md:283, :285-289 ; rapport.md:193 | Cinq exigences non cochées ; la matrice Word/Chrome/VS Code n'a aucune preuve au dépôt. | tests nommés, VM-09 grec seul |
| D en-tête | Publication Microsoft Store.md:5-8, :322 | Version cible 1.3.0, publiée 1.1.0, document daté du 2026-07-06. | — |
| D.1 | Publication Microsoft Store.md:245, :247-254 | Huit étapes de release, dont trois arrêts humains : WACK, Partner Center, synchro GitHub. | — |
| D.2 | Publication Microsoft Store.md:266, :268, :270, :271-276, :277-282, :284-287 | Procédure GitHub avec deux arrêts humains explicites : tag annoté et push après publication. | — |
| D post-pub | Publication Microsoft Store.md:309-313, :314 | DPI 100 à 175 % à tester sur la version installée ; RGPD traité séparément. | — |
| D chemins | Publication Microsoft Store.md:264 ; bundle-msix:42 | La procédure cite une arborescence antérieure à la migration en bundle wiki. | non vérifié sur disque |
| E en-tête | docs/audit-v1.2.0/README.md:454, :456 | Un garde-fou dont on n'a jamais vu un échec ne prouve rien. | — |
| E n°1 | README.md:458-460 ; ToastActivationTests.cs:89-104 ; manifestes-bundle:56 | Corrigé : le test charge le manifeste et porte deux témoins réciproques. | ToastActivationTests |
| E n°2 | README.md:461-462 | Non corrigé : deux `Assert.Contains` sur le texte d'un `.ps1` jamais exécuté. | aucun témoin |
| E n°3 | README.md:463-465 | Non corrigé : aucun test pour les deux scanners bloquants en CI. | aucun |
| E n°4 | README.md:466-468 ; keyboard-platform.md:224-227 ; Sync-LayoutResources.ps1:2 | Non corrigé : `-SyncPublicRepo` copie des fichiers sur eux-mêmes en annonçant un succès. | aucun |
| E n°5 | README.md:469-471 ; list-identity-literals.py:49-53 | Non corrigé : `SKIP_DIRS` saute deux projets devenus du code de production. | aucun |
| E témoins | witness-embedded-resources.py:1 ; rapport.md:225 ; witness-lot-f.py:12 ; witness-lot-b.py:9 ; recette:239 ; correctifs:86 ; bundle-msix:47, :68 | Témoins existants et angles morts assumés ; `TO-DO.md` reste un WARNING permanent. | — |
| F scan | scan-vulnerabilites:9, :11-14, :16-19, :23-30, :32 | Six projets scannés sur un index joignable : les trois du paquet Store sont propres. | — |
| F dette | scan-vulnerabilites:38-39, :44-49, :56, :58, :63, :66 | Deux High transitives par `xunit 2.6.0` côté tests ; montée reportée à la v2.0.0. | — |
| F SDK | rebuild-sdk:10-12, :14, :18, :62-65 ; verification-corrections.json:232-235 | SDK 8.0.425 et runtime 8.0.31 (5 CVE), fin de support .NET 8 au 2026-11-10. | — |
