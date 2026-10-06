namespace AZERTYGlobal;

/// <summary>
/// Les six exercices du tutoriel, en un seul exemplaire : texte à taper, Verr. Maj. gardée,
/// exercice facultatif. Le tutoriel de l'accueil et le module « Initiation » des Leçons les
/// lisent ici, chacun avec ses consignes (audit du 25/09, L-12 : les textes étaient recopiés
/// dans LearningModule et LessonCatalog). La progression enregistrée dépend du texte (clé et
/// empreinte des exercices, rang pour l'import de l'accueil) : changer un texte, ou l'ordre,
/// efface la réussite déjà enregistrée de cet exercice.
/// </summary>
internal static class TutorialSteps
{
    internal readonly record struct Step(string Target, bool KeepCapsLock, bool Skippable);

    public static readonly Step[] All =
    {
        new("É", KeepCapsLock: true, Skippable: false),
        // Textes du testeur choisis le 29/09 (operations/2026-09-29-contenu-lecons/decisions.md,
        // lot 1) : tronc commun, puis Documents, Code et Autres langues (choix du 06/10).
        new("ÇA GÈLE À MONTRÉAL, PAS À ABIDJAN\u202F!", KeepCapsLock: true, Skippable: false),
        new("contact@exemple.com", KeepCapsLock: false, Skippable: false),
        new("«\u00A0Un chef-d\u2019œuvre\u202F!\u00A0» — Maman", KeepCapsLock: false, Skippable: false),
        new("if (a || b) { t[0] = \"\\n\"; }", KeepCapsLock: false, Skippable: true),
        new("María et Niccolò arrivent à São Paulo.", KeepCapsLock: false, Skippable: true),
    };

    /// <summary>
    /// Nombre d'exercices réussis à partir duquel le tutoriel compte pour fait : l'accueil ne
    /// propose plus « Essayer maintenant » en premier, et ne se rouvre plus au démarrage.
    /// </summary>
    public const int DoneThreshold = 3;
}
