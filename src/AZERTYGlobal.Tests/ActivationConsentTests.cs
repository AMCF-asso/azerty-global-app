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
    public void Aucun_accord_ne_se_deduit_du_passe_ou_d_un_fichier_invalide(string? json)
    {
        if (json != null) File.WriteAllText(ConfigPath, json);
        Assert.False(ConfigManager.ActivationConsent);
        if (json != null) Assert.Equal(json, File.ReadAllText(ConfigPath));
    }

    /// <summary>
    /// Ancien cas « configuration illisible » de la théorie ci-dessus. Audit du 25/09 : le
    /// fichier corrompu n'est plus conservé en place, où il bloquait toute sauvegarde (l'accord
    /// n'était jamais mémorisé). Il est mis de côté, intact, et aucun accord ne s'en déduit.
    /// </summary>
    [Fact]
    public void Fichier_illisible_mis_de_cote_sans_accord()
    {
        File.WriteAllText(ConfigPath, "configuration illisible");
        Assert.False(ConfigManager.ActivationConsent);
        var copie = Assert.Single(Directory.GetFiles(_directory, "config.json" + FileQuarantine.Marker + "*"));
        Assert.Equal("configuration illisible", File.ReadAllText(copie));
    }

    [Fact]
    public void Accord_explicite_survit_au_rechargement_et_preserve_la_configuration()
    {
        File.WriteAllText(ConfigPath, "{\"appLanguage\":\"en\",\"future\":{\"x\":1}}");
        ConfigManager.AcceptActivation();
        ConfigManager.Flush(); // écriture groupée (audit du 25/09, A-05) : ce que fait la fermeture
        ConfigManager.OverrideConfigPathForTests(ConfigPath);
        Assert.True(ConfigManager.ActivationConsent);
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ConfigPath));
        Assert.Equal(1, doc.RootElement.GetProperty("future").GetProperty("x").GetInt32());
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Politique_masquant_l_accueil_ne_vaut_jamais_activation(int? policy, bool prompt)
    {
        using var scope = PolicyManager.OverrideForTests((_, name) => name == "ShowOnboarding" ? policy : null);
        Assert.Equal(prompt, ConfigManager.ActivationPromptAtStartup);
        Assert.False(ConfigManager.ActivationConsent);
        ConfigManager.AcceptActivation();
        Assert.False(ConfigManager.ActivationPromptAtStartup);
    }

    public void Dispose() { Directory.Delete(_directory, true); }
}
