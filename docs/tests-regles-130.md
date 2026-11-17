# Tests des 92 règles sans test (audit frappe au hasard, app 1.3.0)

Base c16866b, branche claude/ag-tests-regles-130. Règle, test, verdict. Généré le 2026-11-17 à partir des rapports de lot.

Lignes : 92 (ids distincts 92). Vertes 85, rouges 4, autres 3.

Rouges = règle que le produit ne tient pas (constat) ; chacun échoue sur sa dernière assertion, témoins avant. Les 3 tests `EntreeReelle` (fichier Regles130EntreeReelleTests.cs) sont écrits et compilés mais jamais joués : ils envoient de vraies frappes (SendInput) sur le bureau ; ils tournent par l'étape tests-entree-reelle de scripts/verify.ps1.

| Règle | Test | Verdict | Fichier |
|---|---|---|---|
| M1.2-I8 | M1_2_I8_RelacheDuDeclencheur_VueHorsSuspension_PuisEffaceeApresSuspension | vert | Regles130MoteurTests.cs |
| M1.5-I4 | M1_5_I4_UnSeulAppel_LitLaSuspensionDeManiereCoherente | vert (corrigé 1.3.0) | Regles130MoteurTests.cs |
| M1.6-I5 | M1_6_I5_NombreDEssaisDeReparationBorne_SousRefusPersistant | rouge | Regles130MoteurTests.cs |
| M1.6-I6 | M1_6_I6_DeuxReparationsNeSeSuperposentPas | vert | Regles130MoteurTests.cs |
| M1.7-I1 | M1_7_I1_FileFifo_RendLesAttendusDansLOrdreDesFrappes | vert | Regles130PurTests.cs |
| M1.7-I2 | M1_7_I2_CaractereDeControle_JamaisRemplaceParDuTexte | vert | Regles130PurTests.cs |
| M1.7-I3 | M1_7_I3_Raccourci_AucunTexteAttendu | vert | Regles130PurTests.cs |
| M1.8-I3 | M1_8_I3_Indicateur_NeDetruitJamaisLePinceauDeLaClasse | vert | Regles130PurTests.cs |
| M2.1-I1 | M2_1_I1_Reinstall_UnSeulHookALEtatStable_UneFrappeUnSeulRawKeyDown + M2_1_I1_InstallDeuxFois_DeuxHooksPoses_RougeAttendu | non exécuté (compilé) | Regles130EntreeReelleTests.cs |
| M2.1-I2 | M2_1_I2_M3_8_I2_RafaleDeTouchesRemappees_PendantDesReinstallRepetes_NParesExactement | non exécuté (compilé) | Regles130EntreeReelleTests.cs |
| M2.1-I4 | M2_1_I4_ApresDispose_LeRappelTourneSansEffet | vert | Regles130MoteurTests.cs |
| M2.3-I3 | M2_3_I3_PassThroughTotal_AucuneNotificationRaccourciNiEmission | vert | Regles130MoteurTests.cs |
| M2.3-I4 | M2_3_I4_PassThroughAvecRaccourcis_PasDeRawKeyDown_RaccourcisDetectes | vert | Regles130MoteurTests.cs |
| M2.4-I1 | M2_4_I1_GetEmitContext_LitModeEtHklDUnSeulSnapshot | vert | Regles130MoteurTests.cs |
| M2.4-I3 | M2_4_I3_AntiCheatSurclasseForceOn_AccesDistantSurclasseLesDeux | vert | Regles130MoteurTests.cs |
| M2.4-I5 | M2_4_I5_ForegroundChanged_LeveParChaqueRecomputeNomme_JamaisParLeHook | vert | Regles130MoteurTests.cs |
| M2.4-I6 | M2_4_I6_SuiviIndisponible_N_EstJamaisRetabli_EtatAbsorbant | vert | Regles130MoteurTests.cs |
| M2.4-I7 | M2_4_I7_ExceptionDansRecompute_SnapshotDeRepliSecurise_PuisRepare | vert | Regles130MoteurTests.cs |
| M2.5-I1 | M2_5_I1_RequeteSansResultatFrais_RendSecurise | vert | Regles130MoteurTests.cs |
| M2.5-I3 | M2_5_I3_ResultatPerime_N_EstJamaisRenduAUneRequetePlusRecente | vert | Regles130MoteurTests.cs |
| M2.5-I4 | M2_5_I4_ResultChangedLate_SeulementSiTardifEtDifferentDuPrecedent | vert | Regles130MoteurTests.cs |
| M2.5-I5 | M2_5_I5_SignalDoneResiduel_NeConclutPasSurUnResultatPerime | vert | Regles130MoteurTests.cs |
| M3.1-I1 | M3_1_I1_PauseImpliqueActif_SurToutesLesSequencesDeCommandes | vert | Regles130TrayTests.cs |
| M3.1-I3 | M3_1_I3_ApplyHookState_SeulEcrivainDeLEtatDuHook | vert | Regles130TrayTests.cs |
| M3.1-I4 | M3_1_I4_CommandeDuMenu_AppliqueeALEtatAffiche | rouge | Regles130TrayTests.cs |
| M3.1-I5 | M3_1_I5_RaccourciGlobal_NeFaitPasDeTravailLourdDansLeRappelDuHook | rouge | Regles130TrayTests.cs |
| M3.2-I1 | M3_2_I1_RepriseAnnonceeSeulementSiUneSuspensionLaRenduDue | vert | Regles130TrayTests.cs |
| M3.3-I1 | M3_3_I1_BullesEtNudges_RespectentNotificationsEnabled | vert | Regles130TrayTests.cs |
| M3.3-I2 | M3_3_I2_SollicitationAUsageUnique_MarqueeSeulementSiMontree | rouge | Regles130TrayTests.cs |
| M3.4-I3 | M3_4_I3_QuitterPendantLeDialogue_NeBloquePas_WmQuitRepose | vert | Regles130TrayTests.cs |
| M3.4-I4 | M3_4_I4_AbonnementAppLanguageChanged_RetireALaFermeture | vert | Regles130TrayTests.cs |
| M3.6-I2 | M3_6_I2_FenetreDeBascule_SeFerme2sApresLaDerniereBascule | vert | Regles130TrayTests.cs |
| M3.8-I1 | M3_8_I1_Cleanup_EstIdempotent | vert | Regles130TrayTests.cs |
| M3.8-I2 | M2_1_I2_M3_8_I2_RafaleDeTouchesRemappees_PendantDesReinstallRepetes_NParesExactement | non exécuté (compilé) | Regles130EntreeReelleTests.cs |
| M4.1-I3 | M4_1_I3_SeulLeGagnantEcrit_RotationEtRelanceApresLesSorties | vert | Regles130PurTests.cs |
| M4.1-I5 | M4_1_I5_SecondeInstance_MuetteEnActivationSysteme | vert | Regles130PurTests.cs |
| M4.2-I3 | M4_2_I3_DeuxFlushConcurrents_EcrituresDansLOrdreDesInstantanes | vert | Regles130PurTests.cs |
| M4.2-I7 | M4_2_I7_EcritureAtomique_AncienFichierIntactEtTemporaireCompletAvantRemplacement | vert | Regles130PurTests.cs |
| M4.5-I3 | M4_5_I3_PanneDeRegistre_ValeAucunePolitique | vert | Regles130PurTests.cs |
| M4.5-I4 | M4_5_I4_Canal_ImmuableHorsCrochetDeTest | vert | Regles130PurTests.cs |
| M4.6-I1 | M4_6_I1_Register_Idempotent | vert | Regles130PurTests.cs |
| M4.6-I3 | M4_6_I3_AbonneDuToast_NeFaitQuePosterUnMessage | vert | Regles130PurTests.cs |
| M4.6-I4 | M4_6_I4_ActivationTardive_SansEffet | vert | Regles130PurTests.cs |
| M5.1-I1 | M5_1_I1_DeuxFlushConcurrents_UnSeulEcrivainADeLaFois | vert | Regles130PurTests.cs |
| M5.1-I4 | M5_1_I4_Preload_ChargeAvantLeHook_LaFrappeNeRelitPasLeFichier | vert | Regles130PurTests.cs |
| M5.4-I1 | M5_4_I1_FermeturesEnTrop_LaProfondeurNeDevientJamaisNegative | vert | Regles130PurTests.cs |
| M5.4-I3 | M5_4_I3_Exclusion_SeulBeginTutorialOuvreLaPlage | vert | Regles130PurTests.cs |
| M5.6-I2 | M5_6_I2_MinuterieDeCopie_DetruiteALaFermeture | vert (corrigé 1.3.0) | Regles130PurTests.cs |
| M5.7-I2 | M5_7_I2_RechargementConditionnel_NeRemplacePasUnEtatEchoue | vert | Regles130PurTests.cs |
| M5.7-I3 | M5_7_I3_JsonValideDeFormeInattendue_ChargementSansException | vert | Regles130PurTests.cs |
| M5.7-I4 | M5_7_I4_ResetAll_RemetToutLEtatDeProgression | vert | Regles130PurTests.cs |
| M5.8-I1 | M5_8_I1_MigrationDEmpreinte_LEntreeActuellePrimeSurLesAnciennes | vert | Regles130PurTests.cs |
| M5.9-I2 | M5_9_I2_GesteDuSelonLIntervalleDeSaBoite | vert | Regles130PurTests.cs |
| M5.9-I3 | M5_9_I3_HorlogeQuiRecule_NeRendPasUnGesteDuEnAvance | vert | Regles130PurTests.cs |
| M5.12-I3 | M5_12_I3_TroisRappelsIgnores_ArretDefinitif | vert | Regles130PurTests.cs |
| M6.1-I1 | M6_1_I1_FrappeHorsEnCours_EstIgnoree | vert | Regles130LeconsTests.cs |
| M6.2-I1 | M6_2_I1_CompleteExercise_NeS_ExecuteQuUneFoisParSeance | vert | Regles130LeconsTests.cs |
| M6.2-I2 | M6_2_I2_NouvelleSeance_RepartDeStartCurrentSession | vert | Regles130LeconsTests.cs |
| M6.3-I1 | M6_3_I1_EndTutorial_EstIdempotent | vert | Regles130LeconsTests.cs |
| M6.3-I2 | M6_3_I2_FermerSurLEcranFinal_ValaitReussite | vert | Regles130LeconsTests.cs |
| M6.4-I2 | M6_4_I2_ChangerDeModuleOuDeLecon_SortDeLaSeanceComposee | vert | Regles130LeconsTests.cs |
| M6.4-I5 | M6_4_I5_DefiPerime_NEffacePasLaSeanceComposee_Caracterisation | vert | Regles130LeconsTests.cs |
| M6.5-I1 | M6_5_I1_Hide_CoupeLesMinuteriesDIndice | vert | Regles130LeconsTests.cs |
| M6.5-I2 | M6_5_I2_WmTimerApresHide_EstSansEffet | vert | Regles130LeconsTests.cs |
| M6.6-I2 | M6_6_I2_MinuterieDeStatistiquesLibres_SelonModeDebutEtOption | vert | Regles130LeconsTests.cs |
| M6.7-I1 | M6_7_I1_Hide_EteintVerrMaj | vert | Regles130LeconsTests.cs |
| M6.7-I3 | M6_7_I3_Dispose_LiberLesFournisseursUiAutomation | vert | Regles130LeconsTests.cs |
| M7.1-I1 | M7_1_I1_EtapeCourante_ResteEntre0EtLaDerniereEtape | vert | Regles130AccueilReglagesTests.cs |
| M7.1-I2 | M7_1_I2_Etape3Atteinte_VraiSeulementSiVueDepuisLeDernierShow | vert | Regles130AccueilReglagesTests.cs |
| M7.2-I1 | M7_2_I1_AccueilDejaOuvert_RamenePremierPlanSansRetourALEtape1 | vert (en plus) | Regles130TrayTests.cs |
| M7.2-I5 | M7_2_I5_CloseDeuxFois_UneSeuleEcriture_OnClosedRappele | vert | Regles130AccueilReglagesTests.cs |
| M7.3-I2 | M7_3_I2_ModuleAppris_ImpliqueModuleTenteALOuverture | vert | Regles130AccueilReglagesTests.cs |
| M7.3-I3 | M7_3_I3_TutorialOpen_SuitLeParcoursOuvertDansLesLecons | vert | Regles130AccueilReglagesTests.cs |
| M7.4-I7 | M7_4_I7_DeuxDemandesSuccessives_LaSecondeEstRefusee_CompteurA1 | vert (en plus) | Regles130TrayTests.cs |
| M7.5-I6 | M7_5_I6_TickDeQuietude_NeFaitAucuneEcritureSurDisque | vert (en plus) | Regles130TrayTests.cs |
| M7.6-I1 | M7_6_I1_NouvelleBalloon_RemplaceLaPrecedenteEtInvalideLaCible | vert | Regles130TrayTests.cs |
| M7.6-I2 | M7_6_I2_ClicDeBalloon_NeSAppliqueQuUneFois | vert | Regles130TrayTests.cs |
| M7.6-I3 | M7_6_I3_BalloonExpiree_NEstPlusCliquable | vert | Regles130TrayTests.cs |
| M7.6-I5 | M7_6_I5_ToastAncienClique_OuvreSansNouvelEffetDEtat | vert | Regles130PurTests.cs |
| M7.7-I2 | M7_7_I2_RappelDeFin_NeFaitQueOuvrirLeLienEtJournaliser | vert | Regles130PurTests.cs |
| M7.8-I3 | M7_8_I3_EntreeMaintenue_NOuvreQuUneUrlParAppui | vert (corrigé 1.3.0) | Regles130PurTests.cs |
| M8.1-I1 | M8_1_I1_PasDeCaptureDeRaccourciHorsDeLaFenetreVisible | vert | Regles130AccueilReglagesTests.cs |
| M8.1-I3 | M8_1_I3_PauseDesParametres_JamaisArmeeEnProduction | vert | Regles130PurTests.cs |
| M8.1-I4 | M8_1_I4_Cleanup_DeuxFoisAvecDesFenetresReelles_DisposeUnique | vert (en plus) | Regles130TrayTests.cs |
| M8.1-I5 | M8_1_I5_DoubleDispose_LePinceauNEstSupprimeQuUneFois | vert (corrigé 1.3.0) | Regles130AccueilReglagesTests.cs |
| M8.2-I1 | M8_2_I1_Hide_EffaceLaSurbrillanceDuClavierVirtuel | vert | Regles130AccueilReglagesTests.cs |
| M8.2-I3 | M8_2_I3_CopieDeSecours_SeulementSiRienNAEteEmis | vert | Regles130AccueilReglagesTests.cs |
| M8.3-I1 | M8_3_I1_ClavierMasque_PasDeSurbrillanceNiDeMinuterie | vert | Regles130AccueilReglagesTests.cs |
| M8.3-I2 | M8_3_I2_Cleanup_DecrocheLeHookAvantLesFenetres | vert (en plus) | Regles130TrayTests.cs |
| M8.3-I3 | M8_3_I3_WmSize_EnregistreLesBornesSeulementQuandVisible | vert | Regles130AccueilReglagesTests.cs |
| M8.4-I3 | M8_4_I3_CouchesMaintenables_InscriteSeulementQuandVisible | vert | Regles130AccueilReglagesTests.cs |
| M8.5-I2 | M8_5_I2_Dispose_DesinscritAvantDeDetruire_ChaqueFenetreInscrite | vert | Regles130AccueilReglagesTests.cs |
