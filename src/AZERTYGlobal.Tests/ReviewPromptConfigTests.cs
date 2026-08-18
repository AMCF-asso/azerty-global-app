using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests des clés de la sollicitation d'avis : horloge du premier lancement (enregistrée
/// au premier accès, stable ensuite, tolérante à la corruption), compteur d'essais
/// plafonné à deux, date du dernier essai, drapeau de clic, et migration des
/// installations v1.1 qui ne connaissent que le booléen reviewPromptDone.
/// </summary>
public class ReviewPromptConfigTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public ReviewPromptConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
        ConfigManager.OverrideConfigPathForTests(_configPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void EnsureFirstRunTimestamp_FirstCall_RecordsNowAndPersists()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-2);
        var stamp = ConfigManager.EnsureFirstRunTimestamp();
        var after = DateTimeOffset.UtcNow.AddSeconds(2);

        Assert.InRange(stamp, before, after);
        Assert.True(File.Exists(_configPath));
        Assert.Contains("firstRunTimestamp", File.ReadAllText(_configPath));
    }

    [Fact]
    public void EnsureFirstRunTimestamp_SecondCall_ReturnsSameValue()
    {
        var first = ConfigManager.EnsureFirstRunTimestamp();
        var second = ConfigManager.EnsureFirstRunTimestamp();
        Assert.Equal(first, second);
    }

    [Fact]
    public void EnsureFirstRunTimestamp_ExistingValue_SurvivesReload()
    {
        var first = ConfigManager.EnsureFirstRunTimestamp();

        // Simuler un redémarrage de l'app : vider le cache et relire depuis le disque
        ConfigManager.OverrideConfigPathForTests(_configPath);
        var reloaded = ConfigManager.EnsureFirstRunTimestamp();

        Assert.Equal(first, reloaded);
    }

    [Fact]
    public void EnsureFirstRunTimestamp_CorruptValue_ReRecordsWithoutThrowing()
    {
        File.WriteAllText(_configPath, "{\"firstRunTimestamp\": \"pas-une-date\"}");
        ConfigManager.OverrideConfigPathForTests(_configPath); // recharge depuis le disque

        var before = DateTimeOffset.UtcNow.AddSeconds(-2);
        var stamp = ConfigManager.EnsureFirstRunTimestamp();
        var after = DateTimeOffset.UtcNow.AddSeconds(2);

        Assert.InRange(stamp, before, after);
    }

    [Fact]
    public void ReviewPromptCount_DefaultsToZero()
    {
        Assert.Equal(0, ConfigManager.ReviewPromptCount);
        Assert.Null(ConfigManager.ReviewPromptLastShown);
        Assert.False(ConfigManager.ReviewPromptClicked);
    }

    [Fact]
    public void RecordReviewPromptShown_IncrementsAndPersists()
    {
        ConfigManager.RecordReviewPromptShown();
        Assert.Equal(1, ConfigManager.ReviewPromptCount);
        // Heure locale, comme la comparaison du plancher de 7 jours (R9 de l'audit
        // v1.2.0) : l'assertion en UtcNow épousait le bug au lieu de le voir.
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), ConfigManager.ReviewPromptLastShown);

        // Persistance disque (survit à un redémarrage)
        ConfigManager.OverrideConfigPathForTests(_configPath);
        Assert.Equal(1, ConfigManager.ReviewPromptCount);

        ConfigManager.RecordReviewPromptShown();
        Assert.Equal(2, ConfigManager.ReviewPromptCount);
    }

    [Fact]
    public void RecordReviewPromptShown_CapsAtTwo()
    {
        for (int i = 0; i < 5; i++) ConfigManager.RecordReviewPromptShown();
        Assert.Equal(2, ConfigManager.ReviewPromptCount);
    }

    /// <summary>
    /// R9 de l'audit v1.2.0 : la date était écrite en UTC alors que toutes ses
    /// comparaisons se font en heure locale, ce qui ouvrait le second essai un jour trop
    /// tôt entre 00 h et 02 h locales. Elle vient désormais de l'appelant, qui a déjà
    /// calculé sa date locale. Une date arbitraire est le seul témoin qui ne dépende pas
    /// de l'heure à laquelle le test tourne.
    /// </summary>
    [Fact]
    public void RecordReviewPromptShown_DateDeLAppelant_EstEcriteTelleQuelle()
    {
        var shownOn = new DateOnly(2026, 1, 2);
        ConfigManager.RecordReviewPromptShown(shownOn);

        Assert.Equal(shownOn, ConfigManager.ReviewPromptLastShown);
        Assert.Contains("\"2026-01-02\"", File.ReadAllText(_configPath));

        // Survit au redémarrage : c'est ce que relit la garde quotidienne de R1.
        ConfigManager.OverrideConfigPathForTests(_configPath);
        Assert.Equal(shownOn, ConfigManager.ReviewPromptLastShown);
    }

    /// <summary>
    /// Migration v1.1 → v1.2 : une installation qui ne porte que `reviewPromptDone` a déjà
    /// consommé un essai. Sans cette équivalence, la v1.2.0 enverrait deux notifications
    /// supplémentaires à quelqu'un qui a déjà été sollicité.
    /// </summary>
    [Fact]
    public void ReviewPromptCount_MigratesLegacyDoneFlagToOne()
    {
        File.WriteAllText(_configPath, "{\"reviewPromptDone\": true}");
        ConfigManager.OverrideConfigPathForTests(_configPath);

        Assert.Equal(1, ConfigManager.ReviewPromptCount);

        // Le second essai reste possible et porte bien le compteur à 2, pas à 3.
        ConfigManager.RecordReviewPromptShown();
        Assert.Equal(2, ConfigManager.ReviewPromptCount);
    }

    [Fact]
    public void ReviewPromptCount_ExplicitCountWinsOverLegacyFlag()
    {
        File.WriteAllText(_configPath, "{\"reviewPromptDone\": true, \"reviewPromptCount\": 2}");
        ConfigManager.OverrideConfigPathForTests(_configPath);

        Assert.Equal(2, ConfigManager.ReviewPromptCount);
    }

    [Fact]
    public void SetReviewPromptClicked_PersistsTrue()
    {
        ConfigManager.SetReviewPromptClicked();
        Assert.True(ConfigManager.ReviewPromptClicked);

        ConfigManager.OverrideConfigPathForTests(_configPath);
        Assert.True(ConfigManager.ReviewPromptClicked);
    }
}
