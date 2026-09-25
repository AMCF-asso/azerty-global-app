# Audit 1.3.0 — efficacité, simplicité et lisibilité du code — 25 septembre 2026

Cadrage d'Antoine (QCM du 25/09) :
- **Usage** : les constats servent à alléger le C# pour la 1.4.0 et à donner à la v2 Rust la liste de ce qu'il ne faut pas reproduire.
- **Efficacité** : les performances à l'exécution et l'économie du code.
- **Périmètre** : tout le code de production des 6 projets de `src/`. Les tests ne sont lus que pour la couverture.
- **Grille** : ponytail est étudié, et ses étiquettes servent de grille.
- **Livrable** : des constats seuls, sans correctif. Un rapport et une page privée.
- **Mesures** : faites hors de l'application.
- **Contre-expertise** : second-avis, en interne.

- Commit audité : `f0a98ba` (branche `release/1.2.0-notation-store`, candidat 1.3.0 non recetté du 24/09).
- Méthode :
  - Six revues en lecture seule, une par zone : moteur de frappe, application, Leçons et Défi, fenêtres, clavier visuel et recherche, transversal par script.
  - Une grille commune (`GRILLE.md`), qui reprend les étiquettes de `ponytail-audit` (`delete`, `stdlib`, `native`, `yagni`, `shrink`) et ajoute `perf` et `clarte`.
  - Bancs hors application :
    - moteur en Release et en NativeAOT Size, avec le vrai `HookCallback` appelé sans hook installé ni `SendInput` ;
    - autres zones sur des répliques JIT, qui donnent des ordres de grandeur.
  - Trois contre-expertises adverses sur 28 constats (tous les majeurs et les constats de comportement) : **8 confirmés, 20 nuancés, 0 réfuté**. Les corrections sont appliquées dans `donnees/constats.json`, et chaque valeur d'origine est gardée dans un champ `*_initial`.
- Ce qui n'a pas été fait :
  - Rien n'a été modifié dans `src/`, et l'application n'a pas été lancée.
  - `AZERTYGlobal.Tests` ne s'exécute pas sur ce poste (Application Control). En Release, `TypingEngine.Windows.Tests` passe à 257/257 et `TypingEngine.Core.Tests` à 18/18.
  - Aucun effet visuel n'a été vu à l'écran.
- Page privée filtrable : https://claude.ai/artifact/EJY4SfoYDtF295CNciKxXR

## En bref

1. **Le code est rapide là où ça compte.** Une frappe coûte de 2,2 à 6,5 µs en médiane dans la pile de production, et alloue de 608 à 2 128 octets. C'est plus de 10 000 fois sous `LowLevelHooksTimeout`. Il n'y a aucun chantier de performance à ouvrir dans le chemin chaud.
2. **Il se complique par copie et par concentration, pas par abstraction.** Seuls 6 constats sur 103 relèvent de `yagni`. En revanche :
   - 41 % du code tient dans 5 classes ;
   - la 1.3.0 a trois moteurs de rendu du clavier ;
   - `character-index.json` est lu par cinq chargeurs différents ;
   - le squelette de fenêtre est recopié dans 14 fichiers ;
   - 69 membres sont morts.
3. **Les coûts d'exécution réels sont hors du chemin chaud, sur le fil qui sert le hook :**
   - `config.json` est réécrit avec synchronisation disque de 3 à 7 fois par geste, pour 3,3 ms par écriture ;
   - environ 5 ms à chaque changement de focus du système ;
   - deux repeints complets des Leçons à chaque frappe, soit environ 3,5 ms (estimation).
4. **Une décision précède la 1.4.0 : sa branche de base.** La branche `main` locale contient déjà `ThemeWindow`, `KeyboardTheme` et CH4b, c'est-à-dire le clavier virtuel sur le moteur unifié. Trois chantiers de cet audit partent de ces fichiers, et aucun d'eux n'est dans la release.
5. **Trois défauts de comportement sont sortis de l'audit**, hors de son sujet : Échap sur l'Accueil, un clic décalé dans la recherche, la progression bloquée en silence. S'y ajoute une fragilité des statistiques. Des recettes sont proposées plus bas. Le soir même, Antoine a fait corriger dans la 1.3.0 tous ces défauts sauf la progression (voir « Suite donnée »).
6. **Environ −2 000 lignes sont possibles**, soit à peu près 6 % de la production. C'est une estimation : les gains des constats ne s'additionnent pas. Environ 500 lignes sont du code mort vérifié par comptage.
7. **Ponytail : ne pas l'installer.** Ses étiquettes ont servi de grille. Mais ses angles morts (correction, « Replacement: nothing ») sont précisément là où la contre-expertise a dû corriger les propositions (voir la dernière section).

