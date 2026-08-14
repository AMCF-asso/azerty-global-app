using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests du Défi du jour (v1.2.0) : banque embarquée, sélection déterministe,
/// échauffement dérivé (pipeline inversé), construction du module de leçons et
/// logique de cadence des rappels (décision pure).
/// </summary>
public class DailyChallengeTests
{
    [Fact]
    public void CorpusResource_LoadsWithExtracts()
    {
        Assert.True(DailyChallenge.ExtractCount >= 300,
            $"Banque trop petite : {DailyChallenge.ExtractCount} extraits");
    }

    [Fact]
    public void SessionFor_SameDate_IsDeterministic()
    {
        var date = new DateOnly(2026, 8, 15);
        var a = DailyChallenge.SessionFor(date, 99);
        var b = DailyChallenge.SessionFor(date, 99);
        Assert.NotNull(a);
        Assert.Equal(a!.Extract.Id, b!.Extract.Id);
        Assert.Equal(a.WarmupChars, b.WarmupChars);
    }

    [Fact]
    public void SessionFor_SequencePhase_MatchesStepCategory()
    {
        var date = new DateOnly(2026, 8, 15);
        // Étape 0 = majuscules accentuées : l'extrait doit porter la catégorie 'caps'
        // (le pool caps est non vide dans la banque générée).
        var s = DailyChallenge.SessionFor(date, 0);
        Assert.NotNull(s);
        Assert.True(s!.IsSequencePhase);
        Assert.Contains("caps", s.Extract.Cats);
        // L'échauffement est dérivé de l'extrait : chaque caractère est bien une cible.
        Assert.All(s.WarmupChars, c => Assert.Contains(c, s.Extract.Targets));
    }

    [Fact]
    public void SessionFor_DailyPhase_IsCommonAndFrOrForeign()
    {
        var s = DailyChallenge.SessionFor(new DateOnly(2026, 8, 16), DailyChallenge.SequenceLength);
        Assert.NotNull(s);
        Assert.False(s!.IsSequencePhase);
        Assert.NotEmpty(s.WarmupChars);
    }

    [Fact]
    public void StableIndex_IsBoundedAndVariesByDate()
    {
        int count = 377;
        var seen = new HashSet<int>();
        for (int d = 1; d <= 28; d++)
        {
            int i = DailyChallenge.StableIndex(new DateOnly(2026, 9, d), 0, count);
            Assert.InRange(i, 0, count - 1);
            seen.Add(i);
        }
        Assert.True(seen.Count > 5, "L'index du jour ne varie pas assez d'un jour à l'autre");
    }

    [Fact]
    public void WrapText_RespectsMaxLenAndPreservesWords()
    {
        string text = "Le chat mange la souris dans le jardin de la maison verte.";
        string wrapped = DailyChallenge.WrapText(text, 20);
        foreach (var line in wrapped.Split('\n'))
            Assert.True(line.Length <= 20, $"Ligne trop longue : {line}");
        Assert.Equal(text, wrapped.Replace('\n', ' '));
    }

    [Fact]
    public void BuildModule_ProducesTwoExercisesWithWarmupRepetitions()
    {
        var module = DailyChallenge.BuildModule(new DateOnly(2026, 8, 15), 0);
        Assert.NotNull(module);
        Assert.True(module!.IsSynthetic);
        Assert.Equal(DailyChallenge.ModuleId, module.Id);
        var lesson = Assert.Single(module.Lessons);
        Assert.Equal(2, lesson.Exercises.Count);
        // Échauffement : chaque ligne contient 5 répétitions du même caractère.
        foreach (var line in lesson.Exercises[0].Lines)
        {
            var parts = line.Split(' ');
            Assert.Equal(DailyChallenge.WarmupRepetitions, parts.Length);
            Assert.Single(parts.Distinct());
        }
        Assert.NotEmpty(lesson.Exercises[1].Content);
    }
}

public class TrainingRemindersTests
{
    private static TrainingSignals Base(DateOnly today) => new(
        Enabled: true,
        IgnoredCount: 0,
        SequenceIndex: DailyChallenge.SequenceLength, // séquence finie par défaut
        LastSessionDate: null,
        LastReminderDate: null,
        LastActiveDate: null,
        LastSpecialCharDate: today,                    // caractère enrichi tapé aujourd'hui
        CurrentStreak: 0,
        HelperOpens: 0);

    private static readonly DateOnly Today = new(2026, 8, 15);
    private static readonly DateTime Evening = new(2026, 8, 15, 18, 0, 0);
    private static readonly DateTime Morning = new(2026, 8, 15, 9, 0, 0);

    [Fact]
    public void NoReminder_WhenDisabledOrStopped()
    {
        var s = Base(Today) with { SequenceIndex = 0 };
        Assert.False(TrainingReminders.ShouldRemind(Evening, s with { Enabled = false }, false));
        Assert.False(TrainingReminders.ShouldRemind(Evening, s with { IgnoredCount = TrainingReminders.MaxIgnored }, false));
    }

    [Fact]
    public void NoReminder_BeforeEveningWindow()
    {
        var s = Base(Today) with { SequenceIndex = 0 };
        Assert.False(TrainingReminders.ShouldRemind(Morning, s, false));
        Assert.True(TrainingReminders.ShouldRemind(Evening, s, false));
    }

    [Fact]
    public void NoReminder_WhenReviewPromptShownToday()
    {
        var s = Base(Today) with { SequenceIndex = 0 };
        Assert.False(TrainingReminders.ShouldRemind(Evening, s, reviewPromptShownToday: true));
    }

    [Fact]
    public void NoReminder_TwiceSameDay_OrAfterTodaysSession()
    {
        var s = Base(Today) with { SequenceIndex = 0 };
        Assert.False(TrainingReminders.ShouldRemind(Evening, s with { LastReminderDate = Today }, false));
        Assert.False(TrainingReminders.ShouldRemind(Evening, s with { LastSessionDate = Today }, false));
    }

    [Fact]
    public void Reminds_WhenStreakInDanger()
    {
        var s = Base(Today) with
        {
            CurrentStreak = 4,
            LastActiveDate = Today.AddDays(-1),
            LastSpecialCharDate = Today.AddDays(-1),
        };
        Assert.True(TrainingReminders.ShouldRemind(Evening, s, false));
        // Série d'un seul jour : pas un signal.
        Assert.False(TrainingReminders.ShouldRemind(Evening, s with { CurrentStreak = 1 }, false));
    }

    [Fact]
    public void Reminds_AfterDaysWithoutSpecialChars()
    {
        var s = Base(Today) with { LastSpecialCharDate = Today.AddDays(-TrainingReminders.StaleSpecialCharDays) };
        Assert.True(TrainingReminders.ShouldRemind(Evening, s, false));
        Assert.False(TrainingReminders.ShouldRemind(Evening,
            s with { LastSpecialCharDate = Today.AddDays(-1) }, false));
    }

    [Fact]
    public void Reminds_OnHeavyHelperUse_OnlyBeforeFirstSession()
    {
        var s = Base(Today) with { HelperOpens = TrainingReminders.HelperOpensThreshold };
        Assert.True(TrainingReminders.ShouldRemind(Evening, s, false));
        Assert.False(TrainingReminders.ShouldRemind(Evening,
            s with { LastSessionDate = Today.AddDays(-1) }, false));
    }
}
