# Audit v1.2.0 -- axe « portes de release, documents et conformite Store »

Depot en lecture seule. Plage auditee : `452aab0^..HEAD` = `ad049fc8` -> `697d1a3b` (23 commits),
HEAD date `2026-08-17T14:58:04+02:00`. Aucune commande git d'ecriture utilisee. Aucun package
MSIX produit. Aucune publication AOT tentee.

Un autre axe du meme audit vit deja dans ce dossier (`raw-code.md`, `delta-textes*.md/py`,
`witness-baseline.py`, `AppxManifest.BASELINE-452aab0parent.xml`,
`AppxManifest.INSTALLED-1.1.0.0.xml`) : je le cite quand il recoupe mes propres mesures, je ne
le reecris pas.

---

## 1. Coherence des versions

Chaine relevee litteralement, fichier par fichier :

| Fichier | Ligne | Champ | Valeur litterale |
|---|---|---|---|
| `src/AZERTYGlobal.csproj` | 10 | `<Version>` | `1.2.0` |
| `src/AZERTYGlobal.csproj` | -- | Assembly*/File*/Informational* MSBuild | absents -- `GenerateAssemblyInfo` = `false` (ligne 19), attributs ecrits a la main dans `AssemblyInfo.cs` |
| `src/Program.cs` | 10 | `internal const string Version` | `"1.2.0"` |
| `src/Properties/AssemblyInfo.cs` | 8 | `AssemblyFileVersion(...)` | `"1.2.0.0"` |
| `src/Properties/AssemblyInfo.cs` | 9 | `AssemblyInformationalVersion(...)` | `"1.2.0"` |
| `src/Properties/AssemblyInfo.cs` | 12 | `AssemblyVersion(...)` | `"1.2.0.0"` |
| `msix/AppxManifest.xml` | 12 | `Identity Version` | `"1.2.0.0"` |
| `Changelog.md` | 3 | titre de section | `## Version 1.2.0 -- 17 aout 2026` |
| `msix/Fiche Store.md` | 79 | notes FR | `Version 1.2.0 :` (espace ASCII avant `:`) |
| `msix/Fiche Store.md` | 211 | notes EN | `Version 1.2.0:` (sans espace) |
| `README.md` | 17 | etat du code | version 1.2.0, manifeste MSIX local en 1.2.0.0, portes de version de Verify-Release.ps1 franchies |
| `Publication Microsoft Store.md` | 5 | version cible | `Version cible : 1.2.0` |
| `Publication Microsoft Store.md` | 6 | version publiee | `Version publiee Store : 1.1.0` (valeur reellement en ligne, distincte de la cible) |
| `Publication Microsoft Store.md` | 7 | package cible | `Package Store : 1.2.0.0` |
| `msix/README.md` | 131 | note generique | "Le dernier segment de version doit rester a 0 pour le Store (ex: 0.9.5.0)" -- exemple generique non lie a 1.2.0 |
| `msix/README.md` | 139 | pied de page | "Derniere mise a jour : 2026-06-29 (v1.0.0 -- publication Microsoft Store ; MSIX AMCF a produire)" -- **perime**, jamais remonte depuis v1.0.0 |
| `docs/keyboard-platform.md` | 58 | narratif | "la base interne 1.1.2 et les fonctions 1.2.0 en developpement sont reconciliees dans ce depot canonique" |

Format a quatre segments (manifeste, AssemblyVersion, AssemblyFileVersion) vs trois segments
(csproj Version, Program.Version, AssemblyInformationalVersion) : coherent partout, la regle
« ajouter .0 si 3 segments » de `Verify-Release.ps1:125` est appliquee identiquement dans tous
les fichiers ci-dessus.

**Aucune divergence trouvee dans l'etat actuel du depot.** Le Changelog affirme que
`Program.cs`/`AssemblyInfo.cs` etaient restes en `1.1.2` -- c'est un fait historique (commit
`27541e1`, voir § 3), deja corrige a HEAD.

**Mais l'executable publie sur disque ne suit pas cette table** -- voir § 2 et § "Portes qui
passeraient mais ne prouvent rien" : le binaire `AZERTY Global.exe` actuellement dans
`src/bin/Release/.../publish/` porte encore `FileVersion 1.1.2.0` / `ProductVersion 1.1.2`, pas
`1.2.0`. La table ci-dessus decrit le code source, pas l'artefact construit qui trainerait sur
le poste si on packageait sans republier.

---

## 2. `scripts/Verify-Release.ps1` -- porte de release

### Liste exhaustive des verifications (lecture ligne a ligne du script)

1. **L.117-121** -- charge `src/AZERTYGlobal.csproj` en XML, lit `Project.PropertyGroup.Version`.
   Echec si vide : "Version introuvable dans $csprojPath".
2. **L.125** -- calcule `$storeVersion` = la version telle quelle si 4 segments, sinon
   "$version.0".
3. **L.131-134** -- motif `internal const string Version = "([^"]+)"` sur `src/Program.cs`,
   compare a `$version`. Echec : "Program.cs n'est pas aligne sur $version".
