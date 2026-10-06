namespace AZERTYGlobal;

/// <summary>
/// Les huit exercices du tutoriel, en un seul exemplaire : texte à taper, Verr. Maj. gardée.
/// Le tutoriel de l'accueil et le module « Initiation » des Leçons les lisent ici, chacun avec
/// ses consignes (audit du 25/09, L-12 : les textes étaient recopiés dans LearningModule et
/// LessonCatalog). L'Initiation les reprend tous dans cet ordre ; le tutoriel n'en joue que le
/// parcours choisi (<see cref="BuildRoute"/>). La progression enregistrée dépend du texte (clé
/// et empreinte des exercices, rang pour l'import de l'accueil) : changer un texte, ou l'ordre,
/// efface la réussite déjà enregistrée de cet exercice.
/// </summary>
internal static class TutorialSteps
{
    internal readonly record struct Step(string Target, bool KeepCapsLock);

    // Rangs fixes, ceux de l'Initiation des Leçons et de l'import de l'accueil.
    public const int Documents = 3;
    public const int Code = 4;
    public const int Languages = 5;
    public const int Messages = 6;
    public const int Africa = 7;

    public static readonly Step[] All =
    {
        new("É", KeepCapsLock: true),
        // Textes du testeur choisis le 29/09 (operations/2026-09-29-contenu-lecons/decisions.md,
        // lot 1) : tronc commun, puis un exercice par usage (choix du 06/10).
        new("ÇA GÈLE À MONTRÉAL, PAS À ABIDJAN\u202F!", KeepCapsLock: true),
        new("contact@exemple.com", KeepCapsLock: false),
        new("«\u00A0Un chef-d\u2019œuvre\u202F!\u00A0» — Maman", KeepCapsLock: false),
        new("if (a || b) { t[0] = \"\\n\"; }", KeepCapsLock: false),
        new("María et Niccolò arrivent à São Paulo.", KeepCapsLock: false),
        // Ajoutés le 06/10, après les six premiers : leurs rangs ne déplacent aucune réussite.
        new("À table\u202F! Ça refroidit\u202F!", KeepCapsLock: false),
        new("ɛ ɔ ŋ ɲ ɓ ɗ", KeepCapsLock: false),
    };

    /// <summary>
    /// Nombre d'exercices réussis à partir duquel le tutoriel compte pour fait : l'accueil ne
    /// propose plus « Essayer maintenant » en premier, et ne se rouvre plus au démarrage. C'est
    /// aussi la longueur du tronc commun (rangs 0 à 2), joué quel que soit l'usage.
    /// </summary>
    public const int DoneThreshold = 3;

    // Usages de l'écran de choix (identifiants enregistrés dans la config), du plus particulier
    // au plus courant : le premier coché donne l'exercice principal, les autres suivent en bonus.
    public const string UsageDocuments = "documents";
    public const string UsageMessages = "messages";
    public const string UsageCode = "code";
    public const string UsageLanguages = "langues";
    public const string UsageAfrica = "afrique";

    public static readonly (string Usage, int Step)[] UsagesByPriority =
    {
        (UsageAfrica, Africa),
        (UsageCode, Code),
        (UsageLanguages, Languages),
        (UsageDocuments, Documents),
        (UsageMessages, Messages),
    };

    /// <summary>Un exercice du parcours : son rang fixe et s'il est facultatif (bonus).</summary>
    internal readonly record struct RouteStep(int Index, bool Skippable);

    /// <summary>
    /// Parcours du tutoriel pour les usages cochés : le tronc commun, puis l'exercice de l'usage
    /// le plus particulier, puis ceux des autres usages cochés, facultatifs, dans l'ordre de
    /// <see cref="UsagesByPriority"/>. Aucun usage reconnu : l'exercice Documents.
    /// </summary>
    public static RouteStep[] BuildRoute(IEnumerable<string>? usages)
    {
        var chosen = new HashSet<string>(usages ?? Array.Empty<string>(), StringComparer.Ordinal);
        var route = new List<RouteStep>();
        for (int i = 0; i < DoneThreshold; i++)
            route.Add(new RouteStep(i, Skippable: false));

        bool main = true;
        foreach (var (usage, step) in UsagesByPriority)
        {
            if (!chosen.Contains(usage)) continue;
            route.Add(new RouteStep(step, Skippable: !main));
            main = false;
        }
        if (main)
            route.Add(new RouteStep(Documents, Skippable: false));
        return route.ToArray();
    }

    /// <summary>Les usages connus parmi <paramref name="usages"/>, sans doublon, dans l'ordre
    /// de l'écran de choix ; tout le reste est ignoré (config modifiée à la main).</summary>
    public static string[] NormalizeUsages(IEnumerable<string>? usages)
    {
        var chosen = new HashSet<string>(usages ?? Array.Empty<string>(), StringComparer.Ordinal);
        return new[] { UsageDocuments, UsageMessages, UsageCode, UsageLanguages, UsageAfrica }
            .Where(chosen.Contains)
            .ToArray();
    }
}
