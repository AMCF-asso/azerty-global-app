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

## Ce qui reste du verrou 3 : le WACK

⛔ **Non exécuté.** `appcert.exe` exige une invite élevée ; la session n'est pas
administrateur. Outils présents et vérifiés sur le poste :

- `C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe`
- `C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe`
- `C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe`

À lancer dans un PowerShell **administrateur** :

```powershell
Set-Location "D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store"
New-Item -ItemType Directory -Force "Archives\wack\2026-09" | Out-Null
& "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe" reset
& "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe" test `
    -apptype windowsstoreapp `
    -packagefullname "D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store\msix\AZERTYGlobal-1.2.0.0.msixbundle" `
    -reportoutputpath "Archives\wack\2026-09\wack-report-v1.2.0-20260919.xml"
```

Compter 15 à 25 minutes ; la machine doit rester libre pendant le test.
Le critère est `OVERALL_RESULT=PASS` dans le XML — précédent de référence :
`wack-report-v1.1.0-bundle18-20260723.xml`, PASS le 2026-07-23.

⛔ Un WACK PASS ne remplace ni le verrou 4 (manifestes internes du bundle) ni la
recette VM : il certifie le paquet, pas le comportement de l'application.
