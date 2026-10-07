using System.Text.Json;

namespace AZERTYGlobal;

/// <summary>
/// Table de la recherche par langue (ressource language-search.json) : pour chaque caractère,
/// les noms de langue qui l'utilisent (« pulaar », « wolof », « soussou »…) et, pour les
/// accents combinants, les noms de ton (« ton », « tone », « ton haut »…).
///
/// La table est générée par scripts/build-language-search.py depuis le référentiel Afrique du
/// site (data/afrique/*.json, révision inscrite dans la table) ; elle n'écrit aucun alphabet.
/// character-index.json, copie du site sous contrôle de provenance, n'est pas modifié.
/// </summary>
internal sealed class LanguageSearchTable
{
    /// <summary>Alias d'un caractère, rangés selon leur nature.</summary>
    internal sealed class Aliases
    {
        /// <summary>Noms de langue (« pulaar », « fula »…).</summary>
        public List<string> Languages { get; } = new();
        /// <summary>Noms de signe (« ton », « ton haut », « accent grave combinant »…).</summary>
        public List<string> Signs { get; } = new();
    }

    private static readonly Lazy<LanguageSearchTable> _shared = new(Load);

    public static LanguageSearchTable Shared => _shared.Value;

    /// <summary>Caractère → alias. Un caractère absent n'a aucun alias de langue.</summary>
    public Dictionary<string, Aliases> ByCharacter { get; } = new(StringComparer.Ordinal);

    private static LanguageSearchTable Load()
    {
        var table = new LanguageSearchTable();
        try
        {
            using var stream = typeof(LanguageSearchTable).Assembly.GetManifestResourceStream("language-search.json");
            if (stream == null)
                return table;
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.TryGetProperty("groupes", out var groups) && groups.ValueKind == JsonValueKind.Array)
                table.Fill(groups);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            ConfigManager.Log("LanguageSearchTable.Load", ex);
            return new LanguageSearchTable();
        }
        return table;
    }

    private void Fill(JsonElement groups)
    {
        foreach (var group in groups.EnumerateArray())
        {
            bool sign = group.TryGetProperty("type", out var type) && type.GetString() == "signe";
            var names = new List<string>();
            if (group.TryGetProperty("alias", out var aliases) && aliases.ValueKind == JsonValueKind.Array)
                foreach (var alias in aliases.EnumerateArray())
                    if (alias.TryGetProperty("alias", out var text) && text.GetString() is { Length: > 0 } s)
                        names.Add(s);
            if (names.Count == 0 || !group.TryGetProperty("caracteres", out var chars) || chars.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var ch in chars.EnumerateArray())
            {
                if (ch.GetString() is not { Length: > 0 } character) continue;
                if (!ByCharacter.TryGetValue(character, out var entry))
                    ByCharacter[character] = entry = new Aliases();
                var target = sign ? entry.Signs : entry.Languages;
                foreach (var name in names)
                    if (!target.Contains(name))
                        target.Add(name);
            }
        }
    }
}
