using System.Text.Json;

namespace AZERTYGlobal;

/// <summary>
/// Consignes (FR et EN) que chaque exercice a portées avant le 09/11/2026, quand l'empreinte
/// les incluait. Table figée, générée depuis l'historique git par
/// `scripts/build-lesson-instruction-history.py` : modifier une consigne aujourd'hui ne
/// change plus l'empreinte, il n'y a rien à y ajouter.
/// </summary>
internal static class LessonInstructionHistory
{
    public const string ResourceName = "lesson-instruction-history.json";

    private static readonly Lazy<IReadOnlyDictionary<string, string[]>> Table = new(Load);

    /// <summary>Consignes connues de l'exercice « module/leçon/rang », vide si aucune.</summary>
    public static IReadOnlyList<string> For(string exerciseKey) =>
        Table.Value.TryGetValue(exerciseKey, out var instructions) ? instructions : Array.Empty<string>();

    internal static int Count => Table.Value.Count;

    private static IReadOnlyDictionary<string, string[]> Load()
    {
        try
        {
            using var stream = typeof(LessonInstructionHistory).Assembly.GetManifestResourceStream(ResourceName);
            var table = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (stream == null) return table;
            // JsonDocument plutôt que JsonSerializer : pas de réflexion sous AOT.
            using var doc = JsonDocument.Parse(stream);
            foreach (var entry in doc.RootElement.EnumerateObject())
            {
                table[entry.Name] = entry.Value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? "")
                    .ToArray();
            }
            return table;
        }
        catch (JsonException)
        {
            // Table illisible : seules les progressions déjà au nouveau format restent visibles.
            return new Dictionary<string, string[]>();
        }
    }
}
