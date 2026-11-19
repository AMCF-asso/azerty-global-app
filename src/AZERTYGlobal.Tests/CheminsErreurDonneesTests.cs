using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit des chemins d'erreur, rapport-A : lectures et écritures de fichiers de données qui échouent
/// sur un verrou passager (antivirus, sauvegarde). Chaque test pose son propre dossier temporaire et
/// ses propres chemins ; aucun fichier de l'utilisateur n'est touché.
/// </summary>
public class CheminsErreurDonneesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AZGCE_A_" + Guid.NewGuid().ToString("N"));
    private string ConfigPath => Path.Combine(_dir, "config.json");
    private string ProgressPath => Path.Combine(_dir, "lessons-progress.json");
    private string StatsPath => Path.Combine(_dir, "usage-stats.json");
    private const string ConfigValide = "{\"activationConsent\":true,\"appLanguage\":\"fr\"}";
    private const string StatsOriginal = "{ \"activeDaysCount\": 42, \"bestStreak\": 30 }";

    public CheminsErreurDonneesTests()
    {
        Directory.CreateDirectory(_dir);
        ConfigManager.OverrideConfigPathForTests(ConfigPath);
        UsageStats.OverrideStatsPathForTests(StatsPath);
    }

    public void Dispose()
    {
        UsageStats.OverrideStatsPathForTests(Path.Combine(_dir, "inutilise.json"));
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_dir, "inutilise-config.json"));
        for (int essai = 0; essai < 20; essai++)
        {
            try { Directory.Delete(_dir, true); return; }
            catch (IOException) { Thread.Sleep(25); }
            catch (UnauthorizedAccessException) { Thread.Sleep(25); }
        }
    }

    // ── S-1 (K28) ────────────────────────────────────────────────────────

    /// <summary>Un verrou de 80 ms au démarrage ne doit pas coûter l'accord pour toute la session.</summary>
    // rapport-A S-1, K28
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void ConfigVerrouilleBrievement_LAccordEstRelu()
    {
        using var politique = PolicyManager.OverrideForTests((_, _) => null);
        File.WriteAllText(ConfigPath, ConfigValide);
        ConfigManager.OverrideConfigPathForTests(ConfigPath);

        var verrou = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.None);
        var liberation = Task.Run(async () => { await Task.Delay(80); verrou.Dispose(); });

        bool premier = ConfigManager.ActivationConsent;   // pendant le verrou
        liberation.Wait();
        bool accord = premier || ConfigManager.ActivationConsent; // accès suivant, fichier libre

        Assert.True(accord); // _loadFailed reste vrai : l'accord donné est perdu pour la session
    }

    // ── S-2 (K22) ────────────────────────────────────────────────────────

    // rapport-A S-2, K22
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void LangueDeDemarrage_FichierVerrouilleBrievement_RestePourLaLangueDuFichier()
    {
        using var politique = PolicyManager.OverrideForTests((_, _) => null);
        File.WriteAllText(ConfigPath, "{\"appLanguage\":\"en\"}");
        ConfigManager.OverrideConfigPathForTests(ConfigPath);

        var verrou = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.None);
        var liberation = Task.Run(async () => { await Task.Delay(80); verrou.Dispose(); });
        string auDemarrage = ConfigManager.AppLanguageWithoutLoading();
        liberation.Wait();

        ConfigManager.OverrideConfigPathForTests(ConfigPath); // chargement normal, fichier libre
        Assert.Equal(ConfigManager.AppLanguage, auDemarrage); // "fr" contre "en"
    }

    // ── S-3 (K49) ────────────────────────────────────────────────────────

    private static LessonExercise Exercice(int rang, string texte) =>
        new("m", "l", rang, "practice", "Tape", texte, LessonTypingMode.Flexible);

    private static LessonAttemptStats Reussite(LessonExercise exercice)
    {
        var session = new LessonTypingSession(exercice);
        foreach (char c in exercice.Content) session.TypeChar(c);
        return session.Stats;
    }

    // rapport-A S-3, K49
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void RelectureRefuseeApresChangementExterne_GardeLaProgressionDejaLue()
    {
        var exo = Exercice(0, "ab");
        new LessonProgressStore(ProgressPath).RecordSuccess(exo, Reussite(exo)); // fichier créé
        var lecteur = new LessonProgressStore(ProgressPath);
        Assert.True(lecteur.IsCompleted(exo));

        // Un autre magasin a réécrit le fichier (date changée), puis un verrou le tient.
        File.SetLastWriteTimeUtc(ProgressPath, DateTime.UtcNow.AddMinutes(5));
        using (new FileStream(ProgressPath, FileMode.Open, FileAccess.Read, FileShare.None))
            lecteur.DueGestures(5); // déclenche ReloadIfChangedOnDisk, lecture refusée

        Assert.True(lecteur.IsCompleted(exo)); // Load a vidé _exercises
    }

    // ── S-4 (K46 + K50) ──────────────────────────────────────────────────

    // rapport-A S-4, K46, K50
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void EcritureRefusee_LaReussiteNEstPasEffaceeEtPartiraALaSuivante()
    {
        File.WriteAllText(ProgressPath, "{\"version\":1,\"exercises\":{}}");
        var premier = Exercice(0, "ab");
        var second = Exercice(1, "cd");
        var store = new LessonProgressStore(ProgressPath);

        using (new FileStream(ProgressPath, FileMode.Open, FileAccess.Read, FileShare.None))
            store.RecordSuccess(premier, Reussite(premier)); // File.Replace refusé

        Assert.True(store.IsCompleted(premier)); // rollback : la réussite affichée au bilan disparaît

        store.RecordSuccess(second, Reussite(second)); // verrou levé : écriture
        Assert.True(new LessonProgressStore(ProgressPath).IsCompleted(premier));
    }

    // ── S-5 (K122) ───────────────────────────────────────────────────────

    // rapport-A S-5, K122
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void VerrouBrefAuChargement_LesCompteursDeLaSessionSontEcrits()
    {
        File.WriteAllText(StatsPath, StatsOriginal);
        UsageStats.OverrideStatsPathForTests(StatsPath);

        var verrou = new FileStream(StatsPath, FileMode.Open, FileAccess.Read, FileShare.None);
        var liberation = Task.Run(async () => { await Task.Delay(80); verrou.Dispose(); });
        UsageStats.Preload();
        liberation.Wait();

        UsageStats.RecordEmittedText("É");
        UsageStats.Flush();

        UsageStats.OverrideStatsPathForTests(StatsPath); // lancement suivant
        Assert.Equal(1, UsageStats.AccentedUppercaseCount); // 0 : Flush a sauté l'écriture
        // 42 relus, puis +1 si le jour courant est nouveau : jamais moins que le fichier d'origine.
        Assert.True(UsageStats.ActiveDaysCount >= 42, $"jours actifs relus : {UsageStats.ActiveDaysCount}");
    }

    // ── S-6 = B2 (K125, K98) ─────────────────────────────────────────────

    // rapport-A S-6, K125 ; rapport-B B2, K98
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void VerrouBrefALaFermeture_LesCompteursSontEcritsQuandMeme()
    {
        File.WriteAllText(StatsPath, "{ \"activeDaysCount\": 42 }");
        UsageStats.OverrideStatsPathForTests(StatsPath);
        UsageStats.Preload();
        UsageStats.RecordEmittedText("É");

        var verrou = new FileStream(StatsPath, FileMode.Open, FileAccess.Read, FileShare.None);
        var liberation = Task.Run(async () => { await Task.Delay(80); verrou.Dispose(); });
        // La fermeture (TrayApplication.Cleanup). CE-T4 : elle passait par UsageStats.Flush, un
        // seul essai ; elle passe par FlushFinal, les essais de ConfigManager.FlushFinal. Le test
        // qui suit vérifie que Cleanup l'appelle bien.
        UsageStats.FlushFinal();
        liberation.Wait();

        UsageStats.OverrideStatsPathForTests(StatsPath); // le processus est mort, lancement suivant
        Assert.Equal(1, UsageStats.AccentedUppercaseCount); // 0
    }

    // CE-T4 (CE-12) : le test précédent appelle l'écriture de fermeture directement ; celui-ci
    // lit Cleanup, qui seul décide de ce que la fermeture appelle.
    [Fact]
    public void LaFermetureEcritLesStatistiquesAvecLesNouveauxEssais()
    {
        string source = File.ReadAllText(SourceDeLApp("TrayApplication.cs"));
        Assert.Contains("try { UsageStats.FlushFinal(); }", source);
        Assert.DoesNotContain("try { UsageStats.Flush(); }", source);
    }

    private static string SourceDeLApp(string fichier)
    {
        for (var dossier = new DirectoryInfo(AppContext.BaseDirectory); dossier != null; dossier = dossier.Parent)
        {
            string chemin = Path.Combine(dossier.FullName, "src", fichier);
            if (File.Exists(chemin)) return chemin;
        }
        throw new FileNotFoundException(fichier);
    }
}
