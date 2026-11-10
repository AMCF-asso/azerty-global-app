using System.Text.Json;

namespace AZERTYGlobal;

/// <summary>
/// Anciennes formes des exercices dont le texte à taper, ou le rang dans la leçon, a changé
/// depuis une version publiée (audit de régression du 10/11/2026, RA2). L'empreinte d'un
/// exercice dépend de son texte : sans cette table, un exercice réussi en 1.1.0 ou 1.2.0
/// repassait « à faire » après la mise à jour. Une réussite enregistrée sous une ancienne
/// forme est reprise comme « réussi », sans ses scores.
/// Table générée depuis l'historique git par `scripts/build-lesson-instruction-history.py` :
/// à régénérer quand le texte d'un exercice déjà publié change.
/// </summary>
internal static class LessonContentHistory
{
    public const string ResourceName = "lesson-content-history.json";

    /// <summary>Une ancienne forme : la clé « module/leçon/rang » d'alors et son texte.</summary>
    internal readonly record struct Form(string Key, string Content);

    private static readonly Lazy<IReadOnlyDictionary<string, Form[]>> Table = new(Load);

    /// <summary>Anciennes formes de l'exercice « module/leçon/rang », vide si aucune.</summary>
    public static IReadOnlyList<Form> For(string exerciseKey) =>
        Table.Value.TryGetValue(exerciseKey, out var forms) ? forms : Array.Empty<Form>();

    internal static int Count => Table.Value.Count;

    private static IReadOnlyDictionary<string, Form[]> Load()
    {
        try
        {
            using var stream = typeof(LessonContentHistory).Assembly.GetManifestResourceStream(ResourceName);
            var table = new Dictionary<string, Form[]>(StringComparer.Ordinal);
            if (stream == null) return table;
            // JsonDocument plutôt que JsonSerializer : pas de réflexion sous AOT.
            using var doc = JsonDocument.Parse(stream);
            foreach (var entry in doc.RootElement.EnumerateObject())
            {
                table[entry.Name] = entry.Value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.Object &&
                                   item.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String &&
                                   item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                    .Select(item => new Form(
                        item.GetProperty("key").GetString() ?? "",
                        (item.GetProperty("content").GetString() ?? "").Replace("\r\n", "\n")))
                    .ToArray();
            }
            return table;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // Table illisible : seuls les exercices au texte inchangé gardent leur progression.
            return new Dictionary<string, Form[]>();
        }
    }
}
