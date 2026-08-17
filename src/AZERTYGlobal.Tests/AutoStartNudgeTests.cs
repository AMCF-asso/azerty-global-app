using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests de la relance unique du lancement automatique (décision 2026-08-17). La règle
/// est pure et prend une photographie de signaux, donc testable sans fenêtre ni tâche
/// de démarrage réelle — même découpage que <c>TrainingReminders.ShouldRemind</c>.
/// Le drapeau de persistance est vérifié à part, sur un config.json isolé.
/// </summary>
public class AutoStartNudgeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public AutoStartNudgeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
        ConfigManager.OverrideConfigPathForTests(_configPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    /// <summary>Cas nominal : l'utilisateur est revenu par ses propres moyens, sans autostart.</summary>
    private static AutoStartSignals Nominal(
        bool nudgeDone = false,
        bool alreadyRegistered = false,
        bool notificationsEnabled = true,
        bool hasRemapped = true,
        int activeDaysCount = AutoStartNudge.MinActiveDays) =>
        new(nudgeDone,
            alreadyRegistered,
            notificationsEnabled,
            hasRemapped ? new DateOnly(2026, 8, 10) : null,
            activeDaysCount);

    [Fact]
    public void ShouldPrompt_ReturnedUserWithoutAutoStart_ReturnsTrue()
    {
        Assert.True(AutoStartNudge.ShouldPrompt(Nominal()));
    }

    [Fact]
    public void ShouldPrompt_AlreadyPrompted_ReturnsFalse()
    {
        Assert.False(AutoStartNudge.ShouldPrompt(Nominal(nudgeDone: true)));
    }

    [Fact]
    public void ShouldPrompt_AutoStartAlreadyRegistered_ReturnsFalse()
    {
        Assert.False(AutoStartNudge.ShouldPrompt(Nominal(alreadyRegistered: true)));
    }

    /// <summary>Notifications coupées : le canal n'existe pas. L'entrée du menu de la zone
    /// de notification reste le chemin d'activation pour ces utilisateurs.</summary>
    [Fact]
    public void ShouldPrompt_NotificationsDisabled_ReturnsFalse()
    {
        Assert.False(AutoStartNudge.ShouldPrompt(Nominal(notificationsEnabled: false)));
    }

    /// <summary>Sans première frappe remappée, l'application n'a jamais servi : proposer
    /// de la lancer au démarrage n'aurait rien à quoi s'ancrer.</summary>
    [Fact]
    public void ShouldPrompt_NoRemappedKeystrokeYet_ReturnsFalse()
    {
        Assert.False(AutoStartNudge.ShouldPrompt(Nominal(hasRemapped: false)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ShouldPrompt_BelowMinimumActiveDays_ReturnsFalse(int activeDays)
    {
        Assert.False(AutoStartNudge.ShouldPrompt(Nominal(activeDaysCount: activeDays)));
    }

    /// <summary>Le seuil est bien un plancher inclusif, et pas seulement l'égalité.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(17)]
    public void ShouldPrompt_AtOrAboveMinimumActiveDays_ReturnsTrue(int activeDays)
    {
        Assert.True(AutoStartNudge.ShouldPrompt(Nominal(activeDaysCount: activeDays)));
    }

    [Fact]
    public void AutoStartNudgeDone_DefaultsToFalse()
    {
        Assert.False(ConfigManager.AutoStartNudgeDone);
    }

    [Fact]
    public void MarkPromptShown_PersistsAndSuppressesFurtherPrompts()
    {
        AutoStartNudge.MarkPromptShown();

        Assert.True(ConfigManager.AutoStartNudgeDone);
        Assert.Contains("autoStartNudgeDone", File.ReadAllText(_configPath));
        Assert.False(AutoStartNudge.ShouldPrompt(Nominal(nudgeDone: ConfigManager.AutoStartNudgeDone)));
    }

    /// <summary>Le seuil documenté ne doit pas dériver en silence : la relance vise
    /// l'utilisateur revenu au moins une fois, donc deux jours d'usage distincts.</summary>
    [Fact]
    public void MinActiveDays_IsTwo()
    {
        Assert.Equal(2, AutoStartNudge.MinActiveDays);
    }
}