## Chiffres du dépôt

| Mesure | Valeur | Source |
|---|---|---|
| Production / tests | 32 476 lignes (23 587 de code) / 12 469 lignes pour 652 tests | `metriques.json`, recompté par `wc -l` |
| Lignes de test par ligne de production | moteur 1,43 · app 0,48 · leçons 0,15 · fenêtres 0,13 · visuel 0,05 | `metriques.json` |
| Concentration | 5 classes de 1 900 lignes ou plus = 41,4 % (13 444 lignes) | X-08, confirmé |
| Méthodes | médiane 10 lignes, complexité approchée 2 ; 24 méthodes de plus de 100 lignes (4 001 lignes) ; maximum 117 (`TrayApplication.WndProcCallback`) | X-08 |
| Clones entre fichiers | 4,8 % des lignes normalisées, environ 608 lignes en trop (7,3 % avec identifiants abstraits) | `metriques.json` |
| Interop | 189 `DllImport`, 0 `LibraryImport` ; 11 fonctions et 35 constantes redéclarées, sans aucune valeur divergente | X-02 |
| `catch` | 112, dont 95 attrape-tout ; les muets sont en majorité défendables | X-05, nuancé |
| Hygiène | 0 dépendance NuGet en production, 0 TODO, 0 `#pragma warning disable` ; 2 avertissements CS8604 en Release | X- |
| Constats | 103 : 14 majeurs, 54 moyens, 35 mineurs ; 85 portent une leçon pour la v2 | `donnees/constats.json` |

## Décision préalable : la base de la 1.4.0

Relevé par git le 2026-09-25 :
- Le point de départ commun est `8775489` (27/08). Depuis, `main` locale compte 49 commits propres et la release 98. Aucune ne contient l'autre.
- Les 17 derniers commits de `main` locale ne sont pas poussés.
- `origin/main` (`a148f26`, 20/09) porte 3 commits absents de `main` locale.
- `main` locale contient : `Theme.cs`, `ThemeWindow.cs`, `ThemeControls.cs`, `KeyboardTheme.cs`, des `*.Theme.cs` pour quatre fenêtres, et CH4b (`fb0b854`, `b8dcdda`, 07/09).
- La note de mémoire qui disait CH4b « livré » décrivait `main`, pas la 1.3.0.

Conséquences :
- F-01 (socle de fenêtre) doit partir de `ThemeWindow`.
- L-01 (rendu du tutoriel) doit partir du genre de surlignage `KeyHighlight` de `KeyboardTheme.cs`.
- V-01 (clavier virtuel) ne se reprend pas sans `Theme.cs` et `KeyboardTheme.cs`.
- Planifier ces trois chantiers sur la release reviendrait à réécrire ce qui existe déjà sur `main`.

**À trancher par Antoine :** la 1.4.0 part-elle de la release en réintégrant `main`, ou de `main` en réintégrant les 98 commits de la release ? Et que deviennent les 17 commits non poussés ?

## Chantiers proposés pour la 1.4.0

Ils sont classés du plus sûr au plus lourd. Les gains sont des estimations, sauf mention contraire.

