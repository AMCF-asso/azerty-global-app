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
}
