# Audit du code — AZERTY Global 1.2.0

**Date : 15 septembre 2026. Cible : soumission au Microsoft Store avant le 17 septembre.**

Révision auditée : `8775489fc1170e31e7e08a2a2d9a9fa77f5a21ea`, branche `release/1.2.0-notation-store`.

**Audit statique terminé, avec revue contradictoire des constats de frappe et de suspension. Les tests machine restent à exécuter.**

## 1. Décision de publication

**Je déconseille de soumettre cette révision en l'état.** L'audit retient onze constats numérotés, dont cinq priorités P1 sur le chemin de frappe et la suspension. Il faut corriger ces P1 et terminer la recette Windows avant la soumission.

P1 signifie « à corriger avant soumission ». P2 signifie « impact plus circonscrit, à traiter avant le pilote ou à accepter explicitement avec sa limite documentée ». Ces niveaux indiquent l'ordre de traitement ; ils ne mesurent pas le nombre d'utilisateurs touchés.

| Priorité | ID | Problème | Action principale |
|---|---|---|---|
| P1 | 02 | Relâchement perdu si Alt/Windows intervient dans un raccourci | Relâcher toute touche synthétique possédée par le moteur |
| P1 | 03 | Maj droite reste active pendant certaines émissions natives | Gérer séparément Maj gauche et Maj droite |
| P1 | 04 | Une couche décochée reste verrouillée | Purger les états des couches retirées |
| P1 | 05 | L'Explorateur et la recherche Windows héritent du contexte précédent | Actualiser la vraie cible indépendamment du menu tray |
| P1 | 06 | Des chemins d'émission échappent à la suspension | Fermer ces chemins et gérer la panne du suivi de fenêtre |
| P2 | 01 | Mauvais paramètre des sondes de touches mortes | Utiliser le sondage non mutateur et vérifier ses effets |
| P2 | 07 | Configuration JSON non objet : échec au démarrage | Valider la structure et préserver le fichier original |
| P2 | 08 | Insertion refusée comptée comme réussie | Propager le résultat réel de l'émission |
| P2 | 09 | Progression de version inconnue écrasable | Refuser l'écriture d'un format non compris |
| P2 | 10 | Ancien paquet archivé avec le numéro du nouveau | Archiver selon les métadonnées du paquet sauvegardé |
| P2 | 11 | Masque d'icône de repli trop court à 40 pixels | Partager le calcul de taille alignée |

Les corrections des textes de présentation, les contrôles du bundle et les limites de la collecte de retours sont détaillés plus bas. La [recette VM](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/recette-vm.md>) fournit les scénarios d'acceptation.

Le mandat est un audit complet du code : aucun changement du produit, build, lancement de l'application, installation, signature ou envoi au Store n'a été réalisé. Antoine prépare la VM ; la campagne machine vient ensuite. L'application v2 dans `app-rust` est hors périmètre.

Les constats décrivent la révision candidate. Ils ne sont pas présentés comme autant de régressions depuis la v1.1.0 : aucun commit ou tag fiable correspondant exactement au paquet 1.1.0 publié n'a été établi. La comparaison de mise à jour devra partir de ce paquet réel.

L'objectif produit retenu est de recueillir des retours des utilisateurs actuels sur l'installation, la prise en main et l'usage quotidien. Une soumission le 17 septembre ne constitue pas une garantie de disponibilité publique ce jour-là.

## 2. Constats sur le moteur et la persistance

### AG120-01 — Les sondes de touches mortes utilisent un mauvais paramètre Windows

**P2 — à corriger avant le pilote. Preuve : paramètre incorrect confirmé ; effets visibles à reproduire.**

`IsDeadKeyOnLayout` appelle `ToUnicodeEx` avec `0x01`. Les deux sondes de `DoesCapsLockAffectVk` utilisent le même paramètre. Les commentaires l'interprètent comme « ne pas consommer », alors que le bit 0 indique qu'un menu est actif. Le paramètre de lecture sans modification est le bit 2, soit `0x04`, disponible avant la version minimale de Windows déclarée par l'application. [KeyMapper.cs:985](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/KeyMapper.cs:985>), [KeyMapper.cs:1230](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/KeyMapper.cs:1230>), [documentation Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-tounicodeex).

