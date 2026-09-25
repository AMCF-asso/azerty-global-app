using System.Text.Json;

namespace AZERTYGlobal;

/// <summary>Une façon de taper un caractère, lue dans character-index.json.</summary>
internal sealed class MethodData
{
    public string Type { get; init; } = "";       // "direct", "deadkey", "deadkey_activation"
    public string Key { get; init; } = "";        // Web API key code (ex: "KeyQ", "Digit2")
    public string Layer { get; init; } = "";      // "Base", "Shift", "AltGr", etc.
    public string DeadKey { get; init; } = "";    // dk name (pour type="deadkey")
    // Pour type="deadkey" : comment activer la touche morte
    public string DkActivationKey { get; init; } = "";
    public string DkActivationLayer { get; init; } = "";
}

/// <summary>
/// Index des caractères (ressource character-index.json), lu une seule fois, à la première
/// demande, puis partagé en lecture seule par la recherche, le clavier virtuel, le tutoriel
/// et les Leçons. Audit du 25/09, V-04 et L-03 : cinq chargeurs le lisaient chacun à sa façon.
/// </summary>
internal sealed class CharacterIndex
{
    internal sealed class Entry
    {
        public string Character { get; init; } = "";
        public string CodePoint { get; init; } = "";
        public string NameFr { get; init; } = "";
        public string NameEn { get; init; } = "";
        public string[] FrenchAliases { get; init; } = Array.Empty<string>();
        public string[] EnglishAliases { get; init; } = Array.Empty<string>();
        /// <summary>Toutes les méthodes, dans l'ordre du fichier.</summary>
        public List<MethodData> Methods { get; init; } = new();
        /// <summary>Première méthode marquée « recommended », sinon null.</summary>
        public MethodData? Recommended { get; init; }
        /// <summary>Première méthode sur une couche Caps (exercices KeepCapsHighlight).</summary>
        public MethodData? Caps { get; init; }

        /// <summary>La méthode recommandée, sinon la première.</summary>
        public MethodData? Preferred => Recommended ?? (Methods.Count > 0 ? Methods[0] : null);
    }

    private static readonly Lazy<CharacterIndex> _shared = new(Load);

    public static CharacterIndex Shared => _shared.Value;

    /// <summary>Caractères (hors entrées « dk: »), dans l'ordre du fichier.</summary>
    public List<Entry> Entries { get; } = new();

    public Dictionary<string, Entry> ByCharacter { get; } = new(StringComparer.Ordinal);

    /// <summary>Touche morte (ex. « dk_tilde ») → touche et couche qui l'activent.</summary>
    public Dictionary<string, (string Key, string Layer)> DeadKeyActivations { get; } = new(StringComparer.Ordinal);

    /// <summary>Caractère → méthodes par touche morte (pour l'étape 2 d'une séquence).</summary>
    public Dictionary<string, List<MethodData>> DeadKeyMethodsByCharacter { get; } = new(StringComparer.Ordinal);

    /// <summary>Caractère → noms FR/EN, pour les caractères qui en ont au moins un.</summary>
    public Dictionary<string, (string Fr, string En)> Names { get; } = new(StringComparer.Ordinal);

    private static CharacterIndex Load()
    {
        var index = new CharacterIndex();
        try
        {
            using var stream = typeof(CharacterIndex).Assembly.GetManifestResourceStream("character-index.json");
            if (stream == null)
                return index;
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.TryGetProperty("characters", out var characters)
                && characters.ValueKind == JsonValueKind.Object)
                index.Fill(characters);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            ConfigManager.Log("CharacterIndex.Load", ex);
            return new CharacterIndex();
        }
        return index;
    }

    private void Fill(JsonElement characters)
    {
        // Première passe : activations de touches mortes (la première de chaque entrée « dk: »).
        foreach (var entry in characters.EnumerateObject())
        {
            if (!entry.Name.StartsWith("dk:", StringComparison.Ordinal)) continue;
            foreach (var method in Methods(entry.Value))
            {
                if (ReadString(method, "type") != "deadkey_activation") continue;
                DeadKeyActivations[ReadString(method, "deadkey")] = (ReadString(method, "key"), ReadString(method, "layer"));
                break;
            }
        }

        // Deuxième passe : les caractères. Les entrées « dk: » ne sont pas proposées
        // (écart voulu avec le site : audit du 25/09, V-05).
        foreach (var entry in characters.EnumerateObject())
        {
            if (entry.Name.StartsWith("dk:", StringComparison.Ordinal)) continue;

            var methods = new List<MethodData>();
            var deadKeyMethods = new List<MethodData>();
            MethodData? recommended = null;
            MethodData? caps = null;
            foreach (var method in Methods(entry.Value))
            {
                var data = CreateMethodData(method);
                methods.Add(data);
                if (recommended == null
                    && method.TryGetProperty("recommended", out var rec) && rec.ValueKind == JsonValueKind.True)
                    recommended = data;
                if (caps == null && data.Layer.StartsWith("Caps", StringComparison.Ordinal))
                    caps = data;
                if (data.Type == "deadkey" && data.DeadKey.Length > 0)
                    deadKeyMethods.Add(data);
            }

            var item = new Entry
            {
                Character = entry.Name,
                CodePoint = ReadString(entry.Value, "codePoint"),
                NameFr = ReadString(entry.Value, "unicodeNameFr"),
                NameEn = ReadString(entry.Value, "unicodeName"),
                FrenchAliases = ReadStrings(entry.Value, "frenchAliases"),
                EnglishAliases = ReadStrings(entry.Value, "englishAliases"),
                Methods = methods,
                Recommended = recommended,
                Caps = caps,
            };
            Entries.Add(item);
            ByCharacter[item.Character] = item;
            if (deadKeyMethods.Count > 0)
                DeadKeyMethodsByCharacter[item.Character] = deadKeyMethods;
            if (item.NameFr.Length > 0 || item.NameEn.Length > 0)
                Names[item.Character] = (item.NameFr, item.NameEn);
        }
    }

    private MethodData CreateMethodData(JsonElement method)
    {
        var type = ReadString(method, "type");
        if (type != "deadkey")
            return new MethodData { Type = type, Key = ReadString(method, "key"), Layer = ReadString(method, "layer") };

        var deadKey = ReadString(method, "deadkey");
        DeadKeyActivations.TryGetValue(deadKey, out var activation);
        return new MethodData
        {
            Type = type,
            Key = ReadString(method, "key"),
            Layer = ReadString(method, "layer"),
            DeadKey = deadKey,
            DkActivationKey = activation.Key ?? "",
            DkActivationLayer = activation.Layer ?? "",
        };
    }

    private static IEnumerable<JsonElement> Methods(JsonElement entry) =>
        entry.TryGetProperty("methods", out var methods) && methods.ValueKind == JsonValueKind.Array
            ? methods.EnumerateArray()
            : Enumerable.Empty<JsonElement>();

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static string[] ReadStrings(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var values) || values.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        var list = new List<string>();
        foreach (var value in values.EnumerateArray())
            if (value.ValueKind == JsonValueKind.String && value.GetString() is string s)
                list.Add(s);
        return list.ToArray();
    }
}
