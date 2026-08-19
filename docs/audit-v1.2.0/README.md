# Audit v1.1.0 -> v1.2.0, avant smoke test

Audit du 2026-08-18, sur `HEAD` = `697d1a3`. Quatre axes : comportement visible,
migration des donnees, code et regressions, portes de release. Chaque axe a son
rapport brut dans ce dossier ; ce fichier consolide et arbitre.

Aucun `.cs` de production n'a ete modifie. Aucun MSIX n'a ete produit. Aucune publication.

**Passe de verification du 2026-08-18**, session distincte de celle qui appliquera les
correctifs. Les 6 findings qui n'etaient que cites ont ete reverifies contre le code et
contre le vrai etat anterieur. Resultat : **R6, R7, R8 et R9 confirmes**, **R4 et R5
declasses** en limitations argumentees, **3 citations du rapport brut corrigees**, et
**8 tests ajoutes** qui rendent R6, R7 et R8 mesurables. 266 tests verts, build Release
0 avertissement 0 erreur.

| Fichier | Contenu |
|---|---|
| `raw-comportement.md` | Comportement visible, 18 affirmations du Changelog tracees |
| `raw-migration.md` | Schemas `config.json`, `usage-stats.json`, `lessons-progress.json` |
| `raw-code.md` | Regressions, machine a etats de la sollicitation d'avis |
| `raw-release.md` | Versions, `Verify-Release.ps1`, tests, garde-fous |
| `delta-textes-affine.md` | Delta du texte visible, mesure contre le binaire expedie |
| `witness-baseline.py`, `delta-typographie.py` | Scripts rejouables des mesures ci-dessous |
| `witness-lot-b.py`, `witness-lot-d.py` | Temoins de mutation des lots B et D du plan parc (2026-08-19) |
| `AppxManifest.INSTALLED-1.1.0.0.xml` | Manifeste reel de la 1.1.0 servie par le Store |

## 1. Ce que cet audit peut affirmer, et pourquoi la base a du etre reconstruite

**Il n'existe aucune revision git egale a la v1.1.0.** Les tags s'arretent a
`v1.0.0`. Le commit `452aab0` (2026-08-15, « reconcile v1.2 and extract typing
core ») melange dans un seul commit le rapatriement du code deja expedie en 1.1.0,
des fonctions 1.2.0, et l'extraction du `TypingEngine` : 74 fichiers, 13 694
insertions.

La version que le code s'attribue le prouve :

```
ad049fc  (452aab0^)  src/Program.cs -> Version = "1.0.0"
452aab0              src/Program.cs -> Version = "1.1.2"
697d1a3  (HEAD)      src/Program.cs -> Version = "1.2.0"
```

`452aab0^` s'annonce donc en **1.0.0**. La plage `452aab0^..HEAD` couvre
1.0.0 -> 1.2.0, deux releases, et non la seule 1.2.0. Consequences chiffrees :

| Plage | Commits | Fichiers | Lignes | Defaut |
|---|---|---|---|---|
| `452aab0^..HEAD` | 23 | 115 | +17 840 / -1 946 | sur-declare : compte la 1.1.0 entiere comme neuve |
| `452aab0..HEAD` | 22 | 86 | +5 168 / -2 110 | sous-declare : rate les fonctions 1.2.0 posees dans `452aab0` |

Corroboration structurelle : a `452aab0^`, `src/` ne contient ni `Localization/`,
ni `StoreReview.cs`, ni `ProductIdentity.cs`, ni `UsageStatsWindow.cs` -- donc ni
interface bilingue, ni sollicitation d'avis, ni fenetre de statistiques, qui sont
trois fonctions **de la v1.1.0 publiee**.

**La reference retenue est donc le binaire expedie**, seul artefact qui soit
reellement la 1.1.0 :
`C:\Program Files\WindowsApps\AZERTYGlobal.AZERTYGlobal_1.1.0.0_x64__w9kghr08zmhbg`,
`FileVersion 1.1.0.0`, SHA-256
`3B08F41A8DCF1DDCB5FA56DE4F8D43F576D37E980B4C8397F7785CA1682ABB85`.

### Delta du texte visible, mesure

