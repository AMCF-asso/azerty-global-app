using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// AG130-42 — la fenêtre des leçons tient dans son écran.
///
/// Ce qui est éprouvé : le calcul de taille. Le reste du correctif — lire
/// <c>GetDpiForWindow</c> après création, traiter <c>WM_DPICHANGED</c>, recentrer — tient à
/// Win32 et se recette en VM. Mais c'est bien ce calcul qui manquait : sans plafond, la
/// fenêtre naissait plus grande que l'écran, et rien n'échouait.
/// </summary>
public class WindowSizingTests
{
    // 1120×760 est la taille de référence de LessonsWindow ; 1920×1032 la zone de travail
    // d'un 1920×1080 avec la barre des tâches.
    private const int BaseW = 1120;
    private const int BaseH = 760;
    private const int WorkW = 1920;
    private const int WorkH = 1032;

    [Fact]
    public void TailleQuiTientDéjà_RessortIntacte()
    {
        // À 100 %, 1120×760 tient largement : le plafond ne doit rien toucher, sinon toutes
        // les fenêtres rétréciraient de 10 % sans raison.
        var (w, h) = WindowSizing.ClampToWorkArea(BaseW, BaseH, WorkW, WorkH);
        Assert.Equal(BaseW, w);
        Assert.Equal(BaseH, h);
    }

    [Fact]
    public void LeCasMesuré_175Pourcent_RentreDansLÉcran()
    {
        // ⛔ Le témoin qui compte. D(1120) et D(760) à 175 % valent 1960 et 1330, pour une
        // zone de travail de 1920×1032 : la fenêtre naissait hors écran, commandes du bas
        // inatteignables.
        var (w, h) = WindowSizing.ClampToWorkArea(1960, 1330, WorkW, WorkH);

        Assert.True(w <= (int)(WorkW * 0.9f), $"largeur {w} dépasse le plafond");
        Assert.True(h <= (int)(WorkH * 0.9f), $"hauteur {h} dépasse le plafond");
    }

    [Fact]
    public void LeRapportDeFormeEstConservé()
    {
        // UpdateRenderScaleFromCurrentClient prend le plus petit des deux rapports : écraser
        // une seule dimension laisserait une bande vide sur l'autre.
        var (w, h) = WindowSizing.ClampToWorkArea(1960, 1330, WorkW, WorkH);

        double voulu = 1960 / 1330.0;
        double obtenu = w / (double)h;
        Assert.True(Math.Abs(obtenu - voulu) < 0.01,
            $"rapport {obtenu:F3} au lieu de {voulu:F3}");
    }

    [Fact]
    public void ÉcranLarge_CEstLaHauteurQuiMord()
    {
        // 1960×1330 sur 1920×1032 : la hauteur est la contrainte serrée. La fenêtre doit
        // donc être posée sur la hauteur maximale, pas sur la largeur maximale.
        var (_, h) = WindowSizing.ClampToWorkArea(1960, 1330, WorkW, WorkH);
        Assert.Equal((int)(WorkH * 0.9f), h);
    }

    [Fact]
    public void ÉcranÉtroitEtHaut_CEstLaLargeurQuiMord()
    {
        // Un écran pivoté (1080×1920) : c'est l'autre branche du test de rapport de forme.
        // Sans elle, une seule des deux bornes serait jamais éprouvée.
        var (w, _) = WindowSizing.ClampToWorkArea(1960, 1330, 1080, 1872);
        Assert.Equal((int)(1080 * 0.9f), w);
    }

    [Fact]
    public void UneSeuleDimensionQuiDéborde_DéclencheQuandMêmeLeCalcul()
    {
        // Largeur trop grande, hauteur qui tient : la condition de sortie rapide doit
        // tester les DEUX dimensions, pas l'une ou l'autre.
        var (w, h) = WindowSizing.ClampToWorkArea(2400, 400, WorkW, WorkH);
        Assert.True(w <= (int)(WorkW * 0.9f));
        Assert.True(h < 400);
    }

    [Fact]
    public void FractionDemandée_EstCelleQuiSApplique()
    {
        // Le plafond est un paramètre, pas une constante enterrée dans la formule.
        //
        // ⚠️ Une fenêtre carrée ne le prouve pas : sur 1000×1000 avec un rapport de 1, c'est
        // toujours la branche hauteur qui gagne, et un calcul qui ignorerait le paramètre
        // pour la largeur rendrait quand même la bonne valeur. Mesuré par mutation le
        // 2026-09-21 : le témoin carré restait vert. Il faut une forme large, qui met la
        // largeur en contrainte.
        var (w, h) = WindowSizing.ClampToWorkArea(4000, 1000, 1000, 1000, fraction: 0.5f);
        Assert.Equal(500, w);
        Assert.Equal(125, h);

        // Et la forme haute, sinon c'est le plafond de HAUTEUR qui peut ignorer le paramètre
        // sans que rien ne le montre — une forme large ne passe jamais par cette branche.
        var (wHaut, hHaut) = WindowSizing.ClampToWorkArea(1000, 4000, 1000, 1000, fraction: 0.5f);
        Assert.Equal(500, hHaut);
        Assert.Equal(125, wHaut);
    }

