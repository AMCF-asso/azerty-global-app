# AZERTY Global 1.3.0 — audit avant Microsoft Store

Audit du **22 septembre 2026**, sur `67601c1` (`release/1.2.0-notation-store`). Code produit inchangé pendant cette passe. Paquet annoncé : **1.3.0.0**, x64 et ARM64.

## Décision proposée

**Ne pas soumettre le paquet local actuel.** Il précède douze fichiers de production modifiés. Un paquet CI contenant le code actuel a été récupéré et son attestation vérifiée ; il constitue un meilleur candidat pour la recette, mais n'a pas de recette native ni de WACK rattaché à son empreinte dans les preuves disponibles.

**La validation automatisée est bonne, la validation de publication est incomplète :** 695 tests C# et 125 tests Python passent ; schéma, références et provenance des trois ressources sont conformes. Cela ne valide ni les interactions réelles Windows ni les champs actuellement enregistrés dans Partner Center.

Les points à traiter avant le feu vert sont : corriger le défaut de changement de fenêtre reproduit par un témoin d'audit, choisir et figer le bon paquet, corriger la déclaration de consentement, puis effectuer la recette du paquet final. Le risque distinct de reprise du détecteur UIA est classé P2. Les reports déjà décidés pour la 1.3.1 sont conservés.

## 1. Objets réellement audités

| Objet | Identification | Conclusion bornée |
|---|---|---|
| Source locale | `67601c1` | Trois suites C# reconstruites et exécutées en Release le 22/09 |
| Paquet local versionné et alias stable | SHA-256 `a71544702d95322f3c5ad9095cb10d65e2a58f76b9a6f0aedd967f1ecdd10278`, 6 835 788 octets | Identiques entre eux, mais périmés par rapport aux sources |
| Paquet CI récupéré dans `evidence/ci-package/` | SHA-256 `00b4dc8cf1c3929bb50c983b27999521b9d15d280212780a597e17ea76b83154`, 6 839 033 octets | Intégrité des blocs vérifiée, provenance GitHub vérifiée |
| Code du paquet CI | commit `49187578ac918be8d66f1585964a09ee55190b84`, run `35715039350` | Aucune différence avec HEAD dans `src`, `scripts`, `.github`, manifeste et assets ; seuls Changelog et Fiche Store ont évolué depuis |
| WACK présent | `ReportGenerationTime=9/21/2026 7:20:59 PM`, `OVERALL_RESULT=PASS`, `X64_ONLY=TRUE` | Antérieur aux deux candidats considérés ici ; ne certifie aucun des deux par son seul numéro 1.3.0.0 |
| Application active sur ce poste | 1.1.0.0, identité AMCF, famille `yh4wjq6y8vthy` | Ce n'est pas la 1.3.0. Le manifeste AMCF explique la différence avec le PFN Store `w9kghr08zmhbg` ; aucun défaut d'identité retenu |

