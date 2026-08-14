namespace AZERTYGlobal;

internal static partial class L
{
    // ── Défi du jour (v1.2.0) — module de leçons, rappels et opt-in ──
    public static string Challenge_ModuleTitle => T("Défi du jour", "Daily challenge");
    public static string Challenge_ModuleDescription => T(
        "Une séance courte par jour : échauffement puis extrait à taper.",
        "One short session a day: warm-up, then a passage to type.");
    public static string Challenge_LessonDescription => T(
        "Échauffement sur les caractères cibles, puis l’extrait du jour.",
        "Warm up on the target characters, then type today’s passage.");
    public static string Challenge_SequenceLessonTitle(int step, int total) =>
        T($"Prise en main {step}/{total}", $"Getting started {step}/{total}");
    public static string Challenge_DailyLessonTitle(DateOnly date) =>
        T($"Défi du {FormatDate(date)}", $"Challenge for {FormatDate(date)}");
    public static string Challenge_WarmupInstruction => T(
        "Échauffement : tapez chaque caractère cinq fois.",
        "Warm-up: type each character five times.");
    public static string Challenge_ExtractInstruction => T(
        "Tapez l’extrait du jour.",
        "Type today’s passage.");
    public static string Challenge_ExtractInstructionCredited(string credit) => T(
        $"Tapez l’extrait du jour — d’après {credit}.",
        $"Type today’s passage — after {credit}.");

    // Rappel (balloon tray)
    public static string Challenge_ReminderTitle => T("Défi du jour", "Daily challenge");
    public static string Challenge_ReminderBody => T(
        "Une séance courte vous attend : cliquez pour la lancer.",
        "A short session is waiting: click to start it.");

    // Entrée du menu tray
    public static string Tray_MenuChallenge => T("Défi du jour", "Daily challenge");

    // Opt-in (Paramètres ; repris par l'onboarding)
    public static string Challenge_OptIn => T(
        "Rappels d’entraînement « Défi du jour »",
        "“Daily challenge” training reminders");

    // ── Section « Défi du jour » dans Mes statistiques (v1.2.0) ──────
    public static string Challenge_StatsSectionTitle => T("Défi du jour", "Daily challenge");
    public static string Challenge_StatsSessionsLabel => T("Séances terminées", "Sessions completed");
    public static string Challenge_StatsOnboardingLabel => T("Prise en main", "Getting started");
    public static string Challenge_StatsOnboardingDone => T("Terminée ✓", "Completed ✓");
    public static string Challenge_StatsLastSessionLabel => T("Dernière séance", "Last session");
    public static string Challenge_StatsNoSessionYet => "—";
}
