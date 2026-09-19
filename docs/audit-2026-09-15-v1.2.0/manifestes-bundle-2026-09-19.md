# Manifestes internes du bundle 1.2.0.0 — 2026-09-19

Verrou 4 de `corrections.md` § 4. Ce que `Verify-Release.ps1` **n'ouvre pas** :
le script compare des empreintes de binaires, il ne lit aucun manifeste
embarqué. Tout ce qui suit est lu dans le paquet lui-même, pas dans les
sources.

Paquet examiné : `msix/AZERTYGlobal-1.2.0.0.msixbundle` (6 798 518 octets,
2026-09-19 09:46:20).

## Contenu réel du bundle

| Entrée | Taille |
|---|---|
| `AZERTYGlobal-1.2.0.0-x64.msix` | 3 456 126 |
| `AZERTYGlobal-1.2.0.0-arm64.msix` | 3 340 402 |
| `AppxMetadata/AppxBundleManifest.xml` | 1 260 |
| `AppxBlockMap.xml` | 422 |
| `[Content_Types].xml` | 364 |

⛔ **Aucune entrée `AppxSignature.p7x`** — confirmation matérielle que le bundle
n'est pas signé. C'est l'état attendu : Partner Center signe le paquet soumis.

## `AppxBundleManifest.xml`

```
Identity Name="AZERTYGlobal.AZERTYGlobal"
         Publisher="CN=7FD049E3-1C58-42E0-A07F-A9712DE19E38"
         Version="1.2.0.0"
```

Deux `Package Type="application"`, `Version="1.2.0.0"`, architectures `x64` et
`arm64`. Chacun déclare `Resource Language` **fr-FR** et **en-US**, et
`TargetDeviceFamily Windows.Desktop MinVersion="10.0.17763.0"
MaxVersionTested="10.0.26100.0"`.

`MinVersion` colle au TFM du csproj (`net8.0-windows10.0.17763.0`) : aucune
divergence entre ce que le paquet promet et ce que le binaire cible.

## Les deux `AppxManifest.xml` internes

3 817 octets (x64) et 3 819 (arm64) : **deux octets d'écart, qui sont
exactement `ProcessorArchitecture="x64"` contre `"arm64"`.** Tout le reste est
identique — identité, version, capacités, extensions, assets.

Points vérifiés dans le paquet :

| Contrôle | Valeur lue |
|---|---|
| `Identity Version` | `1.2.0.0` dans les deux |
| `Capabilities` | `rescap:Capability runFullTrust` seule |
| `EntryPoint` | `Windows.FullTrustApplication` |
| `StartupTask TaskId` | `AZERTYGlobalStartup`, `Enabled="false"` |
| Assets référencés | les 6 existent dans le `.msix` |

### Le bloquant B2 est fermé dans le paquet, pas seulement dans les sources

Le CLSID de l'activateur de toast déclaré **deux fois** dans le manifeste
embarqué (`com:Class Id` et `desktop:ToastNotificationActivation
ToastActivatorCLSID`) vaut `126A58B4-3200-43A6-9018-612C108F4A94`, soit la
valeur littérale de `ToastActivation.ActivatorClsidString`
(`src/ToastActivation.cs:54`). Le clic sur une notification atteindra le
processus vivant au lieu de relancer l'exécutable.

### Le `TaskId` est préservé

`AZERTYGlobalStartup`, identique à celui de la 1.1.0 servie par le Store. Un
`TaskId` différent aurait rendu la tâche orpheline à la mise à jour et coupé le
lancement automatique **sans rien dire** — c'est la raison pour laquelle le
Changelog le garde hors de `ProductIdentity`.

## ⚠️ Le `.msix` arm64 contient bien du code ARM64

Un paquet étiqueté `arm64` dont l'exécutable serait un x64 recopié passerait
tous les contrôles précédents, le WACK, et ne se verrait qu'à l'exécution sur
machine ARM. En-têtes PE lus dans les binaires empaquetés :

| | `Machine` | Sous-système | Taille |
|---|---|---|---|
| x64 | `0x8664` AMD64 | `WINDOWS_GUI` | 8 172 544 |
| arm64 | `0xAA64` ARM64 | `WINDOWS_GUI` | 8 528 896 |

Deux architectures réellement distinctes, aucune en console.

## ⚠️ Le chemin de mise à jour depuis la 1.1.0 tient

Windows n'identifie pas une application par son nom mais par son *package
family name*, dont la seconde moitié est un hachage de la chaîne `Publisher`.
Un `Publisher` retouché, même d'un caractère, produirait une **installation
parallèle** au lieu d'une mise à jour, et les utilisateurs de la 1.1.0
garderaient l'ancienne version en place avec sa configuration.

Hachage recalculé depuis le `Publisher` du bundle (SHA-256 de la chaîne en
UTF-16LE, 8 premiers octets, base32 `0123456789abcdefghjkmnpqrstvwxyz`) :

```
CN=7FD049E3-1C58-42E0-A07F-A9712DE19E38  ->  w9kghr08zmhbg
```

C'est le suffixe du dossier du paquet **1.1.0 réellement installé**, relevé
dans `docs/audit-v1.2.0/raw-migration.md` :
`AZERTYGlobal.AZERTYGlobal_1.1.0.0_x64__w9kghr08zmhbg`. Le PFN est donc
inchangé : la 1.2.0 se pose en mise à jour de la 1.1.0.

⚠️ Preuve indirecte, et c'est le mieux disponible ici : aucun bundle 1.1.0
n'est archivé dans le dépôt et le dépôt ne porte pas de tag v1.1.0 (les tags
s'arrêtent à `v1.0.0`). La vérification directe reste au verrou 5, qui installe
la vraie 1.1.0 en VM puis met à jour par-dessus.

## Reste ouvert

- `Verify-Release.ps1` n'ouvre toujours aucun manifeste : les contrôles
  ci-dessus sont manuels et ne se rejoueront pas tout seuls à la prochaine
  release. Les porter dans le script serait le geste durable.
- Le paquet n'embarque pas de variantes `targetsize-*` ni `altform-unplated`
  de `Square44x44Logo` : l'icône de barre des tâches restera la version
  « plaquée » sur fond `#1a1a2e`. Non bloquant, et sans changement par rapport
  à ce que le Store sert déjà.

## Ce qui reste

5. Paquet 1.1.0 publié + VM Hyper-V, puis la recette de `recette-vm.md` —
   installation propre **et** mise à jour depuis la 1.1.0, plus l'exécution
   réelle sur ARM64.
