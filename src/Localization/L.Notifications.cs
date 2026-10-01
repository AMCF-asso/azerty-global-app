namespace AZERTYGlobal;

internal static partial class L
{
    // ── ToggleNotification ──────────────────────────────────────────
    public static string Toggle_Activated => T($"{Product} activé", $"{Product} on");
    public static string Toggle_Deactivated => T($"{Product} désactivé", $"{Product} off");

    // ── PauseDurationDialog ──────────────────────────────────────────
    public static string Pause_WindowTitle => T($"Mettre {Product} en pause", $"Pause {Product}");
    public static string Pause_Label => T("Durée", "Duration");
    // Noms accessibles des deux champs : l'unité affichée suit le champ, et le Narrateur
    // nomme un champ par l'étiquette qui le précède.
    public static string Pause_Hours => T("Heures", "Hours");
    public static string Pause_Minutes => T("Minutes", "Minutes");
    // Noms accessibles des flèches Up-down : Windows les nomme « Plus » et « Moins »,
    // sans dire quel champ elles changent.
    public static string Pause_HoursUp => T("Augmenter les heures", "Increase hours");
    public static string Pause_HoursDown => T("Diminuer les heures", "Decrease hours");
    public static string Pause_MinutesUp => T("Augmenter les minutes", "Increase minutes");
    public static string Pause_MinutesDown => T("Diminuer les minutes", "Decrease minutes");
    public static string Pause_UnitHours => T("h", "hr");
    public static string Pause_UnitMinutes => T("min", "min");
    // Moment de la reprise, repris par la fenêtre et par le menu : « à 16 h 12 » ou
    // « demain à 8 h ».
    public static string Pause_TodayAt(string clock) => T($"à {clock}", $"at {clock}");
    public static string Pause_TomorrowAt(string clock) => T($"demain à {clock}", $"tomorrow at {clock}");
    public static string Pause_Resumes(string when) => T($"Reprise {when}", $"Resumes {when}");
    public static string Pause_BtnConfirm => T("Mettre en pause", "Pause");
    public static string Pause_BtnCancel => T("Annuler", "Cancel");
    public static string Pause_InvalidDuration => T(
        "Choisissez entre 1 min et 23 h 59.",
        "Choose between 1 min and 23 hr 59 min.");

    // ── LayoutConflictWindow ─────────────────────────────────────────
    public static string LayoutConflict_WindowTitle => T(
        $"{Product} — Disposition système détectée",
        $"{Product} — System layout detected");
    public static string LayoutConflict_BtnQuit => T("Quitter l’application", "Quit the app");
    public static string LayoutConflict_BtnKeep => T("Garder l’application", "Keep the app");
    public static string LayoutConflict_Title => T(
        $"Disposition système {Product} détectée",
        $"{Product} system layout detected");
    public static string LayoutConflict_IntroAtStartup => T(
        $"Une disposition système {Product} est déjà installée sur cet ordinateur.",
        $"An {Product} system layout is already installed on this computer.");
    public static string LayoutConflict_IntroAfterSwitch => T(
        $"Une disposition système {Product} vient d’être activée sur cet ordinateur.",
        $"An {Product} system layout has just been enabled on this computer.");
    public static string LayoutConflict_Question => T("Quel est votre besoin ?", "What do you need?");
    // Notes des deux liens de commande, sous leur titre (BtnKeep, BtnQuit).
    public static string LayoutConflict_KeepNote => T(
        $"Pour le clavier virtuel et la recherche de caractère. Retirez ensuite {Product} des dispositions chargées : Paramètres Windows → Heure et langue → Langue et région → Options de la langue concernée. Cochez aussi « Lancer au démarrage de Windows » dans l’application.",
        $"For the virtual keyboard and character search. Then remove {Product} from the loaded layouts: Windows Settings → Time & language → Language & region → Options for the relevant language. Also check \"Launch at Windows startup\" in the app.");
    public static string LayoutConflict_QuitNote => T(
        $"Pour taper avec {Product} avant l’ouverture de session (mot de passe Windows, écran de verrouillage, UAC, BitLocker). La disposition système suffit : l’application ferait double emploi.",
        $"To type with {Product} before sign-in (Windows password, lock screen, UAC, BitLocker). The system layout is enough: the app would be redundant.");
}
