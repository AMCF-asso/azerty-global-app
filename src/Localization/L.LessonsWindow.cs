namespace AZERTYGlobal;

internal static partial class L
{
    public static string LessonsWin_WindowTitle => T($"{Product} — Leçons", $"{Product} — Lessons");
    public static string LessonsWin_Title => Tray_MenuLessons; // "Leçons"/"Lessons" — réutilisé tel quel
    public static string LessonsWin_ExerciseCount(int done, int total) => T($"{done}/{total} exercices", $"{done}/{total} exercises");
    public static string LessonsWin_TabFree => T("Libre", "Free");
    public static string LessonsWin_SettingsTab => Tray_MenuSettings; // "Paramètres"/"Settings"

    // ── Panneau Paramètres ──────────────────────────────────────────
    public static string LessonsWin_SectionDisplay => T("Affichage", "Display");
    public static string LessonsWin_SectionActions => T("Actions", "Actions");
    public static string LessonsWin_ToggleAutoHints => T("Auto-indices", "Auto hints");
    public static string LessonsWin_ToggleSummary => T("Résumé après exercice", "Summary after exercise");
    public static string LessonsWin_ToggleFreeStats => T("Stats du mode libre", "Free mode stats");
    public static string LessonsWin_ToggleKeyboard => T("Clavier visuel", "Visual keyboard");
    public static string LessonsWin_ToggleInvisibleMarkers => T("Marqueurs invisibles", "Invisible markers");
    public static string LessonsWin_ToggleOn => T("Activé", "On");
    public static string LessonsWin_ToggleOff => T("Désactivé", "Off");
    public static string LessonsWin_BtnResetFreeStats => T("Reset stats libre", "Reset free stats");
    public static string LessonsWin_BtnResetProgress => T("Reset progression", "Reset progress");
    public static string LessonsWin_BtnResetStats => T("Reset stats", "Reset stats");

    // ── Exercice en cours ────────────────────────────────────────────
    public static string LessonsWin_ExerciseLabel(int current, int total) => T($"Exercice {current}/{total}", $"Exercise {current}/{total}");
    public static string LessonsWin_ExerciseDone => T("Exercice terminé", "Exercise done");
    public static string LessonsWin_LineLabel(int current, int total) => T($"Ligne {current}/{total}", $"Line {current}/{total}");
    public static string LessonsWin_ExerciseSuccess => T("Exercice réussi", "Exercise passed");
    public static string LessonsWin_MetricSpeed => T("Vitesse", "Speed");
    public static string LessonsWin_MetricAccuracy => T("Précision", "Accuracy");
    public static string LessonsWin_MetricErrors => T("Erreurs", "Errors");
    public static string LessonsWin_DetailNoHardChar(int seconds) => T(
        $"Temps : {seconds}s    Aucun caractère difficile sur cette tentative.",
        $"Time: {seconds}s    No difficult character on this attempt.");
    public static string LessonsWin_DetailHardChars(int seconds, string chars) => T(
        $"Temps : {seconds}s    À retravailler : {chars}",
        $"Time: {seconds}s    To practice: {chars}");

    // ── Boutons icônes (tooltips) ──────────────────────────────────────
    public static string LessonsWin_IconPrevious => Onboarding_Prev; // "Précédent"/"Previous"
    public static string LessonsWin_IconNext => Onboarding_Next; // "Suivant"/"Next"
    public static string LessonsWin_IconRestart => T("Recommencer", "Restart");
    public static string LessonsWin_IconHint => T("Indice", "Hint");
    public static string LessonsWin_HintBackspace => T("Retour arrière → corriger", "Backspace → correct");

    // ── Mode libre ───────────────────────────────────────────────────
    public static string LessonsWin_FreeTitle => T("Mode libre", "Free mode");
    public static string LessonsWin_FreeDescription => T(
        "Tapez librement pour mesurer le rythme. Rien n’est enregistré après fermeture.",
        "Type freely to measure your pace. Nothing is recorded after closing.");
    public static string LessonsWin_FreeStatsEmpty => T(
        "WPM : —    Caractères/min : —    Durée : 0s    Corrections : 0",
        "WPM: —    Characters/min: —    Duration: 0s    Corrections: 0");
    public static string LessonsWin_FreeStatsFilled(string wpm, int cpm, int seconds, int backspaces) => T(
        $"WPM : {wpm}    Caractères/min : {cpm}    Durée : {seconds}s    Corrections : {backspaces}",
        $"WPM: {wpm}    Characters/min: {cpm}    Duration: {seconds}s    Corrections: {backspaces}");

    // ── Réinitialisation de la progression ──────────────────────────────
    public static string LessonsWin_ResetProgressConfirm => T(
        "Réinitialiser toute la progression des leçons ?\n\nLes préférences, comme les auto-indices, seront conservées.",
        "Reset all lesson progress?\n\nPreferences, such as auto hints, will be kept.");

    // ── Statut de frappe et indices ──────────────────────────────────
    public static string LessonsWin_StatusStrict => T(
        "Mode initiation : retapez le bon caractère pour corriger.",
        "Initiation mode: retype the correct character to fix a mistake.");
    public static string LessonsWin_StatusFlexible => T(
        "Retour arrière autorisé. Le collage est bloqué.",
        "Backspace is allowed. Pasting is blocked.");

    /// <summary>Placeholders {ALTGR}/{SHIFT}/{CAPS} dans les instructions de lessons.json.</summary>
    public static string LessonsWin_PlaceholderShift => T("Maj", "Shift");
    public static string LessonsWin_PlaceholderCaps => T("Verr. Maj.", "Caps Lock");
}
