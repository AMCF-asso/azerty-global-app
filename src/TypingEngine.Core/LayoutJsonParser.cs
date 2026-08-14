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
        foreach (var row in root.GetProperty("rows").EnumerateArray())
        {
            foreach (var key in row.GetProperty("keys").EnumerateArray())
            {
                string text = key.GetProperty("scancode").GetString() ?? "0";
                uint scancode = ParseScancode(text);
                layout.Keys[scancode] = new KeyDefinition
                {
                    Position = key.GetProperty("position").GetString() ?? "",
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

        if (!root.TryGetProperty("dead_keys", out var deadKeys)) return layout;
        foreach (var property in deadKeys.EnumerateObject())
        {
            var deadKey = new DeadKeyDefinition
            {
                Name = property.Name,
                Description = GetStringOrNull(property.Value, "description") ?? property.Name
            };
            if (property.Value.TryGetProperty("table", out var table))
            {
                foreach (var entry in table.EnumerateObject())
                    deadKey.Table[entry.Name] = entry.Value.GetString() ?? "";
            }
            layout.DeadKeys[property.Name] = deadKey;
        }
        return layout;
    }

    private static uint ParseScancode(string value)
    {
        if (value.StartsWith("SC", StringComparison.OrdinalIgnoreCase))
            return uint.Parse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.Parse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return uint.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string? GetStringOrNull(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
