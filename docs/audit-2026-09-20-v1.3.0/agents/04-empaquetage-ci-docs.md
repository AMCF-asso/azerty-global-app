# Audit v1.3.0 — axe empaquetage, manifeste, CI, scripts, docs, versions

Dépôt : `D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store`
Branche : `release/1.2.0-notation-store` — HEAD `4d5da24` (2026-09-20 19:01:08 +0200)
Base de branche : `8775489` — 29 commits.
Lecture seule. Aucune écriture, aucun `git` mutant, aucun `dotnet build/publish/test`.
Constats ancrés uniquement — aucune gravité, aucun verdict global.

---

## 1. Manifeste MSIX

Fichier source : `D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store\msix\AppxManifest.xml` (4 038 octets).

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 1.1 | `msix/AppxManifest.xml:11-14` | `Name="AZERTYGlobal.AZERTYGlobal"` / `Publisher="CN=7FD049E3-1C58-42E0-A07F-A9712DE19E38"` / `Version="1.3.0.0"` / `ProcessorArchitecture="x64"` | Identité en 4 segments, alignée sur `<Version>1.3.0</Version>` du csproj (+`.0`). `ProcessorArchitecture` est réécrit par `Pack-MSIX.ps1:130` pour chaque arch ; la valeur du fichier source (`x64`) n'est donc jamais celle du paquet arm64. |
| 1.2 | `msix/AppxManifest.xml:17` et `:38` | `<DisplayName>AZERTY Global - Clavier français amélioré</DisplayName>` | Même chaîne dans `Properties/DisplayName` et dans `uap:VisualElements@DisplayName`. 43 caractères. |
| 1.3 | `msix/AppxManifest.xml:20` et `:39` | `L'AZERTY corrigé : majuscules accentuées, point direct, symboles dev accessibles, langues étrangères.` | Description identique aux deux endroits, 101 caractères. Écrite en français seulement, dans un paquet qui déclare `en-US`. |
| 1.4 | `msix/AppxManifest.xml:28-29` | `<Resource Language="fr-FR" />` / `<Resource Language="en-US" />` | Deux langues déclarées. L'app sert bien FR et EN (`src/Localization/`, `ConfigManager.AppLanguage` lu en `Program.cs:23`). Mais le paquet ne contient **aucun `resources.pri`** (voir 1.10) : les chaînes du manifeste (DisplayName, Description) restent en français dans les deux cas. |
| 1.5 | `msix/AppxManifest.xml:24` | `<TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26100.0" />` | `MinVersion` = TFM `net8.0-windows10.0.17763.0` (`src/AZERTYGlobal.csproj:5`). `MaxVersionTested` 10.0.26100 ; le poste de build est en 10.0.26200 (`docs/…/bundle-msix-2026-09-19.md:80`). |
| 1.6 | `msix/AppxManifest.xml:88` | `<rescap:Capability Name="runFullTrust" />` | Capacité unique. Aucune `uap:Capability`, aucune `DeviceCapability`. |
| 1.7 | `msix/AppxManifest.xml:53-56` | `<desktop:StartupTask TaskId="AZERTYGlobalStartup" Enabled="false" DisplayName="AZERTY Global" />` | `TaskId` identique à celui documenté comme servi par la 1.1.0 (`docs/audit-2026-09-15-v1.2.0/manifestes-bundle-2026-09-19.md:67`). `Enabled="false"`. |
| 1.8 | `msix/AppxManifest.xml:71` et `:79` | `Id="126A58B4-3200-43A6-9018-612C108F4A94"` / `ToastActivatorCLSID="126A58B4-3200-43A6-9018-612C108F4A94"` | |
| 1.9 | `src/ToastActivation.cs:54` | `internal const string ActivatorClsidString = "126A58B4-3200-43A6-9018-612C108F4A94";` | **Identique caractère pour caractère** aux deux déclarations du manifeste. Aucun écart. |
| 1.10 | `msix/AppxManifest.xml:2-8` | `IgnorableNamespaces="uap rescap desktop com"` | 5 espaces de noms déclarés (`foundation`, `uap`, `rescap`, `desktop`, `com`), 4 ignorables. Aucun `uap3`/`uap5`. Aucune extension de protocole (`uap:Protocol`) : le code utilise `ms-windows-store://review/` (`src/ProductIdentity.cs:54-55`) en sortie, pas en entrée. |

### Assets référencés vs fichiers présents

En-têtes PNG lus en Python (octets 16-23, big-endian) dans `msix/Assets/`.

| # | référence | fichier | pixels lus | constat |
|---|---|---|---|---|
| 1.11 | `msix/AppxManifest.xml:19` `<Logo>Assets\StoreLogo.png</Logo>` | `msix/Assets/StoreLogo.png` | **50×50**, 5 469 o | présent |
| 1.12 | `msix/AppxManifest.xml:41` `Square150x150Logo="Assets\Square150x150Logo.png"` | idem | **150×150**, 29 298 o | présent |
| 1.13 | `msix/AppxManifest.xml:42` `Square44x44Logo="Assets\Square44x44Logo.png"` | idem | **44×44**, 4 480 o | présent |
| 1.14 | `msix/AppxManifest.xml:43` `Wide310x150Logo="Assets\Wide310x150Logo.png"` | idem | **310×150**, 18 016 o | présent |
| 1.15 | — | `Square44x44Logo.scale-200.png` | **88×88**, 12 893 o | variante d'échelle ×2, empaquetée (`scripts/Pack-MSIX.ps1:27`) |
| 1.16 | — | `Square150x150Logo.scale-200.png` | **300×300**, 85 044 o | variante d'échelle ×2, empaquetée (`scripts/Pack-MSIX.ps1:28`) |

**Les quatre assets référencés existent, aux dimensions nominales.** Variantes présentes : `.scale-200` pour Square44x44 et Square150x150 seulement.

Constats d'absence, mesurés :

- `msix/Assets/LargeTile.png` existe (**310×310**, 88 179 o) mais **n'est référencé nulle part** dans le manifeste (aucun `Square310x310Logo`) et n'est pas dans la liste blanche `scripts/Pack-MSIX.ps1:23-30`.
- Aucune variante `Square44x44Logo.targetsize-16/24/32/48/256.png`, aucune `altform-unplated`. Déjà consigné : `docs/audit-2026-09-15-v1.2.0/manifestes-bundle-2026-09-19.md:115-118` — « Le paquet n'embarque pas de variantes `targetsize-*` ni `altform-unplated` ».
- `msix/Assets/StoreLogo.png` est en 50×50 ; aucune variante `.scale-*` pour lui.
- Aucun `Square71x71Logo`, aucun `uap:SplashScreen`, aucun `uap:ShowNameOnTiles`.

### Le paquet réellement construit (lu dans le bundle, pas dans les sources)

`msix/AZERTYGlobal-1.3.0.0.msixbundle` — 6 799 981 octets, mtime 2026-09-20 17:27:22, SHA-256 `960A4340F3FA1AEF089DFAEE967F6E38F84FC898357AA9A6BCC026CDD9DBC031`.

```
  AZERTYGlobal-1.3.0.0-x64.msix                         3456295
  AZERTYGlobal-1.3.0.0-arm64.msix                       3341695
  AppxMetadata/AppxBundleManifest.xml                      1260
  AppxBlockMap.xml                                          422
  [Content_Types].xml                                       364
```

```
<Identity Name="AZERTYGlobal.AZERTYGlobal" Publisher="CN=7FD049E3-1C58-42E0-A07F-A9712DE19E38" Version="1.3.0.0"/>
<Package Type="application" Version="1.3.0.0" Architecture="x64" FileName="AZERTYGlobal-1.3.0.0-x64.msix" Offset="59" Size="3456295">
<Package Type="application" Version="1.3.0.0" Architecture="arm64" FileName="AZERTYGlobal-1.3.0.0-arm64.msix" Offset="3456439" Size="3341695">
```

- **Aucune entrée `AppxSignature.p7x`** dans le bundle ni dans les deux `.msix` internes → non signé.
- Contenu de chaque `.msix` (10 entrées) : les 6 assets de la liste blanche, `AZERTY%20Global.exe`, `AppxManifest.xml`, `AppxBlockMap.xml`, `[Content_Types].xml`. **Pas de `resources.pri`** malgré les deux `<Resource Language>` déclarées.
- Manifestes internes : 3 817 o (x64) / 3 819 o (arm64) — seul `ProcessorArchitecture` diffère, identité `1.3.0.0` dans les deux, CLSID de toast présent deux fois dans chacun.
- En-têtes PE des exécutables empaquetés : x64 `Machine = 0x8664` (8 178 688 o, SHA-256 `DF69897528D0171E37CD8E65D6A0E6E82BAE425B01DC546CDABBC11FAE6DED25`), arm64 `Machine = 0xAA64` (8 535 552 o, SHA-256 `A36F74C2EEF995F3D9F58CC88710C04A8C969F4B0F0ABBC980B3C3197613C614`). Deux architectures réellement distinctes.

