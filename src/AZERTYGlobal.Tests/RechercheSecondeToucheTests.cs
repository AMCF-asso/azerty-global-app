using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit final 1.3.0, A02-02 : après une touche morte, la Recherche ne disait la couche de la
/// seconde touche que pour Maj. Pour les méthodes dont la seconde touche est sur AltGr ou
/// Maj + AltGr, le texte suivi à la lettre donnait un autre caractère : « ∉ » disait
/// « AltGr + = » puis « E », qui donne « ∈ » ; « ѓ » donnait « ф ».
///
/// La Recherche refaite (partition, notation « Mots », QCM du 11/10) dessine le geste depuis
/// <see cref="Gesture.Of(MethodData)"/> : ligne de résultat (geste abrégé), partition et
/// phrase. Ces témoins prouvent que chaque temps y porte sa couche. Le texte de
/// <see cref="CharacterSearch.FormatMethod"/>, corrigé lui aussi, n'est plus affiché.
///
/// Ce qui n'est pas éprouvé ici, et reste à la recette : le dessin à l'écran.
/// </summary>
public class RechercheSecondeToucheTests : IDisposable
{
    public RechercheSecondeToucheTests() => L.Language = "fr";
    public void Dispose() => L.Language = "fr";

    private static MethodData Preferee(string caractere)
    {
        var methode = CharacterIndex.Shared.ByCharacter[caractere].Preferred!;
        Assert.True(methode.IsDeadKey, $"{caractere} : touche morte attendue");
        return methode;
    }

    [Theory]
    [InlineData("∉", false, true)]   // dk_scientific puis AltGr + E
    [InlineData("ѓ", false, true)]   // dk_cyrillic puis AltGr + D
    [InlineData("ǣ", false, true)]   // dk_macron puis AltGr + A
    [InlineData("Ǣ", true, true)]    // dk_macron puis Maj + AltGr + A
    [InlineData("ê", false, false)]
    [InlineData("Ê", true, false)]
    public void LaSecondeToucheDuGesteGardeSaCouche(string caractere, bool maj, bool altGr)
    {
        var temps = Gesture.Of(Preferee(caractere));

        Assert.Equal(2, temps.Count);
        Assert.Equal((maj, altGr), (temps[1].Shift, temps[1].AltGr));
    }

    /// <summary>La phrase lue sous la partition (et par le lecteur d'écran) nomme AltGr au
    /// second temps, en français comme en anglais.</summary>
    [Theory]
    [InlineData("fr", "∉")]
    [InlineData("en", "∉")]
    [InlineData("fr", "ѓ")]
    [InlineData("en", "Ǣ")]
    public void LaPhraseNommeAltGrAuSecondTemps(string langue, string caractere)
    {
        L.Language = langue;
        var runs = Gesture.Sentence(Gesture.Of(Preferee(caractere)));
        int puis = runs.FindLastIndex(run => run.Text == L.Gesture_Then);

        Assert.True(puis > 0, "second temps introuvable");
        Assert.Contains(runs.Skip(puis), run => run.Text == "AltGr");
    }

    /// <summary>
    /// Toutes les méthodes à touche morte de l'index, préférées ou non : le dernier temps
    /// porte Maj et AltGr exactement quand la couche brute de l'index les nomme, et un temps
    /// Verr. Maj. le précède exactement quand elle nomme Caps. La couche est lue en texte,
    /// pas par le parseur du geste : sinon le témoin répéterait le code qu'il éprouve.
    /// </summary>
    [Fact]
    public void ChaqueSecondeToucheDeLIndexGardeSaCouche()
    {
        var couches = new HashSet<string>();
        int vues = 0;
        foreach (var entree in CharacterIndex.Shared.Entries)
        {
            foreach (var methode in entree.Methods.Where(m => m.IsDeadKey))
            {
                var temps = Gesture.Of(methode);
                string contexte = $"{entree.Character} ({methode.Layer})";
                Assert.True(temps.Count >= 2, contexte);
                var derniere = temps[^1];
                Assert.True(methode.Layer.Contains("Shift") == derniere.Shift, contexte);
                Assert.True(methode.Layer.Contains("AltGr") == derniere.AltGr, contexte);
                Assert.True(methode.Layer.Contains("Caps") == (temps[^2].KeyCode == Gesture.CapsLockCode), contexte);
                couches.Add(methode.Layer);
                vues++;
            }
        }

        // Le parcours a bien rencontré les couches en cause : sans elles, il ne prouverait rien.
        Assert.Contains("AltGr", couches);
        Assert.Contains("Shift+AltGr", couches);
        Assert.Contains("Caps+AltGr", couches);
        Assert.True(vues > 1000, $"{vues} méthodes à touche morte seulement");
    }

    [Theory]
    [InlineData("fr", "∉", "puis AltGr + E")]
    [InlineData("fr", "ѓ", "puis AltGr + D")]
    [InlineData("fr", "Ǣ", "puis AltGr + Maj + A")]
    [InlineData("en", "∉", "then AltGr + E")]
    [InlineData("en", "Ǣ", "then AltGr + Shift + A")]
    [InlineData("fr", "ê", "puis E")]          // couche de base : inchangée
    [InlineData("fr", "Ê", "puis Maj + E")]    // Maj : inchangée
    public void LeTexteDeLaMethodeDitLaCoucheDeLaSecondeTouche(string langue, string caractere, string attendu)
    {
        L.Language = langue;
        string[] lignes = CharacterSearch.FormatMethod(Preferee(caractere)).Split('\n');

        Assert.Equal(2, lignes.Length);
        Assert.Equal(attendu, lignes[1]);
    }
}
