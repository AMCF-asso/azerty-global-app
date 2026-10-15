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
    public void VotreCarte_UnClicChoisitLaTouche_LesFlechesPassentALaVoisine_LaSeanceSeLance()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            string character = ReviewableCharacter(window);
            MakeDue(window, character);
            uint scancode = HomeKeyCatalog.Shared.All.First(c => c.Character == character).Place.Scancode;

            // Aujourd'hui : un clic sur la touche ouvre « Votre carte » sur elle.
            SetPage(window, "Today");
            Paint(window);
            var keys = BancCapture.Field<List<(uint Scancode, Win32.RECT Rect)>>(window, "_homeMapKeys");
            var rect = keys.First(k => k.Scancode == scancode).Rect;
            BancCapture.SetField(window, "_homeClickPoint", ((rect.left + rect.right) / 2, (rect.top + rect.bottom) / 2));
            Activate(window, L.LessonsHome_MapKeysName);
            Assert.Equal("Map", Page(window));
            Assert.Equal(scancode, BancCapture.Field<uint?>(window, "_homeMapKey"));

            Paint(window);
            Assert.True(Assert.Single(Controls(window), c => c.Kind == LessonControlKind.ListItem && c.Name == L.LessonsHome_YourMap).Selected);
            Assert.Contains(L.LessonsHome_MapKeysHelp, Summary(window));
            Assert.Equal(scancode, BancCapture.Field<uint?>(window, "_homeMapShownKey"));

            // Flèche droite puis gauche : la voisine, puis retour.
            BancCapture.Call(window, "HomeHandleKey", 0x27);
            Paint(window);
            Assert.NotEqual(scancode, BancCapture.Field<uint?>(window, "_homeMapShownKey"));
            BancCapture.Call(window, "HomeHandleKey", 0x25);
            Paint(window);
            Assert.Equal(scancode, BancCapture.Field<uint?>(window, "_homeMapShownKey"));

            // Un clic entre les touches ne change rien.
            BancCapture.SetField(window, "_homeClickPoint", (0, 0));
            Activate(window, L.LessonsHome_MapKeysName);
            Assert.Equal(scancode, BancCapture.Field<uint?>(window, "_homeMapKey"));

            // La séance de la touche : une séance composée, sur l'écran d'exercice.
            Activate(window, L.LessonsHome_KeySessionName(""));
            Assert.Equal("Exercise", Page(window));
            Assert.NotNull(BancCapture.Field<object?>(window, "_homeComposed"));
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
            Assert.Contains(Controls(window), control => control.Name == L.LessonsHome_Today); // lien de l'en-tête papier

            BancCapture.Call(window, "NextExercise");

            Assert.Equal("Today", Page(window));
            Assert.Null(BancCapture.Field<object?>(window, "_homeComposed"));
            Assert.Equal(exercise, BancCapture.Field<int>(window, "_exerciseIndex"));
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void UneRevisionTerminee_AfficheLeRecapitulatif_EnregistreLeGeste_SansEntreeDExercice_EtRevientPage()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            string character = ReviewableCharacter(window);
            MakeDue(window, character);
            var progress = BancCapture.Field<LessonProgressStore>(window, "_progress");
            int attemptsBefore = progress.Gestures[character].Attempts;
            string? lastModule = progress.LastModuleId;
            int lastIndex = progress.LastExerciseIndex;
            SetPage(window, "Today");
            Paint(window);
            Activate(window, L.LessonsHome_ReviewButton);

            var exercise = (LessonExercise)BancCapture.Call(window, "get_CurrentExercise")!;
            foreach (char c in string.Concat(exercise.Lines))
                BancCapture.Call(window, "OnChar", c);

            Assert.True(BancCapture.Field<bool>(window, "_showSummary"));
            Paint(window); // le récapitulatif se dessine sur le module composé
            Assert.Contains(Controls(window), control => control.Name == L.LessonsHome_Finish); // pas de suite : « Terminer »
            Assert.Empty(progress.Exercises);                       // rien à retenir par exercice
            Assert.Equal(lastModule, progress.LastModuleId);        // la position de « Reprendre » n'a pas bougé
            Assert.Equal(lastIndex, progress.LastExerciseIndex);
            Assert.True(progress.Gestures[character].Attempts > attemptsBefore); // mais le geste est enregistré

            BancCapture.Call(window, "NextExercise");

            Assert.Equal("Today", Page(window));
            Assert.Null(BancCapture.Field<object?>(window, "_homeComposed"));
            Paint(window);
            Assert.Contains(L.LessonsHome_ReviewTitle, Summary(window));

            // « Reprendre » retrouve l'exercice du catalogue à son début, ni le récapitulatif ni
            // la séance finie de la révision.
            Activate(window, L.LessonsHome_StartButton, L.LessonsHome_ResumeButton);
            Assert.Equal("Exercise", Page(window));
            Assert.False(BancCapture.Field<bool>(window, "_showSummary"));
            var session = BancCapture.Field<LessonTypingSession>(window, "_session");
            Assert.Same(BancCapture.Call(window, "get_CurrentExercise"), session.Exercise);
            Assert.NotEqual(LessonCatalogLoader.ReviewModuleId, session.Exercise.ModuleId);
            Assert.Equal(0, session.CursorPosition);
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void VotreTexte_LeChampMarqueLesGestes_LeBilanEtLaSeance_TerminerRevientPage()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            SetPage(window, "Today");
            Paint(window);
            Activate(window, L.LessonsHome_TextButton);
            Assert.Equal("Text", Page(window));
            BancCapture.Call(window, "HomeTextForget"); // le presse-papiers de la machine n'entre pas dans le test

            // La frappe va au champ, émoji compris (deux WM_CHAR) ; Entrée passe à la ligne.
            foreach (char c in "Ñaŋ — ok 🙏\rNdar")
                BancCapture.Call(window, "OnChar", c);
            Assert.Equal("Ñaŋ — ok 🙏\nNdar", BancCapture.Field<string>(window, "_homeText"));
            Paint(window);
            Assert.Equal("Ñaŋ — ok 🙏\nNdar", Summary(window)); // le lecteur d'écran lit le champ
            var start = Assert.Single(Controls(window), c => c.Name.StartsWith(L.LessonsHome_TextStart, StringComparison.Ordinal));
            Assert.EndsWith(L.LessonsHome_TextStartName(0, 1)[L.LessonsHome_TextStartName(0, 0).Length..], start.Name); // 🙏 sauté
            Assert.True((bool)BancCapture.Call(window, "get_HasTypingSurface")!);

            // Retour arrière retire une grappe entière, Gauche puis Suppr celle d'après.
            BancCapture.Call(window, "OnChar", '\b');
            Assert.Equal("Ñaŋ — ok 🙏\nNda", BancCapture.Field<string>(window, "_homeText"));
            BancCapture.Call(window, "HomeHandleKey", 0x25);
            BancCapture.Call(window, "HomeHandleKey", 0x2E);
            Assert.Equal("Ñaŋ — ok 🙏\nNd", BancCapture.Field<string>(window, "_homeText"));

            // La séance : le texte sans 🙏, en séance composée, le catalogue intact.
            int exercise = BancCapture.Field<int>(window, "_exerciseIndex");
            Paint(window);
            Activate(window, L.LessonsHome_TextStart);
            Assert.Equal("Exercise", Page(window));
            var current = (LessonExercise)BancCapture.Call(window, "get_CurrentExercise")!;
            Assert.Equal(LessonCatalogLoader.CustomTextModuleId, current.ModuleId);
            Assert.Equal("Ñaŋ — ok\nNd", current.Content);

            foreach (char c in string.Concat(current.Lines))
                BancCapture.Call(window, "OnChar", c);
            Assert.True(BancCapture.Field<bool>(window, "_showSummary"));
            Paint(window);
            Activate(window, L.LessonsHome_Finish);
            Assert.Equal("Today", Page(window));
            Assert.Equal(exercise, BancCapture.Field<int>(window, "_exerciseIndex"));

            // Le texte reste le temps de la fenêtre ; sa fermeture l'oublie.
            Assert.Equal("Ñaŋ — ok 🙏\nNd", BancCapture.Field<string>(window, "_homeText"));
            BancCapture.Call(window, "HomeOnHide");
            Assert.Equal("", BancCapture.Field<string>(window, "_homeText"));
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void VotreTexte_EspaceOuEntreeSurUnBouton_NEcriventRien_EchapRevientPage()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            SetPage(window, "Text");
            Paint(window);
            BancCapture.Call(window, "OnChar", 'a');
            Paint(window);
            BancCapture.Call(window, "OnKeyDown", 0x09); // Tab : du champ à la première cible
            Assert.True(BancCapture.Field<int>(window, "_focusedActionIndex") >= 0);
            BancCapture.Call(window, "HomeTextChar", ' ');
            BancCapture.Call(window, "HomeTextChar", '\r');
            Assert.Equal("a", BancCapture.Field<string>(window, "_homeText"));
            BancCapture.Call(window, "OnChar", 'b'); // une lettre rend le focus au champ
            Assert.Equal(-1, BancCapture.Field<int>(window, "_focusedActionIndex"));
            Assert.Equal("ab", BancCapture.Field<string>(window, "_homeText"));

            BancCapture.Call(window, "OnKeyDown", 0x1B);
            Assert.Equal("Today", Page(window));
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void UnRechargementDeLangue_PendantUneSeanceComposee_GardeLaPositionDuCatalogue()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            string character = ReviewableCharacter(window);
            MakeDue(window, character);
            // Une position hors du premier module, pour que le sélecteur de séance ne la masque pas.
            Assert.True((bool)BancCapture.Call(window, "SelectExercise", LessonCatalogLoader.InitiationModuleId,
                LessonCatalogLoader.InitiationLessonId, 2, false)!);
            SetPage(window, "Today");
            Paint(window);
            Activate(window, L.LessonsHome_ReviewButton);

            L.Language = "en";
            BancCapture.Call(window, "RefreshLanguage");

            BancCapture.Call(window, "NextExercise"); // la fin de séance rend l'index du catalogue, intact
            Assert.Equal(2, BancCapture.Field<int>(window, "_exerciseIndex"));
            Assert.Equal(0, BancCapture.Field<int>(window, "_lessonIndex"));
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
