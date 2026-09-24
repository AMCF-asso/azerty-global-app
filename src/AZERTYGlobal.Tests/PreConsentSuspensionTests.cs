using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 24/09 : avant l'accord d'activation, l'application est inerte, mais le suivi du
/// premier plan annonçait déjà « suspendu dans tel jeu » (bulle de sécurité, icône marquée,
/// infobulle, ligne AntiCheatDetected dans error.log) à quelqu'un qui n'avait encore rien
/// activé. <see cref="TrayApplication.ShouldShowCompatibilitySuspension"/> tient la règle
/// d'affichage ; le suivi continue, et c'est lui qu'ActivateWithConsent applique au hook.
///
/// ⚠️ Limite assumée : que la suspension suivie avant l'accord soit bien appliquée au hook
/// à l'activation (ApplyHookState dans ActivateWithConsent) demande une instance de
/// TrayApplication ; c'est une ligne de recette (jeu anti-triche au premier plan).
/// </summary>
public class PreConsentSuspensionTests
{
    [Fact]
    public void Avant_l_accord_une_suspension_ne_se_montre_pas()
    {
        Assert.False(TrayApplication.ShouldShowCompatibilitySuspension(
            suspendedForCompatibility: true, activationConsent: false));
    }

    [Fact]
    public void Apres_l_accord_la_suspension_se_montre()
    {
        // Réciproque : une règle devenue « jamais » rendrait le test ci-dessus vert.
        Assert.True(TrayApplication.ShouldShowCompatibilitySuspension(
            suspendedForCompatibility: true, activationConsent: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Sans_suspension_rien_a_montrer(bool accord)
    {
        Assert.False(TrayApplication.ShouldShowCompatibilitySuspension(
            suspendedForCompatibility: false, activationConsent: accord));
    }
}