---

## 2. Sources de version

`<Version>` du csproj = `1.3.0`. Table exhaustive des endroits où une version est écrite.

| # | fichier:ligne | valeur littérale |
|---|---|---|
| 2.1 | `src/AZERTYGlobal.csproj:10` | `<Version>1.3.0</Version>` |
| 2.2 | `src/Program.cs:10` | `internal const string Version = "1.3.0";` |
| 2.3 | `src/Properties/AssemblyInfo.cs:8` | `[assembly: AssemblyFileVersion("1.3.0.0")]` |
| 2.4 | `src/Properties/AssemblyInfo.cs:9` | `[assembly: AssemblyInformationalVersion("1.3.0")]` |
| 2.5 | `src/Properties/AssemblyInfo.cs:12` | `[assembly: AssemblyVersion("1.3.0.0")]` |
| 2.6 | `msix/AppxManifest.xml:13` | `Version="1.3.0.0"` |
| 2.7 | `Changelog.md:3` | `## Version 1.3.0 — 19 septembre 2026` |
| 2.8 | `msix/Fiche Store.md:79` | `Version 1.3.0 :` (FR) |
| 2.9 | `msix/Fiche Store.md:218` | `Version 1.3.0:` (EN) |
| 2.10 | `Publication Microsoft Store.md:5` | `Version cible : 1.3.0` |
| 2.11 | `Publication Microsoft Store.md:7` | `Package Store : 1.3.0.0` |
| 2.12 | `README.md:17` | `**État du code :** version 1.2.0, manifeste MSIX local en 1.2.0.0, portes de version de `Verify-Release.ps1` franchies. Aucun package n'a encore été produit ni soumis.` |
| 2.13 | `Distribution Entreprises.md:4` | `version-app: 1.2.0` |
| 2.14 | `Distribution Entreprises.md:18` | `- v1.2.0 préparée et vérifiée dans le dépôt, non encore soumise au Microsoft Store.` |
| 2.15 | `entreprise/Note RGPD - Établissements.md:4` | `version-app: 1.2.0` |
| 2.16 | `entreprise/AZERTYGlobal.admx:27-28` | `<definition name="SUPPORTED_AZERTY_GLOBAL_1_2_0"` |
| 2.17 | `entreprise/fr-FR/AZERTYGlobal.adml:17` | `<string id="SUPPORTED_AZERTY_GLOBAL_1_2_0">AZERTY Global 1.2.0 ou version ultérieure</string>` |
| 2.18 | `msix/README.md:204` | `*Dernière mise à jour : 2026-09-15 (v1.2.0 — contrôle explicite du `.appinstaller` du canal AMCF)*` |
| 2.19 | `msix/` (noms de fichiers) | `AZERTYGlobal-1.2.0.0.msixbundle`, `AZERTYGlobal-1.2.0.1.msixbundle`, `AZERTYGlobal-1.2.0.2.msixbundle`, `AZERTYGlobal-1.3.0.0.msixbundle`, `AZERTYGlobal.msixbundle` |
| 2.20 | `.agent/CONTEXT_APP_MICROSOFT_STORE.md:3` (hors dépôt) | `> **Version actuelle** : 1.3.0` |
| 2.21 | `.agent/CONTEXT_AZERTY_GLOBAL.md:91` (hors dépôt) | `- **Application Microsoft Store / MSIX** v1.3.0 en préparation…` |
| 2.22 | `scripts/gen-appinstaller.py` | **aucune version littérale** — l'identité est lue dans le manifeste du bundle (`:112-143`), `SELF_URI`/`BUNDLE_URI` sont sans version (`:70`, `:76`) |
| 2.23 | `scripts/check-doc-versions.py:130` | `re.search(r"<Version>\s*([^<\s]+)\s*</Version>", texte_csproj)` — pas de littéral, le csproj fait foi |
| 2.24 | `Cahier des charges.md:293` | `*Dernière mise à jour : 2026-08-24*` (pas de numéro de version) |

**Constat** : 11 sources sont en 1.3.0 ou 1.3.0.0 ; **7 sont restées en 1.2.0** (2.12, 2.13, 2.14, 2.15, 2.16, 2.17, 2.18). Aucune des sept n'est vérifiée par `Verify-Release.ps1`.

### `python scripts/check-doc-versions.py` — sortie verbatim, code de retour 1

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

`EXIT=1`. Le script est en lecture seule (aucun `write`/`open(..., "w")` : vérifié, seules des lectures `read_text` en `:254` et `:273`). **Note** : le doc `rebuild-sdk-8.0.425-2026-09-19.md:29` consignait le 19/09 « `documents-corrections` | 1 **attendu** — **0 erreur**, 32 attentes ». Aujourd'hui : **9 erreurs**, même nombre d'attentes — l'écart date du passage du csproj en 1.3.0 (`5ce2a67`, 2026-09-19).

---

## 3. Changelog vs commits

`git log --format="%h %ad %s" --date=short 8775489..HEAD` → 29 commits (compte confirmé par `rev-list --count`).

`git diff --stat 8775489..HEAD -- src` → **40 fichiers, 2 169 insertions, 505 suppressions**.

