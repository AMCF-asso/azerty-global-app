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
        new("GRÂCE À AZERTY GLOBAL, ÉCRIRE EN FRANÇAIS EST TRÈS FACILE !", KeepCapsLock: true, Skippable: false),
        new("jean.dupont@education.gouv.fr", KeepCapsLock: false, Skippable: false),
        new("Lætitia demande « d'où vient ce chef-d'œuvre… » — elle l'approuve à 100 %.", KeepCapsLock: false, Skippable: false),
        new("type Config = { items: string[]; sep: \"~\" | \"\\\\\" };", KeepCapsLock: false, Skippable: true),
        new("São Paulo, Córdoba, Tromsø, Łódź, lunedì, Größe", KeepCapsLock: false, Skippable: true),
    };

    /// <summary>
    /// Nombre d'exercices réussis à partir duquel le tutoriel compte pour fait : l'accueil ne
    /// propose plus « Essayer maintenant » en premier, et ne se rouvre plus au démarrage.
    /// </summary>
    public const int DoneThreshold = 3;
}
