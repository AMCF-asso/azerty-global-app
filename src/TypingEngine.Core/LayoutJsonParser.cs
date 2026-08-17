using System.Globalization;
using System.Text.Json;

namespace TypingEngine.Core;

/// <summary>Parses the shared declarative layout format without loading app resources.</summary>
public static class LayoutJsonParser
{
    public static Layout Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var document = JsonDocument.Parse(stream);
        return Parse(document.RootElement);
    }

    public static Layout Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        return Parse(document.RootElement);
    }

    private static Layout Parse(JsonElement root)
    {
        var layout = new Layout();
        int rowIndex = 0;
        foreach (var row in RequireArray(root, "rows", "rows"))
        {
            string rowPath = $"rows[{rowIndex++}]";
            int keyIndex = 0;
            foreach (var key in RequireArray(row, "keys", $"{rowPath}.keys"))
            {
                string keyPath = $"{rowPath}.keys[{keyIndex++}]";
                string scancodePath = $"{keyPath}.scancode";
                uint scancode = ParseScancode(RequireString(key, "scancode", scancodePath), scancodePath);
                layout.Keys[scancode] = new KeyDefinition
                {
                    Position = RequireString(key, "position", $"{keyPath}.position"),
                    Scancode = scancode,
                    Base = GetStringOrNull(key, "base"),
                    Shift = GetStringOrNull(key, "shift"),
                    AltGr = GetStringOrNull(key, "alt_gr"),
                    ShiftAltGr = GetStringOrNull(key, "shift_alt_gr"),
                    Caps = GetStringOrNull(key, "caps"),
                    CapsShift = GetStringOrNull(key, "caps_shift"),
                    CapsAltGr = GetStringOrNull(key, "caps_alt_gr"),
                    CapsShiftAltGr = GetStringOrNull(key, "caps_shift_alt_gr")
                };
            }
        }

        // dead_keys stays optional here although the schema requires it: a layout without
        // composition is still a layout, and the app can render one.
        if (!root.TryGetProperty("dead_keys", out var deadKeys)) return layout;
        RequireKind(deadKeys, JsonValueKind.Object, "dead_keys", "an object");
        foreach (var property in deadKeys.EnumerateObject())
        {
            var deadKey = new DeadKeyDefinition
            {
                Name = property.Name,
                Description = GetStringOrNull(property.Value, "description") ?? property.Name
            };
            if (property.Value.TryGetProperty("table", out var table))
            {
                string tablePath = $"dead_keys.{property.Name}.table";
                RequireKind(table, JsonValueKind.Object, tablePath, "an object");
                foreach (var entry in table.EnumerateObject())
                    deadKey.Table[entry.Name] = RequireString(entry.Value, $"{tablePath}.{entry.Name}");
            }
            layout.DeadKeys[property.Name] = deadKey;
        }
        return layout;
    }

    private static uint ParseScancode(string value, string path)
    {
        try
        {
            if (value.StartsWith("SC", StringComparison.OrdinalIgnoreCase))
                return uint.Parse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.Parse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return uint.Parse(value, CultureInfo.InvariantCulture);
        }
        catch (Exception error) when (error is FormatException or OverflowException)
        {
            // OverflowException is reachable from a file the schema accepted until its decimal
            // pattern was narrowed to nine digits: ten digits fit the pattern and not a uint.
            throw new LayoutFormatException(
                path,
                $"'{value}' is neither SC-prefixed hex, 0x-prefixed hex, nor a decimal a 32-bit "
                    + "unsigned integer can hold");
        }
    }

    /// <summary>Reads a property the parser cannot do without, naming it when it is absent or
    /// holds the wrong kind. JsonElement.GetProperty would raise a KeyNotFoundException saying
    /// only that some key was missing from some dictionary.</summary>
    private static JsonElement.ArrayEnumerator RequireArray(JsonElement parent, string property, string path)
    {
        var value = RequireProperty(parent, property, path);
        RequireKind(value, JsonValueKind.Array, path, "an array");
        return value.EnumerateArray();
    }

    private static string RequireString(JsonElement parent, string property, string path) =>
        RequireString(RequireProperty(parent, property, path), path);

    private static string RequireString(JsonElement value, string path)
    {
        RequireKind(value, JsonValueKind.String, path, "a string");
        return value.GetString()!;
    }

    private static JsonElement RequireProperty(JsonElement parent, string property, string path)
    {
        if (parent.ValueKind != JsonValueKind.Object)
            throw new LayoutFormatException(path, $"expected an object holding it, found {parent.ValueKind}");
        if (!parent.TryGetProperty(property, out var value))
            throw new LayoutFormatException(path, "property is missing");
        return value;
    }

    private static void RequireKind(JsonElement value, JsonValueKind expected, string path, string label)
    {
        if (value.ValueKind != expected)
            throw new LayoutFormatException(path, $"expected {label}, found {value.ValueKind}");
    }

    private static string? GetStringOrNull(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
