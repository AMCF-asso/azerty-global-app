namespace AZERTYGlobal;

internal static partial class L
{
    // ── Mini-clavier des Leçons (notations « sous le caractère » et « une vignette par temps ») ──
    // Les noms des touches de fonction viennent de L.Gesture_* : « VM » / « Caps », « Maj » / « Shift ».

    /// <summary>Légende : la touche à frapper (fond plein).</summary>
    public static string MiniKeyboard_Struck => T("frappée", "pressed");
    /// <summary>Légende : la touche à tenir (fond teinté, bord épais).</summary>
    public static string MiniKeyboard_Held => T("tenue", "held");
    /// <summary>Légende : un seul temps, tout se fait ensemble.</summary>
    public static string MiniKeyboard_AllAtOnce => T("tout en même temps", "all at once");
    /// <summary>Légende : l'ordre des pastilles, « 1, puis 2 » (<paramref name="steps"/> temps).</summary>
    public static string MiniKeyboard_Order(int steps)
    {
        string then = Search_ThenWord;
        var parts = new List<string>(steps);
        for (int i = 1; i <= steps; i++)
            parts.Add(i == 1 ? "1" : $"{then} {i}");
        return string.Join(", ", parts);
    }
    /// <summary>Au-dessus d'une vignette : le premier temps d'un geste qui en a plusieurs.</summary>
    public static string MiniKeyboard_First => T("d’abord", "first");
    /// <summary>Au-dessus d'une vignette : les temps suivants.</summary>
    public static string MiniKeyboard_Next => Search_ThenWord;
    /// <summary>Au-dessus de la vignette d'un geste à un seul temps.</summary>
    public static string MiniKeyboard_Together => T("ensemble, en un temps", "together, in one step");
}
