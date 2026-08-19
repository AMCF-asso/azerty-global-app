using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Extinction des statistiques d'usage sur le canal sobre — lot D du plan v1.2.0, décision
/// D5 du 2026-08-19, comportement dégradé validé par Antoine le même jour.
///
/// Le critère du plan est asserté tel qu'il est écrit : après une session complète simulée,
/// <c>usage-stats.json</c> <b>n'existe pas</b> sur le disque. C'est l'absence du fichier qui
/// est vérifiée, jamais la valeur d'un booléen — un booléen peut rester juste pendant qu'une
/// écriture passe par un autre chemin.
///
/// Chaque réciproque sur canal Store est obligatoire, pas décorative : sans elle, l'assertion
/// d'absence resterait verte dans un dépôt où plus rien n'écrirait, y compris pour les
/// utilisateurs du Store.
/// </summary>
public class StatsCollectionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _statsPath;

    public StatsCollectionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _statsPath = Path.Combine(_tempDir, "usage-stats.json");
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_tempDir, "config.json"));
        UsageStats.OverrideStatsPathForTests(_statsPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    /// <summary>Ce qu'une session fait réellement : frappes remappées, ouverture des deux
    /// outils, défi terminé, puis le flush de fermeture.</summary>
    private static void SimulerUneSessionComplete()
    {
        UsageStats.Preload();
        UsageStats.RecordEmittedText("ÉÀÇ œuf « François »");
        UsageStats.RecordSearchOpened();
        UsageStats.RecordVirtualKeyboardOpened();
        UsageStats.RecordChallengeCompleted();
        UsageStats.Flush();
    }

    [Fact]
    public void CollectionEnabled_EteinteSurAmcf_ActiveAilleurs()
    {
        using (AppChannel.OverrideForTests(DistributionChannel.Amcf))
            Assert.False(UsageStats.CollectionEnabled);

        using (AppChannel.OverrideForTests(DistributionChannel.Store))
            Assert.True(UsageStats.CollectionEnabled);

        // D8 : l'EXE hors package collecte comme aujourd'hui.
        using (AppChannel.OverrideForTests(DistributionChannel.Unpackaged))
            Assert.True(UsageStats.CollectionEnabled);
    }

    /// <summary>Critère d'acceptation du lot D, mot pour mot.</summary>
    [Fact]
    public void SessionComplete_SurAmcf_NeCreeAucunFichier()
    {
        using (AppChannel.OverrideForTests(DistributionChannel.Amcf))
        {
            UsageStats.OverrideStatsPathForTests(_statsPath);
            SimulerUneSessionComplete();
        }

        Assert.False(File.Exists(_statsPath));
        // Le fichier temporaire de l'écriture atomique non plus : une écriture avortée en
        // laisserait un, et le dossier ne doit rien porter du tout.
        Assert.False(File.Exists(_statsPath + ".tmp"));
        Assert.Empty(Directory.GetFiles(_tempDir, "usage-stats*"));
    }

    /// <summary>Réciproque : la même session sur canal Store écrit bien le fichier.</summary>
    [Fact]
    public void SessionComplete_SurStore_EcritLeFichier()
    {
        using (AppChannel.OverrideForTests(DistributionChannel.Store))
        {
            UsageStats.OverrideStatsPathForTests(_statsPath);
            SimulerUneSessionComplete();
        }

        Assert.True(File.Exists(_statsPath));
    }

    /// <summary>Les compteurs vivent quand même pendant la session : c'est le comportement
    /// validé, le défi du jour et la fenêtre de statistiques continuent de fonctionner.
    /// Un lot D qui aurait éteint la collecte en mémoire casserait les deux.</summary>
    [Fact]
    public void SurAmcf_LesCompteursVivantEnMemoire()
    {
        using var scope = AppChannel.OverrideForTests(DistributionChannel.Amcf);
        UsageStats.OverrideStatsPathForTests(_statsPath);

        UsageStats.RecordEmittedText("ÉÀÇ");

        Assert.Equal(3L, UsageStats.AccentedUppercaseCount);
    }

    /// <summary>Un reliquat laissé par une installation précédente n'est pas relu : sinon la
    /// fenêtre afficherait des chiffres venus d'un fichier qu'elle annonce ne pas tenir.</summary>
    [Fact]
    public void SurAmcf_NeRelitPasUnFichierExistant()
    {
        File.WriteAllText(_statsPath, "{\"accentedUppercaseCount\": 42}");

        using var scope = AppChannel.OverrideForTests(DistributionChannel.Amcf);
        UsageStats.OverrideStatsPathForTests(_statsPath);

        Assert.Equal(0L, UsageStats.AccentedUppercaseCount);
    }

    /// <summary>Réciproque : sur canal Store le même fichier est relu. Sans ce test,
    /// l'assertion à zéro ci-dessus passerait aussi sur une lecture cassée pour tous.</summary>
    [Fact]
    public void SurStore_RelitLeFichierExistant()
    {
        File.WriteAllText(_statsPath, "{\"accentedUppercaseCount\": 42}");

        using var scope = AppChannel.OverrideForTests(DistributionChannel.Store);
        UsageStats.OverrideStatsPathForTests(_statsPath);

        Assert.Equal(42L, UsageStats.AccentedUppercaseCount);
    }
}
