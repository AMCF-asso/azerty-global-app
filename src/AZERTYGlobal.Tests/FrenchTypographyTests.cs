using System.Reflection;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Verrou typographique FR (constat m14 de l'audit i18n, corrigé en v1.2.0) : les chaînes
/// françaises de l'app doivent appliquer la typographie qu'AZERTY Global promeut —
/// espace insécable avant : ; ? ! » (U+202F avant ; ! ?, règle A541) et après «, apostrophe typographique ’.
/// Le test énumère par réflexion toutes les propriétés et méthodes publiques de L
/// retournant string, les évalue en langue française, et échoue à la moindre régression.
/// </summary>
public class FrenchTypographyTests : IDisposable
{
    public FrenchTypographyTests() => L.Language = "fr";
    public void Dispose() => L.Language = "fr";

    private static readonly char[] DoublePunctuation = { ':', ';', '?', '!', '»' };

    /// <summary>Évalue toutes les chaînes FR accessibles de L : (nom, valeur).</summary>
    private static IEnumerable<(string Name, string Value)> AllFrenchStrings()
    {
        L.Language = "fr";
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
    public void FrenchStrings_UseNoBreakSpaceBeforeDoublePunctuation()
    {
        var violations = new List<string>();
        foreach (var (name, value) in AllFrenchStrings())
        {
            for (int i = 1; i < value.Length; i++)
            {
                if (Array.IndexOf(DoublePunctuation, value[i]) >= 0 && value[i - 1] == ' ')
                    violations.Add($"{name}: espace normale avant '{value[i]}' dans \"{value}\"");
                if (value[i] is ';' or '!' or '?' && value[i - 1] == '\u00A0')
                    violations.Add($"{name}: U+00A0 avant '{value[i]}' (U+202F attendue, règle A541) dans \"{value}\"");
                if (value[i - 1] == '«' && i < value.Length && value[i] == ' ')
                    violations.Add($"{name}: espace normale après '«' dans \"{value}\"");
            }
        }
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>
    /// Audit 1.3.0, P2 (AG130-47) : les textes des exercices de l'accueil et de l'Initiation
    /// gardent leur typographie à l'affichage. La saisie tolère l'espace ordinaire et
    /// l'apostrophe droite (TypingTolerance) : sans ce test, une typographie perdue dans le
    /// texte affiché passerait inaperçue. La ligne de code (rang 4) n'est pas du français.
    /// </summary>
    [Fact]
    public void ExerciseTexts_KeepFrenchTypography()
    {
        var violations = new List<string>();
        for (int step = 0; step < TutorialSteps.All.Length; step++)
        {
            if (step == TutorialSteps.Code) continue;
            string text = TutorialSteps.All[step].Target;
            for (int i = 0; i < text.Length; i++)
            {
                if (MissesSpaceBefore(text, i))
                    violations.Add($"rang {step} : pas d'insécable avant '{text[i]}' (U+202F pour ; ! ?) dans \"{text}\"");
                if (text[i] == '«' && (i + 1 >= text.Length || text[i + 1] is not ('\u00A0' or '\u202F')))
                    violations.Add($"rang {step} : pas d'insécable après '«' dans \"{text}\"");
                if (text[i] == '\'')
                    violations.Add($"rang {step} : apostrophe droite dans \"{text}\"");
            }
        }
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>
    /// Fragments du corpus du Défi qui ne sont pas du français et gardent leur ponctuation :
    /// un chemin Windows, un objet JSON, et l'espagnol de l'extrait 14 (marqué fr parce que
    /// la phrase qui suit est française). Chaque fragment doit rester présent, sinon
    /// l'exception ne protège plus rien et doit disparaître.
    /// </summary>
    private static readonly Dictionary<int, string> CorpusExceptions = new()
    {
        [4] = @"C:\Users\vous\Documents",
        [5] = "{\"nom\": \"valeur\"}",
        [14] = "¿Cómo estás? ¡Muy bien!",
    };

    /// <summary>
    /// Audit 1.3.0, P2 (ONB-9) : les extraits français du Défi du jour (defi-corpus.json)
    /// et leurs crédits, affichés dans la consigne « d’après … », suivent la même typographie
    /// que les exercices. Les extraits de, pt (et l'espagnol) ne sont pas du français ; les
    /// crédits sont vérifiés pour tous les extraits, car la consigne qui les cite est
    /// toujours en français. Les leçons (lessons.json) suivront en 1.3.1.
    /// </summary>
    [Fact]
    public void DefiCorpus_KeepsFrenchTypography()
    {
        using var stream = typeof(DailyChallenge).Assembly.GetManifestResourceStream("defi-corpus.json");
        Assert.NotNull(stream);
        using var doc = System.Text.Json.JsonDocument.Parse(stream);

        var violations = new List<string>();
        int frenchTexts = 0;
        foreach (var e in doc.RootElement.GetProperty("extracts").EnumerateArray())
        {
            int id = e.GetProperty("id").GetInt32();
            if (e.TryGetProperty("credit", out var credit) && credit.ValueKind == System.Text.Json.JsonValueKind.String)
                CheckFrenchTypography($"crédit {id}", credit.GetString()!, violations);
            if (e.GetProperty("lang").GetString() != "fr") continue;

            string text = e.GetProperty("text").GetString()!;
            if (CorpusExceptions.TryGetValue(id, out var fragment))
            {
                Assert.Contains(fragment, text);
                text = text.Replace(fragment, "");
            }
            CheckFrenchTypography($"extrait {id}", text, violations);
            frenchTexts++;
        }

        Assert.True(frenchTexts >= 340, $"Corpus incomplet : {frenchTexts} extraits fr lus");
        Assert.True(violations.Count == 0,
            $"{violations.Count} écarts typographiques dans defi-corpus.json :\n"
            + string.Join("\n", violations.Take(40)));
    }

    /// <summary>
    /// Remontée 13b (07/11) : les explications « Pourquoi ici » du parcours, générées depuis le
    /// brouillon validé, gardent la typographie française. Elles sortent d'un switch sur le
    /// caractère, que le balayage des chaînes de L ne voit pas.
    /// </summary>
    [Fact]
    public void TutorialExplanations_KeepFrenchTypography()
    {
        L.Language = "fr";
        var violations = new List<string>();
        int explained = 0;
        foreach (var step in TutorialSteps.All)
            foreach (char c in step.Target.Distinct())
            {
                if (L.TutorialExplanation(c) is not { } e) continue;
                explained++;
                CheckFrenchTypography($"explication de '{c}'", e.Why, violations);
                if (e.Remember is { } remember)
                    CheckFrenchTypography($"« Pour retenir » de '{c}'", remember, violations);
            }
        Assert.True(explained > 0, "Aucune explication lue");
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>
    /// Audit de régression du 10/11, RA14 : dans le même encadré, « Pourquoi » écrivait
    /// « Verr. Maj » et « Pour retenir » « Verr. Maj. » ; et le point-virgule y était précédé
    /// tantôt de U+202F, tantôt de U+00A0. Un seul nom de touche ; puis, règle A541 (10/11),
    /// l'espace fine insécable U+202F devant ; ! ? et U+00A0 devant : seulement.
    /// </summary>
    [Fact]
    public void TutorialExplanations_UseOneKeyNameAndOneNoBreakSpace()
    {
        L.Language = "fr";
        var violations = new List<string>();
        int explained = 0;
        int thin = 0;
        for (int code = 0x20; code <= 0xFFFF; code++)
        {
            if (L.TutorialExplanation((char)code) is not { } e) continue;
            explained++;
            foreach (string text in new[] { e.Why, e.Remember ?? "" })
            {
                thin += System.Text.RegularExpressions.Regex.Matches(text, "\u202F[;!?]").Count;
                if (System.Text.RegularExpressions.Regex.IsMatch(text, @"Verr\. Maj(?!\.)"))
                    violations.Add($"U+{code:X4} : « Verr. Maj » sans point dans \"{text}\"");
                if (System.Text.RegularExpressions.Regex.IsMatch(text, @"[ \u00A0](?=[;!?])|[ \u202F](?=:)"))
                    violations.Add($"U+{code:X4} : mauvaise espace devant un signe double (U+202F pour ; ! ?, U+00A0 pour :) dans \"{text}\"");
            }
        }
        Assert.True(explained >= 29, $"Explications lues : {explained}");
        Assert.True(thin > 0, "Aucune espace fine insécable U+202F devant ; ! ? dans les explications");
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>Insécable avant : ; ? ! » (U+202F pour ; ! ?, U+00A0 pour : et »), U+00A0 après «, aucune apostrophe droite.</summary>
    private static void CheckFrenchTypography(string name, string text, List<string> violations)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (MissesSpaceBefore(text, i))
                violations.Add($"{name} : pas d'insécable avant '{text[i]}' (U+202F pour ; ! ?) dans \"{text}\"");
            if (text[i] == '«' && (i + 1 >= text.Length || text[i + 1] is not ('\u00A0' or '\u202F')))
                violations.Add($"{name} : pas d'insécable après '«' dans \"{text}\"");
            if (text[i] == '\'')
                violations.Add($"{name} : apostrophe droite dans \"{text}\"");
        }
    }

    /// <summary>
    /// Règle A541 (10/11) : U+202F devant ; ! ? ; U+00A0 ou U+202F devant : et ». Une espace
    /// ordinaire, un U+00A0 devant ; ! ? ou l'absence d'espace sont des écarts.
    /// </summary>
    private static bool MissesSpaceBefore(string text, int i)
    {
        char c = text[i];
        if (Array.IndexOf(DoublePunctuation, c) < 0) return false;
        char? previous = i == 0 ? null : text[i - 1];
        return c is ';' or '!' or '?'
            ? previous != '\u202F'
            : previous is not ('\u00A0' or '\u202F');
    }

    [Fact]
    public void FrenchStrings_UseTypographicApostrophe()
    {
        var violations = new List<string>();
        foreach (var (name, value) in AllFrenchStrings())
        {
            for (int i = 1; i < value.Length - 1; i++)
            {
                // Apostrophe droite entre deux lettres = apostrophe de texte français
                if (value[i] == '\'' && char.IsLetter(value[i - 1]) && char.IsLetter(value[i + 1]))
                    violations.Add($"{name}: apostrophe droite dans \"{value}\"");
            }
        }
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }
}