| # | Chantier | Constats | Gain | Risque principal |
|---|---|---|---|---|
| 1 | Retirer le code mort vérifié | X-03, L-07, F-05, F-07, F-11, V-05, A-12, A-14, A-15, M-09, X-04 | ≈ −500 lignes, comptées | La mutation 7 de `docs/audit-v1.2.0/witness-lot-c.py:79` utilise `CurrentIsSober` ; le setter de `MaintainableTutorialCompleted` écrit une clé que personne ne relit |
| 2 | Petits gains d'exécution sans risque | V-03, A-08, L-06, M-04, M-05, V-15 | recherche 8,5 fois plus rapide (mesuré) ; infobulles | Aucun test ne couvre le classement de la recherche |
| 3 | Écritures de config groupées et différées hors du fil du hook ; progression durable | A-05, F-18, M-07, L-14 | de 13 à 23 ms de blocage en moins par geste | Garder l'atomicité et le refus d'écraser un fichier illisible |
| 4 | Un seul chargeur de `character-index.json`, partagé et paresseux | V-04, L-03 | de −100 à −150 lignes ; environ 17 ms et 1,86 Mo de moins au démarrage ; 6,6 Mo alloués en moins à chaque ouverture du tutoriel | Cinq usages, dont deux lisent les couches différemment (L-02) |
| 5 | Un seul rendu du clavier et refonte du tutoriel et des Leçons | V-01, L-01, L-02, L-04, L-09 à L-13, L-18 | de −650 à −800 lignes ; le tutoriel passe d'environ 3 245 à 1 200 lignes | Dépend de la base (voir la section précédente). `LessonCoreTests.cs:588` lit des champs par réflexion. Le repeint du clavier passe de 0,82 à 2,26 ms, ce qui reste acceptable |
| 6 | Socle de fenêtre commun | X-01, F-01, F-02, F-10, F-14, F-15, V-16, L-16, A-20 | de −350 à −460 lignes (463 lignes en trop, mesurées) | Règles : pas de pinceau de classe (Windows le détruit déjà au désenregistrement), et vérifier le retour de `RegisterClassExW` |
| 7 | Paramètres : une table de descripteurs au lieu de 8 à 11 listes parallèles | F-03, F-04, F-06, F-12 | ≈ −150 lignes | Libellés propres à certains contrôles (`TCM_SETITEMW`) ; rectangles de clic à décaler selon le défilement |
| 8 | Sollicitations d'avis : un état persistant et une fonction de décision | A-02, A-13 | jusqu'à −150 lignes (borne haute) | Un « budget unique » changerait le comportement : **règle à trancher par Antoine** |
| 9 | Une seule déclaration d'interop | A-19, M-08, X-02 | de −45 à −100 lignes | Aucun : les valeurs sont identiques partout |

Pour le chantier 5, le découpage cible par blocs de lignes est dans `notes/notes-lecons.md`. Quatre points s'y ajoutent :
- **Le Défi masqué n'a créé aucun code mort.** Tout est dormant derrière `DailyChallenge.Enabled`, et le corpus n'est pas chargé tant que l'interrupteur est coupé.
- **Le Défi est à isoler hors de la fenêtre Leçons**, sans le retirer (L-18).
- **La clé de progression contient le hash du contenu** : les anciennes entrées ne sont jamais purgées, et le Défi en ajoutera deux par jour (L-13).
- **Les Leçons sont peu testées**, avec 0,15 ligne de test par ligne. Il vaut mieux extraire d'abord les parties pures (tampon de saisie libre, guidage) avec leurs tests.

## Ce qu'il ne faut pas faire en C# : laisser à la v2

- **Découper `TrayApplication` (A-01) et `KeyMapper` (M-02).** Le constat est confirmé, mais le gain en lignes est à peu près nul. Des tests trouvent les méthodes privées par leur nom sur `typeof(TrayApplication)` (A-03). La v2 réécrit ces deux classes. Le seul geste à bas coût en 1.4.0 serait un commentaire donnant la table des combinaisons d'état.
- **Réduire les relectures du premier plan (M-01).** La proposition d'origine ferait revenir le défaut b2 : la touche morte serait consommée (`KeyMapper.cs:820`), puis l'émission refusée, et le caractère serait perdu. Il faut garder la double vérification.
- **Alléger les couches de surveillance du hook (M-15, M-16).** Chacune est née d'un bug réel, et la confiance dans ce constat est faible.
- **Lancement automatique hors package (A-04).** Attendre la décision D8.

## Défauts de comportement à vérifier en recette (hors simplicité)

