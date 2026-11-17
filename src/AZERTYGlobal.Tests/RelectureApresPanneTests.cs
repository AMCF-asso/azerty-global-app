using System.Text.Json;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// CE-T2 (audit des chemins d'erreur, 17 novembre 2026) : un fichier de données tenu par un verrou
/// passager est réessayé brièvement au premier chargement, puis relu au prochain accès ; la mémoire
/// n'est vidée qu'après une lecture réussie, et ce que l'utilisateur a fait pendant la panne n'est
/// pas perdu (décision d'Antoine : le réglage récent gagne). Les tests d'audit CE-03 à CE-06 fixent
/// le cas du verrou bref ; ces témoins fixent la panne qui dure plus longtemps que les essais.
/// Pauses des essais coupées : le verrou est tenu pendant tout l'accès.
/// </summary>
public sealed class RelectureApresPanneTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AZGCE_T2_" + Guid.NewGuid().ToString("N"));
    private readonly int[] _delaisOrigine = FileQuarantine.ReadRetryDelaysMilliseconds;
    private readonly Action<int> _sommeilOrigine = FileQuarantine.ReadRetrySleep;
    private string ConfigPath => Path.Combine(_dir, "config.json");
    private string StatsPath => Path.Combine(_dir, "usage-stats.json");
    private string ProgressPath => Path.Combine(_dir, "lessons-progress.json");

    public RelectureApresPanneTests()
    {
        Directory.CreateDirectory(_dir);
        FileQuarantine.ReadRetryDelaysMilliseconds = Array.Empty<int>();
    }

    public void Dispose()
    {
        FileQuarantine.ReadRetryDelaysMilliseconds = _delaisOrigine;
        FileQuarantine.ReadRetrySleep = _sommeilOrigine;
        UsageStats.OverrideStatsPathForTests(Path.Combine(_dir, "inutilise.json"));
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_dir, "inutilise-config.json"));
        try { Directory.Delete(_dir, true); } catch { }
    }

    private FileStream Verrou(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.None);

    // ── Essais courts ─────────────────────────────────────────────────────

    [Fact]
    public void EssaisCourts_FichierToujoursTenu_TroisPausesPuisLEchecRemonte()
    {
        string path = Path.Combine(_dir, "tenu.json");
        File.WriteAllText(path, "{}");
        FileQuarantine.ReadRetryDelaysMilliseconds = new[] { 50, 100, 200 };
        var pauses = new List<int>();
        FileQuarantine.ReadRetrySleep = pauses.Add;

        using (Verrou(path))
            Assert.ThrowsAny<IOException>(() => FileQuarantine.ReadAllTextWithRetries(path));

        Assert.Equal(new[] { 50, 100, 200 }, pauses);
    }

    [Fact]
    public void EssaisCourts_FichierAbsent_AucunePause()
    {
        var pauses = new List<int>();
        FileQuarantine.ReadRetryDelaysMilliseconds = new[] { 50 };
        FileQuarantine.ReadRetrySleep = pauses.Add;

        Assert.Throws<FileNotFoundException>(() => FileQuarantine.ReadAllTextWithRetries(Path.Combine(_dir, "absent.json")));
        Assert.Empty(pauses);
    }

    // ── config.json ───────────────────────────────────────────────────────

    [Fact]
    public void Config_PanneLongue_RelueAuProchainAcces_LeReglageRecentGagneEtToutEstEcrit()
    {
        using var politique = PolicyManager.OverrideForTests((_, _) => null);
        string original = "{\"activationConsent\":true,\"notificationsEnabled\":true,\"appLanguage\":\"en\"}";
        File.WriteAllText(ConfigPath, original);
        ConfigManager.OverrideConfigPathForTests(ConfigPath);

        using (Verrou(ConfigPath))
        {
            Assert.False(ConfigManager.ActivationConsent); // illisible : rien ne s'active seul
            ConfigManager.SetNotifications(false);         // choix fait pendant la panne
            ConfigManager.Flush();                         // n'écrase pas le fichier non lu
        }
        Assert.Equal(original, File.ReadAllText(ConfigPath));

        Assert.True(ConfigManager.ActivationConsent);      // relu au prochain accès
        Assert.False(ConfigManager.NotificationsEnabled);  // le choix de la panne gagne
        Assert.Equal("en", ConfigManager.AppLanguage);     // le reste vient du fichier

        ConfigManager.Flush();
        using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
        Assert.True(doc.RootElement.GetProperty("activationConsent").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("notificationsEnabled").GetBoolean());
        Assert.Equal("en", doc.RootElement.GetProperty("appLanguage").GetString());
    }

    [Fact]
    public void Config_PanneLongue_LaFermetureRelitAvantDEcrire()
    {
        using var politique = PolicyManager.OverrideForTests((_, _) => null);
        File.WriteAllText(ConfigPath, "{\"activationConsent\":true}");
        ConfigManager.OverrideConfigPathForTests(ConfigPath);
        using (Verrou(ConfigPath))
        {
            _ = ConfigManager.ActivationConsent;
            ConfigManager.SetNotifications(false);
        }

        Assert.True(ConfigManager.FlushFinal(sleep: _ => { }));

        using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
        Assert.True(doc.RootElement.GetProperty("activationConsent").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("notificationsEnabled").GetBoolean());
    }

    // ── usage-stats.json ──────────────────────────────────────────────────

    [Fact]
    public void Stats_PanneLongue_LaSessionSAjouteAuFichierAlEcriture()
    {
        File.WriteAllText(StatsPath, "{ \"activeDaysCount\": 42, \"accentedUppercaseCount\": 7, \"searchOpenCount\": 3 }");
        UsageStats.OverrideStatsPathForTests(StatsPath);

        using (Verrou(StatsPath))
        {
            UsageStats.Preload();               // illisible : compteurs de la session depuis zéro
            UsageStats.RecordEmittedText("É");
            UsageStats.Flush();                 // n'écrase pas le fichier non lu
        }
        UsageStats.Flush();                     // relit, ajoute la session, écrit

        UsageStats.OverrideStatsPathForTests(StatsPath); // lancement suivant
        Assert.Equal(8, UsageStats.AccentedUppercaseCount);
        Assert.Equal(3, UsageStats.SearchOpenCount);
        Assert.True(UsageStats.ActiveDaysCount >= 42, $"jours actifs relus : {UsageStats.ActiveDaysCount}");
    }

    private static UsageStats.StatsFile Stats(string? first, string? last, int days, int current, int best, long accents = 0) =>
        new(first, last, days, current, best, 0, accents, 0, 0, 0, 0, 0, 0, null);

    [Fact]
    public void Fusion_SessionSansActivite_GardeLesJoursDuFichierEtAjouteLesTotaux()
    {
        var fusion = UsageStats.Merge(Stats("2026-01-01", "2026-11-10", 42, 5, 30, accents: 7),
            Stats(null, null, 0, 0, 0, accents: 0) with { SearchOpenCount = 2 });

        Assert.Equal((42, 5, 30, "2026-11-10"), (fusion.ActiveDaysCount, fusion.CurrentStreak, fusion.BestStreak, fusion.LastActiveDate));
        Assert.Equal(7, fusion.AccentedUppercaseCount);
        Assert.Equal(2, fusion.SearchOpenCount);
        Assert.Equal("2026-01-01", fusion.FirstRemapDate);
    }

    [Fact]
    public void Fusion_SessionLeMemeJour_LeJourNEstCompteQuUneFois()
    {
        var fusion = UsageStats.Merge(Stats("2026-01-01", "2026-11-10", 42, 5, 30, accents: 7),
            Stats("2026-11-10", "2026-11-10", 1, 1, 1, accents: 1));

        Assert.Equal((42, 5, 30), (fusion.ActiveDaysCount, fusion.CurrentStreak, fusion.BestStreak));
        Assert.Equal(8, fusion.AccentedUppercaseCount);
        Assert.Equal("2026-01-01", fusion.FirstRemapDate);
    }

    [Fact]
    public void Fusion_SessionLeLendemain_LaSerieSePoursuit()
    {
        var fusion = UsageStats.Merge(Stats("2026-01-01", "2026-11-10", 42, 30, 30),
            Stats("2026-11-11", "2026-11-12", 2, 2, 2));

        Assert.Equal((44, 32, 32, "2026-11-12"), (fusion.ActiveDaysCount, fusion.CurrentStreak, fusion.BestStreak, fusion.LastActiveDate));
    }

    [Fact]
    public void Fusion_TrouAvantLaSession_LaSerieRepartDeLaSession()
    {
        var fusion = UsageStats.Merge(Stats("2026-01-01", "2026-11-10", 42, 5, 30),
            Stats("2026-11-13", "2026-11-13", 1, 1, 1));

        Assert.Equal((43, 1, 30, "2026-11-13"), (fusion.ActiveDaysCount, fusion.CurrentStreak, fusion.BestStreak, fusion.LastActiveDate));
    }

    [Fact]
    public void Fusion_TrouDansLaSession_LaSerieEstCelleDeLaFinDeSession()
    {
        // Session : le 11, puis le 13 (série de 1), alors que le fichier finit le 10.
        var fusion = UsageStats.Merge(Stats("2026-01-01", "2026-11-10", 42, 5, 30),
            Stats("2026-11-11", "2026-11-13", 2, 1, 1));

        Assert.Equal((44, 1, 30), (fusion.ActiveDaysCount, fusion.CurrentStreak, fusion.BestStreak));
    }

    // ── lessons-progress.json ─────────────────────────────────────────────

    private static LessonExercise Exercice(int rang, string texte) =>
        new("m", "l", rang, "practice", "Tape", texte, LessonTypingMode.Flexible);

    private static LessonAttemptStats Reussite(LessonExercise exercice)
    {
        var session = new LessonTypingSession(exercice);
        foreach (char c in exercice.Content) session.TypeChar(c);
        return session.Stats;
    }

    [Fact]
    public void Progression_RelectureRefusee_PuisLibre_LesChangementsExternesArrivent()
    {
        var premier = Exercice(0, "ab");
        var second = Exercice(1, "cd");
        new LessonProgressStore(ProgressPath).RecordSuccess(premier, Reussite(premier));
        var lecteur = new LessonProgressStore(ProgressPath);

        new LessonProgressStore(ProgressPath).RecordSuccess(second, Reussite(second)); // autre écrivain
        File.SetLastWriteTimeUtc(ProgressPath, DateTime.UtcNow.AddMinutes(5));
        using (Verrou(ProgressPath))
            lecteur.DueGestures(5); // relecture refusée : la mémoire reste

        Assert.True(lecteur.IsCompleted(premier));
        Assert.True(lecteur.IsCompleted(second)); // relu au prochain accès, verrou levé
    }

    [Fact]
    public void Progression_PremierChargementRefuse_PuisLibre_LaProgressionArrive()
    {
        var exo = Exercice(0, "ab");
        new LessonProgressStore(ProgressPath).RecordSuccess(exo, Reussite(exo));

        LessonProgressStore lecteur;
        using (Verrou(ProgressPath))
            lecteur = new LessonProgressStore(ProgressPath);

        Assert.True(lecteur.IsCompleted(exo));
    }
}
