# Outils communs des recettes du Sandbox, charges par dot-source apres que l'appelant a pose
# $out (dossier de resultats) : recette-auto.ps1 (C:\resultats\auto) et recette-p1.ps1
# (C:\resultats\p1). Journal (Say, Verdict), types Win32 (W, W2), pilotage de l'app (Tray,
# Command, menus, fenetres, config.json) et etat du demarrage automatique.
if (-not $out) { throw 'recette-outils.ps1 : poser $out avant le dot-source' }
$ErrorActionPreference = 'Continue'
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
    // accState MSAA (-1 si illisible) : les cases papier de l'accueil (BS_OWNERDRAW, A7) ne
    // repondent plus a BM_GETCHECK ; l'app pose leur role et leur etat (AccessibleName.TrySetCheckState).
    public static int MsaaState(IntPtr h) {
        Guid iid = new Guid("618736E0-3C3D-11CF-810C-00AA00389B71"); object acc;
        if (AccessibleObjectFromWindow(h, 0xFFFFFFFC, ref iid, out acc) != 0 || acc == null) return -1;
        try { return Convert.ToInt32(acc.GetType().InvokeMember("accState", System.Reflection.BindingFlags.GetProperty, null, acc, new object[] { 0 })); }
        catch { return -1; }
    }
}
'@

