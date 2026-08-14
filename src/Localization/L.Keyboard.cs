namespace AZERTYGlobal;

internal static partial class L
{
    // ── Tooltips des touches contextuelles (KeyboardRenderer.GetContextTooltip) ──
    // Entrée (input) : le libellé fixe dessiné sur la touche (jamais traduit, cf. VirtualKeyboard._visualKeys).
    // Sortie : description longue affichée au survol — celle-ci est traduite.
    public static string Keyboard_TooltipTab => T("Tabulation", "Tab");
    public static string Keyboard_TooltipBackspace => T("Retour arrière", "Backspace");
    public static string Keyboard_TooltipCapsLock => T("Verrouillage Majuscule (Caps Lock)", "Caps Lock");
    public static string Keyboard_TooltipShift => T("Majuscule (Shift)", "Shift");
    public static string Keyboard_TooltipEnter => T("Entrée", "Enter");
    public static string Keyboard_TooltipCtrl => T("Contrôle (Ctrl)", "Control (Ctrl)");
    public static string Keyboard_TooltipWin => T("Touche Windows", "Windows key");
    public static string Keyboard_TooltipAlt => "Alt";
    public static string Keyboard_TooltipAltGr => T("Alt droite (AltGr)", "Right Alt (AltGr)");
    public static string Keyboard_TooltipMenu => T("Menu contextuel", "Context menu");

    // ── Libellés de couche dans le tooltip d'une touche (Base : x — NOM) ──
    public static string Keyboard_LayerShift => T("Maj", "Shift");
    public static string Keyboard_LayerShiftAltGr => T("Maj+AltGr", "Shift+AltGr");

    public static string Keyboard_DeadKeyConnector => T(" — touche morte ", " — dead key ");
    public static string Keyboard_ActiveDeadKeyStatus(string name) => T($"Touche morte active : {name}", $"Active dead key: {name}");

    // ── Marqueurs de caractères invisibles ────────────────────────────
    public static string Keyboard_NarrowNbsp => T("esp. ins. fine", "narrow NBSP");
    public static string Keyboard_Nbsp => T("esp. ins.", "NBSP");

    // ── Noms des touches mortes (anglais — le dictionnaire français reste VirtualKeyboard._deadKeyNamesFr) ──
    public static readonly Dictionary<string, string> DeadKeyNamesEn = new()
    {
        ["dk_circumflex"] = "Circumflex accent",
        ["dk_diaeresis"] = "Diaeresis",
        ["dk_acute"] = "Acute accent",
        ["dk_grave"] = "Grave accent",
        ["dk_tilde"] = "Tilde",
        ["dk_dot_above"] = "Dot above",
        ["dk_dot_below"] = "Dot below",
        ["dk_double_acute"] = "Double acute",
        ["dk_double_grave"] = "Double grave",
        ["dk_horn"] = "Horn",
        ["dk_hook"] = "Hook above",
        ["dk_caron"] = "Caron",
        ["dk_ogonek"] = "Ogonek",
        ["dk_breve"] = "Breve",
        ["dk_inverted_breve"] = "Inverted breve",
        ["dk_stroke"] = "Stroke",
        ["dk_horizontal_stroke"] = "Horizontal stroke",
        ["dk_macron"] = "Macron",
        ["dk_extended_latin"] = "Extended Latin",
        ["dk_cedilla"] = "Cedilla",
        ["dk_comma"] = "Comma below",
        ["dk_phonetic"] = "Phonetic alphabet",
        ["dk_ring_above"] = "Ring above",
        ["dk_greek"] = "Greek alphabet",
        ["dk_cyrillic"] = "Cyrillic alphabet",
        ["dk_misc_symbols"] = "Miscellaneous symbols",
        ["dk_scientific"] = "Scientific symbols",
        ["dk_currencies"] = "Currency symbols",
        ["dk_punctuation"] = "Punctuation symbols",
    };
}
