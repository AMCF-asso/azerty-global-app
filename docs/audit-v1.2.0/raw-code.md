# Audit v1.2.0 — axe « correction du code et régressions »

Plage auditée : `452aab0^..HEAD` (`ad049fc` → `697d1a3`), 23 commits.
Dépôt en lecture seule. Build Release recompilé depuis zéro : **0 warning, 0 erreur**.
Suites xUnit rejouées : **149 + 95 + 14 = 258** tests verts (le Changelog annonce
« 149 + 95 + 6 = 250 » — le compte du moteur portable est périmé, `LayoutJsonParserTests`
en a ajouté 11).

---

## 1. Refactor `ProductIdentity` (`cf5fea0`, `06f997d`, `af745d3`, `efff943`)

### Ce que le code fait (citations)

`src/ProductIdentity.cs` centralise 13 constantes et 2 fabriques :

```
src/ProductIdentity.cs:60  public const string SingleInstanceMutexName = Namespace + "SingleInstance";
src/ProductIdentity.cs:67  public static string WindowClass(string suffix) => $"{Namespace}_{suffix}";
src/ProductIdentity.cs:70  public static string Url(string path) => SiteBaseUrl + path;
```

**Vérification machine de la substitution ligne pour ligne.** J'ai comparé
`cf5fea0^` et `cf5fea0` fichier par fichier, ligne par ligne, en réinjectant la valeur de
chaque constante dans la ligne d'après, puis en normalisant `const`/`var`/`static readonly`
et l'interpolation. Sur les 17 fichiers `.cs` touchés, **5 lignes seulement ressortent, et
les 5 sont des faux positifs de mon normaliseur** (préfixe de namespace dans
`AssemblyInfo.cs`, et `"AZERTY Global v"` contre `"AZERTY Global" + " v"`). Aucun écart de
chaîne produite.

Détail par catégorie, chaîne avant → chaîne après :

| Catégorie | Avant | Après | Verdict |
|---|---|---|---|
| 20 URL du site | `https://azerty.global/guide`, `/nouveautes`, `/assets/Prise_en_main_AZERTY_Global.pdf`, `/guide#cartes`, `/mentions-legales#confidentialite-securite`, `/soutien`, `/feedback`, `/feedback?source=app-notification`, `/bug?v=…&os=…&src=app` | `Url(path)` = `"https://azerty.global" + path` | identique, casse et chemin compris |
| Discord | `https://discord.gg/nYknqshJz3` | `DiscordInviteUrl` | identique |
| Dépôt | `https://github.com/AZERTYGlobal/app` | `RepositoryUrl` | identique |
| Store | `ms-windows-store://review/?ProductId=9N4BTS43SSSZ` | concat de constantes | identique |
| Binaire | `"AZERTY Global.exe"` | `ExecutableName` | identique, et conforme à `<AssemblyName>AZERTY Global</AssemblyName>` (`src/AZERTYGlobal.csproj:8`) + `Executable="AZERTY Global.exe"` (`msix/AppxManifest.xml:33`) |
| Raccourci | `"AZERTY Global.lnk"` | `ShortcutFileName` | identique |
| Dossier de config | `"AZERTY Global"` | `ConfigFolderName`, littéral distinct de `DisplayName` | identique — la progression et la config ne bougent pas |
| Logo | `"favicon-azerty-global.png"` | `LogoResourceName` | identique |
| **Mutex d'instance unique** | `$"Local\\AZERTYGlobalSingleInstance.{sid}"` | `$"Local\\{SingleInstanceMutexName}.{sid}"` = `AZERTYGlobalSingleInstance` | **identique** — pas de double instance ni de refus de démarrage à la mise à jour |
| 31 titres affichés | `"AZERTY Global"` | `DisplayName` | identique |

**Les 12 classes fenêtre, enregistrement contre désenregistrement.** Toutes résolvent
`AZERTYGlobal_<suffixe>`, soit exactement l'ancien littéral. Les 7 paires que le Changelog
cite sont appariées, et aucune classe n'est enregistrée sous un nom et désenregistrée sous
un autre :

| Suffixe | `lpszClassName` | `UnregisterClassW` |
|---|---|---|
| About | `AboutWindow.cs:153` | `AboutWindow.cs:578` |
| CharSearch | `CharacterSearch.cs:713` | `CharacterSearch.cs:1510` |
| LayoutConflict | `LayoutConflictWindow.cs:140` | `LayoutConflictWindow.cs:410` |
| Learning | `LearningModule.cs:799` | `LearningModule.cs:3098` |
| Lessons | `LessonsWindow.cs:381` | `LessonsWindow.cs:2696` |
| Onboarding | `OnboardingWindow.cs:371` | `OnboardingWindow.cs:1678` |
| PauseDuration | `PauseDurationDialog.cs:94` | `PauseDurationDialog.cs:335` |
| Settings | `SettingsWindow.cs:310` | `SettingsWindow.cs:1687` |
| ToggleNotif | `ToggleNotification.cs:60` | `ToggleNotification.cs:210` |
| UsageStats | `UsageStatsWindow.cs:142` | `UsageStatsWindow.cs:618` |
| VK | `VirtualKeyboard.cs:345` | `VirtualKeyboard.cs:1374` |
| Wnd (tray) | `TrayApplication.cs:178` | aucun — inchangé depuis la v1.1, la fenêtre tray vit le process |

**`TaskId` du `StartupTask`.** Inchangé, et aligné :

```
src/AutoStart.cs:238         private const string StartupTaskId = "AZERTYGlobalStartup";
msix/AppxManifest.xml:53       TaskId="AZERTYGlobalStartup"
```

`git show ad049fc:src/AutoStart.cs` porte la même ligne 238. Le lancement automatique
déjà enregistré par la v1.1 n'est donc **pas** orphelin à la mise à jour.

