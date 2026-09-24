using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Aucune sollicitation d'avis pendant une séance de Leçons ou un tutoriel, ni dans les
/// dix minutes qui suivent (décision d'Antoine du 2026-09-24).
///
/// Établi dans le code : les frappes de la fenêtre Leçons passent par le moteur comme les
/// autres (<c>KeyMapper</c> → <c>UsageStats.RecordEmittedText</c>, sans plage exclue) ;
/// elles comptent dans les caractères enrichis, les minutes actives et le silence de
/// frappe. Le tutoriel de l'accueil, lui, est exclu depuis le 2026-09-21. Les frappes
/// d'exercice peuvent donc armer le seuil, mais ne suffisent pas à déclencher : il faut
/// une frappe remappée après la fin de la séance, puis le silence.
///
/// ⚠️ Limite assumée : le câblage (Show/Hide des Leçons, ctor/Dispose du tutoriel, appel
/// dans <c>TrayApplication</c>) reste du Win32, prouvé par la recette.
/// </summary>
public class AvisApresSeanceTests : IDisposable
{
    private const long Silence = 15_000;   // REVIEW_QUIET_SILENCE_MS de TrayApplication
    private const long Minute = 60_000;
    private static readonly long Delai = ReviewPromptGate.LearningCooldownMs;
    private const long Jamais = long.MaxValue;

    public AvisApresSeanceTests() => LearningSessionTracker.ResetForTests();

    public void Dispose() => LearningSessionTracker.ResetForTests();

    private static bool Autorise(ReviewPromptTrigger declencheur, bool seanceOuverte,
        long depuisFin, long depuisDerniereFrappe)
        => ReviewPromptGate.LearningAllowsPrompt(declencheur, seanceOuverte, depuisFin,
            depuisDerniereFrappe, Delai);

    [Fact]
    public void Le_delai_vaut_dix_minutes()
    {
        Assert.Equal(10, ReviewPromptGate.LearningCooldownMinutes);
        Assert.Equal(10 * Minute, ReviewPromptGate.LearningCooldownMs);
    }

    // ReviewPromptTrigger est interne : il ne peut pas paraître dans la signature d'une
    // méthode de test publique, d'où les boucles plutôt que des [Theory].
    private static readonly ReviewPromptTrigger[] TousLesDeclencheurs =
        { ReviewPromptTrigger.QuietTyping, ReviewPromptTrigger.Startup, ReviewPromptTrigger.Share };

    [Fact]
    public void Refusee_pendant_une_seance()
    {
        foreach (var declencheur in TousLesDeclencheurs)
        {
            Assert.False(Autorise(declencheur, seanceOuverte: true, depuisFin: Jamais, depuisDerniereFrappe: Silence));
            Assert.False(Autorise(declencheur, seanceOuverte: true, depuisFin: 60 * Minute, depuisDerniereFrappe: Silence));
        }
    }

    [Fact]
    public void Refusee_dans_les_dix_minutes_apres_la_fin()
    {
        foreach (var declencheur in TousLesDeclencheurs)
        {
            // Frappe hors séance toute récente : la garde tient quand même.
            Assert.False(Autorise(declencheur, false, depuisFin: 0, depuisDerniereFrappe: 0));
            Assert.False(Autorise(declencheur, false, depuisFin: 5 * Minute, depuisDerniereFrappe: Silence));
            Assert.False(Autorise(declencheur, false, depuisFin: Delai - 1, depuisDerniereFrappe: Silence));
        }
    }

    [Fact]
    public void Acceptee_apres_les_dix_minutes_hors_frappe()
    {
        // Démarrage, relais de l'accueil et partage ne naissent pas d'une frappe.
        Assert.True(Autorise(ReviewPromptTrigger.Startup, false, depuisFin: Delai, depuisDerniereFrappe: Jamais));
        Assert.True(Autorise(ReviewPromptTrigger.Share, false, depuisFin: Delai, depuisDerniereFrappe: Jamais));
    }

    [Fact]
    public void Acceptee_apres_une_vraie_frappe_suivie_du_silence()
    {
        // Séance refermée il y a 12 min, frappe dans Word il y a 20 s.
        Assert.True(Autorise(ReviewPromptTrigger.QuietTyping, false,
            depuisFin: 12 * Minute, depuisDerniereFrappe: 20_000));
    }

    [Fact]
    public void Les_frappes_d_exercice_seules_ne_declenchent_pas()
    {
        // Seuil armé par les exercices, dernière frappe pendant la séance (12 min 1 s),
        // séance refermée il y a 12 min, rien tapé depuis.
        long depuisFin = 12 * Minute;
        long depuisDerniereFrappe = depuisFin + 1_000;

        // Le tick de quiétude, lui, tenterait : signal armé, frappe de la session, silence.
        Assert.True(ReviewPromptGate.ShouldTryAfterQuietTyping(signalArmed: true,
            depuisDerniereFrappe, Silence, reviewDeferred: false));
        // La garde des séances refuse : aucune frappe hors exercice depuis la fin.
        Assert.False(Autorise(ReviewPromptTrigger.QuietTyping, false, depuisFin, depuisDerniereFrappe));
    }

    [Fact]
    public void Sans_seance_dans_ce_processus_la_garde_ne_change_rien()
    {
        foreach (var declencheur in TousLesDeclencheurs)
            Assert.True(Autorise(declencheur, false, depuisFin: Jamais, depuisDerniereFrappe: Silence));
    }

    [Fact]
    public void Le_suivi_date_la_fin_d_une_seance_et_rien_d_autre()
    {
        var lecons = new object();
        Assert.False(LearningSessionTracker.IsOpen);
        Assert.Equal(Jamais, LearningSessionTracker.MillisecondsSinceLastClose);

        LearningSessionTracker.Opened(lecons);
        LearningSessionTracker.Opened(lecons); // Show sur une fenêtre déjà visible
        Assert.True(LearningSessionTracker.IsOpen);
        Assert.Equal(Jamais, LearningSessionTracker.MillisecondsSinceLastClose);

        LearningSessionTracker.Closed(lecons);
        Assert.False(LearningSessionTracker.IsOpen);
        Assert.InRange(LearningSessionTracker.MillisecondsSinceLastClose, 0, Delai - 1);
    }

    [Fact]
    public void Une_fermeture_sans_ouverture_ne_date_rien()
    {
        // Hide ou Dispose d'une fenêtre jamais montrée.
        LearningSessionTracker.Closed(new object());
        Assert.Equal(Jamais, LearningSessionTracker.MillisecondsSinceLastClose);
    }

    [Fact]
    public void Deux_seances_ouvertes_la_garde_tient_jusqu_a_la_derniere()
    {
        var lecons = new object();
        var tutoriel = new object();
        LearningSessionTracker.Opened(lecons);
        LearningSessionTracker.Opened(tutoriel);

        LearningSessionTracker.Closed(lecons);
        Assert.True(LearningSessionTracker.IsOpen);

        LearningSessionTracker.Closed(tutoriel);
        Assert.False(LearningSessionTracker.IsOpen);
    }
}
