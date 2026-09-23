# Execute dans Windows Sandbox apres installer-candidat.ps1. Ouvre chaque fenetre de
# l'app par la commande de son menu (WM_COMMAND envoye a la fenetre de l'icone, comme
# un clic), la fait analyser par AxeWindows CLI (C:\axe), puis la ferme.
# Resultats : C:\resultats\axe\ (rapports .a11ytest, journaux, resume.json).
$ErrorActionPreference = 'Continue'
$out = 'C:\resultats\axe'
New-Item -ItemType Directory -Force $out | Out-Null
$log = Join-Path $out 'scan.txt'
function Say([string]$message) {
    $line = '[{0:HH:mm:ss}] {1}' -f (Get-Date), $message
    Add-Content -Path $log -Value $line
    Write-Host $line
}

Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class Win {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    public static List<IntPtr> Visible(uint pid) {
        var list = new List<IntPtr>();
        EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }
    public static string Text(IntPtr h) { var s = new StringBuilder(256); GetWindowText(h, s, 256); return s.ToString(); }
    public static string Cls(IntPtr h) { var s = new StringBuilder(256); GetClassName(h, s, 256); return s.ToString(); }
}
'@

# Copie locale : le dossier monte est en lecture seule.
Copy-Item 'C:\axe' 'C:\tools\axe' -Recurse -Force
$axe = 'C:\tools\axe\AxeWindowsCLI.exe'
$process = Get-Process -Name 'AZERTY Global' -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $process) { Say 'ECHEC : app absente'; exit 1 }
$appPid = [uint32]$process.Id
# [NullString]::Value : un $null passe a un parametre string devient "" sous PowerShell.
$tray = [Win]::FindWindowEx([IntPtr]::Zero, [IntPtr]::Zero, 'AZERTYGlobal_Wnd', [NullString]::Value)
if ($tray -eq [IntPtr]::Zero) { $tray = [Win]::FindWindowEx([IntPtr](-3), [IntPtr]::Zero, 'AZERTYGlobal_Wnd', [NullString]::Value) }
Say ('App pid ' + $appPid + ' ; fenetre de l icone ' + $tray)

$summary = @()
$scanned = @{}
function Scan-Visible([string]$step) {
    foreach ($h in [Win]::Visible($appPid)) {
        $key = [string]$h
        if ($scanned.ContainsKey($key)) { continue }
        $scanned[$key] = $true
        $title = [Win]::Text($h); $cls = [Win]::Cls($h)
        $id = ($step + '-' + ($title -replace '[^A-Za-z0-9]+', '_')).Trim('_')
        # --scanrootwindowhandle sort en 2 sans message en 2.4.2 (mesure le 2026-09-23,
        # meme sur le Bloc-notes) : on garde une seule fenetre visible et --processid
        # analyse la fenetre principale du processus.
        $visibleCount = ([Win]::Visible($appPid)).Count
        if ($visibleCount -ne 1) { Say ($step + ' : ' + $visibleCount + ' fenetres visibles, resultat ambigu') }
        $text = & $axe --processid $appPid --outputdirectory $out --scanid $id --alwayssavetestfile --verbosity Verbose 2>&1 | Out-String
        $text = 'exit=' + $LASTEXITCODE + [Environment]::NewLine + $text
        Set-Content -Path (Join-Path $out ($id + '.log')) -Value $text -Encoding UTF8
        $errors = if ($text -match '(\d+) errors? (was|were) found') { [int]$Matches[1] } else { -1 }
        Say ("{0} : '{1}' ({2}) -> {3} erreur(s)" -f $step, $title, $cls, $errors)
        $script:summary += [ordered]@{ step = $step; title = $title; class = $cls; hwnd = $key; errors = $errors; report = $id }
    }
}

Start-Sleep -Seconds 2
Scan-Visible 'lancement'
foreach ($h in [Win]::Visible($appPid)) { [void][Win]::PostMessage($h, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) }
Start-Sleep -Seconds 1
foreach ($h in @($scanned.Keys)) { $scanned.Remove($h) }
# Pas d'etape Lecons (1023) : sans consentement d'activation, ShowLessonsWindow rouvre
# l'accueil (TrayApplication.cs) ; la fenetre Lecons est analysee par l'etape defi.
$commands = [ordered]@{ parametres = 1012; clavier = 1006; recherche = 1007; statistiques = 1032; apropos = 1016; pause = 1027; defi = 1035; compatibilite = 1034; confidentialite = 1028; accueil = 1010 }
foreach ($name in $commands.Keys) {
    [void][Win]::PostMessage($tray, 0x0111, [IntPtr]$commands[$name], [IntPtr]::Zero)
    Start-Sleep -Seconds 3
    $before = $scanned.Count
    Scan-Visible $name
    if ($scanned.Count -eq $before) { Say ($name + ' : aucune nouvelle fenetre visible') }
    foreach ($h in [Win]::Visible($appPid)) { if ([Win]::Cls($h) -ne 'AZERTYGlobal_Wnd') { [void][Win]::PostMessage($h, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) } }
    Start-Sleep -Seconds 1
    foreach ($h in @($scanned.Keys)) { $scanned.Remove($h) }
}
$summary | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $out 'resume.json') -Encoding UTF8
Say 'fin du scan'
'fin' | Set-Content -Path (Join-Path $out 'done.txt')