Denominateur : 382 litteraux distincts de `src/Localization/` a HEAD, cherches
dans les 2 638 chaines UTF-16 du binaire 1.1.0.0.

| | Chaines |
|---|---|
| Deja dans le binaire 1.1.0.0, hors delta | 259 |
| Absentes du binaire | 123 |
| dont meme phrase, typographie seule | 19 |
| **dont texte reellement neuf ou reformule** | **104** |

Extracteur et normalisateur valides par temoin dans les deux sens : une chaine
mutee doit echouer en comparaison exacte et passer en comparaison normalisee.
Reserve : environ 6 des 104 sont des fragments de concatenation captes par le
regex (`" -- TOUCHE MORTE "`, `", count > 1 ? "`), pas des phrases affichees.

## 2. Bloquants avant tout packaging

### B1. Les executables AOT publies sur disque sont en 1.1.2.0

`Pack-MSIX.ps1` consommerait des binaires anterieurs a la correction de version :

| Artefact | FileVersion | Ecrit le |
|---|---|---|
| `src/bin/Release/net8.0-windows10.0.17763.0/win-x64/publish/AZERTY Global.exe` | **1.1.2.0** | 2026-08-16 23:49 |
| `src/bin/Release/net8.0-windows10.0.17763.0/win-arm64/publish/AZERTY Global.exe` | **1.1.2.0** | 2026-08-16 23:47 |
| `msix/AppxManifest.xml` declare | 1.2.0.0 | -- |
| `src/Properties/AssemblyInfo.cs:8` declare | 1.2.0.0 | -- |

Les deux publications AOT precedent le commit `27541e1` (2026-08-17 11:36) qui a
corrige la version, et les 16 commits suivants. Un MSIX assemble en l'etat
livrerait des binaires 1.1.2 dans un package annonce 1.2.0.0.

Precision par rapport a `raw-release.md` : la sortie *framework-dependent*
`net8.0-windows10.0.17763.0/AZERTY Global.exe` porte bien 1.2.0.0, mais elle date
du 2026-08-18 09:05 et vient du `dotnet build` de l'audit lui-meme. Ce ne sont pas
les artefacts que le packaging consomme. Republier les deux cibles AOT est
obligatoire, prerequis `vswhere` inclus (voir `msix/README.md`).

### B2. L'activateur COM de toast reclame une declaration manifeste absente

`src/ToastActivation.cs` l'exige deux fois : ligne 12, « Le CLSID doit rester
identique a celui du manifest (com:Class + ToastActivatorCLSID) », et ligne 53,
« CLSID declare dans msix/AppxManifest.xml (com:Class + ToastActivatorCLSID) »,
pour `126A58B4-3200-43A6-9018-612C108F4A94`.

`msix/AppxManifest.xml` ne contient **aucune** extension `com:` -- sa seule
`desktop:Extension` est `windows.startupTask` (ligne 49). L'activateur est
pourtant enregistre en production packagee (`src/TrayApplication.cs:227`) et
commande les deux chemins de toast (`:1064`, `:1684`).

Le test qui devrait voir ce trou est aveugle par construction :
`ActivatorClsid_MatchesAppxManifestDeclaration`
(`ToastActivationTests.cs:28-35`) compare une chaine codee en dur a une autre
chaine codee en dur, sans jamais lire le manifeste.

L'effet a l'execution n'est pas etabli : le repli balloon existe, et
`CoRegisterClassObject` peut reussir sans declaration. C'est une contradiction
interne au depot, a lever par le smoke test (etape 9), pas une panne prouvee.

## 3. Regressions confirmees, par gravite decroissante

R1 et R2 ont ete reverifiees ligne par ligne par la session principale. Les
suivantes sont reprises de `raw-code.md` avec leur citation.

### R1. Deux sollicitations d'avis le meme jour, les deux essais de la vie brules

`src/TrayApplication.cs:151` declare `private DateOnly? _reviewPromptShownDate;`
-- champ **d'instance**, donc nul a chaque demarrage du processus. C'est le seul
que teste la garde « une par jour » du chemin partage,
`src/TrayApplication.cs:1791` : `if (_reviewPromptShownDate == today) return;`.