- **Déclencheur :** saisie en mode de compatibilité native, sur une disposition qui possède des touches mortes ; calcul du caractère ou de l'effet de Verr. Maj.
- **Impact :** ces sondes peuvent modifier le tampon de composition Windows consulté. Cela peut perturber une composition suivante ou fausser le résultat mis en cache. L'effet exact dans la fenêtre cible dépend aussi des files d'entrée des threads ; le passage automatique d'une corruption à toutes les applications n'est pas démontré. Aucun caractère parasite précis n'est présenté comme déjà observé dans cet audit.
- **Correction attendue :** utiliser un sondage non mutateur pour ces trois appels, en conservant les opérations qui consomment volontairement une touche morte dans `CompensateSystemDeadKey` et `FlushSystemDeadKey`.
- **Preuve à ajouter :** état identique avant/après sondage, avec `^`, tréma, accent grave, Verr. Maj. et dispositions française et US internationale. Les appels statiques à `ToUnicodeEx` échappent actuellement à l'interface simulée utilisée par les tests.

### AG120-02 — Une touche remappée peut perdre son relâchement pendant un raccourci

**P1 — avant soumission. Preuve : branche de contrôle confirmée ; état Windows à reproduire.**

Les sorties anticipées pour Alt et Windows précèdent `TryReleaseSyntheticVirtualKey`. Une touche virtuelle pressée par le moteur ne reçoit donc pas nécessairement le relâchement correspondant. [KeyMapper.cs:580](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/KeyMapper.cs:580>), [suivi des touches synthétiques:831](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/KeyMapper.cs:831>).

- **Déclencheur :** disposition Windows QWERTY ; Ctrl + position physique du A AZERTY (`SC010`) produit un `VK_A` synthétique. Enfoncer Windows avant de relâcher cette position physique. Le relâchement passe alors par la sortie Windows et le moteur n'émet pas `VK_A up`. Le même problème existe avec Alt gauche seul.
- **Impact :** une touche peut rester logiquement enfoncée pour la cible jusqu'à un nettoyage ultérieur. Le risque dépend de la séquence des touches, pas de la saisie normale de chaque raccourci séparément.
- **Correction attendue :** traiter les relâchements des touches dont le moteur possède le `keydown` avant les sorties Alt/Windows, comme le code le fait déjà pour les déclencheurs de couche.
- **Preuve à ajouter :** Ctrl+A, Ctrl+chiffre et Ctrl+Espace avec Alt/Windows intercalés, relâchement de Ctrl avant/après la lettre, répétition et changement de fenêtre. Les tests Ctrl existants couvrent une partie des ordres de relâchement, pas ces transitions.

### AG120-03 — Le mode de compatibilité neutralise Maj gauche même si Maj droite est tenue

**P1 — avant soumission. Preuve : séquence d'événements construite incorrecte.**

`hasShift` agrège les deux touches Maj, mais `BuildVkComboInputs` et `BuildAltCodeInputs` relâchent puis restaurent uniquement `VK_LSHIFT`. [KeyMapper.cs:1057](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/KeyMapper.cs:1057>), [KeyMapper.cs:1110](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/KeyMapper.cs:1110>).

- **Déclencheur concret :** sur la disposition française Windows traditionnelle, en mode `NativeCombo`, Maj droite + touche point AZERTY Global demande `;`. La combinaison native destinée à produire `;` ne doit pas garder Maj enfoncée. Or le moteur relâche Maj gauche et laisse Maj droite active.
- **Impact :** caractère incorrect possible et restauration d'un modificateur gauche que l'utilisateur ne tenait pas. Microsoft précise que `SendInput` conserve l'état des touches déjà pressées. [Documentation SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput).
- **Correction attendue :** préserver séparément chaque modificateur physique, et restaurer seulement ceux temporairement neutralisés.
- **Preuve à ajouter :** Maj gauche, droite et les deux simultanément ; avec/sans Verr. Maj. ; caractères directs et séquences Alt+code. Les tests de construction actuels emploient Maj gauche pour cette neutralisation.

### AG120-04 — Décocher une couche ne supprime pas ses verrous existants

**P1 — avant soumission. Preuve : machine d'état et interface concordantes.**

`ApplySettings` reconstruit la liste des couches autorisées, mais ne vide les verrous que lorsque l'interrupteur général passe à faux. `GetEffectiveState` peut encore rendre un verrou dont la couche a été décochée. La fenêtre des préférences pousse ces valeurs sans autre purge. [MaintainableLayerManager.cs:81](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/MaintainableLayerManager.cs:81>), [état effectif:233](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/MaintainableLayerManager.cs:233>), [réglages:162](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/MaintainableLayersWindow.cs:162>).

