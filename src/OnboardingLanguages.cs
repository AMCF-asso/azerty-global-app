// Accueil 1.3.0, direction C (décision d'Antoine du 08/10) : l'étape « Écrire le peul, le
// wolof, le bambara », insérée entre les 5 changements et le mode d'emploi.
using System.Globalization;

namespace AZERTYGlobal;

/// <summary>
/// Les étapes de l'accueil, dans l'ordre. <c>OnboardingWindow._currentStep</c> en porte l'indice.
/// </summary>
internal static class OnboardingStep
{
    internal const int Changes = 0;
    internal const int Languages = 1;
    internal const int HowTo = 2;
    internal const int Resources = 3;

    internal const int Count = 4;

    /// <summary>La dernière étape : ressources, préférences et bouton final.</summary>
    internal const int Last = Count - 1;

    /// <summary>« Étape 2 sur 4 » pour l'indice 1.</summary>
    internal static string Label(int step) => L.Onboarding_StepOf(step + 1, Count);
}

/// <summary>Une langue de l'étape : son nom, ses lettres, et celle dont on montre le geste.</summary>
internal readonly record struct OnboardingLanguage(Func<string> Name, string Letters, string Example);

/// <summary>
/// Contenu de l'étape « Vos langues ». Le geste de chaque exemple n'est pas écrit ici : il est
/// lu dans character-index.json par <see cref="Gesture.Of(string)"/>, comme dans la recherche
/// et les Leçons, et ne peut donc pas diverger du clavier.
/// </summary>
internal static class OnboardingLanguages
{
    internal static readonly OnboardingLanguage[] All =
    {
        new(() => L.Onboarding_LangFula, "ɓ ɗ ƴ ñ ŋ", "ɓ"),           // ɓ ɗ ƴ ñ ŋ
        new(() => L.Onboarding_LangWolof, "ñ ŋ ë ó à é", "ñ"),   // ñ ŋ ë ó à é
        new(() => L.Onboarding_LangBambara, "ɛ ɔ ɲ ŋ", "ɛ"),               // ɛ ɔ ɲ ŋ
    };

    /// <summary>Les lettres d'une langue, sans les espaces d'affichage.</summary>
    internal static IEnumerable<string> LettersOf(OnboardingLanguage language)
        => language.Letters.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Texte lu par le lecteur d'écran pour l'étape : chaque langue, ses lettres, puis le geste
    /// de son exemple en toutes lettres (« ɓ : Frappez ù : rien ne s'affiche encore. Puis
    /// frappez B. »). Le tableau peint en GDI n'est pas exposé autrement.
    /// </summary>
    internal static string AccessibleSummary()
    {
        var parts = new List<string>(All.Length + 1) { L.Onboarding_LanguagesSubtitle };
        foreach (var language in All)
        {
            string gesture = Gesture.PlainSentence(Gesture.Of(language.Example));
            parts.Add(L.Onboarding_LanguageSummary(language.Name(), language.Letters, language.Example, gesture));
        }
        return string.Join(" ", parts);
    }
}

/// <summary>Résultat de l'essai : rien de tapé, la bonne lettre, ou une autre.</summary>
internal enum LetterTrialResult { Empty, Match, Other }

/// <summary>
/// L'essai de l'étape « Vos langues » : ù puis b doit donner ɓ. Le champ ne garde que la
/// dernière lettre tapée, affichée en grand ; une nouvelle frappe remplace la précédente.
/// </summary>
internal static class LetterTrial
{
    /// <summary>La lettre attendue : b crocheté (U+0253).</summary>
    internal const string Target = "ɓ";

    /// <summary>Sa capitale (U+0181), acceptée aussi : Verr. Maj. peut être actif.</summary>
    internal const string TargetCapital = "Ɓ";

    /// <summary>
    /// Le dernier caractère affichable du texte (un élément textuel entier, accent combinant
    /// compris), ou une chaîne vide. Les espaces et contrôles ne comptent pas.
    /// </summary>
    internal static string LastLetter(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string last = "";
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            if (element.Length > 0 && !char.IsWhiteSpace(element[0]) && !char.IsControl(element[0]))
                last = element;
        }
        return last;
    }

    internal static LetterTrialResult Evaluate(string? text)
    {
        string letter = LastLetter(text);
        if (letter.Length == 0) return LetterTrialResult.Empty;
        return letter is Target or TargetCapital ? LetterTrialResult.Match : LetterTrialResult.Other;
    }

    /// <summary>Message sous le champ, vide quand rien n'est tapé.</summary>
    internal static string Message(string? text) => Evaluate(text) switch
    {
        LetterTrialResult.Match => L.Onboarding_TrialMatch(LastLetter(text)),
        LetterTrialResult.Other => L.Onboarding_TrialOther,
        _ => "",
    };
}