4. **L.136, 140** -- motif `AssemblyFileVersion\("([^"]+)"\)` sur `AssemblyInfo.cs`, compare a
   `$storeVersion`. Echec : "AssemblyFileVersion n'est pas aligne sur $storeVersion".
5. **L.137, 141** -- motif `AssemblyInformationalVersion\("([^"]+)"\)`, compare a `$version`.
   Echec : "AssemblyInformationalVersion n'est pas aligne sur $version".
6. **L.138, 142** -- motif `AssemblyVersion\("([^"]+)"\)`, compare a `$storeVersion`. Echec :
   "AssemblyVersion n'est pas aligne sur $storeVersion".
7. **L.143** -- `$manifest.Package.Identity.Version` (XML, pas regex) compare a `$storeVersion`.
   Echec : "AppxManifest.xml n'est pas aligne sur $storeVersion".
8. **L.145** -- motif litteral "Version {version} :" (espace ASCII avant `:`) dans
   `msix/Fiche Store.md`. Obligatoire (Assert-Match, pas IfExists).
9. **L.146** -- motif litteral "Version {version}:" (sans espace) dans le meme fichier.
   Obligatoire.
10. **L.147** -- SI `Publication Microsoft Store.md` existe : motif "Version cible : {version}".
11. **L.148** -- SI le meme fichier existe : motif "Package Store : {storeVersion}".
12. **L.149** -- SI `TO-DO.md` existe : motif "Version actuelle : {version}".
13. **L.150** -- motif "## Version {version}" dans `Changelog.md`. Obligatoire.
14. **L.151** -- SI `../../../.agent/CONTEXT_APP_MICROSOFT_STORE.md` existe : motif
    "> **Version actuelle** : {version}".
15. **L.152** -- SI `../../../.agent/CONTEXT_AZERTY_GLOBAL.md` existe : motif
    "**Application Microsoft Store( / MSIX)?** v{version}".
16. **L.156-161** -- pour x64 et arm64 : existence de
    `src/bin/Release/net8.0-windows10.0.17763.0/win-{arch}/publish/AZERTY Global.exe`. Echec :
    "Fichier requis introuvable: $publishExe".
17. **L.163-165** -- existence de `msix/AZERTYGlobal.msixbundle`. Echec :
    "Fichier requis introuvable: $bundlePath".
18. **L.166-168** -- existence de `msix/AZERTYGlobal-{storeVersion}.msixbundle`.
19. **L.170-174** -- SHA256 du bundle stable == SHA256 du bundle versionne.
20. **L.178-191** -- pour chaque arch : SHA256 de l'exe publie == SHA256 de l'exe extrait de
    `AZERTYGlobal-{storeVersion}-{arch}.msix` dans le bundle.

### Ce qu'il couvre / ne couvre PAS

Aucune verification de : capacites du manifeste (Capabilities, StartupTask, extensions com:),
nombre de tests, avertissements de build, contenu reel de la Fiche Store au-dela du numero de
version, presence des notes de version anglaises au-dela du motif de version, typographie NBSP,
schema de disposition, litteraux d'identite, resultat WACK, dimensions des captures d'ecran. Un
mismatch de capacite manifeste, un test supprime, une note de version absente en anglais (hors
le motif de version), une regression NBSP : **rien de tout ca ne fait echouer cette porte.**

### Execution reelle -- sortie brute

```
PS> powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-Release.ps1"
WARNING: TO-DO non disponible dans ce depot; verification ignoree.
WARNING: Contexte app non disponible dans ce depot; verification ignoree.
WARNING: Contexte projet non disponible dans ce depot; verification ignoree.
powershell : Fichier requis introuvable: D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store\msix\AZERTYGlobal.msixbundle
At D:\My files\...\scripts\Verify-Release.ps1:164 char:5
+     throw "Fichier requis introuvable: $bundlePath"
Exit code: 1
```

**Les 15 verifications de version (items 1 a 15 ci-dessus) passent toutes** -- aucune n'a leve
avant l'item 17. Les avertissements sur `TO-DO.md` et les deux `.agent/CONTEXT_*.md` sont
attendus : ces trois fichiers n'existent plus dans ce depot (migration vers le catalogue LLM
wiki, cf. `Keyboard Layouts/CLAUDE.md`), confirme par recherche (aucun des trois trouve). Le
script porte donc une logique morte qui verifie des fichiers qu'aucune session ne maintient
plus.

**Le script s'arrete a l'item 17** : aucun `.msixbundle` n'existe dans `msix/` (recherche
`*.msixbundle`/`*.msix` : zero resultat). Les items 18 a 20 (bundle versionne, comparaison de
hash bundle stable/versionne, comparaison de hash publish/bundle) ne sont **jamais atteints** --
impossibles a evaluer dans l'etat actuel du depot.

Fait notable et non anodin : les fichiers `AZERTY Global.exe` publies (item 16) existent bel et
bien pour les deux architectures -- voir § "Portes qui passeraient mais ne prouvent rien" pour
ce qu'ils contiennent reellement.

---

## 3. Les tests

### Comptage reel par execution de `dotnet test`

