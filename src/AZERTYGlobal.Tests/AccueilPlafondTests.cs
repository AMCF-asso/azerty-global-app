using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Plafond de l'Accueil — l'échelle ne baisse plus que pour la largeur ; la hauteur défile
/// (audit du 30/10, M2 suite : <see cref="WindowSizing.FitScaleToWidth"/>, et les tests de
/// défilement de FenetresDefilantesTests).
///
/// Zone client de l'Accueil : 960 × 720 à 96 DPI, fois 0,75 (<c>ONBOARDING_UI_SCALE</c>), soit
/// 720 × 540 à 100 %, fois l'échelle. Cadre : 2 px de large, barre de défilement 24 px réservée.
/// </summary>
public class AccueilPlafondTests
{
    private const float BaseW = 960 * 0.75f;
    private const int Frame = 2 + 24;

    private static int ClientW(float scale) => (int)(BaseW * scale);

    [Theory]
    [InlineData(1.00f, 1366)]
    [InlineData(1.25f, 1366)]
    [InlineData(1.00f, 1024)]
    [InlineData(1.75f, 1920)]
    public void LargeurQuiTient_LÉchelleNeChangePas(float scale, int workW)
        => Assert.Equal(scale, WindowSizing.FitScaleToWidth(scale, ClientW(scale), Frame, workW));

    [Fact]
    public void ÉcranÉtroit_LaFenêtreTientAuPixelPrès()
    {
        // 600 px de large à 150 % : les 1 080 px de la fenêtre ne tiennent pas.
        float scale = WindowSizing.FitScaleToWidth(1.5f, ClientW(1.5f), Frame, 600);
        Assert.True(scale < 1.5f, $"échelle {scale}");
        Assert.True(ClientW(scale) + Frame <= 600);
        Assert.True(600 - (ClientW(scale) + Frame) <= 1, "réduite juste assez");
    }

    [Fact]
    public void JamaisAgrandie()
        => Assert.Equal(1f, WindowSizing.FitScaleToWidth(1f, ClientW(1f), Frame, 3840));

    [Fact]
    public void ZoneDeTravailNonMesurée_ÉchelleDeLÉcran()
    {
        Assert.Equal(1.25f, WindowSizing.FitScaleToWidth(1.25f, ClientW(1.25f), Frame, 0));
        Assert.Equal(1.25f, WindowSizing.FitScaleToWidth(1.25f, 0, Frame, 1366));
    }

    [Fact]
    public void PlancherDeLisibilité()
    {
        // Un écran absurde (320 px) ne rend pas le texte illisible : la fenêtre déborde.
        Assert.Equal(WindowSizing.MinFitScale, WindowSizing.FitScaleToWidth(1f, ClientW(1f), Frame, 320));
    }
}
