# Corrections de l’audit AZERTY Global 1.3.0 — 22 septembre 2026

Antoine a autorisé ces corrections dans la session de l’audit. Aucun dépôt au Microsoft Store n’est effectué.

## Changements du candidat

- Inspection du processus et du clavier natif liée à un HWND unique ; témoin de non-régression A→B→A.
- Détection UIA liée à la fenêtre attendue par ses ancêtres. Délai, erreur ou résultat d’une autre requête : fonctions avancées suspendues. Un seul worker, reprise après échec d’initialisation et nouvelle vérification des états sécurisés après une seconde.
- Accord explicite avant l’installation du hook. Les configurations anciennes le redemandent une fois. Fermer l’accueil ne l’accorde pas ; le démarrage automatique reste un choix distinct.
- Navigation clavier de l’accueil, bouton natif de réinitialisation des raccourcis, défilement suivant le focus et accumulation des petits deltas de molette.
- Cercle pointillé pour les accents combinants isolés à l’affichage, sans modifier les caractères émis ni les JSON des dispositions.
- BinSkim autonome épinglé par version et empreinte, analyse incomplète bloquante en CI ; fiche Store et explication UIPI corrigées.

## Vérifications du candidat final

Les suites locales passent : **18 tests Core, 235 Windows, 473 applicatifs, soit 726 tests C# ; 136 tests Python**. Les rapports et journaux sont conservés dans `evidence/`. Application Control a refusé un assembly intermédiaire : les exécutions correspondantes, malgré un retour nul de VSTest, portent zéro test et ne sont pas comptées comme succès. La suite du candidat final a bien chargé et exécuté ses 473 tests.

Les publications AOT finales x64 et ARM64, `Pack-MSIX`, `Verify-Release` et le contrôle de fraîcheur réussissent. Les blocs MSIX sont intègres ; les protections PE ASLR, haute entropie, NX et CFG sont présentes ; aucun fichier sensible ou inattendu n’a été trouvé. Deux avertissements de nullabilité préexistants subsistent dans le menu de compatibilité. Les analyses BinSkim avec PDB se terminent, mais leur validation stricte **échoue : cinq avertissements x64 et quatre ARM64**, détaillés ci-dessous.

Les **196 fichiers** de sources, scripts et workflow contrôlés sur disque sont identiques au commit `120b5075ec38e43ee601a7d2498d29424245e0de`. Cette copie a été poussée uniquement sur `ci/verif`, dans l’exception écrite pour les blocages Smart App Control. La branche de travail initiale reste `release/1.2.0-notation-store`, avec les corrections présentes mais non commitées sur cette branche ; `main` et les tags sont inchangés.

