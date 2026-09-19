# Recette VM 1.3.0 — feuille de résultats

⚠️ **La campagne porte désormais sur la 1.3.0**, pas sur la 1.2.0 : la 1.2.0
n’a jamais été soumise au Store, et la réorganisation du menu de la zone de
notification en fait une version de fonctionnalité (décision d’Antoine du
2026-09-19). Les lignes déjà renseignées contre un paquet 1.2.0 le disent dans
leur colonne « Preuve » et **ne valent pas** pour la 1.3.0 quand le paquet a
changé — VM-02 et VM-21 sont dans ce cas.

Compagnon de `recette-vm.md`, qui est le protocole. Ce fichier est le **compte
rendu** : une ligne par scénario, remplie au moment de l'exécution.

⛔ Un scénario non renseigné vaut **NON TESTÉ**, jamais « probablement bon ».

## Paquets de la campagne

| | |
|---|---|
| Candidat | `msix/AZERTYGlobal-1.3.0.0.msixbundle` — construit le 2026-09-19 à 15:51, `Verify-Release.ps1` **vert** |
| SHA-256 exe x64 | `582E3A750A23D3C02F4E35ED89E477CAC1775B334E4DD29C2F792F16F2AEA1B6` |
| SHA-256 exe arm64 | `C46A053C03B6871B057FDEA64A36827C4DF8C5AA22D7DE1E64BF762A10DC9A76` |
| Commit | `c1da0d6` (branche `release/1.2.0-notation-store`) |
| Sideload 1.3.0 | `Archives/local-signing/1.3.0.0/AZERTYGlobal-1.3.0.0-local-signed-20260919.msixbundle` — 6 806 449 o, SHA-256 `7FAF5B169605B86393EFFA9800C7BCD29FD8D561A65AB1A119FBCE767AF790E0` |
| Paquets 1.2.0 (périmés) | `Archives/local-signing/1.2.0.0/` — conservés, ne plus installer |
| Sideload 1.1.0 | `Archives/local-signing/1.1.0.0/AZERTYGlobal-1.1.0.0-resigned-20260919.msixbundle` |
| Certificat | `8086B18C82671DB12B366A60CD55D8EA3DB67DF0`, expire le 2027-08-18 |
| VM | `AZERTY-Test`, génération 2, 12 Go, Hyper-V sur `C:` |

⛔ Le candidat soumis au Store n'est **pas** signé ; les deux paquets sideload
ne servent qu'à la recette. Les binaires y sont identiques — ce que la
signature ne change pas.

## ⚠️ Deux limites à connaître avant de commencer

1. **La mise à jour Store→Store ne peut pas être testée ici.** La 1.1.0 servie
   par le Store est signée Microsoft ; poser par-dessus un paquet signé
   localement échoue sur la signature. VM-02 se joue donc sur la paire
   sideload, qui porte le **même** PFN `AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg`
   (cf. `manifestes-bundle-2026-09-19.md`) : la continuité du `TaskId`, des
   réglages et de la progression est bien vérifiée, le mécanisme Store ne
   l'est pas.
