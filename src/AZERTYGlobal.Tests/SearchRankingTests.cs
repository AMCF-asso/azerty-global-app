using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 25/09, V-03 : témoin du classement de la recherche de caractère, sur le vrai
/// character-index.json embarqué. Aucun test ne couvrait MatchScore, AllWordsMatch ni la
/// normalisation ; le passage des comparaisons en ordinal (8,5 fois plus rapide) devait laisser
/// le classement intact, et ce témoin le fige : score et ordre de tête, nombre de résultats.
///
/// Relevé le 26/09 avec l'ancienne comparaison culturelle (identique en fr-FR et en en-US),
/// inchangé en ordinal. Un changement voulu du score ou de l'index se reporte ici.
/// </summary>
public class SearchRankingTests
{
    private static readonly Lazy<(object Search, MethodInfo Score, PropertyInfo Character)> Index = new(Charger);

    private static (object, MethodInfo, PropertyInfo) Charger()
    {
        // Objet sans constructeur : ni fenêtre ni police, seulement l'index et le classement.
        const BindingFlags Prive = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(CharacterSearch);
        var search = RuntimeHelpers.GetUninitializedObject(type);
        var entree = type.GetNestedType("CharEntry", BindingFlags.NonPublic)!;
        return (search, type.GetMethod("ScoreEntries", Prive)!, entree.GetProperty("Character")!);
    }

    private static (int Total, string[] Tete) Classer(string requete, int n)
    {
        var (search, score, caractere) = Index.Value;
        var liste = (IList)score.Invoke(search, new object[] { requete })!;
        var tete = new List<string>();
        foreach (var element in liste)
        {
            if (tete.Count == n) break;
            var t = element!.GetType();
            tete.Add($"{caractere.GetValue(t.GetField("Item1")!.GetValue(element))}:{t.GetField("Item2")!.GetValue(element)}");
        }
        return (liste.Count, tete.ToArray());
    }

    [Theory]
    // Un caractère seul : correspondance exacte, puis casse, puis lettre de base.
    [InlineData("é", 47, new[] { "é:265", "É:250", "e:110", "E:105", "È:100" })]
    // Deux mots en début de mot, après normalisation (« flèche » → « fleche », « dr » → « droite »).
    [InlineData("flèche dr", 6, new[] { "→:80", "↔:80", "↗:80", "↘:80", "⇒:70", "⇔:70" })]
    // Synonyme de requête : « upper » cherche aussi « capital ».
    [InlineData("e upper", 20, new[] { "E:125", "È:100", "É:100" })]
    // … y compris en début de mot : « capital » trouve « petite capitale ».
    [InlineData("petite upper", 4, new[] { "ɪ:75", "ʀ:75", "ʁ:75", "ʏ:75" })]
    // Les entrées de touche morte (dk:…) ne sont pas proposées (audit du 25/09, V-05).
    [InlineData("touche morte", 0, new string[0])]
    // Méthode propre à la requête (+50 pour ^) puis diacritiques combinants.
    [InlineData("accent circonflexe", 29, new[] { "^:155", "\u032D:95", "\u0302:95" })]
    // Bonus du nom français qui commence par la requête.
    [InlineData("tiret", 4, new[] { "_:120", "-:105", "–:105", "—:105" })]
    [InlineData("pour", 3, new[] { "%:105", "∀:85", "‰:70" })]
    // Code Unicode.
    [InlineData("U+20AC", 1, new[] { "€:100" })]
    [InlineData("u+00e9", 1, new[] { "é:105" })]
    // Espaces : insécable, puis fine insécable.
    [InlineData("espace insecable", 2, new[] { "\u00A0:95", "\u202F:90" })]
    [InlineData("oe", 2, new[] { "œ:95", "Œ:90" })]
    public void LeClassementDesRequetesReellesEstFige(string requete, int total, string[] tete)
    {
        var (obtenuTotal, obtenuTete) = Classer(requete, tete.Length);
        Assert.Equal(tete, obtenuTete);
        Assert.Equal(total, obtenuTotal);
    }
}
