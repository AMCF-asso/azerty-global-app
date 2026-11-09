using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Focus clavier et états de la fenêtre des Leçons (audit UX du 09/11, lot 5 : L4, L5, L6, L7,
/// E5) : un écran changé n'hérite pas du bouton focalisé de l'écran d'avant, une fermeture
/// ne laisse ni bilan périmé ni exercice fini derrière, la liste ouverte garde la frappe.
/// </summary>
public class FocusAndStateTests
{
    private static LessonsWindow Open()
    {
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        return new LessonsWindow(layout, mapper, hook);
    }

    private static void Paint(LessonsWindow window)
    {
        IntPtr hwnd = BancCapture.Handle(window);
        Win32.InvalidateRect(hwnd, IntPtr.Zero, false);
        Win32.SendMessageW(hwnd, Win32.WM_PAINT, IntPtr.Zero, IntPtr.Zero);
    }

    private static LessonTypingSession Session(LessonsWindow window) => BancCapture.Field<LessonTypingSession>(window, "_session");

    private static void SelectRegular(LessonsWindow window, int exercise = 0)
    {
        var catalog = BancCapture.Field<LessonCatalog>(window, "_catalog");
        var module = catalog.Modules.Where(m => m.Id != DailyChallenge.ModuleId).ElementAt(1);
        Assert.True((bool)BancCapture.Call(window, "SelectExercise", module.Id, module.Lessons[0].Id, exercise, false)!);
    }

    private static void TypeToSummary(LessonsWindow window)
    {
        for (int guard = 0; guard < 5000 && !BancCapture.Field<bool>(window, "_showSummary"); guard++)
        {
            char? next = Session(window).GetNextExpectedCharacter();
            Assert.True(next is char, "séance bloquée avant le bilan");
            BancCapture.Call(window, "OnChar", next!.Value);
        }
        Assert.True(BancCapture.Field<bool>(window, "_showSummary"));
    }

    private static void SetMode(LessonsWindow window, string mode)
    {
        var type = typeof(LessonsWindow).GetNestedType("WindowMode", System.Reflection.BindingFlags.NonPublic)!;
        BancCapture.SetField(window, "_mode", Enum.Parse(type, mode));
    }

    /// <summary>L4 : le bouton focalisé (Tab) pendant la frappe ne reste pas focalisé au bilan,
    /// où le même index désigne un autre bouton.</summary>
    [Fact]
    public void A_new_screen_starts_without_a_focused_button()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            SelectRegular(window);
            Paint(window);
            BancCapture.Call(window, "OnKeyDown", 0x09); // Tab : de la surface au premier bouton
            BancCapture.Call(window, "OnKeyDown", 0x09);
            Assert.True(BancCapture.Field<int>(window, "_focusedActionIndex") >= 0);

            TypeToSummary(window);
            Paint(window);
            Assert.Equal(-1, BancCapture.Field<int>(window, "_focusedActionIndex"));
        }
        finally { BancCapture.Teardown(window); }
    }

    /// <summary>L4 : avant le repeint, les cibles connues sont celles de l'écran d'avant :
    /// Entrée n'en active aucune.</summary>
    [Fact]
    public void Enter_never_activates_a_target_of_the_previous_screen()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            SelectRegular(window);
            TypeToSummary(window);
            Paint(window);
            BancCapture.SetField(window, "_focusedActionIndex", 0);
            BancCapture.Call(window, "ReplayExercise"); // l'écran change, pas de repeint
            Assert.False((bool)BancCapture.Call(window, "ActivateFocusedAction")!);
            Assert.Equal(-1, BancCapture.Field<int>(window, "_focusedActionIndex"));
        }
        finally { BancCapture.Teardown(window); }
    }

    /// <summary>La liste ouverte garde son focus sur sa première ligne (posé exprès).</summary>
    [Fact]
    public void Opening_the_list_keeps_its_first_line_focused()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            SelectRegular(window);
            Paint(window);
            BancCapture.Call(window, "TogglePicker");
            Paint(window);
            Assert.Equal(0, BancCapture.Field<int>(window, "_focusedActionIndex"));
        }
        finally { BancCapture.Teardown(window); }
    }

    /// <summary>E5 : liste des leçons ouverte, ni lettre ni Retour arrière n'atteignent
    /// l'exercice caché derrière.</summary>
    [Fact]
    public void Typing_with_the_list_open_does_not_reach_the_exercise()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            SelectRegular(window);
            Paint(window);
            BancCapture.Call(window, "OnChar", Session(window).GetNextExpectedCharacter()!.Value);
            Assert.Equal(1, Session(window).CursorPosition);
            BancCapture.Call(window, "TogglePicker");
            BancCapture.Call(window, "OnChar", Session(window).GetNextExpectedCharacter()!.Value);
            BancCapture.Call(window, "OnKeyDown", 0x08);
            Assert.Equal(1, Session(window).CursorPosition);
        }
        finally { BancCapture.Teardown(window); }
    }

    /// <summary>L5 : fermer pendant un bilan vaut « Suivant » : la réouverture ne rend pas
    /// l'ancien bilan, « Reprendre » mène à l'exercice suivant.</summary>
    [Fact]
    public void Closing_on_a_summary_moves_on_to_the_next_exercise()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            SelectRegular(window, exercise: 0);
            TypeToSummary(window);
            BancCapture.Call(window, "Hide");
            Assert.False(BancCapture.Field<bool>(window, "_showSummary"));
            Assert.Equal(1, BancCapture.Field<int>(window, "_exerciseIndex"));
            Assert.Equal(0, Session(window).CursorPosition);
        }
        finally { BancCapture.Teardown(window); }
    }

    /// <summary>L5 : fermer sur le Défi rend la place du catalogue d'où l'on venait, jamais
    /// l'Initiation par défaut.</summary>
    [Fact]
    public void Closing_on_the_challenge_returns_to_the_catalog_position()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            var catalog = BancCapture.Field<LessonCatalog>(window, "_catalog");
            var module = catalog.Modules.Where(m => m.Id != DailyChallenge.ModuleId).ElementAt(1);
            Assert.True((bool)BancCapture.Call(window, "SelectExercise", module.Id, module.Lessons[0].Id, 1, true)!);
            BancCapture.Field<ChallengeSessionState>(window, "_challenge").Request();
            BancCapture.Call(window, "RefreshChallengeModuleIfStale");
            catalog = BancCapture.Field<LessonCatalog>(window, "_catalog");
            var defi = catalog.Modules.Single(m => m.Id == DailyChallenge.ModuleId);
            Assert.True((bool)BancCapture.Call(window, "SelectExercise", defi.Id, defi.Lessons[0].Id, 0, false)!);
            TypeToSummary(window);

            BancCapture.Call(window, "Hide");
            var current = (LessonExercise)BancCapture.Call(window, "get_CurrentExercise")!;
            Assert.Equal(module.Id, current.ModuleId);
            Assert.Equal(1, BancCapture.Field<int>(window, "_exerciseIndex"));
        }
        finally { BancCapture.Teardown(window); }
    }

    /// <summary>L6 : rouvrir sur un exercice positionné (Défi, parcours) sort du mode Libre, de
    /// la liste et des Paramètres où la fenêtre avait été fermée.</summary>
    [Fact]
    public void Opening_on_an_exercise_leaves_free_mode_list_and_settings()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            SetMode(window, "Free");
            BancCapture.SetField(window, "_pickerOpen", true);
            BancCapture.SetField(window, "_settingsOpen", true);
            BancCapture.SetField(window, "_homeOpenOnExercise", true);
            BancCapture.Call(window, "HomeOnShow");
            Assert.Equal("Lessons", BancCapture.Field<object>(window, "_mode").ToString());
            Assert.False(BancCapture.Field<bool>(window, "_pickerOpen"));
            Assert.False(BancCapture.Field<bool>(window, "_settingsOpen"));
            Assert.Equal("Exercise", BancCapture.Field<object>(window, "_homePage").ToString());
        }
        finally { BancCapture.Teardown(window); }
    }

    /// <summary>W2 : rouvrir depuis la zone de notification une fenêtre déjà visible la ramène
    /// devant sans la déplacer ni sortir de l'exercice en cours, frappe comprise.</summary>
    [Fact]
    public void Show_on_a_visible_window_keeps_the_exercise_and_position()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            SelectRegular(window, exercise: 1);
            BancCapture.SetField(window, "_homePage", HomePageValue("Exercise"));
            BancCapture.Call(window, "OnChar", Session(window).GetNextExpectedCharacter()!.Value);
            BancCapture.SetField(window, "_visible", true);
            IntPtr hwnd = BancCapture.Handle(window);
            Win32.GetWindowRect(hwnd, out var before);

            window.Show();

            Assert.Equal("Exercise", BancCapture.Field<object>(window, "_homePage").ToString());
            Assert.Equal(1, BancCapture.Field<int>(window, "_exerciseIndex"));
            Assert.Equal(1, Session(window).CursorPosition);
            Win32.GetWindowRect(hwnd, out var after);
            Assert.Equal((before.left, before.top), (after.left, after.top));
        }
        finally { BancCapture.Teardown(window); }
    }

    /// <summary>W2 : une demande explicite (le Défi) change bien d'écran sur une fenêtre visible.</summary>
    [Fact]
    public void Show_on_a_visible_window_opens_a_requested_exercise()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            BancCapture.SetField(window, "_homePage", HomePageValue("Today"));
            BancCapture.SetField(window, "_visible", true);
            BancCapture.SetField(window, "_homeOpenOnExercise", true);
            window.Show();
            Assert.Equal("Exercise", BancCapture.Field<object>(window, "_homePage").ToString());
        }
        finally { BancCapture.Teardown(window); }
    }

    private static object HomePageValue(string name)
    {
        var type = typeof(LessonsWindow).GetNestedType("HomePage", System.Reflection.BindingFlags.NonPublic)!;
        return Enum.Parse(type, name);
    }

    /// <summary>L7 : à l'écran final du parcours, l'exercice caché derrière repart de zéro :
    /// « Reprendre » n'ouvre pas un exercice fini où la frappe ne fait rien.</summary>
    [Fact]
    public void The_tutorial_final_screen_leaves_a_fresh_exercise_behind()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            BancCapture.Call(window, "BeginTutorial");
            BancCapture.Call(window, "StartTutorialRoute");
            var route = BancCapture.Field<Array>(window, "_tutorialRoute");
            for (int step = 0; step < route.Length && BancCapture.Field<object>(window, "_tutorialPage").ToString() != "Final"; step++)
            {
                TypeToSummary(window);
                BancCapture.Call(window, "NextExercise");
            }
            Assert.Equal("Final", BancCapture.Field<object>(window, "_tutorialPage").ToString());
            Assert.False(BancCapture.Field<bool>(window, "_showSummary"));
            Assert.False(Session(window).IsExerciseComplete);
            Assert.Equal(0, Session(window).CursorPosition);
        }
        finally { BancCapture.Teardown(window); }
    }
}
