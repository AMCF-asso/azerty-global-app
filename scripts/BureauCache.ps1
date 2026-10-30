<#
.SYNOPSIS
    Lance une commande sur un bureau Win32 dédié, que l'utilisateur ne voit jamais.

.DESCRIPTION
    Les tests de l'application créent de vraies fenêtres (Réglages, Accueil, Conflit, Statistiques)
    pour mesurer taille, DPI et défilement. Sur le bureau de l'utilisateur, même rendues
    transparentes (MasqueFenetresProcessus), elles volent le focus, apparaissent dans
    EnumWindows et flashent. Un process créé avec un autre bureau (lpDesktop) le transmet à ses
    enfants : `dotnet test` puis testhost.exe y créent leurs fenêtres, hors de l'écran de
    l'utilisateur. Mesures, DPI et PrintWindow restent ceux du vrai écran : les écrans sont
    communs à tous les bureaux.

    Dot-sourcer ce fichier, puis : Invoke-SurBureauCache dotnet @('test', ...) | ...
    Rend les lignes de sortie (stdout et stderr fusionnés) ; le code de sortie est dans
    $global:LASTEXITCODE. Si le bureau ne peut pas être créé, la commande tourne sur le bureau
    courant (avertissement) : le masque de MasqueFenetresProcessus reste la seconde ligne.
    AZERTY_BUREAU_VISIBLE=1 force le bureau courant.
#>

if (-not ('AzertyBureauCache' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public static class AzertyBureauCache
{
    private const uint DESKTOP_ALL = 0x000F01FF;   // GENERIC_ALL sur un bureau
    private const uint STARTF_USESTDHANDLES = 0x100;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x400;
    private const uint INFINITE = 0xFFFFFFFF;

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES { public int nLength; public IntPtr lpSecurityDescriptor; public bool bInheritHandle; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb; public string lpReserved; public string lpDesktop; public string lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute;
        public uint dwFlags; public short wShowWindow, cbReserved2; public IntPtr lpReserved2;
        public IntPtr hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDesktopW(string name, string device, IntPtr devmode, uint flags, uint access, IntPtr sa);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseDesktop(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CreatePipe(out IntPtr read, out IntPtr write, ref SECURITY_ATTRIBUTES sa, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetHandleInformation(IntPtr h, uint mask, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadFile(IntPtr h, byte[] buf, uint n, out uint read, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(IntPtr h, out uint code);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessW(string app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags,
        IntPtr env, string cwd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);

    /// <summary>Lance la ligne de commande sur un bureau neuf ; chaque ligne lue passe à onLine. Rend le code de sortie.</summary>
    public static int Run(string commandLine, string workingDirectory, Action<string> onLine)
    {
        string name = "AzertyTests-" + Guid.NewGuid().ToString("N");
        IntPtr desktop = CreateDesktopW(name, null, IntPtr.Zero, 0, DESKTOP_ALL, IntPtr.Zero);
        if (desktop == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateDesktop");
        IntPtr read = IntPtr.Zero, write = IntPtr.Zero, nul = IntPtr.Zero;
        try
        {
            var sa = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES)), bInheritHandle = true };
            if (!CreatePipe(out read, out write, ref sa, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe");
            SetHandleInformation(read, 1, 0); // l'extrémité de lecture reste au lanceur

            var si = new STARTUPINFO
            {
                lpDesktop = "winsta0\\" + name,
                dwFlags = STARTF_USESTDHANDLES,
                hStdOutput = write,
                hStdError = write,
                hStdInput = IntPtr.Zero,
            };
            si.cb = Marshal.SizeOf(si);
            PROCESS_INFORMATION pi;
            if (!CreateProcessW(null, new StringBuilder(commandLine), IntPtr.Zero, IntPtr.Zero, true,
                    CREATE_UNICODE_ENVIRONMENT, IntPtr.Zero, workingDirectory, ref si, out pi))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess");
            CloseHandle(pi.hThread);
            CloseHandle(write); write = IntPtr.Zero;

            var buffer = new byte[8192];
            var pending = new MemoryStream();
            uint n;
            while (ReadFile(read, buffer, (uint)buffer.Length, out n, IntPtr.Zero) && n > 0)
            {
                pending.Write(buffer, 0, (int)n);
                Flush(pending, onLine, false);
            }
            Flush(pending, onLine, true);

            WaitForSingleObject(pi.hProcess, INFINITE);
            uint code;
            GetExitCodeProcess(pi.hProcess, out code);
            CloseHandle(pi.hProcess);
            return unchecked((int)code);
        }
        finally
        {
            if (read != IntPtr.Zero) CloseHandle(read);
            if (write != IntPtr.Zero) CloseHandle(write);
            CloseDesktop(desktop);
        }
    }

    private static void Flush(MemoryStream pending, Action<string> onLine, bool all)
    {
        byte[] data = pending.ToArray();
        int start = 0;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] != (byte)'\n') continue;
            onLine(Encoding.UTF8.GetString(data, start, i - start).TrimEnd('\r'));
            start = i + 1;
        }
        if (all && start < data.Length)
            onLine(Encoding.UTF8.GetString(data, start, data.Length - start).TrimEnd('\r'));
        pending.SetLength(0);
        if (!all && start < data.Length)
            pending.Write(data, start, data.Length - start);
    }
}
'@
}

function Invoke-SurBureauCache([string]$Programme, [string[]]$Arguments) {
    $commande = Get-Command $Programme -ErrorAction Stop
    $ligne = '"' + $commande.Source + '" ' + (($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
    }) -join ' ')
    if ($env:AZERTY_BUREAU_VISIBLE -eq '1') {
        & $Programme @Arguments 2>&1 | ForEach-Object { Write-Host "$_"; "$_" }
        return
    }
    $lignes = New-Object System.Collections.Generic.List[string]
    try {
        $code = [AzertyBureauCache]::Run($ligne, (Get-Location).Path, [Action[string]] { param($l) $lignes.Add($l); Write-Host $l })
    } catch [System.ComponentModel.Win32Exception] {
        Write-Warning "bureau dédié indisponible ($($_.Exception.Message)) : exécution sur le bureau courant"
        & $Programme @Arguments 2>&1 | ForEach-Object { Write-Host "$_"; "$_" }
        return
    }
    $global:LASTEXITCODE = $code
    $lignes
}
