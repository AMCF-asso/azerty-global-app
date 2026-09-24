using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit 24/09 B3 — une fenêtre des leçons plafonnée par AG130-42 se dessine à une échelle
/// inférieure à 1.
///
/// <c>CaptureBaseWindowMetrics</c> prenait comme référence la zone client mesurée APRÈS le
/// plafond : l'échelle valait 1 dans une fenêtre trop basse, et le clavier dessiné, ancré en
/// bas, recouvrait la ligne cible et la saisie. La référence est désormais la zone client
/// prévue au DPI courant. Ce qui reste à la recette : le rendu à l'écran à 150 et 175 %.
/// </summary>
public class LessonsWindowScaleTests
{
    // Taille de référence de LessonsWindow et zone de travail d'un 1920×1080.
    private const int BaseW = 1120;
    private const int BaseH = 760;
    private const int WorkW = 1920;
    private const int WorkH = 1032;

    // Cadre non client d'une fenêtre redimensionnable avec barre de titre : ordre de grandeur
    // (légende + bordures de redimensionnement), pas une mesure.
    private const int NonClientW100 = 16, NonClientH100 = 39;
    private const int NonClientW175 = 28, NonClientH175 = 68;

    // Hauteur de zone client, en unités de référence, sous laquelle le clavier (ancré en bas)
    // recouvre la saisie : 336 = bas de la zone de saisie depuis le haut, 331 = hauteur du
    // clavier et de ses marges depuis le bas (DrawLessonContent, relu le 2026-09-24).
    private const int NoOverlapHeight = 336 + 331;

    [Fact]
    public void HorsPlafond_LÉchelleResteÀ1()
    {
        // À 100 %, rien n'est plafonné : la référence prévue égale la zone client mesurée,
        // sinon toutes les fenêtres changeraient de taille de rendu sans raison.
        var (outerW, outerH) = WindowSizing.ClampToWorkArea(BaseW, BaseH, WorkW, WorkH);
        int clientW = outerW - NonClientW100;
        int clientH = outerH - NonClientH100;

        var (refW, refH) = LessonsWindow.ReferenceClientSize(BaseW, BaseH, NonClientW100, NonClientH100);
        Assert.Equal((clientW, clientH), (refW, refH));
        Assert.Equal(1f, LessonsWindow.RenderScale(clientW, clientH, refW, refH));
    }

    [Theory]
    [InlineData(1.50f, 24, 58)]
    [InlineData(1.75f, NonClientW175, NonClientH175)]
    public void Plafonnée_LÉchelleDescendSous1_EtLeClavierNeCouvrePlusLaSaisie(float dpi, int ncW, int ncH)
    {
        int designW = (int)Math.Round(BaseW * dpi);
        int designH = (int)Math.Round(BaseH * dpi);
        var (outerW, outerH) = WindowSizing.ClampToWorkArea(designW, designH, WorkW, WorkH);
        int clientW = outerW - ncW;
        int clientH = outerH - ncH;

        // Avant : la référence était la zone client mesurée, donc l'échelle valait 1…
        Assert.Equal(1f, LessonsWindow.RenderScale(clientW, clientH, clientW, clientH));
        // … et le contenu demandait plus de hauteur que la fenêtre n'en a.
        Assert.True(NoOverlapHeight * dpi > clientH, $"{NoOverlapHeight * dpi} ≤ {clientH}");

        var (refW, refH) = LessonsWindow.ReferenceClientSize(designW, designH, ncW, ncH);
        float scale = LessonsWindow.RenderScale(clientW, clientH, refW, refH);

        Assert.True(scale < 0.85f, $"échelle {scale}");
        Assert.True(NoOverlapHeight * dpi * scale <= clientH,
            $"le clavier recouvre encore la saisie : {NoOverlapHeight * dpi * scale} > {clientH}");
    }

    [Fact]
    public void CadrePlusGrandQueLaFenêtre_NeDonneJamaisZéro()
    {
        // Mesure aberrante (fenêtre réduite, GetWindowRect en échec) : pas de division par zéro.
        Assert.Equal((1, 1), LessonsWindow.ReferenceClientSize(10, 10, 50, 50));
        Assert.Equal((10, 10), LessonsWindow.ReferenceClientSize(10, 10, -5, -5));
    }
}