```
dotnet test src/TypingEngine.Core.Tests --nologo -v minimal
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 8 ms

dotnet test src/TypingEngine.Windows.Tests --nologo -v minimal
Passed!  - Failed: 0, Passed: 95, Skipped: 0, Total: 95, Duration: 26 ms

dotnet test src/AZERTYGlobal.Tests --nologo -v minimal
Passed!  - Failed: 0, Passed: 149, Skipped: 0, Total: 149, Duration: 557 ms
```

| Suite | Annonce (Changelog.md:59) | Reel (mesure) | Ecart |
|---|---|---|---|
| Applicatifs (AZERTYGlobal.Tests) | 149 | **149** | 0 |
| Moteur Windows (TypingEngine.Windows.Tests) | 95 | **95** | 0 |
| Moteur portable (TypingEngine.Core.Tests) | 6 | **14** | **+8** |
| **Total** | **250** | **258** | **+8** |

Le nombre « 6 » etait exact **au moment ou il a ete ecrit** : a la base de reconciliation
`452aab0`, `TypingEngine.Core.Tests` comptait `CompositionEngineTests` (3 [Fact]) +
`LayoutJsonParserTests` (1 [Theory] x 3 [InlineData]) = 6. Le commit qui a introduit la phrase
« 6 moteur portable » est `7ad768a` (2026-08-17 11:36). Le commit `6bd8624` (« The layout says
where it is wrong, and the schema agrees with the parser », 2026-08-17 14:44 -- **apres**
`7ad768a`, toujours dans la plage auditee) a ajoute 8 [Fact] a `LayoutJsonParserTests.cs`
(0 -> 8 facts, +84 lignes), portant la suite a 3 + 8 + 3 = 14. Le Changelog n'a plus ete
retouche depuis. `docs/audit-v1.2.0/raw-code.md:5-7` (autre axe de cet audit) confirme
independamment le meme total 258 par sa propre execution.

### Verification des trois sous-comptes cites par le Changelog

Comptes directement dans les fichiers ajoutes par les commits nommes ([Fact] + cas
[InlineData], recoupe par lecture du fichier) :

- **« 8 nouveaux sur le texte partage »** -> `src/AZERTYGlobal.Tests/ChallengeShareTests.cs`
  (ajoute par `32ce6d5`) : 8 [Fact], 0 [Theory] -> **8**. Exact. (« texte partage » = le texte
  copie par le bouton « Copier mon resultat », pas une notion de localisation.)
- **« 12 sur la relance du lancement automatique »** ->
  `src/AZERTYGlobal.Tests/AutoStartNudgeTests.cs` (ajoute par `02d5458`) : 8 [Fact] + 2 [Theory]
  (2 [InlineData] chacune) = 8 + 4 = **12**. Exact.
- **« 4 sur la coherence des versions »** -> `src/AZERTYGlobal.Tests/VersionAlignmentTests.cs`
  (ajoute par `27541e1`) : 4 [Fact], 0 [Theory] -> **4**. Exact.

Ces trois sous-comptes sont corrects. Seul le total agrege de la suite portable (6 -> 14) a
derive apres coup, par un commit ulterieur non repercute dans le texte.

### Les 4 tests de coherence de version ne sont pas tautologiques

`src/AZERTYGlobal.Tests/VersionAlignmentTests.cs` :

- `ProgramVersion_HasThreeParts` (l.45-49) : `Assert.Matches(@"^\d+\.\d+\.\d+$", Program.Version)`.
- `ProgramVersion_MatchesAssemblyInformationalVersion` (l.53-57) : compare la constante source
  `Program.Version` ("1.2.0", compilee depuis `Program.cs`) a
  `App.GetCustomAttribute<AssemblyInformationalVersionAttribute>()` lu **par reflexion sur
  l'assembly compile** (`typeof(Program).Assembly`, l.27) -- c'est-a-dire la valeur ecrite a la
  main dans `AssemblyInfo.cs`, compilee separement.
- `AssemblyFileVersion_IsProgramVersionWithRevisionZero` (l.61-65) et
  `AssemblyVersion_IsProgramVersionWithRevisionZero` (l.67-73) : meme schema, contre
  `AssemblyFileVersionAttribute` et `Assembly.GetName().Version`.

Les deux cotes de chaque `Assert.Equal` viennent de **deux endroits distincts du code source**
(une constante dans `Program.cs`, un attribut dans `AssemblyInfo.cs`), lus l'un en dur et
l'autre par reflexion sur le binaire reellement compile -- pas une valeur comparee a elle-meme
recopiee dans le test. Une vraie divergence entre les deux fichiers ferait echouer ces tests, ce
qui est precisement le scenario 1.1.2 vs 1.2.0 que le commentaire du fichier (l.15-17) dit avoir
existe.

### Build Release -- 0 avertissement, 0 erreur

