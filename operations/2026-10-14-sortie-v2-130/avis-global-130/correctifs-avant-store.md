# Correctifs de l'avis global 1.3.0 avant le Store — 8 retouches

Résumé : 8/8 retouches faites, une par commit (plus un test de suivi et le Changelog), sur la
branche `claude/ag-correctifs-avis-130` (worktree `components/ag-correctifs-avis-130`), partie de
`eb9de94`. Rien n'est poussé, rien n'est publié. Suites : AZERTYGlobal.Tests 1824 réussis + 3
ignorés = 1827 (0 échec) ; TypingEngine.Core.Tests 18/18 ; TypingEngine.Windows.Tests 380/382, les
2 échecs étant `HookReelTests`, qui exigent le bureau d'entrée réel (voir « Réserves »). Notes de
version de la fiche : FR 1 412 caractères, EN 1 344 (limite 1 500), recomptés.

## Commits (du plus ancien au plus récent)

| # | Retouche | Commit |
|---|---|---|
| 1 | Libellé doublé du sélecteur (C1) | `7808ab2`, suivi `41cadac` (test du parcours) |
| 2 | Fiche Store : « en un appui » → « avec le Verr. Maj. » | `fb04054` |
| 3 | Fiche Store : écran de verrouillage dans les limites connues | `c54f5f5` |
| 4 | Sigle « VM » en clair | `23790cb` |
| 5 | Double virgule « MONTRÉAL,, sœur » | `3f718a2` |
| 8 | « 5 changements » partout | `a53ee79` |
| 7 | Note RGPD des établissements | `9a77d57` |
| 6 | Changelog (C2) + entrée des retouches | `bddda1d` |

## Retouche par retouche : fait, preuve

1. **Sélecteur.** `LessonNames.SameTitle/Join` (`src/LessonCatalog.cs`) : quand le module et la
   leçon portent le même titre, le bouton du sélecteur, la carte « Reprendre » et les noms lus par
   le lecteur d'écran n'écrivent qu'un nom. La liste hiérarchique déroulée est inchangée. Preuve :
   `AvisGlobal130Tests` (catalogue réel, fr et en, noms des cibles du bouton) ; capture de la carte
   « Commencer » : « Prise en main » une fois. `ParcoursAccueilTests` construisait « module leçon »
   à la main et passait à vide : il compare maintenant à `L.Learning_HeaderTitle`.
2. **« en un appui ».** Fiche Store FR l. 22 et EN l. 152. Les autres occurrences (points forts,
   mécanismes) disaient déjà « avec le Verr. Maj. ».
3. **Écran de verrouillage.** Une phrase dans « Jeux et bureau à distance », FR et EN : écran de
   verrouillage, mot de passe Windows, UAC et BitLocker suivent la disposition de Windows.
