# Note RGPD — établissements scolaires et structures publiques

<!-- suivi-version
version-app: 1.2.0
-->

AZERTY Global fonctionne entièrement sur le poste. Aucune donnée ne parvient à l'AMCF ni à un tiers : l'application n'effectue aucun envoi réseau automatique, ce qui se vérifie en bloquant son trafic sortant au pare-feu pendant le pilote.

## Ce qui est écrit sur le poste

Quatre fichiers au maximum, dans le profil de l'utilisateur, jamais ailleurs.

| Fichier | Contenu | Écrit quand |
|---|---|---|
| `config.json` | Préférences : langue, position des fenêtres, lancement au démarrage, date du premier lancement, compteurs de sollicitation d'avis. | À chaque changement de réglage. |
| `lessons-progress.json` | Progression d'apprentissage : leçon en cours, exercices terminés. | Pendant une séance. |
| `usage-stats.json` | 14 compteurs : jours d'utilisation, séries, minutes de frappe active, majuscules accentuées, caractères internationaux, symboles, défis terminés. Des nombres et des dates, aucun texte. | À la fermeture, si les statistiques sont actives. |
| `error.log` | Journal technique local et événements de compatibilité applicative. Les noms de logiciels y sont anonymisés par HMAC-SHA256 à sel local, au format `hash:` suivi de huit caractères hexadécimaux : le journal montre qu'un même logiciel revient, jamais lequel. | En cas d'erreur. |

Un cinquième fichier, `learning-tweaks.json`, est lu s'il a été déposé à la main dans ce dossier. L'application ne l'écrit jamais.

**Ni les frappes, ni les textes saisis, ni le contenu du presse-papiers ne sont enregistrés.** La touche reçue est transformée en mémoire et n'est pas conservée. La recherche de caractères écrit dans le presse-papiers le caractère choisi, localement.

Emplacement pour une application packagée, canal Microsoft Store comme canal MSIX signé AMCF :

```text
%LocalAppData%\Packages\<PackageFamilyName>\LocalCache\Local\AZERTY Global\
```

Le nom du conteneur dépend du canal d'installation. Il se résout par :

```powershell
(Get-AppxPackage *AZERTYGlobal*).PackageFamilyName
```

Hors package, pour l'installeur classique, le dossier est `%LocalAppData%\AZERTY Global\`.

## Éteindre et effacer

Sur le canal MSIX signé AMCF, les statistiques d'usage sont **éteintes par défaut** : aucun `usage-stats.json` n'est créé. Sur le canal Microsoft Store, elles sont actives par défaut et s'éteignent par politique.

Deux valeurs sous `HKEY_LOCAL_MACHINE\SOFTWARE\Policies\AZERTYGlobal`, en REG_DWORD :

| Valeur | À 0 |
|---|---|
| `UsageStatsEnabled` | Aucun compteur n'est écrit sur le disque. |
| `NotificationsEnabled` | Aucune notification Windows. |

Le modèle d'administration est fourni avec cette note : `AZERTYGlobal.admx`, ses libellés `fr-FR` et `en-US`, et `politiques-exemple.reg`. Les cinq réglages qu'une structure peut imposer sont décrits dans la fiche de déploiement `LISEZMOI-DSI.md`.

Pour effacer : supprimer le dossier indiqué plus haut, ou désinstaller l'application. La désinstallation d'une application packagée retire son conteneur de données.

## Responsabilité

Aucune donnée ne parvenant à l'association, l'AMCF n'est destinataire d'aucun traitement et n'exerce aucun rôle de sous-traitant.

L'établissement qui déploie l'application reste **responsable de traitement** pour les fichiers locaux décrits ci-dessus : ils vivent dans le profil de ses utilisateurs, sur son parc, sous son administration. Il lui appartient de décider s'il les inscrit à son registre des traitements — un compteur d'usage est rattachable à une personne nommée du seul fait d'être écrit dans son profil.

Aucun compte n'est requis pour utiliser l'application. Aucun cookie, aucun identifiant publicitaire, aucun profilage.

Contact : `pilote@azerty.global` — politique de confidentialité : `https://azerty.global/mentions-legales`
