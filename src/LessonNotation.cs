// Notation des touches à taper (vague de maquettes du 11/10, journal
// operations/refonte-app/2026-10-07-refonte-130/DECISIONS-NOTATION.md dans Keyboard Layouts).
namespace AZERTYGlobal;

/// <summary>
/// Façon de montrer le geste du caractère courant. Chaque état du clavier des Leçons garde la
/// sienne (QCM d'Antoine du 11/10) : avec le clavier, <see cref="Words"/>, <see cref="Recipe"/>
/// ou <see cref="NumberedKeyboard"/> ; sans le clavier, <see cref="Words"/>,
/// <see cref="Recipe"/>, <see cref="MiniKeyboard"/> ou <see cref="Frames"/>. Mots par défaut
/// dans les deux.
/// </summary>
internal enum LessonNotation
{
    /// <summary>« Maj », « AltGr » en pavés au-dessus de la case frappée, « puis » entre les temps.</summary>
    Words,
    /// <summary>Une ligne écrite : « Maj + AltGr + T ».</summary>
    Recipe,
    /// <summary>Rien sous le caractère : le grand clavier allume et numérote les temps.</summary>
    NumberedKeyboard,
    /// <summary>Un mini-clavier numéroté sous le caractère.</summary>
    MiniKeyboard,
    /// <summary>Un mini-clavier par temps, sans numéro sur les touches.</summary>
    Frames,
}

internal static class LessonNotations
{
    internal static readonly LessonNotation[] WithKeyboard =
        [LessonNotation.Words, LessonNotation.Recipe, LessonNotation.NumberedKeyboard];

    internal static readonly LessonNotation[] WithoutKeyboard =
        [LessonNotation.Words, LessonNotation.Recipe, LessonNotation.MiniKeyboard, LessonNotation.Frames];

    /// <summary>Une notation lue en config qui n'existe pas dans cet état revient aux Mots.</summary>
    internal static LessonNotation Valid(LessonNotation value, bool keyboardVisible) =>
        Array.IndexOf(keyboardVisible ? WithKeyboard : WithoutKeyboard, value) >= 0 ? value : LessonNotation.Words;

    /// <summary>
    /// Seuil de « geste maîtrisé » pour le masquage (décidé le 11/10) : boîte 4 sur 5 ou plus,
    /// et pas à revoir aujourd'hui. Une erreur fait redescendre la boîte, donc réapparaître le geste.
    /// </summary>
    internal const int MasteredBox = 4;

    internal static bool IsMastered(GestureMasteryEntry? entry, DateTimeOffset now) =>
        entry is not null && entry.Box >= MasteredBox && !entry.IsDue(now);
}
