namespace AZERTYGlobal;

internal static partial class L
{
    // Parcours de l'accueil, dans Leçons › Initiation (audit 1.3.0, B2 voie (b)). Consignes et
    // titres des exercices viennent du catalogue des Leçons (empreintes) : l'ancienne fenêtre
    // d'exercices et ses chaînes ont disparu.

    // ── Écran de choix des usages (1re page du parcours) ───────────────
    public static string Learning_UsagesQuestion => T("Vous écrivez surtout…", "You mostly write…");
    public static string Learning_UsagesHelp => T("Plusieurs réponses possibles.", "Several answers possible.");
    public static string Learning_UsageDocuments => T("Des mails et des documents", "Emails and documents");
    public static string Learning_UsageMessages => T("Des messages et des réseaux sociaux", "Messages and social media");
    public static string Learning_UsageCode => T("Du code", "Code");
    public static string Learning_UsageLanguages => T("D’autres langues que le français", "Languages other than French");
    public static string Learning_UsageAfrica => T("Une langue d’Afrique : bambara, peul, wolof…", "An African language: Bambara, Fula, Wolof…");

    // ── Boutons et ligne du parcours ───────────────────────────────────
    public static string Learning_BtnSkip => T("Passer cet exercice", "Skip this exercise");
    public static string Learning_BtnFinish => T("Terminer", "Finish");
    public static string Learning_BtnContinue => T("Continuer", "Continue");
    public static string Learning_SkipForNow => T("Passer pour l’instant", "Skip for now");
    public static string Learning_SkipForNowTooltip => T(
        "Fermer les exercices : l’accueil les propose de nouveau, à reprendre où vous en étiez",
        "Close the exercises: the welcome screen offers them again, from where you left off");
    public static string Learning_RouteProgress(int current, int total, bool bonus) => bonus
        ? T($"Exercice {current} sur {total} (bonus)", $"Exercise {current} of {total} (bonus)")
        : T($"Exercice {current} sur {total}", $"Exercise {current} of {total}");

    // ── Infobulles du clavier dessiné (KeyboardRenderer) ────────────────
    public static string Learning_TooltipBackspaceDisabled => T(
        "Désactivé pendant les exercices — tapez le bon caractère\npour continuer",
        "Disabled during exercises — type the correct character\nto continue");

    public static string Learning_DeadKeyConnector => T(" — touche morte ", " — dead key ");

    // ── Écran final ─────────────────────────────────────────────────────
    public static string Learning_FinalTitle => T("Bravo !", "Well done!");
    public static string Learning_FinalRecap => T("Ce que vous avez tapé", "What you typed");
    public static string Learning_FinalSubtitle => T($"Vous maîtrisez les bases d’{Product}.", $"You’ve mastered the basics of {Product}.");
    public static string Learning_BtnContinueLessons => T("Continuer les leçons", "Continue the lessons");

    /// <summary>Sous une ligne tapée : le geste de son premier caractère qui n’a pas sa propre
    /// touche (« É : Verr. Maj., puis é »).</summary>
    public static string Learning_FinalGesture(string character, string recipe) =>
        T($"{character} : {recipe}", $"{character}: {recipe}");

    public static string Learning_FinalNextTitle => T("Prochaines étapes", "Next steps");
    public static string Learning_FinalNextTodayHelp => T("Les caractères à revoir aujourd’hui", "The characters to review today");
    public static string Learning_FinalNextMapHelp => T("Les touches que vous avez travaillées", "The keys you have practised");
    public static string Learning_FinalNextAfricaHelp => T("€ ɓ ŋ ɔ, sans chronomètre", "€ ɓ ŋ ɔ, no timer");
    public static string Learning_FinalShortcutsTitle => T("Raccourcis à retenir", "Shortcuts to remember");
}
