# Reconstruction sur SDK 8.0.425 — 2026-09-19

Lève le point 1 de `corrections.md` § 4 (« Actualiser les outils de compilation
avant le paquet à livrer »). Branche : `release/1.2.0-notation-store`.

## Bascule d'outillage

| | Avant (audit du 15/09) | Après |
|---|---|---|
| SDK .NET | 8.0.423 | **8.0.425** |
| Runtime `Microsoft.NETCore.App` | 8.0.29 | **8.0.31** |
| ILCompiler (Native AOT) | 8.0.29 | **8.0.31** |

8.0.31 est sorti le 2026-09-08 et corrige 5 CVE (CVE-2026-69439, -71328,
-69522, -69304, -58649). Source : métadonnées officielles
`release-metadata/8.0/releases.json`.

⚠️ **.NET 8 sort de support le 2026-11-10.** La v1.2.0 passe avant, mais la
v2.0.0 ne peut pas rester sur ce socle.

## Résultat — tout vert

`python docs/audit-2026-09-15-v1.2.0/verifier-corrections.py`

| Étape | Sortie |
|---|---|
| `layout-corrections` | 0 — schéma, compteurs et références conformes |
| `identite-corrections` | 0 — 0 littéral hors `ProductIdentity` |
| `documents-corrections` | 1 **attendu** — 0 erreur, 32 attentes de bascule de version du kit |
| `scripts-complets` | 0 — **116 tests Python OK** |
| `environnement-corrections` | 0 |
| `compilation-native-x64` | 0 |
| `compilation-native-arm64` | 0 |

Tests .NET, suites relancées en `Release` : **18** (`TypingEngine.Core`) +
**152** (`TypingEngine.Windows`) + **323** (`AZERTYGlobal`) = **493**.
Avec les 116 Python : **609** — le chiffre du 15/09 est confirmé, et il
recouvre bien les deux familles de tests, pas seulement .NET.

Les TRX du 15/09 sont conservés dans `test-results-baseline-sdk8.0.423/` ;
les originaux restent de toute façon dans l'historique git.

## Deux pannes rencontrées, et leur cause réelle

### 1. `NU1102` sur les paquets runtime 8.0.31 — cache NuGet périmé

Après la bascule de SDK, la restauration de `AZERTYGlobal.csproj` échouait :

```
error NU1102: Unable to find package runtime.win-x64.Microsoft.DotNet.ILCompiler
with version (= 8.0.31) — Found 120 version(s) in nuget.org
[ Nearest version: 9.0.0-preview.1.24081.3 ]
```

⛔ **Le message ment sur la cause.** Les trois paquets existent bien en 8.0.31
sur nuget.org (vérifié sur `v3-flatcontainer/<paquet>/index.json`). Le cache
HTTP local gardait l'index tronqué rapporté pendant l'épisode hors ligne du
15/09 (celui qui avait produit NU1900).

Correctif : `dotnet nuget locals http-cache --clear`, puis restauration — OK.

⚠️ Conséquence méthodologique : le scan de vulnérabilités du même jour
(`scan-vulnerabilites-2026-09-19.md`) avait tourné **avant** ce nettoyage. Il a
été **relancé après**, résultat identique — les trois projets livrés restent
propres.

### 2. `MSB3073` sur l'édition de liens native — `vswhere.exe` hors PATH

```
'vswhere.exe' is not recognized as an internal or external command
Microsoft.NETCore.Native.targets(370,5): error MSB3073 ... exited with code 3
```

`Microsoft.NETCore.Native.targets` localise les outils MSVC par `vswhere.exe`,
qui vit dans `C:\Program Files (x86)\Microsoft Visual Studio\Installer\` et
n'est pas sur le PATH par défaut de ce poste.

Correctif — préfixer le PATH avant tout `publish` natif ou avant le
vérificateur :

```powershell
Set-Location "D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store"
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;$env:PATH"
python docs\audit-2026-09-15-v1.2.0\verifier-corrections.py
```

⛔ Sans cette ligne, `compilation-native-x64` et `-arm64` échouent toujours,
quel que soit le SDK. À porter dans la recette de release.

## Ce qui reste devant

Les verrous 1 et 2 de la liste du 15/09 sont levés. Restent, inchangés :

3. Empaqueter le bundle MSIX, `Verify-Release.ps1`, **WACK PASS**.
4. Vérifier les manifestes internes du bundle et des MSIX.
5. Récupérer le paquet 1.1.0 publié + VM Hyper-V active, puis la recette VM.

⛔ Rappel : compiler pour ARM64 ne prouve pas l'exécution ARM64.
