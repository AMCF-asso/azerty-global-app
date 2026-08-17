namespace AZERTYGlobal;

internal static partial class L
{
    // ── Les 6 étapes du mini-tutoriel (LearningModule._steps) ─────────
    // Cible (Target = texte à taper) volontairement laissée en français quelle que soit
    // la langue de l'UI — c'est l'objet même de l'apprentissage (cf. plan i18n § périmètre).
    public static string Learning_Step0Title => T("Votre premier É", "Your first É");
    public static string Learning_Step0Instruction => T("Activez Verr. Maj. puis tapez sur la lettre é", "Turn on Caps Lock, then press the é key");
    public static string Learning_Step1Title => T("Majuscules et ponctuation", "Capitals and punctuation");
    public static string Learning_Step1Instruction => T(
        "Gardez le Verrouillage Majuscule activé pour taper cette phrase",
        "Keep Caps Lock on to type this sentence");
    public static string Learning_Step2Title => T("Adresse e-mail", "Email address");
    public static string Learning_Step2Instruction => T(
        "Tapez cette adresse e-mail — le @ est sur la touche ² et le point est en accès direct",
        "Type this email address — @ is on the ² key and the period is directly accessible");
    public static string Learning_Step3Title => T("Typographie française", "French typography");
    public static string Learning_Step3Instruction => T(
        "Tapez cette phrase avec les caractères typographiques — suivez les indications du clavier",
        "Type this sentence with its typographic characters — follow the hints on the keyboard");
    public static string Learning_Step4Title => T("Ligne de code", "Line of code");
    public static string Learning_Step4Instruction => T(
        "Tapez cette ligne de code — les symboles sont accessibles via AltGr",
        "Type this line of code — the symbols are accessible via AltGr");
    public static string Learning_Step5Title => T("Mots étrangers", "Foreign words");
    public static string Learning_Step5Instruction => T(
        "Tapez ces mots étrangers — utilisez les touches mortes indiquées sur le clavier",
        "Type these foreign words — use the dead keys shown on the keyboard");

    // ── Fenêtre et boutons ─────────────────────────────────────────────
    public static string Learning_WindowTitle => T($"{Product} — Exercices", $"{Product} — Exercises");
    public static string Learning_BtnQuit => T("Quitter les exercices", "Quit exercises");
    public static string Learning_BtnSkip => T("Passer cet exercice", "Skip this exercise");
    public static string Learning_BtnFinish => T("Terminer", "Finish");
    public static string Learning_BtnRetry => T("Recommencer l’exercice", "Retry the exercise");
    public static string Learning_BtnNext => T("Exercice suivant", "Next exercise");
    public static string Learning_BtnFinishAll => T("Terminer les exercices", "Finish exercises");

    // ── Tooltip touche Retour arrière (spécifique aux exercices) ────────
    public static string Learning_TooltipBackspaceDisabled => T(
        "Désactivé pendant les exercices — continuez de taper,\nl’erreur se corrige toute seule",
        "Disabled during exercises — keep typing,\nthe mistake corrects itself");

    public static string Learning_DeadKeyConnector => T(" — TOUCHE MORTE ", " — DEAD KEY ");

    // ── Header et statut ────────────────────────────────────────────────
    public static string Learning_ExerciseHeader(int current, int total, string title) => T(
        $"Exercice {current}/{total} — {title}",
        $"Exercise {current}/{total} — {title}");
    public static string Learning_BonusSuffix => T(" (Bonus)", " (Bonus)");
    public static string Learning_CapsLockLabel => T("Verrouillage Majuscule : ", "Caps Lock: ");
    public static string Learning_CapsLockOn => T("ACTIVÉ", "ON");
    public static string Learning_CapsLockOff => T("désactivé", "off");
    public static string Learning_ActiveDeadKey(string name, string symbol) => T(
        string.IsNullOrEmpty(symbol) ? $"Touche morte : {name}" : $"Touche morte : {name} {symbol}",
        string.IsNullOrEmpty(symbol) ? $"Dead key: {name}" : $"Dead key: {name} {symbol}");

    // ── Écrans de fin d'exercice ──────────────────────────────────────
    public static string Learning_BravoShort => T("✓ Bravo !", "✓ Well done!");
    public static string Learning_LegendDeadKey => T("Touche morte", "Dead key");
    public static string Learning_FinalTitle => T("Bravo !", "Well done!");
    public static string Learning_FinalSubtitle => T($"Vous maîtrisez les bases d’{Product}.", $"You've mastered the basics of {Product}.");

    // ── Overlay de pause ────────────────────────────────────────────────
    public static string Learning_ClickToResume => T("Cliquez pour reprendre", "Click to resume");
}