- **Déclencheur :** verrouiller le grec dans une application, décocher uniquement le grec dans les préférences, puis revenir à cette application.
- **Impact :** la saisie et l'indicateur peuvent rester en grec malgré la désactivation de cette couche.
- **Correction attendue :** retirer les états verrouillés, ponctuels et en attente associés à toute couche désactivée, puis actualiser l'indicateur.
- **Preuve à ajouter :** désactivation sélective avec verrou dans l'application courante et dans une autre, puis réactivation ; vérifier qu'aucun ancien verrou ne ressuscite. Le test actuel de fonctionnalité désactivée part d'un état sans verrou.

### AG120-05 — Les fenêtres du shell héritent du contexte de l'application précédente

**P1 — avant soumission. Preuve : contexte conservé explicitement.**

Pour conserver le nom de l'application dans le menu Compatibilité, `ForegroundMonitor.Recompute` ignore notamment `explorer.exe` et `SearchHost.exe`. Il conserve tout le contexte : identité du processus, disposition native, mode et caractère sécurisé du champ. Cette exception s'applique aussi à une vraie saisie dans l'Explorateur ou la recherche Windows. [ForegroundMonitor.cs:187](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/ForegroundMonitor.cs:187>).

- **Déclencheur :** verrouiller le grec dans une application normale, puis saisir un nom de fichier dans l'Explorateur ou une recherche Windows.
- **Impact :** le moteur continue à voir l'identité de l'application précédente ; son verrou peut s'appliquer au shell. Un contexte de compatibilité ou de disposition native peut également être conservé à tort.
- **Correction attendue :** conserver séparément le dernier nom utile pour le menu, tout en actualisant le véritable contexte de frappe.
- **Preuve à ajouter :** application → clic tray → application, application → Explorateur, application → recherche Windows ; conserver un menu utile et une couche correcte pour chaque vraie cible.

### AG120-06 — La suspension ne couvre pas toutes les sorties synthétiques

**P1 — avant soumission pour l'invariant de suspension. Preuve : chemins d'appel accessibles ; aucune conséquence anti-cheat observée.**

Trois cas doivent être traités distinctement :

1. Lors du passage à un processus suspendu pour compatibilité, `OnForegroundChanged` appelle `ClearPassedThroughKeys` après que le nouveau contexte de premier plan a été établi. S'il reste des touches suivies, cette méthode envoie des relâchements synthétiques au premier plan désormais actif. [TrayApplication.cs:2136](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TrayApplication.cs:2136>), [KeyMapper.cs:241](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/KeyMapper.cs:241>).
2. La recherche de caractères bloque ses événements clavier pendant une pause, mais le clic sur un résultat reste actif. Il peut restaurer la cible puis appeler `EmitText`. Cette méthode utilise encore le repli Unicode si le mode vaut `DisabledAntiCheat`. [CharacterSearch.cs:946](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/CharacterSearch.cs:946>), [insertion:1125](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/CharacterSearch.cs:1125>), [KeyMapper.cs:887](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/KeyMapper.cs:887>).
3. Si les deux abonnements WinEvent échouent au démarrage, le contexte initial peut rester figé. `IsHookInstalled` est exposé mais n'est pas utilisé par l'hôte pour imposer une suspension ou un suivi de remplacement. Le remappage peut ainsi rester actif après passage vers une autre cible normalement suspendue. Ce scénario de panne est différent d'une transition normale et doit posséder son propre test. [ForegroundMonitor.cs:101](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/ForegroundMonitor.cs:101>).

Le code ne démontre donc pas l'absence de toute injection vers une cible suspendue. Il ne permet pas davantage d'affirmer qu'un jeu sanctionnerait ces événements : aucun jeu, anti-cheat ou compte utilisateur n'a été testé.

**Correction attendue :** une décision explicite pour chaque émission, fondée sur la cible réelle après restauration du focus ; respecter la pause dans les actions d'insertion ; traiter séparément le nettoyage d'état interne et l'émission de relâchements ; suspendre de façon sûre si le suivi nécessaire du premier plan est indisponible. La suppression naïve de tout nettoyage pourrait créer d'autres touches bloquées.

**Preuve à ajouter :** zéro émission dans un simulateur de cible suspendue, y compris touches tenues au changement de fenêtre et clic dans la recherche. Les premières vérifications doivent utiliser une application factice, sans jeu protégé ni compte réel.

### AG120-07 — Un fichier de réglages JSON valide peut empêcher le démarrage

**P2 — correctif court recommandé avant soumission. Preuve : exception hors du filtre de récupération.**