| # | commit | date | portée | ligne de Changelog 1.3.0 ? |
|---|---|---|---|---|
| 3.1 | `d5c74f3` | 2026-09-15 | 64 f. — `README.md`, `docs/…`, `msix/` | — (correctifs de l'audit v1.2.0, hors section 1.3.0) |
| 3.2 | `8d9652f` | 2026-09-18 | 10 f. — `msix/Fiche Store.md`, `msix/Assets/store-v120/` | — (aucune ligne ; concerne la fiche Store, pas l'app) |
| 3.3 | `8133454` | 2026-09-19 | 9 f. — `docs/` | non (doc d'audit) |
| 3.4 | `1acb0f5` | 2026-09-19 | 2 f. — `docs/`, `scripts/` | non (correction de profondeur dans `Verify-Release.ps1`) |
| 3.5 | `55e7d6c` | 2026-09-19 | 1 f. — `docs/` | non |
| 3.6 | `2f5bb68` | 2026-09-19 | 1 f. — `docs/` | non |
| 3.7 | `0c80ea7` | 2026-09-19 | 1 f. — `docs/` | non |
| 3.8 | `f078906` | 2026-09-19 | 1 f. — `docs/` | non |
| 3.9 | `c9abdc9` | 2026-09-19 | 1 f. — `docs/` | non |
| 3.10 | `0205f3a` | 2026-09-19 | 1 f. — `docs/` | non |
| 3.11 | `edfe727` | 2026-09-19 | 3 f. — `src/TypingEngine.Windows/`, tests | **oui** — `Changelog.md:28-31` « Ouvrir la zone de notification ou cliquer la barre des tâches suffisait à suspendre le remapping » |
| 3.12 | `996405e` | 2026-09-19 | 1 f. — `src/TypingEngine.Windows/` | **oui** — `Changelog.md:32` « Trace de compatibilité enrichie : mode, motif, `hasFg`, `pid` et `tracking` » |
| 3.13 | `ae90808` | 2026-09-19 | 1 f. — `docs/` | non |
| 3.14 | `cbe5791` | 2026-09-19 | 1 f. — `docs/` | non |
| 3.15 | `2e4d8dc` | 2026-09-19 | 2 f. — `src/`, `src/Localization/` | **oui** — `Changelog.md:9` « le menu est réordonné en quatre blocs » |
| 3.16 | `6ce3cbf` | 2026-09-19 | 2 f. — `src/`, `src/Localization/` | **oui** — `Changelog.md:11` « Dix-huit lignes deviennent douze » |
| 3.17 | `5ce2a67` | 2026-09-19 | 2 f. — `msix/`, `src/` | non (bascule de version, pas un changement produit) |
| 3.18 | `c1da0d6` | 2026-09-19 | 4 f. — `Changelog.md`, `msix/`, `src/`, `src/Properties/` | non (idem) |
| 3.19 | `7e29aaf` | 2026-09-19 | 1 f. — `docs/` | non |
| 3.20 | `17382e0` | 2026-09-19 | 2 f. — `docs/`, `src/Localization/` | **absent du Changelog** — « 99 % de vos frappes preservees, pas de vos habitudes » touche une chaîne visible (accueil) et n'est cité nulle part en 1.3.0 |
| 3.21 | `ddd1557` | 2026-09-19 | 1 f. — `docs/` | non |
| 3.22 | `71cc69d` | 2026-09-19 | 1 f. — `docs/` | non |
| 3.23 | `2a10fe3` | 2026-09-20 | 5 f. — `store-analytics/` | non (hors app) |
| 3.24 | `404d68a` | 2026-09-20 | 8 f. — `src/SettingsWindow.cs` (+227), `src/Win32.cs` (+279/-…), `src/TypingEngine.Windows/KeyboardHook.cs` (158 l.), `src/TrayApplication.cs`, tests | **absent du Changelog et de la Fiche Store.** `grep -n -i "alt+tab\|ctrl+maj+w\|fenêtre Paramètres" Changelog.md` → aucune occurrence ; idem dans `msix/Fiche Store.md`. C'est le plus gros commit de code de la branche après les ports. |
| 3.25 | `4451bf5` | 2026-09-20 | 1 f. — `store-analytics/tools/` | non (hors app) |
| 3.26 | `d356841` | 2026-09-20 | 2 f. — `src/ConfigManager.cs`, tests | **oui** — `Changelog.md:14-18` « Le drapeau hérité `reviewPromptDone` ne vaut plus une sollicitation consommée » |
| 3.27 | `fb2ae86` | 2026-09-20 | 1 f. — `src/ConfigManager.cs` (fins de ligne) | non (cosmétique) |
| 3.28 | `06208ed` | 2026-09-20 | 6 f. — `src/ProductIdentity.cs`, `src/TrainingReminders.cs`, `src/TrayApplication.cs`, `src/lessons.json`, tests | **oui** — `Changelog.md:20-24` « Correctifs isolés repris de `main` » |
| 3.29 | `4d5da24` | 2026-09-20 | 1 f. — `Changelog.md` | — (c'est le commit de changelog) |

### Lignes de changelog sans commit identifié dans la plage

- `Changelog.md:10` « Cocher une couche alors que l'interrupteur principal `maintainableLayersEnabled` est éteint l'allume » — couvert par `2e4d8dc`/`6ce3cbf` (mêmes fichiers), pas de commit dédié.
- `Changelog.md:36` « Corrigé et vérifié en VM le 2026-09-19 à 13:44 » — **preuve datée du 19/09**, alors que trois commits de code (`d356841`, `fb2ae86`, `06208ed`, tous du 20/09 18:58→19:00) sont postérieurs et ne sont couverts par aucune vérification VM citée.

### Dates

- `Changelog.md:3` : `## Version 1.3.0 — 19 septembre 2026`. Les quatre derniers commits de la branche, dont trois de code, sont datés du **2026-09-20**. Le titre de section précède le contenu qu'il décrit d'un jour.
- `msix/Fiche Store.md:79-82` (notes de version FR 1.3.0) ne liste que trois puces : menu, couches, correction de suspension. Ni le rattrapage d'avis (`Changelog.md:14-18`) ni les correctifs `404d68a` n'y figurent.

---

## 4. Scripts de release

### `scripts/Verify-Release.ps1` (212 lignes)

Ce qu'il vérifie **littéralement** :

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 4.1 | `scripts/Verify-Release.ps1:129` | `$storeVersion = if (($version -split '\.').Count -eq 4) { $version } else { "$version.0" }` | source unique = `<Version>` du csproj |
| 4.2 | `:136-138` | `if ($programVersion -ne $version) { throw "Program.cs n'est pas aligné sur $version" }` | `Program.cs` |
| 4.3 | `:144-146` | `if ($fileVersion -ne $storeVersion) { throw "AssemblyFileVersion n'est pas aligné sur $storeVersion" }` | 3 attributs d'`AssemblyInfo.cs` |
| 4.4 | `:147` | `if ($manifest.Package.Identity.Version -ne $storeVersion) { throw "AppxManifest.xml n'est pas aligné sur $storeVersion" }` | **manifeste source uniquement**, pas les manifestes internes du bundle |
| 4.5 | `:149-150` | `Assert-Match $fichePath ("Version {0} :" …)` / `("Version {0}:" …)` | Fiche Store FR et EN |
| 4.6 | `:154` | `Assert-Match $changelogPath ("## Version {0}" …)` | présence de la section, pas son contenu |
| 4.7 | `:151-153`, `:155-156` | `Assert-MatchIfExists` | `Publication Microsoft Store.md`, `TO-DO.md`, les 2 contextes `.agent/` — **silencieux si absent** (`:40` `Write-Warning "$Label non disponible dans ce depot; verification ignoree."`) |
| 4.8 | `:160-177` | `$publishFileVersion = '{0}.{1}.{2}.{3}' -f …` puis `if ($publishFileVersion -ne $storeVersion) { throw … }` | **FileVersion du binaire publié**, pour x64 **et** arm64 (`:3` `$architectures = @('x64', 'arm64')`) |
| 4.9 | `:186-190` | `if ($stableBundleHash -ne $versionedBundleHash) { throw "Le bundle stable ne correspond pas au bundle versionne" }` | `AZERTYGlobal.msixbundle` == `AZERTYGlobal-1.3.0.0.msixbundle` |
| 4.10 | `:194-207` | `if ($publishHash -ne $bundleHash) { throw "L'exe $arch embarqué dans le bundle ne correspond pas au publish courant" }` | **empreinte publish → exe dans le `.msix` interne, sur les deux architectures.** C'est bien un contrôle à deux architectures. |

Ce qu'il **ne** vérifie **pas** (aucune ligne correspondante dans le fichier) :

- **Signature** : aucun `Get-AuthenticodeSignature`, aucun `signtool verify`, aucune recherche d'`AppxSignature.p7x`.
- **WACK** : aucune mention d'`appcert`, aucune lecture de `wack-report-*.xml`.
- **Manifestes internes du bundle** : le script n'ouvre que `msix/AppxManifest.xml` (source). Confirmé par `docs/…/manifestes-bundle-2026-09-19.md:112-114` — « `Verify-Release.ps1` n'ouvre toujours aucun manifeste ».
- **Fraîcheur du publish par rapport aux sources** : il compare publish ↔ bundle, jamais source ↔ publish.
- **Existence des assets référencés par le manifeste**.
- Les 7 sources restées en 1.2.0 listées en §2 (`README.md`, `Distribution Entreprises.md`, `entreprise/*`, `msix/README.md`).
- `:15` `$todoPath = Join-Path $projectRoot 'TO-DO.md'` — **le fichier n'existe pas** (`ls` : `No such file or directory`) et est de surcroît listé dans `.gitignore:37`. Le contrôle `:153` est donc un `WARNING` permanent, déjà consigné `docs/…/bundle-msix-2026-09-19.md:66-70`.
- `:21-24` : les deux contextes `.agent/` **existent** aujourd'hui (`D:\My files\Keyboard Layouts\.agent\`, résolus par `..\..\..\..\.agent\`) et portent 1.3.0 (`CONTEXT_APP_MICROSOFT_STORE.md:3`, `CONTEXT_AZERTY_GLOBAL.md:91`) : ces deux contrôles-là s'exécutent réellement.

### `scripts/Pack-MSIX.ps1` (185 lignes) — contrôle de fraîcheur

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 4.11 | `scripts/Pack-MSIX.ps1:83-91` | `if (-not (Test-Path $publishExe)) { … throw "Publish introuvable pour $arch : $publishExe…" }` | **Seul contrôle sur le binaire : son existence.** Aucune comparaison de date, d'empreinte ou de `FileVersion` avec les sources. |
| 4.12 | `scripts/Pack-MSIX.ps1:125` | `Copy-Item $publishExe (Join-Path $stagingDir 'AZERTY Global.exe') -Force` | le binaire trouvé est empaqueté tel quel |
| 4.13 | `docs/audit-2026-09-15-v1.2.0/recette-vm.md` (via commit `ae90808`) | `docs(recette-vm): consigner que Pack-MSIX.ps1 ne compile pas` | le point est déjà consigné dans le dépôt |

**Mesure sur l'état actuel du disque** :

```
publish x64   : 2026-09-20 17:26:17.153719900 +0200  sha256 DF698975…6DED25
publish arm64 : 2026-09-20 17:26:21.003218000 +0200  sha256 A36F74C2…13C614
bundle 1.3.0.0: 2026-09-20 17:27:22.312830500 +0200
AZERTYGlobal.msixbundle : 2026-09-20 17:27:22.312830500 +0200 (même taille, même horodatage)
```

Fichiers source modifiés **après** 17:27 :

```
2026-09-20 18:57  src/AZERTYGlobal.Tests/ReviewPromptConfigTests.cs
2026-09-20 18:59  src/AZERTYGlobal.Tests/DailyChallengeTests.cs
2026-09-20 18:59  src/AZERTYGlobal.Tests/LessonCoreTests.cs
2026-09-20 18:59  src/ProductIdentity.cs
2026-09-20 18:59  src/TrainingReminders.cs
2026-09-20 18:59  src/TrayApplication.cs
2026-09-20 19:02  src/ConfigManager.cs
```

`src/lessons.json` (mtime 2026-09-20 18:59) est déclaré `<EmbeddedResource … LogicalName="lessons.json" />` en `src/AZERTYGlobal.csproj:75` : il entre dans le binaire.

Commits correspondants : `d356841` (18:58:26), `fb2ae86` (18:59:25), `06208ed` (19:00:10).

**Constat** : les exécutables contenus dans `msix/AZERTYGlobal-1.3.0.0.msixbundle` ont été produits à 17:26, soit avant les trois derniers commits de code de la branche. L'empreinte du publish égale celle du bundle (`DF698975…` / `A36F74C2…` retrouvées à l'identique dans les deux `.msix` internes), donc **`Verify-Release.ps1` passerait vert sur ce bundle** : ses deux contrôles portent sur publish↔bundle, pas sur source↔publish. La `FileVersion` restant `1.3.0.0`, le contrôle `:173` passe aussi.

### `scripts/Archive-StableBundle.ps1` (57 lignes)

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 4.14 | `:6-7` | `$entries = @($bundle.Entries | Where-Object { $_.FullName -ceq 'AppxMetadata/AppxBundleManifest.xml' })` … `if ($entries.Count -ne 1) { throw 'Le bundle doit contenir un manifeste unique.' }` | comparaison sensible à la casse |
| 4.15 | `:10-11` | `$settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit` / `$settings.XmlResolver = $null` | XXE fermé |
| 4.16 | `:44-46` | `if (-not $archiveDirectory.StartsWith($root + …)) { throw 'Le chemin de sauvegarde sort du dossier autorisé.' }` | garde de traversée de chemin |
| 4.17 | `:53` | `if ($backupHash -cne $sourceHash) { throw 'La sauvegarde du bundle précédent ne correspond pas à sa source.' }` | la copie est revérifiée |
| 4.18 | `:55` | `[System.IO.File]::WriteAllText($backupPath + '.json', …)` | preuve JSON à côté de l'archive |
| 4.19 | `scripts/tests/test_archive_stable_bundle.py` | `def test_` ×3 | 3 tests seulement, contre 21 pour l'ADMX et 33 pour `check-doc-versions` |

### `scripts/Sync-LayoutResources.ps1` (171 lignes) — copie d'un fichier sur lui-même

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 4.20 | `:27-30` | `$publicRepoRoot = Resolve-FirstExistingPath @(<br>    (Join-Path $projectRoot '..\..\Microsoft Store - app repo'),<br>    $projectRoot<br>) 'Clone public Microsoft Store'` | le **repli est `$projectRoot` lui-même** |
| 4.21 | `:147` | `Copy-AllowedPublicFile $PSCommandPath (Join-Path $publicRootResolved 'scripts\Sync-LayoutResources.ps1')` | quand le repli joue, source et destination sont le même fichier |
| 4.22 | `:60-63` | `if ($sourceHash -ne 'absent' -and $sourceHash -eq $destinationHash) { Write-Host " - INCHANGE $name  $destinationHash"; return }` | **le bug est masqué** : les empreintes étant égales par construction, `Copy-Exact` retourne « INCHANGE » sans écrire. Le même repli s'applique aux lignes `:144-146` et `:148-150` (layout, index, lessons, `ResourceAlignmentTests.cs`, `Changelog.md`, `Fiche Store.md`). |
| 4.23 | `:22-26` | `$siteRoot = Resolve-FirstExistingPath @((Join-Path $projectRoot '..\website'), …)` | le commentaire `:18-21` dit « aucun des deux n'existe plus ici : le script levait donc avant sa premiere copie » — chemin `..\website` = `projects/azerty-global/components/website` |

### `scripts/gen-appinstaller.py` (388 lignes)

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 4.24 | `:154-160` | `if identity.publisher == STORE_PUBLISHER: raise SystemExit("Ce bundle porte l'identité du canal Store. …")` | **le script refuse le bundle actuel** : `Publisher="CN=7FD049E3-1C58-42E0-A07F-A9712DE19E38"` (`:86` `STORE_PUBLISHER`) est exactement celui du bundle 1.3.0.0. Le `.appinstaller` ne peut pas être produit tant que le bundle AMCF n'existe pas. |
| 4.25 | `:70`, `:76` | `SELF_URI = "https://download.azerty.global/AZERTY_Global.appinstaller"` / `BUNDLE_URI = "https://download.azerty.global/AZERTY_Global.msixbundle"` | URL sans version |
| 4.26 | `msix/` | — | **aucun fichier `.appinstaller` dans le dépôt** (`ls msix/` : 5 bundles, `AppxManifest.xml`, `Assets/`, `Fiche Store.md`, `README.md`). L'étape 9 de `msix/README.md:174-187` n'a donc pas de sortie versionnée. |
| 4.27 | `scripts/tests/test_gen_appinstaller.py` | `def test_` ×29 | couverture la plus fournie des scripts Python après `check-doc-versions` |

---

## 5. CI

### `.github/workflows/ci.yml` (131 lignes)

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 5.1 | `:3-8` | `on:` / `push: branches: [main, release/**]` / `pull_request: branches: [main]` / `workflow_dispatch:` | la branche courante `release/1.2.0-notation-store` est bien couverte par `release/**` |
| 5.2 | `:10-14` | `permissions:` / `contents: read` / `security-events: write` / `id-token: write` / `attestations: write` | **`permissions:` est déclaré**, au niveau workflow |
| 5.3 | `:22-33` | job `provenance`, `runs-on: ubuntu-latest`, `timeout-minutes: 5`, `run: python scripts/check-layout-provenance.py` | **pas de `continue-on-error`** ; et `:17-21` explique qu'il n'est volontairement pas un `needs:` du build → **il ne bloque pas la construction**, il rougit seulement le run |
| 5.4 | `:35-36` | job `build`, `runs-on: windows-latest` | **aucun `timeout-minutes`** sur ce job |
| 5.5 | `:67-71` | `run: dotnet publish src/AZERTYGlobal.csproj -c Release -r win-x64` / `-r win-arm64` | **ARM64 est construit** en CI, en `Release` |
| 5.6 | `:73-80` | `dotnet test src/TypingEngine.Core.Tests/… -c Release` / `…TypingEngine.Windows.Tests/… -c Release` / `…AZERTYGlobal.Tests/… -c Release` | **les trois projets de test, un par un, en `Release`** |
| 5.7 | `:82-86` | `run: pwsh ./scripts/Pack-MSIX.ps1` puis `run: pwsh ./scripts/Verify-Release.ps1` | pack puis vérification |
| 5.8 | `:88-94` | `- name: BinSkim hardening check` … `continue-on-error: true   # NuGet BinSkim install est instable, ne pas bloquer` | **seul `continue-on-error` du fichier** |
| 5.9 | `:115-120` | `uses: actions/upload-artifact@v4` … `path: msix/AZERTYGlobal-*.msixbundle` / `retention-days: 90` | le glob ramasserait tous les bundles présents |
| 5.10 | `:122-125` | `uses: actions/attest-build-provenance@v2` avec `subject-path: 'msix/AZERTYGlobal-*.msixbundle'` | consomme `id-token: write` + `attestations: write` |
| 5.11 | `:26`, `:28`, `:38`, `:40`, `:63`, `:116`, `:123`, `:129` | `actions/checkout@v4`, `actions/setup-python@v5`, `actions/setup-dotnet@v4`, `actions/upload-artifact@v4`, `actions/attest-build-provenance@v2`, `github/codeql-action/upload-sarif@v3` | **toutes les actions sont épinglées par tag mobile (`@vN`), aucune par SHA** |
| 5.12 | `:62-65` | `uses: actions/setup-dotnet@v4` / `dotnet-version: '8.0.x'` | **SDK non épinglé** : `8.0.x` flotte. Le poste est en 8.0.425 (§6), la CI prendra le dernier 8.0.x disponible. |
| 5.13 | — | — | **aucun `secrets.` référencé dans `ci.yml`** ; l'OIDC sert à l'attestation (`:13`). |
| 5.14 | — | `grep -rn -i "maquette" .github/` → aucune occurrence | **il n'existe aucun job `maquettes`** dans ce dépôt. La question de son `continue-on-error` est sans objet ici. |

### `.github/workflows/store-analytics.yml` (101 lignes)

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 5.15 | `:4-12` | `schedule: - cron: "15 5 * * *"` + `workflow_dispatch:` avec entrée `start_date` (défaut `2015-01-01`) | quotidien |
| 5.16 | `:14-16` | `permissions:` / `contents: read` / `id-token: write` | déclaré |
| 5.17 | `:18-20` | `concurrency: group: microsoft-store-analytics` / `cancel-in-progress: false` | |
| 5.18 | `:27-32` | `STORE_ID: 9N4BTS43SSSZ`, `PARTNER_CENTER_TENANT_ID/CLIENT_ID/CLIENT_SECRET: ${{ secrets.… }}` | 3 secrets Partner Center |
| 5.19 | `:59-64` | `uses: azure/login@v2` avec `client-id/tenant-id/subscription-id: ${{ secrets.AZURE_… }}` | OIDC Azure + 3 secrets |
| 5.20 | `:35`, `:37`, `:52`, `:59` | `actions/checkout@v4`, `actions/setup-python@v5`, `actions/upload-artifact@v4`, `azure/login@v2` | épinglage par tag, pas par SHA |
| 5.21 | **`.github/workflows/store-analytics.yml:50-57`** | `- name: Conserver une copie de diagnostic`<br>`  if: always()`<br>`  uses: actions/upload-artifact@v4`<br>`  with:`<br>`    name: microsoft-store-analytics-${{ github.run_id }}`<br>`    path: store-analytics/out` | **l'étape supprimée sur `main` est toujours présente sur la branche release.** `git show --stat 1013d2b` : `Keep Store analytics archives in private storage only`, `Sat Sep 5 04:57:11 2026`, `.github/workflows/store-analytics.yml | 9 ---------`. `git diff origin/main..HEAD -- .github/workflows/store-analytics.yml` ne montre que ce bloc en `+`. |
| 5.22 | `:44-45` | `- name: Tester` / `run: python -m unittest discover -s store-analytics/tests -v` | les tests analytics tournent, les tests `scripts/tests` sont dans `ci.yml:60` |

---

## 6. Vulnérabilités NuGet

`dotnet --version` → `8.0.425`. `dotnet --list-sdks` → `8.0.425 [C:\Program Files\dotnet\sdk]` (un seul SDK).

**`global.json` : absent** — ni à la racine du composant, ni dans les deux dossiers parents. Le SDK n'est donc épinglé **nulle part**, ni pour le poste ni pour la CI (`ci.yml:65` `dotnet-version: '8.0.x'`).

Sorties verbatim, exécutées le 2026-09-20 :

```
=== AZERTYGlobal.csproj ===

The following sources were used:
   https://api.nuget.org/v3/index.json

The given project `AZERTYGlobal` has no vulnerable packages given the current sources.
EXIT=0
```

```
=== TypingEngine.Core.csproj ===

The following sources were used:
   https://api.nuget.org/v3/index.json

The given project `TypingEngine.Core` has no vulnerable packages given the current sources.
EXIT=0

=== TypingEngine.Windows.csproj ===

The following sources were used:
   https://api.nuget.org/v3/index.json

The given project `TypingEngine.Windows` has no vulnerable packages given the current sources.
EXIT=0
```

Comparaison au scan du 19/09 :

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 6.1 | `docs/audit-2026-09-15-v1.2.0/scan-vulnerabilites-2026-09-19.md:25-27` | `| `AZERTYGlobal` | oui | aucune vulnérabilité |` … `| `TypingEngine.Core` | oui | aucune vulnérabilité |` … `| `TypingEngine.Windows` | oui | aucune vulnérabilité |` | **résultat identique aujourd'hui** sur les trois projets livrés |
| 6.2 | `scan-vulnerabilites-2026-09-19.md:16-19` | `Index joignable cette fois : chaque exécution imprime `The following sources were used: https://api.nuget.org/v3/index.json`. Aucun NU1900.` | même ligne obtenue aujourd'hui → scan réel, pas un silence d'index |
| 6.3 | `scan-vulnerabilites-2026-09-19.md:28-30`, `:38-39` | `| `AZERTYGlobal.Tests` | non | 2 × High, transitives |` ; `System.Net.Http` 4.3.0 GHSA-7jgj-8wvc-jh57, `System.Text.RegularExpressions` 4.3.0 GHSA-cmhx-cq75-c4mj | **non rejoué ici** (hors des trois projets demandés). Les trois csproj de test référencent toujours `xunit` **2.6.0** (`src/AZERTYGlobal.Tests/AZERTYGlobal.Tests.csproj:17`, `src/TypingEngine.Core.Tests/…:11`, `src/TypingEngine.Windows.Tests/…:13`) : la chaîne décrite `:44-49` est inchangée. |
| 6.4 | `scan-vulnerabilites-2026-09-19.md:66-67` | `⚠️ À rejouer si la soumission glisse de plus de quelques semaines : un scan n'est valable qu'à sa date.` | scan du 19/09 ; re-mesure du 20/09 identique |
| 6.5 | `rebuild-sdk-8.0.425-2026-09-19.md:18-20` | `⚠️ **.NET 8 sort de support le 2026-11-10.** La v1.2.0 passe avant, mais la v2.0.0 ne peut pas rester sur ce socle.` | échéance à 51 jours de l'audit |
| 6.6 | `rebuild-sdk-8.0.425-2026-09-19.md:10-12` | `| SDK .NET | 8.0.423 | **8.0.425** |` / `| Runtime `Microsoft.NETCore.App` | 8.0.29 | **8.0.31** |` / `| ILCompiler (Native AOT) | 8.0.29 | **8.0.31** |` | correspond au SDK installé |

---

## 7. Native AOT

### Propriétés déclarées

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 7.1 | `src/AZERTYGlobal.csproj:23` | `<PublishAot>true</PublishAot>` | AOT actif, projet principal seulement |
| 7.2 | `src/AZERTYGlobal.csproj:27` | `<InvariantGlobalization>false</InvariantGlobalization>` | motivé `:24-26` par `NormalizationForm.FormD` |
| 7.3 | `src/AZERTYGlobal.csproj:28` | `<StackTraceSupport>false</StackTraceSupport>` | |
| 7.4 | `src/AZERTYGlobal.csproj:29` | `<UseSystemResourceKeys>true</UseSystemResourceKeys>` | |
| 7.5 | `src/AZERTYGlobal.csproj:30` | `<OptimizationPreference>Size</OptimizationPreference>` | |
| 7.6 | `src/AZERTYGlobal.csproj:35` | `<ControlFlowGuard>Guard</ControlFlowGuard>` | |
| 7.7 | `src/AZERTYGlobal.csproj:38` | `<Deterministic>true</Deterministic>` | |
| 7.8 | `src/AZERTYGlobal.csproj:43` | `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` | motivé `:40-42` par l'interop COM source-générée |
| 7.9 | `src/AZERTYGlobal.csproj:46-47` | `<EnableNETAnalyzers>true</EnableNETAnalyzers>` / `<AnalysisLevel>latest</AnalysisLevel>` | idem `src/TypingEngine.Windows/TypingEngine.Windows.csproj:7-8` |
| 7.10 | — | `grep -n "IsAotCompatible\|EnableTrimAnalyzer\|TrimMode\|PublishTrimmed" -r src --include=*.csproj` → **aucune occurrence** | **`IsAotCompatible`, `EnableTrimAnalyzer` et `TrimMode` ne sont déclarés dans aucun des six csproj.** `src/TypingEngine.Core/TypingEngine.Core.csproj` (8 lignes) et `src/TypingEngine.Windows/TypingEngine.Windows.csproj` (16 lignes) ne portent aucune propriété AOT : les analyseurs de trim/AOT ne s'exécutent pas sur ces deux bibliothèques en compilation de bibliothèque. |
| 7.11 | `src/AZERTYGlobal.csproj:19` | `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>` | motivé `:17-18` par un warning WACK |
| 7.12 | `src/AZERTYGlobal.csproj:51` | `<ApplicationManifest>app.manifest</ApplicationManifest>` | `src/app.manifest:14` `<dpiAwareness …>PerMonitorV2</dpiAwareness>` |

### Avertissements dans les logs de compilation

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 7.13 | `docs/audit-2026-09-15-v1.2.0/compilation-native-x64.log` | fichier **entier**, 4 lignes :<br>`  TypingEngine.Core -> …\TypingEngine.Core.dll`<br>`  AZERTYGlobal -> …\win-x64\publish\` | `grep -c -i "warning"` → **0**. `grep -E "IL2[0-9]{3}\|IL3[0-9]{3}"` → **0**. |
| 7.14 | `docs/audit-2026-09-15-v1.2.0/compilation-native-arm64.log` | fichier **entier**, 5 lignes ; seule ligne supplémentaire : `  Generating native code` | `grep -c -i "warning"` → **0**. |
| 7.15 | — | — | **Constat de forme** : ces deux « logs » ne contiennent que les lignes de sortie finales de `dotnet publish` (4 et 5 lignes). Ils ne portent ni la bannière du SDK, ni la ligne `Build succeeded`, ni le décompte `0 Warning(s) / 0 Error(s)`, ni aucun horodatage. **Zéro `IL2xxx`/`IL3xxx` y est donc un fait sur le contenu du fichier, pas une preuve que la compilation n'en a produit aucun** — le log ne montre pas la section où ils apparaîtraient. Le log x64 ne porte même pas la ligne `Generating native code` que porte l'arm64. |

---

## 8. Hygiène du dépôt public

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 8.1 | `git ls-files -s msix` | 34 entrées, toutes en `100644` | **aucun `.msixbundle` ni `.msix` n'est suivi par git.** Les 5 bundles présents sur disque (`AZERTYGlobal-1.2.0.0/1.2.0.1/1.2.0.2/1.3.0.0.msixbundle` + `AZERTYGlobal.msixbundle`, 6,80 Mo chacun, 34,0 Mo au total) sont hors index. |
| 8.2 | `.gitignore:8-9` | `*.msix` / `*.msixbundle` | la règle qui les exclut |
| 8.3 | `.gitignore:10` | `wack-report-*.xml` | les rapports WACK sont exclus ; l'unique rapport présent est `Archives/wack/2026-09/wack-report-v1.2.0-20260919.xml` (64 007 o) |
| 8.4 | `git ls-files Archives` | sortie vide | **`Archives/` n'est suivi par aucune entrée.** `git status --porcelain` le montre en `?? Archives/` — **non ignoré non plus** : il n'y a aucune ligne `Archives/` dans `.gitignore`. Il apparaît donc à chaque `git status` et un `git add -A` l'emporterait (il contient `local-signing/`, `msix-previous/`, `wack/`). |
| 8.5 | `.gitignore:36-39` | `# Suivi interne (audits, taches) — hors depot public` / `TO-DO.md` / `Publication Microsoft Store.md` / `Distribution Entreprises.md` | `git ls-files | grep -iE "Publication|Distribution|TO-DO"` → **sortie vide** : les deux documents présents sur disque ne sont effectivement pas publiés |
| 8.6 | `git status --porcelain` | `?? Archives/` et `?? docs/audit-2026-09-15-v1.2.0/test-results-baseline-sdk8.0.423/` | deux dossiers non suivis et non ignorés ; le second contient 12 `.trx` (référencés par `rebuild-sdk-8.0.425-2026-09-19.md:40`) |
| 8.7 | `git ls-files \| grep -iE "\.pfx$\|\.p12$\|\.key$\|\.pem$\|\.snk$"` | sortie vide | aucun matériel cryptographique suivi |
| 8.8 | `LICENCE:1-2` | `EUROPEAN UNION PUBLIC LICENCE v. 1.2` / `EUPL © the European Union 2007, 2016` | **EUPL 1.2**, texte intégral, 13 792 octets, 9 occurrences de « EUPL » |
| 8.9 | `README.md:128` | `[EUPL 1.2](https://eupl.eu/1.2/fr/) — European Union Public Licence` | cohérent |
| 8.10 | `src/AZERTYGlobal.csproj:14` | `<Copyright>© 2017-2026 Antoine Olivier — EUPL 1.2</Copyright>` | idem `src/Properties/AssemblyInfo.cs:6` |
| 8.11 | `src/Program.cs:2` | `// © 2017-2026 Antoine Olivier — Licence EUPL 1.2` | en-tête présent en tête de `Program.cs` |

### Grep « secrets » — lignes citées sans reformulation

`git grep -n -i -E "password|secret|token|pfx|api[_-]?key|BEGIN (RSA|PRIVATE)"`, hors `docs/…/test-results/` :

```
.github/workflows/ci.yml:13:  id-token: write     # pour attestation SLSA / sigstore
.github/workflows/store-analytics.yml:16:  id-token: write
.github/workflows/store-analytics.yml:30:      PARTNER_CENTER_TENANT_ID: ${{ secrets.PARTNER_CENTER_TENANT_ID }}
.github/workflows/store-analytics.yml:31:      PARTNER_CENTER_CLIENT_ID: ${{ secrets.PARTNER_CENTER_CLIENT_ID }}
.github/workflows/store-analytics.yml:32:      PARTNER_CENTER_CLIENT_SECRET: ${{ secrets.PARTNER_CENTER_CLIENT_SECRET }}
.github/workflows/store-analytics.yml:62:          client-id: ${{ secrets.AZURE_CLIENT_ID }}
.github/workflows/store-analytics.yml:63:          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
.github/workflows/store-analytics.yml:64:          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
Cahier des charges.md:152:- [x] **Champs de mot de passe** : remappage ordinaire conservé, mais couches, recherche et indicateur suspendus — détection `ES_PASSWORD` + UI Automation (navigateurs) sur thread dédié, jamais dans le hook clavier
Changelog.md:141:  l'indicateur sont suspendus. Détection native `ES_PASSWORD` complétée par UI Automation pour les
msix/Fiche Store.md:328: • GetGUIThreadInfo + GetWindowLongW(GWL_STYLE) — identifier le contrôle focalisé et son style ES_PASSWORD, en lecture seule.
src/AZERTYGlobal.Tests/QuickWinsTests.cs:69:                new[] { @"C:\Users\Alice\AZERTY Global.exe", "secret-activation-token" }
src/AZERTYGlobal.Tests/QuickWinsTests.cs:75:        Assert.DoesNotContain("secret-activation-token", details);
src/AboutWindow.cs:82:        Win32.GdiplusStartup(out _gdipToken, ref gdipInput, IntPtr.Zero);
src/CharacterSearch.cs:1436:                var token = parts[p];
src/GdiHelpers.cs:85:        var tokens = new List<(string Text, uint Color, IntPtr Font, bool IsSpace)>();
```

(liste tronquée à 60 lignes ; le reste est de la même nature — `token` de tokenisation de texte, `_gdipToken` GDI+, `dk:` tokens de touches mortes dans `scripts/validate-layout.py:108-117` et `src/defi-corpus.json`.)

**Aucune valeur de secret en clair n'apparaît.** Les références `${{ secrets.… }}` sont des noms de variables GitHub, `secret-activation-token` est une chaîne de test qui sert précisément à prouver qu'elle **n'est pas** journalisée (`QuickWinsTests.cs:75`), et toutes les occurrences de « password » décrivent la détection de champ de saisie. Références de signature (`git grep -i "\.pfx|\.cer\b|thumbprint|signtool"`) : uniquement des **procédures**, jamais une empreinte ni un chemin de clé privée — `msix/README.md:102-103`, `:171`, `docs/…/bundle-msix-2026-09-19.md:126`, `docs/…/recette-vm-resultats.md:113-120`.

---

## 9. Documentation vs réalité

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 9.1 | `README.md:17` | `**État du code :** version 1.2.0, manifeste MSIX local en 1.2.0.0, portes de version de `Verify-Release.ps1` franchies. Aucun package n'a encore été produit ni soumis.` | **trois assertions fausses aujourd'hui** : le csproj est en 1.3.0, le manifeste en `1.3.0.0`, et `msix/AZERTYGlobal-1.3.0.0.msixbundle` (6 799 981 o, 2026-09-20 17:27) est bien un paquet produit. |
| 9.2 | `README.md:38` | `- Windows 10 (version 1809+) ou Windows 11` | cohérent avec `MinVersion="10.0.17763.0"` (`msix/AppxManifest.xml:24`) et le TFM (`src/AZERTYGlobal.csproj:5`) |
| 9.3 | `README.md:28` | `- **Statistiques locales** — Compteurs agrégés conservés sur l'appareil, sans télémétrie réseau` | preuve d'appui dans le dépôt : `entreprise/politiques-exemple.reg` (ligne 27-28 décodée) « Ces compteurs ne quittent jamais le poste » et `entreprise/fr-FR/AZERTYGlobal.adml:22`. **Aucune vérification de code faite ici** (hors axe). |
| 9.4 | `README.md:9` | `Elle corrige les problèmes quotidiens du clavier français tout en conservant 99 % des habitudes existantes.` | **contredit la formulation imposée** — voir 9.10 |
| 9.5 | `README.md:5` et `:3` | `disponible sur le [Microsoft Store](https://apps.microsoft.com/detail/9N4BTS43SSSZ)` | l'ID Store correspond à `src/ProductIdentity.cs:43` `public const string StoreProductId = "9N4BTS43SSSZ";` |
| 9.6 | `msix/README.md:81` | `Vérifie que la version est alignée dans : `Program.cs`, `.csproj`, `AssemblyInfo.cs`, `AppxManifest.xml`, `Fiche Store.md` (FR + EN), `Publication Microsoft Store.md`, `TO-DO.md`, `Changelog.md`, `.agent/CONTEXT_APP_MICROSOFT_STORE.md`, `.agent/CONTEXT_AZERTY_GLOBAL.md`.` | liste exacte de `Verify-Release.ps1:135-156`. Mais `TO-DO.md` n'existe pas (§4) : la doc annonce un contrôle qui est un `WARNING` permanent. |
| 9.7 | `msix/README.md:204` | `*Dernière mise à jour : 2026-09-15 (v1.2.0 — contrôle explicite du `.appinstaller` du canal AMCF)*` | non rebasculé en 1.3.0 |
| 9.8 | `Distribution Entreprises.md:18`, `:34` | `- v1.2.0 préparée et vérifiée dans le dépôt, non encore soumise au Microsoft Store.` / `*Dernière mise à jour : 2026-08-21*` | document périmé d'une version ; signalé par `check-doc-versions.py` (§2) |
| 9.9 | `Publication Microsoft Store.md:5-7`, `:19` | `Version cible : 1.3.0` / `Package Store : 1.3.0.0` / `## État vérifié au 2026-08-21 (v1.2.0, artefacts non signés)` | l'en-tête est à jour, mais **aucune section « État vérifié » pour la 1.3.0** : la plus récente est datée du 2026-08-21 et porte sur la v1.2.0. |

### `msix/Fiche Store.md` — les cinq contrôles demandés

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 9.10 | `msix/Fiche Store.md:24` (FR) | `🔤 5 AMÉLIORATIONS, 99 % DES FRAPPES PRÉSERVÉES` | **conforme** au claim imposé |
| 9.11 | **`msix/Fiche Store.md:159` (EN)** | `🔤 5 IMPROVEMENTS, 99% OF YOUR HABITS PRESERVED` | **le pendant anglais dit toujours « HABITS ».** La consigne est écrite dans le dépôt : `msix/Assets/store-v120/brief-chatgpt-images-store.md:13` — `(Formulation imposée : toujours « 99 % des frappes », jamais « 99 % des habitudes ».)` Le commit `17382e0` (« 99 % de vos frappes preservees, pas de vos habitudes ») n'a touché que `src/Localization/`. |
| 9.12 | autres occurrences | `README.md:9` `en conservant 99 % des habitudes existantes` ; `msix/Assets/screenshot-5-changements.html:273` `<div class="sub-text">de vos habitudes préservées</div>` | deux autres survivances du claim interdit, dont une dans un gabarit de capture Store |
| 9.13 | `msix/Fiche Store.md` | `grep -i "quelques heures"` → **aucune occurrence** | **conforme** : la formule est absente |
| 9.14 | `msix/Fiche Store.md` | `grep -i "impossible"` → **aucune occurrence** | **conforme** : rien n'est présenté comme « impossible », ni sur les majuscules accentuées ni ailleurs |
| 9.15 | `msix/Fiche Store.md:79` / `:218` | `Version 1.3.0 :` / `Version 1.3.0:` | les deux formes exactes attendues par `Verify-Release.ps1:149-150` sont présentes |
| 9.16 | `msix/Fiche Store.md:362-365` | `1. `Screenshot1.png` — Icône dans la barre des tâches (tray)` … `4. `Screenshot4.png` — Onboarding / aperçu fonctionnalités` | **4 captures listées** |
| 9.17 | `msix/Fiche Store.md:367-370` | `> ⚠️ Le Store demande des captures d'écran d'au moins 1366×768 px.` / `> Dimensions vérifiées le 2026-06-28 : Screenshot1 (1600×900), Screenshot2 (1902×1194), Screenshot3 (1707×960), Screenshot4 (1500×1057).` | **dimensions re-mesurées aujourd'hui, identiques** : 1600×900, 1902×1194, 1707×960, 1500×1057. Toutes ≥ 1366×768. La réserve « ratios non standards susceptibles de générer des bandes noires » (`:369-370`) reste ouverte. |
| 9.18 | `msix/Assets/store-v120/` | `01-geste.png`, `03-changements.png`, `06-confidentialite.png` — **1920×1080** chacune | trois visuels 16:9 produits pour la v1.2.0 (commit `8d9652f`) **ne figurent dans aucune liste de `Fiche Store.md`** |
| 9.19 | `msix/Fiche Store.md:289-292` | `- **Système** : Windows 10 version 1809 (build 17763) ou ultérieur` / `- **Architecture** : x64 et ARM64 (MSIX bundle dual)` / `- **Espace disque** : ~5 Mo par architecture` / `- **Connexion internet** : Non requise` | build minimal cohérent ; **« ~5 Mo par architecture »** contre 3 456 295 o (x64) et 3 341 695 o (arm64) mesurés dans le bundle → 3,3 et 3,2 Mio compressés, ~8,2 et ~8,5 Mo décompressés |

### Procédure §7 du Cahier des charges — preuve ou « aucune preuve »

`Cahier des charges.md:254-293`, section « 7. Critères de validation / Tests minimaux avant publication », datée `:293` `*Dernière mise à jour : 2026-08-24*`.

| # | fichier:ligne | citation | preuve trouvée dans le dépôt |
|---|---|---|---|
| 9.20 | `Cahier des charges.md:259-268` | 10 cases **Remapping**, toutes `- [x]` | `docs/audit-2026-09-15-v1.2.0/test-results/*.trx` (12 fichiers) + `recette-vm-resultats.md`. **Aucune n'est datée de la 1.3.0** ; la campagne 1.3.0 vit dans `recette-vm-v1.3.0-correctifs.md` (commit `404d68a`) et `71cc69d` clôt « VM-06 a VM-18, ecart 6 non reproduit, **ecarts 7 et 8 ouverts** » |
| 9.21 | `Cahier des charges.md:271-277` | 7 cases **Interface**, toutes `- [x]` | idem ; `Changelog.md:12` dit pourtant « ⚠️ Aucun test ne verrouille la structure du menu — `ShowContextMenu` appelle Win32 directement. La vérification est visuelle, en VM. » |
| 9.22 | `Cahier des charges.md:280-281` | 2 cases **Performance**, `- [x]` | **aucune preuve** : aucun fichier de mesure de CPU ou de latence dans `docs/` |
| 9.23 | `Cahier des charges.md:284-289` | 5 cases **Couches maintenables**, **toutes `- [ ]`**, « à dérouler dans Word/Excel, Chrome, Edge, Firefox et VS Code » | **non cochées à ce jour.** Inclut `:287` `- [ ] Champ de mot de passe (login réel dans les 3 navigateurs …)`. |
| 9.24 | WACK | `docs/…/bundle-msix-2026-09-19.md:74-83` `Archives/wack/2026-09/wack-report-v1.2.0-20260919.xml` (64 007 octets) … `APP_VERSION="1.2.0.0"` | **le seul rapport WACK du dépôt porte sur 1.2.0.0.** `ls Archives/wack/2026-09/` → un seul fichier. **Aucune preuve WACK pour le candidat 1.3.0.0.** |
| 9.25 | `Verify-Release.ps1` | `docs/…/bundle-msix-2026-09-19.md:26-34` `Release vérifiée: version 1.2.0 / package 1.2.0.0` | **la seule trace d'exécution consignée porte sur 1.2.0.** Aucune sortie `Verify-Release` pour 1.3.0.0 dans `docs/`. |
| 9.26 | appinstaller sur machine propre | `msix/README.md:176` `Le fichier est **généré depuis le bundle signé**, jamais écrit à la main` ; `scripts/gen-appinstaller.py:27` `seul le critère d'acceptation du lot E — installation sur machine propre, puis constat d'une montée de version — la prouvera` | **aucune preuve** : pas de `.appinstaller` dans `msix/`, pas de bundle signé AMCF sur disque, aucun document d'installation sur machine propre. `gen-appinstaller.py:154-160` refuserait le bundle actuel (§4.24). |
| 9.27 | `msix/README.md:192` | `⚠️ Le Worker `components/website/workers/download-msix/` applique aujourd'hui `Cache-Control: max-age=31536000, immutable` et `Content-Disposition: attachment` à **tous** ses fichiers : en l'état, le `.appinstaller` serait téléchargé au lieu d'être ouvert par App Installer` | piège consigné, **non résolu dans ce dépôt** (le Worker vit ailleurs) |

---

## 10. Entreprise

### `entreprise/politiques-exemple.reg` — octets mesurés

```
00000000: fffe 5700 6900 6e00 6400 6f00 7700 7300  ..W.i.n.d.o.w.s.
00000010: 2000 5200 6500 6700 6900 7300 7400 7200   .R.e.g.i.s.t.r.
00000020: 7900 2000 4500 6400 6900 7400 6f00 7200  y. .E.d.i.t.o.r.
00000030: 2000 5600 6500 7200 7300 6900 6f00 6e00   .V.e.r.s.i.o.n.
```

```
taille 4538
BOM utf16le True
BOM utf8 False
CRLF utf16le count 42
LF seuls utf16le 0
CRLF ascii count 0
decode utf-16-le OK
```

| # | fichier:ligne | citation | constat |
|---|---|---|---|
| 10.1 | `entreprise/politiques-exemple.reg` octets 0-1 | `ff fe` | **BOM UTF-16 LE présent**, conforme à l'attendu |
| 10.2 | idem, comptage | 42 × `0D 00 0A 00`, **0** `0A 00` isolé | **CRLF partout, aucun LF seul.** Conforme. |
| 10.3 | idem, ligne 1 décodée | `Windows Registry Editor Version 5.00` | en-tête attendu par `reg import` |
| 10.4 | idem, taille | 4 538 octets pour 42 lignes | 2 269 unités UTF-16 |

### Noms de clés et de valeurs, littéralement

Racine, citée dans les trois fichiers :

| # | fichier:ligne | citation |
|---|---|---|
| 10.5 | `entreprise/AZERTYGlobal.admx:9` | `Racine : HKEY_LOCAL_MACHINE\SOFTWARE\Policies\AZERTYGlobal, vue 64 bits. Le produit lit avec RRF_SUBKEY_WOW6464KEY` |
| 10.6 | `entreprise/politiques-exemple.reg` ligne 19 | `[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\AZERTYGlobal]` |
| 10.7 | `entreprise/AZERTYGlobal.admx:38`, `:48`, `:58`, `:71`, `:82` | `key="SOFTWARE\Policies\AZERTYGlobal"` (5 fois, identique) |

Les **cinq valeurs**, telles qu'écrites (à confronter au code par un autre agent — je ne les ai pas lues dans `src/PolicyManager.cs`) :

| # | `valueName` ADMX | `policy name` ADMX | ligne `.reg` | littéral `.reg` | enabled / disabled |
|---|---|---|---|---|---|
| 10.8 | `NotificationsEnabled` (`admx:39`) | `NotificationsEnabled` (`admx:35`) | 24 | `"NotificationsEnabled"=dword:00000000` | `1` / `0` (`admx:42-43`) |
| 10.9 | `UsageStatsEnabled` (`admx:49`) | `UsageStatsEnabled` (`admx:45`) | 29 | `"UsageStatsEnabled"=dword:00000000` | `1` / `0` (`admx:52-53`) |
| 10.10 | `ExternalLinksEnabled` (`admx:59`) | `ExternalLinksEnabled` (`admx:55`) | 33 | `"ExternalLinksEnabled"=dword:00000000` | `1` / `0` (`admx:62-63`) |
| 10.11 | **`ShowOnboarding`** (`admx:72`) | **`RemoveOnboarding`** (`admx:68`) | 38 | `"ShowOnboarding"=dword:00000000` | **`0` / `1`** (`admx:75-76`) — inversion assumée, `admx:12-16` et `admx:66` |
| 10.12 | `Language` (`admx:86`, `enum id="Language" valueName="Language"`) | `Language` (`admx:78`) | 42 | `"Language"="fr"` | valeurs `fr` / `en` (`admx:88`, `:91`), `required="true"` |

Constats :

- Le nom de stratégie `RemoveOnboarding` et le nom de valeur `ShowOnboarding` **diffèrent volontairement** ; la double documentation existe (`admx:12-16`, `admx:65-67`, `.reg` lignes 11-13, `fr-FR/AZERTYGlobal.adml:28`).
- `entreprise/AZERTYGlobal.admx:21` `<target prefix="azertyglobal" namespace="AMCF.Policies.AZERTYGlobal" />` — espace de noms de stratégie propre à l'AMCF.
- Les deux `.adml` (`fr-FR` 3 526 o, `en-US` 3 459 o) portent tous deux `SUPPORTED_AZERTY_GLOBAL_1_2_0` (§2.16-2.17) : le libellé lu par une DSI annonce « AZERTY Global 1.2.0 ou version ultérieure » pour un candidat 1.3.0.
- `scripts/tests/test_admx_agreement.py` porte **21 tests** et son en-tête `:1-12` dit : « Rien dans Windows ne signale un `valueName` qui ne correspond plus à rien : la stratégie s'applique, écrit sa valeur, et l'application ne la lit jamais. Ces tests sont le seul endroit où ce silence devient une erreur. » Ces tests tournent en CI (`ci.yml:59-60` `python -m unittest discover -s scripts/tests -v`).

---

## Non vérifié

- **Aucun `dotnet build`, `publish` ou `test` n'a été lancé** (interdit par le cadrage) : les 609 tests annoncés par `rebuild-sdk-8.0.425-2026-09-19.md:35-38` ne sont pas re-mesurés ici, et je ne peux pas dire si la suite est verte au HEAD actuel — trois commits de code ont suivi le dernier run consigné.
- **`dotnet list package --vulnerable` sur les trois projets de test** n'a pas été exécuté (hors de la liste demandée) : les « 2 × High transitives » de `scan-vulnerabilites-2026-09-19.md:28-30` sont reprises telles quelles, seule la persistance de `xunit 2.6.0` dans les trois csproj est re-mesurée.
- **Le code applicatif** : je n'ai ouvert `src/ToastActivation.cs` que pour le CLSID, et `src/Program.cs` / `src/ProductIdentity.cs` / `src/Properties/AssemblyInfo.cs` que pour les versions. La correspondance entre les 5 noms de valeurs ADMX et `src/PolicyManager.cs` est **explicitement laissée à un autre agent** (question 10).
- **Le contenu réel des logs de compilation** : je constate qu'ils ne portent aucun `IL2xxx`/`IL3xxx`, mais leur forme (4 et 5 lignes, sans `Build succeeded` ni décompte de warnings) ne permet pas d'en conclure que la compilation n'en a produit aucun. Une reconstruction avec `-v normal` ou `/warnaserror` trancherait — hors de mon périmètre.
- **Le WACK sur 1.3.0.0** : aucun rapport dans le dépôt. Je n'ai pas exécuté `appcert.exe`.
- **La signature** : aucun bundle signé n'existe sur disque ; je n'ai pas pu vérifier de chaîne de certification.
- **`msix/AZERTYGlobal-1.2.0.1.msixbundle` et `-1.2.0.2.msixbundle`** : présents sur disque (19/09 13:53 et 14:00), non documentés dans `docs/audit-2026-09-15-v1.2.0/` (qui ne cite que `1.2.0.0`) et non expliqués par le Changelog. Leur provenance n'est pas établie.
- **`.agent/CONTEXT_*.md`** : lus uniquement pour les deux lignes que `Verify-Release.ps1` cherche. Ils sont hors du dépôt public.
- **`store-analytics/`** : hors périmètre ; seul `store-analytics.yml` a été audité.
- **Les 12 `.trx` de `docs/audit-2026-09-15-v1.2.0/test-results/`** n'ont pas été parsés (l'axe tests revient à un autre agent).
