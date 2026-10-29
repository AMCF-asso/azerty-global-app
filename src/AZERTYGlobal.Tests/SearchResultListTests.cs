using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 25/09, corrections de la recherche de caractère : le clic suit la géométrie du
/// dessin (V-07) et le pied annonce le vrai nombre de correspondances (V-08). Refonte 1.3.0 :
/// le pied de la maquette « partition du choix » (nombre en gras, Entrée insère « Ź »), et
/// les gravures de la méthode viennent de la table du socle.
///
/// Ce qui n'est pas éprouvé ici, et reste à la recette : le rendu à l'écran et le clic réel.
/// </summary>
public class SearchResultListTests : IDisposable
{
    public SearchResultListTests() => L.Language = "fr";
    public void Dispose() => L.Language = "fr";

    // Géométrie d'OnPaint avec des lignes de 40 px : séparateur de la recherche en y = 50,
    // 1re ligne de 51 à 90, séparateur en 91, 2e ligne de 92 à 131, séparateur en 132,
    // 3e ligne de 133 à 172, séparateur en 173, 4e ligne de 174 à 213.
    private const int SearchAreaH = 50;
    private static int Hauteur40(int index) => 40;

    [Theory]
    [InlineData(51, 0)]
    [InlineData(90, 0)]   // ⛔ dernier pixel de la 1re ligne : l'ancien calcul rendait la 2e
    [InlineData(92, 1)]
    [InlineData(131, 1)]  // ⛔ l'ancien calcul rendait la 3e dès y = 130
    [InlineData(133, 2)]
    [InlineData(213, 3)]  // ⛔ l'ancien calcul rendait -1 dès y = 210 : le bas de la 4e ne répondait plus
    public void LeClicTombeSurLaLigneDessinee(int mouseY, int attendu)
    {
        Assert.Equal(attendu, CharacterSearch.RowIndexAt(mouseY, SearchAreaH, 0, 4, Hauteur40));
    }

    [Theory]
    [InlineData(50)]   // séparateur sous la recherche
    [InlineData(91)]   // séparateurs entre les lignes : n'appartiennent à aucune
    [InlineData(132)]
    [InlineData(214)]  // sous la dernière ligne visible
    public void UnSeparateurOuLeVideNeSelectionneRien(int mouseY)
    {
        Assert.Equal(-1, CharacterSearch.RowIndexAt(mouseY, SearchAreaH, 0, 4, Hauteur40));
    }

    [Fact]
    public void LeDefilementDecaleLIndiceRendu()
    {
        // Liste défilée de 7 : la première ligne visible est le résultat 7.
        Assert.Equal(7, CharacterSearch.RowIndexAt(51, SearchAreaH, 7, 4, Hauteur40));
        Assert.Equal(8, CharacterSearch.RowIndexAt(92, SearchAreaH, 7, 4, Hauteur40));
    }

    [Fact]
    public void DesLignesDeHauteursDifferentesSEmpilentAvecLeursSeparateurs()
    {
        // Ligne 0 : 30 px (51 à 80), séparateur en 81, ligne 1 : 60 px (82 à 141).
        static int Hauteur(int index) => index == 0 ? 30 : 60;
        Assert.Equal(0, CharacterSearch.RowIndexAt(80, SearchAreaH, 0, 2, Hauteur));
        Assert.Equal(-1, CharacterSearch.RowIndexAt(81, SearchAreaH, 0, 2, Hauteur));
        Assert.Equal(1, CharacterSearch.RowIndexAt(141, SearchAreaH, 0, 2, Hauteur));
        Assert.Equal(-1, CharacterSearch.RowIndexAt(142, SearchAreaH, 0, 2, Hauteur));
    }

    [Fact]
    public void SansLigneVisibleLeClicNeRendRien()
    {
        Assert.Equal(-1, CharacterSearch.RowIndexAt(60, SearchAreaH, 0, 0, Hauteur40));
    }

    [Fact]
    public void LaMaquetteDeCinqLignesDe52PxSuitLeDessin()
    {
        // Champ de 60 px, filet en 60 ; lignes de 52 px séparées d'un filet : 61-112, 114-165…
        static int Hauteur52(int index) => 52;
        Assert.Equal(0, CharacterSearch.RowIndexAt(112, 60, 0, 5, Hauteur52));
        Assert.Equal(-1, CharacterSearch.RowIndexAt(113, 60, 0, 5, Hauteur52));
        Assert.Equal(4, CharacterSearch.RowIndexAt(324, 60, 0, 5, Hauteur52));  // 5e ligne : 272-324
        Assert.Equal(-1, CharacterSearch.RowIndexAt(325, 60, 0, 5, Hauteur52)); // filet sur la bande
    }

    // ── Mise en page verticale : champ, lignes, bande, pied, du haut vers le bas ──

    [Fact]
    public void ACentPourCentLaFenetreDeCinqLignesFaitQuatreCents()
    {
        var layout = CharacterSearch.LayoutResults(20, 1f, 0);
        Assert.Equal(5, layout.Rows);
        Assert.Equal(61, layout.ListTop);       // champ de 60 px et son filet
        Assert.Equal(325, layout.ListBottom);   // cinq lignes de 52 px et quatre filets
        Assert.Equal(400, layout.ClientH);
    }

