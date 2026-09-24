using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Essai 1 de la sollicitation d'avis, audit du 24/09 (décision d'Antoine du 2026-09-24).
///
/// Un utilisateur actif de la 1.1 dépasse déjà les seuils (20 caractères enrichis,
/// 10 minutes) et son compteur d'essais repart à 0 : au premier lancement de la 1.3, la
/// demande partait dès la fermeture de l'accueil, sans une frappe dans cette version.
/// Désormais : l'essai 1 ne part plus que du signal de frappe (seuil puis silence) ; une
/// installation qui avait déjà des statistiques au premier lancement de cette version
/// attend en plus le lendemain ; une installation neuve garde la règle d'avant.
///
/// ⚠️ Limite assumée, comme <see cref="ReviewSignalConsumptionTests"/> : ces témoins
/// tiennent les décisions (<see cref="ReviewPromptGate"/>), la persistance de la date et
/// l'armement du signal. Le câblage dans <c>TrayApplication</c> (timer, appel au
/// démarrage) reste du Win32, prouvé par la recette.
/// </summary>
public class ReviewPromptFirstAttemptTests : IDisposable
{
    private const long Silence = 15_000; // REVIEW_QUIET_SILENCE_MS de TrayApplication
    private static readonly DateOnly JourDeMiseAJour = new(2026, 10, 1);

    private readonly string _tempDir;
    private readonly string _configPath;
    private readonly string _statsPath;

    public ReviewPromptFirstAttemptTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGEssai1_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
        _statsPath = Path.Combine(_tempDir, "usage-stats.json");
        ConfigManager.OverrideConfigPathForTests(_configPath);
        UsageStats.OverrideStatsPathForTests(_statsPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    /// <summary>Statistiques d'un utilisateur 1.1 actif : très au-dessus des deux seuils.</summary>
    private void EcrireStatistiquesV11()
    {
        File.WriteAllText(_statsPath,
            """
            {
              "firstRemapDate": "2026-03-01",
              "lastActiveDate": "2026-09-30",
              "activeDaysCount": 120,
              "totalActiveMinutes": 4000,
              "accentedUppercaseCount": 500,
              "frenchTypographyCount": 300,
              "internationalCount": 0,
              "symbolsCount": 0
            }
            """);
        UsageStats.OverrideStatsPathForTests(_statsPath); // force le rechargement
    }

    // ── 1. Jamais depuis le démarrage ────────────────────────────────

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 30)]
    public void Essai1_jamais_depuis_le_demarrage(bool migree, int joursApresMiseAJour)
    {
        // Le démarrage et la fermeture de l'accueil (son relais) passent le même
        // déclencheur : ni l'un ni l'autre ne peut plus lancer l'essai 1, migré ou non.
        var today = JourDeMiseAJour.AddDays(joursApresMiseAJour);
        Assert.False(ReviewPromptGate.FirstAttemptAllowed(
            ReviewPromptTrigger.Startup, today, migree, JourDeMiseAJour));
    }

    [Fact]
    public void Signal_arme_au_chargement_ne_part_pas_sans_frappe_dans_la_session()
    {
        // Le trou que le nouveau chemin aurait ouvert : MillisecondsSinceLastRemap vaut
        // long.MaxValue avant toute frappe, un « silence infini » qui passerait le seuil.
        EcrireStatistiquesV11();
        Assert.True(ReviewPromptGate.ShouldArmSignalAtLoad(0, true,
            UsageStats.TotalSpecialCharsCount, UsageStats.EnrichedCharsReviewThreshold));
        UsageStats.ArmEnrichedThresholdSignal();

        Assert.True(UsageStats.EnrichedThresholdCrossed);
        Assert.Equal(long.MaxValue, UsageStats.MillisecondsSinceLastRemap);
        Assert.False(ReviewPromptGate.ShouldTryAfterQuietTyping(UsageStats.EnrichedThresholdCrossed,
            UsageStats.MillisecondsSinceLastRemap, Silence, reviewDeferred: false));
    }

    // ── 2. Installation migrée, le jour de la mise à jour ─────────────

    [Fact]
    public void Migree_le_jour_de_la_mise_a_jour_pas_de_demande()
    {
        EcrireStatistiquesV11();
        var premier = ConfigManager.EnsureCurrentVersionFirstRun("1.3.0", JourDeMiseAJour,
            hadUsageBefore: UsageStats.FirstRemapDate != null);
        Assert.True(premier.UpgradedWithUsage);

        // Frappe puis silence : le signal de frappe laisse passer...
        Assert.True(ReviewPromptGate.ShouldTryAfterQuietTyping(true, 20_000, Silence, reviewDeferred: false));
        // ... mais la porte de l'essai 1 refuse le jour même.
        Assert.False(ReviewPromptGate.FirstAttemptAllowed(ReviewPromptTrigger.QuietTyping,
            JourDeMiseAJour, premier.UpgradedWithUsage, premier.Date));
    }

    // ── 3. Installation migrée, le lendemain, après frappe et silence ─

    [Fact]
    public void Migree_le_lendemain_apres_frappe_et_silence_demande()
    {
        EcrireStatistiquesV11();
        ConfigManager.EnsureCurrentVersionFirstRun("1.3.0", JourDeMiseAJour,
            hadUsageBefore: UsageStats.FirstRemapDate != null);

        // Lendemain : nouveau lancement, la date du premier lancement ne bouge pas.
        ConfigManager.OverrideConfigPathForTests(_configPath);
        var relu = ConfigManager.EnsureCurrentVersionFirstRun("1.3.0", JourDeMiseAJour.AddDays(1),
            hadUsageBefore: true);
        Assert.Equal(JourDeMiseAJour, relu.Date);

        // Déjà au-dessus du seuil au chargement : le signal est armé, sans transition.
        UsageStats.OverrideStatsPathForTests(_statsPath);
        Assert.False(UsageStats.EnrichedThresholdCrossed);
        if (ReviewPromptGate.ShouldArmSignalAtLoad(ConfigManager.ReviewPromptCount, true,
                UsageStats.TotalSpecialCharsCount, UsageStats.EnrichedCharsReviewThreshold))
            UsageStats.ArmEnrichedThresholdSignal();
        Assert.True(UsageStats.EnrichedThresholdCrossed);

        // Une vraie frappe : on attend le silence (encore en train de taper)...
        UsageStats.RecordEmittedText("É");
        Assert.False(ReviewPromptGate.ShouldTryAfterQuietTyping(UsageStats.EnrichedThresholdCrossed,
            UsageStats.MillisecondsSinceLastRemap, Silence, reviewDeferred: false));
        // ... puis 15 s de silence : la tentative part, et la porte de l'essai 1 l'autorise.
        Assert.True(ReviewPromptGate.ShouldTryAfterQuietTyping(UsageStats.EnrichedThresholdCrossed,
            20_000, Silence, reviewDeferred: false));
        Assert.True(ReviewPromptGate.FirstAttemptAllowed(ReviewPromptTrigger.QuietTyping,
            JourDeMiseAJour.AddDays(1), relu.UpgradedWithUsage, relu.Date));
    }

    // ── 4. Installation neuve : règle inchangée ───────────────────────

    [Fact]
    public void Neuve_regle_inchangee_sans_delai_d_un_jour()
    {
        // Aucune statistique au premier lancement : pas une mise à jour.
        var premier = ConfigManager.EnsureCurrentVersionFirstRun("1.3.0", JourDeMiseAJour,
            hadUsageBefore: UsageStats.FirstRemapDate != null);
        Assert.False(premier.UpgradedWithUsage);

        // Le jour même, au fil de la frappe : autorisé, comme avant l'audit.
        Assert.True(ReviewPromptGate.FirstAttemptAllowed(ReviewPromptTrigger.QuietTyping,
            JourDeMiseAJour, premier.UpgradedWithUsage, premier.Date));

        // Le seuil franchi dans la session arme toujours le signal par transition.
        for (int i = 0; i < UsageStats.EnrichedCharsReviewThreshold; i++)
            UsageStats.RecordEmittedText("É");
        Assert.True(UsageStats.EnrichedThresholdCrossed);
    }

    [Fact]
    public void Migree_sans_date_etablie_est_refusee()
    {
        // Date absente (écriture impossible) : pas de preuve du jour écoulé, refus.
        Assert.False(ReviewPromptGate.FirstAttemptAllowed(ReviewPromptTrigger.QuietTyping,
            JourDeMiseAJour.AddDays(5), upgradedWithUsage: true, versionFirstRunDate: null));
    }

    // ── Armement au chargement : seulement pour l'essai 1 ─────────────

    [Theory]
    [InlineData(1, true, 500)]   // essai 1 fait : le second garde son chemin de démarrage
    [InlineData(0, false, 500)]  // plus aucune sollicitation possible
    [InlineData(0, true, 19)]    // sous le seuil : la transition s'en chargera
    public void Pas_d_armement_hors_essai_1_ou_sous_le_seuil(int essais, bool possible, long total)
    {
        Assert.False(ReviewPromptGate.ShouldArmSignalAtLoad(essais, possible, total,
            UsageStats.EnrichedCharsReviewThreshold));
    }

    [Fact]
    public void Accueil_du_demarrage_ouvert_differe_la_tentative()
    {
        Assert.False(ReviewPromptGate.ShouldTryAfterQuietTyping(true, 20_000, Silence, reviewDeferred: true));
        Assert.True(ReviewPromptGate.ShouldTryAfterQuietTyping(true, 20_000, Silence, reviewDeferred: false));
    }

    // ── Persistance de la date du premier lancement de la version ─────

    [Fact]
    public void Date_ecrite_une_fois_par_version_et_relue_apres_redemarrage()
    {
        var premier = ConfigManager.EnsureCurrentVersionFirstRun("1.3.0", JourDeMiseAJour, hadUsageBefore: true);
        var json = File.ReadAllText(_configPath);
        Assert.Contains("currentVersionFirstRunDate", json);
        Assert.Contains("currentVersionFirstRunVersion", json);
        Assert.Contains("\"2026-10-01\"", json);

        // Relancements suivants : ni la date ni le constat de migration ne bougent, même si
        // des statistiques existent désormais pour une installation neuve, ou l'inverse.
        ConfigManager.OverrideConfigPathForTests(_configPath);
        var relu = ConfigManager.EnsureCurrentVersionFirstRun("1.3.0", JourDeMiseAJour.AddDays(3), hadUsageBefore: false);
        Assert.Equal(premier, relu);
    }

    [Fact]
    public void Nouvelle_version_reecrit_la_date_et_le_constat()
    {
        ConfigManager.EnsureCurrentVersionFirstRun("1.3.0", JourDeMiseAJour, hadUsageBefore: false);
        var suivante = ConfigManager.EnsureCurrentVersionFirstRun("1.3.1", JourDeMiseAJour.AddDays(20), hadUsageBefore: true);

        Assert.Equal("1.3.1", suivante.Version);
        Assert.Equal(JourDeMiseAJour.AddDays(20), suivante.Date);
        Assert.True(suivante.UpgradedWithUsage);
    }

    [Fact]
    public void Date_illisible_est_reecrite()
    {
        File.WriteAllText(_configPath,
            """{ "currentVersionFirstRunVersion": "1.3.0", "currentVersionFirstRunDate": "hier", "currentVersionUpgradedWithUsage": true }""");
        ConfigManager.OverrideConfigPathForTests(_configPath);

        var repare = ConfigManager.EnsureCurrentVersionFirstRun("1.3.0", JourDeMiseAJour, hadUsageBefore: false);
        Assert.Equal(JourDeMiseAJour, repare.Date);
        Assert.False(repare.UpgradedWithUsage);
    }
}