| Constat | Défaut | État | Recette proposée |
|---|---|---|---|
| F-10 | Accueil, étape 3 : Échap sur un lien ne ferme pas la fenêtre (`OnboardingWindow.cs:1226-1237`), alors qu'À propos le fait (`AboutWindow.cs:463-467`) | probable ; **corrigé en 1.3.0** (`4dbfa9d`) | Étape 3, Tab jusqu'à « Guide », puis Échap. Attendu : la fenêtre se ferme. Prédit : rien ne se passe |
| F-10 | Pause : Entrée sur « Annuler » validerait la pause | improbable, non démontré | Tab jusqu'à « Annuler », regarder si le cadre du bouton par défaut s'y déplace, puis Entrée |
| V-07 | Recherche : un clic sur les 1 à 3 derniers pixels d'une ligne insère le caractère de la ligne suivante | confirmé dans le code ; **corrigé en 1.3.0** (`7b5d71c`) | À 100 % avec la Loupe, cliquer la dernière rangée de pixels de la ligne 2 |
| L-14 | Progression illisible : toutes les sauvegardes sont refusées jusqu'au redémarrage, sans message. Le test `LessonCoreTests.cs:447` le montre déjà | confirmé, rare ; reporté (règle de mise en quarantaine et textes à décider) | Remplacer `lessons-progress.json` par `{ invalid`, relancer, réussir un exercice. Attendu (défaut) : l'exercice n'est pas coché et aucun message n'apparaît |
| A-11 | `usage-stats.json` perd les clés inconnues au retour arrière, et aucune garde `_loadFailed` n'empêche d'écraser le fichier après une lecture ratée | confirmé dans le code ; **garde corrigée en 1.3.0** (`62c7d4e`), clés inconnues laissées à la 1.4.0 | Recette par fichier (retour 1.4 → 1.3). Attention : la proposition « désérialiseur strict » pourrait tout effacer |
| V-08, V-06 | Le pied de la recherche affiche « 20 résultats » quand il y en a 98. Les couleurs de la méthode de saisie sont en dur en français (« Shift » et « then » ne sont pas colorés en anglais) | confirmé dans le code ; **corrigés en 1.3.0** (`7b5d71c`) | Visuel |

Points d'attention, non démontrés :
- **Réentrance pendant l'attente UIA (M-06).** Le fil principal est STA et attend par `WaitOne`, qui sert les messages envoyés. Un rappel `WH_KEYBOARD_LL` peut donc s'exécuter en réentrance au milieu de `Recompute`, avec l'instantané précédent. Cela a été testé par `SendMessage`, sans hook réel.
- **Retour de `RegisterClassExW` non vérifié (X-01).** Si la classe existe déjà (erreur 1410), la fenêtre repart sur l'ancienne procédure.
- **`TrayApplication.IsLayoutAZERTYGlobal` (`:1138-1162`) appelle `ToUnicodeEx` avec le drapeau 0.** Cet appel peut modifier l'état des touches mortes du système, alors que le moteur utilise le drapeau 4. Signalé, non instruit.
- **Constats du 20/09 toujours ouverts :** AG130-44 (peinture sans `try/finally`, `LearningModule.OnPaint:2051` compris), AG130-48 (`WS_CLIPCHILDREN` absent des six fenêtres), F2.8 / AG130-42 (Accueil non borné à la zone de travail).
- **`app.manifest` ne déclare pas comctl32 v6.** SysLink est donc indisponible, et l'activer rethèmerait tous les contrôles. La décision vaut aussi pour la v2.

## Ce qui est bien fait, à garder

- **Le chemin chaud du hook.** Les filtres sont en tête, sous forme de fonctions pures et testées (`ShouldProcessKeystroke`, `ShouldTreatAsPhysical`, `HookSilenceWatchdog`). `StateChanged` est regroupé par `PostMessage` hors du rappel, et l'instantané du premier plan est immuable.
- **Le cœur pur et petit.** `CompositionEngine` fait 54 lignes. `LayoutJsonParser` nomme le chemin JSON fautif. `MaintainableLayerManager` est une machine d'état sans Win32. `IWin32Api` est justifié : 3 implémentations, qui servent de couture à 24 fichiers de tests.
- **La protection des données.**
  - La config s'écrit de façon atomique, avec un `.tmp` par PID. Elle refuse d'écraser un fichier illisible et garde la forme des clés inconnues.
  - `UsageStats` ne fait aucune I/O par frappe et ne garde que des agrégats.
  - Le mutex d'instance unique est qualifié par SID.
