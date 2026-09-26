using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Décision pure de la sollicitation par notification et état persistant unique (audit du
/// 25/09, A-02). Les cas de bout en bout, par <c>TrayApplication</c>, sont dans
/// <see cref="SollicitationAvisCaracterisationTests"/> ; ici, les bornes et la photographie.
/// </summary>
public class ReviewPromptDecisionTests : IDisposable
{
    private static readonly DateOnly Jour = new(2026, 10, 15);
    private static readonly DateTime Utc = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _dossier;
    private readonly string _config;

    public ReviewPromptDecisionTests()
    {
        _dossier = Path.Combine(Path.GetTempPath(), "AZGDecision_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dossier);
        _config = Path.Combine(_dossier, "config.json");
        ConfigManager.OverrideConfigPathForTests(_config);
        UsageStats.OverrideStatsPathForTests(Path.Combine(_dossier, "usage-stats.json"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dossier, true); } catch { }
    }

    /// <summary>Essai 1 tout juste permis au fil de la frappe, installation neuve.</summary>
    private static ReviewPromptSignals Nominal(int essais = 0, DateOnly? derniere = null) => new(
        ExternalLinks: true,
        NotificationsEnabled: true,
        State: new ReviewState(essais, derniere, false),
        LearningOpen: false,
        MillisecondsSinceLearningClosed: long.MaxValue,
        MillisecondsSinceLastRemap: 20_000,
        LastErrorUtc: null,
        UtcNow: Utc,
        Today: Jour,
        FirstRemapDate: Jour.AddDays(-30),
        ActiveDaysCount: ReviewPromptGate.SecondActiveDays,
        LastActiveDate: Jour,
        EnrichedChars: UsageStats.EnrichedCharsReviewThreshold,
        ActiveMinutes: ReviewPromptGate.FirstMinActiveMinutes,
        UpgradedWithUsage: false,
        VersionFirstRunDate: Jour,
        TrainingLastReminderDate: null);

    [Fact]
    public void Valeurs_de_la_1_3_0()
    {
        Assert.Equal(2, ReviewPromptGate.MaxAttempts);
        Assert.Equal(ReviewPromptGate.MaxAttempts, ReviewSharePrompt.MaxPrompts);
        Assert.Equal(10, ReviewPromptGate.FirstMinActiveMinutes);
        Assert.Equal(10, ReviewPromptGate.SecondActiveDays);
        Assert.Equal(7, ReviewPromptGate.SecondMinGapDays);
        Assert.Equal(3, ReviewPromptGate.StaleDays);
        Assert.Equal(48, ReviewPromptGate.ErrorCooldownHours);
        Assert.Equal(ReviewPromptGate.ErrorCooldownHours, TrayApplication.ReviewPromptErrorCooldownHours);
    }

    [Fact]
    public void Numero_de_l_essai_qui_part()
    {
        Assert.True(ReviewPromptGate.ShouldShowNotification(ReviewPromptTrigger.QuietTyping, Nominal(), out int premier));
        Assert.Equal(1, premier);

        Assert.True(ReviewPromptGate.ShouldShowNotification(ReviewPromptTrigger.Startup,
            Nominal(essais: 1, derniere: Jour.AddDays(-ReviewPromptGate.SecondMinGapDays)), out int second));
        Assert.Equal(2, second);
    }

    [Fact]
    public void Silence_apres_erreur_borne_exclue()
    {
        var h = TimeSpan.FromHours(ReviewPromptGate.ErrorCooldownHours);
        Assert.False(ReviewPromptGate.InErrorCooldown(null, Utc));
        Assert.True(ReviewPromptGate.InErrorCooldown(Utc - h + TimeSpan.FromTicks(1), Utc));
        Assert.False(ReviewPromptGate.InErrorCooldown(Utc - h, Utc));
    }

    [Fact]
    public void Essais_restants()
    {
        Assert.True(ReviewPromptGate.AttemptsLeft(0, answered: false));
        Assert.True(ReviewPromptGate.AttemptsLeft(1, answered: false));
        Assert.False(ReviewPromptGate.AttemptsLeft(2, answered: false));
        Assert.False(ReviewPromptGate.AttemptsLeft(0, answered: true));
    }

    [Fact]
    public void Essai2_bornes_d_espacement_et_d_absence()
    {
        var sept = Nominal(essais: 1, derniere: Jour.AddDays(-7));
        Assert.True(ReviewPromptGate.ShouldShowNotification(ReviewPromptTrigger.Startup, sept, out _));
        Assert.False(ReviewPromptGate.ShouldShowNotification(ReviewPromptTrigger.Startup,
            sept with { State = sept.State with { LastShown = Jour.AddDays(-6) } }, out _));
        Assert.True(ReviewPromptGate.ShouldShowNotification(ReviewPromptTrigger.Startup,
            sept with { LastActiveDate = Jour.AddDays(-3) }, out _));
        Assert.False(ReviewPromptGate.ShouldShowNotification(ReviewPromptTrigger.Startup,
            sept with { LastActiveDate = Jour.AddDays(-4) }, out _));
    }

    [Fact]
    public void Rappel_du_defi_compare_sur_la_date_ecrite()
    {
        Assert.False(ReviewPromptGate.ShouldShowNotification(ReviewPromptTrigger.QuietTyping,
            Nominal() with { TrainingLastReminderDate = "2026-10-15" }, out _));
        Assert.True(ReviewPromptGate.ShouldShowNotification(ReviewPromptTrigger.QuietTyping,
            Nominal() with { TrainingLastReminderDate = "2026-10-14" }, out _));
    }

    [Fact]
    public void Etat_lu_sous_les_cles_historiques()
    {
        // reviewPromptDone, hérité de la v1.1, n'est pas lu : il ne consomme aucun essai.
        File.WriteAllText(_config,
            """{ "reviewPromptCount": 1, "reviewPromptLastShown": "2026-10-01", "reviewPromptClicked": true, "reviewPromptDone": true }""");
        ConfigManager.OverrideConfigPathForTests(_config);
        Assert.Equal(new ReviewState(1, new DateOnly(2026, 10, 1), true), ReviewState.Load());

        File.WriteAllText(_config, """{ "reviewPromptDone": true }""");
        ConfigManager.OverrideConfigPathForTests(_config);
        Assert.Equal(new ReviewState(0, null, false), ReviewState.Load());
    }

    [Fact]
    public void Photographie_sans_premier_lancement_etabli()
    {
        var s = ReviewPromptGate.Snapshot(null);
        Assert.True(s.UpgradedWithUsage);
        Assert.Null(s.VersionFirstRunDate);

        var neuve = ReviewPromptGate.Snapshot(new ConfigManager.VersionFirstRun("1.3.0", Jour, false));
        Assert.False(neuve.UpgradedWithUsage);
        Assert.Equal(Jour, neuve.VersionFirstRunDate);
    }
}
