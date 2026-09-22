using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

public class ActivationConsentTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AZGCONSENT_" + Guid.NewGuid().ToString("N"));
    private string ConfigPath => Path.Combine(_directory, "config.json");
    public ActivationConsentTests()
    {
        Directory.CreateDirectory(_directory);
        ConfigManager.OverrideConfigPathForTests(ConfigPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("{\"onboardingDone\":true,\"appLanguage\":\"fr\"}")]
    [InlineData("{\"activationConsent\":false}")]
    [InlineData("{\"activationConsent\":\"true\"}")]
    [InlineData("configuration illisible")]
    public void Aucun_accord_ne_se_deduit_du_passe_ou_d_un_fichier_invalide(string? json)
    {
        if (json != null) File.WriteAllText(ConfigPath, json);
        Assert.False(ConfigManager.ActivationConsent);
        if (json != null) Assert.Equal(json, File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void Accord_explicite_survit_au_rechargement_et_preserve_la_configuration()
    {
        File.WriteAllText(ConfigPath, "{\"appLanguage\":\"en\",\"future\":{\"x\":1}}");
        ConfigManager.AcceptActivation();
        ConfigManager.OverrideConfigPathForTests(ConfigPath);
        Assert.True(ConfigManager.ActivationConsent);
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ConfigPath));
        Assert.Equal(1, doc.RootElement.GetProperty("future").GetProperty("x").GetInt32());
    }

    public void Dispose() { Directory.Delete(_directory, true); }
}