- **Les décisions extraites et testées :** `DialogNavigation`, `SettingsScrollState`, `WindowSizing`, `AppChannel.Classify`, et les motifs « photographie + ShouldX ».
- **L'hygiène du dépôt.** Aucune dépendance NuGet en production. Le binaire est AOT avec CFG et `/CETCOMPAT`. Aucune fuite GDI trouvée. 85 % des renvois d'audit dans les commentaires donnent aussi le pourquoi.

## Leçons pour la v2 Rust

1. **Un état d'activité nommé**, sous forme d'énumération, avec sa table de transitions. Le hook, l'icône et l'infobulle en sont dérivés. C'est l'inverse des 5 champs et 4 booléens réappliqués à la main en 11 endroits (A-03, M-12). La lecture de l'état anti-triche par le moteur reste un garde-fou à conserver.
2. **Rien de bloquant sur le fil qui sert le hook** : ni écriture disque, ni calcul du premier plan, ni repeint (A-05, M-06, M-07, L-05).
3. **Le contexte de premier plan capturé une fois par frappe**, en gardant une vérification juste avant l'émission (défaut b2, M-01).
4. **Une seule table de propriété des touches enfoncées**, que `PressDisposition` fournit déjà. La quarantaine des relâchements reste une notion distincte (M-03).
5. **Des composants uniques** : un seul chargeur de l'index des caractères, un seul rendu du clavier paramétré, un seul socle de fenêtre, une seule déclaration d'interop (V-04, V-01, L-01, X-01, X-02).
6. **Des types, pas des chaînes**, pour les couches, les genres de surlignage et les types de méthode (V-10).
7. **Des fichiers persistés robustes** : garder les clés inconnues, refuser d'écraser un fichier illisible, synchroniser sur disque et le dire à l'utilisateur (A-11, L-14).
8. **Des listes accessibles** : une liste de résultats dessinée à la main est invisible aux lecteurs d'écran. Prévoir des contrôles natifs ou un fournisseur UIA (V-07, L-10).
9. **Les sollicitations d'avis** : un état persistant et une fonction de décision (A-02).

## Ponytail : utilité pour les projets AZERTY Global

Dépôt `DietrichGebert/ponytail`, sous licence MIT, environ 146 000 étoiles, dernier push le 14/09/2026. Le skill propose une échelle en 7 marches : besoin réel ? déjà dans le code ? stdlib ? plateforme ? dépendance installée ? une ligne ? Sinon, le minimum. Il fournit aussi `/ponytail-review` et `/ponytail-audit`. Détail et sources dans `notes/ponytail-recherche.md`.

- **Mesure indépendante (JetBrains, partie 3).** Protocole : 80 paires SkillsBench, `claude-sonnet-5` en effort moyen, v4.8.4. Résultats :
  - coût −10,3 % (p = 0,004) ;
  - code écrit −15,4 % (p = 0,088, non significatif) ;
  - qualité : « a null result, not a clean bill of health ».
  - Installé comme simple skill, il s'est déclenché « zero times » : seul son hook le fait agir. Chiffres relus dans l'article le 25/09.