`ConfigManager.EnsureLoaded` appelle `EnumerateObject()` sans vérifier que la racine est un objet. Les valeurs `[]`, `null` ou une chaîne sont du JSON valide, mais provoquent une `InvalidOperationException`, absente du filtre de récupération. La première lecture des réglages (`AppLanguage`) précède l'installation du gestionnaire d'erreur fatal. [ConfigManager.cs:912](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/ConfigManager.cs:912>), [Program.cs:23](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/Program.cs:23>).

- **Déclencheur :** fichier de configuration structurellement invalide, par corruption ou modification ; ce scénario n'est pas attribué à une mise à jour normale de la v1.1.
- **Impact :** fermeture au lancement, sans le parcours d'erreur prévu par l'application.
- **Correction attendue :** contrôler le type de racine, signaler l'échec de chargement et conserver le fichier original ; le lecteur de statistiques possède déjà cette protection.
- **Preuve à ajouter :** objet valide, fichier absent, JSON syntaxiquement invalide et racines `[]`, `null`, nombre et chaîne ; aucun écrasement du fichier refusé.

### AG120-08 — Une insertion refusée peut être annoncée comme réussie

**P2 — avant le pilote pour obtenir des retours interprétables. Preuve : résultat système ignoré.**

`EmitText` compte le texte avant émission et ignore la valeur retournée par `SendInput`. `TextInsertionService.TryInsert` retourne vrai après un appel `Action<string>`, sans résultat d'émission. [KeyMapper.cs:887](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TypingEngine.Windows/KeyMapper.cs:887>), [TextInsertionService.cs:18](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TextInsertionService.cs:18>).

- **Déclencheur :** focus restauré, mais émission système nulle ou partielle ; une cible exécutée avec des droits plus élevés constitue notamment une limite Windows connue.
- **Impact :** la recherche croit avoir inséré le caractère, ne propose pas son repli presse-papiers et les statistiques peuvent compter une émission refusée.
- **Correction attendue :** propager le résultat de l'émission, gérer explicitement le refus et les émissions partielles, sans tentative aveugle qui dupliquerait un texte déjà partiellement saisi. Ne pas présenter l'absence de réception par une application comme détectable à coup sûr : un succès `SendInput` signifie insertion dans le flux, pas acceptation finale du texte.
- **Preuve à ajouter :** API simulée retournant 0, un résultat partiel et le total ; puis application normale et application élevée en VM. Aucun contournement des droits Windows n'est demandé. [Contrat Microsoft SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput).

### AG120-09 — Une version inconnue du fichier de progression peut être écrasée

**P2 — avant des allers-retours v1/v2 si les fichiers sont partagés. Preuve : protection d'écriture non activée.**

`LessonProgressStore.Load` sort lorsqu'il rencontre une version différente de `CurrentVersion`, tout en laissant `_loadFailed` à faux. Un enregistrement ultérieur peut remplacer le fichier d'origine par une progression vide ou partielle comprise par la v1. [LessonProgressStore.cs:178](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/LessonProgressStore.cs:178>), [garde de sauvegarde:243](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/LessonProgressStore.cs:243>), [remplacement:283](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/LessonProgressStore.cs:283>).

**Correction attendue :** conserver en lecture seule une version inconnue, ou migrer depuis une copie de sauvegarde reconnue. **Test à ajouter :** progression de version future, navigation dans les leçons, exercice réussi et redémarrage ; fichier initial inchangé. Le test actuel protège du JSON syntaxiquement invalide, pas d'un schéma valide mais inconnu. Le partage effectif de ce fichier avec la v2 n'a pas été supposé ni audité.

## 3. Retours utilisateurs : adéquation à l'objectif

### La sollicitation liée aux jours d'usage n'est pas réévaluée pendant une longue session

`MaybeShowReviewPrompt` est appelé au démarrage ou à la fermeture de la fenêtre d'accueil. Il n'est pas réévalué périodiquement pendant une longue session. Un utilisateur qui laisse l'application ouverte peut franchir un seuil d'éligibilité sans jamais recevoir cette sollicitation. [TrayApplication.cs:307](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TrayApplication.cs:307>), [TrayApplication.cs:1049](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TrayApplication.cs:1049>).

Une autre sollicitation existe après la copie volontaire d'un résultat de défi ou du résumé des statistiques. Elle dépend de ce geste précis et ne couvre pas les utilisateurs qui ne partagent rien. [ReviewSharePrompt.cs](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/ReviewSharePrompt.cs>).

