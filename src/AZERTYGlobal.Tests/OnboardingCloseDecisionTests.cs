using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// B2 de l'audit du 24/09 : la case « Lancer au démarrage de Windows » est cochée par
/// défaut à l'étape 3 de l'accueil depuis la 1.3.0, et <c>OnboardingWindow.Close()</c>
/// l'appliquait quel que soit le geste de fermeture. Une croix ou un Échap à l'étape 3
/// enregistraient donc le lancement automatique, contrairement à ce qu'affirment la note
/// au certificateur et le Changelog.
///
/// La décision est désormais pure (<see cref="OnboardingWindow.ShouldApplyPreferenceOnClose"/>) :
/// seul le bouton final valide ; sur croix, Échap ou WM_CLOSE, une case n'est appliquée que
/// si l'utilisateur l'a lui-même changée. La même règle vaut pour « Ne plus afficher »,
/// toujours affichée décochée.
///
/// ⚠️ Limite assumée : ce témoin tient la décision, pas le câblage des gestes vers
/// <c>Close(validated)</c> ni la remise à zéro de l'étape 3, qui relèvent de la recette.
/// </summary>
public class OnboardingCloseDecisionTests
{
    private static bool Appliquer(bool valide, bool etape3 = true, bool activation = true,
        bool cocheeMaintenant = true, bool cocheeALAffichage = true) =>
        OnboardingWindow.ShouldApplyPreferenceOnClose(valide, etape3, activation, cocheeMaintenant, cocheeALAffichage);

    [Fact]
    public void Croix_sur_la_case_cochee_par_defaut_n_active_pas_le_demarrage()
    {
        // ⛔ Le cas de B2 : étape 3 vue, activation acceptée, case laissée cochée par défaut.
        Assert.False(Appliquer(valide: false, cocheeMaintenant: true, cocheeALAffichage: true));
    }

    [Fact]
    public void Bouton_final_applique_la_case_cochee_par_defaut()
    {
        // Réciproque : sans elle, une décision devenue « jamais » rendrait le test ci-dessus
        // vert pour la mauvaise raison.
        Assert.True(Appliquer(valide: true, cocheeMaintenant: true, cocheeALAffichage: true));
    }

    [Fact]
    public void Bouton_final_applique_aussi_une_case_laissee_decochee()
    {
        // Case affichée décochée (refus dans Windows, état réel) : valider l'enregistre tel quel.
        Assert.True(Appliquer(valide: true, cocheeMaintenant: false, cocheeALAffichage: false));
    }

    [Theory]
    [InlineData(false)] // l'utilisateur a décoché la case cochée par défaut
    [InlineData(true)]  // l'utilisateur a coché une case affichée décochée
    public void Croix_ou_Echap_applique_une_case_modifiee(bool cocheeMaintenant)
    {
        bool affichee = !cocheeMaintenant;
        Assert.True(Appliquer(valide: false, cocheeMaintenant: cocheeMaintenant, cocheeALAffichage: affichee));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rien_avant_l_etape_3_ni_sans_activation(bool valide)
    {
        // Décision v0.9.7.1 intacte : une case jamais vue n'est pas un choix, et rien ne
        // s'enregistre tant que l'activation n'a pas été acceptée.
        Assert.False(Appliquer(valide, etape3: false, cocheeMaintenant: false, cocheeALAffichage: true));
        Assert.False(Appliquer(valide, activation: false, cocheeMaintenant: false, cocheeALAffichage: true));
    }

    [Fact]
    public void Ne_plus_afficher_non_touchee_n_est_pas_reecrite_sur_Echap()
    {
        // La case « Ne plus afficher » est toujours affichée décochée. Avant le correctif, un
        // Échap à l'étape 3 réécrivait « afficher au démarrage » et défaisait un refus posé
        // dans les Paramètres ; cochée par l'utilisateur, elle reste enregistrée.
        Assert.False(Appliquer(valide: false, cocheeMaintenant: false, cocheeALAffichage: false));
        Assert.True(Appliquer(valide: false, cocheeMaintenant: true, cocheeALAffichage: false));
    }
}
