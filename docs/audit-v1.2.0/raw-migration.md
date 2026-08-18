# Axe migration des données persistées v1.1 -> v1.2

Dépôt : `microsoft-store` (racine du présent audit). Plage étudiée : `452aab0^..HEAD`
(base approximative `ad049fc`, cible `697d1a3`). Lecture seule ; aucun fichier `.cs` touché.

## Méthode et une mise en garde sur le diff

`452aab0` (« refactor: reconcile v1.2 and extract typing core », 2026-08-15) est un commit
unique qui fusionne deux choses distinctes dans le même diff : le rattrapage de la « base
interne 1.1.2 » (code déjà livré sur le Store mais jamais commité dans ce dépôt avant cette
date) ET les fonctions v1.2.0 en développement (Changelog.md:7). Conséquence directe :
`git diff 452aab0^..HEAD` fait apparaître `UsageStats.cs` (533 lignes) comme entièrement
« ajouté », alors que la fenêtre « Mes statistiques » qu'il alimente existait déjà en v1.1.0
(Changelog.md:72-76). Pour ce fichier précis, `git diff --stat 452aab0..HEAD -- src/UsageStats.cs`
renvoie 0 ligne : rien n'a changé depuis la réconciliation, donc l'historique git est aveugle
sur ce qui, dans ce fichier, est « v1.1 déjà là » contre « v1.2 nouveau ».

J'ai donc construit chaque ligne du schéma à partir de trois sources, par ordre de confiance :

1. **Le fichier réel** lu sur ce poste (v1.1.0.0 installée, voir section dédiée) — preuve la
   plus forte, c'est ce que le binaire Store a effectivement écrit.
2. **Le diff isolé après la réconciliation** (`git diff 452aab0..HEAD -- <fichier>`), qui lui
   n'est pas contaminé par le squash et constitue une preuve de code fiable.
3. **Les en-têtes de section du code lui-même** (ex. `// Défi du jour (v1.2.0)`) et le
   `Changelog.md`, quand les deux premières sources ne tranchent pas.

Quand une ligne du tableau ne repose que sur la source 3, je le signale explicitement — ce
n'est pas une preuve aussi solide qu'un fichier réel ou qu'un diff propre.

## Schéma `config.json` — avant / après

Chemin (inchangé, voir section ConfigFolderName) : `%LocalAppData%\AZERTY Global\config.json`
en mode MSIX, `<dossier exe>\config.json` en mode standalone. Toute clé inconnue est
préservée telle quelle à travers un cycle lecture/écriture : `EnsureLoaded`
(`src/ConfigManager.cs:755-792`) charge génériquement chaque propriété JSON dans
`_cache["<nom>"]` (ligne 783), et `Save` (`src/ConfigManager.cs:794-858`) réécrit tout ce que
contient `_cache` (boucle ligne 813-824). Aucune clé n'est donc perdue par une simple mise à
jour, sauf action explicite (`ClearWindowBounds`, seul appelant de `_cache.Remove`, ligne 525).

