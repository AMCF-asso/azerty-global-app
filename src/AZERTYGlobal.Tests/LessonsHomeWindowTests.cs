using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Accueil des Leçons (direction B), de bout en bout sur la fenêtre cachée : jamais de
/// <c>Show()</c>, la page se pose par réflexion et se repeint par un vrai WM_PAINT. Le
/// repeint remplit les zones cliquables, que les tests activent comme le ferait un clic.
/// </summary>
public class LessonsHomeWindowTests
{
    private static LessonsWindow Open()
    {
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        return new LessonsWindow(layout, mapper, hook);
    }

    private static void SetPage(LessonsWindow window, string page)
    {
        var type = typeof(LessonsWindow).GetNestedType("HomePage", System.Reflection.BindingFlags.NonPublic)!;
        BancCapture.SetField(window, "_homePage", Enum.Parse(type, page));
    }

    private static string Page(LessonsWindow window) => BancCapture.Field<object>(window, "_homePage").ToString()!;

    private static void Paint(LessonsWindow window)
    {
        IntPtr hwnd = BancCapture.Handle(window);
        Win32.InvalidateRect(hwnd, IntPtr.Zero, false);
        Win32.SendMessageW(hwnd, Win32.WM_PAINT, IntPtr.Zero, IntPtr.Zero);
    }

    private static List<LessonControlView> Controls(LessonsWindow window) =>
        BancCapture.Field<List<LessonControlView>>(window, "_controlViews");

    /// <summary>Active la cible nommée, comme un clic ou Entrée sur elle.</summary>
    private static void Activate(LessonsWindow window, params string[] namePrefixes)
    {
        var controls = Controls(window);
        int index = controls.FindIndex(control => namePrefixes.Any(prefix => control.Name.StartsWith(prefix, StringComparison.Ordinal)));
        Assert.True(index >= 0, $"aucune cible « {string.Join(" ou ", namePrefixes)} » parmi : {string.Join(" | ", controls.Select(c => c.Name))}");
        BancCapture.Field<List<(Win32.RECT Rect, Action Action)>>(window, "_clickActions")[index].Action();
    }

    private static string Summary(LessonsWindow window) => BancCapture.Field<LessonSurfaceView?>(window, "_surfaceView")!.Text;

    /// <summary>Un caractère à geste que les textes des leçons portent, pour que la révision
    /// y trouve un mot.</summary>
    private static string ReviewableCharacter(LessonsWindow window)
    {
        var catalog = BancCapture.Field<LessonCatalog>(window, "_catalog");
        var inLessons = LessonsHomeLogic.LessonCharacters(catalog);
        return HomeKeyCatalog.Shared.All.Select(c => c.Character).First(c => inLessons.Contains(c[0]));
    }

    /// <summary>Un caractère à revoir maintenant : tapé de travers, donc échu aussitôt.</summary>
    private static void MakeDue(LessonsWindow window, string character)
    {
        var stats = new LessonAttemptStats(() => DateTimeOffset.UtcNow);
        stats.RecordChar('~', character[0]);
        stats.RecordChar(character[0], character[0]);
        BancCapture.Field<LessonProgressStore>(window, "_progress").RecordGestures(stats, c => c == character[0]);
    }

    [Fact]
    public void LAccueilNEstPasLEtatParDefaut_LesAutresTestsLesLeconsLeSavent()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            Assert.Equal("Exercise", Page(window));
            Paint(window);
            Assert.DoesNotContain(Controls(window), control => control.Name == L.LessonsHome_Today && control.Kind == LessonControlKind.ListItem);
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void Aujourdhui_ONomme_LesOnglets_LesBoutons_EtResumeLaPageAuLecteurDEcran()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            SetPage(window, "Today");
            Paint(window);

            var controls = Controls(window);
            var today = Assert.Single(controls, control => control.Kind == LessonControlKind.ListItem && control.Name == L.LessonsHome_Today);
            Assert.True(today.Selected);
            Assert.Contains(controls, control => control.Name == L.LessonsHome_AllLessons);
            Assert.Contains(controls, control => control.Name.StartsWith(L.LessonsHome_StartButton, StringComparison.Ordinal)
                                                || control.Name.StartsWith(L.LessonsHome_ResumeButton, StringComparison.Ordinal));
            Assert.All(controls, control => Assert.False(string.IsNullOrWhiteSpace(control.Name)));
            Assert.Contains(L.LessonsHome_ReviewTitle, Summary(window));
            Assert.Contains(L.LessonsHome_ReviewNone, Summary(window)); // aucune progression dans l'isolation
            Assert.NotEmpty(BancCapture.Field<System.Collections.IEnumerable>(window, "_hoverAreas").Cast<object>()); // infobulles de la carte
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void Reviser_LanceLaSeanceComposee_SansToucherLaPositionDuCatalogue_EtLeRetourRamenePage()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            string character = ReviewableCharacter(window);
            MakeDue(window, character);
            int module = BancCapture.Field<int>(window, "_moduleIndex");
            int lesson = BancCapture.Field<int>(window, "_lessonIndex");
            int exercise = BancCapture.Field<int>(window, "_exerciseIndex");
            SetPage(window, "Today");
            Paint(window);
            Assert.Contains(L.LessonsHome_ReviewCount(1), Summary(window));

