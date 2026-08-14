namespace AZERTYGlobal;

internal static partial class L
{
    // ── ToggleNotification ──────────────────────────────────────────
    public static string Toggle_Activated => T("AZERTY Global activé", "AZERTY Global on");
    public static string Toggle_Deactivated => T("AZERTY Global désactivé", "AZERTY Global off");

    // ── PauseDurationDialog ──────────────────────────────────────────
    public static string Pause_WindowTitle => T("Mettre AZERTY Global en pause", "Pause AZERTY Global");
    public static string Pause_Label => T("Durée de pause temporaire", "Temporary pause duration");
    public static string Pause_Hours => T("Heures", "Hours");
    public static string Pause_Minutes => T("Minutes", "Minutes");
    public static string Pause_BtnConfirm => T("Mettre en pause", "Pause");
    public static string Pause_BtnCancel => T("Annuler", "Cancel");
    public static string Pause_InvalidDuration => T(
        "Choisissez une durée entre 1 minute et 23 h 59.",
        "Choose a duration between 1 minute and 23 hours 59 minutes.");

    // ── LayoutConflictWindow ─────────────────────────────────────────
    public static string LayoutConflict_WindowTitle => T(
        "AZERTY Global — Disposition système détectée",
        "AZERTY Global — System layout detected");
    public static string LayoutConflict_BtnQuit => T("Quitter l’application", "Quit the app");
    public static string LayoutConflict_BtnKeep => T("Garder l’application", "Keep the app");
    public static string LayoutConflict_Title => T(
        "Disposition système AZERTY Global détectée",
        "AZERTY Global system layout detected");
    public static string LayoutConflict_IntroAtStartup => T(
        "Une disposition système AZERTY Global est déjà installée sur cet ordinateur.",
        "An AZERTY Global system layout is already installed on this computer.");
    public static string LayoutConflict_IntroAfterSwitch => T(
        "Une disposition système AZERTY Global vient d’être activée sur cet ordinateur.",
        "An AZERTY Global system layout has just been enabled on this computer.");
    public static string LayoutConflict_Question => T("Quel est votre besoin ?", "What do you need?");
    public static string LayoutConflict_Option1Heading => T(
        "▸ Taper avec AZERTY Global AVANT le login",
        "▸ Type with AZERTY Global BEFORE login");
    public static string LayoutConflict_Option1Subline => T(
        "(mot de passe Windows, écran de verrouillage, UAC, BitLocker)",
        "(Windows password, lock screen, UAC, BitLocker)");
    public static string LayoutConflict_Option1Body => T(
        "→ Gardez la disposition système et quittez cette application — elle fait double emploi et ne tourne pas avant le login de toute façon.",
        "→ Keep the system layout and quit this application — it's redundant here and doesn't run before login anyway.");
    public static string LayoutConflict_Option2Heading => T(
        "▸ Profiter du clavier virtuel et de la recherche de caractère",
        "▸ Enjoy the virtual keyboard and character search");
    public static string LayoutConflict_Option2Body => T(
        "→ Utilisez plutôt cette application. Enlevez AZERTY Global de la liste des dispositions chargées dans les options de langue (Paramètres Windows → Heure et langue → Langue → Options de la langue concernée). N’oubliez pas alors de cocher « Lancer au démarrage de Windows » dans cette application pour qu’elle soit toujours active après le login.",
        "→ Use this application instead. Remove AZERTY Global from the list of loaded layouts in the language options (Windows Settings → Time & language → Language → Options for the relevant language). Then remember to check \"Launch at Windows startup\" in this application so it stays active after login.");
}
