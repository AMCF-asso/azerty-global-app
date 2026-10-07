namespace AZERTYGlobal;

internal static partial class L
{
    // ── Leçons, refonte 1.3.0 (maquette Leçons F « Tablature », 07/10) ──────────────
    // Les espaces avant « : » et dans « » sont insécables (typographie française).
    public static string LessonsPaper_FreeTyping => T("Écriture libre", "Free typing");
    public static string LessonsPaper_ExerciseOf(int current, int total) =>
        T($"Exercice {current} sur {total}", $"Exercise {current} of {total}");
    /// <summary>Légende de la portée, après la phrase du geste.</summary>
    public static string LessonsPaper_TabLegend =>
        T("Sur la tablature, le trait épais se tient, la case se frappe.",
          "On the tablature, a thick bar is held, a box is pressed.");
    /// <summary>Titre du panneau de geste quand une faute attend Retour arrière (mode souple).</summary>
    public static string LessonsPaper_CorrectionTitle => T("Une faute à corriger", "A mistake to fix");
    public static string LessonsPaper_BackspaceKey => T("Retour arrière", "Backspace");
    public static string LessonsPaper_ChooseLesson => T("Choisir une leçon", "Choose a lesson");
    /// <summary>Nom d'un caractère absent de l'index : le caractère entre guillemets.</summary>
    public static string LessonsPaper_CharacterFallback(string character) =>
        T($"« {character} »", $"“{character}”");
}
