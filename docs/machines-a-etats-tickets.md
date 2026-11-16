# Tickets issus de l'analyse des machines à états (application 1.3.0)

Source : [machines-a-etats.md](machines-a-etats.md), base `250c4ef`, 15 novembre 2026.
Les constats (`C<lot>.<n>`) et leurs statuts y sont décrits ; ce fichier les regroupe par correctif.

## Règles

- Un ticket regroupe les constats d'une même zone du produit, corrigés et vérifiés ensemble.
  ME-07 fait exception : il réunit de petits correctifs indépendants ; la décision peut s'y prendre
  constat par constat.
- Les tickets sont classés par impact pour l'utilisateur (gravité de l'effet et probabilité de le
  rencontrer), du plus fort au plus faible. Aucun n'est critique ni fort.
- « Preuve » dit ce qui est établi aujourd'hui : un test en échec sur `250c4ef`, ou la seule
  lecture du code.
- Un correctif arrive avec son test de régression et la mise à jour du document, dans le même commit.
- Chaque ticket porte sa décision : `à décider`, `1.3.0`, `1.3.1` ou `laissé` (documenté, non corrigé).
- Un ticket corrigé le dit sur sa ligne « Décision » ; son commit porte l'identifiant du ticket
  dans son titre (`git log --grep ME-03`).

## Vue d'ensemble

| Ticket | Sujet | Impact | Constats | Preuve |
|---|---|---|---|---|
| ME-03 | Accueil : double clic sur « Suivant », case « Ne plus afficher » | moyen | C7.1, C7.2 | 3 tests en échec |
| ME-04 | Lancement automatique dit actif alors que Windows le refuse | moyen | C4.2, C4.4 | 1 test en échec (accueil) ; le reste à la lecture |
| ME-05 | Couches : Maj d'activation, répétition du déclencheur | faible | C1.4, C1.6 | C1.4 et C1.6 corrigés (2 tests) |
| ME-06 | Leçons : caractère attendu faux sous une couche | faible | C1.2, C1.3 | 4 tests en échec |
| ME-01 | Dates relues sans culture invariante | faible | C5.1 | 3 tests en échec |
| ME-02 | Progression des Leçons : une entrée mal formée bloque la fenêtre | faible | C5.6 | 2 tests en échec |
| ME-07 | Petits états périmés dans quatre fenêtres | faible | C5.5, C6.2, C8.2, C1.7 | 4 tests en échec ; C1.7 à la lecture |
| ME-08 | Demande d'avis : mauvais moment, minuterie perdue, double appel | faible | C7.3, C7.4, C7.5 | C7.3 et C7.4 corrigés (4 tests) ; C7.5 laissé plausible |
| ME-09 | Réglages : seconde instance, écriture de fermeture, instance élevée | faible | C4.5, C4.3, C4.1 | C4.5, C4.3 et C4.1 corrigés (10 tests, 19 cas) |
| ME-10 | Horloge ou fuseau qui recule | faible | C5.7 | 3 tests en échec |
| ME-11 | Presse-papiers : échec muet, et un test qui dépend du presse-papiers réel | faible | C8.1 | lecture du code ; 1 test existant en échec |
| ME-12 | Comportements à trancher (conception) | faible | C1.1, C1.5, C2.2, C6.1, C2.1, C2.4, C5.2 | 4 atteints (tests en échec) ; 3 à la lecture |
| ME-13 | Zone de notification : clics, menu, bulles, icône | faible | C3.1 à C3.7 | lecture du code seulement |

Total : 36 constats répartis (15 confirmés, 4 atteints à trancher, 17 plausibles) ; 3 constats
réfutés (C2.3, C5.3, C5.4) n'ont pas de ticket.

## ME-03 — Accueil : double clic sur « Suivant », case « Ne plus afficher » (moyen)

