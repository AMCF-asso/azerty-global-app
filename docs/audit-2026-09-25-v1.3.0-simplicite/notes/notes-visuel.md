# Zone visuel (V-) — synthèse, f0a98ba

**Vue d'ensemble.** La prémisse de départ était fausse. À f0a98ba, le clavier virtuel n'est **pas** sur `KeyboardRenderer.Draw` : CH4b (fb0b854, b8dcdda) n'existe que sur `main`, et seulement en local. En 1.3.0, deux moteurs de rendu du clavier coexistent, et même un troisième dans LearningModule (V-01). Sur `main`, CH4b laisse quelques restes morts (V-02).

La recherche n'est pas lente : 2,2 ms par frappe, et 0,26 ms en passant les comparaisons en ordinal (V-03). Ce qui la rend « à améliorer » tient surtout à sa mise en page et à ce qu'elle affiche : clic décalé, fenêtre dimensionnée sans tenir compte du défilement, compte plafonné à 20, couleurs en dur en français (V-06, V-07, V-08, V-15). Le reste relève surtout de l'économie : cinq lecteurs du même JSON, du code mort, des aides GDI existantes mais non réutilisées.

**Points forts à garder**
- La normalisation des accents est faite une fois au chargement (`Normalized*` précalculés), et une seule fois par frappe pour la requête ; le scan linéaire de 1 005 entrées suffit.
- Aucune fuite GDI trouvée : chaque `Create*` a son `DeleteObject`, les polices sont en cache, et le compteur d'objets GDI de la réplique reste stable. `LayerIndicatorWindow` (179 l.) est court et suit bien le DPI, avec `IndicatorSize` testable.
- Les gardes nées de vrais bugs sont nettes et testées : cible d'insertion refusée sur le shell (B4, `SearchTargetShellTests`), nom accessible du champ (N8), identifiants de touches figés par un test (`KeyCapLabelTests`), try/finally de SEV-A2-01.

**Chiffres** (réplique hors application, JIT ; l'application est AOT)
- `character-index.json` : 605 678 octets, 1 005 entrées. Chargé au démarrage en 17 ms à chaud (27–32 ms au premier passage), avec 7,6 Mo alloués et 1,86 Mo retenus.
- Recherche : environ 11 000 comparaisons de mots par frappe, 115 Ko alloués.
- Repeint du clavier (réplique) : chemin VK de f0a98ba 0,82 ms ; chemin KeyboardRenderer 2,26 ms. Mettre pinceaux et stylos en cache ne gagne que 5 %, car le coût est dans `DrawTextW` : **ne pas optimiser les objets GDI**.
- En cours de frappe, le clavier virtuel se repeint entièrement deux fois par touche (appui, puis minuterie de 120 ms), soit ≈1,7 ms d'UI par frappe : négligeable.
- 18 constats : 2 majeurs, 8 moyens, 8 mineurs. Gain estimé ≈ −550 lignes dans la zone et ses voisins ; les chiffres ne s'additionnent pas strictement (V-04 inclut du hors-zone).

**Signalements hors zone** (non instruits)
- `LearningModule.cs:2373-2557` (`PaintKeyboard`, plus `PaintKeyCharacters` à partir de 2614) : troisième moteur de clavier (Entrée ISO recodée), resté tel quel même sur `main`.
- `LearningModule.cs:660-736` et `LessonHintProvider.cs:72-138` : 4e et 5e lecteurs de l'index ; `CharNamesOverride` existe en double (LearningModule et KeyboardRenderer).
- `LessonsWindow.cs:1501-1508` : à chaque repeint, les 62 infobulles sont construites (StringBuilder, recherche du nom, ToUpperInvariant) au lieu de l'être au survol.
- Double tampon recopié dans 7 autres fenêtres (V-16).
- `TrayApplication.GetDeadKeySymbol` et `DisplayGlyph` n'existent que sur la release ; `main` utilise `IsCombiningMark`. Divergence à réconcilier si CH4b est repris.
- Les commits CH4b ne sont dans aucune branche distante visible localement (`git branch -r --contains fb0b854` ne renvoie rien).

**Limites.** J'ai lu intégralement les 8 fichiers de la zone et `L.Search.cs` ; les tests de zone ont été survolés. L'application n'a pas été lancée : les effets visuels de V-07, V-11 et V-15 sont déduits du code, pas vus à l'écran. Aucune mesure n'a été faite en AOT. Les tests `AZERTYGlobal.Tests` n'ont pas été exécutés (Application Control). La parité avec `tester-search.js` du site n'a pas été vérifiée.
