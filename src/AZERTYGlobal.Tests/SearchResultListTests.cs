using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 25/09, corrections de la recherche de caractère : le clic suit la géométrie du
/// dessin (V-07), le pied annonce le vrai nombre de correspondances (V-08), et les mots de la
/// méthode de saisie prennent leur couleur dans la langue courante (V-06).
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
    public void LePiedDitCombienDeCorrespondancesDepassentLePlafond()
    {
        // « cyrillique » : 98 correspondances, 20 lignes affichées.
        Assert.Equal("20 sur 98 résultats — Entrée pour insérer", CharacterSearch.FooterCountText(20, 98));

        L.Language = "en";
        Assert.Equal("20 of 98 results — Enter to insert", CharacterSearch.FooterCountText(20, 98));
    }

    [Fact]
    public void SousLePlafondLePiedGardeSesTextes()
    {
        Assert.Equal(L.Search_ResultCountPlural(12), CharacterSearch.FooterCountText(12, 12));
        Assert.Equal(L.Search_ResultCountPlural(20), CharacterSearch.FooterCountText(20, 20));
        Assert.Equal(L.Search_ResultCountSingular, CharacterSearch.FooterCountText(1, 1));
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void LesMotsDeLaMethodeSeColorentDansLaLangueCourante(string langue)
    {
        L.Language = langue;
        // Le texte réel d'une couche Maj suivie d'une touche morte, tel que la recherche l'affiche.
        string methode = $"{L.Search_LayerKeyLabel("Shift", "é")} {L.Search_AfterDeadKeyLabel("Base", "e")}";
        var genres = methode.Split(' ').Select(CharacterSearch.ClassifyMethodToken).ToArray();

        Assert.Equal(new[]
        {
            CharacterSearch.MethodToken.Shift,      // « Maj » / « Shift »
            CharacterSearch.MethodToken.Separator,  // « + »
            CharacterSearch.MethodToken.Key,        // « é »
            CharacterSearch.MethodToken.Separator,  // « puis » / « then »
            CharacterSearch.MethodToken.Key,        // « e »
        }, genres);
    }

    [Fact]
    public void AltGrGardeSaCouleurEtUneToucheResteUneTouche()
    {
        Assert.Equal(CharacterSearch.MethodToken.AltGr, CharacterSearch.ClassifyMethodToken("AltGr"));
        Assert.Equal(CharacterSearch.MethodToken.Key, CharacterSearch.ClassifyMethodToken("ù"));
        // Hors de sa langue, le mot d'une autre langue n'est plus pris pour un modificateur.
        L.Language = "en";
        Assert.Equal(CharacterSearch.MethodToken.Key, CharacterSearch.ClassifyMethodToken("Maj"));
    }
}