2. ⛔ **Hyper-V sur ce poste x64 ne peut pas exécuter d'invité ARM64.** Les
   lignes ARM64 de la recette (VM-21 et l'exécution réelle exigée au § 1)
   demandent une machine ARM physique. Aucune VM ne lèvera ce point.

## § 2 — Tests indispensables à la décision de soumission

| ID | Résultat | Date | Preuve / note |
|---|---|---|---|
| VM-01 | NON TESTÉ | | |
| VM-02 | **PARTIEL** | 2026-09-19 | Volet identité **vert** : `Add-AppxPackage` de la 1.2.0 par-dessus la 1.1.0 à 12:28, `Get-AppxPackage` rend **une seule** entrée — `AZERTYGlobal.AZERTYGlobal_1.2.0.0_x64__w9kghr08zmhbg`, X64. Remplacement, pas installation parallèle. ⛔ Reste dû : conservation des réglages, raccourcis, statistiques et progression, et **un seul** démarrage actif. Voir limite 1 pour ce que ce test ne couvre pas |
| VM-03 | NON TESTÉ | | |
| VM-04 | NON TESTÉ | | |
| VM-05 | NON TESTÉ | | |
| VM-06 | NON TESTÉ | | |
| VM-07 | NON TESTÉ | | |
| VM-08 | NON TESTÉ | | |
| VM-09 | NON TESTÉ | | |
| VM-10 | NON TESTÉ | | |
| VM-11 | NON TESTÉ | | |
| VM-12 | NON TESTÉ | | |
| VM-13 | NON TESTÉ | | |
| VM-14 | NON TESTÉ | | |
| VM-15 | NON TESTÉ | | |
| VM-16 | NON TESTÉ | | |
| VM-17 | NON TESTÉ | | |
| VM-18 | NON TESTÉ | | |
| VM-19 | NON TESTÉ | | |
| VM-20 | NON TESTÉ | | priorité : couche sécurisée sur champ de mot de passe |
| VM-21 | **PARTIEL** | 2026-09-19 | identité, version, architectures, contenu et absence de signature vérifiés hors VM : `manifestes-bundle-2026-09-19.md` ; WACK PASS : `bundle-msix-2026-09-19.md`. ⛔ Reste dû : le volet ARM64 exécuté, cf. limite 2 |
| VM-22 | NON TESTÉ | | |
| VM-23 | NON TESTÉ | | **priorité** : retours partiels de `SendInput` |

## § 3 — Parcours de retour utilisateur

| ID | Résultat | Date | Preuve / note |
|---|---|---|---|
| RET-01 | NON TESTÉ | | |
| RET-02 | NON TESTÉ | | |
| RET-03 | NON TESTÉ | | |
| RET-04 | **NON TESTÉ — décidé** | 2026-09-19 | ⛔ Hors périmètre de cette campagne : la VM est installée avec un **compte local**, sans compte Microsoft (décision d'Antoine du 2026-09-19). L'avis intégré Store ne peut pas être exercé sans session Store. À rouvrir après publication, sur poste réel |
| RET-05 | NON TESTÉ | | active le CLSID de toast vérifié au verrou 4 |
| RET-06 | NON TESTÉ | | dépend d'un état 1.1 réel, donc de VM-02 |
| RET-07 | NON TESTÉ | | |
| RET-08 | NON TESTÉ | | Narrateur, clavier seul, contraste |
| RET-09 | NON TESTÉ | | session longue |

## Préparation de la VM

⛔ **État au 2026-09-19 : `AZERTY-Test` est une coquille vide.** Le VHDX pèse
4 194 304 octets (4 Mo) pour 127 Go alloués — disque dynamique créé, jamais
démarré. Aucun système, aucun instantané. Rien de la recette ne peut commencer
avant l'installation de Windows 11.

Choix arrêtés le 2026-09-19 : **ISO officiel Microsoft** (pas l'image dev
Quick Create, qui embarque de l'outillage et expire à 90 jours) et **compte
local** (d'où RET-04 clos en NON TESTÉ ci-dessus).

0. ✅ Windows 11 installé depuis l'ISO officiel, compte local.
1. ✅ **Invité mesuré le 2026-09-19 à 12:21** : Microsoft Windows 11
   Professionnel 64 bits, **build 26200** (25H2), machine `AZERTY-RECETTE`,
   compte local `testeur`. Disposition clavier active : `040C:0000040C`
   — français (France), AZERTY standard : poste représentatif.
2. ✅ **Instantané « propre »** `propre-win11-25h2-20260919`.
3. ✅ Certificat `8086B18C` importé en **Personnes de confiance** de la machine
   locale, dans l'invité.
4. ✅ Sideload 1.1.0 installé le 2026-09-19 à 12:21. Contrôle d'identité **vert** :
   `AZERTYGlobal.AZERTYGlobal_1.1.0.0_x64__w9kghr08zmhbg`, architecture X64,
   PFN `AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg` — **le même que la 1.1.0 du
   Store**. Le chemin de mise à jour de VM-02 est donc le bon : la 1.2.0
   remplacera ce paquet au lieu de s'installer à côté.
   Application lancée et **onboarding mené à son terme** par Antoine à 12:24,
   ce qui crée les réglages et la progression dont VM-02 vérifie la survie.
5. ✅ **Second instantané** `etat-1.1.0-20260919`, pris le 2026-09-19 à
   12:24:48. Les deux instantanés de référence coexistent : `propre-win11-25h2-20260919`
   (12:00:44) et `etat-1.1.0-20260919`. ⚠️ Un troisième, `Automatic Checkpoint`
   du 11:11:37, est créé par Hyper-V lui-même ; il n'est pas une référence de
   campagne et les instantanés automatiques ont été désactivés pour qu'il n'en
   apparaisse plus.

⛔ **Ce qui vient d'être fait n'est pas VM-01.** VM-01 demande le **paquet
final** — la 1.2.0 — dans une VM propre. La 1.1.0 posée ici est le *socle* de
VM-02, pas un scénario. Le lancement sans erreur observé porte donc sur la
version précédente et ne renseigne aucune ligne de la grille.
6. Installer le candidat 1.2.0 par-dessus → c'est VM-02.

⚠️ Sans l'instantané de l'étape 2, VM-01 et VM-22 ne sont pas rejouables : une
VM déjà utilisée n'est plus une VM propre.

## ⛔ Piège : `Pack-MSIX.ps1` ne compile pas

**Mesuré le 2026-09-19.** Le script empaquette le `publish` déjà présent
(`src/bin/Release/net8.0-windows10.0.17763.0/win-<arch>/publish/AZERTY Global.exe`,
ligne 125) et ne lève une erreur que s'il est **absent**. Un publish périmé passe
donc sans un mot, et le bundle produit annonce la bonne version en portant l'ancien
binaire.

Coût réel ce jour-là : deux bundles reconstruits, signés, copiés en VM et installés
— tous trois embarquant un `publish` de 09:43, antérieur aux correctifs de 13:19 et
13:26. La recette a conclu « le correctif ne marche pas » sur un binaire qui ne le
contenait pas.

⛔ **Avant tout `Pack-MSIX.ps1`, compiler les deux architectures :**

```powershell
$env:PATH += ";C:\Program Files (x86)\Microsoft Visual Studio\Installer"
dotnet publish src\AZERTYGlobal.csproj -c Release -r win-x64
dotnet publish src\AZERTYGlobal.csproj -c Release -r win-arm64
```

⛔ **La version vit à cinq endroits indépendants, pas deux.** Mesuré le
2026-09-19 en passant à 1.3.0 : `scripts/Verify-Release.ps1` a refusé la release
trois fois de suite, sur un source différent à chaque passage.

| Source | Ce qu’elle gouverne | Format |
|---|---|---|
| `src/AZERTYGlobal.csproj` `<Version>` | version du bundle et noms de fichiers | `1.3.0` |
| `msix/AppxManifest.xml` `Version=` | **identité** du paquet | `1.3.0.0` |
| `src/Program.cs` `internal const string Version` | version affichée (infobulle, À propos, URL `/bug`) | `1.3.0` |
| `src/Properties/AssemblyInfo.cs` | `AssemblyFileVersion` et `AssemblyVersion` en 4 segments, `AssemblyInformationalVersion` en 3 | `1.3.0.0` / `1.3.0` |
| Documents | `msix/Fiche Store.md` (FR **et** EN), `Changelog.md`, `Publication Microsoft Store.md`, `../../../../.agent/CONTEXT_APP_MICROSOFT_STORE.md`, `CONTEXT_AZERTY_GLOBAL.md` | texte |

✅ **Le contrôle qui tranche : `scripts/Verify-Release.ps1`.** Il lit les cinq et
échoue sur le premier écart. ⛔ Le lancer **avant** de signer, pas après : les deux
premiers bundles 1.3.0 ont été construits et le premier signé avec un exe portant
encore `1.2.0` dans `Program.cs`.

⛔ **Et rien ne vérifie l’accord csproj / AppxManifest hors de ce script.** `src/AZERTYGlobal.csproj`
(`<Version>`) donne la version du **bundle** et les noms de fichiers produits ;
`msix/AppxManifest.xml` (`Version=`) donne la version **d'identité** du paquet, que le
script ne réécrit pas — il n'ajuste que `ProcessorArchitecture` (ligne 128). Rien ne
vérifie qu'elles coïncident : un candidat peut partir avec une identité qui ne
correspond pas à la version annoncée. Mesuré le 2026-09-19 en corrigeant l'une puis
l'autre sans jamais les avoir toutes les deux.

⚠️ Le contrôle qui tranche, et qui ne coûte rien : comparer l'horodatage du
`publish` à celui du dernier commit touchant `src/`. Un exe plus vieux que le commit
n'a pas à être empaqueté.

Même famille que le `dotnet test` à la racine de `src/` déjà consigné dans
`.claude/rules/app-repo-guard-blind-spots.md` : une commande qui réussit sans avoir
fait le travail. ⛔ Reste dû : porter ce piège dans cette rule, hors périmètre de la
session qui l'a mesuré.

## Écarts mesurés en VM

Constatés pendant la campagne, hors grille : ce sont des défauts de l'application,
pas des lignes de recette. ⛔ Les deux sont **ouverts** au 2026-09-19.

### Écart 1 — suspension pour compatibilité sur un geste banal

Ouvrir la zone de notification suffit à suspendre le remapping. Six occurrences
journalisées dans `error.log` le 2026-09-19, entre 12:29:44 et 12:32:33, toutes
identiques : `UnknownForegroundSuspended: action=disable`.

Deux causes possibles, dans `src/TypingEngine.Windows/ForegroundMonitor.cs` :

| Ligne | Cause | Lecture |
|---|---|---|
| 180 | la fenêtre de premier plan change entre les deux lectures | course, faux positif |
| 225 | `hasFg` faux avec un `pid` non nul — process illisible | identité vraiment inconnue |

Instrument qui a tranché : la clé `compatibilityDebugLog` de `config.json`, qui fait
écrire dans le même `error.log` des lignes `CompatMode` portant le nom du process
(`ConfigManager.cs:628`, `TrayApplication.cs:2151`). Les noms sont anonymisés par
HMAC-SHA256 à sel local (`ConfigManager.AnonymizeProcessName`) : ils se résolvent en
recalculant le HMAC des candidats **dans la VM**, avec le sel `_compat_log_salt` de
`config.json`.

**Cause établie le 2026-09-19** : `hash:f5796950` = `explorer.exe` (suspend),
`hash:93afb154` = `ShellExperienceHost.exe` (rétablit), `hash:96e5f434` = `msedge.exe`.
Cliquer la barre des tâches met `explorer.exe` au premier plan, la fenêtre bascule vers
le volet `ShellExperienceHost` entre les deux `GetForegroundWindow()` d'un même
`Recompute`, et la branche de course fabriquait une identité inconnue. **Faux positif
sur le shell Windows, au geste le plus banal qui soit.**

✅ **Corrigé** (`edfe727`) : la course ne suspend plus, seul un suivi réellement
indisponible (`IsTrackingAvailable` faux) suspend. La sécurité de frappe est inchangée :
`GetEmitContext()` recontrôle la fenêtre à chaque émission et refuse d'émettre dès
qu'elle a bougé — c'est la garde qui compte. Deux tests neufs dans
`src/TypingEngine.Windows.Tests/ShellRaceSuspensionTests.cs`, témoin de mutation passé :
ancienne ligne remise, 1 rouge et le bon, 153 verts.

✅ **Vérifié en VM le 2026-09-19 à 13:44**, paquet de diagnostic `1.2.0.1` portant le
binaire de 13:38 : explorateur ouvert, Edge ouvert, zone de notification ouverte —
**`error.log` n'existe pas**, aucun événement de compatibilité, aucune bulle.

### Écart 2 — la bulle de précaution masque l'icône du tray

Conséquence du précédent, et plus grave que lui : ouvrir la zone de notification
déclenche la bulle, la bulle recouvre l'icône de l'application, **et l'utilisateur
ne peut plus quitter l'app par le seul geste prévu**. Mesuré par Antoine le
2026-09-19 dans la VM.

Contournement, en ligne de commande seulement :

```powershell
Get-Process | Where-Object ProcessName -like '*AZERTY*' | Stop-Process -Force
```

⚠️ Un utilisateur du Store n'a pas ce contournement dans les mains.

✅ **Clos par conséquence le 2026-09-19** : l'écart 1 corrigé, la bulle ne se déclenche
plus sur ce geste, donc l'icône reste accessible. ⚠️ Le défaut de conception demeure en
théorie — une bulle émise pour un vrai motif masquera toujours l'icône — mais il n'est
plus atteignable par un usage normal. À rouvrir si une suspension légitime se produit
pendant que l'utilisateur cherche à quitter.

## Porte de décision

Reprendre le § 5 de `recette-vm.md`. ⛔ Ni ce fichier ni un bundle vert ne
valent décision : la soumission se décide sur des lignes renseignées.

⚠️ L'échéance « avant le 17 septembre » du protocole est dépassée depuis le
2026-09-17. Elle n'a pas été retenue ici : aucune date de soumission n'est
posée tant que cette grille est vide.
