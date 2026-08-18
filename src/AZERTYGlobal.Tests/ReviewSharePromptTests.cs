using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests de la sollicitation d'avis déclenchée par un partage (R1 et R3 de l'audit
/// v1.2.0, corrigés le 2026-08-18). La règle est pure et prend une photographie de
/// signaux, donc testable sans fenêtre, sans Store et sans horloge réelle — même
/// découpage que <c>AutoStartNudge.ShouldPrompt</c>.
///
/// Le témoin qui compte est <c>Snapshot_ApresRedemarrage_PorteEncoreLaDatePersistee</c> :
/// c'est le scénario exact que l'ancienne garde ne voyait pas, et il s'appuie sur un
/// config.json isolé.
/// </summary>
public class ReviewSharePromptTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 8, 18);
    private static readonly DateTime Utc = new(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _tempDir;
    private readonly string _configPath;

    public ReviewSharePromptTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
        ConfigManager.OverrideConfigPathForTests(_configPath);
        UsageStats.OverrideStatsPathForTests(Path.Combine(_tempDir, "usage-stats.json"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    /// <summary>Cas nominal : l'utilisateur a servi, il est revenu, il vient de partager.</summary>
    private static ReviewSharePromptSignals Nominal(
        bool isPackaged = true,
        bool promptClicked = false,
        int promptCount = 0,
        DateOnly? promptLastShown = null,
        DateTime? lastErrorUtc = null,
        bool hasRemapped = true,
        int activeDaysCount = ReviewSharePrompt.MinActiveDays) =>
        new(isPackaged,
            promptClicked,
            promptCount,
            promptLastShown,
            lastErrorUtc,
            Utc,
            Today,
            hasRemapped ? Today.AddDays(-5) : null,
            activeDaysCount);

    [Fact]
    public void ShouldPrompt_PartageApresDeuxJoursDUsage_ReturnsTrue()
    {
        Assert.True(ReviewSharePrompt.ShouldPrompt(Nominal()));
    }

    [Fact]
    public void ShouldPrompt_HorsPackage_ReturnsFalse()
    {
        Assert.False(ReviewSharePrompt.ShouldPrompt(Nominal(isPackaged: false)));
    }

    [Fact]
    public void ShouldPrompt_SollicitationDejaCliquee_ReturnsFalse()
    {
        Assert.False(ReviewSharePrompt.ShouldPrompt(Nominal(promptClicked: true)));
    }

    [Fact]
    public void ShouldPrompt_DeuxEssaisConsommes_ReturnsFalse()
    {
        Assert.False(ReviewSharePrompt.ShouldPrompt(
            Nominal(promptCount: ReviewSharePrompt.MaxPrompts)));
    }

    [Fact]
    public void ShouldPrompt_UnSeulEssaiConsomme_ReturnsTrue()
    {
        Assert.True(ReviewSharePrompt.ShouldPrompt(Nominal(promptCount: 1)));
    }

    [Fact]
    public void ShouldPrompt_ErreurPendantLeSilence_ReturnsFalse()
    {
        int cooldown = TrayApplication.ReviewPromptErrorCooldownHours;
        Assert.False(ReviewSharePrompt.ShouldPrompt(
            Nominal(lastErrorUtc: Utc.AddHours(-(cooldown - 1)))));
    }

    [Fact]
    public void ShouldPrompt_ErreurPlusVieilleQueLeSilence_ReturnsTrue()
    {
        int cooldown = TrayApplication.ReviewPromptErrorCooldownHours;
        Assert.True(ReviewSharePrompt.ShouldPrompt(
            Nominal(lastErrorUtc: Utc.AddHours(-(cooldown + 1)))));
    }

    /// <summary>R1 : une sollicitation déjà affichée aujourd'hui ferme la journée.</summary>
    [Fact]
    public void ShouldPrompt_DejaSolliciteLeMemeJour_ReturnsFalse()
    {
        Assert.False(ReviewSharePrompt.ShouldPrompt(Nominal(promptLastShown: Today)));
    }

    /// <summary>R1, réciproque : la veille ne bloque pas, sinon la garde serait un mur.</summary>
    [Fact]
    public void ShouldPrompt_SolliciteLaVeille_ReturnsTrue()
    {
        Assert.True(ReviewSharePrompt.ShouldPrompt(
            Nominal(promptLastShown: Today.AddDays(-1))));
    }

    /// <summary>R3 : le partage du jour 1 ne sollicite plus. C'est le cas que l'audit a
    /// relevé — Défi du jour terminé et « Copier mon résultat » dans l'heure suivant
    /// l'installation ouvraient la boîte de notation.</summary>
    [Fact]
    public void ShouldPrompt_PremierJourDUsage_ReturnsFalse()
    {
        Assert.False(ReviewSharePrompt.ShouldPrompt(Nominal(activeDaysCount: 1)));
    }

    /// <summary>R3 : le seuil est atteint, pas dépassé — il doit passer.</summary>
    [Fact]
    public void ShouldPrompt_AuSeuilExactDeJoursActifs_ReturnsTrue()
    {
        Assert.True(ReviewSharePrompt.ShouldPrompt(
            Nominal(activeDaysCount: ReviewSharePrompt.MinActiveDays)));
        Assert.False(ReviewSharePrompt.ShouldPrompt(
            Nominal(activeDaysCount: ReviewSharePrompt.MinActiveDays - 1)));
    }

    /// <summary>R3 : sans frappe remappée, l'application n'a jamais servi.</summary>
    [Fact]
    public void ShouldPrompt_SansFrappeRemappee_ReturnsFalse()
    {
        Assert.False(ReviewSharePrompt.ShouldPrompt(Nominal(hasRemapped: false)));
    }

    /// <summary>
    /// Témoin de R1. On enregistre une sollicitation, puis on simule un redémarrage du
    /// processus en vidant le cache de ConfigManager : la photographie doit encore porter
    /// la date du jour, et la décision doit rester non. Avec le champ d'instance de
    /// TrayApplication, cette date était perdue au redémarrage et un second partage le
    /// même jour repassait, brûlant les deux essais de la vie de l'installation.
    /// </summary>
    [Fact]
    public void Snapshot_ApresRedemarrage_PorteEncoreLaDatePersistee()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        ConfigManager.RecordReviewPromptShown(today);

        // Redémarrage : le cache est vidé, tout est relu depuis le disque.
        ConfigManager.OverrideConfigPathForTests(_configPath);

        var signals = ReviewSharePrompt.Snapshot();
        Assert.Equal(today, signals.PromptLastShown);
        Assert.Equal(today, signals.Today);

        // Tous les signaux d'environnement sont épinglés : seuls PromptLastShown et Today
        // viennent encore de la photographie réelle, et c'est bien eux que ce test éprouve.
        // LastErrorUtc l'exige — c'est un statique de processus (ConfigManager.cs:583) que
        // rien ne remet à zéro entre les tests, si bien qu'une erreur journalisée par une
        // autre classe de la suite armait le silence de 48 h et faisait dépendre ce test de
        // l'ordre d'exécution. Constaté le 2026-08-18 : vert en isolation, rouge dans la
        // suite entière, attrapé par l'assertion réciproque ci-dessous.
        var pinned = signals with
        {
            IsPackaged = true,
            PromptClicked = false,
            PromptCount = 0,
            LastErrorUtc = null,
            FirstRemapDate = today.AddDays(-5),
            ActiveDaysCount = ReviewSharePrompt.MinActiveDays,
        };
        Assert.False(ReviewSharePrompt.ShouldPrompt(pinned));

        // Réciproque : la même photographie datée de la veille doit passer, sinon le test
        // ci-dessus serait vert pour une autre raison que la date.
        Assert.True(ReviewSharePrompt.ShouldPrompt(
            pinned with { PromptLastShown = today.AddDays(-1) }));
    }
}
