# Recette Windows à exécuter après l'audit du code

**État au 15 septembre 2026 : aucun scénario ci-dessous n'a été exécuté dans cet audit.**

But : décider de la soumission de la v1.2.0 au Microsoft Store avant le 17 septembre. Les onze constats du code ont été corrigés et les validations automatisées ont été exécutées ; voir le [bilan des corrections](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/corrections.md>). Commencer cette recette après préparation de la VM et du paquet candidat.

## 1. Préparer une preuve utilisable

Pour chaque exécution, noter : version de Windows et architecture, disposition Windows active, application cible/version, version et SHA-256 du paquet, commit de construction, installation neuve ou mise à jour, résultat attendu/obtenu. Utiliser uniquement du texte factice, y compris pour les mots de passe.

Créer un instantané de VM avant l'installation et un second après installation du paquet 1.1.0 réellement publié. Ne pas reconstruire une prétendue v1.1.0 depuis un commit qui contient déjà du développement 1.2.0. Les parcours de consentement au démarrage automatique doivent être exécutés explicitement.

La cible principale est Windows 11 x64. Le bundle annonce aussi ARM64 : une compilation croisée n'est pas une exécution ARM64. Il faut une preuve sur cette architecture et une vérification des versions de Windows effectivement promises par la fiche Store et le manifeste.

## 2. Tests indispensables pour la décision de soumission

| ID | Manipulation | Résultat attendu |
|---|---|---|
| VM-01 | Installer le paquet final dans une VM propre, lancer depuis le menu Démarrer | Une instance, icône accessible, fenêtre d'accueil lisible, version correcte |
| VM-02 | Mettre à jour le paquet 1.1.0 publié vers le candidat 1.2.0 | Réglages, raccourcis, statistiques et progression conservés ; un seul démarrage actif |
| VM-03 | Refuser puis autoriser le démarrage automatique ; fermer la session puis la rouvrir | L'état réel Windows correspond au choix ; aucun consentement refusé contourné |
| VM-04 | Saisir les cinq changements dans un éditeur normal et un navigateur | Résultat exact, sans caractère ajouté, perdu ni répété |
| VM-05 | Tester touches mortes avec `^`, tréma et accent grave ; alterner Verr. Maj., Maj et AltGr ; répéter en compatibilité native | Même texte attendu, état de composition inchangé par les sondes AG120-01 |
| VM-06 | Sous disposition Windows QWERTY, Ctrl + position A AZERTY ; enfoncer Alt ou Windows avant de relâcher la lettre | Chaque touche virtuelle pressée est relâchée ; aucun état bloqué après le raccourci (AG120-02) |
| VM-07 | Sous disposition Windows française traditionnelle et compatibilité native : Maj droite + point AZERTY Global ; comparer Maj gauche et les deux Maj | `;` dans les trois cas, aucun modificateur restant artificiellement enfoncé (AG120-03) |
| VM-08 | Même vérification avec un caractère qui exige Alt+code ; NumLock allumé puis éteint | Caractère exact ; NumLock et modificateurs retrouvent leur état initial |
| VM-09 | Verrouiller chaque couche, la décocher dans les préférences puis revenir à la cible ; réactiver la couche | Désactivation immédiate et aucun ancien verrou qui ressuscite (AG120-04) |
| VM-10 | Verrouiller une couche dans une application, passer à l'Explorateur et à la recherche Windows ; revenir à l'application | Couche propre à chaque cible ; le clic tray ne fausse pas le contexte de frappe (AG120-05) |
| VM-11 | Ouvrir la recherche, afficher des résultats, mettre en pause puis cliquer sur un résultat | Respect de la pause sur toutes les actions ; aucune insertion surprise (AG120-06) |
| VM-12 | Avec une application factice reconnue comme suspendue, changer de fenêtre en tenant des touches ; tenter aussi l'insertion depuis la recherche | Aucune émission vers la cible suspendue ; le nettoyage ne laisse pas de touches bloquées (AG120-06) |
| VM-13 | Fenêtre normale puis fenêtre exécutée avec des droits plus élevés ; insertion depuis la recherche | Réussite correcte ou échec compréhensible/repli explicite ; aucun faux succès (AG120-08) |
| VM-14 | Dans une copie isolée des fichiers utilisateur, tester configuration absente, invalide et JSON `[]`/`null` ; relancer | Démarrage maîtrisé, original conservé, récupération explicite (AG120-07) |
| VM-15 | Fichier de progression de version inconnue ; ouvrir une leçon, naviguer et terminer un exercice | Fichier initial conservé ; aucune conversion silencieuse destructive (AG120-09) |
| VM-16 | Activer/désactiver, pause temporaire, pause par application ; maintenir une touche pendant les transitions | Bon état de l'icône, du hook et des fenêtres ; aucune touche bloquée ; expiration correcte de la pause |
| VM-17 | Changer la disposition Windows dans la même fenêtre, puis changer de fenêtre ; tester français/QWERTY | Remappage et raccourcis fondés sur la véritable disposition de la cible |
| VM-18 | Verrouiller/déverrouiller la session, veille/reprise, redémarrer l'Explorateur | Icône et hook rétablis, état cohérent, aucune multiplication des instances |
| VM-19 | Ouvrir/fermer les principales fenêtres 30 fois, déplacer entre deux écrans à 100/125/150/200 % ; forcer le repli d'icône dans un candidat de test | Pas de crash, perte durable d'images, fenêtre inaccessible ni croissance continue de ressources graphiques ; masque d'icône correctement dimensionné (AG120-11) |
| VM-20 | Tester les champs de mot de passe factices d'un contrôle natif et d'un navigateur, avec couche verrouillée | La politique de couche sécurisée est respectée ; vérifier le délai de détection et la reprise |
| VM-21 | Exécuter le contrôle du bundle final et la certification Windows prévue par le dépôt, pour les deux architectures | Identité/version/contenu/signature corrects ; résultats joints au même SHA-256 que le paquet soumis |
| VM-22 | Désinstaller puis réinstaller dans la VM | Aucun processus actif résiduel ni lancement automatique orphelin ; comportement des données utilisateur documenté |
| VM-23 | Provoquer un refus temporaire ou une émission partielle avec un candidat de test instrumenté ; suspendre, changer de cible, relâcher puis réenfoncer une lettre, maintenir/relâcher chaque Maj et changer NumLock ; reprendre | Aucun texte rejoué ni émission dans la cible suspendue ; relâchements différés conservés, nouveau choix NumLock respecté, bon état des Maj et paires appui/relâchement cohérentes ; reprise des insertions après réparation complète |