- **Banc indépendant (issue #236, 480 builds, Opus 4.8).** Environ −44 % de code, sans perte de correction ni de sécurité. En revanche, sur 5 tâches sur 24 aux cas limites non énoncés, la gestion des entrées invalides s'effondre, et c'est pire aux niveaux élevés. Relu le 25/09.
- **Chiffres du dépôt (−54 % de lignes).** Obtenus avec Haiku sur 12 tâches Python/React, n = 4. La mention « Safety 100 % » repose sur 20 exécutions. Aucune donnée n'existe pour C#, Rust ou un site statique.
- **Coûts.**
  - Environ 1 300 jetons injectés à chaque démarrage, reprise, compaction, et dans chaque sous-agent.
  - Des hooks qui écrivent des fichiers d'état.
  - Sous Windows : une latence de 6,4 s par prompt a été signalée en juillet (#647, fermée depuis, à re-mesurer), et une fenêtre console s'ouvre à chaque hook (#791, ouverte).
  - Un clone piégé du dépôt a circulé (#735).
- **Mis à l'épreuve dans cet audit.**
  - Sa cible principale, `yagni`, ne concerne que 6 constats sur 103. La marche utile ici est la deuxième, « déjà dans le code ? réutilise ».
  - Son hors-champ (correction, performance) et son « Replacement: nothing » (#866) sont précisément là où la contre-expertise a dû corriger : 20 constats relus sur 28 ont été nuancés. La proposition de M-01 faisait revenir b2, et celle d'A-11 pouvait effacer les statistiques.
  - L'issue #679 décrit le même piège : un `delete` qui affirme « zero callers » sans avoir regardé les tests.

Verdict :
- **App 1.x (C#)** : ne pas installer. Garder l'échelle et les étiquettes comme grille de revue manuelle, toujours suivie d'une vérification des appelants et de la raison d'être (git log, audits).
- **App v2 (Rust, code neuf)** : c'est là que l'échelle aide le plus (pas de trait à une seule implémentation, std avant une crate, pas d'échafaudage). Le gain mesuré reste modeste. Recopier les 7 marches et la règle « Lazy, not negligent » dans les règles du projet `app-rust` coûterait moins que le plugin.
- **Site Eleventy et outils Python** : peu d'intérêt. Seule la marche « plateforme native » (HTML/CSS avant JS) sert, et le site a déjà ses fondations.
- **Si le plugin est un jour installé**, le prendre uniquement depuis le dépôt officiel.

## Suite donnée : corrections dans la 1.3.0 (25/09 au soir)

Décision d'Antoine (QCM du 25/09 au soir) : corriger dans la 1.3.0, quitte à repousser la soumission de quelques jours, F-10 (Échap sur l'Accueil), V-07 (clic de la recherche), A-11 (garde des statistiques), V-08 et V-06 (textes de la recherche). Commits, push et CI sont autorisés. Les chantiers de simplification restent pour la 1.4.0, et L-14 est reporté.

Commits sur `release/1.2.0-notation-store`, depuis `f0a98ba` :
- `4dbfa9d` (F-10) : dans la sous-classe des liens de l'Accueil, Échap appelle `Close(validated: false)`, la sortie de la croix. C'est ce que font déjà À propos et Mes statistiques.
- `7b5d71c` (V-07, V-08, V-06) :
  - `CharacterSearch.RowIndexAt` suit la géométrie d'`OnPaint` (séparateurs de 1 px compris) ;
  - le pied dit « 20 sur 98 résultats » (nouveau texte `L.Search_ResultCountCapped`) ;
  - `ClassifyMethodToken` compare aux libellés de la langue courante.
  - « Verr. Maj. » reste sans couleur propre dans les deux langues. Le colorer serait un choix d'affichage, à valider sur maquette en 1.4.0.
- `62c7d4e` (A-11 et Changelog) :
  - **Fichier présent mais illisible** (verrou, droits) : il n'est plus réécrit pendant la session, et le lancement suivant le relit.
  - **Fichier corrompu** : il repart de zéro comme avant. Recopier telle quelle la garde `_loadFailed` de `ConfigManager` aurait bloqué ses sauvegardes pour toujours, le défaut relevé pour la config le 24/09.
  - La perte des clés inconnues au retour arrière reste pour la 1.4.0.

Vérification :
- Mesure locale, en Release, le 25/09 : 18 / 257 / 641 tests. Ce soir-là, `AZERTYGlobal.Tests` a pu s'exécuter sur le poste. S'y ajoutent Python 147/147, 0 littéral d'identité, et `check-doc-versions` sans erreur.
- **22 tests neufs** : `SearchResultListTests` (18 cas) et `UsageStatsReadFailureTests` (4 cas, dont un contrôle qui prouve que le scénario écrit bien quand le fichier est lisible).
- **Mutations**, jouées puis annulées à l'octet près (script `outils/correctifs/mutations.py`). Chacune rétablit l'ancien comportement :
  - V-07 : 7 rouges ;
  - V-08 : 1 rouge ;
  - V-06 : 2 rouges ;
  - A-11 : 2 rouges.
- **F-10 n'a pas de témoin automatique.** Le routage des messages Windows se vérifie à la recette.
- **Candidat** : CI [36180703298](https://github.com/AMCF-asso/azerty-global-app/actions/runs/36180703298) sur `62c7d4e`, verte.
  - Tests C# : 18 / 257 / 641.
  - BinSkim : aucun contrôle bloquant, en x64 comme en ARM64.
  - Bundle : `AZERTYGlobal-1.3.0.0.msixbundle`, SHA-256 `7E1487C9D6F29D5FB6CB71DA146D0D96D50B5E5A169ECC76761917CA78F25B66`. L'empreinte est lue dans le journal de la CI (troisième ligne `Get-FileHash`) et reste à vérifier sur le fichier téléchargé.
  - Le candidat `D5FFD859` est remplacé : la recette et le WACK se font sur ce bundle.

Lignes de recette à ajouter, à la suite des lignes 1 à 13 du rapport du 24/09 :

14. **F-10.** Accueil, étape 3 : Tab jusqu'au lien « Guide », puis Échap. L'accueil doit se fermer. Si la case « Lancer au démarrage » est restée cochée sans qu'on y touche, le démarrage n'est pas activé (règle B2).
15. **V-07.** Recherche « cyrillique », à 100 %, avec la Loupe :
    - cliquer la dernière rangée de pixels de la 2e ligne : c'est le caractère de la 2e ligne qui s'insère, pas celui de la 3e ;
    - cliquer un séparateur : rien ne se passe.
16. **V-08.** Recherche « cyrillique » : le pied affiche « 20 sur 98 résultats — Entrée pour insérer ». Pour une requête à moins de 20 résultats, il affiche « N résultats ».
17. **V-06.** App en anglais, recherche d'un caractère tapé avec Maj ou après une touche morte : « Shift » et « then » ont les couleurs de « Maj » et « puis » en français.
18. **A-11**, facultatif, sur le poste :
    - quitter l'app et noter les chiffres de Mes statistiques ;
    - verrouiller `usage-stats.json` depuis PowerShell : `$f = [IO.File]::Open("<chemin>", 'Open', 'Read', 'None')` ;
    - lancer l'app, taper quelques majuscules accentuées, quitter l'app ;
    - libérer le fichier (`$f.Close()`) et relancer.
    - Attendu : les chiffres notés au départ, et le fichier inchangé.

## Limites

- Aucune exécution de l'application. Les effets visuels et le routage des messages sont déduits du code et de la documentation.
- Bancs :
  - seul le moteur a été mesuré en NativeAOT ;
  - les autres zones l'ont été sur des répliques JIT, qui donnent des ordres de grandeur ;
  - `SendInput`, l'attente UIA réelle et la fréquence des changements de focus ne sont pas mesurés ;
  - la durée d'écriture n'a pas été mesurée sur disque dur ni sous antivirus d'entreprise.
- Analyse transversale :
  - elle repose sur un analyseur maison, sans Roslyn ;
  - la complexité cyclomatique est approchée par comptage ;
  - le code mort est détecté par le texte, donc sous-estimé.
- Les agents d'audit et de contre-expertise sont du même modèle. Pour M-06 (réentrance) et X-01/X-04 (cycle de vie des fenêtres), les contre-expertises recommandent un avis d'un autre fournisseur avant toute décision d'architecture.
- Les gains en lignes sont des estimations non additives, sauf ceux notés « comptés ».

## Fichiers

- `annexe-constats.md` : les 103 constats par zone, sous forme de tableau généré.
- `donnees/constats.json` : tous les constats, verdicts et corrections compris. Il est généré par `outils/construire.py` depuis `donnees/zones/constats-*.json` et `donnees/verdicts.json`. `donnees/metriques.json` contient les mesures transversales.
- `notes/` : les synthèses des six zones et la recherche ponytail.
- `contre-avis/` : les trois contre-expertises.
- `bancs/` : les sources et résultats des bancs (moteur, application, Leçons, visuel, pompe STA). Ils se relancent avec `dotnet run -c Release --artifacts-path <hors dépôt>`.
- `outils/` : les scripts de mesure et de génération. Pour tout relancer : `python outils/metriques.py <dépôt> donnees/metriques.json`.
- `GRILLE.md` : la grille commune des auditeurs.
- `page/` : le gabarit et la synthèse de la page privée.
