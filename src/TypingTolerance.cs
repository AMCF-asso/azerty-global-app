// Tolérance typographique des exercices de découverte (audit 1.3.0, B1, A24 et A25 ;
// décision d'Antoine du 20/10 : « tolérer », la typographie viendra par un exercice dédié).
namespace AZERTYGlobal;

/// <summary>
/// Un seul test d'équivalence entre le caractère tapé et le caractère attendu. Là où le texte
/// demande une espace insécable (U+00A0) ou fine insécable (U+202F), les trois espaces sont
/// acceptées l'une pour l'autre ; là où il demande l'apostrophe typographique (U+2019),
/// l'apostrophe droite (U+0027) est acceptée, pas l'inverse. Le texte affiché garde sa
/// typographie. Changer cette règle ne touche que ce fichier.
///
/// Exercices concernés : Leçons › Initiation (les huit exercices de l'accueil), Défi du jour
/// et « Votre texte ». Les leçons du catalogue, dont « typographie-francaise », et les
/// séances de révision restent strictes.
/// </summary>
internal static class TypingTolerance
{
    public static bool Accepts(char typed, char expected)
    {
        if (typed == expected) return true;
        if (expected is ' ' or ' ')
            return typed is ' ' or ' ' or ' ';
        return expected == '’' && typed == '\'';
    }

    /// <summary>Vrai quand la frappe n'est pas le caractère attendu mais passe grâce à la
    /// tolérance : comptée juste pour la précision, et en faute pour la progression par geste,
    /// qui ramène le caractère dans la Révision du jour.</summary>
    public static bool IsTolerated(char typed, char expected) => typed != expected && Accepts(typed, expected);

    public static bool AppliesTo(LessonExercise exercise) =>
        exercise.ModuleId is LessonCatalogLoader.InitiationModuleId
            or DailyChallenge.ModuleId
            or LessonCatalogLoader.CustomTextModuleId;
}
