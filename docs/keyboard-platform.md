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

## Dispositions et provenance au 2026-08-17

Les trois JSON de `src/` ne sont pas des sources. `AZERTY Global 2026.json`,
`character-index.json` et `lessons.json` sont des copies de fichiers du dépôt du site,
où ils sont protégés en écriture. Mesuré ce jour : les trois copies sont byte-identiques
à leur original.

**« Dossier partagé » ne peut pas vouloir dire un même dossier sur le disque.** Le site,
l'application et l'espace qui les contient sont trois dépôts git indépendants, et la CI de
l'application ne récupère qu'elle-même. Ce qui est partagé est donc la source de vérité,
pas l'emplacement : l'original reste au site, la copie devient prouvable.

Le job `provenance` de `ci.yml` lit l'original sur son URL brute publique et compare les
empreintes SHA-256. `scripts/check-layout-provenance.py` sort en 0 quand les trois copies
sont identiques, en 1 quand l'une a dérivé, et en **2** quand l'original n'a pas pu être
lu — un code distinct pour qu'une panne réseau ne se lise jamais comme une dérive. Le job
est délibérément séparé du build plutôt qu'un `needs:` : un incident réseau ne doit pas
empêcher une release de se construire, le run passant au rouge dans les deux cas. Un
`needs: provenance` sur le job build suffit à rendre le contrôle bloquant.

Lire les fichiers protégés est permis ; seule leur modification est interdite. Une
première version de ce plan avait converti l'une en l'autre, et cette confusion vidait le
contrôle de son intérêt : une empreinte consignée dans le même dépôt que le fichier
qu'elle décrit ne prouve rien que git ne prouve déjà.

`schemas/azerty-layout.schema.json` décrit le format natif — les 17 clés racine réelles,
`additionalProperties: false` à chaque niveau structurel, et un `pattern` de scancode
conforme aux trois seules formes que `ParseScancode` accepte : préfixe `SC` en
hexadécimal, préfixe `0x` en hexadécimal, sinon décimal. Il décrit le fichier, pas
seulement ce que le parseur consomme, sans quoi une faute de frappe sur `dead_keys`
passerait les deux contrôles et l'application démarrerait avec zéro touche morte. Le
schéma n'est publié à aucune URL et ne porte donc pas de `$id`. Il reste distinct du
manifeste OKLM, format d'export produit en aval par un convertisseur, dont le draft 0.1
est gelé derrière une revue de compatibilité non commencée.

`scripts/validate-layout.py` l'applique, et y ajoute les deux contrôles que JSON Schema ne
sait pas exprimer. Les cinq compteurs de `statistics` sont **recalculés** depuis la donnée
au lieu d'être crus — `physical_keys` 49, `dead_keys_count` 29, `dead_key_combinations`
1016, `direct_characters` 131, `total_unique_characters` 1005, ces deux derniers en
excluant les 29 jetons `dk_*` posés sur les couches. Les références croisées sont
recoupées : tout jeton `dk_*` armé sur une couche doit être déclaré, toute touche morte
déclarée doit être posée, et scancodes comme positions doivent être uniques — un doublon
de scancode s'écrase en silence dans le dictionnaire que construit le parseur.

La validation tourne en CI dans le job `build`, où elle **bloque** : elle ne dépend que du
dépôt, et empaqueter une disposition malformée livrerait une application cassée. Elle
n'ajoute rien au binaire publié, `jsonschema` restant une dépendance de la CI seule, ce
qui importe puisque `PublishAot` est actif.

