using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 25/09 — L-14 (progression des Leçons) et report du 24/09 (config.json) : un
/// fichier illisible au chargement bloquait toute sauvegarde, en silence. Pour config.json,
/// l'accord n'était jamais mémorisé et l'application restait inactive à chaque démarrage.
///
/// Même distinction que le correctif A-11 de UsageStats :
/// - lecture impossible (verrou, droits) : fichier sans doute intact, aucune écriture pendant
///   la session, nouvel essai au lancement suivant ;
/// - contenu corrompu : le fichier est renommé « nom.illisible-aaaammjj-hhmmss » à côté de
///   l'original, jamais supprimé ; on repart de zéro, les sauvegardes reprennent, et un
///   message est demandé une seule fois.
///
/// Chaque témoin porte sa mutation dans le compte rendu du lot 4.
/// </summary>
public class QuarantaineTests : IDisposable
{
    private static readonly DateTime Instant = new(2026, 9, 25, 21, 30, 5);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AZGQUAR_" + Guid.NewGuid().ToString("N"));
    private string ConfigPath => Path.Combine(_dir, "config.json");
    private string ProgressPath => Path.Combine(_dir, "lessons-progress.json");

    private const string ConfigValide = "{\"activationConsent\":true,\"appLanguage\":\"fr\",\"notificationsEnabled\":true}";
    private const string ConfigCorrompu = "{\"activationConsent\":true,\"appLanguage\":\"fr\",";
    private const string ProgressionCorrompue = "{ invalid";
    private const string ProgressionValide = "{\"version\":1,\"exercises\":{}}";

    public QuarantaineTests()
    {
        Directory.CreateDirectory(_dir);
        ConfigManager.OverrideConfigPathForTests(ConfigPath);
        LessonProgressStore.TakeQuarantineNotice(); // purge un message laissé par un autre test
    }

    public void Dispose()
    {
        FileQuarantine.Clock = () => DateTime.Now;
        // Le journal asynchrone peut finir une écriture pendant le nettoyage.
        for (int essai = 0; essai < 20; essai++)
        {
            try { Directory.Delete(_dir, true); return; }
            catch (IOException) { Thread.Sleep(25); }
            catch (UnauthorizedAccessException) { Thread.Sleep(25); }
        }
    }

    private string[] Copies(string nom) => Directory.GetFiles(_dir, nom + FileQuarantine.Marker + "*");

    private static void Recharger(string path) => ConfigManager.OverrideConfigPathForTests(path);

    private static LessonExercise Exercice() =>
        new("m", "l", 0, "practice", "Tape", "ab", LessonTypingMode.Flexible);

    private static LessonAttemptStats Réussite(LessonExercise exercice)
    {
        var session = new LessonTypingSession(exercice);
        session.TypeChar('a');
        session.TypeChar('b');
        return session.Stats;
    }

    // ═══════════════════════════════════════════════════════════════
    // Nom de la copie, et jamais d'écrasement
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void NomDeLaCopie_ÀCôtéDeLOriginal_HorodatéÀLaSeconde()
    {
        Assert.Equal(Path.Combine(_dir, "config.json.illisible-20260925-213005"),
            FileQuarantine.BuildPath(ConfigPath, Instant));
    }

    /// <summary>Une copie déjà là (deux mises de côté dans la même seconde) n'est jamais
    /// écrasée : le renommage échoue, et l'original reste en place.</summary>
    [Fact]
    public void MiseDeCôté_NÉcraseJamaisUneCopieExistante()
    {
        FileQuarantine.Clock = () => Instant;
        File.WriteAllText(ConfigPath, "original");
        File.WriteAllText(FileQuarantine.BuildPath(ConfigPath, Instant), "copie précédente");

        Assert.Null(FileQuarantine.TryMoveAside(ConfigPath, "test"));

        Assert.Equal("original", File.ReadAllText(ConfigPath));
        Assert.Equal("copie précédente", File.ReadAllText(FileQuarantine.BuildPath(ConfigPath, Instant)));
    }