4. **« VM ».** Le test de troncature ne tranche pas seul (les cases s'élargissent avec le texte) :
   arbitré par mesure. « Verr. Maj. » en clair dans les cases de gestes (Leçons, Recherche,
   Révision) ; le sigle reste sur la touche du mini-clavier (1,75 unité, 42 px : le nom en clair
   ne tient pas, test) et dans la bande de révision de l'accueil si le nom en clair fait perdre
   des caractères à revoir. Preuve : captures `lecons-aujourdhui-revision-fr-100.png` et
   `recherche-fr-100.png` (3 caractères + « +2 » comme avant), `MiniKeyboardRendererTests`
   inchangés.
5. **Double virgule.** Cause : mots d'exemple coupés aux espaces seulement, donc « MONTRÉAL, »
   arrivait avec sa virgule et `string.Join(", ")` en ajoutait une. `LessonsHomeLogic.PickWords`
   retire la ponctuation finale collée (sauf si c'est le caractère à revoir), `JoinWords` n'en
   ajoute pas à un mot qui en porte une. Affichage seul : aucun texte à taper ni consigne modifié,
   empreintes de progression intactes. Preuve : `AvisGlobal130RevisionTests` (5 cas) et capture.
6. **Changelog (C2).** Citations remises à jour : « Séance du jour déjà comptée. », « Prise en
   main : N exercices », « Prise en main » à la place d'« Initiation » (5 lignes ; Antoine en
   listait 3, les 2 autres citaient le même module), et l'espace fine insécable devant « ; ! ? »
   limitée aux textes de l'interface. Aucun « Initiation » ne reste. Nouvelle entrée « Retouches
   de l'avis global (10 novembre, avant le Store) ».
7. **Note RGPD.** Voir « Écarts à connaître ». ADML FR/EN et `politiques-exemple.reg` alignés.
   Preuve : `scripts/tests` 159/159 (dont `test_admx_agreement` 21/21), ADML bien formés, `.reg`
   toujours en UTF-16 avec BOM.
8. **« 5 changements ».** Fiche Store, description longue et point fort, FR (l. 32, 92) et EN
   (l. 160, 220). Aucun autre texte public du dépôt ne cite 12 (hors `docs/` et Changelog
   historique).

## Compteurs mesurés (état du code au commit `3f718a2`, inchangé depuis : les commits suivants sont du texte)

- AZERTYGlobal.Tests : 1 824 réussis, 3 ignorés, 0 échec, total 1 827 = 1 810 annoncés + 17 nouveaux
  (2 + 10 cas dans `AvisGlobal130Tests`, 5 dans `AvisGlobal130RevisionTests`). Les 3 ignorés ne
  viennent pas de ces retouches.
- TypingEngine.Core.Tests : 18/18.
- TypingEngine.Windows.Tests : 380/382. Échecs : `HookReelTests.AvecLeHookDuProduit_…` et
  `SansHookDuProduit_…`, « SendInput a accepté 0 événement(s) sur 2 ». Ces tests injectent de
  vraies touches et ne tournent pas sur le bureau caché de `BureauCache.ps1` (dit dans le test
  lui-même). Aucun fichier de `TypingEngine.*` n'a changé depuis `eb9de94`. À rejouer par Antoine
  hors stream, sur le bureau réel.
- `scripts/tests` (Python) : 159 réussis.
- Fiche Store, mesures (`count.py`) : description courte FR 207, EN 215 ; description longue FR
  4 030 (4 049 avant retouche 8), EN 3 607 (3 631) ; notes de version FR 1 412 (1 423 en CRLF), EN
  1 344 (1 355) ; ≤ 1 500 dans les deux cas. Les retouches 2, 3 et 8 ne touchent pas aux notes.

## Écarts à connaître (décisions ou vérifications)

- **Note RGPD l. 7.** Texte validé repris, avec un ajout : « et l'endroit de l'application d'où
  part le lien ». `ProductIdentity.DiagnosticUrl` envoie aussi `src=` (origine du clic) ;
  « rien d'autre » aurait été inexact. À retirer si Antoine préfère le texte exact.
- **Note RGPD, chemins.** MSIX : `…\Packages\<PFN>\LocalCache\Local\AZERTY Global\` (le manifeste
  ne désactive pas la virtualisation, mesure de recette 1.3.0 et audits). Les deux ADML et le
  `.reg` disaient la forme non redirigée : corrigés. Hors paquet, le code écrit **à côté de
  l'exécutable** (`ConfigManager.GetConfigPath`, `LogDirectory`), pas dans `%LocalAppData%` comme le
  disait la note : corrigé aussi, et l'intro « dans le profil de l'utilisateur » précisée.
- **`LISEZMOI-DSI.md`.** Il vit hors dépôt (`sources/legacy/…/Windows Store-MSIX`). Le renvoi est
  remplacé par les commentaires de `politiques-exemple.reg`. Le fichier lui-même n'est pas touché.
- **Ouvert : note RGPD l. 55** (« Aucune donnée ne parvenant à l'association, l'AMCF n'est
  destinataire d'aucun traitement… »). Elle est maintenant en tension avec la l. 7 (version et
  Windows reçues par le site sur clic). Position juridique : non modifiée, à trancher par Antoine.
- **Ouvert : phrase doublée dans la fiche** (retouche 3) : « la disposition de Windows s'applique »
  revient deux fois de suite avec la phrase sur les administrateurs. Fusion possible, non faite.
- Non vérifié : `knowledge/claims/chiffres-publics.md` (cité par la fiche), hors dépôt ; il peut
  encore dire « 12 ». `check-doc-versions.py` : inconcluant (documents hors dépôt absents du clone).
- Constats « APRÈS » et espace fine dans les guillemets : non touchés, comme demandé.
