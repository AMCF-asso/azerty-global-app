// Séance de révision (progression par geste, refonte 1.3.0) : quelques lignes tirées des
// textes des Leçons, choisies pour faire repasser les caractères échus.
namespace AZERTYGlobal;

internal static class LessonReviewBuilder
{
    /// <summary>Longueur visée d'une ligne : celle des lignes des leçons du site.</summary>
    internal const int TargetLineLength = 40;
    /// <summary>Chaque caractère révisé revient au moins autant de fois.</summary>
    internal const int RepetitionsPerCharacter = 3;
    internal const int MaxLines = 4;

    /// <summary>
    /// Compose la séance : des mots des leçons qui contiennent les caractères à revoir,
    /// les plus fragiles en tête, jusqu'à <see cref="RepetitionsPerCharacter"/> passages
    /// chacun. Null si aucun mot ne convient (rien à revoir, ou caractère absent des textes).
    /// Le tirage dépend de <paramref name="seed"/> : la même séance pour un même jour.
    /// </summary>
    internal static LessonExercise? Build(
        LessonCatalog catalog,
        IReadOnlyList<string> characters,
        string instruction,
        int seed)
    {
        if (characters.Count == 0) return null;

        var words = catalog.Exercises
            .Where(exercise => !LessonCatalogLoader.IsComposed(exercise.ModuleId))
            .SelectMany(exercise => exercise.Content.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            .Where(word => word.Length <= TargetLineLength)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(word => word, StringComparer.Ordinal)
            .ToArray();

        var random = new Random(seed);
        var picked = new List<string>();
        foreach (string character in characters)
        {
            var candidates = words.Where(word => word.Contains(character, StringComparison.Ordinal)).ToArray();
            if (candidates.Length == 0) continue;
            int seen = picked.Sum(word => Occurrences(word, character));
            var order = candidates.OrderBy(_ => random.Next()).ToArray();
            for (int i = 0; seen < RepetitionsPerCharacter && i < order.Length * RepetitionsPerCharacter; i++)
            {
                string word = order[i % order.Length];
                picked.Add(word);
                seen += Occurrences(word, character);
            }
        }

        if (picked.Count == 0) return null;

        // Les mots mêlés : alterner les caractères fait travailler le geste, pas la mémoire
        // d'une suite.
        var shuffled = picked.OrderBy(_ => random.Next()).ToList();
        var lines = new List<string>();
        var line = new System.Text.StringBuilder();
        foreach (string word in shuffled)
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > TargetLineLength)
            {
                lines.Add(line.ToString());
                line.Clear();
                if (lines.Count == MaxLines) break;
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0 && lines.Count < MaxLines) lines.Add(line.ToString());

        return new LessonExercise(
            LessonCatalogLoader.ReviewModuleId,
            "seance",
            0,
            "practice",
            instruction,
            string.Join('\n', lines),
            LessonTypingMode.Flexible);
    }

    private static int Occurrences(string word, string character)
    {
        int count = 0;
        for (int index = word.IndexOf(character, StringComparison.Ordinal);
             index >= 0;
             index = word.IndexOf(character, index + character.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
