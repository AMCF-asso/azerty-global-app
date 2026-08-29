# Patchs de la session du 2026-08-29 — chantiers CH1 et CH2

Les scripts qui ont écrit les six commits `f9e7a8e` → `fa22664`. Ils sont **rejoués une fois et
conservés comme méthode**, pas comme outillage : leurs ancres ne correspondent plus au code
depuis qu'ils ont été appliqués, et les rejouer échouerait proprement sur un compte d'occurrences.

Motif de conservation : le dépôt interdit `Edit` sur un `.cs`, et chaque patch montre comment
la contrainte se tient — mesure des fins de ligne avant écriture, refus si un CRLF traîne dans
un lot annoncé LF pur, recomptage des terminaisons après remplacement, et vérification que
chaque ancre apparaît exactement une fois avant qu'un seul octet ne soit écrit.

| Script | Ce qu'il a fait |
|---|---|
| `patch_class_brush.py` | La brosse de fond de classe cesse de venir du cache partagé de `Theme` (§12 de l'audit) |
| `patch_test.py` | Témoin `FondDeClasse_NeConsommePasLaBrosseDuCacheDeTheme` |
| `patch_stepper.py` | Les flèches empilées du dialogue de pause deviennent un moins et un plus |
| `patch_board.py` | La planche d'états gagne la rangée des compteurs |
| `patch_focus.py` | Anneau de focus des compteurs, et l'espacement qui lui fait place |
| `patch_layers.py` | Migration de Couches maintenables |
| `patch_stats.py`, `patch_stats2.py` | Migration des Statistiques, en deux passes |
| `patch_conflict.py` | Migration du Conflit de disposition |
| `patch_audit.py`, `patch_claudemd.py` | Les deux écritures de doctrine hors dépôt app |

Les quatre autres scripts mesurent plutôt qu'ils n'écrivent, et **ceux-là se rejouent** sur
n'importe quelle capture : `top.py` donne les couleurs dominantes d'un lot de PNG, `band.py` la
couleur dominante par bande horizontale, `probe.py` le décompte d'une image, `alpha.py` son
canal alpha. C'est `top.py` qui a montré que le client de Durée de pause était blanc à 62 % là
où l'œil lisait « gris système », et c'est une mesure de ce genre qui doit trancher chaque
constat visuel de la refonte.

---

*Dernière mise à jour : 2026-08-29*
