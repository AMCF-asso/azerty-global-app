using System.Text.Json;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Écriture de config.json ratée (décision d'Antoine du 29/09, suite de R58) : File.Replace
/// échoue quand un lecteur tient le fichier, et l'échec remettait le cache « à écrire » sans
/// réarmer la minuterie. Le réglage attendait le suivant ou la sortie de l'application.
/// Désormais : nouvel essai à 1 s, 5 s, 30 s, puis toutes les 5 min ; une ligne d'error.log
/// par palier ; un nouveau réglage relance le cycle normal à 400 ms.
///
/// Aucune attente réelle : la minuterie est remplacée par <c>SaveSchedulerForTests</c>, qui
/// note chaque délai armé, et le test déclenche lui-même chaque essai par Flush. L'échec vient
/// de <c>DiskWritePhaseForTests</c>, pendant l'écriture. Aucune fenêtre.
/// </summary>
public class EcritureRateeReessaiTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AZGRETRY_" + Guid.NewGuid().ToString("N"));
    private readonly List<int> _armés = new();
    private int _échecsÀVenir;
    private string ConfigPath => Path.Combine(_dir, "config.json");
    private string LogPath => Path.Combine(_dir, "error.log");

    public EcritureRateeReessaiTests()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath, "{\"activationConsent\":true,\"appLanguage\":\"fr\",\"notificationsEnabled\":true}");
        ConfigManager.OverrideConfigPathForTests(ConfigPath); // journal dans le même dossier
        _ = ConfigManager.ActivationConsent;
        ConfigManager.SaveDelayMilliseconds = 400; // valeur de production
        ConfigManager.SaveSchedulerForTests = délai => _armés.Add(délai);
        ConfigManager.DiskWritePhaseForTests = () =>
        {
            if (_échecsÀVenir > 0)
            {
                _échecsÀVenir--;
                throw new IOException("remplacement refusé (simulé)");
            }
        };
    }

    public void Dispose()
    {
        ConfigManager.SaveSchedulerForTests = null;
        ConfigManager.DiskWritePhaseForTests = null;
        ConfigManager.SaveDelayMilliseconds = -1;
        for (int essai = 0; essai < 20; essai++)
        {
            try { Directory.Delete(_dir, true); return; }
            catch (IOException) { Thread.Sleep(25); }
            catch (UnauthorizedAccessException) { Thread.Sleep(25); }
        }
    }

    private bool NotificationsSurLeDisque()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
        return doc.RootElement.GetProperty("notificationsEnabled").GetBoolean();
    }

    /// <summary>Lignes d'échec d'écriture dans error.log. Le journal s'écrit sur le pool de
    /// threads, sans ordre garanti entre deux lignes : attendre le compte voulu, puis
    /// vérifier qu'aucune ligne ne suit.</summary>
    private string[] LignesDÉchec(int attendues)
    {
        string[] Lire()
        {
            try
            {
                using var flux = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var lecteur = new StreamReader(flux);
                return lecteur.ReadToEnd().Split('\n').Where(l => l.Contains("ConfigManager.Save")).ToArray();
            }
            catch (IOException) { return Array.Empty<string>(); } // absent, ou en cours d'ajout
        }
        var limite = DateTime.UtcNow.AddSeconds(2);
        while (Lire().Length < attendues && DateTime.UtcNow < limite) Thread.Sleep(10);
        Thread.Sleep(150);
        return Lire();
    }

    private static int Écritures() => Volatile.Read(ref ConfigManager.DiskWriteCount);

    [Fact]
    public void ÉcritureRatée_SeRéessaieSeuleÀ1s_EtRéussitAuPalierSuivant()
    {
        int avant = Écritures();
        ConfigManager.SetNotifications(false);
        Assert.Equal(new[] { 400 }, _armés); // le cycle normal

        _échecsÀVenir = 1;
        ConfigManager.Flush(); // l'écriture différée, ratée
        Assert.Equal(new[] { 400, 1_000 }, _armés); // nouvel essai armé à 1 s, sans autre geste
        Assert.True(NotificationsSurLeDisque());
        Assert.Equal(avant, Écritures());

        ConfigManager.Flush(); // le palier de 1 s
        Assert.False(NotificationsSurLeDisque());
        Assert.Equal(avant + 1, Écritures());
        Assert.Equal(new[] { 400, 1_000 }, _armés); // réussie : plus rien d'armé

        var lignes = LignesDÉchec(1);
        Assert.Single(lignes);
        Assert.Contains("nouvel essai dans 1 s", lignes[0]);
    }

    [Fact]
    public void ÉchecsRépétés_1s5s30sPuis5min_UneSeuleLigneDeJournalParPalier()
    {
        ConfigManager.SetNotifications(false);
        _échecsÀVenir = 6;
        for (int essai = 0; essai < 6; essai++) ConfigManager.Flush();

        Assert.Equal(new[] { 400, 1_000, 5_000, 30_000, 300_000, 300_000, 300_000 }, _armés);
        var lignes = LignesDÉchec(4);
        Assert.Equal(4, lignes.Length); // six échecs, quatre paliers
        foreach (string palier in new[] { "dans 1 s", "dans 5 s", "dans 30 s", "dans 300 s" })
            Assert.Single(lignes, l => l.Contains(palier));

        ConfigManager.Flush(); // le disque revient : l'essai suivant passe
        Assert.False(NotificationsSurLeDisque());
    }

    [Fact]
    public void NouveauRéglagePendantLesEssais_RemetLeDélaiÀZéro()
    {
        ConfigManager.SetNotifications(false);
        _échecsÀVenir = 3;
        for (int essai = 0; essai < 3; essai++) ConfigManager.Flush();
        Assert.Equal(new[] { 400, 1_000, 5_000, 30_000 }, _armés);

        ConfigManager.SetAutoStart(true); // un geste de l'utilisateur
        Assert.Equal(400, _armés[^1]); // cycle normal, pas 5 min

        _échecsÀVenir = 1;
        ConfigManager.Flush();
        Assert.Equal(1_000, _armés[^1]); // le cycle repart du premier palier
        var lignes = LignesDÉchec(4);
        Assert.Equal(4, lignes.Length); // le premier palier se journalise de nouveau
        Assert.Equal(2, lignes.Count(l => l.Contains("dans 1 s")));

        ConfigManager.Flush();
        Assert.False(NotificationsSurLeDisque());
    }
}
