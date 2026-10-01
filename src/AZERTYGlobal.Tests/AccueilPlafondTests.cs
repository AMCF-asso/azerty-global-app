using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Plafond de l'Accueil (1.3.0) — la fenêtre, de hauteur fixe, tient dans son écran.
///
/// Zone client de l'Accueil : 560 × 763 à 96 DPI, fois 0,75 (<c>ONBOARDING_UI_SCALE</c>), fois
/// l'échelle. Cadres mesurés au banc le 29/09 (écran à 120 DPI) : 2 × 39 px. Zones de travail :
/// écran moins la barre des tâches de Windows 11 (48 px à 100 %).
/// </summary>
public class AccueilPlafondTests
{
    private const float BaseW = 560 * 0.75f;
    private const float BaseH = 763 * 0.75f;

    private static (int W, int H) Client(float scale) => ((int)(BaseW * scale), (int)(BaseH * scale));

    private static float Fit(float screenScale, int nonClientW, int nonClientH, int workW, int workH)
    {
        var (w, h) = Client(screenScale);
        return WindowSizing.FitScale(screenScale, w, h, nonClientW, nonClientH, workW, workH);
    }

    [Theory]
    [InlineData(1.25f, 39, 1366, 768 - 60)]   // 754 px de haut pour 708 : boutons coupés
    [InlineData(1.75f, 54, 1920, 1080 - 84)]  // environ 1 055 pour 996
    [InlineData(1.50f, 47, 1024, 768 - 72)]   // l'écran du banc CI
    public void ÉcranTropPetit_LaFenêtreTientAuPixelPrès(float screenScale, int cadreH, int zoneW, int zoneH)
    {
        float scale = Fit(screenScale, 2, cadreH, zoneW, zoneH);
        var (w, h) = Client(scale);

        Assert.True(scale < screenScale, $"échelle {scale}");
        Assert.True(h + cadreH <= zoneH, $"hauteur {h} + {cadreH} > {zoneH}");
        Assert.True(w + 2 <= zoneW);
        // Réduite juste assez : pas plus d'un pixel de marge perdue.
        Assert.True(zoneH - (h + cadreH) <= 1, $"{zoneH - (h + cadreH)} px perdus");
    }

    [Theory]
    [InlineData(1.00f, 31, 1366, 768 - 48)]
    [InlineData(1.50f, 47, 1920, 1080 - 72)]
    [InlineData(1.25f, 39, 1920, 1080 - 60)]
    public void ÉcranAssezGrand_LÉchelleNeChangePas(float screenScale, int cadreH, int zoneW, int zoneH)
    {
        Assert.Equal(screenScale, Fit(screenScale, 2, cadreH, zoneW, zoneH));
    }

    [Fact]
    public void ÉcranÉtroit_CEstLaLargeurQuiMord()
    {
        // 600 × 1024 à 150 % : la hauteur tient, pas les 630 px de large.
        float scale = Fit(1.5f, 16, 47, 600, 1024 - 72);
        var (w, _) = Client(scale);
        Assert.True(w + 16 <= 600, $"largeur {w}");
        Assert.True(600 - (w + 16) <= 1);
    }

    [Fact]
    public void JamaisAgrandie()
    {
        Assert.Equal(1f, Fit(1f, 2, 31, 3840, 2160 - 48));
    }

    [Fact]
    public void ZoneDeTravailNonMesurée_ÉchelleDeLÉcran()
    {
        Assert.Equal(1.25f, Fit(1.25f, 2, 39, 0, 0));
        Assert.Equal(1.25f, WindowSizing.FitScale(1.25f, 0, 0, 2, 39, 1366, 708));
    }

    [Fact]
    public void PlancherDeLisibilité()
    {
        // Un écran absurde (320 × 240) ne rend pas le texte illisible : la fenêtre déborde.
        Assert.Equal(WindowSizing.MinFitScale, Fit(1f, 2, 31, 320, 240));
    }
}