Les garde-fous existants — nombre limité d'essais, absence d'insistance après clic, absence de sollicitation dans les contextes inadaptés — sont utiles. Il ne faut pas remettre les compteurs à zéro pour cette sortie. Si la couverture des longues sessions est requise, ajouter une réévaluation sobre avec les mêmes gardes et une recette du cycle complet.

### Un avis Store ne remplace pas un retour exploitable pour la v2

La version packagée oriente la sollicitation vers la notation Store. Les commandes « Donner mon avis » et « Signaler un bug » existent ; le lien de signalement ajoute la version et le système. Les statistiques restent locales : leur existence ne donne pas au mainteneur un tableau de bord des frictions.

Pour ce pilote, le livrable utile est un retour qui précise **la version, la version de Windows, l'application cible, la disposition Windows active, les étapes, le résultat attendu et le résultat obtenu**. Demander d'abord : « As-tu pu l'utiliser normalement ? Où as-tu hésité ou dû désactiver l'application ? » Ajouter une catégorie installation/mise à jour, prise en main, frappe, compatibilité ou autre.

La campagne auprès des utilisateurs actuels doit être préparée séparément, après validation du paquet. Aucun message n'a été envoyé dans cet audit. Aucun volume d'audience, taux de conversion, nombre d'avis attendu ou nombre d'utilisateurs affectés n'a été reconstruit à partir de commentaires du code ou d'anciens graphiques.

## 4. Sécurité, confidentialité et robustesse

### Protections présentes dans le code

- Les applications reconnues comme protégées et les principaux clients de contrôle à distance suspendent le remappage ; l'anti-cheat et l'accès distant ont priorité sur un forçage utilisateur.
- Le refus d'inspection d'un processus identifié conduit à la suspension ; les transitions sensibles possèdent un chemin plus rapide que le délai normal de regroupement des événements.
- Le moteur dispose d'un suivi des touches synthétiques, d'un nettoyage d'état et de protections contre les séquences Unicode invalides.
- Les statistiques sont des agrégats locaux. Les chemins de journalisation examinés ne constituent pas un enregistrement ordonné des frappes ; les noms des processus sont hachés dans les événements concernés. Les arguments bruts d'une seconde instance ne sont pas journalisés.
- Les enregistrements de configuration, statistiques et progression comportent des mécanismes de remplacement et de conservation en cas d'échec ; AG120-07 et AG120-09 en montrent les limites.
- Le projet utilise NativeAOT, active les analyseurs .NET et demande Control Flow Guard. Leur présence dans le fichier projet ne prouve pas leur résultat dans un binaire qui n'a pas été construit ici.

### Limites à présenter correctement

- La détection des champs de mot de passe des applications modernes est faite au mieux, avec une attente plafonnée et un résultat précédent en cas de retard. Une garantie universelle de détection n'est pas démontrée.
- La liste de jeux et d'applications est codée en dur. Une protection universelle contre tous les anti-cheats, ou une garantie d'absence de sanction, n'en découle pas. La fiche Store source emploie pourtant « éviter tout risque de bannissement » : remplacer cette garantie par une description limitée aux applications reconnues. [Fiche Store.md:118](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/msix/Fiche Store.md:118>).
- Le chemin de compatibilité repose sur le processus et ses modules. Le README parle de « détection des applications fullscreen » ; la lecture n'a pas trouvé de détecteur général de plein écran dans ce chemin. Aligner cette phrase sur les processus reconnus et les exclusions configurées. [README.md:30](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/README.md:30>).
- Le hook et les fenêtres partagent la boucle de messages. Les lectures de contexte, l'énumération de modules, les traitements graphiques et les écritures synchrones rares méritent une mesure sous charge. Aucun temps de réponse, consommation de mémoire, taux de crash ou résultat antivirus n'est revendiqué ici.
- La déclaration `runFullTrust` est attendue pour cette application de bureau. Elle ne dispense pas de tester l'installation et les permissions effectives du paquet final.

## 5. Affichage et points réservés à la recette machine

### AG120-11 — Le masque d'icône de secours est trop court pour certaines tailles

**P2 — correctif local recommandé avant soumission. Preuve : taille incompatible avec le contrat de l'API native.**

`CreateTextIcon` alloue `size * size / 8` octets pour son masque 1 bit. À 40 pixels, cela fait 200 octets. `CreateBitmap` exige des lignes alignées sur un mot de 16 bits : 6 octets par ligne, soit 240 octets. Le chemin `RefreshBalloonIcon` peut demander cette taille lorsqu'il utilise l'icône texte en repli après un échec du logo. Le chemin principal `CreateLogoIcon` possède déjà la bonne formule. [TrayApplication.cs:2271](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TrayApplication.cs:2271>), [appel du repli:2056](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/TrayApplication.cs:2056>), [contrat CreateBitmap](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createbitmap).

