using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Texte partageable du défi commun (v1.2.0). Ce texte est destiné à être collé
/// publiquement : ces tests fixent ce qu'il contient, et surtout ce qu'il ne contient pas.
/// </summary>
public class ChallengeShareTests : IDisposable
{
    private static readonly DateOnly Date = new(2026, 8, 16);

    public ChallengeShareTests() => L.Language = "fr";
    public void Dispose() => L.Language = "fr";

    /// <summary>
    /// Séance déterministe : horloge pilotée, donc durée, vitesse et précision stables.
    /// 40 frappes en 30 s = 8 mots normalisés en 0,5 min = 16 mots/min.
    /// </summary>
    private static LessonAttemptStats BuildStats(int errors, params char[] errorChars)
    {
        var start = new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero);
        var now = start;
        var stats = new LessonAttemptStats(() => now);

        int correct = 40 - errors;
        for (int i = 0; i < correct; i++)
            stats.RecordChar('a', 'a');
        for (int i = 0; i < errors; i++)
            stats.RecordChar('x', errorChars[i % errorChars.Length]);

        now = start.AddSeconds(30);
        stats.Complete();
        return stats;
    }

    [Fact]
    public void Build_CarriesDateSpeedAccuracyAndLink()
    {
        string text = ChallengeShare.Build(Date, credit: null, BuildStats(0), isPersonalBest: false);

        Assert.Contains("AZERTY Global", text);
        Assert.Contains("16 août 2026", text);
        Assert.Contains("16 mots/min", text);
        Assert.Contains("30 s", text);
        Assert.Contains("azerty.global", text);
    }

    [Fact]
    public void Build_WithoutErrors_AnnouncesFlawlessRatherThanAnEmptyList()
    {
        string text = ChallengeShare.Build(Date, credit: null, BuildStats(0), isPersonalBest: false);

        Assert.Contains("Sans faute", text);
        Assert.DoesNotContain("résisté", text);
    }

    [Fact]
    public void Build_WithErrors_ListsTheHardestCharacters()
    {
        // 'œ' fauté trois fois, '«' une seule : l'ordre suit le nombre d'erreurs.
        string text = ChallengeShare.Build(Date, credit: null,
            BuildStats(4, 'œ', 'œ', 'œ', '«'), isPersonalBest: false);

        Assert.Contains("résisté", text);
        Assert.Contains("œ", text);
        Assert.DoesNotContain("Sans faute", text);
    }

    [Fact]
    public void Build_MentionsThePersonalBestOnlyWhenThereIsOne()
    {
        Assert.DoesNotContain("record",
            ChallengeShare.Build(Date, null, BuildStats(0), isPersonalBest: false));
        Assert.Contains("record",
            ChallengeShare.Build(Date, null, BuildStats(0), isPersonalBest: true));
    }

    [Fact]
    public void Build_CreditsTheSourceWhenTheExtractCarriesOne()
    {
        string credited = ChallengeShare.Build(Date, "Victor Hugo", BuildStats(0), false);
        Assert.Contains("Victor Hugo", credited);

        // Extraits CC0 et maison : aucune ligne d'attribution vide.
        string plain = ChallengeShare.Build(Date, null, BuildStats(0), false);
        Assert.DoesNotContain("Extrait", plain);
    }

    /// <summary>
    /// Décision du 2026-08-16 : les chiffres d'usage de l'application restent dans la
    /// fenêtre « Mes statistiques ». Le texte partagé ne porte que la performance de la
    /// séance — un jour d'utilisation ou une série n'a rien à faire dans un salon public.
    /// </summary>
    [Fact]
    public void Build_CarriesNoUsageStatistics()
    {
        string text = ChallengeShare.Build(Date, "Victor Hugo", BuildStats(2, 'é'), true);

        Assert.DoesNotContain("série", text);
        Assert.DoesNotContain("jours", text);
        Assert.DoesNotContain("caractères spéciaux", text);
    }

    [Fact]
    public void Build_FollowsTheApplicationLanguage()
    {
        L.Language = "en";
        string text = ChallengeShare.Build(Date, null, BuildStats(0), isPersonalBest: false);

        Assert.Contains("Challenge for", text);
        Assert.Contains("wpm", text);
        Assert.DoesNotContain("mots/min", text);
    }

    /// <summary>
    /// Le texte part au presse-papiers Windows, où un LF seul se colle sur une seule ligne
    /// dans plusieurs applications.
    /// </summary>
    [Fact]
    public void Build_SeparatesLinesWithCrLf()
    {
        string text = ChallengeShare.Build(Date, null, BuildStats(0), isPersonalBest: false);

        Assert.Contains("\r\n", text);
        Assert.DoesNotContain("\n\n", text);
        Assert.Equal(text.Split("\r\n").Length - 1, text.Count(c => c == '\n'));
    }
}
