using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 25/09 (A-11) : un usage-stats.json présent mais illisible au chargement (verrou
/// d'un antivirus ou d'une sauvegarde) n'est jamais écrasé par des compteurs repartis de
/// zéro, et le lancement suivant le relit. Un fichier corrompu, lui, repart de zéro et se
/// réécrit comme avant : la garde ne doit pas le bloquer pour toujours.
/// </summary>
public class UsageStatsReadFailureTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _statsPath;
    private const string Original = "{ \"activeDaysCount\": 42, \"bestStreak\": 30 }";

    public UsageStatsReadFailureTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _statsPath = Path.Combine(_tempDir, "usage-stats.json");
        File.WriteAllText(_statsPath, Original);
        UsageStats.OverrideStatsPathForTests(_statsPath);
    }

    public void Dispose()
    {
        UsageStats.OverrideStatsPathForTests(Path.Combine(_tempDir, "inutilise.json"));
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void FichierVerrouilleAuChargement_NEstJamaisEcrase()
    {
        using (new FileStream(_statsPath, FileMode.Open, FileAccess.Read, FileShare.None))
            UsageStats.Preload(); // lecture refusée : violation de partage

        // Scénario joué : le chargement a bien échoué (42 attendus s'il avait réussi).
        Assert.Equal(0, UsageStats.ActiveDaysCount);

        UsageStats.RecordEmittedText("É");
        UsageStats.Flush();

        Assert.Equal(Original, File.ReadAllText(_statsPath));
    }

    [Fact]
    public void ApresUneLectureRatee_LeLancementSuivantRelitLeFichier()
    {
        using (new FileStream(_statsPath, FileMode.Open, FileAccess.Read, FileShare.None))
            UsageStats.Preload();
        UsageStats.RecordEmittedText("É");
        UsageStats.Flush();

        UsageStats.OverrideStatsPathForTests(_statsPath); // redémarrage simulé

        Assert.Equal(42, UsageStats.ActiveDaysCount);
        Assert.Equal(30, UsageStats.BestStreak);
    }

    [Fact]
    public void Controle_FichierLisible_EstBienReecritAuFlush()
    {
        // ⛔ Réciproque du premier témoin : sans verrou, le même scénario réécrit le fichier.
        // Sans elle, une collecte éteinte dans ce test ferait passer le premier sans rien prouver.
        UsageStats.Preload();
        Assert.Equal(42, UsageStats.ActiveDaysCount);

        UsageStats.RecordEmittedText("É");
        UsageStats.Flush();

        Assert.NotEqual(Original, File.ReadAllText(_statsPath));
        UsageStats.OverrideStatsPathForTests(_statsPath);
        Assert.Equal(1, UsageStats.AccentedUppercaseCount);
    }

    [Fact]
    public void FichierCorrompu_RepartDeZeroEtSeReecrit()
    {
        File.WriteAllText(_statsPath, "{ ceci n'est pas du JSON valide");
        UsageStats.OverrideStatsPathForTests(_statsPath);

        UsageStats.RecordEmittedText("É");
        UsageStats.Flush();

        UsageStats.OverrideStatsPathForTests(_statsPath);
        Assert.Equal(1, UsageStats.AccentedUppercaseCount);
    }
}
