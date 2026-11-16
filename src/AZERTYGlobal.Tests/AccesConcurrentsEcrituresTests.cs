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
        ConfigManager.LogWritePhaseForTests = null;
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

    /// <summary>
    /// Constat P2, ticket T2. <c>LogCompatEvent</c> est appelé sur le fil d'interface (changement
    /// de mode au premier plan, ForegroundMonitor). Il écrivait error.log en synchrone, sous le
    /// verrou du journal partagé avec le pool : un disque lent pendant une écriture du pool
    /// gelait la boucle de messages, donc le hook clavier.
    /// </summary>
    [Fact]
    public async Task Journal_LogCompatEventNAttendPasUneEcritureEnCours()
    {
        using var dansLÉcriture = new ManualResetEventSlim();
        using var reprendre = new ManualResetEventSlim();
        ConfigManager.LogWritePhaseForTests = ligne =>
        {
            if (!ligne.Contains("EcritureLente")) return;
            dansLÉcriture.Set();
            reprendre.Wait(10_000);
        };
        ConfigManager.Log("EcritureLente", new IOException("disque lent (simulé)"));
        Assert.True(dansLÉcriture.Wait(5_000), "l'écriture du pool n'a pas commencé");

        var appel = Task.Run(() => ConfigManager.LogCompatEvent("CompatMode", "Default → Default"));
        bool rendu = await Task.WhenAny(appel, Task.Delay(1_000)) == appel;
        reprendre.Set();
        await appel.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(rendu, "LogCompatEvent attend la fin d'une écriture du journal sur le pool");
        Assert.True(Attendre(() => LignesDuJournal().Any(l => l.Contains("CompatMode"))),
            "la ligne CompatMode n'est jamais arrivée dans error.log");
    }

    /// <summary>
    /// Constat P6, ticket T5. Les lignes d'error.log suivent l'ordre des appels, et une ligne
    /// critique (écrite en synchrone pour survivre à un plantage) emporte avec elle tout ce
    /// qui a été journalisé avant elle. Avant : un work item par ligne, dans le désordre, et
    /// les lignes du pool encore en file manquaient au moment de la ligne critique.
    /// </summary>
    [Fact]
    public void Journal_LignesDansLOrdreDesAppelsEtCompletesALaLigneCritique()
    {
        const int nombre = 50;
        for (int i = 0; i < nombre; i++)
            ConfigManager.Log($"Ordre{i:D2}", new IOException("x"));
        ConfigManager.LogCompatCriticalEvent("Ordre99", "fin");

        var lignes = LignesDuJournal().Where(l => l.Contains("] Ordre")).ToArray();
        Assert.Equal(nombre + 1, lignes.Length);
        for (int i = 0; i < nombre; i++)
            Assert.Contains($"] Ordre{i:D2}:", lignes[i]);
        Assert.Contains("] Ordre99:", lignes[nombre]);
    }

    /// <summary>
    /// Constat P5, ticket T5. La ligne FATAL du gestionnaire d'exception non rattrapée est
    /// écrite en synchrone, après les lignes encore en file (avant : écrite hors verrou, et les
    /// lignes en attente sur le pool mouraient avec le processus).
    /// </summary>
    [Fact]
    public void Journal_LigneFatale_EcritAvantElleLesLignesEnFile()
    {
        using var dansLÉcriture = new ManualResetEventSlim();
        ConfigManager.LogWritePhaseForTests = ligne =>
        {
            if (ligne.Contains("AvantFatal0")) { dansLÉcriture.Set(); Thread.Sleep(200); }
        };
        ConfigManager.Log("AvantFatal0", new IOException("x"));
        Assert.True(dansLÉcriture.Wait(5_000));
        ConfigManager.Log("AvantFatal1", new IOException("x")); // en file derrière l'écriture lente

        ConfigManager.LogFatal("InvalidOperationException: boom");

        var lignes = LignesDuJournal();
        int avant = Array.FindIndex(lignes, l => l.Contains("] AvantFatal1:"));
        int fatale = Array.FindIndex(lignes, l => l.Contains("] FATAL: InvalidOperationException: boom"));
        Assert.True(avant >= 0, "la ligne en file n'a pas été écrite avant la fin de LogFatal");
        Assert.True(fatale > avant, "la ligne FATAL précède une ligne journalisée avant elle");
    }

    private string[] LignesDuJournal()
    {
        try
        {
            using var flux = new FileStream(Path.Combine(_dir, "error.log"), FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var lecteur = new StreamReader(flux);
            return lecteur.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }
        catch (IOException) { return Array.Empty<string>(); }
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
