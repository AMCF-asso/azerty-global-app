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
}
