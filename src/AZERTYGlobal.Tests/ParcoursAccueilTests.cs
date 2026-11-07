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
    public void Continuer_les_lecons_clot_le_parcours_comme_reussi_et_ouvre_Aujourd_hui()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        bool? closed = null;
        window.TutorialClosed = completed => closed = completed;
        try
        {
            window.BeginTutorial();
            BancCapture.Call(window, "StartTutorialRoute");
            for (int i = 0; i < 4; i++)
            {
                ConfigManager.RecordLearningExerciseCompleted(i);
                BancCapture.Call(window, "AdvanceTutorialRoute");
            }
            Assert.Equal("Final", Page(window));

            BancCapture.Call(window, "ContinueAfterTutorial", (Action)(() => BancCapture.Call(window, "HomeShowToday")));

            Assert.True(closed);
            Assert.False(window.IsTutorialActive);
            Assert.Equal("Today", BancCapture.Field<object>(window, "_homePage")!.ToString());
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

    /// <summary>Audit 1.3.0, D5 : l'en-tête comptait les huit exercices de l'Initiation (« 5 sur 8 »)
    /// quand la ligne du parcours en comptait cinq (« 5 sur 5 (bonus) »), et l'écran final
    /// affichait « 4 sur 8 ». L'en-tête suit la longueur du parcours, comme cette ligne.</summary>
    [Fact]
    public void L_en_tete_compte_le_parcours_et_non_les_huit_exercices_de_l_Initiation()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            Assert.Null(HeaderRoute(window)); // hors parcours : la leçon entière

            window.BeginTutorial();
            BancCapture.Call(window, "ToggleTutorialUsage", 2); // Code
            BancCapture.Call(window, "ToggleTutorialUsage", 4); // Afrique
            BancCapture.Call(window, "StartTutorialRoute");
            Assert.Equal(5, HeaderRoute(window)!.Length);
            Assert.Equal(0, BancCapture.Field<int>(window, "_tutorialRoutePos"));

            for (int i = 0; i < 4; i++) BancCapture.Call(window, "AdvanceTutorialRoute");
            Assert.Equal(5, HeaderRoute(window)!.Length); // le bonus : 5 sur 5
            Assert.Equal(4, BancCapture.Field<int>(window, "_tutorialRoutePos"));

            // Hors de l'étape (« ‹ » ramène à un autre exercice) : la leçon entière reprend.
            BancCapture.Call(window, "PreviousExercise");
            Assert.Null(HeaderRoute(window));

            BancCapture.Call(window, "AdvanceTutorialRoute"); // l'écran final
            Assert.Equal("Final", Page(window));
            Assert.Equal(5, HeaderRoute(window)!.Length);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    /// <summary>Remontée n° 6 d'Antoine (07/11) : « Il y a des boutons de partout. Si on clique
    /// sur un bouton, ça nous fait quitter l'onboarding. » Pendant le parcours, l'en-tête n'offre
    /// plus ni Aujourd'hui, ni Recommencer, ni Libre, ni Réglages, ni sélecteur, ni chevrons.</summary>
    [Fact]
    public void Pendant_le_parcours_rien_ne_fait_quitter_l_accueil()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            string[] leaving =
            {
                L.LessonsHome_Today, L.LessonsWin_IconRestart, L.LessonsPaper_FreeTyping, L.LessonsWin_SettingsTab,
                L.LessonsWin_IconPrevious, L.LessonsWin_IconNext, L.LessonsWin_Title,
            };

            window.BeginTutorial();
            Assert.Equal(L.Learning_WindowTitle, Caption(window));
            Paint(window);
            Assert.DoesNotContain(ControlNames(window), name => leaving.Contains(name));
            Assert.Contains(L.Learning_BtnContinue, ControlNames(window));

            BancCapture.Call(window, "StartTutorialRoute");
            Paint(window);
            var names = ControlNames(window);
            Assert.DoesNotContain(names, name => leaving.Contains(name));
            string picker = BancCapture.Call(window, "get_CurrentModule") is LessonModule module
                ? $"{module.Title} {((LessonLesson)BancCapture.Call(window, "get_CurrentLesson")!).Title}" : "";
            Assert.DoesNotContain(picker, names); // le sélecteur de leçon
            Assert.Contains(L.LessonsWin_IconHint, names);
            Assert.Contains(L.Learning_SkipForNow, names);

            // Hors parcours, l'en-tête complet revient.
            BancCapture.Call(window, "LeaveTutorialForNow");
            Paint(window);
            Assert.Contains(L.LessonsWin_SettingsTab, ControlNames(window));
            Assert.Equal(L.LessonsWin_WindowTitle, Caption(window));
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    private static void Paint(LessonsWindow window)
    {
        IntPtr hwnd = BancCapture.Handle(window);
        Win32.InvalidateRect(hwnd, IntPtr.Zero, false);
        Win32.SendMessageW(hwnd, Win32.WM_PAINT, IntPtr.Zero, IntPtr.Zero);
    }

    private static string Caption(LessonsWindow window)
    {
        var text = new System.Text.StringBuilder(256);
        Win32.GetWindowTextW(BancCapture.Handle(window), text, text.Capacity);
        return text.ToString();
    }

    private static string[] ControlNames(LessonsWindow window) =>
        BancCapture.Field<System.Collections.Generic.List<LessonControlView>>(window, "_controlViews")!
            .Select(control => control.Name).ToArray();

    private static TutorialSteps.RouteStep[]? HeaderRoute(LessonsWindow window) =>
        (TutorialSteps.RouteStep[]?)typeof(LessonsWindow)
            .GetProperty("TutorialRouteForHeader", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(window);

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
