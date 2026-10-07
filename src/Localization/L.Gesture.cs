namespace AZERTYGlobal;

internal static partial class L
{
    // ── Geste d'un caractère (Tablature, refonte 1.3.0) ──────────────
    // « Tenez Maj et AltGr, frappez W. » / « Frappez ù : rien ne s'affiche encore. Puis
    // tenez Maj, frappez Z. » Les espaces avant « : » sont insécables (typographie française).
    public static string Gesture_HoldCapital => T("Tenez ", "Hold ");
    public static string Gesture_Hold => T("tenez ", "hold ");
    public static string Gesture_And => T(" et ", " and ");
    public static string Gesture_StrikeAfterHold => T(", frappez ", ", press ");
    public static string Gesture_StrikeCapital => T("Frappez ", "Press ");
    public static string Gesture_Strike => T("frappez ", "press ");
    public static string Gesture_NothingYet => T(" : rien ne s'affiche encore.", ": nothing appears yet.");
    public static string Gesture_Then => T(" Puis ", " Then ");
    /// <summary>Gravure courte d'Espace dans une case de portée.</summary>
    public static string Gesture_SpaceShort => T("Esp", "Spc");
    /// <summary>Gravure courte de Verr. Maj. dans une case de portée.</summary>
    public static string Gesture_CapsLockShort => T("VM", "Caps");
    public static string Gesture_LineShift => Settings_ShortcutModifier2;
    public static string Gesture_LineAltGr => "AltGr";
    public static string Gesture_LineKey => T("Touche", "Key");
    public static string Gesture_ThenShort => Search_ThenWord;
}
