namespace AZERTYGlobal;

internal static partial class L
{
    // ── Notation « Mots » des touches à taper (QCM du 11/10) ─────────
    /// <summary>Sous la case Verr. Maj. qui allume les majuscules.</summary>
    public static string KeyNotation_TurnsOn => T("allume", "on");
    /// <summary>Sous la case Verr. Maj. qui les éteint, avant une minuscule.</summary>
    public static string KeyNotation_TurnsOff => T("éteint", "off");
    /// <summary>Recette écrite : « ù, puis B » ; « Verr. Maj., é, puis Verr. Maj. ».</summary>
    public static string KeyNotation_RecipeThen => ", ";
    public static string KeyNotation_RecipeThenLast => T(", puis ", ", then ");
    public static string KeyNotation_RecipeTurnsOff => T(" (pour l’éteindre)", " (to switch it off)");
    /// <summary>Geste masqué parce que maîtrisé ; « Indice » le montre.</summary>
    public static string KeyNotation_Mastered => T("Maîtrisé : geste masqué", "Mastered: gesture hidden");
    public static string KeyNotation_MasteredPanel => T(
        "Vous maîtrisez ce geste : il reste masqué. Une erreur le fait réapparaître ; Indice le montre à tout moment.",
        "You have mastered this gesture, so it stays hidden. A mistake brings it back; Hint shows it at any time.");
    /// <summary>Grand clavier numéroté : rien à lire sous le caractère.</summary>
    public static string KeyNotation_FollowKeyboard => T("Suivez le clavier : ", "Follow the keyboard: ");
    public static string KeyNotation_AllAtOnce => T("tout en même temps", "all at once");
    public static string KeyNotation_StepsInOrder(int count) => count switch
    {
        2 => T("1, puis 2", "1, then 2"),
        3 => T("1, 2, puis 3", "1, 2, then 3"),
        _ => T($"1 à {count}, dans l’ordre", $"1 to {count}, in order"),
    };
}