**Les phrases traduites (`06f997d`).** J'ai comparé `efff943` et `HEAD` sur les 15 fichiers
`Localization/`, en réinjectant `{Product}` → `AZERTY Global` et
`{ProductIdentity.SiteDomain}` → `azerty.global`, octet par octet. **Zéro écart de contenu**,
y compris les espaces insécables (`\xa0` préservé sur `L.Tray.cs:12`). Les 2 lignes qui
ressortaient portaient `ProductIdentity.Url("/soutien")`, qui vaut littéralement
`https://azerty.global/soutien`. L'affirmation « aucun texte visible ne change » tient.

### Régressions confirmées

Aucune.

### Soupçons non prouvés

- **Le scanner d'identité saute deux répertoires de production.**
  `scripts/list-identity-literals.py:52-56` a `SKIP_DIRS = {… "TypingEngine.Core",
  "TypingEngine.Windows", …}`. Ces deux-là ne sont plus des dossiers de test depuis
  `452aab0` : un littéral d'identité qui y reviendrait serait invisible. Aujourd'hui les
  seules occurrences y sont dans des commentaires (`KeyMapper.cs:1,6,106,328,504,637`,
  `ForegroundMonitor.cs:242`, `GameRegistry.cs:38`), que `strip_line_comment` couperait de
  toute façon. Latent, pas actif.
- **`AZERTYGlobalStartup` échappe au motif du scanner.** Le motif
  (`list-identity-literals.py:33-42`) connaît `AZERTYGlobalSingleInstance` en dur et
  `"AZERTYGlobal_[^"]*"` (avec underscore obligatoire), mais pas `AZERTYGlobalStartup`.
  L'identifiant le plus dangereux du lot — celui dont la dérive casse le démarrage
  automatique en silence — est le seul que ni `ProductIdentity` ni le scanner ne tiennent.
- **`const` → `static readonly`** sur `WND_CLASS_NAME` / `ClassName`
  (`LayoutConflictWindow.cs:25`, `LearningModule.cs:49`, `LessonsWindow.cs:7`,
  `PauseDurationDialog.cs:8`) : la chaîne n'est plus internée à la compilation mais allouée
  au tas et enracinée par le champ statique. `RegisterClassExW` / `UnregisterClassW`
  comparent par contenu, Windows copie le nom de classe : je ne vois pas de conséquence,
  mais je ne l'ai pas prouvé à l'exécution.

### Couverture de tests

- Le scanner tourne **en CI seulement** (`af745d3` touche `.github/workflows/ci.yml`, pas les
  `.csproj` : `grep "list-identity-literals" src/*.csproj` ne rend rien). « Runs on every
  build » veut dire « chaque run GitHub Actions », pas chaque `dotnet build` local.
- Je l'ai rejoué : exit 0, 9 déclarations listées, « Littéraux hors ProductIdentity : 0 ».
- Aucun test n'assert le nom du mutex, le `TaskId`, ni les 12 noms de classe fenêtre. Ma
  vérification est une comparaison de diff, pas un test rejouable — une dérive future ne
  serait vue que par le scanner, avec les deux trous ci-dessus.

---

## 2. Extraction `TypingEngine.Core` / `TypingEngine.Windows` (`452aab0`, `1b68caa`)

### Ce que le code fait (citations)

Le diff de `KeyMapper.cs` fait 255 lignes. Le cœur du changement de comportement est le
remplacement des ~50 lignes de composition de touche morte par un appel au moteur portable :

```
src/TypingEngine.Windows/KeyMapper.cs:597  // La composition est pure et portable ; l'émission reste dans l'adaptateur Windows.
src/TypingEngine.Windows/KeyMapper.cs:598  if (output.StartsWith("dk_", StringComparison.Ordinal) || _composition.ActiveDeadKey != null)
src/TypingEngine.Windows/KeyMapper.cs:599  {
src/TypingEngine.Windows/KeyMapper.cs:600      var result = _composition.Process(output);
src/TypingEngine.Windows/KeyMapper.cs:601      if (result.Text.Length > 0)
src/TypingEngine.Windows/KeyMapper.cs:602          EmitText(result.Text);
src/TypingEngine.Windows/KeyMapper.cs:603      if (result.StateChanged)
src/TypingEngine.Windows/KeyMapper.cs:604          StateChanged?.Invoke();
src/TypingEngine.Windows/KeyMapper.cs:605      return true;
src/TypingEngine.Windows/KeyMapper.cs:606  }
```

`EmitText` concatène **toute** la chaîne en un seul `INPUT[]` et l'envoie en **un seul**
`SendInput` :

```
src/TypingEngine.Windows/KeyMapper.cs:739  /// Tous les events d'une chaîne sont concaténés en un INPUT[] global puis envoyés
src/TypingEngine.Windows/KeyMapper.cs:740  /// en un seul SendInput pour atomicité (cf. plan v0.9.7 § Limites SendInput).
src/TypingEngine.Windows/KeyMapper.cs:783  if (inputs.Count > 0)
src/TypingEngine.Windows/KeyMapper.cs:784      _api.SendInput(inputs.ToArray());
```

**Ce qui est préservé, vérifié :**

- Le pass-through des lettres identiques avec Verr. Maj. actif — le correctif « préserver K
  sur YouTube » — est **intact et hors du diff** :
  `src/TypingEngine.Windows/KeyMapper.cs` (`CanPassThrough`) teste
  `if (upper >= 'A' && upper <= 'Z' && vkCode == upper) return true;` **avant**
  `if (_capsLockState) return false;`. Aucune ligne de `CanPassThrough` n'apparaît dans le
  diff. Test associé vert :
  `KeyMapperPassThroughLayoutTests.ProcessKey_CapsLockActive_PassesThroughMatchingAsciiLetterForWebShortcuts:110`.
- La résolution positionnelle pour les dispositions sous-jacentes non-AZERTY
  (`KeyMapper.cs:328`, `:504`) tombe **entre** deux hunks du diff : inchangée. Tests verts :
  `ProcessKey_QwertyForeground_DoesNotPassThroughAzertyPositions:24`,
  `ProcessKey_CtrlD01_QwertyForeground_EmitsCtrlAInsteadOfPassingCtrlQ:69`.
