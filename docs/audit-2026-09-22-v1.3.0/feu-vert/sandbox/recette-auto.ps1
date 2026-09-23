# Execute dans Windows Sandbox apres installer-candidat.ps1. Joue sans humain les lignes
# de recette.md que la machine sait trancher (A1, A2, A3, A5, A10, A11, A12, B12), sur une
# app sans accord d'activation. Les autres lignes restent a jouer a la main.
# Les touches simulees (Tab, Entree, Echap) atteignent la fenetre au premier plan, mais le
# hook ne les remappe pas (AG130-07) : aucune ligne de frappe remappee n'est jugee ici.
# Resultats : C:\resultats\auto\ (journal.txt, resultats.json, releves menu-*.txt, msaa-*.txt).
$ErrorActionPreference = 'Continue'
$out = 'C:\resultats\auto'
New-Item -ItemType Directory -Force $out | Out-Null
$log = Join-Path $out 'journal.txt'
function Say([string]$message) {
    $line = '[{0:HH:mm:ss}] {1}' -f (Get-Date), $message
    Add-Content -Path $log -Value $line -Encoding UTF8
    Write-Host $line
}
$results = New-Object System.Collections.ArrayList
function Verdict([string]$id, [string]$verdict, [string]$note) {
    [void]$results.Add([ordered]@{ ligne = $id; verdict = $verdict; observation = $note })
    Say ('{0} : {1} - {2}' -f $id, $verdict, $note)
}

Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class W {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc f, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetMenuItemCount(IntPtr m);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetMenuString(IntPtr m, uint item, StringBuilder s, int n, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetSubMenu(IntPtr m, int pos);
    [DllImport("user32.dll")] public static extern uint GetMenuState(IntPtr m, uint item, uint flags);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int l, t, r, b; }
    [StructLayout(LayoutKind.Sequential)] public struct GUITHREADINFO {
        public int cbSize; public int flags; public IntPtr hwndActive; public IntPtr hwndFocus; public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner; public IntPtr hwndMoveSize; public IntPtr hwndCaret; public RECT rcCaret; }
    [DllImport("user32.dll")] public static extern bool GetGUIThreadInfo(uint tid, ref GUITHREADINFO info);
    public static List<IntPtr> Visible(uint pid) {
        var list = new List<IntPtr>();
        EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }
    public static List<IntPtr> Children(IntPtr parent) {
        var list = new List<IntPtr>();
        EnumChildWindows(parent, (h, l) => { list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }
    public static string Text(IntPtr h) { var s = new StringBuilder(512); GetWindowText(h, s, 512); return s.ToString(); }
    public static string Cls(IntPtr h) { var s = new StringBuilder(256); GetClassName(h, s, 256); return s.ToString(); }
    public static IntPtr Focus(IntPtr h) {
        uint pid; uint tid = GetWindowThreadProcessId(h, out pid);
        var info = new GUITHREADINFO(); info.cbSize = Marshal.SizeOf(info);
        return GetGUIThreadInfo(tid, ref info) ? info.hwndFocus : IntPtr.Zero;
    }
    public static bool Front(IntPtr h) {
        uint pid; uint fg = GetWindowThreadProcessId(GetForegroundWindow(), out pid);
        uint me = GetCurrentThreadId();
        AttachThreadInput(me, fg, true);
        BringWindowToTop(h); SetForegroundWindow(h);
        AttachThreadInput(me, fg, false);
        return GetForegroundWindow() == h;
    }
    public static void Key(byte vk) { keybd_event(vk, 0, 0, UIntPtr.Zero); keybd_event(vk, 0, 2, UIntPtr.Zero); }
    // Nom et role MSAA du HWND, comme les lit le proxy MSAA de UI Automation. Le client UIA
    // manage de PowerShell (System.Windows.Automation) ignore la Dynamic Annotation (N10) et
    // rend tout en Pane : mesure le 2026-09-23 sur ba587cce, d'ou ce passage par oleacc.
    [DllImport("oleacc.dll")] public static extern int AccessibleObjectFromWindow(IntPtr h, uint id, ref Guid iid, [MarshalAs(UnmanagedType.IDispatch)] out object acc);
    [DllImport("oleacc.dll", CharSet = CharSet.Unicode)] public static extern uint GetRoleText(uint role, StringBuilder s, uint n);
    public static string Msaa(IntPtr h) {
        Guid iid = new Guid("618736E0-3C3D-11CF-810C-00AA00389B71"); object acc;
        if (AccessibleObjectFromWindow(h, 0xFFFFFFFC, ref iid, out acc) != 0 || acc == null) return "?\t?";
        try {
            var t = acc.GetType();
            object name = t.InvokeMember("accName", System.Reflection.BindingFlags.GetProperty, null, acc, new object[] { 0 });
            object role = t.InvokeMember("accRole", System.Reflection.BindingFlags.GetProperty, null, acc, new object[] { 0 });
            string roleText = Convert.ToString(role);
            if (role is int) { var s = new StringBuilder(128); GetRoleText((uint)(int)role, s, 128); roleText = s.ToString(); }
            return roleText + "\t" + (name as string ?? "");
        } catch (Exception e) { return "?\t" + e.GetType().Name; }
    }
}
'@

$WM_COMMAND = 0x0111; $WM_CLOSE = 0x0010; $WM_SYSCOMMAND = 0x0112; $SC_CLOSE = 0xF060
$WM_TRAYICON = 0x8001; $WM_RBUTTONUP = 0x0205; $MN_GETHMENU = 0x01E1
$VK_TAB = 0x09; $VK_RETURN = 0x0D; $VK_ESCAPE = 0x1B

$package = Get-AppxPackage -Name 'AZERTYGlobal.AZERTYGlobal'
$pfn = $package.PackageFamilyName
$dataDirs = @((Join-Path $env:LOCALAPPDATA ('Packages\' + $pfn + '\LocalCache\Local\AZERTY Global')), (Join-Path $env:LOCALAPPDATA 'AZERTY Global'))

function App { Get-Process -Name 'AZERTY Global' -ErrorAction SilentlyContinue | Select-Object -First 1 }
function Tray {
    $h = [W]::FindWindowEx([IntPtr]::Zero, [IntPtr]::Zero, 'AZERTYGlobal_Wnd', [NullString]::Value)
    if ($h -eq [IntPtr]::Zero) { $h = [W]::FindWindowEx([IntPtr](-3), [IntPtr]::Zero, 'AZERTYGlobal_Wnd', [NullString]::Value) }
    return $h
}
function Command([int]$id) { [void][W]::PostMessage((Tray), $WM_COMMAND, [IntPtr]$id, [IntPtr]::Zero) }
function WaitFor([scriptblock]$condition, [int]$seconds = 6) {
    $end = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $end) { $value = & $condition; if ($value) { return $value }; Start-Sleep -Milliseconds 250 }
    return $null
}
function VisibleOf([string]$cls) {
    $p = App; if (-not $p) { return $null }
    foreach ($h in [W]::Visible([uint32]$p.Id)) { if ([W]::Cls($h) -eq $cls) { return $h } }
    return $null
}
# Fenetre visible de l'app autre que celles deja connues (dialogues sans classe propre).
function NewWindow($known) {
    $p = App; if (-not $p) { return $null }
    foreach ($h in [W]::Visible([uint32]$p.Id)) { if (-not ($known -contains [string]$h) -and [W]::Cls($h) -ne 'AZERTYGlobal_Wnd') { return $h } }
    return $null
}
function KnownWindows { $p = App; if (-not $p) { return @() }; return @([W]::Visible([uint32]$p.Id) | ForEach-Object { [string]$_ }) }
function CloseOthers {
    $p = App; if (-not $p) { return }
    foreach ($h in [W]::Visible([uint32]$p.Id)) { if ([W]::Cls($h) -ne 'AZERTYGlobal_Wnd') { [void][W]::PostMessage($h, $WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero) } }
    Start-Sleep -Seconds 1
}
function DataFile([string]$name) {
    foreach ($d in $dataDirs) { $f = Join-Path $d $name; if (Test-Path $f) { return $f } }
    return $null
}
function Consent {
    $f = DataFile 'config.json'; if (-not $f) { return $false }
    try { return ((Get-Content $f -Raw | ConvertFrom-Json).activationConsent -eq $true) } catch { return $false }
}
function Launch {
    Start-Process ('shell:AppsFolder\' + $pfn + '!AZERTYGlobal')
    return (WaitFor { $p = App; if ($p -and (Tray) -ne [IntPtr]::Zero) { $p } } 10)
}
function Quit {
    $p = App; if (-not $p) { return $true }
    Command 1005
    return [bool](WaitFor { -not (App) } 10)
}

# --- A1 : journal d'installation (dernier passage) ------------------------------------
try {
    $install = Get-Content 'C:\resultats\installation.txt' -Encoding UTF8
    $start = ($install | Select-String 'SHA-256 candidat' | Select-Object -Last 1).LineNumber
    $block = ($install | Select-Object -Skip ($start - 1)) -join "`n"
    $expected = (Get-FileHash 'C:\candidat\candidat.msixbundle' -Algorithm SHA256).Hash.ToLower()
    $ok = ($block -match [regex]::Escape($expected)) -and ($block -match '_1\.3\.0\.0_x64__w9kghr08zmhbg') -and ($block -match 'shadow stack ON ; CFG ON')
    Verdict 'A1' $(if ($ok) { 'OK' } else { 'ECHEC' }) ('empreinte ' + $expected.Substring(0, 12) + ', paquet 1.3.0.0 x64, shadow stack et CFG lus dans installation.txt')
} catch { Verdict 'A1' 'ECHEC' $_.Exception.Message }

# --- A2 : fermer l'accueil par la croix, puis par Echap, sans accord memorise ---------
try {
    $ob = WaitFor { VisibleOf 'AZERTYGlobal_Onboarding' } 8
    if (-not $ob) { throw 'accueil absent au lancement' }
    [void][W]::PostMessage($ob, $WM_SYSCOMMAND, [IntPtr]$SC_CLOSE, [IntPtr]::Zero)
    $closed1 = [bool](WaitFor { -not (VisibleOf 'AZERTYGlobal_Onboarding') } 5)
    $alive1 = [bool](App); $consent1 = Consent
    Command 1010
    $ob = WaitFor { VisibleOf 'AZERTYGlobal_Onboarding' } 5
    $front = if ($ob) { [W]::Front($ob) } else { $false }
    [W]::Key($VK_ESCAPE)
    $closed2 = [bool](WaitFor { -not (VisibleOf 'AZERTYGlobal_Onboarding') } 5)
    $alive2 = [bool](App); $consent2 = Consent
    $ok = $closed1 -and $alive1 -and -not $consent1 -and $front -and $closed2 -and $alive2 -and -not $consent2
    Verdict 'A2' $(if ($ok) { 'OK' } else { 'ECHEC' }) ("croix (SC_CLOSE) : fermee=$closed1, app vivante=$alive1, accord=$consent1 ; Echap au premier plan=$front : fermee=$closed2, app vivante=$alive2, accord=$consent2. Clavier du Bloc-notes non juge (frappe simulee).")
} catch { Verdict 'A2' 'ECHEC' $_.Exception.Message }

# --- A3 : accueil au clavier seul jusqu'a << Activer et essayer >>, Entree -------------
try {
    Command 1010
    $ob = WaitFor { VisibleOf 'AZERTYGlobal_Onboarding' } 5
    if (-not $ob) { throw 'accueil non rouvert' }
    $try = [W]::Children($ob) | Where-Object { [W]::Cls($_) -eq 'Button' -and [W]::Text($_) -match 'Activer et essayer|Activate and try' } | Select-Object -First 1
    if (-not $try) { throw ('bouton introuvable ; boutons : ' + (([W]::Children($ob) | Where-Object { [W]::Cls($_) -eq 'Button' } | ForEach-Object { [W]::Text($_) }) -join ' | ')) }
    $front = [W]::Front($ob)
    $path = @()
    for ($i = 0; $i -lt 20 -and [W]::Focus($ob) -ne $try; $i++) {
        [W]::Key($VK_TAB); Start-Sleep -Milliseconds 300
        $f = [W]::Focus($ob); $path += ([W]::Text($f) -replace '\s+', ' ')
    }
    $reached = ([W]::Focus($ob) -eq $try)
    if ($reached) { [W]::Key($VK_RETURN) }
    $consent = [bool](WaitFor { Consent } 5)
    # Le bouton lance le module d'apprentissage de l'accueil (LaunchLearningModule), pas la fenetre Lecons.
    $lesson = [bool](WaitFor { VisibleOf 'AZERTYGlobal_Learning' } 5)
    $ok = $front -and $reached -and $consent -and $lesson
    Verdict 'A3' $(if ($ok) { 'OK' } else { 'ECHEC' }) ("premier plan=$front ; Tab x$($path.Count) jusqu'au bouton=$reached (" + ($path -join ' > ') + ") ; Entree : accord=$consent, lecon ouverte=$lesson. Remappage actif non juge (frappe simulee).")
    CloseOthers
} catch { Verdict 'A3' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- Releves : menu de l'icone et noms UI Automation -----------------------------------
function MenuItems([IntPtr]$menu, [string]$indent) {
    $lines = @()
    $n = [W]::GetMenuItemCount($menu)
    for ($i = 0; $i -lt $n; $i++) {
        $state = [W]::GetMenuState($menu, [uint32]$i, 0x400)
        $s = New-Object System.Text.StringBuilder 512
        [void][W]::GetMenuString($menu, [uint32]$i, $s, 512, 0x400)
        $sub = [W]::GetSubMenu($menu, $i)
        if ($state -band 0x800) { $lines += ($indent + '----') ; continue }
        $lines += ($indent + ($s.ToString() -replace "`t", ' [') + $(if ($s.ToString() -match "`t") { ']' } else { '' }) + $(if ($sub -ne [IntPtr]::Zero) { ' >' } else { '' }))
        if ($sub -ne [IntPtr]::Zero) { $lines += MenuItems $sub ($indent + '    ') }
    }
    return $lines
}
function DumpMenu {
    [void][W]::PostMessage((Tray), $WM_TRAYICON, [IntPtr]::Zero, [IntPtr]$WM_RBUTTONUP)
    $popup = WaitFor { $p = App; foreach ($h in [W]::Visible([uint32]$p.Id)) { if ([W]::Cls($h) -eq '#32768') { return $h } } } 5
    if (-not $popup) { return $null }
    $menu = [W]::SendMessage($popup, $MN_GETHMENU, [IntPtr]::Zero, [IntPtr]::Zero)
    $lines = MenuItems $menu ''
    [W]::Key($VK_ESCAPE)
    [void](WaitFor { $p = App; -not ([W]::Visible([uint32]$p.Id) | Where-Object { [W]::Cls($_) -eq '#32768' }) } 3)
    return , $lines
}
# Une ligne par HWND : classe, role MSAA, nom MSAA, visible. Les onglets caches des
# Parametres gardent leurs HWND : ils sont releves sans etre affiches.
function MsaaNames([IntPtr]$hwnd) {
    $rows = @('Window' + "`t" + [W]::Msaa($hwnd) + "`t" + [W]::IsWindowVisible($hwnd))
    foreach ($c in [W]::Children($hwnd)) { $rows += ([W]::Cls($c) + "`t" + [W]::Msaa($c) + "`t" + [W]::IsWindowVisible($c)) }
    return , $rows
}
function DumpNames([string]$lang) {
    $names = @()
    foreach ($step in @(@{ n = 'pause'; id = 1027 }, @{ n = 'parametres'; id = 1012 }, @{ n = 'recherche'; id = 1007 })) {
        # La Recherche ne s'ouvre qu'une fois sur deux ici (essais 1 et 2 du 2026-09-23), et
        # jamais avec l'accueil au premier plan (essai 3) : suspension liee au premier plan
        # (_suspendedForCompatibility) probable, non expliquee. A juger a la main (A8, B12).
        $known = KnownWindows
        Command $step.id
        $h = WaitFor { NewWindow $known } 8
        if (-not $h) { CloseOthers; $known = KnownWindows; Command $step.id; $h = WaitFor { NewWindow $known } 8 }
        if (-not $h) { $fg = [W]::GetForegroundWindow(); $rows = @('aucune fenetre ; premier plan : ' + [W]::Cls($fg) + ' "' + [W]::Text($fg) + '"') } else { $rows = MsaaNames $h }
        Set-Content -Path (Join-Path $out ('msaa-' + $step.n + '-' + $lang + '.txt')) -Value $rows -Encoding UTF8
        $names += $rows
        CloseOthers
    }
    return , $names
}
# Langue deduite du menu (l'app suit la langue de Windows au premier lancement).
function Pass {
    $menu = DumpMenu
    if (-not $menu) { return @{ lang = '?'; menu = $null; names = @() } }
    $lang = if (@($menu | Where-Object { $_ -match '^Quitter' }).Count) { 'fr' } else { 'en' }
    Set-Content -Path (Join-Path $out ('menu-' + $lang + '.txt')) -Value $menu -Encoding UTF8
    return @{ lang = $lang; menu = $menu; names = (DumpNames $lang) }
}
function Missing($rows, $expected) { return @($expected | Where-Object { $e = $_; -not ($rows | Where-Object { ($_ -split "`t")[2] -match $e }) }) }

$fr = @('^Heures$', '^Minutes$', '^Augmenter les heures$', '^Diminuer les heures$', '^Augmenter les minutes$', 'Clavier virtuel', '^Recherche$', 'Apps suspendues', 'Rechercher un caract')
$en = @('^Hours$', '^Minutes$', '^Increase hours$', '^Decrease hours$', '^Increase minutes$', 'Virtual keyboard', '^Search$', 'Suspended apps', 'Find a character')
try {
    $first = Pass
    Command 1033; Start-Sleep -Seconds 1
    $second = Pass
    Command 1033; Start-Sleep -Seconds 1
    $pFr = @($first, $second) | Where-Object { $_.lang -eq 'fr' } | Select-Object -First 1
    $pEn = @($first, $second) | Where-Object { $_.lang -eq 'en' } | Select-Object -First 1
    $menuFr = if ($pFr) { $pFr.menu } else { $null }; $menuEn = if ($pEn) { $pEn.menu } else { $null }
    $uiaFr = if ($pFr) { $pFr.names } else { @() }; $uiaEn = if ($pEn) { $pEn.names } else { @() }

    if (-not $menuFr -or -not $menuEn) { Verdict 'A10' 'ECHEC' ("menu releve en " + $first.lang + ' puis ' + $second.lang + ' : bascule FR/EN ou ouverture du menu en defaut') }
    else {
        $top = @($menuFr | Where-Object { $_ -notmatch '^\s' -and $_ -ne '----' })
        $topEn = @($menuEn | Where-Object { $_ -notmatch '^\s' -and $_ -ne '----' })
        $subs = @($top | Where-Object { $_ -match ' >$' })
        $needSubs = @('Couches', 'Apprendre', 'propos') | Where-Object { $k = $_; -not ($subs | Where-Object { $_ -match $k }) }
        $same = @(for ($i = 0; $i -lt [Math]::Min($top.Count, $topEn.Count); $i++) { if ($top[$i] -eq $topEn[$i]) { $top[$i] } })
        $ok = ($top.Count -eq 12) -and ($topEn.Count -eq 12) -and ($needSubs.Count -eq 0) -and ($same.Count -le 1)
        Verdict 'A10' $(if ($ok) { 'OK' } else { 'A VOIR' }) ("$($top.Count) lignes FR, $($topEn.Count) EN ; sous-menus : " + ($subs -join ', ') + $(if ($needSubs) { ' ; manquants : ' + ($needSubs -join ', ') } else { '' }) + " ; lignes identiques FR/EN : " + $(if ($same) { $same -join ', ' } else { 'aucune' }) + '. Releves menu-fr.txt, menu-en.txt. Ouverture des sous-menus a la souris non jugee.')
    }
    $missFr = Missing $uiaFr $fr; $missEn = Missing $uiaEn $en
    $ok = ($missFr.Count -eq 0) -and ($missEn.Count -eq 0)
    Verdict 'B12' $(if ($ok) { 'OK' } else { 'A VOIR' }) ('noms MSAA de Pause, Parametres, Recherche. Absents FR : ' + $(if ($missFr) { $missFr -join ', ' } else { 'aucun' }) + ' ; absents EN : ' + $(if ($missEn) { $missEn -join ', ' } else { 'aucun' }) + '. Lecture par le Narrateur non jugee. Releves msaa-*.txt.')
} catch { Verdict 'A10/B12' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- A5 : quitter par l'icone, relancer depuis Demarrer --------------------------------
try {
    $exited = Quit
    $p = Launch
    $consent = Consent
    $ob = WaitFor { VisibleOf 'AZERTYGlobal_Onboarding' } 4
    $ok = $exited -and $p -and $consent
    Verdict 'A5' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ("sortie par Quitter=$exited ; relance=$([bool]$p) ; accord conserve=$consent ; accueil reaffiche=$([bool]$ob). Clavier systeme a la sortie et remappage revenu non juges (frappe simulee).")
    CloseOthers
} catch { Verdict 'A5' 'ECHEC' $_.Exception.Message }

# --- A11 : config.json illisible ([1]) au demarrage ------------------------------------
try {
    [void](Quit)
    $cfg = DataFile 'config.json'
    if (-not $cfg) { throw ('config.json introuvable dans ' + ($dataDirs -join ' ; ')) }
    $logFile = Join-Path (Split-Path -Parent $cfg) 'error.log'
    $before = if (Test-Path $logFile) { @(Select-String -Path $logFile -Pattern 'JsonException').Count } else { 0 }
    Set-Content -Path $cfg -Value '[1]' -NoNewline -Encoding ASCII
    $p = Launch
    Start-Sleep -Seconds 5
    $alive = [bool](App)
    $after = if (Test-Path $logFile) { @(Select-String -Path $logFile -Pattern 'JsonException').Count } else { 0 }
    $content = Get-Content $cfg -Raw
    $ok = $p -and $alive -and ($after -gt $before) -and ($content -eq '[1]')
    Verdict 'A11' $(if ($ok) { 'OK' } else { 'ECHEC' }) ("config $cfg ; demarrage=$([bool]$p), vivante 5 s apres=$alive ; JsonException dans error.log : $before -> $after ; fichier inchange=$($content -eq '[1]')")
    if (Test-Path $logFile) { Copy-Item $logFile (Join-Path $out 'error.log') -Force }
    CloseOthers
} catch { Verdict 'A11' 'ECHEC' $_.Exception.Message }

# --- A12 : desinstallation --------------------------------------------------------------
try {
    Remove-AppxPackage -Package $package.PackageFullName
    $gone = [bool](WaitFor { -not (App) } 10)
    $removed = -not (Get-AppxPackage -Name 'AZERTYGlobal.AZERTYGlobal')
    Verdict 'A12' $(if ($gone -and $removed) { 'OK' } else { 'ECHEC' }) ("processus arrete=$gone ; paquet retire=$removed. Clavier systeme utilisable non juge (frappe simulee).")
} catch { Verdict 'A12' 'ECHEC' $_.Exception.Message }

$results | ConvertTo-Json -Depth 3 | Set-Content -Path (Join-Path $out 'resultats.json') -Encoding UTF8
Say 'fin de la recette automatique'
'fin' | Set-Content -Path (Join-Path $out 'done.txt')
