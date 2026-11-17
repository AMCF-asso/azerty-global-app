# Note RGPD — établissements scolaires et structures publiques

<!-- suivi-version
version-app: 1.3.0
-->

AZERTY Global fonctionne entièrement sur le poste. L'application n'effectue aucun envoi réseau : rien ne part sans un clic de l'utilisateur, ce qui se vérifie au pare-feu, par exemple pendant le pilote. Seule exception, à son initiative : les liens « Signaler un bug » et « Donner mon avis » ouvrent le navigateur sur le site de l'AMCF en indiquant la version de l'application, celle de Windows et l'endroit de l'application d'où part le lien, rien d'autre. Une stratégie de groupe supprime même cette exception : avec `OutboundLinksEnabled` à 0, l'application n'ouvre plus aucun site tiers (voir « Éteindre et effacer »).

## Ce qui est écrit sur le poste

Quatre fichiers et, au besoin, les copies décrites sous le tableau, dans le profil de l'utilisateur pour une application packagée (emplacement plus bas), jamais ailleurs.

| Fichier | Contenu | Écrit quand |
|---|---|---|
| `config.json` | Préférences : langue, position des fenêtres, lancement au démarrage, date du premier lancement, compteurs de sollicitation d'avis. | À chaque changement de réglage. |
| `lessons-progress.json` | Progression d'apprentissage : leçon en cours ; exercices réussis, avec leurs meilleurs résultats (vitesse, précision, durée) et le nombre d'indices utilisés ; pour chaque caractère travaillé, le nombre d'essais et d'erreurs, la boîte de révision et les dates de révision. Aucun texte tapé. | Pendant une séance. |
| `usage-stats.json` | 14 compteurs : jours d'utilisation, séries, minutes de frappe active, majuscules accentuées, caractères internationaux, symboles, défis terminés. Des nombres et des dates, aucun texte. | À la fermeture, si les statistiques sont actives. |
| `error.log` | Journal technique local et événements de compatibilité applicative. Les noms de logiciels y sont anonymisés par HMAC-SHA256 à sel local, au format `hash:` suivi de huit caractères hexadécimaux : le journal montre qu'un même logiciel revient, jamais lequel. | En cas d'erreur, et quand une prise en main à distance commence ou se termine (seul l'état, actif ou inactif, est écrit, jamais le nom de l'outil). |

Deux sortes de copies peuvent s'y ajouter. `error.log.old` : l'ancien journal, quand `error.log` dépasse 5 Mo au démarrage ; une seule copie, remplacée à la rotation suivante. `config.json.illisible-<date>` et `lessons-progress.json.illisible-<date>` : un fichier illisible au lancement est mis de côté sous ce nom, et l'application repart de zéro ; elle ne supprime jamais ces copies.

**Ni les frappes, ni les textes saisis, ni le contenu du presse-papiers ne sont enregistrés.** La touche reçue est transformée en mémoire et n'est pas conservée. La recherche de caractères écrit dans le presse-papiers le caractère choisi, localement.

Emplacement pour une application packagée, canal Microsoft Store comme canal MSIX signé AMCF :

```text
%LocalAppData%\Packages\<PackageFamilyName>\LocalCache\Local\AZERTY Global\
```

Le nom du conteneur dépend du canal d'installation. Il se résout par :

```powershell
(Get-AppxPackage *AZERTYGlobal*).PackageFamilyName
```

Hors package (exécutable lancé seul), les mêmes fichiers sont écrits à côté de l'exécutable, sans passer par `%LocalAppData%`. Depuis la 1.3.0, ce canal est sobre comme le MSIX signé AMCF : voir « Éteindre et effacer ».

## Éteindre et effacer

Sur le canal MSIX signé AMCF et, depuis la 1.3.0, sur l'exécutable hors package, les statistiques d'usage sont **éteintes par défaut** : aucun `usage-stats.json` n'est créé. Ces deux canaux ne sollicitent pas d'avis, ne proposent pas de notation sur le Microsoft Store et n'affichent aucun lien de soutien (HelloAsso, Discord, « Soutenir le projet »). Sur le canal Microsoft Store, les statistiques sont actives par défaut et s'éteignent par politique.

Deux valeurs sous `HKEY_LOCAL_MACHINE\SOFTWARE\Policies\AZERTYGlobal`, en REG_DWORD :

| Valeur | À 0 |
|---|---|
| `UsageStatsEnabled` | Aucun compteur n'est écrit sur le disque. |
| `NotificationsEnabled` | Aucune notification Windows. |

Trois autres valeurs, ajoutées en 1.3.0, sont aussi en REG_DWORD sous la même clé :

| Valeur | Effet | Conformité |
|---|---|---|
| `OutboundLinksEnabled` à 0 | Masque tout lien qui ouvre le navigateur ou le Microsoft Store : avis, bug, site, code source, licence, guide, confidentialité, ressources, Discord, soutien, notation Store, sollicitation d'avis. À 1 ou absente : aucun effet. | L'application n'ouvre plus aucun site tiers, ni celui de l'AMCF : plus aucune version de l'application ni de Windows ne peut partir sur un clic. |
| `AutoStartEnabled` à 1, ou à 0 | Active ou coupe le lancement à l'ouverture de session. La case des Paramètres est grisée, « Géré par votre organisation ». | Aucune donnée. L'effet ne vient qu'après un premier lancement par chaque utilisateur, et un refus exprimé dans Windows reste respecté. |
| `LessonsEnabled` à 0 | Masque Leçons et Défi du jour (menu Apprendre, rappels, lien de l'accueil, section Défi des Statistiques). « Revoir l'accueil » reste. | Aucune donnée. Un `lessons-progress.json` déjà écrit reste en place : la stratégie ne supprime aucun fichier. |

Ces trois stratégies ne créent aucune donnée nouvelle ni aucun envoi. Elles se lisent une fois, au démarrage : redémarrer l'application pour les appliquer. Elles valent sur tous les canaux, Microsoft Store compris.

Le modèle d'administration est fourni avec cette note : `AZERTYGlobal.admx`, ses libellés `fr-FR` et `en-US`, et `politiques-exemple.reg`. Les commentaires de `politiques-exemple.reg` décrivent les huit réglages qu'une structure peut imposer : les cinq déjà cités (`UsageStatsEnabled`, `NotificationsEnabled`, `ExternalLinksEnabled`, `ShowOnboarding`, `Language`), plus les trois ci-dessus.

Pour effacer : supprimer le dossier indiqué plus haut, ou désinstaller l'application. La désinstallation d'une application packagée retire son conteneur de données.

## Responsabilité

Hors la version de l'application et celle de Windows que reçoit le site sur un clic de l'utilisateur (voir plus haut), aucune donnée ne parvient à l'association : l'AMCF n'est destinataire d'aucun traitement et n'exerce aucun rôle de sous-traitant.

L'établissement qui déploie l'application reste **responsable de traitement** pour les fichiers locaux décrits ci-dessus : ils vivent dans le profil de ses utilisateurs, sur son parc, sous son administration. Il lui appartient de décider s'il les inscrit à son registre des traitements — un compteur d'usage est rattachable à une personne nommée du seul fait d'être écrit dans son profil.

Aucun compte n'est requis pour utiliser l'application. Aucun cookie, aucun identifiant publicitaire, aucun profilage.

Contact : `pilote@azerty.global` — politique de confidentialité : `https://azerty.global/confidentialite`