- `WM_SYSKEYDOWN` / `WM_SYSKEYUP` : `KeyboardHook.cs:161-162` est hors hunk, la ligne
  `msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN` est celle de la v1.1. `WM_SYSCHAR` n'existe
  pas dans le moteur — il n'est traité que par les wndproc de l'application
  (`LearningModule.cs:1342`, `LessonsWindow.cs:798`, `SettingsWindow.cs:1216`), tous hors du
  périmètre extrait.
- **Aucune construction du moteur sans hôte réel en production.** Les trois sites passent
  l'adaptateur :
  `TrayApplication.cs:310` `new KeyMapper(layout, _win32Api, _typingHost)`,
  `:313` `new KeyboardHook(_mapper, _typingHost)`,
  `:353` `new ForegroundMonitor(_win32Api, _hWnd, _typingHost)`.
  Le risque que `NullWindowsTypingHost` (raccourcis à 0, overrides de compatibilité ignorés,
  stats perdues, journal muet) atterrisse en production n'est pas réalisé.
- `AzertyGlobalWindowsTypingHost.cs:8-24` délègue les 9 membres un pour un à
  `ConfigManager` / `UsageStats`, sans transformation.
- `SyncState()` passe de `Win32.GetKeyState(0x14)` à `_api.GetKeyState(0x14)` ;
  `RealWin32Api.cs:15` est `public short GetKeyState(int vk) => Win32.GetKeyState(vk);` —
  même P/Invoke, `[DllImport("user32.dll")]` dans les deux classes `Win32`. Sans effet en
  production, testable désormais.
- **Le modificateur Win collé se répare avant d'être lu.** `CleanupStaleModifiers()` est
  appelé en `KeyMapper.cs:456`, le nouveau garde `if (_leftWinDown || _rightWinDown)` en
  `:497`. La resynchronisation par `GetAsyncKeyState` (`:410-413`) précède donc l'usage :
  un keyup de Win avalé par Windows (Win+L, Win+Tab) se corrige à la frappe suivante. Pas
  de blocage durable du remapping.

### Régressions confirmées

**2.1 — Deux `SendInput` sont devenus un, sur le chemin « touche morte + caractère non
composable ».** C'est le seul changement de comportement observable du refactor.

Avant (`git show 452aab0^:src/KeyMapper.cs`, branche `_activeDeadKey != null`) :

```
var isolatedChar = dk.GetIsolated();
if (isolatedChar != null)
    EmitText(isolatedChar);
EmitText(output);
```

Après :

```
src/TypingEngine.Core/CompositionEngine.cs:31  string text = deadKey.Apply(output) ?? string.Concat(deadKey.GetIsolated(), output);
```

`´` puis `.` partait en **deux** lots `SendInput` d'un caractère ; il part maintenant en
**un** lot de deux caractères. Trois conséquences mesurables :

1. `_foregroundMonitor.GetEmitContext()` (`KeyMapper.cs:750`) est lu **une** fois au lieu de
   deux : le mode de compatibilité et le `hkl` ne peuvent plus changer entre les deux
   caractères. Plutôt un gain d'atomicité.
2. En mode `NativeCombo`, les séquences de modificateurs des deux caractères sont désormais
   entrelacées dans un unique lot `SendInput`. Je n'ai pas de contre-exemple, mais ce n'est
   plus la séquence que la v1.1 envoyait.
3. `_host.RecordEmittedText(text)` (`KeyMapper.cs:747`) reçoit **un** appel de 2 caractères
   au lieu de deux appels d'un caractère. Comme `UsageStats` est neuf en v1.2, il n'y a pas
   de base v1.1 à contredire — mais le comptage dépend de cette granularité.

Le texte final produit est identique. Ce n'est donc pas une régression fonctionnelle
prouvée ; c'est un changement de comportement réel sur le chemin de frappe, non annoncé, et
que la suite de tests ne peut pas voir (cf. § Couverture).

**2.2 — Le cas « touche morte armée dont le nom est absent de `dead_keys` » ne retombe plus
sur le pass-through.**

Avant, quand `dk == null`, le `if (dk != null)` était faux et le flux **traversait** vers
`CanPassThrough(...)` puis le chemin de caractère normal. Après :

```
src/TypingEngine.Core/CompositionEngine.cs:28  if (!_layout.DeadKeys.TryGetValue(activeName, out var deadKey))
src/TypingEngine.Core/CompositionEngine.cs:29      return new CompositionResult(output, true);
```

et l'appelant fait `EmitText(result.Text); return true;` sans jamais passer par
`CanPassThrough`. La touche est donc injectée au lieu d'être laissée passer nativement —
ce qui casse la compatibilité jeux/DirectInput sur cette frappe.

**Atteignabilité** : il faut une disposition qui déclare `dk_x` sur une couche de touche
sans entrée `dk_x` dans `dead_keys`. Ni le parseur ni le schéma ne l'interdisent :
`LayoutJsonParser.cs:54-71` n'effectue aucun contrôle référentiel, et
`schemas/azerty-layout.schema.json:74` ne contraint que la **forme** des noms
(`"propertyNames": { "pattern": "^dk_[a-z0-9_]+$" }`). La disposition embarquée étant
verrouillée par `ResourceAlignmentTests`, le cas n'est pas atteignable en production **par
la ressource livrée**. Régression réelle, gravité faible.

**2.3 — L'ordre `EmitText` / `StateChanged` est inversé sur le chemin « touche morte +
caractère composable ».**

Avant : `_activeDeadKey = null; StateChanged?.Invoke();` **puis** `EmitText(transformed);`.
Après (`KeyMapper.cs:600-604`) : `EmitText` **puis** `StateChanged`. Les abonnés sont
`LearningModule.cs:440`, `LessonsWindow.cs:165`, `TrayApplication.cs:311` — tous repeignent
de l'interface. Le caractère est donc injecté avant que le clavier virtuel ne se rafraîchisse.
Confirmé comme inversion ; conséquence utilisateur non démontrée.

### Soupçons non prouvés

- Le tout-nouveau garde `if (_leftWinDown || _rightWinDown) return false;`
  (`KeyMapper.cs:497`) rend **tout** remapping inerte tant qu'une touche Windows est vue
  enfoncée. La resynchronisation le protège d'un état collé durable (voir ci-dessus), mais
  je n'ai pas pu prouver qu'aucune séquence Win+… n'est ratée pendant l'intervalle entre le
  keydown de Win et la première frappe non-modificateur suivante.
