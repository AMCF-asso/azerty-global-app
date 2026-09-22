# Corrections de l’audit AZERTY Global 1.3.0 — 22 septembre 2026

Antoine a autorisé ces corrections dans la session de l’audit. Aucun dépôt au Microsoft Store n’est effectué.

## Changements du candidat

- Inspection du processus et du clavier natif liée à un HWND unique ; témoin de non-régression A→B→A.
- Détection UIA liée à la fenêtre attendue par ses ancêtres. Délai, erreur ou résultat d’une autre requête : fonctions avancées suspendues. Un seul worker, reprise après échec d’initialisation et nouvelle vérification des états sécurisés après une seconde.
- Accord explicite avant l’installation du hook. Les configurations anciennes le redemandent une fois. Fermer l’accueil ne l’accorde pas ; le démarrage automatique reste un choix distinct.
- Navigation clavier de l’accueil, bouton natif de réinitialisation des raccourcis, défilement suivant le focus et accumulation des petits deltas de molette.
- Cercle pointillé pour les accents combinants isolés à l’affichage, sans modifier les caractères émis ni les JSON des dispositions.
- BinSkim autonome épinglé par version et empreinte, analyse incomplète bloquante en CI ; fiche Store et explication UIPI corrigées.

## Vérifications au moment de préparer la CI

Les suites locales Core (18), Windows (235) et Python (130) passent. La dernière suite applicative chargée avant les derniers ajustements passait 463 tests ; son nouvel assembly est ensuite refusé par Application Control, y compris hors bac à sable. Un retour nul de VSTest avec zéro test n’est pas compté comme succès. La validation du candidat final est confiée à la branche `ci/verif`, selon l’exception écrite du projet.

Les publications locales x64 et ARM64 et les deux analyses BinSkim ont réussi. Une reconstruction finale suivra les derniers ajustements d’accueil.

La recette native du paquet final (Windows 10/11, ARM64 réel, DPI, lecteur d’écran, navigateur, verrouillage/reprise, installation/mise à jour) et le WACK restent à exécuter. L’ancien WACK ne couvre pas ces sources. Les surfaces dessinées sans fournisseur UIA et le contraste élevé restent une dette d’accessibilité ; cette correction ne certifie pas leur conformité.

Les reports déjà décidés pour 1.3.1 sur la reprise après `SendInput` refusé et les modificateurs Alt-code restent en place.

## Brief des ajustements d’interface

Public : utilisateurs Windows découvrant un remappage clavier ou mettant l’app à jour. Moment : décision d’activer puis réglage au clavier. Priorité : rendre l’état et l’action explicites, conserver le focus visible et toutes les sorties. Direction : continuité avec les contrôles Win32 existants ; aucun nouveau langage visuel. Les validations natives de rendu et de lecture d’écran sont distinctes des tests de logique.
