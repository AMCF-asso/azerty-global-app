namespace AZERTYGlobal;

internal static partial class L
{
    // ── Program.cs ───────────────────────────────────────────────────
    public static string Startup_AlreadyRunning => T(
        $"{Product} est déjà en cours d’exécution.",
        $"{Product} is already running.");
    // AG130-11 (b) : deux sessions Windows du même compte (console + Bureau à
    // distance) partagent config.json, usage-stats.json et error.log.
    public static string Startup_AlreadyRunningOtherSession => T(
        $"{Product} est déjà ouvert dans une autre session Windows de ce compte.\n" +
        "Fermez-le là-bas avant de le lancer ici : les deux écriraient dans les mêmes réglages.",
        $"{Product} is already open in another Windows session of this account.\n" +
        "Close it there before starting it here: both would write to the same settings.");
    public static string Startup_FatalErrorTitle => T("Erreur fatale", "Fatal error");
    public static string Startup_FatalErrorBody => T(
        "Une erreur fatale est survenue. Le détail technique a été écrit dans error.log.",
        "A fatal error occurred. Technical details were written to error.log.");

    // ── AutoStart.cs ──────────────────────────────────────────────────
    public static string AutoStart_ShortcutDescription => T(
        $"{Product} – Lancement automatique",
        $"{Product} – Launch at startup");
    public static string AutoStart_FailureMessagePackaged => T(
        "Impossible d’enregistrer le lancement automatique.\nVérifiez l’autorisation dans Paramètres > Applications > Démarrage.",
        "Couldn't register startup launch.\nCheck the permission in Settings > Apps > Startup.");
    public static string AutoStart_FailureMessageUnpackaged => T(
        "Impossible d’enregistrer le lancement automatique.\nVérifiez les permissions du dossier Démarrage.",
        "Couldn't register startup launch.\nCheck the permissions of the Startup folder.");
}
