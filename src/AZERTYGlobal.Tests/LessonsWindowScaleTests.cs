using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 30/10, M2 — le texte des Leçons ne descend plus sous l'échelle choisie dans Windows.
///
/// Historique : l'audit 24/09 B3 avait fait de la zone client PRÉVUE la référence, pour que le
/// clavier (ancré en bas) ne recouvre plus la saisie dans une fenêtre plafonnée par AG130-42.
/// L'échelle de rendu descendait alors sous 1 : 119 % au lieu de 150 % sur un 1920 × 1080,
/// 85 % en 1366 × 768. Elle reste désormais à 1 au moins ; la surface dessinée garde la taille
/// de référence et la fenêtre défile sur l'axe qui ne tient pas. Le clavier ne recouvre
/// toujours pas la saisie, puisque la surface est celle de référence.
///
/// Les tests rejouent la chaîne de LessonsWindow : taille extérieure bornée
/// (<see cref="WindowSizing.ScrollingWindowSize"/>), zone client, référence
/// (<see cref="LessonsWindow.ReferenceClientSize"/>), puis <see cref="WindowSizing.FitCanvas"/>.
/// Ce qui reste à la recette : le rendu, prouvé par les captures de la CI à 96, 144 et 192 DPI.
/// </summary>
public class LessonsWindowScaleTests
{
    // Taille de référence de LessonsWindow à 96 DPI.
    private const int BaseW = 1120;
    private const int BaseH = 760;

    // Hauteur de zone client, en unités de référence, sous laquelle le clavier (ancré en bas)
    // recouvre la saisie : 336 = bas de la zone de saisie depuis le haut, 331 = hauteur du
    // clavier et de ses marges depuis le bas (DrawLessonContent, relu le 2026-09-24).
    private const int NoOverlapHeight = 336 + 331;

    /// <summary>Une configuration d'écran. Cadre et barres : ordres de grandeur (légende,
    /// bordures, SM_CXVSCROLL), pas des mesures ; la zone de travail retire une barre des tâches
    /// de 48 px à 100 %.</summary>
    public sealed record Écran(string Nom, float Dpi, int WorkW, int WorkH, int NcW, int NcH, int Bar,
        bool DéfileX, bool DéfileY)
    {
        public override string ToString() => Nom;
    }

    public static TheoryData<Écran> Écrans => new()
    {
        new Écran("1920×1080 à 100 %", 1.00f, 1920, 1032, 16, 39, 17, false, false),
        new Écran("1920×1080 à 150 %", 1.50f, 1920, 1008, 24, 58, 26, false, true),
        new Écran("1920×1080 à 200 %", 2.00f, 1920, 984, 32, 78, 34, true, true),
        new Écran("1366×768 à 100 %", 1.00f, 1366, 720, 16, 39, 17, false, true),
    };

    private static WindowSizing.ScrolledCanvas Disposition(Écran e, out (int W, int H) reference)
    {
        int designW = (int)Math.Round(BaseW * e.Dpi);
        int designH = (int)Math.Round(BaseH * e.Dpi);
        var (outerW, outerH) = WindowSizing.ScrollingWindowSize(designW, designH, e.WorkW, e.WorkH, e.Bar, e.Bar);
        reference = LessonsWindow.ReferenceClientSize(designW, designH, e.NcW, e.NcH);
        return WindowSizing.FitCanvas(outerW - e.NcW, outerH - e.NcH, reference.W, reference.H, e.Bar, e.Bar);
    }

    [Theory]
    [MemberData(nameof(Écrans))]
    public void LeTexteGardeLÉchelleDeWindows(Écran e)
    {
        var fit = Disposition(e, out _);

        // Le rendu est à l'échelle de l'écran fois Scale : jamais sous celle de Windows.
        Assert.True(fit.Scale >= 1f, $"{e} : échelle {e.Dpi * fit.Scale:P0} au lieu de {e.Dpi:P0}");
    }