- `StartsWith("dk_")` sans comparateur (culture courante) → `StringComparison.Ordinal`. Va
  dans le sens de la correction, mais c'est un changement de sémantique de comparaison que
  je n'ai pas testé sur une culture exotique.
- Duplication de la couche `Win32` : `GetKeyState`, `MapVirtualKeyExW`, `ToUnicodeEx`,
  `GetKeyboardLayout` existent maintenant dans `src/Win32.cs` **et** dans
  `src/TypingEngine.Windows/Win32.cs`. Les déclarations `DllImport` que j'ai comparées sont
  équivalentes ; rien n'empêche une divergence future, et aucun test ne l'attraperait.

### Couverture de tests

- **2.1 n'est couvert par aucun test, et le test qui devrait le voir est structurellement
  aveugle.** `DeadKeyAndSmartCapsTests.ProcessKey_DeadKeyThenUnmappedChar_EmitsIsolatedThenCharacter:104`
  assert `Assert.Equal("´.", EmittedText(mock))`. Or l'assistant
  `EmittedText` (`DeadKeyAndSmartCapsTests.cs:42-51`) itère `mock.AllInputs`, et
  `MockWin32Api.cs:22` définit `AllInputs => SendInputCalls.SelectMany(b => b).ToArray()` :
  **les lots sont aplatis**. Un lot de 2 caractères et deux lots d'un caractère rendent la
  même chaîne. Le mock enregistre bien `SendInputCalls` par lot
  (`MockWin32Api.cs:19`, `:90`), mais aucun test de touche morte n'assert
  `SendInputCalls.Count`. C'est exactement le motif « tests verts qui n'ont rien vu ».
- Le test frère côté portable,
  `CompositionEngineTests.DeadKeyThenUnsupportedCharacter_EmitsIsolatedMarkAndCharacter:22`,
  teste la **nouvelle** sémantique (une chaîne concaténée) : il valide le nouveau contrat,
  il ne prouve pas l'équivalence avec la v1.1.
- **2.2 n'est couvert par aucun test.** Aucun test n'arme un `dk_` absent de `dead_keys`,
  ni côté `CompositionEngineTests` (3 tests) ni côté `DeadKeyAndSmartCapsTests`.
- **2.3 n'est couvert par aucun test.** Aucun test n'observe l'ordre relatif de
  `StateChanged` et de `SendInput`.
- **Le pass-through Win+`<touche>` et l'exclusion de Win dans `IsToggleShortcut` sont
  entièrement non testés** : `grep -rl "VK_LWIN\|_leftWinDown\|0x5B"` ne rend **aucun**
  fichier de test dans les trois suites. C'est du code neuf sur le chemin de frappe.
- Ce qui **est** bien couvert : le pass-through positionnel (5 tests), Ctrl+Espace
  synthétique (2 tests), la composition nominale (3+3 tests), `EmitText` par mode de
  compatibilité (5 tests, avec `Assert.Single(mock.SendInputCalls)` — donc le lot unique
  y est assert, mais seulement pour un caractère isolé).

---

## 3. Sollicitation d'avis (`666fb91`, `32ce6d5`, `7ad768a`)

### Ce que le code fait (citations)

**État persistant** (`src/ConfigManager.cs`) :

| Clé | Accès | Écriture |
|---|---|---|
| `reviewPromptCount` (0-2) | `:206-215`, avec migration `reviewPromptDone → 1` | `:239-251` |
| `reviewPromptLastShown` (date **UTC**, `yyyy-MM-dd`) | `:217-227` | `:246-247` |
| `reviewPromptClicked` | `:232` | `:252` |
| `reviewPromptDone` (legacy, conservé pour un retour arrière v1.1) | `:213` | `:248` |

**État volatil :**

```
src/ConfigManager.cs:265   private static DateTime? _lastErrorUtc;
src/TrayApplication.cs:151 private DateOnly? _reviewPromptShownDate;
```

**Deux machines distinctes, pas une.**

`MaybeShowReviewPrompt()` (`TrayApplication.cs:1633-1701`) — chemin notification :
`NotificationsEnabled` → `already >= 2 || ReviewPromptClicked` (`:1640`) → cooldown 48 h
(`:1643-1645`) → `FirstRemapDate != null` (`:1649`) → essai 1 : `activeDays >= 3` et
`today - firstRemap >= 3` (`:1657-1658`) ; essai 2 : `activeDays >= 10`, `today - lastShown
>= 7` **si `lastShown` est connu** (`:1665-1668`), `today - lastActive <= 3` (`:1670-1672`)
→ `RecordReviewPromptShown()` (`:1675`) → `_reviewPromptShownDate = today` (`:1676`) →
toast COM ou balloon.

`MaybeShowReviewAfterShare(source)` (`TrayApplication.cs:1777-1808`) — chemin partage,
appelé par `OnChallengeShared():1770` et `OnStatsShared():1775` :
`IsPackaged` → `ReviewPromptClicked` → `ReviewPromptCount >= 2` → cooldown 48 h →
`if (_reviewPromptShownDate == today) return;` (`:1791`) → `StoreReview.TryShow` (`:1793`)
→ `RecordReviewPromptShown()` (`:1804`) → `_reviewPromptShownDate = today` (`:1805`).

**Points d'entrée réels**, exhaustifs :

