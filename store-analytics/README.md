# Statistiques Microsoft Store — AZERTY Global

Pipeline en lecture seule pour collecter chaque jour les statistiques de
l'application MSIX AZERTY Global (`Store ID 9N4BTS43SSSZ`), les archiver dans
Azure Blob Storage et les interroger par MCP.

## Architecture

```text
Microsoft Store Analytics API
             |
       GitHub Actions (quotidien)
             |
   Azure Blob Storage privé
             |
      serveur MCP local
```

Le collecteur utilise uniquement la bibliothèque standard Python. Le serveur
MCP ajoute la dépendance officielle `mcp`.

Les données collectées couvrent :

- acquisitions, installations et conversions par canal ;
- utilisation quotidienne et mensuelle ;
- crashs, blocages et autres événements de santé ;
- notes, avis et insights Microsoft ;
- variantes agrégées fiables et variantes détaillées par marché, appareil,
  système et version lorsque l'API le permet.

L'API `appchannelconversions` renvoie `conversionCount` à 0 quelle que soit
la forme de la requête, y compris agrégée sans `groupby` : 1416 clics cumulés
pour 0 conversion entre le 23 mars et le 3 août 2026, alors que
`appacquisitions` remonte bien 777 acquisitions sur la même période. Vérifié le
2026-08-03 par un run dédié. La limite est côté Microsoft, pas dans le
collecteur : seul `clickCount` est exploitable pour ce jeu de données.

Microsoft limite actuellement l'utilisation quotidienne et mensuelle aux
90 derniers jours et les erreurs aux 30 derniers jours. Le passage quotidien
de la pipeline constitue donc l'historique durable qui manquerait autrement.

## 1. Préparer Partner Center et Microsoft Entra

Dans Partner Center :

1. associer le compte Partner Center au tenant Microsoft Entra ;
2. créer ou sélectionner une application Entra dédiée à la collecte ;
3. l'ajouter dans **Paramètres du compte > Gestion des utilisateurs >
   Applications Microsoft Entra** ;
4. lui attribuer le rôle **Manager** requis par l'API Analytics ;
5. noter le Tenant ID et le Client ID, puis créer une clé.

Ne jamais placer la clé dans le dépôt. Ajouter ces secrets GitHub :

| Secret | Contenu |
|---|---|
| `PARTNER_CENTER_TENANT_ID` | ID du tenant lié à Partner Center |
| `PARTNER_CENTER_CLIENT_ID` | ID de l'application Entra Partner Center |
| `PARTNER_CENTER_CLIENT_SECRET` | clé créée pour cette application |

Le Store ID est public et déjà fixé à `9N4BTS43SSSZ` dans le workflow.

## 2. Préparer Azure Blob sans secret de stockage

Créer un compte Storage et un conteneur privé. Créer ensuite une application
Entra utilisée par GitHub Actions, lui ajouter un identifiant fédéré GitHub
pour le dépôt `AZERTYGlobal/app`, puis attribuer le rôle
**Storage Blob Data Contributor** au compte de stockage ou au conteneur.

Ajouter les secrets GitHub :

| Secret | Contenu |
|---|---|
| `AZURE_CLIENT_ID` | ID client de l'identité fédérée GitHub |
| `AZURE_TENANT_ID` | ID du tenant Azure |
| `AZURE_SUBSCRIPTION_ID` | ID de l'abonnement Azure |

Ajouter les variables GitHub :

| Variable | Exemple |
|---|---|
| `AZURE_STORAGE_ACCOUNT` | `amcfanalytics` |
| `AZURE_STORAGE_CONTAINER` | `analytics-private` |

Le workflow force le conteneur en mode privé. Les blobs sont rangés sous :

```text
microsoft-store/azerty-global/
├── manifest.json
├── latest/
│   ├── manifest.json
│   └── *.json
└── runs/
    └── AAAAMMJJTHHMMSSZ/
        ├── manifest.json
        └── *.json
```

## 3. Premier lancement et backfill

Dans l'onglet **Actions** du dépôt GitHub, lancer manuellement
**Microsoft Store analytics**. La date par défaut `2015-01-01` demande tout
l'historique disponible. Les API à rétention limitée ajustent automatiquement
leur date de début.

Le workflow planifié s'exécute ensuite chaque jour à `05:15 UTC`, soit le matin
en heure de Paris. Il échoue explicitement si un seul jeu de données manque et
conserve un artefact de diagnostic pendant 14 jours.

## 4. Exécution locale du collecteur

```powershell
python -m pip install .\store-analytics
$env:PARTNER_CENTER_TENANT_ID = "..."
$env:PARTNER_CENTER_CLIENT_ID = "..."
$env:PARTNER_CENTER_CLIENT_SECRET = "..."
$env:STORE_ANALYTICS_OUTPUT = ".\store-analytics\out"
azerty-store-collect
```

Les valeurs sensibles doivent rester dans le terminal courant ou un coffre de
secrets, jamais dans un fichier versionné.

## 5. Serveur MCP

Installer l'extra MCP :

```powershell
python -m pip install -e ".\store-analytics[mcp]"
```

Pour lire un export local :

```powershell
$env:STORE_ANALYTICS_SOURCE = "D:\chemin\vers\latest"
azerty-store-mcp
```

Pour lire le conteneur privé Azure, générer un SAS **en lecture seule**, limité
au conteneur et de courte durée :

```powershell
$env:STORE_ANALYTICS_SOURCE = "https://COMPTE.blob.core.windows.net/CONTENEUR/microsoft-store/azerty-global/latest"
$env:STORE_ANALYTICS_SAS_TOKEN = "sv=...&sp=r&..."
azerty-store-mcp
```

Le serveur expose :

- `get_store_summary`
- `get_store_timeseries`
- `get_store_breakdown`
- `get_store_reviews`
- `get_store_health`
- `get_store_insights`
- `get_store_datasets`

Il ne modifie jamais Partner Center ni Azure.

## Politique de publication

Le choix actuel est « seulement certains indicateurs publics ». Par prudence,
la première version ne publie donc **rien** : le conteneur est privé et le MCP
est local.

Avant toute restitution publique, définir une liste blanche. Base recommandée :

- publiables : installations et acquisitions agrégées, tendance mensuelle,
  nombre de pays représentés ;
- privés : noms et textes complets des avis, hashes et symboles d'erreurs,
  données trop fines par marché/version lorsque le volume est faible.

Les métriques d'utilisateurs actifs sont des métriques de période. Additionner
plusieurs jours ne produit pas un nombre d'utilisateurs uniques.

## Tests

```powershell
python -m pip install .\store-analytics
python -m unittest discover -s .\store-analytics\tests -v
```

Les tests n'appellent ni Microsoft ni Azure.
