using AZERTYGlobal;
using TypingEngine.Windows;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests de régression du lot 1 de l'analyse des machines à états (indicateur de couche). Chaque
/// test affirme l'invariant correct : il échoue tant que le constat est vrai.
/// </summary>
public sealed class MachinesEtatsLot1Tests
{
    // Constat C1.7
    // Classe de fenêtre refusée : l'indicateur existe sans fenêtre (HWND nul). Une couche qui
    // devient active ne doit alors rien placer ni repeindre : InvalidateRect(NULL) invalide toutes
    // les fenêtres.
    // Modèle suivi : Accessibilite130Tests (LayerIndicatorWindow).
    [Fact]
    public void IndicateurDeCouche_SansFenetre_NeRepeintRien()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetMaintainableLayersEnabled(true);
        ConfigManager.SetMaintainableVisualFeedbackEnabled(true);
        var invalidations = new List<IntPtr>();
        using var indicateur = new LayerIndicatorWindow((_, _) => false) { InvalidateWindow = invalidations.Add };

        indicateur.Update(new MaintainableLayerState("dk_greek", MaintainableLayerMode.OneShot, default), secureInput: false);

        Assert.Empty(invalidations);
    }
}
