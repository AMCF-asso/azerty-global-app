using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>Progression par geste (refonte 1.3.0) : boîtes, stockage, séance de révision.</summary>
public class GestureMasteryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Apply_CleanSuccessWhenDue_MovesUpAndSchedules()
    {
        var entry = new GestureMasteryEntry("é");

        GestureMastery.Apply(entry, attempts: 3, errors: 0, T0);

        Assert.Equal(1, entry.Box);
        Assert.Equal(T0 + GestureMastery.IntervalFor(1), entry.DueUtc);
        Assert.False(entry.IsDue(T0.AddHours(1)));
    }

    [Fact]
    public void Apply_CleanSuccessBeforeDue_CountsWithoutMovingUp()
    {
        var entry = new GestureMasteryEntry("é");
        GestureMastery.Apply(entry, 3, 0, T0);

        GestureMastery.Apply(entry, 2, 0, T0.AddHours(2));

        Assert.Equal(1, entry.Box);
        Assert.Equal(5, entry.Attempts);
    }

    [Fact]
    public void Apply_Error_SendsBackToBoxOneAndDueNow()
    {
        var entry = new GestureMasteryEntry("œ") { Box = 4, DueUtc = T0.AddDays(-1) };

        GestureMastery.Apply(entry, 4, 1, T0);

        Assert.Equal(1, entry.Box);
        Assert.True(entry.IsDue(T0));
        Assert.Equal(1, entry.Errors);
    }

    [Fact]
    public void Apply_BoxIsCappedAtMax()
    {
        var entry = new GestureMasteryEntry("ŋ") { Box = GestureMastery.MaxBox };

        GestureMastery.Apply(entry, 1, 0, T0);

        Assert.Equal(GestureMastery.MaxBox, entry.Box);
        Assert.Equal(1.0, entry.Level);
    }

    [Fact]
    public void Due_OrdersByBoxThenErrorRate()
    {
        var entries = new[]
        {
            new GestureMasteryEntry("a") { Box = 2, DueUtc = T0.AddDays(-1) },
            new GestureMasteryEntry("b") { Box = 1, Attempts = 10, Errors = 1, DueUtc = T0 },
            new GestureMasteryEntry("c") { Box = 1, Attempts = 10, Errors = 5, DueUtc = T0 },
            new GestureMasteryEntry("d") { Box = 0, DueUtc = T0.AddDays(2) }, // pas échu
        };

        var due = GestureMastery.Due(entries, T0, 10).Select(e => e.Character).ToArray();

        Assert.Equal(new[] { "c", "b", "a" }, due);
    }

    [Fact]
    public void IsTracked_IgnoresOwnKeyAndSpace_KeepsOtherGestures()
    {
        var plainE = new[] { new GestureStep(false, false, "KeyE", "E") };
        var space = new[] { new GestureStep(false, false, "Space", "Espace") };
        var digitOne = new[] { new GestureStep(true, false, "Digit1", "&") };
        var altGr = new[] { new GestureStep(false, true, "KeyE", "E") };

        Assert.False(GestureMastery.IsTracked("e", plainE));
        Assert.False(GestureMastery.IsTracked("E", plainE));
        Assert.False(GestureMastery.IsTracked(" ", space));
        Assert.True(GestureMastery.IsTracked("1", digitOne));
        Assert.True(GestureMastery.IsTracked("€", altGr));
        Assert.False(GestureMastery.IsTracked("?", Array.Empty<GestureStep>()));
    }

    [Fact]
    public void Store_RecordsTrackedCharactersAndRoundTrips()
    {
        string path = TempPath();
        try
        {
            var now = T0;
            var exercise = new LessonExercise("m", "l", 0, "practice", "Tape", "aéb", LessonTypingMode.Flexible);
            var session = new LessonTypingSession(exercise, () => now);
            session.TypeChar('a');
            session.TypeChar('e'); // faute sur « é »
            session.Backspace();
            session.TypeChar('é');
            session.TypeChar('b');

            var store = new LessonProgressStore(path, () => now);
            store.RecordSuccess(exercise, session.Stats, c => c == 'é');

            var reloaded = new LessonProgressStore(path, () => now);
            Assert.True(reloaded.IsCompleted(exercise));
            var entry = Assert.Single(reloaded.Gestures).Value;
            Assert.Equal("é", entry.Character);
            Assert.Equal(2, entry.Attempts);
            Assert.Equal(1, entry.Errors);
            Assert.Equal(1, entry.Box);
            Assert.Equal(now, entry.DueUtc);
            Assert.Equal("é", Assert.Single(reloaded.DueGestures(5)).Character);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Store_ReviewExercise_UpdatesGesturesWithoutExerciseEntry()
    {
        string path = TempPath();
        try
        {
            var review = new LessonExercise(LessonCatalogLoader.ReviewModuleId, "seance", 0, "practice", "Révision", "é", LessonTypingMode.Flexible);
            var session = new LessonTypingSession(review, () => T0);
            session.TypeChar('é');

            var store = new LessonProgressStore(path, () => T0);
            store.RecordSuccess(review, session.Stats, c => c == 'é');

            var reloaded = new LessonProgressStore(path, () => T0);
            Assert.Empty(reloaded.Exercises);
            Assert.Equal(1, reloaded.Gestures["é"].Box);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>Tutoriel d'accueil (décision du 07/10) : ses frappes nourrissent les gestes,
    /// faute comprise, sans créer d'entrée d'exercice.</summary>
    [Fact]
    public void Store_TutorialGestures_UpdateGesturesWithoutExerciseEntry()
    {
        string path = TempPath();
        try
        {
            var stats = new LessonAttemptStats(() => T0);
            stats.RecordChar('ŋ', 'ŋ');
            stats.RecordChar('n', 'ɓ');
            stats.RecordChar('ɓ', 'ɓ');
            stats.RecordChar('a', 'a');

            new LessonProgressStore(path, () => T0).RecordGestures(stats, c => c is 'ŋ' or 'ɓ');

            var reloaded = new LessonProgressStore(path, () => T0);
            Assert.Empty(reloaded.Exercises);
            Assert.Equal(new[] { "ŋ", "ɓ" }, reloaded.Gestures.Keys.OrderBy(k => k == "ɓ"));
            Assert.Equal(1, reloaded.Gestures["ŋ"].Box);
            Assert.Equal(1, reloaded.Gestures["ɓ"].Errors);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Store_ResetAll_ClearsGestures()
    {
        string path = TempPath();
        try
        {
            var exercise = new LessonExercise("m", "l", 0, "practice", "Tape", "é", LessonTypingMode.Flexible);
            var session = new LessonTypingSession(exercise, () => T0);
            session.TypeChar('é');
            var store = new LessonProgressStore(path, () => T0);
            store.RecordSuccess(exercise, session.Stats, c => c == 'é');

            store.ResetAll(onboardingSyncedMaxStep: 0);

            Assert.Empty(new LessonProgressStore(path).Gestures);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ReviewBuilder_RepeatsEachCharacterAndIsStablePerSeed()
    {
        var catalog = LessonCatalogLoader.LoadFromResource();
        var characters = new[] { "é", "@" };

        var first = LessonReviewBuilder.Build(catalog, characters, "Révision", seed: 20261007);
        var again = LessonReviewBuilder.Build(catalog, characters, "Révision", seed: 20261007);

        Assert.NotNull(first);
        Assert.Equal(first!.Content, again!.Content);
        Assert.Equal(LessonCatalogLoader.ReviewModuleId, first.ModuleId);
        Assert.InRange(first.Lines.Length, 1, LessonReviewBuilder.MaxLines);
        Assert.All(first.Lines, line => Assert.InRange(line.Length, 1, LessonReviewBuilder.TargetLineLength));
        foreach (string character in characters)
            Assert.True(first.Content.Split(character).Length - 1 >= 1, $"« {character} » absent de la séance");
    }

    [Fact]
    public void ReviewBuilder_NothingToReview_ReturnsNull()
    {
        var catalog = LessonCatalogLoader.LoadFromResource();

        Assert.Null(LessonReviewBuilder.Build(catalog, Array.Empty<string>(), "Révision", 1));
        Assert.Null(LessonReviewBuilder.Build(catalog, new[] { "☃" }, "Révision", 1));
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "azerty-gestures-test-" + Guid.NewGuid() + ".json");
}