La date persistee existe : `ConfigManager.ReviewPromptLastShown`
(`src/ConfigManager.cs:217`), ecrite par `RecordReviewPromptShown()`. Elle n'est
lue qu'a `src/TrayApplication.cs:1665`, et seulement pour le plancher de 7 jours
du second essai.

Partager un resultat, redemarrer l'application, repartager : deuxieme
sollicitation le meme jour. « Une seule par jour » n'est en realite qu'« une seule
par session de processus ». Correctif : une ligne.

### R2. La relance d'autostart se reemet apres un refus explicite

`AutoStartNudge.MarkPromptShown()` n'est appele que par les deux chemins du menu
tray (`src/TrayApplication.cs:1062`, `:1109`). Les deux autres chemins
d'ecriture appellent `AutoStart.Set` sans marquer :
`src/SettingsWindow.cs:754` et `src/OnboardingWindow.cs:681`.

Correction apportee a `raw-code.md`, qui elargissait la porte d'entree :
`AutoStartNudge.ShouldPrompt` teste `if (s.AlreadyRegistered) return false;`
(`src/AutoStartNudge.cs:41`). Decocher une case deja decochee est donc un
non-evenement. Le scenario reel exige d'avoir **active** le lancement automatique,
puis de l'avoir **desactive** par Parametres ou par la case de l'accueil :
`AlreadyRegistered` redevient faux, `NudgeDone` est reste faux, et deux jours
d'usage plus tard l'application propose d'activer ce que l'utilisateur venait de
couper.

Le Changelog ecrit « Un choix fait a la main **dans le menu** eteint la relance,
dans un sens comme dans l'autre ». Pris au mot, c'est exact : le menu marque bien.
Le defaut est que Parametres et Accueil ne sont pas couverts.

### R3. Le chemin partage ignore tous les seuils d'usage

`src/TrayApplication.cs:1777-1793`, `MaybeShowReviewAfterShare` ne teste que
`IsPackaged`, `ReviewPromptClicked`, `ReviewPromptCount >= 2`, le silence de 48 h
(inerte, voir R4) et la date en memoire (voir R1). **Aucun `activeDays`, aucun
plancher depuis la premiere frappe remappee.**

Un utilisateur du jour 1 qui termine le Defi du jour et clique « Copier mon
resultat » recoit la boite de notation Store immediatement. Les seuils 3 jours /
10 jours que le Changelog presente comme la refonte centrale ne gouvernent que le
chemin par notification, et le Changelog ne dit nulle part qu'ils ne s'appliquent
pas au partage.

### R4. Le silence de 48 h apres erreur est inerte au demarrage — DECLASSE

**Revu le 2026-08-18 : ce n'est pas un oubli, c'est un compromis argumente.**
`src/ConfigManager.cs:255-267` expose deux raisons de ne pas persister, que le
premier passage n'avait pas citees :

- la date d'ecriture d'`error.log` ne conviendrait pas, « puisque le meme fichier
  recoit les evenements de compatibilite jeux, qui ne sont pas des erreurs et
  bloqueraient un joueur regulier en permanence » ;
- persister est exclu parce que « `Save` journalise ses propres echecs, une
  ecriture de config depuis le chemin d'erreur se rappellerait elle-meme ».

La consequence observable tient : au demarrage le champ est nul, donc la garde ne
s'applique pas a la sollicitation la plus probable. Mais l'argument
d'impossibilite est surdimensionne — persister la seule date sur un chemin qui
n'appelle pas `Log` leverait la recursion. **Verdict : limitation connue et
documentee, argument perfectible, consequence reelle. Pas une regression.**

### R5. L'essai est consomme avant toute preuve d'affichage — DECLASSE

**Revu le 2026-08-18 : le repli existe, le premier passage ne l'avait pas lu.**
`OnCompleted` ouvre le lien profond dans les deux cas d'echec :

```
src/StoreReview.cs:79-82   if (status != AsyncStatus.Completed) { ShellExecuteW(fallbackUrl); return; }
src/StoreReview.cs:86-90   if (result.Status is NetworkError or Error) { Log(...); ShellExecuteW(fallbackUrl); }
```

Un essai consomme sans boite affichee n'est donc pas perdu : l'utilisateur
atterrit sur le volet d'avis du Store. Et `src/StoreReview.cs:32-35` explique
pourquoi l'operation n'est jamais attendue — la boite a besoin de la boucle de
messages du thread appelant, un `GetAwaiter().GetResult()` la bloquerait
definitivement.

