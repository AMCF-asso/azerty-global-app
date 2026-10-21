using System;
using System.Linq;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Parcours de l'accueil dans Leçons › Initiation (audit 1.3.0, B2 voie (b), décision
/// d'Antoine du 20/10) : écran des usages, exercices du parcours, reprise au premier
/// exercice non réussi (A1), écran final, retour vers l'accueil, et la réussite d'un
/// exercice de l'Initiation versée au compteur de l'accueil. La fenêtre n'est jamais
/// affichée par <c>Show()</c>, qui prendrait le premier plan du poste.
/// </summary>
public class ParcoursAccueilTests
{
    [Fact]
    public void Le_parcours_s_ouvre_sur_les_usages_coches_au_dernier_passage()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetLearningUsages(new[] { TutorialSteps.UsageCode });
        var window = Open();
        try
        {
            window.BeginTutorial();

            Assert.Equal("Usages", Page(window));
            var usages = BancCapture.Field<bool[]>(window, "_tutorialUsages")!;
            Assert.Equal(new[] { false, false, true, false, false }, usages);
            Assert.Equal(LessonCatalogLoader.InitiationModuleId, CurrentModuleId(window));
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void Continuer_sans_usage_mene_au_parcours_Documents()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            window.BeginTutorial();
            BancCapture.Call(window, "StartTutorialRoute");

            Assert.Equal("None", Page(window));
            Assert.Equal(new[] { 0, 1, 2, TutorialSteps.Documents }, RouteIndexes(window));
            Assert.Equal(0, BancCapture.Field<int>(window, "_exerciseIndex"));
            Assert.Empty(ConfigManager.LearningUsages);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    /// <summary>A1 : les exercices déjà réussis sont sautés, le parcours ne repart pas de « É ».</summary>
    [Fact]
    public void Le_parcours_reprend_au_premier_exercice_non_reussi()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.RecordLearningExerciseCompleted(0);
        ConfigManager.RecordLearningExerciseCompleted(1);
        var window = Open();
        try
        {
            window.BeginTutorial();
            BancCapture.Call(window, "StartTutorialRoute");

            Assert.Equal(2, BancCapture.Field<int>(window, "_exerciseIndex"));
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    /// <summary>Sans ce relais, l'accueil revenait à chaque démarrage : seul l'ancien tutoriel
    /// écrivait ce compteur (TrayApplication : LearningMaxStepCompleted &lt; DoneThreshold).</summary>
    [Fact]
    public void Reussir_le_tronc_commun_dans_l_Initiation_arrete_le_retour_de_l_accueil()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            for (int i = 0; i < TutorialSteps.DoneThreshold; i++)
            {
                Assert.True((bool)BancCapture.Call(window, "SelectExercise", LessonCatalogLoader.InitiationModuleId,
                    LessonCatalogLoader.InitiationLessonId, i, false)!);
                // Le rang 1 avec une espace ordinaire : la tolérance de B1 suffit à le réussir.
                Type(window, TutorialSteps.All[i].Target.Replace('\u202F', ' '));
                Assert.True(BancCapture.Field<bool>(window, "_showSummary"), $"exercice {i} non terminé");
            }

            Assert.Equal(TutorialSteps.DoneThreshold, ConfigManager.LearningMaxStepCompleted);
            Assert.Equal(new[] { 0, 1, 2 }, ConfigManager.LearningCompletedExercises.ToArray());
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void Suivant_suit_le_parcours_jusqu_a_l_ecran_final_puis_Terminer_previent_l_accueil()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        bool? closed = null;
        window.TutorialClosed = completed => closed = completed;
        try
        {
            window.BeginTutorial();
            BancCapture.Call(window, "StartTutorialRoute");
            Type(window, "É");
            Assert.True(BancCapture.Field<bool>(window, "_showSummary"));

            BancCapture.Call(window, "NextExercise");
            Assert.Equal(1, BancCapture.Field<int>(window, "_exerciseIndex"));

            // Les trois suivants réussis ailleurs : « Suivant » mène à l'écran final.
            ConfigManager.RecordLearningExerciseCompleted(1);
            ConfigManager.RecordLearningExerciseCompleted(2);
            ConfigManager.RecordLearningExerciseCompleted(TutorialSteps.Documents);
            BancCapture.Call(window, "NextExercise");
            Assert.Equal("Final", Page(window));
            Assert.Null(closed);

            BancCapture.Call(window, "FinishTutorial");
            Assert.True(closed);
            Assert.False(window.IsTutorialActive);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void Un_exercice_bonus_se_passe()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            window.BeginTutorial();
            BancCapture.Call(window, "ToggleTutorialUsage", 2); // Code
            BancCapture.Call(window, "ToggleTutorialUsage", 4); // Afrique, plus particulier : principal
            BancCapture.Call(window, "StartTutorialRoute");
            Assert.Equal(new[] { 0, 1, 2, TutorialSteps.Africa, TutorialSteps.Code }, RouteIndexes(window));

            for (int i = 0; i < 4; i++) BancCapture.Call(window, "AdvanceTutorialRoute");
            Assert.Equal(TutorialSteps.Code, BancCapture.Field<int>(window, "_exerciseIndex"));

            BancCapture.Call(window, "AdvanceTutorialRoute"); // « Passer cet exercice »
            Assert.Equal("Final", Page(window));
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void Fermer_la_fenetre_ou_passer_pour_l_instant_clot_le_parcours_sans_reussite()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        bool? closed = null;
        window.TutorialClosed = completed => closed = completed;
        try
        {
            window.BeginTutorial();
            BancCapture.Call(window, "StartTutorialRoute");
            BancCapture.Call(window, "LeaveTutorialForNow");

            Assert.False(closed);
            Assert.False(window.IsTutorialActive);
            Assert.False(window.IsVisible);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    /// <summary>Repris de l'ancien tutoriel (constat du 2026-09-21) : les exercices de l'accueil
    /// ne nourrissent pas le seuil de la demande d'avis ; la plage se referme avec le parcours,
    /// et au plus tard avec la fenêtre.</summary>
    [Fact]
    public void La_frappe_du_parcours_reste_hors_des_statistiques_jusqu_a_sa_fin()
    {
        using var isolation = new BancCapture.Isolation();
        Assert.False(UsageStats.IsTypingExcluded);
        var window = Open();
        try
        {
            window.BeginTutorial();
            Assert.True(UsageStats.IsTypingExcluded);
            window.BeginTutorial(); // rouvert depuis l'accueil : une seule plage
            BancCapture.Call(window, "Hide");
            Assert.False(UsageStats.IsTypingExcluded);

            window.BeginTutorial();
            Assert.True(UsageStats.IsTypingExcluded);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
        Assert.False(UsageStats.IsTypingExcluded);
    }

    [Fact]
    public void L_ecran_des_usages_ne_recoit_aucune_frappe()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            window.BeginTutorial();
            Type(window, "É");

            Assert.False(BancCapture.Field<bool>(window, "_showSummary"));
            Assert.Equal("Usages", Page(window));
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void Le_parcours_ouvert_une_fois_reste_note_dans_la_config()
    {
        using var isolation = new BancCapture.Isolation();
        Assert.False(ConfigManager.LearningTutorialOpened);
        ConfigManager.MarkLearningTutorialOpened();
        Assert.True(ConfigManager.LearningTutorialOpened);
    }

    private static LessonsWindow Open()
    {
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        return new LessonsWindow(layout, mapper, hook);
    }

    private static void Type(LessonsWindow window, string text)
    {
        foreach (char c in text)
            BancCapture.Call(window, "OnChar", c);
    }

    private static string Page(LessonsWindow window) =>
        BancCapture.Field<object>(window, "_tutorialPage")!.ToString()!;

    private static int[] RouteIndexes(LessonsWindow window) =>
        BancCapture.Field<TutorialSteps.RouteStep[]>(window, "_tutorialRoute")!.Select(step => step.Index).ToArray();

    private static string CurrentModuleId(LessonsWindow window)
    {
        var catalog = BancCapture.Field<LessonCatalog>(window, "_catalog")!;
        return catalog.Modules[BancCapture.Field<int>(window, "_moduleIndex")].Id;
    }
}