| Entrée | Site | Chemin |
|---|---|---|
| Démarrage (pas d'accueil) | `TrayApplication.cs:287` | `MaybeShowReviewPrompt` |
| Fermeture de l'accueil (relais différé) | `TrayApplication.cs:971`, garde `_reviewPromptDeferred:969` | `MaybeShowReviewPrompt` |
| Partage d'un résultat de défi | `TrayApplication.cs:1770` ← `LessonsWindow.ChallengeShared:999` | `MaybeShowReviewAfterShare` |
| Copie depuis « Mes statistiques » | `TrayApplication.cs:1775` | `MaybeShowReviewAfterShare` |
| Menu tray « Noter sur le Microsoft Store » | `TrayApplication.cs:584` → `OnRateStoreFromMenu:1740` | ni essai ni compteur : pose `reviewPromptClicked` puis ouvre |
| Clic sur la balloon | `TrayApplication.cs:503-504` → `OpenReviewTarget` | pose `reviewPromptClicked` |
| Clic sur le toast (COM re-routé) | `TrayApplication.cs:524-531` | pose `reviewPromptClicked` |

### Régressions confirmées

**3.1 — « Une seule sollicitation par jour » n'est pas tenue : le garde du chemin de partage
est en mémoire et se réinitialise à chaque relance.**

`MaybeShowReviewAfterShare` n'a qu'un seul garde journalier, `TrayApplication.cs:1791` :

```
if (_reviewPromptShownDate == today) return;
```

`_reviewPromptShownDate` est un champ d'instance `DateOnly?` (`TrayApplication.cs:151`),
donc `null` à chaque démarrage. La date **persistée** (`ConfigManager.ReviewPromptLastShown`,
`ConfigManager.cs:217`) existe et n'est **jamais lue** sur ce chemin.

Séquence, sans état inventé :

1. Jour D, `reviewPromptCount = 0`. L'utilisateur copie son résultat de défi →
   `MaybeShowReviewAfterShare` passe tous les gardes → boîte de notation affichée →
   `count = 1`, `reviewPromptLastShown = D`, `_reviewPromptShownDate = D`.
2. Le même jour D, l'application est relancée (fermeture manuelle, redémarrage, ouverture
   de session). `_reviewPromptShownDate` repart à `null`.
3. Second partage le même jour D : `count = 1 < 2` ✓, `clicked = false` ✓, cooldown ✓,
   `_reviewPromptShownDate == today` → **faux** → **seconde boîte de notation le même jour**,
   `count = 2`.

Les **deux** essais de toute la vie de l'installation sont consommés le même jour, ce que le
plancher de 7 jours de `MaybeShowReviewPrompt:1667` était censé empêcher. Le correctif est
d'une ligne : comparer à `ConfigManager.ReviewPromptLastShown` au lieu du champ en mémoire.

**3.2 — « Aucune sollicitation dans les 48 heures qui suivent une erreur journalisée » ne
survit pas à une relance, et est inerte au point d'entrée principal.**

`ConfigManager.cs:265` : `private static DateTime? _lastErrorUtc;`, alimenté seulement par
`Log(...)`, non persisté — et le commentaire (`:253-263`) assume ce choix. Deux effets :

- Une erreur journalisée hier n'existe plus aujourd'hui : le silence de 48 h ne couvre en
  réalité que la durée de vie du process.
- Le point d'entrée principal est le **constructeur** de `TrayApplication`
  (`TrayApplication.cs:287`). À cet instant, `_lastErrorUtc` ne peut être non nul que si une
  erreur a été journalisée pendant ce même démarrage. Le garde annoncé est donc
  structurellement muet là où il compte le plus.

**3.3 — L'essai est consommé avant toute preuve d'affichage, contrairement au commentaire.**

`TrayApplication.cs:1793-1804` :

```
if (!StoreReview.TryShow(_hWnd, StoreReviewUrl)) return;
// Consommé seulement une fois la boîte réellement affichée : contrairement à la
// notification, dont l'échec est invisible, l'API répond ici tout de suite.
ConfigManager.RecordReviewPromptShown();
```

Or `StoreReview.TryShow` rend `true` dès que le handler `Completed` est **câblé**
(`src/StoreReview.cs:49-52`), avant tout affichage :

```
src/StoreReview.cs:49  var operation = context.RequestRateAndReviewAppAsync();
src/StoreReview.cs:50  operation.Completed = (asyncOperation, status) =>
src/StoreReview.cs:51      OnCompleted(asyncOperation, status, fallbackUrl);
src/StoreReview.cs:52  return true;
```

Le commentaire est faux : l'API ne « répond pas tout de suite ». Un échec asynchrone
(`NetworkError`, `Error`, `status != Completed`) est traité dans `OnCompleted` **après** que
l'essai a été décompté. Écart entre le contrat écrit et le code.

**3.4 — Mélange UTC / heure locale sur le plancher de 7 jours.**

`RecordReviewPromptShown` écrit `DateTime.UtcNow` (`ConfigManager.cs:246-247`) ;
`MaybeShowReviewPrompt` compare contre `DateOnly.FromDateTime(DateTime.Now)`
(`TrayApplication.cs:1652`), heure **locale**. En France (UTC+1/+2), entre minuit et 2 h
locales, la date UTC est celle de la veille : décalage d'un jour sur un plancher de 7.
Gravité faible, mais réel.

### Soupçons non prouvés

- **Le propriétaire de la boîte diffère selon le chemin.** `OpenStoreReview:1726` utilise
  `ReviewOwnerWindow():1755-1759`, qui préfère une fenêtre visible ; `MaybeShowReviewAfterShare:1793`
  passe `_hWnd` en dur — la fenêtre tray, **masquée**. Combiné à 3.3, une boîte qui ne
  s'afficherait pas sur un propriétaire masqué brûlerait un essai en silence. Je n'ai pas pu
  le prouver sans exécution en package.
- **« Deux essais au maximum » : je n'ai pas trouvé de chemin qui dépasse 2.** Le plafond est
  double, `Math.Min(ReviewPromptCount + 1, 2)` (`ConfigManager.cs:243`) et le garde
  `>= 2` aux deux entrées. Je n'ai pas pu exclure le cas où `Save()` échoue silencieusement
  (`ConfigManager.cs:794-800` : un `_loadFailed` avec un `config.json` présent **retourne
  sans écrire** en se contentant de journaliser), qui laisserait `reviewPromptCount` à sa
  valeur d'avant et rendrait l'essai réutilisable.
- Le chemin de partage n'applique **aucun** seuil d'usage (ni `activeDays`, ni délai depuis
  la première frappe). Un utilisateur du jour 1 qui partage son défi reçoit la boîte
  immédiatement. Le commentaire `:1762-1765` l'assume explicitement — c'est un écart
  documenté avec les règles d'en-tête du Changelog, pas un bug.

