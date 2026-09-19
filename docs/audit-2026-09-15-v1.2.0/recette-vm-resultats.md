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
| Sideload 1.3.0 **en vigueur** | `Archives/local-signing/1.3.0.0/AZERTYGlobal-1.3.0.0-local-signed-20260919b.msixbundle` — 6 806 578 o, SHA-256 `6DCEB741B7F1199C2128AEFA97846C1DEA9B8033B352597A4E5416ABBD5876DA`, commit `17382e0`. ⛔ C'est celui-ci qui porte le libellé d'accueil corrigé |
| Sideload 1.3.0 (périmé) | `…-local-signed-20260919.msixbundle` — 6 806 449 o, SHA-256 `7FAF5B169605B86393EFFA9800C7BCD29FD8D561A65AB1A119FBCE767AF790E0`. Même identité **et même version** que le précédent : réinstaller exige `Remove-AppxPackage` d'abord, sinon `0x80073CFB` |
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
| VM-01 | **VERT** | 2026-09-19 | Instantané `propre-win11-25h2-20260919` appliqué, certificat importé (voir la prépa, étape 2), `Add-AppxPackage` du sideload 1.3.0.0 accepté. Lancement depuis le menu Démarrer : **une seule** instance, icône présente dans la zone de notification, fenêtre d’accueil lisible et complète, **v1.3.0** affichée dans son en-tête. ⚠️ **Preuve corrigée le 2026-09-19 à 16:51.** Le premier contrôle visait `$env:LOCALAPPDATA\AZERTY Global\error.log` et rendait `False` : **mauvais chemin**. L'app est en MSIX, ses écritures sont redirigées vers `%LOCALAPPDATA%\Packages\AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg\LocalCache\Local\AZERTY Global\`. Le fichier **existe** à cet endroit, et son contenu ne comporte **aucune erreur** — uniquement des événements de compatibilité (`UserOverrideApplied`, `CompatMode`), que ce même fichier reçoit par conception. Écart 1 donc bien clos sur le paquet de release, mais pour cette raison-là. ⛔ Un défaut de libellé relevé au passage, corrigé après coup, voir « Écart 3 » |
| VM-02 | **PARTIEL** | 2026-09-19 | Volet identité **vert** : `Add-AppxPackage` de la 1.2.0 par-dessus la 1.1.0 à 12:28, `Get-AppxPackage` rend **une seule** entrée — `AZERTYGlobal.AZERTYGlobal_1.2.0.0_x64__w9kghr08zmhbg`, X64. Remplacement, pas installation parallèle. ⛔ Reste dû : conservation des réglages, raccourcis, statistiques et progression, et **un seul** démarrage actif. Voir limite 1 pour ce que ce test ne couvre pas |
| VM-03 | **VERT** | 2026-09-19 | Paquet b, `StartupTask` MSIX `AZERTYGlobalStartup` (⛔ pas une clé `Run` : c'est Windows qui tranche, pas l'app). État lu à chaque étape dans `HKCU\…\AppModel\SystemAppData\AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg\AZERTYGlobalStartup`. **(a) Refus initial** : `State 0`, `UserEnabledStartupOnce 0` — l'installation n'active rien, conforme à `Enabled="false"` du manifeste. **(b) Accord depuis l'app** : `State 2`, `UserEnabledStartupOnce 1`, AZERTY Global **Activé** dans Paramètres Windows → Applications → Démarrage. **(c) Refus imposé par Windows** — le vrai piège, l'app rejoue `Set(true)` à chaque fermeture : après bascule sur Désactivé, quitter l'app, la relancer et la requitter, `State` reste à **1** avec `LastDisabledTime` posé, et la case « Lancer au démarrage de Windows » de l'app **s'est décochée d'elle-même** — l'interface reflète le refus au lieu de mentir. **(d) Fermeture/réouverture de session** : refus → aucune icône AG au retour ; réactivation depuis Paramètres Windows puis nouvelle session → une seule icône AG, apparue seule. ✅ L'état réel Windows correspond au choix dans les quatre cas, aucun consentement refusé contourné |
| VM-04 | **VERT** | 2026-09-19 | Paquet b, Bloc-notes Windows 11 (texte brut, UTF-8, CRLF). Les cinq changements saisis d'affilée rendent `éèçàÉÈÇÀ.;@#(){}[]|\ˆ¨~` — **23 caractères**, compteur du Bloc-notes à 23, `Ln 1, Col 24` : aucun caractère ajouté, perdu ni répété, et les quatre majuscules accentuées sortent bien du Verr. Maj. intelligent. Touches mortes `ˆ ¨ ~` posées en fin de ligne sans composition parasite. ⚠️ Au passage : les parenthèses `(` `)` ne sont **pas** en AltGr sur la rangée de repos — la carte 4 de l'accueil n'annonce que `{ } [ ] | \`. Erreur de consigne de la session, pas un défaut du produit. ✅ Volet **navigateur** : même séquence dans la barre d'adresse Edge (Chromium), rendu `éèçàÉÈÇÀ.;@#{}[]|\^¨~` — identique, aucun doublon ni caractère avalé par le rendu Chromium |
| VM-05 | **PARTIEL** | 2026-09-19 | Paquet b. **Chemin normal : vert.** Bloc-notes, `^a ^e ^espace` → `âê^`, `¨e ¨i ¨espace` → `ëï¨`, `` `a `u `espace `` → `` àù` ``, puis les mêmes avec Verr. Maj. → `ÂÊ^ ËÏ¨ ÀÙ``. Espace rend bien la touche morte seule. Sondes AG120-01 passées : `^` + **Maj+a** → `Â`, `¨` + **Maj+e** → `Ë`, et **AltGr** relâché juste avant une touche morte → `{â` — aucun modificateur résiduel, aucune touche morte fantôme. Tilde : `~n` → `ñ`, `~espace` → `~`. ⛔ **Volet « compatibilité native » (forceOn) : non concluant, voir Écart 5** |
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
   ⛔ **Mesuré le 2026-09-19 à 16:00 : cet instantané ne contient PAS le
   certificat.** Il a été pris à 12:00:44, l’import de l’étape 3 est venu après.
   Repartir de « propre » et installer un paquet signé échoue sur
   `0x800B0109` — « le certificat racine de la signature doit être approuvé ».
   ⚠️ L’étape 3 ci-dessous, écrite au passé, laisse croire le contraire : elle
   n’est vraie que pour `etat-1.1.0-20260919`.
   ✅ Geste à refaire après chaque application de « propre », en PowerShell
   **administrateur** dans l’invité, le `.cer` copié sur le Bureau :

   ```powershell
   Import-Certificate -FilePath "$env:USERPROFILE\Desktop\AZERTYGlobal-local-test.cer" -CertStoreLocation Cert:\LocalMachine\Root
   Import-Certificate -FilePath "$env:USERPROFILE\Desktop\AZERTYGlobal-local-test.cer" -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```

   Le `.cer` vit dans `Archives/local-signing/1.3.0.0/` (exporté le 2026-09-19 à
   16:04 depuis `Cert:\CurrentUser\My`, empreinte `8086B18C82671DB12B366A60CD55D8EA3DB67DF0`).
   ⚠️ Il était auparavant rangé dans `Archives/local-signing/1.2.0.0/` seulement,
   donc invisible depuis le dossier de la campagne en cours.
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

### Écart 3 — « 99 % de vos habitudes préservées » dans l’accueil

Relevé par Antoine le 2026-09-19 à 16:05, dans la VM, sur le paquet 1.3.0.0.
Le titre de l’étape 1 de la fenêtre d’accueil annonçait « 5 améliorations,
99 % de vos **habitudes** préservées ». Le chiffre porte sur les frappes, pas
sur les habitudes : l’ancien libellé promettait quelque chose de plus large que
ce qui est mesuré.

✅ **Corrigé** : `src/Localization/L.Onboarding.cs`, `Onboarding_Step1Title`
devient « 5 améliorations, 99 % de vos **frappes** préservées ». Anglais aligné
dans la foulée, `habits` → `keystrokes` — décidé sans Antoine, même
raisonnement, le mot anglais portait le même glissement.

⚠️ La chaîne n’apparaît qu’à cet endroit ; la fiche Store ne reprend pas la
formule. ⛔ Aucun test ne verrouille ce libellé.

✅ **Clos le 2026-09-19 à 16:18.** Chaîne republiée, réempaquetée,
`Verify-Release.ps1` vert, resignée en `…-20260919b`. Antoine a désinstallé puis
réinstallé dans la VM — sur le même état que VM-01, sans restaurer d'instantané —
et l'étape 1 de l'accueil affiche « 99 % de vos **frappes** préservées ».
VM-01 reste **VERT** : le paquet b ne diffère du précédent que par ce libellé.

### Écart 4 — la fenêtre Paramètres est plus haute que l'écran

Relevé le 2026-09-19 à 16:41 dans la VM (Windows 11 25H2, affichage Hyper-V).
La fenêtre Paramètres ne tient pas dans la hauteur disponible, **n'a pas de
défilement** et ne se redimensionne pas utilement. Le troisième bouton radio de
la section « Apps suspendues », *Forcer désactivation*, est hors écran et
inatteignable à la souris.

⛔ Conséquence concrète : sur un portable 1366×768, un utilisateur qui a posé
*Forcer compatibilité jeu* sur une application **ne peut plus revenir en
arrière** par la souris. Contournement clavier : sélectionner un radio visible
puis parcourir le groupe aux flèches.

⚠️ La refonte déjà arbitrée pour la v2.0.0 (Paramètres à 3 onglets,
`operations/refonte-app/2026-08-28-audit-refonte-ui.md:784`) supprime la cause.
La question ouverte est de savoir si la v1.3.0 part au Store avec ce défaut.

⚠️ Deux frictions de la même section, mineures et non bloquantes : **Ajouter…**
ouvre un sélecteur de fichiers `*.exe` alors que le réglage porte sur un **nom
de processus** — l'utilisateur doit aller chercher `notepad.exe` dans
`System32` — et une application ajoutée arrive **par défaut en « désactivée »**
sans que rien ne l'annonce.

### Écart 5 — mode « Forcer compatibilité jeu » : caractères perdus, mesures contradictoires

⛔ **Ouvert. Bloquant tant qu'il n'est pas expliqué.** Relevé le 2026-09-19
entre 16:40 et 16:55 sur le paquet b, `notepad.exe` puis `msedge.exe` en
`forceOn` (`config.json` relu : `"compatibility": { "Notepad.exe": "forceOn" }`).

Ce qui a été mesuré, dans l'ordre :

1. Bloc-notes en `forceOn` : `^a`, `@` et `²` **ne produisent rien** ; la frappe
   est avalée, pas remplacée par un caractère faux.
2. Bloc-notes, seconde passe : `²`, `a` et `é` sortent — tous atteignables
   **directement** sur le clavier natif. `^a` et `@` restent muets ; tous deux
   exigent un repli (Alt+code pour `â`, combo **AltGr** pour `@`).
3. Edge en `forceOn` : `^a` rend `a` — la touche morte est **perdue**, la lettre
   est gardée. `²` sort.
4. ⚠️ **Puis, en revenant dans Edge sans rien changer, `â`, `@` et `é` sortent
   correctement.** Le même geste dans la même application donne deux résultats.

Le comportement dépend donc de l'instant, pas seulement de la cible : une piste
est la fraîcheur du snapshot `ForegroundMonitor` après un changement de fenêtre.

✅ Ce qui est **écarté** par lecture du code, pas par supposition :
- l'app ne s'auto-consomme pas ses injections — tous les `INPUT` portent
  `KeyboardHook.INJECTED_FLAG` (`KeyMapper.cs:933, 1138, 1390, 1406`) et le hook
  les ignore (`KeyboardHook.cs:157`) ;
- le mode est bien actif : `error.log` journalise
  `CompatMode: Default → NativeCombo` puis le retour, avec `hasFg=True` ;
- l'override utilisateur est bien lu (`ForegroundMonitor.cs:258`).

⛔ **À reprendre dans une session de débogage dédiée**, pas au fil de la recette :
il faut une reproduction propre — un seul processus en `forceOn`, application
relancée, aucun passage par la fenêtre Paramètres entre le réglage et la frappe —
et les statistiques d'émission de niveau 2 (`compatibilityDebugLog`, déjà activé
dans la VM) relevées après sortie de l'application.

## Porte de décision

Reprendre le § 5 de `recette-vm.md`. ⛔ Ni ce fichier ni un bundle vert ne
valent décision : la soumission se décide sur des lignes renseignées.

⚠️ L'échéance « avant le 17 septembre » du protocole est dépassée depuis le
2026-09-17. Elle n'a pas été retenue ici : aucune date de soumission n'est
posée tant que cette grille est vide.
