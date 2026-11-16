using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit des accès concurrents 1.3.0 : courses entre le fil d'interface et le pool de threads
/// sur les écritures groupées de config.json et de usage-stats.json. Chaque test suspend
/// l'écriture du pool au milieu (crochets <c>…WritePhaseForTests</c>), joue le geste du fil
/// d'interface pendant la suspension, puis vérifie l'état final. Aucune fenêtre.
/// </summary>
public class AccesConcurrentsEcrituresTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AZGRACE_" + Guid.NewGuid().ToString("N"));
    private readonly List<int> _armés = new();
    private string ConfigPath => Path.Combine(_dir, "config.json");
    private string StatsPath => Path.Combine(_dir, "usage-stats.json");

    public AccesConcurrentsEcrituresTests()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath, "{\"activationConsent\":true,\"appLanguage\":\"fr\",\"notificationsEnabled\":true}");
        ConfigManager.OverrideConfigPathForTests(ConfigPath);
        _ = ConfigManager.ActivationConsent;
    }

    public void Dispose()
    {
        ConfigManager.SaveSchedulerForTests = null;
        ConfigManager.DiskWritePhaseForTests = null;
        ConfigManager.SaveDelayMilliseconds = -1;
        UsageStats.WritePhaseForTests = null;
        UsageStats.OverrideStatsPathForTests(Path.Combine(_dir, "inutilise.json"));
        for (int essai = 0; essai < 20; essai++)
        {
            try { Directory.Delete(_dir, true); return; }
            catch (IOException) { Thread.Sleep(25); }
            catch (UnauthorizedAccessException) { Thread.Sleep(25); }
        }
    }

    /// <summary>
    /// Constat CM6 (ConfigManager.cs:1490-1512). Le pool écrit config.json ; pendant l'écriture,
    /// l'utilisateur change un réglage (MarkDirty : nouveau cycle, minuterie à 400 ms, compteur
    /// d'échecs remis à zéro). L'écriture du pool échoue ensuite et reprend la main : elle
    /// incrémente le compteur du nouveau cycle et remplace les 400 ms du réglage par le palier
    /// d'échec de 1 s. La règle voulue (NouveauRéglagePendantLesEssais_RemetLeDélaiÀZéro) est
    /// qu'un geste de l'utilisateur relance le cycle normal.
    /// </summary>
    [Fact]
    public void Config_ReglageChangePendantUneEcritureQuiEchoue_GardeLeCycleNormal()
    {
        ConfigManager.SaveDelayMilliseconds = 400; // valeur de production
        ConfigManager.SaveSchedulerForTests = délai => { lock (_armés) _armés.Add(délai); };
        ConfigManager.SetNotifications(false);

        using var dansLÉcriture = new ManualResetEventSlim();
        using var reprendre = new ManualResetEventSlim();
        ConfigManager.DiskWritePhaseForTests = () =>
        {
            dansLÉcriture.Set();
            reprendre.Wait(10_000);
            throw new IOException("remplacement refusé (simulé)");
        };

        var pool = new Thread(() => { try { ConfigManager.Flush(); } catch { } }) { IsBackground = true };
        pool.Start();
        Assert.True(dansLÉcriture.Wait(5_000), "l'écriture n'a pas atteint le disque");
        ConfigManager.DiskWritePhaseForTests = null;

        ConfigManager.SetAutoStart(true); // le geste de l'utilisateur, pendant l'écriture
        int armeParLeGeste;
        lock (_armés) armeParLeGeste = _armés[^1];

        reprendre.Set();
        Assert.True(pool.Join(10_000));

        Assert.Equal(400, armeParLeGeste);
        lock (_armés) Assert.Equal(400, _armés[^1]); // constaté : 1 000 (palier d'échec)
    }

    /// <summary>
    /// Constat B2 (UsageStats.cs:667-673). Une demande d'écriture en arrière-plan arrivée
    /// pendant une autre est ignorée, même si l'écriture en cours a déjà pris son instantané.
    /// Ce qui a été compté entre les deux attend le minuteur de 5 min ou la sortie : le
    /// verrouillage de session (TrayApplication.cs:1093, « écrire ce qui attend ») n'écrit rien.
    /// </summary>
    [Fact]
    public void Stats_DemandePendantUneEcritureEnCours_EstServie()
    {
        UsageStats.OverrideStatsPathForTests(StatsPath);
        UsageStats.RecordEmittedText("É");

        using var dansLÉcriture = new ManualResetEventSlim();
        using var reprendre = new ManualResetEventSlim();
        UsageStats.WritePhaseForTests = () => { dansLÉcriture.Set(); reprendre.Wait(10_000); };

        UsageStats.FlushInBackground(); // minuteur de 5 min
        Assert.True(dansLÉcriture.Wait(5_000), "l'écriture n'a pas atteint le disque");
        UsageStats.WritePhaseForTests = null;

        UsageStats.RecordEmittedText("É"); // frappe après l'instantané
        UsageStats.FlushInBackground();    // verrouillage de session : « écrire ce qui attend »

        reprendre.Set();
        Assert.True(Attendre(() => File.Exists(StatsPath)), "la première écriture n'a pas abouti");
        Assert.True(Attendre(() => CompteSurLeDisque() == 2, 2_000),
            $"la seconde demande n'a rien écrit : {CompteSurLeDisque()} sur le disque, 2 en mémoire");
    }

    private int CompteSurLeDisque()
    {
        try
        {
            using var flux = new FileStream(StatsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var doc = System.Text.Json.JsonDocument.Parse(flux);
            return doc.RootElement.GetProperty("accentedUppercaseCount").GetInt32();
        }
        catch (IOException) { return -1; }
        catch (System.Text.Json.JsonException) { return -1; }
    }

    private static bool Attendre(Func<bool> condition, int millisecondes = 5_000)
    {
        var limite = DateTime.UtcNow.AddMilliseconds(millisecondes);
        while (DateTime.UtcNow < limite)
        {
            if (condition()) return true;
            Thread.Sleep(10);
        }
        return condition();
    }
}