    [Fact]
    public void ZoneDeTravailNonMesurée_RendLaTailleVoulue()
    {
        // GetMonitorInfo peut échouer. Une fenêtre trop grande vaut mieux qu'une fenêtre
        // de 0 px, et une division par zéro vaudrait un plantage au démarrage.
        Assert.Equal((BaseW, BaseH), WindowSizing.ClampToWorkArea(BaseW, BaseH, 0, 0));
        Assert.Equal((BaseW, BaseH), WindowSizing.ClampToWorkArea(BaseW, BaseH, -1, -1));
    }

    [Fact]
    public void TailleVoulueNulle_NeRendJamaisZéro()
    {
        var (w, h) = WindowSizing.ClampToWorkArea(0, 0, WorkW, WorkH);
        Assert.True(w >= 1);
        Assert.True(h >= 1);
    }

    [Fact]
    public void LeRésultatNEstJamaisNul_MêmeSurUneZoneMinuscule()
    {
        // Un plafond calculé sur une zone de quelques pixels ne doit pas rendre 0 :
        // CreateWindowExW accepterait, et la fenêtre serait invisible.
        //
        // ⚠️ Les deux branches ont chacune leur plancher, et il faut une forme EXTRÊME pour
        // que la division tombe sous 1 — mesuré par mutation le 2026-09-21 : avec le rapport
        // ordinaire 1960/1330, retirer un plancher laissait le témoin vert.
        var (wLarge, hLarge) = WindowSizing.ClampToWorkArea(1960, 100, 4, 4);
        Assert.True(wLarge >= 1);
        Assert.True(hLarge >= 1, "la branche largeur a perdu son plancher");

        var (wHaut, hHaut) = WindowSizing.ClampToWorkArea(100, 1960, 4, 4);
        Assert.True(wHaut >= 1, "la branche hauteur a perdu son plancher");
        Assert.True(hHaut >= 1);
    }

    [Fact]
    public void LePlafondParDéfautEst90Pourcent()
    {
        // C'est la valeur de LearningModule, la seule fenêtre qui bornait déjà. La changer
        // se voit ici plutôt qu'au rendu.
        Assert.Equal(0.9f, WindowSizing.MaxWorkAreaFraction);
    }

    // ── D1 (accessibilité 1.3.0) : mise à l'échelle DPI de la Pause, des Couches et de
    // l'indicateur de couche. Le branchement sur GetDpiForWindow et WM_DPICHANGED se
    // recette ; c'est le calcul qui s'éprouve ici.

    [Theory]
    [InlineData(520, 96, 520)]   // 100 % : rien ne bouge
    [InlineData(520, 120, 650)]  // 125 %
    [InlineData(520, 144, 780)]  // 150 %
    [InlineData(520, 168, 910)]  // 175 %
    [InlineData(520, 192, 1040)] // 200 %
    public void D1_ScaleForDpi_SuitLÉchelleDeLÉcran(int valeur, int dpi, int attendu)
    {
        Assert.Equal(attendu, WindowSizing.ScaleForDpi(valeur, dpi));
    }

    [Fact]
    public void D1_ScaleForDpi_ArrondiAuPlusProche_CommeMulDiv()
    {
        // 15 × 1,25 = 18,75 : une troncature rendrait 18, et la police de l'indicateur
        // perdrait un pixel à chaque échelle non entière.
        Assert.Equal(19, WindowSizing.ScaleForDpi(15, 120));
        // Le demi s'arrondit en s'éloignant de zéro, dans les deux signes.
        Assert.Equal(2, WindowSizing.ScaleForDpi(1, 144));
        Assert.Equal(-2, WindowSizing.ScaleForDpi(-1, 144));
    }

    [Fact]
    public void D1_ScaleForDpi_HauteurDePoliceNégative()
    {
        // CreateFontW prend une hauteur négative (hauteur de caractère) : elle grandit en
        // valeur absolue, elle ne change pas de signe.
        Assert.Equal(-28, WindowSizing.ScaleForDpi(-14, 192));
        Assert.Equal(-24, WindowSizing.ScaleForDpi(-16, 144));
    }

    [Fact]
    public void D1_ScaleForDpi_DpiNonMesuré_RendLaValeurÀ100Pourcent()
    {
        Assert.Equal(520, WindowSizing.ScaleForDpi(520, 0));
        Assert.Equal(520, WindowSizing.ScaleForDpi(520, -96));
    }
}
