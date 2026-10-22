# Execute dans Windows Sandbox apres installer-candidat.ps1. Joue sans humain les lignes
# de recette.md que la machine sait trancher (A1, A2, A3, A5, A10, A11, A12, B12), puis les
# lignes de docs/recette-2026-09-27-v1.3.0/RECETTE-1.3.0.md qu'elle sait trancher (R20,
# R22 a R26, R37, R39 a R41, R45, R47, R49, R57, R57b, R58, R60, R70 a R75), sur une app
# sans accord d'activation au depart. Les autres lignes restent a jouer a la main.
# Les touches simulees (Tab, Entree, Echap) atteignent la fenetre au premier plan, mais le
# hook ne les remappe pas (AG130-07) : aucune ligne de frappe remappee n'est jugee ici.
# Verdicts : OK ; OK (partiel) quand une partie de la ligne reste a l'oeil, au banc de
# captures ou a un test unitaire (l'observation le dit) ; A VOIR quand la preuve manque ;
# ECHEC. lire-resultats.ps1, sur l'hote, les compare aux niveaux attendus.
# Resultats : C:\resultats\auto\ (journal.txt, resultats.json, releves menu-*.txt, msaa-*.txt).
# -Fermer : eteint le Sandbox une fois done.txt ecrit (lancer-recette.ps1 -Auto le passe).
param([switch]$Fermer)
$ErrorActionPreference = 'Continue'
$out = 'C:\resultats\auto'
. (Join-Path $PSScriptRoot 'recette-outils.ps1')

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
# Bouton trouve par son identifiant, comme R20 (IDC_BTN_TRY = 2011, src/OnboardingWindow.cs:25) :
# son libelle suit la langue et l'accord (Activer et essayer, Turn on and try, Essayer
# maintenant). Le chemin reste le clavier (Tab, Entree), objet de la ligne. Si l'Entree ne
# donne pas l'accord, clic par identifiant en repli (ClickId 2011), pour que A5, B12, R57b,
# R57 et R60 ne tournent pas sans accord ; la ligne passe alors A VOIR (clavier : R11).
try {
    Command 1010
    $ob = WaitFor { VisibleOf 'AZERTYGlobal_Onboarding' } 5
    if (-not $ob) { throw 'accueil non rouvert' }
    $try = Child $ob 2011
    if ($try -eq [IntPtr]::Zero) { throw ('bouton 2011 introuvable ; boutons : ' + (([W]::Children($ob) | Where-Object { [W]::Cls($_) -eq 'Button' } | ForEach-Object { [string][W2]::GetDlgCtrlID($_) + ':' + [W]::Text($_) }) -join ' | ')) }
    $label = [W]::Text($try)
    $front = [W]::Front($ob)
    $path = @()
    for ($i = 0; $i -lt 20 -and [W]::Focus($ob) -ne $try; $i++) {
        [W]::Key($VK_TAB); Start-Sleep -Milliseconds 300
        $f = [W]::Focus($ob); $path += ([W]::Text($f) -replace '\s+', ' ')
    }
    $reached = ([W]::Focus($ob) -eq $try)
    if ($reached) { [W]::Key($VK_RETURN) }
    $consent = [bool](WaitFor { Consent } 5)
    $byClick = $false
    if (-not $consent) { ClickId $ob 2011; $byClick = [bool](WaitFor { Consent } 5) }
    # Depuis B2 voie b (20/10), le bouton ouvre le parcours dans Lecons > Initiation
    # (LaunchTutorial, src/OnboardingWindow.cs:1510) : la fenetre sombre AZERTYGlobal_Learning n'existe plus.
    $lesson = [bool](WaitFor { VisibleOf 'AZERTYGlobal_Lessons' } 5)
    $v = if ($front -and $reached -and $consent -and $lesson) { 'OK' } elseif ($byClick -and $lesson) { 'A VOIR' } else { 'ECHEC' }
    Verdict 'A3' $v ("bouton 2011 '$label' ; premier plan=$front ; Tab x$($path.Count) jusqu'au bouton=$reached (" + ($path -join ' > ') + ") ; Entree : accord=$consent" + $(if ($consent) { '' } else { " ; repli par clic (identifiant 2011) : accord=$byClick, clavier a juger en R11" }) + " ; lecon ouverte=$lesson. Remappage actif non juge (frappe simulee).")
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
        [void](Calm)
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

# Pause refaite le 28/09 (e7c7217) : fleches Windows (Up-down), sans nom MSAA propre ; les
# champs Heures et Minutes gardent le leur. Les anciens boutons Augmenter/Diminuer n'existent plus.
# Section des Parametres renommee par e3327c6 : Compatibilite des applications / App compatibility
# (Settings_SectionCompat, src/Localization/L.Settings.cs:49), ex-Apps suspendues.
$fr = @('^Heures$', '^Minutes$', 'Clavier virtuel', '^Recherche$', 'Compatibilit. des applications', 'Rechercher un caract')
$en = @('^Hours$', '^Minutes$', 'Virtual keyboard', '^Search$', 'App compatibility', 'Find a character')
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

# --- R37, R40, R57b (partie statique) : assertions sur menu-*.txt et msaa-parametres-*.txt --
try {
    if (-not $menuFr -or -not $menuEn) { throw 'releves menu-fr.txt ou menu-en.txt absents (voir A10)' }
    $learnFr = SubItems $menuFr '^Apprendre >$'; $learnEn = SubItems $menuEn '^Learn >$'
    $defi = @(@($menuFr) + @($menuEn) | Where-Object { $_ -match 'D.fi|[Cc]hallenge' })
    # Defi du jour reactive le 08/10 (1ec1173) : Lecons, Defi du jour, Revoir l'accueil.
    $okFr = ($null -ne $learnFr) -and $learnFr.Count -eq 3 -and $learnFr[0] -match '^Le.ons$' -and $learnFr[1] -match '^D.fi du jour$' -and $learnFr[2] -match '^Revoir l.accueil$'
    $okEn = ($null -ne $learnEn) -and $learnEn.Count -eq 3 -and $learnEn[0] -eq 'Lessons' -and $learnEn[1] -eq 'Daily challenge' -and $learnEn[2] -eq 'Replay the welcome tour'
    $ok = $okFr -and $okEn
    Verdict 'R37' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ('Apprendre FR : ' + ($learnFr -join ' | ') + ' ; Learn EN : ' + ($learnEn -join ' | ') + ' ; lignes citant le Defi : ' + $defi.Count + ' (Defi du jour reactive). Sous-menus Couches, Apprendre, A propos et aide : A10. Ouverture a la souris non jugee.')
} catch { Verdict 'R37' 'ECHEC' $_.Exception.Message }

try {
    $notes = @(); $shown = 0; $hidden = 0; $missing = @()
    foreach ($lang in 'fr', 'en') {
        $f = Join-Path $out ('msaa-parametres-' + $lang + '.txt')
        $rows = if (Test-Path $f) { @(Get-Content $f -Encoding UTF8) } else { @() }
        if ($rows.Count -lt 5) { $missing += $lang; continue }
        foreach ($row in $rows) {
            $c = $row -split "`t"
            if ($c.Count -ge 4 -and $c[2] -match 'Rappels d.entra|training reminders') { if ($c[3] -eq 'True') { $shown++ } else { $hidden++ } }
        }
    }
    # Defi du jour reactive (1ec1173) : la case est attendue visible, en FR et en EN.
    $v = if ($missing.Count) { 'ECHEC' } elseif ($shown -ge 1 -and $hidden -eq 0) { 'OK (partiel)' } else { 'ECHEC' }
    Verdict 'R40' $v ("case Rappels d'entrainement visible : $shown, presente mais masquee : $hidden (FR et EN)" + $(if ($missing) { ' ; releve absent : ' + ($missing -join ', ') } else { '' }) + '. Absence de trou : image parametres-general du banc.')
} catch { Verdict 'R40' 'ECHEC' $_.Exception.Message }

try {
    if (-not $menuFr -or -not $menuEn) { throw 'releves menu absents (voir A10)' }
    $tomorrow = (Get-Date).Hour -ge 8
    $pauseFr = SubItems $menuFr '^Mettre en pause >$'; $pauseEn = SubItems $menuEn '^Pause >$'
    $wantFr = @('^15 minutes$', '^30 minutes$', '^1 heure$', '^2 heures$', $(if ($tomorrow) { '^Jusqu\S+ demain 8 h$' } else { '^Jusqu\S+ 8 h$' }), '^Personnaliser')
    $wantEn = @('^15 minutes$', '^30 minutes$', '^1 hour$', '^2 hours$', $(if ($tomorrow) { '^Until tomorrow 8:00 AM$' } else { '^Until 8:00 AM$' }), '^Custom')
    $r57bStaticOk = (Fits $pauseFr $wantFr) -and (Fits $pauseEn $wantEn)
    $r57bStatic = 'FR : ' + ($pauseFr -join ' | ') + ' ; EN : ' + ($pauseEn -join ' | ')
} catch { $r57bStaticOk = $false; $r57bStatic = $_.Exception.Message }

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

# --- R57b : sous-menu Pause pendant une pause, puis application desactivee -----------------
try {
    Ensure; CloseOthers
    Command 1044; Start-Sleep -Seconds 1
    $paused = DumpMenuRows
    $resume = @($paused | Where-Object { $_.depth -eq 0 -and -not $_.sub -and $_.text -match '^Reprendre maintenant \(reprise auto |^Resume now \(auto-resume ' })
    $noSub = @($paused | Where-Object { $_.depth -eq 0 -and $_.sub -and $_.text -match '^Mettre en pause$|^Pause$' }).Count -eq 0
    Command 1027; Start-Sleep -Seconds 1
    $back = DumpMenuRows
    $again = @($back | Where-Object { $_.depth -eq 0 -and $_.sub -and -not $_.grayed -and $_.text -match '^Mettre en pause$|^Pause$' }).Count -eq 1
    Command 1001; Start-Sleep -Seconds 1
    $off = DumpMenuRows
    $gray = @($off | Where-Object { $_.depth -eq 0 -and $_.sub -and $_.grayed -and $_.text -match '^Mettre en pause$|^Pause$' }).Count -eq 1
    Command 1001; Start-Sleep -Seconds 1
    CloseOthers
    $ok = $r57bStaticOk -and $resume.Count -eq 1 -and $noSub -and $again -and $gray
    Verdict 'R57b' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ('sous-menu ' + $r57bStatic + ' ; apres 15 minutes : ' + $(if ($resume) { $resume[0].text } else { 'aucun Reprendre maintenant' }) + ", sous-menu retire=$noSub ; apres reprise : sous-menu revenu=$again ; application desactivee : sous-menu grise=$gray. Bulle En pause pour ... non jugee.")
} catch { Verdict 'R57b' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- R57 : Pause > Personnaliser... au clavier et a la molette ------------------------------
try {
    Ensure; CloseOthers
    Command 1027
    $pd = WaitFor { VisibleOf 'AZERTYGlobal_PauseDuration' } 6
    if (-not $pd) { throw 'fenetre Personnaliser absente' }
    $hEd = Child $pd 4301; $mEd = Child $pd 4302; $okBtn = Child $pd 1
    $spins = @([W]::Children($pd) | Where-Object { [W]::Cls($_) -eq 'msctls_updown32' -and [W]::IsWindowVisible($_) })
    $right = 0
    foreach ($s in $spins) {
        $sr = [W2]::RectOf($s)
        foreach ($e in @($hEd, $mEd)) { $er = [W2]::RectOf($e); if ([Math]::Abs($sr[0] - $er[2]) -le 4 -and $sr[1] -lt $er[3] -and $sr[3] -gt $er[1]) { $right++ } }
    }
    $render = [W2]::Render($pd)
    $front = [W]::Front($pd); Start-Sleep -Milliseconds 300
    $focus0 = FocusId $pd
    function ResumeLine { return (@(StaticTexts $pd | Where-Object { $_ -match '^Reprise |^Resumes |^Choisissez|^Choose ' }) | Select-Object -First 1) }
    $m0 = [int][W2]::Text($mEd); $line0 = ResumeLine
    [W]::Key($VK_UP); Start-Sleep -Milliseconds 400; $mUp = [int][W2]::Text($mEd); $line1 = ResumeLine
    [W]::Key($VK_DOWN); Start-Sleep -Milliseconds 400; $mDown = [int][W2]::Text($mEd)
    [void][W]::PostMessage($pd, $WM_MOUSEWHEEL, [IntPtr](120 -shl 16), [IntPtr]::Zero); Start-Sleep -Milliseconds 400; $mWheel = [int][W2]::Text($mEd)
    [void][W]::PostMessage($pd, $WM_MOUSEWHEEL, [IntPtr]((-120) -shl 16), [IntPtr]::Zero); Start-Sleep -Milliseconds 400; $mWheel2 = [int][W2]::Text($mEd)
    $path = @()
    for ($i = 0; $i -lt 6; $i++) { [W]::Key($VK_TAB); Start-Sleep -Milliseconds 250; $id = FocusId $pd; $path += $id; if ($id -eq 4302) { break } }
    [W2]::SetText($hEd, '0'); [W2]::SetText($mEd, '0'); Start-Sleep -Milliseconds 400
    $okEnabled = [W2]::IsWindowEnabled($okBtn); $line2 = ResumeLine
    [void][W]::Front($pd); [W]::Key($VK_RETURN); Start-Sleep -Milliseconds 600
    $stillOpen = [W]::IsWindowVisible($pd)
    [W]::Key($VK_ESCAPE)
    $escClosed = WaitGone $pd 4
    $arrowsOk = ($mUp -eq $m0 + 5) -and ($mDown -eq $m0) -and ($mWheel -eq $m0 + 5) -and ($mWheel2 -eq $m0)
    $tabOk = ($path -contains 4301) -and ($path -contains 1) -and ($path -contains 2) -and ($path[-1] -eq 4302)
    $ok = $spins.Count -eq 2 -and $right -eq 2 -and $render[1] -ge 200 -and $front -and $focus0 -eq 4302 -and $arrowsOk -and ($line1 -ne $line0) -and $tabOk -and -not $okEnabled -and ($line2 -match '^Choisissez|^Choose ') -and $stillOpen -and $escClosed
    Verdict 'R57' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ("fleches Windows : $($spins.Count), collees a droite d'un champ : $right ; fond : luminance $($render[1]) sur 255, $($render[0]) couleurs ; focus initial $focus0 (4302 = minutes) ; minutes $m0, Haut $mUp, Bas $mDown, molette $mWheel puis $mWheel2 ; ligne : '$line0' puis '$line1' ; Tab : " + ($path -join '>') + " ; 0 h 0 : bouton actif=$okEnabled, ligne '$line2', Entree laisse ouvert=$stillOpen ; Echap ferme=$escClosed. Couleur rouge de la ligne : image duree-de-pause-invalide du banc.")
} catch { Verdict 'R57' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- R45, R47, R49 : Parametres au clavier ------------------------------------------------------
function Sel([IntPtr]$strip) { return [int][W]::SendMessage($strip, $TCM_GETCURSEL, [IntPtr]::Zero, [IntPtr]::Zero) }
# Tab jusqu'a la bande d'onglets, puis Droite, Droite, Gauche, Gauche : les onglets 0,1,2,1,0
# s'affichent et le rectangle de la fenetre ne bouge pas.
function WalkTabs([IntPtr]$st, [IntPtr]$strip) {
    [void](FrontSure $st)
    $f = TabTo $st { param($h) $h -eq $strip } 30
    if ($f -eq [IntPtr]::Zero) { return @{ ok = $false; note = "bande d'onglets jamais atteinte au Tab" } }
    for ($k = 0; $k -lt 2 -and (Sel $strip) -ne 0; $k++) { [W]::Key($VK_LEFT); Start-Sleep -Milliseconds 400 }
    $r0 = [W2]::Rect($st); $sels = @(Sel $strip); $same = $true
    foreach ($vk in @($VK_RIGHT, $VK_RIGHT, $VK_LEFT, $VK_LEFT)) {
        [W]::Key($vk); Start-Sleep -Milliseconds 500
        $sels += (Sel $strip); if ([W2]::Rect($st) -ne $r0) { $same = $false }
    }
    $labels = [W2]::ChildNames($strip) -join ' | '
    return @{ ok = (($sels -join ',') -eq '0,1,2,1,0') -and $same; labels = $labels; note = ('onglets ' + ($sels -join '>') + ', rectangle ' + $r0 + " inchange=$same, libelles " + $labels) }
}
try {
    Ensure; CloseOthers
    $susp0 = Suspended; $calm = Calm; $susp1 = Suspended; [void](Calm)
    Say ("R45-R49 : suspendu avant=$susp0, premier plan neutre=$calm, suspendu apres=$susp1")
    Command 1012
    $st = WaitFor { VisibleOf 'AZERTYGlobal_Settings' } 8
    if (-not $st) { throw 'Parametres absents' }
    $strip = [W]::Children($st) | Where-Object { [W]::Cls($_) -eq 'SysTabControl32' } | Select-Object -First 1
    if (-not $strip) { throw "bande d'onglets SysTabControl32 absente" }
    $wFr = WalkTabs $st $strip
    Command 1033; Start-Sleep -Milliseconds 1500
    $wEn = WalkTabs $st $strip
    Command 1033; Start-Sleep -Milliseconds 1500
    # Une passe par langue, dans l'ordre ou l'app les montre (sa langue de depart, puis l'autre).
    $both = @($wFr.labels, $wEn.labels)
    # Quatre onglets depuis 061b099 (11/10) : Lecons ajoute en dernier.
    $labelsOk = (@($both | Where-Object { $_ -match '^G.n.ral \| Applications \| Langue \| Le.ons$' }).Count -eq 1) -and (@($both | Where-Object { $_ -match '^General \| Applications \| Language \| Lessons$' }).Count -eq 1)
    Verdict 'R47' $(if ($wFr.ok -and $wEn.ok -and $labelsOk) { 'OK' } else { 'ECHEC' }) ('FR : ' + $wFr.note + ' ; EN : ' + $wEn.note + '.')

    # R45 : Reinitialiser les raccourcis (3107) par Entree puis par Espace, reponse Non : config.json intact.
    [void](FrontSure $st)
    $cfgPath = DataFile 'config.json'
    $reset = Child $st 3107
    $f = TabTo $st { param($h) $h -eq $reset } 30
    $h0 = Hash $cfgPath
    if ($f -ne [IntPtr]::Zero) { [W]::Key($VK_RETURN) }
    $a1 = AnswerBox $VK_N 7
    $h1 = Hash $cfgPath
    [void](FrontSure $st); Start-Sleep -Milliseconds 300
    $back2 = ([W]::Focus($st) -eq $reset)
    $f2 = if ($back2) { $reset } else { TabTo $st { param($h) $h -eq $reset } 30 }
    if ($f2 -ne [IntPtr]::Zero) { [W]::Key($VK_SPACE) }
    $a2 = AnswerBox $VK_N 7
    $h2 = Hash $cfgPath
    $ok = ($f -ne [IntPtr]::Zero) -and ($f2 -ne [IntPtr]::Zero) -and $a1 -eq 'fermee au clavier' -and $a2 -eq 'fermee au clavier' -and $h0 -eq $h1 -and $h1 -eq $h2 -and $wFr.ok
    Verdict 'R45' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ("trois onglets au clavier (R47)=$($wFr.ok) ; Tab jusqu'a Reinitialiser les raccourcis=$($f -ne [IntPtr]::Zero) ; Entree : confirmation $a1 ; focus revenu seul sur le bouton=$back2, Espace : confirmation $a2 ; config.json inchange=$($h0 -eq $h1 -and $h1 -eq $h2). Focus visible sur chaque bouton : a l'oeil.")

    # R49 : touche reservee (T) dans le champ du clavier virtuel, puis Langue > Reinitialiser
    # clavier virtuel ; chaque changement d'onglet efface le message.
    $vk0 = CfgValue 'shortcutVirtualKeyboardVk'
    [void](FrontSure $st)
    $kb = Child $st 3101
    $fk = TabTo $st { param($h) $h -eq $kb } 30
    if ($fk -ne [IntPtr]::Zero) { [W]::Key($VK_T); Start-Sleep -Milliseconds 500 }
    # Refus reecrit le 07/10 (0fd3c1d, L.SettingsClarity.cs:44) : Ctrl + Maj + T rouvre l'onglet ferme...
    $refusal = @(StaticTexts $st | Where-Object { $_ -match 'rouvre l.onglet ferm|reopens a closed tab' }) | Select-Object -First 1
    $vk1 = CfgValue 'shortcutVirtualKeyboardVk'
    [void](TabTo $st { param($h) $h -eq $strip } 30)
    [W]::Key($VK_RIGHT); Start-Sleep -Milliseconds 400; [W]::Key($VK_RIGHT); Start-Sleep -Milliseconds 500
    $cleared1 = @(StaticTexts $st | Where-Object { $_ -match 'rouvre l.onglet ferm|reopens a closed tab' }).Count -eq 0
    $vkBtn = Child $st 3108
    $fb = TabTo $st { param($h) $h -eq $vkBtn } 30
    if ($fb -ne [IntPtr]::Zero) { [W]::Key($VK_RETURN); Start-Sleep -Milliseconds 500 }
    $confirm = @(StaticTexts $st | Where-Object { $_ -match 'clavier virtuel r.initialis|Virtual keyboard window reset' }) | Select-Object -First 1
    [void](TabTo $st { param($h) $h -eq $strip } 30)
    [W]::Key($VK_LEFT); Start-Sleep -Milliseconds 500
    $cleared2 = @(StaticTexts $st | Where-Object { $_ -match 'clavier virtuel r.initialis|Virtual keyboard window reset' }).Count -eq 0
    $ok = ($fk -ne [IntPtr]::Zero) -and $refusal -and ("$vk0" -eq "$vk1") -and $cleared1 -and ($fb -ne [IntPtr]::Zero) -and $confirm -and $cleared2
    Verdict 'R49' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ("T dans le champ du clavier virtuel : message '$refusal', raccourci inchange=$("$vk0" -eq "$vk1") ; onglet Langue : message efface=$cleared1 ; Reinitialiser clavier virtuel : '$confirm' ; onglet suivant : message efface=$cleared2. Rouge pour un refus : SettingsValidationColorTests et images parametres-*-message du banc.")
    CloseOthers
} catch { Verdict 'R45/R47/R49' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- R58 : Couches > Configurer... au clavier ; Entree et Echap enregistrent -----------------
try {
    Ensure; CloseOthers; [void](Calm)
    Command 1040
    $ly = WaitFor { VisibleOf 'AZERTYGlobal_MaintainableLayers' } 6
    if (-not $ly) { throw 'fenetre Couches absente' }
    $master = Child $ly 5201
    [void](FrontSure $ly); Start-Sleep -Milliseconds 300
    $init = FocusId $ly
    $c0 = Checked $master
    [W]::Key($VK_SPACE); Start-Sleep -Milliseconds 400
    $c1 = Checked $master
    $path = @()
    for ($i = 0; $i -lt 10; $i++) { [W]::Key($VK_TAB); Start-Sleep -Milliseconds 250; $id = FocusId $ly; $path += $id; if ($id -eq 5201) { break } }
    ShiftTab; $back = FocusId $ly
    [W]::Key($VK_RETURN)
    $box1 = AnswerBox $VK_RETURN 1
    $closed1 = WaitGone $ly 4
    [void](WaitFor { if ([bool](CfgValue 'maintainableLayersEnabled') -eq $c1) { $true } } 8)
    $saved1 = [bool](CfgValue 'maintainableLayersEnabled')
    Command 1040
    [void](WaitFor { if ([W]::IsWindowVisible($ly)) { $ly } } 6)
    [void](FrontSure $ly); Start-Sleep -Milliseconds 300
    $init2 = FocusId $ly
    [W]::Key($VK_SPACE); Start-Sleep -Milliseconds 400
    $c2 = Checked $master
    [W]::Key($VK_ESCAPE)
    $box2 = AnswerBox $VK_RETURN 1
    $closed2 = WaitGone $ly 4
    [void](WaitFor { if ([bool](CfgValue 'maintainableLayersEnabled') -eq $c2) { $true } } 8)
    $saved2 = [bool](CfgValue 'maintainableLayersEnabled')
    $inner = @(5202, 5203, 5204, 5205, 5206 | Where-Object { $path -notcontains $_ }).Count -eq 0
    $cycleOk = ($path.Count -ge 2) -and ($path[-1] -eq 5201) -and ($path -contains 5208) -and (-not $c1 -or $inner)
    $ok = ($init -eq 5201) -and ($c1 -ne $c0) -and $cycleOk -and ($back -eq 5208) -and $closed1 -and ($saved1 -eq $c1) -and ($init2 -eq 5201) -and ($c2 -eq $c0) -and $closed2 -and ($saved2 -eq $c2)
    Verdict 'R58' $(if ($ok) { 'OK' } else { 'ECHEC' }) ("focus initial $init (5201 = case principale) ; Espace : case $c0 -> $c1 ; Tab : " + ($path -join '>') + " ; Maj+Tab : $back (5208 = Fermer) ; Entree : fermee=$closed1, boite $box1, enregistre=$saved1 ; rouverte, focus $init2, Espace -> $c2, Echap : fermee=$closed2, boite $box2, enregistre=$saved2.")
} catch { Verdict 'R58' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- R60 : ouvrir, fermer, rouvrir deux fois chaque fenetre --------------------------------
try {
    Ensure; CloseOthers
    $targets = @(@{ id = 1016; n = 'A propos' }, @{ id = 1032; n = 'Mes statistiques' }, @{ id = 1012; n = 'Parametres' }, @{ id = 1010; n = 'Accueil' }, @{ id = 1040; n = 'Couches' }, @{ id = 1027; n = 'Pause' })
    $bad = @(); $notes = @()
    foreach ($t in $targets) {
        $cells = @()
        for ($k = 1; $k -le 3; $k++) {
            $known = KnownWindows
            Command $t.id
            $h = WaitFor { NewWindow $known } 8
            if (-not $h) { $bad += ($t.n + ' #' + $k + ' absente'); $cells += 'absente'; continue }
            Start-Sleep -Milliseconds 600
            $resp = [W2]::Responds($h); $colors = ([W2]::Render($h))[0]; $kids = @([W]::Children($h)).Count
            if (-not $resp -or $colors -lt 2) { $bad += ($t.n + ' #' + $k + " : repond=$resp, couleurs=$colors") }
            $cells += "$colors couleurs/$kids enfants"
            [void][W]::PostMessage($h, $WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
            if (-not (WaitGone $h 5)) { $bad += ($t.n + ' #' + $k + ' non fermee'); CloseOthers }
            Start-Sleep -Milliseconds 300
        }
        $notes += ($t.n + ' ' + ($cells -join ', '))
    }
    $reg = LogCount 'RegisterClass'
    $ok = $bad.Count -eq 0 -and $reg -eq 0
    Verdict 'R60' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ('trois ouvertures : ' + ($notes -join ' ; ') + ' ; defauts : ' + $(if ($bad) { $bad -join ', ' } else { 'aucun' }) + " ; RegisterClass dans error.log : $reg. Conflit non ouvrable dans le Sandbox (pas de disposition systeme).")
} catch { Verdict 'R60' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- R20, R22 a R26 : derniere etape de l'accueil et demarrage automatique -------------------
# Premier accueil rejoue a chaque cas : accord, choix du demarrage et statistiques effaces,
# puis Activer et essayer (accord), tutoriel ferme, Suivant jusqu'a la derniere etape. La case
# a ete calculee a l'affichage (AutoStart.DefaultOnboardingCheck), avant l'accord : c'est le cas
# a juger. Depuis cadab2c (08/10) l'accueil a 4 etapes : Suivant est clique jusqu'a ce que la
# case du demarrage (2006, visible a la seule derniere etape) apparaisse, 5 fois au plus.
function ToLastStep([IntPtr]$ob) {
    for ($i = 0; $i -lt 5 -and -not [W]::IsWindowVisible((Child $ob 2006)); $i++) { ClickId $ob 2002 }
}
function ToStep3([IntPtr]$ob) {
    ClickId $ob 2011
    $lm = WaitFor { VisibleOf 'AZERTYGlobal_Lessons' } 6
    if ($lm) { [void][W]::PostMessage($lm, $WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero); [void](WaitGone $lm 5) }
    ToLastStep $ob
    return [bool]$lm
}
function FreshOnboarding {
    [void](Quit)
    Cle 'activationConsent' $null; Cle 'autoStartEnabled' $null; Cle 'autoStartNudgeDone' $null
    $u = DataFile 'usage-stats.json'; if ($u) { Remove-Item $u -Force }
    [void](Launch)
    $ob = WaitFor { VisibleOf 'AZERTYGlobal_Onboarding' } 10
    if (-not $ob) { throw 'accueil absent au lancement' }
    $chk = Child $ob 2006
    $shown = Checked $chk
    $lm = ToStep3 $ob
    # Cases papier par identifiant : 2001 ne plus afficher, 2006 demarrage, 2013 rappels du Defi.
    $boxes = @(2001, 2006, 2013 | Where-Object { $c = Child $ob $_; $c -ne [IntPtr]::Zero -and [W]::IsWindowVisible($c) }).Count
    return @{ ob = $ob; chk = $chk; shown = $shown; step3 = [W]::IsWindowVisible($chk); boxes = $boxes; lm = $lm; consent = (Consent); now = (Checked $chk); next = [W2]::Text((Child $ob 2002)) }
}
function OffVerdict([bool]$gesture, $s) {
    if (-not $gesture) { return 'ECHEC' }
    if (-not (Known $s)) { return 'A VOIR' }
    if (IsOn $s) { return 'ECHEC' }
    return 'OK'
}
$r22 = $null
try {
    $s0 = StartupState
    Say ('R20-R26 : etat de depart ' + $s0.text)
    $a = FreshOnboarding
    $ok = $a.shown -and $a.step3 -and $a.now -and $a.boxes -eq 3 -and $a.consent
    Verdict 'R20' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ("premier accueil : case cochee a l'affichage=$($a.shown), derniere etape atteinte=$($a.step3), toujours cochee=$($a.now), cases visibles=$($a.boxes) (3 attendues : demarrage, ne plus afficher, rappels du Defi), accord par Activer et essayer=$($a.consent), tutoriel ouvert puis ferme=$($a.lm). Sans vide : image accueil-etape4 du banc.")
    [void][W]::Front($a.ob)
    $g = TabTo $a.ob { param($h) [W]::Text($h) -match 'Guide de prise en main|Getting started guide' } 20
    if ($g -ne [IntPtr]::Zero) { [W]::Key($VK_ESCAPE) }
    $closed = WaitGone $a.ob 5; Start-Sleep -Seconds 1
    $s = StartupState
    $r22 = @{ ok = (OffVerdict ($closed -and $g -ne [IntPtr]::Zero) $s); text = $s.text }
    Verdict 'R22' $r22.ok ("case laissee cochee ; Tab jusqu'au lien Guide=$($g -ne [IntPtr]::Zero), Echap : accueil ferme=$closed ; " + $s.text)
} catch { Verdict 'R20/R22' 'ECHEC' $_.Exception.Message; CloseOthers }

try {
    $b = FreshOnboarding
    [void][W]::PostMessage($b.ob, $WM_SYSCOMMAND, [IntPtr]$SC_CLOSE, [IntPtr]::Zero)
    $closedX = WaitGone $b.ob 5; Start-Sleep -Seconds 1
    $sX = StartupState
    $c = FreshOnboarding
    $fg = [W]::Front($c.ob); Start-Sleep -Milliseconds 300
    # Alt+F4 ne part que si l'accueil est au premier plan : sinon il fermerait la console.
    $altF4 = $fg -and ([W]::GetForegroundWindow() -eq $c.ob)
    if ($altF4) { [W2]::Chord($VK_MENU, $VK_F4) }
    $closedF4 = WaitGone $c.ob 5; Start-Sleep -Seconds 1
    $sF4 = StartupState
    if (-not $altF4) { CloseOthers }
    $vX = OffVerdict $closedX $sX; $vF4 = OffVerdict ($altF4 -and $closedF4) $sF4
    $vEsc = if ($r22) { $r22.ok } else { 'ECHEC' }
    $all = @($vX, $vF4, $vEsc)
    $v = if ($all -contains 'ECHEC') { 'ECHEC' } elseif ($all -contains 'A VOIR') { 'A VOIR' } else { 'OK' }
    Verdict 'R23' $v ("case laissee cochee ; croix (SC_CLOSE) : fermee=$closedX, " + $sX.text + " ; Echap : voir R22 ($vEsc) ; Alt+F4 au premier plan=$altF4 : fermee=$closedF4, " + $sF4.text)
} catch { Verdict 'R23' 'ECHEC' $_.Exception.Message; CloseOthers }

$regOn = '?'; $regOff = '?'
try {
    $d = FreshOnboarding
    $label = $d.next
    ClickId $d.ob 2002
    $closedGo = WaitGone $d.ob 6; Start-Sleep -Seconds 2
    $sOn = StartupState; $regOn = $sOn.reg
    $v = if (-not ($closedGo -and $label -match 'parti|Let.s go')) { 'ECHEC' } elseif (-not (Known $sOn)) { 'A VOIR' } elseif (IsOn $sOn) { 'OK' } else { 'ECHEC' }
    Verdict 'R24' $v ("bouton final '$label' : accueil ferme=$closedGo ; " + $sOn.text)
} catch { Verdict 'R24' 'ECHEC' $_.Exception.Message; CloseOthers }

try {
    Ensure; CloseOthers
    Command 1010
    $ob = WaitFor { VisibleOf 'AZERTYGlobal_Onboarding' } 6
    if (-not $ob) { throw 'accueil non rouvert' }
    [void](ToStep3 $ob)
    $chk = Child $ob 2006
    $shownOn = Checked $chk
    # Case papier : BM_SETCHECK n'a plus d'effet, le clic (BN_CLICKED) la bascule (ToggleCheckBox).
    if ($shownOn) { ClickId $ob 2006 }
    $nowOff = -not (Checked $chk)
    ClickId $ob 2002
    $closedOff = WaitGone $ob 6; Start-Sleep -Seconds 2
    $sOff = StartupState; $regOff = $sOff.reg
    $nudge = CfgValue 'autoStartNudgeDone'
    [void](Quit); [void](Launch); Start-Sleep -Seconds 20
    $sOff2 = StartupState
    $nudge2 = CfgValue 'autoStartNudgeDone'
    $gesture = $shownOn -and $nowOff -and $closedOff -and ($nudge -eq $true) -and ($nudge2 -eq $true)
    $v1 = OffVerdict $gesture $sOff; $v2 = OffVerdict $gesture $sOff2
    $v = if (@($v1, $v2) -contains 'ECHEC') { 'ECHEC' } elseif (@($v1, $v2) -contains 'A VOIR') { 'A VOIR' } else { 'OK' }
    Verdict 'R25' $v ("rouvert : case cochee (etat reel, MSAA)=$shownOn, decochee par clic=$nowOff, validee : ferme=$closedOff, " + $sOff.text + " ; autoStartNudgeDone=$nudge ; relance, 20 s : " + $sOff2.text + ", autoStartNudgeDone=$nudge2 (plus de relance).")
    CloseOthers
} catch { Verdict 'R25' 'ECHEC' $_.Exception.Message; CloseOthers }

try {
    [void](Quit)
    $regBefore = StartupRegistry
    if (-not (Test-Path $startupKey)) { New-Item -Path $startupKey -Force | Out-Null }
    Set-ItemProperty -Path $startupKey -Name State -Value 1 -Type DWord
    $sUser = StartupState
    $e = FreshOnboarding
    [void][W]::PostMessage($e.ob, $WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero); [void](WaitGone $e.ob 5)
    $sAfter = StartupState
    $v = if ($sUser.probe -ne 'DisabledByUser') { 'A VOIR' } elseif (-not $e.shown -and -not $e.now -and -not (IsOn $sAfter)) { 'OK' } else { 'ECHEC' }
    Verdict 'R26' $v ("State observe : active=$regOn, desactive=$regOff, avant ecriture=$regBefore ; State=1 ecrit, lu par la tache : $($sUser.probe) ; premier accueil : case cochee a l'affichage=$($e.shown), a l'etape 3=$($e.now) ; apres fermeture : " + $sAfter.text)
    CloseOthers
    # Retour a l'etat desactive observe en R25, pour que la suite ne herite pas d'un refus.
    if ($regOff -match '^\d+$') { Set-ItemProperty -Path $startupKey -Name State -Value ([int]$regOff) -Type DWord }
} catch { Verdict 'R26' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- R39 : Defi reactive, opt-in coche et annonce non faite -----------------------------------
# Depuis 1ec1173 (08/10) : avec l'opt-in, l'annonce ne part pas (ShouldShowChallengeAnnouncement)
# et Mes statistiques montre la section Defi (UsageStatsWindow.cs:28). La section est peinte en
# GDI (DrawChallengeSection), sans controle enfant : on la juge a la hauteur client, 460 + 130
# (BASE_WIN_H + CHALLENGE_SECTION_H) a l'echelle de la largeur (BASE_WIN_W = 460), avec un
# temoin negatif sans opt-in (460). Seance terminee effacee pour que le temoin tienne (usage-stats.json).
function StatsSize {
    CloseOthers
    Command 1032
    $sw = WaitFor { VisibleOf 'AZERTYGlobal_UsageStats' } 6
    $size = if ($sw) { [W2]::ClientSize($sw) } else { @(0, 0) }
    CloseOthers
    return , $size
}
try {
    [void](Quit)
    $u = DataFile 'usage-stats.json'; if ($u) { Remove-Item $u -Force }
    Cle 'trainingEnabled' $false; Cle 'showOnboardingAtStartup' $false
    [void](Launch); Start-Sleep -Seconds 3
    $off = StatsSize
    [void](Quit)
    Cle 'trainingEnabled' $true; Cle 'challengeAnnounceDone' $null; Cle 'showOnboardingAtStartup' $false
    [void](Launch); Start-Sleep -Seconds 15
    $announced = $null -ne (CfgValue 'challengeAnnounceDone')
    $obShown = [bool](VisibleOf 'AZERTYGlobal_Onboarding')
    $on = StatsSize
    $scale = if ($on[0] -gt 0) { $on[0] / 460.0 } else { 0 }
    $withSection = ($scale -gt 0) -and ([Math]::Abs($on[1] - 590 * $scale) -le 3)
    $withoutSection = ($off[0] -gt 0) -and ([Math]::Abs($off[1] - 460 * ($off[0] / 460.0)) -le 3)
    $ok = -not $announced -and -not $obShown -and $withSection -and $withoutSection
    Verdict 'R39' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ("trainingEnabled=true, challengeAnnounceDone efface, accueil au demarrage coupe : annonce marquee apres 15 s=$announced, accueil affiche=$obShown ; Mes statistiques, zone client : sans opt-in $($off[0]) x $($off[1]) (460 attendu, section absente=$withoutSection), avec opt-in $($on[0]) x $($on[1]) ($([Math]::Round(590 * $scale)) attendu, section presente=$withSection). Contenu de la section : a l'oeil (R116-R117). Module Defi des Lecons : DefiDuJourMasqueTests.Le_module_Defi_entre_dans_les_Lecons_avec_l_opt_in.")
} catch { Verdict 'R39' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- R41, R70 a R73 : avis pilotes par config.json et usage-stats.json -----------------------
# Accueil ferme par son bouton final : Suivant jusqu'a la derniere etape (ToLastStep), puis
# Activer (accord).
# Clic par identifiant (IDC_BTN_NEXT = 2002) ; libelle releve a l'etape 3 sans accord :
# Onboarding_Activate, Activer AZERTY Global / Turn on AZERTY Global (L.Onboarding.cs:10).
# learningMaxStepCompleted = 1 montre Suivant des l'etape 1 sans ouvrir le tutoriel, dont la
# fermeture interdirait l'avis dix minutes (ReviewPromptGate.LearningCooldownMinutes).
function AcceptAtStep3([IntPtr]$ob) {
    ToLastStep $ob
    $label = [W2]::Text((Child $ob 2002))
    ClickId $ob 2002
    $closed = WaitGone $ob 6
    return @{ label = $label; closed = $closed; consent = (Consent) }
}
try {
    [void](Quit)
    Cle 'reviewPromptCount' $null; Cle 'reviewPromptLastShown' $null; Cle 'reviewPromptClicked' $false
    Usage 1
    [void](Launch); Start-Sleep -Seconds 20
    $n1 = ReviewCount
    $ob = VisibleOf 'AZERTYGlobal_Onboarding'
    if ($ob) { [void][W]::PostMessage($ob, $WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero); Start-Sleep -Seconds 15 }
    $n2 = ReviewCount
    Verdict 'R41' $(if ($n1 -eq 0 -and $n2 -eq 0) { 'OK' } else { 'ECHEC' }) ("Usage 1 (20 caracteres enrichis, 10 min), relance : reviewPromptCount apres 20 s = $n1 ; accueil ferme=$([bool]$ob), 15 s apres : $n2.")
    CloseOthers
} catch { Verdict 'R41' 'ECHEC' $_.Exception.Message; CloseOthers }

try {
    [void](Quit)
    Cle 'activationConsent' $null; Cle 'currentVersionFirstRunDate' $null; Cle 'reviewPromptCount' 1; Cle 'reviewPromptLastShown' $j3; Cle 'reviewPromptClicked' $false
    Cle 'cleRecette' 42; Cle 'shortcutVirtualKeyboardVk' 120; Cle 'learningMaxStepCompleted' 1; Cle 'showOnboardingAtStartup' $true
    Usage 12
    [void](Launch)
    $ob = WaitFor { VisibleOf 'AZERTYGlobal_Onboarding' } 10
    if (-not $ob) { throw 'accueil absent : accord non redemande' }
    $acc = AcceptAtStep3 $ob
    Start-Sleep -Seconds 20
    $n = ReviewCount; $ls = CfgValue 'reviewPromptLastShown'; $cr = CfgValue 'cleRecette'; $vk = CfgValue 'shortcutVirtualKeyboardVk'
    $ok71 = $acc.consent -and $n -eq 1 -and "$ls" -eq $j3 -and "$cr" -eq '42' -and "$vk" -eq '120'
    Verdict 'R71' $(if ($ok71) { 'OK (partiel)' } else { 'ECHEC' }) ("apres l'accord et 20 s : reviewPromptCount=$n, reviewPromptLastShown=$ls ($j3 attendu), cleRecette=$cr, shortcutVirtualKeyboardVk=$vk. Branche apres frappe : Essai2_six_jours_apres_le_premier_refuse.")
    [void](Quit); [void](Launch); Start-Sleep -Seconds 3
    $kept = Consent; $cr2 = CfgValue 'cleRecette'; $vk2 = CfgValue 'shortcutVirtualKeyboardVk'
    $ok70 = ($acc.label -match '^Activer |^Turn on ') -and $acc.closed -and $acc.consent -and $kept -and "$cr2" -eq '42' -and "$vk2" -eq '120'
    Verdict 'R70' $(if ($ok70) { 'OK' } else { 'ECHEC' }) ("config 1.2 simulee : accueil ouvert au lancement, bouton '$($acc.label)', ferme=$($acc.closed), accord=$($acc.consent) ; relance : accord garde=$kept, cleRecette=$cr2, raccourci=$vk2.")
    CloseOthers
} catch { Verdict 'R70/R71' 'ECHEC' $_.Exception.Message; CloseOthers }

try {
    [void](Quit)
    Cle 'activationConsent' $null; Cle 'reviewPromptLastShown' $j8
    $err0 = LogCount 'StoreReview|MaybeShowReviewPrompt|ToastActivation'
    [void](Launch)
    $ob = WaitFor { VisibleOf 'AZERTYGlobal_Onboarding' } 10
    if (-not $ob) { throw 'accueil absent' }
    $acc = AcceptAtStep3 $ob
    $n = WaitFor { if ((ReviewCount) -eq 2) { 2 } } 20
    $ls = CfgValue 'reviewPromptLastShown'
    $err1 = LogCount 'StoreReview|MaybeShowReviewPrompt|ToastActivation'
    $ok = $acc.consent -and $n -eq 2 -and "$ls" -eq $today -and $err1 -eq $err0
    Verdict 'R72' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ("reviewPromptLastShown a J-8, accueil ferme par Activer : reviewPromptCount=$(ReviewCount) (2 attendu), reviewPromptLastShown=$ls, erreurs d'avis dans error.log : $err0 -> $err1. Affichage du toast non observe.")
    CloseOthers
} catch { Verdict 'R72' 'ECHEC' $_.Exception.Message; CloseOthers }

try {
    [void](Quit)
    Cle 'activationConsent' $null; Cle 'reviewPromptCount' 0; Cle 'reviewPromptLastShown' $null; Cle 'reviewPromptClicked' $false
    [void](Launch)
    $ob = WaitFor { VisibleOf 'AZERTYGlobal_Onboarding' } 10
    if (-not $ob) { throw 'accueil absent' }
    $acc = AcceptAtStep3 $ob
    Start-Sleep -Seconds 20
    $n = ReviewCount
    Verdict 'R73' $(if ($acc.consent -and $n -eq 0) { 'OK' } else { 'ECHEC' }) ("compteur 0, seuils de l'essai 1 atteints (Usage 12) : accueil ferme par Activer, accord=$($acc.consent), reviewPromptCount apres 20 s = $n.")
    CloseOthers
} catch { Verdict 'R73' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- R74 : lessons-progress.json illisible ------------------------------------------------------
try {
    [void](Quit)
    $dir = DataDir
    $lp = Join-Path $dir 'lessons-progress.json'
    $asideBefore = @(Get-ChildItem -Path $dir -Filter 'lessons-progress.json.illisible-*' -File -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
    [IO.File]::WriteAllText($lp, '{ invalid')
    [void](Launch); Start-Sleep -Seconds 2
    CloseOthers
    Command 1023
    $lw = WaitFor { VisibleOf 'AZERTYGlobal_Lessons' } 8
    Start-Sleep -Seconds 2
    $aside = @(Get-ChildItem -Path $dir -Filter 'lessons-progress.json.illisible-*' -File -ErrorAction SilentlyContinue | Where-Object { $asideBefore -notcontains $_.FullName })
    $asideOk = ($aside.Count -eq 1) -and ((Get-Content $aside[0].FullName -Raw) -eq '{ invalid')
    $alive = [bool](App)
    Verdict 'R74' $(if ($lw -and $asideOk -and $alive) { 'OK (partiel)' } else { 'ECHEC' }) ("Lecons ouvertes=$([bool]$lw), app vivante=$alive ; copie mise de cote=" + $(if ($aside.Count -eq 1) { $aside[0].Name } else { "$($aside.Count) fichier(s)" }) + ", contenu intact=$asideOk. Bulle unique : QuarantaineTests (progression corrompue, message une seule fois).")
    CloseOthers
} catch { Verdict 'R74' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- A11 : config.json illisible ([1]) au demarrage ------------------------------------
try {
    [void](Quit)
    $cfg = DataFile 'config.json'
    if (-not $cfg) { throw ('config.json introuvable dans ' + ($dataDirs -join ' ; ')) }
    $logFile = Join-Path (Split-Path -Parent $cfg) 'error.log'
    $before = if (Test-Path $logFile) { @(Select-String -Path $logFile -Pattern 'JsonException').Count } else { 0 }
    $cfgDir = Split-Path -Parent $cfg
    $asideBefore = @(Get-ChildItem -Path $cfgDir -Filter 'config.json.illisible-*' -File -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
    Set-Content -Path $cfg -Value '[1]' -NoNewline -Encoding ASCII
    $p = Launch
    Start-Sleep -Seconds 5
    $alive = [bool](App)
    $after = if (Test-Path $logFile) { @(Select-String -Path $logFile -Pattern 'JsonException').Count } else { 0 }
    # Depuis le lot 4 (1.3.0), un config.json corrompu est renomme a cote en
    # config.json.illisible-<aaaaMMjj-HHmmss> (src/FileQuarantine.cs), jamais laisse en place.
    $aside = @(Get-ChildItem -Path $cfgDir -Filter 'config.json.illisible-*' -File -ErrorAction SilentlyContinue | Where-Object { $asideBefore -notcontains $_.FullName })
    $asideOk = ($aside.Count -eq 1) -and ((Get-Content $aside[0].FullName -Raw) -eq '[1]')
    $current = if (Test-Path $cfg) { Get-Content $cfg -Raw } else { $null }
    $ok = $p -and $alive -and ($after -gt $before) -and $asideOk -and ($current -ne '[1]')
    Verdict 'A11' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ("config $cfg ; demarrage=$([bool]$p), vivante 5 s apres=$alive ; JsonException dans error.log : $before -> $after ; copie mise de cote=" + $(if ($aside.Count -eq 1) { $aside[0].Name } else { "$($aside.Count) fichier(s)" }) + " avec [1]=$asideOk ; config.json n'est plus [1]=$($current -ne '[1]'). Accueil, app inactive jusqu'a l'accord et relance active : R75 ci-dessous.")
    if (Test-Path $logFile) { Copy-Item $logFile (Join-Path $out 'error.log') -Force }
} catch { Verdict 'A11' 'ECHEC' $_.Exception.Message }

# --- R75 : suite d'A11 : accueil, app inactive jusqu'a l'accord, accord, relance active ---
# Accord par clic par identifiant (IDC_BTN_TRY = 2011), comme R20 : le chemin clavier (Tab,
# Entree) a echoue les 28 et 29/09 sur le refus de premier plan, et le libelle suit la langue.
# Le clavier de l'accueil est juge par A3 (et R11 a la main).
try {
    $ob = WaitFor { VisibleOf 'AZERTYGlobal_Onboarding' } 6
    $consent0 = Consent
    $rows0 = DumpMenuRows
    $menu0 = @($rows0 | Where-Object { $_.depth -eq 0 -and -not $_.separator }) | Select-Object -First 1
    $inactive = $menu0 -and $menu0.text -match '^Activer\t|^Turn on\t'
    $consent1 = $false; $lesson = $false; $label = 'accueil absent'
    if ($ob) {
        $try = Child $ob 2011
        if ($try -ne [IntPtr]::Zero) {
            $label = [W]::Text($try)
            ClickId $ob 2011
            $consent1 = [bool](WaitFor { Consent } 5)
            $lesson = [bool](WaitFor { VisibleOf 'AZERTYGlobal_Learning' } 5)
        } else { $label = 'bouton 2011 introuvable' }
    }
    CloseOthers
    [void](Quit); [void](Launch); Start-Sleep -Seconds 3
    $consent2 = Consent
    $rows2 = DumpMenuRows
    $menu2 = @($rows2 | Where-Object { $_.depth -eq 0 -and -not $_.separator }) | Select-Object -First 1
    $active = $menu2 -and $menu2.text -match '^D.sactiver\t|^Turn off\t'
    $ok = [bool]$ob -and -not $consent0 -and $inactive -and $consent1 -and $consent2 -and $active
    Verdict 'R75' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ("apres [1] : accueil ouvert=$([bool]$ob), accord=$consent0, menu " + $(if ($menu0) { "'" + ($menu0.text -replace "`t", ' ') + "'" } else { 'non releve' }) + " (inactive=$inactive) ; accord par clic (identifiant 2011, '$label') : accord=$consent1, tutoriel=$lesson ; relance : accord=$consent2, menu " + $(if ($menu2) { "'" + ($menu2.text -replace "`t", ' ') + "'" } else { 'non releve' }) + " (active=$active). Bulle unique : QuarantaineTests (config corrompue, message une seule fois).")
    CloseOthers
} catch { Verdict 'R75' 'ECHEC' $_.Exception.Message; CloseOthers }

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
# R04 : le Sandbox se ferme seul ; l'hote attend done.txt (lancer-recette.ps1 -Auto).
if ($Fermer) { Start-Sleep -Seconds 3; shutdown.exe /s /t 0 }