    // ═══════════════════════════════════════════════════════════════
    // config.json corrompu
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ConfigCorrompu_MisDeCôté_SousLeNomAttendu()
    {
        FileQuarantine.Clock = () => Instant;
        File.WriteAllText(ConfigPath, ConfigCorrompu);
        Recharger(ConfigPath);

        _ = ConfigManager.ActivationConsent;

        Assert.False(File.Exists(ConfigPath));
        Assert.Equal(ConfigCorrompu, File.ReadAllText(FileQuarantine.BuildPath(ConfigPath, Instant)));
    }

    /// <summary>
    /// Le report du 24/09 : l'accord n'était jamais mémorisé. Après la mise de côté, rien ne
    /// s'active sans accord (le texte corrompu portait pourtant « activationConsent »),
    /// l'accueil revient, puis l'accord donné se garde et survit au redémarrage.
    /// </summary>
    [Fact]
    public void ConfigCorrompu_AccueilPuisAccordMémorisé()
    {
        using var politique = PolicyManager.OverrideForTests((_, _) => null);
        File.WriteAllText(ConfigPath, ConfigCorrompu);
        Recharger(ConfigPath);

        Assert.False(ConfigManager.ActivationConsent);
        Assert.True(ConfigManager.ActivationPromptAtStartup);

        ConfigManager.AcceptActivation();
        Assert.True(ConfigManager.ActivationConsent);
        ConfigManager.Flush();
        Recharger(ConfigPath);

        Assert.True(ConfigManager.ActivationConsent);
        Assert.Equal(ConfigCorrompu, File.ReadAllText(Assert.Single(Copies("config.json"))));
    }

    [Fact]
    public void ConfigCorrompu_MessageDemandéUneSeuleFois()
    {
        File.WriteAllText(ConfigPath, ConfigCorrompu);
        Recharger(ConfigPath);

        Assert.True(ConfigManager.TakeQuarantineNotice());
        Assert.False(ConfigManager.TakeQuarantineNotice());

        ConfigManager.SetNotifications(false);
        ConfigManager.Flush();
        Recharger(ConfigPath); // lancement suivant : le fichier neuf se lit
        Assert.False(ConfigManager.TakeQuarantineNotice());
    }

    /// <summary>Contrôle : un config.json lisible ne demande aucun message.</summary>
    [Fact]
    public void ConfigLisible_AucunMessage()
    {
        File.WriteAllText(ConfigPath, ConfigValide);
        Recharger(ConfigPath);

        Assert.True(ConfigManager.ActivationConsent);
        Assert.False(ConfigManager.TakeQuarantineNotice());
        Assert.Empty(Copies("config.json"));
    }

