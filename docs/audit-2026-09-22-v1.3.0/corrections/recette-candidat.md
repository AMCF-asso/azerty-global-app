# Recette du candidat corrigé — 22 septembre 2026

**État : à exécuter sur Windows, aucune ligne ci-dessous n’est réputée passée.** Utiliser une VM de recette pour ne pas cumuler les hooks avec l’application déjà installée sur le poste.

Paquet local : `msix/AZERTYGlobal-1.3.0.0.msixbundle`, SHA-256 `2d8c7f57b34042c2c6e7ad4b2de8e818c3cc67d2c8e0270449aee48b212ac16f`. Identité Store et architectures x64/ARM64 contrôlées. L’ancien paquet a été archivé par le script de construction.

## Activation — priorité de cette recette

1. Sur une installation propre, lancer l’app. Vérifier le bouton « Activer et essayer », l’état inactif et le clavier système dans le Bloc-notes. Attendre au moins 10 secondes pour couvrir les timers de réinstallation.
2. Fermer l’accueil avec la croix, puis relancer. Refaire avec Échap. Le clavier reste inchangé et aucun accord n’est mémorisé.
3. Sur une ancienne configuration sans `activationConsent`, vérifier la demande unique sans perte des réglages. L’option de démarrage Windows doit refléter le choix existant. Si une politique d’entreprise masque l’accueil, l’app reste inactive et le menu « Activer » doit ouvrir la demande manuelle.
4. Avec Tab/Maj+Tab et Entrée/Espace, activer « Activer et essayer ». La leçon et le remappage doivent fonctionner. Quitter puis relancer : l’accord est conservé.
5. Si les exercices étaient déjà terminés, parcourir jusqu’à l’étape finale. Une flèche seule doit placer le focus sur « Activer AZERTY Global », sans fermer ni activer. Entrée sur ce bouton doit activer.
6. Fermer sans accord puis utiliser « Activer » dans l’icône : l’accueil revient. Tester aussi veille/reprise, verrouillage et redémarrage d’Explorer sans accord : aucune activation implicite.

## Régressions ciblées

| Parcours | Résultat attendu |
|---|---|
| Paramètres, bouton réinitialiser | Accessible par Tab et Maj+Tab, focus Windows visible, Entrée et Espace ouvrent la confirmation, annulation sans changement |
| Paramètres en 1366×768 à 150–200 % | Le focus fait défiler les contrôles hors écran ; le retour vers les premiers contrôles remonte le contenu |
| Molette haute résolution / pavé tactile | Les petits déplacements successifs produisent un défilement ; inversion de direction cohérente |
| Chrome, Edge et Firefox : champ normal puis mot de passe, deux fenêtres | Fonctions avancées suspendues dans le champ protégé, puis disponibles dans le champ normal ; aucune insertion dans l’autre fenêtre |
| Application occupée / UIA indisponible | Aucun blocage durable de la frappe ordinaire ; protection conservatrice des fonctions avancées et reprise après résolution |
| Alt+Tab rapide, barre des tâches, applications suspendues | Pas d’émission dans une cible suspendue, pas de rafale de notifications liée aux surfaces du shell |
| Accents combinants isolés | Cercle pointillé visible dans le clavier et les indications, caractère réellement tapé inchangé |

## Validation finale avant soumission

- Traiter les alertes BinSkim décrites dans `README.md` : le contrôle complet actuel est bloquant (cinq warnings x64, quatre ARM64), malgré les tests fonctionnels réussis.
- Reprendre les autres parcours du document `../recette-finale.md` : FR/EN, DPI multiples, lecteur d’écran, contraste élevé, presse-papiers, touches mortes, couches, statistiques et liens volontaires.
- Exécuter installation, mise à jour, désinstallation et WACK sur **ce paquet précis**, puis noter son empreinte avec les résultats. L’ancien PASS WACK ne couvre pas les corrections.
- Exécuter le smoke ARM64 sur une machine ou VM ARM64 ; une compilation croisée ne prouve pas le fonctionnement natif.
- Les surfaces GDI sans fournisseur UIA et le thème de contraste élevé restent à évaluer séparément. Aucun résultat de conformité globale n’est déduit des contrôles natifs corrigés.

Ne pas publier tant que les parcours bloquants n’ont pas de preuve. Les reports 1.3.1 déjà arbitrés ne sont pas rouverts par cette liste.
