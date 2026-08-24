using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests du socle i18n : persistance de la langue dans config.json (clé appLanguage),
/// valeur par défaut "fr", tolérance aux valeurs corrompues, et bascule L.Language.
/// </summary>
public class LocalizationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public LocalizationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
        ConfigManager.OverrideConfigPathForTests(_configPath);
        L.Language = "fr";
    }

    public void Dispose()
    {
        L.Language = "fr";
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void AppLanguage_SansCle_DeriveDeLaLangueWindows()
    {
        // Sans clé ni politique, le défaut suit l'interface Windows (QW-2, 2026-08-24).
        // Comparé au LANGID réel du poste : sur une CI anglophone le défaut devient
        // « en », et un « fr » codé en dur y rougirait.
        Assert.Equal(
            ConfigManager.DefaultAppLanguage(ConfigManager.WindowsUiLanguageIdForTests()),
            ConfigManager.AppLanguage);
    }

    [Theory]
    [InlineData((ushort)0x040C, "fr")] // fr-FR
    [InlineData((ushort)0x0C0C, "fr")] // fr-CA
    [InlineData((ushort)0x080C, "fr")] // fr-BE
    [InlineData((ushort)0x0409, "en")] // en-US
    [InlineData((ushort)0x0407, "en")] // de-DE — tout non-francophone retombe sur « en »
    public void DefaultAppLanguage_DeriveDuLangId(ushort langId, string expected)
    {
        Assert.Equal(expected, ConfigManager.DefaultAppLanguage(langId));
    }

    [Fact]
    public void SetAppLanguage_En_PersistsAndSurvivesReload()
    {
        ConfigManager.SetAppLanguage("en");
        Assert.Equal("en", ConfigManager.AppLanguage);

        ConfigManager.OverrideConfigPathForTests(_configPath);
        Assert.Equal("en", ConfigManager.AppLanguage);
    }

    [Fact]
    public void SetAppLanguage_InvalidValue_IsIgnored()
    {
        ConfigManager.SetAppLanguage("en");
        ConfigManager.SetAppLanguage("de");
        Assert.Equal("en", ConfigManager.AppLanguage);
    }

    [Fact]
    public void AppLanguage_CorruptValue_FallsBackToFr()
    {
        File.WriteAllText(_configPath, "{\"appLanguage\": \"xx\"}");
        ConfigManager.OverrideConfigPathForTests(_configPath);
        Assert.Equal("fr", ConfigManager.AppLanguage);
    }

    [Fact]
    public void L_IsEnglish_ReflectsLanguage()
    {
        L.Language = "fr";
        Assert.False(L.IsEnglish);

        L.Language = "en";
        Assert.True(L.IsEnglish);
    }

    [Fact]
    public void L_DisplayCulture_MatchesLanguage()
    {
        L.Language = "fr";
        Assert.Equal("fr-FR", L.DisplayCulture.Name);

        L.Language = "en";
        Assert.Equal("en-US", L.DisplayCulture.Name);
    }
}
