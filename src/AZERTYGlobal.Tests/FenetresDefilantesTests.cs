using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Lot 130 A (D1 d'Antoine, 29/10) — les fenêtres Conflit de disposition et Couches ne dépassent
/// plus l'écran : plafonnées à la zone de travail, texte qui revient à la ligne, contenu qui
/// défile. Ce qui est éprouvé : le calcul de taille (<see cref="WindowSizing.FitScrollable"/>) et
/// le défilement (<see cref="WindowScroll"/>). Le rendu se lit au banc de captures
/// (<c>AZERTY_CAPTURE_ECRAN=1024x728</c>).
///
/// Cadre mesuré sur Windows 11 : 16 px de large, 39 de haut ; barre de défilement : 17 px.
/// </summary>
public class FenetresDefilantesTests
{
    private const int FrameW = 16;
    private const int FrameH = 39;
    private const int Bar = 17;

    // Écran de la CI : 1024×768, moins la barre des tâches.
    private const int WorkW = 1024;
    private const int WorkH = 728;

    /// <summary>Contenu qui grandit quand la largeur diminue : texte qui revient à la ligne.</summary>
    private static int Wrapping(int width) => 400 * 1000 / Math.Max(1, width) * 2 + 300;

    [Fact]
    public void FenetreQuiTient_GardeSaTailleDeReference()
    {
        // 100 % sur 1920×1080 : rien ne change, ni la largeur, ni la hauteur, ni de barre.
        var fit = WindowSizing.FitScrollable(560, _ => 440, FrameW, FrameH, 1920, 1032, Bar);

        Assert.Equal(new WindowSizing.ScrollableFit(560, 560, 440, 440, false), fit);
    }

    [Fact]
    public void LeCasMesure_200PourcentSur1024x768_LaFenetreTientDansLEcran()
    {
        // Conflit à 200 % : référence 1120 px de zone cliente pour 1008 de disponible.
        var fit = WindowSizing.FitScrollable(1120, Wrapping, FrameW, FrameH, WorkW, WorkH, Bar);

        Assert.True(fit.ClientWidth + FrameW <= WorkW, $"largeur {fit.ClientWidth + FrameW}");
        Assert.True(fit.ClientHeight + FrameH <= WorkH, $"hauteur {fit.ClientHeight + FrameH}");
        Assert.True(fit.Scrolls);
        Assert.True(fit.ContentHeight > fit.ClientHeight);
    }

    [Fact]
    public void QuandIlDefile_LaBarreNeMangePasLaLargeurDeMiseEnPage()
    {
        // La barre s'ajoute à droite : le contenu est mesuré à la largeur qui reste, pas à
        // celle de la zone cliente entière.
        var fit = WindowSizing.FitScrollable(1120, Wrapping, FrameW, FrameH, WorkW, WorkH, Bar);

        Assert.Equal(fit.LayoutWidth + Bar, fit.ClientWidth);
        Assert.Equal(Wrapping(fit.LayoutWidth), fit.ContentHeight);
    }

    [Fact]
    public void Largeur_EstPlafonneeMemeSansDefilement()
    {
        // Un contenu court mais large : la fenêtre se resserre, le texte repasse à la ligne,
        // et aucune barre n'apparaît tant que la hauteur suffit.
        var fit = WindowSizing.FitScrollable(1120, _ => 300, FrameW, FrameH, WorkW, WorkH, Bar);

        Assert.Equal(WorkW - FrameW, fit.ClientWidth);
        Assert.Equal(fit.ClientWidth, fit.LayoutWidth);
        Assert.False(fit.Scrolls);
        Assert.Equal(300, fit.ClientHeight);
    }

    [Fact]
    public void MesureEchouee_ZoneDeTravailNulle_NePlafonnePas()
    {
        var fit = WindowSizing.FitScrollable(1120, _ => 600, FrameW, FrameH, 0, 0, Bar);

        Assert.Equal(new WindowSizing.ScrollableFit(1120, 1120, 600, 600, false), fit);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    public void DeCentAcentSoixanteQuinzePourcent_RienNeDefile_SurUnEcranDe1920(double scale)
    {
        int wanted = (int)(560 * scale);
        int content = (int)(440 * scale);
        var fit = WindowSizing.FitScrollable(wanted, _ => content, FrameW, FrameH, 1920, 1032, Bar);

        Assert.False(fit.Scrolls);
        Assert.Equal(wanted, fit.ClientWidth);
        Assert.Equal(content, fit.ClientHeight);
    }

    [Fact]
    public void Defilement_PositionBorneAuContenu()
    {
        var scroll = new WindowScroll();
        scroll.Reset(IntPtr.Zero, contentHeight: 900, viewHeight: 600);

        Assert.True(scroll.Needed);
        Assert.Equal(300, scroll.Clamp(int.MaxValue));
        Assert.Equal(0, scroll.Clamp(-50));
        Assert.Equal(120, scroll.Clamp(120));
    }

    [Fact]
    public void Defilement_ContenuQuiTient_NEstPasNecessaire()
    {
        var scroll = new WindowScroll();
        scroll.Reset(IntPtr.Zero, contentHeight: 500, viewHeight: 600);

        Assert.False(scroll.Needed);
        Assert.Equal(0, scroll.Clamp(200));
        Assert.False(scroll.OnMouseWheel(IntPtr.Zero, IntPtr.Zero, 24));
        Assert.False(scroll.OnVScroll(IntPtr.Zero, IntPtr.Zero, 24));
    }
}
