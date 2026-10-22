using System;
using System.Linq;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit 1.3.0, A23 (FUN-3) : « Recommencer » la finale du Défi consommait une étape de la
/// séquence et gonflait « Défis terminés » à chaque rejeu ; le partage lisait l'étape déjà
/// avancée. Et sans l'opt-in d'entraînement, le menu « Défi du jour » ouvrait les Leçons
/// ordinaires.
/// </summary>
public class DefiRejouableTests
{
    private static readonly DateOnly Jour = new(2026, 10, 21);

    [Fact]
    public void Une_etape_de_la_sequence_ne_compte_qu_une_fois()
    {
        using var isolation = new BancCapture.Isolation();
        long avant = UsageStats.ChallengesCompletedCount;

        Assert.True(TrainingReminders.MarkSessionCompleted(Jour, playedIndex: 0));
        Assert.False(TrainingReminders.MarkSessionCompleted(Jour, playedIndex: 0)); // rejeu

        Assert.Equal(1u, ConfigManager.TrainingSequenceIndex);
        Assert.Equal(avant + 1, UsageStats.ChallengesCompletedCount);
    }

    [Fact]
    public void L_etape_suivante_jouee_le_meme_jour_compte_toujours()
    {
        using var isolation = new BancCapture.Isolation();
        Assert.True(TrainingReminders.MarkSessionCompleted(Jour, playedIndex: 0));
        Assert.True(TrainingReminders.MarkSessionCompleted(Jour, playedIndex: 1));
        Assert.Equal(2u, ConfigManager.TrainingSequenceIndex);
    }

    [Fact]
    public void Apres_la_sequence_le_defi_commun_compte_une_fois_par_jour()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingSequenceIndex(DailyChallenge.SequenceLength);
        long avant = UsageStats.ChallengesCompletedCount;
        uint fin = DailyChallenge.SequenceLength;

        Assert.False(TrainingReminders.MarkSessionCompleted(Jour, playedIndex: fin - 1)); // rejeu de la dernière étape
        Assert.True(TrainingReminders.MarkSessionCompleted(Jour, playedIndex: fin));
        Assert.False(TrainingReminders.MarkSessionCompleted(Jour, playedIndex: fin));
        Assert.True(TrainingReminders.MarkSessionCompleted(Jour.AddDays(1), playedIndex: fin));

        Assert.Equal(fin, ConfigManager.TrainingSequenceIndex);
        Assert.Equal(avant + 2, UsageStats.ChallengesCompletedCount);
    }

    [Fact]
    public void Recommencer_la_finale_ne_compte_pas_une_seconde_fois()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingEnabled(true);
        var state = new ChallengeSessionState();
        state.LoadCatalog();
        long avant = UsageStats.ChallengesCompletedCount;

        state.Complete(new LessonAttemptStats(), previousBestWpm: null);
        state.Complete(new LessonAttemptStats(), previousBestWpm: null); // « Recommencer », même module

        Assert.Equal(1u, ConfigManager.TrainingSequenceIndex);
        Assert.Equal(avant + 1, UsageStats.ChallengesCompletedCount);
    }

    [Fact]
    public void Le_Defi_demande_s_ouvre_sans_l_opt_in_puis_quitte_le_catalogue()
    {
        using var isolation = new BancCapture.Isolation();
        Assert.False(ConfigManager.TrainingEnabled);
        var state = new ChallengeSessionState();
        Assert.DoesNotContain(state.LoadCatalog().Modules, m => m.Id == DailyChallenge.ModuleId);

        state.Request();
        Assert.True(state.IsStale());
        Assert.Contains(state.LoadCatalog().Modules, m => m.Id == DailyChallenge.ModuleId);

        state.EndRequest();
        Assert.True(state.IsStale());
        Assert.DoesNotContain(state.LoadCatalog().Modules, m => m.Id == DailyChallenge.ModuleId);
    }

    [Fact]
    public void Avec_l_opt_in_le_Defi_reste_dans_le_catalogue()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingEnabled(true);
        var state = new ChallengeSessionState();
        Assert.Contains(state.LoadCatalog().Modules, m => m.Id == DailyChallenge.ModuleId);
        state.EndRequest();
        Assert.Contains(state.LoadCatalog().Modules, m => m.Id == DailyChallenge.ModuleId);
    }
}
