# Bundle MSIX 1.2.0.0 et vérification de release — 2026-09-19

Verrou 3 de `corrections.md` § 4. Branche `release/1.2.0-notation-store`,
sur le socle reconstruit du même jour (`rebuild-sdk-8.0.425-2026-09-19.md`).

## Bundle produit

`scripts/Pack-MSIX.ps1` — deux architectures natives, pas de cross-only.

| | |
|---|---|
| Bundle versionné | `msix/AZERTYGlobal-1.2.0.0.msixbundle` |
| Taille | 6 798 518 octets |
| Horodatage | 2026-09-19 09:46:20 |
| Paquets internes | `AZERTYGlobal-1.2.0.0-x64.msix`, `AZERTYGlobal-1.2.0.0-arm64.msix` |
| SHA-256 `AZERTY Global.exe` x64 | `4D8B82F579692F86D4F4FA682642F0957BB2B339B5AAAB56CE7531AFDE375E6D` |
| SHA-256 `AZERTY Global.exe` arm64 | `4D64569A07375568E6855F534871A80D006CB073260AB25144EB4E8F203290AC` |

Le bundle du 2026-08-27 (6 738 046 octets, non signé, jamais passé par
`Verify-Release.ps1` ni le WACK) est archivé sous
`Archives/msix-previous/by-version/1.2.0.0/` avant écrasement.

⛔ **Le bundle n'est pas signé.** Le paquet Partner Center est signé par le
Store ; le MSIX hors Store AMCF est un artefact distinct, à produire séparément.

## `Verify-Release.ps1` — vert

```
FileVersion x64   : 1.2.0.0 (publish = 1.2.0.0)
FileVersion arm64 : 1.2.0.0 (publish = 1.2.0.0)
SHA256 x64        : … (publish = bundle)
SHA256 arm64      : … (publish = bundle)
Release vérifiée: version 1.2.0 / package 1.2.0.0
```

Les empreintes des exécutables publiés et de ceux réellement empaquetés dans le
bundle coïncident : le paquet contient bien les binaires construits ce jour.

## ⚠️ Deux contrôles dormaient depuis la migration wiki