- **Décision** : 1.3.0 (QCM du 16 novembre 2026) ; corrigé.
- **Fait** : C7.1 : `OnboardingWindow` ignore « Suivant » pendant le délai de double clic de Windows (`GetDoubleClickTime`) qui suit un changement d'étape (`IsSecondClickOfDoubleClick`, horloge et délai remplaçables : `Clock`, `DoubleClickDelay`) ; un clic posé après le délai agit comme avant. C7.2 : la case « Ne plus afficher » reprend à l'ouverture le réglage enregistré de l'utilisateur (`ShowOnboardingUserSetting`, pas la politique) et `Close` reçoit cet état affiché (`_dontShowShownChecked`) au lieu de `false` : décochée tant qu'aucun refus n'est enregistré (v0.9.7.1 conservée), Échap et la croix n'écrivent rien sans case changée (audit 24/09, B2 conservé). Tests : `MachinesEtatsLot7Tests`, quatre tests rouges avant le correctif, dont le test du double clic réécrit avec deux vrais clics (`BM_CLICK`), horloge injectée, et la vérification qu'un clic après le délai valide toujours ; un quatrième garde Échap et la case décochée à la main. Écart avec le ticket : aucun sur le fond ; le test de la copie en attente envoyait deux `WM_COMMAND`, il est remplacé. À vérifier à la main : un vrai double clic à la souris sur « Suivant » à l'étape 2 (l'accueil reste à l'étape 3), puis un clic voulu juste après le délai ; rouvrir l'accueil après un refus enregistré (case cochée), valider sans y toucher, le refus reste.
- **Constats** : C7.1 (corrigé), C7.2 (corrigé).
- **Effet C7.1** : un double clic sur « Suivant » à l'étape 2 produit deux clics ; le second tombe
  sur le bouton final de l'étape 3 et valide l'accueil sans que l'étape ait été vue. Les cases de
  l'étape 3 sont alors appliquées, dont « Lancer au démarrage », cochée par défaut.
- **Effet C7.2** : la case « Ne plus afficher » s'ouvre toujours décochée ; valider l'accueil
  réécrit à « afficher » un refus déjà enregistré.
- **Où** : `OnboardingWindow.cs:882-889, 909, 926-928, 1027-1046, 1130-1134`.
- **Correctif C7.1** : ignorer « Suivant » pendant le délai de double clic de Windows après un
  changement d'étape. Changer l'identifiant du bouton final ne suffirait pas : le second clic
  arrive après le passage à l'étape 3 et porterait le nouvel identifiant.
- **Correctif C7.2** : à l'ouverture, la case reprend le réglage enregistré, et la fermeture reçoit
  l'état affiché (aujourd'hui `false` en dur, sinon décocher puis fermer par Échap ne
  s'enregistrerait pas). Cela nuance une décision documentée (v0.9.7.1 : case décochée par
  défaut) : elle reste décochée tant qu'aucun refus n'est enregistré.
- **Tests** : `MachinesEtatsLot7Tests`, quatre tests « Accueil_… ». Le test du double clic envoie
  deux commandes directes ; il est réécrit avec deux vrais clics sur le bouton (BM_CLICK) et une
  horloge injectée. Le double clic à la souris reste à vérifier à la main.
- **Risque du correctif** : moyen (comportement de fermeture de l'accueil).

## ME-04 — Lancement automatique dit actif alors que Windows le refuse (moyen)

- **Décision** : 1.3.0 (QCM du 16 novembre 2026) ; corrigé.
- **Constats** : C4.2 (corrigé), C4.4 (corrigé).
- **Fait** : C4.2 : `AutoStart.IsRegistered` vaut désormais « l'état Windows est `Enabled` » (version EXE : un raccourci refusé dans le Gestionnaire des tâches ou par stratégie n'est plus « enregistré » ; version packagée : prédicat inchangé, `Enabled` ou `EnabledByPolicy`, état illisible = faux). `Set(true)` rend donc `false` quand Windows refuse et n'écrit pas `autoStartEnabled` ; la case des Paramètres et de l'accueil reflète l'état réel ; `DefaultOnboardingCheck` teste le refus avant le raccourci présent. Le message d'échec (`GetFailureMessage`) devient, pour un refus utilisateur ou stratégie, « Windows refuse le lancement automatique… Réactivez-le dans Paramètres Windows → Applications → Démarrage » (`AutoStart_FailureMessageRefused`, français et anglais), pour les deux versions. C4.4 : `AutoStartSignals` porte `WindowsState` (lu une fois par `Snapshot`), `ShouldPrompt` rend faux pour `DisabledByUser`, `DisabledByPolicy` et `Unknown` ; `MarkPromptShown` n'étant appelé qu'après `ShouldPrompt`, la relance n'est ni proposée ni consommée. Points d'appui des tests : `AutoStart.WindowsStateReader`, `ShortcutExists`, `ShortcutEnabler`. Appelants d'`IsRegistered` relus : `Set` (même contrôle d'état final), `DefaultOnboardingCheck`, `AutoStartNudge.Snapshot`, cases et `wasRegistered` de `SettingsWindow` et `OnboardingWindow` ; `IsStartupTaskEnabled`, devenu inutile, est supprimé. Tests : `MachinesEtatsLot4Tests`, douze lignes rouges avant le correctif (Theory comprises), deux témoins verts d'office (état `Enabled`, état `Disabled`). À vérifier à la main : désactiver l'entrée dans le Gestionnaire des tâches puis cocher la case des Paramètres (message, case décochée) ; version MSIX : mêmes écrans, aucun changement attendu hors message de refus ; que Windows conserve le refus après suppression puis recréation du raccourci (non rejoué).
- **Effet C4.2** : en version EXE, « enregistré » veut dire « le raccourci existe ». Si Windows a
  désactivé l'entrée (Gestionnaire des tâches, stratégie), la case reste cochée et l'activation se
  dit réussie, alors que l'application ne se lancera pas au démarrage.