**Impact :** le tampon fourni est plus court que les données décrites à l'API native ; risque de lecture hors de sa borne. Ce n'est ni un crash reproduit ni une exploitation démontrée. Le cas courant à 32 pixels possède la bonne taille et masque ce défaut.

**Correction attendue :** partager le calcul de taille alignée avec `CreateLogoIcon`, ou employer une allocation native correctement dimensionnée. **Preuve à ajouter :** dimensions 32/40/48/64, et repli de logo forcé à 125 % en VM.

### Hypothèses et comportements à observer

Ces points ne sont pas comptés comme des bugs Windows reproduits :

1. **Recherche qui se ferme après insertion.** `TextInsertionService` active la cible, tandis que `CharacterSearch` se masque sur `WA_INACTIVE`. Le commentaire prévoit pourtant des insertions successives avec la fenêtre ouverte. Clarifier le comportement souhaité et observer le cycle réel de messages. [CharacterSearch.cs:931](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/CharacterSearch.cs:931>).
2. **Durée de vie des images GDI+.** Le code libère sa référence au flux après création de l'image. La recherche Microsoft n'a pas établi si l'API native conserve sa propre référence ; le contrat de `System.Drawing.Image.FromStream` ne suffit pas à prouver un défaut dans cet appel GDI+ natif. Garder la piste pour le chargement/dessin/destruction répétés, sans la comptabiliser comme bug confirmé. [GdiImageLoader.cs:32](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/GdiImageLoader.cs:32>), [Bitmap::FromStream](https://learn.microsoft.com/en-us/windows/win32/api/gdiplusheaders/nf-gdiplusheaders-bitmap-fromstream), [libération finale du flux](https://learn.microsoft.com/en-us/windows/win32/api/combaseapi/nf-combaseapi-createstreamonhglobal).
3. **DPI, multiécran et accessibilité.** Indicateur de couche, recherche, leçons et dialogue de durée de pause : déplacements entre écrans, 125/150/200 %, navigation Tab/Entrée/Échap, Narrateur, contraste et agrandissement. Le dessin natif personnalisé rend ces vérifications nécessaires ; une lecture de chaînes localisées ne prouve pas leur accessibilité.
4. **API Store et activation de toast.** App ouverte/fermée, double clic, annulation, Store indisponible/hors ligne, identité packagée et langues FR/EN. Les tests unitaires de textes et de callbacks ne prouvent pas l'activation COM du paquet AOT livré.
5. **Cycle Windows.** Mise à jour depuis le paquet 1.1.0 effectivement publié, démarrage automatique consenti/refusé, arrêt de session, verrouillage, veille, redémarrage de l'Explorateur et désinstallation.

## 6. Couverture et niveau de preuve

L'[inventaire détaillé](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/inventaire.json>) contient les empreintes SHA-256, les chemins et les longueurs des fichiers du périmètre. Il recense **75 fichiers C# de produit, soit 28 098 lignes**, et **41 fichiers C# de tests/support, soit 6 602 lignes**. Ces nombres décrivent les sources, pas un taux de couverture ni un nombre de tests exécutés.

| Domaine | Travail effectué | Limite |
|---|---|---|
| Moteur portable et ressources | Lecture composition, disposition, parser, erreurs et chargement ; comparaison de la copie de disposition | Aucune frappe Windows exécutée |
| Moteur Windows | Lecture hook, mapper, couches, contexte, détection sécurisée, registre d'applications et API natives | Ordre réel des messages et timing à tester |
| Hôte Windows | Lecture démarrage, tray, pause, compatibilité, démarrage automatique, canaux, stratégies, notification et notation | Activation COM/WinRT et paquet AOT non exécutés |
| Interface et apprentissage | Revue intégrale ou ciblée des fenêtres, recherche, insertion, presse-papiers, leçons, statistiques et images ; scan des 16 fichiers de localisation | Revue statique, pas validation visuelle ni audit Narrateur |
| Persistance et vie privée | Configuration, statistiques, progression, journaux et chemins de liens externes | Aucun fichier personnel ni trafic réel inspecté |
| Tests existants | Lecture ciblée des suites moteur, configuration, interfaces, leçons, localisation, avis et toasts | Aucun test lancé ; aucun ancien résultat repris comme preuve actuelle |
| Livraison | Lecture des scripts de paquet, vérification, synchronisation/provenance, identité/versions, génération App Installer, de leurs tests, des deux workflows et des documents Store/entreprise pertinents | Aucun paquet construit ou soumis ; services déployés hors périmètre |

