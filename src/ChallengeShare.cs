// Texte de partage du « Défi du jour » (v1.2.0).
//
// Décision du 2026-08-16 : l'application ne produisait jusqu'ici aucun artefact qui
// puisse circuler. Le défi commun est pourtant seedé par la date et identique pour
// tous les utilisateurs depuis sa conception (cf. DailyChallenge, commentaire d'en-tête) —
// il ne lui manquait qu'une sortie. Le format retenu est du texte brut : il se colle dans
// un salon Discord ou un message sans capture d'écran ni téléversement, là où une image
// demanderait les deux.
//
// Seul le défi commun est partageable. Les cinq séances de « prise en main » dépendent de
// la progression individuelle : deux personnes n'y tapent pas le même extrait, il n'y a
// donc rien à y comparer.
namespace AZERTYGlobal;

static class ChallengeShare
{
    /// <summary>Nombre de caractères difficiles cités dans le texte partagé.</summary>
    private const int HardestCharacterCount = 3;

    /// <summary>
    /// Construit le texte partagé. Fonction pure : aucune lecture de configuration ni
    /// d'horloge, tout arrive par paramètre — c'est ce qui la rend testable.
    ///
    /// <paramref name="credit"/> : attribution de l'extrait (« Victor Hugo, « Les
    /// Misérables » »), null pour les extraits CC0 et maison.
    /// <paramref name="isPersonalBest"/> : la tentative bat le meilleur score enregistré
    /// pour cet exercice. Faux au premier passage — un premier résultat n'est pas un record.
    /// </summary>
    public static string Build(DateOnly date, string? credit, LessonAttemptStats stats, bool isPersonalBest)
    {
        var lines = new List<string> { L.Challenge_ShareTitle(date) };

        var metrics = new List<string>();
        if (stats.Wpm.HasValue) metrics.Add("⌨️ " + L.Challenge_ShareSpeed(stats.Wpm.Value));
        if (stats.AccuracyPercent.HasValue) metrics.Add("🎯 " + L.Challenge_ShareAccuracy(stats.AccuracyPercent.Value));
        metrics.Add("⏱️ " + L.Challenge_ShareSeconds((int)Math.Round(stats.ElapsedSeconds)));
        lines.Add(string.Join(" · ", metrics));

        if (isPersonalBest)
            lines.Add(L.Challenge_ShareRecord);

        var hardest = stats.GetHardestCharacters(HardestCharacterCount);
        lines.Add(hardest.Count == 0
            ? L.Challenge_ShareFlawless
            : L.Challenge_ShareHardest(string.Join(" ", hardest)));

        if (!string.IsNullOrWhiteSpace(credit))
            lines.Add(L.Challenge_ShareCredit(credit));

        lines.Add(L.Challenge_ShareFooter);

        // CRLF : le texte est destiné au presse-papiers Windows, où un LF seul se colle
        // sur une seule ligne dans plusieurs applications.
        return string.Join("\r\n", lines);
    }
}
