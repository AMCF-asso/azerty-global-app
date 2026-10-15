using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Clavier virtuel, geste à deux temps (Antoine, 15/10 : « les étapes 1 et 2 sont de la même
/// couleur, pas archi lisible ») : l'étape 1 a son fond jaune et son contour ambre, l'étape 2
/// garde le bleu de l'action ; une touche sans étape (cible des Leçons) reste à l'action.
/// </summary>
public class PaperKeyboardStepColorTests
{
    [Fact]
    public void LesDeuxEtapesNOntPasLaMemeCouleur()
    {
        Assert.NotEqual(PaperKeyboard.StepColor(1), PaperKeyboard.StepColor(2));
        Assert.Equal(PaperTheme.Step1, PaperKeyboard.StepColor(1));
        Assert.Equal(PaperTheme.Step2, PaperKeyboard.StepColor(2));
    }

    [Fact]
    public void SansEtapeLaToucheGardeLAction()
    {
        Assert.Equal(PaperTheme.Action, PaperKeyboard.StepColor(0));
    }

    [Fact]
    public void LEtapeUneEstPleineEtClaire()
    {
        Assert.NotEqual(PaperTheme.Surface, PaperTheme.Step1Fill);
        // Le caractère reste à l'encre : fond clair, distinct du contour.
        Assert.NotEqual(PaperTheme.Step1, PaperTheme.Step1Fill);
    }
}
