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

### `TypingEngine.Windows` — prochaine extraction

Projet Windows commun :

- hook clavier bas niveau ;
- suivi fiable des modificateurs ;
- injection Unicode, combinaisons natives et Alt codes ;
- adaptation au layout de la fenêtre active ;
- politique de pass-through et compatibilité jeux.

Le code correspondant vit encore dans `KeyMapper`, `KeyboardHook`, `ForegroundMonitor`, `Win32` et `Win32Api`. Il sera déplacé par tranches couvertes par les tests existants.

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

## Séquence d'extraction

1. Compléter les tests de caractérisation nécessaires à chaque tranche Windows avant son déplacement.
2. Extraire `TypingEngine.Windows` derrière des contrats d'entrée et de sortie, sans renommer simultanément l'application.
3. Déplacer les dispositions vers un dossier partagé validé par schéma, tout en gardant les fichiers canoniques protégés inchangés.
4. Renommer le projet produit en `App.AZERTYGlobal` seulement après stabilisation des références et du packaging AOT/MSIX.
5. Ajouter un second produit ou module comme preuve de réutilisation avant toute publication NuGet.

## Règles d'évolution

- aucune référence de `TypingEngine.Core` vers `AZERTYGlobal` ou une autre application ;
- aucune API Windows dans le Core ;
- aucune règle de disposition codée dans une UI ;
- chaque bug moteur reçoit un test au niveau commun ;
- un module optionnel dépend du Core, jamais l'inverse ;
- AZERTY Global Science reste un point d'extension architectural : son développement ne commence pas avant la réouverture explicitement validée de ce chantier.
