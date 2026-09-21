# Déclencheur de la sollicitation d'avis — v1.3.0

**Décision d'Antoine du 2026-09-21, par QCM.** La sollicitation d'avis ne part plus sur un
seuil de calendrier, mais sur la preuve que l'utilisateur a tapé des caractères qu'AZERTY
Global apporte et que l'AZERTY traditionnel de Windows ne donne pas.

Portée : le chemin **notification** (`TrayApplication.MaybeShowReviewPrompt`) uniquement.
⛔ Le chemin **partage** (`ReviewSharePrompt.ShouldPrompt`) reste inchangé — décision
explicite : un partage est volontaire, il n'interrompt personne, ses seuils actuels
(première frappe remappée + 2 jours actifs) restent légitimes.

## Les quatre décisions

| # | Décision | Valeur retenue |
|---|---|---|
| D1 | Seuil de caractères enrichis | **20**, sur `UsageStats.TotalSpecialCharsCount` |
| D2 | Moment de l'affichage | **15 s après la dernière frappe remappée** |
| D3 | Seuils de jours de l'essai 1 | **remplacés**, pas cumulés |
| D4 | Chemin partage | **inchangé** |

### D1 — pourquoi 20 et pas 1

Le signal existe déjà et il est exactement le bon : `UsageStats.TryCategorize`
(`src/UsageStats.cs:216`) **exclut explicitement** ce que l'AZERTY traditionnel offre déjà —
minuscules accentuées françaises, symboles gravés `€ £ µ § ° ² ¤ ¨`. Ce que compte
`TotalSpecialCharsCount` (`src/UsageStats.cs:121`) est donc, par construction, la valeur
ajoutée de la disposition.

À 1, la condition n'apporterait rien : `MaybeShowReviewPrompt` exige déjà
`UsageStats.FirstRemapDate != null` (`src/TrayApplication.cs:2123`), c'est-à-dire au moins une
frappe remappée. Le gain est dans le **volume**, qui distingue un essai d'un usage.

### D2 — pourquoi 15 s, et pourquoi c'est la vraie nouveauté

Aujourd'hui la sollicitation ne part qu'au **démarrage de l'application**
(`src/TrayApplication.cs:314`, dans le `else` de l'affichage de l'accueil). Ajouter une
condition de plus à ce chemin laisserait le déclenchement aussi inopiné qu'avant. L'intérêt de
la décision est de solliciter **à l'instant où la valeur vient d'être ressentie** — il vient de
taper `«` ou `É` sans y penser.

15 s après la dernière frappe : assez pour ne pas couper une phrase en cours, assez court pour
qu'il soit encore devant son écran.

⛔ **Jamais depuis le hook clavier.** `UsageStats.RecordEmittedText` s'interdit toute I/O parce
qu'il est sur le chemin critique de la frappe. Le franchissement du seuil arme un **drapeau en
mémoire** ; c'est un timer de la boucle de messages qui décide et affiche.

### D3 — pourquoi remplacer les jours

Cumuler `ReviewPromptFirstActiveDays = 3` et `ReviewPromptFirstMinDays = 3`
(`src/TrayApplication.cs:94-99`) avec un seuil de caractères repousse la première sollicitation
très loin. Or la mesure du 2026-09-20 donne **0 notation pour 394 utilisateurs actifs en
juillet 2026** sur la 1.1.0 : le risque n'est pas de trop demander.

Le volume de caractères enrichis dit déjà « l'application a servi » — c'est même un signal
strictement meilleur qu'un compte de jours, qui ne dit rien de ce qui a été tapé.

⚠️ Les seuils de l'**essai 2** ne bougent pas : `ReviewPromptSecondActiveDays = 10`,
`ReviewPromptSecondMinGapDays = 7`, `ReviewPromptStaleDays = 3`. Ce sont des garde-fous
d'espacement, pas des preuves d'usage.

## Ce qui ne bouge pas

Tous les autres gardes de `MaybeShowReviewPrompt` restent en vigueur, dans le même ordre :

- `PolicyManager.ExternalLinksEnabledNow` — liens externes éteints par stratégie de groupe ;
- `ConfigManager.NotificationsEnabled` ;
- deux essais au maximum sur la vie de l'installation, aucun après un clic ;
- silence de `ReviewPromptErrorCooldownHours = 48` après une erreur journalisée ;
- un seul essai par jour (`ReviewPromptLastShown`).

## Implémentation

### 1. Exposer le franchissement sans I/O

`UsageStats` tient déjà les compteurs en mémoire. Ajouter, dans le `lock` de
`RecordEmittedText`, un drapeau armé une seule fois au passage du seuil :

