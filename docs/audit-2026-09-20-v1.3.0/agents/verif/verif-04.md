# Vérification 04-empaquetage-ci-docs.md
Citations : 200 total · EXACT 178 · DÉCALÉ 0 · ABSENT 0 · NON VÉRIFIABLE 22

> Rapport > 150 citations : **toutes** ont été vérifiées, aucun échantillonnage.
> Unité de compte = un couple (ligne de table × fichier:ligne) ; une ligne qui cite deux fichiers compte deux fois.
> `04` n'a pas de colonne « test qui couvre » → colonne « test cité » = `—` partout.
> Légende : `AM`=msix/AppxManifest.xml · `CP`=src/AZERTYGlobal.csproj · `VR`=scripts/Verify-Release.ps1 · `PM`=scripts/Pack-MSIX.ps1 · `AS`=scripts/Archive-StableBundle.ps1 · `SL`=scripts/Sync-LayoutResources.ps1 · `GA`=scripts/gen-appinstaller.py · `CI`=.github/workflows/ci.yml · `SA`=.github/workflows/store-analytics.yml · `FS`=msix/Fiche Store.md · `MR`=msix/README.md · `CS`=Cahier des charges.md · `AX`=entreprise/AZERTYGlobal.admx · `RG`=entreprise/politiques-exemple.reg · `SV`=docs/audit-2026-09-15-v1.2.0/scan-vulnerabilites-2026-09-19.md · `RS`=docs/…/rebuild-sdk-8.0.425-2026-09-19.md · `BM`=docs/…/bundle-msix-2026-09-19.md

## Décalées et absentes (détail)

| section | fichier:ligne cité | verdict | vraie ligne ou « nulle part » | citation (≤ 60 car.) |
|---|---|---|---|---|
| — | — | — | **aucune** : 0 DÉCALÉ, 0 ABSENT | — |

Les 22 NON VÉRIFIABLE ne portent pas de citation de ligne : 1.15, 1.16, 2.19, 2.22, 4.13, 4.19, 4.26, 4.27, 5.13, 5.14, 7.10, 7.15, 8.1, 8.4, 8.6, 8.7, 9.13, 9.14, 9.18, 10.1, 10.2, 10.4 — sorties de `git`/`grep`, décomptes de `def test_`, listings de dossier, mesures binaires (dimensions PNG, BOM, CRLF).
Hors dépôt mais lus et confirmés : 2.20 (`.agent/CONTEXT_APP_MICROSOFT_STORE.md:3`), 2.21 (`.agent/CONTEXT_AZERTY_GLOBAL.md:91`).

## Constats à citation confirmée (EXACT ou DÉCALÉ), condensés

