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
        "Fermez-le là-bas avant de le lancer ici : les deux écriraient dans les mêmes paramètres.",
        $"{Product} is already open in another Windows session of this account.\n" +
        "Close it there before starting it here: both would write to the same settings.");
    public static string Startup_FatalErrorTitle => T($"{Product} — Erreur", $"{Product} — Error");
    public static string Startup_FatalErrorBody => T(
        $"{Product} doit se fermer à cause d’une erreur. Le détail technique a été enregistré dans error.log.",
        $"{Product} has to close because of an error. Technical details were recorded in error.log.");

    // ── AutoStart.cs ──────────────────────────────────────────────────
    public static string AutoStart_ShortcutDescription => T(
        $"{Product} — Lancement automatique",
        $"{Product} — Launch at startup");
    public static string AutoStart_FailureMessagePackaged => T(
        "Impossible d’enregistrer le lancement automatique.\nVérifiez l’autorisation dans Paramètres Windows → Applications → Démarrage.",
        "Couldn't register startup launch.\nCheck the permission in Windows Settings → Apps → Startup.");
    public static string AutoStart_FailureMessageUnpackaged => T(
        "Impossible d’enregistrer le lancement automatique.\nVérifiez les autorisations du dossier Démarrage.",
        "Couldn't register startup launch.\nCheck the permissions of the Startup folder.");
}
