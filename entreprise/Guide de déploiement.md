# Guide de déploiement : AZERTY Global 1.3.0, édition MSIX signée par l’AMCF

<!-- suivi-version
version-app: 1.3.0
-->

**En bref**

- Un paquet MSIX (`.msixbundle`, x64 et ARM64), signé au nom de l’Association pour la Modernisation du Clavier Français (AMCF), pour Windows 10 version 1809 ou ultérieure. Aucune dépendance de framework.
- Installation par Intune, Configuration Manager ou script de démarrage déployé par GPO. Mise à jour en deux voies : vous redéployez chaque version (parcs gérés), ou Windows la cherche au lancement de l’application, au plus une fois toutes les 24 heures, par un fichier `.appinstaller` (structures sans outil de parc).
- Huit stratégies de groupe, dont trois nouvelles en 1.3.0, sous `HKLM\SOFTWARE\Policies\AZERTYGlobal`, avec modèle ADMX et libellés français et anglais.
- AZERTY Global remappe la frappe par un hook clavier de bas niveau, dans la session de l’utilisateur ; il ne change pas la disposition de Windows et ne touche ni l’écran de connexion ni les fenêtres élevées (voir les limites).
- L’application ne fait aucun appel réseau. Elle ne s’active qu’après l’accord de l’utilisateur, donné dans sa fenêtre de bienvenue.
- Limites à connaître avant un pilote : section [Limites connues](#limites-connues).

Les procédures Microsoft citées ont été lues le 17/11/2026 ; leurs pages font foi si elles ont changé depuis.

## Ce que vous déployez

| Fichier | Rôle | Où le trouver |
|---|---|---|
| `AZERTY_Global_1.3.0.msixbundle` | Le paquet signé, à importer dans votre outil de parc. | `https://download.azerty.global/AZERTY_Global_1.3.0.msixbundle`, empreinte SHA-256 à la même adresse suivie de `.sha256` |
| `AZERTYGlobal.admx`, `fr-FR\AZERTYGlobal.adml`, `en-US\AZERTYGlobal.adml` | Le modèle d’administration des stratégies. | Dossier `entreprise/` du dépôt public [github.com/AMCF-asso/azerty-global-app](https://github.com/AMCF-asso/azerty-global-app/tree/main/entreprise) |
| `politiques-exemple.reg` | Les mêmes stratégies en fichier `.reg`, commentées, pour un poste isolé ou un test. | Même dossier |
| `Note RGPD - Établissements.md` | Ce que l’application écrit sur le poste, et comment l’effacer. | Même dossier |
| `AZERTY_Global.appinstaller` | La voie de mise à jour automatique (voir [Mettre à jour](#mettre-à-jour)). | `https://download.azerty.global/AZERTY_Global.appinstaller` |

Identité du paquet, utile pour la détection, AppLocker et les scripts :

| Champ | Valeur |
|---|---|
| Nom | `AZERTYGlobal.AZERTYGlobal` |
| Éditeur | `CN=Association pour la Modernisation du Clavier Français, O=Association pour la Modernisation du Clavier Français, L=Clermont-Ferrand, S=Puy-de-Dôme, C=FR` |
| Nom de famille du paquet | `AZERTYGlobal.AZERTYGlobal_yh4wjq6y8vthy` |
| Version | `1.3.0.0` |
| Windows minimal | 10.0.17763 (Windows 10 version 1809) |

La signature passe par Microsoft Artifact Signing. Vérifier un fichier téléchargé avant de l’importer :

```powershell
Get-FileHash -Algorithm SHA256 .\AZERTY_Global_1.3.0.msixbundle
Get-AuthenticodeSignature .\AZERTY_Global_1.3.0.msixbundle | Format-List Status, SignerCertificate
```

`Status` doit valoir `Valid` et le sujet du certificat commencer par `CN=Association pour la Modernisation du Clavier Français`. L’empreinte doit égaler celle publiée en `.sha256`.

Sources : [Get-FileHash](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/get-filehash) et [Get-AuthenticodeSignature](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.security/get-authenticodesignature), lues le 17/11/2026.

## Avant de déployer

1. **Retirer la version du Microsoft Store si elle est installée.** Elle porte le même nom de paquet mais un autre éditeur, donc un autre nom de famille (`AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg`) : l’édition AMCF ne la remplace pas, les deux coexistent, et une seule tourne à la fois pour un même utilisateur, la première lancée. Les réglages et la progression des leçons ne passent pas d’une version à l’autre : chacune a son propre dossier de données. Pour retirer la seule version du Store, en administrateur :

   ```powershell
   Get-AppxPackage -AllUsers -PackageTypeFilter Bundle -Name AZERTYGlobal.AZERTYGlobal | Where-Object PackageFamilyName -eq "AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg" | Remove-AppxPackage -AllUsers
   ```

2. **Prévoir l’accord d’activation.** Depuis la 1.3.0, l’application ne modifie la frappe qu’après un accord explicite de l’utilisateur, donné dans la fenêtre de bienvenue (bouton « Activer et essayer »). Aucune stratégie ne donne cet accord à sa place. Si vous supprimez la fenêtre de bienvenue au démarrage, la frappe reste celle de Windows tant que l’utilisateur ne l’a pas rouverte (menu de l’icône, Apprendre > Revoir l’accueil) pour donner son accord.
3. **Prévoir le premier lancement.** Installer le paquet ne lance pas l’application. Le lancement à l’ouverture de session est désactivé à l’installation ; l’utilisateur l’active à l’étape 3 de la fenêtre de bienvenue, après son accord. D’ici là, chaque utilisateur doit lancer AZERTY Global une première fois depuis le menu Démarrer ; la stratégie `AutoStartEnabled` à 1 ne dispense pas de ce premier lancement.
4. **Vérifier que l’installation d’applications signées hors Store est permise.** Les pages de Microsoft divergent : l’une la donne active par défaut sous Windows 10 version 2004 et ultérieures et sous Windows 11, l’autre indique qu’une stratégie « non configurée » la laisse désactivée. La stratégie en cause est « Allow all trusted apps to install » (Computer Configuration > Administrative Templates > Windows Components > App Package Deployment) ; si elle est désactivée sur vos postes, l’installation est bloquée. Aucun certificat n’est à distribuer : celui de l’AMCF est chaîné à la « Microsoft Identity Verification Root Certificate Authority 2020 », reconnue par Windows.
5. **Poser les stratégies avant la première ouverture de l’application** : elles sont lues une seule fois, au démarrage.

Sources Microsoft, lues le 17/11/2026 : [installation depuis le web et sideloading](https://learn.microsoft.com/en-us/windows/msix/app-installer/installing-windows10-apps-web), [Sideload line of business apps](https://learn.microsoft.com/en-us/windows/application-management/sideload-apps-in-windows), [modèles de confiance d’Artifact Signing](https://learn.microsoft.com/en-us/azure/artifact-signing/concept-trust-models), [Get-AppxPackage](https://learn.microsoft.com/en-us/powershell/module/appx/get-appxpackage), [Remove-AppxPackage](https://learn.microsoft.com/en-us/powershell/module/appx/remove-appxpackage).

## Stratégies de groupe

Toutes les stratégies sont de classe Ordinateur et s’écrivent sous `HKEY_LOCAL_MACHINE\SOFTWARE\Policies\AZERTYGlobal`. Dans la console, elles se trouvent sous Configuration ordinateur > Stratégies > Modèles d’administration > AZERTY Global.

Règles communes :

- **Lues au démarrage de l’application**, une seule fois. Après un changement, `gpupdate` ne suffit pas : l’application doit être quittée (menu de l’icône, « Quitter ») puis relancée, ou la session rouverte.
- **Ordre de priorité** : la stratégie, puis le réglage de l’utilisateur, puis le défaut de l’édition. Un réglage tenu par une stratégie apparaît grisé dans les Paramètres de l’application.
- **Vue 64 bits uniquement.** Une valeur écrite par un outil 32 bits sous `HKLM\SOFTWARE\WOW6432Node\Policies\AZERTYGlobal` n’est pas lue.
- **Une valeur absente** laisse le comportement par défaut ; ce n’est pas la même chose qu’une valeur à 0.

| Stratégie (libellé de la console) et valeur | Effet | Sans stratégie |
|---|---|---|
| Autoriser les notifications de l’application : `NotificationsEnabled`, REG_DWORD | La stratégie impose le réglage dans les deux sens, et la case des Paramètres est grisée. 1 : notifications imposées, l’utilisateur ne peut pas les couper. 0 : aucune notification, sollicitation d’avis comprise, et l’utilisateur ne peut pas les rallumer ; les avertissements de sécurité s’affichent dans tous les cas. | L’utilisateur décide. |
| Autoriser les statistiques d’usage locales : `UsageStatsEnabled`, REG_DWORD | 1 : l’application tient ses compteurs dans `usage-stats.json`, sur le poste. 0 : aucun fichier de statistiques n’est créé ni mis à jour. Ces compteurs ne quittent jamais le poste. | Éteintes sur la version distribuée par l’association (édition AMCF), actives sur la version du Store. |
| Autoriser les liens externes : `ExternalLinksEnabled`, REG_DWORD | 1 : « Soutenir le projet », « Noter sur le Microsoft Store » et le lien Discord sont visibles. 0 : ils sont masqués. « Donner mon avis » et « Signaler un bug » restent : pour les retirer aussi, voir `OutboundLinksEnabled`. | Masqués sur la version distribuée par l’association (édition AMCF), visibles sur la version du Store. |
| Supprimer la fenêtre de bienvenue au démarrage : `ShowOnboarding`, REG_DWORD | ⚠️ Sens inversé : activer la stratégie écrit **0**. À 0, la fenêtre ne s’ouvre plus au démarrage et la case est verrouillée ; le menu de l’icône (Apprendre > Revoir l’accueil) l’ouvre toujours à la demande. À 1, la fenêtre suit le réglage de l’utilisateur. Voir l’accord d’activation ci-dessus. | La fenêtre suit le réglage de l’utilisateur. |
| Imposer la langue de l’interface : `Language`, REG_SZ | `fr` ou `en`. Impose la langue et verrouille les trois endroits qui permettent de la changer. Toute autre valeur est ignorée et tracée dans `error.log`. | L’utilisateur choisit. |
| Masquer tous les liens vers Internet : `OutboundLinksEnabled`, REG_DWORD | ⚠️ Sens inversé : activer la stratégie écrit **0**. À 0, tout lien qui ouvre le navigateur ou le Microsoft Store est masqué : « Donner mon avis », « Signaler un bug », le site, le code source, la licence, le guide, la politique de confidentialité, les ressources, Discord, le soutien au projet, la notation sur le Store et la sollicitation d’avis. À 1, ou stratégie désactivée : aucun effet ; elle ne rallume pas les liens que la version distribuée par l’association ou `ExternalLinksEnabled` à 0 masquent. | Aucun lien n’est retiré, sauf les liens de soutien, qui suivent `ExternalLinksEnabled`. |
| Lancement à l’ouverture de session : `AutoStartEnabled`, REG_DWORD | 1 : à chaque démarrage, l’application active son lancement à l’ouverture de session. 0 : elle le coupe à chaque démarrage. Dans les deux cas, la case des Paramètres est grisée (« Géré par votre organisation »). Prend effet après un premier lancement par chaque utilisateur ; un refus exprimé dans Windows (Gestionnaire des tâches, Paramètres > Applications > Démarrage) reste respecté : ce n’est pas un forçage. | L’utilisateur choisit (étape 3 de la fenêtre de bienvenue). |
| Masquer Leçons et Défi du jour : `LessonsEnabled`, REG_DWORD | ⚠️ Sens inversé : activer la stratégie écrit **0**. À 0, Leçons et Défi du jour disparaissent : entrées du menu Apprendre, rappels, lien de l’accueil et section Défi des Statistiques. « Revoir l’accueil » reste disponible, avec l’exercice d’activation (« Activer et essayer ») : c’est la prise en main du clavier, pas une leçon ; pour supprimer aussi la fenêtre de bienvenue, utilisez `ShowOnboarding`. À 1, ou stratégie désactivée : aucun effet. | Leçons et Défi du jour sont présents. |

Les cinq premières stratégies existent depuis la version 1.2.0 ; les trois dernières arrivent avec la 1.3.0. Aucune ne s’applique sans valeur posée.

Libellés anglais (en-US), les seuls que montre Intune : Allow application notifications, Allow local usage statistics, Allow external links, Remove the welcome window at startup, Force the interface language, Hide all links to the Internet, Start at sign-in, Hide Lessons and the Daily Challenge.

Une application MSIX qui a la capacité « full trust », comme celle-ci, lit les clés de stratégie de groupe comme une application installée autrement ([Group Policy et applications empaquetées](https://learn.microsoft.com/en-us/windows/msix/group-policy-msix), lue le 17/11/2026).

## Déployer avec Intune

Non joué par l’AMCF au 17/11/2026 : les étapes suivent la documentation de Microsoft.

**Application**

1. Ajoutez une application Windows de type « Line-of-business app » et sélectionnez `AZERTY_Global_1.3.0.msixbundle`. Microsoft range `.msixbundle` parmi les extensions d’applications Windows acceptées, même si l’étape de sélection de la page ne cite que `.msi`, `.appx` et `.appxbundle`.
2. Le contexte d’installation (utilisateur ou appareil) est fixé d’après le paquet et ne se modifie pas.
3. Affectez l’application à vos groupes.
4. ⚠️ Pendant un enrôlement Windows Autopilot, Microsoft signale qu’un mélange d’applications Win32 et d’applications métier peut faire échouer l’installation.

Source : [Add a Windows line-of-business app to Microsoft Intune](https://learn.microsoft.com/en-us/intune/intune-service/apps/lob-apps-windows), lue le 17/11/2026.

**Stratégies**

1. Si votre locataire a déjà importé le modèle de la 1.2.0, faites ceci avant d’importer : il porte le même espace de noms (`AMCF.Policies.AZERTYGlobal`), et Microsoft indique que l’import d’un fichier dont les paramètres sont déjà importés échoue sur une erreur d’espace de noms. Sa voie est de supprimer d’abord les profils qui s’en servent, puis l’ancien modèle ; prévoyez le temps de recréer le profil. Non joué par l’AMCF.
2. Importez `AZERTYGlobal.admx` avec `en-US\AZERTYGlobal.adml`. Intune n’accepte que les ADML en-US : les libellés y sont donc en anglais. L’import est en préversion publique chez Microsoft. Le modèle de la 1.3.0 ne dépend d’aucun autre modèle : aucun `Windows.admx` à importer avant.
3. Créez un profil de configuration à partir du modèle importé, réglez les stratégies voulues et affectez-le aux appareils.

La clé `SOFTWARE\Policies\AZERTYGlobal` est hors des emplacements qu’Intune interdit aux ADMX importés (`System`, `Software\Microsoft`, `Software\Policies\Microsoft`).

Sources : [Import custom and third-party partner ADMX templates](https://learn.microsoft.com/en-us/intune/device-configuration/settings-catalog/import-custom-admx-templates) et [Win32 and Desktop Bridge app ADMX policy ingestion](https://learn.microsoft.com/en-us/windows/client-management/win32-and-centennial-app-policy-configuration), lues le 17/11/2026.

## Déployer avec Configuration Manager

Non joué par l’AMCF au 17/11/2026 : les étapes suivent la documentation de Microsoft.

1. Déposez `AZERTY_Global_1.3.0.msixbundle` sur un partage lisible par les clients.
2. Créez une application de type « Windows app package (\*.appx, \*.appxbundle, \*.msix, \*.msixbundle) » et sélectionnez le bundle.
3. Cochez « Provision this application for all users on the device » (onglet « User Experience » si l’application existe déjà) : l’application est alors installée pour tous les utilisateurs du poste.
4. Déployez l’application sur vos regroupements d’appareils.
5. Clé de sideloading : Configuration Manager pose `AllowAllTrustedApps` à 1 au premier déploiement si elle n’est pas configurée. Si vos postes l’ont à 0, il ne la change pas et le déploiement échoue.
6. Pour désinstaller une application provisionnée, Microsoft indique que Windows ne la retire pas des profils déjà servis : il faut un déploiement de désinstallation pour l’appareil et un pour les utilisateurs.

Les stratégies se posent par GPO (section suivante) ou en déployant les valeurs de registre du tableau.

Sources : [Create applications](https://learn.microsoft.com/en-us/intune/configmgr/apps/deploy-use/create-applications) et [Create Windows applications](https://learn.microsoft.com/en-us/intune/configmgr/apps/get-started/creating-windows-applications), lues le 17/11/2026.

## Déployer par stratégie de groupe, sans outil de parc

Non joué par l’AMCF au 17/11/2026 : les étapes suivent la documentation de Microsoft.

**Stratégies**

1. Copiez `AZERTYGlobal.admx` dans le magasin central, `\\<domaine>\SYSVOL\<domaine>\Policies\PolicyDefinitions\`, puis `fr-FR\AZERTYGlobal.adml` et `en-US\AZERTYGlobal.adml` dans les sous-dossiers de même nom. Le magasin est répliqué sur tous les contrôleurs de domaine.
2. Dans une GPO liée à l’unité d’organisation des ordinateurs, réglez les stratégies sous Configuration ordinateur > Stratégies > Modèles d’administration > AZERTY Global.

Source : [Create and manage the Central Store](https://learn.microsoft.com/en-us/troubleshoot/windows-client/group-policy/create-and-manage-central-store), lue le 17/11/2026.

**Application**

Une stratégie de groupe ne sait pas installer un paquet MSIX par elle-même ; elle sert à lancer un script PowerShell.

- **Pour tous les utilisateurs du poste** : script de démarrage de l’ordinateur, exécuté avec les droits du système. Le partage doit être lisible par les comptes d’ordinateur.

  ```powershell
  Add-AppxProvisionedPackage -Online -PackagePath "\\serveur\partage\AZERTY_Global_1.3.0.msixbundle" -SkipLicense
  ```

  Microsoft décrit ce provisionnement comme une installation pour l’utilisateur courant et pour chaque nouveau compte. Sa page ne dit pas ce qui arrive aux comptes qui existent déjà sur le poste, et ne cite que les paquets `.appx` : ces deux points ne sont pas vérifiés. Le script d’ouverture de session ci-dessous couvre les comptes existants.
- **Pour l’utilisateur qui ouvre sa session**, sans droits d’administrateur : script d’ouverture de session.

  ```powershell
  Add-AppxPackage -Path "\\serveur\partage\AZERTY_Global_1.3.0.msixbundle"
  ```

  Si vos postes interdisent aux non-administrateurs d’installer des paquets (`BlockNonAdminUserInstall`), ce script échoue. La stratégie `AllowedNonAdminPackageFamilyNameRules` permet d’autoriser ce seul paquet par son nom de famille, `AZERTYGlobal.AZERTYGlobal_yh4wjq6y8vthy`. Les pages de Microsoft divergent sur les versions concernées : l’une la donne pour Windows 11 23H2 et 24H2 à jour, l’autre la réserve à une préversion Windows Insider et l’indique non prise en charge sous Windows Pro. Sous Windows 10, utilisez le script de démarrage.

Sources : [Group Policy et applications empaquetées](https://learn.microsoft.com/en-us/windows/msix/group-policy-msix), [Add-AppxProvisionedPackage](https://learn.microsoft.com/en-us/powershell/module/dism/add-appxprovisionedpackage), [Add-AppxPackage](https://learn.microsoft.com/en-us/powershell/module/appx/add-appxpackage), [Create and manage the Central Store](https://learn.microsoft.com/en-us/troubleshoot/windows-client/group-policy/create-and-manage-central-store), [Policy CSP ApplicationManagement](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-applicationmanagement), lues le 17/11/2026 (la dernière relue le 18/11/2026).

## Mettre à jour

Deux voies. Choisissez-en une par poste : un poste installé par le bundle ne reçoit aucune mise à jour automatique.

### Voie 1 : vous redéployez chaque version (parcs gérés)

Rien ne se met à jour seul et rien n’est contacté. Chaque version est publiée sous `AZERTY_Global_<version>.msixbundle`, avec son empreinte en `.sha256`, et annoncée sur `https://azerty.global/download`.

1. Vérifiez l’empreinte et la signature du nouveau bundle (section [Ce que vous déployez](#ce-que-vous-déployez)).
2. Redéployez-le par le même outil et la même procédure que l’installation. Dans Intune, téléversez-le dans la même application : Microsoft exige une version supérieure dans le manifeste, ce que porte chaque nouvelle version d’AZERTY Global.
3. La nouvelle version garde l’identité et le nom de famille du paquet : elle remplace l’ancienne, et les réglages et la progression de l’utilisateur restent en place.

Lors de nos essais, Windows a refusé de remplacer un paquet dont l’application tournait (erreur `0x80073D02`). Planifiez la mise à jour hors session, faites quitter l’application (menu de l’icône, « Quitter »), ou, par script, utilisez l’option `-ForceApplicationShutdown` ou `-DeferRegistrationWhenPackagesAreInUse` d’`Add-AppxPackage` ([Add-AppxPackage](https://learn.microsoft.com/en-us/powershell/module/appx/add-appxpackage), lue le 17/11/2026).

### Voie 2 : Windows vérifie au lancement, au plus toutes les 24 heures (structures sans outil de parc)

L’installation passe par un fichier `.appinstaller` hébergé par l’AMCF. Windows (App Installer, et non l’application) le relit au lancement d’AZERTY Global, au plus une fois toutes les 24 heures, et installe le bundle `https://download.azerty.global/AZERTY_Global.msixbundle` s’il est plus récent. Aucune vérification n’a lieu en tâche de fond.

Installation, pour l’utilisateur courant : téléchargez `https://download.azerty.global/AZERTY_Global.appinstaller`, puis ouvrez-le par un double-clic, ou :

```powershell
Add-AppxPackage -AppInstallerFile ".\AZERTY_Global.appinstaller"
```

Le lien `ms-appinstaller:` depuis une page web est désactivé par défaut par Microsoft depuis décembre 2023 (stratégie `EnableMSAppInstallerProtocol` pour le réactiver) ; ni la commande ni le double-clic n’en ont besoin.

À savoir :

- Autorisez `https://download.azerty.global` au proxy. Bloquer cette adresse doit arrêter les mises à jour sans gêner l’application, puisque l’application ne s’y connecte pas.
- Microsoft indique qu’une vérification au lancement « peut afficher une interface » ; ce que l’utilisateur voit pendant une mise à jour n’a pas été observé.
- ⚠️ Au 17/11/2026, ni l’installation ni une montée de version par ce fichier n’ont été constatées sur un poste propre. Risque connu : le nom de l’éditeur contient des caractères accentués (« Français », « Puy-de-Dôme »), que la documentation de Microsoft exclut du fichier `.appinstaller` alors que d’autres éditeurs en publient en service. Tant que l’essai n’est pas fait, préférez la voie 1 pour un parc.

Sources : [App Installer file overview](https://learn.microsoft.com/en-us/windows/msix/app-installer/app-installer-file-overview), [Update settings](https://learn.microsoft.com/en-us/windows/msix/app-installer/update-settings), [Add-AppxPackage](https://learn.microsoft.com/en-us/powershell/module/appx/add-appxpackage), [installation depuis le web](https://learn.microsoft.com/en-us/windows/msix/app-installer/installing-windows10-apps-web), lues le 17/11/2026.

## Vérifier un poste

En administrateur (`-AllUsers` l’exige) :

```powershell
Get-AppxPackage -AllUsers -Name AZERTYGlobal.AZERTYGlobal | Format-List Name, Version, PackageFamilyName, SignatureKind
reg query "HKLM\SOFTWARE\Policies\AZERTYGlobal" /reg:64
```

Le nom de famille doit finir par `_yh4wjq6y8vthy` (édition AMCF) ; `_w9kghr08zmhbg` désigne la version du Store. Le journal technique de l’utilisateur, utile au support :

```text
%LocalAppData%\Packages\AZERTYGlobal.AZERTYGlobal_yh4wjq6y8vthy\LocalCache\Local\AZERTY Global\error.log
```

Source : [Get-AppxPackage](https://learn.microsoft.com/en-us/powershell/module/appx/get-appxpackage), lue le 17/11/2026.

## Désinstaller

Avec Intune ou Configuration Manager, utilisez la désinstallation de l’outil. Par script, en administrateur :

```powershell
Get-AppxProvisionedPackage -Online | Where-Object PackageName -like "AZERTYGlobal.AZERTYGlobal_*_yh4wjq6y8vthy" | Remove-AppxProvisionedPackage -Online
Get-AppxPackage -AllUsers -PackageTypeFilter Bundle -Name AZERTYGlobal.AZERTYGlobal | Where-Object PackageFamilyName -eq "AZERTYGlobal.AZERTYGlobal_yh4wjq6y8vthy" | Remove-AppxPackage -AllUsers
```

Le filtre sur `_yh4wjq6y8vthy` est indispensable : sans lui, la commande retire aussi la version du Store et ses données. La première ligne arrête l’installation pour les nouveaux comptes, sans retirer l’application des comptes existants ; la seconde la retire de tous les comptes du poste. La désinstallation supprime aussi le dossier de données de chaque utilisateur (voir la note RGPD).

Sources : [Remove-AppxProvisionedPackage](https://learn.microsoft.com/en-us/powershell/module/dism/remove-appxprovisionedpackage) et [Remove-AppxPackage](https://learn.microsoft.com/en-us/powershell/module/appx/remove-appxpackage), lues le 17/11/2026.

## AppLocker et App Control for Business

- **AppLocker** : une règle d’application empaquetée, par éditeur, contrôle à la fois l’installation et l’exécution. Renseignez l’éditeur (la valeur complète du tableau d’identité), le nom de paquet `AZERTYGlobal.AZERTYGlobal` et, si vous le voulez, une version minimale.
- **App Control for Business** : une règle par nom de famille (`AZERTYGlobal.AZERTYGlobal_yh4wjq6y8vthy`), ou une règle de signataire créée depuis le `.msixbundle`. Microsoft interdit de mélanger les deux sortes dans une même stratégie de base. Une règle de signataire peut retenir l’autorité de certification intermédiaire ; si Microsoft la change, une version suivante pourrait être refusée (non vérifié). La règle par nom de famille n’a pas ce risque.
- **N’épinglez pas l’empreinte du certificat.** Les certificats d’Artifact Signing sont renouvelés chaque jour et valables 72 heures ; l’horodatage garde la signature valide après leur expiration. Microsoft déconseille d’ancrer la confiance sur une empreinte ou une clé publique.

Sources : [Packaged apps rules in AppLocker](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/applocker/packaged-apps-and-packaged-app-installer-rules-in-applocker), [Manage packaged apps with App Control](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/design/manage-packaged-apps-with-appcontrol), [Artifact Signing certificate management](https://learn.microsoft.com/en-us/azure/artifact-signing/concept-certificate-management), lues le 17/11/2026.

## Réseau et pare-feu

L’application n’ouvre aucune connexion réseau. Les seules sorties partent d’un clic de l’utilisateur sur un lien, qui ouvre le navigateur. « Signaler un bug » et « Donner mon avis » transmettent alors au site de l’AMCF la version de l’application, celle de Windows et l’endroit de l’application d’où part le lien, rien d’autre. `OutboundLinksEnabled` à 0 retire tous ces liens. Avec la voie `.appinstaller`, c’est Windows (App Installer), et non l’application, qui interroge `download.azerty.global`.

## Limites connues

- **Écran de connexion, fenêtres élevées et BitLocker.** Le remappage passe par un hook clavier de bas niveau dans la session de l’utilisateur. L’écran de connexion, les fenêtres lancées en administrateur (élévation UAC) et la saisie du code BitLocker au démarrage suivent donc la disposition de Windows, pas AZERTY Global.
- **Clients de connexion à distance et machines virtuelles.** L’application se suspend quand l’un d’eux a le focus : Bureau à distance (`mstsc`, `msrdc`, `msrdcw`), AnyDesk, TeamViewer, Hyper-V, Windows Sandbox, VMware Workstation, VirtualBox, Citrix Workspace et VMware Horizon. La machine distante garde sa propre disposition. Pour VMware Horizon Client, cette suspension n’a pas été mesurée sur un poste réel.
- **Un compte, une session.** Si le même compte a déjà l’application ouverte dans une autre session Windows (console et Bureau à distance, par exemple), elle refuse de démarrer dans la seconde et dit pourquoi. Sur un serveur de sessions partagé par un compte générique, une seule session peut donc l’utiliser.
- **À l’intérieur d’une session distante** (application installée sur un serveur RDS, Citrix ou Horizon), l’usage n’est pas mesuré.
- **Installation de l’édition AMCF 1.3.0** : non jouée sur un poste propre au 17/11/2026, comme la voie `.appinstaller` (voir [Mettre à jour](#mettre-à-jour)).

## Contact

`pilote@azerty.global`, politique de confidentialité : `https://azerty.global/confidentialite`.
