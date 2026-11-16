using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests de régression du lot 6 de l'analyse des machines à états (fenêtre Leçons). Chaque test
/// affirme l'invariant correct : il échoue tant que le constat est vrai.
/// </summary>
public sealed class MachinesEtatsLot6Tests
{
    private static LessonsWindow Ouvrir()
    {
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        return new LessonsWindow(layout, mapper, hook);
    }

    private static void Taper(LessonsWindow fenêtre)
    {
        for (int garde = 0; garde < 5000 && !BancCapture.Field<bool>(fenêtre, "_showSummary"); garde++)
        {
            var séance = BancCapture.Field<LessonTypingSession>(fenêtre, "_session");
            Assert.True(séance.GetNextExpectedCharacter() is char, "séance bloquée avant le bilan");
            BancCapture.Call(fenêtre, "OnChar", séance.GetNextExpectedCharacter()!.Value);
        }
        Assert.True(BancCapture.Field<bool>(fenêtre, "_showSummary"));
    }

    private static (int Module, int Lecon, int Exercice) Position(LessonsWindow fenêtre) =>
        (BancCapture.Field<int>(fenêtre, "_moduleIndex"),
         BancCapture.Field<int>(fenêtre, "_lessonIndex"),
         BancCapture.Field<int>(fenêtre, "_exerciseIndex"));

    // Constat C6.2
    // Le Défi est l'exercice courant (OpenChallenge), puis « Réinitialiser la progression » est
    // confirmée : ApplyProgressReset relance la séance avec savePosition: true et enregistre le
    // Défi comme dernière position du catalogue. Le Défi n'est jamais la place de « Reprendre ».
    // Modèle suivi : DefiRecordTests.Reinitialiser_la_progression_efface_aussi_le_record_du_Defi
    // (ApplyProgressReset par BancCapture.Call, _progress par BancCapture.Field).
    [Fact]
    public void ReinitialiserLaProgression_SurLeDefi_NEnregistrePasLeDefiCommePosition()
    {
        using var isolation = new BancCapture.Isolation();
        var fenêtre = Ouvrir();
        try
        {
            Assert.True(fenêtre.OpenChallenge());
            Assert.Equal(DailyChallenge.ModuleId, ((LessonModule)BancCapture.Call(fenêtre, "get_CurrentModule")!).Id);

            BancCapture.Call(fenêtre, "ApplyProgressReset");

            var progression = BancCapture.Field<LessonProgressStore>(fenêtre, "_progress");
            Assert.NotEqual(DailyChallenge.ModuleId, progression.LastModuleId);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    // Constat C6.2 (variante : effet à la fermeture)
    // Même scénario puis fermeture : SettleSessionOnHide relit la position enregistrée et
    // re-sélectionne le Défi au lieu de rendre la place du catalogue.
    // Modèle suivi : FocusAndStateTests.Closing_on_the_challenge_returns_to_the_catalog_position.
    [Fact]
    public void ReinitialiserLaProgression_SurLeDefi_PuisFermer_RendLaPlaceDuCatalogue()
    {
        using var isolation = new BancCapture.Isolation();
        var fenêtre = Ouvrir();
        try
        {
            Assert.True(fenêtre.OpenChallenge());
            BancCapture.Call(fenêtre, "ApplyProgressReset");

            BancCapture.Call(fenêtre, "Hide");

            var courant = (LessonModule)BancCapture.Call(fenêtre, "get_CurrentModule")!;
            Assert.NotEqual(DailyChallenge.ModuleId, courant.Id);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    // Constat C6.1 (décision d'Antoine, QCM du 16 novembre 2026 : « Rendre la garde active »)
    // Fenêtre fermée pendant le parcours de prise en main, sur un bilan : Hide appelait EndTutorial
    // avant SettleSessionOnHide, donc la garde IsTutorialActive était toujours fausse et le solde
    // avançait la position du catalogue. Pendant le parcours, la fermeture ne solde pas la séance.
    // Modèle suivi : FocusAndStateTests.Closing_on_a_summary_moves_on_to_the_next_exercise
    // (Hide par BancCapture.Call) et Audit0911Lot8Tests.Passer_cet_exercice_disparait_au_bilan
    // (parcours amorcé par BeginTutorial et StartTutorialRoute).
    [Fact]
    public void Fermeture_PendantLeParcours_NeSoldePasLaSeanceNiNAvanceLaPosition()
    {
        using var isolation = new BancCapture.Isolation();
        var fenêtre = Ouvrir();
        try
        {
            fenêtre.BeginTutorial();
            BancCapture.Call(fenêtre, "StartTutorialRoute");
            Assert.True(fenêtre.IsTutorialActive);
            Taper(fenêtre);
            var avant = Position(fenêtre);

            BancCapture.Call(fenêtre, "Hide");

            Assert.Equal(avant, Position(fenêtre));
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    // Constat C6.1 (variante : « Passer pour l'instant », qui clôt le parcours puis ferme)
    // LeaveTutorialForNow appelait EndTutorial puis Hide : le tutoriel était déjà inactif quand
    // la garde était lue. Même invariant : le bilan d'une étape du parcours n'avance rien.
    [Fact]
    public void PasserPourLInstant_AuBilanDuParcours_NeSoldePasLaSeance()
    {
        using var isolation = new BancCapture.Isolation();
        var fenêtre = Ouvrir();
        try
        {
            fenêtre.BeginTutorial();
            BancCapture.Call(fenêtre, "StartTutorialRoute");
            Taper(fenêtre);
            var avant = Position(fenêtre);

            BancCapture.Call(fenêtre, "LeaveTutorialForNow");

            Assert.False(fenêtre.IsTutorialActive);
            Assert.Equal(avant, Position(fenêtre));
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    // Constat C6.1 (garde-fou : hors parcours, le solde à la fermeture reste actif, invariant I3)
    // Un bilan lu des Leçons, fenêtre fermée : l'exercice suivant attend.
    [Fact]
    public void Fermeture_HorsParcours_AuBilan_SoldeToujoursLaSeance()
    {
        using var isolation = new BancCapture.Isolation();
        var fenêtre = Ouvrir();
        try
        {
            Assert.False(fenêtre.IsTutorialActive);
            Taper(fenêtre);
            var avant = Position(fenêtre);

            BancCapture.Call(fenêtre, "Hide");

            Assert.NotEqual(avant, Position(fenêtre));
        }
        finally { BancCapture.Teardown(fenêtre); }
    }
}