Reste vrai : le commentaire de `TrayApplication.cs` « l'API repond ici tout de
suite » est inexact, `TryShow` ne signifie que « operation lancee ». Et le soupcon
du HWND masque subsiste, non couvert par le repli — si la boite echoue a
s'afficher tout en rapportant `Completed`, rien ne se declenche.
**Verdict : choix delibere et documente, plus une question d'execution (etape 9).**

### R6. Deux `SendInput` fusionnes en un sur le chemin de frappe — CONFIRME

**Revu et prouve le 2026-08-18, avec le vrai etat anterieur sous les yeux.** Le
rapport brut situait le defaut dans `CompositionEngine.cs`, qui est une fonction
**pure** ne faisant aucun `SendInput` : le site reel est l'appelant.

```
ad049fc (1.0.0), src/KeyMapper.cs:626-629    EmitText(isolatedChar);  puis  EmitText(output);   -> 2 lots
HEAD, TypingEngine.Core/CompositionEngine.cs:31    string.Concat(GetIsolated(), output)
HEAD, TypingEngine.Windows/KeyMapper.cs:601-602    if (result.Text.Length > 0) EmitText(result.Text);   -> 1 lot
```

Le mock n'etait **pas** aveugle : `src/TestSupport/MockWin32Api.cs:19` enregistre
deja `List<Win32.INPUT[]>`, un element par lot. C'est `AllInputs` (`:22`), simple
aplatisseur de confort, que toutes les assertions existantes utilisaient. Le
rapport brut citait en outre un chemin qui n'existe pas
(`AZERTYGlobal.Tests/MockWin32Api.cs`).

Desormais couvert : `src/AZERTYGlobal.Tests/DeadKeyEmissionBatchTests.cs` asserte
sur `SendInputCalls` directement — `dk_acute` puis `.`, absent des 70 entrees de
la table, doit rendre **un seul** lot valant l'isole suivi du point. Sur le code
de 1.0.0 le meme test verrait deux lots et echouerait.

### R7. Touche morte orpheline : plus de repli sur le pass-through — CONFIRME

**Revu le 2026-08-18.** Correction de lecture au passage : le `true` de
`CompositionResult` n'est pas un « handled » mais le champ `StateChanged` —
`CompositionResult(string Text, bool StateChanged)`, `CompositionEngine.cs:54`.

```
ad049fc (1.0.0), src/KeyMapper.cs:615-633    if (dk != null) { ... return true; }   -- dk nul : on TRAVERSE
ad049fc (1.0.0), src/KeyMapper.cs:637        if (CanPassThrough(...)) { ...; return false; }
HEAD, TypingEngine.Windows/KeyMapper.cs:598-606    toute la branche return true, CanPassThrough (:610) jamais atteint
```

Le repli est donc bien perdu, et `ActivateDeadKey` accepte un nom absent de la
table sans aucune validation (`CompositionEngine.cs:37-40`). Atteignable seulement
par une disposition malformee, que ni `LayoutJsonParser.cs:54-71` ni
`schemas/azerty-layout.schema.json:74` ne rejettent.

Desormais couvert :
`src/TypingEngine.Core.Tests/CompositionEngineOrphanDeadKeyTests.cs` construit le
layout malforme que le layout embarque ne permet pas, et fixe les trois etats de
ce chemin plus un temoin sain.

### R8. Ordre `EmitText` / `StateChanged` inverse — CONFIRME

**Revu le 2026-08-18, les deux etats cites.**

```
ad049fc (1.0.0), src/KeyMapper.cs:612-620    _activeDeadKey = null; StateChanged?.Invoke();  PUIS  EmitText(transformed);
HEAD, TypingEngine.Windows/KeyMapper.cs:601-604    EmitText(result.Text);  PUIS  StateChanged?.Invoke();
```

Tout abonne qui lit l'etat du clavier dans ce callback observe donc un instant
different selon la version. Desormais couvert par
`DeadKeyEmissionBatchTests.ProcessKey_DeadKeyResolution_NotifieApresAvoirEmis`,
qui compte les lots deja emis au moment de l'evenement : `[0, 1]` aujourd'hui,
`[0, 0]` sur le code de 1.0.0.

