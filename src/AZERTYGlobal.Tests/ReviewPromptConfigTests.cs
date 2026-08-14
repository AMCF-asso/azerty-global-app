using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests des clés de la sollicitation d'avis one-shot : horloge du premier lancement
/// (enregistrée au premier accès, stable ensuite, tolérante à la corruption) et
/// flag reviewPromptDone (défaut false, persisté une fois posé).
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
    public void ReviewPromptDone_DefaultsToFalse()
    {
        Assert.False(ConfigManager.ReviewPromptDone);
    }

    [Fact]
    public void SetReviewPromptDone_PersistsTrue()
    {
        ConfigManager.SetReviewPromptDone();
        Assert.True(ConfigManager.ReviewPromptDone);

        // Persistance disque (survit à un redémarrage)
        ConfigManager.OverrideConfigPathForTests(_configPath);
        Assert.True(ConfigManager.ReviewPromptDone);
    }
}
