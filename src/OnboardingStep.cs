// Étapes de l'accueil 1.3.0.
namespace AZERTYGlobal;

/// <summary>
/// Les étapes de l'accueil, dans l'ordre. <c>OnboardingWindow._currentStep</c> en porte l'indice.
/// </summary>
internal static class OnboardingStep
{
    internal const int Changes = 0;
    internal const int HowTo = 1;
    // Décision d'Antoine du 11/11 : l'étape « Écrire le peul, le wolof, le bambara » est retirée
    // pour tous. La Prise en main demande déjà l'usage (case « Une langue d'Afrique ») et en fait
    // l'exercice ; l'étape Ressources garde un lien vers la page /afrique.
    internal const int Resources = 2;

    internal const int Count = 3;

    /// <summary>La dernière étape : ressources, préférences et bouton final.</summary>
    internal const int Last = Count - 1;

    /// <summary>« Étape 2 sur 3 » pour l'indice 1.</summary>
    internal static string Label(int step) => L.Onboarding_StepOf(step + 1, Count);
}
