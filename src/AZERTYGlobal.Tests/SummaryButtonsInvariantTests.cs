using System.Reflection;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Invariant (audit UX du 09/11, L2) : au bilan d'un exercice, chaque cible dessinée agit.
/// « Suivant » restait dessiné et mort à la fin du Défi et du catalogue. Pour chaque fin
/// d'exercice type, on active chaque cible une à une, sur une fenêtre neuve, et on vérifie
/// que l'état de la fenêtre a changé.
/// </summary>
public class SummaryButtonsInvariantTests
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

    private static LessonCatalog Catalog(LessonsWindow window) => BancCapture.Field<LessonCatalog>(window, "_catalog");

    /// <summary>Fins d'exercice : milieu du catalogue, fin d'une leçon, dernier exercice du
    /// catalogue, échauffement et extrait du Défi.</summary>
    public static IEnumerable<object[]> Ends() => new[]
    {
        new object[] { "milieu" }, new object[] { "fin-de-lecon" }, new object[] { "fin-du-catalogue" },
        new object[] { "defi-echauffement" }, new object[] { "defi-final" }, new object[] { "defi-final-partage" },
        new object[] { "composee" },
    };

    private static void Reach(LessonsWindow window, string end)
    {
        if (end.StartsWith("defi", StringComparison.Ordinal))
        {
            // Pendant la prise en main (5 premières séances), le Défi n'a rien à partager ;
            // après, son extrait final offre « Partager ».
            if (end == "defi-final-partage")
                ConfigManager.SetTrainingSequenceIndex(DailyChallenge.SequenceLength);
            BancCapture.Field<ChallengeSessionState>(window, "_challenge").Request();
            BancCapture.Call(window, "RefreshChallengeModuleIfStale");
        }
        var catalog = Catalog(window);
        var regular = catalog.Modules.Where(m => m.Id != DailyChallenge.ModuleId).ToArray();
        (string module, string lesson, int exercise) = end switch
        {
            "milieu" or "composee" => (regular[1].Id, regular[1].Lessons[0].Id, 0),
            "fin-de-lecon" => (regular[1].Id, regular[1].Lessons[0].Id, regular[1].Lessons[0].Exercises.Count - 1),
            "fin-du-catalogue" => (regular[^1].Id, regular[^1].Lessons[^1].Id, regular[^1].Lessons[^1].Exercises.Count - 1),
            "defi-echauffement" => (DailyChallenge.ModuleId, Defi(catalog).Lessons[0].Id, 0),
            _ => (DailyChallenge.ModuleId, Defi(catalog).Lessons[0].Id, Defi(catalog).Lessons[0].Exercises.Count - 1),
        };
        Assert.True((bool)BancCapture.Call(window, "SelectExercise", module, lesson, exercise, false)!);
        if (end == "composee")
        {
            // Séance composée de l'accueil (révision, touche de la carte) : un module d'un seul exercice.
            var source = regular[1].Lessons[0].Exercises[0];
            BancCapture.Call(window, "HomeStartComposed", source,
                (Func<(string Module, string Lesson, string Instruction)>)(() => ("Révision", "Révision", source.Instruction)),
                (IReadOnlyList<string>)new[] { "a" });
            Assert.NotNull(BancCapture.Field<object?>(window, "_homeComposed"));
        }
        TypeToSummary(window, end);
        Paint(window);
    }

    /// <summary>Tout le texte tapé sans faute, comme l'utilisateur : forcer le bilan sans finir
    /// la séance laisserait « Indice » trouver un caractère attendu et paraître vivant.</summary>
    private static void TypeToSummary(LessonsWindow window, string end)
    {
        for (int guard = 0; guard < 5000 && !BancCapture.Field<bool>(window, "_showSummary"); guard++)
        {
            var session = BancCapture.Field<LessonTypingSession>(window, "_session");
            Assert.True(session.GetNextExpectedCharacter() is char, $"{end} : séance bloquée avant le bilan");
            BancCapture.Call(window, "OnChar", session.GetNextExpectedCharacter()!.Value);
        }
        Assert.True(BancCapture.Field<bool>(window, "_showSummary"));
        Assert.True(BancCapture.Field<LessonTypingSession>(window, "_session").IsExerciseComplete);
    }

    private static LessonModule Defi(LessonCatalog catalog) => catalog.Modules.Single(m => m.Id == DailyChallenge.ModuleId);

    /// <summary>Ce que l'utilisateur peut voir changer : position, page, panneaux, séance
    /// (une nouvelle séance = rejouer), indice, partage, réglages enregistrés.</summary>
    private static string State(LessonsWindow window)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var parts = new List<string>();
        foreach (var field in typeof(LessonsWindow).GetFields(flags))
        {
            string name = field.Name;
            if (name.Contains("hot", StringComparison.OrdinalIgnoreCase) || name.Contains("focus", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("mouse", StringComparison.OrdinalIgnoreCase) || name.Contains("tooltip", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("pressed", StringComparison.OrdinalIgnoreCase) || name.Contains("click", StringComparison.OrdinalIgnoreCase))
                continue;
            var type = field.FieldType;
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || Nullable.GetUnderlyingType(type) is { IsPrimitive: true } ||
                Nullable.GetUnderlyingType(type) is { IsEnum: true })
                parts.Add($"{name}={field.GetValue(window)}");
        }
        parts.Add($"session={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(BancCapture.Field<object>(window, "_session"))}");
        var challenge = BancCapture.Field<ChallengeSessionState>(window, "_challenge");
        parts.Add($"share={challenge.ShareCopied}/{challenge.ShareCopyFailed}");
        parts.Add($"config={ConfigManager.LessonSummaryVisible}/{ConfigManager.LessonAutoHintsEnabled}");
        return string.Join(";", parts);
    }

    [Theory]
    [MemberData(nameof(Ends))]
    public void EveryDrawnTargetOfTheSummary_Acts(string end)
    {
        using var isolation = new BancCapture.Isolation();
        var probe = Open();
        int count;
        List<string> names;
        try
        {
            Reach(probe, end);
            count = BancCapture.Field<List<(Win32.RECT Rect, Action Action)>>(probe, "_clickActions").Count;
            names = BancCapture.Field<List<LessonControlView>>(probe, "_controlViews").Select(c => c.Name).ToList();
        }
        finally { BancCapture.Teardown(probe); }
        Assert.True(count > 0);

        var dead = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var window = Open();
            try
            {
                Reach(window, end);
                // Écriture du presse-papiers remplacée : le test ne dépend plus du presse-papiers réel
                // de la machine (OpenClipboard refusé dans un bac à sable ou une session distante).
                BancCapture.Field<ChallengeSessionState>(window, "_challenge").ClipboardWriter = (_, _) => true;
                string before = State(window);
                BancCapture.Field<List<(Win32.RECT Rect, Action Action)>>(window, "_clickActions")[i].Action();
                if (State(window) == before)
                    dead.Add(i < names.Count ? names[i] : $"#{i}");
            }
            finally { BancCapture.Teardown(window); }
        }

        Assert.True(dead.Count == 0, $"{end} : cibles mortes au bilan : {string.Join(" | ", dead)} (sur {string.Join(" | ", names)})");
    }

    /// <summary>QCM 09/11 : fin du Défi = « Terminer » vers Aujourd'hui, « Rejouer », et
    /// « Partager » quand la séance a un texte à partager (hors prise en main).</summary>
    [Theory]
    [InlineData("defi-final", false)]
    [InlineData("defi-final-partage", true)]
    public void EndOfChallenge_SaysFinish_AndGoesToToday(string end, bool shares)
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            Reach(window, end);
            var names = BancCapture.Field<List<LessonControlView>>(window, "_controlViews").Select(c => c.Name).ToList();
            Assert.Contains(L.LessonsHome_Finish, names);
            Assert.Contains(L.LessonsWin_Replay, names);
            Assert.Equal(shares, names.Contains(L.Challenge_ShareButton));
            Assert.DoesNotContain(L.LessonsWin_IconNext, names);

            BancCapture.Call(window, "NextExercise");
            Assert.Equal("Today", BancCapture.Field<object>(window, "_homePage").ToString());
            Assert.False(BancCapture.Field<bool>(window, "_showSummary"));
        }
        finally { BancCapture.Teardown(window); }
    }

    /// <summary>L12 : au bilan sans bouton focalisé, Entrée et Espace valent « Suivant ». E4 :
    /// le WM_CHAR de l'Espace n'entre pas dans la séance suivante.</summary>
    [Theory]
    [InlineData(0x0D, '\r')]
    [InlineData(0x20, ' ')]
    public void EnterOrSpace_OnTheSummary_GoNext_WithoutTypingIntoTheNextSession(int vk, char wmChar)
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            Reach(window, "milieu");
            Assert.Equal(-1, BancCapture.Field<int>(window, "_focusedActionIndex"));
            int before = BancCapture.Field<int>(window, "_exerciseIndex");

            BancCapture.Call(window, "OnKeyDown", vk);
            BancCapture.Call(window, "OnChar", wmChar);

            Assert.False(BancCapture.Field<bool>(window, "_showSummary"));
            Assert.Equal(before + 1, BancCapture.Field<int>(window, "_exerciseIndex"));
            Assert.Equal(0, BancCapture.Field<LessonTypingSession>(window, "_session").Stats.ProductiveKeystrokes);
        }
        finally { BancCapture.Teardown(window); }
    }

    /// <summary>Le WM_CHAR avalé est celui de la touche qui a activé, jamais une frappe suivante.</summary>
    [Fact]
    public void ActivationSpace_IsSwallowedOnce_ThenSpacesTypeAgain()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            Reach(window, "milieu");
            BancCapture.Call(window, "OnKeyDown", 0x20);
            BancCapture.Call(window, "OnChar", ' ');
            Assert.False(BancCapture.Field<bool>(window, "_swallowActivationSpace"));

            var session = BancCapture.Field<LessonTypingSession>(window, "_session");
            char first = session.GetNextExpectedCharacter()!.Value;
            BancCapture.Call(window, "OnKeyDown", 0x41);
            BancCapture.Call(window, "OnChar", first);
            Assert.Equal(1, session.Stats.ProductiveKeystrokes);
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void AdjacentPosition_NeverCrossesIntoOrOutOfTheChallenge()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            BancCapture.Field<ChallengeSessionState>(window, "_challenge").Request();
            BancCapture.Call(window, "RefreshChallengeModuleIfStale");
            var catalog = Catalog(window);
            int defi = catalog.Modules.ToList().FindIndex(m => m.Id == DailyChallenge.ModuleId);
            Assert.True(defi >= 0);
            for (int m = 0; m < catalog.Modules.Count; m++)
                for (int l = 0; l < catalog.Modules[m].Lessons.Count; l++)
                    for (int e = 0; e < catalog.Modules[m].Lessons[l].Exercises.Count; e++)
                        foreach (int step in new[] { -1, 1 })
                        {
                            var to = LessonsWindow.AdjacentPosition(catalog, m, l, e, step);
                            if (to is { } p)
                                Assert.True((p.Module == defi) == (m == defi), $"{m}/{l}/{e} {step:+0;-0} → {p}");
                        }

            int last = Enumerable.Range(0, catalog.Modules.Count).Last(i => i != defi);
            var lastModule = catalog.Modules[last];
            Assert.Null(LessonsWindow.AdjacentPosition(catalog, last, lastModule.Lessons.Count - 1,
                lastModule.Lessons[^1].Exercises.Count - 1, 1));
            Assert.Null(LessonsWindow.AdjacentPosition(catalog, 0, 0, 0, -1));
            // L9 : ‹ franchit la frontière entre deux modules ordinaires.
            Assert.NotNull(LessonsWindow.AdjacentPosition(catalog, 1, 0, 0, -1));
        }
        finally { BancCapture.Teardown(window); }
    }

    [Fact]
    public void Next_NeverEntersTheChallenge_FromTheLastRegularExercise()
    {
        using var isolation = new BancCapture.Isolation();
        var window = Open();
        try
        {
            BancCapture.Field<ChallengeSessionState>(window, "_challenge").Request();
            BancCapture.Call(window, "RefreshChallengeModuleIfStale");
            Reach(window, "fin-du-catalogue");
            BancCapture.Call(window, "NextExercise");
            var catalog = Catalog(window);
            Assert.NotEqual(DailyChallenge.ModuleId, catalog.Modules[BancCapture.Field<int>(window, "_moduleIndex")].Id);
            Assert.Equal("Today", BancCapture.Field<object>(window, "_homePage").ToString());
        }
        finally { BancCapture.Teardown(window); }
    }
}
