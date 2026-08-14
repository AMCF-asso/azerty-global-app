using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests des statistiques d'usage 100 % locales (v1.1) : catégorisation des caractères,
/// logique de streak/jours actifs (nouveau jour, jour consécutif, rupture de série),
/// tolérance à la corruption du fichier, et persistance via Flush.
/// </summary>
public class UsageStatsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _statsPath;

    public UsageStatsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _statsPath = Path.Combine(_tempDir, "usage-stats.json");
        UsageStats.OverrideStatsPathForTests(_statsPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static string Today() => DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
    private static string DaysAgo(int days) => DateOnly.FromDateTime(DateTime.Now).AddDays(-days).ToString("yyyy-MM-dd");

    private void SeedFile(string firstRemapDate, string lastActiveDate, int activeDays, int currentStreak, int bestStreak)
    {
        File.WriteAllText(_statsPath,
            $$"""
            {
              "firstRemapDate": "{{firstRemapDate}}",
              "lastActiveDate": "{{lastActiveDate}}",
              "activeDaysCount": {{activeDays}},
              "currentStreak": {{currentStreak}},
              "bestStreak": {{bestStreak}},
              "accentedUppercaseCount": 0,
              "frenchTypographyCount": 0,
              "internationalCount": 0,
              "symbolsCount": 0
            }
            """);
        UsageStats.OverrideStatsPathForTests(_statsPath); // force le rechargement depuis le disque
    }

    [Fact]
    public void RecordEmittedText_FirstSpecialChar_SetsFirstRemapDateAndCounts()
    {
        UsageStats.RecordEmittedText("É");

        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), UsageStats.FirstRemapDate);
        Assert.Equal(1, UsageStats.ActiveDaysCount);
        Assert.Equal(1, UsageStats.CurrentStreak);
        Assert.Equal(1, UsageStats.BestStreak);
        Assert.Equal(1, UsageStats.AccentedUppercaseCount);
    }

    [Fact]
    public void RecordEmittedText_PlainAsciiOnly_RecordsActivityButNoSpecialChars()
    {
        // Toute émission remappée compte comme jour d'utilisation (plan § 1a),
        // même sans caractère spécial (ex. point en accès direct).
        UsageStats.RecordEmittedText(".");

        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), UsageStats.FirstRemapDate);
        Assert.Equal(1, UsageStats.ActiveDaysCount);
        Assert.Equal(0, UsageStats.TotalSpecialCharsCount);
    }

    [Fact]
    public void RecordEmittedText_SameDayTwice_DoesNotDoubleCountActiveDays()
    {
        UsageStats.RecordEmittedText("É");
        UsageStats.RecordEmittedText("È");

        Assert.Equal(1, UsageStats.ActiveDaysCount);
        Assert.Equal(1, UsageStats.CurrentStreak);
        Assert.Equal(2, UsageStats.AccentedUppercaseCount);
    }

    [Fact]
    public void RecordEmittedText_ConsecutiveDay_IncrementsStreak()
    {
        SeedFile(DaysAgo(3), DaysAgo(1), activeDays: 3, currentStreak: 3, bestStreak: 3);

        UsageStats.RecordEmittedText("É");

        Assert.Equal(4, UsageStats.ActiveDaysCount);
        Assert.Equal(4, UsageStats.CurrentStreak);
        Assert.Equal(4, UsageStats.BestStreak);
    }

    [Fact]
    public void RecordEmittedText_GapInDays_ResetsStreakToOneButKeepsBest()
    {
        SeedFile(DaysAgo(20), DaysAgo(5), activeDays: 10, currentStreak: 10, bestStreak: 10);

        UsageStats.RecordEmittedText("É");

        Assert.Equal(11, UsageStats.ActiveDaysCount);
        Assert.Equal(1, UsageStats.CurrentStreak);
        Assert.Equal(10, UsageStats.BestStreak); // le record n'est jamais réduit
    }

    [Fact]
    public void Categorization_FourCategories_CountedSeparately()
    {
        // É → majuscule accentuée ; œ « » ’ — → typographie française ;
        // ñ ¿ → international ; © ¥ → symboles (absents de l'AZERTY trad).
        UsageStats.RecordEmittedText("É œ « » ’ — ñ ¿ © ¥");

        Assert.Equal(1, UsageStats.AccentedUppercaseCount);
        Assert.Equal(5, UsageStats.FrenchTypographyCount);
        Assert.Equal(2, UsageStats.InternationalCount);
        Assert.Equal(2, UsageStats.SymbolsCount);
        Assert.Equal(10, UsageStats.TotalSpecialCharsCount);
    }

    [Fact]
    public void Categorization_TradAzertySymbols_NotCounted()
    {
        // € £ µ § ° ² ¤ ¨ sont gravés sur l'AZERTY Windows traditionnel → non comptés
        // (même règle que les minuscules accentuées) ; l'activité du jour reste marquée.
        UsageStats.RecordEmittedText("€£µ§°²¤¨");

        Assert.Equal(1, UsageStats.ActiveDaysCount);
        Assert.Equal(0, UsageStats.TotalSpecialCharsCount);
    }

    [Fact]
    public void Categorization_FrenchLowercaseAccented_NotCounted()
    {
        // Règle produit : à/ê/ç/ü… minuscules sont déjà accessibles sur l'AZERTY Windows
        // traditionnel (touches directes ou mortes natives) → aucune catégorie comptée,
        // mais l'émission marque quand même l'activité du jour.
        UsageStats.RecordEmittedText("être à hôtel çà capharnaüm aïeul déjà ÿ");

        Assert.Equal(1, UsageStats.ActiveDaysCount);
        Assert.Equal(0, UsageStats.TotalSpecialCharsCount);
    }

    [Fact]
    public void Categorization_NonFrenchLetters_CountAsInternational()
    {
        // ä/ö ne sont pas françaises (contrairement à ë/ï/ü) ; α grec et ª (lettre pour
        // Unicode) tombent aussi dans les caractères internationaux.
        UsageStats.RecordEmittedText("äöαª");

        Assert.Equal(4, UsageStats.InternationalCount);
        Assert.Equal(0, UsageStats.AccentedUppercaseCount);
        Assert.Equal(0, UsageStats.SymbolsCount);
    }

    [Fact]
    public void Categorization_NonBreakingSpaces_CountAsFrenchTypography()
    {
        // Espace insécable (U+00A0) + espace fine insécable (U+202F).
        UsageStats.RecordEmittedText("\u00A0\u202F");

        Assert.Equal(2, UsageStats.FrenchTypographyCount);
    }

    [Fact]
    public void EnsureLoaded_CorruptFile_ResetsWithoutThrowing()
    {
        File.WriteAllText(_statsPath, "{ ceci n'est pas du JSON valide");
        UsageStats.OverrideStatsPathForTests(_statsPath);

        var ex = Record.Exception(() => UsageStats.ActiveDaysCount);

        Assert.Null(ex);
        Assert.Equal(0, UsageStats.ActiveDaysCount);
        Assert.Null(UsageStats.FirstRemapDate);
    }

    [Fact]
    public void EnsureLoaded_ValidJsonButNonObjectRoot_ResetsWithoutThrowing()
    {
        // JSON valide mais racine non-objet : TryGetProperty jetterait
        // InvalidOperationException (hors filtre) sans le garde ValueKind.
        File.WriteAllText(_statsPath, "[]");
        UsageStats.OverrideStatsPathForTests(_statsPath);

        var ex = Record.Exception(() => UsageStats.ActiveDaysCount);

        Assert.Null(ex);
        Assert.Equal(0, UsageStats.ActiveDaysCount);
        Assert.Null(UsageStats.FirstRemapDate);
    }

    [Fact]
    public void CurrentStreak_BrokenStreak_ReadsAsZeroBeforeNextKeystroke()
    {
        // Dernière activité il y a 5 jours : la série est rompue et doit se lire 0
        // dès l'affichage, sans attendre la prochaine frappe remappée.
        SeedFile(DaysAgo(20), DaysAgo(5), activeDays: 10, currentStreak: 10, bestStreak: 10);

        Assert.Equal(0, UsageStats.CurrentStreak);
        Assert.Equal(10, UsageStats.BestStreak);

        UsageStats.RecordEmittedText("É");
        Assert.Equal(1, UsageStats.CurrentStreak);
    }

    [Fact]
    public void CurrentStreak_ActiveYesterday_StillReadsAsCurrent()
    {
        SeedFile(DaysAgo(3), DaysAgo(1), activeDays: 3, currentStreak: 3, bestStreak: 3);

        Assert.Equal(3, UsageStats.CurrentStreak);
    }

    [Fact]
    public void Flush_PersistsDirtyState_SurvivesReload()
    {
        UsageStats.RecordEmittedText("É É Ç");
        UsageStats.Flush();

        Assert.True(File.Exists(_statsPath));

        // Simuler un redémarrage : vider le cache et relire depuis le disque
        UsageStats.OverrideStatsPathForTests(_statsPath);

        Assert.Equal(3, UsageStats.AccentedUppercaseCount);
        Assert.Equal(1, UsageStats.ActiveDaysCount);
    }

    [Fact]
    public void Flush_NoEmissionAtAll_DoesNotCreateFile()
    {
        // Aucune frappe remappée → rien à persister, pas de fichier créé.
        UsageStats.Flush();

        Assert.False(File.Exists(_statsPath));
    }

    [Fact]
    public void Flush_WriteFailure_RetriesWithoutRequiringAnotherKeystroke()
    {
        var blockedParent = Path.Combine(_tempDir, "blocked-parent");
        File.WriteAllText(blockedParent, "not a directory");
        var retryPath = Path.Combine(blockedParent, "usage-stats.json");
        UsageStats.OverrideStatsPathForTests(retryPath);
        UsageStats.RecordEmittedText("É");

        UsageStats.Flush(); // échoue : le parent est un fichier

        File.Delete(blockedParent);
        Directory.CreateDirectory(blockedParent);
        UsageStats.Flush(); // doit retenter le même état resté dirty

        Assert.True(File.Exists(retryPath));
        UsageStats.OverrideStatsPathForTests(retryPath);
        Assert.Equal(1, UsageStats.AccentedUppercaseCount);
    }

    [Fact]
    public void RecordActivity_PartialFileWithoutFirstRemapDate_BackfillsIt()
    {
        // Fichier partiel (lastActiveDate présent, firstRemapDate absent) : la date de
        // première frappe doit être re-posée sans casser le comptage du jour.
        File.WriteAllText(_statsPath,
            $$"""{ "lastActiveDate": "{{Today()}}", "activeDaysCount": 1, "currentStreak": 1, "bestStreak": 1 }""");
        UsageStats.OverrideStatsPathForTests(_statsPath);

        UsageStats.RecordEmittedText("É");

        Assert.NotNull(UsageStats.FirstRemapDate);
        Assert.Equal(1, UsageStats.ActiveDaysCount); // pas de double comptage du jour
    }

    [Fact]
    public void RecordEmittedText_SameMinute_CountsOneActiveMinute()
    {
        UsageStats.RecordEmittedText("É");
        UsageStats.RecordEmittedText("È");
        UsageStats.RecordEmittedText(".");

        // Trois émissions dans la même minute → une seule minute active.
        // (Fenêtre de tolérance : le test peut chevaucher un changement de minute.)
        Assert.InRange(UsageStats.TotalActiveMinutes, 1, 2);
    }

    [Fact]
    public void TotalActiveMinutes_SurvivesFlushAndReload()
    {
        UsageStats.RecordEmittedText("É");
        UsageStats.Flush();

        UsageStats.OverrideStatsPathForTests(_statsPath); // simule un redémarrage

        Assert.True(UsageStats.TotalActiveMinutes >= 1);
    }

    [Theory]
    [InlineData(0, "0 min")]
    [InlineData(45, "45 min")]
    [InlineData(60, "1 h 00")]
    [InlineData(185, "3 h 05")]
    [InlineData(6000, "100 h")]
    public void FormatActiveTime_FormatsAsExpected(long minutes, string expected)
    {
        Assert.Equal(expected, UsageStats.FormatActiveTime(minutes));
    }

    [Fact]
    public void FormatActiveTime_English_UsesHrMin()
    {
        L.Language = "en";
        try
        {
            Assert.Equal("45 min", UsageStats.FormatActiveTime(45));
            Assert.Equal("3 hr 05 min", UsageStats.FormatActiveTime(185));
            Assert.Equal("100 hr", UsageStats.FormatActiveTime(6000));
        }
        finally { L.Language = "fr"; }
    }

    [Fact]
    public void BuildShareText_English_DoesNotDuplicateTyped()
    {
        L.Language = "en";
        try
        {
            UsageStats.RecordEmittedText("É « » œ");

            var text = UsageStats.BuildShareText();

            Assert.Contains("special character", text);
            Assert.DoesNotContain("typed typed", text);
        }
        finally { L.Language = "fr"; }
    }

    [Fact]
    public void BuildShareText_NoActivity_ReturnsJustStartedMessage()
    {
        var text = UsageStats.BuildShareText();

        Assert.Contains("commencer", text);
    }

    [Fact]
    public void BuildShareText_WithActivity_ContainsCountsAndDate()
    {
        UsageStats.RecordEmittedText("É « » œ");

        var text = UsageStats.BuildShareText();

        Assert.Contains("AZERTY Global", text);
        Assert.Contains("jour", text);
        Assert.DoesNotContain("Je viens tout juste", text);
    }
}
