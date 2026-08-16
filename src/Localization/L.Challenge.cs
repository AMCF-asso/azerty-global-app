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

    // ── Partage du résultat (v1.2.0) ─────────────────────────────────
    // Ce texte est destiné à être collé publiquement : il ne porte aucune statistique
    // d’usage de l’application, seulement la performance de la séance qui vient d’être
    // jouée. Les chiffres d’usage restent dans « Mes statistiques ».
    public static string Challenge_ShareTitle(DateOnly date) => T(
        $"AZERTY Global — Défi du {FormatDate(date)}",
        $"AZERTY Global — Challenge for {FormatDate(date)}");
    public static string Challenge_ShareSpeed(int wpm) => T($"{wpm} mots/min", $"{wpm} wpm");
    public static string Challenge_ShareAccuracy(int percent) => T($"{percent} % de précision", $"{percent}% accuracy");
    public static string Challenge_ShareSeconds(int seconds) => T($"{seconds} s", $"{seconds}s");
    public static string Challenge_ShareRecord => T("🏆 Nouveau record personnel", "🏆 New personal best");
    public static string Challenge_ShareFlawless => T("⭐ Sans faute", "⭐ Flawless");
    public static string Challenge_ShareHardest(string characters) => T(
        $"Ce qui m’a résisté : {characters}",
        $"What tripped me up: {characters}");
    public static string Challenge_ShareCredit(string credit) => T(
        $"Extrait d’après {credit}",
        $"Passage after {credit}");
    public static string Challenge_ShareFooter => T(
        "Même extrait pour tout le monde → azerty.global",
        "Same passage for everyone → azerty.global");

    // Bouton du récapitulatif de fin de défi
    public static string Challenge_ShareButton => T("Copier mon résultat", "Copy my result");
    public static string Challenge_ShareButtonCopied => T("Copié ✓", "Copied ✓");
    public static string Challenge_ShareTooltip => T(
        "Copie ton résultat du jour, prêt à coller dans une conversation.",
        "Copies today’s result, ready to paste into a conversation.");

    // ── Section « Défi du jour » dans Mes statistiques (v1.2.0) ──────
    public static string Challenge_StatsSectionTitle => T("Défi du jour", "Daily challenge");
    public static string Challenge_StatsSessionsLabel => T("Séances terminées", "Sessions completed");
    public static string Challenge_StatsOnboardingLabel => T("Prise en main", "Getting started");
    public static string Challenge_StatsOnboardingDone => T("Terminée ✓", "Completed ✓");
    public static string Challenge_StatsLastSessionLabel => T("Dernière séance", "Last session");
    public static string Challenge_StatsNoSessionYet => "—";
}