# Complements des lignes Rxx : identifiants et styles de controles, texte d'un champ d'un
# autre processus (WM_GETTEXT, que GetWindowText ne lit pas pour un EDIT), rendu par
# PrintWindow, noms MSAA des enfants (onglets) et reponse d'une fenetre.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
public static class W2 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int l, t, r, b; }
    [DllImport("user32.dll")] public static extern int GetDlgCtrlID(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr h);
    [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr h, bool altTab);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int index);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")] public static extern IntPtr SendString(IntPtr h, uint msg, IntPtr w, string l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")] public static extern IntPtr SendBuffer(IntPtr h, uint msg, IntPtr w, StringBuilder l);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("oleacc.dll")] public static extern int AccessibleObjectFromWindow(IntPtr h, uint id, ref Guid iid, [MarshalAs(UnmanagedType.IDispatch)] out object acc);
    public static string Text(IntPtr h) { var s = new StringBuilder(1024); SendBuffer(h, 0x000D, (IntPtr)1024, s); return s.ToString(); }
    public static void SetText(IntPtr h, string text) { SendString(h, 0x000C, IntPtr.Zero, text); }
    public static int[] RectOf(IntPtr h) { RECT r; GetWindowRect(h, out r); return new int[] { r.l, r.t, r.r, r.b }; }
    public static string Rect(IntPtr h) { RECT r; GetWindowRect(h, out r); return r.l + "," + r.t + "," + r.r + "," + r.b; }
    public static int[] ClientSize(IntPtr h) { RECT r; GetClientRect(h, out r); return new int[] { r.r - r.l, r.b - r.t }; }
    // WM_NULL par SendMessageTimeout (SMTO_ABORTIFHUNG, 3 s) : la fenetre repond-elle ?
    public static bool Responds(IntPtr h) { IntPtr res; return SendMessageTimeout(h, 0, IntPtr.Zero, IntPtr.Zero, 2, 3000, out res) != IntPtr.Zero; }
    public static void Chord(byte modifier, byte vk) {
        keybd_event(modifier, 0, 0, UIntPtr.Zero); keybd_event(vk, 0, 0, UIntPtr.Zero);
        keybd_event(vk, 0, 2, UIntPtr.Zero); keybd_event(modifier, 0, 2, UIntPtr.Zero);
    }
    // Zone cliente rendue par PrintWindow (PW_CLIENTONLY | PW_RENDERFULLCONTENT), lue sur
    // une grille de 10 x 10 points : { couleurs distinctes, luminance de la dominante }.
    public static int[] Render(IntPtr h) {
        RECT r; GetClientRect(h, out r); int w = r.r - r.l, hh = r.b - r.t;
        if (w <= 0 || hh <= 0) return new int[] { 0, -1 };
        using (var bmp = new Bitmap(w, hh)) {
            using (var g = Graphics.FromImage(bmp)) { IntPtr dc = g.GetHdc(); try { PrintWindow(h, dc, 3); } finally { g.ReleaseHdc(dc); } }
            var count = new Dictionary<int, int>();
            for (int i = 0; i < 10; i++) for (int j = 0; j < 10; j++) {
                int c = bmp.GetPixel(w * (2 * i + 1) / 20, hh * (2 * j + 1) / 20).ToArgb();
                int n; count.TryGetValue(c, out n); count[c] = n + 1;
            }
            int best = 0, bestN = -1;
            foreach (var kv in count) if (kv.Value > bestN) { best = kv.Key; bestN = kv.Value; }
            Color b = Color.FromArgb(best);
            return new int[] { count.Count, (b.R * 299 + b.G * 587 + b.B * 114) / 1000 };
        }
    }
    // Noms MSAA des enfants d'un controle (onglets d'un SysTabControl32), identifiants 1..n.
    public static string[] ChildNames(IntPtr h) {
        Guid iid = new Guid("618736E0-3C3D-11CF-810C-00AA00389B71"); object acc;
        var list = new List<string>();
        if (AccessibleObjectFromWindow(h, 0xFFFFFFFC, ref iid, out acc) != 0 || acc == null) return list.ToArray();
        try {
            var t = acc.GetType();
            int n = Convert.ToInt32(t.InvokeMember("accChildCount", System.Reflection.BindingFlags.GetProperty, null, acc, null));
            for (int i = 1; i <= n; i++) {
                object name = t.InvokeMember("accName", System.Reflection.BindingFlags.GetProperty, null, acc, new object[] { i });
                list.Add(name as string ?? "");
            }
        } catch (Exception e) { list.Add("?" + e.GetType().Name); }
        return list.ToArray();
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

# --- Outils des lignes Rxx (RECETTE-1.3.0.md) -------------------------------------------
$VK_SPACE = 0x20; $VK_LEFT = 0x25; $VK_UP = 0x26; $VK_RIGHT = 0x27; $VK_DOWN = 0x28
$VK_SHIFT = 0x10; $VK_MENU = 0x12; $VK_F4 = 0x73; $VK_N = 0x4E; $VK_T = 0x54
$BM_GETCHECK = 0x00F0; $BM_SETCHECK = 0x00F1; $WM_MOUSEWHEEL = 0x020A; $TCM_GETCURSEL = 0x130B
$today = (Get-Date).ToString('yyyy-MM-dd')
$j3 = (Get-Date).AddDays(-3).ToString('yyyy-MM-dd'); $j8 = (Get-Date).AddDays(-8).ToString('yyyy-MM-dd')

function Ensure { if (-not (App)) { [void](Launch) } }
function DataDir { $f = DataFile 'config.json'; if ($f) { return (Split-Path -Parent $f) }; return $dataDirs[0] }
# Memes ecritures que Cle et Usage de la fiche (section 3). L'app doit etre quittee.
function Cle([string]$k, $v) {
    $f = Join-Path (DataDir) 'config.json'
    $o = Get-Content $f -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($null -eq $v) { $o.PSObject.Properties.Remove($k) } else { $o | Add-Member -NotePropertyName $k -NotePropertyValue $v -Force }
    [IO.File]::WriteAllText($f, ($o | ConvertTo-Json -Depth 10))
}
function CfgValue([string]$k) {
    $f = DataFile 'config.json'; if (-not $f) { return $null }
    try { return (Get-Content $f -Raw -Encoding UTF8 | ConvertFrom-Json).$k } catch { return $null }
}
function Usage([int]$jours) {
    [IO.File]::WriteAllText((Join-Path (DataDir) 'usage-stats.json'), ('{{"firstRemapDate":"{0}","lastActiveDate":"{0}","activeDaysCount":{1},"totalActiveMinutes":10,"accentedUppercaseCount":20}}' -f $today, $jours))
}
function ReviewCount { $v = CfgValue 'reviewPromptCount'; if ($null -eq $v) { return 0 }; return [int]$v }
function Hash([string]$f) { if ($f -and (Test-Path $f)) { return (Get-FileHash $f -Algorithm SHA256).Hash }; return 'absent' }
function LogCount([string]$pattern) { $f = DataFile 'error.log'; if (-not $f) { return 0 }; return @(Select-String -Path $f -Pattern $pattern).Count }

function Child([IntPtr]$parent, [int]$id) {
    foreach ($c in [W]::Children($parent)) { if ([W2]::GetDlgCtrlID($c) -eq $id) { return $c } }
    return [IntPtr]::Zero
}
# Meme message qu'un clic sur le bouton : WM_COMMAND (BN_CLICKED, id) a sa fenetre.
function ClickId([IntPtr]$parent, [int]$id) {
    [void][W]::PostMessage($parent, $WM_COMMAND, [IntPtr]$id, (Child $parent $id)); Start-Sleep -Milliseconds 700
}
# Case papier (BS_OWNERDRAW = 0xB, accueil depuis A7) : etat lu en MSAA (STATE_SYSTEM_CHECKED
# = 0x10) ; case Windows ordinaire : BM_GETCHECK.
function Checked([IntPtr]$h) {
    if ((([W2]::GetWindowLong($h, -16)) -band 0xF) -eq 0xB) { $s = [W]::MsaaState($h); return ($s -ge 0) -and (($s -band 0x10) -ne 0) }
    return ([W]::SendMessage($h, $BM_GETCHECK, [IntPtr]::Zero, [IntPtr]::Zero).ToInt64() -eq 1)
}
function WaitGone([IntPtr]$h, [int]$seconds = 5) { return [bool](WaitFor { -not [W]::IsWindowVisible($h) } $seconds) }
function StaticTexts([IntPtr]$win) {
    return @([W]::Children($win) | Where-Object { [W]::Cls($_) -eq 'Static' -and [W]::IsWindowVisible($_) } | ForEach-Object { [W2]::Text($_) })
}
function FocusId([IntPtr]$win) { $f = [W]::Focus($win); if ($f -eq [IntPtr]::Zero) { return 0 }; return [W2]::GetDlgCtrlID($f) }
# Tab jusqu'au controle que $match accepte, au plus $max fois ; rend son HWND ou zero.
function TabTo([IntPtr]$win, [scriptblock]$match, [int]$max = 25) {
    $trace = @()
    for ($i = 0; $i -le $max; $i++) {
        $f = [W]::Focus($win)
        if ($f -ne [IntPtr]::Zero -and (& $match $f)) { return $f }
        $trace += $(if ($f -eq [IntPtr]::Zero) { '0' } else { [W]::Cls($f) + ':' + [W2]::GetDlgCtrlID($f) })
        if ($i -lt $max) { [W]::Key($VK_TAB); Start-Sleep -Milliseconds 250 }
    }
    Say ('TabTo sans succes dans ' + [W]::Cls($win) + ' ; premier plan ' + [W]::Cls([W]::GetForegroundWindow()) + ' ; focus : ' + (($trace | Select-Object -First 12) -join ' > '))
    return [IntPtr]::Zero
}
function ShiftTab { [W2]::Chord($VK_SHIFT, $VK_TAB); Start-Sleep -Milliseconds 300 }
# Premier plan verifie : W.Front, puis SwitchToThisWindow si le verrou de premier plan resiste.
function FrontSure([IntPtr]$h) {
    for ($i = 0; $i -lt 4; $i++) {
        if ($i -lt 2) { [void][W]::Front($h) } else { [W2]::SwitchToThisWindow($h, $true) }
        Start-Sleep -Milliseconds 300
        if ([W]::GetForegroundWindow() -eq $h) { return $true }
    }
    Say ('premier plan refuse a ' + [W]::Cls($h) + ' ; premier plan : ' + [W]::Cls([W]::GetForegroundWindow()))
    return $false
}
# Premier plan neutre (passage 1 du 28/09) : la console de la recette tourne elevee, l'app ne
# peut pas lire son processus et se suspend (UnknownForeground, ForegroundMonitor.cs) ; ses
# fenetres ignorent alors le clavier (SetInputPaused) et la Recherche ne s'ouvre pas. Une
# fenetre de l'Explorateur au premier plan leve la suspension avant d'ouvrir une fenetre.
function Calm {
    $find = { $h = [W]::FindWindowEx([IntPtr]::Zero, [IntPtr]::Zero, 'CabinetWClass', [NullString]::Value); if ($h -ne [IntPtr]::Zero) { $h } }
    $ex = & $find
    if (-not $ex) { Start-Process explorer.exe $out; $ex = WaitFor $find 10 }
    if (-not $ex) { Say 'Calm : aucune fenetre de l Explorateur'; return $false }
    $ok = FrontSure $ex; Start-Sleep -Milliseconds 700
    return $ok
}
# Suspension vue du menu : Rechercher un caractere grise alors que l'app est active.
function Suspended {
    $rows = DumpMenuRows
    $s = @($rows | Where-Object { $_.depth -eq 0 -and $_.text -match '^Rechercher un caract|^Find a character' }) | Select-Object -First 1
    if (-not $s) { return '?' }
    return [string]$s.grayed
}
# Boite de message de l'app (#32770) : la ferme par la touche $vk, sinon par WM_COMMAND $id.
function AnswerBox([byte]$vk, [int]$id) {
    $mb = WaitFor { VisibleOf '#32770' } 4
    if (-not $mb) { return 'aucune' }
    [void][W]::Front($mb); [W]::Key($vk)
    if (WaitGone $mb 3) { return 'fermee au clavier' }
    [void][W]::PostMessage($mb, $WM_COMMAND, [IntPtr]$id, [IntPtr]::Zero)
    if (WaitGone $mb 3) { return 'fermee par WM_COMMAND' }
    return 'restee ouverte'
}
# Menu de l'icone en lignes : profondeur, texte, grise (MF_GRAYED | MF_DISABLED), separateur,
# sous-menu. menu-*.txt garde son format (A10) ; ces lignes servent aux etats de R57b.
function MenuRows([IntPtr]$menu, [int]$depth) {
    $rows = @()
    $n = [W]::GetMenuItemCount($menu)
    for ($i = 0; $i -lt $n; $i++) {
        $state = [W]::GetMenuState($menu, [uint32]$i, 0x400)
        $s = New-Object System.Text.StringBuilder 512
        [void][W]::GetMenuString($menu, [uint32]$i, $s, 512, 0x400)
        $sub = [W]::GetSubMenu($menu, $i)
        $rows += [pscustomobject]@{ depth = $depth; text = $s.ToString(); grayed = [bool]($state -band 0x3); separator = [bool]($state -band 0x800); sub = ($sub -ne [IntPtr]::Zero) }
        if ($sub -ne [IntPtr]::Zero) { $rows += @(MenuRows $sub ($depth + 1)) }
    }
    return $rows
}
function DumpMenuRows {
    [void][W]::PostMessage((Tray), $WM_TRAYICON, [IntPtr]::Zero, [IntPtr]$WM_RBUTTONUP)
    $popup = WaitFor { $p = App; foreach ($h in [W]::Visible([uint32]$p.Id)) { if ([W]::Cls($h) -eq '#32768') { return $h } } } 5
    if (-not $popup) { return @() }
    $menu = [W]::SendMessage($popup, $MN_GETHMENU, [IntPtr]::Zero, [IntPtr]::Zero)
    $rows = @(MenuRows $menu 0)
    [W]::Key($VK_ESCAPE)
    [void](WaitFor { $p = App; -not ([W]::Visible([uint32]$p.Id) | Where-Object { [W]::Cls($_) -eq '#32768' }) } 3)
    return , $rows
}
# Enfants directs (retrait de 4 espaces) d'une ligne de menu-*.txt, separateurs exclus.
function SubItems($menu, [string]$pattern) {
    $lines = @($menu)
    for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match $pattern) { break } }
    if ($i -ge $lines.Count) { return $null }
    $items = @()
    for ($k = $i + 1; $k -lt $lines.Count -and $lines[$k] -match '^    '; $k++) {
        if ($lines[$k] -match '^    \S' -and $lines[$k] -ne '    ----') { $items += $lines[$k].Trim() }
    }
    return , $items
}
function Fits($items, $want) {
    if (-not $items -or $items.Count -ne $want.Count) { return $false }
    for ($i = 0; $i -lt $want.Count; $i++) { if ($items[$i] -notmatch $want[$i]) { return $false } }
    return $true
}

