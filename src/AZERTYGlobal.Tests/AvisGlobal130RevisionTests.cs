using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Avis global 1.3.0 : la carte « Révision du jour » affichait « MONTRÉAL,, sœur » (deux virgules).
/// Cause : les mots d'exemple sont des morceaux du texte coupés aux seules espaces, donc
/// « MONTRÉAL, » arrivait avec sa virgule et l'écran les séparait encore par « , ».
/// </summary>
public class AvisGlobal130RevisionTests
{
    [Fact]
    public void Un_mot_garde_sa_ponctuation_collee_hors_du_mot_affiche()
    {
        var mots = LessonsHomeLogic.PickWords(
            new[] { "Vive", "MONTRÉAL,", "sœur;", "« Un", "fin." },
            new[] { "É", "œ", "«" });
        Assert.Equal(new[] { "MONTRÉAL", "sœur", "« Un" }, mots);
        Assert.Equal("MONTRÉAL, sœur, « Un", LessonsHomeLogic.JoinWords(mots));
    }

    [Fact]
    public void Aucune_virgule_doublee_ni_point_double_dans_la_phrase_de_la_carte()
    {
        var mots = LessonsHomeLogic.PickWords(new[] { "MONTRÉAL,", "sœur,", "école." }, new[] { "É", "œ", "é" });
        string phrase = L.LessonsHome_ReviewWords(LessonsHomeLogic.JoinWords(mots));
        Assert.DoesNotContain(",,", phrase);
        Assert.DoesNotContain(", ,", phrase);
        Assert.DoesNotContain("..", phrase);
    }

    [Fact]
    public void Le_caractere_a_revoir_reste_dans_le_mot_meme_si_c_est_une_ponctuation()
    {
        // Revoir « ! » ou « , » : le mot garde ce signe, sans doubler la virgule du séparateur.
        var mots = LessonsHomeLogic.PickWords(new[] { "Bravo!", "Oui,", "non" }, new[] { "!", "," });
        Assert.Equal(new[] { "Bravo!", "Oui," }, mots);
        Assert.Equal("Bravo!, Oui,", LessonsHomeLogic.JoinWords(mots));
        Assert.DoesNotContain(",,", LessonsHomeLogic.JoinWords(new[] { "Oui,", "Bravo!" }));
    }

    [Fact]
    public void Un_meme_mot_n_est_pas_repris_pour_deux_caracteres()
    {
        var mots = LessonsHomeLogic.PickWords(new[] { "œuvre,", "cœur" }, new[] { "œ", "œ" });
        Assert.Equal(new[] { "œuvre", "cœur" }, mots);
    }

    [Fact]
    public void Au_plus_trois_mots_et_un_caractere_sans_mot_est_ignore()
    {
        var mots = LessonsHomeLogic.PickWords(new[] { "É1", "È2", "Ç3", "À4", "Z" }, new[] { "É", "ß", "È", "Ç", "À" });
        Assert.Equal(new[] { "É1", "È2", "Ç3" }, mots);
    }
}