Les 24 témoins de `scripts/tests/test_validate_layout.py` prouvent que le contrôle voit ce
qu'il prétend voir : clé racine mal orthographiée, champ de touche inconnu, scancode
hexadécimal sans préfixe, déclencheur de table à deux caractères, doigt hors énumération,
compteur faux, touche morte retirée, caractère direct inédit posé sur une couche libre,
caractère inédit produit par une combinaison, touche morte orpheline, scancode en double.
Touche morte retirée, caractère direct inédit et caractère inédit produit sont arrivés avec
la déduplication ci-dessous : un compteur ne perd sa constante écrite à la main qu'une fois
prouvé qu'une mutation de la donnée le fait virer au rouge nommément. Le schéma a d'ailleurs commencé
par rejeter la disposition réelle, sur un `design_notes` que sa première version croyait
être une chaîne alors que c'est un tableau : la donnée avait raison, le schéma a été
corrigé.

Les constantes de comptage tenues à la main en double ont disparu le 2026-08-17. Le
here-string Node de `Sync-LayoutResources.ps1` portait 63 assertions ; un comparateur les a
extraites des deux côtés, normalisé les échappements `\uXXXX` et confronté les ensembles
famille par famille : 60 étaient le double mot pour mot de `ResourceAlignmentTests`, aucune
assertion C# n'était absente du Node, et les 3 restantes — `direct_characters` 131,
`dead_key_combinations` 1016, `total_unique_characters` 1005 — étaient précisément des
comptes que `validate-layout.py` recalcule. Cette mesure est ce qui a autorisé la
suppression : rien n'a été retiré sur une impression de doublon. Le script appelle
maintenant `validate-layout.py`, qui contrôle davantage que le validateur retiré, une
sortie 2 avertissant sans bloquer puisque `jsonschema` n'existe qu'en CI. Côté C#, le compte
des touches mortes est parti et les deux totaux d'index en dur sont devenus une relation :
le total déclaré doit valoir le nombre réel d'entrées. Les treize contrôles de position
restent écrits, dans `ResourceAlignmentTests` seul, parce qu'ils encodent une intention et
non un compte.

Le rouge a été vérifié plutôt que supposé : les trois copies embarquées mutées une par une
— `E00` en Maj, le total de l'index, le nom français du circonflexe — font échouer
exactement les trois tests survivants, et rien d'autre. Un piège s'y cache pour la
prochaine fois : restaurer avec `shutil.copy2` rend les octets d'origine **et** leur date
d'origine, si bien que MSBuild juge l'assembly à jour et garde les ressources mutées dans
le binaire. Le premier run vert après restauration était donc rouge à tort ; il faut
toucher les fichiers restaurés avant de reconstruire.

Reste sur cette étape : `LayoutJsonParser` lève toujours un `KeyNotFoundException` nu, et
l'accord entre le schéma et lui n'est prouvé que dans un sens.

`Sync-LayoutResources.ps1` était **inexécutable** depuis la migration : ses deux candidats
de `$siteRoot` désignaient des dossiers disparus et la résolution levait avant la première
copie. Le contrôle de provenance conseillant de le rejouer, il fallait qu'il puisse
tourner. Il vise maintenant `../website`, les deux anciens noms restant en repli, et il
dit ce qu'il fait : une ligne par fichier avec l'empreinte avant et après, et un `-DryRun`
qui compare sans rien écraser. Il importe l'état de travail du site, pas une version
publiée — le seul instant où il écrit est précisément celui où quelque chose change.

Deux réserves demeurent. La branche `-SyncPublicRepo` est résiduelle : le clone public
qu'elle cherche n'existe pas, son second candidat est le dépôt lui-même, si bien qu'elle
copie les fichiers sur eux-mêmes en annonçant un succès. La supprimer casserait
`LessonCoreTests.SyncScript_AllowsCreatingPublicLessonsResource`, qui assère sur le texte
du script — les deux se traitent ensemble ou pas du tout. Et le récapitulatif final
annonçait « 29 touches mortes » et « 1034 entrees » en dur, troisième endroit à corriger à
la main ; ces lignes sont retirées plutôt que recalculées, parce que PowerShell ne sait pas
lire ce fichier : le `ConvertFrom-Json` de la 5.1 est insensible à la casse et rejette les
tables de touches mortes, qui contiennent `a` et `A`.

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