### R9. Melange UTC / local sur le plancher de 7 jours — CONFIRME

**Revu le 2026-08-18, avec l'effet chiffre.**

```
src/ConfigManager.cs:246-247   SetString("reviewPromptLastShown", DateTime.UtcNow.ToString("yyyy-MM-dd", ...))
src/TrayApplication.cs:1651    var today = DateOnly.FromDateTime(DateTime.Now);
src/TrayApplication.cs:1667    today.DayNumber - lastShown.Value.DayNumber < ReviewPromptSecondMinGapDays
```

En France l'ete (UTC+2), une sollicitation affichee entre 00 h 00 et 02 h 00
locales ecrit la date de la veille. Le plancher de 7 jours se calcule alors depuis
J-1 : **le second essai devient disponible un jour trop tot, 6 jours au lieu de
7.** `UsageStats` travaille en heure locale (`UsageStats.cs:108` et `:297`), donc
l'ecriture UTC de `reviewPromptLastShown` est la seule discordante.

## 4. Ecarts entre le Changelog et le code

Sur les 18 affirmations tracees : **16 confirmees, 2 partielles, 0 introuvable**.
Les deux partielles sont R1/R4 (non persistes) et le chiffre de « 31 titres » via
`ProductIdentity`, repris du message de commit sans recomptage.

Trois affirmations a corriger dans le Changelog :

1. **`ProductIdentity` ne couvre pas le `TaskId` de demarrage.** Le Changelog
   liste « le prefixe du `TaskId` de demarrage » parmi les 78 sites unifies.
   `src/ProductIdentity.cs` n'a aucun membre `TaskId` ; `src/AutoStart.cs:238`
   garde `private const string StartupTaskId = "AZERTYGlobalStartup";`. C'est un
   defaut de documentation, et le litteral est le choix le plus sur des deux --
   il est identique au manifeste 1.1.0.0 installe, donc la tache enregistree par
   la v1.1 ne devient pas orpheline a la mise a jour. **Risque le plus grave de
   la release, ecarte.**
   Soupcon associe : cet identifiant est aussi hors du motif du scanner
   (`scripts/list-identity-literals.py:33-42`) -- le seul dont la derive casse le
   demarrage automatique est le seul non tenu.
2. **La population jamais sollicitee en v1.1 est plus etroite qu'annonce.** Le
   Changelog dit « un utilisateur qui revoyait l'accueil a chaque demarrage
   n'etait jamais sollicite ». La condition reelle a `452aab0` est
   `shouldShowOnboarding = ShowOnboardingAtStartup && LearningMaxStepCompleted < 3` :
   il faut aussi n'avoir jamais depasse l'etape 3 du module d'apprentissage.
3. **Le compte de tests est perime.** Voir section 7.

Affirmations confirmees qui meritaient de l'etre : le tirage 50/50 de la v1.1
etait bien reel (`bool toStore = ConfigManager.IsPackaged && Random.Shared.Next(2) == 0;`
a `452aab0`) ; le bouton de copie de « Mes statistiques » existait bien en v1.1
(« Copier mes statistiques » est dans le binaire 1.1.0.0) ; la non-regression
ligne par ligne du refactor `ProductIdentity` tient -- mutex
`AZERTYGlobalSingleInstance` identique, 12 classes fenetre appariees
register/unregister, 20 URL identiques en chemin et en casse, NBSP compris.

## 5. Nouveautes v1.2.0 absentes du Changelog

Prouvees sur le binaire expedie, donc reellement invisibles pour un utilisateur
de la 1.1.0.

1. **Detection d'acces distant.** Zero occurrence de `distant`, `remote` ou
   `Remote` dans le binaire 1.1.0.0. Le concept de suspension existait
   (« Suspendu pour compatibilite » / « Suspended for compatibility » y sont),
   mais pas le motif de refus pour application protegee ou de connexion a
   distance.
2. **Selecteur manuel d'application et liste des suspensions.** « Choisir une
   application », « Applications (*.exe) », « Apps suspendues » : absents du
   binaire 1.1.0.0.
