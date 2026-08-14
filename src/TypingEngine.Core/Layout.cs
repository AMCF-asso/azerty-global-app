namespace TypingEngine.Core;

/// <summary>A declarative keyboard layout, independent from any operating system.</summary>
public sealed class Layout
{
    public Dictionary<uint, KeyDefinition> Keys { get; } = new();
    public Dictionary<string, DeadKeyDefinition> DeadKeys { get; } = new();
}

/// <summary>A physical key and its outputs across the eight supported modifier layers.</summary>
public sealed class KeyDefinition
{
    public string Position { get; set; } = "";
    public uint Scancode { get; set; }
    public string? Base { get; set; }
    public string? Shift { get; set; }
    public string? AltGr { get; set; }
    public string? ShiftAltGr { get; set; }
    public string? Caps { get; set; }
    public string? CapsShift { get; set; }
    public string? CapsAltGr { get; set; }
    public string? CapsShiftAltGr { get; set; }

    public string? GetOutput(bool shift, bool altGr, bool capsLock)
    {
        if (!capsLock)
        {
            if (shift && altGr) return ShiftAltGr;
            if (altGr) return AltGr;
            if (shift) return Shift;
            return Base;
        }

        if (shift && altGr) return CapsShiftAltGr ?? ShiftAltGr;
        if (altGr) return CapsAltGr ?? AltGr;
        if (shift) return CapsShift ?? Shift;
        return Caps ?? Base;
    }
}

/// <summary>A dead key and its declarative input-to-output composition table.</summary>
public sealed class DeadKeyDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Dictionary<string, string> Table { get; } = new();

    public string? Apply(string input) => Table.TryGetValue(input, out var result) ? result : null;
    public string? GetIsolated() => Apply(" ");
}
