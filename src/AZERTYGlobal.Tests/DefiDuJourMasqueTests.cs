using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Défi du jour masqué en 1.3.0 (décision d'Antoine du 2026-09-24) : il revient, revu, en
/// 1.4.0. Un seul interrupteur, <see cref="DailyChallenge.Enabled"/>, ferme toutes les
/// portes d'entrée ; ces témoins tiennent les décisions pures qui en dépendent.
///
/// ⚠️ Limite assumée : les fenêtres (case d'opt-in de l'accueil et des Paramètres, section
/// de « Mes statistiques », menu réellement affiché) restent du Win32, prouvé par la recette.
/// </summary>
public class DefiDuJourMasqueTests : IDisposable
{
    private readonly string _tempDir;

    public DefiDuJourMasqueTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGDefiMasque_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_tempDir, "config.json"));
        UsageStats.OverrideStatsPathForTests(Path.Combine(_tempDir, "usage-stats.json"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    /// <summary>⛔ À retourner en 1.4.0, et seulement là : c'est l'état livré de la 1.3.0.</summary>
    [Fact]
    public void L_interrupteur_est_coupe_en_1_3_0()
    {
        Assert.False(DailyChallenge.Enabled);
    }

    [Fact]
    public void Le_menu_Apprendre_garde_Lecons_et_accueil_sans_le_Defi()
    {
        Assert.Equal(
            new[] { TrayApplication.IDM_EXERCISES, TrayApplication.IDM_ONBOARDING },
            TrayApplication.LearnMenuEntries(challengeEnabled: false));
    }

    [Fact]
    public void Le_menu_construit_avec_l_interrupteur_reel_n_a_pas_d_entree_Defi()
    {
        Assert.DoesNotContain(TrayApplication.IDM_CHALLENGE,
            TrayApplication.LearnMenuEntries(DailyChallenge.Enabled));
    }

    /// <summary>Témoin du retour en 1.4.0 : le Défi reprend sa place entre les deux.</summary>
    [Fact]
    public void Interrupteur_leve_le_Defi_revient_entre_Lecons_et_accueil()
    {
        Assert.Equal(
            new[] { TrayApplication.IDM_EXERCISES, TrayApplication.IDM_CHALLENGE, TrayApplication.IDM_ONBOARDING },
            TrayApplication.LearnMenuEntries(challengeEnabled: true));
    }

    [Fact]
    public void L_annonce_unique_ne_part_pas_quand_le_Defi_est_masque()
    {
        Assert.False(TrayApplication.ShouldShowChallengeAnnouncement(challengeEnabled: false,
            announceDone: false, trainingEnabled: false, notificationsEnabled: true));
        // Témoin : les mêmes réglages, Défi disponible, donnent bien l'annonce.
        Assert.True(TrayApplication.ShouldShowChallengeAnnouncement(challengeEnabled: true,
            announceDone: false, trainingEnabled: false, notificationsEnabled: true));
    }

    [Fact]
    public void L_annonce_ne_part_pas_avec_l_interrupteur_reel()
    {
        Assert.False(TrayApplication.ShouldShowChallengeAnnouncement(DailyChallenge.Enabled,
            announceDone: false, trainingEnabled: false, notificationsEnabled: true));
    }

    [Fact]
    public void Le_rappel_d_entrainement_ne_part_pas_quand_le_Defi_est_masque()
    {
        var soir = new DateTime(2026, 10, 1, TrainingReminders.EarliestHour + 1, 0, 0);
        var signaux = new TrainingSignals(
            Enabled: true,
            IgnoredCount: 0,
            SequenceIndex: 0, // séquence inachevée : signal 1 actif, rappel dû
            LastSessionDate: null,
            LastReminderDate: null,
            LastActiveDate: null,
            LastSpecialCharDate: null,
            CurrentStreak: 0,
            HelperOpens: 0,
            ReviewPromptLastShown: null,
            ChallengeAvailable: false);

        Assert.False(TrainingReminders.ShouldRemind(soir, signaux));
        // Témoin : Défi disponible, ce même soir donne un rappel.
        Assert.True(TrainingReminders.ShouldRemind(soir, signaux with { ChallengeAvailable = true }));
    }

    [Fact]
    public void Le_rappel_lit_l_interrupteur_meme_opt_in_coche()
    {
        // Un testeur de la 1.2.0 a coché l'opt-in : la valeur reste sur disque, intacte.
        ConfigManager.SetTrainingEnabled(true);
        var signaux = TrainingReminders.Snapshot();

        Assert.True(signaux.Enabled);
        Assert.Equal(DailyChallenge.Enabled, signaux.ChallengeAvailable);
        var soir = DateOnly.FromDateTime(DateTime.Now).ToDateTime(new TimeOnly(TrainingReminders.EarliestHour + 1, 0));
        Assert.False(TrainingReminders.ShouldRemind(soir, signaux));
    }

    [Fact]
    public void Le_module_Defi_n_entre_pas_dans_les_Lecons_meme_avec_l_opt_in()
    {
        Assert.False(DailyChallenge.ShouldOfferModule(challengeEnabled: false, trainingEnabled: true));
        Assert.False(DailyChallenge.ShouldOfferModule(DailyChallenge.Enabled, trainingEnabled: true));
        // Règle d'avant, inchangée pour la 1.4.0 : interrupteur et opt-in.
        Assert.True(DailyChallenge.ShouldOfferModule(challengeEnabled: true, trainingEnabled: true));
        Assert.False(DailyChallenge.ShouldOfferModule(challengeEnabled: true, trainingEnabled: false));
    }
}
