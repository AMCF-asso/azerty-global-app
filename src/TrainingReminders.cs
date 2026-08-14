// Rappels d'entraînement « Défi du jour » — cadence intelligente (v1.2.0).
//
// Décisions du 2026-07-22 (+ arbitrages du 2026-07-29) :
// - opt-in explicite, décoché par défaut ; canal balloon tray uniquement ;
// - cadence intelligente, pas de fréquence fixe — signaux 100 % locaux : série de jours
//   en danger, N jours sans caractère enrichi, usage soutenu de la recherche de
//   caractères et du clavier virtuel (compteurs globaux, jamais le contenu) ;
// - garde-fou anti-fatigue : 3 rappels ignorés → arrêt définitif (réactivable dans
//   Paramètres) ; jamais deux balloons cliquables le même jour — l'avis J+7 prime.
using System.Globalization;

namespace AZERTYGlobal;

/// <summary>Photographie des signaux locaux au moment de la décision (testable).</summary>
readonly record struct TrainingSignals(
    bool Enabled,
    uint IgnoredCount,
    uint SequenceIndex,
    DateOnly? LastSessionDate,
    DateOnly? LastReminderDate,
    DateOnly? LastActiveDate,       // dernière frappe remappée (UsageStats)
    DateOnly? LastSpecialCharDate,  // dernier caractère enrichi tapé
    int CurrentStreak,
    long HelperOpens);              // recherche + clavier virtuel (compteurs globaux)

static class TrainingReminders
{
    public const uint MaxIgnored = 3;

    /// <summary>Pas de rappel avant cette heure locale : la série du jour n'est « en
    /// danger » qu'en fin de journée, et un rappel matinal serait du bruit.</summary>
    public const int EarliestHour = 17;

    /// <summary>Jours sans caractère enrichi avant de considérer le signal actif.</summary>
    public const int StaleSpecialCharDays = 3;

    /// <summary>Ouvertures cumulées recherche + clavier virtuel signalant des automatismes
    /// non acquis (les « béquilles » du plan) quand aucune séance n'a encore été faite.</summary>
    public const long HelperOpensThreshold = 20;

    /// <summary>
    /// Décision pure : faut-il émettre un rappel maintenant ? Ne touche à aucun état.
    /// <paramref name="reviewPromptShownToday"/> : la sollicitation d'avis J+7 a été
    /// émise aujourd'hui → priorité à l'avis, aucun rappel (décision 2026-07-29).
    /// </summary>
    public static bool ShouldRemind(DateTime now, TrainingSignals s, bool reviewPromptShownToday)
    {
        if (!s.Enabled) return false;
        if (s.IgnoredCount >= MaxIgnored) return false;         // arrêt définitif
        if (reviewPromptShownToday) return false;               // l'avis J+7 prime
        if (now.Hour < EarliestHour) return false;

        var today = DateOnly.FromDateTime(now);
        if (s.LastReminderDate == today) return false;          // un rappel par jour max
        if (s.LastSessionDate == today) return false;           // séance déjà faite

        // Signal 1 — nouveaux utilisateurs (cible prioritaire v1.2.0) : la séquence de
        // désapprentissage n'est pas finie → rappeler tant qu'il reste des étapes,
        // dès le lendemain de la dernière séance.
        if (s.SequenceIndex < DailyChallenge.SequenceLength)
            return true;

        // Signal 2 — série en danger : active hier, rien aujourd'hui, la série se rompt
        // ce soir sans frappe. (CurrentStreak > 1 : une « série » d'un jour n'en est pas une.)
        if (s.CurrentStreak > 1 && s.LastActiveDate != null &&
            s.LastActiveDate == today.AddDays(-1))
            return true;

        // Signal 3 — N jours sans caractère enrichi : les automatismes s'endorment.
        if (s.LastSpecialCharDate != null &&
            today.DayNumber - s.LastSpecialCharDate.Value.DayNumber >= StaleSpecialCharDays)
            return true;

        // Signal 4 — béquilles : usage soutenu de la recherche/clavier virtuel sans
        // aucune séance encore faite → une séance serait plus efficace.
        if (s.LastSessionDate == null && s.HelperOpens >= HelperOpensThreshold)
            return true;

        return false;
    }

    /// <summary>Photographie les signaux depuis ConfigManager et UsageStats.</summary>
    public static TrainingSignals Snapshot()
    {
        return new TrainingSignals(
            Enabled: ConfigManager.TrainingEnabled,
            IgnoredCount: ConfigManager.TrainingIgnoredCount,
            SequenceIndex: ConfigManager.TrainingSequenceIndex,
            LastSessionDate: ParseDate(ConfigManager.TrainingLastSessionDate),
            LastReminderDate: ParseDate(ConfigManager.TrainingLastReminderDate),
            LastActiveDate: UsageStats.LastActiveDate,
            LastSpecialCharDate: UsageStats.LastSpecialCharDate,
            CurrentStreak: UsageStats.CurrentStreak,
            HelperOpens: UsageStats.SearchOpenCount + UsageStats.VirtualKeyboardOpenCount);
    }

    /// <summary>Marque le rappel du jour comme émis (avant l'affichage de la balloon).</summary>
    public static void MarkReminderShown(DateOnly today) =>
        ConfigManager.SetTrainingLastReminderDate(today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    /// <summary>Rappel cliqué : le consentement est réaffirmé, le compteur repart à zéro.</summary>
    public static void MarkReminderClicked() => ConfigManager.SetTrainingIgnoredCount(0);

    /// <summary>Rappel expiré sans clic : ignoré. Au 3e, arrêt définitif (silencieux —
    /// ne jamais pousser l'utilisateur, réactivable dans Paramètres).</summary>
    public static void MarkReminderIgnored() =>
        ConfigManager.SetTrainingIgnoredCount(ConfigManager.TrainingIgnoredCount + 1);

    /// <summary>Séance du jour terminée : date + avancement de la séquence.</summary>
    public static void MarkSessionCompleted(DateOnly today)
    {
        ConfigManager.SetTrainingLastSessionDate(today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        uint seq = ConfigManager.TrainingSequenceIndex;
        if (seq < DailyChallenge.SequenceLength)
            ConfigManager.SetTrainingSequenceIndex(seq + 1);
        UsageStats.RecordChallengeCompleted();
    }

    private static DateOnly? ParseDate(string? s) =>
        s != null && DateOnly.TryParseExact(s, "yyyy-MM-dd", out var d) ? d : null;
}