- **Ce qui est prouvé** : seule la décision de l'accueil est testée (raccourci présent, refus de
  Windows, aucune trace de choix : la case est cochée). Ce cas précis suppose des réglages perdus.
  Le cas courant, activer depuis les Paramètres alors que Windows refuse, est établi à la lecture
  seulement.
- **Effet C4.4** : la relance unique « Lancer au démarrage ? » est proposée même quand Windows ou la
  stratégie refuse ; l'unique proposition est consommée pour rien.
- **Où** : `AutoStart.cs:132-136, 147-148, 162-168, 379-380`.
- **Correctif** : « effectif » = état Windows `Enabled` ; l'activation signale le refus de Windows
  et renvoie vers les Paramètres Windows ; la relance n'est ni proposée ni consommée quand l'état
  est refusé ou inconnu. Demande de rendre la lecture de l'état Windows remplaçable dans les tests.
- **Tests** : `MachinesEtatsLot4Tests` : accueil (2 cas), activation refusée (2 cas), case des
  Paramètres et de l'accueil, message de refus (FR, EN), relance non proposée (4 états), signaux,
  relance non consommée.
- **Risque du correctif** : moyen (change le sens d'`IsRegistered` pour ses appelants ; texte
  d'interface à ajouter en français et en anglais).

## ME-05 — Couches : Maj d'activation, répétition du déclencheur (faible)

- **Décision** : 1.3.0 (QCM du 16 novembre 2026) ; corrigé.
- **Fait** : C1.4, `CanPassThrough` reçoit les modificateurs effectifs (`KeyMapper.cs`) et refuse le passage natif dès que Maj ou AltGr effectif diffère de celui qui est physiquement tenu (le système lirait le physique) ; les comparaisons Maj de la fin de la fonction utilisent aussi le Maj effectif. C1.6, `ProcessKey` rend la décision mémorisée, sans rejouer `ProcessKeyCore`, pour la répétition du déclencheur de couche tenu (`PendingTriggerScanCode`) ; les lettres tenues continuent de répéter. Écart au plan : le plan disait « toute touche déjà prise en charge », ce qui aurait cassé la répétition des lettres (chaque répétition émet) ; le court-circuit est donc limité au déclencheur.
- **Constats** : C1.4 (corrigé), C1.6 (corrigé).
- **Effet** : après une saisie de couche avec Maj tenu, la lettre suivante sortait en majuscule native (C1.4) ; la répétition automatique du déclencheur après relâchement du modificateur émettait un caractère parasite (C1.6).
- **Où** : `TypingEngine.Windows/KeyMapper.cs` (`ProcessKey`, `CanPassThrough`).
- **Tests** : `MachinesEtatsLot1Tests` (`MajDActivationNeutralise_LaSecondeLettreNeFuitPasEnMajusculeNative`, `RepetitionDuDeclencheur_ApresRelachementDeMaj_NEmetRienEtNeSuitRien`), rouges avant le correctif, verts après.
- **À vérifier à la main** : au clavier, couche grecque active (Maj + la touche `*`), taper deux fois la même lettre en gardant Maj tenu (a puis a) : alpha puis « a » minuscule, jamais « A » ; tenir Maj+`*`, relâcher Maj, garder `*` enfoncé jusqu'à la répétition automatique : pas d'étoile ; frappe normale, Maj+lettre, AltGr+lettre, jeux (touche tenue) inchangés.
- **Risque du correctif** : moyen (cœur du moteur de frappe).

## ME-06 — Leçons : caractère attendu faux sous une couche (faible)

- **Décision** : 1.3.0 (QCM du 16 novembre 2026) ; à corriger.
- **Constats** : C1.2, C1.3 (confirmés).
- **Effet** : la fenêtre des Leçons recalcule le caractère attendu au lieu de reprendre celui que le
  moteur émet. Sous une couche (accord tenu, Maj d'activation encore enfoncé, second appui du
  déclencheur, touche morte), l'exercice attend un autre caractère ou une frappe fantôme. Sans effet
  hors des Leçons.
- **Où** : `PhysicalTextInputBuffer.cs`, appelé par `LessonsWindow.cs:3225`.
- **Correctif** : reprendre le résultat réel du moteur et ne rien attendre quand il n'émet rien.
- **Tests** : `AZERTYGlobal.Tests/MachinesEtatsLot1Tests`, quatre tests « FileDesLecons_… ».
- **Risque du correctif** : moyen (interface entre le moteur et les Leçons).

## ME-01 — Dates relues sans culture invariante (faible)

- **Décision** : 1.3.0 (QCM du 16 novembre 2026) ; corrigé.
- **Fait** : les cinq lectures de date passent par un seul analyseur, `StoredDate.Parse` (`src/StoredDate.cs`, `yyyy-MM-dd`, culture invariante), qui remplace `ParseDate` (`UsageStats`, `TrainingReminders`), `ParseTrainingDate` (`UsageStatsWindow`) et les deux `TryParseExact` de `IsConsecutiveDay` ; `IsConsecutiveDay` devient `internal` pour le test de série. Tests : `MachinesEtatsLot5Tests`, trois tests « …QuelleQueSoitLaCulture » (Défi, rappel, statistiques) et une théorie de série sur deux jours sous `th-TH`, tous rouges avant le correctif (le cas du 29 février 2024 aussi : date refusée par le calendrier thaï). À vérifier à la main : une session Windows réglée sur un format régional thaï.
- **Constat** : C5.1 (corrigé).
- **Effet** : les dates sont écrites en culture invariante (`2026-11-15`) mais relues avec la
  culture de Windows. Sous un calendrier non grégorien (thaï bouddhiste, par exemple), la date relue
  n'est plus celle du jour : la série affichée tombe à 0, le Défi commun se recompte le même jour,
  et le rappel d'entraînement peut se répéter le même soir ; or trois rappels ignorés coupent
  définitivement les rappels (`TrainingReminders.cs:118-121`).
- **Probabilité** : faible. Seuls sont touchés les formats régionaux de Windows dont le calendrier
  par défaut n'est pas grégorien (thaï, arabe d'Arabie saoudite, par exemple), pour une application
  en français et en anglais. L'effet est sérieux pour ces utilisateurs, la gravité est ramenée à
  faible pour cette raison.
