namespace TypingEngine.Windows;

/// <summary>
/// Modificateurs configurables d'un raccourci d'application (clavier virtuel, recherche).
/// Alt désigne toujours Alt gauche : AltGr (Ctrl gauche + Alt droit) et Win sont exclus
/// pour préserver les 30 touches mortes et les raccourcis système Windows.
/// </summary>
[Flags]
public enum ShortcutModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
}

/// <summary>Validation et sérialisation des combinaisons de modificateurs de raccourcis.</summary>
public static class ShortcutModifierRules
{
    public const ShortcutModifiers Default = ShortcutModifiers.Ctrl | ShortcutModifiers.Shift;

    private const ShortcutModifiers AllowedMask = ShortcutModifiers.Ctrl | ShortcutModifiers.Shift | ShortcutModifiers.Alt;

    /// <summary>
    /// Au moins deux modificateurs parmi Ctrl, Maj et Alt gauche (et aucun bit inconnu),
    /// afin de ne pas capturer de raccourcis courants à un seul modificateur (Ctrl+C, Alt+F4, Maj+Q).
    /// </summary>
    public static bool IsValid(ShortcutModifiers modifiers)
    {
        if ((modifiers & ~AllowedMask) != 0)
            return false;
        int count = 0;
        if ((modifiers & ShortcutModifiers.Ctrl) != 0) count++;
        if ((modifiers & ShortcutModifiers.Shift) != 0) count++;
        if ((modifiers & ShortcutModifiers.Alt) != 0) count++;
        return count >= 2;
    }

    public static ShortcutModifiers FromBools(bool ctrl, bool shift, bool alt)
    {
        var m = ShortcutModifiers.None;
        if (ctrl) m |= ShortcutModifiers.Ctrl;
        if (shift) m |= ShortcutModifiers.Shift;
        if (alt) m |= ShortcutModifiers.Alt;
        return m;
    }

    public static string Serialize(ShortcutModifiers modifiers)
    {
        if (!IsValid(modifiers))
            modifiers = Default;
        var parts = new List<string>(3);
        if ((modifiers & ShortcutModifiers.Ctrl) != 0) parts.Add("ctrl");
        if ((modifiers & ShortcutModifiers.Alt) != 0) parts.Add("alt");
        if ((modifiers & ShortcutModifiers.Shift) != 0) parts.Add("shift");
        return string.Join("+", parts);
    }

    public static ShortcutModifiers ParseOrDefault(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Default;
        var m = ShortcutModifiers.None;
        foreach (string raw in value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": m |= ShortcutModifiers.Ctrl; break;
                case "shift": m |= ShortcutModifiers.Shift; break;
                case "alt": m |= ShortcutModifiers.Alt; break;
                default: return Default;
            }
        }
        return IsValid(m) ? m : Default;
    }
}
