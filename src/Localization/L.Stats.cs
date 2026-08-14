namespace AZERTYGlobal;

internal static partial class L
{
    // ── Fenêtre « Mes statistiques » ─────────────────────────────────
    public static string Stats_WindowTitle => T("AZERTY Global — Mes statistiques", "AZERTY Global — My statistics");
    public static string Stats_Title => T("Mes statistiques", "My statistics");
    public static string Stats_LinkFeedback => T("Donner mon avis", "Give feedback");
    public static string Stats_LinkDiscord => T("Rejoindre la communauté Discord", "Join the Discord community");
    public static string Stats_BtnCopy => T("Copier mes statistiques", "Copy my statistics");

    public static string Stats_HeadlineNoData => T(
        "Vous n’avez pas encore tapé de caractère spécial avec AZERTY Global.",
        "You haven't typed a special character with AZERTY Global yet.");

    public static string Stats_HeadlineWithDate(string dateText) => T(
        $"Vous utilisez AZERTY Global depuis le {dateText}.",
        $"You've been using AZERTY Global since {dateText}.");

    public static string Stats_DaysLine(int days, int streak, int best) => T(
        $"{days} jour(s) d’utilisation — série actuelle : {streak} jour(s), record : {best} jour(s).",
        $"{days} day(s) of use — current streak: {streak} day(s), record: {best} day(s).");

    public static string Stats_ActiveTimeLine(string activeTime, string? avgTime) => T(
        avgTime != null
            ? $"Temps de frappe actif : {activeTime} — en moyenne {avgTime} par jour actif."
            : $"Temps de frappe actif : {activeTime}.",
        avgTime != null
            ? $"Active typing time: {activeTime} — averaging {avgTime} per active day."
            : $"Active typing time: {activeTime}.");

    public static string Stats_LabelAccented => T("Majuscules accentuées (É, À, Ç…)", "Accented capitals (É, À, Ç…)");
    public static string Stats_LabelTypography => T("Typographie française (« », ’, —, œ…)", "French typography (« », ’, —, œ…)");
    public static string Stats_LabelInternational => T("Caractères internationaux (ñ, ß, ¿…)", "International characters (ñ, ß, ¿…)");
    public static string Stats_LabelSymbols => T("Symboles (©, →, ½…)", "Symbols (©, →, ½…)");
    public static string Stats_LabelTotal => T("Total", "Total");

    public static string Stats_CopiedFeedback => T("Copié dans le presse-papiers !", "Copied to clipboard!");
    public static string Stats_PrivacyReassurance => T(
        "Ces statistiques sont calculées et stockées uniquement sur votre appareil. Rien n’est transmis.",
        "These statistics are calculated and stored only on your device. Nothing is ever transmitted.");

    // ── Export presse-papiers (UsageStats.BuildShareText) ────────────
    public static string Stats_ShareJustStarted => T(
        "Je viens tout juste de commencer à utiliser AZERTY Global.",
        "I've only just started using AZERTY Global.");

    public static string Stats_ShareDayWord(int count) => T(count > 1 ? "jours" : "jour", count > 1 ? "days" : "day");

    public static string Stats_ShareBase(string dateText, int days, string dayWord, string intensityText) => T(
        $"J’utilise AZERTY Global depuis le {dateText} — {days} {dayWord} d’utilisation{intensityText}",
        $"I've been using AZERTY Global since {dateText} — {days} {dayWord} of use{intensityText}");

    public static string Stats_ShareIntensity(string activeTime, string? avgTime) => T(
        avgTime != null
            ? $" ({activeTime} de frappe active, en moyenne {avgTime} par jour actif)"
            : $" ({activeTime} de frappe active)",
        avgTime != null
            ? $" ({activeTime} of active typing, averaging {avgTime} per active day)"
            : $" ({activeTime} of active typing)");

    public static string Stats_ShareAccentedDetail(long count) => T($"{count} majuscules accentuées", $"{count} accented capitals");
    public static string Stats_ShareTypographyDetail(long count) => T($"{count} caractères de typographie française", $"{count} French typography characters");
    public static string Stats_ShareInternationalDetail(long count) => T($"{count} caractères internationaux", $"{count} international characters");
    public static string Stats_ShareSymbolsDetail(long count) => T($"{count} symboles", $"{count} symbols");

    public static string Stats_ShareDetailWrapper(List<string> details)
    {
        // Énumération naturelle : « A, B et C » / "A, B and C".
        string joined = details.Count == 1
            ? details[0]
            : string.Join(", ", details.GetRange(0, details.Count - 1)) + T(" et ", " and ") + details[details.Count - 1];
        return T($" (dont {joined})", $" (including {joined})");
    }

    public static string Stats_ShareCharWord(long total) => T(
        total > 1 ? "caractères spéciaux tapés" : "caractère spécial tapé",
        total > 1 ? "special characters typed" : "special character typed");

    public static string Stats_ShareFull(string baseText, long total, string charWord, string detailText) => T(
        $"{baseText}, {total} {charWord} directement grâce au remapping{detailText}.",
        $"{baseText}, {total} {charWord} directly thanks to the remapping{detailText}.");
}