3. **Mention de confidentialite de la detection.** « La detection reste
   entierement locale. Aucun nom d'application n'est transmis. » : absente. Elle
   engage le depot sur un point de conformite, elle merite une ligne au
   Changelog.
4. **Activateur COM de toast** (`src/ToastActivation.cs`, `src/Program.cs:30-40`).
   Le binaire 1.1.0.0 ne contient ni `ToastGeneric` ni `ActivatorCLSID` ni
   `StoreContext`, mais contient `ms-windows-store://review/?ProductId=9N4BTS43SSSZ`
   -- profil d'une v1.1 en balloon plus lien profond. Limite de la preuve : en
   Native AOT les noms de types ne sont pas garantis presents comme chaines, donc
   l'absence de `ToastActivation` seule ne conclurait rien ; ce sont les
   litteraux XML de toast et `ActivatorCLSID` qui portent le verdict.
5. **Trois messages de suspension distincts** (`src/TrayApplication.cs:1863-1886`)
   -- acces distant, override utilisateur, foreground inconnu.
6. **Boutons de reinitialisation de fenetre** (« Fenetre Lecons reinitialisee »,
   « Fenetre clavier virtuel reinitialisee »).

Ecart d'ergonomie a trancher, pas un defaut : `src/OnboardingWindow.cs:756-764`
contre `:669-685` -- la case « Defi du jour » de l'accueil s'applique au clic,
alors que ses deux voisines (lancement automatique, ne plus afficher) n'appliquent
qu'a la fermeture et seulement si l'etape 3 est atteinte. Trois cases cote a cote,
deux regimes de persistance.

## 6. Migration des donnees v1.1 -> v1.2

Aucun numero de version de schema dans `config.json` -- `lessons-progress.json`
en a un. Dix cles ajoutees : `reviewPromptCount`, `reviewPromptLastShown`,
`reviewPromptClicked`, `autoStartNudgeDone`, `trainingEnabled`,
`trainingIgnoredCount`, `trainingSequenceIndex`, `trainingLastSessionDate`,
`trainingLastReminderDate`, `challengeAnnounceDone`.

La migration `reviewPromptDone` -> `reviewPromptCount` est deduite **a la
lecture**, sans reecriture :

```csharp
var stored = GetUInt("reviewPromptCount");
if (stored > 0) return (int)stored;
return GetBool("reviewPromptDone") ? 1 : 0;
```

Elle est donc idempotente ; `reviewPromptDone` est laisse en place et reecrit a
`true` a chaque essai, si bien qu'un retour arriere vers la v1.1 se comporte
correctement. Un `reviewPromptDone` a `false` explicite et une absence sont
traites identiquement (`GetBool` ne reconnait que le litteral `true`).

`LessonProgressStore.cs` : zero ligne de diff sur toute la plage, schema
inchange depuis la v1.0. `usage-stats.json` : quatre champs ajoutes
(`searchOpenCount`, `virtualKeyboardOpenCount`, `challengesCompletedCount`,
`lastSpecialCharDate`), degradation propre a `0` / `null`.

### Le poste de test est un cas de migration, pas une installation neuve

Etat reel mesure sur ce poste, `config.json` du 2026-08-05 et
`usage-stats.json` du 2026-08-18 :

| Garde-fou | Etat mesure | Verdict |
|---|---|---|
| `shouldShowOnboarding` | `showOnboardingAtStartup: true` mais `learningMaxStepCompleted: 6` | faux -> branche `else`, sollicitation evaluee |
| `already >= 2 \|\| ReviewPromptClicked` (`:1640`) | `reviewPromptDone: true` -> count 1 ; clicked absent | passe |
| `NotificationsEnabled` | absent, getter `GetBoolDefaultTrue` (`ConfigManager.cs:687`) -> true | passe |
| `activeDays >= 10` (essai 2) | `activeDaysCount: 19` | passe |
| plancher de 7 jours | `reviewPromptLastShown` absent -> `null` -> test saute | passe |
| `today - lastActive <= 3` | `lastActiveDate: 2026-08-18` | passe |
| silence de 48 h | `_lastErrorUtc` non persiste, nul au demarrage | passe |

**Le premier demarrage de la v1.2.0 sur ce poste consomme l'essai 2, le dernier
de la vie de l'installation.** Sauvegarder `config.json` avant le smoke test est
donc un prerequis, sinon aucun chemin de sollicitation n'est retestable.

