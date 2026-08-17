# Plateforme commune de saisie clavier

## Décision

Ce dépôt évolue progressivement vers un monorepo modulaire. Les applications restent des produits ; elles consomment un moteur commun qui ne dépend d'aucune application.

```text
App.AZERTYGlobal ─┐
App.Scientific ───┼─> TypingEngine.Windows ─> TypingEngine.Core
Autre application ┘                         └> modules optionnels
```

Les projets sont d'abord reliés par `ProjectReference`. La publication de paquets NuGet séparés attendra que les interfaces soient stabilisées et qu'un consommateur extérieur au dépôt le justifie.

## Frontières

### `TypingEngine.Core`

Projet `net8.0` portable, sans cible `-windows` et sans P/Invoke :

- modèle déclaratif des dispositions ;
- lecture et validation du JSON ;
- sélection des couches ;
- état et résolution des touches mortes ;
- futures transformations de texte déterministes ;
- contrats des modules optionnels.

Le Core reçoit des entrées explicites et retourne des décisions. Il ne capture pas le clavier, n'injecte pas de frappes, n'ouvre pas de fenêtre et ne charge pas une ressource propre à une application.

### `TypingEngine.Windows`

Projet Windows commun :

- hook clavier bas niveau ;
- suivi fiable des modificateurs ;
- injection Unicode, combinaisons natives et Alt codes ;
- adaptation au layout de la fenêtre active ;
- politique de pass-through et compatibilité jeux.

Cette couche est maintenant extraite dans un projet `net8.0-windows10.0.17763.0` séparé. Elle contient `KeyMapper`, `KeyboardHook`, `ForegroundMonitor`, `GameRegistry`, les P/Invoke strictement nécessaires à la saisie et l'abstraction testable `IWin32Api`.

Le contrat `IWindowsTypingHost` inverse la dépendance vers le produit : AZERTY Global fournit les raccourcis, les overrides de compatibilité, la journalisation et le compteur local de texte émis. La couche Windows ne référence donc ni `ConfigManager`, ni `UsageStats`, ni une classe de l'application.

### `App.*`

Chaque produit conserve :

- son point d'entrée, son interface et son identité ;
- ses paramètres et sa configuration de modules ;
- ses ressources de disposition ;
- ses fonctions propres, comme les leçons AZERTY Global ;
- son manifeste et son packaging Store.

## État au 2026-08-15

La première tranche est intégrée :

- la base interne 1.1.2 et les fonctions 1.2.0 en développement sont réconciliées dans ce dépôt canonique ;
- `TypingEngine.Core` contient `Layout`, `KeyDefinition`, `DeadKeyDefinition`, `LayoutJsonParser` et `CompositionEngine` ;
- `LayoutLoader` n'est plus qu'un adaptateur de ressource propre à l'application ;
- `KeyMapper` délègue la composition des touches mortes au Core ;
- `TypingEngine.Core.Tests` cible `net8.0` et s'exécute sans l'application ni Windows.
- `TypingEngine.Windows` porte désormais le remapping, le hook global, l'injection et la politique de compatibilité Windows ;
- `TypingEngine.Windows.Tests` contient 95 tests de caractérisation indépendants du produit ;
- `AzertyGlobalWindowsTypingHost` est le seul adaptateur entre les services du moteur et la configuration/statistique du produit.

## Identité produit au 2026-08-17

`ProductIdentity` centralise l'identité du produit : nom affiché, forme identifiant,
domaine et URL du site, identifiant Store, dossier de configuration, ressources embarquées
et noms de classes fenêtre. La tranche est complète, en deux moitiés.

**Moitié non localisée** — 78 sites ramenés sur `ProductIdentity`, sans changement de
comportement.

**Moitié localisée** — les 86 occurrences du nom dans les phrases traduites de
`Localization/` interpolent désormais `L.Product`, alias privé de
`ProductIdentity.DisplayName` ; les 4 sites qui portaient le domaine passent par
`SiteDomain` et `Url()`. Le chemin complet aurait rendu les phrases illisibles, l'alias
garde une seule indirection. La conversion a été vérifiée en comparant les 700 chaînes
rendues, français et anglais, avant et après : identiques.

Trois littéraux restent volontairement autonomes : `ConfigFolderName` — renommer le
produit ne doit pas déplacer la configuration de tout le monde — `ExecutableName`, dont la
source est `<AssemblyName>`, et `Namespace`, la forme identifiant, que renommer le nom
affiché ne renomme pas.

`scripts/list-identity-literals.py` couvre maintenant `Localization/` et cherche le nom
n'importe où dans un littéral, plus seulement en tête ; il ignore les commentaires. Sortie
attendue : les seules déclarations de `ProductIdentity.cs`, code de sortie 0.

Depuis le 2026-08-17 il tourne en CI, étape « Aucun littéral d'identité hors
ProductIdentity » de `ci.yml`, placée juste après le checkout : une régression passe le
build au rouge en quelques secondes au lieu d'attendre les deux publications. Le contrôle
a été prouvé dans les deux sens sur l'arbre propre — 0 littéral inattendu, sortie 0 ; et
avec un littéral injecté dans `TrayApplication.cs`, sortie 1 avec le fichier nommé.

Cette classe ne figure pas dans la séquence d'extraction ci-dessous parce qu'elle ne
déplace aucun code entre projets : elle prépare l'étape 4, où le produit sera renommé, et
sert déjà à un dépôt qui n'aura jamais de seconde application.

## Séquence d'extraction

1. Terminé — compléter et transférer les tests de caractérisation de la couche Windows.
2. Terminé — extraire `TypingEngine.Windows` derrière `IWin32Api` et `IWindowsTypingHost`, sans renommer simultanément l'application.
3. Déplacer les dispositions vers un dossier partagé validé par schéma, tout en gardant les fichiers canoniques protégés inchangés.
4. Renommer le projet produit en `App.AZERTYGlobal` seulement après stabilisation des références et du packaging AOT/MSIX.
5. Ajouter un second produit ou module comme preuve de réutilisation avant toute publication NuGet.

## Règles d'évolution

- aucune référence de `TypingEngine.Core` vers `AZERTYGlobal` ou une autre application ;
- aucune API Windows dans le Core ;
- aucune référence de `TypingEngine.Windows` vers `AZERTYGlobal` ou ses services concrets ;
- aucune règle de disposition codée dans une UI ;
- chaque bug moteur reçoit un test au niveau commun ;
- un module optionnel dépend du Core, jamais l'inverse ;
- AZERTY Global Science reste un point d'extension architectural : son développement ne commence pas avant la réouverture explicitement validée de ce chantier.
