using System.Reflection;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Verrou typographique EN (audit 1.3.0, A30, LOC-9) : l'interface anglaise mêlait apostrophes
/// droites et courbes (21 contre 20) et guillemets droits et “ ” (7 contre 8). Elle suit
/// désormais le choix du français et du site : apostrophe ’ et guillemets “ ”.
/// Même énumération par réflexion que <see cref="FrenchTypographyTests"/>, en anglais.
/// </summary>
public class EnglishTypographyTests : IDisposable
{
    public EnglishTypographyTests() => L.Language = "en";
    public void Dispose() => L.Language = "fr";

    /// <summary>Évalue toutes les chaînes EN accessibles de L : (nom, valeur).</summary>
    private static IEnumerable<(string Name, string Value)> AllEnglishStrings()
    {
        L.Language = "en";
        var type = typeof(L);

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (prop.PropertyType != typeof(string)) continue;
            var value = (string?)prop.GetValue(null);
            if (value != null) yield return (prop.Name, value);
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.ReturnType != typeof(string) || method.IsSpecialName) continue;
            if (method.ContainsGenericParameters) continue;
            var args = new object?[method.GetParameters().Length];
            bool supported = true;
            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i].ParameterType;
                if (p == typeof(string)) args[i] = "x";
                else if (p == typeof(int)) args[i] = 2;
                else if (p == typeof(long)) args[i] = 2L;
                else if (p == typeof(DateOnly)) args[i] = new DateOnly(2026, 1, 1);
                else if (p == typeof(List<string>)) args[i] = new List<string> { "a", "b" };
                else { supported = false; break; }
            }
            if (!supported) continue;
            string? value = null;
            try { value = (string?)method.Invoke(null, args); }
            catch { /* méthode non évaluable avec des arguments factices : ignorée */ }
            if (value != null) yield return (method.Name, value);
        }
    }

    [Fact]
    public void EnglishStrings_UseTypographicApostrophe()
    {
        var violations = new List<string>();
        foreach (var (name, value) in AllEnglishStrings())
        {
            for (int i = 1; i < value.Length - 1; i++)
            {
                // Apostrophe droite entre deux lettres : contraction ou possessif (don't, app's)
                if (value[i] == '\'' && char.IsLetter(value[i - 1]) && char.IsLetter(value[i + 1]))
                    violations.Add($"{name}: apostrophe droite dans \"{value}\"");
            }
        }
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void EnglishStrings_UseCurlyQuotes()
    {
        var violations = new List<string>();
        foreach (var (name, value) in AllEnglishStrings())
        {
            if (value.Contains('"'))
                violations.Add($"{name}: guillemet droit dans «{value}»");
        }
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>Les explications « Pourquoi ici » sortent d'un switch sur le caractère, que le
    /// balayage des chaînes de L ne voit pas : (nom, valeur) de chacune, en anglais.</summary>
    private static IEnumerable<(string Name, string Value)> EnglishExplanations()
    {
        L.Language = "en";
        for (int code = 0x20; code <= 0xFFFF; code++)
        {
            if (L.TutorialExplanation((char)code) is not { } e) continue;
            yield return ($"TutorialExplanation(U+{code:X4}).Why", e.Why);
            if (e.Remember is { } remember)
                yield return ($"TutorialExplanation(U+{code:X4}).Remember", remember);
        }
    }

    /// <summary>
    /// Audit de régression du 10/11, RA14 : l'interface est en anglais américain, avec un mot
    /// par chose. Les textes ajoutés début novembre mêlaient “full stop” et “period”,
    /// “neighbours”, “e-mail” et “email”, “chevrons” et “angle brackets”, “no-break” et
    /// “non-breaking”, et trois calques du français.
    /// </summary>
    [Theory]
    [InlineData("full stop")]         // period
    [InlineData("neighbour")]         // neighbor
    [InlineData("e-mail")]            // email
    [InlineData("chevron")]           // angle brackets
    [InlineData("waits for resume")]  // « attend la reprise »
    [InlineData("your route")]        // « votre parcours »
    [InlineData("discovery")]         // « Découverte » : Introduction
    [InlineData("emblem of")]         // « emblème de »
    [InlineData("free spot")]         // « sur une place libre »
    public void EnglishStrings_AvoidBritishSpellingsAndCalques(string banned)
    {
        var explanations = EnglishExplanations().ToList();
        Assert.True(explanations.Count >= 29, $"Explications lues : {explanations.Count}");
        var violations = AllEnglishStrings().Concat(explanations)
            .Where(s => s.Value.Contains(banned, StringComparison.OrdinalIgnoreCase))
            .Select(s => $"{s.Name}: « {banned} » dans “{s.Value}”").ToList();
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>RA14 : l'encadré des deux espaces s'ouvre par l'onglet “Non-breaking spaces” ;
    /// son texte disait “no-break space”. Les étiquettes courtes du clavier dessiné
    /// (PaperKeyboard_Nbsp) gardent “no-break” et ne sont pas visées ici.</summary>
    [Fact]
    public void EnglishExplanations_NameTheSpacesLikeTheirTab()
    {
        Assert.Contains("Non-breaking", L.Explanation_NbspTab);
        var violations = EnglishExplanations()
            .Where(s => s.Value.Contains("no-break", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Name).ToList();
        Assert.True(violations.Count == 0, string.Join("\n", violations));
        Assert.Contains("non-breaking space", L.TutorialExplanation(' ')!.Value.Why);
    }
}