Second point de methode : `%LOCALAPPDATA%\AZERTY Global` **n'existe pas** sur ce
poste. La configuration vit dans la redirection packagee,
`…\Packages\AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg\LocalCache\Local\AZERTY Global\`.
Un build local non package demarre donc sur un etat vierge et ne traverse jamais
le code de migration.

## 7. Portes de release

`Verify-Release.ps1`, execute : les 15 verifications de version passent, puis
echec net a l'item 17, `Fichier requis introuvable: msix\AZERTYGlobal.msixbundle`.
Les comparaisons de hash publish <-> bundle (items 18 a 20) ne sont jamais
atteintes. Le script ne verifie ni les capacites du manifeste, ni les comptes de
tests, ni la typographie, ni le contenu de la Fiche Store au-dela du numero de
version. Il ne verifie pas non plus la `FileVersion` des executables publies,
c'est-a-dire exactement B1.

**Comptes de tests : 258 reels contre 250 annonces avant cet audit, 266 apres.**
Mesure par `dotnet test` sur les trois suites : 149 applicatifs + 95 moteur
Windows + **14** moteur portable, contre 6 annonces. Deux mesures independantes
concordantes (`raw-code.md` et `raw-release.md`). Cause : le commit `6bd8624`
(2026-08-17 14:44) a ajoute 8 tests a `LayoutJsonParserTests.cs` apres que le
Changelog a fige le chiffre au commit `7ad768a` (11:36). Les trois sous-comptes
annonces sont exacts. Build Release : 0 avertissement, 0 erreur, confirme.

La passe de verification du 2026-08-18 ajoute **8 tests** — 4 dans
`AZERTYGlobal.Tests` (R6, R8, et la consommation systematique de la touche en
composition), 4 dans `TypingEngine.Core.Tests` (R7). Nouveau total mesure :
**153 + 95 + 18 = 266 verts**, build Release toujours 0/0. Le Changelog devra
porter ce chiffre, pas 250.

**Versions dans les sources : aucune divergence.** `csproj:10`, `Program.cs:10`,
`AssemblyInfo.cs:8,9,12`, `AppxManifest.xml:12`, `Changelog.md:3`,
`Fiche Store.md:79,211` (FR et EN), `Publication Microsoft Store.md:5,7`.

**Manifeste : le seul ecart entre la 1.1.0.0 installee et HEAD est la ligne
`Version`.** Identiques : `Identity Name`, `Publisher`, `Application Id`,
`MinVersion 10.0.17763.0`, `MaxVersionTested 10.0.26100.0`,
`TaskId AZERTYGlobalStartup`, `Enabled="false"`, et la seule capacite
`runFullTrust`. Aucune capacite ajoutee pour la boite de notation integree, ce qui
est correct -- `StoreContext.RequestRateAndReviewAppAsync` n'en exige pas.

`mp:PhoneIdentity` figure dans le manifeste installe et pas dans le depot, ou
`git log -S` sur toutes les branches montre qu'il n'a **jamais** existe. Ses deux
GUID sont des valeurs Partner Center : le Store les injecte. Ce n'est pas une
regression, et tout diff installe contre depot montrera toujours cet ecart.

### Garde-fous sans temoin

Un garde-fou dont on n'a jamais vu un echec ne prouve rien.

- `ActivatorClsid_MatchesAppxManifestDeclaration` (`ToastActivationTests.cs:28-35`)
  -- deux chaines codees en dur comparees entre elles, le manifeste n'est jamais
  lu. C'est ce test qui aurait du voir B2.
- `SyncScript_AllowsCreatingPublicLessonsResource` (`LessonCoreTests.cs:471-479`)
  -- deux `Assert.Contains` sur le texte source du `.ps1`, jamais execute.
- `scripts/list-identity-literals.py` et `scripts/check-layout-provenance.py` --
  bloquants en CI, exit 0 en l'etat, aucun test ne reintroduit une regression pour
  prouver qu'ils la detecteraient.
- `scripts/Sync-LayoutResources.ps1` -- `-SyncPublicRepo` copie un fichier sur
  lui-meme en annoncant un succes, bug reconnu par
  `docs/keyboard-platform.md:224-227`, non corrige.
- Le `SKIP_DIRS` du scanner d'identite (`list-identity-literals.py:52-56`) saute
  `TypingEngine.Core` et `TypingEngine.Windows`, devenus du code de production
  depuis `452aab0`.

## 8. Smoke test qui decoule de cet audit

Ordre impose par les dependances. Les etapes 1 et 2 sont des prerequis, pas des
tests.

| # | Etape | Resultat attendu |
|---|---|---|
| 1 | Sauvegarder `config.json` et `usage-stats.json` du dossier package | copie hors du dossier avant tout lancement |
| 2 | Republier les deux cibles AOT (`vswhere` dans le PATH), puis relancer `Verify-Release.ps1` | `FileVersion 1.2.0.0` sur les deux `publish/`, script au vert jusqu'au bundle |
| 3 | Build local non package, etat vierge : verifier les 104 chaines neuves du delta | textes FR et EN corrects, aucune cle manquante affichee brute |
| 4 | Non package, jour 1 : terminer le Defi du jour, cliquer « Copier mon resultat » | R3 : la boite de notation apparait-elle des le jour 1 ? |
| 5 | Non package : partager, redemarrer l'app, repartager le meme jour | R1 : deuxieme sollicitation le meme jour ? |
| 6 | Non package : activer le lancement automatique, le desactiver depuis Parametres, simuler deux jours d'usage | R2 : la relance revient-elle ? |
| 7 | Non package : entree « Defi du jour » du menu tray sur installation neuve | toujours visible, meme avec `trainingEnabled` a `false` |
| 8 | Non package : les trois cases de l'accueil, fermeture a l'etape 1 puis a l'etape 3 | la case « Defi du jour » s'applique au clic, les deux autres a la fermeture |
| 9 | **MSIX installe par-dessus la 1.1.0** : premier demarrage | essai 2 attendu ; verifier si le toast passe ou si le repli balloon prend (B2) |
| 10 | MSIX : cliquer la notification de sollicitation | un seul processus, pas de seconde instance ; la boite integree s'ouvre sans quitter l'app |
| 11 | MSIX : verifier que le lancement automatique de la v1.1 survit a la mise a jour | tache toujours enregistree, `State: 2` |
| 12 | MSIX : touches mortes, `Verr. Maj.` + lettre identique, `K` sur YouTube | R6, R7, R8 : composition et pass-through inchanges |
| 13 | MSIX : selecteur manuel d'application, liste des suspensions, message d'acces distant | fonctions non documentees, a valider avant de les documenter |

## 9. Limites de cet audit

- **Aucune execution de l'application.** Tout provient de la lecture du code, de
  git, des fichiers de configuration reels et des chaines du binaire expedie. Les
  13 etapes ci-dessus sont precisement ce que la lecture ne peut pas trancher.
- **La v1.1.0 n'est connue que par son binaire x64.** L'ARM64 expedie n'a pas ete
  inspecte ; les notes de version publiees sur le Store n'ont pas ete relues.
- **L'equivalence comportementale de l'extraction `TypingEngine.Windows`**
  (`1b68caa`) n'a pas ete relue ligne a ligne. R6 a R8 viennent de
  `TypingEngine.Core`.
- **Le pass-through Win + touche et l'exclusion de Win dans `IsToggleShortcut`
  sont entierement non testes** -- `grep VK_LWIN` sur les tests : zero fichier.
- **Tout a ete reverifie le 2026-08-18** sauf les soupcons qui exigent une
  execution : le HWND masque de R5, l'effet runtime de B2, et l'equivalence
  comportementale de `TypingEngine.Windows`. R6, R7 et R8 sont confirmes contre
  `ad049fc` et couverts par 8 tests neufs — mais `ad049fc` est du code **1.0.0**,
  et la 1.1.0 expediee se situe entre les deux dans la ligne interne. Que ces
  trois changements soient deja partis en 1.1.0 reste **indecidable depuis le
  depot** : seule l'etape 12 du smoke test, comparant la frappe de la 1.1.0
  installee a celle du nouveau build, le tranche.
- **Environ 6 des 104 chaines du delta sont des fragments de concatenation**, pas
  des phrases affichees.