### Couverture de tests

- `ReviewPromptConfigTests` (11 tests) couvre bien la **couche de persistance** :
  `RecordReviewPromptShown_IncrementsAndPersists:84`, `_CapsAtTwo:99`,
  `ReviewPromptCount_MigratesLegacyDoneFlagToOne:111`,
  `_ExplicitCountWinsOverLegacyFlag:124`, `SetReviewPromptClicked_PersistsTrue:133`.
  Elle teste la chaîne réelle (`ConfigManager`, fichier de config temporaire).
- **Aucun test ne couvre 3.1, 3.2, 3.3 ni 3.4.** La logique de décision vit dans deux
  méthodes privées de `TrayApplication`, qui exige une fenêtre : elle n'est pas extraite en
  règle pure, contrairement à `AutoStartNudge` et `TrainingReminders`. Le garde journalier,
  le cooldown d'erreur et l'ordonnancement des deux chemins sont hors de portée des tests
  actuels.
- C'est le trou de couverture le plus large de l'axe : une logique refaite trois fois en deux
  jours, sans un seul test sur la machine à états elle-même.

---

## 4. `AutoStartNudge` (`02d5458`, `7ad768a`)

### Ce que le code fait (citations)

`src/AutoStartNudge.cs:38-47` — décision pure, sans effet de bord, sur un
`readonly record struct AutoStartSignals` (`:23-28`) :

```
if (s.NudgeDone) return false;
if (s.AlreadyRegistered) return false;
if (!s.NotificationsEnabled) return false;
if (s.FirstRemapDate == null) return false;
if (s.ActiveDaysCount < MinActiveDays) return false;
return true;
```

`Snapshot():50-55` fait les 5 lectures d'état ; `MarkPromptShown():59` fait la seule
écriture. La pureté est réelle : `ShouldPrompt` ne lit ni `ConfigManager`, ni `AutoStart`,
ni `UsageStats`, ni l'horloge. Modèle `TrainingReminders.ShouldRemind` respecté.

`MaybeShowAutoStartNudge()` (`TrayApplication.cs:1054-1078`) marque **avant** d'afficher
(`:1062`), donc une balloon perdue ne rouvre pas la porte. « Une seule fois sur la vie de
l'installation » est tenu par `autoStartNudgeDone` (`ConfigManager.cs:475-476`).

**Rien ne s'active sans clic** : `EnableAutoStartFromPrompt():1082` n'est atteignable que
depuis un clic — `NIN_BALLOONUSERCLICK` (`TrayApplication.cs:514`) ou `WM_APP_TOAST`
(`:531`). La décision v0.9.7.1 (« aucune case jamais vue n'est persistée en silence ») est
respectée.

### Régressions confirmées

**4.1 — Un choix manuel ne l'éteint que depuis le menu tray. Depuis Paramètres ou
l'accueil, la relance se réémet.**

Le Changelog annonce « un choix manuel dans le menu l'éteint dans un sens comme dans
l'autre ». C'est vrai du menu tray :

```
src/TrayApplication.cs (ToggleAutoStart) : bool target = !AutoStart.IsRegistered;
                                            if (!AutoStart.Set(target)) { … return; }
                                            AutoStartNudge.MarkPromptShown();
```

Ce n'est vrai d'**aucun** des deux autres chemins d'activation, qui appellent `AutoStart.Set`
sans jamais `MarkPromptShown()` :

```
src/SettingsWindow.cs:754    bool autoStartSaved = AutoStart.Set(autoStart);
src/OnboardingWindow.cs:681  bool autoStartSaved = AutoStart.Set(autoStartState == (IntPtr)BST_CHECKED);
```

Le sens « activer » est couvert par accident, parce que `ShouldPrompt` teste
`s.AlreadyRegistered`. Le sens « **refuser** » ne l'est pas :

- L'utilisateur ouvre Paramètres et **décoche** « Lancer au démarrage de Windows ».
  `AutoStart.Set(false)` s'exécute, `autoStartNudgeDone` reste `false`.
- Deux jours d'usage distincts plus tard, `ShouldPrompt` rend `true` et l'application lui
  propose par toast d'activer exactement ce qu'il vient de désactiver.

Même chose pour qui atteint l'étape 3 de l'accueil et **décoche** la case pré-cochée :
son refus explicite n'éteint pas la relance. C'est le cas « où la relance se réémettrait »,
et le seul écart confirmé entre la règle annoncée et le code.

### Soupçons non prouvés

- `MarkPromptShown()` → `SetBool` → `Save()`. Si `Save()` sort par la branche
  `_loadFailed && File.Exists(_configPath)` (`ConfigManager.cs:797-801`), l'écriture est
  ignorée avec une simple ligne de journal, et la proposition redeviendrait unique-par-
  session au lieu d'unique-par-installation. Même soupçon que 3.x, non prouvé.

### Couverture de tests

- `AutoStartNudgeTests` : 9 tests sur `ShouldPrompt` (`:44`, `:50`, `:56`, `:64`, `:72`,
  `:80`, `:89`), `AutoStartNudgeDone_DefaultsToFalse:95`,
  `MarkPromptShown_PersistsAndSuppressesFurtherPrompts:101`, `MinActiveDays_IsTwo:113`.
  Ils testent la chaîne réelle (vrai `ConfigManager` sur config temporaire), pas une valeur
  inventée.
- **Aucun ne peut voir 4.1.** Ce sont des tests de la fonction pure ; l'oubli est un **site
  d'appel manquant** dans `SettingsWindow` et `OnboardingWindow`. Un test qui couvrirait la
  régression devrait asserter que tout chemin appelant `AutoStart.Set` appelle aussi
  `MarkPromptShown` — il n'en existe aucun, et la structure actuelle (logique dans des
  wndproc) ne le permet pas sans extraction.

---

## 5. `StoreContext.RequestRateAndReviewAppAsync` (`32ce6d5`)

### Ce que le code fait (citations)