| section | fichier:ligne | constat en une phrase | test cité |
|---|---|---|---|
| 1.1 | AM:11-14 | Identité en 4 segments alignée sur le csproj ; l'arch source est réécrite au pack. | — |
| 1.2-1.3 | AM:17, :20, :38, :39 | DisplayName (43 car.) et description (101 car.) identiques aux deux endroits, en français seul. | — |
| 1.4 | AM:28-29 | fr-FR et en-US déclarées, mais le paquet n'embarque aucun `resources.pri`. | — |
| 1.5 | AM:24 | MinVersion = TFM 17763 ; MaxVersionTested 26100 sous le poste de build 26200. | — |
| 1.6-1.7 | AM:88, :53-56 | `runFullTrust` seule capacité ; StartupTask `Enabled="false"`, TaskId inchangé depuis la 1.1.0. | — |
| 1.8 | AM:71, :79 | Le CLSID d'activation toast est déclaré deux fois, à l'identique. | — |
| 1.9 | src/ToastActivation.cs:54 | Le CLSID du code est identique caractère pour caractère au manifeste. | — |
| 1.10 | AM:2-8 | Cinq espaces de noms, quatre ignorables ; aucun uap3/uap5, aucune extension de protocole. | — |
| 1.11-1.14 | AM:19, :41, :42, :43 | Les quatre assets référencés existent, aux dimensions nominales. | — |
| 2.1-2.11 | CP:10 · Program.cs:10 · AssemblyInfo.cs:8,9,12 · AM:13 · Changelog.md:3 · FS:79,218 · Publication Microsoft Store.md:5,7 | Onze sources portent littéralement 1.3.0 ou 1.3.0.0. | — |
| 2.12 | README.md:17 | Le README annonce encore 1.2.0 et « aucun package produit ni soumis ». | — |
| 2.13-2.18 | Distribution Entreprises.md:4, :18 · Note RGPD - Établissements.md:4 · AX:27-28 · fr-FR/AZERTYGlobal.adml:17 · MR:204 | Six sources restées littéralement en 1.2.0, dont le libellé ADMX lu par une DSI. | — |
| 2.20 | .agent/CONTEXT_APP_MICROSOFT_STORE.md:3 | « Version actuelle : 1.3.0 » (fichier hors dépôt). | — |
| 2.21 | .agent/CONTEXT_AZERTY_GLOBAL.md:91 | v1.3.0 annoncée en préparation (fichier hors dépôt). | — |
| 2.23 | scripts/check-doc-versions.py:130 | Aucun littéral de version : le csproj fait foi par regex. | — |
| 2.24 | CS:293 | Le cahier des charges ne porte pas de version, seulement la date 2026-08-24. | — |
| 3.11-3.28 | Changelog.md:9, :11, :14-18, :20-24, :28-31, :32 | Six commits de code sont couverts par une ligne de changelog : menu, couches, suspension, trace, avis, ports. | — |
| 4.1 | VR:129 | La version de référence vient uniquement de `<Version>` du csproj. | — |
| 4.2-4.3 | VR:136-138, :144-146 | `Program.cs` et les trois attributs d'`AssemblyInfo.cs` doivent être alignés, sinon `throw`. | — |
| 4.4 | VR:147 | Le contrôle porte sur le manifeste source, pas sur ceux du bundle. | — |
| 4.5 | VR:149-150 | Les deux formes « Version X : » et « Version X: » de la Fiche Store sont exigées. | — |
| 4.6 | VR:154 | Seule la présence de la section de changelog est vérifiée, pas son contenu. | — |
| 4.7 | VR:151-156, :40 | `Assert-MatchIfExists` n'émet qu'un `Write-Warning` quand le fichier manque. | — |
| 4.8 | VR:160-177, :3 | La `FileVersion` du binaire publié est contrôlée pour x64 et arm64. | — |
| 4.9 | VR:186-190 | Le bundle stable doit avoir la même empreinte que le bundle versionné. | — |
| 4.10 | VR:194-207 | L'empreinte du publish est comparée à l'exe du `.msix` interne, sur deux architectures. | — |
| 4.11 | PM:83-91 | Le seul contrôle sur le binaire est son existence. | — |
| 4.12 | PM:125 | Le binaire trouvé est copié et empaqueté tel quel. | — |
| 4.14 | AS:6-7 | Le manifeste de bundle doit être unique, comparaison sensible à la casse. | — |
| 4.15-4.18 | AS:10-11, :44-46, :53, :55 | XXE fermé, garde de traversée, copie revérifiée par empreinte, preuve JSON écrite. | — |
| 4.20 | SL:27-30 | Le repli du clone public est `$projectRoot` lui-même. | — |
| 4.21 | SL:147 | Quand le repli joue, le script se copie sur lui-même. | — |
| 4.22 | SL:60-63 | Empreintes égales par construction : « INCHANGE » est affiché et rien n'est écrit. | — |
| 4.23 | SL:22-26, :18-21 | Le commentaire dit que `..\website` n'existe plus et que le script levait avant. | — |
| 4.24 | GA:154-160, :86 | Le script refuse tout bundle portant l'identité du canal Store. | — |
| 4.25 | GA:70, :76 | Les URI du `.appinstaller` et du bundle sont écrites sans version. | — |
| 5.1 | CI:3-8 | `release/**` couvre la branche courante ; `workflow_dispatch` présent. | — |
| 5.2 | CI:10-14 | `permissions:` est déclaré au niveau workflow, cinq portées. | — |
| 5.3 | CI:22-33 | Le job `provenance` a un timeout de 5 min et pas de `continue-on-error`. | — |
| 5.4 | CI:35-36 | Le job `build` n'a aucun `timeout-minutes`. | — |
| 5.5 | CI:67-71 | ARM64 et x64 sont publiés en `Release` en CI. | — |
| 5.6 | CI:73-80 | Les trois projets de test sont lancés un par un en `Release`. | — |
| 5.7 | CI:82-86 | `Pack-MSIX.ps1` puis `Verify-Release.ps1` s'enchaînent. | — |
| 5.8 | CI:88-94 | BinSkim porte le seul `continue-on-error` du fichier. | — |
| 5.9-5.10 | CI:115-120, :122-125 | Upload du glob des bundles, rétention 90 jours, puis attestation de provenance. | — |
| 5.11 | CI:26, 28, 38, 40, 63, 116, 123, 129 | Toutes les actions sont épinglées par tag mobile, aucune par SHA. | — |
| 5.12 | CI:62-65 | Le SDK `8.0.x` flotte, il n'est épinglé nulle part. | — |
| 5.15-5.17 | SA:4-12, :14-16, :18-20 | Exécution quotidienne plus dispatch, permissions déclarées, concurrence sans annulation. | — |
| 5.18 | SA:27-32 | Trois secrets Partner Center et l'ID Store en clair. | — |
| 5.19 | SA:59-64 | Connexion Azure par OIDC avec trois secrets. | — |
| 5.20 | SA:35, 37, 52, 59 | Actions épinglées par tag, pas par SHA. | — |
| 5.21 | SA:50-57 | L'étape de copie de diagnostic supprimée sur `main` subsiste sur la branche release. | — |
| 5.22 | SA:44-45 | Les tests analytics tournent par `unittest discover`. | — |
| 6.1-6.4 | SV:16-19, :25-27, :28-30, :38-39, :66-67 ; AZERTYGlobal.Tests.csproj:17 | Trois projets livrés sans vulnérabilité sur un index joignable ; 2 High transitives côté tests, `xunit` 2.6.0 inchangé ; scan valable à sa date seulement. | — |
| 6.5-6.6 | RS:10-12, :18-20 | SDK 8.0.425 et runtime 8.0.31 conformes au poste ; .NET 8 sort de support le 2026-11-10. | — |
| 7.1-7.8 | CP:23, 27, 28, 29, 30, 35, 38, 43 | Huit propriétés AOT/durcissement déclarées sur le seul projet principal. | — |
| 7.9 | CP:46-47 ; TypingEngine.Windows.csproj:7-8 | Analyseurs .NET activés au niveau `latest` dans les deux projets. | — |
| 7.11 | CP:19 | `GenerateAssemblyInfo` désactivé, motivé par un warning WACK. | — |
| 7.12 | CP:51 ; src/app.manifest:14 | Manifeste applicatif déclaré, `dpiAwareness` PerMonitorV2. | — |
| 7.13 | docs/…/compilation-native-x64.log (4 l.) | Le log ne contient ni « warning » ni IL2xxx/IL3xxx. | — |
| 7.14 | docs/…/compilation-native-arm64.log (5 l.) | Même constat, avec en plus la ligne « Generating native code ». | — |
| 8.2 | .gitignore:8-9 | `*.msix` et `*.msixbundle` sont exclus du dépôt. | — |
| 8.3 | .gitignore:10 | Les rapports WACK sont exclus. | — |
| 8.5 | .gitignore:36-39 | Suivi interne (TO-DO, Publication, Distribution) tenu hors dépôt public. | — |
| 8.8-8.11 | LICENCE:1-2 ; README.md:128 ; CP:14 ; AssemblyInfo.cs:6 ; Program.cs:2 | EUPL 1.2 annoncée de façon cohérente en cinq endroits. | — |
| 9.1 | README.md:17 | Trois assertions fausses aujourd'hui : version, manifeste et « aucun package produit ». | — |
| 9.2-9.3 | README.md:28, :38 | Prérequis cohérents avec `MinVersion` et le TFM ; statistiques locales annoncées sans vérification de code. | — |
| 9.4 | README.md:9 | Le README emploie « 99 % des habitudes », contraire à la formulation imposée. | — |
| 9.5 | README.md:3, :5 ; src/ProductIdentity.cs:43 | L'ID Store du README correspond à celui du code. | — |
| 9.6 | MR:81 | La liste documentée correspond à `Verify-Release.ps1`, mais `TO-DO.md` n'existe pas. | — |
| 9.7 | MR:204 | Pied de page non rebasculé en 1.3.0. | — |
| 9.8 | Distribution Entreprises.md:18, :34 | Document périmé d'une version, dernière mise à jour 2026-08-21. | — |
| 9.9 | Publication Microsoft Store.md:19 | La plus récente section « État vérifié » date du 2026-08-21 et porte sur la v1.2.0. | — |
| 9.10 | FS:24 | Le claim FR de la Fiche Store est conforme : « 99 % DES FRAPPES PRÉSERVÉES ». | — |
| 9.11 | FS:159 ; store-v120/brief-chatgpt-images-store.md:13 | Le pendant anglais dit encore « HABITS » alors que la consigne est écrite dans le dépôt. | — |
| 9.12 | README.md:9 ; msix/Assets/screenshot-5-changements.html:273 | Deux autres survivances du claim interdit, dont une dans un gabarit de capture. | — |
| 9.15 | FS:79, :218 | Les deux formes exactes attendues par `Verify-Release.ps1` sont présentes. | — |
| 9.16-9.17 | FS:362-365, :367-370 | Quatre captures listées, dimensions re-mesurées identiques et toutes au-dessus de 1366×768. | — |
| 9.19 | FS:289-292 | « ~5 Mo par architecture » face à 3,3 et 3,2 Mio compressés mesurés. | — |
| 9.20-9.22 | CS:259-268, :271-277, :280-281 | 10 + 7 + 2 cases cochées, aucune datée de la 1.3.0 et aucun fichier de mesure de performance. | — |
| 9.23 | CS:284-289 | Cinq cases Couches maintenables restent non cochées, dont le champ de mot de passe. | — |
| 9.24 | BM:74-83 | Le seul rapport WACK du dépôt porte sur 1.2.0.0. | — |
| 9.25 | BM:26-34 | La seule trace d'exécution de `Verify-Release` consignée porte sur 1.2.0. | — |
| 9.26 | MR:176 ; GA:27 | Aucun `.appinstaller` ni preuve d'installation sur machine propre. | — |
| 9.27 | MR:192 | Le piège `Cache-Control` du Worker est consigné, non résolu ici. | — |
| 10.3 | RG:1 | En-tête « Windows Registry Editor Version 5.00 » attendu par `reg import`. | — |
| 10.5-10.6 | AX:9 ; RG:19 | Même racine HKLM\SOFTWARE\Policies\AZERTYGlobal des deux côtés, lue en vue 64 bits. | — |
| 10.7 | AX:38, 48, 58, 71, 82 | Le même `key=` est répété cinq fois à l'identique. | — |
| 10.8-10.10 | AX:39, :49, :59 ; RG:24, :29, :33 | `NotificationsEnabled`, `UsageStatsEnabled`, `ExternalLinksEnabled` : mêmes noms ADMX et `.reg`, enabled 1 / disabled 0. | — |
| 10.11-10.12 | AX:68, :72, :86 ; RG:38, :42 | Stratégie `RemoveOnboarding` pour la valeur `ShowOnboarding`, inversion assumée ; `Language` en enum fr/en requis. | — |