    [Theory]
    [MemberData(nameof(Écrans))]
    public void LaFenêtreDéfileSeulementSurLAxeQuiNeTientPas(Écran e)
    {
        var fit = Disposition(e, out _);

        Assert.Equal((e.DéfileX, e.DéfileY), (fit.ScrollsX, fit.ScrollsY));
    }

    [Theory]
    [MemberData(nameof(Écrans))]
    public void LaSurfaceGardeLaTailleDeRéférence_EtLeClavierNeCouvrePasLaSaisie(Écran e)
    {
        var fit = Disposition(e, out var reference);

        Assert.True(fit.CanvasWidth >= reference.W && fit.CanvasHeight >= reference.H,
            $"{e} : surface {fit.CanvasWidth}×{fit.CanvasHeight} pour {reference.W}×{reference.H}");
        Assert.True(NoOverlapHeight * e.Dpi * fit.Scale <= fit.CanvasHeight,
            $"{e} : le clavier recouvre la saisie ({NoOverlapHeight * e.Dpi * fit.Scale} > {fit.CanvasHeight})");
        // Ce qui se voit tient dans la fenêtre : le reste se rejoint en défilant.
        Assert.True(fit.ViewWidth <= fit.CanvasWidth && fit.ViewHeight <= fit.CanvasHeight);
    }

    [Theory]
    [InlineData(1.50f, 1920, 1008, 24, 58, 1.17f, 1.19f)]
    [InlineData(1.00f, 1366, 720, 16, 39, 0.84f, 0.86f)]
    public void Avant_LeRenduSuivaitLaFenêtreBornée(float dpi, int workW, int workH, int ncW, int ncH,
        float min, float max)
    {
        // Le constat de l'audit : bornage à rapport constant, puis le plus petit des deux
        // rapports zone client / référence. 119 % au lieu de 150 %, 85 % au lieu de 100 %.
        int designW = (int)Math.Round(BaseW * dpi);
        int designH = (int)Math.Round(BaseH * dpi);
        var (outerW, outerH) = WindowSizing.ClampToWorkArea(designW, designH, workW, workH);
        var (refW, refH) = LessonsWindow.ReferenceClientSize(designW, designH, ncW, ncH);
        float avant = dpi * MathF.Min((outerW - ncW) / (float)refW, (outerH - ncH) / (float)refH);

        Assert.InRange(avant, min, max);
        Assert.True(LessonsWindow.RenderScale(outerW - ncW, outerH - ncH, refW, refH) >= 1f);
    }

    [Fact]
    public void SansLaPlaceDeLaBarre_UnDéfilementHorizontalParasite()
    {
        // Pourquoi ScrollingWindowSize : à 150 % sur 1920 × 1080, borner chaque dimension laisse
        // la largeur juste égale à la référence ; la barre verticale la ronge de 26 px.
        var (w, h) = WindowSizing.ClampEachToWorkArea(1680, 1140, 1920, 1008);
        Assert.True(WindowSizing.FitCanvas(w - 24, h - 58, 1656, 1082, 26, 26).ScrollsX);

        var (w2, h2) = WindowSizing.ScrollingWindowSize(1680, 1140, 1920, 1008, 26, 26);
        Assert.Equal((1706, h), (w2, h2));
        Assert.False(WindowSizing.FitCanvas(w2 - 24, h2 - 58, 1656, 1082, 26, 26).ScrollsX);
    }

    [Fact]
    public void FenêtreQuiTient_TailleDeRéférence()
    {
        Assert.Equal((BaseW, BaseH), WindowSizing.ScrollingWindowSize(BaseW, BaseH, 1920, 1032, 17, 17));
        Assert.Equal((BaseW, BaseH), WindowSizing.ClampEachToWorkArea(BaseW, BaseH, 1920, 1032));
    }

    [Fact]
    public void ZoneDeTravailInconnue_LaTailleVoulue()
    {
        Assert.Equal((1680, 1140), WindowSizing.ScrollingWindowSize(1680, 1140, 0, 0, 26, 26));
        Assert.Equal((1680, 1140), WindowSizing.ClampEachToWorkArea(1680, 1140, 0, 0));
    }