- un champ `private static bool _enrichedThresholdJustCrossed;`
- armé quand le total des quatre compteurs passe de `< 20` à `>= 20`
  (le total est déjà calculé par `TotalSpecialCharsCount`) ;
- lu **et désarmé** par un `internal static bool ConsumeEnrichedThresholdSignal()`.

⛔ Aucune écriture disque, aucun appel `ConfigManager`, aucune notification depuis ce chemin :
il tourne sur le thread du hook clavier.

⚠️ Le seuil doit être franchissable **une fois par installation**, pas à chaque démarrage : le
total est persisté dans `usage-stats.json`, donc une session qui démarre déjà au-dessus de 20
ne doit pas armer le drapeau. Armer uniquement sur la **transition** observée en mémoire.

### 2. Le timer de quiétude

Dans `TrayApplication`, un nouveau timer (`TIMER_REVIEW_QUIET`, 15 000 ms) :

- réarmé à chaque frappe remappée tant que le drapeau n'a pas été consommé ;
- à son échéance, appelle `MaybeShowReviewPrompt()` ;
- s'annule si une fenêtre modale de l'application est ouverte (même logique que
  `_reviewPromptDeferred` / `OnOnboardingClosed`, `src/TrayApplication.cs:1152`).

### 3. La condition dans `MaybeShowReviewPrompt`

Dans la branche `attempt == 1`, remplacer les deux tests de jours par :

```
if (UsageStats.TotalSpecialCharsCount < ReviewPromptFirstEnrichedChars) return false;
```

avec `private const int ReviewPromptFirstEnrichedChars = 20;` placé auprès des autres seuils
(`src/TrayApplication.cs:94-100`), commentaire à l'appui.

⚠️ Le chemin de démarrage (`src/TrayApplication.cs:314`) garde son appel : quelqu'un qui a
franchi le seuil pendant une session où la notification n'a pas pu s'afficher doit pouvoir
être sollicité au lancement suivant. Le nouveau chemin s'ajoute, il ne remplace pas.

## Tests exigés

Le dépôt a un historique de gardes verts qui ne prouvaient rien
(`.claude/rules/app-repo-guard-blind-spots.md`). Trois exigences non négociables :

1. **Épingler explicitement tous les signaux non éprouvés.** `ConfigManager._lastErrorUtc` est
   un statique de processus sans hook de remise à zéro : un test vert en isolation peut être
   rouge dans la suite entière parce qu'une autre classe a journalisé une erreur.
2. **Assertion réciproque obligatoire.** Un test qui vérifie « pas de sollicitation à 19
   caractères » doit être doublé d'un « sollicitation à 20 ». Sans quoi un
   `MaybeShowReviewPrompt` devenu inerte passe au vert.
3. **Témoin de mutation**, listant *tous* les tests tombés et non le premier.

⚠️ `scripts/list-identity-literals.py` ne lit pas `AZERTYGlobal.Tests` : son exit 0 ne dit
rien de ce qui est écrit dans les tests.

Les trois projets se lancent un par un — `dotnet test` à la racine de `src/` rend **exit 0 sans
exécuter un seul test** :

```
dotnet test src/TypingEngine.Core.Tests/TypingEngine.Core.Tests.csproj -c Release
dotnet test src/TypingEngine.Windows.Tests/TypingEngine.Windows.Tests.csproj -c Release
dotnet test src/AZERTYGlobal.Tests/AZERTYGlobal.Tests.csproj -c Release
```

Un run ne se rapporte jamais sans ses trois compteurs.

## Conventions de texte

⛔ Pas d'`Edit` sur un `.cs` de ce dépôt : patcher par script Python en `newline=''`.
`src/TrayApplication.cs` et `src/UsageStats.cs` doivent être mesurés en octets avant
modification (CRLF/LF mixtes selon le fichier). Un fichier neuf de `src/` se crée en LF pur.
`src/AZERTYGlobal.Tests/` est en français accentué.

## Ce que ça ne résout pas

⚠️ Le Microsoft Store sert la **1.1.0** depuis le 2026-07-23. Ni la 1.2.0 ni la 1.3.0 n'ont
été soumises. Tant que la soumission n'a pas eu lieu, aucun de ces seuils n'atteint un
utilisateur réel, et le « 0 notation » mesuré reste imputable à la logique cassée de la 1.1.0
(sollicitation dans le `else` de l'accueil + tirage 50/50 vers la page de feedback privée).

⚠️ L'effet de ce changement est **incalculable a posteriori** : les agrégats Store ne portent
aucun identifiant utilisateur persistant. On ne saura jamais combien d'utilisateurs franchissent
le seuil. Le seul témoin disponible sera le nombre de notes par jour.

---

*Dernière mise à jour : 2026-09-21*