**Contrôles statiques exécutés :** lecture/parsing de neuf fichiers JSON/XML/projets/manifeste ; syntaxe acceptée. La copie `src/AZERTY Global 2026.json` est identique octet pour octet à la disposition canonique du site : SHA-256 `5f79f9e04232393cff7bfaa4fbd24a4cf8a4a1f994bb1925b9811407b565b0dd`.

L'index des caractères est également identique à celui du site. Les leçons diffèrent : le site possède en plus le module événementiel `vingt-millions` et ses trois textes. Le diff ne montre pas d'autre différence de contenu ; cet ajout propre au site n'est pas classé comme défaut de l'application Store. Une synchronisation automatique avant publication l'importerait : relire le diff avant de lancer une telle synchronisation.

Les contrôles de données vérifient aussi : total déclaré de l'index égal aux entrées, total du corpus égal aux extraits, identifiants d'extraits uniques, textes non vides et présence d'extraits français dans chacune des cinq catégories de la séquence d'apprentissage. Tous ces invariants sont satisfaits. Ils ne prouvent pas la qualité pédagogique de chaque texte ni la saisie réelle de chaque caractère.

| Contrôle du dépôt exécuté le 15 septembre | Résultat | Ce qu'il prouve |
|---|---|---|
| `validate-layout.py` | Sortie 0 : schéma, compteurs, références conformes | Cohérence structurelle de la disposition source |
| `list-identity-literals.py` | Sortie 0 : aucun littéral ciblé hors `ProductIdentity` | Centralisation des valeurs d'identité recherchées par ce script |
| `check-doc-versions.py --release` | Sortie 1 : 0 erreur et 32 attentes de bascule | Les anciens kits sont encore en attente de versions/noms/empreintes finales ; les deux documents du composant contrôlés sont conformes |

Les [sorties exactes](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/docs/audit-2026-09-15-v1.2.0/controles-statiques.json>) sont conservées. Les 32 attentes ne sont ni 32 bugs de l'application, ni un refus du Store. Elles concernent les kits legacy si ceux-ci sont distribués avec la sortie. `witness-embedded-resources.py` n'a pas été exécuté : il modifie des sources et lance les tests, ce qui dépasse cette phase.

**Ne constituent pas des validations réalisées :** compilation, tests .NET, analyses du binaire, signature, x64/ARM64 réel, certification Windows, inspection antivirus, publication, formulaire de retours en ligne et performance. Les archives non suivies, les anciennes sorties de build et l'application v2 n'ont pas été utilisés comme preuves de la v1.2.0.

## 7. Chaîne de livraison

### AG120-10 — L'ancien paquet stable est archivé sous la version à construire

**P2 — avant la prochaine fabrication du paquet. Preuve : nom et répertoire calculés depuis la nouvelle version.**

Le répertoire d'archive utilise `$storeVersion` lu dans les sources courantes. Le paquet stable précédent est déplacé vers ce répertoire avec un nom construit à partir de la même valeur. Une fabrication 1.2.0 peut donc ranger un paquet 1.1.0 dans `by-version/1.2.0.0`, sous un nom 1.2.0.0. [Pack-MSIX.ps1:76](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/scripts/Pack-MSIX.ps1:76>), [archivage:162](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/scripts/Pack-MSIX.ps1:162>).

**Impact :** le binaire archivé reste l'ancien, mais son étiquette devient trompeuse pour un retour arrière ou une preuve de version. **Correction attendue :** lire la version et l'architecture dans le paquet sauvegardé, conserver son empreinte, et séparer les métadonnées de l'ancienne et de la nouvelle livraison. **Test :** ancien paquet 1.1, nouveau 1.2, puis seconde fabrication 1.2 ; aucune archive mal étiquetée ni écrasée.

### Porte supplémentaire — Vérifier les manifestes du paquet final

`Verify-Release.ps1` contrôle notamment les hashes des exécutables extraits avec les noms de MSIX attendus. Il ne lit pas le manifeste du bundle ni les manifestes des MSIX internes. Ses contrôles ne prouvent donc pas, à eux seuls, que l'identité et l'architecture déclarées à l'intérieur du paquet correspondent aux sources. [Verify-Release.ps1:194](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/scripts/Verify-Release.ps1:194>).

