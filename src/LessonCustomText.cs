// Leçon depuis ton texte (refonte 1.3.0, décision d'Antoine du 07/10) : un texte collé
// par l'utilisateur devient un exercice à taper en Tablature.
using System.Globalization;
using System.Text;

namespace AZERTYGlobal;

/// <summary>Résultat de la préparation : l'exercice (null si rien n'est tapable) et les
/// caractères retirés parce que la disposition ne sait pas les taper.</summary>
internal sealed record LessonCustomTextResult(LessonExercise? Exercise, IReadOnlyList<string> Removed);

internal static class LessonCustomText
{
    public const int MaxLines = 12;

    /// <summary>
    /// Prépare le texte : forme composée (NFC), tabulations et espaces multiples ramenés à
    /// une espace, caractères absents de l'index retirés et listés, lignes coupées aux mots
    /// vers <see cref="LessonReviewBuilder.TargetLineLength"/> caractères, au plus
    /// <see cref="MaxLines"/> lignes.
    /// </summary>
    internal static LessonCustomTextResult Build(string text, string instruction, Func<string, bool> isTypable)
    {
        string normalized = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Normalize(NormalizationForm.FormC);
        var removed = new List<string>();
        var kept = new StringBuilder(normalized.Length);
        var elements = StringInfo.GetTextElementEnumerator(normalized);
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            if (element is "\n" or "\t")
            {
                kept.Append(element == "\n" ? '\n' : ' ');
                continue;
            }
            // Un élément peut être une lettre suivie de diacritiques combinants (« ɛ̀ ») :
            // tapable si chacun de ses caractères l'est. Les paires de substitution (émojis)
            // ne le sont jamais : la séance compare caractère UTF-16 par caractère.
            bool typable = !element.Any(char.IsSurrogate) && element.All(c => isTypable(c.ToString()));
            if (typable)
                kept.Append(element);
            else if (!removed.Contains(element, StringComparer.Ordinal))
                removed.Add(element);
        }

        var lines = new List<string>();
        foreach (string paragraph in kept.ToString().Split('\n'))
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var line = new StringBuilder();
            foreach (string word in words)
            {
                foreach (string piece in Chunk(word, LessonReviewBuilder.TargetLineLength))
                {
                    if (line.Length > 0 && line.Length + 1 + piece.Length > LessonReviewBuilder.TargetLineLength)
                    {
                        lines.Add(line.ToString());
                        line.Clear();
                    }
                    if (line.Length > 0) line.Append(' ');
                    line.Append(piece);
                }
            }
            if (line.Length > 0) lines.Add(line.ToString());
        }

        if (lines.Count > MaxLines) lines.RemoveRange(MaxLines, lines.Count - MaxLines);
        if (lines.Count == 0) return new LessonCustomTextResult(null, removed);

        var exercise = new LessonExercise(
            LessonCatalogLoader.CustomTextModuleId,
            "texte",
            0,
            "practice",
            instruction,
            string.Join('\n', lines),
            LessonTypingMode.Flexible);
        return new LessonCustomTextResult(exercise, removed);
    }

    internal static LessonCustomTextResult Build(string text, string instruction) =>
        Build(text, instruction, character => CharacterIndex.Shared.ByCharacter.ContainsKey(character));

    /// <summary>Un mot plus long qu'une ligne est coupé, sans séparer une lettre de ses
    /// diacritiques.</summary>
    private static IEnumerable<string> Chunk(string word, int size)
    {
        if (word.Length <= size)
        {
            yield return word;
            yield break;
        }
        var piece = new StringBuilder();
        var elements = StringInfo.GetTextElementEnumerator(word);
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            if (piece.Length + element.Length > size)
            {
                yield return piece.ToString();
                piece.Clear();
            }
            piece.Append(element);
        }
        if (piece.Length > 0) yield return piece.ToString();
    }
}
