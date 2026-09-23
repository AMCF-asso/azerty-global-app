// Gestion du lancement automatique au démarrage de Windows
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// État du lancement automatique tel que Windows le rapporte, refus compris. Lu par l'accueil
/// pour décider de sa case par défaut (<see cref="AutoStart.DefaultOnboardingCheck()"/>).
/// </summary>
internal enum AutoStartWindowsState
{
    /// <summary>État illisible (API indisponible, exception) : ne rien présumer.</summary>
    Unknown,
    /// <summary>Inactif, et l'application peut l'activer elle-même.</summary>
    Disabled,
    /// <summary>Actif, y compris imposé par une stratégie.</summary>
    Enabled,
    /// <summary>Refusé par l'utilisateur dans Windows (Paramètres &gt; Applications &gt;
    /// Démarrage, Gestionnaire des tâches) : lui seul peut le rétablir.</summary>
    DisabledByUser,
    /// <summary>Interdit par une stratégie d'entreprise.</summary>
    DisabledByPolicy,
}

/// <summary>
/// Gère le lancement automatique au démarrage de Windows.
/// Mode unpackaged (EXE standalone) : raccourci .lnk dans le dossier Startup.
/// Mode packaged (MSIX) : API WinRT StartupTask déclarée dans le manifeste.
/// </summary>
static class AutoStart
{
    private const string ShortcutName = ProductIdentity.ShortcutFileName;

    private static string StartupFolder =>
        Environment.GetFolderPath(Environment.SpecialFolder.Startup);

    private static string ShortcutPath =>
        Path.Combine(StartupFolder, ShortcutName);

    // ──────────────────────────────────────────────
    //  COM GUIDs pour IShellLink / IPersistFile
    // ──────────────────────────────────────────────

    // CLSID_ShellLink = 00021401-0000-0000-C000-000000000046
    private static readonly Guid CLSID_ShellLink =
        new("00021401-0000-0000-C000-000000000046");

    // IID_IShellLinkW = 000214F9-0000-0000-C000-000000000046
    private static readonly Guid IID_IShellLinkW =
        new("000214F9-0000-0000-C000-000000000046");

    // IID_IPersistFile = 0000010B-0000-0000-C000-000000000046
    private static readonly Guid IID_IPersistFile =
        new("0000010B-0000-0000-C000-000000000046");

