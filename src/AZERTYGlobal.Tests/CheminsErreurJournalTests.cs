using System.Diagnostics;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit des chemins d'erreur, rapport-C T1 : une exception répétée ne doit pas faire dépasser
/// 5 Mo à error.log en cours de session (la rotation n'a lieu qu'au démarrage). Le journal est
/// redirigé vers un dossier temporaire ; l'error.log réel n'est pas touché.
/// </summary>
public sealed class CheminsErreurJournalTests : IDisposable
{
    private const long PlafondOctets = 5L * 1024 * 1024; // ConfigManager.LogRotationSizeBytes
    private readonly string _dossier = Path.Combine(Path.GetTempPath(), "AZGCE_C_" + Guid.NewGuid().ToString("N"));

    public CheminsErreurJournalTests()
    {
        Directory.CreateDirectory(_dossier);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_dossier, "config.json")); // LogDirectory = _dossier
    }

    public void Dispose()
    {
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_dossier, "inutilise-config.json"));
        for (int essai = 0; essai < 20; essai++)
        {
            try { Directory.Delete(_dossier, true); return; }
            catch (IOException) { Thread.Sleep(50); }
            catch (UnauthorizedAccessException) { Thread.Sleep(50); }
        }
    }

    // rapport-C T1
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void Une_exception_repetee_ne_depasse_pas_le_plafond_de_error_log_en_cours_de_session()
    {
        string journal = Path.Combine(ConfigManager.LogDirectory, "error.log");
        Assert.StartsWith(_dossier, journal, StringComparison.OrdinalIgnoreCase); // isolation : jamais le vrai journal
        var exception = new InvalidOperationException(new string('x', 200)); // 200 = plafond de SanitizeException

        // ~270 octets par ligne : 30 000 appels = ~8 Mo, au-delà du plafond. Log écrit sur le pool de threads.
        for (int i = 0; i < 30_000; i++)
            ConfigManager.Log("VirtualKeyboard WndProc", exception);

        // Attendre que le pool ait fini : taille inchangée sur 5 relevés de 200 ms.
        long derniere = -1;
        int stable = 0;
        var chrono = Stopwatch.StartNew();
        while (chrono.Elapsed < TimeSpan.FromSeconds(120) && stable < 5)
        {
            Thread.Sleep(200);
            long taille = File.Exists(journal) ? new FileInfo(journal).Length : 0;
            stable = taille > 0 && taille == derniere ? stable + 1 : 0;
            derniere = taille;
        }

        long finale = File.Exists(journal) ? new FileInfo(journal).Length : 0;
        Assert.True(finale <= PlafondOctets + 64 * 1024,
            $"error.log fait {finale / 1024} Ko en cours de session ; la rotation à 5 Mo n'a lieu qu'au démarrage");
    }
}
