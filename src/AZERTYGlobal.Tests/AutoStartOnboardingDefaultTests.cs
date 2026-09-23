using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Case « Lancer au démarrage de Windows » de l'accueil (décision d'Antoine du 2026-09-23,
/// v1.3.0) : cochée par défaut au premier accueil, sans jamais contourner un refus exprimé
/// dans Windows, et fidèle à l'état réel dès qu'un choix a pu être fait. La décision est
/// pure : testable sans tâche de démarrage ni raccourci réels.
/// </summary>
public class AutoStartOnboardingDefaultTests
{
    // L'énumération est interne : les théories la reçoivent par son nom.
    private static AutoStartWindowsState State(string name) => Enum.Parse<AutoStartWindowsState>(name);

    [Fact]
    public void Premier_accueil_sans_refus_coche_la_case()
    {
        Assert.True(AutoStart.DefaultOnboardingCheck(
            AutoStartWindowsState.Disabled, isRegistered: false, choiceAlreadyPossible: false));
    }

    [Theory]
    [InlineData("DisabledByUser")]
    [InlineData("DisabledByPolicy")]
    [InlineData("Unknown")]
    public void Refus_Windows_ou_etat_illisible_ne_coche_jamais(string state)
    {
        Assert.False(AutoStart.DefaultOnboardingCheck(State(state), isRegistered: false, choiceAlreadyPossible: false));
    }

    [Theory]
    [InlineData("Disabled", false)]
    [InlineData("Enabled", true)]
    [InlineData("DisabledByUser", false)]
    [InlineData("Unknown", false)]
    public void Choix_deja_possible_reflete_l_etat_reel(string state, bool isRegistered)
    {
        Assert.Equal(isRegistered, AutoStart.DefaultOnboardingCheck(State(state), isRegistered, choiceAlreadyPossible: true));
    }

    [Fact]
    public void Deja_enregistre_reste_coche_au_premier_accueil()
    {
        Assert.True(AutoStart.DefaultOnboardingCheck(
            AutoStartWindowsState.Enabled, isRegistered: true, choiceAlreadyPossible: false));
    }
}