| Clé | Type | Défaut | Lue | Écrite | Statut v1.1 -> v1.2 |
|---|---|---|---|---|---|
| `onboardingDone` | bool | (aucun, legacy) | `ConfigManager.cs:93` (repli dans `ShowOnboardingAtStartup`) | jamais (aucun `SetBool("onboardingDone"...)` dans le fichier) | INCHANGÉE — vestige antérieur à `showOnboardingAtStartup` (ère <= v0.7 probable), hors périmètre de cette release |
| `showOnboardingAtStartup` | bool | `true` | `ConfigManager.cs:81-105` | `ConfigManager.cs:108` (`SetShowOnboardingAtStartup`) | INCHANGÉE — présente dans le config.json réel v1.1.0.0 |
| `learningMaxStepCompleted` | number (0..6) | `0` | `ConfigManager.cs:116` | `ConfigManager.cs:122-126` (monotone, no-op si pas de progrès) | INCHANGÉE — présente dans le réel (`6`) |
| `autoStartEnabled` | bool | `false` | `ConfigManager.cs:129` | `ConfigManager.cs:132` | INCHANGÉE — cache UI uniquement, la vérité vit dans `AutoStart.IsRegistered` (voir section dédiée). Présente dans le réel (`true`) |
| `notificationsEnabled` | bool | `true` (absence = actif) | `ConfigManager.cs:135-146` (logique inversée : absent ou non-`false` => true) | `ConfigManager.cs:149` | INCHANGÉE — absente du réel (jamais désactivées) |
| `appLanguage` | string (`"fr"`\|`"en"`) | `"fr"` | `ConfigManager.cs:152-159` | `ConfigManager.cs:166-172` | AMBIGU — déjà présent tel quel dès le commit de réconciliation `452aab0` (hors du bloc explicitement étiqueté v1.2), et le Changelog fait de l'interface bilingue une fonction v1.1.0 (Changelog.md:67-70). Absent du fichier réel, mais s'explique aussi par « jamais basculé vers l'anglais » (le setter n'écrit que si la langue change, ligne 169). Non tranchable avec certitude par le seul historique git |
| `firstRunTimestamp` | string (ISO 8601 UTC) | (aucun avant 1er accès) | `ConfigManager.cs:185-196` (`EnsureFirstRunTimestamp`) | même méthode ; valeur corrompue réécrite à `now` sans crash | INCHANGÉE — présente dans le réel. Depuis la v1.2 elle n'est plus l'horloge de la sollicitation d'avis (rôle transféré à `UsageStats.ActiveDaysCount`), mais reste écrite « pour les installations existantes et le diagnostic » (`ConfigManager.cs:181-183`) |
| `reviewPromptCount` | number (0, 1 ou 2) | dérivé, jamais 0 stocké tant que non consommé | `ConfigManager.cs:206-214` (`ReviewPromptCount`, avec repli sur `reviewPromptDone`) | `ConfigManager.cs:239-249` (`RecordReviewPromptShown`) | AJOUTÉE — diff propre post-réconciliation (`git diff 452aab0..HEAD -- src/ConfigManager.cs`), remplace l'ancien bool unique `ReviewPromptDone` |
| `reviewPromptLastShown` | string (`yyyy-MM-dd`, UTC) | absent | `ConfigManager.cs:217-226` | `ConfigManager.cs:245-246` | AJOUTÉE — même diff |
| `reviewPromptClicked` | bool | `false` | `ConfigManager.cs:232` | `ConfigManager.cs:252` (`SetReviewPromptClicked`) | AJOUTÉE — même diff |
| `reviewPromptDone` | bool | `false` | `ConfigManager.cs:212` (repli de `ReviewPromptCount`) | `ConfigManager.cs:247` (toujours posé à `true` à chaque affichage, essai 1 ou 2) | INCHANGÉE (clé) mais **rôle changé** : de source de vérité unique (v1.1) à compat descendante + entrée de migration (v1.2). Présente dans le réel (`true`) |
| `lessonAutoHintsEnabled` | bool | `false` | `ConfigManager.cs:270` | `ConfigManager.cs:273` | INCHANGÉE — déjà présente (ligne de contexte non modifiée) au commit `452aab0`, donc antérieure à la réconciliation elle-même |
| `lessonFreeStatsVisible` | bool | `true` | `ConfigManager.cs:276` (`GetBoolDefaultTrue`) | `ConfigManager.cs:278` | INCHANGÉE — idem |
| `lessonSummaryVisible` | bool | `true` | `ConfigManager.cs:281` | `ConfigManager.cs:283` | INCHANGÉE — idem |
| `lessonKeyboardVisible` | bool | `true` | `ConfigManager.cs:286` | `ConfigManager.cs:288` | INCHANGÉE — idem |
| `lessonInvisibleMarkersVisible` | bool | `true` | `ConfigManager.cs:291` | `ConfigManager.cs:293` | INCHANGÉE — idem |
| `shortcutVirtualKeyboardVk` | number (VK code) | `0x51` ('Q') | `ConfigManager.cs:333-339` | `ConfigManager.cs:340-344` (validé par `IsShortcutAllowedVk`) | INCHANGÉE |
| `shortcutCharacterSearchVk` | number (VK code) | `0x57` ('W') | `ConfigManager.cs:348-354` | `ConfigManager.cs:355-359` | INCHANGÉE |
| `compatibility` | objet imbriqué `{ "<processus>": "forceOn"\|"forceOff" }` | absent / vide | `ConfigManager.cs:390-402`, chargement spécial `ConfigManager.cs:768-781` | `ConfigManager.cs:408-422`, écriture spéciale `ConfigManager.cs:825-836` (omise du fichier si la map est vide, même si elle existait avant) | INCHANGÉE |
| `trainingEnabled` | bool | `false` (opt-in) | `ConfigManager.cs:441` | `ConfigManager.cs:442-448` (remet aussi `trainingIgnoredCount` à 0 si activé) | AJOUTÉE — bloc explicitement étiqueté `// Défi du jour (v1.2.0)` (`ConfigManager.cs:436-438`) dès le commit de réconciliation |
| `trainingIgnoredCount` | number | `0` | `ConfigManager.cs:451` | `ConfigManager.cs:452` | AJOUTÉE — même bloc |
| `trainingSequenceIndex` | number | `0` | `ConfigManager.cs:456` | `ConfigManager.cs:457` | AJOUTÉE — même bloc |
| `trainingLastSessionDate` | string (`yyyy-MM-dd`) | absent | `ConfigManager.cs:460` | `ConfigManager.cs:461` | AJOUTÉE — même bloc |
| `trainingLastReminderDate` | string (`yyyy-MM-dd`) | absent | `ConfigManager.cs:464` | `ConfigManager.cs:465` | AJOUTÉE — même bloc |
| `challengeAnnounceDone` | bool | `false` | `ConfigManager.cs:470` | `ConfigManager.cs:471` | AJOUTÉE — même bloc |
| `autoStartNudgeDone` | bool | `false` | `ConfigManager.cs:475` | `ConfigManager.cs:476` | AJOUTÉE — diff propre post-réconciliation (`02d5458`, 2026-08-17), hors du squash |
| `compatibilityDebugLog` | bool | `false` | `ConfigManager.cs:482` | `ConfigManager.cs:484` | INCHANGÉE |
| `virtualKeyboardBounds` | string (`"left,top,w,h"`) | absent | `ConfigManager.cs:486-509` (`TryGetWindowBounds`), clé `ConfigManager.cs:17` | `ConfigManager.cs:511-517`, effacée par `ConfigManager.cs:519-530` | INCHANGÉE — présente dans le réel |
| `lessonsWindowBounds` | string (`"left,top,w,h"`) | absent | idem, clé `ConfigManager.cs:18` | idem | INCHANGÉE — absente du réel (fenêtre Leçons jamais ouverte sur ce poste, cohérent avec l'absence de `lessons-progress.json`) |
| `_compat_log_salt` | string (base64, 16 octets) | généré au premier besoin | `ConfigManager.cs:618` | `ConfigManager.cs:623-624` (`GetOrCreateLogSalt`) | INCHANGÉE — absente du réel (aucun événement de compatibilité n'a encore eu besoin d'anonymiser un process sur ce poste) |

Aucune clé de version de schéma n'existe dans `config.json` : grep exhaustif de
`"version"` / `schemaVersion` / `configVersion` sur tout `src/*.cs`, seules occurrences dans
`LessonProgressStore.cs:186` et `:257` (un fichier différent). `ConfigManager.cs` ne
comporte aucune de ces trois chaînes.

## `ConfigFolderName` — le dossier n'a pas bougé

`ProductIdentity.ConfigFolderName = "AZERTY Global"` (`src/ProductIdentity.cs:41`), consommé
par `ConfigManager.GetConfigPath` (`src/ConfigManager.cs:65`) et `ConfigManager.LogDirectory`
(`src/ConfigManager.cs:539`). Diff du refactor `cf5fea0` sur ces deux lignes :

```
-            var appDataDir = Path.Combine(localAppData, "AZERTY Global");
+            var appDataDir = Path.Combine(localAppData, ProductIdentity.ConfigFolderName);
```

Substitution littérale : `ProductIdentity.ConfigFolderName` vaut exactement la chaîne
retirée. Le chemin logique calculé par le code (`%LocalAppData%\AZERTY Global\`) est donc
identique avant et après le refactor d'identité. Confirmé par grep sur tout l'historique du
fichier (`git log -p --follow -- src/ConfigManager.cs`) : la chaîne `"AZERTY Global"` apparaît
déjà à l'ajout initial du fichier, inchangée jusqu'à la substitution `cf5fea0`.

Nuance MSIX non liée à la migration : en paquet, Windows redirige silencieusement
`%LocalAppData%` vers `C:\Users\<user>\AppData\Local\Packages\<PackageFamilyName>\LocalCache\Local\`
pour une app Win32 packagée — le code ne voit jamais cette redirection, mais qui inspecte le
disque à la main doit la connaître (confirmé en listant `AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg`
sur ce poste, voir plus bas). Le segment `AZERTY Global` reste le même des deux côtés.

## Migration `reviewPrompt*`

Code de la migration — c'est un calcul à la lecture, jamais une réécriture :

```
// src/ConfigManager.cs:198-214
/// Migration v1.1 -> v1.2 : les installations existantes ne connaissent que le booléen
/// `reviewPromptDone`. Un `true` vaut une sollicitation déjà consommée, sinon la
/// v1.2.0 enverrait deux nouvelles notifications à quelqu'un qui a déjà été sollicité.
/// La conversion est déduite à la lecture, sans réécrire les configs existantes.
public static int ReviewPromptCount
{
    get
    {
        var stored = GetUInt("reviewPromptCount");
        if (stored > 0) return (int)stored;
        return GetBool("reviewPromptDone") ? 1 : 0;
    }
}
```

Écriture (seul point qui modifie le disque) :

```
// src/ConfigManager.cs:239-249
public static void RecordReviewPromptShown()
{
    lock (_lock)
    {
        var next = (uint)Math.Min(ReviewPromptCount + 1, 2);
        SetUInt("reviewPromptCount", next);
        SetString("reviewPromptLastShown",
            DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        SetBool("reviewPromptDone", true);
    }
}
```

`GetBool` (`src/ConfigManager.cs:676-685`) : `true` seulement si la valeur JSON est
littéralement `true` (`ValueKind == JsonValueKind.True`) ; toute autre valeur ou absence de
clé renvoie `false`. Tests xUnit qui couvrent précisément cette migration :
`src/AZERTYGlobal.Tests/ReviewPromptConfigTests.cs:111-121`
(`ReviewPromptCount_MigratesLegacyDoneFlagToOne`) et `:124-130`
(`ReviewPromptCount_ExplicitCountWinsOverLegacyFlag`).

**`reviewPromptDone` à `false` explicitement présent ?**
`GetBool("reviewPromptDone")` lit `ValueKind == JsonValueKind.False`, donc `!= True` ->
retourne `false` (`ConfigManager.cs:681-683`). `ReviewPromptCount` vaut alors `0`, exactement
comme si la clé était absente. Non couvert par un test dédié (les tests xUnit couvrent absent
et `true`, pas `false` explicite), mais découle directement de la lecture de `GetBool`.

**`config.json` v1.1 où `reviewPromptDone` est absent ?**
Même chemin de code : `_cache.TryGetValue("reviewPromptDone", ...)` échoue, `GetBool` renvoie
`false` (`ConfigManager.cs:681,684`), `ReviewPromptCount` vaut `0`. Identique au cas
« `false` explicite » — l'absence et le `false` explicite sont indiscernables et traités
pareil. Test : `ReviewPromptCount_DefaultsToZero` (`ReviewPromptConfigTests.cs:76-81`).

**Idempotence d'une relecture d'un `config.json` v1.2 déjà migré ?**
Oui. `ReviewPromptCount` (lignes 206-214) est un pur getter : aucun `Set*` n'y est appelé,
seul `RecordReviewPromptShown` écrit sur disque et il n'est jamais invoqué depuis le getter.
Relire le même fichier N fois sans appeler `RecordReviewPromptShown` renvoie N fois la même
valeur, sans effet de bord. Test explicite : `RecordReviewPromptShown_IncrementsAndPersists`
(`ReviewPromptConfigTests.cs:84-96`) vérifie notamment qu'un rechargement complet du
fichier (`ConfigManager.OverrideConfigPathForTests(_configPath)` ligne 91, qui vide le cache
mémoire et relit le disque) renvoie toujours `1` avant tout second appel.

**`reviewPromptDone` retiré du fichier après migration, ou laissé en place ?**
Laissé en place, et même réécrit à `true` à *chaque* affichage (`RecordReviewPromptShown`,
ligne 247), essai 1 comme essai 2. Aucun appel à `_cache.Remove("reviewPromptDone")`
n'existe dans le fichier (le seul `Remove` du fichier est celui de `ClearWindowBounds`,
ligne 525, sur une clé différente). Citation explicite de l'intention :

```
// src/ConfigManager.cs:234-237
/// Enregistre une sollicitation affichée : incrémente le compteur et pose la date.
/// `reviewPromptDone` reste écrit pour qu'un retour arrière en v1.1 ne reparte pas
/// de zéro.
```

Conséquence sur un retour arrière v1.1 : un retour arrière se comporte correctement au sens
où `reviewPromptDone=true` est déjà posé dès le premier des deux essais v1.2 (pas seulement
après le second) — donc même un utilisateur qui n'a vu qu'un seul des deux essais v1.2 ne
sera pas resollicité par le code v1.1 (qui ne connaît que ce booléen unique et l'interprète
comme « one-shot déjà fait »). Le code v1.1 lui-même n'est pas dans ce dépôt (le Changelog
précise que le paquet v1.1.0 a été produit hors dépôt, `Changelog.md:65`) ; cette conclusion
repose sur la description du comportement v1.1 dans `Changelog.md:81`
(« Sollicitation d'avis unique [...] marquée comme faite dès l'affichage [...]
`reviewPromptDone` [...] écrits dans config.json ») et non sur une lecture directe du binaire
v1.1.

**Numéro de version de schéma dans `config.json` ?**
Non. Aucune clé de version n'existe pour `config.json` (voir tableau ci-dessus) — à
contraster avec `lessons-progress.json` qui, lui, en porte une (`LessonProgressStore.cs:8,186,257`).

Gate complet observé dans l'appelant (utile pour la section scénarios) :

```
// src/TrayApplication.cs:1639-1673 (essai 1 vs essai 2)
int already = ConfigManager.ReviewPromptCount;
if (already >= 2 || ConfigManager.ReviewPromptClicked) return false;
...
int attempt = already + 1;
if (attempt == 1) {
    if (activeDays < ReviewPromptFirstActiveDays) return false;          // 3
    if (today.DayNumber - firstRemap.Value.DayNumber < ReviewPromptFirstMinDays) return false; // 3
} else {
    if (activeDays < ReviewPromptSecondActiveDays) return false;         // 10
    // Null pour une installation migrée depuis la v1.1, qui ne connaissait pas
    // cette date : l'essai 1 y est forcément ancien, le plancher est acquis.
    var lastShown = ConfigManager.ReviewPromptLastShown;
    if (lastShown.HasValue && today.DayNumber - lastShown.Value.DayNumber < ReviewPromptSecondMinGapDays) return false; // 7
    var lastActive = UsageStats.LastActiveDate;
    if (lastActive == null || today.DayNumber - lastActive.Value.DayNumber > ReviewPromptStaleDays) return false; // 3
}
```
Constantes : `TrayApplication.cs:80,81,85,86,88,90`
(`ReviewPromptFirstActiveDays=3`, `ReviewPromptSecondActiveDays=10`,
`ReviewPromptFirstMinDays=3`, `ReviewPromptSecondMinGapDays=7`, `ReviewPromptStaleDays=3`,
`ReviewPromptErrorCooldownHours=48`).

Écart code/Changelog relevé en passant, sans jugement : la porte « today » de ce gate est
calculée en heure locale (`DateOnly.FromDateTime(DateTime.Now)`, `TrayApplication.cs:1651`,
et de même dans `UsageStats.cs:297,310`), alors que `reviewPromptLastShown` est écrit en
date UTC (`DateTime.UtcNow.ToString("yyyy-MM-dd")`, `ConfigManager.cs:246`). Autour de minuit
UTC (02h locale, fuseau Europe/Paris d'après les horodatages des commits), le jour écrit et
le jour comparé peuvent différer d'un jour.

## `lessons-progress.json` — schéma et tolérance

Chemin : `Path.Combine(ConfigManager.LogDirectory, "lessons-progress.json")`
(`src/LessonProgressStore.cs:9,17`) — même dossier que `config.json`.

| Clé | Type | Lue | Écrite |
|---|---|---|---|
| `version` | number, doit valoir `1` (`CurrentVersion`) | `LessonProgressStore.cs:186` | `LessonProgressStore.cs:257` |
| `lastModuleId` | string\|null | `:189` | `:258` |
| `lastLessonId` | string\|null | `:190` | `:259` |
| `lastExerciseIndex` | number | `:191` | `:260` |
| `onboardingSyncedMaxStep` | number | `:192` | `:261` |
| `exercises.<stableKey>.hash` | string | `:200` | `:266` |
| `exercises.<stableKey>.completed` | bool | `:205` | `:267` |
| `exercises.<stableKey>.successfulAttempts` | number | `:206` | `:268` |
| `exercises.<stableKey>.bestWpm` | number\|null | `:207` | `:269` |
| `exercises.<stableKey>.bestAccuracyPercent` | number\|null | `:208` | `:270` |
| `exercises.<stableKey>.bestSeconds` | number\|null | `:209` | `:271` |
| `exercises.<stableKey>.lastCompletedUtc` | string ISO\|null | `:213-215` | `:272-275` |
| `exercises.<stableKey>.hintsUsed` | number | `:210` | `:276` |
| `exercises.<stableKey>.errorMatrix` (legacy) | — | détectée ligne `:217-218`, jamais réécrite | jamais (absente du writer) |

Aucun champ nouveau pour la v1.2 : `git diff 452aab0^..HEAD -- src/LessonProgressStore.cs`
renvoie 0 ligne — le fichier est **identique**, octet pour octet au niveau du code, entre la
base et `HEAD`. Ce que change la v1.2, c'est uniquement qui *lit* `bestWpm` :

```
// src/LessonsWindow.cs:2101-2107
bool challengeFinale = CurrentModule.Id == DailyChallenge.ModuleId &&
                       _exerciseIndex == CurrentLesson.Exercises.Count - 1;
int? previousBestWpm = challengeFinale
    ? _progress.GetValidProgress(CurrentExercise)?.BestWpm
    : null;
```

```
// src/LessonsWindow.cs:2142-2148
// Un premier résultat n'est pas un record : sans meilleur score antérieur,
// il n'y a rien à battre.
bool personalBest = previousBestWpm.HasValue && _session.Stats.Wpm.HasValue &&
                    _session.Stats.Wpm.Value > previousBestWpm.Value;
```

Tolérance à un fichier v1.0/v1.1 : `GetValidProgress` (`LessonProgressStore.cs:27-33`) renvoie
`null` si le fichier est absent (`Load` sort immédiatement, `:179`), si la clé
`exercises.<stableKey>` n'existe pas, ou si le hash stocké ne correspond pas au hash actuel
de l'exercice (`StringComparer.Ordinal.Equals(progress.Hash, exercise.Hash)`, ligne 30). Le
`?.BestWpm` (ligne 2106) chaîne sur ce `null`, donc `previousBestWpm` devient `null` et
`personalBest` est `false` sans exception (ligne 2144, `previousBestWpm.HasValue` coupe
court). Vérifié réellement sur ce poste : **`lessons-progress.json` n'existe pas du tout**
dans le dossier de configuration de l'installation v1.1.0.0 (voir listing plus bas) —
c'est exactement le cas « fichier absent » que `Load()` gère ligne 179.

Point non couvert par le Changelog mais visible dans le code : le module « Défi du jour »
(`DailyChallenge.ModuleId`) est lui-même nouveau en v1.2 (`DailyChallenge.cs`, 255 lignes
ajoutées à `452aab0`). Aucune installation v1.0/v1.1 n'a jamais pu enregistrer de progrès
pour ce module précis — donc `previousBestWpm` vaudra `null` et `personalBest` sera `false`
au **premier** défi de **tout** utilisateur qui met à jour, même celui qui utilise les Leçons
depuis la v1.0. Ce n'est pas un bug de migration, juste une conséquence mécanique du fait que
la clé cherchée (`exercise.StableKey` du Défi du jour) n'a jamais pu exister avant.

## Statistiques d'usage (`usage-stats.json`)

Chemin : `Path.Combine(ConfigManager.LogDirectory, "usage-stats.json")` (`src/UsageStats.cs:85`).
En-tête du fichier lui-même (`src/UsageStats.cs:1-2`) : « Statistiques d'usage 100 % locales —
v1.1 (arbitrage LLM Council 2026-07-11 [...]) ». Aucune clé de version de schéma (mêmes
grep que pour `config.json`, aucune occurrence dans ce fichier).

| Champ | Lu | Écrit | Statut |
|---|---|---|---|
| `firstRemapDate` | `UsageStats.cs:437` | `:501` | INCHANGÉE — **présente et non nulle dans le fichier réel** (`2026-07-27`) |
| `lastActiveDate` | `:438` | `:502` | INCHANGÉE — présente dans le réel (`2026-08-18`) |
| `activeDaysCount` | `:439` | `:503` | INCHANGÉE — présente dans le réel (`19`) |
| `currentStreak` | `:440` | `:504` | INCHANGÉE — présente dans le réel (`4`) |
| `bestStreak` | `:441` | `:505` | INCHANGÉE — présente dans le réel (`15`) |
| `totalActiveMinutes` | `:442` | `:506` | INCHANGÉE — présente dans le réel (`837`) |
| `accentedUppercaseCount` | `:443` | `:507` | INCHANGÉE — présente dans le réel (`41`) |
| `frenchTypographyCount` | `:444` | `:508` | INCHANGÉE — présente dans le réel (`57`) |
| `internationalCount` | `:445` | `:509` | INCHANGÉE — présente dans le réel (`88`) |
| `symbolsCount` | `:446` | `:510` | INCHANGÉE — présente dans le réel (`9`) |
| `searchOpenCount` | `:447` | `:511` | AJOUTÉE (v1.2.0) — **absente du fichier réel v1.1.0.0** ; commentaire de section explicite `UsageStats.cs:42-46` (« Défi du jour (v1.2.0) [...] compteurs GLOBAUX d'ouvertures uniquement ») |
| `virtualKeyboardOpenCount` | `:448` | `:512` | AJOUTÉE (v1.2.0) — même preuve |
| `challengesCompletedCount` | `:449` | `:513` | AJOUTÉE (v1.2.0) — même preuve |
| `lastSpecialCharDate` | `:450` | `:514` | AJOUTÉE (v1.2.0) — même preuve |

Chaque lecture passe par un helper qui dégrade en valeur neutre si la clé est absente :
`GetStringProp` -> `null` (`UsageStats.cs:462-463`), `GetIntProp` -> `0` (`:465-466`),
`GetLongProp` -> `0L` (`:468-469`). Il n'existe **aucun** garde-fou de version qui rejette un
fichier v1.1 incomplet — `EnsureLoaded` (`:422-460`) lit ce qu'il trouve et laisse `0`/`null`
sur tout le reste, contrairement à `LessonProgressStore.Load` qui, lui, rejette
explicitement tout fichier dont `version != 1` (`LessonProgressStore.cs:186`).

**Réponse au point 4 du brief (jours d'usage distincts / première frappe remappée) :**
`activeDaysCount` et `firstRemapDate` existaient déjà en v1.1 — confirmé par le fichier réel,
qui porte des valeurs déjà accumulées (19 jours, première frappe le 2026-07-27) sous un
binaire qui n'a jamais connu le code v1.2. La v1.2 ne les crée pas : elle les *consomme* pour
de nouveaux déclencheurs (`AutoStartNudge`, `MaybeShowReviewPrompt` essai 1/2,
`TrainingReminders`). En revanche, `searchOpenCount`, `virtualKeyboardOpenCount`,
`challengesCompletedCount` et `lastSpecialCharDate` sont bien créés par la v1.2 — une
installation qui met à jour repart à `0`/`null` sur ces quatre champs précis, jamais sur les
dix autres.

Conséquence directe sur les seuils : `AutoStartNudge.ShouldPrompt`
(`AutoStartNudge.cs:38-47`) ne lit que `ActiveDaysCount` et `FirstRemapDate` — deux champs
**déjà peuplés** — donc son seuil (`MinActiveDays = 2`, `AutoStartNudge.cs:34`) peut être
satisfait dès le premier lancement post-mise à jour pour un utilisateur v1.1 de longue date,
sans palier de « remise à zéro ». `TrainingReminders.Snapshot`
(`TrainingReminders.cs:83-95`) mélange les deux catégories : `CurrentStreak` et
`LastActiveDate` (hérités, déjà non nuls sur ce poste) mais `HelperOpens` (somme de
`SearchOpenCount` + `VirtualKeyboardOpenCount`, tous deux nouveaux) qui, lui, repart
nécessairement à `0` pour tout le monde.

## Le `config.json` réel lu sur ce poste

Installation Store v1.1.0.0, dossier `LocalCache\Local\AZERTY Global\` sous
`AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg` (redirection MSIX de `%LocalAppData%`, voir plus
haut). Trois fichiers seulement dans ce dossier : `config.json`, `error.log`,
`usage-stats.json`. **`lessons-progress.json` n'existe pas** sur ce poste.

`config.json` (6 clés, aucune masquée — aucune ne ressemble à une donnée personnelle) :

- `firstRunTimestamp` = `2026-07-27T17:05:54.9267893+00:00`
- `showOnboardingAtStartup` = `true`
- `learningMaxStepCompleted` = `6` (les 6 exercices d'onboarding terminés)
- `autoStartEnabled` = `true` (cache — ne prouve pas que la StartupTask Windows est
  réellement active, voir section suivante)
- `reviewPromptDone` = `true` (aucun `reviewPromptCount` ni `reviewPromptLastShown` :
  c'est exactement le cas de migration décrit par le Changelog)
- `virtualKeyboardBounds` = `"1279,528,882,383"`

`usage-stats.json` (10 clés, toutes des compteurs/dates agrégées, rien à masquer) :

- `firstRemapDate` = `2026-07-27`, `lastActiveDate` = `2026-08-18` (aujourd'hui)
- `activeDaysCount` = `19`, `currentStreak` = `4`, `bestStreak` = `15`
- `totalActiveMinutes` = `837`
- `accentedUppercaseCount` = `41`, `frenchTypographyCount` = `57`,
  `internationalCount` = `88`, `symbolsCount` = `9`

`error.log` (2 lignes, aucune frappe ni contenu, conforme à la politique de confidentialité) :
deux entrées `SecondInstance: packaged=True, args=[...AZERTY Global.exe]` du 2026-07-27 —
pas d'exception journalisée, donc `LastErrorUtc` serait de toute façon `null` au démarrage
d'un nouveau process (ce champ n'est de toute manière jamais persisté, voir section suivante).

## Autres états persistés

- **`virtualKeyboardBounds` / `lessonsWindowBounds`** — clés constantes
  (`ConfigManager.cs:17-18`), non affectées par le refactor `ProductIdentity` : le message du
  commit `cf5fea0` le dit explicitement (« the saved window positions key off their own
  constants [...] not off class names »). Consommateurs : `VirtualKeyboard.cs:561,599`,
  `LessonsWindow.cs:410,468`, remise à zéro manuelle via
  `SettingsWindow.cs:828` et `:836`.
- **Lancement automatique** — deux mécanismes distincts selon le mode, aucun des deux dans
  `config.json` :
  - Paqueté (ce poste) : `Windows.ApplicationModel.StartupTask`, `TaskId="AZERTYGlobalStartup"`
    (`AutoStart.cs:238`), qui correspond au `TaskId` déclaré dans le manifeste installé
    (`docs/audit-v1.2.0/AppxManifest.INSTALLED-1.1.0.0.xml:27`) et dans le manifeste v1.2.0
    (`msix/AppxManifest.xml:53`) — **identique dans les deux**, donc une tâche déjà enregistrée
    sous la v1.1 reste reconnue après mise à jour. Cet état vit dans le registre interne de
    Windows (pas un fichier que ce poste de travail expose directement) ; `AutoStart.IsRegistered`
    (`AutoStart.cs:128-129`) l'interroge en direct via `Windows.ApplicationModel.StartupTask.GetAsync`,
    sans passer par le cache `config.json`.
  - Non paqueté : raccourci `.lnk` dans le dossier Startup (`AutoStart.cs:13-19`,
    `ProductIdentity.ShortcutFileName = "AZERTY Global.lnk"`) — non applicable à cette
    installation Store.
  - `autoStartEnabled` dans `config.json` est documenté comme un cache non-autoritaire
    (`ConfigManager.cs:128` : « Cache de compatibilité du lancement automatique. L'UI doit
    utiliser `AutoStart.IsRegistered` »).
- **Registre Windows (HKCU/HKLM)** — aucune écriture trouvée. Les seules occurrences de
  `Registry` dans `src/` appartiennent à la classe `GameRegistry` (liste interne de process
  anti-cheat/accès distant), sans rapport avec le registre Windows.
- **`error.log` / `error.log.old`** — rotation à 5 Mo, une seule génération conservée
  (`ConfigManager.cs:652-674`). Changement v1.2 : `Log()` alimente désormais aussi
  `_lastErrorUtc` en mémoire (`ConfigManager.cs:574`, absent du code à `452aab0^`) pour la
  fenêtre de silence de 48 h de la sollicitation d'avis — **jamais persisté** (doc explicite
  `ConfigManager.cs:259-264` : une écriture de config depuis le chemin d'erreur se
  rappellerait elle-même).
- **`lessons-progress.json`** — déjà traité ci-dessus ; absent sur ce poste précis.

## Scénarios de migration à tester à la main

1. **État de départ** : `config.json` réel de ce poste (`reviewPromptDone: true`, pas de
   `reviewPromptCount`/`reviewPromptLastShown`), `usage-stats.json` réel
   (`activeDaysCount: 19`, `firstRemapDate: 2026-07-27`, `lastActiveDate: 2026-08-18`),
   `NotificationsEnabled` par défaut (clé absente = actif), pas d'erreur récente (`error.log`
   sans exception). **Action** : mettre à jour cette installation vers la v1.2.0.0 et la
   lancer un jour où `UsageStats.LastActiveDate` reste à moins de 3 jours d'écart avec
   aujourd'hui. **Résultat attendu d'après le code** : `ReviewPromptCount` vaut `1`
   (migration), `attempt = 2` ; la branche essai 2 passe tous ses gardes (`activeDays=19 >= 10` ;
   `ReviewPromptLastShown` absent -> plancher considéré acquis ; `LastActiveDate` à moins de
   3 jours) -> `MaybeShowReviewPrompt` affiche l'essai 2 (dernier possible) dès la première
   session post-mise à jour qui remplit ces conditions, et pose `reviewPromptCount=2`,
   `reviewPromptLastShown=<aujourd'hui UTC>`, `reviewPromptDone=true` (déjà vrai).
2. **État de départ** : `config.json` v1.1 sans aucune clé `reviewPrompt*` (installation qui
   n'a jamais atteint le déclencheur J+7 de la v1.1). **Action** : mise à jour puis usage
   normal. **Résultat attendu** : `ReviewPromptCount` vaut `0`, `attempt=1` ; l'essai 1 attend
   `ActiveDaysCount >= 3` ET `today - firstRemap >= 3` jours (`TrayApplication.cs:1657-1658`)
   avant de s'afficher — donc pas de sollicitation immédiate, contrairement au scénario 1.
3. **État de départ** : `config.json` avec `"reviewPromptDone": false` écrit explicitement
   (jamais produit par le code actuel, mais un fichier édité à la main ou un futur format
   pourrait le faire). **Résultat attendu** : identique au scénario 2 — `GetBool` traite
   `false` explicite exactement comme une absence, `ReviewPromptCount=0`.
4. **État de départ** : `config.json` v1.2 avec `reviewPromptCount=1` déjà posé. **Action** :
   relancer l'application plusieurs fois sans que `RecordReviewPromptShown` ne soit rappelé
   (aucune notification ne se déclenche). **Résultat attendu** : `ReviewPromptCount` reste
   `1` à chaque lecture — aucune réécriture, aucune dérive.
5. **État de départ** : installation qui a reçu l'essai 2 sous la v1.2
   (`reviewPromptCount=2`, `reviewPromptDone=true`). **Action** : désinstaller puis
   réinstaller la v1.1.0.0 par-dessus le même profil utilisateur (mêmes fichiers de config).
   **Résultat attendu d'après le Changelog** (le binaire v1.1 n'est pas dans ce dépôt) : le
   code v1.1 ne lit que `reviewPromptDone`, le trouve à `true`, et ne resollicite pas —
   comportement « correct » au sens où personne n'est reharcelé, même si l'utilisateur n'a
   vu qu'un ou deux essais réels.
6. **État de départ** : `lessons-progress.json` absent (état réel de ce poste). **Action** :
   ouvrir le module Leçons, terminer le Défi du jour une première fois avec un score W.
   **Résultat attendu** : `GetValidProgress` renvoie `null` (fichier absent,
   `LessonProgressStore.cs:179`), `previousBestWpm=null`, `personalBest=false` — le texte
   partagé ne mentionne jamais de record au premier essai, quel que soit W.
7. **État de départ** : `usage-stats.json` v1.1 réel de ce poste (10 champs, sans
   `searchOpenCount` ni les 3 autres champs v1.2). **Action** : lancer la v1.2.0.0 et ouvrir
   la recherche de caractères une fois. **Résultat attendu** : chargement sans exception
   (`GetLongProp` dégrade à `0` pour les champs absents), `searchOpenCount` passe de `0`
   (implicite) à `1` et le fichier réécrit porte désormais les 14 champs.
8. **État de départ** : `config.json` réel de ce poste, `autoStartEnabled: true` (cache).
   **Vérification à faire en direct** (non concluante depuis les fichiers seuls) : interroger
   `Windows.ApplicationModel.StartupTask.GetAsync("AZERTYGlobalStartup")` pour vérifier l'état
   *réel* de la tâche planifiée sur ce compte Windows. Si elle est déjà `Enabled`,
   `AutoStartNudge.ShouldPrompt` renvoie `false` dès la première garde
   (`AlreadyRegistered`, `AutoStartNudge.cs:41`) quel que soit `ActiveDaysCount` ; si elle ne
   l'est pas malgré le cache à `true`, la relance peut se déclencher dès `ActiveDaysCount >= 2`
   (déjà acquis sur ce poste, `activeDaysCount=19`).

## Ce que je n'ai pas pu vérifier

- **L'état réel de la `StartupTask` Windows** (`AZERTYGlobalStartup`) sur ce compte : lecture
  seule imposée par le brief, et cette information ne vit dans aucun fichier — seule l'API
  WinRT `StartupTask.GetAsync` la connaît. Le cache `autoStartEnabled=true` dans
  `config.json` ne prouve rien dans un sens ou dans l'autre (voir scénario 8).
- **Le comportement réel du binaire v1.1.0.0 face à un retour arrière depuis un
  `config.json` v1.2** : ce code n'est pas dans ce dépôt (`Changelog.md:65` : le paquet
  v1.1.0 a été produit hors dépôt, aucun tag ni commit ne lui correspond ici). Ma réponse sur
  le retour arrière (section migration `reviewPrompt*`) repose sur la description du
  Changelog et sur l'inspection du fichier réel, pas sur une lecture du binaire v1.1
  lui-même — je n'ai pas décompilé ni exécuté
  `AZERTYGlobal.AZERTYGlobal_1.1.0.0_x64__w9kghr08zmhbg\AZERTY Global.exe`.
- **La date exacte d'introduction de `appLanguage`, `lessonAutoHintsEnabled` et les 4 clés
  `lesson*Visible`** : ces propriétés existent déjà, inchangées, au commit de réconciliation
  `452aab0` (lignes de contexte, pas des lignes ajoutées) — donc antérieures à la
  réconciliation dans ce dépôt, mais je ne peux pas dater plus précisément leur première
  apparition réelle sur le Store (v1.0 ? v1.1 ?) sans le commit qui les a introduites, qui
  n'existe pas dans cet historique.
- **Un fichier `config.json` v1.0 authentique** (avant `showOnboardingAtStartup`, avec
  seulement `onboardingDone`) : je n'en ai trouvé aucune trace sur ce poste ni dans le dépôt ;
  la tolérance du code à ce cas repose sur la lecture du repli ligne 93, non sur un fichier
  réel observé.
- Je n'ai pas ouvert `docs/audit-v1.2.0/witness-baseline.py`, `delta-textes.py` ni
  `delta-typographie.py` en détail (scripts d'une autre session parallèle sur l'axe
  identité/localisation de ce même audit) — je les ai seulement constatés présents et non
  suivis pour ne pas déborder de mon axe.
