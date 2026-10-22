using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tolérance typographique (audit 1.3.0, B1, A24, A25 ; décision d'Antoine du 20/10) : dans les
/// exercices de découverte, l'espace ordinaire passe pour une insécable et l'apostrophe droite
/// pour la typographique ; les leçons du catalogue restent strictes.
/// </summary>
public class TypingToleranceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(' ', ' ')]
    [InlineData(' ', ' ')]
    [InlineData(' ', ' ')]
    [InlineData(' ', ' ')]
    [InlineData('\'', '’')]
    [InlineData('a', 'a')]
    public void Les_espaces_et_l_apostrophe_droite_sont_acceptees(char typed, char expected)
    {
        Assert.True(TypingTolerance.Accepts(typed, expected));
    }

    [Theory]
    [InlineData('m', ' ')]
    [InlineData('’', '\'')]
    [InlineData(' ', ' ')]
    [InlineData(' ', ' ')]
    [InlineData('`', '’')]
    [InlineData('-', '—')]
    public void Le_reste_demeure_une_faute(char typed, char expected)
    {
        Assert.False(TypingTolerance.Accepts(typed, expected));
    }

    [Fact]
    public void Seuls_l_Initiation_le_Defi_et_Votre_texte_sont_toleres()
    {
        var catalog = LessonCatalogLoader.LoadFromResource();
        Assert.All(catalog.Modules[0].Lessons.SelectMany(l => l.Exercises),
            exercise => Assert.True(TypingTolerance.AppliesTo(exercise)));
        Assert.All(catalog.Modules.Skip(1).SelectMany(m => m.Lessons).SelectMany(l => l.Exercises),
            exercise => Assert.False(TypingTolerance.AppliesTo(exercise)));
        Assert.True(TypingTolerance.AppliesTo(Exercise(DailyChallenge.ModuleId, "x", LessonTypingMode.Flexible)));
        Assert.True(TypingTolerance.AppliesTo(Exercise(LessonCatalogLoader.CustomTextModuleId, "x", LessonTypingMode.Flexible)));
        Assert.False(TypingTolerance.AppliesTo(Exercise(LessonCatalogLoader.ReviewModuleId, "x", LessonTypingMode.Flexible)));
    }

    [Fact]
    public void L_exercice_2_de_l_Initiation_se_tape_avec_une_espace_ordinaire()
    {
        var exercise = InitiationExercise(1);
        var session = new LessonTypingSession(exercise, () => T0);

        LessonInputResult last = default;
        foreach (char c in "ÇA GÈLE À MONTRÉAL, PAS À ABIDJAN !")
        {
            last = session.TypeChar(c);
            Assert.False(last.WasError, $"faute sur « {c} »");
        }

        Assert.True(last.ExerciseCompleted);
        Assert.Equal(100, session.Stats.AccuracyPercent);
        Assert.Equal(0, session.Stats.ErrorCount);
    }

    [Fact]
    public void L_exercice_Documents_accepte_espaces_ordinaires_et_apostrophe_droite()
    {
        var session = new LessonTypingSession(InitiationExercise(TutorialSteps.Documents), () => T0);

        LessonInputResult last = default;
        foreach (char c in "« Un chef-d'œuvre ! » — Maman")
        {
            last = session.TypeChar(c);
            Assert.False(last.WasError, $"faute sur « {c} »");
        }

        Assert.True(last.ExerciseCompleted);
        Assert.Equal(100, session.Stats.AccuracyPercent);
    }

    [Fact]
    public void Une_vraie_faute_reste_une_faute_dans_l_Initiation()
    {
        var session = new LessonTypingSession(InitiationExercise(1), () => T0);
        foreach (char c in "ÇA GÈLE À MONTRÉAL, PAS À ABIDJAN")
            session.TypeChar(c);

        var result = session.TypeChar('m');

        Assert.True(result.WasError);
        Assert.Equal(1, session.Stats.ErrorCount);
    }

    [Fact]
    public void Une_lecon_de_typographie_reste_stricte()
    {
        var catalog = LessonCatalogLoader.LoadFromResource();
        var typo = catalog.Modules.Single(m => m.Id == "typographie-francaise")
            .Lessons.SelectMany(l => l.Exercises)
            .First(e => e.Content.Contains(' ') || e.Content.Contains(' '));
        var session = new LessonTypingSession(typo, () => T0);
        string line = session.CurrentLine;
        int space = line.IndexOfAny(new[] { ' ', ' ' });
        Assert.True(space >= 0, "l'exercice choisi commence par une ligne sans insécable");

        LessonInputResult result = default;
        for (int i = 0; i < space; i++)
            result = session.TypeChar(line[i]);
        result = session.TypeChar(' ');

        Assert.True(result.WasError);
    }

    [Fact]
    public void Le_Defi_se_termine_avec_une_espace_ordinaire_en_mode_souple()
    {
        var exercise = Exercise(DailyChallenge.ModuleId, "L’été !", LessonTypingMode.Flexible);
        var session = new LessonTypingSession(exercise, () => T0);

        LessonInputResult last = default;
        foreach (char c in "L'été !")
            last = session.TypeChar(c);

        Assert.True(last.ExerciseCompleted);
        Assert.False(last.WasError);
        Assert.Equal(100, session.Stats.AccuracyPercent);
    }

    [Fact]
    public void Un_retour_arriere_apres_une_frappe_toleree_la_garde_juste()
    {
        var exercise = Exercise(LessonCatalogLoader.CustomTextModuleId, "a !b", LessonTypingMode.Flexible);
        var session = new LessonTypingSession(exercise, () => T0);
        session.TypeChar('a');
        session.TypeChar(' ');
        session.TypeChar('x');

        Assert.True(session.NeedsBackspaceCorrection);
        session.Backspace();

        Assert.False(session.NeedsBackspaceCorrection);
        Assert.All(session.GetCurrentLineSnapshot().Take(2),
            cell => Assert.Equal(LessonCharacterState.Correct, cell.State));
        Assert.Equal(' ', session.GetTypedLineSnapshot()[1].Actual);
    }

    [Fact]
    public void Votre_texte_colle_avec_insecables_et_invisibles_se_tape_au_clavier()
    {
        var result = LessonCustomText.Build("« Oui ! » c’est­à​dire", "Tape", _ => true);
        Assert.NotNull(result.Exercise);
        Assert.Empty(result.Removed);
        Assert.Equal("« Oui ! » c’estàdire", result.Exercise!.Content);

        var session = new LessonTypingSession(result.Exercise, () => T0);
        LessonInputResult last = default;
        foreach (char c in "« Oui ! » c'estàdire")
            last = session.TypeChar(c);

        Assert.True(last.ExerciseCompleted);
        Assert.False(last.WasError);
    }

    /// <summary>Une frappe tolérée ne coûte rien à la précision, mais le geste attendu compte
    /// une faute : il revient échu dans la Révision du jour (option A du plan de l'audit).</summary>
    [Fact]
    public void Le_geste_tolere_revient_echu_dans_la_revision()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ag-tolerance-{Guid.NewGuid():N}.json");
        try
        {
            var exercise = InitiationExercise(1);
            var session = new LessonTypingSession(exercise, () => T0);
            foreach (char c in "ÇA GÈLE À MONTRÉAL, PAS À ABIDJAN !")
                session.TypeChar(c);
            Assert.Equal(100, session.Stats.AccuracyPercent);
            Assert.Contains(' ', session.Stats.GetHardestCharacters(3));

            var store = new LessonProgressStore(path, () => T0);
            store.RecordSuccess(exercise, session.Stats, c => c is ' ' or 'Ç');

            var fine = store.Gestures[" "];
            Assert.Equal(1, fine.Errors);
            Assert.Equal(1, fine.Box);
            Assert.Contains(store.DueGestures(8), entry => entry.Character == " ");
            Assert.DoesNotContain(store.DueGestures(8), entry => entry.Character == "Ç");

            var catalog = LessonCatalogLoader.LoadFromResource();
            var review = LessonsHomeLogic.BuildReview(catalog, store.DueGestures(8), "Révise", seed: 1);
            Assert.Contains(" ", review.Characters);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static LessonExercise InitiationExercise(int index) =>
        LessonCatalogLoader.LoadFromResource().Modules[0].Lessons[0].Exercises[index];

    private static LessonExercise Exercise(string module, string content, LessonTypingMode mode) =>
        new(module, "l", 0, "practice", "Tape", content, mode);
}
