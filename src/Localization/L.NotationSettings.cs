namespace AZERTYGlobal;

internal static partial class L
{
    // ── Notation des touches à taper (QCM du 11/10, voir LessonNotation.cs) ──────
    // Partagées par Paramètres (onglet « Leçons ») et par le menu « Affichage » de la fenêtre
    // Leçons : un seul libellé par notion, jamais deux copies qui divergent.

    public static string Notation_Words => T("Mots", "Words");
    public static string Notation_Recipe => T("Recette écrite", "Written recipe");
    public static string Notation_NumberedKeyboard => T("Grand clavier numéroté", "Numbered keyboard");
    public static string Notation_MiniKeyboard => T("Mini-clavier sous le caractère", "Mini keyboard under the character");
    public static string Notation_Frames => T("Une vignette par temps", "One frame per step");

    /// <summary>Nom d'une notation, tel qu'il s'écrit dans les listes de choix.</summary>
    public static string Notation_Name(LessonNotation notation) => notation switch
    {
        LessonNotation.Recipe => Notation_Recipe,
        LessonNotation.NumberedKeyboard => Notation_NumberedKeyboard,
        LessonNotation.MiniKeyboard => Notation_MiniKeyboard,
        LessonNotation.Frames => Notation_Frames,
        _ => Notation_Words,
    };

    public static string Notation_GroupTitle => T("Touches à taper", "Keys to type");
    public static string Notation_WithKeyboard => T("Avec le clavier", "With the keyboard");
    public static string Notation_WithoutKeyboard => T("Sans le clavier", "Without the keyboard");
    public static string Notation_KeyboardUnderText => T("Clavier sous le texte", "Keyboard under the text");
    public static string Notation_HideMastered =>
        T("Masquer le geste des caractères maîtrisés", "Hide the gesture of mastered characters");

    /// <summary>Libellé de l'onglet de Paramètres qui porte ces réglages.</summary>
    public static string Settings_TabLessons => T("Leçons", "Lessons");
}
