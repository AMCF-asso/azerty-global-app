# Packaging MSIX - AZERTY Global

> Objectif : packager l'application AZERTY Global pour le Microsoft Store et la distribution hors Store signée AMCF.

## Prérequis

- Windows 10/11 SDK (fournit `MakeAppx.exe` et `SignTool.exe`)
- Compte développeur Microsoft Partner Center
- Microsoft Artifact Signing opérationnel au nom de l'AMCF pour le canal hors Store signé
- Exe AOT publié (`AZERTY Global.exe`)

## Structure du package

```text
msix/
|-- AppxManifest.xml          <- Manifeste du package
|-- Assets/                   <- Logos, icones et captures Store
|   |-- StoreLogo.png         <- 50x50 px
|   |-- Square44x44Logo.png   <- 44x44 px
|   |-- Square150x150Logo.png <- 150x150 px
|   `-- Wide310x150Logo.png   <- 310x150 px
|-- AZERTY Global.exe         <- Copie depuis ../bin/
`-- README.md                 <- Ce fichier
```

## Étapes

### 1. Réserver le nom de l'app

1. Aller sur https://developer.microsoft.com/en-us/microsoft-store/register
2. Créer ou utiliser un compte Microsoft
3. Réserver le nom "AZERTY Global" dans Partner Center
4. Reporter le `Package Identity Name` et le `Publisher` dans `AppxManifest.xml`

### 2. Vérifier les assets visuels

Les fichiers PNG du dossier `Assets/` doivent être cohérents avec la version soumise :

- `StoreLogo.png`
- `Square44x44Logo.png`
- `Square150x150Logo.png`
- `Wide310x150Logo.png`
- captures Store (`Screenshot*.png`)

### 3. Assembler le package

Prérequis : avoir publié les 2 architectures via :

```powershell
$env:PATH += ";C:\Program Files (x86)\Microsoft Visual Studio\Installer"
dotnet publish -c Release -r win-x64
dotnet publish -c Release -r win-arm64
```

Puis lancer le pack :

```powershell
powershell -ExecutionPolicy Bypass -File "..\scripts\Pack-MSIX.ps1"
```

`Pack-MSIX.ps1` :

- copie chaque exe publié (x64 + arm64) dans un dossier de staging temporaire
- ajuste `ProcessorArchitecture` dans `AppxManifest.xml` selon l'arch
- produit un `.msix` par architecture, puis les groupe dans un `.msixbundle`
- écrit le bundle versionné `msix\AZERTYGlobal-<version>.msixbundle`
- rafraîchit aussi `msix\AZERTYGlobal.msixbundle` (bundle stable, archive l'ancien dans `Archives\msix-previous\by-version\<version>\`)

`MakeAppx.exe` se trouve en général dans :

```text
C:\Program Files (x86)\Windows Kits\10\bin\<version>\x64\MakeAppx.exe
```

### 4. Vérifier la cohérence release

```powershell
powershell -ExecutionPolicy Bypass -File "..\scripts\Verify-Release.ps1"
```

Vérifie que la version est alignée dans : `Program.cs`, `.csproj`, `AssemblyInfo.cs`, `AppxManifest.xml`, `Fiche Store.md` (FR + EN), `Publication Microsoft Store.md`, `TO-DO.md`, `Changelog.md`, `.agent/CONTEXT_APP_MICROSOFT_STORE.md`, `.agent/CONTEXT_AZERTY_GLOBAL.md`. Vérifie aussi que le SHA-256 de l'exe publish correspond au SHA-256 dans le bundle pour chaque architecture.

### 5. Tester localement avant soumission

```powershell
Add-AppxPackage -Path ".\msix\AZERTYGlobal-<version>.msixbundle"
```

Vérifier :

- l'app se lance depuis le menu Démarrer
- l'icône tray apparaît
- les hooks clavier fonctionnent
- la recherche de caractères fonctionne
- la couche compatibilité jeux : auto-disable sur process anti-cheat (cf. `GameRegistry.AntiCheatTerms`), combo native sur process avec framework gaming (cf. `GameRegistry.GameFrameworkDlls`), Alt+code en RDP/VPN
- l'app fonctionne dans les applications non admin
- pas de blocage Smart App Control

### 6. Validation WACK

```powershell
appcert.exe test -appxpackagepath "msix\AZERTYGlobal-<version>.msixbundle" -reportoutputpath "wack-report-v<version>.xml"
```

Corriger les problèmes signalés avant soumission. Les nouvelles APIs v0.9.7 (`PSAPI`, `SetWinEventHook`) sont declaratees dans `Fiche Store.md` (notes de certification).

### 7. Soumettre au Store

1. Ouvrir la soumission dans Partner Center
2. Uploader le `.msixbundle` versionné depuis `msix\`
3. Compléter la fiche Store, les captures, la privacy policy et la classification
4. Ajouter une note de certification expliquant l'usage de `WH_KEYBOARD_LL` + APIs v0.9.7 (`PSAPI`, `SetWinEventHook`) — voir section "Notes pour l'équipe de certification Microsoft" de `Fiche Store.md`
5. Soumettre

### 8. Signer le canal hors Store AMCF

Le bundle Microsoft Store et le bundle hors Store signé AMCF sont deux artefacts de release distincts.

- Store : uploader uniquement le bundle versionné attendu par Partner Center.
- Hors Store : signer une copie du bundle avec Microsoft Artifact Signing au nom de l'AMCF, puis publier cette copie pour les environnements où le Store est bloqué.
- Ne jamais uploader dans Partner Center un bundle signé localement pour test ou un artefact hors Store sans vérification explicite.

Commande type à adapter avec le vrai `metadata.json` local :

```powershell
signtool.exe sign /v /debug /fd SHA256 /tr "http://timestamp.acs.microsoft.com" /td SHA256 /dlib "<ArtifactSigningClient>\bin\x64\Azure.CodeSigning.Dlib.dll" /dmdf "metadata.json" "msix\AZERTYGlobal-<version>.msixbundle"
```

## Notes importantes

- Le dernier segment de version doit rester à `0` pour le Store (ex: `0.9.5.0`)
- Microsoft re-signe automatiquement le package soumis
- Le canal hors Store est signé au nom de l'AMCF via Microsoft Artifact Signing
- L'app utilise `runFullTrust` via Desktop Bridge
- L'onboarding sert de dialogue de consentement explicite

---

*Dernière mise à jour : 2026-06-29 (v1.0.0 — publication Microsoft Store ; MSIX AMCF à produire)*