    /// <summary>
    /// Lecture impossible (violation de partage) : ni copie, ni écriture pendant la session,
    /// ni message ; le lancement suivant relit le fichier intact.
    /// </summary>
    [Fact]
    public void ConfigVerrouillé_NiQuarantaineNiÉcriture()
    {
        File.WriteAllText(ConfigPath, ConfigValide);
        Recharger(ConfigPath);
        using (new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.False(ConfigManager.ActivationConsent); // lecture refusée : pas d'accord pour la session

        ConfigManager.SetNotifications(false);
        ConfigManager.Flush();

        Assert.Equal(ConfigValide, File.ReadAllText(ConfigPath));
        Assert.Empty(Copies("config.json"));
        Assert.False(ConfigManager.TakeQuarantineNotice());
        Recharger(ConfigPath);
        Assert.True(ConfigManager.ActivationConsent);
    }

    /// <summary>Renommage impossible (une copie de la même seconde existe déjà) : même règle
    /// qu'une lecture impossible, le fichier corrompu n'est pas écrasé.</summary>
    [Fact]
    public void ConfigRenommageImpossible_AucuneÉcriture()
    {
        FileQuarantine.Clock = () => Instant;
        File.WriteAllText(FileQuarantine.BuildPath(ConfigPath, Instant), "copie précédente");
        File.WriteAllText(ConfigPath, ConfigCorrompu);
        Recharger(ConfigPath);

        ConfigManager.SetNotifications(false);
        ConfigManager.Flush();

        Assert.Equal(ConfigCorrompu, File.ReadAllText(ConfigPath));
        Assert.False(ConfigManager.TakeQuarantineNotice());
    }

    // ═══════════════════════════════════════════════════════════════
    // Progression des Leçons corrompue (L-14)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ProgressionCorrompue_MiseDeCôté_SousLeNomAttendu()
    {
        FileQuarantine.Clock = () => Instant;
        File.WriteAllText(ProgressPath, ProgressionCorrompue);

        var store = new LessonProgressStore(ProgressPath);

        Assert.Equal(FileQuarantine.BuildPath(ProgressPath, Instant), store.QuarantinedPath);
        Assert.Equal(ProgressionCorrompue, File.ReadAllText(store.QuarantinedPath!));
        Assert.False(File.Exists(ProgressPath));
    }

    [Fact]
    public void ProgressionCorrompue_MessageDemandéUneSeuleFois()
    {
        File.WriteAllText(ProgressPath, ProgressionCorrompue);

        var store = new LessonProgressStore(ProgressPath);
        store.RecordSuccess(Exercice(), Réussite(Exercice()));

        Assert.True(LessonProgressStore.TakeQuarantineNotice());
        Assert.False(LessonProgressStore.TakeQuarantineNotice());
        _ = new LessonProgressStore(ProgressPath); // réouverture des Leçons : fichier neuf lisible
        Assert.False(LessonProgressStore.TakeQuarantineNotice());
    }

    [Fact]
    public void ProgressionVerrouillée_NiQuarantaineNiÉcriture()
    {
        File.WriteAllText(ProgressPath, ProgressionValide);
        LessonProgressStore store;
        using (new FileStream(ProgressPath, FileMode.Open, FileAccess.Read, FileShare.None))
            store = new LessonProgressStore(ProgressPath);

        store.RecordSuccess(Exercice(), Réussite(Exercice()));

        Assert.False(store.IsCompleted(Exercice())); // retour arrière en mémoire, comme avant
        Assert.Equal(ProgressionValide, File.ReadAllText(ProgressPath));
        Assert.Null(store.QuarantinedPath);
        Assert.Empty(Copies("lessons-progress.json"));
        Assert.False(LessonProgressStore.TakeQuarantineNotice());
    }

    [Fact]
    public void ProgressionRenommageImpossible_AucuneÉcriture()
    {
        FileQuarantine.Clock = () => Instant;
        File.WriteAllText(FileQuarantine.BuildPath(ProgressPath, Instant), "copie précédente");
        File.WriteAllText(ProgressPath, ProgressionCorrompue);

        var store = new LessonProgressStore(ProgressPath);
        store.RecordSuccess(Exercice(), Réussite(Exercice()));

        Assert.False(store.IsCompleted(Exercice()));
        Assert.Equal(ProgressionCorrompue, File.ReadAllText(ProgressPath));
        Assert.False(LessonProgressStore.TakeQuarantineNotice());
    }

    // ═══════════════════════════════════════════════════════════════
    // Textes : gabarit des notifications, en français et en anglais
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Titre = état, sans le nom du produit ; corps = ce qui s'est passé et ce qui suit, avec
    /// le nom du produit une seule fois, 120 caractères par ligne au plus, et la copie gardée.
    /// </summary>
    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void MessagesDeQuarantaine_SuiventLeGabarit(string langue)
    {
        L.Language = langue;
        try
        {
            foreach (var (titre, corps) in new[]
                     {
                         (L.Tray_SettingsResetTitle, L.Tray_SettingsResetBody),
                         (L.Tray_ProgressResetTitle, L.Tray_ProgressResetBody),
                     })
            {
                Assert.DoesNotContain(ProductIdentity.DisplayName, titre);
                Assert.Single(System.Text.RegularExpressions.Regex.Matches(corps, ProductIdentity.DisplayName));
                Assert.All(corps.Split('\n'), ligne => Assert.InRange(ligne.Length, 1, 120));
                Assert.Contains(langue == "fr" ? "copie" : "copy", corps);
            }
        }
        finally
        {
            L.Language = "fr";
        }
    }
}
