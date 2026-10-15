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
    public void LaMaquetteDeCinqLignesDe49PxSuitLeDessin()
    {
        // Champ de 60 px, filet en 60 ; lignes de 49 px séparées d'un filet : 61-109, 111-159…
        static int Hauteur49(int index) => 49;
        Assert.Equal(0, CharacterSearch.RowIndexAt(109, 60, 0, 5, Hauteur49));
        Assert.Equal(-1, CharacterSearch.RowIndexAt(110, 60, 0, 5, Hauteur49));
        Assert.Equal(4, CharacterSearch.RowIndexAt(309, 60, 0, 5, Hauteur49));  // 5e ligne : 261-309
        Assert.Equal(-1, CharacterSearch.RowIndexAt(310, 60, 0, 5, Hauteur49)); // filet sur la partition
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
