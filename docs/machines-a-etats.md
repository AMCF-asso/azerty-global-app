# Machines à états de l'application AZERTY Global 1.3.0

## Objet

Ce document recense les machines à états de l'application Windows, qu'elles soient explicites
(une énumération, un champ d'état nommé) ou implicites (une combinaison de booléens, de
minuteries et de poignées de fenêtre qui se comporte comme une machine sans en porter le nom).
Pour chacune il donne les variables d'état, les états, les invariants, la table de transition,
les temporisations et réessais, puis les états ou transitions invalides que le code permet
réellement d'atteindre.

- Base de l'analyse : commit `250c4ef` (branche `claude/ag-correctifs-avis-130`), le 15 novembre 2026.
- État des correctifs : branche `claude/ag-machines-etats-130`, 12 commits depuis `250c4ef` (le document et les tickets, puis onze correctifs ME-01 à ME-11), suivis des suites du 16 novembre 2026 et de ME-12 (C1.1, C1.5, C2.2, C6.1), au 16 novembre 2026.
- Périmètre : `src/*.cs`, `src/TypingEngine.Core`, `src/TypingEngine.Windows` (45 181 lignes de C# hors tests).
- Tickets issus des constats : [machines-a-etats-tickets.md](machines-a-etats-tickets.md).

## Méthode

1. Huit lots de code ont été lus séparément, chacun rendant ses machines et ses constats avec les
   lignes de code à l'appui.
2. Chaque constat a été relu par un relecteur chargé de le réfuter : il tient, il est incertain
   (comportement réel mais peut-être voulu, ou scénario non établi) ou il est réfuté.
3. Un test de régression a été écrit pour chaque constat non réfuté, quand le code offre déjà un
   point d'accroche. Le test affirme le comportement correct : il échoue tant que le défaut existe.
4. La liste des tickets a été relue par un relecteur chargé de la contredire ; six statuts ou
   gravités ont été ramenés à ce qui est réellement prouvé.
5. Les constats ont ensuite été corrigés, un commit par ticket, chaque correctif avec son test
   rouge avant et vert après. Une relecture des correctifs a relevé des points mineurs ; ceux que le code a confirmés
   ont donné les « suites du 16 novembre 2026 » citées dans les tickets.

Vocabulaire des statuts :

| Statut | Sens |
|---|---|
| confirmé | Le constat tient à la relecture et son test échoue sur `250c4ef` pour la raison décrite. |
| corrigé | Confirmé, puis corrigé : le même test réussit. Le commit porte l'identifiant du ticket dans son titre. |
| atteignable, à trancher | Le test prouve que l'état est atteint, mais le comportement peut être voulu : décision de conception. |
| plausible | Pas de preuve par test : il faudrait un point d'accroche (« couture ») dans le code de production, ou la preuve n'existe qu'en conditions réelles. Un test qui simule la situation sans prouver qu'elle se produit ne suffit pas. |
| réfuté | La relecture a montré que l'état n'est pas atteignable ou n'est pas un défaut. Gardé pour mémoire. |

Un constat n'est jamais dit « confirmé » sans test en échec avant correctif.

## Modèle de fils

Les « courses » possibles dépendent de ce modèle, établi à la lecture du code :

- Le fil principal (STA) fait tourner la pompe de messages. Le rappel du hook clavier bas niveau
  s'exécute sur ce même fil : le hook et les fenêtres ne se font donc jamais concurrence.
- Vraies concurrences : la pompe contre le pool de fils (`System.Threading.Timer` de
  `ConfigManager`, `QueueUserWorkItem`), deux processus sur les mêmes fichiers (seconde instance),
  les retours COM, et le fil de travail MTA de `SecureInputProbe`.
- Tout le reste est de la réentrance sur le fil principal : menu de la zone de notification
  (`TrackPopupMenuEx`), boucle modale du dialogue de pause, `MessageBox`.

Chaque lot précise son propre modèle de fils en tête de section.

## Tenir ce document à jour

- Un changement d'état, de minuterie ou de garde dans le code se reporte dans la table de la
  machine concernée, dans le même commit.
- Un correctif passe le constat à « corrigé » dans le tableau ci-dessous et dans le ticket, et la
  transition invalide disparaît de la section « États ou transitions invalides atteignables » de sa
  machine ; l'invariant rétabli y est cité avec son test.
- Un nouveau constat reçoit l'identifiant suivant de son lot (`C<lot>.<n>`) ; un identifiant n'est
  jamais réutilisé.
- Les tests de régression portent l'identifiant du constat en commentaire (`// Constat C5.1`) dans
  les fichiers `MachinesEtatsLot<N>Tests.cs`.

## Index des machines

| Id | Machine | Nature | États | Transitions | Constats |
|---|---|---|---|---|---|
| M1.1 | Composition par touche morte (`CompositionEngine`) | implicite | 2 | 15 | — |
| M1.2 | Couches maintenables : déclencheur, un coup, verrou (`MaintainableLayerManager`) | explicite | 7 | 18 | C1.1, C1.5 |
| M1.3 | Modificateurs suivis et Verr. Maj. (`KeyMapper`) | implicite | 9 | 8 | C1.4 |
| M1.4 | Propriété des appuis physiques et touches synthétiques (`KeyMapper`) | implicite | 5 | 9 | C1.6 |
| M1.5 | Suspension d'émission et abandon de la touche morte au changement d'application (`KeyMapper`) | implicite | 3 | 5 | — |
| M1.6 | Réparation après émission partielle (`InputRecovery`, `_pendingInputRecovery`) | implicite | 4 | 7 | — |
| M1.7 | File du texte attendu (`PhysicalTextInputBuffer`) | implicite | 3 | 7 | C1.2, C1.3 |
| M1.8 | Indicateur de couche (`LayerIndicatorWindow`) | implicite | 4 | 7 | C1.7 |
| M2.1 | Cycle de vie du hook Win32 (`KeyboardHook`) | implicite | 3 | 11 | — |
| M2.2 | Sonde de silence du hook (`HookSilenceWatchdog` + `ProbeHookSilence`) | implicite | 4 | 8 | C2.1 |
| M2.3 | Mode de traitement du hook (`Enabled` / `PassThroughAll` / `ShortcutsWhilePassThrough`) | implicite | 5 | 7 | — |
| M2.4 | Contexte de premier plan et mode de compatibilité (`ForegroundMonitor`) | explicite | 9 | 13 | C2.3, C2.4 |
| M2.5 | Sonde de saisie sécurisée (`SecureInputProbe` / `SecureInputDetector`) | implicite | 4 | 8 | C2.2 |
| M3.1 | Activation, pause et suspension (état du hook) | explicite | 5 | 8 | C3.2, C3.4 |
| M3.2 | Suspension de compatibilité et annonces | explicite | 4 | 3 | — |
| M3.3 | Bulles et sollicitations à usage unique | implicite | 5 | 4 | C3.5 |
| M3.4 | Dialogue de durée de pause | implicite | 3 | 4 | — |
| M3.5 | Fenêtre de conflit de disposition | implicite | 2 | 3 | C3.3, C3.7 |
| M3.6 | Icône, infobulle et notification de bascule | implicite | 4 | 5 | C3.6 |
| M3.7 | Clics sur l'icône | implicite | 2 | 5 | C3.1 |
| M3.8 | Cycle de vie et arrêt | implicite | 2 | 2 | — |
| M4.1 | Instance unique, signal de relance, activation système | implicite | 8 | 12 | C4.1 |
| M4.2 | Écriture différée de config.json | implicite | 7 | 15 | C4.3 |
| M4.3 | Chargement de config.json et mise de côté d'un fichier illisible | implicite | 5 | 7 | C4.5 |
| M4.4 | Lancement automatique (`AutoStartWindowsState`, `IsRegistered`, relance unique) | explicite | 7 | 11 | C4.2, C4.4 |
| M4.5 | Cache de politiques d'entreprise et canal de distribution | implicite | 2 | 4 | — |
| M4.6 | Serveur COM d'activation de toast | implicite | 3 | 6 | — |
| M5.1 | Persistance différée de UsageStats | implicite | 4 | 6 | — |
| M5.2 | Chargement et lecture de UsageStats | implicite | 3 | 4 | — |
| M5.3 | Activité du jour et série | implicite | 4 | 4 | C5.1 |
| M5.4 | Plage de frappe exclue | explicite | 2 | 4 | C5.2 |
| M5.5 | Signal de seuil et collecte | implicite | 2 | 3 | — |
| M5.6 | Fenêtre Statistiques | implicite | 4 | 5 | C5.5 |
| M5.7 | Magasin de progression (LessonProgressStore) | implicite | 3 | 6 | C5.3, C5.4 |
| M5.8 | Progression d'un exercice (Leitner) | implicite | 3 | 3 | C5.6 |
| M5.9 | Maîtrise d'un geste (boîtes Leitner) | explicite | 6 | 3 | — |
| M5.10 | État de séance du Défi (ChallengeSessionState) | implicite | 3 | 3 | — |
| M5.11 | Séquence du Défi et comptage des séances | explicite | 3 | 4 | — |
| M5.12 | Rappel d'entraînement (balloon) | implicite | 3 | 3 | C5.7 |
| M6.1 | Séance de frappe (LessonTypingSession) | explicite | 3 | 7 | — |
| M6.2 | Mode de fenêtre et bilan (_mode, _showSummary, _session) | implicite | 6 | 8 | C6.2 |
| M6.3 | Tutoriel de l'accueil (_tutorialPage, _tutorialRoute) | implicite | 4 | 6 | C6.1 |
| M6.4 | Accueil des Leçons (_homePage, séance composée, texte collé) | implicite | 5 | 6 | — |
| M6.5 | Indices (_hint*, minuteries d'indice) | implicite | 3 | 3 | — |
| M6.6 | Mode Libre et ses statistiques | implicite | 2 | 3 | — |
| M6.7 | Fenêtre : visibilité, focus, pause d'entrée | implicite | 4 | 4 | — |
| M7.1 | Accueil : navigation entre les étapes | explicite | 4 | 8 | C7.1 |
| M7.2 | Accueil : cycle de vie, activation et enregistrement des préférences | implicite | 7 | 12 | C7.2 |
| M7.3 | Accueil : essai de frappe intégré (états A, B, C de l'étape 1) | implicite | 4 | 7 | — |
| M7.4 | Demande d'avis : état persistant et décision (essais 1 et 2) | implicite | 4 | 8 | C7.3 |
| M7.5 | Demande d'avis : signal de frappe et minuterie de quiétude | implicite | 5 | 10 | C7.4 |
| M7.6 | Demande d'avis : notification affichée et cible du clic | implicite | 3 | 6 | — |
| M7.7 | Boîte d'avis du Store (opération asynchrone) | implicite | 2 | 5 | C7.5 |
| M7.8 | À propos et liens STATIC | implicite | 5 | 7 | — |
| M8.1 | Cycle de vie et pause de la fenêtre Paramètres | implicite | 6 | 10 | — |
| M8.2 | Fenêtre de recherche de caractère et insertion | implicite | 5 | 10 | C8.1, C8.2 |
| M8.3 | Clavier virtuel | implicite | 4 | 6 | — |
| M8.4 | Fenêtre des couches maintenables | implicite | 3 | 5 | — |
| M8.5 | Fenêtres natives, navigation de dialogue, défilement et dimensionnement | implicite | 3 | 4 | — |

## Constats : états et transitions invalides atteignables

Trente-neuf constats ont été relevés sur `250c4ef` : 15 confirmés, 4 atteints à trancher, 17 plausibles, 3 réfutés. Au 17 novembre 2026, après les correctifs : 29 corrigés (dont C3.1 et C3.6 le 16 novembre, C3.2 et C3.4 le 17, par l'audit par frappe au hasard), 6 plausibles, 1 confirmé et voulu (C3.5 : règle M3.3 I2 réécrite « jamais deux fois », décision d'Antoine), 3 réfutés, aucun à trancher. Le statut suit le vocabulaire de la section « Méthode » ; la gravité est celle retenue après les deux relectures (celle de chaque constat, puis celle de la liste des tickets). Aucun constat n'est critique ni fort.

| Id | Machine | Constat | Statut | Gravité | Ticket | Preuve |
|---|---|---|---|---|---|---|
| C1.1 | M1.2 | Un coup de couche et une composition de touche morte coexistent (grec après l'accent aigu) | corrigé | faible | ME-12 | `TypingEngine.Windows/KeyMapper.cs` (voie composition : la touche morte qui arme une composition annule le coup) ; tests `AZERTYGlobal.Tests/MachinesEtatsLot1Tests.cs` (`UnCoupDeCouche_EtUneCompositionDeToucheMorte_NeCoexistentJamais`, `UnCoupDeCouche_PuisAccent_LaLettreSuivanteEstFrancaiseAccentuee`, `UnCoupDeCouche_PuisAccent_LIndicateurSuitLAnnulation`) |
| C1.2 | M1.7 | File des Leçons : le caractère attendu diverge de l'émis si l'accord est tenu ou si le Maj d'activation est encore enfoncé | corrigé | faible | ME-06 | `TypingEngine.Windows/KeyMapper.cs` (`KeyDecided`), `PhysicalTextInputBuffer.cs` (`ApplyEngineDecision`), `LessonsWindow.cs` ; tests `AZERTYGlobal.Tests/MachinesEtatsLot1Tests.cs` (`FileDesLecons_AccordTenu_AttendCeQueLeMoteurEmet`, `FileDesLecons_MajDActivationEncoreTenu_AttendCeQueLeMoteurEmet`, suites : `FileDesLecons_LotPartiel_GardeUnAttenduCoherentAvecCeQuiAPuPartir`, `FileDesLecons_LotRefuStrictement_NAttendRien`, `FileDesLecons_TexteDePlusieursCaracteresCoupe_AnnonceLesCaracteresCommences` ; `TypingEngine.Windows.Tests/CharsStartedByTests.cs`) |
| C1.3 | M1.7 | File des Leçons : entrée fantôme quand un déclencheur ou une touche morte est pressé sous un coup armé | corrigé | faible | ME-06 | `TypingEngine.Windows/KeyMapper.cs` (`KeyDecided`), `PhysicalTextInputBuffer.cs` (`ApplyEngineDecision`), `LessonsWindow.cs` ; tests `AZERTYGlobal.Tests/MachinesEtatsLot1Tests.cs` (`FileDesLecons_SecondAppuiDuDeclencheurSousUnCoup_NAttendAucunCaractere`, `FileDesLecons_ToucheMorteSousUnCoup_NAttendAucunCaractere`) |
| C1.4 | M1.3 | Le passage natif des lettres lit le Maj physique alors que le Maj d'activation est neutralisé (A au lieu de a) | corrigé | faible | ME-05 | `TypingEngine.Windows/KeyMapper.cs` (`CanPassThrough` reçoit les modificateurs effectifs) ; test `AZERTYGlobal.Tests/MachinesEtatsLot1Tests.cs` (`MajDActivationNeutralise_LaSecondeLettreNeFuitPasEnMajusculeNative`) |
| C1.5 | M1.2 | Un coup de couche survit à Entrée et à Tab, sans expiration | corrigé | faible | ME-12 | `TypingEngine.Windows/KeyMapper.cs` (Entrée et Tab appellent `CancelOneShot`) ; tests `AZERTYGlobal.Tests/MachinesEtatsLot1Tests.cs` (`UnCoupDeCouche_EstAnnuleParEntreeEtTab`, `UnCoupDeCouche_AnnuleParEntreeOuTab_LIndicateurSuitLAnnulation`, `UnCoupDeCouche_UnModificateurSeul_NAnnuleJamaisLAttente`, `UnCoupDeCouche_AvecMaj_LaMajusculeDeLAutreAlphabetSort`, `UnCoupDeCouche_SecondDeclencheurTenuPuisEntreeTabOuRetourArriere_LAccordSePromeutEtLaLettreSortEnGrec`, `UnCoupDeCouche_AccordDejaPromuPuisEntreeTabOuRetourArriere_LeCoupEstAnnuleEtRienNeReste`) |
| C1.6 | M1.4 | Répétition automatique du déclencheur après relâchement du modificateur d'activation : étoile émise ou code de touche périmé dans `_passedThroughKeys` | corrigé | faible | ME-05 | `TypingEngine.Windows/KeyMapper.cs` (`ProcessKey` : répétition du déclencheur tenu) ; test `AZERTYGlobal.Tests/MachinesEtatsLot1Tests.cs` (`RepetitionDuDeclencheur_ApresRelachementDeMaj_NEmetRienEtNeSuitRien`) |
| C1.7 | M1.8 | `LayerIndicatorWindow` sans fenêtre : `InvalidateRect` avec un HWND nul | corrigé (test rouge avant le correctif : confirmé) | faible | ME-07 | `AZERTYGlobal/LayerIndicatorWindow.cs` (`Update`, `RefreshPosition`, `Hide` ; points d'appui `InvalidateWindow` et constructeur interne à enregistrement de classe injectable) ; test `AZERTYGlobal.Tests/MachinesEtatsLot1Tests.cs` (`IndicateurDeCouche_SansFenetre_NeRepeintRien`) |
| C2.1 | M2.2 | La sonde de silence réinstalle le hook toutes les ~4 s pendant la frappe dans une fenêtre de niveau d'intégrité supérieur | plausible (preuve en réel seulement) | faible | ME-12 | lecture du code |
| C2.2 | M2.5 | Un ouvrier UIA unique bloqué rend « sécurisé » le premier plan de toutes les applications | corrigé | faible | ME-12 | `TypingEngine.Windows/SecureInputProbe.cs` (abandon de l’ouvrier bloqué après un délai, générations), `SecureInputDetector.cs` (`_automation` par fil, relâchée à toute sortie du fil par la couture `workerExit`) ; tests `TypingEngine.Windows.Tests/MachinesEtatsLot2Tests.cs` (`Ouvrier_bloque_sur_une_fenetre_ne_garde_pas_une_autre_fenetre_securisee_indefiniment`, `Ouvrier_arrete_libere_son_instance_sur_son_propre_fil`, `Ouvrier_abandonne_qui_revient_libere_son_instance_et_pas_celle_du_remplacant`, `Ouvrier_dont_l_initialisation_echoue_ou_leve_libere_aussi`, `Le_detecteur_branche_ReleaseAutomation_sur_la_sortie_de_l_ouvrier`) et `SecureInputProbeTests.cs` (`Worker_bloque_n_est_ni_multiplie_ni_abandonne_avant_le_delai`, `Worker_bloque_est_abandonne_apres_le_delai_et_une_nouvelle_detection_repart`, `Retour_tardif_d_un_worker_abandonne_n_ecrase_pas_la_detection_recente`, `Workers_abandonnes_sont_bornes_puis_la_sonde_reste_securisee`, `Apres_le_retour_des_workers_abandonnes_la_sonde_repart_normalement`) |
| C2.3 | M2.4 | Premier plan absent traité Default/None (fail-open) alors que l'inconnu est traité fail-closed | réfuté | — | — | relecture critique |
| C2.4 | M2.4 | Recompute attend sur un fil STA : pompe imbriquée possible pendant l'attente UIA | plausible (preuve en réel seulement) | faible | ME-12 | lecture du code ; preuve hors dépôt : test simulé non retenu (il simulait l'imbrication sans prouver qu'elle se produit) |
| C3.1 | M3.7 | Un double clic sur l'icône réarme la minuterie de simple clic après son `KillTimer` | plausible (couture à ajouter) | faible | ME-13 | lecture du code |
| C3.2 | M3.1 | Commandes de menu appliquées à l'état courant et non à l'état affiché | confirmé (couture `ITrayShell`), puis corrigé, 17 novembre 2026 : état affiché retenu (`MenuShownState`) | faible | ME-13 | `Regles130TrayEtatTests.M3_1_I4_*` (rouge avant le correctif) |
| C3.3 | M3.5 | `_layoutPopupOpen` posé avant la construction de la fenêtre, sans `try/finally` | plausible (couture à ajouter) | faible | ME-13 | lecture du code |
| C3.4 | M3.1 | Le raccourci global exécute tout `OnToggle`/`StopPause` dans le rappel du hook clavier | confirmé (couture `ITrayShell`), puis corrigé, 17 novembre 2026 : bascule postée (`WM_APP_TOGGLE`) | faible | ME-13 | `Regles130TrayEtatTests.M3_1_I5_*` (rouge avant le correctif) |
| C3.5 | M3.3 | Bulles à usage unique marquées faites avant l'affichage, dans la foulée du `NIM_ADD` | confirmé (couture `ITrayShell`), comportement voulu (décision d'Antoine, 17 novembre 2026) : règle M3.3 I2 réécrite « jamais deux fois » | faible | ME-13 | `Regles130TrayBullesTests.M3_3_I2_*` |
| C3.6 | M3.6 | `NIM_ADD` sans contrôle de retour ni réessai, y compris sur `TaskbarCreated` | confirmé (couture `ITrayShell`), puis corrigé, 16 novembre 2026 : réessai borné | faible | ME-13 | `CoutureTrayTests.C3_6_*` (4 tests : réessai, borne, doublon, nettoyage) |
| C3.7 | M3.5 | Fenêtre de conflit de disposition sans garde sur l'état de l'application | plausible (couture à ajouter) | faible | ME-13 | lecture du code |
| C4.1 | M4.1 | Mutex `Local\` créé par une instance élevée : le lancement non élevé plante avant tout gestionnaire d'erreur | corrigé | faible | ME-09 | `AZERTYGlobal/Program.cs` (`AcquireSingleInstance`) ; tests `AZERTYGlobal.Tests/MachinesEtatsLot4Tests.cs` (`Mutex_local_refuse_par_une_instance_elevee_est_une_seconde_instance`, `Acquisition_des_deux_mutex_dit_qui_l_on_est`, `Mutex_global_indisponible_laisse_demarrer_en_mode_degrade`) |
| C4.2 | M4.4 | Version non packagée : `IsRegistered` ignore le refus de Windows, donc `Set(true)` rend vrai sans effet réel | corrigé | moyenne | ME-04 | `AZERTYGlobal/AutoStart.cs` (`IsRegistered`, `DefaultOnboardingCheck`, `GetFailureMessage`), `Localization/L.Startup.cs` (`AutoStart_FailureMessageRefused`) ; tests `AZERTYGlobal.Tests/MachinesEtatsLot4Tests.cs` (`Accueil_ne_coche_pas_la_case_quand_Windows_refuse_meme_si_le_raccourci_existe`, `Activation_refusee_par_Windows_n_est_pas_dite_reussie`, `Case_des_Parametres_et_de_l_accueil_refletent_le_refus_de_Windows`, `Refus_de_Windows_renvoie_vers_les_Parametres_Windows`) |
| C4.3 | M4.2 | Écriture de fermeture ratée : réglages perdus, aucune reprise synchrone | corrigé | faible | ME-09 | `AZERTYGlobal/ConfigManager.cs` (`FlushFinal`), `TrayApplication.cs` (`WM_QUERYENDSESSION`, `Cleanup`) ; tests `AZERTYGlobal.Tests/MachinesEtatsLot4Tests.cs` (`Ecriture_de_fermeture_reessaie_puis_ecrit_le_reglage`, `Ecriture_de_fermeture_impossible_rend_la_main_sous_le_plafond_et_journalise_la_perte`, `Arret_confirme_et_sortie_du_tray_passent_par_l_ecriture_de_fermeture`, `Demande_d_arret_annulee_n_endort_pas_le_fil_d_interface`, `Demande_d_arret_appelle_OnQueryEndSession_et_n_ecrit_pas_la_fermeture_dans_le_case`) |
| C4.4 | M4.4 | La relance unique du lancement automatique est proposée même quand Windows ou la politique refuse | corrigé | faible | ME-04 | `AZERTYGlobal/AutoStartNudge.cs` (`AutoStartSignals.WindowsState`, `ShouldPrompt`, `Snapshot`) ; tests `AZERTYGlobal.Tests/MachinesEtatsLot4Tests.cs` (`Relance_unique_n_est_pas_proposee_quand_l_etat_est_refuse_ou_inconnu`, `Signaux_de_la_relance_portent_l_etat_Windows`, `Relance_refusee_ne_consomme_pas_la_proposition`) |
| C4.5 | M4.3 | Seconde instance : `config.json` lu et mis de côté avant le mutex, `_loadFailed` reste collé et la notice est perdue | corrigé | faible | ME-09 | `AZERTYGlobal/Program.cs`, `ConfigManager.cs` (`AppLanguageWithoutLoading`, `EnsureLoaded`) ; tests `AZERTYGlobal.Tests/MachinesEtatsLot4Tests.cs` (`Fichier_corrompu_deja_mis_de_cote_par_l_autre_instance_ne_bloque_pas_les_ecritures`, `Langue_lue_au_demarrage_ne_met_pas_le_fichier_corrompu_de_cote`, `Langue_lue_au_demarrage_est_celle_du_chargement_normal`) |
| C5.1 | M5.3 | Dates analysées sans culture invariante : série et Défi faussés sous un calendrier non grégorien | corrigé | faible | ME-01 | `AZERTYGlobal/StoredDate.cs` (analyseur unique), `UsageStats.cs`, `TrainingReminders.cs`, `UsageStatsWindow.cs` ; tests `AZERTYGlobal.Tests/MachinesEtatsLot5Tests.cs` (`DefiCommun_UneSeanceDejaFaiteAujourdhui_NeCompteJamaisDeuxFois_QuelleQueSoitLaCulture`, `Rappel_UnSeulParJour_QuelleQueSoitLaCulture`, `Statistiques_UneDateEcriteEnInvariantSeRelitAIdentique_QuelleQueSoitLaCulture`, `Statistiques_DeuxJoursQuiSeSuivent_FontUneSerie_QuelleQueSoitLaCulture`) |
| C5.2 | M5.4 | Exclusion de frappe liée à l'ouverture du tutoriel et non au focus | plausible (couture à ajouter) | faible | ME-12 | lecture du code |
| C5.3 | M5.7 | États absorbants _loadFailed et _readFailed jusqu'au redémarrage | réfuté | — | — | relecture critique |
| C5.4 | M5.7 | ResetAll partiel : état de progression résiduel | réfuté | — | — | relecture critique |
| C5.5 | M5.6 | Prédicat de mise en page différent de celui de la peinture dans la fenêtre Statistiques | corrigé | faible | ME-07 | `AZERTYGlobal/UsageStatsWindow.cs` (`OnTrainingEnabledChanged`), `ConfigManager.cs` (`TrainingEnabledChanged`) ; test `AZERTYGlobal.Tests/MachinesEtatsLot5Tests.cs` (`Statistiques_SectionDefiMasqueePendantLOuverture_LaHauteurDeMiseEnPageSuit`) |
| C5.6 | M5.8 | Entrée d'exercice non objet : TryGetProperty lève et fait échouer tout le chargement | corrigé | faible | ME-02 | `AZERTYGlobal/LessonProgressStore.cs` (`Load`) ; tests `AZERTYGlobal.Tests/MachinesEtatsLot5Tests.cs` (`Progression_UneEntreeNonObjet_NeFaitPasEchouerLeChargement`, `Progression_EntreesMalFormees_IgnoreesSansPerdreLesExercicesValides`) |
| C5.7 | M5.12 | Horloge ou fuseau qui recule : séries, intervalles et rappels incohérents | corrigé | faible | ME-10 | `AZERTYGlobal/UsageStats.cs` (`RecordActivityLocked`, `CurrentStreak`), `TrainingReminders.cs` (`WouldCount`, `ShouldRemind`) ; tests `AZERTYGlobal.Tests/MachinesEtatsLot5Tests.cs` (`Statistiques_DerniereActiviteDansLeFutur_LeJourNEstPasRecompte`, `…_LaSerieEnCoursNEstPasRemiseAZero`, `…_LaSerieAfficheeNeTombePasAZero`, `DefiCommun_SeanceDatee_DuLendemain_NeSeRecomptePasAujourdhui`, `Rappel_DernierRappelDateDuLendemain_NeRepartPasAujourdhui`, `Rappel_DerniereSeanceDateeDuLendemain_VautAujourdhui`, suite : `Rappel_DatesLointainesDansLeFutur_LeLendemainLeRappelRepart`, `DefiCommun_SeanceDateeDe400Jours_LeLendemainLaSeanceCompteDeNouveau`, `Rappel_DatesDuJourOuPassees_RepairFutureDatesNeLesChangePas`, `FinDeSeance_RepareUneDateLointaineDansLeFutur` (fin de séance), `MaybeShowTrainingReminder_ReparePuisSeulementEnsuiteLaGardeDesNotifications` (rappel) ; `TrainingReminders.RepairFutureDates`) |
| C6.1 | M6.3 | Garde IsTutorialActive inopérante dans SettleSessionOnHide | corrigé | faible | ME-12 | `AZERTYGlobal/LessonsWindow.cs` (`Hide`, `HideWindow`, `SettleSessionOnHide`), `LessonsWindow.Tutorial.cs` (`FinishTutorial`, `LeaveTutorialForNow`) ; tests `AZERTYGlobal.Tests/MachinesEtatsLot6Tests.cs` (`Fermeture_PendantLeParcours_NeSoldePasLaSeanceNiNAvanceLaPosition`, `PasserPourLInstant_AuBilanDuParcours_NeSoldePasLaSeance`, garde-fou `Fermeture_HorsParcours_AuBilan_SoldeToujoursLaSeance`) |
| C6.2 | M6.2 | Réentrance de ResetProgress : position enregistrée sur le Défi | corrigé | faible | ME-07 | `AZERTYGlobal/LessonProgressStore.cs` (`SetLastPosition`) ; tests `AZERTYGlobal.Tests/MachinesEtatsLot6Tests.cs` (`ReinitialiserLaProgression_SurLeDefi_NEnregistrePasLeDefiCommePosition`, `ReinitialiserLaProgression_SurLeDefi_PuisFermer_RendLaPlaceDuCatalogue`) |
| C7.1 | M7.1 | Double clic sur « Suivant » à l'étape 2 : l'accueil se valide sans que l'étape 3 ait été vue | corrigé | moyenne | ME-03 | `AZERTYGlobal/OnboardingWindow.cs` (`IsSecondClickOfDoubleClick`, `GoToNextStep`) ; test `AZERTYGlobal.Tests/MachinesEtatsLot7Tests.cs` (`Accueil_DoubleClicSuivantÀLÉtape2_NeValidePasSansAvoirVuLÉtape3`) |
| C7.2 | M7.2 | « Ne plus afficher » toujours affichée décochée : « C'est parti ! » réécrit à true un refus déjà persisté | corrigé | faible | ME-03 | `AZERTYGlobal/OnboardingWindow.cs` (`Show`, `Close`, `_dontShowShownChecked`) ; tests `AZERTYGlobal.Tests/MachinesEtatsLot7Tests.cs` (`Accueil_RefusPersisté_NEstPasDéfaitParUneValidationSansToucherLaCase`, `Accueil_CaseNePlusAfficher_EstCochéeÀLOuvertureQuandLeRéglageEstRefusé`, `Accueil_CaseReprise_ÉchapNeValideRien_MaisUnChoixDécochéEstEnregistré`) |
| C7.3 | M7.4 | La demande d'avis peut partir pendant que l'accueil, rouvert depuis le menu, est ouvert | corrigé | faible | ME-08 | `AZERTYGlobal/TrayApplication.cs` (`MaybeShowReviewAfterQuietTyping`) ; test `AZERTYGlobal.Tests/MachinesEtatsLot7Tests.cs` (`Avis_AccueilRouvertDepuisLeMenu_DiffèreLaSollicitationAuTickDeQuiétude`) |
| C7.4 | M7.5 | La minuterie de quiétude de l'avis n'est jamais réarmée après coupure puis rétablissement des notifications | corrigé | faible | ME-08 | `AZERTYGlobal/TrayApplication.cs` (`EnsureReviewTimer`, `ConfigureSettingsWindow`), `SettingsWindow.cs` (`ApplyNotifications`, `NotificationsChanged`) ; tests `AZERTYGlobal.Tests/MachinesEtatsLot7Tests.cs` (`Avis_NotificationsRallumeesDansLesParametres_ReposentLaMinuterieDeQuietude`, `Parametres_CaseNotifications_PrevientLeTrayApresEcriture`, `Avis_TickPendantLaCoupure_ArreteLaMinuterie_PuisLeRallumageLaRepose`) |
| C7.5 | M7.7 | Aucun garde de double appel de la boîte d'avis du Store | plausible (preuve en réel seulement) | faible | ME-08 | lecture du code |
| C8.1 | M8.2 | Copie de secours sans aucun retour quand le presse-papiers est occupé | corrigé | faible | ME-11 | `AZERTYGlobal/CharacterSearch.cs`, `ClipboardText.cs`, `ChallengeSessionState.cs` ; test `AZERTYGlobal.Tests/MachinesEtatsLot8Tests.cs` (`Recherche_EchecDeLInsertionEtDeLaCopie_EstAnnoncé`) |
| C8.2 | M8.2 | Retour Copié périmé à la réouverture | corrigé | faible | ME-07 | `AZERTYGlobal/CharacterSearch.cs` (`Show`) ; test `AZERTYGlobal.Tests/MachinesEtatsLot8Tests.cs` (`Recherche_RetourCopié_NeSurvitPasÀLaRéouverture`) |

## Lot 1 — Moteur de frappe

Périmètre lu en entier : `CompositionEngine.cs`, `Layout.cs`, `LayoutJsonParser.cs`, `KeyMapper.cs`, `MaintainableLayerManager.cs`, `InputRecovery.cs`, `TextEmissionResult.cs`, `PhysicalTextInputBuffer.cs`, `LayerIndicatorWindow.cs` (tous chemins relatifs à `src/`). Lus pour le contexte d'appel : `TypingEngine.Windows/KeyboardHook.cs`, `TypingEngine.Windows/ForegroundMonitor.cs`, `TypingEngine.Windows/Win32Api/RealWin32Api.cs`, `TrayApplication.cs` (extraits), `LessonsWindow.cs` (extraits), `NativeWindow.cs` (extrait).

### Modèle de fils du lot

| Code | Fil d'exécution | Preuve |
|---|---|---|
| Rappel du hook `HookCallback` → `TrackModifiers`, `ProcessKey`, `RawKeyDown*` | Fil principal (celui qui a posé le hook et qui pompe les messages). Un hook WH_KEYBOARD_LL est livré au fil qui a appelé `SetWindowsHookEx`. | `TypingEngine.Windows/KeyboardHook.cs:151` `_hookId = Win32.SetWindowsHookEx(WH_KEYBOARD_LL, _proc, Win32.GetModuleHandleW(null), 0);` posé par `TrayApplication.ActivateWithConsent` (`TrayApplication.cs:1389` `_hook.Install();`). Aveu de l'auteur : `TypingEngine.Windows/KeyMapper.cs:1279-1280` « Thread affinity: UI thread uniquement (LL hook callback exécute sur le thread qui a appelé SetWindowsHookEx, soit notre main thread) ». |
| `ForegroundMonitor.OnWinEvent` → `Recompute` → `ForegroundChanged` → `TrayApplication.OnForegroundChanged` → `KeyMapper.RefreshForegroundContext` | Même fil principal (WinEvent hors contexte, livré au fil qui a appelé `SetWinEventHook`). | `TypingEngine.Windows/ForegroundMonitor.cs:4-6` (commentaire), `:197-203` `private void OnWinEvent(...) { Recompute(); }`, `TrayApplication.cs:2949-2952`. |
| Minuteries de `TrayApplication` (`SetTimer(_hWnd, id, ms, IntPtr.Zero)`) | Fil principal, via `WM_TIMER` dans la procédure de fenêtre. Aucune `System.Threading.Timer` n'agit sur le moteur. | `TrayApplication.cs:663-679` (tous les `Win32.SetTimer(_hWnd, ...)`). Les seules `System.Threading.Timer` / `ThreadPool` du dépôt hors tests : `ConfigManager.cs:932,1011,1330,1392,1400`, `UsageStats.cs:662`, `TrayApplication.cs:1358` (sonde session distante), `SecureInputProbe.cs:82` (fil « SecureInputDetector »). |
| `OnStateChanged` → `WM_APP_STATE` → `ApplyStateChange` → `LayerIndicatorWindow.Update` | `StateChanged` est levé dans le hook ; le travail UI est reporté par `PostMessageW` sur la pompe (même fil), coalescé par `Interlocked.Exchange(ref _stateRefreshQueued, ...)`. | `TrayApplication.cs:2191-2196`, `:2199-2216`, `:812-813`. |
| Fil du pool `ProbeRemoteHost` | Écrit `KeyboardHook.RemoteSessionActive` (booléen `volatile`) ; ne touche aucune variable du lot. | `TrayApplication.cs:1355-1380`, `TypingEngine.Windows/KeyboardHook.cs:54`. |
| Fil « SecureInputDetector » | Appelé depuis `Recompute` (fil principal) avec budget d'attente ; résultat tardif → `ResultChangedLate` → nouveau `Recompute` sur la pompe. Hors lot (lot ForegroundMonitor). | `TrayApplication.cs:428`. |
| `MessageBoxW` modales (`TrayApplication.cs:947`, `MaintainableLayersWindow.cs:370,418`, …) | Pompe imbriquée sur le fil principal : le hook y reste servi. Aucune de ces boîtes n'est ouverte depuis l'intérieur de `ProcessKey` ; la réentrance n'atteint donc pas un état à mi-transition du moteur. | recherche `MessageBoxW?\(` sur `src/` hors tests. |
| `SendInput` depuis le rappel du hook | Les événements injectés reviennent au hook après le retour du rappel courant ; ils sont filtrés en tête par le marqueur aléatoire et ne modifient aucun état du moteur. | `TypingEngine.Windows/KeyboardHook.cs:304-305` `if (hookStruct.dwExtraInfo == INJECTED_FLAG) return Win32.CallNextHookEx(...)`. |

Conséquences : tout l'état du lot (`KeyMapper`, `CompositionEngine`, `MaintainableLayerManager`, `InputRecovery`, `PhysicalTextInputBuffer`, `LayerIndicatorWindow`) vit sur un seul fil. Les verrous qui existent (`_passedThroughKeysLock`, `Interlocked.Exchange(ref _emitInputs, null)`) sont défensifs : le commentaire `KeyMapper.cs:61-62` (« ClearPassedThroughKeys peut être appelé depuis le thread WinEventHook ») est périmé, le rappel WinEvent est sur la même pompe. Les courses « fil du hook contre fil du pool » n'existent pas dans ce lot. Les seules fenêtres de course réelles sont (a) la lecture répétée de `GetForegroundWindow()` dans `IsEmissionSuspended` au cours d'un même appel (système asynchrone, voir « Notes non retenues »), (b) l'ordre entre rappel du hook et événement WinEvent (neutralisé par `GetEmitContext`, `ForegroundMonitor.cs:112-118`).

Aucune minuterie propre au lot : l'expiration du double appui (`MaintainableLayerManager.cs:192-195`), de la file de texte attendu (`PhysicalTextInputBuffer.cs:151-155`) et de la réparation d'émission est évaluée paresseusement à l'événement suivant. Aucun écrit disque dans le lot.

### M1.1 — Composition par touche morte (`CompositionEngine`)

**Nature** : implicite (un champ nullable code l'état ; la classe se dit « Pure state machine » mais n'a pas d'enum). **Fichiers** : `TypingEngine.Core/CompositionEngine.cs`, `TypingEngine.Core/Layout.cs` (`DeadKeyDefinition.Apply`, `GetIsolated`), `TypingEngine.Core/LayoutJsonParser.cs` (construction des tables), appelant `TypingEngine.Windows/KeyMapper.cs:850-869`.

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `ActiveDeadKey` | `string?` | `TypingEngine.Core/CompositionEngine.cs:11` | fil principal (hook : `Process`, `Cancel` ; pompe : `SyncState`, `ApplyForegroundApplication`) | aucune, fil unique |
| `_layout` | `Layout` (immuable après `Parse`) | `CompositionEngine.cs:6` | lecture seule | immuabilité de fait : `Layout.Keys`/`DeadKeys` ne sont écrits que par `LayoutJsonParser.Parse` (`LayoutJsonParser.cs:36,70`) |

`Layout.cs` et `LayoutJsonParser.cs` ne contiennent aucune machine à états : structures de données remplies une fois par `Parse`, puis lues. Une erreur de format lève `LayoutFormatException` (`LayoutJsonParser.cs:89,118,126`), rien n'est à moitié construit hors de la fonction.

#### États

| état | prédicat |
|---|---|
| Repos | `ActiveDeadKey is null` |
| Armée(n) | `ActiveDeadKey == n` (n commence par `dk_`) ; n peut être absent de `Layout.DeadKeys` |

#### Invariants

- I1 : une seule touche morte en attente. Garanti par `CompositionEngine.cs:37-41` et `:43-50` (un champ unique).
- I2 : un caractère non mort consomme toujours l'état armé (jamais deux caractères sous une même armée). Garanti par `CompositionEngine.cs:27` `ActiveDeadKey = null;` avant toute branche.
- I3 : l'état armé est abandonné avant d'être appliqué à une autre application, ou après Retour arrière, Entrée, Tab, Échap, raccourci. Garanti par `KeyMapper.cs:626, 638, 655, 1109, 1145-1146, 220`. Non garanti pour : flèches, clic souris, Début/Fin (touches étendues, `KeyMapper.cs:679-680`, qui laissent l'armée en place, comme Windows natif).
- I4 : l'état armé n'est jamais détruit pour une émission qui n'aura pas lieu. Garanti pour la voie composition (`KeyMapper.cs:852-861`). Non garanti pour la voie couche (voir M1.2, notes non retenues).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1.1.1 | Repos | `Process("dk_x")` | — | Armée(dk_x) | texte émis `""`, `StateChanged=true` | `CompositionEngine.cs:23,37-41` |
| T1.1.2 | Armée(a) | `Process("dk_b")` | `b.Apply(isolé(a)) != null` | Repos | émet le composé (touches mortes chaînées) | `CompositionEngine.cs:43-47` |
| T1.1.3 | Armée(a) | `Process("dk_b")` | pas de composé (y compris double frappe de la même touche si la table ne mappe pas l'isolé) | Armée(b) | émet `isolé(a)` (ou `""` si a inconnue) | `CompositionEngine.cs:49-50` |
| T1.1.4 | Armée(a) | `Process(c)` | a connue, `a.Apply(c) != null` | Repos | émet le composé | `CompositionEngine.cs:28,31` |
| T1.1.5 | Armée(a) | `Process(c)` | a connue, pas de composé | Repos | émet `isolé(a) + c` | `CompositionEngine.cs:31` |
| T1.1.6 | Armée(a) | `Process(c)` | a absente de `DeadKeys` | Repos | émet `c`, la morte disparaît sans trace | `CompositionEngine.cs:28-29` |
| T1.1.7 | Repos | `Process(c)` | — | Repos | émet `c`, `StateChanged=false` | `CompositionEngine.cs:24` |
| T1.1.8 | Armée | `Cancel()` | — | Repos | `StateChanged` levé par l'appelant | `CompositionEngine.cs:13-18` |
| T1.1.9 | Armée | Retour arrière (keydown) | — | Repos | Retour arrière transmis à l'application | `KeyMapper.cs:624-632` |
| T1.1.10 | Armée | Entrée / Tab (keydown) | — | Repos | touche transmise | `KeyMapper.cs:638-642` |
| T1.1.11 | Armée | Échap ou Ctrl/Alt/Win + touche non modificatrice | `isKeyDown && !IsModifierVk` | Repos | touche suit son chemin | `KeyMapper.cs:651-659` |
| T1.1.12 | Armée | changement d'application (`LastApplicationIdentity`) | identités non nulles et différentes | Repos | — | `KeyMapper.cs:1135-1149` |
| T1.1.13 | Armée | suspension imposée par le premier plan (mode anti-triche) pendant une touche | `IsSuspendedByForeground` | Repos | — | `KeyMapper.cs:1106-1110` |
| T1.1.14 | Armée | `SyncState(preservePendingDeadKey=false)` | reprise après pause/réactivation | Repos | — | `KeyMapper.cs:220` |
| T1.1.15 | Armée | pause volontaire (`EmissionPaused`) | — | Armée (conservée) | décision d'Antoine du 20/09 | `KeyMapper.cs:1098-1100, 1108` |

#### Temporisations et réessais

Aucune temporisation : une touche morte armée n'expire pas (Windows natif non plus). Aucun réessai : une émission refusée par `SendInput` ne réarme pas la touche (`KeyMapper.cs:863-865` ignore le `TextEmissionResult`) ; limite connue AG130-09, reportée 1.3.1 (`KeyMapper.cs:1200-1207`).

Note : aucune transition invalide trouvée dans `CompositionEngine` lui-même ; l'état produit `Armée(x)` avec une couche d'un coup simultanément armée est traité en M1.2 (C1.1, corrigé : la touche morte annule le coup).

#### États ou transitions invalides atteignables

C1.1.

### M1.2 — Couches maintenables : déclencheur, un coup, verrou (`MaintainableLayerManager`)

**Nature** : explicite pour l'état effectif (`enum MaintainableLayerMode { Inactive, OneShot, Locked }`, `MaintainableLayerManager.cs:3-8`) ; implicite pour le déclencheur (`_pressedLayer != null`) et l'historique de double appui (`_lastTapLayer != null`). **Fichiers** : `TypingEngine.Windows/MaintainableLayerManager.cs`, appelant `TypingEngine.Windows/KeyMapper.cs:559-562, 619-621, 626-627, 655-656, 672-676, 783-792, 813-847`, consommateurs `LayerIndicatorWindow.cs`, `PhysicalTextInputBuffer.cs:99-100`.

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `_enabled`, `_enabledLayers`, `_doubleTapMilliseconds` | bool, `HashSet<string>`, int | `MaintainableLayerManager.cs:45-50` | écrits par `ApplySettings` (pompe : réglages, `TrayApplication.cs:2240`), lus par le hook | fil unique |
| `_foreground`, `_secureInput` | `ForegroundProcessIdentity`, bool | `:47-48` | écrits par `SetForeground` (pompe, `KeyMapper.cs:149`), lus par le hook | fil unique |
| `_pressedLayer`, `_pressedScanCode`, `_pressedOwner`, `_pressedUsedAsChord` | string?, uint, identité, bool | `:52-55` | hook | fil unique |
| `_oneShotLayer`, `_oneShotOwner` | string?, identité | `:57-58` | hook, pompe (reset) | fil unique |
| `_lockedLayers` | `Dictionary<identité,string>` | `:44` | hook, pompe (`RemoveDeadProcessLocks`) | fil unique |
| `_lastTapLayer`, `_lastTapOwner`, `_lastTapAt` | string?, identité, long | `:60-62` | hook | fil unique ; horloge `Environment.TickCount64` (`:69`), insensible aux changements d'heure |
| `_consumeEscapeKeyUp` | bool | `:63` | hook | fil unique |
| `_suppressShiftForLayer`, `_suppressAltGrForLayer` (dans `KeyMapper`) | bool | `KeyMapper.cs:53-54` | hook | fil unique |

#### États

| état | prédicat |
|---|---|
| Inactive | `GetEffectiveState` rend `Inactive` : `!_enabled \|\| _secureInput \|\| !identity.IsValid \|\| identity != _foreground`, ou ni un coup valide ni verrou valide (`MaintainableLayerManager.cs:254-266`) |
| OneShot(L) | `_oneShotLayer == L && _oneShotOwner == _foreground && IsLayerEnabled(L)` (priorité sur le verrou, `:259-260`) |
| Locked(L) | pas de un coup valide et `_lockedLayers[_foreground] == L && IsLayerEnabled(L)` (`:262-263`) |
| Déclencheur au repos | `_pressedLayer == null` |
| Déclencheur enfoncé (appui simple) | `_pressedLayer != null && !_pressedUsedAsChord` |
| Déclencheur enfoncé (accord) | `_pressedLayer != null && _pressedUsedAsChord` (un coup armé en même temps) |
| Armé pour double appui | `_lastTapLayer != null` (valable `_doubleTapMilliseconds`, 150 à 1000 ms) |

#### Invariants

- I1 : un seul verrou par processus. Garanti par `_lockedLayers[owner] = layer` (`MaintainableLayerManager.cs:200`, dictionnaire).
- I2 : un coup et un verrou ne s'appliquent jamais à un autre processus que celui où ils ont été armés. Garanti par `owner == _foreground` (`:181, 219, 259`) et `ResetTransient` au changement de processus (`:132-136`). L'identité contient l'heure de création du processus contre la réutilisation de PID (`:13`, test `ReusedPid_DoesNotInheritLock`).
- I3 : un coup non consommé disparaît au relâchement d'un déclencheur promu en accord. Garanti par `:215-226`.
- I4 : en saisie sécurisée, aucune couche n'est active et rien ne reste armé. Garanti par `:132-136` et `:256`.
- I5 : un coup armé disparaît quand l'utilisateur fait autre chose que taper un caractère de couche (Retour arrière, Entrée, Tab, raccourci, Échap, touche morte armant une composition, toute touche sans caractère, touche du pavé numérique comprise). Garanti par `CancelOneShot` dans `KeyMapper.ProcessKeyCore` (Retour arrière, Entrée et Tab, raccourci et Échap), dans `CancelOneShotOnKeyOutsideLayout` (toute autre touche sans caractère) et dans la voie composition (touche morte). Rétabli par ME-12 pour Entrée et Tab (C1.5, décision d'Antoine du 16 novembre 2026 : « Annuler l'attente »), puis pour toute touche sans caractère (même constat, même jour : « Toutes annulent ») et pour les touches du pavé numérique qui écrivent un caractère (« Le chiffre annule »), sauf Maj, Ctrl, Alt, AltGr, Verr. maj et Verr. num : voir la table « Touches sans caractère » ci-dessous.
- I6 : un coup et une composition de touche morte ne coexistent jamais. Rétabli par ME-12 (C1.1, décision d'Antoine du 16 novembre 2026 : « L'accent annule le grec ») : dans la voie composition de `KeyMapper.ProcessKeyCore`, une touche morte qui arme une composition appelle `CancelOneShot`. Seul l'accent compte, la lettre suivante est une lettre française accentuée (`e` donne é, puis la frappe d'après est ordinaire) ; une seule attente à la fois. La voie couche reste gardée par `_composition.ActiveDeadKey == null` et `!output.StartsWith("dk_")` : elle ne voit plus de composition sous un coup, puisque le coup n'existe plus. Le verrou n'est pas touché (une touche morte sous une couche verrouillée arme la composition, le verrou reste).
- I7 : le modificateur d'activation reste neutralisé tant qu'il est enfoncé, pour toutes les voies de sortie. Garanti depuis ME-05 : la voie « laisser passer la touche native » reçoit les modificateurs effectifs (`CanPassThrough(…, effectiveShift, effectiveAltGr)`) et refuse dès que l'un d'eux diffère du modificateur physiquement tenu, puisque le système lirait le physique (C1.4 corrigé).
- I8 : la relâche du déclencheur est toujours vue du gestionnaire. Garanti hors suspension (`KeyMapper.cs:559-562`) ; si une suspension tombe pendant l'appui, `_pressedLayer` reste tant que `SyncState` (`KeyMapper.cs:235`) ou un changement de processus (`:132`) ne l'efface.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1.2.1 | déclencheur au repos | `BeginTrigger(L, sc)` | `!_secureInput`, `_foreground.IsValid`, `IsLayerEnabled(L)` | enfoncé (appui simple) | un coup armé précédent effacé ; le verrou n'est pas touché ; `NotifyIfChanged` | `MaintainableLayerManager.cs:143-164` |
| T1.2.2 | enfoncé | `BeginTrigger(L, sc)` même scancode | répétition automatique, modificateur d'activation toujours tenu ; si le modificateur est déjà relâché, `ProcessKey` rend la décision de l'appui initial (consommée) sans rejouer `ProcessKeyCore` (C1.6 corrigé) | inchangé, rend true | — | `:149-150`, `KeyMapper.cs` (`ProcessKey`, `PendingTriggerScanCode`) |
| T1.2.3 | enfoncé | `BeginTrigger(L2, sc2)` scancode différent | — | inchangé, rend false : la touche est traitée par l'appelant comme une touche morte ordinaire (composition armée sur `dk_L2`). Avant cela, la frappe de `sc2` a déjà promu le premier déclencheur en accord (T1.2.4 : un coup armé, repris au relâchement du premier, T1.2.8) ; la touche morte armant une composition, ce coup est annulé (T1.2.20, C1.1 corrigé) | — | `:149-150`, `KeyMapper.cs` (voie composition) |
| T1.2.4 | enfoncé (appui simple) | autre touche enfoncée | `_pressedOwner == _foreground`, pas secure | enfoncé (accord) + OneShot(L) | `ClearTapHistory` | `:238-252`, `KeyMapper.cs:672-676` |
| T1.2.5 | enfoncé (appui simple) | `EndTrigger(sc)` | `Locked(L)` sur ce processus | déclencheur au repos, Inactive | verrou retiré | `:181-188` |
| T1.2.6 | enfoncé (appui simple) | `EndTrigger(sc)` | double appui : même couche, même propriétaire, `0 <= now-_lastTapAt <= délai` | au repos, Locked(L) | un coup effacé, historique effacé | `:192-204` |
| T1.2.7 | enfoncé (appui simple) | `EndTrigger(sc)` | sinon | au repos, OneShot(L) | `_lastTap*` renseigné | `:205-212` |
| T1.2.8 | enfoncé (accord) | `EndTrigger(sc)` | un coup de cet accord non consommé | au repos, Inactive (ou Locked inchangé) | historique effacé | `:215-226` |
| T1.2.9 | OneShot | `ConsumeOneShot()` | `_oneShotOwner == _foreground` | Inactive ou Locked précédent | historique effacé | `:268-278`, appelé `KeyMapper.cs:844-845` |
| T1.2.10 | OneShot | `CancelOneShot()` (Retour arrière, Entrée, Tab, raccourci, Échap, toute touche sans caractère : T1.2.22) | — | Inactive/Locked | Entrée et Tab : ajoutées par ME-12 (C1.5) ; la touche passe à l'application | `:280-291`, `KeyMapper.cs` (blocs Retour arrière, Entrée et Tab, raccourci) |
| T1.2.11 | Locked | `HandleEscape(keyDown)` | verrou du processus, pas secure | Inactive ; `_consumeEscapeKeyUp = true` | keydown absorbé | `:293-312`, `KeyMapper.cs:620-621` |
| T1.2.12 | tout | `HandleEscape(keyUp)` | — | `_consumeEscapeKeyUp = false` | keyup absorbé si le drapeau l'était | `:295-299` |
| T1.2.13 | tout | `SetForeground(autre processus)` | `identity != _foreground` | transitoires et historique effacés ; verrous gardés | notification | `:126-141` |
| T1.2.14 | tout | `SetForeground(secure=true)` | `securityChanged && secureInput` | idem | — | `:132-136` |
| T1.2.15 | tout | `ApplySettings(enabled=false)` | — | tout effacé, verrous vidés | — | `:93-98` |
| T1.2.16 | tout | `ApplySettings` retirant une couche | couche armée/verrouillée retirée | état de cette couche effacé | — | `:99-118` |
| T1.2.17 | tout | `ResetTransientState()` (`SyncState`) | — | transitoires et historique effacés ; verrous gardés | — | `:314-320`, `KeyMapper.cs:235` |
| T1.2.18 | Locked(processus p) | `RemoveDeadProcessLocks` | `!isAlive(p)` | verrou retiré | appelé à chaque `RefreshForegroundContext` | `:322-331`, `KeyMapper.cs:153-155` |
| T1.2.19 | OneShot | touche morte ordinaire armant une composition (accent aigu SC028…) | `output` commence par `dk_`, la composition s'arme | Inactive/Locked, composition Armée(dk_x) | ME-12 (C1.1) : une seule attente à la fois, `StateChanged` (l'indicateur de couche disparaît, la touche morte à visualiser est l'accent) | `KeyMapper.cs` (voie composition) |
| T1.2.20 | OneShot (accord tenu) | touche morte d'un autre scancode que le déclencheur tenu | `BeginTrigger` rend false | idem T1.2.19 | voir T1.2.3 | `KeyMapper.cs` (voie composition) |
| T1.2.21 | OneShot (accord déjà promu, déclencheur encore tenu) | Entrée, Tab ou Retour arrière (keydown) | `CancelOneShot()` rend true (ou une composition est annulée) | Inactive/Locked ; l'accord reste « utilisé » | `ProcessKeyCore` sort par `return false` avant l'étape de promotion du déclencheur en attente : sans effet, car `PromotePendingTriggerToOneShot` ne fait rien sur un accord déjà promu ; les trois touches se comportent de la même façon ; un coup et un déclencheur en attente non promu ne coexistent jamais (`BeginTrigger` efface le coup, T1.2.1) ; tests `UnCoupDeCouche_AccordDejaPromuPuisEntreeTabOuRetourArriere_LeCoupEstAnnuleEtRienNeReste` et `UnCoupDeCouche_SecondDeclencheurTenuPuisEntreeTabOuRetourArriere_LAccordSePromeutEtLaLettreSortEnGrec` | `KeyMapper.cs` (blocs Retour arrière, Entrée et Tab, promotion) |
| T1.2.22 | OneShot | touche hors disposition, premier appui : flèches, Début, Fin, Page préc., Page suiv., Inser, Suppr, Applications, Impr. écran, F1 à F24, Arrêt défil, Pause, Win seule, volume et multimédia, pavé numérique (Verr. num éteint : Début, Fin, flèches ; Verr. num allumé : chiffres, + - * /, séparateur décimal, VK 0x60 à 0x6F) | `CancelsOneShot` : touche étendue ou absente de la disposition, hors Maj, Ctrl, Alt, AltGr, Verr. maj et Verr. num ; pas de répétition automatique (`_keyDownOwnership`) | Inactive ; le verrou et la composition d'une touche morte seule ne sont pas touchés | ME-12 (C1.5, décisions du 16 novembre 2026 : « Toutes annulent », puis « Le chiffre annule » pour le pavé numérique) : `CancelOneShotOnKeyOutsideLayout` ; la touche passe à l'application (jamais absorbée ni transformée : un chiffre du pavé numérique sort normalement), `StateChanged` levé, la lettre suivante sort en français ; Win seule passe par le `case` des modificateurs de `ProcessKeyCore` ; l'annulation précède la promotion du déclencheur en attente (T1.2.4), qui garde son résultat ; tests `UnCoupDeCouche_EstAnnuleParToutesLesTouchesSansCaractere`, `UnCoupDeCouche_AnnuleParUneToucheSansCaractere_LIndicateurSuitLAnnulation`, `UneCoucheVerrouillee_NEstPasDefaiteParUneToucheSansCaractere`, `UneToucheMorteSeule_N_EstPasAnnuleeParUneToucheSansCaractere`, `UneToucheSansCaractereQuiSeRepete_NAnnuleQueAuPremierAppui`, `UnCoupDeCouche_UneToucheDuPaveNumeriqueQuiEcritUnCaractere_SortNormalementEtAnnuleLAttente`, `UnCoupDeCouche_AnnuleParUneToucheDuPaveNumerique_LIndicateurSuitLAnnulation`, `UneCoucheVerrouillee_NEstPasDefaiteParUneToucheDuPaveNumerique`, `UneToucheMorteSeule_N_EstPasAnnuleeParUnChiffreDuPaveNumerique` | `KeyMapper.cs` (`CancelsOneShot`, `CancelOneShotOnKeyOutsideLayout`, `case` Win, bloc après Entrée et Tab) |

#### Temporisations et réessais

- Double appui : 150 à 1000 ms (`:91`), comparaison d'horodatages à l'événement suivant, jamais de minuterie. Le décompte repart de l'appui simple précédent ; il est annulé par `ClearTapHistory` (consommation, accord, annulation, changement de processus).
- Un coup : aucune expiration. Il vit jusqu'à une frappe de caractère de couche, Retour arrière, Entrée, Tab, Échap, raccourci, touche morte armant une composition, toute touche sans caractère, changement de processus, entrée en saisie sécurisée, `SyncState` (C1.5 corrigé pour Entrée, Tab et toute touche sans caractère).
- Pas de réessai.

Note non retenue : `_consumeEscapeKeyUp` périmé (voir plus bas).

#### États ou transitions invalides atteignables

C1.1 corrigé (ticket ME-12) : invariant I6 rétabli, tests `MachinesEtatsLot1Tests.UnCoupDeCouche_EtUneCompositionDeToucheMorte_NeCoexistentJamais` et `UnCoupDeCouche_PuisAccent_LaLettreSuivanteEstFrancaiseAccentuee`.

C1.5 corrigé (ticket ME-12) : invariant I5 rétabli pour Entrée et Tab, puis pour toute touche sans caractère (T1.2.22, voir la table ci-dessous), tests `MachinesEtatsLot1Tests.UnCoupDeCouche_EstAnnuleParEntreeEtTab` et `UnCoupDeCouche_AnnuleParEntreeOuTab_LIndicateurSuitLAnnulation`. La sortie de `ProcessKeyCore` avant la promotion d'un déclencheur en attente, quand Entrée ou Tab annule un coup, est sans effet et identique à celle de Retour arrière (T1.2.21), tests `UnCoupDeCouche_SecondDeclencheurTenuPuisEntreeTabOuRetourArriere_LAccordSePromeutEtLaLettreSortEnGrec` et `UnCoupDeCouche_AccordDejaPromuPuisEntreeTabOuRetourArriere_LeCoupEstAnnuleEtRienNeReste`.

Ordre inverse (accent puis touche spéciale), comportement actuel, non modifié : après un accent, la garde `_composition.ActiveDeadKey == null` empêche le déclencheur de couche de s'armer ; la touche est traitée comme une touche morte `dk_greek` dans la composition en cours. L'accent est émis isolé (`´`), la composition passe à `dk_greek` (une seule attente à la fois, aucun coup armé) et la lettre suivante est grecque (`e` donne ε), puis la frappe d'après est ordinaire. Il n'y a donc pas de coexistence ; le grec l'emporte ici sur l'accent, à l'inverse de l'ordre direct (mesuré au banc, non vérifié au clavier).

#### Touches sans caractère : ce que chacune fait aujourd'hui d'un coup armé

Mesuré au banc (`KeyMapper.ProcessKey`, coup grec armé par un appui bref de Maj+SC02B, couche non verrouillée). Décision d'Antoine du 16 novembre 2026 : Entrée et Tab, puis les touches mortes, puis « Toutes annulent » pour toutes les autres touches sans caractère ; « avant » désigne l'état mesuré avant cette dernière décision.

| touche | effet sur le coup | touche absorbée | décision |
|---|---|---|---|
| Entrée, Entrée du pavé, Tab | annule | non (passe) | prise (C1.5) |
| touche morte (accent aigu, tréma…) | annule, la composition s'arme | oui | prise (C1.1) |
| Retour arrière | annule | non | antérieure (audit 10/11) |
| Échap | annule (couche non verrouillée) | non | antérieure (audit 10/11) |
| Ctrl, Alt, Win + touche (raccourci) | annule | selon la touche | antérieure (RA3) |
| Maj, Ctrl, Alt, AltGr, Verr. maj, Verr. num seuls | ne touche pas : le coup reste armé | non | borne : jamais d'annulation (majuscule de l'autre alphabet) ; test `UnCoupDeCouche_UnModificateurSeul_NAnnuleJamaisLAttente` (Verr. num ajouté) |
| Espace | consomme le coup (espace émis par la voie couche) | oui | existant |
| flèches, Début, Fin, Page préc., Page suiv., Inser, Suppr, Applications, Impr. écran, Début du pavé numérique (Verr. num éteint) | annule (avant : le coup restait armé) | non (passe) | prise (C1.5, « Toutes annulent ») ; test `UnCoupDeCouche_EstAnnuleParToutesLesTouchesSansCaractere` |
| F1 à F24, Arrêt défil, Pause | annule (avant : le coup restait armé, mesuré) | non (passe) | prise (idem) |
| Win seule, volume, multimédia, navigateur, messagerie | annule (avant : le coup restait armé, mesuré) | non (passe) | prise (idem) |
| chiffres et opérateurs du pavé numérique (Verr. num allumé, VK 0x60 à 0x6F) | annule (avant : le coup restait armé) ; le caractère sort normalement | non (passe, non transformée) | prise (C1.5, « Le chiffre annule ») ; test `UnCoupDeCouche_UneToucheDuPaveNumeriqueQuiEcritUnCaractere_SortNormalementEtAnnuleLAttente` |

Entrée frappée pendant que le déclencheur est encore tenu : le coup n'est pas annulé (l'accord le promeut juste après), mais il disparaît au relâchement du déclencheur (T1.2.8) : mesuré au banc, aucune lettre grecque ne reste armée. Le même geste après un appui qui remplace le coup (le déclencheur tenu de nouveau efface le coup, T1.2.1) promeut l'accord et la lettre sort en grec ; accord déjà promu par une touche étendue (flèche), puis Entrée, Tab ou Retour arrière : le coup est annulé, la lettre sort en français, rien ne reste armé au relâchement. Les trois touches donnent le même résultat (tests de T1.2.21).

Règle (ME-12, C1.5, décision d'Antoine du 16 novembre 2026 : « Toutes annulent ») : après un coup armé, toute touche qui n'écrit pas de caractère l'annule ; seules Maj, Ctrl, Alt, AltGr, Verr. maj et Verr. num font exception. Le moteur sait déjà dire qu'une touche n'écrit rien (touche étendue, ou absente de la disposition `AZERTY Global 2026.json`, qui ne contient que les touches alphanumériques, la touche `<>` et Espace) : `CancelsOneShot` s'appuie là-dessus, sans liste de codes, avec une seule borne : les six exceptions. Les touches du pavé numérique qui écrivent un caractère (VK 0x60 à 0x6F, Verr. num allumé) annulent aussi (décision du 16 novembre 2026 : « Le chiffre annule ») : le caractère sort normalement, la lettre suivante sort en français, seule la toute prochaine touche peut sortir dans l'autre alphabet. La touche n'est jamais absorbée ; le verrou de couche et l'attente d'une touche morte seule gardent leur comportement (tests `UneCoucheVerrouillee_NEstPasDefaiteParUneToucheSansCaractere` et `UneToucheMorteSeule_N_EstPasAnnuleeParUneToucheSansCaractere`). Seul le premier appui annule : l'auto-répétition d'une touche tenue ne défait pas le coup qu'un accord vient de promouvoir (test `UneToucheSansCaractereQuiSeRepete_NAnnuleQueAuPremierAppui`). Win seule passe par le `case` des modificateurs de `ProcessKeyCore`, qui sortait avant tout. Où l'annulation peut surprendre : Win (appuyer sur Windows pour ouvrir le menu Démarrer défait le coup), Impr. écran, Applications et les touches multimédia, frappées sans lien avec le texte ; une touche de fonction spéciale d'un portable qui se déclare sans caractère annule aussi ; Verr. num éteint, les touches du pavé numérique deviennent Début, Fin et flèches et annulent. Tests : `UnCoupDeCouche_EstAnnuleParToutesLesTouchesSansCaractere` (41 touches : non absorbée, couche inactive, lettre suivante en français) et `UnCoupDeCouche_AnnuleParUneToucheSansCaractere_LIndicateurSuitLAnnulation` ; 44 cas rouges avant le correctif, tous verts après ; les gardes (Verr. num, verrou, accent seul, répétition) sont verts d'emblée. Pavé numérique : 19 cas rouges avant (16 touches, 3 pour l'indicateur), tous verts après, `UneCoucheVerrouillee_NEstPasDefaiteParUneToucheDuPaveNumerique` vert d'emblée. Mesuré : un chiffre du pavé numérique sur un accent en attente (sans coup de couche) sort normalement et laisse l'accent armé pour la lettre suivante (é), comme les autres touches hors disposition ; test `UneToucheMorteSeule_N_EstPasAnnuleeParUnChiffreDuPaveNumerique`.

C1.4 corrigé (ticket ME-05) : invariant I7 rétabli, test `MachinesEtatsLot1Tests.MajDActivationNeutralise_LaSecondeLettreNeFuitPasEnMajusculeNative`.

### M1.3 — Modificateurs suivis et Verr. Maj. (`KeyMapper`)

**Nature** : implicite (combinaison de booléens). **Fichiers** : `TypingEngine.Windows/KeyMapper.cs`.

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `_leftShiftDown`, `_rightShiftDown`, `_leftCtrlDown`, `_rightCtrlDown`, `_leftAltDown`, `_rightAltDown` | bool | `KeyMapper.cs:44-49` | écrits par `TrackModifiers` (hook) et `CleanupStaleModifiers` (hook, pompe via `SyncState`) ; lus par le hook, les fenêtres via `ShiftDown`, `AltGrDown`, `CtrlDown`, `AltDown` | fil unique |
| `_leftWinDown`, `_rightWinDown` | bool | `:57-58` | idem | fil unique |
| `_capsLockState` | bool | `:41` | hook (bascule sur chaque keydown VK_CAPITAL, resynchronisé sur chaque autre touche), pompe (`SyncState`, `RequestCapsLockOff`) | fil unique |
| `_suppressShiftForLayer`, `_suppressAltGrForLayer` | bool | `:53-54` | hook (`:789-790` armés, `:394-397` levés au relâchement complet), pompe (`:233-234`) | fil unique |

Dérivés : `IsShiftDown` (`:346`), `IsCtrlDown` (`:350`), `IsAltGrDown = _rightAltDown || (_rightCtrlDown && _leftAltDown)` (`:349`). Ctrl gauche + Alt gauche n'est donc pas AltGr dans ce code ; Alt gauche sans Ctrl droit est un raccourci système (`:684-685`).

#### États

| état | prédicat |
|---|---|
| Aucun modificateur | tous les booléens faux |
| Maj | `IsShiftDown && !IsCtrlDown && !_leftAltDown && !IsAltGrDown` |
| AltGr réel | `_rightAltDown` (+ Ctrl gauche fantôme `_leftCtrlDown`, ignoré par `IsCtrlDown && !IsAltGrDown`, `:701`) |
| AltGr simulé | `_rightCtrlDown && _leftAltDown` |
| Raccourci Ctrl | `IsCtrlDown && !IsAltGrDown` |
| Raccourci Alt | `_leftAltDown && !_rightCtrlDown` |
| Raccourci Win | `_leftWinDown \|\| _rightWinDown` |
| Basculement Ctrl+Maj | `IsToggleShortcut()` (`:407-408`) |
| Verr. Maj. logique actif | `_capsLockState` |

#### Invariants

- I1 : le suivi reflète les touches physiques et ignore les injections de l'application et des tiers. Garanti par `KeyboardHook.cs:304-315` (marqueur, `ShouldProcessKeystroke`).
- I2 : un modificateur dont le keyup a été manqué est resynchronisé avant toute frappe non modificatrice. Garanti par `KeyMapper.cs:596` / `:475-501`, pas dans `ProcessKey` suspendu ni pendant une réparation en attente (`:232`).
- I3 : `_capsLockState` égale l'état système avant de choisir un caractère. Garanti par `:585-590` (hors VK_CAPITAL).
- I4 : après une injection, le suivi garde l'état physique. Garanti par `InputRecovery` (restaure les modificateurs physiquement enfoncés, M1.6) et par le fait que les événements injectés ne sont pas suivis.
- I5 : aucun état « Ctrl+Maj » périmé ne déclenche la bascule du remappage. **Non garanti** pour une touche Verr. Maj. tapée juste après une session verrouillée avec Ctrl et Maj enfoncées : `IsToggleShortcut()` est évalué dans le hook avant `CleanupStaleModifiers` (`KeyboardHook.cs:336-359`). Non retenu comme constat (voir notes).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1.3.1 | tout | `TrackModifiers(vk, down)` | vk ∈ Maj/Ctrl/Alt/Win | booléen correspondant := down | `StateChanged` si Maj/AltGr/Ctrl/Alt change | `KeyMapper.cs:356-403` |
| T1.3.2 | `_suppress*` armé | relâchement complet de Maj (ou AltGr) | `!IsShiftDown` / `!IsAltGrDown` | `_suppress*` := false | — | `:394-397` |
| T1.3.3 | `_suppress*` faux | frappe d'un déclencheur de couche accepté | `BeginTrigger` true | `_suppressShift \|= IsShiftDown`, `_suppressAltGr \|= IsAltGrDown` | — | `:783-792` |
| T1.3.4 | tout | keydown d'une touche non modificatrice | — | booléen := faux si `GetAsyncKeyState` bit 15 absent | `StateChanged` | `:596`, `:475-501` |
| T1.3.5 | `_capsLockState` s | keydown VK_CAPITAL (répétitions comprises) | `!IsEmissionSuspended`, `FlushInputRecovery` | `!s`, touche transmise | `StateChanged` | `:609-617` |
| T1.3.6 | `_capsLockState != système` | keydown autre touche | — | := état système | `StateChanged` | `:585-590` |
| T1.3.7 | tout | `RequestCapsLockOff` | état système ou interne actif | `_capsLockState := false` seulement si l'injection est complète | injection VK_CAPITAL | `:169-205` |
| T1.3.8 | tout | `SyncState` | reprise, activation | Verr. Maj. := système ; `_suppress* := false` ; modificateurs resynchronisés sauf réparation en attente | — | `:212-249` |

#### Temporisations et réessais

Aucune. Resynchronisation opportuniste à chaque frappe. Pas de limite de réessai.

Répétition automatique de Verr. Maj. : `KeyMapper.cs:611-614` bascule sur chaque keydown y compris répété ; l'écart est resynchronisé à la frappe suivante (`:585-590`), donc pas de caractère faux, seulement un affichage transitoire. Non retenu comme constat.

#### États ou transitions invalides atteignables

C1.4 corrigé (ticket ME-05) : invariant M1.2 I7 rétabli, test `MachinesEtatsLot1Tests.MajDActivationNeutralise_LaSecondeLettreNeFuitPasEnMajusculeNative`.

### M1.4 — Propriété des appuis physiques et touches synthétiques (`KeyMapper`)

**Nature** : implicite (trois collections). **Fichiers** : `TypingEngine.Windows/KeyMapper.cs`.

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `_keyDownOwnership` | `Dictionary<(scan, extended), bool>` (vrai = consommé par AZERTY Global) | `KeyMapper.cs:507` | hook, `TrackPassThroughKey` | aucune, fil unique |
| `_passedThroughKeys` | `HashSet<uint>` (scancodes laissés passer) | `:63` | hook, pompe (`SyncState`, `ClearPassedThroughKeys`) | `_passedThroughKeysLock` (défensif) |
| `_syntheticVirtualKeyDowns` | `Dictionary<uint,ushort>` (keydown VK injecté pour Ctrl+lettre) | `:64` | idem | `_passedThroughKeysLock` |

#### États (par touche physique)

| état | prédicat |
|---|---|
| Libre | clé absente de `_keyDownOwnership` |
| Consommée | `_keyDownOwnership[key] == true` |
| Transmise | `_keyDownOwnership[key] == false` |
| Passage natif suivi | `_passedThroughKeys` contient le scancode |
| Keydown synthétique tenu | `_syntheticVirtualKeyDowns` contient le scancode |
| Déclencheur de couche enfoncé | `_pressedLayer != null && _pressedScanCode == scancode` (M1.2) |

#### Invariants

- I1 : chaque répétition garde la décision de l'appui initial. Garanti par `KeyMapper.ProcessKey` (`if (hadDown) handled = consumed;`), à une réserve près : `ProcessKeyCore` réévalue quand même l'appui d'une touche ordinaire (c'est ce qui fait répéter une lettre tenue). Depuis ME-05, la répétition du déclencheur de couche tenu (`PendingTriggerScanCode`) rend la décision mémorisée sans réévaluation : relâcher Maj avant l'auto-répétition n'émet plus d'étoile et ne laisse plus de scancode dans `_passedThroughKeys` (C1.6 corrigé).
- I2 : tout keydown synthétique injecté reçoit un keyup (à la relâche, ou à la reprise sûre). Garanti par `:557-563`, `:257-289`, garde de débordement `:601-604`. Les keyups sont conservés en quarantaine pendant la suspension (`:259-261`).
- I3 : une touche dont le keydown a été transmis a son keyup transmis. Garanti par `:692-696`, `:769-772`.
- I4 : une touche dont le keydown a été absorbé a son keyup absorbé. Garanti par `:561-562, 567-568, 773`, sauf suspension (`:535-537`, conservation des relâchements).

Passage par un écran sécurisé, verrouillage, changement de session, veille ou hook décroché (audit par frappe au hasard, AFH-4, 16 novembre 2026) : les relâches faites pendant ce temps n'atteignent pas le hook. `KeyboardHook.ForgetHeldKeys(heure)` marque périmés les appuis vus avant le passage (`KeyMapper._staleKeyDowns`). L'appui suivant d'une touche périmée est neuf (I1 ne s'applique pas) ; sa relâche tardive garde la décision de l'ancien appui (I3, I4). Le déclencheur de couche en attente est abandonné sans armer de coup (`MaintainableLayerManager.AbandonPendingTrigger`). Appelé par `TrayApplication` sur `EVENT_SYSTEM_DESKTOPSWITCH`, `WM_WTSSESSION_CHANGE`, la reprise de veille et `ProbeHookSilence`, jamais aux réinstallations de routine.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1.4.1 | Libre | keydown, ProcessKeyCore rend true | — | Consommée | blocage | `KeyMapper.cs:520-525` |
| T1.4.2 | Libre | keydown, rend false | — | Transmise ; `+ _passedThroughKeys` si `CanPassThrough` | — | `:873-877` |
| T1.4.3 | Consommée/Transmise | keydown répété | — | inchangée ; effets de `ProcessKeyCore` rejoués | décision forcée = ancienne | `:523` |
| T1.4.4 | Consommée/Transmise | keyup | hors suspension | Libre ; `_passedThroughKeys` retiré | — | `:526`, `:692-696` |
| T1.4.5 | Libre | keydown pendant suspension | `IsEmissionSuspended` | Transmise | `TrackPassThroughKey` | `:512-517, 530-537` |
| T1.4.6 | Libre | Ctrl+lettre/chiffre keydown | Ctrl sans AltGr, VK trouvé | Consommée + keydown synthétique tenu | injecte VK | `:701-763, 965-976` |
| T1.4.7 | keydown synthétique tenu | keyup (même Ctrl relâché) | — | Libre ; synthétique retiré si `SendInput == 1` | injecte keyup | `:557-563, 949-963` |
| T1.4.8 | keydown synthétique tenu | `ClearPassedThroughKeys` | pas suspendu, pas de réparation en attente | Libre si tous les keyups acceptés | injecte keyups | `:257-289` |
| T1.4.9 | tout | `Count > 20` | — | `ClearPassedThroughKeys` | — | `:601-604` |

#### Temporisations et réessais

Pas de minuterie. Le nettoyage des keyups synthétiques est répété à chaque `SyncState` et à chaque débordement ; en cas de retour partiel on garde tous les keyups (`:272-274`).

Fuite bénigne notée : une répétition qui prend la voie « laisser passer » laisse un scancode dans `_passedThroughKeys` jusqu'à la prochaine remise à zéro (keyup synthétique inoffensif). Plus atteignable pour le déclencheur de couche depuis ME-05 (C1.6).

#### États ou transitions invalides atteignables

C1.6 corrigé (ticket ME-05) : invariant I1 rétabli, test `MachinesEtatsLot1Tests.RepetitionDuDeclencheur_ApresRelachementDeMaj_NEmetRienEtNeSuitRien`.

### M1.5 — Suspension d'émission et abandon de la touche morte au changement d'application (`KeyMapper`)

**Nature** : implicite (booléen + mode externe). **Fichiers** : `TypingEngine.Windows/KeyMapper.cs`, mode lu depuis `ForegroundMonitor.GetEmitContext`.

#### Variables d'état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `EmissionPaused` | bool (auto-propriété) | `KeyMapper.cs:1082` | écrit par `ApplyHookState` (pompe), lu par le hook | fil unique |
| mode de compatibilité du snapshot | `CompatibilityMode` (snapshot immuable `volatile`) | `ForegroundMonitor.cs:63` | écrit par `Recompute` (pompe), lu par le hook via `GetEmitContext` | référence atomique |
| `_lastApplicationIdentity` | identité | `KeyMapper.cs:1112` | pompe (`ApplyForegroundApplication`) | fil unique |

#### États

| état | prédicat |
|---|---|
| Émission active | `!EmissionPaused` et mode ≠ DisabledAntiCheat et fenêtre du snapshot = premier plan réel |
| Pause volontaire | `EmissionPaused` |
| Suspension imposée | `GetEmitContext().Mode == DisabledAntiCheat` (suivi indisponible, fenêtre du snapshot ≠ premier plan, anti-triche, accès distant, forceOff) (`ForegroundMonitor.cs:112-118`) |

#### Invariants

- I1 : rien n'est émis pendant une suspension. Garanti par `KeyMapper.cs:1018, 1070, 1216`.
- I2 : une suspension est fail-closed pendant le laps entre changement de fenêtre et recalcul du snapshot. Garanti par `ForegroundMonitor.cs:115` (comparaison de fenêtre).
- I3 : la suspension imposée détruit la touche morte, la pause volontaire la garde. Garanti par `KeyMapper.cs:1106-1110, 514, 858`.
- I4 : l'état de la suspension est lu de manière cohérente pendant un seul appel. Garanti depuis 1.3.0 (17 novembre 2026) : `ProcessKey` prend un instantané du contexte d'émission (`LiveEmitContext`) à l'entrée ; `IsEmissionSuspended`, `IsSuspendedByForeground`, `GetTargetKeyboardLayout` et `TryEmitText` lisent cet instantané (`EmitContext()`), une seule lecture de `GetForegroundWindow()` par frappe. Test : `Regles130MoteurTests.M1_5_I4_UnSeulAppel_LitLaSuspensionDeManiereCoherente`.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1.5.1 | Émission active | frappe | — | Émission active | `ProcessKeyCore` | `KeyMapper.cs:518-519` |
| T1.5.2 | Suspension (pause ou imposée) | frappe | — | inchangé | touche transmise ; touche morte abandonnée si imposée | `:512-517` |
| T1.5.3 | Émission active | `EmissionPaused := true` | — | Pause | `ClearPassedThroughKeys` côté hôte (`TrayApplication.cs:2122`) | `:1082` |
| T1.5.4 | Pause | reprise | `ApplyHookState(syncWhenActive)` → `SyncState(preserve)` | Émission active | keyups synthétiques de quarantaine envoyés | `:212-249`, `TrayApplication.cs:608-609` |
| T1.5.5 | tout | `ApplyForegroundApplication(app)` | identités non nulles et ≠ | touche morte abandonnée | `StateChanged` | `:1143-1149` |

#### Temporisations et réessais

Pas de minuterie propre. Le hôte recalcule le snapshot sur événement et par minuteries de la pompe (`TIMER_FOREGROUND_WATCHDOG`, `TrayApplication.cs:676`).

#### États ou transitions invalides atteignables

Aucun constat.

### M1.6 — Réparation après émission partielle (`InputRecovery`, `_pendingInputRecovery`)

**Nature** : implicite. **Fichiers** : `TypingEngine.Windows/InputRecovery.cs`, `TypingEngine.Windows/KeyMapper.cs:1154-1176`, `TypingEngine.Windows/TextEmissionResult.cs`.

#### Variables d'état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_pendingInputRecovery` | `InputRecovery?` | `KeyMapper.cs:1084` | hook et appels `TryEmitText` de l'UI (même fil) | fil unique |
| `_inputs` (lot d'origine) | `Win32.INPUT[]` | `InputRecovery.cs:6` | idem | immuable |
| `_numLockTarget` | `bool?` | `:7` | écrit par le constructeur et `CancelNumLockRestoration` (sur keydown NumLock, `KeyMapper.cs:358-359`) | fil unique |
| `_deferred` | bool | `:8` | écrit par `MarkDeferred` après la première tentative | fil unique |
| `_emissionLossCount`, `_emissionLossStreak` | int | `KeyMapper.cs:1191-1192` | `SendInputs`, `RecordEmissionLoss` | fil unique |

#### États

| état | prédicat |
|---|---|
| Sain | `_pendingInputRecovery == null` |
| Réparation en attente, non différée | `_pendingInputRecovery != null && !_deferred` (instant de la création, une tentative faite aussitôt) |
| Réparation en attente, différée | `_pendingInputRecovery != null && _deferred` (après au moins un essai) |
| Série de refus | `_emissionLossStreak > 0` |

#### Invariants

- I1 : le texte d'un lot partiel n'est jamais rejoué. Garanti par `KeyMapper.cs:1161` (« Une tentative, jamais un rejeu du texte ») et `InputRecovery.cs:44-47` (non modificateurs seulement relâchés).
- I2 : une réparation en attente est rejouée avant toute nouvelle émission ou injection : `KeyMapper.cs:198, 261, 564, 1018`.
- I3 : un modificateur que l'utilisateur a relâché entre-temps n'est pas rebloqué. Garanti par `InputRecovery.cs:36-38` (`_deferred` ajoute `GetAsyncKeyState`).
- I4 : NumLock est remis à l'état voulu seulement si l'utilisateur n'y a pas touché. Garanti par `KeyMapper.cs:358-359` et `InputRecovery.cs:50-62`.
- I5 : le nombre d'essais est borné. Garanti depuis 1.3.0 (17 novembre 2026) : au bout de `KeyMapper.MaxInputRecoveryAttempts` (8) essais refusés (`InputRecovery.RecordFailedAttempt`), la réparation est abandonnée et journalisée en événement critique `ReparationAbandonnee` ; les frappes suivantes repartent sur le chemin normal. Test : `Regles130MoteurTests.M1_6_I5_NombreDEssaisDeReparationBorne_SousRefusPersistant`.
- I6 : deux réparations ne se superposent pas. Garanti par le garde de `FlushInputRecovery` avant tout `SendInputsWithRecovery` (`KeyMapper.cs:198, 1018`) ; unique écrivain `:1160`.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1.6.1 | Sain | `SendInputsWithRecovery(inputs)` | `0 < sent < inputs.Length` | non différée → différée après le premier `Flush` | crée `InputRecovery`, un essai immédiat | `KeyMapper.cs:1154-1164` |
| T1.6.2 | Sain | `SendInputsWithRecovery` | `sent == 0` | Sain | série de refus +1, journal 1er puis /50 | `:1219-1220, 1230-1239` |
| T1.6.3 | Sain | `SendInputsWithRecovery` | `sent == inputs.Length` | Sain | série remise à 0 | `:1220` |
| T1.6.4 | en attente | `FlushInputRecovery` | `IsEmissionSuspended` | inchangé, rend false | — | `:1169` |
| T1.6.5 | en attente | `FlushInputRecovery` | `SendInputs(Build(...)) == Length` | Sain | keyups des caractères, état des modificateurs et de NumLock rétablis | `:1170-1175` |
| T1.6.6 | en attente | `FlushInputRecovery` | refus ou envoi partiel | différée, rend false | prochaine tentative à la prochaine frappe ou émission | `:1171-1173` |
| T1.6.7 | en attente | keydown NumLock | — | `_numLockTarget := null` | — | `KeyMapper.cs:358-359`, `InputRecovery.cs:18` |

#### Temporisations et réessais

Réessai sur événement uniquement : prochain `ProcessKeyCore` (`:564`), prochaine émission (`:1018`), `RequestCapsLockOff`, `ClearPassedThroughKeys`. Pas de limite, pas de remise à zéro à part le succès. `Build` est recalculé à chaque essai : l'intention (`_inputs`) reste d'origine, l'état physique des modificateurs est relu, ce qui évite l'écriture d'un état périmé.

Pendant que `FlushInputRecovery` échoue : un nouveau keydown physique est transmis (`KeyMapper.cs:575`), donc le caractère de la disposition native sort à la place du caractère AZERTY Global (choix assumé, fail-open).

Note : le comportement fail-open sous refus persistant est voulu et testé (`TypingEngine.Windows.Tests/Audit120RecoveryTests.cs`) ; pas de constat.

#### États ou transitions invalides atteignables

Aucun constat.

### M1.7 — File du texte attendu (`PhysicalTextInputBuffer`)

**Nature** : implicite (file d'entrées avec index et horodatage). **Fichiers** : `PhysicalTextInputBuffer.cs` ; consommateur `LessonsWindow.cs:167, 3197-3236, 1273-1291`.

#### Variables d'état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_entries` | `Queue<Entry(Text, CreatedAt, Index)>` | `PhysicalTextInputBuffer.cs:23` | écrit par `Enqueue` depuis `RawKeyDownDetailed` (hook, avant `ProcessKey`), lu/dépilé par `Resolve` depuis `WM_CHAR` (pompe) : même fil | aucune |
| `_timeoutMs`, `_clock` | long, `Func<long>` | `:24-25` | lecture | immuables |
| `_provisional`, `_awaitingDecision` | `Entry?`, bool | `PhysicalTextInputBuffer.cs` | posés par `CaptureKey` (keydown, avant `ProcessKey`), consommés par `ApplyEngineDecision` (`KeyMapper.KeyDecided`, fin de `ProcessKey`) : même fil | aucune |

#### États

| état | prédicat |
|---|---|
| Vide | `_entries.Count == 0` (après purge des périmées) |
| File de n entrées | `_entries.Count == n`, tête à l'index `entry.Index` |
| Entrée périmée | `now - CreatedAt > _timeoutMs` (purgée en tête, `:151-155`) |

#### Invariants

- I1 : ordre FIFO des frappes physiques. Garanti par `Queue` (`:23`).
- I2 : un caractère de contrôle reçu n'est jamais remplacé par du texte. Garanti par `:65-69`.
- I3 : aucun texte attendu pour un raccourci. Garanti par `:94-95` (`IsShortcutChord`).
- I4 : la file reflète ce que le moteur émet réellement. Garanti depuis ME-06 : `CaptureKey` pose une entrée provisoire (calcul de `ExpectedText`, fait avant le moteur, qui sert encore quand la touche est laissée passer au système) et le moteur annonce ensuite sa décision (`KeyMapper.KeyDecided`, texte réellement émis) ; `ApplyEngineDecision` remplace l'entrée provisoire par ce texte (C1.2 corrigé).
- I5 : toute entrée correspond à un `WM_CHAR` à venir. Garanti depuis ME-06 pour les touches que le moteur absorbe : quand il n'émet rien (second appui du déclencheur sous un coup armé, touche morte ou autre déclencheur frappé sur un coup armé), `ApplyEngineDecision` retire l'entrée provisoire et rien n'est attendu (C1.3 corrigé). Une touche laissée passer au système garde l'attendu de la disposition ; la péremption (1 s) reste le filet des cas où la décision n'arrive pas (émission suspendue).
- I6 : le texte attendu tient compte de la neutralisation du modificateur d'activation. Garanti depuis ME-06 : le texte attendu est celui que le moteur a émis (Maj d'activation neutralisé : α et non Α, accord tenu compris) (C1.2 corrigé).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1.7.1 | tout | `CaptureKey` | chord Ctrl/Alt/Win sans AltGr | inchangé | rien | `:94-95` |
| T1.7.2 | tout | `CaptureKey` | `ExpectedText` non vide | +1 entrée provisoire | purge des périmées ; attend la décision du moteur | `CaptureKey`, `Enqueue` |
| T1.7.3 | file ≥ 1 | `Resolve(c)` | `c` est contrôle | inchangé sauf si l'entrée attendait aussi un contrôle | rend `c` | `:65-69` |
| T1.7.4 | file ≥ 1 | `Resolve(c)` | `c` n'est pas contrôle | index +1, dépile si fini | rend le caractère attendu (et non `c`) | `:72-75` |
| T1.7.5 | Vide | `Resolve(c)` | — | Vide | rend `c` | `:78` |
| T1.7.6 | file | `Clear()` | changement de séance/fenêtre | Vide | — | `:81` |
| T1.7.7 | file | temps | `now - CreatedAt > 1000` | entrée retirée | — | `:151-155` |
| T1.7.8 | file avec entrée provisoire | `ApplyEngineDecision(handled, texte)` (fin de `ProcessKey`) | touche absorbée ou texte émis | entrée provisoire retirée ; +1 entrée du texte réellement émis s'il n'est pas vide | rien attendu quand le moteur n'émet rien (C1.2, C1.3) | `ApplyEngineDecision` |
| T1.7.9 | file avec entrée provisoire | `ApplyEngineDecision(false, vide)` | touche laissée passer sans texte émis | inchangé | Windows écrira le caractère natif ; l'attendu de la disposition reste | `ApplyEngineDecision` |
| T1.7.10 | file avec entrée provisoire | `ApplyEngineDecision(true, préfixe)` | lot `SendInput` partiel : au moins un événement parti, pas tous | entrée provisoire retirée ; +1 entrée du préfixe de caractères dont au moins un événement est parti | l'attendu reste cohérent avec ce qui a pu s'écrire ; l'entrée provisoire n'est jamais retirée quand un caractère a pu partir (suite du 16 novembre) | `KeyMapper.BuildAndSendText` (`CharsStartedBy`), `ApplyEngineDecision` |

#### Temporisations et réessais

Une seconde de péremption paresseuse (`Enqueue`, `HasPending`, `Resolve`). Pas de réessai.

#### États ou transitions invalides atteignables

C1.2 corrigé (ticket ME-06) : invariants I4 et I6 rétablis, tests `MachinesEtatsLot1Tests.FileDesLecons_AccordTenu_AttendCeQueLeMoteurEmet` et `MachinesEtatsLot1Tests.FileDesLecons_MajDActivationEncoreTenu_AttendCeQueLeMoteurEmet`.

C1.3 corrigé (ticket ME-06) : invariant I5 rétabli, tests `MachinesEtatsLot1Tests.FileDesLecons_SecondAppuiDuDeclencheurSousUnCoup_NAttendAucunCaractere` et `MachinesEtatsLot1Tests.FileDesLecons_ToucheMorteSousUnCoup_NAttendAucunCaractere`. Suite du 16 novembre : un lot `SendInput` partiel n'est plus annoncé « rien émis » (`KeyDecision.EmittedText` porte les caractères dont au moins un événement est parti), tests `MachinesEtatsLot1Tests.FileDesLecons_LotPartiel_GardeUnAttenduCoherentAvecCeQuiAPuPartir` et `FileDesLecons_LotRefuStrictement_NAttendRien`. Texte de plusieurs caractères : `FileDesLecons_TexteDePlusieursCaracteresCoupe_AnnonceLesCaracteresCommences` (« ´h », quatre événements, coupé à 1, 2, 3 et 4 : entre l'appui et le relâchement du premier caractère, entre les deux caractères, entre l'appui et le relâchement du second, lot complet) et `CharsStartedByTests` (texte de plusieurs lettres, substitut isolé, paire de substitution). Un caractère sans événement existe (le substitut isolé, que `BuildAndSendText` saute) mais n'est compté comme parti que si un caractère suivant a commencé, jamais à la coupure : pas de défaut, code inchangé. Limite connue : un caractère dont seuls certains événements sont partis (appui sans relâchement) est supposé écrit ; si Windows l'ignore (séquence Alt+code interrompue), l'entrée attendue expire après une seconde.

### M1.8 — Indicateur de couche (`LayerIndicatorWindow`)

**Nature** : implicite (poignée de fenêtre, `_visible`, `_label`). **Fichiers** : `LayerIndicatorWindow.cs`.

#### Variables d'état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_hWnd` | `IntPtr` (0 = pas de fenêtre) | `LayerIndicatorWindow.cs:9, 26` | pompe | fil unique |
| `_label`, `_mode`, `_visible`, `_dpi` | string, enum, bool, int | `:11-15` | pompe (`Update`, `RefreshPosition`, `Hide`, `WM_DPICHANGED`) | fil unique |
| `_hBrush` | `IntPtr` (appartient à la classe) | `:10, 31` | pompe | — |

#### États

| état | prédicat |
|---|---|
| Sans fenêtre | `_hWnd == 0` (classe refusée ou création refusée, `:22-23, 26-28`) |
| Caché | `_hWnd != 0 && !_visible` |
| Visible | `_hWnd != 0 && _visible && _label != ""` |
| Détruit | après `Dispose` : `_hWnd := 0` (`:160-164`) |

#### Invariants

- I1 : l'indicateur n'est montré que si la couche est active, les réglages l'autorisent, hors saisie sécurisée (`LayerIndicatorWindow.cs:56-62`).
- I2 : aucun appel Win32 sur une fenêtre absente. Garanti depuis ME-07 : `Update` et `RefreshPosition` sortent dès que `_hWnd == 0`, et `Hide` n'appelle `ShowWindow` que si la fenêtre existe (C1.7 corrigé).
- I3 : le pinceau n'est pas détruit par l'indicateur (propriété de la classe). Garanti par `:29-31` et `:158-166`.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1.8.1 | — | construction | classe et fenêtre créées | Caché | DPI lu | `:17-37` |
| T1.8.2 | — | construction | classe ou fenêtre refusée | Sans fenêtre | — ; `Update`, `RefreshPosition` et `Hide` n'agissent plus (ME-07) | `:22-23, 26-28` |
| T1.8.3 | Caché/Visible | `Update(state, secure)` | réglages off, secure ou inactif | Caché | `ShowWindow(0)` si visible | `:56-62, 93-99` |
| T1.8.4 | Caché/Visible | `Update` | actif et fenêtre présente (`_hWnd != 0`, ME-07) | Visible si caret trouvé, sinon Caché | libellé recalculé, `SetWindowPos`, `InvalidateRect` | `:64-74, 76-91` |
| T1.8.5 | Visible | `RefreshPosition` | pas de caret | Caché (`_label` vidé) | — | `:79-83` |
| T1.8.6 | Visible | `WM_DPICHANGED` | nouveau DPI | Visible | redimensionne, repaint | `:128-142` |
| T1.8.7 | tout | `Dispose` | — | Détruit | `DestroyWindow`, `UnregisterClass` | `:158-166` |

#### Temporisations et réessais

Aucune minuterie. Repositionnement déclenché par chaque keydown quand une couche est active (`TrayApplication.cs:2218-2225`) et par `StateChanged`.

#### États ou transitions invalides atteignables

C1.7 corrigé (ticket ME-07) : invariant I2 rétabli, test `MachinesEtatsLot1Tests.IndicateurDeCouche_SansFenetre_NeRepeintRien`.

### Constats du lot (renvoi)

- C1.1 un coup et composition coexistent (corrigé, ME-12)
- C1.2 file de texte attendu : accord du déclencheur et Maj neutralisé ignorés (y compris Maj tenu après un appui bref : coup armé, Maj d'activation neutralisé, `CaptureKey` calcule Α, le moteur émet α)
- C1.3 file de texte attendu : entrée fantôme quand le coup armé est lu comme touche morte active : second appui du double appui qui verrouille la couche (cas direct), touche morte ou autre déclencheur frappé sur un coup armé
- C1.4 Maj d'activation neutralisé mais pris tel quel par le passage natif des lettres
- C1.5 un coup survit à Entrée et Tab (corrigé, ME-12)
- C1.6 déclencheur enfoncé : répétition après relâchement du modificateur
- C1.7 indicateur sans fenêtre : `InvalidateRect(NULL)`

### Notes non retenues (hypothèses écartées ou trop étroites)

- **Course interne à l'émission (TOCTOU de premier plan)** : `IsEmissionSuspended` est relu plusieurs fois (`KeyMapper.cs:512, 852, 1018, 1070, 1216`). Si Alt+Tab tombe entre l'entrée de `ProcessKey` et l'émission, les voies « couche » (`:822-845`) et « caractère » (`:879`) perdent le caractère et `ConsumeOneShot` est tout de même appelé (`:844-845`) ; seule la voie composition a la garde `:852-861`. Fenêtre de quelques microsecondes, non reproductible sans écriture du système : non retenu.
- **`_consumeEscapeKeyUp` périmé** : si le keyup d'Échap est vu par `ProcessKey` pendant une suspension (`KeyMapper.cs:512-517`, qui n'appelle pas `HandleEscape`), le drapeau (`MaintainableLayerManager.cs:309`) reste vrai ; le keyup du prochain Échap sera absorbé (`:296-299`). Nécessite un verrou débloqué par Échap puis une pause entre keydown et keyup : non retenu.
- **`IsToggleShortcut` sur Ctrl+Maj périmés** après un verrouillage de session : voir I5 de M1.3, enchaînement trop spécifique, auto-réparé à la frappe suivante.
- **Perte de caractère sur refus `SendInput`** (UIPI, bureau sécurisé) : accepté et documenté AG130-09, reporté 1.3.1 (`KeyMapper.cs:1200-1207`).
- **Verrou de couche perdu par `RemoveDeadProcessLocks`** pour un processus non interrogeable : une identité sans heure de création est conservée (`KeyMapper.cs:155`), `TryGetWindowProcess` fait passer en suspension les processus non ouvrables (`ForegroundMonitor.cs:299-307`) : écarté.

### Frontières avec les autres lots

- **Hook clavier (`KeyboardHook`)** : appelle `TrackModifiers`, puis `ProcessKey` ; filtre les injections (`KeyboardHook.cs:304-315`) ; détecte Ctrl+Maj+Verr. Maj. avant `CleanupStaleModifiers` (`:336-359`). `ObserveSuspendedModifiers` alimente `TrackPassThroughKey` et `TrackModifiers`.
- **`ForegroundMonitor` / `SecureInputDetector`** : fournissent `GetEmitContext`, `IsSecureInput`, `CurrentIdentity`, `LastApplicationIdentity` au moteur. Un champ mot de passe détecté en retard laisse la couche et la composition agir dans le champ jusqu'au `Recompute` tardif (`TrayApplication.cs:428`).
- **`TrayApplication`** : `ApplyHookState`/`SyncState`/`ClearPassedThroughKeys` (pause, reprise, bascule, anti-triche), `PushMaintainableLayerSettings` (`:2233-2244`), `OnStateChanged`/`ApplyStateChange` (reporté sur la pompe), propriétaire de `LayerIndicatorWindow` (`:410`).
- **`LessonsWindow`** : unique consommateur de `PhysicalTextInputBuffer` (`LessonsWindow.cs:167, 3225, 3230`), hors « Votre texte » du tutoriel ; ses `WM_CHAR` arrivent sur la pompe, mais l'état lu par `CaptureKey` est celui d'avant `ProcessKey`.
- **`TextInsertionService` / `CharacterSearch`** : appellent `KeyMapper.TryEmitText` hors rappel du hook (`TrayApplication.cs:383`) ; ils partagent `_pendingInputRecovery` et `_emitInputs`.
- **`ConfigManager` (écritures différées sur le pool)** et **`UsageStats.RecordEmittedText`** (appelé depuis le hook : `KeyMapper.cs:1077`) : autres lots.

## Lot 2 — Hook clavier et contexte de premier plan

Chemins relatifs au dépôt. Le code fait foi ; les rapports `docs/audit-*` n'ont servi que d'indices.

### Modèle de fils du lot

| Fil | Ce qui y tourne | Preuve |
|---|---|---|
| **Fil principal (STA, pompe de messages)** | Rappel `WH_KEYBOARD_LL` (`KeyboardHook.HookCallback`), `WndProc` et tous les `WM_TIMER`, rappels `SetWinEventHook` (`OnWinEvent`), `ForegroundMonitor.Recompute`, `ReinstallHook`, `ProbeHookSilence`, `OnForegroundChanged`, `ApplyHookState`, `Cleanup` | `src/Program.cs:36` `[STAThread]` ; hook posé sur ce fil : `src/TrayApplication.cs:695` (`_hook!.Install()` dans `Run`) et `:1389` (`ActivateWithConsent`, appelé par le rappel de l'accueil) ; `RealWin32Api.cs:183` `SetWinEventHook(..., 0, 0, WINEVENT_OUTOFCONTEXT)` appelé depuis le constructeur de `ForegroundMonitor` (`ForegroundMonitor.cs:174`) sur ce même fil, donc rappels livrés par sa pompe (commentaire `ForegroundMonitor.cs:5-6`) ; minuteries `SetTimer(_hWnd, …, IntPtr.Zero)` (`TrayApplication.cs:663-679`) : `WM_TIMER` sur ce fil |
| **Fil de travail UIA « SecureInputDetector »** (MTA, un seul actif, `IsBackground` ; au plus 3 fils abandonnés encore bloqués dans COM, soit 4 en tout) | `SecureInputProbe.Run` : `_query` (COM UIA `GetFocusedElement`…), déclenchement de `ResultChangedLate` | `SecureInputProbe.cs:82,96-144` ; `SecureInputDetector.cs:44` `CoInitializeEx(…, COINIT_MULTITHREADED)` |
| **Pool de threads** | `ProbeRemoteHost` (`GameRegistry.FindActiveRemoteSession` + écriture de `KeyboardHook.RemoteSessionActive`), vidage du journal différé (`LogCompatCriticalEvent` de `Reinstall` via `AzertyGlobalWindowsTypingHost.cs:27-28`), `UsageStats.FlushInBackground` | `TrayApplication.cs:1355-1380` ; `AzertyGlobalWindowsTypingHost.cs:27-28` |

Conséquences utiles :

- Le rappel du hook, `Recompute`, les minuteries et `ReinstallHook` partagent **un seul fil** : entre eux, pas de course, seulement de la réentrance. Les champs `volatile`/`Interlocked` de `KeyboardHook` et `ForegroundMonitor` ne protègent donc en pratique que les lecteurs/écrivains du pool (`_remoteSessionActive`) ; le `_snapshot volatile` (`ForegroundMonitor.cs:63`) est surdimensionné mais inoffensif. `_lastCallbackTicks` n'est lu que par `ProbeHookSilence` (même fil).
- Seule frontière réellement inter-fils du lot : **fil principal <-> fil UIA** (`SecureInputProbe`, protégé par `_gate`, `Volatile`, deux `AutoResetEvent`) et **pool -> fil principal** (`RemoteSessionActive`, `volatile`).
- Attente bloquante sur le fil principal : `SecureInputProbe.Query` fait `_done.WaitOne(remaining)` (`SecureInputProbe.cs:87-91`), jusqu'à 30 ms (`SecureInputDetector.cs:28`), depuis `Recompute` (`ForegroundMonitor.cs:246-247` -> `RealWin32Api.cs:137`). Le fil principal est STA (`[STAThread]`), mais le code n'établit pas que cette attente pompe des messages : c'est une hypothèse sur le runtime NativeAOT, dont C2.4 dépend entièrement.
- Écritures disque : `LogCompatCriticalEvent` (synchrone, sur le fil principal) est appelé par `ProbeHookSilence` (`TrayApplication.cs:1157`) ; `LogCompatibilityCriticalEvent` de l'hôte est différé sur le pool (`ConfigManager.cs:1007-1012`). Aucun fichier d'état n'est tenu par ce lot.


### M2.1 — Cycle de vie du hook Win32 (`KeyboardHook`)

**Nature** : implicite (un handle, `_hookId`, code l'état ; l'état « décroché par Windows » existe sans variable). **Fichiers** : `src/TypingEngine.Windows/KeyboardHook.cs`, appelants `src/TrayApplication.cs` (hors lot, cités pour le fil et les gardes).

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `_hookId` | `IntPtr` | `KeyboardHook.cs:40` | écrit par `Install` (`:151`), `Reinstall` (`:186`), `Dispose` (`:452`) ; lu par `IsInstalled` (`:140`), `CallNextHookEx` du rappel (`:305` etc.) ; tout sur le fil principal | fil unique (pompe) |
| `_lastCallbackTicks` | `long` | `:46` | écrit par le rappel (`:289`, `Interlocked.Exchange`), lu par `ProbeHookSilence` via `LastCallbackTicks` (`:194`) ; fil principal | `Interlocked` (inutile en pratique) |
| `TrayApplication._activationConsent` | `bool` | `TrayApplication.cs` (hors lot) | fil principal | fil unique |
| état externe : « hook retiré par Windows » | — | pas de variable | Windows, sans notification | aucune |

#### États

| état | prédicat |
|---|---|
| H0 Absent | `_hookId == 0` (avant consentement, après `Dispose`, ou `Install` jamais appelé) |
| H1 Posé et vivant | `_hookId != 0` et Windows livre les rappels |
| H2 Posé mais décroché en silence (inobservable directement) | `_hookId != 0` et Windows a retiré le hook (`LowLevelHooksTimeout`, reprise de veille) ; se déduit de M2.2 |
| (hors prod) H0' aperçu dev | `#if DEBUG` et `AG_DEV_PREVIEW` : `Install`/`Reinstall` sont des no-op (`:144-148`, `:166-170`) |

#### Invariants

- I1 : au plus un hook par processus à l'état stable. Garanti pour `Reinstall` (pose le nouveau avant de décrocher l'ancien, `:171-186`, même fil donc aucun rappel entre les deux). **Non garanti dans `Install`** : `_hookId = SetWindowsHookEx(...)` (`:151`) écrase sans décrocher ; la garde est externe (`ShouldInstallHookAtLoopStart` `TrayApplication.cs:628-630`, `if (_activationConsent) return true` `:1384`). Aucun chemin atteint ne double-installe (voir « Table »).
- I2 : aucune frappe ni double traitement pendant `Reinstall` : garanti par le fil unique (`:159-163`, commentaire) — vrai tant que `Reinstall` ne pompe pas, ce qui est le cas.
- I3 : `Reinstall` retourne `true` ssi un hook est posé à la sortie (`:181`, `:187`).
- I4 : après `Dispose`, plus aucun appel `CallNextHookEx` valide ne dépend de `_hookId` : le rappel peut encore s'exécuter avec `_hookId == 0` (Windows ignore le premier argument de `CallNextHookEx`) — sans effet.
- I5 : `Install` échoué -> exception (`:152-153`) ; `ActivateWithConsent` appelle `_hook.Dispose()` dans son `catch` si le consentement n'est pas acquis (`TrayApplication.cs:1403`) pour ne pas laisser un hook semi-activé.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1.1 | H0 | `Install()` au démarrage de la boucle | `ShouldInstallHookAtLoopStart` (consentement, hook créé, non installé, démarrage sans échec) | H1 | `SetWindowsHookEx(WH_KEYBOARD_LL)` ; `SyncState` du mapper | `TrayApplication.cs:691-698`, `KeyboardHook.cs:142-154` |
| T1.2 | H0 | `Install()` via `ActivateWithConsent` | `!_activationConsent` | H1 | pose du hook, `AcceptActivation`, `ApplyHookState` | `TrayApplication.cs:1382-1399` |
| T1.3 | H0 | `Install()` échoue | `_hookId == 0` après l'appel | H0 | `InvalidOperationException` ; `Dispose` ; `MessageBox` (pompe imbriquée) | `KeyboardHook.cs:152-153`, `TrayApplication.cs:1401-1409` |
| T1.4 | H1 ou H2 | `Reinstall()` | `SetWindowsHookEx` renvoie non nul | H1 | nouveau hook posé, ancien décroché | `KeyboardHook.cs:171-187` |
| T1.5 | H1 ou H2 | `Reinstall()` échoue | `newId == 0` et `_hookId != 0` | inchangé (H1 ou H2) | journal `HookReinstallFailed` ; retour `true` | `KeyboardHook.cs:172-181` |
| T1.6 | H0 | `Reinstall()` échoue | `newId == 0` et `_hookId == 0` | H0 | journal ; retour `false` | `KeyboardHook.cs:172-181` |
| T1.7 | H0 | `Reinstall()` réussit | consentement (garde de l'appelant `ReinstallHook`) | H1 | pose sans passer par `Install` | `TrayApplication.cs:1180-1184` |
| T1.8 | H1 | Windows retire le hook (rappel trop lent, reprise de veille) | externe | H2 | aucune notification | `HookSilenceWatchdog.cs:6-11` |
| T1.9 | H2 | `Reinstall()` réussit | — | H1 | `UnhookWindowsHookEx(ancien)` retourne `false`, ignoré | `KeyboardHook.cs:184-186` |
| T1.10 | H1 ou H2 | `Dispose()` | `_hookId != 0` | H0 | `UnhookWindowsHookEx` | `KeyboardHook.cs:447-454` ; `TrayApplication.cs:2344` |
| T1.11 | H0 | `Reinstall()` appelé alors qu'aucun consentement | `ReinstallHook` sort si `!_activationConsent` | H0 | rien | `TrayApplication.cs:1180` |

Déclencheurs de `ReinstallHook` (tous sur le fil principal) : `TIMER_REHOOK/2/3` à 0,5 s / 3 s / 8 s avec `nudgeForeground=true` (`:663-665`, `:1008-1012`), `TIMER_HOOK_WATCHDOG` toutes les 60 s sans condition (`:667`, `:1014-1019`), `ProbeHookSilence` (`:1163`), `PBT_APMRESUMEAUTOMATIC` / `PBT_APMRESUMESUSPEND` (`:1059-1066`, deux événements consécutifs possibles -> deux réinstallations), `WTS_SESSION_UNLOCK/CONSOLE_CONNECT/REMOTE_CONNECT` (`:1069-1083`), `TaskbarCreated` avec nudge (`:1105-1111`).

#### Temporisations et réessais

Réinstallation périodique 60 s (aveugle), sonde 2 s (M2.2), trois réinstallations de démarrage. Aucun réessai après `T1.3` en dehors des minuteries de démarrage ; l'échec de démarrage les coupe (`TrayApplication.cs:702-710`). Pas de compteur d'échecs de `SetWindowsHookEx` : la ligne `HookReinstallFailed` est écrite à chaque échec (pas de limite de débit).

#### États ou transitions invalides atteignables

C2.1


### M2.2 — Sonde de silence du hook (`HookSilenceWatchdog` + `ProbeHookSilence`)

**Nature** : implicite et explicite (un compteur `_silenceStreak`, une fenêtre `_hookProbeWindowStartTicks`, des bits « consommés » de `GetAsyncKeyState`). **Fichiers** : `src/TypingEngine.Windows/HookSilenceWatchdog.cs`, `KeyboardHook.cs:196-220`, `TrayApplication.cs:1137-1164` (hors lot).

#### Variables d'état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_silenceStreak` | `int` | `HookSilenceWatchdog.cs:32` | fil principal (`Observe`, `Suspend`) | fil unique |
| `ReinstallCount` | `int` (auto-prop) | `:45` | fil principal | fil unique |
| `_hookProbeWindowStartTicks` | `long` | `TrayApplication.cs:501` | fil principal | fil unique |
| bit de poids faible de `GetAsyncKeyState` (état du noyau) | — | consommé à la lecture (`KeyboardHook.cs:217`) | lecteurs : la sonde ; aussi `KeyMapper.cs:480-482` et `InputRecovery.cs:37` pour Shift/touches (lecture du seul bit 15 mais lecture = consommation du bit 0) | aucune (état global du système) |
| `_lastCallbackTicks` | voir M2.1 | | | |

#### États

| état | prédicat |
|---|---|
| W0 Inactif | `!_activationConsent`, ou `_hook == null`, ou `!ShouldProcessHook` : la sonde sort sans compter |
| W1 Veille, série 0 | `_silenceStreak == 0` |
| W2 Série k (0 < k < N) | `0 < _silenceStreak < _consecutiveProbes` (N=2 par défaut : un seul état intermédiaire k=1) |
| W3 Déclenché | `Observe` retourne `true` : réinitialisation de la série à 0, `ReinstallCount++` |

#### Invariants

- I1 : une série ne traverse jamais une pause/suspension : garanti par `Suspend()` appelé quand `_hook == null || !ShouldProcessHook` (`TrayApplication.cs:1143-1151`, `HookSilenceWatchdog.cs:79`).
- I2 : sans touche vue, jamais d'incrément : `!keyPressObserved` remet la série à 0 (`HookSilenceWatchdog.cs:60-63`).
- I3 : un rappel reçu pendant la fenêtre remet la série à 0 (`lastCallbackTicks >= windowStartTicks`, `:60`).
- I4 : les bits `GetAsyncKeyState` sont consommés même hors surveillance (`TrayApplication.cs:1148`) et sur toute la plage 0x08-0xFE sans sortie anticipée (`KeyboardHook.cs:212-218`) : garanti. Mais lecture concurrente par d'autres appelants possible (faux négatif seulement, car consommer un bit retire une frappe vue).
- I5 : **« une frappe vue par `GetAsyncKeyState` est une frappe que le hook aurait dû voir »** : **non garanti**. Faux quand le système ne livre pas la frappe au hook (fenêtre de niveau d'intégrité supérieur / UIPI, voir C2.1).
- I6 : `ReinstallCount` mesure des tentatives, pas des réussites : `Observe` l'incrémente avant que `Reinstall` ait réussi (`HookSilenceWatchdog.cs:69-71`, `TrayApplication.cs:1163`) ; la remise à 0 de la série se fait même si `SetWindowsHookEx` échoue.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T2.1 | tout | tick `TIMER_HOOK_PROBE` (2 s) | `!_activationConsent` | inchangé (même la fenêtre n'avance pas) | rien | `TrayApplication.cs:1139` |
| T2.2 | tout | tick | `_hook == null \|\| !ShouldProcessHook` | W0 -> W1 | fenêtre avancée, bits consommés, `Suspend()` | `:1140-1151` |
| T2.3 | W1 | tick | touche vue ET rappel dans la fenêtre | W1 | série 0 | `HookSilenceWatchdog.cs:60-63` |
| T2.4 | W1 | tick | pas de touche vue | W1 | série 0 | idem |
| T2.5 | W1 | tick | touche vue ET `lastCallbackTicks < windowStart` | W2 (k=1) | `_silenceStreak++` | `:66-67` |
| T2.6 | W2 | tick | touche vue ET silence | W3 -> W1 | `ReinstallCount++`, journal `HookSilentlyDetached` (disque synchrone), `ReinstallHook(false)` | `:66-71`, `TrayApplication.cs:1157-1163` |
| T2.7 | W2 | tick | rappel dans la fenêtre, ou aucune touche | W1 | série 0 | `:60-63` |
| T2.8 | tout | `TIMER_HOOK_WATCHDOG` 60 s | sans condition | inchangé | `ReinstallHook(false)` | `TrayApplication.cs:1014-1019` |

#### Temporisations et réessais

Fenêtre 2 s (`HOOK_PROBE_INTERVAL_MS`, `TrayApplication.cs:499`), 2 sondes consécutives (`HookSilenceWatchdog.cs:29`), donc ~4 s de détection. Pas de plafond de réinstallations, pas de temporisation exponentielle après déclenchement : une cause persistante (C2.1) redéclenche toutes les ~4 s. Réinstallation périodique 60 s en filet. Fenêtre de course µs signalée sans constat : `windowStart` est mis à jour (`:1141`) avant la lecture des bits (`:1153`) ; une frappe tombant entre les deux est comptée dans cette sonde alors que son rappel appartient à la fenêtre suivante -> une seule série à 1, il faut deux sondes de suite : négligeable.

#### États ou transitions invalides atteignables

C2.1


### M2.3 — Mode de traitement du hook (`Enabled` / `PassThroughAll` / `ShortcutsWhilePassThrough`)

**Nature** : implicite (trois booléens indépendants dans `KeyboardHook` codent le mode, calculés par `ApplyHookState` hors lot). **Fichiers** : `KeyboardHook.cs:50-51,81-116,285-410`, `TrayApplication.cs:585-610`.

#### Variables d'état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_enabled` | `bool` | `KeyboardHook.cs:50` (défaut `true`) | écrit par `ApplyHookState` (`TrayApplication.cs:606`), lu par le rappel (`:395`) ; fil principal | fil unique |
| `_passThroughAll` | `bool` | `:51` | idem (`:589`, lu `:324,389`) | fil unique |
| `ShortcutsWhilePassThrough` | `bool` auto-prop | `:116` | idem (`:602`, lu `:324`) | fil unique |
| `_vkSearch`, `_vkVirtualKeyboard` | `uint` | `:57-58` | `ReloadShortcuts` (ctor, réglages) ; lus par le rappel | fil unique |
| `_nonModifierPressed` | `bool` | `:61` | rappel seul | fil unique |
| `_remoteSessionActive` | `volatile bool` | `:54` | **écrit par le pool** (`TrayApplication.cs:1367`), lu par le rappel (`:314`) | `volatile` |

#### États

| état | prédicat |
|---|---|
| P0 Remappage actif | `_enabled && !_passThroughAll` |
| P1 Désactivé par l'utilisateur | `!_enabled && !_passThroughAll` : modificateurs suivis, raccourcis détectés, `TrackPassThroughKey`, aucun remappage (`:395-409`) |
| P2 Pass-through total (suspension imposée, ou avant consentement) | `_passThroughAll && !ShortcutsWhilePassThrough` : seuls `ObserveSuspendedModifiers`, rien d'autre (`:324-329`) |
| P3 Pass-through avec raccourcis (pause volontaire, `UserOverride`) | `_passThroughAll && ShortcutsWhilePassThrough` : traitement complet sans remappage (`!_enabled`) |
| P4 incohérent | `_enabled && _passThroughAll`, ou `ShortcutsWhilePassThrough && !_passThroughAll` |

#### Invariants

- I1 : `_enabled == ShouldProcessHook`, `_passThroughAll == ShouldBlockHookCompletely` : garanti par l'unique écrivain `ApplyHookState` (`TrayApplication.cs:585-610`) ; `ShouldProcessHook` implique `!ShouldBlockHookCompletely` (`:560-561`), donc P4 est inatteignable après un `ApplyHookState`.
- I2 : les trois écritures sont séquentielles mais sans rappel possible entre elles (même fil, aucune pompe dans `ApplyHookState`) : garanti. Les défauts du constructeur (`_enabled = true`, `_passThroughAll = false`, P0) ne durent pas : `ApplyHookState()` est appelé juste après la création (`TrayApplication.cs:361-366`) et avant toute pose (le hook n'est pas posé avant `:695`/`:1389`).
- I3 : en P2, aucune notification, aucun raccourci, aucune émission : garanti (`:324-329`, retour avant le reste).
- I4 : en P3, `RawKeyDown` n'est pas notifié (`ShouldNotifyRawKeyDown`, `:438-439`) : garanti.
- I5 : le mode suit le premier plan réel sans délai : **non garanti** pendant l'intervalle entre le changement de fenêtre et le `WinEvent` correspondant (le rappel du hook, livré comme message envoyé, peut passer avant l'événement posté) ; atténué par `GetEmitContext` côté émission (hors lot).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T3.1 | P0 | suspension imposée (anti-triche, accès distant, premier plan inconnu) | `ClassifySuspensionTransition` = Enter | P2 | `_suspendedForCompatibility=true` ; `ApplyHookState` | `TrayApplication.cs:2960-2973` |
| T3.2 | P0 | suspension `UserOverride` | idem | P3 | idem | `:2960-2973`, `:602-605` |
| T3.3 | P2/P3 | autre motif de suspension | `ReasonChanged` | P2 ou P3 | `ShortcutsWhilePassThrough` recalculé | `:2976-2988` |
| T3.4 | P2/P3 | sortie de suspension | `Leave` | P0 (ou P1 si `_enabled` faux, P3 si pause) | `ApplyHookState(syncWhenActive)` | `:2990-3011` |
| T3.5 | P0 | pause volontaire | `_pauseUntilUtc` | P3 | idem | `:2121-2124` |
| T3.6 | P0 | bascule utilisateur | `OnToggle` | P1 | idem | `:2025-2027` |
| T3.7 | tout | `RemoteSessionActive` change | écrit par le pool toutes les 5 s | inchangé (autre dimension : traitement des injections étrangères) | lu au prochain rappel | `:1355-1380`, `KeyboardHook.cs:263-267` |

#### Temporisations et réessais

`ProbeRemoteHost` toutes les 5 s (`TrayApplication.cs:521,679`), un seul à la fois (`Interlocked.Exchange(ref _remoteHostProbeRunning, 1)`, `:1357`), levé dans `finally` (`:1375-1378`). Latence connue et documentée : début de session jusqu'à 5 s non vu, fin de session jusqu'à 5 s encore vue (`GameRegistry.cs:108-109`) ; pendant ce délai une injection étrangère (auto-saisie d'un gestionnaire de mots de passe) peut être remappée.

#### États ou transitions invalides atteignables

Aucun constat.


### M2.4 — Contexte de premier plan et mode de compatibilité (`ForegroundMonitor`)

**Nature** : explicite (`CompatibilityMode`, `CompatibilitySuspendReason`) et implicite (`Snapshot` immuable, trois handles `WinEventHook`). **Fichiers** : `ForegroundMonitor.cs`, `GameRegistry.cs`, `Win32Api/RealWin32Api.cs`, `Win32Api/IWin32Api.cs`.

#### Variables d'état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_snapshot` | `volatile Snapshot?` (record immuable : fenêtre, process, chemin, HKL, mode, motif, identité, `SecureInput`) | `ForegroundMonitor.cs:54-63` | écrit par `Recompute` (`:253`, `:281`) ; lu par `GetEmitContext` (`:112-118`), `IsSnapshotStale`, `IsWindowStale`, `CurrentMode`… ; tout sur le fil principal | `volatile` + remplacement de référence ; fil unique |
| `_lastApplication` | `Snapshot?` | `:64` | `Recompute` (`:254-255`), lu par le menu | fil unique |
| `IsTransientShellForeground` | `bool` auto-prop | `:84` | `Recompute` (`:256`, `:282`) | fil unique |
| `_secureRetryAfter` | `long` | `:42` | `Recompute` (`:248`), lu par `IsSnapshotStale` (`:135`) | fil unique |
| `_winEventHook`, `_focusEventHook`, `_switchEventHook` | `IntPtr` | `:46-48` | ctor (`:174-184`), `Dispose` (`:341-355`) | fil unique |
| `_winEventDelegate` | délégué | `:45` | ctor, `Dispose` (mis à `null`, `:356`) | fil unique |
| mode et motif résolus | `CompatibilityMode`, `CompatibilitySuspendReason` | `:18-35` | `ResolveState` | pur |

#### États

Mode et motif sont dérivés de `ResolveState` (`:296-337`) ; l'état du moniteur est le produit (suivi disponible ?) x (mode, motif) x (`SecureInput`) x (fenêtre = premier plan réel ?).

| état | prédicat |
|---|---|
| F0 Suivi indisponible (absorbant) | `!IsTrackingAvailable` (`_winEventHook == 0 \|\| _focusEventHook == 0`, `:154-155`) -> `(DisabledAntiCheat, UnknownForeground)` partout (`:237-239`) |
| F1 Normal | suivi dispo, `Mode=Default`, `Reason=None` |
| F2 NativeCombo | `forceOn` utilisateur, ou DLL de jeu chargée (`:324-325`, `:330-334`) ; `Reason=None` |
| F3 Suspendu `RemoteAccess` | processus dans `RemoteAccessProcesses`, prioritaire sur tout (`:315-316`) |
| F4 Suspendu `AntiCheat` | `IsAntiCheatProcess`, prioritaire sur `forceOn` (`:321-322`) |
| F5 Suspendu `UserOverride` | `forceOff` (`:326-327`) |
| F6 Suspendu `UnknownForeground` | `!hasFg && pid != 0`, ou suivi indisponible, ou exception dans `Recompute` (`:304-305`, `:237-239`, `:281-282`) |
| F7 Aucun premier plan | `!hasFg && pid == 0` -> `(Default, None)` mais `secureInput == true` (`:306`, `:246`) |
| F8 Snapshot périmé | `_snapshot.Window != GetForegroundWindow()` (`IsWindowStale`, `:144-151`) ou `SecureInput` et horloge >= `_secureRetryAfter` (`IsSnapshotStale`, `:129-137`) |

#### Invariants

- I1 : lecture cohérente (mode, HKL) pour l'émission : garanti, une seule capture de `_snapshot` (`:114`).
- I2 : toute émission est refusée quand le snapshot décrit une autre fenêtre que le premier plan (`GetEmitContext` `:115-116`) : garanti. La seule exception est F7 : `Window == 0 == GetForegroundWindow()` passe la garde, mais uniquement tant que le système lui-même n'a aucune fenêtre active ; dès qu'une fenêtre réelle est active, `snap.Window != GetForegroundWindow()` bloque toute émission. Le laps « avant l'événement qui désigne la vraie fenêtre » n'existe donc pas.
- I3 : un anti-cheat surclasse `forceOn` ; un accès distant surclasse les deux (`:315-325`) : garanti par l'ordre des tests.
- I4 : pas d'appel UIA (`IsWindowPasswordField`) pour `AntiCheat`/`RemoteAccess` (`SkipsPasswordProbe`, `:246-247,293-294`) : garanti. Pas pour `UserOverride` ni `UnknownForeground` avec suivi disponible.
- I5 : `ForegroundChanged` n'est jamais levé par le rappel clavier : garanti (appelé depuis `Recompute` seulement, `:276,285`). Il est levé à **chaque** `Recompute` ayant un nom de processus (la 4e branche du `||` est toujours vraie, `:272`) : conséquence, `OnForegroundChanged` s'exécute au moins une fois par seconde tant que `SecureInput` est vrai (relance 1 s, `:135`) ; conforme au commentaire « Toujours notifier » (`:274-275`) mais plus fréquent que « changement effectif » (`:157`).
- I6 : un suivi indisponible n'est jamais rétabli : **non garanti** par du code de reprise ; état absorbant jusqu'au redémarrage, documenté (`:14`, `:185`). Aucune réinstallation des WinEventHook.
- I7 : exception dans `Recompute` -> snapshot de repli `UnknownForeground` + `SecureInput=true` (`:279-286`), réparé par `IsSnapshotStale` (fenêtre 0 != premier plan) dans la seconde : garanti. Une exception levée par un abonné de `ForegroundChanged` (`:276`) tombe dans ce même `catch` : l'état « suspendu » est alors imposé par une erreur d'interface (pas de preuve d'atteignabilité).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T4.1 | — | construction | `SetWinEventHook` x3 | F1 ou F0 | `Recompute` initial | `:165-195` |
| T4.2 | tout | `EVENT_SYSTEM_FOREGROUND`, `EVENT_OBJECT_FOCUS`, `SWITCHSTART/END` | rappel `OnWinEvent` (fil principal) | recalcul | `Recompute()` | `:197-203` |
| T4.3 | tout | `TIMER_FOREGROUND_WATCHDOG` (250 ms) | `IsSnapshotStale` | recalcul | `Recompute()` | `TrayApplication.cs:1039-1046`, `:514` |
| T4.4 | tout | `WM_INPUTLANGCHANGE`, `WM_APP_SECURE_RECHECK`, changement d'override | — | recalcul | `Recompute()` | `TrayApplication.cs:1054-1057,817-821,1531` |
| T4.5 | tout | `Recompute`, premier plan = `RemoteAccess` | `IsRemoteAccessProcess` | F3 | `secureInput=false` | `ForegroundMonitor.cs:315-316,246` |
| T4.6 | tout | `Recompute`, anti-triche | `IsAntiCheatProcess` | F4 | idem | `:321-322` |
| T4.7 | tout | `Recompute`, override | `forceOn` / `forceOff` | F2 / F5 | | `:324-327` |
| T4.8 | tout | `Recompute`, modules de jeu | `HasGameFrameworkLoaded` | F2 | | `:330-334` |
| T4.9 | tout | `Recompute`, `OpenProcess` refusé | `!hasFg && pid != 0` | F6 | fail-closed | `:304-305` |
| T4.10 | tout | `Recompute`, pas de fenêtre ou fenêtre détruite | `window == 0` ou `tid == 0` | F7 | `secureInput=true` | `RealWin32Api.cs:58,60-61`, `ForegroundMonitor.cs:306,246` |
| T4.11 | tout | exception | `catch` | F6, `Window=0` | journal, `ForegroundChanged` | `:279-286` |
| T4.12 | F8 | prochain événement | — | état recalculé | — | `:210-253` |
| T4.13 | tout | `Dispose` | — | F0 | trois `UnhookWinEvent`, délégué à `null` | `:339-357` |

#### Temporisations et réessais

Relance sécurisée 1 s (`_secureRetryAfter = clock + 1000`, `:248`), chien de garde 250 ms, plus relance sur résultat UIA tardif (M2.5). Pas de réessai du `SetWinEventHook` (I6). L'état absorbant F0 est documenté et non retenu comme bogue.

#### États ou transitions invalides atteignables

C2.3, C2.4


### M2.5 — Sonde de saisie sécurisée (`SecureInputProbe` / `SecureInputDetector`)

**Nature** : implicite (identifiants de requête, résultat, ouvrier, génération d'ouvrier, `retryAfter`) ; un seul fil de travail actif, les ouvriers bloqués sont abandonnés (C2.2). **Fichiers** : `SecureInputProbe.cs`, `SecureInputDetector.cs`, `RealWin32Api.cs:107-139`.

#### Variables d'état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_requested` | `Request(Id, Window, Deadline)` | `SecureInputProbe.cs:17` | écrit par `Query` (fil principal, sous `_gate`, `Volatile.Write`), lu par `Run` (`Volatile.Read`) | `_gate` + `Volatile` |
| `_result` | `Result(Request, Secure)` | `:20` | écrit par `Run` (worker), lu par `Query` | `Volatile` |
| `_worker` | `Thread?` | `:16` | `Query` (sous `_gate`) | `_gate` |
| `_retryAfter` | `long` | `:18` | `Run` (`finally`), `Query` | `Volatile.Write`, lecture simple sous `_gate` |
| `_stopping` | `volatile bool` | `:19` | `Dispose` / worker | `volatile` |
| `_generation`, `_busySince`, `_abandoned` | `int`, `long`, `int` | `:27-29` | écrits par `Query` et par `Run`, toujours sous `_gate` | `_gate` |
| `_abandonAfter`, `_clock` | `int`, `Func<long>` | `:19-20` | lus par `Query` et `Run` | lecture seule, injectables pour les tests |
| `_wake`, `_done` | `AutoResetEvent` | `:14-15` | les deux | primitives |
| `SecureInputDetector._automation` | `IntPtr` (`[ThreadStatic]`) | `SecureInputDetector.cs:51-52` | le fil qui l'utilise | une instance par fil : un ouvrier abandonné ne partage pas celle de son remplaçant |

#### États

| état | prédicat |
|---|---|
| S0 Sans ouvrier | `_worker == null` ou `!IsAlive` |
| S1 Ouvrier en attente | worker vivant, bloqué sur `_wake.WaitOne()` (`:63`) |
| S2 Ouvrier occupé | worker dans `_query` (COM cross-process), `_busySince != 0` |
| S4 Ouvrier abandonné | ancien worker toujours bloqué dans `_query` après abandon : `generation != _generation` ; il n'est plus `_worker`, et son retour est ignoré. Au plus 3 (`MaxAbandonedWorkers`) |
| S3 Arrêté | `_stopping == true` : `Query` rend `true` (`:37`) |
| résultat | « frais » si `_result.Request == id` demandé ; sinon la requête rend `true` (sécurisé) |

#### Invariants

- I1 : une requête sans résultat frais est sécurisée (fail-closed) : garanti (`:53` `result.Request != request.Id || result.Secure`).
- I2 : jamais plus d'un ouvrier ACTIF (courant) ; un ouvrier occupé n'est pas multiplié avant le délai d'abandon (`:70`, test `Worker_bloque_n_est_ni_multiplie_ni_abandonne_avant_le_delai`). Un ouvrier occupé depuis `abandonAfter` ms (2 s par défaut) est abandonné : la génération change, un nouvel ouvrier repart (`:70-84`, tests `Ouvrier_bloque_sur_une_fenetre_ne_garde_pas_une_autre_fenetre_securisee_indefiniment`, `Worker_bloque_est_abandonne_apres_le_delai_et_une_nouvelle_detection_repart`). Au plus 3 ouvriers abandonnés restent bloqués, donc 4 fils au maximum ; au-delà, plus aucun fil n'est lancé et la sonde reste sécurisée tant qu'un abandonné ne revient pas (test `Workers_abandonnes_sont_bornes_puis_la_sonde_reste_securisee`). Avant l'abandon et pendant qu'un nouvel ouvrier n'a pas répondu, la requête rend « sécurisé » (I1, fail-closed : choix déjà pris par le code quand il ne sait pas, le plus sûr pour l'utilisateur car il ne laisse jamais passer à tort une saisie de mot de passe) (C2.2 corrigé).
- I3 : un résultat périmé n'est jamais renvoyé à une requête plus récente : garanti par l'identifiant (`:87,93`). Le retour tardif d'un ouvrier abandonné n'écrit ni `_result` ni `ResultChangedLate` : la génération est vérifiée sous `_gate` avant toute écriture (`:116-127`, test `Retour_tardif_d_un_worker_abandonne_n_ecrase_pas_la_detection_recente`).
- I4 : `ResultChangedLate` n'est levé que si la requête a dépassé son délai ET que le résultat diffère du **précédent résultat**, quelle que soit la fenêtre qui l'a produit (`:125-132`) : le « précédent » n'est pas comparé à la même fenêtre -> pas de garantie de levée pour un résultat tardif correct ; reste ainsi après C2.2, car l'ouvrier abandonné ne lève plus rien et que l'ouvrier courant est relancé par la relance sécurisée de 1 s de `ForegroundMonitor`.
- I5 : `_done` peut rester signalé sans résultat utile : la boucle de `Query` revérifie `_result` (`:87-91`) : sans conséquence.
- I6 : instance COM créée/relâchée par le seul worker : garanti ; recréée après tout `HRESULT` négatif (`SecureInputDetector.cs:90-94,104-109`) donc `CoCreateInstance` à chaque requête tant que l'erreur persiste. Depuis la suite de ME-12, l'instance de chaque fil (`[ThreadStatic]`) est relâchée par ce fil à toute sortie de `Run` : arrêt, abandon suivi d'un retour tardif, initialisation en échec ou qui lève (`workerExit`, branché sur `ReleaseAutomation`) ; un ouvrier abandonné ne relâche jamais l'instance de son remplaçant (tests `Ouvrier_arrete_libere_son_instance_sur_son_propre_fil`, `Ouvrier_abandonne_qui_revient_libere_son_instance_et_pas_celle_du_remplacant`, `Ouvrier_dont_l_initialisation_echoue_ou_leve_libere_aussi`).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T5.1 | S0 | `Query` | `clock >= _retryAfter` | S1 puis S2 | nouveau `Thread` de la génération courante ; `_wake.Set()` | `SecureInputProbe.cs:67-86` |
| T5.2 | S0 | `Query` | `clock < _retryAfter` | S0 | attend 30 ms puis rend `true` | `:79,87-93` |
| T5.3 | S1 | `_wake` | `_stopping == false` et génération courante | S2 | lit `_requested` ; `_busySince = clock` | `:103-111` |
| T5.4 | S2 | `_query` retourne | génération courante | S1 | `_busySince = 0` ; `_result = Result(id, secure)` ; `_done.Set()` ; levée tardive si `clock > deadline && previous.Secure != secure` | `:112-132` |
| T5.5 | S2 | `_query` lève | `catch` | S1 | `secure = true` | `:113-114` |
| T5.6 | S0/S1 | `_initialize` rend `false`, ou exception | génération courante | S0 | `_retryAfter = now + 1000` ; `workerExit` relâche l'instance du fil | `:100,135-143` |
| T5.9 | S2 | `Query` | `clock - _busySince >= abandonAfter` (2 s) et `_abandoned < 3` | S4 (l'ancien) ; S0 puis S1/S2 (le nouveau) | `_generation++`, `_abandoned++`, `_worker = null` ; nouveau `Thread` ; la requête rend `true` tant que le nouveau n'a pas répondu | `:70-84` |
| T5.10 | S4 | `_query` retourne ou lève | génération périmée | fin du fil | `_abandoned--` ; ni `_result`, ni `_done`, ni `ResultChangedLate`, ni `_retryAfter` ; puis `workerExit` sur ce fil : l'instance UIA de l'ouvrier abandonné est relâchée | `:116-123,136-143` |
| T5.11 | S2 | `Query` | `clock - _busySince >= abandonAfter` et `_abandoned == 3` | S2 | aucun nouveau fil ; la requête rend `true` | `:70-71` |
| T5.7 | tout | `Dispose` | — | S3 | `_wake.Set()`, `Join(100)` ; l'ouvrier qui s'arrête relâche son instance (`workerExit`) | `:82-89` |
| T5.8 | tout | `Query` | `_stopping` | S3 | rend `true` | `:37` |

#### Temporisations et réessais

Délai de requête 30 ms (`SecureInputDetector.cs:28`), reprise de l'ouvrier après 1 s (`retryDelay`, `:141`), relance du snapshot sécurisé 1 s côté `ForegroundMonitor`. Abandon d'un ouvrier bloqué : 2 s (`DefaultAbandonAfter`, `SecureInputProbe.cs:41`), soit deux relances sécurisées de 1 s et plus de quatre fois la durée « plusieurs centaines de ms » d'un appel UIA lent mais vivant ; injectable (`abandonAfter`) avec l'horloge (`clock`) pour les tests. Borne : 3 ouvriers abandonnés bloqués au plus (`MaxAbandonedWorkers`), donc 4 fils. `Dispose` n'est jamais appelé sur la sonde statique (vie du processus).

#### États ou transitions invalides atteignables

C2.2 corrigé (ticket ME-12) : invariant rétabli, test `MachinesEtatsLot2Tests.Ouvrier_bloque_sur_une_fenetre_ne_garde_pas_une_autre_fenetre_securisee_indefiniment`. Suite : l'instance UIA de chaque ouvrier (arrêté ou abandonné) est relâchée à sa sortie, tests `Ouvrier_arrete_libere_son_instance_sur_son_propre_fil`, `Ouvrier_abandonne_qui_revient_libere_son_instance_et_pas_celle_du_remplacant` et `Ouvrier_dont_l_initialisation_echoue_ou_leve_libere_aussi` (couture `workerExit`), `Le_detecteur_branche_ReleaseAutomation_sur_la_sortie_de_l_ouvrier` (branchement du détecteur, texte source).


### Frontières avec les autres lots

- **TrayApplication (hôte)** : possède `_activationConsent`, `_enabled`, `_pauseUntilUtc`, `_suspendedForCompatibility`, `_appliedSuspendReason`, `_wasEnabledBeforeAutoDisable`, `_announcementState`, `ApplyHookState`, `OnForegroundChanged`, `ClassifySuspensionTransition`, tous les minuteries et `Cleanup`. Ce lot n'en documente que les gardes qui commandent le hook et le watchdog (`ProbeHookSilence`, `ReinstallHook`, `ProbeRemoteHost`). `Cleanup` ne tue ni `TIMER_FOREGROUND_WATCHDOG`, ni `TIMER_REMOTE_HOST_PROBE`, ni `TIMER_REHOOK*` (`TrayApplication.cs:2336-2339`), mais tous testent `_hook`/`_foregroundMonitor` à `null` : sans effet prouvé.
- **KeyMapper (moteur)** : consomme `GetEmitContext`, `IsSecureInput`, `KeyDisplaySuppressed` (`KeyMapper.cs:81,90-92,935,1021,1092,1152`), `ProcessKey`, `TrackModifiers`, `TrackPassThroughKey`, `SyncState`, `RefreshForegroundContext` (`:145-156`). Ce que devient une frappe quand `GetEmitContext` refuse (fenêtre périmée, F7, mode `DisabledAntiCheat`) est dans son lot.
- **ConfigManager / UsageStats** : journal (`LogCompatCriticalEvent` synchrone, `LogCompatEventDeferred` différé), `GetCompatibilityOverride`.
- **Fenêtres UI** (clavier virtuel, recherche, leçons, tutoriel) : abonnées à `RawKeyDown`, `SearchRequested`, `VirtualKeyboardRequested`, `LayoutMayHaveChanged` ; les abonnés s'exécutent dans le rappel du hook (fil principal, sans pompe attendue).
- **MaintainableLayerManager / InputRecovery** : lisent `GetAsyncKeyState` et l'identité de processus ; `InputRecovery.cs:37` et `KeyMapper.cs:480-482` consomment des bits de `GetAsyncKeyState` partagés avec M2.2.
- **Programme / instance unique** (`Program.cs:36-91`) : deux instances du processus (autre session) : mutex local/global, hors de ce lot ; chaque instance poserait son hook.


### Constats

#### C2.1 — La sonde de silence réinstalle le hook toutes les ~4 s pendant la frappe dans une fenêtre de niveau d'intégrité supérieur

Catégorie : réessai. Gravité faible à moyenne, confiance moyenne (dépend du comportement UIPI de Windows, non vérifiable par lecture).

Scénario :
1. L'application tourne en niveau d'intégrité moyen, activée, hors pause, hors suspension (`ShouldProcessHook` vrai).
2. L'utilisateur lance un terminal ou un installeur « en administrateur » et tape (le texte de l'application, `src/Localization/L.Tray.cs:202`, dit lui-même qu'elle ne peut pas y agir).
3. Windows ne livre pas ces frappes au hook `WH_KEYBOARD_LL` d'un processus moins privilégié (UIPI) : `_lastCallbackTicks` n'avance pas. `GetAsyncKeyState` voit les frappes : `AnyKeyPressedSinceLastPoll()` est vrai.
4. Sonde n° 1 : `keyPressObserved && lastCallbackTicks < windowStart` -> série à 1. Sonde n° 2 (2 s plus tard) : série à 2 -> `Observe` rend `true`.
5. `ProbeHookSilence` écrit `HookSilentlyDetached` dans `error.log` (écriture disque synchrone sur la pompe, qui est aussi le fil du hook : elle retarde le hook, sans course ni parallélisme) et appelle `ReinstallHook`. La cause persiste : la série repart de 0 et recommence, sans plafond ni temporisation croissante.

Preuves :
- `src/TypingEngine.Windows/HookSilenceWatchdog.cs:60-71` : `if (!keyPressObserved || lastCallbackTicks >= windowStartTicks) { _silenceStreak = 0; return false; } _silenceStreak++; if (_silenceStreak < _consecutiveProbes) return false; _silenceStreak = 0; ReinstallCount++; return true;`
- `src/TrayApplication.cs:1153-1163` : `bool keyPressObserved = KeyboardHook.AnyKeyPressedSinceLastPoll(); if (!_hookSilence.Observe(...)) return; ConfigManager.LogCompatCriticalEvent("HookSilentlyDetached", ...); ReinstallHook(nudgeForeground: false);`
- `src/TrayApplication.cs:1143` : seule exclusion, `if (_hook == null || !ShouldProcessHook)` ; aucune exclusion pour une fenêtre élevée (aucun appel à l'intégrité ou à l'élévation dans le dépôt : recherche `elevat|integrity|TokenElevation` vide hors commentaires UIPI de `SendInput`).
- `src/ConfigManager.cs:989-995` : `LogCompatCriticalEvent` écrit sur disque sans file.

Impact : une ligne de journal critique toutes les ~4 s de frappe en fenêtre élevée, une rotation unique à 5 Mo qui écrase `error.log.old` (`ConfigManager.cs:1038-1060`) et donc efface les diagnostics plus anciens ; un `SetWindowsHookEx` / `UnhookWindowsHookEx` toutes les 4 s ; une écriture disque synchrone qui retarde la pompe et donc le hook (latence seulement, aucune course). Perte de frappe à la réinstallation : prouvée sur bureau réel le 17/11/2026 (une touche adressée à l'ancien hook partait non remappée quand il était décroché), corrigée dans `KeyboardHook.Reinstall` (frappes en attente traitées avant de décrocher, doublon écarté ; test M2_1_I2 vert).

Ce que j'ai vérifié pour l'écarter : aucune garde d'élévation ; `Suspend()` n'est appelé que hors `ShouldProcessHook` ; les tests ne couvrent que la décision pure (`HookSilenceWatchdogTests.cs:1-12`, qui dit que le câblage n'est pas couvert). Incertitude : si `GetAsyncKeyState` est aussi filtré par UIPI pour ces fenêtres, il n'y a pas de faux positif.

Tests existants : `src/TypingEngine.Windows.Tests/HookSilenceWatchdogTests.cs` (décision pure, jamais plafond).

Esquisse de test : étendre `HookSilenceWatchdog` (ou une petite politique pure appelée par `ProbeHookSilence`) pour plafonner : après un déclenchement suivi d'un nouveau silence immédiat (réinstallation inutile), temporisation croissante ; test dans `HookSilenceWatchdogTests` : N appels `Observe(true, 0, windowStart)` ne produisent qu'un nombre borné de `true` en N sondes, et une réinstallation suivie d'un rappel remet la politique à zéro. Aucune fenêtre réelle : la classe n'a aucun appel système.

#### C2.2 — Un ouvrier UIA unique bloqué rend « sécurisé » le premier plan de toutes les applications (le temps du blocage)

Catégorie : concurrence. Gravité faible, confiance basse (nécessite un fournisseur UIA bloqué, durée dépendante du délai de transaction UIA).

Scénario :
1. L'utilisateur donne le focus à une application dont le fil UI est figé (ex. navigateur gelé). `Recompute` -> `IsWindowPasswordField` -> `Probe.Query` : l'ouvrier entre dans `_query` (appel COM cross-process) et y reste (S2).
2. L'utilisateur bascule vers le Bloc-notes : `Recompute` relance `Query` ; l'ouvrier est occupé, un seul ouvrier n'est jamais multiplié ; la requête expire à 30 ms et rend `true`.
3. `secureInput = true` dans le snapshot du Bloc-notes : `AdvancedFeaturesSuppressed` vrai, recherche et clavier virtuel refusés avec la bulle « saisie sécurisée », affichage des touches coupé. `IsSnapshotStale` relance un `Recompute` chaque seconde, chaque relance bloquant le fil principal (donc le hook) 30 ms.
4. Ce dure tant que l'appel COM bloqué n'est pas revenu ; à son retour l'ouvrier reprend la dernière requête (`SecureInputProbe.cs:65`), donc les requêtes suivantes ne sont pas privées définitivement de résultat. Le résultat tardif éventuel ne déclenche `ResultChangedLate` que s'il diffère du dernier résultat, quelle que soit sa fenêtre.

Preuves :
- `SecureInputProbe.cs:40-45` : `if (_worker?.IsAlive != true && Environment.TickCount64 >= _retryAfter) { _worker = new Thread(Run) ...}` (aucun deuxième ouvrier).
- `SecureInputProbe.cs:53` : `return result.Request != request.Id || result.Secure;`
- `SecureInputProbe.cs:72` : `if (Environment.TickCount64 > request.Deadline && previous.Secure != secure)` (comparaison au précédent résultat, pas à la même fenêtre).
- `RealWin32Api.cs:134-138` : la sonde UIA est appelée pour tout focus non `ES_PASSWORD`, donc pour toute application.
- `ForegroundMonitor.cs:135` : `(snap.SecureInput && _clock() >= _secureRetryAfter)`.

Impact : fonctions avancées (recherche, clavier virtuel, couches maintenables) indisponibles dans les autres applications pendant le blocage ; coût de 30 ms par seconde sur le fil du hook. Le remappage lui-même n'est pas coupé par `SecureInput` dans ce lot.

Ce que j'ai vérifié pour l'écarter : le comportement pendant le blocage est voulu (fail-closed), fixé alors par un test de la sonde, réécrit depuis par le correctif (`Worker_bloque_n_est_ni_multiplie_ni_abandonne_avant_le_delai`, voir le statut ci-dessous) et ne doit pas être présenté comme invalide ; seule la durée du blocage, non bornée côté ouvrier, reste en question ; la durée réelle d'un blocage UIA n'est pas lisible dans le code.

Tests existants : `SecureInputProbeTests.cs` (ouvrier bloqué, un seul ouvrier, échec initialisation).

Esquisse de test : avec `SecureInputProbe(() => true, query, timeout: 50)` où `query` bloque sur la fenêtre 1 puis répond vite sur la fenêtre 2 ; vérifier qu'une requête pour la fenêtre 2 ne reste pas sécurisée indéfiniment (par exemple en abandonnant l'ouvrier bloqué et en démarrant un second ouvrier au bout d'un délai borné). Le test existant montre la couture (délégué `_query` injecté).

Statut : corrigé (ticket ME-12, décision d'Antoine du 16 novembre 2026 : « Abandonner la détection bloquée après un délai »). Un ouvrier occupé depuis 2 s dans une requête est abandonné ; un nouvel ouvrier repart (nouvelle génération) ; le retour COM tardif de l'ancien est ignoré (ni résultat, ni `ResultChangedLate`) ; au plus 3 ouvriers abandonnés restent bloqués, donc 4 fils. Pendant le blocage et tant que le nouvel ouvrier n'a pas répondu, la sonde reste « sécurisée » (fail-closed, le choix que le code prend déjà quand il ne sait pas). Le test existant `Worker_bloque_ne_reutilise_pas…` affirmait l'ancien comportement : il est réécrit (`Worker_bloque_n_est_ni_multiplie_ni_abandonne_avant_le_delai`) et rendu déterministe par une horloge injectée. Test du constat : `MachinesEtatsLot2Tests.Ouvrier_bloque_sur_une_fenetre_ne_garde_pas_une_autre_fenetre_securisee_indefiniment`.

#### C2.3 — Premier plan absent traité comme `Default/None` (fail-open) alors que l'inconnu est traité fail-closed

Catégorie : bord temporel. Gravité faible, confiance basse (fenêtre de temps courte, non reproduite).

Scénario :
1. Une transition de fenêtre ou la destruction de la fenêtre active laisse `GetForegroundWindow()` à 0 un court instant ; un événement de focus ou la minuterie de 250 ms provoque un `Recompute` à cet instant.
2. `TryGetWindowProcess(0, ...)` rend `false` avec `pid = 0` ; `ResolveState` renvoie `(Default, None)` : aucune suspension, alors que le commentaire adjacent choisit la suspension quand un PID existe sans nom (`ForegroundMonitor.cs:301-305`).
3. Snapshot avec `Window = 0`. `GetEmitContext` compare `snap.Window != GetForegroundWindow()` : `0 == 0`, la garde passe, il rend `(Default, IntPtr.Zero)`.
4. Une frappe arrivant tant que `GetForegroundWindow()` vaut encore 0 est traitée en mode `Default` (injection Unicode, `KeyMapper.cs:1051` : `Hkl == 0` écarte la voie NativeCombo). Dès qu'une fenêtre réelle est active, `snap.Window (0) != GetForegroundWindow()` refuse toute émission : le laps « avant l'événement qui désigne la vraie fenêtre » n'existe pas.

Preuves :
- `ForegroundMonitor.cs:299-307` : `if (!hasFg) { if (pid != 0) return (DisabledAntiCheat, UnknownForeground); return (CompatibilityMode.Default, CompatibilitySuspendReason.None); }`
- `ForegroundMonitor.cs:115-117` : `if (!IsTrackingAvailable || snap == null || snap.Window != _api.GetForegroundWindow()) return (DisabledAntiCheat, IntPtr.Zero); return (snap?.Mode ?? Default, snap?.Hkl ?? IntPtr.Zero);`
- `RealWin32Api.cs:58` : `if (window == IntPtr.Zero) return false;`

Impact : un caractère émis en voie Unicode alors qu'aucune fenêtre n'est active (le système n'en désigne aucune) ; HKL nul. Écart de principe avec le fail-closed de l'inconnu, sans cible protégée atteignable.

Ce que j'ai vérifié pour l'écarter : `secureInput = !hasFg` (`:246`) coupe les fonctions avancées dans cet état ; une frappe pendant que le snapshot vaut `Window=0` suppose un `Recompute` exactement dans l'intervalle ; l'effet de `Hkl == 0` est tranché : `KeyMapper.cs:1051` (`mode == NativeCombo && hkl != 0`) donne la voie Unicode. Aucun test ne scripte `GetForegroundWindow() == 0` avec émission.

Tests existants : `Audit130MoteurTests.cs`, `ForegroundMonitorMockedTests.cs:171-204` (péremption), `ShellRaceSuspensionTests.cs`.

Esquisse de test : avec `MockWin32Api` dont le premier plan scripté est 0, `Recompute`, puis `GetEmitContext()` doit rendre `DisabledAntiCheat` (fail-closed) ; couture existante : `MockWin32Api.Scripted*` utilisé dans `SecureInputProbeTests.cs:17-20`.

#### C2.4 — `Recompute` attend sur un fil STA : pompe imbriquée possible pendant l'attente UIA

Catégorie : réentrance. Gravité faible, confiance basse (dépend du comportement de `WaitHandle.WaitOne` sous NativeAOT en STA, non prouvé par le code).

Scénario :
1. Un `EVENT_OBJECT_FOCUS` appelle `Recompute` (fil principal, STA), qui calcule le snapshot puis appelle `Probe.Query`.
2. `Query` attend `_done.WaitOne` jusqu'à 30 ms. Hypothèse non établie par le code : sur un fil STA, une attente de ce type pomperait des messages (COM / messages envoyés, voire des messages de fenêtre selon l'environnement d'exécution NativeAOT). Tout le scénario en dépend.
3. Si un message est livré pendant cette attente (un `WM_TIMER` du chien de garde 250 ms, un autre événement de focus), un deuxième `Recompute` s'exécute dans le premier. Le premier reprend ensuite et écrit un snapshot calculé avec l'ancien `window` par-dessus le plus récent.
4. L'écart est détecté ensuite par `IsWindowStale` / `IsSnapshotStale` et réparé en ≤ 250 ms ; entre-temps `GetEmitContext` refuse (fail-closed).

Preuves :
- `src/Program.cs:36` : `[STAThread]`.
- `SecureInputProbe.cs:87-91` : `while (Volatile.Read(ref _result).Request < request.Id) { ... if (remaining <= 0 || !_done.WaitOne((int)remaining)) break; }`
- `ForegroundMonitor.cs:218-253` : `window` capturé au début, snapshot écrit à la fin, aucune garde de réentrance (pas de drapeau « Recompute en cours »).

Impact : au pire un snapshot périmé pendant ≤ 250 ms (sans émission, fail-closed) ; pas de perte durable. Le rappel du hook lui-même peut aussi s'exécuter pendant cette attente si elle pompe les messages envoyés (le hook est livré ainsi), ce qui n'est pas non plus un défaut avéré.

Ce que j'ai vérifié pour l'écarter : aucune garde de réentrance dans `Recompute` ; je n'ai pas pu établir par le code que l'attente pompe. À confirmer par une mesure (journal d'imbrication de `Recompute`).

Tests existants : aucun sur la réentrance de `Recompute`.

Esquisse de test : `ForegroundMonitor` avec un `IWin32Api` de test dont `IsWindowPasswordField` appelle `monitor.Recompute()` (pompe imbriquée simulée) et change le premier plan entre-temps ; vérifier que le snapshot final décrit le dernier premier plan (suppose d'ajouter une garde ou un recalcul en fin de `Recompute` si une réentrance a eu lieu).

## Lot 3 — Application de zone de notification (activation, pause, suspension)

Fichiers du lot : `src/TrayApplication.cs`, `src/PauseSchedule.cs`, `src/PauseDurationDialog.cs`, `src/ToggleNotification.cs`, `src/TrayIcon.cs`, `src/LayoutConflictWindow.cs`. Le code fait foi ; les numéros de ligne sont ceux de la branche `claude/ag-machines-etats-130`.

### Modèle de fils du lot

- Fil UI unique : `Run()` boucle sur `GetMessage` (`src/TrayApplication.cs:658-726`) ; `WndProcCallback` (731-1127) sert les clics de l'icône, les minuteries `SetTimer` sur `_hWnd` et les `WM_APP_*`. Le callback du hook clavier bas niveau s'exécute sur ce même fil : il n'y a donc pas de course entre le hook et les minuteries, seulement de la réentrance (pompes imbriquées de `TrackPopupMenuEx`, de la boucle modale de `PauseDurationDialog` `while (!_done)` 102-119, de `MessageBox`, attentes `SendMessage` de `Shell_NotifyIcon`).
- Fils du pool : `ProbeRemoteHost` (1355, garde `Interlocked` sur `_remoteHostProbeRunning`, écrit `hook.RemoteSessionActive`), `UsageStats.FlushInBackground`, `ConfigManager.FlushInBackground`.
- Fils tiers qui ne touchent l'état que par `PostMessage` : activation COM de toast (RPC) vers `WM_APP_TOAST`, rappel `SecureInputDetector.ResultChangedLate` (fil UIA) vers `WM_APP_SECURE_RECHECK`.
- `AppLanguageChanged` est levé sur le fil UI. `ForegroundMonitor.Recompute()` lève `ForegroundChanged` de façon synchrone sur le fil principal.
- Identifiants de minuteries sur `_hWnd` : 9001-9003 (réinstallation du hook, 500 ms / 3 s / 8 s), 9004 (watchdog 60 s), 9005 (stats 5 min), 9006 (premier plan 250 ms), 9007 (sonde de silence 2 s), 9010 (simple clic), 9020 (conflit de disposition 100 ms), 9030 (pause 1 s), 9040 (bulle de démarrage 1500 ms), 9041 (notice de réinitialisation), 9050 (calme avant avis 5 s), 9060 (sonde hôte distant 5 s). Pas de collision constatée.

### M3.1 — Activation, pause et suspension (état du hook)

**Nature** : explicite.
**Fichiers** : `src/TrayApplication.cs`, `src/PauseSchedule.cs`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_activationConsent` | bool | `src/TrayApplication.cs:142-184` (champs), `ActivateWithConsent` 1382 | UI | fil unique |
| `_enabled` | bool | initialisé depuis `ConfigManager.ActivationConsent` (154), posé à vrai par `ActivateWithConsent` (1392), inversé par `OnToggle` (1972-2063, 2025) | UI | fil unique |
| `_pauseUntilUtc` | `DateTimeOffset?` | 142-184, `StartPauseUntil`, `StopPause` (2068-2144) | UI | fil unique |
| `_suspendedForCompatibility` | bool | 142-184, `ApplyCompatibilityOverride` 3117 | UI | fil unique |
| `_appliedSuspendReason` | enum | 142-184, transitions 2470-2506 | UI | fil unique |
| `_wasEnabledBeforeAutoDisable` | bool | 142-184 | UI | fil unique |

#### États

| état | prédicat |
|---|---|
| Sans consentement | `!_activationConsent` |
| Actif | `_activationConsent && _enabled && !IsPaused && !_suspendedForCompatibility` (`ShouldProcessHook`, 559-561) |
| Désactivé | `_activationConsent && !_enabled && !IsPaused && !_suspendedForCompatibility` |
| En pause | `IsPaused => _pauseUntilUtc.HasValue` (559-561) |
| Suspendu (compatibilité) | `_suspendedForCompatibility` |

Ces prédicats ne forment pas une partition : « Désactivé » exclut la pause et la suspension, mais « En pause » n'exclut pas la suspension. Pause et suspension de compatibilité coexistent : `CanStartPause` ne regarde que `_enabled` et `_pauseDialogOpen` (2072) et le menu propose les pauses pendant une suspension.

`ShouldBlockHookCompletely => !_activationConsent || IsPaused || _suspendedForCompatibility`.

#### Invariants

- I1 `IsPaused => _enabled` : garanti par `StartPause` qui exige `_enabled` et par `OnToggle` qui efface la pause avant de basculer (2068-2144, 1972-2025).
- I2 `IsPaused => minuterie 9030 active` : garanti, posée dans `StartPauseUntil`, tuée dans `StopPause`, `OnToggle` et `Cleanup` (2068-2144, 2331-2373).
- I3 Une seule écriture de l'état du hook : garanti par `ApplyHookState` (585-610), seul écrivain de `PassThroughAll`, `ShortcutsWhilePassThrough`, `Enabled` et `EmissionPaused` (grep confirmé).
- I4 Le menu et la commande exécutée correspondent au même état : garanti depuis 1.3.0 (17 novembre 2026, C3.2 corrigé). `ShowContextMenu` retient l'état affiché (`MenuShownState`) ; `IDM_TOGGLE` et `IDM_PAUSE` ne s'appliquent que si l'état visé n'a pas changé pendant le menu (`MenuCommandStillApplies`), sinon l'intention affichée est déjà atteinte et la commande ne fait rien. Test : `Regles130TrayEtatTests.M3_1_I4_CommandeDuMenu_AppliqueeALEtatAffiche`.
- I5 Le raccourci global ne fait pas de travail lourd dans le hook : garanti depuis 1.3.0 (17 novembre 2026, C3.4 corrigé). `OnToggleShortcut`, câblé sur `KeyMapper.ToggleRequested`, ne fait que poster `WM_APP_TOGGLE` ; `ApplyToggleShortcut` (reprise de pause ou `OnToggle`) tourne dans la boucle de messages. Test : `Regles130TrayEtatTests.M3_1_I5_RaccourciGlobal_NeFaitPasDeTravailLourdDansLeRappelDuHook`.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1 | Sans consentement | consentement | — | Actif | `AcceptActivation`, `ApplyHookState` | 1382 |
| T2 | Actif | bascule (menu `IDM_TOGGLE`, Ctrl+Maj+Verr.Maj ; le double-clic ne bascule pas `_enabled`) | — | Désactivé | `ApplyHookState`, icône, `ToggleNotification` | 1972-2063 |
| T2b | En pause | bascule (menu `IDM_TOGGLE`, Ctrl+Maj+Verr.Maj) | — | Désactivé | `OnToggle` efface `_pauseUntilUtc`, tue la minuterie 9030, inverse `_enabled` | 2019-2025 |
| T3 | Désactivé | bascule | — | Actif | idem | 1972-2063 |
| T3b | Actif ou En pause ou Désactivé | double-clic sur l'icône | — | inchangé | sans accord : ouvre l'accueil ; sinon, si `ShouldProcessHook`, bascule le clavier virtuel, sinon bulle d'indisponibilité | 743-751 |
| T4 | Actif | `StartPause` | `CanStartPause`, `_enabled` | En pause | `_pauseUntilUtc`, minuterie 9030, `ApplyHookState` | 2068-2144 |
| T5 | En pause | `WM_TIMER` 9030 | `UtcNow >= _pauseUntilUtc` | Actif | `StopPause(expired: true)` | 990-993 |
| T6 | En pause | IDM_PAUSE / raccourci | — | Actif | `StopPause(expired: false)` | 827-831, 1930 |
| T7 | Actif | `ForegroundChanged` | raison de suspension | Suspendu | transitions `ClassifySuspensionTransition` | 2949-3019, 2470-2506 |
| T8 | Suspendu | `ForegroundChanged` | plus de raison | Actif ou Désactivé | restauration via `_wasEnabledBeforeAutoDisable` | 2470-2506 |

#### Temporisations et réessais

Pause : échéance en UTC murale (non monotone), tick 1 s, comparaison `UtcNow >= _pauseUntilUtc`. « Jusqu'à demain » = prochain 8 h local (`PauseSchedule.NextMorning`). Durée bornée 1..1439 min par `Clamp`. Aucun réessai.

#### États ou transitions invalides atteignables

C3.2, C3.4, C3.7

### M3.2 — Suspension de compatibilité et annonces

**Nature** : explicite (fonctions pures).
**Fichiers** : `src/TrayApplication.cs`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_appliedSuspendReason` | enum | 2470-2506 | UI | fil unique |
| `CompatibilityAnnouncementState` | struct `{AnnouncedReason, AnnouncedApplication, ResumeOwed}` | `NextAnnouncement` 3057 | UI | fil unique |

#### États

`SuspensionTransition` : None / Enter / ReasonChanged / Leave (`ClassifySuspensionTransition`). `CompatibilityAnnouncement` : None / Suspension / Resumed.

#### Invariants

- I1 Une annonce de reprise n'est due que si une suspension a été annoncée (`ResumeOwed`) : garanti par `NextAnnouncement` (3057), joué par séquences dans `ShellHopBalloonTests.cs`.
- I2 Pas d'annonce pour un premier plan transitoire du shell : garanti côté `ForegroundMonitor.IsTransientShellForeground` (lot compatibilité).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| S1 | None | nouvelle raison | — | Enter | suspension, bulle ou toast | 2470-2506 |
| S2 | Enter | raison différente | — | ReasonChanged | bulle de sécurité (`ShowSecurityBalloon` 2933) | 2470-2506 |
| S3 | Enter ou ReasonChanged | raison disparue | — | Leave | reprise | 2470-2506 |

#### Temporisations et réessais

Aucun. Un saut de premier plan court est absorbé par `NextAnnouncement`. Tests : `SuspensionTransitionTests.cs`, `PreConsentSuspensionTests.cs`.

#### États ou transitions invalides atteignables

Aucun constat propre.

### M3.3 — Bulles et sollicitations à usage unique

**Nature** : implicite.
**Fichiers** : `src/TrayApplication.cs`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_pendingBalloon` | `PendingBalloonKind` (None/Review/Training/Announcement/AutoStart) | 142-184 | UI | fil unique |
| `_reviewTargetIsStore` | bool | 2697-2792 | UI | fil unique |
| `_reviewPromptDeferred` | bool | 142-184 | UI | fil unique |

#### États

None, Review, Training, Announcement, AutoStart : le type de la dernière bulle demandée, utilisé pour router le clic sur la bulle.

#### Invariants

- I1 Les bulles et nudges respectent `NotificationsEnabled` : garanti (portes présentes).
- I2 Une sollicitation à usage unique n'est jamais montrée deux fois : garanti (règle réécrite le 17 novembre 2026, décision d'Antoine : C3.5 est le comportement voulu). Elle est marquée faite avant l'affichage, même si le shell refuse la bulle : mieux vaut une annonce perdue qu'une annonce répétée. Test : `Regles130TrayBullesTests.M3_3_I2_SollicitationAUsageUnique_JamaisDeuxFois`. Points de marquage : `ConfigManager.SetChallengeAnnounceDone(); // avant l'affichage` (1555), `AutoStartNudge.MarkPromptShown();` (1597), `ConfigManager.RecordReviewPromptShown(signals.Today);` (2769).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| B1 | None | `MaybeShowReviewPrompt` | `ReviewPromptGate` | Review | marque l'essai, `ShowBalloon` | 2769, 2877 |
| B2 | None | `MaybeShowChallengeAnnouncement` | pas déjà annoncé | Announcement | marque avant affichage | 1555 |
| B3 | None | `MaybeShowAutoStartNudge` | décision pure | AutoStart | `MarkPromptShown` puis bulle | 1595-1604 |
| B4 | tout | clic sur la bulle | — | None | ouvre la cible | 731-1127 |

#### Temporisations et réessais

Chaîne de démarrage 328-332 : `else if (!MaybeShowReviewPrompt(Startup) && !MaybeShowChallengeAnnouncement() && !MaybeShowAutoStartNudge()) { SetTimer(... TIMER_STARTUP_BALLOON ...)`. Le commentaire 526-531 dit que le shell jette en silence une bulle demandée dans la foulée du `NIM_ADD` (237), d'où le délai de 1500 ms, mais seul le chemin « Actif » en profite.

#### États ou transitions invalides atteignables

C3.5.

### M3.4 — Dialogue de durée de pause

**Nature** : implicite.
**Fichiers** : `src/PauseDurationDialog.cs`, `src/TrayApplication.cs`, `src/PauseSchedule.cs`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_pauseDialogOpen` | bool | `ShowPauseDialogAndStart` (2068-2144) | UI | fil unique |
| `_pauseSerial` | int | idem | UI | fil unique |
| `_done`, `_result`, `_durationValid`, `_fieldBusy` | champs du dialogue | `src/PauseDurationDialog.cs` | UI | fil unique |

#### États

Fermé, Ouvert (boucle modale), Résultat disponible.

#### Invariants

- I1 Pas de double ouverture : garanti par `_pauseDialogOpen`.
- I2 Un résultat périmé est écarté : garanti par `_pauseSerial` et `ShouldApplyDialogPause`.
- I3 Quitter pendant le dialogue ne bloque pas : garanti, owner détruit, `WM_QUIT` reposté par la boucle (108-112), owner réactivé dans le `finally`.
- I4 L'abonnement `AppLanguageChanged` est retiré à la fermeture : garanti dans `Dispose`.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| D1 | Fermé | IDM_PAUSE | `!IsPaused` | Ouvert | `_pauseDialogOpen = true`, owner désactivé | 827-831 |
| D2 | Ouvert | OK | durée valide | Résultat | `_done = true` | 102-119 |
| D3 | Ouvert | Échap, WM_CLOSE | — | Résultat annulé | `_done = true` | 102-119 |
| D4 | Résultat | retour de la boucle | `ShouldApplyDialogPause` | Fermé | `StartPause` ou rien | 2068-2144 |

#### Temporisations et réessais

Aucun. La pause expire éventuellement pendant que le dialogue est ouvert : `ShouldApplyDialogPause` gère ce cas.

#### États ou transitions invalides atteignables

C3.2

### M3.5 — Fenêtre de conflit de disposition

**Nature** : implicite.
**Fichiers** : `src/TrayApplication.cs`, `src/LayoutConflictWindow.cs`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_layoutPopupOpen` | bool | `ShowLayoutConflictPopup` 1288-1315 | UI | fil unique |
| `_layoutConflictWindow` | `LayoutConflictWindow?` | 1288-1315 | UI | fil unique |
| `_pendingOnboardingShow` | bool | 142-184 | UI | fil unique |

#### États

Fermée, Ouverte.

#### Invariants

- I1 `_layoutPopupOpen <=> fenêtre vivante` : non garanti (C3.3), `_layoutPopupOpen = true` est posé avant la construction, sans try/finally.
- I2 Le popup n'apparaît que s'il y a vraiment double remappage : non garanti (C3.7).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| L0 | Fermée | démarrage : `CheckSystemLayout` appelé depuis le constructeur | layout système déjà AZERTY Global | Ouverte | `ShowLayoutConflictPopup(isAtStartup: true)` ; si l'accueil est dû, `_pendingOnboardingShow` est posé (302-312) | 1250-1255, 302-312 |
| L1 | Fermée | `LayoutMayHaveChanged` puis minuterie 9020 | conflit détecté | Ouverte | `SetForegroundWindow`, nouvelle fenêtre | 1261-1277, 1288-1315 |
| L2 | Ouverte | « quitter » | — | Fermée | `_onQuit` | `src/LayoutConflictWindow.cs` |
| L3 | Ouverte | « garder » ou WM_CLOSE | — | Fermée | `_onKeep` ; si `_pendingOnboardingShow`, le remet à faux et appelle `ShowOnboardingNow()` | idem, 1304-1312 |

#### Temporisations et réessais

Minuterie 9020 à 100 ms. Pas de réessai.

#### États ou transitions invalides atteignables

C3.3, C3.7.

### M3.6 — Icône, infobulle et notification de bascule

**Nature** : implicite.
**Fichiers** : `src/TrayApplication.cs`, `src/TrayIcon.cs`, `src/ToggleNotification.cs`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_nid` (NOTIFYICONDATA) | struct | constructeur 237, `UpdateIcon` 2518 | UI | fil unique |
| `TooltipPublisher._shown` | état d'infobulle | 2542 | UI | fil unique |
| `_stateRefreshQueued` | int | `OnStateChanged` 2191-2216 | UI et autres | `Interlocked` |
| `TrayIcon.LogoCache` | `Dictionary<int, uint[]?>` | `src/TrayIcon.cs:22` | UI | `lock (LogoCache)` |
| `ToggleNotification._currentActivated`, `_hWnd` | bool, handle | `src/ToggleNotification.cs:27` | UI | fil unique |

#### États

Icône absente, présente ; fenêtre de bascule masquée, visible (2 s).

#### Invariants

- I1 L'icône existe tant que l'application tourne : garanti depuis le correctif de C3.6 (16 novembre 2026 : `AddTrayIcon`, réessai toutes les 2 s, 15 fois au plus, puis au prochain `TaskbarCreated`). Avant : `Win32.Shell_NotifyIconW(NIM_ADD, ref _nid); ReinstallHook();` (1109-1110, retour ignoré).
- I2 `ToggleNotification` se ferme 2 s après la dernière bascule : garanti (`KillTimer` puis `SetTimer 2000`, `src/ToggleNotification.cs:104-105`).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| I-1 | absente | constructeur | — | présente | NIM_ADD | 237 |
| I-2 | présente | TaskbarCreated | — | présente (réajoutée) | NIM_ADD sans contrôle | 1109 |
| I-3 | présente | changement d'état | — | présente | NIM_MODIFY | 2518, 2580 |
| N-1 | masquée | `Show` | — | visible | minuterie 2 s | `src/ToggleNotification.cs:66-106` |
| N-2 | visible | `WM_TIMER` | — | masquée | `ShowWindow(0)` | 118-124 |

#### Temporisations et réessais

Fermeture à 2 s, réarmée à chaque `Show`. Un échec de rendu du logo est mis en cache à `null` (`LogoCache[size] = pixels`, 44), l'icône de repli texte prend le relais ; jugé trop rare pour un constat.

#### États ou transitions invalides atteignables

C3.4, C3.6

### M3.7 — Clics sur l'icône

**Nature** : implicite.
**Fichiers** : `src/TrayApplication.cs`.

#### Variables d état

Minuterie 9010 (`TIMER_SINGLECLICK`), armée à `GetDoubleClickTime()`. Pas de variable.

#### États

Repos, Simple clic en attente.

#### Invariants

- I1 Un double-clic ne produit que l'action du double-clic : non garanti (C3.1).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| K1 | Repos | `WM_RBUTTONUP` | — | Repos | `ShowContextMenu` | 737-775 |
| K2 | Repos | `WM_LBUTTONUP` | — | Simple clic en attente | `SetTimer 9010` | 742 |
| K3 | Simple clic en attente | `WM_LBUTTONDBLCLK` | — | Repos | `KillTimer 9010`, action du double-clic | 745 |
| K4 | Simple clic en attente | `WM_TIMER 9010` | — | Repos | action du simple clic (menu) | 973-1052 |
| K5 | Repos | `WM_LBUTTONUP` après K3 | — | Simple clic en attente | `SetTimer 9010` | 742 |

#### Temporisations et réessais

`GetDoubleClickTime()` (500 ms par défaut).

#### États ou transitions invalides atteignables

C3.1

### M3.8 — Cycle de vie et arrêt

**Nature** : implicite.
**Fichiers** : `src/TrayApplication.cs`.

#### Variables d état

`_cleaned` (bool), minuteries du hook 9001-9007, `_hookId` côté `KeyboardHook`.

#### États

En marche, Nettoyée.

#### Invariants

- I1 `Cleanup` est idempotent : garanti par `_cleaned` (2331-2373).
- I2 `ReinstallHook` ne perd aucune frappe : garanti, nouveau hook posé avant décrochage de l'ancien, même fil (`KeyboardHook.cs` Reinstall 164).
- I3 La veille, la reprise et la fin de session réinstallent ou arrêtent proprement : lignes 1059-1098.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| V1 | En marche | WM_ENDSESSION, Quitter, `WM_DESTROY` | `!_cleaned` | Nettoyée | minuteries tuées, icône retirée, hook décroché | 2331-2373 |
| V2 | En marche | silence du hook (sonde 9007) ou watchdog 9004 | — | En marche | `ReinstallHook` | 1137, 1178 |

#### Temporisations et réessais

Réinstallation : 500 ms, 3 s, 8 s (9001-9003), watchdog 60 s, sonde de silence 2 s.

#### États ou transitions invalides atteignables

C3.4

### Constats

- C3.4 (moyenne) : le raccourci global exécute `OnToggle` et `StopPause` dans le callback du hook (`src/TypingEngine.Windows/KeyboardHook.cs:359`, `src/TypingEngine.Windows/KeyMapper.cs:467`, `src/TrayApplication.cs:1930-2062`), avec `Shell_NotifyIcon`, GDI+ et création de fenêtre. Corrigé en 1.3.0 (17 novembre 2026) : bascule postée (`WM_APP_TOGGLE`).
- C3.1 (faible) : double-clic sur l'icône suivi d'un menu contextuel tardif (`src/TrayApplication.cs:737-751`).
- C3.6 (faible à moyenne) : `NIM_ADD` sans contrôle de retour ni réessai (237, 1109).
- C3.2 (faible) : commande de menu relative à un état périmé (827-831, 1758-1761, 990-993, 1972-2025). Corrigé en 1.3.0 (17 novembre 2026) : état affiché retenu (`MenuShownState`).
- C3.5 (faible) : bulles à usage unique consommées avant l'affichage (328-329, 1555, 1597, 2769). Comportement voulu (17 novembre 2026, décision d'Antoine) : règle M3.3 I2 réécrite « jamais deux fois ».
- C3.7 (faible) : popup de conflit sans porte d'état (1288-1315).
- C3.3 (faible) : `_layoutPopupOpen` jamais remis à faux si la construction lève (1291).

Détail des scénarios, preuves et pistes de correctif : voir la sortie structurée du lot.

### Frontières avec les autres lots

- `KeyboardHook`, `KeyMapper` (moteur) : `HandleToggleShortcut`, `EmissionPaused`, `SyncState`, `ClearPassedThroughKeys`, `RemoteSessionActive`, `Install` / `Reinstall` (`src/TypingEngine.Windows/KeyboardHook.cs:142,164`).
- `ForegroundMonitor`, `SecureInputDetector` (compatibilité) : `CurrentSuspendReason`, `IsTransientShellForeground`, `Recompute`, `ResultChangedLate` (fil UIA).
- `ConfigManager`, `UsageStats` (persistance) : `Flush`, `FlushInBackground`, `AcceptActivation`, `_loadFailed`.
- `ToastActivation` (toasts) : activation COM sur fil RPC ; un clic de toast application fermée lance une seconde instance qui meurt (`src/Program.cs`).
- `OnboardingWindow`, `SettingsWindow`, `LessonsWindow`, `CharacterSearch`, `VirtualKeyboard` : fenêtres dont le tray tient les références.
- `ReviewPromptGate`, `TrainingReminders`, `AutoStartNudge` : décisions pures.
- `Program.cs`, `RelaunchSignal` : mutex local/global, signal de relance.

## Lot 4 — Processus, instance unique et configuration persistée

Tous les chemins sont relatifs au dépôt (`src/...`). Le code fait foi ; les numéros de ligne sont ceux de la branche `claude/ag-machines-etats-130`.

### Modèle de fils du lot

| Fil | Ce qui y tourne dans ce lot | Source |
|---|---|---|
| Fil principal STA (`[STAThread] Main`) = pompe de messages = fil du rappel du hook bas niveau | `Program.Main` entier (mutex, signal de relance), construction puis `Run()` de `TrayApplication`, `WndProc` du tray, tous les setters/getters de `ConfigManager` appelés par les fenêtres, `AutoStart.Set` / `IsRegistered` / `WindowsState` (appels WinRT bloquants `.GetAwaiter().GetResult()`), `AutoStartNudge`, `Cleanup()`, `WM_QUERYENDSESSION` → `ConfigManager.Flush()` | `src/Program.cs:36`, `src/TrayApplication.cs:1085-1088`, `src/TrayApplication.cs:2360`. Le commentaire `ConfigManager.cs:1316` dit que l'écriture s'exécutait « sur le fil qui sert aussi le rappel du hook clavier ». |
| Pool de threads (`System.Threading.Timer`) | Écriture différée et nouveaux essais de config.json : `FlushInBackgroundCore` → `Flush` | `src/ConfigManager.cs:1392` `_saveTimer ??= new System.Threading.Timer(static _ => FlushInBackgroundCore(), ...)` |
| Pool de threads (`QueueUserWorkItem`) | `FlushInBackground` (changement de session WTS), `Log` (ajout dans error.log), vidage du journal différé | `src/ConfigManager.cs:1399-1400`, `926-936`, `1011` ; `src/TrayApplication.cs:1076` |
| Fil RPC/STA COM | `NotificationActivator.Activate` → événement `ToastActivation.Activated`. Le code suppose un fil RPC (commentaire `ToastActivation.cs:68`) ; comme `Main` est STA et enregistre la fabrique depuis ce fil, Windows peut aussi livrer l'appel via la pompe : non vérifiable à la lecture. Dans les deux cas le seul abonné ne fait que `PostMessageW` | `src/ToastActivation.cs:76-80`, `src/TrayApplication.cs:253-261` |
| Fil principal d'un AUTRE processus | Seconde instance : lit la langue (donc charge config.json) AVANT d'avoir tenté le mutex, puis `FindWindowW`/`PostMessageW` | `src/Program.cs:44`, `src/RelaunchSignal.cs:62-72` |

Verrous de `ConfigManager` : `_lock` (cache `_cache`, `_compatibilityCache`, `_dirty`, `_failedWrites`, `_loadFailed`, `_quarantineNoticePending`), `_fileLock` (une écriture disque à la fois). Ordre imposé : `_fileLock` puis `_lock`, garde `Monitor.IsEntered(_lock)` dans `Flush` (`ConfigManager.cs:1418`). `_logFlushLock` pour error.log (`ConfigManager.cs:896-904`). `PolicyManager._lock` (`PolicyManager.cs:92`).

Règle appliquée : le code de la pompe (hook, WndProc, setters des fenêtres) ne peut pas courir contre lui-même ; les seules vraies courses de ce lot sont (a) pompe contre minuterie du pool sur `ConfigManager`, (b) deux processus sur les mêmes fichiers, (c) retours COM. Les réentrances de pompe (MessageBox de `Program.Main` avant `app.Run`, `MessageBoxW` de `TrayApplication`) ne touchent pas l'état de ce lot, sauf le MessageBox de la seconde instance (M4.1) pendant lequel la minuterie de config.json de ce processus reste active.


### M4.1 — Instance unique, signal de relance, activation système

**Nature** : implicite (combinaison `isNew` / `isNewCrossSession` / `crossSession == null` / ligne de commande, plus l'existence de la fenêtre `AZERTYGlobal_Wnd`).
**Fichiers** : `src/Program.cs`, `src/RelaunchSignal.cs`, `src/RestartRegistration.cs`, `src/ProductIdentity.cs` (noms).

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `mutex` / `isNew` | `Mutex` nommé `Local\AZERTYGlobalSingleInstance.<SID>` | `Program.AcquireSingleInstance` | fil principal du processus | objet noyau nommé (atomique entre processus) ; libéré à la mort du processus ou au `using` |
| `crossSession` / `isNewCrossSession` | `Mutex?` nommé `Global\…` ; défaut `isNewCrossSession = true` | `Program.cs:66-76` | idem | idem ; `null` si privilège absent (3 types d'exception capturés, `Program.cs:73`) |
| ligne de commande `args` | `string[]` | `Program.cs:97` | seconde instance | locale |
| `ConfigManager.IsPackaged` | `bool` | `ConfigManager.cs:58` | tous | initialisation statique, immuable |
| fenêtre `AZERTYGlobal_Wnd` | HWND de l'instance vivante | `TrayApplication` (hors lot) | instance vivante crée, seconde instance cherche | aucune : `FindWindowW` puis `PostMessageW` non atomiques |
| message enregistré `AZERTYGlobal_RelaunchRequested` | `uint` | `RelaunchSignal.cs:30`, `TrayApplication.cs:557` | les deux processus | système (atome global) |
| enregistrement de relance après mise à jour | état Windows (Restart Manager) | `RestartRegistration.cs:41` | instance gagnante | système |

#### États

| état | prédicat |
|---|---|
| S0 Démarrage, noms non encore pris | avant `Program.cs:52` |
| S1 Gagnant complet | `isNew && isNewCrossSession` : les deux mutex possédés ; `RestartRegistration.Register()` fait, `TrayApplication` créée |
| S2 Gagnant dégradé | `isNew && crossSession == null` : seule la portée session protège |
| S3 Refusé, autre session | `isNew && !isNewCrossSession` : message « autre session » puis sortie |
| S4 Seconde instance, même session, packagée, activation système | `!isNew && IsPackaged && IsSystemActivation(args)` : sortie muette |
| S5 Seconde instance, même session, packagée, lancement Démarrer | `!isNew && IsPackaged && !IsSystemActivation(args)` : signal puis sortie |
| S6 Seconde instance, même session, non packagée | `!isNew && !IsPackaged` : MessageBox puis sortie |
| S7 Mutex `Local\` refusé (instance élevée) | `UnauthorizedAccessException` sur `Local\` dans `AcquireSingleInstance` : traité comme une seconde instance, donc S4, S5 ou S6 selon le package et les arguments (C4.1 corrigé) |

#### Invariants

- I1 : au plus un processus par compte et par session détient `Local\…` — garanti par le noyau (`Program.cs:52`).
- I2 : au plus un processus par compte sur la machine détient `Global\…` quand le privilège existe — garanti par `Program.cs:70-71`. Hors privilège : non garanti (assumé, `Program.cs:62-65`).
- I3 : seul le gagnant écrit config.json, usage-stats.json, error.log (rotation) et s'enregistre pour la relance — garanti pour `RotateLogIfNeeded` et `RestartRegistration.Register` (`Program.cs:129-133`, après les `return`) ; garanti aussi pour la lecture de config.json depuis ME-09 : `Program.Main` lit la langue par `ConfigManager.AppLanguageWithoutLoading` (aucun chargement, aucune quarantaine) et seul le gagnant charge et met de côté, au premier accès de `TrayApplication` (C4.5 corrigé).
- I4 : tout lancement concurrent aboutit à exactement un processus vivant — non garanti : une instance valide peut mourir en silence pendant que la gagnante est en train de quitter (cas S4 contre arrêt de S1, fenêtre étroite ; non retenu comme constat, voir « tentative d'écarter » de C4.1 pour le cadrage) ; un refus d'accès sur `Local\` mène désormais à S4, S5 ou S6 (C4.1 corrigé).
- I5 : la seconde instance ne montre rien en cas d'activation système — garanti par `Program.cs:110-124` (ordre MessageBox seulement hors package, signal seulement si `!IsSystemActivation`).
- I6 : un signal de relance n'est jamais perdu — non garanti, assumé : fenêtre pas encore créée → `false` (`RelaunchSignal.cs:65`).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1 | S0 | création `Local\` | succès | S0 (`isNew` connu) | — | `Program.cs:52` |
| T2 | S0 | création `Local\` | `UnauthorizedAccessException` (mutex créé par un processus élevé) | S4, S5 ou S6 (seconde instance, voir T7 à T9) | journal `SingleInstanceMutexDenied` ; le mutex `Global\` n'est pas créé | `Program.AcquireSingleInstance` |
| T3 | S0 | création `Global\` | succès | S0 | `isNewCrossSession` | `Program.cs:70-71` |
| T4 | S0 | création `Global\` | `UnauthorizedAccessException`/`IOException`/`NotSupportedException` | S0 avec `crossSession = null` | `LogCompatEvent("CrossSessionMutexUnavailable")` | `Program.cs:73-76` |
| T5 | S0 | décision | `isNew && !isNewCrossSession` | S3 | journal, MessageBox, `crossSession.Dispose`, sortie | `Program.cs:81-89` |
| T6 | S0 | décision | `isNew` et (`isNewCrossSession` ou `null`) | S1 ou S2 | `RotateLogIfNeeded`, `RestartRegistration.Register`, gestionnaire d'exception fatale, polices, `TrayApplication.Run` | `Program.cs:91,128-156` |
| T7 | S0 | décision | `!isNew && !IsPackaged` | S6 | journal `SecondInstance`, MessageBox « déjà en cours », sortie | `Program.cs:92-115,125` |
| T8 | S0 | décision | `!isNew && IsPackaged && IsSystemActivation` | S4 | journal, sortie muette | `Program.cs:92-99,110,120,125` |
| T9 | S0 | décision | `!isNew && IsPackaged && !IsSystemActivation` | S5 | journal ; `FindWindowW` ; si fenêtre : `AllowSetForegroundWindow(ASFW_ANY)` + `PostMessageW` ; journal `posted/not-posted` ; sortie | `Program.cs:120-125`, `RelaunchSignal.cs:62-72` |
| T10 | S1/S2 | message de relance reçu | `msg == _wmRelaunchRequested` | S1/S2 | `OnRelaunchRequested` (accueil, bulle d'état) — hors lot | `TrayApplication.cs:1113-1116,1441` |
| T11 | S1/S2 | `Cleanup` puis sortie de `Main` | — | processus mort, mutex libérés | `using` | `Program.cs:52,91` |
| T12 | S4 (arguments `--apres-maj`) | relance Windows après mise à jour alors que l'ancienne instance vit encore | `!isNew` | S4 (mort muette) | rien ne relance ensuite | `RelaunchSignal.cs:44-56`, `RestartRegistration.cs:41` |

Remarque T12 : Windows ne relance qu'après la fermeture de l'ancienne instance (doc de `RestartRegistration.cs:17-19`), donc T12 ne survient que si l'autostart a déjà lancé une instance (comportement prévu, `RestartRegistration.cs:13-15`).

#### Temporisations et réessais

Aucune minuterie propre. `TrySignalLiveInstance` n'essaie qu'une fois : pas de nouvel essai si la fenêtre manque (`RelaunchSignal.cs:65`).

#### États ou transitions invalides atteignables

C4.1 corrigé (ticket ME-09) : invariant I4 rétabli, test `MachinesEtatsLot4Tests.Mutex_local_refuse_par_une_instance_elevee_est_une_seconde_instance`. C4.5 corrigé (ticket ME-09) : invariant I3 rétabli, test `MachinesEtatsLot4Tests.Langue_lue_au_demarrage_ne_met_pas_le_fichier_corrompu_de_cote`.


### M4.2 — Écriture différée de config.json

**Nature** : implicite.
**Fichiers** : `src/ConfigManager.cs` (lignes 1299-1542, appels depuis 25-42, 1362-1395), `src/TrayApplication.cs` (déclencheurs d'arrêt).

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `_cache` | `Dictionary<string, JsonElement>?` | `ConfigManager.cs:120` | pompe (setters/getters), pool (`Flush` lit `SerializeLocked`) | `_lock` |
| `_compatibilityCache` | `Dictionary<string,string>?` | `ConfigManager.cs:121` | idem | `_lock` |
| `_dirty` | `bool` | `ConfigManager.cs:1328` | pompe (`MarkDirty`), pool et pompe (`Flush`) | `_lock` |
| `_failedWrites` | `int` | `ConfigManager.cs:1350` | pompe (remise à 0), flusheur (incrément) | `_lock` |
| `_dirtyGeneration` | `int` | `ConfigManager.cs` (après `_failedWrites`) | pompe (incrément par `MarkDirty`), flusheur (relevé avec l'instantané) | `_lock` |
| `_saveTimer` | `System.Threading.Timer?` | `ConfigManager.cs:1330,1392` | créé sous `_lock` par `ArmSaveLocked` ; callback sur le pool | `_lock` pour `Change` ; callback sans verrou, `Flush` reprend les siens |
| `SaveDelayMilliseconds` | `int` statique (400, ou −1 en test) | `ConfigManager.cs:1334` | lu sous `_lock` | champ non volatile, modifié seulement par les tests |
| `_loadFailed` | `bool` | `ConfigManager.cs:123` | `Flush` copie sous `_lock` | `_lock` |
| `_fileLock` | verrou | `ConfigManager.cs:1329` | une écriture à la fois | — |
| fichier `config.json` et `config.json.<pid>.tmp` | disque | `ConfigManager.cs:1503,1516-1526` | écrivains : flusheur courant ; lecteur : `EnsureLoaded` (autre processus compris) | `File.Replace` atomique ; temp par PID (`ConfigManager.cs:1307`) |

#### États

| état | prédicat |
|---|---|
| W0 Propre | `!_dirty`, aucun essai en cours |
| W1 À écrire, délai normal | `_dirty && _failedWrites == 0`, minuterie armée à 400 ms |
| W2 Écriture en cours | un flusheur détient `_fileLock` après avoir copié le cache et mis `_dirty = false` (`ConfigManager.cs:1433`), hors `_lock` |
| W3 Écriture en cours + nouveau réglage | W2 et `_dirty == true` reposé par `MarkDirty` |
| W4 Essai n° k en attente | `_dirty && _failedWrites == k`, minuterie armée à `RetryDelayAfter(k)` (1 s, 5 s, 30 s, 300 s, puis 300 s indéfiniment) |
| W5 Verrouillé sans écriture | `_loadFailed` : `WriteFile` rend `true` sans écrire si le fichier existe (`ConfigManager.cs:1506-1510`) |
| W6 Fermeture | `Cleanup` a appelé `FlushFinal` (`Flush` puis jusqu'à 3 reprises synchrones) |

#### Invariants

- I1 : pas d'écriture disque sous `_lock` — garanti pour l'écriture différée (`SerializeLocked` en mémoire, `WriteFile` hors `_lock`, `ConfigManager.cs:1427-1441`). Pas vrai des lectures : `EnsureLoaded` exécute `File.Exists`, `File.ReadAllText` et, en cas de corruption, `File.Move` de la quarantaine sous `_lock` (`ConfigManager.cs:1219-1236, 1269-1272`), appelé sous verrou par `TakeQuarantineNotice` (`:1207-1212`) et par les accesseurs (voir M4.3, « protection `_lock` »).
- I2 : ordre `_fileLock` → `_lock`, `Flush` refusé sous `_lock` — garanti (`ConfigManager.cs:1418-1421`).
- I3 : écritures dans l'ordre des instantanés — garanti par `_fileLock` (`ConfigManager.cs:1421`).
- I4 : un réglage posé n'est jamais perdu tant que le processus vit — garanti : l'échec remet `_dirty = true` (`ConfigManager.cs:1453`), un `MarkDirty` concurrent ne l'efface pas, un succès avec `_dirty` reposé est suivi d'un nouvel essai (la minuterie de `MarkDirty` reste armée).
- I5 : `_failedWrites` compte les échecs d'affilée du dernier instantané — garanti depuis l'audit des accès concurrents (CM6) : un échec d'écriture n'incrémente le compteur et ne réarme la minuterie que si `_dirtyGeneration` n'a pas changé depuis l'instantané ; sinon le cycle normal relancé par `MarkDirty` (0, 400 ms) reste en place. Test : `AccesConcurrentsEcrituresTests.Config_ReglageChangePendantUneEcritureQuiEchoue_GardeLeCycleNormal`.
- I6 : un réglage posé avant la fermeture est durable à la sortie — garanti depuis ME-09 dans une limite : `FlushFinal` (appelée par `Cleanup`, donc à la sortie normale et à `WM_ENDSESSION` confirmé) refait jusqu'à 3 essais synchrones espacés de 250 ms (750 ms de pause au plus) ; un verrou plus long fait perdre le réglage, et la perte est journalisée (`ConfigFinalWriteLost`) (C4.3 corrigé). `WM_QUERYENDSESSION` n'est qu'une demande, qu'une autre application peut annuler : elle ne fait qu'un `Flush` immédiat sans attente (`TrayApplication.OnQueryEndSession`), la minuterie de reprise couvrant l'app qui continue de vivre (suite du 16 novembre).
- I7 : fichier jamais tronqué en cas de coupure — garanti : temp + `Flush(true)` + `File.Replace`/`File.Move` (`ConfigManager.cs:1516-1526`).
- I8 : deux processus ne s'écrasent pas par le temp — garanti par `BuildTempPath(pid)` ; ne protège pas du « dernier écrivain gagne » sur le fichier final.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1 | W0 | setter avec valeur différente | — | W1 | `_dirty = true`, `_failedWrites = 0`, minuterie 400 ms | `ConfigManager.cs:1362-1379` |
| T2 | W0 | setter avec valeur identique | `GetRawText()` égal | W0 | rien | `ConfigManager.cs:1364-1366` |
| T3 | W1/W4 | setter | valeur différente | W1 | `_failedWrites = 0`, minuterie replacée à 400 ms (abandon du palier de reprise) | `ConfigManager.cs:1375-1378` |
| T4 | W1/W4 | minuterie tire ou `FlushInBackground` ou `Flush` direct | `_dirty && _cache != null` | W2 | copie du cache, `_dirty = false`, contexte de journal selon palier | `ConfigManager.cs:1429-1438` |
| T5 | W1/W4 | idem | `!_dirty` | W0 | retour immédiat | `ConfigManager.cs:1429` |
| T6 | W2 | écriture réussie | `path == _configPath` | W0 ou W1 | `_failedWrites = 0`, `DiskWriteCount++` | `ConfigManager.cs:1446-1449,1527` |
| T7 | W2 | écriture échouée (IOException/UnauthorizedAccess) | — | W4 (k = ++`_failedWrites`) | `_dirty = true`, minuterie `RetryDelayAfter(k)`, journal une fois par palier, temp supprimé | `ConfigManager.cs:1450-1456,1530-1540` |
| T8 | W2 | setter pendant l'écriture | — | W3 | `_dirty = true`, `_failedWrites = 0`, minuterie 400 ms | `ConfigManager.cs:1375-1378` |
| T9 | W3 | fin d'écriture réussie | — | W1 | `_failedWrites = 0` ; `_dirty` reste vrai, minuterie 400 ms déjà armée | `ConfigManager.cs:1446-1449` |
| T10 | W3 | fin d'écriture échouée | `_dirtyGeneration` changé depuis l'instantané | W1 | `_dirty = true` ; `_failedWrites` reste à 0 et la minuterie de 400 ms armée par le setter reste en place (avant l'audit des accès concurrents, CM6 : W4 avec k = 1, minuterie remplacée par 1 s) | `ConfigManager.Flush` |
| T11 | tout état avec `_loadFailed` et fichier présent | `Flush` | — | W0 (réglages jamais écrits) | journal « Sauvegarde ignorée » ; `true` rendu | `ConfigManager.cs:1506-1510` |
| T12 | W1/W4 | `WM_WTSSESSION_CHANGE` (tout événement) | — | W2 sur le pool | `FlushInBackground` | `TrayApplication.cs:1076`, `ConfigManager.cs:1399` |
| T13 | W1/W4 | `WM_QUERYENDSESSION` (demande d'arrêt, annulable, peut se répéter) | — | W2 sur la pompe | `OnQueryEndSession` : un `Flush` synchrone puis `UsageStats.Flush`, sans pause ; un échec est repris par la minuterie de reprise (W4), l'app vivant toujours ; répond vrai | `TrayApplication.OnQueryEndSession` ; le `case` est gardé par un test de texte source |
| T14 | W1/W4 | `Cleanup` (menu Quitter, `WM_ENDSESSION` avec wParam non nul, arrêt confirmé) | `_cleaned` faux | W6 | `FlushFinal` (après le `Dispose` des fenêtres) : 3 reprises de 250 ms au plus, seule attente de la fermeture ; échec persistant = perte journalisée (`ConfigFinalWriteLost`) | `TrayApplication.cs:2331-2360` |
| T15 | tout | `RecordReviewPromptShown` | — | W2 sur la pompe | `Flush` immédiat hors `_lock` | `ConfigManager.cs:496-512` |

#### Temporisations et réessais

Départ 400 ms après le dernier geste (`ConfigManager.cs:1334`). Reprises 1 s, 5 s, 30 s, 5 min puis 5 min (`ConfigManager.cs:1347,1357-1358`), minuterie unique `Change(delay, Infinite)` ; un nouveau réglage remet le compteur à 0. Aucune limite de nombre d'essais (compteur `int` qui croît, `Clamp` sur le dernier palier). Journal : une ligne par palier ouvert (`ConfigManager.cs:1436-1438`). Les reprises tournent sur le pool : elles disparaissent à la fin du processus ; la fermeture ne s'y fie plus (`FlushFinal`, ME-09). T10 est un défaut de comptage sans perte (non retenu).

#### États ou transitions invalides atteignables

C4.3 corrigé (ticket ME-09) : invariant I6 rétabli, test `MachinesEtatsLot4Tests.Ecriture_de_fermeture_reessaie_puis_ecrit_le_reglage`. Suite : une demande d'arrêt annulée ne gèle plus l'app, test `Demande_d_arret_annulee_n_endort_pas_le_fil_d_interface`. Le `case` du message est lui aussi gardé : le test de texte source `Demande_d_arret_appelle_OnQueryEndSession_et_n_ecrit_pas_la_fermeture_dans_le_case` échoue si le `case` de `WM_QUERYENDSESSION` appelle autre chose que `OnQueryEndSession` ou y remet `FlushFinal` (le test précédent, qui appelle `OnQueryEndSession` directement, restait alors vert).


### M4.3 — Chargement de config.json et mise de côté d'un fichier illisible

**Nature** : implicite (`_cache == null`, `_loadFailed`, `_quarantineNoticePending`, présence du fichier et de sa copie `.illisible-…`).
**Fichiers** : `src/ConfigManager.cs:1218-1297`, `src/FileQuarantine.cs`, `src/TrayApplication.cs:336`.

#### Variables d'état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_cache` | `Dictionary?` ; non nul dès le début de `EnsureLoaded` | `ConfigManager.cs:1220-1221` | pompe, pool via `Flush` | `_lock` (tous les appelants de `EnsureLoaded` le tiennent) |
| `_loadFailed` | `bool` | `ConfigManager.cs:1222,1258,1274` | idem | `_lock` |
| `_quarantineNoticePending` | `bool` | `ConfigManager.cs:1201,1272` | pompe (`TakeQuarantineNotice`) | `_lock` |
| `FileQuarantine.Clock` | `Func<DateTime>` statique | `FileQuarantine.cs:24` | fil appelant | aucune (couture de test) |
| fichier config.json / copie | disque | — | tout processus du compte | aucune entre processus |

#### États

| état | prédicat |
|---|---|
| L0 Non chargé | `_cache == null` |
| L1 Chargé sain | `_cache != null && !_loadFailed`, fichier lu (ou absent) |
| L2 Installation neuve | L1, fichier absent, `appLanguage` dérivé et `_dirty` |
| L3 Corrompu, mis de côté | `_cache` vide, copie `.illisible-AAAAMMJJ-HHMMSS` créée, `_quarantineNoticePending` |
| L4 Illisible, verrouillé | `_loadFailed` (lecture impossible ou renommage raté), aucune écriture si le fichier existe |

#### Invariants

- I1 : un fichier existant jamais écrasé s'il n'a pas été chargé — garanti : `WriteFile` refuse (`ConfigManager.cs:1506-1510`), quarantaine jamais en suppression ni écrasement (`FileQuarantine.cs:49`, `overwrite: false`).
- I2 : `_loadFailed` décrit le fichier présent sur le disque — garanti depuis ME-09 : un renommage raté parce que le fichier a disparu (mis de côté par une autre instance) ne pose plus `_loadFailed` (`File.Exists`), et une seconde instance ne charge plus le fichier avant les mutex (C4.5 corrigé).
- I3 : le message de remise à zéro part au plus une fois — garanti (`TakeQuarantineNotice`, `ConfigManager.cs:1207-1216`).
- I4 : `_cache` jamais non nul et vide pour un fichier valide non lu — garanti pour les exceptions listées (`ConfigManager.cs:1252-1280`) ; une exception hors liste laisserait `_cache` vide et `_loadFailed` faux (non atteignable avec les types levés par `ReadAllText` et `JsonDocument.Parse` documentés ; non retenu).
- I5 : lecture impossible → pas de réessai de lecture dans la session — garanti (assumé : « le prochain lancement relit », `ConfigManager.cs:1254-1257`).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1 | L0 | premier accès (getter, setter, `Flush`, `TakeQuarantineNotice`) | fichier lisible, objet JSON | L1 | cache rempli, sous-objet `compatibility` à part | `ConfigManager.cs:1225-1250` |
| T2 | L0 | idem | fichier absent, `_cache.Count == 0` | L2 | `appLanguage` dérivé de l'UI Windows, `MarkDirty` | `ConfigManager.cs:1288-1296` |
| T3 | L0 | idem | `IsReadFailure` (verrou, droits) | L4 | journal | `ConfigManager.cs:1252-1260` |
| T4 | L0 | idem | `IsCorruption` (JsonException/FormatException/racine non objet) et renommage réussi | L3 puis L2 | cache vidé, copie `.illisible-…`, `_quarantineNoticePending = true`, langue dérivée | `ConfigManager.cs:1261-1274,1288` |
| T5 | L0 | idem | corruption et renommage raté, fichier toujours présent (verrou) ; si le fichier est parti (autre instance) : L2 sans `_loadFailed` | L4 | `_loadFailed = true` | `ConfigManager.cs:1273-1274`, `FileQuarantine.cs:52-56` |
| T6 | L0 | idem | `IOException` restante (fichier disparu) | L1 vide, sans langue dérivée si le fichier n'existe pas : L2 | journal | `ConfigManager.cs:1276-1280` |
| T7 | L3 | `TakeQuarantineNotice` | — | L3 sans message | `TIMER_SETTINGS_RESET_NOTICE` posé par le tray | `TrayApplication.cs:336-338` |

#### Temporisations et réessais

Aucun. Horodatage de la copie à la seconde (`FileQuarantine.cs:27-28`) : deux mises de côté dans la même seconde font échouer la seconde (T5).

#### États ou transitions invalides atteignables

C4.5 corrigé (ticket ME-09) : invariant I2 rétabli, test `MachinesEtatsLot4Tests.Fichier_corrompu_deja_mis_de_cote_par_l_autre_instance_ne_bloque_pas_les_ecritures`.


### M4.4 — Lancement automatique (`AutoStartWindowsState`, `IsRegistered`, relance unique)

**Nature** : explicite (`enum AutoStartWindowsState`, `AutoStart.cs:10-23`) + implicite (`IsRegistered`, `autoStartEnabled`, `autoStartNudgeDone`).
**Fichiers** : `src/AutoStart.cs`, `src/AutoStartNudge.cs`, `src/ConfigManager.cs:284-287,829-830`.

#### Variables d'état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| état Windows non packagé : fichier `…\Startup\AZERTY Global.lnk` | disque | `AutoStart.cs:34-38,187-282` | pompe | aucune (partagé avec l'utilisateur, l'Explorateur) |
| refus Windows non packagé : valeur `StartupApproved\StartupFolder\AZERTY Global.lnk`, bit 0 du premier octet | registre HKCU | `AutoStart.cs:390-407` | Windows écrit, l'app lit seulement | aucune |
| `StartupTask` MSIX `AZERTYGlobalStartup` : `Enabled`/`EnabledByPolicy`/`Disabled`/`DisabledByUser`/`DisabledByPolicy` | WinRT | `AutoStart.cs:289-371` | pompe (appels bloquants) | Windows |
| `autoStartEnabled` | clé JSON | `ConfigManager.cs:284-287` | pompe | `_lock` |
| `autoStartNudgeDone` | clé JSON | `ConfigManager.cs:829-830` | pompe | `_lock` |
| signaux de `AutoStartSignals` | enregistrement | `AutoStartNudge.cs:23-28,50-55` | pompe | instantané |

#### États

Windows vu par l'app (`AutoStart.WindowsState`, `AutoStart.cs:184-185`) :

| état | prédicat (non packagé / packagé) |
|---|---|
| Enabled | raccourci présent et refus absent / `Enabled` ou `EnabledByPolicy` |
| Disabled | raccourci absent et refus absent / `Disabled` ou autre valeur non listée |
| DisabledByUser | refus inscrit (peu importe la présence du raccourci, `AutoStart.cs:379`) / `DisabledByUser` |
| DisabledByPolicy | jamais en non packagé / `DisabledByPolicy` |
| Unknown | exception de l'API WinRT |

`IsRegistered` (`AutoStart.cs:152`) vaut `WindowsState == Enabled` depuis ME-04 : « effectif » veut dire « Windows exécutera l'entrée ». Un raccourci présent avec le refus inscrit n'est plus « enregistré » (C4.2 corrigé). En packagé, le prédicat est inchangé (`Enabled` ou `EnabledByPolicy`, état illisible = faux). La lecture de l'état (`WindowsStateReader`), la présence du raccourci (`ShortcutExists`) et sa création (`ShortcutEnabler`) sont remplaçables dans les tests.

Relance unique : N0 `autoStartNudgeDone == false` (éligible ou non selon `ShouldPrompt`), N1 `autoStartNudgeDone == true` (absorbant).

#### Invariants

- I1 : `Set(x)` ne rend vrai que si `IsRegistered == x` après coup (`AutoStart.cs:132-134`) — garanti, et `IsRegistered` est l'état réel de Windows (`Enabled`) depuis ME-04 : `Set(true)` rend `false` quand Windows refuse, sans écrire `autoStartEnabled` ; l'interface dit le refus et renvoie vers les Paramètres Windows (`GetFailureMessage`, `AutoStart_FailureMessageRefused`).
- I2 : `autoStartEnabled` suit la dernière réussite de `Set` — garanti (`AutoStart.cs:136`) ; vaut cache, jamais source de vérité (`ConfigManager.cs:283`).
- I3 : un refus Windows n'est jamais contourné par l'accueil — garanti par `DefaultOnboardingCheck` pour l'état `DisabledByUser`/`DisabledByPolicy` quelle que soit la présence du raccourci, et pour `Unknown` quand aucune trace d'un choix passé existe (`AutoStart.cs:177-188`) : le refus est testé avant `isRegistered` depuis ME-04.
- I4 : la relance unique ne part qu'une fois — garanti (`AutoStartNudge.cs:40`, `MarkPromptShown` avant l'affichage, `TrayApplication.cs:1597`).
- I5 : la relance ne propose que ce que l'app peut obtenir — garanti depuis ME-04 : `AutoStartSignals.WindowsState` est lu une fois par `Snapshot`, et `ShouldPrompt` rend faux pour `DisabledByUser`, `DisabledByPolicy` et `Unknown` (`AutoStartNudge.cs:45-46`) ; la relance n'est alors ni proposée ni consommée (`MarkPromptShown` n'est appelé qu'après `ShouldPrompt`). Choix retenu : un état illisible (`Unknown`, exception de l'API `StartupTask` en version packagée) ne déclenche pas la proposition, car on ne demande pas d'activer ce qui l'est peut-être déjà ; mais la relance n'est pas consommée, et elle repart au lancement suivant si l'état redevient lisible (`Disabled`). Test `MachinesEtatsLot4Tests.Relance_non_consommee_par_un_etat_illisible_repart_quand_il_redevient_lisible`.
- I6 : `Set` n'appelle pas `RequestEnableAsync` si déjà actif — garanti (`AutoStart.cs:314-315`).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1 | Disabled | `Set(true)` non packagé | `Enable()` réussit | Enabled si pas de refus (retour `true`) ; sinon DisabledByUser, `IsRegistered == false`, retour `false` (ME-04) | raccourci créé via IShellLink ; `autoStartEnabled = true` seulement si Enabled ; refus : message vers les Paramètres Windows | `AutoStart.cs:124-136,187-269` |
| T2 | Enabled | `Set(false)` non packagé | suppression réussie | Disabled (le refus éventuel reste) | raccourci supprimé, `autoStartEnabled = false` | `AutoStart.cs:129,271-282` |
| T3 | Enabled | `Set(false)` non packagé | suppression bloquée (verrou) | Enabled | exception avalée, `IsRegistered` vrai, `Set` rend `false` | `AutoStart.cs:278-281,133-134` |
| T4 | Disabled | `Set(true)` packagé | `RequestEnableAsync` rend Enabled/EnabledByPolicy | Enabled | `autoStartEnabled = true` | `AutoStart.cs:319-320` |
| T5 | Disabled/DisabledByUser/DisabledByPolicy | `Set(true)` packagé | `RequestEnableAsync` rend un refus | inchangé | `Set` rend `false` ; l'appelant affiche le message d'échec | `AutoStart.cs:319-320`, `TrayApplication.cs:1621-1624` |
| T6 | Enabled | `Set(true)` packagé | déjà actif | Enabled | aucun appel Windows | `AutoStart.cs:314-315` |
| T7 | Enabled/EnabledByPolicy | `Set(false)` packagé | — | Disabled, ou Enabled si imposé par politique | `task.Disable()` ; vérité relue ; politique : `Set` rend `false` | `AutoStart.cs:324-325,132-134` |
| T8 | tout (packagé) | exception WinRT dans `SetStartupTask` | relecture : `enabled ? actif : true` | selon relecture | journal ; une exception de relecture donne `false` | `AutoStart.cs:328-341` |
| T9 | N0 éligible | démarrage de l'app, aucune autre bulle | `ShouldPrompt` (faux si Windows refuse ou état illisible : N0 conservé, relance non consommée, reposée au lancement suivant si l'état redevient lisible) | N1 | `SetAutoStartNudgeDone`, toast ou bulle | `TrayApplication.cs:328-329,1589-1606` |
| T10 | N0 | choix manuel Paramètres ou accueil, `Set` réussi | Paramètres : état réellement changé (`autoStart != autoStartWasRegistered`). Accueil : `userChoice \|\| autoStart != wasRegistered \|\| autoStart != _autoStartShownChecked` | N1 | `MarkPromptShown` | `SettingsWindow.cs:1488-1489`, `OnboardingWindow.cs:677-678` |
| T11 | N1 | clic sur le toast (même tardif, ou répété) ou clic sur la bulle (une seule fois utile) | bulle : `_pendingBalloon` remis à `None` avant d'agir, un clic répété ne rejoue pas l'action ; toast COM : `WM_APP_TOAST` sans garde | N1 | `AutoStart.Set(true)` ; succès ou bulle d'erreur | `TrayApplication.cs:753-767,777-785,1617-1625` |

#### Temporisations et réessais

Aucun réessai automatique. Les appels WinRT sont bloquants sur la pompe (`AutoStart.cs:303-304,319`) ; un blocage de service retient l'interface.

#### États ou transitions invalides atteignables

C4.2 corrigé (ticket ME-04) : invariants I1 et I3 rétablis, test `MachinesEtatsLot4Tests.Activation_refusee_par_Windows_n_est_pas_dite_reussie`.
C4.4 corrigé (ticket ME-04) : invariant I5 rétabli, test `MachinesEtatsLot4Tests.Relance_unique_n_est_pas_proposee_quand_l_etat_est_refuse_ou_inconnu`. Suite : l'état illisible supprime la proposition sans la consommer, test `…Relance_non_consommee_par_un_etat_illisible_repart_quand_il_redevient_lisible`.


### M4.5 — Cache de politiques d'entreprise et canal de distribution

**Nature** : implicite (`_cache == null` / non nul ; `_override` de test).
**Fichiers** : `src/PolicyManager.cs`, `src/AppChannel.cs`.

#### Variables d'état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_cache` | `PolicySet?` | `PolicyManager.cs:93` | tout fil qui lit `Current` | `_lock` (`PolicyManager.cs:101`) |
| `_reader` | `PolicyValueReader` | `PolicyManager.cs:94` | idem + tests | `_lock` |
| `AppChannel.Detected` | `DistributionChannel`, auto-propriété à initialiseur | `AppChannel.cs:46` | tous | initialisation statique |
| `AppChannel._override` | `DistributionChannel?` | `AppChannel.cs:44` | tests | aucune (tests seulement) |

#### États

| état | prédicat |
|---|---|
| P0 Non lu | `_cache == null` |
| P1 Lu | `_cache` non nul : `PolicySet.None` (aucune valeur, ou registre illisible) ou valeurs lues |

#### Invariants

- I1 : lecture unique par processus — garanti (`PolicyManager.cs:103-104`) ; aucun mécanisme de relecture : un changement de stratégie en cours de route n'est vu qu'au redémarrage (assumé, `PolicyManager.cs:22-25`).
- I2 : politique > réglage utilisateur > défaut du canal — garanti par les fonctions pures (`PolicyManager.cs:211-234`).
- I3 : panne de registre = « aucune politique » — garanti par `catch` (`PolicyManager.cs:110-118`). Une lecture individuelle en échec transitoire (`ERROR_MORE_DATA`, accès refusé) est aussi vue comme « absente » et mise en cache pour toute la session (`PolicyManager.cs:316-338`) ; comportement assumé, non retenu comme constat.
- I4 : canal immuable — garanti hors tests.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1 | P0 | premier `Current` | lecture sans exception | P1 | cache posé | `PolicyManager.cs:106-108` |
| T2 | P0 | premier `Current` | exception | P1 = `None` | journal | `PolicyManager.cs:110-117` |
| T3 | P1 | `Current` | — | P1 | retour du cache | `PolicyManager.cs:103-104` |
| T4 | P0/P1 | `OverrideForTests` | — | P0 | cache vidé, restauré au `Dispose` | `PolicyManager.cs:167-201` |

#### Temporisations et réessais

Aucun.

#### États ou transitions invalides atteignables

Aucun constat.


### M4.6 — Serveur COM d'activation de toast

**Nature** : implicite (`_registrationCookie != 0`, `_factoryUnknown != 0`).
**Fichiers** : `src/ToastActivation.cs`, abonné `src/TrayApplication.cs:248-263`.

#### Variables d'état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_registrationCookie` | `uint` | `ToastActivation.cs:63,122-124,147` | pompe (Register, Unregister) | aucune (un seul fil appelant) |
| `_factoryUnknown` | `IntPtr` | `ToastActivation.cs:64,120,157` | pompe | aucune |
| `Activated` | événement statique | `ToastActivation.cs:71` | abonné sur la pompe (`TrayApplication.cs:253`), invoqué sur le fil COM | liste de délégués du .NET |

#### États

| état | prédicat |
|---|---|
| A0 Non enregistré | `_registrationCookie == 0` |
| A1 Enregistré | `_registrationCookie != 0` |
| A2 Révoqué | A0 après `Unregister` |

#### Invariants

- I1 : enregistrement idempotent — garanti (`ToastActivation.cs:117`).
- I2 : échec de `CoRegisterClassObject` libère la fabrique — garanti (`ToastActivation.cs:125-129,133-138`).
- I3 : l'abonné ne touche pas l'UI depuis le fil COM — garanti (`PostMessageW` seulement, `TrayApplication.cs:253-261`).
- I4 : une activation reçue après `Unregister`/`DestroyWindow` est sans effet — garanti de fait : `PostMessageW` vers un HWND détruit échoue, `Activate` capture toute exception (`ToastActivation.cs:78-79`).
- I5 : aucune activation perdue entre `Register` (`TrayApplication.cs:250`) et l'abonnement (`TrayApplication.cs:253`) — garanti si la livraison passe par la pompe STA (le constructeur ne la pompe pas) ; non garanti si Windows livre sur un fil RPC (fenêtre de quelques instructions, `Activated` encore nul). Non retenu : preuve du mode de livraison impossible à la lecture.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1 | A0 | `Register` | succès | A1 | fabrique COM créée, classe enregistrée | `ToastActivation.cs:115-131` |
| T2 | A0 | `Register` | `hr != 0` ou exception | A0 | journal, `ReleaseFactory`, rend `false` : le tray retombe sur la bulle | `ToastActivation.cs:125-129,133-138`, `TrayApplication.cs:1599-1604` |
| T3 | A1 | `Register` | — | A1 | rend `true` | `ToastActivation.cs:117` |
| T4 | A1 | activation (clic toast) | — | A1 | `Activated(args)` → `PostMessageW(WM_APP_TOAST, 1/2/3)` | `ToastActivation.cs:76-80`, `TrayApplication.cs:253-261,777-785` |
| T5 | A1 | `Unregister` (Cleanup) | — | A2 | `CoRevokeClassObject`, `Release` | `ToastActivation.cs:142-150`, `TrayApplication.cs:2361` |
| T6 | A2 | `Unregister` | — | A2 | no-op | `ToastActivation.cs:144-149` |

#### Temporisations et réessais

Aucun.

#### États ou transitions invalides atteignables

Aucun constat.


### Frontières avec les autres lots

- `TrayApplication` (WndProc, `WM_APP_TOAST`, `OnRelaunchRequested`, `ShowBalloon`, `_pendingBalloon`, `Cleanup`) : consomme `RelaunchSignal`, `ToastActivation.Activated`, `AutoStartNudge`, `ConfigManager.Flush`. Les machines de bulle en attente (`PendingBalloonKind`) et d'accueil appartiennent à un autre lot ; ce lot documente seulement ce qu'il lui livre. Un toast COM cliqué plusieurs fois re-déclenche l'action (`OpenReviewTarget`, `EnableAutoStartFromPrompt`), car `WM_APP_TOAST` n'a pas de garde (`TrayApplication.cs:777-785`) ; pour la bulle, `NIN_BALLOONUSERCLICK` remet `_pendingBalloon` à `None` avant d'agir (`:753-756`) et un clic répété ne rejoue rien.
- `OnboardingWindow.ApplyAutoStart` / `Close`, `SettingsWindow.ApplyAutoStart` : appellent `AutoStart.Set` sur la pompe, pilotent `AutoStartNudge.MarkPromptShown`. Leur machine (`_step3Reached`, `_autoStartShownChecked`) est hors lot.
- `UsageStats` (autre fichier json, mêmes principes de quarantaine, `Flush`/`FlushInBackground`) : `ActiveDaysCount` et `FirstRemapDate` alimentent `AutoStartNudge.Snapshot`. Sa propre écriture différée n'est pas décrite ici.
- `KeyboardHook` : son rappel s'exécute sur la pompe et appelle `ConfigManager` (getters) ; aucun accès au pool en dehors de `Flush`.
- `LessonsWindow` / progression des Leçons : même `FileQuarantine` (`FileQuarantine.cs:7`), machine hors lot.
- `Win32.cs` : `CoRegisterClassObject`, `PostMessageW`, `SetProcessDpiAwarenessContext` (aucun état).
- Tests proches : `src/AZERTYGlobal.Tests/EcritureRateeReessaiTests.cs` (paliers, une ligne par palier, remise à zéro par nouveau réglage), `EcrituresGroupeesTests.cs` (regroupement, écriture en cours, garde sous verrou), `QuarantaineTests.cs` (corruption, verrou, renommage impossible), `AutoStartNudgeTests.cs`, `AutoStartOnboardingDefaultTests.cs` (décision pure), `RelaunchSignalTests.cs` (arguments), `ConfigShapePreservationTests.cs:189-216` (noms de mutex), `PolicyTests.cs`, `AppChannelTests.cs`, `ToastActivationTests.cs`.

### Constats du lot

#### C4.1 — Un mutex `Local\` créé par une instance élevée fait planter le lancement non élevé, avant tout gestionnaire d'erreur

Gravité moyenne, confiance moyenne. Détail dans la sortie structurée. Preuve : `src/Program.cs:52` (aucun `try`) contre `src/Program.cs:73` (le `catch` n'existe que pour `Global\`), gestionnaire fatal posé seulement en `src/Program.cs:136`.

#### C4.2 — En non packagé, `IsRegistered` ignore le refus inscrit par Windows : `Set(true)` réussit sans effet

Gravité moyenne, confiance moyenne. Preuve : `src/AutoStart.cs:147-148`, `132-136`, `379-380`, `166`.

#### C4.3 — Écriture de fermeture ratée = réglages perdus, sans reprise

Gravité moyenne, confiance haute sur le code, probabilité faible. Preuve : `src/ConfigManager.cs:1450-1456`, `src/TrayApplication.cs:2360`, `1088`.

#### C4.4 — La relance unique du lancement automatique ignore le refus Windows

Gravité faible, confiance haute. Preuve : `src/AutoStartNudge.cs:23-28,38-47,50-55`, `src/AutoStart.cs:344-353`.

#### C4.5 — Seconde instance : config.json lu et mis de côté avant le mutex

Gravité faible, confiance moyenne. Preuve : `src/Program.cs:44` contre `52`, `src/ConfigManager.cs:1261-1274`, `src/FileQuarantine.cs:49-56`.

## Lot 5 — Statistiques, progression et Défi du jour

Tous les chemins sont relatifs au dépôt. Lecture seule : aucun test n'a été exécuté, les constats reposent sur la lecture du code (confiance indiquée pour chacun).

### Modèle de fils du lot

| Fil | Rôle | Source |
|---|---|---|
| Fil UI / pompe de messages | Rappel du hook clavier (`UsageStats.RecordEmittedText`), `WM_TIMER`, fenêtres Statistiques et Leçons, `LessonProgressStore`, `ChallengeSessionState`, `TrainingReminders` | `src/TrayApplication.cs:542`, `src/UsageStats.cs:261` |
| Pool de threads | Uniquement `UsageStats.FlushInBackground` | `src/UsageStats.cs:659-667` |

Verrouillage de `UsageStats` : `_fileLock` puis `_lock`, jamais l'inverse ; instantané JSON sous `_lock` sans I/O, écriture hors `_lock` (`src/UsageStats.cs:630-651`) ; la lecture du fichier (`EnsureLoaded`) se fait en revanche sous `_lock` (`:549`, `:635`). `_backgroundFlushQueued` est posé par `Interlocked.Exchange` (`src/UsageStats.cs:661`). Le hook et les minuteries partagent le fil de pompe : pas de course entre eux. Le reste du lot est mono-fil. Deux processus ne coexistent pas (mutex Local + Global, `src/Program.cs:51-75`).

### M5.1 — Persistance différée de UsageStats

**Nature** : implicite. **Fichiers** : `src/UsageStats.cs`.

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `_dirty` | bool | `src/UsageStats.cs:98,234,310,642,651` | UI, pool | `_lock` |
| `_backgroundFlushState` | int (0, 1, 2) | `src/UsageStats.cs:41` | UI, pool | `Interlocked` |
| `_statsPath` | string | `src/UsageStats.cs` | UI, pool | `_lock` |

#### États

| état | prédicat |
|---|---|
| Propre | `!_dirty` |
| Sale | `_dirty` |
| Écriture en cours | `_fileLock` tenu, `_dirty` déjà remis à faux |
| Vidage de fond en file ou en cours | `_backgroundFlushState == 1` |
| Vidage de fond avec un tour de plus demandé | `_backgroundFlushState == 2` |

#### Invariants

- I1 : un seul écrivain de fichier à la fois — garanti par `_fileLock` (`src/UsageStats.cs:630`).
- I2 : aucune écriture sous `_lock` — garanti (`src/UsageStats.cs:640-646`) ; la lecture, non : `EnsureLoaded` lit le fichier (`File.ReadAllText`, `src/UsageStats.cs:564-565`) sous `_lock`, via `Preload` (`:549`) et `Flush` (`:635`).
- I3 : un échec d'écriture rend l'état sale — garanti (`src/UsageStats.cs:651`, uniquement si le chemin n'a pas changé).
- I4 : le hook n'effectue jamais d'I/O — garanti seulement parce que `Preload` est appelé au démarrage (`src/UsageStats.cs:545`) : sans lui, la première frappe déclencherait la lecture paresseuse sous `_lock`.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | Propre | Record* | collecte active | Sale | compteur incrémenté | `src/UsageStats.cs:234` |
| 2 | Sale | Flush | `_dirty` | Propre | instantané puis écriture | `src/UsageStats.cs:630-646` |
| 3 | Écriture | échec d'écriture | même chemin | Sale | `_dirty = true` | `src/UsageStats.cs:651` |
| 4 | Propre | Flush | `!_dirty` | Propre | retour immédiat | `src/UsageStats.cs:639` |
| 5 | — | FlushInBackground | file libre | Vidage en file | tâche pool | `src/UsageStats.cs:661` |
| 6 | Vidage en file | FlushInBackground | état 1 | Tour de plus demandé | aucun : servi à la fin du vidage (audit des accès concurrents, B2 ; avant : demande jetée) | `src/UsageStats.cs:FlushInBackground` |
| 7 | Tour de plus demandé | FlushInBackground | état 2 | idem | retour : le tour demandé verra ces frappes | `src/UsageStats.cs:FlushInBackground` |
| 8 | Vidage en file ou tour de plus | fin d'un vidage | état décrémenté > 0 | Vidage en file | nouveau `Flush` sur le même fil du pool | `src/UsageStats.cs:RunBackgroundFlushes` |

#### Temporisations et réessais

Pas de réessai propre : l'état sale est repris au prochain vidage périodique (`WM_TIMER`, `src/TrayApplication.cs`). Écriture atomique : `.tmp` suffixé par le PID, `File.Replace` ou `File.Move`.

#### États ou transitions invalides atteignables

C5.3

### M5.2 — Chargement et lecture de UsageStats

**Nature** : implicite. **Fichiers** : `src/UsageStats.cs`.

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `_loaded` | bool | `src/UsageStats.cs:555` | UI, pool | `_lock` |
| `_readFailed` | bool | `src/UsageStats.cs:35,99,597` | UI, pool | `_lock` |

#### États

| état | prédicat |
|---|---|
| Non chargé | `!_loaded` |
| Chargé | `_loaded && !_readFailed` |
| Lecture échouée | `_loaded && _readFailed` |

#### Invariants

- I1 : un fichier illisible n'est pas écrasé — garanti (`src/UsageStats.cs:684` : le vidage est refusé si `_readFailed`).
- I2 : lecture impossible ≠ corruption (`FileQuarantine`) — garanti côté quarantaine.
- I3 : `_readFailed` ne se lève que sur changement de chemin — le seul chemin est le crochet de test `OverrideStatsPathForTests` (`src/UsageStats.cs:93-101`) : il n'y a pas de récupération en cours de session, par conception (limite voulue, pinnée par `UsageStatsReadFailureTests.cs:48-59` ; état absorbant C5.3).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | Non chargé | EnsureLoaded | lecture OK | Chargé | valeurs lues | `src/UsageStats.cs:552-555` |
| 2 | Non chargé | EnsureLoaded | lecture KO | Lecture échouée | `_readFailed = true` | `src/UsageStats.cs:597` |
| 3 | tout | changement de chemin | test | Non chargé | `_readFailed = false` | `src/UsageStats.cs:98-99` |
| 4 | Lecture échouée | Flush / Snapshot | — | Lecture échouée | refus | `src/UsageStats.cs:684` |

#### Temporisations et réessais

Aucun réessai de lecture dans le processus.

#### États ou transitions invalides atteignables

C5.3.

### M5.3 — Activité du jour et série

**Nature** : implicite. **Fichiers** : `src/UsageStats.cs`.

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `_lastActiveDate` | string? | `src/UsageStats.cs:444` | UI | `_lock` |
| `_currentStreak` | int | `src/UsageStats.cs:437` | UI | `_lock` |
| `_activeDaysCount` | int | `src/UsageStats.cs:444` | UI | `_lock` |

#### États

| état | prédicat |
|---|---|
| Aucun jour | `_lastActiveDate == null` |
| Actif aujourd'hui | `_lastActiveDate == today` |
| Série vivante | `_lastActiveDate` = hier |
| Série rompue | écart > 1 jour |

#### Invariants

- I1 : `_currentStreak >= 1` dès qu'un jour est compté — garanti (`src/UsageStats.cs:437`).
- I2 : un jour n'est compté qu'une fois — garanti (`src/UsageStats.cs:434`).
- I3 : la série ne dépend pas de la culture — garanti depuis ME-01 : `IsConsecutiveDay` et toutes les lectures de date passent par `StoredDate.Parse` (culture invariante, `src/StoredDate.cs`).
- I4 : la date n'avance que vers le futur — garanti depuis ME-10 : une dernière activité postérieure à aujourd'hui vaut aujourd'hui (`RecordActivityLocked` ne recompte pas le jour et ramène la date à aujourd'hui, `CurrentStreak` la lit comme aujourd'hui).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | Aucun jour | première frappe | — | Actif aujourd'hui | série 1 | `src/UsageStats.cs:437` |
| 2 | Actif aujourd'hui | frappe | même jour | idem | rien | `src/UsageStats.cs:434` |
| 3 | Série vivante | frappe | `IsConsecutiveDay` | Actif aujourd'hui | série + 1 | `src/UsageStats.cs:437-444` |
| 4 | Série rompue | frappe | non consécutif | Actif aujourd'hui | série = 1 | `src/UsageStats.cs:437` |
| 5 | Dernière activité datée d'après aujourd'hui (horloge ou fuseau reculé) | frappe | `StoredDate.Parse(_lastActiveDate) > today` | Actif aujourd'hui | date ramenée à aujourd'hui, série et jours inchangés (ME-10) | `src/UsageStats.cs:434-441` |

#### Temporisations et réessais

Aucune.

#### États ou transitions invalides atteignables

C5.1 corrigé (ticket ME-01) : invariant I3 rétabli, test `MachinesEtatsLot5Tests.Statistiques_DeuxJoursQuiSeSuivent_FontUneSerie_QuelleQueSoitLaCulture`. C5.7 corrigé (ticket ME-10) : invariant I4 rétabli, tests `MachinesEtatsLot5Tests.Statistiques_DerniereActiviteDansLeFutur_LeJourNEstPasRecompte` et `…_LaSerieEnCoursNEstPasRemiseAZero`.

### M5.4 — Plage de frappe exclue

**Nature** : explicite (compteur de profondeur). **Fichiers** : `src/UsageStats.cs`, `src/LessonsWindow.Tutorial.cs` (seul appelant de `BeginExcludedTyping`, via `BeginTutorial`, `:90-93`).

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `_excludedTypingDepth` | int | `src/UsageStats.cs:62,219,226` | UI | `_lock` |

#### États

| état | prédicat |
|---|---|
| Comptée | `_excludedTypingDepth == 0` |
| Exclue | `_excludedTypingDepth > 0` |

#### Invariants

- I1 : profondeur jamais négative — garanti (`src/UsageStats.cs:226`).
- I2 : toute plage ouverte est refermée — garanti par `EndTutorial` dans `Hide`/`Dispose`/`OpenChallenge` (garde `_tutorialExcludesTyping`).
- I3 : l'exclusion ne couvre que la frappe du tutoriel : la plage n'est ouverte que par `BeginTutorial` (`src/LessonsWindow.Tutorial.cs:90-93`), pas par les exercices ordinaires des Leçons, qui restent comptés (`UsageStatsTests.cs:362-364`) ; ce que la plage exclut exactement reste l'objet de C5.2.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | Comptée | BeginExcludedTyping | — | Exclue | profondeur 1 | `src/UsageStats.cs:217-219` |
| 2 | Exclue | BeginExcludedTyping | — | Exclue | profondeur + 1 | `src/UsageStats.cs:219` |
| 3 | Exclue | EndExcludedTyping | profondeur > 0 | Comptée ou Exclue | profondeur - 1 | `src/UsageStats.cs:224-226` |
| 4 | Exclue | RecordEmittedText | — | Exclue | texte ignoré | `src/UsageStats.cs:269` |

#### Temporisations et réessais

Aucune.

#### États ou transitions invalides atteignables

C5.2.

### M5.5 — Signal de seuil et collecte

**Nature** : implicite. **Fichiers** : `src/UsageStats.cs`.

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `CollectionEnabled` | bool (politique, cache du processus) | `src/PolicyManager.cs:92-123` | UI, pool (lu aussi par `UsageStats.FlushInBackground` -> `Flush` -> `SnapshotLocked`, `src/UsageStats.cs:659-667, 680-684`) | cache du processus |

#### États

| état | prédicat |
|---|---|
| Collecte active | `CollectionEnabled` |
| Collecte coupée | `!CollectionEnabled` |

#### Invariants

- I1 : aucune donnée n'est enregistrée quand la collecte est coupée — garanti (`src/UsageStats.cs:561,680`).
- I2 : la politique ne bascule pas en cours de processus — garanti par le cache (`src/PolicyManager.cs:92-123`).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | Collecte active | Record* | — | idem | compteur | `src/UsageStats.cs:234-246` |
| 2 | Collecte coupée | Record* | — | idem | retour | `src/UsageStats.cs:561` |
| 3 | Collecte coupée | Snapshot | — | idem | `null` | `src/UsageStats.cs:680` |

#### Temporisations et réessais

Aucune.

#### États ou transitions invalides atteignables

Aucun constat.

### M5.6 — Fenêtre Statistiques

**Nature** : implicite. **Fichiers** : `src/UsageStatsWindow.cs`.

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `ChallengeSectionVisible` | prédicat | `src/UsageStatsWindow.cs:37-39` | UI | fil unique |
| `WinContentH` | int | `src/UsageStatsWindow.cs:42-45` | UI | fil unique |

#### États

| état | prédicat |
|---|---|
| Fermée | pas de fenêtre |
| Ouverte, section Défi visible | `ChallengeSectionVisible` |
| Ouverte, section Défi masquée | `!ChallengeSectionVisible` |
| Copie confirmée | minuterie de copie active (armée par `OnCopyStats`, `src/UsageStatsWindow.cs:493-502`, `SetTimer` `:501` ; détruite par son propre `WM_TIMER`, `:453-460`) |

#### Invariants

- I1 : la mise en page et la peinture utilisent le même prédicat — garanti depuis ME-07 : la mise en page est refaite (`FitWindow`) dès que l'opt-in du Défi change pendant l'ouverture (`ConfigManager.TrainingEnabledChanged`, `OnTrainingEnabledChanged`), au lieu de ne l'être qu'à `Show` et au changement de langue (C5.5 corrigé).
- I2 : la minuterie de copie est détruite à la fermeture — garanti depuis 1.3.0 (17 novembre 2026) : `Close()` fait `KillTimer(TIMER_COPY_FEEDBACK)` et remet `_showCopiedFeedback` et `_copyFailed` à faux ; une réouverture ne montre plus un retour de copie périmé. `OnCopyStats` l'arme. Test : `Regles130PurTests.M5_6_I2_MinuterieDeCopie_DetruiteALaFermeture`.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | Fermée | Show | — | Ouverte | taille calculée | `src/UsageStatsWindow.cs:334-345` |
| 2 | Ouverte | Paint | `ChallengeSectionVisible` | idem | section peinte | `src/UsageStatsWindow.cs:658` |
| 2b | Ouverte (ou masquée) | opt-in du Défi changé (Paramètres, accueil) | fenêtre créée | idem | `FitWindow` (hauteur, boutons), repeinte (ME-07) | `OnTrainingEnabledChanged` |
| 3 | Ouverte | copie (`OnCopyStats`) | — | Copie confirmée | indicateur posé, minuterie armée (1500 ms) | `src/UsageStatsWindow.cs:493-502` |
| 4 | Copie confirmée | `WM_TIMER` de la minuterie de copie | — | Ouverte | minuterie détruite, libellé restauré | `src/UsageStatsWindow.cs:453-460` |
| 5 | Ouverte ou Copie confirmée | fermeture (`Close`) | — | Fermée (masquée) | `ShowWindow(0)`, `_visible = false` ; la minuterie de copie, si armée, n'est pas détruite | `src/UsageStatsWindow.cs:354-358` |

#### Temporisations et réessais

Minuterie de confirmation de copie, courte, sans réessai.

#### États ou transitions invalides atteignables

C5.5 corrigé (ticket ME-07) : invariant I1 rétabli, test `MachinesEtatsLot5Tests.Statistiques_SectionDefiMasqueePendantLOuverture_LaHauteurDeMiseEnPageSuit`.

### M5.7 — Magasin de progression (LessonProgressStore)

**Nature** : implicite. **Fichiers** : `src/LessonProgressStore.cs`, `src/FileQuarantine.cs`.

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `_loadFailed` | bool | `src/LessonProgressStore.cs:17,393,408,496,508` | UI | fil unique |
| `_knownWriteUtc` | DateTime | `src/LessonProgressStore.cs` | UI | fil unique |

#### États

| état | prédicat |
|---|---|
| Non chargé | pas de chargement |
| Chargé | `!_loadFailed` |
| Chargement échoué | `_loadFailed` |

#### Invariants

- I1 : un fichier illisible n'est pas écrasé — garanti (`src/LessonProgressStore.cs:528`).
- I2 : un rechargement conditionnel ne remplace pas un état échoué — garanti (`src/LessonProgressStore.cs:371`).
- I3 : un chargement ne lève jamais d'exception sur contenu JSON valide mais de forme inattendue — garanti depuis ME-02 : `Load` ignore toute entrée d'`exercises` qui n'est pas un objet, comme il le faisait déjà pour `gestures`. Avant le correctif, l'effet réel n'était pas une perte de progression : une `InvalidOperationException` (hors `FileQuarantine.IsCorruption`, `src/FileQuarantine.cs:37`, donc non attrapée par le filtre `src/LessonProgressStore.cs:482`) sortait du constructeur de `LessonProgressStore` et rendait la fenêtre Leçons inouvrable (`src/LessonsWindow.cs:243`, `src/TrayApplication.cs:1514`, journalisé en `src/TrayApplication.cs:1119-1124`). Restait faux après ME-02 pour une chaîne à demi-caractère isolé (`"\uD800"`), dans les trois fichiers : garanti depuis AF-01 (`docs/audit-fichiers.md`, `FileQuarantine.ParseUserFile`).
- I4 : `ResetAll` remet tout l'état de progression — remet exercices, gestes, `LastModuleId`, `LastLessonId`, `LastExerciseIndex` et `_onboardingSynced` (`src/LessonProgressStore.cs:292-303`) ; le résidu d'`_onboardingSynced` (rangs réimportés depuis `ConfigManager.LearningCompletedExercises`) est voulu (`:270-275`). Ce qui reste de C5.4 est ce que `ResetAll` ne couvre pas.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | Non chargé | Load | JSON valide | Chargé | modèles remplis | `src/LessonProgressStore.cs:389-512` |
| 2 | Non chargé | Load | E/S ou corruption | Chargement échoué | quarantaine si corruption | `src/LessonProgressStore.cs:482,496,508` |
| 3 | Chargé | ReloadIfChangedOnDisk | mtime changé | Chargé | rechargement | `src/LessonProgressStore.cs:369-381` |
| 4 | Chargement échoué | ReloadIfChangedOnDisk | — | Chargement échoué | retour | `src/LessonProgressStore.cs:371` |
| 5 | Chargé | Save | — | Chargé | écriture atomique | `src/LessonProgressStore.cs:521-609` |
| 6 | Chargé | ResetAll | — | Chargé | réinitialisation | `src/LessonProgressStore.cs:292-307` |

#### Temporisations et réessais

Aucun réessai : l'état d'échec est collant jusqu'au prochain démarrage, par décision documentée (`src/LessonProgressStore.cs:494-495`, « le prochain lancement relit »).

#### États ou transitions invalides atteignables

C5.3, C5.4. C5.6 corrigé (ticket ME-02) : invariant I3 rétabli, tests `MachinesEtatsLot5Tests.Progression_UneEntreeNonObjet_NeFaitPasEchouerLeChargement` et `…Progression_EntreesMalFormees_IgnoreesSansPerdreLesExercicesValides`.

### M5.8 — Progression d'un exercice (Leitner)

**Nature** : implicite. **Fichiers** : `src/LessonProgressStore.cs`, `src/LessonContentHistory.cs`, `src/LessonInstructionHistory.cs`.

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| hash / clé stable | string | `src/LessonProgressStore.cs:456` | UI | fil unique |
| dernière réussite | DateTimeOffset? | `src/LessonProgressStore.cs:469` | UI | fil unique |

#### États

| état | prédicat |
|---|---|
| Jamais fait | pas d'entrée |
| Réussi | entrée avec date de réussite |
| Empreinte périmée | hash remplacé (`LegacyHashes`, `SupersededKeys`) |

#### Invariants

- I1 : une migration d'empreinte n'écrase pas une progression plus récente — garanti par `MergeLegacy` (`src/LessonProgressStore.cs:88-123`).
- I2 : une entrée dont l'objet est mal formé ne fait pas échouer tout le chargement — garanti depuis ME-02 : `if (el.ValueKind != JsonValueKind.Object) continue;` avant la lecture de l'entrée (`src/LessonProgressStore.cs:455-460`).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | Jamais fait | RecordSuccess | — | Réussi | entrée créée | `src/LessonProgressStore.cs:180-214` |
| 2 | Réussi | RecordSuccess | — | Réussi | date mise à jour | `src/LessonProgressStore.cs:180-214` |
| 3 | Empreinte périmée | MergeLegacy | hash ancien connu | Réussi | fusion | `src/LessonProgressStore.cs:88-123` |
| 4 | (fichier) | `Load`, entrée d'`exercises` | la valeur n'est pas un objet (nombre, `null`, tableau, chaîne) | inchangé | entrée ignorée, les autres sont gardées, fichier non mis de côté (ME-02) | `src/LessonProgressStore.cs:455-460` |

#### Temporisations et réessais

Aucun.

#### États ou transitions invalides atteignables

C5.6 corrigé (ticket ME-02) : invariant I2 rétabli, test `MachinesEtatsLot5Tests.Progression_EntreesMalFormees_IgnoreesSansPerdreLesExercicesValides`.

### M5.9 — Maîtrise d'un geste (boîtes Leitner)

**Nature** : explicite (six boîtes). **Fichiers** : `src/GestureMastery.cs`, `src/Gesture.cs`.

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| boîte | 0 à 5 | `src/GestureMastery.cs:79-97` | UI | fil unique |
| date de dernière revue | date | `src/GestureMastery.cs` | UI | fil unique |

#### États

Boîtes 0 à 5, intervalles 0 / 1 / 3 / 7 / 16 / 35 jours.

#### Invariants

- I1 : boîte dans [0, 5] — garanti par `Apply` (`src/GestureMastery.cs:79-97`).
- I2 : un geste dû l'est selon l'intervalle de sa boîte — garanti (`src/GestureMastery.cs:103-114`).
- I3 : une horloge qui recule ne rend pas un geste dû en avance — l'échéance est absolue (`DueUtc = now + intervalle`, `src/GestureMastery.cs:96` ; `IsDue` : `due <= now`, `:28`) : un recul d'horloge retarde l'échéance, il ne l'avance pas. Aucun constat ne le couvre : C5.7 ne porte que sur la série, les rappels et `WouldCount`, pas sur `GestureMastery`.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | boîte b | réussite | b < 5 | b + 1 | date mise à jour | `src/GestureMastery.cs:79-97` |
| 2 | boîte 5 | réussite | — | 5 | date mise à jour | `src/GestureMastery.cs:79-97` |
| 3 | boîte b | échec | — | repli | date mise à jour | `src/GestureMastery.cs:79-97` |

#### Temporisations et réessais

Intervalles Leitner uniquement. Le brief « 3 réussites sans indice sur 2 jours » n'existe pas dans le code.

#### États ou transitions invalides atteignables

Aucun constat.

### M5.10 — État de séance du Défi (ChallengeSessionState)

**Nature** : implicite. **Fichiers** : `src/ChallengeSessionState.cs`.

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `_requested` | bool | `src/ChallengeSessionState.cs` | UI | fil unique |
| `_lessonKey` | string? | `src/ChallengeSessionState.cs` | UI | fil unique |
| `_moduleDate` | DateOnly | `src/ChallengeSessionState.cs` | UI | fil unique |
| `_moduleSequenceIndex` | uint | `src/ChallengeSessionState.cs` | UI | fil unique |

#### États

| état | prédicat |
|---|---|
| Aucune demande | `!_requested` |
| Demandée | `_requested` |
| Module daté | `_moduleDate` posé (module figé au jour de sa création) |

#### Invariants

- I1 : un module ouvert avant minuit reste daté de son jour — garanti (module daté).
- I2 : un rejeu ne recompte pas — garanti par `WouldCount` (`src/TrainingReminders.cs:146-152`).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | Aucune demande | demande du Défi | — | Demandée | clé posée | `src/ChallengeSessionState.cs` |
| 2 | Demandée | ouverture de la séance | — | Module daté | date et étape figées | `src/ChallengeSessionState.cs` |
| 3 | Module daté | fin de séance | — | Aucune demande | comptage éventuel | `src/ChallengeSessionState.cs` |

#### Temporisations et réessais

Aucune.

#### États ou transitions invalides atteignables

Aucun constat.

### M5.11 — Séquence du Défi et comptage des séances

**Nature** : explicite (indice de séquence 0 à 5, puis défi commun). **Fichiers** : `src/TrainingReminders.cs`, `src/DailyChallenge.cs`, `src/ConfigManager.cs`.

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `TrainingSequenceIndex` | uint | `src/ConfigManager.cs:790-819` | UI | fil unique |
| `TrainingLastSessionDate` | string | `src/TrainingReminders.cs:134` | UI | fil unique |
| `_challengesCompletedCount` | int | `src/UsageStats.cs:246` | UI | `_lock` |

#### États

| état | prédicat |
|---|---|
| Séquence en cours | `TrainingSequenceIndex < DailyChallenge.SequenceLength` |
| Défi commun | `TrainingSequenceIndex >= SequenceLength` |
| Fait aujourd'hui | `StoredDate.Parse(TrainingLastSessionDate) >= today` (une date future vaut aujourd'hui, ME-10 ; `RepairFutureDates` la réécrit à aujourd'hui, donc le lendemain la séance compte de nouveau) |

#### Invariants

- I1 : une étape ne compte qu'une fois — garanti (`src/TrainingReminders.cs:149-151`).
- I2 : en défi commun, une séance par jour — garanti, la date est relue par `StoredDate.Parse` en culture invariante (ME-01).
- I3 : trois écritures de configuration indépendantes dans `MarkSessionCompleted` restent cohérentes — non garanti en cas d'arrêt du processus entre `:134` et `:137` (pas de constat : fenêtre de quelques instructions, écriture config atomique).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | Séquence en cours | séance finie, étape jouée == indice | `WouldCount` | indice + 1 | date, compteur | `src/TrainingReminders.cs:130-139` |
| 2 | Séquence en cours | rejeu d'une étape passée | `!WouldCount` | idem | rien | `src/TrainingReminders.cs:132` |
| 3 | Défi commun | séance finie | pas déjà faite aujourd'hui | idem | date, compteur | `src/TrainingReminders.cs:130-139` |
| 4 | Défi commun | séance finie | déjà faite aujourd'hui | idem | rien | `src/TrainingReminders.cs:151` |

#### Temporisations et réessais

Aucune.

#### États ou transitions invalides atteignables

C5.1 corrigé (ticket ME-01) : invariant I2 rétabli, test `MachinesEtatsLot5Tests.DefiCommun_UneSeanceDejaFaiteAujourdhui_NeCompteJamaisDeuxFois_QuelleQueSoitLaCulture`. C5.7 corrigé (ticket ME-10) : `WouldCount` lit une séance datée d'après aujourd'hui comme faite aujourd'hui, test `MachinesEtatsLot5Tests.DefiCommun_SeanceDatee_DuLendemain_NeSeRecomptePasAujourdhui`. Suite : la date future est réécrite à aujourd'hui (`RepairFutureDates`), test `…DefiCommun_SeanceDateeDe400Jours_LeLendemainLaSeanceCompteDeNouveau`. Les deux appels de la réparation ont chacun leur test : la fin de séance (`ChallengeSessionState.Complete`, jour réel et non celui du module), test de comportement `FinDeSeance_RepareUneDateLointaineDansLeFutur` ; le rappel (`MaybeShowTrainingReminder`, appel avant la garde `NotificationsEnabled`), test de texte source `MaybeShowTrainingReminder_ReparePuisSeulementEnsuiteLaGardeDesNotifications`.

### M5.12 — Rappel d'entraînement (balloon)

**Nature** : implicite. **Fichiers** : `src/TrainingReminders.cs`, `src/TrayApplication.cs`, `src/ReviewPromptGate.cs`.

#### Variables d'état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `TrainingIgnoredCount` | uint | `src/TrainingReminders.cs:116,121` | UI | fil unique |
| `TrainingLastReminderDate` | string | `src/TrainingReminders.cs:112-113` | UI | fil unique |
| `_pendingBalloon` | état d'instance | `src/TrayApplication.cs` | UI | fil unique |

#### États

| état | prédicat |
|---|---|
| Actif | `Enabled && IgnoredCount < 3` |
| Arrêté | `IgnoredCount >= MaxIgnored` (`src/TrainingReminders.cs:60`) |
| Ballon en attente | `_pendingBalloon` posé |

#### Invariants

- I1 : un seul rappel par jour — garanti (`src/TrainingReminders.cs:66`).
- I2 : jamais deux balloons cliquables le même jour, l'avis prime — garanti (`src/TrainingReminders.cs:63`, garde symétrique côté avis).
- I3 : trois rappels ignorés arrêtent définitivement — garanti (`src/TrainingReminders.cs:60,120-121`).
- I4 : une date de rappel ou de séance postérieure à aujourd'hui ne bloque que la journée en cours — garanti depuis la suite de ME-10 : `RepairFutureDates` la ramène à aujourd'hui et la réécrit à chaque tick et à chaque fin de séance.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | Actif | tick après 17 h | `ShouldRemind` | Ballon en attente | date marquée avant affichage | `src/TrainingReminders.cs:56-92,112` |
| 2 | Ballon en attente | NIN_BALLOONUSERCLICK | — | Actif | ignorés = 0 | `src/TrainingReminders.cs:116` |
| 3 | Ballon en attente | NIN_BALLOONTIMEOUT | — | Actif ou Arrêté | ignorés + 1 | `src/TrainingReminders.cs:120-121` |
| 4 | Actif | tick (toute heure) | date de dernier rappel ou de dernière séance > aujourd'hui | Actif | dates réécrites à aujourd'hui (ME-10, suite) | `TrainingReminders.RepairFutureDates`, `src/TrayApplication.cs` (`MaybeShowTrainingReminder`) ; test de texte source `MaybeShowTrainingReminder_ReparePuisSeulementEnsuiteLaGardeDesNotifications` |
| 5 | Séquence en cours ou Défi commun | séance finie (`ChallengeSessionState.Complete`) | date de dernier rappel ou de dernière séance > aujourd'hui (jour réel) | idem | dates réécrites à aujourd'hui avant `MarkSessionCompleted` | `src/ChallengeSessionState.cs` (`Complete`), test `FinDeSeance_RepareUneDateLointaineDansLeFutur` |

#### Temporisations et réessais

Pas avant 17 h (`:38`) ; un seul rappel par jour ; arrêt au 3e ignoré. Pas de réessai le même jour. `Snapshot` relit `TrainingLastSessionDate` et `TrainingLastReminderDate` par `StoredDate.Parse` (`:101-102`, culture invariante depuis ME-01) : un échec de lecture fausse directement les gardes `LastReminderDate == today` et `LastSessionDate == today` de `ShouldRemind` (`:66-67`).

#### États ou transitions invalides atteignables

C5.1 corrigé (ticket ME-01) : gardes `LastReminderDate` et `LastSessionDate` rétablies, test `MachinesEtatsLot5Tests.Rappel_UnSeulParJour_QuelleQueSoitLaCulture`. C5.7 corrigé (ticket ME-10) : `ShouldRemind` traite un rappel ou une séance datés d'après aujourd'hui comme datés d'aujourd'hui (`>= today`), tests `MachinesEtatsLot5Tests.Rappel_DernierRappelDateDuLendemain_NeRepartPasAujourdhui` et `…Rappel_DerniereSeanceDateeDuLendemain_VautAujourdhui`. Suite : les dates lointaines sont réécrites à aujourd'hui (`RepairFutureDates`), test `…Rappel_DatesLointainesDansLeFutur_LeLendemainLeRappelRepart`. Le test `MaybeShowTrainingReminder_ReparePuisSeulementEnsuiteLaGardeDesNotifications` lit la méthode : la réparation précède la garde `NotificationsEnabled`, sans quoi un utilisateur qui a coupé les notifications garderait une date lointaine que rien ne répare.

### Frontières avec les autres lots

- `src/TrayApplication.cs` (timers, ballon, écoute des notifications de la barre) et `src/ReviewPromptGate.cs` : lot propriétaire de la barre des tâches ; ce lot ne couvre que l'appel à `TrainingReminders`.
- `src/ConfigManager.cs` : persistance des champs `Training*` (lot configuration).
- `src/LessonsWindow.cs` et `LessonsWindow.Tutorial.cs` : fenêtre Leçons (appelante de `BeginExcludedTyping`, `WouldCount`).
- `src/TypingEngine.Windows/KeyMapper.cs:1077` : appelant du hook (`RecordEmittedText`).
- `src/Program.cs` : instance unique.
- `src/FileQuarantine.cs` : quarantaine, partagée.

## Lot 6 — Fenêtre des Leçons

Lecture seule, HEAD 250c4ef. Fichiers non lus en détail : `LessonsWindow.Paper.cs`, `.Notation.cs`, `.Explanations.cs` (dessin), `LessonHintProvider.cs` (lecture partielle par Grep seulement). Résultat honnête : aucun bogue solide trouvé ; deux constats faibles (confiance basse).

### Modèle de fils du lot

- Un seul fil écrit l'état : la pompe de messages principale. Le hook `WH_KEYBOARD_LL` rappelle sur le fil qui l'a installé (`KeyboardHook.cs:392` -> `RawKeyDownDetailed` -> `OnRawKeyDown`, `LessonsWindow.cs:3197`). Les `WM_TIMER`, `WM_CHAR`, `WM_LBUTTONDOWN`, `WM_PAINT` arrivent sur la même pompe.
- UI Automation : `ServerSideProvider | UseComThreading` (`LessonsAutomation.cs:258`) ; les appels COM sont ramenés sur le fil STA ; `Invoke` est différé (`_pendingInvoke`, `RunPendingInvoke` `LessonsWindow.cs:1520`) et rejoué sur la pompe, avec contrôle du nom de la zone (`_controlViews[index].Name == name`). Aucune écriture d'état hors pompe.
- Pas de course entre fils dans ce lot. Les seules réentrances : pompe imbriquée de `MessageBoxW` (`ResetProgress`, l.2910), `SetFocus`/`ShowWindow` synchrones, entrées du tray (`ShowChallenge`).
- `KillTimer` ne purge pas un `WM_TIMER` déjà posté : les gestionnaires de minuterie revérifient leurs conditions (`OnTimer`, l.2639).

### M6.1 — Séance de frappe (LessonTypingSession)

**Nature** : explicite (`LessonCharacterState`, modes Strict/Flexible). **Fichiers** : `src/LessonTypingSession.cs`, `src/TypingTolerance.cs`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_typedLine`, index de ligne/caractère | string/int | `LessonTypingSession.cs` | pompe | mono-fil |
| `IsLineComplete`, `IsExerciseComplete` | bool | idem | pompe | mono-fil |
| stats `LessonAttemptStats` | classe | idem | pompe | mono-fil |

#### États

| état | prédicat |
|---|---|
| EnCours | ni `IsLineComplete` ni `IsExerciseComplete` |
| LigneTerminée | `IsLineComplete` |
| ExerciceTerminé | `IsExerciseComplete` |

#### Invariants

- I1 : une frappe n'est jamais prise en compte hors EnCours — garanti (`TypeChar` ignore si `IsExerciseComplete || IsLineComplete`).
- I2 : `\r`, `\n` et substitut bas isolé sont ignorés ; substitut haut devient `'�'` — garanti dans `TypeChar`.
- I3 : en Strict, Retour arrière est ignoré — garanti (`Backspace`).
- I4 : la tolérance ne s'applique qu'à Initiation, Défi, Votre texte — garanti (`TypingTolerance.AppliesTo`).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | EnCours | caractère juste | dernier de la ligne | LigneTerminée | stats | `TypeStrict`/`TypeFlexible` |
| 2 | EnCours | caractère faux | Strict | EnCours | erreur comptée | `TypeStrict` |
| 3 | EnCours | caractère faux | Flexible | EnCours | `_typedLine += c` | `TypeFlexible` |
| 4 | LigneTerminée | avance | ligne suivante existe | EnCours | `AdvanceCompletedLine` | idem |
| 5 | LigneTerminée | avance | dernière ligne | ExerciceTerminé | — | idem |
| 6 | tout | `ResetExercise` | — | EnCours | remise à zéro | idem |
| 7 | ExerciceTerminé / LigneTerminée | frappe | — | inchangé | ignorée | `TypeChar` |

#### Temporisations et réessais

Aucune dans la machine ; la complétion est portée par `CompleteExercise` (M6.2). Le catalogue ne contient aucun contenu avec ligne vide (`lessons.json`), `LessonCustomText` n'en produit pas.

#### États ou transitions invalides atteignables

Aucun constat.

### M6.2 — Mode de fenêtre et bilan (_mode, _showSummary, _session)

**Nature** : implicite. **Fichiers** : `src/LessonsWindow.cs`, `LessonsWindow.Home.cs`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_mode` | WindowMode (Lessons/Free) | `LessonsWindow.cs:146` | pompe | mono-fil |
| `_settingsOpen` | bool | :147 | pompe | mono-fil |
| `_session` | LessonTypingSession | :151 | pompe | mono-fil |
| `_showSummary` | bool | :152 | pompe | mono-fil |
| `_swallowActivationSpace` | bool | :154 | pompe | mono-fil |
| `_homeComposed` | nullable | `Home.cs` | pompe | mono-fil |

#### États

| état | prédicat |
|---|---|
| Frappe | `_mode==Lessons && !_showSummary` |
| Bilan | `_mode==Lessons && _showSummary` |
| Libre | `_mode==Free` |
| Réglages | `_settingsOpen` |
| Composée | `_homeComposed != null` |
| Défi | `_homeComposed==null && CurrentModule.Id==DailyChallenge.ModuleId` |

#### Invariants

- I1 : `CompleteExercise` ne s'exécute qu'une fois par séance — garanti (`if (_showSummary) return;`, l.2672).
- I2 : toute nouvelle séance repart de `StartCurrentSession` (l.1096) — garanti : `ReplayExercise`, `GoTo` et `SelectExercise` y passent. Cela ne garantit pas la position enregistrée (voir I4).
- I3 : fermer la fenêtre solde un bilan lu — garanti sauf en mode Libre (`_mode != Lessons`) et pendant le parcours de l'accueil. Depuis ME-12 (C6.1, décision du 16 novembre 2026 : « Rendre la garde active »), `HideWindow` lit `IsTutorialActive` avant `EndTutorial` et le passe à `SettleSessionOnHide(tutorialWasActive)` : la fermeture, « Terminer » et « Passer pour l'instant » du parcours ne soldent rien, le bilan d'une étape n'avance pas la position du catalogue. Avant : `EndTutorial` remettait le parcours à zéro avant la garde, qui était toujours fausse.
- I4 : la position enregistrée ne désigne jamais le Défi — garanti depuis ME-07 par le magasin : `SetLastPosition` ignore un exercice du Défi, comme `RecordSuccess` (`LessonProgressStore.cs`). Avant : le bouton de remise à zéro est dans l'onglet Réglages (`LessonsWindow.Paper.cs:1001`), module courant inchangé ; après `ShowChallenge`, `ApplyProgressReset` enregistrait le Défi comme position (`StartCurrentSession(savePosition: true)`) (C6.2 corrigé).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | Frappe | dernier caractère juste | `!_showSummary` | Bilan | progression, stats | `CompleteExercise` l.2670 |
| 2 | Bilan | Suivant | `AdjacentPosition` non nul | Frappe | `GoTo` | `NextExercise` l.3011-3019 |
| 3 | Bilan | Rejouer | — | Frappe | `ReplayExercise` | l.3069 (bouton `LessonsWindow.Paper.cs:867`) |
| 4 | Bilan | Terminer (fin catalogue) | `AdjacentPosition` nul | Frappe | `StartCurrentSession(false)` puis page « Aujourd'hui » | `FinishPath` l.3033-3037 |
| 5 | Frappe/Bilan | choisir module/leçon | index valide | Frappe | `HomeClearComposed`, `_settingsOpen=false` | `SelectModule` l.2926, `SelectLesson` l.2939 |
| 6 | Frappe ou Bilan | Hide | `_mode == Lessons` et parcours de l'accueil non ouvert à l'appel (`tutorialWasActive` faux, ME-12 C6.1) | Frappe | `SettleSessionOnHide` : bilan lu soldé par `HomeFinishComposed`, `GoTo` ou `StartCurrentSession(false)` en fin de catalogue ; pendant le parcours, rien n'est soldé | `Hide`, `HideWindow`, `SettleSessionOnHide` |
| 7 | Défi | Hide | — | catalogue | `PickInitialSelection` | l.3063 |
| 8 | tout | reset confirmé | IDYES | Frappe | `ApplyProgressReset` ; sur le Défi, la séance repart sans enregistrer de position (ME-07) | l.2908-2924 |

#### Temporisations et réessais

`TIMER_FREE_STATS` : `KillTimer` puis réarmement si les conditions tiennent (l.2659-2667) ; `TIMER_HINT_CLEAR`, `TIMER_HINT_FLASH_CLEAR` coupés dans `Hide` (l.3253-3254). Pas de réessai. Autres combinaisons examinées sans suite : double clic sur « Continuer » (garde d'écran L4), `Invoke` UIA périmé (nom contrôlé), `WM_TIMER` périmé (conditions revérifiées).

#### États ou transitions invalides atteignables

C6.1 corrigé (ticket ME-12) : invariant I3 rétabli pendant le parcours, tests `MachinesEtatsLot6Tests.Fermeture_PendantLeParcours_NeSoldePasLaSeanceNiNAvanceLaPosition` et `…PasserPourLInstant_AuBilanDuParcours_NeSoldePasLaSeance`.
C6.2 corrigé (ticket ME-07) : invariant I4 rétabli, tests `MachinesEtatsLot6Tests.ReinitialiserLaProgression_SurLeDefi_NEnregistrePasLeDefiCommePosition` et `…_PuisFermer_RendLaPlaceDuCatalogue`.

### M6.3 — Tutoriel de l'accueil (_tutorialPage, _tutorialRoute)

**Nature** : implicite. **Fichiers** : `LessonsWindow.Tutorial.cs`, `TutorialSteps.cs`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_tutorialPage` | TutorialPage {None, Usages, Final} | `Tutorial.cs` | pompe | mono-fil |
| `_tutorialRoute` | RouteStep[] | `Tutorial.cs` | pompe | mono-fil |
| `_tutorialExcludesTyping` | bool | `Tutorial.cs` | pompe | drapeau d'idempotence d'`EndTutorial` |

#### États

| état | prédicat |
|---|---|
| Inactif | `_tutorialPage==None` et route vide |
| Choix des usages | `_tutorialPage==Usages` |
| Exercice k | route non vide, page None |
| Final | `_tutorialPage==Final` |

#### Invariants

- I1 : `EndTutorial` est idempotent — garanti (`_tutorialExcludesTyping`).
- I2 : fermer sur l'écran final vaut réussite — garanti (`Hide`, l.3241).
- I3 : route cohérente : tronc commun (rangs 0-2) puis usages — garanti (`TutorialSteps.BuildRoute`, `DoneThreshold = 3`).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | Inactif | `BeginTutorial` | — | Choix des usages / Exercice 0 | route construite | `Tutorial.cs` |
| 2 | Exercice k | réussite | k < dernier | Exercice k+1 | `AdvanceTutorialRoute` | idem |
| 3 | Exercice dernier | réussite | — | Final | — | idem |
| 4 | Exercice k | Précédent | k > 0 | Exercice k-1 | `TutorialPrevious` | idem |
| 5 | Final | Retour | — | Exercice dernier | `TutorialBackFromFinal` | idem |
| 6 | tout | Hide ou Continuer | — | Inactif | `EndTutorial(completed: Final)` | l.3241, `ContinueAfterTutorial` |

#### Temporisations et réessais

Aucune.

#### États ou transitions invalides atteignables

C6.1 corrigé (ticket ME-12) : la garde `IsTutorialActive` de `SettleSessionOnHide` agit, test `MachinesEtatsLot6Tests.Fermeture_PendantLeParcours_NeSoldePasLaSeanceNiNAvanceLaPosition`.

### M6.4 — Accueil des Leçons (_homePage, séance composée, texte collé)

**Nature** : implicite. **Fichiers** : `LessonsWindow.Home.cs`, `LessonsWindow.HomeText.cs`, `LessonsHomeLogic.cs`, `TextEditBuffer.cs`, `LessonCustomText.cs`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_homePage` | HomePage {Exercise, Today, Map, Text} | `Home.cs` | pompe | mono-fil |
| `_homeComposed` | nullable | `Home.cs` | pompe | mono-fil |
| `_homeEdit` | TextEditBuffer (4000, FormC) | `HomeText.cs` | pompe | mono-fil |
| caches révision / clé / texte | objets | `Home.cs`, `HomeText.cs` | pompe | clés d'invalidation (langue, jour, caractères échus, texte+langue) |

#### États

| état | prédicat |
|---|---|
| Exercice | `_homePage==Exercise` |
| Aujourd'hui | `_homePage==Today` |
| Carte | `_homePage==Map` |
| Texte | `_homePage==Text` |
| Séance composée | `_homeComposed != null` |

#### Invariants

- I1 : les index du catalogue ne sont pas modifiés par une séance composée — garanti (`HomeStartComposed`, séance séparée).
- I2 : changer de module/leçon sort de la séance composée — garanti (`HomeClearComposed` dans `SelectModule`/`SelectLesson`).
- I3 : le champ texte reste borné et coupé à une limite de grappe — garanti (`TextEditBuffer.Insert`).
- I4 : Hide oublie clic et texte collé — garanti (`HomeOnHide`, l.3251).
- I5 : un Défi périmé efface la séance composée — non garanti (`RefreshChallengeModuleIfStale` ne touche pas `_homeComposed` ; effet nul constaté car `HomeFinishComposed` s'exécute avant à la fermeture).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | Exercice | onglet Aujourd'hui/Carte/Texte | — | page cible | — | `Home.cs` |
| 2 | Aujourd'hui | Réviser / séance touche | échus non vide | Séance composée | `HomeStartReview`, `HomeStartKeySession` | `Home.cs` |
| 3 | Texte | Démarrer | texte non vide | Séance composée | `HomeTextBuild`, `HomeStartText` | `HomeText.cs` |
| 4 | Séance composée | bilan lu / Terminer | — | Exercice | `HomeFinishComposed` | `Home.cs` |
| 5 | Séance composée | langue changée | — | Séance composée | `HomeRetranslateComposed` | `Home.cs` |
| 6 | tout | Hide | — | page remise | `HomeOnHide` | l.3251 |

#### Temporisations et réessais

Aucune. `TextEditBuffer` : annulation limitée à `MaxUndo` = 200 ; `Cut` ne supprime pas si l'écriture au presse-papiers échoue.

#### États ou transitions invalides atteignables

Aucun constat.

### M6.5 — Indices (_hint*, minuteries d'indice)

**Nature** : implicite. **Fichiers** : `LessonsWindow.cs` (`ShowHintCore` l.2746, `ArmHintTimers` l.2793), `LessonHintProvider.cs`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_hint*` | divers | `LessonsWindow.cs:162-165` | pompe | mono-fil |
| `TIMER_HINT_CLEAR`, `TIMER_HINT_FLASH_CLEAR` | minuteries | l.20-28 | pompe | `KillTimer` + revérification |

#### États

| état | prédicat |
|---|---|
| Aucun indice | pas d'indice affiché |
| Indice affiché | `ShowHintCore` exécuté, minuteries armées |
| Clignotement | minuterie flash armée |

#### Invariants

- I1 : Hide coupe les minuteries d'indice — garanti (l.3253-3254).
- I2 : un `WM_TIMER` déjà posté après `KillTimer` est sans effet nuisible — non vérifié en détail (`LessonHintProvider.cs` non lu).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | Aucun | demande d'indice / erreur | — | Indice affiché | `ShowHintCore`, `ArmHintTimers` | l.2746, 2793 |
| 2 | Indice affiché | minuterie | — | Aucun | repeinture | `OnTimer` l.2639 |
| 3 | tout | Hide | — | Aucun | `KillTimer` ×2 | l.3253-3254 |

#### Temporisations et réessais

Deux minuteries à usage unique, réarmées à chaque indice.

#### États ou transitions invalides atteignables

Aucun constat.

### M6.6 — Mode Libre et ses statistiques

**Nature** : implicite. **Fichiers** : `LessonsWindow.cs`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_freeStartedAt`, `_freeChars`, `_freeBackspaces`, `_freeHighSurrogate` | nullable/int | `LessonsWindow.cs` | pompe | mono-fil |
| `TIMER_FREE_STATS` | minuterie | l.2905 | pompe | revérification dans `OnTimer` |

#### États

| état | prédicat |
|---|---|
| Libre inactif | `_mode==Free && !_freeStartedAt.HasValue` |
| Libre en cours | `_mode==Free && _freeStartedAt.HasValue` |

#### Invariants

- I1 : Hide remet les statistiques à zéro (l.3258-3261) mais garde le texte — garanti.
- I2 : la minuterie de stats ne tourne que si mode Libre, début posé, option visible (l.2900-2903) — garanti.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | inactif | première frappe | — | en cours | `_freeStartedAt` posé, minuterie | `OnChar` l.2563 |
| 2 | en cours | tick | conditions vraies | en cours | repeinture, réarmement | l.2659-2667 |
| 3 | en cours | Hide | — | inactif | stats à zéro, `KillTimer` | l.3252-3261 |

#### Temporisations et réessais

`FREE_STATS_REFRESH_MS` : minuterie recréée à chaque tick. En mode Libre, `SettleSessionOnHide` retourne tôt (`_mode != Lessons`, l.3047) : voulu, sans rapport avec C6.1.

#### États ou transitions invalides atteignables

Aucun constat.

### M6.7 — Fenêtre : visibilité, focus, pause d'entrée

**Nature** : implicite. **Fichiers** : `LessonsWindow.cs` (`_visible` l.86, `_hasFocus` l.90, `_inputPaused` l.311, `Hide` l.3238, `Dispose` l.3266), `LessonsAutomation.cs`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_visible`, `_hasFocus`, `_inputPaused` | bool | l.86, 90, 311 | pompe | mono-fil |
| génération UIA | int | `LessonsAutomation.cs` (`OnPainted`) | pompe/STA | événements différés par `PostMessage` |

#### États

| état | prédicat |
|---|---|
| Caché | `!_visible` |
| Visible avec focus | `_visible && _hasFocus` |
| Visible sans focus | `_visible && !_hasFocus` |
| Entrée en pause | `_inputPaused` |

#### Invariants

- I1 : Hide éteint Verr. Maj. (`RequestCapsLockOff`, l.3244) — garanti.
- I2 : les fournisseurs UIA d'une génération périmée lèvent via `ThrowIfGone` — garanti (`LessonsAutomation.cs`).
- I3 : `Dispose` libère les fournisseurs — garanti.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| 1 | Caché | Show | — | Visible | Verr. Maj. éteinte, `HomeOnShow` | `Show` |
| 2 | Visible | Hide | — | Caché | séquence l.3238-3261 | `Hide` |
| 3 | Visible | perte de focus | — | sans focus | repeinture | `WndProc` |
| 4 | Visible | `Invoke` UIA | nom concordant | inchangé | action rejouée | `RunPendingInvoke` l.1520 |

#### Temporisations et réessais

`Invoke` UIA différé ; un seul en attente (`_pendingInvoke`). Autres cas examinés sans suite.

#### États ou transitions invalides atteignables

C6.2 corrigé (ticket ME-07) : voir M6.2.

### M6.8 — File de texte physique attendu (PhysicalTextInputBuffer)

Même objet que M1.7 (lot 1, `PhysicalTextInputBuffer`), décrit en entier là : la fenêtre des Leçons en est l'unique consommateur, avec une péremption de 1000 ms (`_timeoutMs` côté Leçons), le hook et `WM_CHAR` étant sur le même fil (l'ordre est celui de la file système) ; constats C1.2 et C1.3.

### Constats

#### C6.1 — Garde `IsTutorialActive` inopérante dans `SettleSessionOnHide`

`Hide` appelait `EndTutorial` avant `SettleSessionOnHide` ; la garde `IsTutorialActive` était donc toujours fausse à cet appel, et fermer les Leçons sur un bilan du parcours avançait la position du catalogue (test rouge : (0, 0, 0) attendu, (0, 0, 1) obtenu). « Terminer » et « Passer pour l'instant » clôturaient le parcours avant `Hide` : même défaut. Gravité faible. Corrigé par ME-12 (décision d'Antoine, 16 novembre 2026, « Rendre la garde active ») : `HideWindow(tutorialCompleted)` lit l'état du parcours avant de le clore et le passe à `SettleSessionOnHide(tutorialWasActive)` ; `FinishTutorial` et `LeaveTutorialForNow` passent par `HideWindow` sans clore le parcours avant. Les autres lectures de `IsTutorialActive` sont inchangées.

#### C6.2 — La remise à zéro des Leçons enregistre le Défi comme dernière position

Le bouton de remise à zéro est dans l'onglet Réglages (`LessonsWindow.Paper.cs:1001`), qui laisse le module courant inchangé. Après `ShowChallenge`, le module courant est le Défi : `ResetProgress` (l.2908) puis `ApplyProgressReset` (l.2923) appellent `StartCurrentSession(savePosition: true)`, qui enregistre le Défi comme dernière position (l.1111-1112). `SetLastPosition` n'a pas la garde de `RecordSuccess` (`LessonProgressStore.cs:165-172` contre `:209-212`), alors que le code veut que le Défi rende la place du catalogue (`SettleSessionOnHide`, l.3063-3064). Effet secondaire : `PickInitialSelection` re-sélectionne le Défi depuis cette position (l.1050-1052). La réentrance du `MessageBoxW` (l.2910) n'est pas nécessaire. Défaut accessible par un enchaînement ordinaire ; gravité faible, confiance basse sur l'impact.

### Frontières avec les autres lots

- `KeyboardHook.cs` (hook bas niveau, `RawKeyDownDetailed`) et `KeyMapper` (CapsLock, touches mortes, couches) : lot clavier/moteur ; ce lot suppose que le callback est sur le fil de la pompe.
- `ChallengeSessionState`, `DailyChallenge` (`IsStale`, `Complete` daté) : lot Défi ; ici seule l'entrée par `ShowChallenge` et `RefreshChallengeModuleIfStale`.
- `LessonProgressStore` (`ResetAll`, `StableKey`), `ConfigManager` : lot persistance/config ; non relus.
- `LearningSessionTracker.Closed` : lot suivi de séance.
- Tray et `ShowChallenge` : lot shell/tray, source de la réentrance de C6.2.
- `LessonsAutomation` (UIA) : fil STA COM, seule frontière de fils du lot.

## Lot 7 — Accueil et demande d'avis

Chemins relatifs au dépôt `components/ag-machines-etats`. Le code fait foi ; numéros de ligne relevés sur la branche `claude/ag-machines-etats-130`.

### Modèle de fils du lot

| Fil | Ce qui y tourne | Preuve |
|---|---|---|
| Pompe de messages (fil principal) | Tout `OnboardingWindow`, `AboutWindow`, `LinkBehavior` (WndProc et sous-classes), `TrayApplication.WndProcCallback`, les minuteries `SetTimer` (`TIMER_REVIEW_QUIET`, `TIMER_STATS_FLUSH`, `TIMER_STARTUP_BALLOON`), `MaybeShowReviewPrompt`, `OpenReviewTarget`, `OnOnboardingClosed`. | `src/TrayApplication.cs:674` `Win32.SetTimer(_hWnd, (UIntPtr)TIMER_REVIEW_QUIET, REVIEW_QUIET_POLL_MS, IntPtr.Zero);` ; boucle `GetMessageW` `src/TrayApplication.cs:717`. |
| Rappel du hook clavier bas niveau | Même fil que la pompe (WH_KEYBOARD_LL est livré au fil qui a posé le hook, via sa boucle). Il appelle `UsageStats.RecordEmittedText` (écrit `_enrichedThresholdCrossed`, `_lastRemapTickCount`) sous `lock (_lock)`. | `src/TypingEngine.Windows/KeyboardHook.cs:160-161` « Install/Unhook et les callbacks s'exécutent sur le même thread » ; `src/UsageStats.cs:265` `lock (_lock)`. |
| Pool de threads | (a) `UsageStats.FlushInBackground` (tick `TIMER_STATS_FLUSH`), (b) `ConfigManager` : `System.Threading.Timer _saveTimer` et `FlushInBackground`, (c) `StoreReview.OnCompleted` (rappel de l'opération WinRT), (d) `ProbeRemoteHost` (hors lot). | `src/ConfigManager.cs:1392` `_saveTimer ??= new System.Threading.Timer(...)` ; `src/StoreReview.cs:72-75` « Exécuté sur un thread de pool, jamais sur le thread d'interface ». |
| Fil RPC COM | Activation d'un toast d'avis ; l'abonné doit re-router par `PostMessage` (`WM_APP_TOAST`), traité ensuite sur la pompe. | `src/ToastActivation.cs:68-69` ; `src/TrayApplication.cs:777-785`. |
| Aucun fil dédié | Ni l'accueil, ni À propos, ni la garde d'avis ne créent de fil. Aucun `DoEvents`. | recherche dans les six fichiers du lot. |

Réentrance possible sur la pompe (seule source de « course » du lot) : `MessageBoxW` imbriqués — `OnboardingWindow.ShowAutoStartError` (`src/OnboardingWindow.cs:1455`, propriétaire = l'accueil, donc l'accueil est désactivé pendant la boîte), `TrayApplication.ActivateWithConsent` (`src/TrayApplication.cs:1406`, propriétaire = la fenêtre masquée du tray : **l'accueil reste actif** pendant la boîte). Pendant ces pompes imbriquées, les `WM_TIMER` du tray (dont `TIMER_REVIEW_QUIET`) continuent d'être livrés. `SendMessageW(WM_KEYDOWN)` de la sous-classe des boutons (`src/OnboardingWindow.cs:1513`) est une réentrance synchrone sans effet d'état propre.

Protection des états du lot : champs d'instance de l'accueil et de À propos = fil unique de la pompe, aucun verrou nécessaire ; état persistant d'avis = `ConfigManager` (`lock (_lock)`, écriture disque différée sur le pool) ; signal de frappe = `UsageStats` (`lock (_lock)`).

### M7.1 — Accueil : navigation entre les étapes

**Nature** : explicite (constantes `OnboardingStep` + champ `_currentStep`). **Fichiers** : `src/OnboardingStep.cs`, `src/OnboardingWindow.cs`.

#### Variables d état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `_currentStep` | int (0..2) | `src/OnboardingWindow.cs:146` | pompe seule | fil unique ; bornes tenues par `GoToNextStep` (`:884`) et `GoToPreviousStep` (`:893`) |
| `OnboardingStep.Count/Last` | const | `src/OnboardingStep.cs:16-19` | — | constantes |
| `_step3Reached` | bool | `:156` | pompe seule | fil unique |
| `_stepChangedAt` | long? (ms, horloge `Clock`) | `:164` | pompe seule | fil unique ; posé par `GoToNextStep` et `GoToPreviousStep`, remis à nul par `Show` ; lu par `IsSecondClickOfDoubleClick` (`:680`) |
| `_nextMouseDoubleClick` | bool | `OnboardingWindow.cs` (après `_stepChangedAt`) | pompe seule | fil unique ; vrai de la réception de `WM_LBUTTONDBLCLK` par « Suivant » à la fin du `WM_LBUTTONUP` qui suit ; effacé par un nouvel appui, `WM_KILLFOCUS`, `WM_CANCELMODE` ; posé et effacé dans `ButtonArrowSubclassProc`, lu par `IsSecondClickOfDoubleClick` |
| `_visible` | bool | `:268` | pompe seule | fil unique |
| `_scroll` (position) | `WindowScroll` | `:277` | pompe seule | fil unique |

#### États

| état | prédicat |
|---|---|
| Étape 1 « Changes » | `_currentStep == 0` |
| Étape 2 « HowTo » | `_currentStep == 1` |
| Étape 3 « Resources » (dernière) | `_currentStep == OnboardingStep.Last` (2) |
| Fermé | `!_visible` (la fenêtre existe toujours : masquée, jamais détruite avant `Dispose`) |

#### Invariants

- I1 : `0 <= _currentStep <= Last`. Garanti par `src/OnboardingWindow.cs:884` `if (_currentStep >= OnboardingStep.Last) return;` et `:893` `if (_currentStep <= 0) return;`. Seul autre écrivain : `Show()` (`:917`), `ResetState()` (`:959`, Debug) et le banc de test.
- I2 : `_step3Reached` vrai ⇒ l'étape 3 a été affichée depuis le dernier `Show()`. Garanti par `:886` (posé à la transition) et `:919` (remis à faux au `Show`). Remis à faux aussi dans `Close` (`:1026`).
- I3 : atteindre l'étape 3 ne vaut pas validation ; seul `Close(validated: true)` valide. Garanti pour clavier (`FocusAfterStepChange` sort le focus des boutons) et, depuis ME-03, pour la souris : un `IDC_BTN_NEXT` reçu moins de `GetDoubleClickTime()` après un changement d'étape ET produit par un `WM_LBUTTONDBLCLK` du bouton (second clic d'un double clic de souris) est ignoré (`IsSecondClickOfDoubleClick`, garde à `:1157`) ; un clic posé après ce délai agit comme avant. Suite du 16 novembre : le filtre ne vise que ce second clic de souris ; Entrée, Espace, flèche, invocation par l'accessibilité (`BM_CLICK`) et « Précédent » puis « Suivant » enchaînés agissent toujours, même dans le délai. Seule la conversion d'un vrai double clic à la souris en `WM_LBUTTONDBLCLK` par le bouton système reste à vérifier à la main.
- I4 : le focus n'est jamais sur un contrôle masqué. Garanti par deux mécanismes distincts. (1) Contrôles de la dernière étape (cases, liens) : `:701-702` (`if (step3Vis == 0 && IsLastStepControl(GetFocus())) SetFocus(_hWndBtnNext)`), garde limitée à ces contrôles (`IsLastStepControl` `:876-878`). (2) « Suivant » masqué en état A de l'étape 1 (`:754`) : `Show()` place alors le focus sur « Essayer » (`:949`, `_currentStep == 0 && !_learningModuleDone`), jamais sur « Suivant ».
- I5 : un seul clic ne franchit qu'une étape. Garanti (une `WM_COMMAND` = un `GoToNextStep`) ; les deux clics d'un double clic de souris (moins que le délai de double clic de Windows) n'en franchissent plus qu'une depuis ME-03. Deux activations au clavier ou par l'accessibilité en franchissent deux (suite du 16 novembre). La flèche → / ↓ (T1.2) n'est pas soumise au filtre.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1.1 | étape 1 ou 2 | clic/Entrée sur « Suivant » (`IDC_BTN_NEXT`) | `_currentStep < Last` ; ignoré, sans effet, seulement si c'est le second clic d'un double clic de souris (`WM_LBUTTONDBLCLK` reçu par le bouton) dans le délai de double clic suivant un changement d'étape ; jamais pour Entrée, Espace ou l'accessibilité | étape +1 | `_step3Reached = true` si cible = dernière ; `UpdateStepVisibility` ; `FocusAfterStepChange` (focus → 1re case si dernière) | `:1130-1133`, `:882-889` |
| T1.2 | étape 1 ou 2 | flèche → ou ↓ | `ForwardArrowAction == Next` | étape +1 | idem T1.1 | `:1229-1236`, `:976-983` |
| T1.3 | étape 2 ou 3 | « Précédent » ou flèche ← / ↑ | `_currentStep > 0` | étape −1 | `UpdateStepVisibility` ; masque liens/cases ; focus → « Suivant » si focus sur la dernière étape | `:1142-1144`, `:1242-1245`, `:891-897` |
| T1.4 | étape 3 | « C'est parti ! » | `RequestActivation()` vrai ; second clic d'un double clic de souris dans le délai suivant l'arrivée à l'étape 3 ignoré, appui clavier ou accessibilité jamais | Fermé | voir M7.2 T2.5 | `:1133-1134` |
| T1.5 | étape 3 | flèche → / ↓ avec focus sur « Suivant » et activation acceptée | `ForwardArrowAction == Finish` | Fermé | `Close(validated: true)` | `:1237` |
| T1.6 | étape 1 ou 2 | flèche ← sur étape 1 / « Précédent » absent | `_currentStep <= 0` | inchangé | aucun | `:893` |
| T1.7 | toute | `WM_DPICHANGED` / `WM_SETTINGCHANGE` / `AppLanguageChanged` | — | inchangé | recomposition (`UpdateStepVisibility`, polices, palette) ; jamais d'annonce d'étape | `:1075-1087`, `:1105-1109`, `:1430-1435` |
| T1.8 | étape 3 | Entrée avec focus sur une case | `ButtonToPressOnEnter` | inchangé | bascule la case (`ToggleCheckBox`) — jamais le bouton final | `:1116-1125` |

#### Temporisations et réessais

Aucune minuterie propre. Aucun réessai.

#### États ou transitions invalides atteignables

C7.1 corrigé (ticket ME-03) : invariants I3 et I5 rétablis, test `MachinesEtatsLot7Tests.Accueil_DoubleClicSuivantÀLÉtape2_NeValidePasSansAvoirVuLÉtape3`. Suite : le filtre ne perd plus le second appui d'un clavier ou d'un lecteur d'écran, tests `Accueil_DeuxActivationsAuClavierRapprochées_AvancentDeDeuxÉtapes`, `Accueil_PrécédentPuisSuivantEnchaînés_AgissentTousLesDeux`, `Accueil_DoubleClicAbandonné_NeFiltrePasLActivationAuClavierSuivante`.

### M7.2 — Accueil : cycle de vie, activation et enregistrement des préférences

**Nature** : implicite (combinaison `_visible`, `_activationAccepted`, `_activationRefused`, `_step3Reached`, trois cases peintes et leur état « affiché »). **Fichiers** : `src/OnboardingWindow.cs`, `src/TrayApplication.cs` (appelant).

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_visible` | bool | `src/OnboardingWindow.cs:268` | pompe | fil unique |
| `_activationAccepted` | bool | `:186` (init = `ConfigManager.ActivationConsent`) | pompe | fil unique ; persistance = `ConfigManager.AcceptActivation` sous `_lock` |
| `_activationRefused` | bool | `:191` | pompe | fil unique ; remis à faux au `Show` (`:918`) |
| `_step3Reached` | bool | `:156` | pompe | fil unique ; consommé dès l'entrée de `Close` (`:1025-1026`) |
| `_autoStartChecked`, `_dontShowChecked`, `_trainingChecked` | bool | `:245-247` | pompe | fil unique |
| `_autoStartShownChecked` | bool | `:159` | pompe | fil unique |
| `_dontShowShownChecked` | bool | `:162` | pompe | fil unique ; posé par `Show` d'après `ConfigManager.ShowOnboardingUserSetting`, lu par `Close` |
| `ShowOnboardingAtStartup` (config) | bool (clé `showOnboardingAtStartup`) | `src/ConfigManager.cs:160-191` | pompe (accueil, Paramètres), pool (écriture disque) | `_lock` ; écriture différée 400 ms (`:1334`) |
| `_reviewPromptDeferred` | bool | `src/TrayApplication.cs:147` | pompe | fil unique |
| `_pendingOnboardingShow` | bool | `src/TrayApplication.cs:142` | pompe | fil unique |

#### États

| état | prédicat |
|---|---|
| Absent (jamais créé) | `_onboarding == null` |
| Masqué | `_onboarding != null && !_visible` |
| Ouvert sans accord | `_visible && !_activationAccepted && !_activationRefused` |
| Ouvert, activation refusée | `_visible && !_activationAccepted && _activationRefused` |
| Ouvert avec accord, étapes 1-2 | `_visible && _activationAccepted && !_step3Reached` |
| Ouvert avec accord, étape 3 vue | `_visible && _activationAccepted && _step3Reached` |
| Différé par conflit de disposition | `_pendingOnboardingShow` (accueil pas encore créé) |

#### Invariants

- I1 : jamais deux affichages (`Show`) concurrents ; un second appel ramène devant sans remettre à l'étape 1. Garanti par `src/TrayApplication.cs:1324-1328` (`ShowOnboardingNow`), `:883-887` (menu), `:1445-1448` (relance Démarrer). **Non garanti** pour la branche Debug `IDM_RESET_ONBOARDING` (`:951-967`, hors Release).
- I2 : aucune préférence n'est écrite sans accord d'activation ni étape 3 vue. Garanti par `src/OnboardingWindow.cs:652` et `:1027`.
- I3 : sur croix, Échap, `WM_CLOSE`, seule une case modifiée par l'utilisateur est persistée. Garanti par `ShouldApplyPreferenceOnClose` (`:993-998`), qui compare chaque case à son état affiché (`_dontShowShownChecked`, `_autoStartShownChecked`).
- I4 : la case « Ne plus afficher » affichée reflète le réglage persisté. Garanti depuis ME-03 : `Show()` la pose d'après le réglage de l'utilisateur seul (`ConfigManager.ShowOnboardingUserSetting`, pas la politique) : décochée tant qu'aucun refus n'est enregistré (décision v0.9.7.1 conservée), cochée sinon (`:956-957`). `Close` reçoit cet état affiché comme référence (`_dontShowShownChecked`) : la validation, case inchangée, réécrit la valeur déjà enregistrée ; Échap et la croix n'écrivent rien.
- I5 : `Close` est idempotent. Garanti en partie : `_step3Reached` consommé (`:1026`) ; mais `OnClosed` est rappelé à chaque `Close` (`:1056`) et `ShowWindow(0)` est rejoué. Sans effet d'état tant que `_reviewPromptDeferred` est déjà levé (`src/TrayApplication.cs:1427-1428`).
- I6 : la sollicitation d'avis ne s'affiche pas par-dessus l'accueil. Garanti depuis ME-08 : l'accueil du démarrage par `_reviewPromptDeferred` (`:313`), l'accueil rouvert depuis le menu par `_onboarding is { IsVisible: true }`, lu au tick de quiétude (`MaybeShowReviewAfterQuietTyping`).
- I7 : `_activationAccepted` vrai ⇔ `ConfigManager.ActivationConsent` vrai. Garanti par `src/TrayApplication.cs:1390-1391` ; mais la réentrance de `ActivateWithConsent` pendant sa boîte d'erreur rend l'appelant incohérent (voir « Temporisations et réessais »).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T2.1 | Absent | démarrage, `shouldShowOnboarding` vrai, pas de popup de conflit | — | Ouvert (sans accord ou avec accord) | `_reviewPromptDeferred = true` ; création ; `Show()` (étape 0, focus « Essayer » ou « Suivant ») | `src/TrayApplication.cs:305-325`, `src/OnboardingWindow.cs:915-950` |
| T2.2 | Absent | démarrage avec popup de conflit | `_layoutPopupOpen` | Différé | `_pendingOnboardingShow = true`, `_reviewPromptDeferred = true` | `:313-321` |
| T2.3 | Différé | « Garder l'app » | `_pendingOnboardingShow` | Ouvert | `ShowOnboardingNow` | `:1301-1312` |
| T2.4 | Masqué ou Absent | menu « Accueil », double-clic sans accord, `ShowLessonsWindow` sans accord, relance Démarrer sans accord | non visible | Ouvert | `Show()` ; ne touche **pas** `_reviewPromptDeferred` : le tick de quiétude lit la visibilité de l'accueil (C7.3 corrigé) | `:881-898`, `:748`, `:1474`, `:1453-1456` |
| T2.5 | Ouvert étape 3 | « C'est parti ! » ou flèche | `RequestActivation()` vrai | Masqué | `Close(validated: true)` : écrit `showOnboardingAtStartup = !_dontShowChecked` (case reprise du réglage à l'ouverture, C7.2 corrigé) et `ApplyAutoStart(_autoStartChecked)` ; `ShowWindow(0)` ; `OnClosed` | `src/OnboardingWindow.cs:1014-1057` |
| T2.6 | Ouvert | croix, Échap, `WM_CLOSE`, Échap sur un lien | — | Masqué | `Close(false)` : n'écrit qu'une case modifiée (et seulement avec accord + étape 3 vue) | `:970`, `:1250-1252`, `src/LinkBehavior.cs:136-139` |
| T2.7 | Ouvert sans accord | « Essayer/Activer » ou « Activer » (étape 3) | `ActivationRequested()` vrai | Ouvert avec accord | `_activationAccepted = true`, texte du bouton, `UpdateStepVisibility` | `:193-210`, `src/TrayApplication.cs:1382-1397` |
| T2.8 | Ouvert sans accord | idem | `ActivationRequested()` faux (hook non posé, `MessageBoxW`) | Ouvert, activation refusée | `_activationRefused = true`, « Suivant » apparaît | `:196-203`, `src/TrayApplication.cs:1402-1408` |
| T2.9 | Ouvert étape 3, accord | clic/Espace/Entrée sur case | `_step3Reached && _activationAccepted` | inchangé | écrit à l'instant (`SetShowOnboardingAtStartup` ou `ApplyAutoStart(on, true)`) ; Défi : `SetTrainingEnabled` sans condition | `:646-659` |
| T2.10 | Ouvert | `WM_CLOSE` sur fenêtre déjà masquée (mise à jour MSIX) | `!_visible` | Masqué | `Close(false)` rejoué ; `_step3Reached` déjà consommé ; `OnClosed` rappelé | `:1022-1024`, `:1056` |
| T2.11 | Masqué | `OnClosed` | `_activationConsent && _reviewPromptDeferred` | Masqué | lève le report, `MaybeShowReviewPrompt(Startup)` (seul l'essai 2 peut partir). Sans accord (`!_activationConsent`), retour immédiat : `_reviewPromptDeferred` reste vrai jusqu'à un accord, donc le signal de frappe (M7.5) reste différé tout ce temps | `src/TrayApplication.cs:1425-1432` |
| T2.12 | Ouvert | fermeture refusée par échec d'écriture « Lancer au démarrage » | `!AutoStartSetter(on)` | inchangé | case rendue à l'état réel, boîte d'erreur modale (accueil désactivé) | `src/OnboardingWindow.cs:668-681`, `:1453-1458` |

#### Temporisations et réessais

Aucune minuterie. Réessai de l'activation : l'utilisateur peut relancer « Essayer maintenant » ou « Activer » tant que `_hook.Install()` échoue (`src/TypingEngine.Windows/KeyboardHook.cs:151-153`, relançable : `Dispose` remet `_hookId` à zéro `:449-453`). Aucun compteur d'essais, donc aucune dérive. Réentrance : pendant la boîte de `ActivateWithConsent` (propriétaire = fenêtre du tray), l'accueil n'est pas désactivé ; un second clic relance `ActivateWithConsent` en imbriqué (hypothèse non retenue comme constat : l'échec de `SetWindowsHookEx` est rare et l'état final reste cohérent sauf `_activationRefused` périmé).

#### États ou transitions invalides atteignables

C7.3 corrigé (ticket ME-08) : invariant I6 rétabli, test `MachinesEtatsLot7Tests.Avis_AccueilRouvertDepuisLeMenu_DiffèreLaSollicitationAuTickDeQuiétude`. C7.2 corrigé (ticket ME-03) : invariant I4 rétabli, tests `MachinesEtatsLot7Tests.Accueil_RefusPersisté_NEstPasDéfaitParUneValidationSansToucherLaCase`, `…Accueil_CaseNePlusAfficher_EstCochéeÀLOuvertureQuandLeRéglageEstRefusé` et `…Accueil_CaseReprise_ÉchapNeValideRien_MaisUnChoixDécochéEstEnregistré`.

### M7.3 — Accueil : essai de frappe intégré (états A, B, C de l'étape 1)

**Nature** : implicite. **Fichiers** : `src/OnboardingWindow.cs`, `src/TrayApplication.cs:1485-1495`, `src/LessonsWindow.Tutorial.cs` (seul le rappel de fin).

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_learningModuleAttempted` | bool | `src/OnboardingWindow.cs:164` | pompe | fil unique ; recalculé au `Show` (`:938-939`) |
| `_learningModuleDone` | bool | `:149` | pompe | fil unique ; monotone dans une ouverture (`:1490-1491`) |
| `_tutorialOpen` | bool | `:167` | pompe | fil unique ; **pas remis à faux par `Show`/`ResetState`** |
| `_activationRefused` | bool | `:191` | pompe | fil unique |
| `learningMaxStepCompleted`, `learningTutorialOpened` | config | `src/ConfigManager.cs:201`, `:276` | pompe, pool (écriture) | `_lock` |
| `UsageStats._excludedTypingDepth` | int | `src/UsageStats.cs:219-227` | pompe (Leçons) | `_lock` ; compteur apparié `BeginExcludedTyping`/`EndExcludedTyping` |

#### États

| état | prédicat |
|---|---|
| A — jamais essayé | `_currentStep == 0 && !_learningModuleAttempted && !_activationRefused && !_learningModuleDone` (`:731`) |
| B — essayé non terminé | `_currentStep == 0 && (_learningModuleAttempted \|\| _activationRefused) && !_learningModuleDone` (`:732`) |
| C — terminé | `_currentStep == 0 && _learningModuleDone` ; étapes 2 et 3 : comportement de C |
| Parcours ouvert | `_tutorialOpen` (accueil passe de topmost à notopmost, `:694-695`) |

#### Invariants

- I1 : exactement un de A, B, C à l'étape 1. Garanti par les prédicats de `:731-732` + C = complément.
- I2 : `_learningModuleDone` ⇒ `_learningModuleAttempted` à l'ouverture (`completed >= DoneThreshold` ⇒ `completed >= 1`). Garanti (`:938-940`) ; en cours de session `OnTutorialClosed(completed: true)` pose `_learningModuleDone` sans toucher `_learningModuleAttempted` (`:1490-1491`), mais `LaunchTutorial` l'a posé avant (`:1474`).
- I3 : `_tutorialOpen` vrai ⇔ un parcours est ouvert dans les Leçons. Garanti par la paire `LaunchTutorial` (`:1476`) / `OnTutorialClosed` (`:1489`), fermée par `EndTutorial` (`src/LessonsWindow.Tutorial.cs:200-216`) appelée depuis `LessonsWindow.cs:373`, `:3241`, `:3268`. **Non garanti** si `TutorialRequested` ne lance rien : `LaunchTutorial` quitte tôt si `TutorialRequested == null` (`:1473`) sans avoir posé `_tutorialOpen` (cohérent) ; mais `ShowTutorialFromOnboarding` qui échoue sur `EnsureLessonsWindow` rappelle `OnTutorialClosed(false)` (`src/TrayApplication.cs:1487-1491`), ce qui referme bien l'état. Aucune violation atteinte trouvée.
- I4 : la frappe du parcours est exclue des statistiques et ne peut pas armer le signal d'avis. Garanti par `src/UsageStats.cs:269` et la paire `BeginTutorial`/`EndTutorial` (`src/LessonsWindow.Tutorial.cs:90-94`, `:208-212`), gardée contre le double `Begin` par `_tutorialExcludesTyping`.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T3.1 | A ou B | « Essayer maintenant » / « Activer et essayer » | `RequestActivation()` vrai | B (parcours ouvert) | `_learningModuleAttempted = true`, `MarkLearningTutorialOpened`, `_tutorialOpen = true`, accueil notopmost, `TutorialRequested()` | `:1136-1141`, `:1471-1480` |
| T3.2 | A | « Essayer » refusé | `ActivationRequested()` faux | B | `_activationRefused = true` ; « Suivant » visible | `:196-203` |
| T3.3 | A ou B | flèche → / ↓ sans focus sur « Suivant » | `ForwardArrowAction` | A/B → lance le parcours ou focus « Essayer » | `LaunchTutorial()` ou `SetFocus(_hWndBtnTry)` | `:976-979`, `:1234-1235` |
| T3.4 | B | fin du parcours, exercices ≥ seuil ou écran final | `completed \|\| LearningMaxStepCompleted >= DoneThreshold` | C | `_tutorialOpen = false`, `_learningModuleDone = true`, `UpdateStepVisibility`, `BringToFront` | `:1487-1496` |
| T3.5 | B | fin du parcours sous le seuil | sinon | B | `_tutorialOpen = false` ; « Essayer » reste proposé | `:1489-1496` |
| T3.6 | A/B/C | `Show()` | — | recalculé depuis la config | `completed >= 1 \|\| TutorialOpened` ⇒ attempted ; `completed >= DoneThreshold` ⇒ done | `:938-941` |
| T3.7 | tout | fin du parcours alors que l'accueil est masqué | `!_visible` | inchangé (visuel) | `_tutorialOpen = false`, `_learningModuleDone` mis à jour, retour sans repeindre | `:1489-1492` |

#### Temporisations et réessais

Aucune minuterie. Réessai : « Essayer maintenant » reste proposé tant que `!_learningModuleDone` ; chaque clic relance `BeginTutorial` (route remise à zéro, usages relus, `_tutorialExcludesTyping` empêche un double `BeginExcludedTyping`). Une rafale de flèches →/↓ avec focus sur « Essayer » relance le parcours à chaque répétition tant que l'accueil garde le clavier ; le parcours prend le premier plan au premier appel, ce qui coupe la rafale (non retenu).

#### États ou transitions invalides atteignables

Aucun constat.

### M7.4 — Demande d'avis : état persistant et décision (essais 1 et 2)

**Nature** : implicite (compteur `reviewPromptCount`, date, drapeau `reviewPromptClicked`, plus un `enum ReviewPromptTrigger` explicite). **Fichiers** : `src/ReviewPromptGate.cs`, `src/TrayApplication.cs`, `src/ConfigManager.cs:455-560`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `reviewPromptCount` | uint 0..2 | `src/ConfigManager.cs:463`, écrit `:500-501` | pompe (écriture), pool (flush) | `_lock` ; `Flush()` immédiat hors verrou `:511` |
| `reviewPromptLastShown` | `yyyy-MM-dd` | `:466-475`, écrit `:503` | idem | idem |
| `reviewPromptClicked` | bool | `:481`, écrit `:515` | pompe | `_lock` ; écriture **différée** (400 ms, `SaveDelayMilliseconds` `:1334`) |
| `reviewPromptDone` | bool (compat v1.1) | `:505` | pompe | idem |
| `currentVersionFirstRun*` | 3 clés | `:542-560` | pompe (ctor) | `_lock` |
| `_versionFirstRun` | `VersionFirstRun?` | `src/TrayApplication.cs:151` | pompe | fil unique |
| `ReviewPromptSignals` | struct photographiée | `src/ReviewPromptGate.cs:40-57` | pompe | copie par valeur |

#### États

| état | prédicat (`ReviewState`) |
|---|---|
| S0 — aucun essai | `Attempts == 0 && !Answered` |
| S1 — essai 1 affiché | `Attempts == 1 && !Answered` |
| S2 — plafond | `Attempts == 2` (ou `Answered`) |
| Répondu | `Answered == true` (clic sur une sollicitation, « Noter » du menu, bouton des statistiques) |

#### Invariants

- I1 : `Attempts <= MaxAttempts` (2). Garanti par `Math.Min(ReviewPromptCount + 1, 2)` (`src/ConfigManager.cs:500`) et `AttemptsLeft` (`src/ReviewPromptGate.cs:96-97`).
- I2 : aucune relance après réponse. Garanti par `StillPossible` (`:111-112`) évalué avant toute autre garde (`:142`).
- I3 : l'essai 1 ne part jamais du démarrage. Garanti par `FirstAttemptAllowed` : `if (trigger != ReviewPromptTrigger.QuietTyping) return false;` (`:218`).
- I4 : jamais pendant une séance d'apprentissage ni dans les 10 minutes qui suivent. Garanti par `LearningAllowsPrompt` (`:270-278`, `LearningCooldownMinutes = 10` `:249`), lue depuis `LearningSessionTracker`.
- I5 : un essai est compté avant l'affichage et durable. Garanti par `RecordReviewPromptShown` puis `Flush()` (`src/ConfigManager.cs:496-512`) appelé avant `ShowBalloon`/toast (`src/TrayApplication.cs:2769`).
- I6 : « Répondu » est durable. **Garanti en partie** : écriture différée de 400 ms ; un arrêt brutal dans cet intervalle perd la réponse (le `Flush` de sortie existe mais pas sur crash). Sans scénario complet retenu.
- I7 : décision et enregistrement atomiques. Garanti par le fil unique (`ShouldShowNotification` puis `RecordReviewPromptShown` sur la même pompe, `:2763-2769`) ; second processus exclu par le mutex d'instance unique (`src/Program.cs:51-52`).
- I8 : le plancher d'espacement de l'essai 2 (7 jours) n'est pas violé par l'heure locale. Garanti : la date écrite est celle de la décision (`shownOn`, `src/ConfigManager.cs:496-504`).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T4.1 | S0 | `MaybeShowReviewPrompt(QuietTyping)` | `StillPossible`, apprentissage permis, pas d'erreur < 48 h, `FirstRemapDate != null`, `FirstAttemptAllowed`, pas de rappel Défi du jour, `EnrichedChars >= 20`, `ActiveMinutes >= 10` | S1 | `RecordReviewPromptShown(today)` (+ `Flush`) ; toast (packagé + activateur) ou balloon | `src/ReviewPromptGate.cs:138-176`, `src/TrayApplication.cs:2759-2785` |
| T4.2 | S0 | `MaybeShowReviewPrompt(Startup)` | gardes de T4.1 | S0 (refus) | aucun : `FirstAttemptAllowed` refuse | `:218` |
| T4.3 | S1 | `MaybeShowReviewPrompt(Startup ou QuietTyping)` | gardes communes, `ActiveDaysCount >= 10`, `Today − LastShown >= 7`, `Today − LastActiveDate <= 3` | S2 | idem T4.1 avec `attempt = 2` | `:164-174` |
| T4.4 | S0 ou S1 | clic sur la balloon / le toast d'avis | `_pendingBalloon == Review` ou `WM_APP_TOAST` 1/2 | Répondu | `SetReviewPromptClicked()` puis ouverture Store ou page feedback | `src/TrayApplication.cs:752-757`, `:777-782`, `:2799-2810` |
| T4.5 | S0 ou S1 | « Noter sur le Microsoft Store » (menu) | — | Répondu | `SetReviewPromptClicked()` ; `OpenStoreReview()` | `:2835-2840` |
| T4.6 | S0 ou S1 | « Noter l'application » (Statistiques) | — | Répondu | idem | `:2861-2866` |
| T4.7 | S0/S1 | échec d'écriture config | `Flush` rate | inchangé en mémoire, dirty | réessai 1 s, 5 s, 30 s, 5 min | `src/ConfigManager.cs:1343-1347`, `:1450-1456` |
| T4.8 | S0/S1/S2 | nouvelle version installée | `currentVersionFirstRunVersion != version` | inchangé | réécrit les 3 clés de premier lancement ; ne remet pas `reviewPromptCount` à zéro | `:546-558` |

#### Temporisations et réessais

Constantes : essai 2 après ≥ 7 jours (`SecondMinGapDays`), ≥ 10 jours d'usage, dernière frappe ≤ 3 jours (`StaleDays`), silence d'erreur 48 h (`ErrorCooldownHours`), 10 minutes après séance (`LearningCooldownMinutes`). Réessai : aucun automatique ; chaque refus provisoire est réévalué au tick suivant (M7.5) ou au démarrage suivant. Un essai « manqué » (toast supprimé par Windows, bulle jamais vue) est consommé : documenté (`src/TrayApplication.cs:2755-2756`), pas un défaut de la machine.

#### États ou transitions invalides atteignables

C7.3 corrigé (ticket ME-08) : l'essai 1 ne part plus pendant que l'accueil, rouvert depuis le menu, est à l'écran ; test `MachinesEtatsLot7Tests.Avis_AccueilRouvertDepuisLeMenu_DiffèreLaSollicitationAuTickDeQuiétude`.

### M7.5 — Demande d'avis : signal de frappe et minuterie de quiétude

**Nature** : implicite (drapeau `_enrichedThresholdCrossed`, minuterie `TIMER_REVIEW_QUIET` tuée ou non, report `_reviewPromptDeferred`). **Fichiers** : `src/TrayApplication.cs`, `src/ReviewPromptGate.cs`, `src/UsageStats.cs`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `UsageStats._enrichedThresholdCrossed` | bool | écrit `src/UsageStats.cs:306`, `:202`, `:211` | hook (pompe) écrit ; pompe lit ; pool lit pour flush | `lock (_lock)` |
| `UsageStats._lastRemapTickCount` | long (`TickCount64`) | `:410` | hook écrit, pompe lit | `lock (_lock)` ; 0 = aucune frappe dans ce processus (`:192`) |
| minuterie `TIMER_REVIEW_QUIET` | `SetTimer` du tray, 5 s | posée et tuée par `EnsureReviewTimer` (au `Run`, au tick, au changement du réglage des notifications) ; état `_reviewTimerArmed` | pompe | fil unique |
| `_reviewPromptDeferred` | bool | `:147` | pompe | fil unique |

#### États

| état | prédicat |
|---|---|
| Minuterie absente | jamais posée ou tuée par `EnsureReviewTimer` parce que `!ReviewPromptStillPossible()` ; reposée dès que le réglage des notifications change et qu'une sollicitation redevient possible (C7.4 corrigé) |
| Minuterie active, signal désarmé | minuterie posée ∧ `!_enrichedThresholdCrossed` |
| Armé | minuterie posée ∧ `_enrichedThresholdCrossed` |
| Armé et différé | armé ∧ `_reviewPromptDeferred` (reste vrai après un accueil fermé sans accord : M7.2 T2.11) |
| Armé et prêt | armé ∧ ¬différé ∧ `MillisecondsSinceLastRemap >= 15 000` |

#### Invariants

- I1 : le signal ne part que sur une frappe remappée de ce processus suivie de silence. Garanti par `ShouldTryAfterQuietTyping` : `if (millisecondsSinceLastRemap == long.MaxValue) return false;` (`src/ReviewPromptGate.cs:234`).
- I2 : un refus provisoire laisse le signal armé ; un envoi ou l'impossibilité le désarme. Garanti par `ShouldConsumeEnrichedSignal` (`src/TrayApplication.cs:2678-2679`).
- I3 : la minuterie tourne ⇔ une sollicitation reste possible. Garanti depuis ME-08 par `EnsureReviewTimer`, idempotente, appelée au `Run`, au tick et par `SettingsWindow.NotificationsChanged` (écrit après chaque clic sur la case des notifications). Le même effet par la politique d'entreprise ou les liens externes n'est relu qu'au prochain tick tant que la minuterie tourne, et au prochain lancement sinon.
- I4 : pas de sollicitation tant que l'accueil est ouvert. Garanti : accueil du démarrage par `_reviewPromptDeferred` (levé par `OnOnboardingClosed`), accueil rouvert depuis le menu par `_onboarding.IsVisible`, les deux lus dans la même garde du tick.
- I5 : le signal armé au chargement n'existe que pour l'essai 1. Garanti par `ShouldArmSignalAtLoad` (`src/ReviewPromptGate.cs:243-245`).
- I6 : aucune I/O disque dans le tick. Garanti : `ReviewPromptStillPossible` lit le cache de config ; `RecordReviewPromptShown` fait un `Flush` synchrone seulement au moment d'un essai réel (au plus deux fois).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T5.1 | — | `Run()` | `ReviewPromptStillPossible()` | Minuterie active | `SetTimer(9050, 5 s)` | `src/TrayApplication.cs:673-674` |
| T5.2 | — | `Run()` | `!ReviewPromptStillPossible()` (notifications coupées, liens externes éteints, plafond, réponse) | Minuterie absente | rien ; reposée par T5.11 si les notifications sont rallumées | `EnsureReviewTimer` |
| T5.3 | désarmé | frappe remappée | `enrichedBefore < 20 <= enrichedAfter` | Armé | `_enrichedThresholdCrossed = true` | `src/UsageStats.cs:301-307` |
| T5.4 | désarmé | `Run()` | `ShouldArmSignalAtLoad` (`count == 0`, possible, total ≥ 20) | Armé | `ArmEnrichedThresholdSignal()` | `src/TrayApplication.cs:687`, `:2729-2742` |
| T5.5 | actif | tick 5 s | `!ReviewPromptStillPossible()` (y compris une simple coupure des notifications couvrant un tick, puis rétablie) | Minuterie absente | `KillTimer` via `EnsureReviewTimer` ; reposée par T5.11 (C7.4 corrigé) | `MaybeShowReviewAfterQuietTyping` |
| T5.6 | armé | tick | `ShouldTryAfterQuietTyping` faux (différé, accueil visible, pas de frappe, silence < 15 s) | inchangé | aucun | `:2711-2713` |
| T5.7 | armé et prêt | tick | `MaybeShowReviewPrompt(QuietTyping)` vrai | désarmé | essai compté (M7.4) ; `ClearEnrichedThresholdSignal` | `:2715-2717` |
| T5.8 | armé et prêt | tick | décision fausse, encore possible | armé | aucun | `:2715-2716`, `:2678` |
| T5.9 | armé | décision fausse et `!StillPossible` | — | désarmé | `ClearEnrichedThresholdSignal` | `:2716-2717` |
| T5.10 | différé | fermeture de l'accueil avec accord | `_reviewPromptDeferred` | Armé et prêt éventuel | `MaybeShowReviewPrompt(Startup)` (refusera l'essai 1) | `:1425-1432` |
| T5.11 | Minuterie absente | clic sur la case des notifications des Paramètres | `ReviewPromptStillPossible()` redevenu vrai | Minuterie active | `SettingsWindow.NotificationsChanged` → `EnsureReviewTimer` → `SetTimer(9050, 5 s)` | `src/SettingsWindow.cs` (`ApplyNotifications`), `src/TrayApplication.cs` (`ConfigureSettingsWindow`) |

#### Temporisations et réessais

Tick 5 s (`REVIEW_QUIET_POLL_MS`), silence requis 15 s (`REVIEW_QUIET_SILENCE_MS`, `:551`). Réessai implicite : chaque tick réévalue un signal armé (coût : une photographie de signaux, verrous `UsageStats` brefs). Aucun compteur, rien à remettre à zéro. `TickCount64` est monotone (pas de sensibilité à l'horloge) et compte le temps de veille : après un réveil, un silence de plus de 15 s est correctement vu.

#### États ou transitions invalides atteignables

C7.3 corrigé (ticket ME-08) : invariant I4 rétabli, test `MachinesEtatsLot7Tests.Avis_AccueilRouvertDepuisLeMenu_DiffèreLaSollicitationAuTickDeQuiétude`. C7.4 corrigé (ticket ME-08) : invariant I3 rétabli, tests `MachinesEtatsLot7Tests.Avis_NotificationsRallumeesDansLesParametres_ReposentLaMinuterieDeQuietude`, `…Parametres_CaseNotifications_PrevientLeTrayApresEcriture` et `…Avis_TickPendantLaCoupure_ArreteLaMinuterie_PuisLeRallumageLaRepose`.

### M7.6 — Demande d'avis : notification affichée et cible du clic

**Nature** : implicite (`_pendingBalloon`, `_reviewTargetIsStore`, toast COM). **Fichiers** : `src/TrayApplication.cs`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `_pendingBalloon` | enum `{None, Review, Training, Announcement, AutoStart}` | `src/TrayApplication.cs:161-162` | pompe | fil unique |
| `_reviewTargetIsStore` | bool | `:166` | pompe | fil unique |
| `_toastActivatorRegistered` | bool | `:170` | pompe | fil unique |

#### États

`Aucune` (`_pendingBalloon == None`), `Balloon d'avis en attente` (`== Review`, cible dans `_reviewTargetIsStore`), `Toast d'avis affiché` (aucune variable : la cible est dans les arguments du toast `action=review&target=store|feedback`).

#### Invariants

- I1 : toute nouvelle balloon remplace la précédente et invalide la cible. Garanti : `ShowBalloon` et `ShowSecurityBalloon` posent `None` en tête (`:2880`, `:2935`).
- I2 : un clic ne s'applique qu'une fois. Garanti : `var kind = _pendingBalloon; _pendingBalloon = PendingBalloonKind.None;` (`:754-755`).
- I3 : une balloon expirée n'est plus cliquable. Garanti par `NIN_BALLOONTIMEOUT` (`:769-774`).
- I4 : le clic d'un toast arrive sur la pompe. Garanti par le re-routage `WM_APP_TOAST` (`:777`).
- I5 : un toast d'avis plus ancien cliqué après réponse ne produit qu'une ouverture sans nouvel effet d'état (`SetReviewPromptClicked` idempotent, `SetValueLocked` ignore une valeur identique `src/ConfigManager.cs:1364-1366`).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T6.1 | Aucune | décision d'avis positive, packagé et activateur COM | `ToastActivation.TryShowToast` vrai | Toast affiché | aucun état local | `src/TrayApplication.cs:2777-2780` |
| T6.2 | Aucune | décision positive, repli | sinon | Balloon d'avis | `ShowBalloon` ; `_pendingBalloon = Review` ; `_reviewTargetIsStore` | `:2782-2785` |
| T6.3 | Balloon d'avis | `NIN_BALLOONUSERCLICK` | — | Aucune | `OpenReviewTarget(_reviewTargetIsStore)` | `:752-757` |
| T6.4 | Balloon d'avis | `NIN_BALLOONTIMEOUT` | — | Aucune | — | `:769-774` |
| T6.5 | Balloon d'avis | autre balloon | — | Aucune | remplacée | `:2880` |
| T6.6 | Toast | clic | `wParam` 1 ou 2 | — | `OpenReviewTarget(store/feedback)` | `:777-782` |

#### Temporisations et réessais

Aucun réessai : un toast ou une balloon ignorés (concentration, désactivation du Centre de notifications) consomment l'essai (documenté). Aucun compteur de rappel propre.

#### États ou transitions invalides atteignables

Aucun constat.

### M7.7 — Boîte d'avis du Store (opération asynchrone)

**Nature** : implicite (opération WinRT en vol, sans variable). **Fichiers** : `src/StoreReview.cs`, `src/TrayApplication.cs:2819-2866`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| opération `IAsyncOperation<StoreRateAndReviewResult>` | locale à `TryShow` | `src/StoreReview.cs:58` | pompe démarre ; pool termine | aucune ; aucune variable partagée |
| `fallbackUrl` | string capturée | `:46`, `:60` | pool | immuable |

#### États

« Aucune opération connue » (rien ne mémorise l'opération) ; en vol ou terminée ne sont pas distinguables.

#### Invariants

- I1 : `TryShow` rend vrai ⇒ l'appelant n'ouvre pas le lien profond lui-même. Garanti (`src/TrayApplication.cs:2823`).
- I2 : le rappel de fin ne touche ni l'état ni les fenêtres. Garanti (`src/StoreReview.cs:81-106` : `ShellExecuteW` et `ConfigManager.Log` seulement).
- I3 : une annulation par l'utilisateur n'ouvre pas le lien. Garanti : l'annulation est rapportée par `StoreRateAndReviewStatus` avec `AsyncStatus.Completed` ; seul `NetworkError`/`Error` ouvre le repli (`:95-100`).
- I4 : au plus une boîte à la fois. **Non garanti** (C7.5).

#### Table de transition

| n° | état | événement | garde | cible | effets | code |
|---|---|---|---|---|---|---|
| T7.1 | — | `TryShow` | canal Store, `owner != 0`, `StoreContext` obtenu | opération lancée | `Initialize(context, owner)`, `Completed = OnCompleted` | `src/StoreReview.cs:48-61` |
| T7.2 | — | `TryShow` | hors Store, owner nul, `StoreContext` nul ou exception | repli | `false` ⇒ l'appelant ouvre le lien profond | `:48-69` |
| T7.3 | opération | fin `AsyncStatus != Completed` | — | repli | `ShellExecuteW(fallbackUrl)` sur le pool | `:88-92` |
| T7.4 | opération | fin `NetworkError`/`Error` | — | repli | journal + `ShellExecuteW` | `:95-100` |
| T7.5 | opération | fin `Succeeded`/`CanceledByUser` | — | fin | rien | `:94-95` |

#### Temporisations et réessais

Aucune. Pas de minuterie, pas de délai d'attente de l'opération ; si le Store ne répond jamais, rien ne se passe et rien n'est relancé (acceptable : l'utilisateur voit la boîte du Store ou rien).

#### États ou transitions invalides atteignables

C7.5.

### M7.8 — À propos et liens STATIC

**Nature** : implicite, deux petits états (`_visible`, `Hovered`). **Fichiers** : `src/AboutWindow.cs`, `src/LinkBehavior.cs`.

#### Variables d état

| variable | type | défini à | fils | protection |
|---|---|---|---|---|
| `AboutWindow._visible` | bool | `src/AboutWindow.cs:55` | pompe | fil unique |
| `AboutWindow.UiLanguage` | string | `:72` | pompe | fil unique ; `OnLanguageChanged` la relit (`:485-497`) |
| `AboutWindow._supportLink` | bool readonly | `:41-42` | pompe | readonly (lu à la création, politique non relue) |
| `TrayApplication._about` | `AboutWindow?` | `src/TrayApplication.cs:133` | pompe | fil unique |
| `LinkBehavior.Hovered` | IntPtr | `src/LinkBehavior.cs:53` | pompe | fil unique ; remis à zéro par `WM_MOUSELEAVE` et `Detach` |

#### États

À propos : Absent / Masqué / Visible. Lien : Repos / Survolé (`Hovered == hwnd`) / Focalisé (via `GetFocus`).

#### Invariants

- I1 : une seule instance d'À propos. Garanti par `_about ??=` (`src/TrayApplication.cs:918`) et le menu sur le fil de la pompe.
- I2 : une instance masquée et d'une autre langue est recréée ; visible, elle est rafraîchie. Garanti par `:913-917` et `AboutWindow.OnLanguageChanged`.
- I3 : le lien d'un lien Enter ouvre exactement une URL par appui. Garanti depuis 1.3.0 (17 novembre 2026) : `WM_KEYDOWN` ignore la répétition automatique (`LinkBehavior.IsAutoRepeat`, bit 30 de `lParam`) ; Entrée maintenue n'ouvre l'URL qu'une fois. Test : `Regles130PurTests.M7_8_I3_EntreeMaintenue_NOuvreQuUneUrlParAppui`.
- I4 : un double-clic sur un lien n'ouvre qu'une URL. Garanti par `if (code == 0)` (`src/AboutWindow.cs:308-317`, `src/OnboardingWindow.cs:1146-1155`) : `STN_DBLCLK` ignoré.

#### Table de transition

| n° | état | événement | garde | cible | effets | code |
|---|---|---|---|---|---|---|
| T8.1 | Absent | menu « À propos » | — | Visible | création ; `Show` | `src/TrayApplication.cs:909-919` |
| T8.2 | Masqué, langue changée | menu | `UiLanguage != L.Language` | Visible (recréée) | `Dispose` puis création | `:913-918` |
| T8.3 | Visible | « Fermer », Entrée, Échap, croix | — | Masqué | `ShowWindow(0)` | `src/AboutWindow.cs:319-325`, `:348-358` |
| T8.4 | Visible | changement de langue | — | Visible | textes réécrits | `:485-497` |
| T8.5 | Repos | `WM_MOUSEMOVE` | `Hovered != hwnd` | Survolé | repeint, `TrackMouseEvent(TME_LEAVE)` | `src/LinkBehavior.cs:101-114` |
| T8.6 | Survolé | `WM_MOUSELEAVE` | `Hovered == hwnd` | Repos | repeint | `:115-121` |
| T8.7 | tout | Entrée sur lien | — | inchangé | `WM_COMMAND` vers le propriétaire | `:129-135` |

#### Temporisations et réessais

Aucune.

#### États ou transitions invalides atteignables

Aucun constat.

### Frontières avec les autres lots

- **Leçons** (`LessonsWindow.Tutorial.cs`, `LearningSessionTracker`) : `EndTutorial` appelle `TutorialClosed` (rappel vers `OnboardingWindow.OnTutorialClosed`) ; `LearningSessionTracker.IsOpen`/`MillisecondsSinceLastClose` alimentent `LearningAllowsPrompt`. La garde « jamais pendant ni 10 min après » dépend de l'exactitude de ce suiveur (hors lot).
- **UsageStats** : `RecordEmittedText` (fil du hook, `lock (_lock)`) écrit `_enrichedThresholdCrossed` et `_lastRemapTickCount` ; l'exclusion de frappe du parcours (`BeginExcludedTyping`) est tenue par les Leçons.
- **ConfigManager** : persistance des clés d'avis et de l'accueil (écriture différée par `System.Threading.Timer` sur le pool, réessais 1 s / 5 s / 30 s / 5 min) ; `_lastErrorUtc` (`DateTime?`, écrit depuis tout fil qui journalise, lu sur la pompe par `Snapshot`) n'est pas protégé : lecture déchirée possible, au pire garde d'erreur manquée (hors lot, sans constat).
- **TrayApplication** : minuteries, menu, toast COM (`ToastActivation`, fil RPC → `WM_APP_TOAST`), relance par second processus (`RelaunchSignal`), `ApplyWindowInputState`, `ActivateWithConsent` (installation du hook ; boîte d'erreur imbriquée avec l'accueil resté actif).
- **KeyboardHook / TypingEngine.Windows** : le hook est posé et appelé sur le fil de la pompe ; `Install` relançable après `Dispose`.
- **AutoStart / AutoStartNudge** : `ApplyAutoStart` écrit l'état de démarrage Windows et la relance unique.
- **SettingsWindow** : écrit `notificationsEnabled` (`SettingsWindow.cs:1498`) et la case « afficher l'accueil » pendant la vie du processus ; depuis ME-08, le changement de la case des notifications repose la minuterie de quiétude (`NotificationsChanged`, C7.4 corrigé).
- **UsageStatsWindow** : bouton « Noter l'application » → `OnRateStoreFromStats`.
- **DialogNavigation / WindowScroll / NativeWindow** : acheminement Tab/Entrée/flèches (`IsDialogMessage`) et défilement ; les flèches des cases ne passent pas par le `WM_KEYDOWN` de l'accueil (pas de sous-classe, `src/OnboardingWindow.cs:577-578`).

### Constats du lot

- **C7.1** — Double clic sur « Suivant » à l'étape 2 : valide l'accueil sans voir l'étape 3 (moyenne, confiance moyenne).
- **C7.2** — « Ne plus afficher » toujours affichée décochée : « C'est parti ! » réécrit un refus déjà posé (moyenne, confiance haute).
- **C7.3** — La demande d'avis peut partir pendant que l'accueil, rouvert depuis le menu, est ouvert (faible, confiance moyenne) : corrigé (ME-08).
- **C7.4** — La minuterie de quiétude n'est jamais réarmée après coupure des notifications (faible, confiance haute) : corrigé (ME-08).
- **C7.5** — Aucun garde de double appel de la boîte d'avis du Store (faible, confiance basse).

Détail des scénarios, preuves et esquisses de test : voir la sortie structurée du lot (champ `constats`).

## Lot 8 — Paramètres, recherche de caractère, clavier virtuel et fenêtres natives

Lecture seule. Chemins relatifs au dépôt, sous `src/`. Les numéros de ligne sont ceux lus pendant l'analyse. Là où un fragment littéral n'a pas été relu après compaction, la ligne est donnée sans citation et la confiance est abaissée.

### Modèle de fils du lot

- Toutes les fenêtres du lot (Paramètres, recherche, clavier virtuel, couches maintenables) sont créées et pompées par le fil de la boucle unique de `TrayApplication.Run` (`TrayApplication.cs:717` : `while ((ret = Win32.GetMessageW(out var msg, IntPtr.Zero, 0, 0)) != 0)`), avec `DialogNavigation.TryRoute(ref msg)` avant `DispatchMessageW` (l. 722). Aucun verrou n'est donc nécessaire sur leurs champs : une course n'existe que par réentrance (boîte modale, `GetOpenFileNameW`, `SetForegroundWindow` qui envoie `WM_ACTIVATE`, `SendInput`).
- Les demandes issues du hook passent par `PostMessageW` vers la fenêtre du tray (`TrayApplication.cs:363-364`, `WM_APP_SEARCH`, `WM_APP_VKBD`) : la recherche et le clavier virtuel s'ouvrent sur le fil UI, jamais depuis le rappel du hook.
- `NotifyKeyPress`, `UpdateState`, `HighlightMethod` du clavier virtuel sont relayés par `TrayApplication` (l. 2220, 2229, 2293-2298 d'après la lecture précédente) depuis le rappel du hook, qui s'exécute sur le fil de la pompe : `KeyboardHook.cs:160-161` pose que l'installation et les callbacks s'exécutent sur le même thread (même modèle que le lot 7). Ces méthodes touchent `_pressedScancode` et appellent `InvalidateRect`/`SetTimer` sur le fil UI : aucun verrou nécessaire.
- Arrêt : `TrayApplication.Cleanup` est idempotent (`TrayApplication.cs:2333` : `if (_cleaned) return;`), décroche le hook avant les fenêtres (l. 2344) puis dispose chaque fenêtre et remet le champ à null (l. 2345-2355). Un double `Dispose` ou un appel après `Dispose` n'est donc pas atteignable depuis `TrayApplication`.
- Pause : `ApplyWindowInputState` ne gèle que la recherche et les leçons (`TrayApplication.cs:617-618`) ; `SettingsWindow.SetInputPaused` n'a aucun appelant en production (grep : seules définitions et tests). Les Paramètres restent utilisables en pause, par décision (commentaire l. 619-621).

### M8.1 — Cycle de vie et pause de la fenêtre Paramètres

**Nature** : implicite (booléens et handles), avec une énumération explicite pour l'onglet.
**Fichiers** : `SettingsWindow.cs`, `SettingsWindow.Paper.cs`, `SettingsWindow.CompatScroll.cs`, `SettingsScrollState.cs`.

#### Variables d état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `_hWnd` | IntPtr | création ; remis à zéro dans `Dispose` (l. 2528) | fil UI | fil unique |
| `_visible` | bool | `Show()` (l. 1400-1432) après `ShowWindow` ; faux dans `Close()` (l. 1452-1456) | fil UI | fil unique |
| `_inputPaused` | bool | `SetInputPaused` (l. 1434-1446) | fil UI (aucun appelant de production) | fil unique |
| `_focusedShortcut` | IntPtr | `WM_SETFOCUS`/`WM_KILLFOCUS` de `ShortcutSubclassProc` (l. 2396-2511) ; remis à zéro par `Show()` | fil UI | fil unique |
| `_captureConfirmed` | bool | `ApplyCapturedShortcut` (l. 2341-2394) | fil UI | fil unique |
| onglet courant | `SettingsTab` | `SetActiveTab` (l. 1098-1115) | fil UI | fil unique |
| `_scrollY`, `_contentHeight`, `_viewportHeight` | int | `SetActiveTab`, `FitWindowToContent` (l. 814-872) | fil UI | fil unique, bornes pures dans `SettingsScrollState` |
| `_hSurfaceBrush`, `_hBgBrush` | IntPtr | `RefreshPalette` (Paper) sur `WM_SYSCOLORCHANGE`/`WM_SETTINGCHANGE` ; supprimé dans `Dispose` (l. 2531) | fil UI | fil unique |
| `_compatDragging` | bool | `SettingsWindow.CompatScroll.cs` (SetCapture/ReleaseCapture ; faux sur `WM_CAPTURECHANGED`) | fil UI | fil unique |

#### États

| état | prédicat |
|---|---|
| Absente | `_hWnd == 0` (classe refusée ou après `Dispose`) |
| Masquée | `_hWnd != 0 && !_visible` |
| Visible, repos | `_visible && _focusedShortcut == 0` |
| Capture de raccourci | `_visible && _focusedShortcut != 0` (champ clavier virtuel ou recherche) |
| Capture confirmée | `_captureConfirmed` (retour visuel après une touche valide) |
| Figée (pause) | `_inputPaused` : `WM_COMMAND` et saisie jetés (l. 1516, 2292-2297) |

#### Invariants

- I1 : pas de capture de raccourci hors de la fenêtre visible. Garanti partiellement : `SetInputPaused` annule la capture (l. 1434-1446) ; `Show()` remet `_focusedShortcut = IntPtr.Zero` sans `CancelShortcutCapture` (l. 1400-1432) : non garanti pour l'état visuel, mais `WM_KILLFOCUS` le remet à zéro à la perte de focus.
- I2 : deux raccourcis distincts. Garanti par `ApplyCapturedShortcut` (collision `vk == otherVk`, refus, l. 2341-2394).
- I3 : aucune écriture de configuration pendant la pause pour une case affichée comme cochée. Garanti par construction : la pause du `SettingsWindow` n'est jamais armée en production (aucun appelant).
- I4 : `Dispose` unique. Garanti par `TrayApplication.Cleanup` (l. 2333) et le champ remis à null (l. 2348).
- I5 : un handle GDI n'est supprimé qu'une fois. Garanti depuis 1.3.0 (17 novembre 2026) : `Dispose` supprime `_hSurfaceBrush` s'il est non nul puis remet le champ à zéro ; un second `Dispose` ne le supprime pas de nouveau (cas déjà non atteignable par I4). Test : `Regles130AccueilReglagesTests.M8_1_I5_DoubleDispose_LePinceauNEstSupprimeQuUneFois`.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1 | Absente/Masquée | `Show()` | `_hWnd != 0` | Visible, repos | `_focusedShortcut = 0`, `ShowWindow`, `_visible = true` | `SettingsWindow.cs:1400-1432` |
| T2 | Visible | `Close()` ou croix | aucune | Masquée | `ShowWindow(0)`, `_visible = false` | `SettingsWindow.cs:1452-1456` |
| T3 | Visible, repos | focus sur un champ raccourci | `WM_SETFOCUS` | Capture | `_focusedShortcut = hWnd` | `SettingsWindow.cs:2396-2511` |
| T4 | Capture | touche valide | pas de collision | Capture confirmée | écrit `ConfigManager.Shortcut…Vk`, `ShortcutChanged` | `SettingsWindow.cs:2341-2394` |
| T5 | Capture | Échap | focus dans le champ | Visible, repos | annule la capture, ou quitte, ou ferme | `SettingsWindow.cs:2396-2511` |
| T6 | Capture | `WM_KILLFOCUS` | aucune | Visible, repos | `_focusedShortcut = 0` | `SettingsWindow.cs:2396-2511` |
| T7 | Visible | changement d'onglet | aucune | Visible | efface le message, `_scrollY = 0`, remesure | `SettingsWindow.cs:1098-1115` |
| T8 | Visible | clic « Réinitialiser les raccourcis » | aucune | Visible | `ShowMessageBox` (pompe imbriquée), `SetFocus`, puis écriture config | `SettingsWindow.cs:1685-1704` |
| T9 | Visible | `SetInputPaused(true)` | jamais appelé en production | Figée | annule la capture | `SettingsWindow.cs:1434-1446` |
| T10 | tout | `Dispose` | `_hWnd != 0` pour la destruction | Absente | retire les sous-classes, `Unregister`, `DestroyWindow`, `DeleteObject` | `SettingsWindow.cs:2513-2540` |

#### Temporisations et réessais

Aucune minuterie propre aux Paramètres dans le périmètre lu. Aucun réessai. Écarté : cases cochées sans écriture pendant la pause (le `SetInputPaused` des Paramètres n'est jamais appelé, `TrayApplication.cs:617-618` ne vise que la recherche et les leçons) ; double `Dispose` (voir I4).

#### États ou transitions invalides atteignables

Aucun constat.

### M8.2 — Fenêtre de recherche de caractère et insertion

**Nature** : implicite, plus l'énumération explicite `TextInsertionOutcome`.
**Fichiers** : `CharacterSearch.cs`, `TextInsertionService.cs`, `ClipboardText.cs`.

#### Variables d état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| visibilité | `IsVisible` = `_hWnd != 0 && IsWindowVisible` | `CharacterSearch.cs:135` | fil UI | fil unique |
| `_inputPaused` | bool | `SetInputPaused` (l. 137-141) | fil UI | fil unique |
| `_targetWindow` | IntPtr | `Show` via `ResolveInsertionTarget` (l. 832-833, 894-900) | fil UI | fil unique |
| `_showCopiedFeedback`, `_copiedChar` | bool, string | `InsertSelectedCharacter` (l. 1202-1203) ; faux sur `WM_TIMER` (l. 979) | fil UI | fil unique |
| `_showCopyFailedFeedback` | bool | `InsertSelectedCharacter` (échec de la copie, ME-11) ; faux sur `WM_TIMER` et après une copie réussie | fil UI | fil unique |
| `_enterFoundNothing` | bool | `InsertSelectedCharacter` (l. 1181) ; faux dans `Show` (l. 838) | fil UI | fil unique |
| (retours de copie) | `_showCopiedFeedback`, `_showCopyFailedFeedback`, minuterie 1500 ms | faux, minuterie tuée, dans `Show` (ME-07) | fil UI | fil unique |
| `_selectedIndex`, `_scrollOffset`, `_filteredResults` | int, int, liste | `Search`, molette, clic | fil UI | fil unique |

#### États

| état | prédicat |
|---|---|
| Masquée | `!IsVisible` |
| Ouverte | `IsVisible && !_inputPaused` |
| Gelée | `IsVisible && _inputPaused` : `InsertSelectedCharacter` revient aussitôt (l. 1176) |
| Retour « Copié » | `_showCopiedFeedback`, minuterie 1500 ms armée |

Résultat d'insertion (explicite) : `Inserted`, `NotInserted`, `Partial`, `Blocked` (`TextInsertionService.cs:5`).

#### Invariants

- I1 : la fenêtre masquée ne laisse pas de surbrillance au clavier virtuel. Garanti par `Hide()` : `SelectionChanged?.Invoke(null)` (l. 915).
- I2 : une insertion ne s'émet ni sous blocage ni sur champ protégé. Garanti par `_canInsert()` testé avant et après `_refreshTarget()` (`TextInsertionService.cs:29, 37`).
- I3 : la copie de secours ne part que si aucun événement n'a été émis. Garanti : `if (outcome != TextInsertionOutcome.NotInserted) return;` (`CharacterSearch.cs:1196-1197`).
- I4 : un échec d'insertion suivi d'un échec de copie est annoncé à l'utilisateur. Garanti depuis ME-11 : `ClipboardWriter` (remplaçable, `ClipboardText.TrySet` par défaut) rend false, alors `FallbackCopyFailed` (ballon), annonce du champ et ligne de pied « Copie impossible » pendant 1500 ms ; `TrySet` ouvre le presse-papiers jusqu'à 4 fois, 25 ms entre les essais (`ClipboardText.OpenWithRetry`). `Blocked` retourne toujours sans rien dire : pause ou champ protégé, voulu.
- I5 : l'état « Copié » (et « Copie impossible ») est propre à une ouverture. Garanti depuis ME-07 : `Show()` remet les deux drapeaux à faux et tue la minuterie de 1500 ms encore en vol ; `Hide()` n'y touche pas (un retour armé fenêtre masquée, T7, est effacé à la réouverture).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1 | Masquée | `Toggle`/`Show` | cible résolue | Ouverte | cible mémorisée, statistiques, redimensionnement, focus forcé (AttachThreadInput) | `CharacterSearch.cs:825-864` |
| T2 | Ouverte | `Toggle`/`Hide` | visible | Masquée | `ShowWindow(0)`, surbrillance effacée | `CharacterSearch.cs:819-820, 912-916` |
| T3 | Ouverte | `WM_ACTIVATE` inactif | état 0 | Masquée | `Hide()` | `CharacterSearch.cs:945-949` |
| T4 | Ouverte | `WM_CLOSE` | aucune | Masquée | `Hide()` | `CharacterSearch.cs:955-957` |
| T5 | Ouverte | Entrée, résultat sélectionné | `!_inputPaused` | Ouverte, ou Masquée dès que la cible est non nulle et activée (`ActivateTargetWindow` `TextInsertionService.cs:44-60` : le focus passe à la cible, la recherche reçoit `WA_INACTIVE`, T3, avant le retour de `Insert`) | `Insert(target, caractère)` | `CharacterSearch.cs:1174-1192` |
| T6 | Ouverte | Entrée, aucun résultat | index hors liste | Ouverte | `_enterFoundNothing`, annonce | `CharacterSearch.cs:1177-1184` |
| T7 | Ouverte ou Masquée (Masquée quand la cible a été activée puis que `_emitText` n'a envoyé aucun événement : T3 a déjà masqué la fenêtre) | insertion `NotInserted` | copie réussie | Retour « Copié » (Ouverte ou Masquée : `Hide()` ne remet pas le drapeau à zéro ; `Show()` le fait, ME-07) | `_showCopiedFeedback`, ballon, minuterie 1500 ms ; posés sur une fenêtre masquée dans le cas Masquée (C8.2) | `CharacterSearch.cs:1196-1207`, `TextInsertionService.cs:30-41` |
| T8 | Ouverte ou Masquée (comme T7) | insertion `NotInserted` | copie en échec (après 4 essais d'ouverture) | inchangé | `_showCopyFailedFeedback`, `FallbackCopyFailed` (ballon), annonce, minuterie 1500 ms (ME-11) | `CharacterSearch.cs:1199` |
| T9 | Retour « Copié » (Ouverte ou Masquée) | `WM_TIMER` | id copie | Ouverte ou Masquée (l'état de visibilité ne change pas) | `KillTimer`, feedback faux, repeinte, `AnnounceSelection(notify: false)` même fenêtre masquée | `CharacterSearch.cs:975-983` |
| T10 | Ouverte | `SetInputPaused(true)` | pause hors suspension choisie | Gelée | saisie jetée | `TrayApplication.cs:617`, `CharacterSearch.cs:137` |

#### Temporisations et réessais

- Copie : `SetTimer(..., 1500)` (l. 1205), tuée à l'échéance (l. 978). Réessai borné de `OpenClipboard` depuis ME-11 : `ClipboardText.TrySet` tente jusqu'à 4 fois, 25 ms entre les essais (75 ms au plus sur le fil d'interface).
- Insertion : une seule tentative d'activation de la cible, puis une lecture synchrone (`_refreshTarget`).

#### États ou transitions invalides atteignables

C8.1 corrigé (ticket ME-11) : invariant I4 rétabli, test `MachinesEtatsLot8Tests.Recherche_EchecDeLInsertionEtDeLaCopie_EstAnnoncé`. C8.2 corrigé (ticket ME-07) : invariant I5 rétabli, test `MachinesEtatsLot8Tests.Recherche_RetourCopié_NeSurvitPasÀLaRéouverture`.

### M8.3 — Clavier virtuel

**Nature** : implicite.
**Fichiers** : `VirtualKeyboard.cs`.

#### Variables d état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `_hWnd` | IntPtr | constructeur ; zéro après `Dispose` (l. 1347) | fil UI | fil unique |
| `_visible` | bool | `Show` (l. 468-481), `Hide` (l. 628-633) | fil UI | fil unique |
| `_pressedScancode` | uint | `NotifyKeyPress` (l. 750-757) ; effacé sur `WM_TIMER` (l. 839-847) | fil UI | fil unique |
| `_ctrlDown`, `_altDown`, `_altGrDown` | bool | `UpdateState` (l. 636-651) | fil UI | fil unique |

#### États

| état | prédicat |
|---|---|
| Absent | `_hWnd == 0` (classe refusée, retour anticipé l. 322-323, ou après `Dispose`) |
| Masqué | `_hWnd != 0 && !_visible` |
| Visible | `_visible` |
| Touche mise en évidence | `_visible && _pressedScancode != 0`, minuterie `TIMER_KEYPRESS` 120 ms |

#### Invariants

- I1 : pas de surbrillance ni de minuterie quand masqué. Garanti : `if (!_visible) return;` (l. 752), `HighlightMethod` ignoré si `!_visible`.
- I2 : après `Dispose`, plus aucun appel ne vise la fenêtre. Garanti par l'ordre `Cleanup` : le hook est décroché avant (`TrayApplication.cs:2344-2345`) et le champ remis à null.
- I3 : bornes enregistrées seulement visible. Garanti : `WM_SIZE` → `SaveWindowBounds` si `_visible`.

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1 | Masqué | `Show`/`Toggle` | `_hWnd != 0` | Visible | positionnement, `_visible = true` | `VirtualKeyboard.cs:468-481` |
| T2 | Visible | `Hide`/`Toggle` | aucune | Masqué | `ShowWindow(0)`, `_visible = false` | `VirtualKeyboard.cs:628-633` |
| T3 | Visible | `NotifyKeyPress` | `_visible` | Touche mise en évidence | scancode, repeinte, `SetTimer` 120 ms | `VirtualKeyboard.cs:750-757` |
| T4 | Touche mise en évidence | `WM_TIMER` | id `TIMER_KEYPRESS` | Visible | efface le scancode, repeint | `VirtualKeyboard.cs:839-847` |
| T5 | Visible | `UpdateState` | aucune | Visible | modifs mis à jour, repeinte | `VirtualKeyboard.cs:636-651` |
| T6 | tout | `Dispose` | aucune | Absent | désabonne les évènements, `DestroyWindow`, `UnregisterClass` | `VirtualKeyboard.cs:1335-1351` |

#### Temporisations et réessais

`TIMER_KEYPRESS` 120 ms, réarmée à chaque frappe (même identifiant : redémarre). `Dispose` ne remet pas `_visible = false`, sans conséquence atteignable (I2). Écarté : `InvalidateRect`/`SetTimer` sur handle nul après `Dispose` : le hook est décroché et le champ est null avant (l. 2344-2345).

#### États ou transitions invalides atteignables

Aucun constat.

### M8.4 — Fenêtre des couches maintenables

**Nature** : implicite, plus l'énumération `Role`.
**Fichiers** : `MaintainableLayersWindow.cs`, `DialogNavigation.cs`.

#### Variables d état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `_visible` | bool | `Show` (l. 121), `Hide` (l. 129) | fil UI | fil unique |
| `_delayBusy` | bool | `LoadFromConfig` (l. 340-342), `ReadDelay` (l. 393-395) | fil UI | `try/finally` |
| cases (`_hMaster`, `_hGreek`, …) | handles | `LoadFromConfig` (l. 335-339) | fil UI | fil unique |
| registre `DialogNavigation` | statique sans verrou | `Register`/`Unregister` | fil UI | fil unique |

#### États

| état | prédicat |
|---|---|
| Absente | `_hWnd == 0` |
| Masquée | `!_visible`, désinscrite de la navigation |
| Visible | `_visible`, inscrite (l. 113-114) |

#### Invariants

- I1 : les cases reflètent la configuration. Garanti : `RefreshFromConfig` appelé par `ToggleMaintainableLayer` (`TrayApplication.cs:2274`) et `Show` (l. 104).
- I2 : le délai reste dans 150..1000. Garanti : `ClampDelay` (l. 378-386).
- I3 : inscrite dans `DialogNavigation` seulement visible. Garanti : `Register` en `Show`, `Unregister` en `Hide` et `Dispose` (l. 127, 575).
- I4 : un clic écrit tout de suite (W1). Garanti : `OnCheckboxClicked` (l. 404-428).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1 | Masquée | `Show` | aucune | Visible | relit la config, défilement 0, `Register`, focus | `MaintainableLayersWindow.cs:102-122` |
| T2 | Visible | `Hide`/`WM_CLOSE` | aucune | Masquée | `SaveToConfig`, `Unregister`, `ShowWindow(0)` | `MaintainableLayersWindow.cs:124-130, 551-553` |
| T3 | Visible | clic sur une case | id connu | Visible | écrit la case, `SettingsChanged` | `MaintainableLayersWindow.cs:404-428` |
| T4 | Visible | changement du délai | `!_delayBusy` | Visible | écrit si valeur valable | `MaintainableLayersWindow.cs:433` |
| T5 | tout | `Dispose` | aucune | Absente | `Unregister` avant `DestroyWindow`, supprime polices | `MaintainableLayersWindow.cs:569-585` |

#### Temporisations et réessais

Aucune. Écarté : `Hide()` qui réécrit un état périmé : le menu du tray relit toujours la fenêtre (`TrayApplication.cs:2274`).

#### États ou transitions invalides atteignables

Aucun constat.

### M8.5 — Fenêtres natives, navigation de dialogue, défilement et dimensionnement

**Nature** : implicite (registre statique), utilitaires purs.
**Fichiers** : `NativeWindow.cs`, `DialogNavigation.cs`, `WindowScroll.cs`, `WindowSizing.cs`, `SettingsScrollState.cs`.

#### Variables d état

| variable | type | défini à | fils lecteurs/écrivains | protection |
|---|---|---|---|---|
| `_dialogs`, `_focusHandlers`, `_keyFilters` | tables statiques | `DialogNavigation.Register`/`Unregister` | fil UI | fil unique, sans verrou |
| `Position`, `ContentHeight`, `ViewHeight` | int | `WindowScroll.Reset`/`ScrollTo` (`WindowScroll.cs:44-66`) | fil UI | bornes par `Clamp` (l. 53) |
| reste de molette | int | `SettingsScrollState.ConsumeWheel` | fil UI | fil unique |
| cache d'icône | handle | `NativeWindow.LogoIcon` | tous | `lock` |

#### États

Fenêtre inscrite ou non ; défilement nécessaire (`ContentHeight > ViewHeight`, `WindowScroll.cs:38`) ou non.

#### Invariants

- I1 : le défilement reste dans `[0, contenu − visible]`. Garanti par `Clamp` (`WindowScroll.cs:53`) et `ScrollTo` (l. 58).
- I2 : une fenêtre est désinscrite avant destruction. Garanti dans les trois `Dispose` lus.
- I3 : pas de défilement quand le contenu tient. Garanti : `if (!Needed) return false;` (l. 123, 156, 170).

#### Table de transition

| n° | état source | événement | garde | état cible | effets | code |
|---|---|---|---|---|---|---|
| T1 | non défilé | `Reset` | aucune | Position 0 | barre masquée si le contenu tient | `WindowScroll.cs:44-50` |
| T2 | Position p | VSCROLL, molette, touche, Tab | `Needed` | Position bornée | décale les enfants, barre, `Scrolled` | `WindowScroll.cs:56-66, 121-161, 168-177` |
| T3 | non inscrite | `Register` | aucune | Inscrite | filtres de focus et de touche | `DialogNavigation.cs` |
| T4 | Inscrite | `Unregister` | aucune | Non inscrite | retire les trois entrées | `DialogNavigation.cs` |

#### Temporisations et réessais

Aucune.

#### États ou transitions invalides atteignables

Aucun constat.

### Frontières avec les autres lots

- Hook clavier et `KeyMapper` (`TypingEngine.Windows`) : fil du rappel, `TryEmitText`, `SendInput` réentrant, `ShouldProcessHook`.
- `TrayApplication` : création des fenêtres, `Cleanup`, `ApplyWindowInputState`, `RefreshFromConfig`, relais `UpdateState`/`NotifyKeyPress`/`HighlightMethod`.
- `ConfigManager` : persistance, `AppLanguageChanged`, `WindowBoundsCleared`.
- `LessonsWindow` : `SetInputPaused`, `LessonDisplayChanged`.
- `ForegroundMonitor` : `CompatibilityOverridesChanged`, `Recompute`.
- `AutoStart`, `AutoStartNudge`, `UsageStats`, `MaintainableLayerManager`.

### Constats retenus

#### C8.1 — Copie de secours sans retour quand le presse-papiers est occupé

- Scénario : (1) la recherche est ouverte, la cible est la barre des tâches ou une fenêtre détruite (`ResolveInsertionTarget` rend `IntPtr.Zero`, `CharacterSearch.cs:898-899`) ; (2) un gestionnaire de presse-papiers ou une session distante garde le presse-papiers ouvert ; (3) Entrée sur un résultat : `Insert` rend `NotInserted` ; (4) `TrySet` échoue à son unique `OpenClipboard` ; (5) la fonction revient sans copie, sans ballon, sans annonce, sans réessai.
- Invariant violé : un échec d'insertion suivi d'un échec de copie est annoncé.
- Gravité faible à moyenne, confiance moyenne : le retour de `TrySet` n'a pas été relu après compaction.

#### C8.2 — Retour « Copié » périmé à la réouverture

- Scénario : (1) copie de secours réussie, `_showCopiedFeedback = true`, minuterie 1500 ms ; (2) Échap puis raccourci avant 1500 ms ; (3) `Show` ne remet que `_enterFoundNothing` à faux (l. 838) ; (4) la fenêtre rouverte affiche « Copié ! » pour le caractère précédent jusqu'à l'échéance.
- Chemin plus fréquent, sans Échap : avec une cible non nulle, `ActivateTargetWindow` rend le focus à la cible, la recherche reçoit `WA_INACTIVE` et se masque (`CharacterSearch.cs:945-949`) avant le retour de `Insert`. Si aucun événement n'est émis (`NotInserted`), la copie de secours et le drapeau « Copié » sont posés fenêtre masquée (`:1202-1205`) ; une réouverture dans les 1500 ms montre alors le « Copié ! » périmé. La minuterie (`:975-982`) tire aussi fenêtre masquée.
- Invariant violé : l'état « Copié » est propre à une ouverture.
- Gravité faible, confiance moyenne.