    [Theory]
    [InlineData(1.0f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(1.75f)]
    [InlineData(2.0f)]
    [InlineData(2.5f)]
    public void LePiedNeRecouvreJamaisLaListeNiLaBande(float scale)
    {
        // Les zones de travail de 720 px (CI en 1024 × 768) et de 1400 px (écran 1440 p), plafond
        // à 20 px du bord, comme ResizeToFitResults.
        foreach (int workH in new[] { 720, 1400 })
        {
            int max = workH - 20;
            var layout = CharacterSearch.LayoutResults(20, scale, max);

            Assert.InRange(layout.Rows, 1, 5);
            Assert.True(layout.ListTop < layout.ListBottom);
            Assert.True(layout.ListBottom <= layout.BandTop);
            Assert.True(layout.BandTop < layout.FooterTop);
            Assert.True(layout.FooterTop < layout.ClientH);
            Assert.True(layout.ClientH <= max, $"{scale} × {workH} : {layout.ClientH} > {max}");

            // Au plus de lignes qui tiennent : une de plus dépasserait le plafond.
            int rowStep = (layout.ListBottom - layout.ListTop + 1) / layout.Rows;
            Assert.True(layout.Rows == 5 || layout.ClientH + rowStep > max,
                $"{scale} × {workH} : {layout.Rows} lignes, de la place pour une de plus");
        }
    }

    [Fact]
    public void AdeuxCentPourCentSurUnePetiteZoneLaListeMontreMoinsDeLignes()
    {
        // Le défaut des captures 1.3.0 : 560 × 543 à 100 %, fenêtre plafonnée à 700 px dès 150 %
        // (zone de 720 px), pied par-dessus la liste. À 200 %, cinq lignes demandent 794 px :
        // quatre tiennent (689 px).
        var layout = CharacterSearch.LayoutResults(20, 2f, 720 - 20);
        Assert.Equal(4, layout.Rows);
        Assert.Equal(689, layout.ClientH);
    }

    [Fact]
    public void UneZoneMinusculeGardeAuMoinsUneLigne()
    {
        var layout = CharacterSearch.LayoutResults(20, 2.5f, 200);
        Assert.Equal(1, layout.Rows);
    }

    [Fact]
    public void PeuDeResultatsPeuDeLignes()
    {
        Assert.Equal(2, CharacterSearch.LayoutResults(2, 1f, 0).Rows);
        Assert.Equal(1, CharacterSearch.LayoutResults(1, 2f, 680).Rows);
    }

    [Fact]
    public void LaListeCommenceUnPixelSousLeFiletDuChampEtSArreteAvantLaBande()
    {
        var layout = CharacterSearch.LayoutResults(20, 1.5f, 0);
        int fieldH = layout.ListTop - 1;
        Assert.Equal(0, CharacterSearch.RowIndexAt(layout.ListTop, fieldH, 0, layout.Rows, _ => (layout.ListBottom - layout.ListTop + 1 - (layout.Rows - 1)) / layout.Rows));
        Assert.Equal(-1, CharacterSearch.RowIndexAt(layout.ListBottom + 1, fieldH, 0, layout.Rows, _ => (layout.ListBottom - layout.ListTop + 1 - (layout.Rows - 1)) / layout.Rows));
    }

    private static string Texte(List<TextRun> runs) => string.Concat(runs.Select(r => r.Text));

    [Fact]
    public void LePiedDitCombienDeCorrespondancesDepassentLePlafond()
    {
        // « cyrillique » : 98 correspondances, 20 lignes affichées.
        var runs = CharacterSearch.FooterCountRuns(20, 98);
        Assert.Equal("20 résultats affichés sur 98", Texte(runs));
        Assert.True(runs[0].Strong);
        Assert.Equal("20", runs[0].Text);

        L.Language = "en";
        Assert.Equal("20 of 98 results shown", Texte(CharacterSearch.FooterCountRuns(20, 98)));
    }

    [Fact]
    public void SousLePlafondLePiedCompteSimplement()
    {
        Assert.Equal("12 résultats", Texte(CharacterSearch.FooterCountRuns(12, 12)));
        Assert.Equal("20 résultats", Texte(CharacterSearch.FooterCountRuns(20, 20)));
        Assert.Equal("1 résultat", Texte(CharacterSearch.FooterCountRuns(1, 1)));
        L.Language = "en";
        Assert.Equal("1 result", Texte(CharacterSearch.FooterCountRuns(1, 1)));
    }

    [Fact]
    public void LePiedDitCeQuInsereEntree()
    {
        Assert.Equal("Entrée insère « Ź »", L.Search_FooterInsert("Ź"));
        L.Language = "en";
        Assert.Equal("Enter inserts “Ź”", L.Search_FooterInsert("Ź"));
    }

    [Fact]
    public void LesGravuresDeLaRechercheSontCellesDuSocle()
    {
        // Refonte 1.3.0 : la table des gravures n'est plus recopiée dans la recherche.
        var prop = typeof(CharacterSearch).GetProperty("KeyLabels",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        Assert.Same(Gesture.KeyLegends, prop.GetValue(null));
    }
}
