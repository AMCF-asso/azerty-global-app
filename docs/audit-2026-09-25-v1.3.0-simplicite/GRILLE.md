# Grille commune — audit efficacité / simplicité / lisibilité, app AZERTY Global 1.3.0

Dépôt : `D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store`
Branche `release/1.2.0-notation-store`, commit de référence `f0a98ba`. Application C# .NET 8 (Win32/GDI, hook clavier bas niveau), 6 projets sous `src/`.

## But (décidé par Antoine le 2026-09-25)

Constats seuls, AUCUNE modification du code. Deux usages :
1. alléger le C# pour la 1.4.0 (les Leçons et le Défi y sont revus) ;
2. donner à la v2 (réécriture en Rust, `components/app-rust/`, décisions dans `components/app-rust/docs/DECISIONS.md`) la liste de ce qu'il ne faut pas reproduire.

## Trois axes

- **exec** — efficacité à l'exécution : chemin chaud du hook (le callback LowLevelKeyboardProc doit rendre la main vite), allocations par frappe, LINQ/boxing/string dans le chemin chaud, verrous, SendInput, fuites d'objets GDI, repeints inutiles, minuteries qui réveillent le CPU, I/O sur le fil UI, chargements au démarrage (JSON), mémoire retenue.
- **eco** — économie du code : code mort, doublons, abstractions inutiles, lignes superflues.
- **lisi** — facilité de compréhension : fichiers/classes trop gros (god class), méthodes longues, états implicites (booléens multiples au lieu d'un état), nombres magiques, nommage incohérent (FR/EN), commentaires qui mentent ou manquent là où le « pourquoi » n'est pas évident, couplage caché.

## Étiquettes ponytail (skill DietrichGebert/ponytail, grille de `ponytail-audit`)

- `delete` : code mort, flexibilité inutilisée, fonctionnalité spéculative. Remplacement : rien.
- `stdlib` : réécriture maison de ce que la BCL .NET fournit. Nommer l'API.
- `native` : code qui refait ce que Windows / WinForms / .NET fait déjà. Nommer la fonctionnalité.
- `yagni` : interface à une seule implémentation, config que personne ne règle, couche à un seul appelant.
- `shrink` : même logique, moins de lignes. Montrer la forme courte.
Hors ponytail : `perf` (axe exec) et `clarte` (axe lisi sans gain de lignes).

Échelle ponytail : « Does this need to exist? → Already in this codebase? → Stdlib? → Native platform? → Installed dependency? → One line? → Only then the minimum ».

## Garde-fous (ponytail : « Lazy, not negligent »)

Ne JAMAIS proposer de retirer sans preuve : validation aux frontières de confiance, gestion d'erreur qui évite une perte de données, sécurité (saisie sécurisée, `SecureInput*`), accessibilité (noms accessibles, UIA, navigation clavier), gardes de course/réentrance du hook.
Cette app contient beaucoup de défenses nées de vrais bugs (audits dans `docs/audit-*`, tests `Audit120*`, B1 VK_PACKET du 24/09, etc.). Avant de dire « mort » ou « inutile » : grep des appelants, `git log -L`/`git blame` ou recherche dans `docs/` et `Changelog.md` pour la raison d'être. Sans raison trouvée ni preuve d'inutilité : confiance « faible » et proposer une vérification, pas une suppression.
Un constat = un fait vérifié dans le code, avec fichier:ligne. Distinguer **preuve** (mesuré, compté, lu) et **estimation**.

## Gravité

- **majeur** : complexité qui cause ou a causé des bugs, ≥100 lignes supprimables ou fusionnables, coût mesurable dans le chemin chaud, ou zone qui rend la 1.4.0 risquée à modifier.
- **moyen** : 20 à 100 lignes, ou confusion locale réelle pour un lecteur.
- **mineur** : moins de 20 lignes, cosmétique.
Ne pas gonfler : peu de constats solides valent mieux que beaucoup de faibles. Regrouper les occurrences d'un même motif en un seul constat (liste des emplacements).

## Interdits

- Aucune écriture dans le dépôt (ni `src/`, ni `docs/`), aucune commande git qui modifie l'état (lecture seule : log, blame, show, grep, ls-files).
- Si un build est nécessaire : `dotnet build/test ... --artifacts-path "<scratchpad>/audit/build-<zone>"` pour ne rien écrire dans le dépôt. Sur ce PC, Application Control bloque le chargement de `AZERTY Global.dll` (tests `AZERTYGlobal.Tests` en échec local, la CI fait foi) ; `TypingEngine.Core.Tests` et `TypingEngine.Windows.Tests` passent en local. Ne pas lancer l'application, ne pas installer de hook clavier global, ne pas toucher au registre.
- Scripts multi-lignes : les écrire dans un fichier du scratchpad puis l'exécuter (jamais de heredoc, `python -c` ou `-e` inline). Une commande par étape.
- Ne jamais lire `D:/My files/Finances/` ni `D:/My files/Infos perso/`.

## Livrable de chaque zone

1. `<scratchpad>/audit/constats-<ZONE>.json` : tableau JSON, un objet par constat :
```json
{
  "id": "M-01",
  "zone": "moteur",
  "fichiers": ["src/TypingEngine.Windows/KeyMapper.cs:120-188"],
  "axe": "exec|eco|lisi",
  "etiquette": "delete|stdlib|native|yagni|shrink|perf|clarte",
  "gravite": "majeur|moyen|mineur",
  "titre": "une ligne, en français",
  "constat": "1 à 3 phrases factuelles",
  "preuve": "extrait court du code, comptage, mesure ou commande ; dire si c'est une estimation",
  "proposition": "ce qui remplace ; forme courte si shrink",
  "gain": {"lignes": -40, "exec": "texte ou null", "nature": "preuve|estimation"},
  "risque": "ce qui peut casser ; tests qui couvrent (nom de fichier) ou « non couvert »",
  "cible": "1.4.0|v2|les deux",
  "lecon_v2": "une phrase si le motif ne doit pas être reproduit en Rust, sinon null",
  "confiance": "élevée|moyenne|faible"
}
```
2. `<scratchpad>/audit/notes-<ZONE>.md` : synthèse de la zone (10 à 30 lignes) : vue d'ensemble, 3 points forts réels (ce qui est déjà simple et bien fait, à garder), chiffres de la zone, signalements hors zone (suspicions sur d'autres fichiers, sans les instruire), limites de ce qui a été lu.
Écrire en français. Rendre en réponse finale : nombre de constats par gravité, les 5 plus importants en une ligne chacun, et les chemins des deux fichiers.