    [Fact]
    public void ChaqueDimensionEstBornéePourElleMême()
    {
        // 1366 × 768 : la largeur tenait, elle n'est plus rognée avec la hauteur.
        Assert.Equal((1120, 648), WindowSizing.ClampEachToWorkArea(1120, 760, 1366, 720));
        Assert.Equal((954, 648), WindowSizing.ClampToWorkArea(1120, 760, 1366, 720));
    }

    [Fact]
    public void FenêtrePlusGrande_LeRenduGrandit_SansDéfiler()
    {
        var fit = WindowSizing.FitCanvas(1325, 900, 1104, 721, 17, 17);

        Assert.Equal(1.2f, fit.Scale, 2);
        Assert.False(fit.ScrollsX || fit.ScrollsY);
        Assert.Equal((1325, 900), (fit.CanvasWidth, fit.CanvasHeight));
        Assert.True(LessonsWindow.LocksAspect(1325, 900, 1104, 721));
    }

    [Fact]
    public void SousLaRéférence_LeRapportNEstPlusImposé()
    {
        Assert.False(LessonsWindow.LocksAspect(1121, 609, 1104, 721));
        Assert.False(LessonsWindow.LocksAspect(900, 721, 1104, 721));
    }

    [Theory]
    // Hauteur trop basse, largeur juste égale : la barre verticale fait déborder la largeur.
    [InlineData(1104, 600, true, true)]
    // Largeur trop étroite, hauteur juste égale : la barre horizontale fait déborder la hauteur.
    [InlineData(1000, 721, true, true)]
    // Largeur avec la place de la barre : un seul axe.
    [InlineData(1121, 600, false, true)]
    [InlineData(1000, 738, true, false)]
    public void UneBarreEnAppelleUneAutre(int clientW, int clientH, bool x, bool y)
    {
        var fit = WindowSizing.FitCanvas(clientW, clientH, 1104, 721, 17, 17);

        Assert.Equal((x, y), (fit.ScrollsX, fit.ScrollsY));
        Assert.Equal(1f, fit.Scale);
        Assert.Equal(clientW - (y ? 17 : 0), fit.ViewWidth);
        Assert.Equal(clientH - (x ? 17 : 0), fit.ViewHeight);
        Assert.Equal((1104, 721), (fit.CanvasWidth, fit.CanvasHeight));
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(120, 120)]
    [InlineData(900, 500)]
    public void LaPositionResteDansLaSurface(int position, int attendu)
        => Assert.Equal(attendu, WindowSizing.ClampScroll(position, 1000, 500));

    [Fact]
    public void SurfaceQuiTient_PositionNulle()
        => Assert.Equal(0, WindowSizing.ClampScroll(40, 400, 500));

    [Theory]
    // Déjà visible : rien ne bouge.
    [InlineData(100, 200, 300, 100)]
    // Plus bas que la vue : le bas juste visible, marge comprise.
    [InlineData(0, 600, 700, 210)]
    // Plus haut : le haut juste visible.
    [InlineData(400, 100, 150, 90)]
    // Plus grand que la vue : son début.
    [InlineData(300, 100, 800, 90)]
    // Au bout de la surface : borné.
    [InlineData(0, 980, 1000, 500)]
    public void LeFocusSeVoitEnBougeantLeMoinsPossible(int position, int start, int end, int attendu)
        => Assert.Equal(attendu, WindowSizing.ScrollToShow(position, start, end, 500, 1000, 10));

    [Fact]
    public void CadrePlusGrandQueLaFenêtre_NeDonneJamaisZéro()
    {
        // Mesure aberrante (fenêtre réduite, GetWindowRect en échec) : pas de division par zéro.
        Assert.Equal((1, 1), LessonsWindow.ReferenceClientSize(10, 10, 50, 50));
        Assert.Equal((10, 10), LessonsWindow.ReferenceClientSize(10, 10, -5, -5));
        Assert.Equal(1f, WindowSizing.FitCanvas(0, 0, 0, 0, 17, 17).Scale);
    }
}
