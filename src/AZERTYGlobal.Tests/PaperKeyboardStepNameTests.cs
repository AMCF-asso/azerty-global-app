using System.Linq;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Clavier virtuel, geste de la recherche (revue du 07/10, P1 n°2) : la touche à pastille
/// porte le nom que lui donne la consigne. Pour Ź, la phrase dit « Frappez ù » ; la touche
/// montrait seulement « ´ », ce qu'elle tape dans la couche courante.
/// </summary>
public class PaperKeyboardStepNameTests
{
    private const uint ScanQuote = 0x28; // la touche gravée « ù »
    private const uint ScanW = 0x11;     // la touche gravée « Z »

    [Fact]
    public void ToucheAPastille_PorteLeNomDeLaConsigne()
    {
        var steps = Gesture.Of("Ź");
        var state = new PaperKeyboardState();
        VirtualKeyboard.AddGesture(state, steps, namesOnKeys: true);

        Assert.Equal(1, state.StepBadges[ScanQuote]);
        Assert.Equal(2, state.StepBadges[ScanW]);
        // La touche à droite du M montre ce qu'elle tape avec AZERTY Global, l'accent, et la
        // consigne la nomme par sa place (choix d'Antoine du 15/10, au lieu de la gravure « ù »).
        Assert.Equal("´", state.StepKeyNames[ScanQuote]);
        Assert.Equal("Z", state.StepKeyNames[ScanW]);

        // Chaque touche à pastille est nommée en gras dans la consigne.
        var strong = Gesture.Sentence(steps).Where(r => r.Strong).Select(r => r.Text).ToList();
        Assert.All(steps, step => Assert.Contains(step.SpokenLabel, strong));
        Assert.Contains(L.Gesture_KeyRightOfM, strong);
        Assert.Equal(state.StepBadges.Keys.OrderBy(k => k), state.StepKeyNames.Keys.OrderBy(k => k));
    }

    [Fact]
    public void RaccourciTenu_NiNomNiChangementDesPastilles()
    {
        var steps = Gesture.Of("Ź");
        var state = new PaperKeyboardState();
        VirtualKeyboard.AddGesture(state, steps, namesOnKeys: false);

        Assert.Empty(state.StepKeyNames);
        Assert.Equal(2, state.StepBadges.Count);
    }

    [Fact]
    public void GesteDUnSeulTemps_SansPastilleNiNom()
    {
        var steps = Gesture.Of("é");
        Assert.Single(steps);
        var state = new PaperKeyboardState();
        VirtualKeyboard.AddGesture(state, steps, namesOnKeys: true);

        Assert.Empty(state.StepBadges);
        Assert.Empty(state.StepKeyNames);
        Assert.Single(state.TargetScancodes);
    }

    [Fact]
    public void PetitSigne_CeQueLaToucheTape_SaufSilRediteSonNom()
    {
        // « ù » en grand, l'accent de la touche morte en petit.
        Assert.Equal(TrayApplication.GetDeadKeySymbol("dk_acute"), PaperKeyboard.TypedBesideName("ù", "dk_acute", space: false));
        // « Z » en grand : « z » le redirait.
        Assert.Null(PaperKeyboard.TypedBesideName("Z", "z", space: false));
        // Une touche qui ne tape rien dans la couche courante : le nom seul.
        Assert.Null(PaperKeyboard.TypedBesideName("ù", null, space: false));
        // Espace ordinaire : rien en petit.
        Assert.Null(PaperKeyboard.TypedBesideName("Espace", " ", space: true));
    }
}
