using System.Reflection;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Témoin R8 de la revue de code du 2026-09-21.
///
/// Le franchissement du seuil de caractères enrichis était consommé **avant** la
/// tentative de sollicitation : quelqu'un qui franchit 20 caractères enrichis à
/// 9 minutes actives voyait le signal désarmé alors que <c>MaybeShowReviewPrompt</c>
/// venait de refuser sur le plancher des 10 minutes. Le franchissement ne se reproduisant
/// qu'une fois par session de processus, la sollicitation ne repartait jamais avant un
/// redémarrage — alors qu'il suffisait d'attendre une minute.
///
/// La règle tenue ici : on ne consomme que ce qui est acquis. Sollicitation partie, ou
/// sollicitation devenue impossible. Tout autre refus laisse le signal armé pour le
/// prochain silence.
///
/// ⚠️ Limite assumée : ce témoin tient la décision, pas le câblage. Que le timer
/// <c>TIMER_REVIEW_QUIET</c> repasse bien toutes les <c>REVIEW_QUIET_POLL_MS</c> et que
/// la seconde tentative parte pour de bon reste du Win32, prouvé par la recette VM.
/// </summary>
public class ReviewSignalConsumptionTests
{
    private static bool Consommer(bool sollicitationPartie, bool encorePossible)
    {
        var method = typeof(TrayApplication).GetMethod(
            "ShouldConsumeEnrichedSignal",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        return (bool)method!.Invoke(null, new object[] { sollicitationPartie, encorePossible })!;
    }

    [Fact]
    public void La_sollicitation_partie_consomme_le_franchissement()
    {
        // Elle ne se rejoue pas : le signal a servi.
        Assert.True(Consommer(sollicitationPartie: true, encorePossible: true));
    }

    [Fact]
    public void Un_refus_provisoire_laisse_le_signal_arme()
    {
        // ⛔ Le cas de R8 : refus sur le plancher des 10 minutes actives, alors que la
        // sollicitation reste possible. Avant le correctif, ce cas rendait true et brûlait
        // le franchissement pour toute la session de processus.
        Assert.False(Consommer(sollicitationPartie: false, encorePossible: true));
    }

    [Fact]
    public void Une_sollicitation_devenue_impossible_consomme_le_franchissement()
    {
        // Avis déjà cliqué, deux essais faits, notifications ou liens externes coupés :
        // garder le signal armé ne servirait plus qu'à faire tourner le timer.
        Assert.True(Consommer(sollicitationPartie: false, encorePossible: false));
    }

    [Fact]
    public void Le_franchissement_survit_a_autant_de_refus_provisoires_qu_il_faut()
    {
        // Le plancher se rattrape minute par minute : cinq silences de suite avant que
        // les 10 minutes soient là ne doivent rien consommer.
        for (int i = 0; i < 5; i++)
            Assert.False(Consommer(sollicitationPartie: false, encorePossible: true));
    }
}