La [CI examinée](https://github.com/AMCF-asso/azerty-global-app/actions/runs/35715039350) publie les deux architectures et une attestation. Sa conclusion verte ne signifie pas que tous ses contrôles facultatifs ont réussi.

## 2. Contrôles exécutés

| Contrôle | Résultat frais | Preuve |
|---|---|---|
| Moteur portable C# | **18/18**, 0 échec, 0 ignoré | `evidence/core.trx`, `core-tests.log` |
| Adaptateur Windows C# | **226/226**, 0 échec, 0 ignoré | `evidence/windows.trx`, `windows-tests.log` |
| Application C# | **451/451**, 0 échec, 0 ignoré | `evidence/app.trx`, `app-tests.log` |
| Scripts Python | **125 tests, OK** | `evidence/python-tests.log` |
| Nouveau témoin de course A→B→A | **3 contrôles passent, 1 propriété de sécurité échoue** ; échec attendu qui reproduit A22-04 | `race-witness/evidence/foreground-aba-witness.trx` |
| Disposition JSON | Schéma, compteurs, références conformes | `evidence/layout-schema.log` |
| Ressources publiques canoniques | Disposition, index de caractères, leçons : trois empreintes identiques | `evidence/layout-provenance.log` |
| Identité produit | 0 littéral hors `ProductIdentity` | `evidence/identity.log` |
| Versions documentaires, mode courant | 0 erreur ; **32 attentes** de bascule du kit entreprises | `evidence/doc-versions.log` |
| Versions documentaires, mode publication | Échec attendu tant que ces 32 attentes subsistent | `evidence/doc-versions-release.log` |
| `Verify-Release.ps1` | Code 0 sur le paquet local ; avertissement TO-DO absent | `evidence/verify-release.log` |
| Fraîcheur source → publish | **Échec x64 et ARM64**, 12 fichiers plus récents | `evidence/freshness.log`, `package.json` |
| Intégrité MSIX | Tous les blocs et hashes de fichiers vérifiés sur les deux architectures des deux candidats ; aucun PFX/CER/PDB/source/bundle imbriqué détecté | `evidence/package.json`, `ci-package.json` |
| Architecture / protections PE | Machine x64 `0x8664`, ARM64 `0xaa64` ; flags ASLR, high entropy, NX et CFG présents | Mêmes inventaires JSON |
| Provenance CI | `gh attestation verify` sort en 0 ; sujet et commit cohérents | `evidence/ci-attestation.json` |
| NuGet du projet applicatif | Requête en ligne réussie, aucune vulnérabilité de package rapportée | `evidence/nuget-vulnerabilities.log` |
| BinSkim local sur les EXE CI | Contrôles d'en-tête favorables, **analyse incomplète : PDB exact absent**, code 1 sur les deux architectures | `evidence/binskim-*.sarif`, `binskim-summary.json` |

Les deux avertissements C# `CS8604` dans `TrayApplication.cs:1522,1535` sont préexistants. Le booléen `hasTarget` garde les appels ; aucune panne correspondante n'a été démontrée. Ils ne sont pas promus en défaut fonctionnel.

Le premier passage Python manquait de `jsonschema` ; la dépendance a été installée dans le seul dossier de cet audit puis les contrôles concernés ont été rejoués avec succès. Les premières lectures réseau étaient interdites par le bac à sable ; elles ont été rejouées avec accès approuvé. `evidence/checks.json` conserve ces tentatives et leurs codes, sans transformer un contrôle empêché en succès.

Le scan NuGet est limité : les trois projets de production n'ont pas de `PackageReference` applicatif explicite. Ce résultat n'est pas une certification de sécurité du runtime .NET embarqué ni des API Windows.

## 3. Constats à traiter

P1 : à résoudre ou arbitrer avant soumission. P2 : défaut ou dette vérifiable, avec risque et correction précisés. P3 : cohérence documentaire ou finition. « Statique » signifie que le code démontre le mécanisme, sans observation de son rendu ou de sa fréquence sur une machine réelle.

### A22-01 — P1 — Le paquet local ne contient pas les dernières corrections

**Preuve exécutée.** Le garde `Assert-PublishNotStale` refuse les deux publishes. Le paquet local date du 21/09 à 21:49, les publishes de 21:46, heures de Paris. Douze fichiers de production sont plus récents : `AboutWindow`, `DialogNavigation`, `LearningModule`, `OnboardingWindow`, `ProductIdentity`, `SettingsWindow`, `TrayApplication`, `UsageStatsWindow`, `Win32`, `ForegroundMonitor`, `KeyboardHook`, `KeyMapper`.

**Impact.** Soumettre `msix/AZERTYGlobal-1.3.0.0.msixbundle` livrerait un autre état que celui dont les 695 tests viennent de passer. `Verify-Release` reste vert car il compare le publish au paquet, pas aux sources : ce vert est exact mais insuffisant.

**Action.** Retenir le paquet CI vérifié ou reconstruire les deux publishes et empaqueter. Consigner l'empreinte choisie dans la fiche de recette. Le paquet CI a été téléchargé dans les preuves, sans remplacement silencieux du paquet local.

### A22-02 — P1 — La recette native et le WACK ne suivent pas le candidat final

**Preuve documentaire et chronologique.** Le rapport WACK existant précède même le paquet local de 21:49. Il porte `X64_ONLY=TRUE`. Le suivi du 21–22/09 demande encore reconstruction et recette finale (`../audit-2026-09-20-v1.3.0/revue-code-2026-09-21-sans-vm.md:256`).

**Impact.** Les tests sur doubles Win32 ne prouvent pas l'activation COM des toasts, l'ordre réel des messages, les hooks, les pertes en jeu, le focus ou une mise à jour Store 1.1 → 1.3. Aucune exécution ARM64 n'est démontrée par le build ARM64.

**Action.** Exécuter la recette jointe sur l'empreinte retenue. WACK est une porte du processus de release de ce projet ; son succès n'est pas à lui seul une garantie d'acceptation Microsoft.

### A22-03 — P1 — La note de certification annonce un consentement que le flux n'impose pas

**Confirmé par lecture croisée.** `msix/Fiche Store.md:329` qualifie l'onboarding d'écran de consentement conforme à 10.2.8. Or `TrayApplication.cs:281` appelle `LoadAndStart` avant la décision d'afficher l'accueil ; `:345–357` crée et installe le hook. Le hook et le tray sont actifs par défaut (`TypingEngine.Windows/KeyboardHook.cs:46`, `TrayApplication.cs:168`). Les actions de l'accueil sont Essayer, Suivant et C'est parti ; X/Échap ferment. Aucune acceptation préalable ne conditionne le remappage.

**Impact.** La déclaration donnée au certificateur est matériellement plus forte que le comportement. L'ancien audit AG130-35 avait validé cette déclaration à tort. Ce constat ne prédit pas un refus Microsoft et ne tranche pas à lui seul l'interprétation de 10.2.8 pour ce produit.

**Action.** Mettre la note en accord avec le comportement observé et décider explicitement si un accord préalable est requis : dans ce cas, conserver le hook inactif ou transparent jusqu'au bouton d'activation, puis mémoriser ce choix. Tester premier lancement, fermeture sans accord, relance et mise à jour.

### A22-04 — P1 — Un aller-retour de fenêtres peut neutraliser la suspension anti-cheat

`TypingEngine.Windows/ForegroundMonitor.cs:206–212` capture un HWND puis appelle deux méthodes qui relisent indépendamment le premier plan (`Win32Api/RealWin32Api.cs:43–53,100–105`). La référence du snapshot est atomique ; l'acquisition de ses éléments ne l'est pas. `GetEmitContext` (`ForegroundMonitor.cs:116`) ne revalide ensuite que le HWND.

**Scénario reproduit.** A (`valorant.exe`) → B (`notepad.exe`) → A pendant un recalcul : le HWND A reçoit le nom et le mode de B. La validation finale « HWND courant = HWND du snapshot » accepte l'ensemble incohérent et `GetEmitContext` retourne `Default`, alors que la cible A doit donner `DisabledAntiCheat`. Le mock standard ne reproduit pas la relecture interne de `TryGetForegroundProcess`.

**Preuve exécutée.** Le témoin isolé [ForegroundAbaRaceWitnessTests.cs](race-witness/ForegroundAbaRaceWitnessTests.cs) reproduit les relectures de l'API réelle avec un décorateur du mock. Trois contrôles passent : A stable est suspendue, B stable est autorisée, et la séquence A→B→A est effectivement consommée. L'unique assertion de sécurité en course échoue avec `Expected: DisabledAntiCheat / Actual: Default` ; résultat détaillé dans [RESULT.md](race-witness/RESULT.md). C'est un défaut logique reproduit, sans mesure de sa fréquence ni observation d'un anti-cheat réel. Ce témoin ne démontre pas un contournement des protections de mot de passe.

**Action.** Faire inspecter explicitement le HWND capturé par les méthodes de collecte et revalider l'identité avant publication. Tester aussi le cas stable, A→B et A→B→A avec consommation effective de chaque étape ; conserver un cas positif de suspension.

### A22-05 — P2 — La CI verte masque un BinSkim qui ne démarre pas

**Reproduit dans les journaux CI.** `.github/workflows/ci.yml:87` installe le package comme outil .NET. Le run répond `Package microsoft.codeanalysis.binskim is not a .NET tool.`, puis ne trouve pas `binskim`. `continue-on-error: true` laisse le job vert et aucun SARIF n'est téléversé. Le Changelog le signalait déjà : le nouvel audit en isole la cause.

La distribution autonome Microsoft a été téléchargée dans `dependencies/`, version 4.4.9.11. L'option `--pretty-print` utilisée dans la CI est également refusée par cette version. Après correction de la commande dans le seul outil d'audit, les EXE CI sont analysables en partie mais les règles nécessitant leur PDB exact ne peuvent pas être exécutées. Les protections CFG/ASLR vérifiées ne constituent pas un BinSkim complet.

**Action.** Installer la distribution autonome documentée, fixer sa version, utiliser ses options acceptées et scanner les publishes avec leurs PDB dans la CI. Décider une règle explicite d'échec en cas d'absence de rapport ou d'analyse incomplète. [Documentation Microsoft BinSkim](https://github.com/microsoft/binskim#for-users).

### A22-06 — P2 — Le lien de réinitialisation des raccourcis reste inaccessible au clavier

**Statique, nouveau.** `SettingsWindow.cs:473–477` crée un STATIC cliquable sans `WS_TABSTOP`. `LinkSubclassProc` (`:1905`) ne gère que le survol. À la différence des liens d'À propos, il ne traite ni Entrée ni la négociation de dialogue.

**Impact.** Une personne utilisant seulement le clavier ne peut pas atteindre normalement cette commande de récupération. Critère de référence : WCAG 2.1.1, source **review**, sans règle axe/pa11y applicable à cette UI native.

**Action.** Préférer un bouton natif, ou appliquer la sémantique clavier des liens déjà corrigés en ajoutant un indicateur de focus visible ; tester Tab, Maj+Tab, Entrée et retour du focus après confirmation.

### A22-07 — P2 — Le focus peut se déplacer dans une partie invisible de Paramètres

**Statique, dette toujours présente.** `SettingsWindow.cs:1214` traite `WM_VSCROLL` et `:1246–1252` la molette ; aucune amenée automatique du contrôle focalisé dans la zone visible n'est présente. La molette divise en outre le delta par 120 sans accumulation.

**Impact à mesurer.** Sur petite zone de travail ou fort DPI, Tab peut atteindre un contrôle hors écran ; des petits deltas de pavé tactile peuvent être perdus. Les correctifs de navigation ne prouvent pas le défilement réel. Références : WCAG 2.1.1 et 2.4.11, source **review**.

**Action.** Faire défiler vers le contrôle recevant le focus, couvrir les commandes clavier adaptées et accumuler les deltas de molette. Recette à 175–200 %, fenêtre réduite, souris et pavé tactile.

### A22-08 — P2 — Une détection UIA défaillante peut conserver « non sensible »

**Risque statique, sans panne COM provoquée.** `SecureInputDetector.cs:85` retourne la dernière valeur connue après 30 ms. `EnsureWorker` (`:90–104`) teste seulement l'existence de `_worker`. `WorkerLoop` quitte si `CoInitializeEx` échoue (`:110`) ; une requête COM bloquée n'est pas remplacée. La dernière valeur, initialement fausse, peut rester fausse indéfiniment.

**Limite importante.** La détection native `ES_PASSWORD` reste disponible. Le risque concerne surtout les champs de navigateur dépendant d'UIA et la suspension des fonctions avancées ; aucun enregistrement ou envoi du contenu d'un mot de passe n'a été trouvé. Le remappage ordinaire est délibérément conservé dans un champ reconnu.

**Action.** Représenter l'état inconnu, contrôler la santé du worker et définir une stratégie bornée de reprise. Tester le démarrage COM refusé, le timeout prolongé et le passage champ normal → mot de passe ; ne pas considérer une ancienne réponse comme une preuve de sécurité du focus courant.

### A22-09 — P2 — La reformulation UIPI confond absence de cause et absence d'échec

**Documentaire, nouvel écart de lecture de source.** `Changelog.md:70` indique que le blocage UIPI n'est signalé ni par la valeur de retour ni par `GetLastError`. Microsoft indique que la fonction échoue sous UIPI et que ces retours ne permettent pas d'attribuer cet échec à UIPI. Il s'agit de l'identification de la **cause**, pas d'une garantie de réussite apparente.

**Action.** Corriger cette explication et la preuve attendue : le compteur couvre un retour nul, il ne détermine pas sa cause et ne prouve pas que le texte a effectivement atteint la cible. Le report déjà accepté du mécanisme de récupération reste inchangé. [Référence SendInput, Return value](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput#return-value).

### A22-10 — P2/P3 — Accessibilité des zones dessinées et finitions restant à mesurer

Les résultats de recherche, certaines actions des leçons et le clavier virtuel sont dessinés en GDI sans fournisseur accessible identifié (`CharacterSearch.cs:1298,1403`, `LessonsWindow.cs:1926`). Les contrôles Win32 standards restent accessibles : il serait faux de qualifier toute l'application d'inaccessible. Vérifier avec Narrateur/NVDA les noms, rôles, sélection et annonces ; référence WCAG 4.1.2, source **review**.

Le contraste élevé n'est pas pris en compte par les couleurs codées en dur ; `Win32.cs:544–547` force une barre de titre sombre. Aucun ratio de contraste ni rendu système n'a été mesuré ici. Enfin six touches mortes restent affichées comme marques combinantes nues (`TrayApplication.cs:2119`, `VirtualKeyboard.cs:1159,1173`) : vérifier leur lisibilité FR/EN avant de conclure au défaut visuel.

### A22-11 — P3 — Les documents contiennent encore des repères périmés

`README.md:72` annonce 412 tests alors que le résultat frais est 695. La fiche Store contient bien les notes 1.3.0 FR/EN ; le constat inverse obtenu en lisant la copie `sources/legacy` a été écarté. Sa date de mise à jour reste au 21/09 malgré l'ajout des dons le 22/09. La date affichée de la page publique de confidentialité ne suffit pas non plus à dater son contenu : le texte courant contient les précisions récentes.

**Action.** Rebaser les compteurs et dates utiles. Les 32 attentes documentaires du kit entreprises bloquent la publication de ce kit, pas à elles seules la soumission du seul MSIX au Store.

## 4. Éléments revérifiés et dettes déjà acceptées

| Sujet antérieur | État retenu le 22/09 |
|---|---|
| CI rouge sur les versions documentaires | Soldé pour le build : scripts locaux et run CI passent |
| Ressources de disposition divergentes | Non constaté : trois comparaisons publiques identiques |
| PFX de test accidentellement embarqué | Aucun certificat, clé privée ou bundle imbriqué dans les deux candidats inspectés |
| `IsDialogMessageW`, pièges Tab, Entrée/Échap | Correctifs et tests présents ; garder la recette native |
| DPI de `LessonsWindow` | Traitement `WM_DPICHANGED` et plafonnement présents ; rendu non mesuré |
| Noms anglais des touches mortes | Code de localisation présent ; rendu non mesuré |
| Raccourcis sous suspension et champs sensibles | Prédicat commun et tests présents ; ne pas les déclarer encore absents |
| R13 touche morte au changement d'application, R15 bulles shell, R16 URL diagnostics | Dans le code courant et le paquet CI, pas dans le paquet local périmé |
| R7 repli Alt+code / scan codes droits | **Report 1.3.1 choisi par Antoine le 22/09**, conservé, sans nouveau blocage automatique |
| Refus `SendInput` : frappe perdue malgré compteur/log | **Report fonctionnel déjà choisi**, conservé. Ni les tests ni le compteur ne garantissent la réception réelle par la cible |
| Politique de confidentialité et mention dons | Texte public détaillé et mention AMCF/HelloAsso dans la fiche canonique ; ne pas rouvrir AG130-04 sur la seule ancienne date |

Le choix exact « ne pas récupérer immédiatement la frappe refusée » est écrit dans `KeyMapper.cs:1134–1141`. Il est distingué du nouveau défaut documentaire A22-09.

## 5. Vérification Microsoft Store

Référentiel temporel : la [politique 7.19](https://learn.microsoft.com/en-us/windows/apps/publish/store-policy-archive/store-policy-7-19) est effective depuis le 14/10/2025. La [7.20 publiée](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies) annonce une prise d'effet le 22/10/2026 : ne pas la présenter comme déjà applicable le 22/09.

Le manifeste inspecté déclare le nom et l'éditeur Store attendus, `1.3.0.0`, Windows Desktop minimum `10.0.17763.0`, les langues fr-FR/en-US, les deux architectures dans le bundle et la seule capacité `runFullTrust`. Les extensions de démarrage et d'activation COM sont présentes ; le démarrage automatique est désactivé par défaut.

L'absence de signature de test dans un candidat destiné à l'upload Store n'est pas un défaut en soi : Microsoft resigne le MSIX lors de sa distribution. Cela ne permet pas de l'installer localement comme un paquet de test sans la préparation adéquate. [Exigences de paquet MSIX](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements).

Les pages [confidentialité](https://azerty.global/mentions-legales) et [soutien](https://azerty.global/soutien) sont consultables. La confidentialité décrit le fonctionnement local, les statistiques agrégées et les ouvertures volontaires de liens. La fiche FR/EN explicite les dons à l'AMCF via HelloAsso et le fait que Microsoft ne les collecte pas. La déclaration du prestataire dans Partner Center n'est pas vérifiée ; revoir aussi la mention d'absence de parrainage demandée par la clause sur les dons.

Les captures effectivement publiées, l'âge IARC enregistré, les notes de certification déposées, les capacités approuvées et le paquet attaché au brouillon **n'ont pas été consultés dans Partner Center**. La fiche locale ne prouve pas leur état distant.

## 6. Couverture et limites

Cette passe combine exécution des suites complètes, vérification CI et attestation, inspection des paquets réels, revue indépendante du moteur/sécurité, revue des fenêtres et recherche Microsoft. Le code applicatif n'a pas été corrigé ; les seuls ajouts sont les scripts, rapports, dépendances isolées et preuves de cet audit. Le témoin nouveau est séparé de la suite produit : ses résultats ne sont pas compris dans les 695 tests verts.

La passe UX a lu intégralement `SettingsWindow`, `CharacterSearch`, `VirtualKeyboard`, `LessonsWindow` et `DialogNavigation`. Les autres fenêtres, traductions et points de routage ont été examinés de façon ciblée : il ne s'agit pas d'une revue ligne par ligne de tout le dépôt. La passe moteur a ciblé les hooks, émission, transitions de premier plan, UIA, configuration, statistiques et canaux de distribution.

**Non effectué :** installation ou mise à jour 1.3.0 sur le poste, lancement global du hook 1.3.0, WACK neuf, interactions réelles de jeu/anti-cheat, mots de passe de navigateur, toast COM réel, tests Windows 10 minimum et ARM64, mesure longue de CPU/mémoire/handles, rendu multi-écran, lecteur d'écran et contraste élevé. Aucune conformité WCAG globale ni absence générale de vulnérabilité n'est revendiquée.

Le skill d'accessibilité fourni pilote des pages web ; pa11y/axe ne mesure pas cette interface Win32 native. Les constats d'accessibilité sont donc des revues de code explicitement signalées, pas un score automatisé.

La [recette finale](recette-finale.md) définit les observations qui manquent pour décider de soumettre. Les résultats doivent être rattachés au SHA-256 final ; une recompilation invalide l'association avec un ancien WACK ou une ancienne recette.

## 7. Rejouer et retrouver les preuves

- `run_checks.py` : commandes, logs complets et codes dans `evidence/checks.json` ; un argument sélectionne un contrôle.
- `inspect_package.py` : paquet local ; `--ci` : paquet CI récupéré.
- `collect_ci.py` : provenance et logs du run identifié ; pas de déclenchement de workflow.
- `run_binskim.py 4.4.9.11` : distribution autonome déjà téléchargée, télémétrie désactivée ; les erreurs de PDB restent explicites.
- `evidence/ci-package/` : candidat téléchargé, non installé et non substitué au paquet de `msix/`.

Les outils d'audit sont des moyens de reproduction ; les fichiers de référence restent les journaux, TRX, SARIF et empreintes cités. Un code de sortie vert sans compteur de tests ou sans rapport complet ne suffit pas.
