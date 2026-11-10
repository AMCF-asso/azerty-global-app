namespace AZERTYGlobal;

internal static partial class L
{
    // ── Leçons : ce que le lecteur d'écran entend (audit de régression du 10/11, RA6 et RA7) ──
    //   : espace insécable avant « : » (typographie française).

    /// <summary>Un chiffre du bilan, dit avec son libellé : « Vitesse : 42 mots/min ».</summary>
    public static string LessonsAutomation_Metric(string label, string value) =>
        T($"{label} : {value}", $"{label}: {value}");

    /// <summary>Faute de frappe, annoncée sans déplacer le focus.</summary>
    public static string LessonsAutomation_TypingError(string typed, string expected) =>
        T($"Faute : {typed} au lieu de {expected}.", $"Mistake: {typed} instead of {expected}.");
    /// <summary>Frappe au-delà de la fin de la ligne (frappe souple).</summary>
    public static string LessonsAutomation_TypingExtra(string typed) =>
        T($"Faute : {typed} en trop.", $"Mistake: extra {typed}.");
    /// <summary>Suite de l'annonce en frappe souple : le curseur a avancé, la faute reste à effacer.</summary>
    public static string LessonsAutomation_FixWithBackspace =>
        T("Retour arrière pour corriger.", "Press Backspace to fix.");
    /// <summary>Suite de l'annonce en frappe stricte : le curseur n'a pas avancé.</summary>
    public static string LessonsAutomation_TypeAgain =>
        T("Tapez le bon caractère.", "Type the right character.");

    public static string LessonsAutomation_SpaceName => T("espace", "space");
    public static string LessonsAutomation_NbspName => T("espace insécable", "no-break space");
    public static string LessonsAutomation_NarrowNbspName => T("espace fine insécable", "narrow no-break space");
}