- **Où** : cinq `DateOnly.TryParseExact` sans `CultureInfo.InvariantCulture`
  (`UsageStats.cs:449, 450, 455`, `TrainingReminders.cs:155`, `UsageStatsWindow.cs:755`).
  `ConfigManager.cs:471-473` le fait déjà correctement.
- **Correctif** : un seul analyseur de date partagé, en culture invariante, appelé aux cinq
  endroits. Les cinq doivent changer ensemble : corriger `:449` sans `:450` ferait repartir la série
  chaque jour.
- **Tests** : `MachinesEtatsLot5Tests`, trois tests « …QuelleQueSoitLaCulture » (Défi, rappel,
  statistiques) ; à compléter par un test de série sur deux jours.
- **Risque du correctif** : très faible (lecture seule, format inchangé).

## ME-02 — Progression des Leçons : une entrée mal formée bloque la fenêtre (faible)

- **Décision** : 1.3.0 (QCM du 16 novembre 2026) ; corrigé.
- **Fait** : `LessonProgressStore.Load` ignore une entrée d'`exercises` qui n'est pas un objet (nombre, `null`, tableau, chaîne) et garde les autres, comme il le faisait déjà pour `gestures` ; le fichier n'est pas mis de côté. Tests : `MachinesEtatsLot5Tests`, deux tests rouges avant le correctif (`InvalidOperationException` sortant du constructeur). Écart avec la piste du constat : l'ajout de `InvalidOperationException` au filtre de `Load` n'est pas fait, il aurait fait mettre le fichier de côté pour une seule entrée mal formée. À vérifier à la main : un `lessons-progress.json` édité avec une entrée numérique, puis ouverture de la fenêtre des Leçons.
- **Constat** : C5.6 (corrigé).
- **Effet** : si une entrée d'`exercises` dans `lessons-progress.json` n'est pas un objet (un
  nombre, `null`, un tableau), le chargement échoue à chaque ouverture : la fenêtre des Leçons ne
  s'ouvre plus, le fichier n'est ni réparé ni mis de côté.