            Activate(window, L.LessonsHome_ReviewButton);

            Assert.Equal("Exercise", Page(window));
            Assert.NotNull(BancCapture.Field<object?>(window, "_homeComposed"));
            Assert.True((bool)BancCapture.Call(window, "get_HomeIsComposedSession")!);
            var current = (LessonExercise)BancCapture.Call(window, "get_CurrentExercise")!;
            Assert.Equal(LessonCatalogLoader.ReviewModuleId, current.ModuleId);
            Assert.Contains(character, current.Content);
            // Les index du catalogue, eux, n'ont pas bougé : « Reprendre » les retrouvera.
            Assert.Equal(module, BancCapture.Field<int>(window, "_moduleIndex"));
            Assert.Equal(lesson, BancCapture.Field<int>(window, "_lessonIndex"));
            Assert.Equal(exercise, BancCapture.Field<int>(window, "_exerciseIndex"));
            Paint(window); // l'écran d'exercice se dessine sur la séance composée
            Assert.Contains(Controls(window), control => control.Name == L.LessonsHome_BackToTodayTooltip);

            BancCapture.Call(window, "NextExercise");

            Assert.Equal("Today", Page(window));
            Assert.Null(BancCapture.Field<object?>(window, "_homeComposed"));
            Assert.Equal(exercise, BancCapture.Field<int>(window, "_exerciseIndex"));
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void Reprendre_RamenePageDExerciceSurLaPositionDuCatalogue()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            SetPage(window, "Today");
            Paint(window);

            Activate(window, L.LessonsHome_StartButton, L.LessonsHome_ResumeButton);

            Assert.Equal("Exercise", Page(window));
            Assert.Null(BancCapture.Field<object?>(window, "_homeComposed"));
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void ToutesLesLecons_QuitteLAccueil()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            SetPage(window, "Today");
            Paint(window);

            Activate(window, L.LessonsHome_AllLessons);

            Assert.Equal("Exercise", Page(window));
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void SurAujourdhui_LaFrappeNAtteintPasLaSeanceCachee_EtTabParcourtLesBoutons()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            SetPage(window, "Today");
            Paint(window);
            var before = BancCapture.Field<LessonTypingSession>(window, "_session");
            string line = before.Exercise.Lines[0];
            int position = before.CursorPosition;

            BancCapture.Call(window, "OnChar", line[0]);
            BancCapture.Call(window, "OnKeyDown", 0x08); // Retour arrière

            Assert.Same(before, BancCapture.Field<LessonTypingSession>(window, "_session"));
            Assert.Equal(position, before.CursorPosition);

            BancCapture.Call(window, "OnKeyDown", 0x09);
            Assert.Equal(0, BancCapture.Field<int>(window, "_focusedActionIndex"));
            BancCapture.Call(window, "OnKeyDown", 0x09);
            Assert.Equal(1, BancCapture.Field<int>(window, "_focusedActionIndex"));
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void PlusTard_ReplieLaRevisionSansLaPerdre()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            MakeDue(window, ReviewableCharacter(window));
            SetPage(window, "Today");
            Paint(window);

            Activate(window, L.LessonsHome_ReviewLater);
            Paint(window);

            Assert.True(BancCapture.Field<bool>(window, "_homeReviewDeferred"));
            Assert.Contains(L.LessonsHome_ReviewCount(1), Summary(window)); // reste à revoir, simplement pas proposé
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void LAnglais_ChangeLesLibelles()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            L.Language = "en";
            SetPage(window, "Today");
            Paint(window);

            Assert.Contains(Controls(window), control => control.Name == "Today" && control.Kind == LessonControlKind.ListItem);
            Assert.Contains("Today’s review", Summary(window));
        }
        finally { BancCapture.Teardown(window); }
    }
}