Les deux premières CI ont passé les tests et le packaging, puis échoué sur l’envoi SARIF : catégorie commune, puis absence de `message.text`. Le workflow utilise désormais deux catégories, conformément à la [documentation GitHub](https://docs.github.com/en/code-security/how-tos/find-and-fix-code-vulnerabilities/integrate-with-existing-tools/upload-sarif-file), et résout les textes fournis par BinSkim sans changer les verdicts. Les fichiers bruts sont conservés. Le modèle commun omis pour BA4002 par la version épinglée n’est repris que si ses autres déclarations concordent.

**Rectification des vérifications intermédiaires :** le premier validateur ignorait les résultats dont `kind` est absent. Or leur valeur implicite est `fail` ([SARIF 2.1, §3.27.9](https://docs.oasis-open.org/sarif/sarif/v2.1.0/os/sarif-v2.1.0-os.html)). Les anciens retours « aucun contrôle en échec » ne prouvaient donc pas l’absence d’alertes. Le défaut est corrigé et couvert par un test. La politique actuelle bloque tout `fail`, y compris les avertissements : aucune dérogation ni suppression n’a été ajoutée.

La vérification distante [35728008839](https://github.com/AMCF-asso/azerty-global-app/actions/runs/35728008839) est terminée : **726 tests C# et 136 Python réussis**, publications x64/ARM64, packaging, vérification de release et provenance des JSON réussis. Les **deux envois SARIF sont réussis**, ainsi que la conservation des preuves. Le run est **rouge uniquement sur les alertes BinSkim ci-dessous**. Le dépôt du bundle et son attestation ont donc été sautés par le contrôle bloquant ; aucun paquet de cette exécution n’est présenté comme prêt à publier. Preuves : `evidence/ci-final.json` et `evidence/ci-final.log`.

### Alertes de durcissement encore ouvertes

Source mesurée : `evidence/binskim/findings.json`, avec les SARIF complets et leurs originaux dans le même dossier. Toutes les alertes ci-dessous ont le niveau effectif `warning`, hérité de leur règle ; leur statut SARIF reste `fail`.

| Règle | Architectures | Constat et suite nécessaire |
|---|---|---|
| BA2024 | x64, ARM64 | `/Qspectre` absent dans des objets précompilés du runtime NativeAOT, de Globalization et du CRT. La liste réelle ne cite aucun objet applicatif. Ajouter un argument C# ne recompile pas ces bibliothèques ; étudier la chaîne de compilation ou une dérogation précise et motivée. |
| BA2025 | x64 | Marque CET absente. Le SDK NativeAOT 8.0.31 local n’ajoute pas `/CETCOMPAT` ; le SDK 9 le fait. Cela ne prouve pas une impossibilité avec .NET 8. Tester une activation compatible avant toute acceptation. |
| BA2026 | x64, ARM64 | BinSkim ne trouve pas les vérifications SDL attendues. Le rapport ne permet pas d’attribuer ce constat uniquement à des bibliothèques tierces ; investigation encore nécessaire. |
| BA2027 | x64, ARM64 | SourceLink absent du PDB natif : dette de diagnostic et de traçabilité. |
| BA6006 | x64, ARM64 | LTCG absent : recommandation d’optimisation, conservée sans suppression. |

La [documentation des règles Microsoft](https://github.com/microsoft/binskim/blob/main/docs/BinSkimRules.md) décrit ces vérifications ; le [fonctionnement NativeAOT](https://github.com/dotnet/runtime/blob/main/docs/workflow/building/coreclr/nativeaot.md) confirme l’édition de liens avec un runtime natif précompilé. Aucun changement majeur de runtime ni reconstruction de .NET n’est intégré à ce correctif. Les alertes sont un résultat nouveau de l’analyse désormais complète, pas une preuve de régression fonctionnelle introduite par les modifications. Une contre-revue a confirmé l’absence de justification pour une dérogation générale « NativeAOT ». BA2025 et BA2026 doivent rester bloquants tant que leur faisabilité n’est pas établie ; BA2024 appelle une étude du runtime et des bibliothèques. SourceLink et LTCG peuvent faire l’objet d’un arbitrage distinct, mais aucune exception n’est activée ici.

**Paquet local à recetter** : `msix/AZERTYGlobal-1.3.0.0.msixbundle` (même contenu que l’alias stable), SHA-256 `2d8c7f57b34042c2c6e7ad4b2de8e818c3cc67d2c8e0270449aee48b212ac16f`. Les versions remplacées sont archivées. Le rapport WACK du 21 septembre est explicitement marqué comme non applicable à ce candidat dans `evidence/package.json`.

La recette native du paquet final (Windows 10/11, ARM64 réel, DPI, lecteur d’écran, navigateur, verrouillage/reprise, installation/mise à jour) et le WACK restent à exécuter. L’ancien WACK ne couvre pas ces sources. Les surfaces dessinées sans fournisseur UIA et le contraste élevé restent une dette d’accessibilité ; cette correction ne certifie pas leur conformité.

Les reports déjà décidés pour 1.3.1 sur la reprise après `SendInput` refusé et les modificateurs Alt-code restent en place.

## État des constats

| Constat d’audit | Traitement dans cette session | Limite restante |
|---|---|---|
| A22-01 — paquet périmé | Reconstruit x64/ARM64, sources et contenu vérifiés | Recette et WACK sur l’empreinte finale |
| A22-02 — recette finale manquante | [Recette ciblée préparée](recette-candidat.md) | Non exécutée, donc pas de feu vert Store |
| A22-03 — consentement | Installation et réinstallation du hook gardées ; accord explicite persistant ; ancienne configuration non présumée consentante | Validation native du parcours ; politique d’entreprise respectée sans activation implicite |
| A22-04 — course A→B→A | Identité et HKL liés au HWND capturé, témoins de non-régression | Fréquence réelle et smoke de bascule Windows non mesurés |
| A22-05 — BinSkim inopérant | Distribution officielle et empreinte épinglées, rapports complets requis, catégories séparées, messages résolus, valeurs SARIF implicites respectées | Contrôle opérationnel mais bloquant sur les alertes de durcissement découvertes |
| A22-06 — réinitialisation inaccessible | Bouton Win32 tabulable, Entrée/Espace et focus natif | Mesure clavier/lecteur d’écran à faire |
| A22-07 — défilement | Focus rendu visible ; petits deltas cumulés ; tests des limites | DPI et pavé tactile réels à recetter |
| A22-08 — UIA indisponible | Requête liée à la fenêtre et à son échéance, ancêtres vérifiés ; erreurs conservatrices et reprise bornée | Navigateurs et fournisseur UIA bloqué non exercés nativement |
| A22-09 — explication UIPI | Changelog corrigé : le refus est visible, sa cause UIPI n’est pas identifiable avec ces retours | Reprise des frappes refusées toujours reportée en 1.3.1 |
| A22-10 — accessibilité des surfaces dessinées | Accents combinants rendus sur cercle pointillé, navigation de l’accueil corrigée | Fournisseurs UIA des surfaces GDI et thème de contraste élevé non refondus |
| A22-11 — documentation | Fiche Store, déclaration sur les dons, date et référence des tests mises à jour | Documents de kit entreprise hors MSIX non élargis |

## Brief des ajustements d’interface

Public : utilisateurs Windows découvrant un remappage clavier ou mettant l’app à jour. Moment : décision d’activer puis réglage au clavier. Priorité : rendre l’état et l’action explicites, conserver le focus visible et toutes les sorties. Direction : continuité avec les contrôles Win32 existants ; aucun nouveau langage visuel. Les validations natives de rendu et de lecture d’écran sont distinctes des tests de logique.

Les fichiers `patch_*.py` conservent la trace des modifications ponctuelles et de leurs gardes d’ancrage ; ils ne sont pas destinés à être rejoués sur un arbre déjà corrigé. L’ancien témoin isolé A→B→A reste une preuve du commit audité ; les tests maintenus sont maintenant dans `src/TypingEngine.Windows.Tests/ForegroundWindowBindingTests.cs`.