- **Probabilité** : très faible. L'application n'écrit que des objets et un format plus récent est
  écarté avant la lecture (`LessonProgressStore.cs:404-417`) ; il reste le fichier modifié à la
  main. L'effet est sérieux, la gravité est ramenée à faible pour cette raison.
- **Où** : `LessonProgressStore.cs:455-456, 482, 611-613`. La garde existe pour `gestures` (`:442`),
  pas pour `exercises`.
- **Correctif** : ignorer une entrée qui n'est pas un objet, garder les autres.
- **Tests** : `MachinesEtatsLot5Tests.Progression_UneEntreeNonObjet_…` et `…_EntreesMalFormees_…`.
- **Risque du correctif** : très faible.

## ME-07 — Petits états périmés dans quatre fenêtres (faible)

- **Décision** : 1.3.0 (QCM du 16 novembre 2026) ; corrigé.
- **Constats** : C5.5 (corrigé), C6.2 (corrigé), C8.2 (corrigé), C1.7 (corrigé : le test, écrit avant le correctif, était rouge, donc confirmé).
- **Fait** : C5.5 : `ConfigManager.SetTrainingEnabled` déclenche un nouvel événement `TrainingEnabledChanged` quand la valeur change ; `UsageStatsWindow` s'y abonne (désabonnement dans `Dispose`) et refait sa mise en page (`FitWindow`) et sa peinture : la hauteur et les boutons suivent la section Défi, que la fenêtre soit visible ou non. C6.2 : `LessonProgressStore.SetLastPosition` ignore un exercice du Défi, comme `RecordSuccess` (le correctif est à la source et non dans `ApplyProgressReset` : tout chemin est couvert ; au Défi, la remise à zéro laisse la position vide et la fermeture rend la première leçon à faire). C8.2 : `CharacterSearch.Show` remet à faux « Copié » et « Copie impossible » et tue la minuterie de 1500 ms encore en vol. C1.7 : `LayerIndicatorWindow.Update` et `RefreshPosition` sortent sans fenêtre, `Hide` n'appelle `ShowWindow` que si la fenêtre existe. Points d'appui ajoutés au code : `LayerIndicatorWindow.InvalidateWindow` (invalidation remplaçable) et un constructeur interne qui reçoit l'enregistrement de classe, pour éprouver l'indicateur sans fenêtre. Tests : `MachinesEtatsLot5Tests` (1), `MachinesEtatsLot6Tests` (2, fichier créé), `MachinesEtatsLot8Tests` (1), `MachinesEtatsLot1Tests` (1, fichier créé), tous rouges avant le correctif, verts après. À vérifier à la main : ouvrir Statistiques, décocher l'entraînement dans les Paramètres, la fenêtre se resserre sans bandeau vide (et se rallonge si on le recoche) ; sur le Défi, « Réinitialiser la progression » puis fermer : la fenêtre rouverte propose la première leçon, pas le Défi ; copier un caractère dans la recherche hors cible, rouvrir aussitôt (moins de 1,5 s) : pas de « Copié » ; l'échec réel de création de la fenêtre de l'indicateur n'est pas reproductible à la main.
- **Effets** : la fenêtre Statistiques garde la hauteur de la section Défi après son masquage
  (C5.5) ; « Réinitialiser la progression » depuis le Défi enregistre le Défi comme position de
  reprise (C6.2) ; la recherche de caractère rouverte affiche encore « Copié » pendant 1,5 s au plus
  (C8.2) ; l'indicateur de couche demande un repeint alors que sa fenêtre n'a pas pu être créée (C1.7).
- **Correctif** : un correctif local par fenêtre, sans lien entre eux.
- **Tests** : `MachinesEtatsLot5Tests` (1), `MachinesEtatsLot6Tests` (2), `MachinesEtatsLot8Tests` (1),
  `MachinesEtatsLot1Tests` (1, C1.7).
- **Risque du correctif** : faible.

## ME-08 — Demande d'avis : mauvais moment, minuterie perdue, double appel (faible)

