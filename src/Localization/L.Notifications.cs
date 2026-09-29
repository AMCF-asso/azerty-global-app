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
    public static string LayoutConflict_Option1Heading => T(
        $"▸ Taper avec {Product} avant l’ouverture de session",
        $"▸ Type with {Product} before sign-in");
    public static string LayoutConflict_Option1Subline => T(
        "(mot de passe Windows, écran de verrouillage, UAC, BitLocker)",
        "(Windows password, lock screen, UAC, BitLocker)");
    public static string LayoutConflict_Option1Body => T(
        "→ Gardez la disposition système et quittez cette application — elle ferait double emploi et ne fonctionne pas avant l’ouverture de session.",
        "→ Keep the system layout and quit this application — it would be redundant, and it doesn't run before sign-in.");
    public static string LayoutConflict_Option2Heading => T(
        "▸ Profiter du clavier virtuel et de la recherche de caractère",
        "▸ Enjoy the virtual keyboard and character search");
    public static string LayoutConflict_Option2Body => T(
        $"→ Utilisez plutôt cette application. Enlevez {Product} de la liste des dispositions chargées dans les options de langue (Paramètres Windows → Heure et langue → Langue et région → Options de la langue concernée). N’oubliez pas alors de cocher « Lancer au démarrage de Windows » dans cette application pour qu’elle soit toujours active après l’ouverture de session.",
        $"→ Use this application instead. Remove {Product} from the list of loaded layouts in the language options (Windows Settings → Time & language → Language & region → Options for the relevant language). Then remember to check \"Launch at Windows startup\" in this application so it stays active after sign-in.");
}
