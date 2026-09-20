# Vérification 05-tests-preuves.md
Citations : 140 total · EXACT 68 · DÉCALÉ 0 · ABSENT 3 · NON VÉRIFIABLE 69

> Les 69 NON VÉRIFIABLE : les 21 lignes de §2.1 et les 37 de §2.2 (fichier + nombre de lignes + nom de type, **aucun numéro de ligne cité**), les 3 motifs de §3 comptés par grep sans ancre, et les 8 lignes de §7 « Non vérifié » (justifications, sorties de commande).
> Neuf citations désignent un fichier **sans** numéro de ligne : le texte y a été trouvé, elles sont comptées EXACT et la ligne réelle est donnée dans la colonne de droite du second tableau.

## Décalées et absentes (détail)

| section | fichier:ligne cité | verdict | vraie ligne ou « nulle part » | citation (≤ 60 car.) |
|---|---|---|---|---|
| §4 (lot B) | `docs/audit-v1.2.0/witness-lot-b.py` (en-tête) | ABSENT | **nulle part** sous cette forme ; variante non accentuée à `witness-lot-b.py:4` (« cense ») | « Un test qui reste vert sur la mutation qu'il est censé at… » |
| §4 (lot D) | `docs/audit-v1.2.0/witness-lot-d.py` | ABSENT | **nulle part** : c'est une sortie formatée ; le gabarit est à `witness-lot-d.py:83` | « ECHEC : 0 occurrence(s) de l'ancre » |
| §4 (lot F) | `docs/audit-v1.2.0/witness-lot-f.py` | ABSENT | **nulle part** sous cette forme ; la liste `[sys.executable, "-m", "unittest", "discover", "-s", "scripts/tests"]` est à `:45` | « python -m unittest discover -s scripts/tests » |

Les trois portent sur la **forme** de la citation (accents restitués, sortie rendue au lieu du gabarit, ligne de commande au lieu de la liste `subprocess`) ; le fichier visé contient bien l'équivalent, aux lignes indiquées.

## Constats à citation confirmée (EXACT ou DÉCALÉ), condensés