`Verify-Release.ps1` résolvait les contextes partagés en
`..\..\..\.agent\`, c'est-à-dire `Keyboard Layouts/projects/.agent/` — un
dossier qui n'existe pas. La migration en bundle wiki a descendu le composant
d'un niveau et personne n'a suivi le chemin.

Conséquence : `Assert-MatchIfExists` ne trouvait pas les fichiers, émettait un
`WARNING` et **passait**. Deux contrôles de cohérence de version rendaient donc
« vert » depuis des semaines sans rien vérifier.

⛔ C'est le motif « témoin contre soi » : un contrôle qui ne trouve pas sa cible
et continue est indiscernable d'un contrôle qui réussit.

Corrections appliquées :

1. `scripts/Verify-Release.ps1` — profondeur portée à `..\..\..\..\.agent\`.
2. `.agent/CONTEXT_APP_MICROSOFT_STORE.md` — ligne `> **Version actuelle** : 1.2.0`
   rétablie (le contrôle l'attend littéralement ; elle avait été reformulée en
   « Versions en chantier »).
3. `.agent/CONTEXT_AZERTY_GLOBAL.md` — ligne v1.2.0 ajoutée **au-dessus** de la
   ligne v1.1.0, sans toucher à celle-ci : 1.1.0 reste la version *publiée*, et
   la réécrire aurait été un faux.

Une fois les trois corrections faites, les deux contrôles s'exécutent
réellement et passent.

### Reste ouvert

`TO-DO.md` est attendu à la racine du composant et n'existe nulle part dans le
dépôt : le contrôle reste en `WARNING` permanent. Soit le fichier revient, soit
le contrôle saute — le laisser en l'état entretient un troisième témoin muet.

## WACK — PASS le 2026-09-19

`Archives/wack/2026-09/wack-report-v1.2.0-20260919.xml` (64 007 octets),
exécuté par Antoine en invite élevée, ~1 min.

```
OVERALL_RESULT="PASS"   VERSION=10.0.26100.7705
APP_TYPE="Centennial"   APP_NAME="AZERTYGlobal.AZERTYGlobal"
APP_VERSION="1.2.0.0"   OS=Windows 11 Pro for Workstations 10.0.26200.0
```

**24 tests : 23 PASS, 1 FAIL.**

⚠️ Le `PASS` global coexiste avec un test en échec — `Blocked executables`,
marqué `OPTIONAL="TRUE"`, qui ne bloque donc pas le verdict. Lire l'en-tête
seul aurait manqué l'échec ; il faut parser les `<RESULT>` par test.

⛔ Piège de lecture : le verdict de chaque test est un **élément enfant en
CDATA** (`<RESULT><![CDATA[PASS]]></RESULT>`), pas un attribut. Une regex sur
`RESULT="..."` rend 24 « sans résultat » et zéro échec — panne muette.

### Le FAIL est préexistant et déjà accepté par le Store

`Blocked executables` signale trois références dans `AZERTY Global.exe` :

- API de lancement de processus `shell32.dll!ShellExecuteW` ;
- référence à l'exécutable bloqué « CMD » ;
- référence à l'exécutable bloqué « MSBuild ».

Les trois sont sans action :

1. **`ShellExecuteW`** — 19 appels dans le source, tous de la forme
   `ShellExecuteW(0, "open", <url>, …)` : ouverture d'une URL dans le
   navigateur par défaut (site, guide, Discord, feedback, fiche Store). Aucun
   appel ne lance d'exécutable.
2. **« MSBuild »** — déjà identifié et traité : `GenerateAssemblyInfo` est
   désactivé dans le csproj pour cette raison précise, cf. l'en-tête de
   `src/AssemblyAttributes.cs`. Le littéral restant vient du runtime lié
   statiquement par Native AOT, pas du code applicatif.
3. **« CMD »** — aucun littéral correspondant dans le source ; même origine AOT.

**Preuve que ce n'est pas un régresseur** : le rapport
`wack-report-v1.1.0-bundle18-20260723.xml` — le bundle effectivement publié sur
le Store le 2026-07-23 — porte le **même** `OVERALL_RESULT=PASS` avec le
**même** `Blocked executables FAIL`, et une liste plus longue : « CMd », « CsI »
et « MSBuild ». La v1.2.0 en a une de moins.

⛔ Ne pas « corriger » ce FAIL : il est structurel à Native AOT et la version
publiée vit avec depuis juillet.

## Outillage vérifié sur le poste

- `C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe`
- `C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe`
- `C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe`

Commande exacte, en PowerShell **administrateur** :

```powershell
& "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe" reset
& "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe" test `
    -appxpackagepath "D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store\msix\AZERTYGlobal-1.2.0.0.msixbundle" `
    -reportoutputpath "D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store\Archives\wack\2026-09\wack-report-v1.2.0-20260919.xml"
```

⛔ Deux contraintes de la ligne de commande, apprises à l'usage :

- c'est `-appxpackagepath` (fichier de paquet) et **non** `-packagefullname`,
  qui désigne un paquet déjà installé ;
- `-reportoutputpath` **exige un chemin absolu** ; un chemin relatif rend
  « The '/reportoutputpath' argument must be valid path to the report file »
  après un `reset` pourtant réussi.

Durée réelle : **~1 minute**, pas les 15-25 min d'un WACK complet. Sur un bundle
`Centennial`, le kit n'exécute que les 24 contrôles statiques de paquet ; il n'y
a ni test de performance au lancement ni test de suspension. La machine n'a donc
pas besoin d'être au repos — la consigne valait pour la suite runtime, qui ne
s'exécute pas ici.

⛔ Un WACK PASS ne remplace ni le verrou 4 (manifestes internes du bundle) ni la
recette VM : il certifie le paquet, pas le comportement de l'application.

## Ce qui reste

4. Vérifier les manifestes internes du bundle et des MSIX (identité, version,
   architectures, ressources) — `Verify-Release.ps1` ne les ouvre pas.
5. Paquet 1.1.0 publié + VM Hyper-V, puis la recette de `recette-vm.md`.
