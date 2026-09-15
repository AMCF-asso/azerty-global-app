# Corrections après audit — AZERTY Global 1.2.0

**15 septembre 2026 — Les onze constats numérotés de l'audit sont corrigés dans le code. Les 609 tests automatisés exécutés passent ; les compilations natives x64 et ARM64 réussissent. La recette en VM reste à faire avant la soumission Store.**

Branche : `release/1.2.0-notation-store`. Base : `8775489fc1170e31e7e08a2a2d9a9fa77f5a21ea`. Les corrections sont présentes dans les fichiers de travail, non commitées. Le [rapport initial](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/rapport.md>) décrit cette base historique ; ses mentions « aucun test/build exécuté » concernent la phase d'audit, antérieure aux corrections autorisées ensuite par Antoine.

## 1. Corrections livrées

| Constat | Comportement corrigé | Source principale |
|---|---|---|
| AG120-01 | Les trois sondes de touches mortes et de Verr. Maj. utilisent le paramètre non mutateur 0x04. Les appels de conversion passent par l'interface Windows simulable ; les opérations volontairement mutatrices sont conservées. | [Sondes Windows](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/KeyMapper.cs:1101>) |
| AG120-02 | Le moteur relâche les touches virtuelles qu'il a pressées avant les sorties Alt/Windows. Il conserve la trace d'un relâchement refusé et associe les répétitions/relâchements à l'appui initial. | [Traitement des appuis](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/KeyMapper.cs:486>) |
| AG120-03 | Les émissions natives et Alt+code neutralisent puis restaurent Maj gauche et Maj droite séparément. | [Construction des combinaisons](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/KeyMapper.cs:1149>) |
| AG120-04 | Décocher une couche purge ses verrous, états ponctuels, appuis en attente et historique de double appui, y compris dans les autres applications. Les couches encore autorisées restent disponibles. | [Réglages des couches](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/MaintainableLayerManager.cs:81>) |
| AG120-05 | Le contexte de frappe suit la vraie fenêtre, y compris l'Explorateur et la recherche Windows. Le menu conserve séparément la dernière application externe et fige cette cible pendant son utilisation. | [Contexte de fenêtre](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/ForegroundMonitor.cs:99>) |
| AG120-06 | La pause et la suspension bloquent aussi l'insertion depuis la recherche, Verr. Maj. et le nettoyage synthétique. Une panne du suivi de fenêtre/focus suspend les émissions. La reprise conserve les relâchements nécessaires. | [Garde d'émission](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/KeyMapper.cs:1001>), [Insertion depuis la recherche](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TextInsertionService.cs:27>) |
| AG120-07 | Une configuration JSON dont la racine est un tableau, une valeur simple ou null est traitée comme illisible ; le fichier original est préservé. | [Chargement de configuration](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/ConfigManager.cs:912>) |
| AG120-08 | Le résultat distingue émission complète, nulle, partielle et bloquée. Un lot incomplet n'incrémente pas les statistiques et n'est pas rejoué. La recherche informe d'une insertion partielle ; le repli de copie reste réservé à une absence d'insertion. | [Résultat d'émission](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/KeyMapper.cs:952>), [Réparation des touches](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/InputRecovery.cs:20>) |
| AG120-09 | Une progression de version absente, invalide ou inconnue reste intacte, même si l'utilisateur termine ensuite un exercice. Les mutations ne sont pas enregistrées dans ce format non compris. | [Chargement de progression](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/LessonProgressStore.cs:183>) |
| AG120-10 | L'archivage lit la vraie version du bundle précédent, crée une copie au nom unique, vérifie son SHA-256 et écrit sa preuve. Il conserve le paquet stable jusqu'à la copie du nouveau. | [Sauvegarde du bundle](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/scripts/Archive-StableBundle.ps1:40>) |
| AG120-11 | Les deux chemins d'icône partagent un calcul de masque monochrome aligné sur 16 bits, notamment à 40 pixels. | [Taille du masque](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/GdiHelpers.cs:12>) |

Les descriptions du [README](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/README.md:30>) et de la [fiche Store FR/EN](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/msix/Fiche Store.md:118>) reflètent désormais la détection des applications reconnues. La promesse de détection générale du plein écran et celle d'absence de risque de bannissement ont été retirées. La [documentation AMCF](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/msix/README.md:184>) indique le contrôle explicite réellement disponible pour comparer le fichier App Installer au bundle.

## 2. Vérifications exécutées

| Vérification | Résultat | Preuve |
|---|---|---|
| Moteur portable | 18 tests réussis, aucun échec ni test ignoré | [Résultats Core](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/test-results/core-complet.trx>) |
| Adaptateur Windows | 152 tests réussis, aucun échec ni test ignoré | [Résultats Windows](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/test-results/windows-complet.trx>) |
| Application | 323 tests réussis, aucun échec ni test ignoré | [Résultats application](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/test-results/application-complet.trx>) |
| Scripts de livraison et de contrôle | 116 tests réussis | [Journal des scripts](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/scripts-complets.log>) |
| Compilation Native AOT x64 | Réussite ; en-tête PE x64 contrôlé | [Journal x64](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/compilation-native-x64.log>) |
| Compilation Native AOT ARM64 | Réussite ; en-tête PE ARM64 contrôlé | [Journal ARM64](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/compilation-native-arm64.log>) |
| Disposition et identité | Contrôles conformes | [Disposition](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/layout-corrections.log>), [Identité](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/identite-corrections.log>) |
| Diff et conventions de texte | Contrôles conformes ; encodages et familles de fins de ligne préservés | [Diff](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/diff-corrections.log>), [Empreintes et mesures](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/verification-corrections.json>) |
| Documents de release | 0 erreur, 32 attentes de bascule des anciens kits ; sortie 1 | [Détail des attentes](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/documents-corrections.log>) |

**Total : 493 tests .NET + 116 tests Python = 609 réussites.** Les API de frappe sont simulées dans ces tests. Les scénarios d'archivage utilisent des bundles factices et des répertoires temporaires ; le script de fabrication du paquet réel n'a pas été exécuté.

Les témoins ont observé **33 échecs avant leur correction** : 12 dans le premier lot, 8 dans le second, 12 dans le troisième et 1 sur la quarantaine des relâchements. Les résultats avant/après sont conservés dans le dossier test-results. Les autres tests ajoutés couvrent notamment les réparations différées et les séquences de reprise ; ils ne sont pas tous présentés comme des témoins exécutés avant modification.

La suite complète a également détecté une régression intermédiaire : un Retour arrière maintenu ne supprimait plus une nouvelle composition. La garde des répétitions a été déplacée après l'annulation de composition ; le test historique a été conservé et passe.

Une contre-revue indépendante a relu les corrections de frappe, contexte et suspension (AG120-01 à 06 et 08). Elle ne relève plus de blocage concret dans ce périmètre. Cette contre-revue est statique ; les exécutions de tests et compilations ci-dessus ont été réalisées par la session principale.

## 3. Points sensibles désormais couverts par le code

- **Suspension :** aucune émission synthétique vers la cible suspendue. Dans la branche de suspension pour compatibilité, le hook conserve en mémoire les transitions physiques nécessaires à la reprise, sans texte saisi, notification ni entrée/sortie dans ce suivi passif.
- **Émission partielle :** le nombre d'événements accepté par Windows ne suffit pas à identifier un préfixe garanti. La réparation relâche donc les caractères potentiellement pressés et recalcule les modificateurs ; elle ne réémet jamais les appuis de caractères.
- **Réparation refusée :** l'intention reste disponible. Les nouvelles émissions sont bloquées jusqu'à réparation complète, tandis que les relâchements des touches déjà possédées restent traités.
- **Reprise :** une touche relâchée puis réenfoncée pendant suspension garde une paire appui/relâchement cohérente. Un nouvel appui physique sur NumLock annule sa restauration différée. Retour arrière et Échap peuvent encore annuler une composition pendant une répétition.
- **Contexte de frappe :** un changement de fenêtre détecté avant sa notification interdit l'emploi du contexte précédent ; la recherche rafraîchit la cible après l'activation et avant l'insertion.

Ces propriétés restent à confirmer avec les messages Windows réels, le comportement des applications cibles et les délais de focus.

## 4. Prochaine session : VM et paquet final

Suivre la [recette VM actualisée](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/recette-vm.md>). Tous ses scénarios restent **NON TESTÉS** dans cette session.

1. **Actualiser les outils de compilation avant le paquet à livrer.** Le poste utilise le SDK 8.0.423 et a résolu les outils Native AOT/ILLink 8.0.29, consignés dans les preuves. La page Microsoft consultée le 15 septembre indique .NET 8.0.31 comme correctif courant. Native AOT embarque ses bibliothèques : il faut reconstruire le binaire avec les correctifs maintenus. [Politique de support .NET](https://dotnet.microsoft.com/en-us/platform/support/policy), [fonctionnement Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/).
2. **Rétablir la vérification des vulnérabilités des dépendances.** NuGet a signalé NU1900, car son index en ligne était inaccessible. Les tests et compilations ont utilisé les dépendances disponibles localement ; ils ne constituent pas un scan de vulnérabilités à jour.
3. **Tester l'installation neuve et la mise à jour depuis le vrai paquet 1.1.0 publié**, puis la frappe quotidienne, les raccourcis, les deux Maj, les couches, les pauses, les champs protégés et la recherche. Donner priorité aux transitions et aux retours partiels de SendInput couverts par VM-23.
4. **Vérifier le paquet exact à soumettre :** manifestes du bundle et des MSIX internes, identité, version, architectures, ressources, signature et certification Windows. Le script Verify-Release existant ne contrôle toujours pas à lui seul ces manifestes internes. Les compilations réussies ici ne remplacent pas ces vérifications.
5. **Tester sur ARM64 et vérifier les parcours de retour utilisateur.** La compilation croisée ne prouve pas l'exécution ARM64. Avis Store, signalement, activation des notifications et accessibilité restent dans la recette.

Les 32 attentes documentaires concernent les anciens kits si ceux-ci sont distribués ; elles ne correspondent pas à 32 défauts du produit Store. Le canal entreprise AMCF conserve ses contrôles propres de signature, d'éditeur et de publication. L'amélioration de la sollicitation d'avis pendant une session longue reste un choix produit distinct, sans ajout de fonctionnalité dans ces corrections.

L'application n'a pas été lancée interactivement ni installée ; aucun paquet n'a été signé, poussé ou soumis. Les anciennes archives et l'application v2 n'ont pas été modifiées. Les fichiers de preuve recensent les sources et les exécutables réellement vérifiés.

## 5. Rejouer la validation après une correction

Depuis la racine du composant microsoft-store, exécuter les trois suites explicitement, puis le vérificateur. Celui-ci contrôle les résultats TRX, lance les tests Python et les compilations natives, et renouvelle les empreintes ; il ne lance pas l'application.

```powershell
dotnet test src/TypingEngine.Core.Tests/TypingEngine.Core.Tests.csproj -c Release --logger 'trx;LogFileName=core-complet.trx' --results-directory docs/audit-2026-09-15-v1.2.0/test-results
dotnet test src/TypingEngine.Windows.Tests/TypingEngine.Windows.Tests.csproj -c Release --logger 'trx;LogFileName=windows-complet.trx' --results-directory docs/audit-2026-09-15-v1.2.0/test-results
dotnet test src/AZERTYGlobal.Tests/AZERTYGlobal.Tests.csproj -c Release --logger 'trx;LogFileName=application-complet.trx' --results-directory docs/audit-2026-09-15-v1.2.0/test-results
python docs/audit-2026-09-15-v1.2.0/verifier-corrections.py
```

Le vérificateur de l'audit initial, verifier-rapport.py, vérifie au contraire l'absence de modification par rapport à l'inventaire initial. Son résultat historique ne doit pas être réutilisé pour certifier les sources corrigées.