| section | fichier:ligne | constat en une phrase | test cité |
|---|---|---|---|
| §1.3 | `src/TrayApplication.cs:1394`, `:1407` | Les deux seuls CS8604 du build Release portent sur le même argument nullable, passé à deux appels. | — |
| §2.1 | `src/StoreReview.cs:37` | `StoreReview.TryShow`, objet même de la 1.3.0, n'est nommé par aucun fichier de test. | aucun |
| §2.1 | `src/TypingEngine.Windows/SecureInputDetector.cs:26` | 218 lignes de détection de champ sécurisé sans aucun test. | aucun |
| §3 cas 1 | `AZERTYGlobal.Tests/Placeholder.cs:8`, `:1-2` | Test à corps vide, zéro assertion ; le remplacement annoncé en en-tête n'a pas eu lieu. | lui-même |
| §3 cas 2 | `AZERTYGlobal.Tests/LessonCoreTests.cs:472-479` | Deux `Assert.Contains` sur le texte source d'un `.ps1` qui n'est jamais exécuté. | LessonCoreTests |
| §3 cas 3 | `FrenchTypographyTests.cs:53-54`, `:73`, `:91` ; `scripts/tests/test_schema_parser_agreement.py:64-65` | Aucune assertion ne contrôle que l'énumération par réflexion rend quelque chose ; la suite Python, elle, garde ce motif. | FrenchTypographyTests |
| §3 cas 4 | `ReviewPromptConfigTests.cs:146`, `:170`, `:174-181`, `:132-134` | Assertion sur une valeur par défaut, rendue non vide seulement par un test voisin. | ReviewPromptConfigTests |
| §3 cas 5 | `LocalizationTests.cs:89-94`, `:71` | « fr » est à la fois la valeur de repli et le défaut : un `AppLanguage` inerte passerait. | LocalizationTests |
| §3 motifs | `KeyMapperMaintainableLayerCoverageTests.cs:443` | Le seul `foreach … .Keys` est une recherche dans un helper, pas une comparaison d'ensembles. | — |
| §3.1 | `ToastActivationTests.cs:37-48`, `:88-126`, `:118` | Seul garde-fou du dépôt portant ses deux témoins réciproques dans la suite elle-même. | ToastActivationTests |
| §3.1 | `SoberChannelTests.cs:80-84` | Chaque entrée de menu est éprouvée dans les deux sens, absente et présente. | SoberChannelTests |
| §3.1 | `VersionAlignmentTests.cs:22-23` | Le test ne voit ni le csproj ni le manifeste, et le dit en commentaire. | VersionAlignmentTests |
| §3.1 | `ResourceAlignmentTests.cs:46` | Une relation recalculée, pas deux constantes comparées. | ResourceAlignmentTests |
| §3.1 | `LessonCoreTests.cs:13-17` | Cinq compteurs figés, lus depuis la ressource embarquée, fragiles à chaque évolution du catalogue. | LessonCoreTests |
| §4 | `docs/audit-v1.2.0/witness-baseline.py:22-25` | Il lit un chemin `WindowsApps` de la 1.1.0 qui n'existe plus sur ce poste. | — |
| §4 | `docs/audit-v1.2.0/witness-lot-b.py:8-10` | Mutations 5 et 6 attendues à zéro rouge : l'angle mort du lot B est assumé en en-tête. | — |
| §4 | `docs/audit-v1.2.0/witness-lot-c.py:3-5` | Mutations 9 et 10 sans autre preuve que le smoke test du lot G. | — |
| §4 | `witness-lot-d.py:41` ; `src/UsageStats.cs:426-427` | L'ancre de la mutation 3 n'existe plus : la ligne a été réécrite par le lot C. | — |
| §4 | `witness-lot-f.py:14`, `:161` | Dix mutations attendues rouges, mais le préambule lève tant que la suite Python est rouge. | scripts/tests |
| §4 | `witness-lot-f-versions.py:18-20`, `:163` | Dix mutations et six restaurations attendues ; sortie INCONCLUANT si le scanner rougit d'abord. | scripts/tests |
| §4 | `scripts/witness-embedded-resources.py:86`, `:112`, `:16` | Il écrit, copie par `shutil.copy2` et lance `dotnet` : ce n'est pas un contrôle statique. | — |
| §4.1 | `docs/audit-v1.2.0/witness-lot-b.py:79-82` | Le juge commun aux trois témoins `.cs` ne lance que `AZERTYGlobal.Tests`. | AZERTYGlobal.Tests |
| §5.1 | `scripts/check-layout-provenance.py:36` | Le script lit par le réseau sur l'ancien compte personnel, pas sur `AMCF-asso`. | — |
| §5.3 | `scripts/list-identity-literals.py:49-53` | `SKIP_DIRS` exclut deux dossiers de production, `TypingEngine.Core` et `TypingEngine.Windows`. | — |
| §5.3 | `ToastActivationTests.cs:26`, `:69` | Trois occurrences du propre motif du scanner, dans un dossier qu'il ne lit pas. | — |
| §5.3 | `src/ProductIdentity.cs:24`, `:43`, `:49`, `:71` | Les littéraux d'identité sont centralisés ; `RepositoryUrl` pointe bien `AMCF-asso`. | list-identity-literals.py |
| §5.5 | `scripts/tests/test_check_doc_versions.py:223` | L'assertion qui échoue compare les erreurs réelles des documents de parc à une liste vide. | lui-même |
| §5.6 | `.github/workflows/ci.yml:59-60` | L'étape des témoins du schéma précède tout `dotnet` dans le job `build`. | scripts/tests |
| §6.1-1 | `ForegroundMonitorMockedTests.cs:171-177` | Cas de repos : seul, c'est une assertion sur la valeur par défaut. | lui-même |
| §6.1-2 | `ForegroundMonitorMockedTests.cs:180-191` | Témoin réel : le même objet rend `false` puis `true` sur le seul changement de premier plan. | lui-même |
| §6.1-3 | `ForegroundMonitorMockedTests.cs:194-205` | Réciproque : `Recompute()` ramène la propriété à `false`. | lui-même |
| §6.1 | `src/TypingEngine.Windows/ForegroundMonitor.cs:117-123` | La garde existe bien côté production. | ForegroundMonitorMockedTests |
| §6.1 | `src/TrayApplication.cs:826-832`, `:540` | Le câblage du chien de garde à la boucle de messages n'est nommé par aucun test. | aucun |
| §6.2-1 | `ReviewPromptConfigTests.cs:141-155` | Témoin annoncé par le commit ; le plafond dur à 2 est éprouvé dans le même corps. | lui-même |
| §6.2-2 | `ReviewPromptConfigTests.cs:163-172` | Prouve que `reviewPromptClicked` reste lu, donc que le rattrapage est borné. | lui-même |
| §6.2-3 | `ReviewPromptConfigTests.cs:174-181` | Rend le témoin principal non vide en prouvant que le fichier est réellement lu. | lui-même |
| §6.3 | `src/TrayApplication.cs:509`, `:507-510` ; `KeyboardHook.cs:80`, `:171` | `ShortcutsWhilePassThrough` a trois sites de production et zéro en test. | aucun |
| §6.3 | `AZERTYGlobal.Tests/QuickWinsTests.cs:28-39` | Le seul test voisin est antérieur au correctif et ne couvre pas son prédicat réel. | QuickWinsTests |
| §6.4 | `src/SettingsWindow.cs:137, 298, 313, 403, 576, 910, 1080, 1355` | `FitWindowToContent` et `ShiftLayout` ont huit sites de production et zéro en test. | aucun |
| §6.5 | `DailyChallengeTests.cs:98` ; `AutoStartNudgeTests.cs:9` | Le port `1b025d0` est le seul des quatre à arriver avec ses tests. | ces deux fichiers |