# Demarrage automatique. src/AutoStart.cs n'ecrit aucune valeur de registre : en paquet, il
# appelle l'API StartupTask (RequestEnableAsync, Disable) et Windows garde l'etat. Reference :
# StartupTask.GetAsync lu DANS le paquet par Invoke-CommandInDesktopPackage. La valeur State
# de la cle SystemAppData du paquet est relevee a cote, sans hypothese sur son codage.
$startupKey = 'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\' + $pfn + '\AZERTYGlobalStartup'
$probeFile = Join-Path $out 'etat-demarrage.txt'
$probeCode = @'
$ErrorActionPreference = 'Stop'
try {
    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    $null = [Windows.ApplicationModel.StartupTask, Windows.ApplicationModel, ContentType = WindowsRuntime]
    $asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
    $task = $asTask.MakeGenericMethod([Windows.ApplicationModel.StartupTask]).Invoke($null, @([Windows.ApplicationModel.StartupTask]::GetAsync('AZERTYGlobalStartup')))
    if (-not $task.Wait(10000)) { throw 'delai depasse' }
    $state = [string]$task.Result.State
} catch { $state = '? ' + $_.Exception.Message }
[IO.File]::WriteAllText('OUTFILE', $state)
'@
$probeArgs = '-NoProfile -WindowStyle Hidden -EncodedCommand ' + [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($probeCode.Replace('OUTFILE', $probeFile)))
function StartupRegistry {
    try { return [string](Get-ItemProperty -Path $startupKey -Name State -ErrorAction Stop).State } catch { return 'absente' }
}
function StartupProbe {
    Remove-Item $probeFile -Force -ErrorAction SilentlyContinue
    try { Invoke-CommandInDesktopPackage -PackageFamilyName $pfn -AppId 'AZERTYGlobal' -Command 'powershell.exe' -Args $probeArgs -ErrorAction Stop }
    catch { return ('? ' + $_.Exception.Message) }
    $v = WaitFor { if (Test-Path $probeFile) { $t = Get-Content $probeFile -Raw; if ($t) { $t.Trim() } } } 20
    if ($v) { return $v }
    return '? sans reponse'
}
function StartupState {
    $p = StartupProbe; $r = StartupRegistry; $c = CfgValue 'autoStartEnabled'
    return @{ probe = $p; reg = $r; cfg = $c; text = "tache=$p, State=$r, autoStartEnabled=$c" }
}
function IsOn($s) { return ($s.probe -match '^Enabled') }
function Known($s) { return ($s.probe -match '^(Enabled|EnabledByPolicy|Disabled|DisabledByUser|DisabledByPolicy)$') }