    private const uint CLSCTX_INPROC_SERVER = 1;
    private const int S_OK = 0;

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        ref Guid rclsid, IntPtr pUnkOuter, uint dwClsContext,
        ref Guid riid, out IntPtr ppv);

    // ──────────────────────────────────────────────
    //  IShellLinkW vtable offsets
    // ──────────────────────────────────────────────

    // IShellLinkW vtable (hérite IUnknown: 0=QI, 1=AddRef, 2=Release):
    //  3=GetPath, 4=GetIDList, 5=SetIDList,
    //  6=GetDescription, 7=SetDescription,
    //  8=GetWorkingDirectory, 9=SetWorkingDirectory,
    // 10=GetArguments, 11=SetArguments,
    // 12=GetHotkey, 13=SetHotkey,
    // 14=GetShowCmd, 15=SetShowCmd,
    // 16=GetIconLocation, 17=SetIconLocation,
    // 18=SetRelativePath, 19=Resolve, 20=SetPath
    private const int VT_IShellLink_SetDescription = 7;
    private const int VT_IShellLink_SetWorkingDirectory = 9;
    private const int VT_IShellLink_SetPath = 20;

    // ──────────────────────────────────────────────
    //  IPersistFile vtable offsets (IUnknown=0,1,2 + IPersist::GetClassID=3)
    // ──────────────────────────────────────────────
    //  4: IsDirty  5: Load  6: Save  7: SaveCompleted  8: GetCurFile

    private const int VT_IPersistFile_Save = 6;

    // Delegate types pour les méthodes COM
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    private delegate int SetPathDelegate(IntPtr self, string pszFile);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    private delegate int SetWorkingDirectoryDelegate(IntPtr self, string pszDir);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    private delegate int SetDescriptionDelegate(IntPtr self, string pszName);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QueryInterfaceDelegate(IntPtr self, ref Guid riid, out IntPtr ppv);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ReleaseDelegate(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    private delegate int SaveDelegate(IntPtr self, string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);

    /// <summary>
    /// Active ou désactive le lancement automatique et met à jour la config.
    /// Mode unpackaged : raccourci .lnk dans le dossier Startup.
    /// Mode packaged (MSIX) : API WinRT StartupTask.
    /// Retourne false si l'opération a échoué.
    /// </summary>
    public static bool Set(bool enabled)
    {
        try
        {
            if (ConfigManager.IsPackaged)
            {
                if (!SetStartupTask(enabled))
                    return false;
            }
            else
            {
                if (enabled)
                {
                    if (!Enable()) return false;
                }
                else
                    Disable();
            }

            bool finalState = IsRegistered;
            if (finalState != enabled)
                return false;

            ConfigManager.SetAutoStart(finalState);
            return true;
        }
        catch (Exception ex)
        {
            ConfigManager.Log($"AutoStart.Set({enabled})", ex);
            return false;
        }
    }

    /// <summary>Vérifie si le lancement automatique est enregistré.</summary>
    public static bool IsRegistered =>
        ConfigManager.IsPackaged ? IsStartupTaskEnabled() : File.Exists(ShortcutPath);

    public static string GetFailureMessage() =>
        ConfigManager.IsPackaged
            ? L.AutoStart_FailureMessagePackaged
            : L.AutoStart_FailureMessageUnpackaged;

    /// <summary>
    /// Case « Lancer au démarrage de Windows » de l'accueil (décision d'Antoine du
    /// 2026-09-23, v1.3.0) : cochée par défaut au premier accueil, pour que l'activation
    /// l'applique. Garde-fous : un refus exprimé dans Windows (utilisateur ou stratégie)
    /// n'est jamais contourné, et dès qu'un choix a pu être fait, la case reflète l'état
    /// réel comme avant. Décision pure, sans accès à Windows.
    /// </summary>
    internal static bool DefaultOnboardingCheck(
        AutoStartWindowsState state, bool isRegistered, bool choiceAlreadyPossible)
    {
        if (choiceAlreadyPossible) return isRegistered;
        if (isRegistered) return true;
        return state == AutoStartWindowsState.Disabled;
    }

    /// <summary>Photographie les signaux, puis applique la décision pure.</summary>
    internal static bool DefaultOnboardingCheck()
    {
        // Traces d'un choix déjà possible. Une ancienne configuration ne vaut pas accord
        // d'activation, d'où les trois autres signaux : qui a déjà réglé le démarrage ou
        // utilisé l'application n'en est plus à son premier accueil.
        bool choiceAlreadyPossible = ConfigManager.ActivationConsent
            || ConfigManager.AutoStartNudgeDone
            || ConfigManager.AutoStartEnabled
            || UsageStats.FirstRemapDate != null;
        return DefaultOnboardingCheck(WindowsState, IsRegistered, choiceAlreadyPossible);
    }

    /// <summary>État du lancement automatique vu de Windows, refus compris.</summary>
    internal static AutoStartWindowsState WindowsState =>
        ConfigManager.IsPackaged ? GetStartupTaskWindowsState() : GetShortcutWindowsState();

    private static bool Enable()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
            return false;

        string? exeDir = Path.GetDirectoryName(exePath);

        // Créer le dossier Startup s'il n'existe pas
        string startupDir = StartupFolder;
        if (!Directory.Exists(startupDir))
            Directory.CreateDirectory(startupDir);

        // Créer le raccourci via COM IShellLink
        Guid clsid = CLSID_ShellLink;
        Guid iidShellLink = IID_IShellLinkW;

        int hr = CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX_INPROC_SERVER,
            ref iidShellLink, out IntPtr pShellLink);
        if (hr != S_OK || pShellLink == IntPtr.Zero)
            return false;

        try
        {
            // Lire la vtable
            IntPtr vtable = Marshal.ReadIntPtr(pShellLink);

            // SetPath (slot 20)
            IntPtr pSetPath = Marshal.ReadIntPtr(vtable, VT_IShellLink_SetPath * IntPtr.Size);
            var setPath = Marshal.GetDelegateForFunctionPointer<SetPathDelegate>(pSetPath);
            hr = setPath(pShellLink, exePath);
            if (hr != S_OK) return false;

            // SetWorkingDirectory (slot 9)
            if (!string.IsNullOrEmpty(exeDir))
            {
                IntPtr pSetWorkDir = Marshal.ReadIntPtr(vtable, VT_IShellLink_SetWorkingDirectory * IntPtr.Size);
                var setWorkDir = Marshal.GetDelegateForFunctionPointer<SetWorkingDirectoryDelegate>(pSetWorkDir);
                hr = setWorkDir(pShellLink, exeDir);
                if (hr != S_OK) return false;
            }

            // SetDescription (slot 7)
            IntPtr pSetDesc = Marshal.ReadIntPtr(vtable, VT_IShellLink_SetDescription * IntPtr.Size);
            var setDesc = Marshal.GetDelegateForFunctionPointer<SetDescriptionDelegate>(pSetDesc);
            hr = setDesc(pShellLink, L.AutoStart_ShortcutDescription);
            if (hr != S_OK) return false;

            // QueryInterface pour IPersistFile
            IntPtr pQueryInterface = Marshal.ReadIntPtr(vtable, 0 * IntPtr.Size);
            var queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(pQueryInterface);
            Guid iidPersistFile = IID_IPersistFile;
            hr = queryInterface(pShellLink, ref iidPersistFile, out IntPtr pPersistFile);
            if (hr != S_OK || pPersistFile == IntPtr.Zero)
                return false;

            try
            {
                // IPersistFile::Save (slot 6)
                IntPtr vtablePF = Marshal.ReadIntPtr(pPersistFile);
                IntPtr pSave = Marshal.ReadIntPtr(vtablePF, VT_IPersistFile_Save * IntPtr.Size);
                var save = Marshal.GetDelegateForFunctionPointer<SaveDelegate>(pSave);
                hr = save(pPersistFile, ShortcutPath, true);
                if (hr != S_OK) return false;
            }
            finally
            {
                IntPtr vtablePF = Marshal.ReadIntPtr(pPersistFile);
                IntPtr pReleasePF = Marshal.ReadIntPtr(vtablePF, 2 * IntPtr.Size);
                var releasePF = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(pReleasePF);
                releasePF(pPersistFile);
            }
        }
        finally
        {
            IntPtr vtable = Marshal.ReadIntPtr(pShellLink);
            IntPtr pRelease = Marshal.ReadIntPtr(vtable, 2 * IntPtr.Size);
            var release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(pRelease);
            release(pShellLink);
        }

        return true;
    }

    private static void Disable()
    {
        try
        {
            if (File.Exists(ShortcutPath))
                File.Delete(ShortcutPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fichier verrouillé ou accès refusé — on ignore
        }
    }

    // ──────────────────────────────────────────────
    //  Mode MSIX : API WinRT StartupTask
    // ──────────────────────────────────────────────

    /// <summary>TaskId déclaré dans AppxManifest.xml.</summary>
    private const string StartupTaskId = "AZERTYGlobalStartup";

    private static bool IsStartupTaskActive(Windows.ApplicationModel.StartupTaskState state) =>
        state is Windows.ApplicationModel.StartupTaskState.Enabled
            or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;

    /// <summary>
    /// Active ou désactive la StartupTask MSIX.
    /// Retourne true si l'état final correspond à la demande.
    /// </summary>
    private static bool SetStartupTask(bool enabled)
    {
        try
        {
            var task = Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId)
                .GetAwaiter().GetResult();

            if (enabled)
            {
                // Déjà activée : ne PAS rappeler RequestEnableAsync. C'est le cas normal à
                // chaque fermeture (Close() rejoue Set(true)), y compris pendant une mise à
                // jour MSIX où l'updater diffuse un WM_CLOSE à la fenêtre Paramètres masquée :
                // RequestEnableAsync peut alors échouer en plein teardown et afficher une
                // fausse erreur, alors que le lancement automatique est bel et bien actif
                // (constat smoke test Phase 5, 2026-07-20).
                if (IsStartupTaskActive(task.State))
                    return true;

                // RequestEnableAsync retourne l'état réel après la demande.
                // Peut être Enabled, DisabledByUser, ou DisabledByPolicy.
                var state = task.RequestEnableAsync().GetAwaiter().GetResult();
                return IsStartupTaskActive(state);
            }
            else
            {
                task.Disable();
                return true;
            }
        }
        catch (Exception ex)
        {
            // Ne plus avaler silencieusement : un échec sans trace rendait ce bug invisible
            // dans error.log (constat 2026-07-20). Si la tâche est en fait active, considérer
            // comme un succès plutôt que d'alarmer l'utilisateur à tort.
            ConfigManager.Log("AutoStart.SetStartupTask", ex);
            try
            {
                var t = Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId)
                    .GetAwaiter().GetResult();
                return enabled ? IsStartupTaskActive(t.State) : true;
            }
            catch { return false; }
        }
    }

    private static bool IsStartupTaskEnabled()
    {
        try
        {
            var task = Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId)
                .GetAwaiter().GetResult();
            return IsStartupTaskActive(task.State);
        }
        catch { return false; }
    }

    private static AutoStartWindowsState GetStartupTaskWindowsState()
    {
        try
        {
            var task = Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId)
                .GetAwaiter().GetResult();
            return task.State switch
            {
                Windows.ApplicationModel.StartupTaskState.Enabled
                    or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy => AutoStartWindowsState.Enabled,
                Windows.ApplicationModel.StartupTaskState.DisabledByUser => AutoStartWindowsState.DisabledByUser,
                Windows.ApplicationModel.StartupTaskState.DisabledByPolicy => AutoStartWindowsState.DisabledByPolicy,
                _ => AutoStartWindowsState.Disabled,
            };
        }
        catch { return AutoStartWindowsState.Unknown; }
    }

    // ──────────────────────────────────────────────
    //  Mode non empaqueté : refus inscrit par Windows
    // ──────────────────────────────────────────────

    private static AutoStartWindowsState GetShortcutWindowsState()
    {
        if (IsShortcutDisabledInWindows()) return AutoStartWindowsState.DisabledByUser;
        return File.Exists(ShortcutPath) ? AutoStartWindowsState.Enabled : AutoStartWindowsState.Disabled;
    }

    /// <summary>
    /// Le Gestionnaire des tâches et Paramètres &gt; Applications &gt; Démarrage ne suppriment
    /// pas le raccourci qu'on y désactive : ils inscrivent le refus sous
    /// StartupApproved\StartupFolder, valeur binaire au nom du raccourci dont le premier
    /// octet est impair (0x03) quand l'entrée est désactivée, pair (0x02) quand elle est
    /// permise. Lecture seule, par RegGetValueW comme PolicyManager.
    /// </summary>
    private static bool IsShortcutDisabledInWindows()
    {
        try
        {
            var buffer = new byte[64];
            uint size = (uint)buffer.Length;
            int rc = RegGetValueBinary(HKEY_CURRENT_USER, StartupApprovedKey, ShortcutName,
                RRF_RT_REG_BINARY, IntPtr.Zero, buffer, ref size);
            return rc == ERROR_SUCCESS && size > 0 && (buffer[0] & 1) == 1;
        }
        catch { return false; }
    }

    private const string StartupApprovedKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
    private static readonly IntPtr HKEY_CURRENT_USER = new(unchecked((int)0x80000001));
    private const uint RRF_RT_REG_BINARY = 0x00000008;
    private const int ERROR_SUCCESS = 0;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegGetValueW")]
    private static extern int RegGetValueBinary(IntPtr hkey, string lpSubKey, string lpValue,
        uint dwFlags, IntPtr pdwType, [Out] byte[] pvData, ref uint pcbData);
}