Avant soumission, lire dans **le bundle exact** : identité/éditeur, version, architectures, exécutable, Windows minimal, démarrage automatique, activation COM/toast et capacité `runFullTrust`. Ce contrôle peut être ajouté au script ou effectué séparément avec une preuve archivée. Aucun paquet incohérent réellement produit n'a été démontré dans cette phase.

### Construction et dépendances

Les versions dans le programme et le projet sont `1.2.0`, les versions assembly/manifeste `1.2.0.0`. Les ressources déclarées et l'identité source ont été examinées. Il reste à vérifier leur présence dans chaque exécutable AOT publié, puis dans le bundle.

La CI publie x64 et ARM64 mais exécute ses trois projets de tests sur le runner Windows x64, sans exécution ARM64. Le contrôle de provenance est un job séparé, sans dépendance bloquant la fabrication, et BinSkim possède `continue-on-error: true`. Ce sont des choix visibles dans la CI ; le téléchargement d'un artefact ne suffit donc pas à conclure que toutes ces vérifications ont réussi. [ci.yml:17](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/.github/workflows/ci.yml:17>), [publication/tests:67](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/.github/workflows/ci.yml:67>), [BinSkim:88](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/.github/workflows/ci.yml:88>).

Les projets de produit n'ont pas de `PackageReference` tiers explicite ; ils dépendent néanmoins du SDK, de .NET, des projections Windows et des API du système. Les packages de tests sont épinglés directement. Le dépôt n'a pas de `global.json` ni de fichier de verrouillage NuGet, et la CI demande `8.0.x`. Pour pouvoir reproduire et expliquer le paquet livré, conserver la version exacte du SDK et des dépendances résolues, la révision source, les commandes et les empreintes. Aucun scan de vulnérabilités du binaire ou de ses dépendances résolues n'a été exécuté ; l'audit ne certifie pas leur absence.

Au 15 septembre, Microsoft classe .NET 8 en maintenance, avec fin de support au **10 novembre 2026** ; la page consultée indique le correctif **8.0.31 du 8 septembre 2026**. NativeAOT intègre des bibliothèques du runtime : installer un runtime plus récent sur le poste ne remplace pas celles du binaire publié. Construire avec un SDK corrigé et consigner sa version exacte ; préparer la transition de support dans la trajectoire de la v2. La version du runtime .NET et celle du SDK sont deux numérotations distinctes. [Politique de support .NET](https://dotnet.microsoft.com/en-us/platform/support/policy), [déploiement Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/).

### Distribution entreprise/AMCF : limites distinctes du Store

- Le générateur App Installer rejette l'éditeur Store, mais ne vérifie pas la signature ni l'éditeur AMCF attendu. Il suppose ces préconditions acquises. Renforcer cette porte avant de distribuer le canal AMCF. [gen-appinstaller.py:154](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/scripts/gen-appinstaller.py:154>).
- La documentation dit que la vérification de release compare également le fichier `.appinstaller` au bundle ; ce n'est pas implémenté dans `Verify-Release.ps1`. Seule la commande explicite `gen-appinstaller.py --check` fait ce contrôle. Rectifier la phrase ou raccorder réellement le contrôle. [msix/README.md:184](<D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/msix/README.md:184>).
- L'absence actuelle de bundle AMCF signé et de fichier App Installer n'est pas un bug du paquet Store. Les propriétés HTTP déployées, la signature AMCF et la mise à jour réelle de ce canal n'ont pas été revérifiées. Les anciens kits attendent leurs noms et empreintes finaux.

## 8. Séquence de travail pour les deux jours

1. **15–16 septembre :** corriger les défauts du chemin de frappe et de suspension ; ajouter les régressions qui reproduisent ces cas ; traiter la configuration et les couches. La revue suivante porte sur les modifications réellement faites, sans refonte générale de l'interface.
2. **Dès la VM prête :** exécuter la matrice de recette sur le paquet exact, en commençant par installation/mise à jour, frappe française quotidienne et activation/désactivation. Revenir aux corrections pour tout défaut de caractère, touche bloquée ou perte de réglages.
3. **16–17 septembre :** fabriquer et vérifier les deux architectures, contrôler l'identité et les ressources du bundle final, vérifier la fiche Store et les parcours de retours ; conserver les preuves attachées à l'empreinte du paquet.
4. **17 septembre :** soumettre uniquement si les portes de publication sont franchies. Le retour utilisateur viendra après disponibilité du paquet ; préparer la sollicitation des utilisateurs actuels avec un lien de signalement contextualisé.

La décision finale de soumission doit s'appuyer sur les corrections et le paquet effectivement testé, pas seulement sur l'absence d'un échec dans un ancien compte rendu.