```
src/StoreReview.cs:39  if (!ConfigManager.IsPackaged) return false;
src/StoreReview.cs:40  if (owner == IntPtr.Zero) return false;
src/StoreReview.cs:44  var context = StoreContext.GetDefault();
src/StoreReview.cs:45  if (context == null) return false;
src/StoreReview.cs:47  WinRT.Interop.InitializeWithWindow.Initialize(context, owner);
src/StoreReview.cs:49  var operation = context.RequestRateAndReviewAppAsync();
src/StoreReview.cs:50  operation.Completed = …
src/StoreReview.cs:54  catch (Exception ex) { ConfigManager.Log("StoreReview.TryShow", ex); return false; }
```

**Hors package** : `ConfigManager.IsPackaged` court-circuite avant tout appel WinRT.
L'appelant retombe bien sur le lien profond — `OpenStoreReview():1727-1728` :
`if (StoreReview.TryShow(...)) return; Win32.ShellExecuteW(..., StoreReviewUrl, ...)`. ✅

**Interblocage** : évité, et délibérément. Aucun `GetAwaiter().GetResult()` : le résultat
passe par `operation.Completed`. Le commentaire `:32-35` documente précisément pourquoi le
motif de `AutoStart` (`AutoStart.cs:252` `GetAsync(...).GetAwaiter().GetResult()`) serait
fatal ici. ✅

**Fil d'exécution** : tous les chemins atteignent `TryShow` depuis la boucle de messages.
Le seul chemin à risque était l'activation COM, arrivant sur un thread RPC — il est
re-routé :

```
src/TrayApplication.cs:223       // Le callback arrive sur un thread RPC COM → re-routage PostMessage vers le
src/TrayApplication.cs:233           Win32.PostMessageW(_hWnd, WM_APP_TOAST, …);
src/TrayApplication.cs:524       case WM_APP_TOAST:
```

`ToastActivation.Activate` (`src/ToastActivation.cs`) ne fait que lever l'événement ; c'est
`TrayApplication` qui `PostMessage`. ✅

**Exception si le Store est absent ou coupé par stratégie** : le `catch (Exception)`
(`:54-60`) est large, journalise et rend `false` → repli sur `ms-windows-store://review/`.
`OnCompleted:79-91` gère `status != Completed` et `NetworkError`/`Error` en rouvrant le lien
profond ; `CanceledByUser` et `Succeeded` sont volontairement silencieux. ✅

**`MinVersion`** : `msix/AppxManifest.xml:26` déclare
`MinVersion="10.0.17763.0"` (Windows 10 1809), et `src/AZERTYGlobal.csproj:5` cible
`net8.0-windows10.0.17763.0`. **Preuve depuis le dépôt** : la compilation Release complète
réussit (0 warning) alors que `StoreReview.cs` est bien dans les sources compilées
(`AZERTYGlobal.csproj:57-65` n'exclut que les projets frères et `TestSupport`). Donc
`RequestRateAndReviewAppAsync` **existe dans la projection CsWinRT du contrat 17763** : le
`MinVersion` déclaré n'est pas trop bas. ✅

### Régressions confirmées

Aucune sur les cinq points demandés. Le seul défaut de ce fichier est **3.3** (le `true`
rendu par `TryShow` ne prouve pas l'affichage), et il se matérialise chez l'appelant.

### Soupçons non prouvés

- `MaybeShowReviewAfterShare` passe la fenêtre tray **masquée** comme propriétaire, là où
  `ReviewOwnerWindow()` existe précisément pour préférer une fenêtre visible (voir 3.x).
  Comportement d'une boîte UWP sur un propriétaire masqué : non vérifié.
- `operation.Completed` posé après le retour éventuel de l'opération : WinRT invoque alors le
  handler immédiatement, sur le thread appelant — donc `Win32.ShellExecuteW` depuis le thread
  UI plutôt que depuis un thread de pool. Sans conséquence apparente, non testé.
- Le commentaire `:64-66` affirme que `OnCompleted` tourne « sur un thread de pool, jamais
  sur le thread d'interface ». Vu le point précédent, ce n'est pas garanti.

### Couverture de tests

- `ToastActivationTests` existe et passe, mais couvre l'activateur COM, pas `StoreReview`.
- **Aucun test ne touche `StoreReview`.** C'est attendu : l'API exige une identité de package
  et une boucle de messages. La conséquence est que les quatre comportements demandés
  (hors package, thread, non-blocage, exception) ne sont prouvés que par lecture de code et
  par le succès de la compilation — pas par exécution. Tout ici doit passer par le smoke test.

---

## Étapes de smoke test que ces risques imposent

Chaque étape est formulée comme une action, avec le résultat attendu. Les six premières
couvrent des régressions **confirmées**.

**A. Deux sollicitations le même jour (régression 3.1).** Sur une installation packagée
neuve : jouer le défi du jour, copier le résultat, fermer la fenêtre → la boîte de notation
s'affiche. **Quitter l'application par le menu tray, la relancer.** Rejouer le défi, copier,
fermer.
→ *Attendu si le bug est réel* : une **seconde** boîte de notation le même jour, et
`%LocalAppData%\AZERTY Global\config.json` avec `"reviewPromptCount": 2`.
→ *Attendu après correctif* : rien ne s'affiche, `reviewPromptCount` reste à 1.

**B. Relance de l'autostart après un refus explicite (régression 4.1).** Ouvrir Paramètres,
**décocher** « Lancer au démarrage de Windows », valider. Vérifier `autoStartNudgeDone`
absent ou `false` dans `config.json`. Utiliser l'application deux jours distincts.
→ *Attendu si le bug est réel* : un toast propose d'activer le lancement automatique.
→ *Attendu après correctif* : aucune proposition ; `autoStartNudgeDone: true` dès la
validation des Paramètres.

**C. Idem depuis l'accueil (régression 4.1, second chemin).** Réinitialiser l'accueil,
aller jusqu'à l'étape 3, **décocher** la case de lancement automatique, fermer. Deux jours
d'usage plus tard.
→ *Attendu* : aucune proposition.

**D. Silence de 48 h après une erreur (régression 3.2).** Provoquer une erreur journalisée
(rendre `config.json` illisible puis le restaurer, ou couper le dossier de config),
constater la ligne dans `error.log`. **Redémarrer l'application.** Déclencher les conditions
d'un essai (partage d'un défi).
→ *Attendu si le bug est réel* : la sollicitation s'affiche malgré l'erreur d'il y a cinq
minutes.

**E. Composition de touche morte non composable, un seul lot (régression 2.1).** Avec un
moniteur d'entrée (ou dans un jeu en mode `NativeCombo`), taper `´` puis `.`.
→ *Attendu* : `´.` s'affiche correctement, dans le bon ordre, sans caractère perdu ni
inversé, y compris dans une application plein écran DirectInput. C'est le seul point de ce
lot que le passage de deux `SendInput` à un pourrait faire dévier.

**F. Ordre d'affichage clavier virtuel / caractère (régression 2.3).** Clavier virtuel
ouvert, taper une touche morte puis une lettre composable (`´` + `e`).
→ *Attendu* : `é` s'insère et la surbrillance de touche morte s'éteint ; aucun scintillement,
aucune touche morte restée affichée comme armée.

**G. Mutex d'instance unique à la mise à jour (point 1, non-régression à confirmer).**
Installer la v1.1.0, la laisser tournée, installer la v1.2.0 par-dessus.
→ *Attendu* : une seule instance vivante, et un second lancement affiche
« AZERTY Global est déjà en cours d'exécution ». Une seconde icône tray, ou un refus de
démarrage, contredirait ma vérification.

**H. `StartupTask` déjà enregistré à la mise à jour (point 1).** Sur une installation v1.1
avec le lancement automatique **activé**, mettre à jour vers la v1.2.0 et redémarrer Windows.
→ *Attendu* : l'application revient au démarrage, et la case « Lancer au démarrage » est
cochée dans Paramètres.

**I. Boîte de notation, chemin partage, sans fenêtre visible (soupçon 3.x / 5.x).** Sur une
installation packagée, copier ses statistiques puis **fermer** la fenêtre « Mes statistiques »
avant que la boîte n'apparaisse.
→ *Attendu* : la boîte s'affiche au premier plan. Si elle n'apparaît pas alors que
`reviewPromptCount` a été incrémenté, le propriétaire masqué (`_hWnd`) est bien le problème.

**J. Store désactivé par stratégie d'entreprise (point 5).** Sur une machine avec le Store
bloqué par GPO, déclencher « Noter sur le Microsoft Store » depuis le menu tray.
→ *Attendu* : aucun crash, un message ou rien, et `error.log` contenant
`StoreReview.TryShow`. Le repli `ms-windows-store://review/` peut échouer visiblement —
c'est acceptable, l'inertie silencieuse ou le crash ne le sont pas.

**K. Hors package (point 5).** Lancer le binaire non empaqueté et déclencher la même entrée
de menu.
→ *Attendu* : `TryShow` rend `false` sans appel WinRT et le lien profond s'ouvre (ou échoue
proprement) ; aucune trace dans `error.log`, puisque `IsPackaged` court-circuite avant le
`try`.

