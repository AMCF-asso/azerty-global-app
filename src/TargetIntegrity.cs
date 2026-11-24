using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Audit de l'environnement Windows, EW-7 : Windows refuse les frappes simulées
/// (<c>SendInput</c>) d'un processus vers une fenêtre de niveau d'intégrité supérieur
/// (UIPI), sans le dire de façon fiable : le retour et GetLastError peuvent ne rien
/// signaler. La recherche de caractère compare donc, avant d'émettre, le niveau
/// d'intégrité du processus de la fenêtre cible au sien ; au-dessus, elle copie le
/// caractère au lieu de le perdre. L'application n'a pas <c>uiAccess</c>
/// (msix/AppxManifest.xml), qui seul lèverait ce refus.
/// </summary>
internal static class TargetIntegrity
{
    /// <summary>Niveau « moyen » (SECURITY_MANDATORY_MEDIUM_RID), celui d'une application
    /// ordinaire ; retenu pour nous si notre propre jeton ne se lit pas.</summary>
    internal const uint MediumRid = 0x2000;

    private static readonly Lazy<uint> s_ourLevel = new(() =>
        TryReadLevel(GetCurrentProcess(), out uint level) ? level : MediumRid);

    /// <summary>
    /// Décision pure. <paramref name="processOpened"/> faux : le processus ne s'ouvre même
    /// pas en lecture limitée (processus protégé, ou disparu). <paramref name="targetLevel"/>
    /// nul : le processus s'ouvre mais son jeton ne se lit pas, ce que Windows refuse
    /// justement pour un processus élevé vu d'un processus ordinaire. Dans le doute, la
    /// réponse est « au-dessus » : la copie de secours ne perd rien, une frappe refusée
    /// perd le caractère.
    /// </summary>
    internal static bool IsAbove(bool processOpened, uint? targetLevel, uint ourLevel) =>
        !processOpened || targetLevel is not uint level || level > ourLevel;

    /// <summary>
    /// La fenêtre appartient-elle à un processus de niveau d'intégrité supérieur au nôtre ?
    /// Faux pour une fenêtre sans processus (HWND périmé : l'activation l'a déjà écartée)
    /// et pour nos propres fenêtres. Lecture seule : OpenProcess en
    /// PROCESS_QUERY_LIMITED_INFORMATION, OpenProcessToken en TOKEN_QUERY.
    /// </summary>
    internal static bool IsWindowAboveUs(IntPtr window)
    {
        if (window == IntPtr.Zero) return false;
        TypingEngine.Windows.Win32.GetWindowThreadProcessIdOut(window, out uint pid);
        if (pid == 0) return false;
        if (pid == (uint)Environment.ProcessId) return false;

        IntPtr process = TypingEngine.Windows.Win32.OpenProcess(
            TypingEngine.Windows.Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero)
            return IsAbove(processOpened: false, targetLevel: null, s_ourLevel.Value);
        try
        {
            uint? level = TryReadLevel(process, out uint read) ? read : null;
            return IsAbove(processOpened: true, level, s_ourLevel.Value);
        }
        finally
        {
            TypingEngine.Windows.Win32.CloseHandle(process);
        }
    }

    /// <summary>Niveau d'intégrité (dernière sous-autorité du SID d'étiquette) du jeton du
    /// processus. Faux si le jeton ne s'ouvre pas ou ne se lit pas.</summary>
    internal static bool TryReadLevel(IntPtr process, out uint level)
    {
        level = 0;
        if (!OpenProcessToken(process, TOKEN_QUERY, out IntPtr token)) return false;
        try
        {
            GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out uint size);
            if (size == 0) return false;
            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, size, out _)) return false;
                // TOKEN_MANDATORY_LABEL : SID_AND_ATTRIBUTES, dont le premier champ est le SID.
                IntPtr sid = Marshal.ReadIntPtr(buffer);
                if (sid == IntPtr.Zero || !IsValidSid(sid)) return false;
                IntPtr count = GetSidSubAuthorityCount(sid);
                if (count == IntPtr.Zero) return false;
                byte n = Marshal.ReadByte(count);
                if (n == 0) return false;
                IntPtr last = GetSidSubAuthority(sid, (uint)(n - 1));
                if (last == IntPtr.Zero) return false;
                level = (uint)Marshal.ReadInt32(last);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            TypingEngine.Windows.Win32.CloseHandle(token);
        }
    }

    private const uint TOKEN_QUERY = 0x0008;
    private const int TokenIntegrityLevel = 25;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass,
        IntPtr tokenInformation, uint tokenInformationLength, out uint returnLength);

    [DllImport("advapi32.dll")]
    private static extern bool IsValidSid(IntPtr sid);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint subAuthority);
}
