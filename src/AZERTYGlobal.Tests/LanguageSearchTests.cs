using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Recherche par langue (1.3.0, décisions du 27/09 et du 07/10) : taper le nom d'une langue,
/// en français, en anglais ou dans la langue, avec ou sans accents, fait remonter ses
/// caractères ; « ton » et « tone » trouvent les accents combinants des tons. Table :
/// language-search.json, générée par scripts/build-language-search.py.
/// </summary>
public class LanguageSearchTests
{
    private static readonly Lazy<(object Search, MethodInfo Score, PropertyInfo Character)> Index = new(Charger);

    private static (object, MethodInfo, PropertyInfo) Charger()
    {
        const BindingFlags Prive = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(CharacterSearch);
        var search = RuntimeHelpers.GetUninitializedObject(type);
        var entree = type.GetNestedType("CharEntry", BindingFlags.NonPublic)!;
        return (search, type.GetMethod("ScoreEntries", Prive)!, entree.GetProperty("Character")!);
    }

    private static List<(string Character, int Score)> Chercher(string requete)
    {
        var (search, score, caractere) = Index.Value;
        var liste = (IList)score.Invoke(search, new object[] { requete })!;
        var resultats = new List<(string, int)>();
        foreach (var element in liste)
        {
            var t = element!.GetType();
            resultats.Add(((string)caractere.GetValue(t.GetField("Item1")!.GetValue(element))!,
                (int)t.GetField("Item2")!.GetValue(element)!));
        }
        return resultats;
    }

    private static string[] Caracteres(string requete) => Chercher(requete).Select(r => r.Character).ToArray();

    [Theory]
    // Le peul sous tous ses noms : français, anglais, endonymes et variétés.
    [InlineData("pulaar", "ɓ ɗ ƴ ŋ Ɓ Ɗ Ƴ Ŋ")]
    [InlineData("pular", "ɓ ɗ ƴ ŋ")]
    [InlineData("fulfulde", "ɓ ɗ ƴ ŋ ɲ")]
    [InlineData("peul", "ɓ ɗ ƴ ŋ ɲ")]
    [InlineData("fula", "ɓ ɗ ƴ ŋ")]
    [InlineData("fulani", "ɓ ɗ ƴ ŋ")]
    // Wolof : ñ et ŋ étaient déjà trouvés ; ë, é, à et ó ne l'étaient pas (audit du 27/09).
    [InlineData("wolof", "ñ ŋ ë é à ó Ë")]
    [InlineData("bambara", "ɛ ɔ ɲ ŋ")]
    [InlineData("bamanankan", "ɛ ɔ ɲ ŋ")]
    // Malinké : avec et sans accent, et en maninka.
    [InlineData("malinké", "ɛ ɔ ɲ ŋ")]
    [InlineData("malinke", "ɛ ɔ ɲ ŋ")]
    [InlineData("maninka", "ɛ ɔ ɲ ŋ")]
    [InlineData("mandingue", "ɛ ɔ ɲ ŋ ñ")]
    [InlineData("soussou", "ɛ ɔ ɲ")]
    [InlineData("susu", "ɛ ɔ ɲ")]
    [InlineData("lingala", "ɛ ɔ \u0301 \u0302 \u030C")]
    [InlineData("éwé", "ɖ ƒ ɣ ɛ ɔ ŋ")]
    [InlineData("ewe", "ɖ ƒ ɣ ɛ ɔ ŋ")]
    [InlineData("yoruba", "ẹ ọ ṣ")]
    [InlineData("hausa", "ɓ ɗ ƙ ƴ")]
    [InlineData("haoussa", "ɓ ɗ ƙ ƴ")]
    [InlineData("dioula", "ɛ ɔ ɲ ŋ")]
    [InlineData("jula", "ɛ ɔ ɲ ŋ")]
    [InlineData("WOLOF", "ë")]
    public void UnNomDeLangueTrouveSesCaracteres(string requete, string attendus)
    {
        var obtenus = Caracteres(requete);
        foreach (var c in attendus.Split(' '))
            Assert.Contains(c, obtenus);
    }

    [Fact]
    public void LesAliasDeLIndexGardentLaTete()
    {
        // « wolof » trouvait ñ et ŋ par leurs alias ; ils restent devant les lettres ajoutées.
        var tete = Caracteres("wolof").Take(4).ToHashSet();
        Assert.Equal(new HashSet<string> { "ñ", "Ñ", "ŋ", "Ŋ" }, tete);
    }