**L. Touche Windows sur le chemin de frappe (soupçon 2.x, code non testé).** Faire Win+L,
déverrouiller, taper immédiatement `é`. Puis Win+Tab, `Échap`, taper `é`. Puis
Win+Ctrl+Maj+`Verr.Maj`.
→ *Attendu* : `é` sort remappé dans les deux premiers cas dès la première frappe ; la
troisième combinaison ne bascule **pas** le remapping.

---

## Ce que je n'ai pas pu vérifier

1. **Toute exécution en package.** Le dépôt ne contient pas de MSIX installé. `IsPackaged`,
   `StoreContext.GetDefault()`, `InitializeWithWindow`, `RequestRateAndReviewAppAsync`,
   l'activateur COM de toast et `StartupTask.GetAsync` ne sont vérifiés que par lecture et
   par le succès de la compilation.
2. **La version d'introduction réelle de `RequestRateAndReviewAppAsync`** côté Microsoft.
   Ce que je prouve est plus faible mais suffisant : l'API est présente dans la projection du
   contrat 17763, puisque le projet compile contre ce TFM. Je n'ai pas vérifié qu'elle
   *fonctionne* sur une machine réellement en 1809.
3. **Le comportement d'une boîte UWP sur une fenêtre propriétaire masquée** (soupçon 3.x
   / 5.x). Non déterminable sans exécution.
4. **La séquence `SendInput` réellement reçue par une application cible** pour la régression
   2.1. Le mock aplatit les lots, et je n'ai pas instrumenté un vrai `SendInput`. Je prouve
   que le nombre de lots change ; je ne prouve pas qu'une application le remarque.
5. **`ConfigManager.Save()` en échec.** J'ai lu la branche `_loadFailed` qui retourne sans
   écrire (`ConfigManager.cs:797-801`), mais je n'ai pas construit le scénario où un compteur
   d'essai est réutilisé faute de persistance. Soupçon commun à 3.x et 4.x.
6. **Le comportement observable de la régression 2.2** : je n'ai pas fabriqué de disposition
   avec un `dk_` orphelin pour l'exécuter, seulement établi par lecture du parseur
   (`LayoutJsonParser.cs:54-71`) et du schéma (`azerty-layout.schema.json:74`) que rien ne
   l'interdit.
7. **Les commits hors de mon axe** : `6fc9b21` (validation par schéma), `02d6935` /
   `697d1a3` (contrôles de ressource embarquée), `7d19b2c` (recalcul des compteurs),
   `a0b11f8` (script de synchronisation), `5cdde01` / `27541e1` (empaquetage et version),
   `3b7501e` / `8050f42` / `825a453` (snapshot Store, `.gitignore`). Je ne les ai touchés
   que là où ils éclairaient un point chaud.
8. **La publication Native AOT.** Je n'ai lancé que `dotnet build`, jamais `dotnet publish`
   avec l'AOT — donc rien sur le comportement du marshalling WinRT ou des `DllImport`
   dupliqués une fois compilés en natif.