### Texte de frappe factice

```text
É È Ç À — é è ê ë à ç ù œ Œ æ Æ
. ; , ? : / ! @ # { } [ ] | \
« Bonjour ! » 1 234,56 € — Avez-vous 20 % ?
α β γ — А Б В — ∑ ≠ ∞
```

Pour la typographie, comparer les points de code des espaces attendues : une capture d'écran ne distingue pas toujours espace ordinaire, insécable et insécable fine. Vérifier aussi Retour arrière, Échap, répétition, copier/coller et changement de cible pendant une composition.

## 3. Parcours indispensables pour recueillir des retours

| ID | Manipulation | Résultat attendu |
|---|---|---|
| RET-01 | Utilisateur existant, préférences déjà enregistrées, première ouverture de la v1.2.0 | Il comprend ce qui est disponible et retrouve ses réglages ; aucun accueil bloquant répété |
| RET-02 | Recherche : Entrée puis clic, plusieurs insertions, focus de la cible refusé | Bon caractère au bon endroit ; comportement d'ouverture/fermeture explicite ; repli de copie visible |
| RET-03 | Leçons : nouvelle progression, exercice terminé, recommencer, fermer/réouvrir, changement de langue | Progression conservée et consignes compréhensibles ; pas de résultat attribué au mauvais exercice |
| RET-04 | Avis intégré Store : ouvrir, annuler, réessayer depuis le menu, Store hors ligne/indisponible | Aucun blocage ; annulation respectée ; un repli lisible en cas d'échec |
| RET-05 | Toast avec application ouverte puis fermée, double clic et vieux toast | Bonne action une seule fois ; aucune seconde instance silencieuse inutile ni message « déjà en cours » |
| RET-06 | Préférences de notifications et sollicitation d'avis déjà consommée en v1.1 | Choix conservés, compteur non remis à zéro ; absence d'insistance |
| RET-07 | Commandes « Donner mon avis » et « Signaler un bug » depuis le paquet final | Bonne page ; version correcte ; formulaire utilisable ; aucune information personnelle envoyée à l'insu de l'utilisateur |
| RET-08 | FR/EN, clavier seul, Narrateur, contraste/agrandissement | Principales commandes identifiables et atteignables ; focus visible ; dialogues quittables |
| RET-09 | Une session longue traversant les seuils de sollicitation d'avis | Comportement choisi et vérifié, avec les mêmes gardes anti-fatigue |

## 4. Tests automatisés réalisés et compléments machine

Après l'audit initial, 609 tests automatisés ont été exécutés avec succès, dont les nouveaux témoins de correction. Les résultats et leurs limites sont détaillés dans le [bilan des corrections](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/corrections.md>). Les appels de frappe et les pannes sont simulés dans ces suites ; les essais réels ci-dessous restent à réaliser dans la campagne Windows :

- Sondes `ToUnicodeEx` : paramètre non mutateur et préservation de l'état, avec une partie réelle Windows.
- Relâchement d'une touche virtuelle possédée par le moteur avant les branches Alt/Windows.
- Construction des séquences avec modificateurs gauche/droite/deux côtés, restauration exacte.
- Suppression sélective des couches actives/ponctuelles/en attente, y compris dans une autre application.
- Contexte shell distinct de l'application conservée pour le menu.
- Refus d'émission sur cible suspendue et protection du clic de recherche en pause.
- Échec des deux abonnements WinEvent : aucune persistance silencieuse d'un ancien contexte avec remappage actif ; récupération maîtrisée du suivi.
- Résultat `SendInput` nul/partiel/total, sans double émission.
- Fichiers JSON structurellement invalides et versions de progression inconnues conservés.
- Archive de paquet portant sa version réelle, et vérification séparée des résultats x64/ARM64.
- Taille alignée du masque d'icône à 32/40/48/64 pixels et exercice du chemin de repli.
- Reprise après réparation refusée : ancienne touche remappée, nouveau down transmis pendant suspension, répétition, relâchement, Maj gauche/droite et nouveau choix NumLock.

## 5. Porte de décision

**Soumettre seulement si :** les défauts retenus comme bloquants sont corrigés et revérifiés ; les scénarios de frappe, suspension, installation/mise à jour et conservation des données passent ; les parcours de retour fonctionnent ; la preuve de vérification concerne exactement le paquet à soumettre.

Un résultat manquant reste « non testé ». Un échec qui modifie les caractères, bloque une touche, injecte vers une cible suspendue, perd des données ou empêche l'installation suspend la décision de soumission jusqu'à résolution. La présence d'un bundle, une compilation réussie ou un ancien compte rendu ne suffit pas.

Pour chaque scénario, renseigner ultérieurement : **PASS / FAIL / NON TESTÉ**, date, paquet, configuration, preuve et ticket éventuel. Cette grille est un protocole, pas un compte rendu de réussite.