    [Theory]
    [InlineData("ton")]
    [InlineData("tone")]
    public void TonEtToneTrouventLesAccentsCombinantsDesTons(string requete)
    {
        var tons = new[] { "\u0301", "\u0300", "\u0302", "\u030C", "\u0304", "\u030B", "\u030F", "\u1DC6", "\u1DC7" };
        var tete = Caracteres(requete).Take(tons.Length).ToHashSet();
        Assert.Equal(tons.ToHashSet(), tete);
    }

    [Theory]
    [InlineData("ton haut", "\u0301")]
    [InlineData("high tone", "\u0301")]
    [InlineData("ton bas", "\u0300")]
    [InlineData("low tone", "\u0300")]
    [InlineData("ton moyen", "\u0304")]
    [InlineData("ton montant", "\u030C")]
    [InlineData("rising tone", "\u030C")]
    [InlineData("ton descendant", "\u0302")]
    [InlineData("grave combinant", "\u0300")]
    [InlineData("accent grave combinant", "\u0300")]
    [InlineData("combining grave", "\u0300")]
    public void UnTonNommeVientEnTete(string requete, string attendu)
    {
        Assert.Equal(attendu, Caracteres(requete)[0]);
    }

    [Theory]
    [InlineData("pulaar b", "ɓ", "ɗ")]
    [InlineData("wolof e", "ë", "ñ")]
    [InlineData("peul d", "ɗ", "ɓ")]
    public void LangueEtLettreSeCombinent(string requete, string attendu, string exclu)
    {
        var obtenus = Caracteres(requete);
        Assert.Contains(attendu, obtenus);
        Assert.DoesNotContain(exclu, obtenus);
    }

    [Fact]
    public void UnDebutDeMotTropCourtNeTrouvePasDeLangue()
    {
        // « pu » ne nomme pas encore le pulaar : rien en ɓ pendant la frappe.
        Assert.DoesNotContain("ɓ", Caracteres("pu"));
        Assert.Contains("ɓ", Caracteres("pul"));
    }

    [Fact]
    public void UnNomDeLangueQuiEstUnMotPasseApresLesNoms()
    {
        // « four » est le fur en français ; ses lettres viennent après tout caractère trouvé
        // par son nom ou ses alias.
        var resultats = Chercher("four");
        var fur = new HashSet<string> { "ŋ", "Ŋ", "ɨ", "Ɨ", "ʉ", "Ʉ" };
        int premierFur = resultats.FindIndex(r => fur.Contains(r.Character));
        Assert.True(premierFur >= 0);
        Assert.All(resultats.Take(premierFur), r => Assert.DoesNotContain(r.Character, fur));
        Assert.All(resultats.Skip(premierFur), r => Assert.True(r.Score < 70));
    }

    [Fact]
    public void LaTableNeCiteQueDesCaracteresDeLIndexEtChaqueAliasASaSource()
    {
        using var flux = typeof(LanguageSearchTable).Assembly.GetManifestResourceStream("language-search.json");
        Assert.NotNull(flux);
        using var doc = JsonDocument.Parse(flux!);
        Assert.StartsWith("AZERTYGlobal/website, révision ",
            doc.RootElement.GetProperty("meta").GetProperty("referentiel").GetString());
        int groupes = 0;
        foreach (var groupe in doc.RootElement.GetProperty("groupes").EnumerateArray())
        {
            groupes++;
            Assert.False(string.IsNullOrEmpty(groupe.GetProperty("source").GetProperty("ref").GetString()));
            foreach (var c in groupe.GetProperty("caracteres").EnumerateArray())
                Assert.True(CharacterIndex.Shared.ByCharacter.ContainsKey(c.GetString()!),
                    $"{groupe.GetProperty("id").GetString()} : {c.GetString()} absent de l'index");
            foreach (var alias in groupe.GetProperty("alias").EnumerateArray())
                Assert.False(string.IsNullOrEmpty(alias.GetProperty("source").GetString()));
        }
        Assert.True(groupes > 100);
        Assert.True(LanguageSearchTable.Shared.ByCharacter.ContainsKey("ɓ"));
    }
}
