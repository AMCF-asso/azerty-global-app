// AZERTY Global
// © 2017-2026 Antoine Olivier — Licence EUPL 1.2
// https://azerty.global

namespace AZERTYGlobal;

static class Program
{
    /// <summary>Version affichée partout (tooltip, À propos, etc.).</summary>
    internal const string Version = "1.1.2";

    private static string BuildSecondInstanceLogDetails(bool packaged, string[] args) =>
        $"packaged={packaged}, argCount={args.Length}";

    [STAThread]
    static void Main()
    {
        // Déclarer l'app DPI-aware AVANT toute création de fenêtre
        try { Win32.SetProcessDpiAwarenessContext((IntPtr)(-4)); } // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
        catch { try { Win32.SetProcessDPIAware(); } catch { } }   // Fallback Windows 8.1-

        // Langue de l'UI : lue en tout premier, avant tout message affichable (mutex, erreurs fatales).
        L.Language = ConfigManager.AppLanguage;

        // Empêcher les instances multiples — Audit sécu 2026-05 SEV-A2-03 :
        // préfixe Local\ explicite + qualif SID pour éviter qu'un autre process
        // user-land squatte le nom et bloque le démarrage (DoS trivial sans
        // préfixe). Local\ scope = current session uniquement, donc safe.
        var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? "anon";
        var mutexName = $"Local\\AZERTYGlobalSingleInstance.{sid}";
        using var mutex = new Mutex(true, mutexName, out bool isNew);
        if (!isNew)
        {
            // Diagnostic (sert aussi à préparer l'activateur COM prévu en v1.2.0, cf. TO-DO
            // « Option 2 ») : trace uniquement la forme de l'activation. Les arguments
            // bruts peuvent contenir une URI, un chemin utilisateur ou un token.
            var args = Environment.GetCommandLineArgs();
            ConfigManager.LogCompatEvent("SecondInstance",
                BuildSecondInstanceLogDetails(ConfigManager.IsPackaged, args));

            // En contexte packagé (MSIX), une seconde instance provient quasi toujours d'une
            // activation par Windows : le clic sur le toast d'avis (notre balloon
            // Shell_NotifyIcon rendue en toast), la jump list, un protocole… Faute
            // d'activateur COM (v1.2.0), Windows RELANCE l'exe au lieu de livrer l'événement
            // à l'instance vivante — qui, elle, a déjà reçu NIN_BALLOONUSERCLICK et ouvre la
            // page d'avis. Cette seconde instance doit donc mourir en silence : afficher
            // « déjà en cours » ici est le bug signalé au smoke test (clic sur la
            // sollicitation d'avis J+7). Hors package (dev/test), aucune relance d'activation
            // n'existe → on garde le message pour un vrai double-lancement manuel.
            if (!ConfigManager.IsPackaged)
            {
                Win32.MessageBoxW(IntPtr.Zero,
                    L.Startup_AlreadyRunning,
                    "AZERTY Global", 0x40); // MB_ICONINFORMATION
            }
            return;
        }

        // Rotation log centralisée : si error.log > 5 Mo, renommer en error.log.old
        ConfigManager.RotateLogIfNeeded();

        // Gestion des erreurs fatales (le handler ne peut pas empêcher la terminaison)
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var logPath = Path.Combine(ConfigManager.LogDirectory, "error.log");
            var safeMessage = ConfigManager.SanitizeException(e.ExceptionObject as Exception);
            try { Directory.CreateDirectory(ConfigManager.LogDirectory); } catch { }
            try
            {
                // Audit sécu 2026-05 SEV-A1-01 : sanitize au lieu de ex.ToString() complet.
                File.AppendAllText(logPath, $"[{DateTime.Now:s}] FATAL: {safeMessage}\n");
            }
            catch { }
            Win32.MessageBoxW(IntPtr.Zero,
                L.Startup_FatalErrorBody,
                L.Startup_FatalErrorTitle, 0x10); // MB_ICONERROR
        };

        using var app = new TrayApplication();
        app.Run();
    }
}
