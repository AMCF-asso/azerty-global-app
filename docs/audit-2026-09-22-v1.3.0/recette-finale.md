# Recette finale 1.3.0 — à remplir sur le paquet retenu

**État initial : non exécutée pendant l'audit du 22/09.** Aucun résultat ci-dessous ne se déduit des 695 tests unitaires. Conserver les détails de la recette existante `../audit-2026-09-15-v1.2.0/recette-vm-v1.3.0-correctifs.md` ; cette page est une synthèse des portes de sortie, pas leur remplacement.

| Identification obligatoire | Valeur à renseigner |
|---|---|
| SHA-256 du bundle | Candidat CI disponible : `00b4dc8cf1c3929bb50c983b27999521b9d15d280212780a597e17ea76b83154` ; remplacer si reconstruction |
| Source | Commit et éventuels correctifs postérieurs à `67601c1` |
| OS, build, architecture, utilisateur standard/admin | — |
| Paquet précédent et mode d'installation | Store 1.1 / AMCF / neuf : distinguer les identités |
| Date, opérateur, captures/logs | — |

## Installation et cycle de vie

| Test | Résultat attendu | Statut |
|---|---|---|
| Installation neuve, premier lancement | Explication conforme à la note de certification ; état d'activation cohérent avec le choix de consentement retenu ; pas de démarrage automatique imposé | Non testé |
| Mise à jour depuis 1.1 Store | Préférences/progression conservées, absence de double instance ou double hook ; canal Store conservé | Non testé |
| Quitter, relancer, changer de session | Retour immédiat au clavier système après fermeture ; seconde instance gérée ; pas de corruption de configuration | Non testé |
| Désinstallation | Processus/hook arrêtés, clavier système utilisable ; restes éventuels documentés | Non testé |
| WACK sur paquet final | Rapport complet daté, relié à l'empreinte de la copie testée et aux transformations de signature | Non testé |

## Frappe et protection

| Test | Résultat attendu | Statut |
|---|---|---|
| AZERTY et disposition système QWERTY/US | Majuscules, Ctrl/Alt/AltGr, touches mortes, pression longue et keyup ; aucun modificateur coincé | Non testé |
| Alt+Tab rapide et séquence A→B→A | Caractères dans la bonne cible, snapshot cohérent, aucune suspension durable ni fenêtre avancée ouverte sur cible protégée | Non testé |
| Passage application ordinaire ↔ désactivation choisie ↔ anti-cheat connu | Motifs cohérents ; raccourcis avalés ou autorisés selon la politique ; aucune fuite Ctrl+W | Non testé |
| Champ mot de passe natif, Chrome et Firefox | Fonctions avancées suspendues quand le champ est reconnu ; tester délai/échec UIA sans vrai secret | Non testé |
| Fenêtre élevée, verrouillage, veille/reprise | Frappe effectivement reçue ; aucune preuve fondée sur le seul compteur ; récupération du hook | Non testé |
| Mode jeu, AltGr et repli Alt+code | Rejouer l'écart 5 ; distinguer correctifs livrés et dette R7 acceptée pour 1.3.1 | Non testé |

## Interface et fonctions

| Test | Résultat attendu | Statut |
|---|---|---|
| Parcours clavier de toutes les fenêtres | Tab/Maj+Tab sans piège, boutons/onglets/liens atteignables ; reset, Entrée/Échap et retour focus fonctionnels | Non testé |
| Recherche puis insertion dans la cible | Bon symbole, bonne fenêtre ; gestion de fermeture de la cible et de suspension | Non testé |
| Leçons, défi facultatif, statistiques et opt-out | Progression conservée, compteurs cohérents, aucun texte tapé persisté ; remise à zéro/opt-out conformes | Non testé |
| FR/EN, DPI 100/175/200 %, petite zone de travail, deux moniteurs | Textes lisibles, commandes visibles, focus amené dans la zone affichée, pas de perte de taille après déplacement | Non testé |
| Narrateur/NVDA et contraste élevé | Nom/rôle/état des commandes et résultats, sélection annoncée, focus visible ; limites consignées sans score supposé | Non testé |
| Toast packagé, app vivante puis arrêtée | Clic route vers la bonne action, aucune boîte de seconde instance, pas de duplication d'interface | Non testé |
| Démarrage Windows et refus dans Paramètres Windows | Réglage respecté, aucun contournement d'un refus utilisateur | Non testé |
| Session prolongée et activité inactive | Mesurer CPU, mémoire privée et handles GDI avant/après ouvertures répétées, frappe et veille ; pas de dérive continue | Non testé |

## Matrice de distribution

| Cible | Preuve actuelle | À obtenir |
|---|---|---|
| Windows 11 x64 | Build CI + tests sur doubles | Recette native sur candidat final |
| Windows 10 minimum annoncé | Manifeste `10.0.17763.0` | Installation/lancement et fonctions principales sur environnement compatible |
| Windows ARM64 | Binaire ARM64 réel compilé | Exécution native et recette principale ; ne pas assimiler compilation à fonctionnement |
| Partner Center | Fiche locale 1.3 FR/EN | Vérifier paquet/empreinte, captures, privacy/support, dons externes, IARC, capacité runFullTrust et notes de certification |

**Critère de sortie :** chaque test indispensable possède un résultat observé ou une dérogation explicite et datée ; aucun défaut P1 non arbitré ; une seule empreinte candidate est désignée. Un défaut corrigé dans le code oblige à retester le paquet reconstruit concerné.