- **Décision** : 1.3.0 (QCM du 16 novembre 2026) ; partiellement corrigé (C7.3 et C7.4) ; C7.5 laissé plausible.
- **Fait** : C7.3, l'essai de frappe est reporté tant que l'accueil est visible, y compris rouvert depuis le menu (`_onboarding is { IsVisible: true }` ajouté à la garde du tick de quiétude, signal conservé). C7.4, `EnsureReviewTimer` pose ou retire la minuterie d'après `ReviewPromptStillPossible` (appelée au `Run`, au tick et par le nouveau `SettingsWindow.NotificationsChanged`, déclenché après chaque clic sur la case des notifications ; branchement extrait dans `ConfigureSettingsWindow`). C7.5 : non corrigé, le défaut ne se prouve qu'avec un vrai paquet Store signé (double clic sur « Noter sur le Microsoft Store ») et aucun test honnête n'existe ; la garde `Interlocked` prévue reste à écrire si l'observation la confirme. Tests : `MachinesEtatsLot7Tests`, quatre tests ; le test C7.3 et le test de câblage côté Paramètres étaient rouges avant le correctif (compteur d'essais à 1 au lieu de 0 ; tray jamais prévenu), les deux autres couvrent les seams ajoutées (`TrayApplication.ReviewTimerSwitch`, `ReviewTimerArmed`). Écart avec le correctif prévu : la minuterie est réarmée par un rappel de la fenêtre des Paramètres vers le tray, qu'il fallait créer (rien ne les reliait), et non par un abonnement existant. À vérifier à la main : rallumer les notifications dans les Paramètres puis taper au-delà du seuil, et rouvrir l'accueil depuis le menu pendant un silence de 15 s (aucun toast d'avis ne doit apparaître) ; double clic réel sur « Noter sur le Microsoft Store » (C7.5).
- **Constats** : C7.3 (corrigé), C7.4 (corrigé), C7.5 (plausible, vérifiable seulement avec un vrai paquet Store).
- **Effet** : un des deux essais de demande d'avis part pendant que l'accueil, rouvert depuis le menu,
  est à l'écran (C7.3) ; si les notifications sont coupées puis rétablies, l'essai attend le
  prochain lancement (C7.4) ; rien n'empêche deux appels de la boîte d'avis du Store (C7.5).
