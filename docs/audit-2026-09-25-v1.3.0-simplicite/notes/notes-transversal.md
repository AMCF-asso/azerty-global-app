# Zone transversale : ce qui ne se voit qu'à l'échelle du dépôt

`f0a98ba`, `src/` : 176 fichiers .cs suivis par git (et non 178), soit 87 de production et 89 de tests. Mesures faites par script (`outils/metriques.py`, stdlib seule, relançable avec `python outils/metriques.py <dépôt> metriques.json`). Résultats bruts dans `metriques.json`. Il y a 12 constats : 3 majeurs, 4 moyens et 5 mineurs. Chacun indique les constats de zone qu'il recoupe (`recoupe`).

**Vue d'ensemble.** Le code est simple à petite échelle : une méthode médiane fait 10 lignes, avec une complexité approchée de 2. Il se complique par concentration et par copie. D'abord, cinq classes portent 41 % du code. Ensuite, chaque fenêtre refait le même squelette Win32, et deux couches d'interop se doublent.

**Chiffres du dépôt.**
- Production : 32 476 lignes, dont 23 587 de code, 5 296 de commentaires et 3 584 vides, pour 1 179 méthodes. Tests : 12 469 lignes pour 652 tests, soit 0,38 ligne de test par ligne de production.
- Lignes de test par ligne de production, selon la zone : moteur 1,43, app 0,48, leçons 0,15, fenêtres 0,13, visuel 0,05.
- Méthodes longues : 24 dépassent 100 lignes (4 001 lignes à elles toutes) et 11 dépassent 150. La complexité approchée atteint 117 au maximum. 35 méthodes dépassent 20 et 6 dépassent 50.
- Procédures de fenêtre : 23, pour 2 362 lignes. 5 classes de 1 900 lignes ou plus totalisent 13 403 lignes.
- Clones, littéraux conservés : 897 des 18 508 lignes normalisées (4,8 %), environ 608 en trop, en 18 familles. Avec les identifiants abstraits : 7,3 %, environ 951 en trop.
- P/Invoke : 189 `DllImport` et aucun `LibraryImport`. 11 fonctions sont déclarées plusieurs fois. 35 constantes Win32 sont redéclarées (46 déclarations de trop), sans aucune valeur divergente.
- Erreurs : sur 111 `catch`, 95 attrapent tout et 36 se taisent. On ne trouve ni TODO, ni `#pragma warning disable`, ni `SuppressMessage`, et seulement 4 `#if DEBUG`.
- Code mort vérifié : 69 membres, environ 151 lignes.
- Nommage : la production est à 99 % en anglais. Parmi les noms de test, 60 % contiennent du français.

**Les 10 plus grosses familles de clones (lignes normalisées en trop, puis zones).**
1. Création de fenêtre et `CreateControls` : 171 (fenêtres, leçons, visuel).
2. En-tête de WndProc : 98 (fenêtres, leçons, visuel).
3. Sous-classe `WM_GETDLGCODE` : 92 (fenêtres, visuel, app).
4. `LinkSubclassProc` : 50 (fenêtres, visuel).
5. DPI initial : 28 (fenêtres, visuel).
6. `SetWindowIcon` : 24 (fenêtres).
7. Liste `HiddenDeadKeysInOnboarding` : 21 (leçons, visuel).
8. `IsRectVisibleOnScreen` : 19 (leçons, visuel).
9. `ResolveTypedCharacter` : 13 (leçons).
10. Structure `GUITHREADINFO` : 13 (app, moteur).

Les emplacements exacts sont dans `metriques.json > clones_mode_A_litteraux_conserves.top_familles`, ou dans la sortie de `python outils/familles.py A 15`.

**Points forts à garder.**
1. La production n'a aucune dépendance NuGet. Le build Release est presque propre : 2 avertissements CS8604, mesurés. Le binaire est durci : AOT, CFG, `/CETCOMPAT`, build déterministe.
2. La discipline locale est bonne : méthodes courtes en médiane, aucun marqueur TODO/HACK, aucune suppression d'avertissement. Les constantes redéclarées gardent toutes la même valeur.
3. Les tests du moteur sont denses et les tests sont peu dupliqués (1,4 %). 85 % des renvois d'audit dans les commentaires donnent aussi le pourquoi.

**Signalements hors zone (non instruits).**
- `L.DisplayCulture` crée une `CultureInfo` à chaque accès (L.cs:14-17), y compris pendant la peinture (UsageStatsWindow.cs:603).
- Zone moteur : `MaintainableLayerManager.ClearAll` (MaintainableLayerManager.cs:314) est mort, et M-09 ne le cite pas. `ProcessKeyCore` et `CurrentFullPath` sont déjà traités par M-02 et M-09.
- COLORREF s'écrit en `0x00BBGGRR` : `0x000078D4` est donc orange, pas le bleu Windows. C'est un piège de lecture dans 6 fichiers.

**Limites.**
- L'analyseur est maison, sans Roslyn. La couverture du découpage a été vérifiée : il ne reste hors membre que les lignes d'attribut et les structures d'une ligne.
- La complexité cyclomatique est une approximation par comptage de mots-clés.
- La détection de code mort est textuelle. Un membre qui porte le même nom qu'un autre membre utilisé n'est jamais signalé, donc le total est sous-estimé.
- La part FR/EN repose sur un lexique : c'est une estimation.
- Ni les tests ni la publication AOT n'ont été lancés : les avertissements IL2xxx/IL3xxx ne sont pas mesurés.
- La lecture manuelle s'est limitée aux candidats et aux familles cités.