```
dotnet build src/AZERTYGlobal.csproj -c Release --nologo
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Confirme pour le projet applicatif. Les trois projets de tests compilent aussi sans erreur en
Release ; ils affichent 2 `warning NU1900` chacun (« Unable to load the service index for ...
nuget.org ») -- un avertissement reseau de l'environnement d'audit (sandbox sans acces a l'API
d'audit de vulnerabilites NuGet), pas un avertissement de code. Le projet applicatif seul n'a
montre aucun NU1900.

---

## 4. `msix/AppxManifest.xml`

### Valeurs litterales a HEAD

| Champ | Valeur |
|---|---|
| Identity Name | `AZERTYGlobal.AZERTYGlobal` |
| Identity Publisher | `CN=7FD049E3-1C58-42E0-A07F-A9712DE19E38` |
| Identity Version | `1.2.0.0` |
| Identity ProcessorArchitecture | `x64` (valeur du gabarit committe ; `Pack-MSIX.ps1` la reecrit par architecture au moment du pack, `msix/README.md:64`) |
| TargetDeviceFamily MinVersion | `10.0.17763.0` |
| TargetDeviceFamily MaxVersionTested | `10.0.26100.0` |
| Application Id | `AZERTYGlobal` |
| StartupTask TaskId | `AZERTYGlobalStartup` |
| StartupTask Enabled | `false` |
| Capability (liste complete) | `runFullTrust` (une seule, rescap:) |

### Diff contre la base `452aab0^`

```
git diff 452aab0^..HEAD -- msix/AppxManifest.xml
-    Version="1.0.0.0"
+    Version="1.2.0.0"
```

**Seule la version change.** Identity Name, Publisher, ProcessorArchitecture,
TargetDeviceFamily, Application Id, StartupTask (TaskId et Enabled="false" compris) et la liste
des Capability sont **octet pour octet identiques** entre la base et HEAD. Zero capacite
ajoutee, zero capacite retiree.

### La question posee -- une declaration manquante existe, mais ce n'est pas une Capability

`StoreReview.cs` (notation integree, `StoreContext.RequestRateAndReviewAppAsync`) : le
commentaire du fichier (l.10-12) dit que l'API « exige une identite de package (MSIX) et
Windows 10 1809 -- soit exactement le MinVersion declare dans AppxManifest.xml ». Aucune
capacite supplementaire n'est revendiquee par le code ni par les docs du depot pour cette API.
Le manifeste inchange semble donc suffisant pour cette fonction precise, dans les limites de ce
que ce depot documente lui-meme.

**L'activateur de toast COM (`ToastActivation.cs`) est une autre histoire.** Le fichier
lui-meme affirme, deux fois :

- `src/ToastActivation.cs:12` -- « Le CLSID doit rester identique a celui du manifest
  (com:Class + ToastActivatorCLSID). »
- `src/ToastActivation.cs:53` -- « CLSID declare dans msix/AppxManifest.xml (com:Class +
  ToastActivatorCLSID). »

`msix/AppxManifest.xml` ne contient **aucune** des chaines `com:Class`, `ToastActivatorCLSID`
ni `xmlns:com` (recherche exhaustive, zero resultat) -- ni a HEAD, ni a la base (le diff
ci-dessus montre que seule la ligne Version a change). Le test qui devrait garantir cet accord
le dit lui-meme :

```
src/AZERTYGlobal.Tests/ToastActivationTests.cs:28-35
[Fact]
public void ActivatorClsid_MatchesAppxManifestDeclaration()
{
    // Le CLSID du code doit rester aligne sur msix/AppxManifest.xml
    // (com:Class Id + ToastActivatorCLSID). Le manifest n'est pas lisible depuis les
    // tests (hors arborescence src) : on fige la valeur ici -- toute divergence
    // volontaire doit etre repercutee aux deux endroits.
    Assert.Equal("126A58B4-3200-43A6-9018-612C108F4A94", ToastActivation.ActivatorClsidString);
}
```

Ce test compare une chaine codee en dur dans le test a une constante codee en dur dans
`ToastActivation.cs` (l.54) -- les deux litteraux peuvent diverger *ensemble* du manifeste sans
que ce test le voie jamais, et c'est exactement l'etat actuel : le manifeste ne porte tout
simplement pas de declaration com:Class/ToastActivatorCLSID, avec ou sans CLSID correct.
`TrayApplication.cs:227` (`_toastActivatorRegistered = ToastActivation.Register();`) prouve que
ce n'est pas du code mort : il tourne a chaque demarrage package (ConfigManager.IsPackaged).

Je ne peux pas, depuis ce depot seul, dire si `CoRegisterClassObject` en cours de process
suffit sans declaration manifeste ou si Windows a besoin de cette derniere pour router
l'activation de toast vers le process vivant -- le depot ne documente que le premier mecanisme
et affirme lui-meme avoir besoin du second. C'est un pointeur, pas un verdict : la
contradiction est interne au depot (le commentaire du code contre le contenu reel du
manifeste), verifiable sans sortir d'ici.

---

## 5. Typographie -- espace insecable vs espace ordinaire

Comptage par octets (`\xc2\xa0` = U+00A0 encode UTF-8), pas a l'oeil :

```
src/Localization/L.Challenge.cs       7
src/Localization/L.Keyboard.cs        1
src/Localization/L.Learning.cs        5
src/Localization/L.LessonsWindow.cs   13
src/Localization/L.Notifications.cs   3
src/Localization/L.Onboarding.cs      4
src/Localization/L.Search.cs          3
src/Localization/L.Settings.cs        8
src/Localization/L.Stats.cs           6
src/Localization/L.Tray.cs            14
TOTAL NBSP (U+00A0) dans src/Localization/*.cs : 64  (sur 15 fichiers scannes)
```

`msix/Fiche Store.md` : **0** occurrence de U+00A0 (fichier entier scanne en bytes).

Verification ciblee demandee -- octet precedant le `:` de chaque ligne "Version 1.2.0..." de
la Fiche :

```
match=b'Version 1.2.0 :'  byte_before_colon=32 (0x20)  is_ascii_space=True
match=b'Version 1.2.0:'   byte_before_colon=48 (0x30)  is_ascii_space=False   (chiffre '0', convention EN)
```

L'octet precedant le `:` de la ligne FR ("Version 1.2.0 :") vaut bien **32** (espace ASCII
ordinaire), pas 0xA0. Meme verification sur `Publication Microsoft Store.md:5,7`
("Version cible : 1.2.0", "Package Store : 1.2.0.0") : octet avant chaque `:` = 0x20, 0 NBSP
dans le fichier entier.

Les deux conventions decrites dans la consigne sont donc bien **simultanement respectees et
verifiees** dans l'etat actuel du depot :

- `src/AZERTYGlobal.Tests/FrenchTypographyTests.cs:59-74`,
  `FrenchStrings_UseNoBreakSpaceBeforeDoublePunctuation` : enumere par reflexion toutes les
  proprietes/methodes statiques publiques de `L` retournant string, evalue en francais, et
  echoue si un ASCII space (0x20) precede `:` `;` `?` `!` `»`, ou si `«` est suivi d'un ASCII
  space. Ce test tourne dans la suite AZERTYGlobal.Tests (149/149 verts, § 3) -- il passe
  actuellement.
- `scripts/Verify-Release.ps1:145-148` cherche des motifs `-f` PowerShell avec espace ASCII
  litteral -- un NBSP dans `msix/Fiche Store.md` casserait cette porte. Confirme : 0 NBSP dans
  ce fichier, la porte passe (§ 2).

Aucune contamination croisee trouvee : le camp NBSP (Localization/) et le camp ASCII
(Fiche Store.md, Publication Microsoft Store.md) sont chacun purs a 100% dans les fichiers
mesures.

---

## 6. `msix/Fiche Store.md` et notes de version

### Ce que 1.2.0 ajoute cote utilisateur, absent de la Fiche Store (FR et EN)

`Changelog.md:48-54` (« Lancement automatique -- rattrapage hors accueil ») decrit une fonction
utilisateur neuve : une entree **« Lancer au demarrage de Windows »** au premier niveau du menu
de la zone de notification (cochee si la tache est enregistree), plus une relance unique
(toast/notification) apres deux jours d'usage sans demarrage automatique. Code : `AutoStart.cs`
via `AutoStartNudge`, cable dans `TrayApplication.cs`.

`msix/Fiche Store.md:79-84` (FR) et `:211-215` (EN), les seules sections « Nouveautes de cette
version » pour 1.2.0, ne mentionnent **que** le Defi du jour, le bouton « Copier mon resultat »,
le record personnel et la notation integree. **Aucune mention du demarrage automatique ou de sa
relance**, ni dans les puces de version, ni dans les descriptions longues FR/EN (relu
integralement, aucune occurrence de « demarrage » ou « startup »).

Confirmation croisee independante : `docs/audit-v1.2.0/delta-textes-affine.md:47-51` (autre
axe, diff des chaines UTF-16 du binaire 1.1.0.0 installe contre Localization/ a HEAD) classe
« Lancement au demarrage active », « Launch at startup enabled » et « L'application ne demarre
pas encore avec Windows... » parmi le texte **reellement neuf**, absent du binaire 1.1.0.0 deja
expedie. La fonction est donc bien neuve et bien absente de la fiche.

### Notes de version anglaises

Presentes : `msix/Fiche Store.md:209-251` porte une section « What's new (release notes) »
complete en anglais, y compris pour 1.1.0 (`:217-222`), coherent avec le Changelog qui dit que
la version 1.1.0 avait des notes FR et EN publiees (`Changelog.md:65`).

### Fraicheur du document

`msix/Fiche Store.md:354` -- pied de page : "Derniere mise a jour : 2026-06-29 (v1.0.0 --
publication Microsoft Store)". Le fichier contient pourtant des sections 1.1.0 et 1.2.0 ajoutees
apres cette date (par `666fb91` le 2026-08-16 et `32ce6d5`/`7ad768a` les 2026-08-16 et 17). La
ligne de fraicheur n'a pas suivi le contenu.

`msix/README.md:81` decrit encore la porte de version comme couvrant
`.agent/CONTEXT_APP_MICROSOFT_STORE.md` et `.agent/CONTEXT_AZERTY_GLOBAL.md` -- les deux
fichiers n'existent plus (§ 2). `msix/README.md:139` porte la meme date perimee que
`Fiche Store.md`.

---

## 7. Conformite de la sollicitation de notation -- ce que le depot documente lui-meme

Recherche exhaustive (avis, notation, review, rating, sollicit, politique, policy, 10.2, 11.x,
certification) sur les .md du depot et les commentaires .cs.

**Une seule politique Microsoft numerotee est citee dans tout le depot**, et elle ne concerne
pas la sollicitation d'avis :

- `Cahier des charges.md:74` -- « L'utilisateur grand public qui double-clique sur l'exe doit
  comprendre immediatement ce qui se passe et tenir le role d'**ecran de consentement** au
  regard de la politique Microsoft Store **10.2.8** (apps qui modifient le comportement
  systeme). »
- `msix/Fiche Store.md:303` -- meme politique 10.2.8, meme sujet (transparence du hook clavier
  via OnboardingWindow), redite dans les notes de certification.

**Aucun document du depot ne cite de politique Microsoft numerotee sur la frequence ou les
conditions de sollicitation d'avis.** Les garde-fous existants (`TrayApplication.cs:80-90`)
sont presentes comme des decisions produit internes, jamais adossees a une regle Microsoft
citee :

```
src/TrayApplication.cs:80-90
private const int ReviewPromptFirstActiveDays = 3;
private const int ReviewPromptSecondActiveDays = 10;
private const int ReviewPromptFirstMinDays = 3;
private const int ReviewPromptSecondMinGapDays = 7;
private const int ReviewPromptStaleDays = 3;
private const int ReviewPromptErrorCooldownHours = 48;
```

Plafond de 2 essais sur la vie de l'installation, silence de 48h apres une erreur journalisee,
1 sollicitation maximum par jour (`Changelog.md:32`) -- tout ceci est du reglage produit motive
par un taux de conversion mesure (« 671 acquisitions en trois mois, trois avis seulement »,
`src/StoreReview.cs:6`), pas une contrainte Store documentee dans ce depot.

**Conclusion pour ce point : il n'y a rien a comparer.** Le depot ne documente aucune regle
Microsoft specifique a la cadence de sollicitation d'avis dont le comportement v1.2.0 pourrait
s'ecarter. Le seul ecart possible serait vis-a-vis de la politique 10.2.8 citee -- mais
celle-ci porte sur le consentement a la modification du comportement systeme (le hook clavier),
pas sur les avis, et rien dans le diff v1.1.0->v1.2.0 ne touche a OnboardingWindow comme ecran
de consentement.

---

## 8. Scripts et schemas nouveaux -- ce qu'ils verifient, quand, et leur temoin

| Script/dossier | Commit(s) | Verifie | Tourne | Temoin trouve ? |
|---|---|---|---|---|
| `scripts/validate-layout.py` | `6fc9b21` | Schema JSON ferme + 5 compteurs recalcules + references croisees (dk_* declares/poses, unicite scancode/position) | A la main ; **bloquant en CI** (ci.yml:56-57, avant le build) | **Oui** -- 30 tests unitaires, voir ci-dessous |
| `scripts/check-layout-provenance.py` | `02d6935` | SHA256 des 3 JSON embarques == original sur raw.githubusercontent.com/AZERTYGlobal/website | En CI, job separe `provenance` (ci.yml:22-33), **non bloquant** pour le job build (ci.yml:17-21 : « Une panne reseau ne doit donc pas empecher une release de se construire ») | **Non** -- aucun test ne mutile une copie locale pour verifier que le script dit DERIVE |
| `scripts/list-identity-literals.py` | `efff943`, `06f997d` | Aucun litteral AZERTY Global/azerty.global/ID Store hors de ProductIdentity.cs | A la main ; **bloquant en CI** (ci.yml:47-48, avant meme l'installation de jsonschema) | **Non** -- pas de temoin automatise qui reintroduit un litteral ; le docstring documente un faux positif corrige (TrayApplication.cs:1219), preuve d'un reglage de bruit, pas d'une capacite de detection vraie positive |
| `scripts/witness-embedded-resources.py` | `697d1a3` | Que les 3 tests ResourceAlignmentTests (xUnit) echouent bien sur une mutation ciblee des JSON embarques, puis restaure | **A la main uniquement, jamais en CI** (docstring l.9 : « il ecrit dans l'arbre de travail et coute une reconstruction ») | C'est lui-meme le temoin de ResourceAlignmentTests -- voir verification ci-dessous |
| `schemas/azerty-layout.schema.json` | `6fc9b21`, `6bd8624` | Structure fermee (additionalProperties: false partout), formats stricts (scancode, position, finger enum, cle de table a 1 caractere, nom dk_*) | Consomme par validate-layout.py et par les deux suites scripts/tests/ | **Oui** -- 16 des 30 tests ci-dessous le ciblent directement |
| `scripts/tests/test_validate_layout.py` | `6fc9b21`, `54aab7e`, `7d19b2c` | Que validate-layout.py rejette bien chaque mutation qu'il pretend detecter (26 cas : schema, compteurs, references) | En CI (ci.yml:60, `python -m unittest discover -s scripts/tests -v`) | Auto-temoin (fichier de temoins lui-meme) |
| `scripts/tests/test_schema_parser_agreement.py` | `6bd8624` | Que tout ce que LayoutJsonParser.cs lit est declare dans le schema, et que tout ce qu'il exige est required -- lu depuis la source reelle du parseur (regex), pas une liste tenue a la main | En CI (meme commande) | **Oui** -- `test_le_parseur_lit_bien_quelque_chose` verifie que l'extracteur trouve encore les ancres connues, pour ne pas laisser un motif casse faire passer silencieusement tous les autres tests du fichier |
| `scripts/Sync-LayoutResources.ps1` | `a0b11f8`, `7d19b2c` | Copie website/ -> src/ (layout, index, lecons) si le hash differe ; -DryRun n'ecrit rien ; appelle validate-layout.py apres copie | A la main uniquement ; **absent de ci.yml** | **Non fonctionnel** -- voir ci-dessous |

### Executions reelles (toutes en lecture, aucune n'ecrit de facon permanente)

```
python scripts/list-identity-literals.py
### ProductIdentity.cs  (9)
     [...9 lignes, toutes dans ProductIdentity.cs...]
Litteraux hors ProductIdentity : 0

python scripts/validate-layout.py
Schema : conforme
Compteurs : conforme
References : conforme
AZERTY Global 2026.json est conforme au schema, a ses compteurs et a ses references.
(exit 0)

python -m unittest discover -s scripts/tests -v
[...30 tests, tous "ok"...]
Ran 30 tests in 0.259s
OK

python scripts/check-layout-provenance.py
IDENTIQUE  src/AZERTY Global 2026.json  5f79f9e04232393c  33494 o
IDENTIQUE  src/character-index.json  b2eba45490e0e81a  605678 o
IDENTIQUE  src/lessons.json  0002a340c2a1ed92  33060 o
Les 3 copies sont identiques a leur original canonique.
(exit 0)

powershell -File scripts\Sync-LayoutResources.ps1 -DryRun
 - INCHANGE AZERTY Global 2026.json  5f79f9e04232393c
 - INCHANGE character-index.json  b2eba45490e0e81a
 - INCHANGE lessons.json  0002a340c2a1ed92
Schema : conforme / Compteurs : conforme / References : conforme
Aucune ecriture: -DryRun.
```

Les trois hash INCHANGE de `Sync-LayoutResources.ps1 -DryRun` correspondent exactement aux
trois hash IDENTIQUE de `check-layout-provenance.py` -- coherence croisee confirmee entre les
deux scripts, sur les vraies donnees du depot.

`scripts/witness-embedded-resources.py` **n'a pas ete execute** : il ecrit dans l'arbre de
travail (meme en le restaurant ensuite), ce qui sort du mandat "depot en lecture seule". Verifie
statiquement a la place -- les trois chaines qu'il cible sont reelles, pas inventees :

```
src/AZERTY Global 2026.json:84
{"position": "E00", ..., "shift": "#", ...}
-> correspond a ResourceAlignmentTests.cs:19 : Assert.Equal("#", Key(layout, "E00").Shift)

src/character-index.json:25310
"totalCharacters": 1034
-> correspond a ResourceAlignmentTests.cs:46 : Assert.Equal(characters.Count(), totalCharacters)

src/character-index.json:1377
"unicodeNameFr": "CIRCONFLEXE",
-> correspond a ResourceAlignmentTests.cs:63 : Assert.Equal("CIRCONFLEXE", ...unicodeNameFr...)
```

Les trois cibles existent reellement dans les fichiers et correspondent chacune a l'assertion
xUnit qu'elles pretendent faire echouer. Je n'ai pas la preuve d'execution (le test vire
reellement au rouge) que le commit `697d1a3` revendique -- seulement la preuve que la cible
n'est pas une chaine fantome.

### `Sync-LayoutResources.ps1` -- gap documente par le depot lui-meme

`docs/keyboard-platform.md:224-227` : « La branche -SyncPublicRepo est residuelle : le clone
public qu'elle cherche n'existe pas, son second candidat est le depot lui-meme, si bien qu'elle
copie les fichiers sur eux-memes en annoncant un succes. » -- un bug reconnu et non corrige,
laisse en l'etat parce que le supprimer casserait un test.

Ce test, seul lien C# vers ce script, est un controle de **presence de texte**, pas un test
fonctionnel :

```
src/AZERTYGlobal.Tests/LessonCoreTests.cs:471-479
[Fact]
public void SyncScript_AllowsCreatingPublicLessonsResource()
{
    string script = File.ReadAllText(...Sync-LayoutResources.ps1...);
    Assert.Contains("[switch]$AllowCreate", script);
    Assert.Contains("src\lessons.json') -AllowCreate", script);
}
```

Il verifie que deux sous-chaines existent dans le fichier .ps1 -- jamais que le script
s'execute ni que -SyncPublicRepo fait ce qu'il pretend. `Sync-LayoutResources.ps1` n'a donc
**aucun temoin comportemental**, seulement une preuve que son code source contient certains
mots.

---

## Portes qui bloqueraient aujourd'hui

- `scripts/Verify-Release.ps1` -- echoue a l'item 17 (`msix/AZERTYGlobal.msixbundle`
  introuvable). Aucun .msixbundle n'existe dans ce depot pour 1.2.0.
- CI `ci.yml` job build -- s'arreterait a `dotnet publish` puis `Pack-MSIX.ps1` puis
  `Verify-Release.ps1` pour la meme raison (aucun run CI reel observable depuis ce depot local,
  mais la meme absence de bundle s'appliquerait).

## Portes qui passeraient mais ne prouvent rien

- **Les 15 verifications de version de Verify-Release.ps1 passent, mais aucune ne regarde le
  manifeste au-dela du numero de version** : le manifeste pourrait perdre runFullTrust ou
  gagner une capacite dangereuse sans faire echouer cette porte.
- **`ActivatorClsid_MatchesAppxManifestDeclaration` (ToastActivationTests.cs:28-35) passe et ne
  prouve rien** : il compare une chaine codee en dur dans le test a une chaine codee en dur dans
  ToastActivation.cs ; le manifeste, qui ne porte aucune declaration com:Class du tout, n'est
  jamais lu. Le commentaire du test l'admet explicitement.
- **`SyncScript_AllowsCreatingPublicLessonsResource` (LessonCoreTests.cs:471-479) passe et ne
  prouve rien** de fonctionnel : deux Assert.Contains sur le texte source d'un script
  PowerShell, jamais execute par le test.
- **Si un bundle MSIX etait produit aujourd'hui sans republier**, les verifications de hash
  (items 19-20 de Verify-Release.ps1) pourraient passer tout en embarquant un executable
  obsolete : `src/bin/Release/.../win-x64/publish/AZERTY Global.exe` (16/08 23:49) et son
  equivalent arm64 (16/08 23:47) portent FileVersion 1.1.2.0 / ProductVersion 1.1.2 dans leur
  ressource Win32 -- mesure via `Get-Item ... .VersionInfo`, confirme par 6 occurrences
  UTF-16LE de "1.1.2" dans le binaire x64. Ces artefacts datent d'avant le commit `27541e1`
  (2026-08-17 11:36, qui a justement corrige Program.cs/AssemblyInfo.cs de 1.1.2 a 1.2.0) et
  d'avant 16 commits ulterieurs de la plage auditee, dont tout le refactor ProductIdentity et
  toute l'infrastructure schema/scanner. Le controle de hash (Verify-Release.ps1:186-188) ne
  prouve que « le publish == le bundle » -- jamais « le publish == HEAD ». Republier avant tout
  packaging est une etape manuelle non forcee par aucune porte automatisee de ce depot.
- **`list-identity-literals.py` et `check-layout-provenance.py` tournent proprement (exit 0) et
  bloquent la CI, mais aucun des deux n'a de temoin qui prouve qu'ils savent dire non** -- voir
  § 8.

## Etapes de smoke test imposees

Relevees dans les documents du depot (msix/README.md:83-98, commentaires code) :

1. `Add-AppxPackage -Path ".\msix\AZERTYGlobal-<version>.msixbundle"`, verifier lancement,
   icone tray, hooks clavier, recherche de caracteres.
2. Compatibilite jeux : auto-desactivation sur process anti-cheat, combo native sur framework
   gaming detecte, Alt+code en RDP/VPN.
3. App fonctionnelle sans droits admin ; pas de blocage Smart App Control.
4. `appcert.exe test` (WACK) sur le bundle avant soumission.
5. Specifique 1.2.0, cite dans Program.cs:47-49 : clic sur la sollicitation d'avis a J+7 (ou
   selon les nouveaux seuils d'usage) -- verifier que le clic sur le toast active l'instance
   vivante et n'ouvre pas une seconde fenetre "deja en cours". C'est exactement le chemin que
   § 4 documente comme potentiellement casse par l'absence de declaration manifeste.
6. Nouveau smoke test 1.2.0 non documente mais necessaire au vu du diff : verifier que
   "Lancer au demarrage de Windows" du menu tray reflete l'etat reel de la tache planifiee, et
   que la relance unique se declenche apres deux jours d'usage sans autostart (AutoStartNudge).

## Ce que je n'ai pas pu verifier

- **L'execution reelle de scripts/witness-embedded-resources.py** -- ecrit dans l'arbre de
  travail (meme transitoirement) ; hors mandat lecture seule. Verifie statiquement a la place
  (chaines cibles reelles, § 8) mais pas le passage reel au rouge qu'il revendique.
- **Les items 18-20 de Verify-Release.ps1** (bundle versionne, hash bundle-stable vs
  bundle-versionne, hash publish vs bundle) -- jamais atteints, aucun bundle n'existe.
- **Le comportement reel de Windows face a l'absence de declaration com:Class/
  ToastActivatorCLSID** -- je peux montrer que le depot affirme en avoir besoin et que le
  manifeste ne le porte pas ; je ne peux pas, sans packager ni publier (interdit), observer si
  Windows relance malgre tout l'exe ou si CoRegisterClassObject suffit seul.
- **Le job provenance de ci.yml en conditions CI reelles** -- j'ai rejoue
  check-layout-provenance.py en local avec succes (acces reseau disponible dans cet
  environnement), mais je n'ai pas de run GitHub Actions a inspecter.
- **Le resultat WACK pour 1.2.0** -- aucun rapport wack-report-v1.2.0.xml dans le depot ; le
  dernier cite est celui de v1.0.0 (msix/Fiche Store.md:316).
- **Les captures d'ecran de la fiche Store** -- non reverifiees (hors perimetre de mon axe,
  msix/Fiche Store.md:339-350 documente deja des ratios non standards non resolus).