- **Où** : `TrayApplication.cs` (demande d'avis), `StoreReview`.
- **Correctif** : « accueil visible » vaut report ; minuterie réarmée aussi quand le réglage des
  notifications change ; un seul appel à la fois pour la boîte du Store.
- **Tests** : `MachinesEtatsLot7Tests` (`Avis_AccueilRouvertDepuisLeMenu_…`, `Avis_NotificationsRallumeesDansLesParametres_…`, `Parametres_CaseNotifications_PrevientLeTrayApresEcriture`, `Avis_TickPendantLaCoupure_…`).
- **Risque du correctif** : faible.

## ME-09 — Réglages : seconde instance, écriture de fermeture, instance élevée (faible)

- **Décision** : 1.3.0 (QCM du 16 novembre 2026) ; corrigé.
- **Fait** : C4.5, `Program.Main` lit la langue par `ConfigManager.AppLanguageWithoutLoading` (lecture seule, sans cache ni quarantaine, même valeur que le chargement normal) : la seconde instance ne touche plus à `config.json`, le gagnant le charge et le met de côté au premier accès de `TrayApplication`. C4.3, `ConfigManager.FlushFinal` (Flush puis 3 reprises synchrones de 250 ms, perte journalisée `ConfigFinalWriteLost`), appelée par `Cleanup` et `WM_QUERYENDSESSION`. C4.1, la prise des deux mutex est extraite en `Program.AcquireSingleInstance` (fabrique remplaçable) ; `UnauthorizedAccessException` sur `Local\` vaut seconde instance. Tests : `MachinesEtatsLot4Tests`, 10 tests ajoutés (19 cas) ; rouges avant le correctif : C4.5 (2 tests et 2 lignes du test paramétré, sur une lecture qui mettait le fichier de côté), C4.3 (3 tests), C4.1 (1 test) ; les autres gardent le comportement. Écarts avec le correctif prévu : le test rouge d'origine de C4.5 (fichier disparu pendant le renommage) ne se corrige pas par l'ordre de démarrage seul, d'où un second correctif de défense dans `EnsureLoaded` (fichier absent après un renommage raté : pas de `_loadFailed`) ; le gestionnaire d'erreur fatale n'est pas déplacé avant les mutex (la prise des mutex ne lève plus rien d'autre que ce que `Global\` capturait déjà). À vérifier à la main : lancer l'application en administrateur puis en mode normal (aucune boîte d'erreur système, seconde instance qui sort) ; deux lancements simultanés sur un `config.json` corrompu ; un `config.json` tenu ouvert par un autre programme au moment de quitter.
- **Constats** : C4.5 (corrigé), C4.3 (corrigé), C4.1 (corrigé).
- **Effet** : deux lancements simultanés sur un `config.json` corrompu : l'un garde l'état
  « chargement échoué », l'accord d'activation y est lu comme absent pendant la session et le
  message de remise à zéro est perdu (C4.5) ; une écriture de fermeture ratée perd les derniers
  réglages sans nouvel essai (C4.3) ; avec une instance élevée déjà lancée, un lancement non élevé
  plante au lieu de sortir en silence (C4.1).
- **Où** : `Program.cs:43-52, 84-85`, `ConfigManager.cs`.
- **Correctif** : lire la langue sans mettre le fichier de côté, et ne le mettre de côté qu'après
  l'acquisition des mutex (la langue doit rester lue avant, pour traduire les messages de refus) ;
  écriture finale avec quelques nouveaux essais bornés ; accès refusé au mutex traité comme « déjà lancée ».
- **Tests** : `MachinesEtatsLot4Tests` (`Fichier_corrompu_deja_mis_de_cote_…`, `Langue_lue_au_demarrage_…`, `Ecriture_de_fermeture_…`, `Arret_et_sortie_du_tray_…`, `Mutex_local_refuse_…`, `Acquisition_des_deux_mutex_…`, `Mutex_global_indisponible_…`).
- **Risque du correctif** : moyen (ordre de démarrage du processus).

## ME-10 — Horloge ou fuseau qui recule (faible)

- **Décision** : 1.3.0 (QCM du 16 novembre 2026) ; corrigé.
- **Fait** : une date enregistrée postérieure à aujourd'hui vaut aujourd'hui. `UsageStats.RecordActivityLocked` ne recompte plus le jour et ramène la dernière activité à aujourd'hui (série et jours actifs inchangés) ; `CurrentStreak` la lit comme aujourd'hui ; `TrainingReminders.WouldCount` (Défi) et `ShouldRemind` (dernier rappel, dernière séance) comparent en `>= today`. Tests : `MachinesEtatsLot5Tests`, six tests, tous rouges avant le correctif (jour recompté, série à 1, série affichée à 0, séance du Défi recomptée, rappel et séance datés de demain). Écart avec le verdict : il ne corrigeait que `RecordActivityLocked` et jugeait les étapes rappels inexactes ; une date de rappel future autorisait pourtant un second rappel (test rouge), d'où `>= today` dans `ShouldRemind`. À vérifier à la main : un vrai recul d'horloge d'un jour sur la machine.
- **Constat** : C5.7 (corrigé).
- **Effet** : si la dernière date enregistrée est dans le futur (horloge corrigée, voyage), le jour
  est recompté, la série repart à 1 et le Défi du jour se recompte.
- **Correctif** : une date enregistrée postérieure à aujourd'hui vaut aujourd'hui, pour la série, le
  Défi et les rappels.
- **Tests** : `MachinesEtatsLot5Tests`, trois tests.
- **Risque du correctif** : faible.

## ME-11 — Presse-papiers : échec muet, et un test qui dépend du presse-papiers réel (faible)

- **Décision** : 1.3.0 (QCM du 16 novembre 2026) ; corrigé.
- **Fait** : C8.1 confirmé par test (couture ajoutée, test rouge avant le correctif) puis corrigé. `CharacterSearch.ClipboardWriter` et `ChallengeSessionState.ClipboardWriter` remplacent l'appel direct à `ClipboardText.TrySet` ; `ClipboardText.OpenWithRetry` ouvre le presse-papiers jusqu'à 4 fois (25 ms entre les essais). Échec de la copie : recherche = ballon, annonce du champ et « Copie impossible » dans le pied ; Défi = le bouton passe à « Copie impossible, réessayer » (lecteur d'écran notifié), un nouveau clic réessaie. `SummaryButtonsInvariantTests.EveryDrawnTargetOfTheSummary_Acts("defi-final-partage")` passe par l'écriture remplaçable (cause confirmée : `OpenClipboard` refusé, erreur 5) et ne dépend plus du presse-papiers réel. Tests : `MachinesEtatsLot8Tests`, 6 tests dont 5 rouges avant le correctif (le sixième, sans attente quand l'ouverture réussit, garde le comportement). À vérifier à la main : un presse-papiers réellement tenu par un autre programme, sur le bureau interactif.
- **Constat** : C8.1 (corrigé).
- **Effet** : quand le presse-papiers est occupé ou refusé, la copie de secours de la recherche de
  caractère et « Copier mon résultat » du Défi échouent sans rien dire.
- **Test existant en échec, indépendant de cette analyse** :
  `SummaryButtonsInvariantTests.EveryDrawnTargetOfTheSummary_Acts(end: "defi-final-partage")` échoue
  sur `250c4ef` dans l'environnement d'exécution de l'analyse : trois exécutions sur trois, relevées
  pendant l'analyse (deux suites complètes : 1801 réussis, 1 échec, 3 ignorés sur 1805 ; puis la
  classe seule : 13 réussis, 1 échec). Cause très probable : l'ouverture du presse-papiers y est refusée (mesuré
  par une sonde lancée depuis un autre processus du même environnement), donc le bouton ne change
  aucun état et le test le juge mort. Ce n'est pas un défaut démontré du bilan du Défi. Non vérifié
  sur le bureau interactif.
- **Correctif** : écriture du presse-papiers remplaçable dans les tests (comme
  `UsageStatsWindow.ClipboardWriter`), nouvel essai borné de l'ouverture, retour visible en cas
  d'échec ; le test existant ne dépend plus de la machine.
- **Tests** : `AZERTYGlobal.Tests/MachinesEtatsLot8Tests` (échec annoncé, seconde Entrée, Défi, nouvel essai borné).
- **Risque du correctif** : faible.

## ME-12 — Comportements à trancher (faible)

- **Décision** : à décider, question par question.

Les quatre premiers sont atteints (un test le montre) mais peuvent être voulus ; les trois derniers
sont probables à la lecture. Chacun demande une décision avant tout correctif.

| Constat | Comportement | Question |
|---|---|---|
| C1.1 (atteint) | Un coup de couche et une touche morte armés en même temps. | L'un doit-il annuler l'autre ? |
| C1.5 (atteint) | Un coup de couche armé survit à Entrée et à Tab : la lettre suivante sort dans la couche. L'indicateur reste affiché. | Entrée et Tab doivent-elles annuler le coup ? (À vérifier avant de corriger : Maj ne devrait pas l'annuler, pour garder la capitale après un coup.) |
| C2.2 (atteint) | Un ouvrier de détection de saisie sécurisée bloqué garde « sécurisé » pour toutes les fenêtres. | Garder cet échec fermé ? Un test existant le fixe comme voulu (`SecureInputProbeTests.Worker_bloque_ne_reutilise_pas_…`). |
| C6.1 (atteint) | Fermer les Leçons pendant le bilan du parcours solde la séance et avance la position ; la garde prévue contre cela n'agit jamais. | Solder ou non à la fermeture ? |
| C2.1 (lecture) | La sonde de silence réinstalle le hook toutes les 4 s environ pendant la frappe dans une fenêtre élevée, avec une ligne de journal à chaque fois. | Plafonner les réinstallations et le journal ? |
| C2.4 (lecture) | Un recalcul du premier plan lancé pendant un autre serait écrasé par l'ancien résultat, 250 ms au plus. Le test simule cette situation ; rien ne prouve qu'elle se produit dans l'application compilée. | Recalculer en fin de traitement, par précaution ? |
| C5.2 (lecture) | Pendant le parcours de prise en main, la frappe faite dans une autre application n'est pas comptée. | Lier l'exclusion à la fenêtre active ? |

- **Tests** : `AZERTYGlobal.Tests/MachinesEtatsLot1Tests` (C1.1, C1.5), `MachinesEtatsLot6Tests` (C6.1),
  `TypingEngine.Windows.Tests/MachinesEtatsLot2Tests` (C2.2, C2.4).

## ME-13 — Zone de notification : clics, menu, bulles, icône (faible)

- **Décision** : laissé, documenté (QCM du 16 novembre 2026) ; à reprendre en 1.3.1 si besoin.
- **Constats** : C3.1 à C3.7, tous plausibles (aucun test : `TrayApplication` n'offre pas de point
  d'accroche pour ces chemins).
- **Effets décrits** : action de simple clic après un double clic (C3.1) ; commande de menu appliquée
  à un état qui a changé pendant que le menu était ouvert (C3.2) ; détection de conflit de
  disposition morte si la fenêtre échoue à se construire (C3.3) ; bascule complète exécutée dans le
  rappel du hook (C3.4) ; bulles à usage unique marquées faites avant d'être affichées (C3.5) ; icône
  absente sans nouvel essai si son ajout échoue (C3.6) ; fenêtre de conflit ouverte pendant une pause
  ou un jeu (C3.7).
- **Correctif** : d'abord extraire des décisions testables (routeur de clics, garde de conflit,
  nouvel essai d'icône), écrire les tests, puis corriger ce que les tests confirment.
- **Risque du correctif** : moyen (restructuration de `TrayApplication`).
