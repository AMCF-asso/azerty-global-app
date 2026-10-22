# Execute dans Windows Sandbox apres installer-candidat.ps1 (lancer-recette.ps1 -P1). Joue sans
# humain la part machine des lignes P1 de docs/recette-2026-09-27-v1.3.0/RECETTE-1.3.0.md
# (section 3) : R10, R11, R116 a R122, R142, sur une app sans accord au depart. La frappe
# remappee (AG130-07), les fenetres peintes sans controle (Lecons, Recherche, section Defi de
# Mes statistiques) et les bulles restent a l'oeil : l'observation de chaque ligne le dit.
# Verdicts comme recette-auto.ps1 : OK, OK (partiel), A VOIR, ECHEC.
# Resultats : C:\resultats\p1\ (journal.txt, resultats.json, releves msaa-lecons-*.txt).
# -Fermer : eteint le Sandbox une fois done.txt ecrit.
param([switch]$Fermer)
$ErrorActionPreference = 'Continue'
$out = 'C:\resultats\p1'
. (Join-Path $PSScriptRoot 'recette-outils.ps1')

$WM_CHAR = 0x0102; $EM_GETSEL = 0x00B0; $BM_CLICK = 0x00F5
function Lang { if (@(DumpMenuRows | Where-Object { $_.depth -eq 0 -and $_.text -match '^Quitter' }).Count) { return 'fr' }; return 'en' }
function SetLang([string]$want) { if ((Lang) -ne $want) { Command 1033; Start-Sleep -Milliseconds 1500 } }
function TypeChars([IntPtr]$h, [string]$s) { foreach ($c in $s.ToCharArray()) { [void][W]::PostMessage($h, $WM_CHAR, [IntPtr][int]$c, [IntPtr]::Zero); Start-Sleep -Milliseconds 120 } }
function AllTexts([IntPtr]$win) { return @([W]::Children($win) | ForEach-Object { [W2]::Text($_) }) }
# Une ligne par HWND : classe, role MSAA, nom MSAA, visible (meme format que recette-auto.ps1).
function MsaaNames([IntPtr]$hwnd) {
    $rows = @('Window' + "`t" + [W]::Msaa($hwnd) + "`t" + [W]::IsWindowVisible($hwnd))
    foreach ($c in [W]::Children($hwnd)) { $rows += ([W]::Cls($c) + "`t" + [W]::Msaa($c) + "`t" + [W]::IsWindowVisible($c)) }
    return , $rows
}

# --- R10 : Tab et Maj+Tab dans l'accueil, drapeau au clavier et au clic ------------------------
# Premier lancement : l'accueil est ouvert, sans accord. Aucun lien fantome = chaque controle qui
# prend le focus au Tab est visible. Langue lue sur le bouton Suivant (2002).
try {
    $ob = WaitFor { VisibleOf 'AZERTYGlobal_Onboarding' } 10
    if (-not $ob) { [void](Launch); $ob = WaitFor { VisibleOf 'AZERTYGlobal_Onboarding' } 10 }
    if (-not $ob) { throw 'accueil absent au premier lancement' }
    [void](FrontSure $ob)
    $seen = @(); $ghost = @()
    for ($i = 0; $i -lt 16; $i++) {
        [W]::Key($VK_TAB); Start-Sleep -Milliseconds 250
        $f = [W]::Focus($ob); if ($f -eq [IntPtr]::Zero) { continue }
        $id = [W2]::GetDlgCtrlID($f); $seen += $id
        if (-not [W]::IsWindowVisible($f)) { $ghost += $id }
    }
    for ($i = 0; $i -lt 8; $i++) {
        ShiftTab
        $f = [W]::Focus($ob); if ($f -eq [IntPtr]::Zero) { continue }
        $id = [W2]::GetDlgCtrlID($f); $seen += $id
        if (-not [W]::IsWindowVisible($f)) { $ghost += $id }
    }
    $flag = Child $ob 2014
    $next = Child $ob 2002
    $fl = TabTo $ob { param($h) $h -eq $flag } 20
    $l0 = [W2]::Text($next)
    $langs = @($l0); $stay = @()
    if ($fl -ne [IntPtr]::Zero) {
        [W]::Key($VK_RETURN); Start-Sleep -Milliseconds 900; $langs += [W2]::Text($next); $stay += ([W]::Focus($ob) -eq $flag)
        [W]::Key($VK_SPACE); Start-Sleep -Milliseconds 900; $langs += [W2]::Text($next); $stay += ([W]::Focus($ob) -eq $flag)
    }
    [void][W]::SendMessage($flag, $BM_CLICK, [IntPtr]::Zero, [IntPtr]::Zero); Start-Sleep -Milliseconds 900
    $langs += [W2]::Text($next); $stay += ([W]::Focus($ob) -eq $flag)
    $flips = 0; for ($k = 1; $k -lt $langs.Count; $k++) { if ($langs[$k] -ne $langs[$k - 1]) { $flips++ } }
    $ok = ($ghost.Count -eq 0) -and ($fl -ne [IntPtr]::Zero) -and ($flips -eq 3) -and (@($stay | Where-Object { -not $_ }).Count -eq 0)
    Verdict 'R10' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ("Tab x16 puis Maj+Tab x8 : focus " + (($seen | Select-Object -Unique) -join ',') + ", invisibles : " + $(if ($ghost) { ($ghost | Select-Object -Unique) -join ',' } else { 'aucun' }) + " ; drapeau (2014) atteint au Tab=$($fl -ne [IntPtr]::Zero) ; Suivant lu : " + ($langs -join ' > ') + " (Entree, Espace, clic : $flips bascules sur 3) ; focus reste sur le drapeau : " + ($stay -join ',') + ". Cadre de focus : a l'oeil.")
} catch { Verdict 'R10' 'ECHEC' $_.Exception.Message }

# --- R11 : etape 1, Tab jusqu'a Activer et essayer, Entree -> Lecons ---------------------------
# Meme geste que A3 de recette-auto.ps1 ; ici on releve aussi les noms MSAA de la fenetre Lecons
# (ecran des usages) pour savoir ce qu'un script pourrait y lire (R102).
try {
    $ob = WaitFor { VisibleOf 'AZERTYGlobal_Onboarding' } 5
    if (-not $ob) { throw 'accueil ferme avant R11' }
    $try = Child $ob 2011
    [void](FrontSure $ob)
    $t = TabTo $ob { param($h) $h -eq $try } 20
    if ($t -ne [IntPtr]::Zero) { [W]::Key($VK_RETURN) }
    $consent = [bool](WaitFor { Consent } 5)
    if (-not $consent) { ClickId $ob 2011; [void](WaitFor { Consent } 5) }
    $ls = WaitFor { VisibleOf 'AZERTYGlobal_Lessons' } 6
    $names = if ($ls) { MsaaNames $ls } else { @('Lecons absentes') }
    $kids = if ($ls) { [W2]::ChildNames($ls) } else { @() }
    Set-Content -Path (Join-Path $out ('msaa-lecons-' + (Lang) + '.txt')) -Value (@($names) + @('--- enfants MSAA ---') + @($kids)) -Encoding UTF8
    $usages = @($kids | Where-Object { $_ -match 'Vous .crivez surtout|You mostly write|mails|Du code|code' }).Count
    $ok = ($t -ne [IntPtr]::Zero) -and $consent -and $ls
    Verdict 'R11' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ("Tab jusqu'a 2011=$($t -ne [IntPtr]::Zero) ; Entree : accord=$consent ; Lecons ouvertes=$([bool]$ls) ; enfants MSAA des Lecons : $($kids.Count), citant les usages : $usages (releve msaa-lecons-*.txt). Ecran des usages, icone bleue, infobulle et bulle Actif : a l'oeil.")
    CloseOthers
} catch { Verdict 'R11' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- R121 : desactiver -> Activer en tete du menu, sans ligne grisee ; reactiver -> Desactiver ---
# La ligne grisee du motif n'existe que pendant une suspension de compatibilite
# (ShouldShowCompatibilitySuspension, src/TrayApplication.cs:1693 ; 761817e) : apres une
# desactivation a la main, elle doit etre absente. Elle se juge a la main, dans un jeu ou en acces distant.
try {
    Ensure; CloseOthers
    $on = DumpMenuRows
    Command 1001; Start-Sleep -Seconds 1
    $off = DumpMenuRows
    Command 1001; Start-Sleep -Seconds 1
    $back = DumpMenuRows
    $top = @($off | Where-Object { $_.depth -eq 0 -and -not $_.separator }) | Select-Object -First 1
    $topOn = @($on | Where-Object { $_.depth -eq 0 -and -not $_.separator }) | Select-Object -First 1
    $topBack = @($back | Where-Object { $_.depth -eq 0 -and -not $_.separator }) | Select-Object -First 1
    # Ligne du motif = ligne grisee placee avant Activer/Desactiver. Plus bas, Afficher le clavier
    # virtuel et Rechercher un caractere sont grises par l'application desactivee : attendu.
    $rootOff = @($off | Where-Object { $_.depth -eq 0 })
    $iToggle = 0; while ($iToggle -lt $rootOff.Count -and $rootOff[$iToggle].text -notmatch '^Activer|^Turn on|^D.sactiver|^Turn off') { $iToggle++ }
    $grayOff = @($rootOff | Select-Object -First $iToggle | Where-Object { $_.grayed -and -not $_.separator }).Count
    $toggled = ($topOn.text -match '^D.sactiver|^Turn off') -and $top -and ($top.text -match '^Activer|^Turn on') -and -not $top.grayed
    $gone = $topBack -and ($topBack.text -eq $topOn.text) -and -not $topBack.grayed
    $ok = $toggled -and ($grayOff -eq 0) -and $gone
    Verdict 'R121' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ("tete du menu active : '$($topOn.text)' ; desactivee : '$($top.text)', lignes grisees de motif : $grayOff (0 attendu hors suspension) ; reactivee : '$($topBack.text)'. Ligne grisee du motif pendant une suspension, infobulle et bulle de pause : a la main.")
} catch { Verdict 'R121' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- R122 : Personnaliser... minutes selectionnees, 30 sans clic, Entree ; 75 refuse -----------
try {
    Ensure; CloseOthers
    Command 1027
    $pd = WaitFor { VisibleOf 'AZERTYGlobal_PauseDuration' } 6
    if (-not $pd) { throw 'fenetre Personnaliser absente' }
    $hEd = Child $pd 4301; $mEd = Child $pd 4302; $okBtn = Child $pd 1
    [void](FrontSure $pd)
    $focus0 = FocusId $pd
    $len = ([W2]::Text($mEd)).Length
    $sel = [W]::SendMessage($mEd, $EM_GETSEL, [IntPtr]::Zero, [IntPtr]::Zero).ToInt64()
    $selAll = (($sel -band 0xFFFF) -eq 0) -and ((($sel -shr 16) -band 0xFFFF) -eq $len) -and $len -gt 0
    $h0 = [int][W2]::Text($hEd)
    TypeChars $mEd '30'; Start-Sleep -Milliseconds 500
    $m1 = [W2]::Text($mEd)
    $line = (@(StaticTexts $pd | Where-Object { $_ -match '^Reprise |^Resumes ' }) | Select-Object -First 1)
    $want = (Get-Date).AddMinutes($h0 * 60 + 30)
    $times = @(-1, 0, 1 | ForEach-Object { $t = $want.AddMinutes($_); $t.ToString('%H') + ' h ' + $t.ToString('mm'); $t.ToString('H:mm'); $t.ToString('h:mm tt', [Globalization.CultureInfo]'en-US') })
    $timeOk = @($times | Where-Object { $line -and $line.Contains($_) }).Count -gt 0
    # 75 dans les minutes : refus borne 1 min - 23 h 59.
    [W2]::SetText($mEd, ''); TypeChars $mEd '75'; Start-Sleep -Milliseconds 500
    $refusal = (@(StaticTexts $pd | Where-Object { $_ -match '^Choisissez|^Choose ' }) | Select-Object -First 1)
    $okOff = -not [W2]::IsWindowEnabled($okBtn)
    [W2]::SetText($mEd, ''); TypeChars $mEd '30'; Start-Sleep -Milliseconds 400
    [void](FrontSure $pd); [W]::Key($VK_RETURN)
    $closed = WaitGone $pd 4
    $rows = DumpMenuRows
    $resume = (@($rows | Where-Object { $_.depth -eq 0 -and $_.text -match '^Reprendre maintenant|^Resume now' }) | Select-Object -First 1)
    if ($resume) { Command 1027; Start-Sleep -Seconds 1 }
    $ok = $selAll -and ($focus0 -eq 4302) -and ($m1 -eq '30') -and $timeOk -and $refusal -and $okOff -and $closed -and $resume
    Verdict 'R122' $(if ($ok) { 'OK' } else { 'ECHEC' }) ("focus initial $focus0 (4302 = minutes), minutes selectionnees=$selAll (sel $($sel -band 0xFFFF)-$(($sel -shr 16) -band 0xFFFF), longueur $len) ; heures $h0, 30 tape sans clic : minutes '$m1', ligne '$line' (attendu ~" + $want.ToString('HH:mm') + ", conforme=$timeOk) ; 75 : '$refusal', bouton grise=$okOff ; Entree : fermee=$closed, menu : '" + $(if ($resume) { $resume.text } else { 'aucun Reprendre' }) + "' (pause levee ensuite).")
} catch { Verdict 'R122' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- R142 : liens du menu vers le site, URL lue dans la ligne de commande d'Edge ----------------
function EdgeUrl([int]$id) {
    Get-Process msedge -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 1
    Command $id
    $u = WaitFor {
        foreach ($p in @(Get-CimInstance Win32_Process -Filter "Name='msedge.exe'" -ErrorAction SilentlyContinue)) {
            if ($p.CommandLine -match '(https?://[^\s"]*azerty[^\s"]*)') { return $Matches[1] }
        }
    } 15
    Get-Process msedge -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    if ($u) { return $u }; return 'aucune'
}
try {
    Ensure; CloseOthers
    $links = @(
        @{ n = 'Donner mon avis'; id = 1014; want = '/contact\?source=feedback' },
        @{ n = 'Signaler un bug'; id = 1009; want = '/contact\?source=bug' },
        @{ n = 'Aide-memoire imprimable'; id = 1025; want = '/assets/aide-memoire-azerty-global\.pdf' },
        @{ n = 'Cartes du clavier'; id = 1026; want = '/guide#aide-memoire' })
    $notes = @(); $bad = 0
    foreach ($l in $links) {
        $u = EdgeUrl $l.id
        $hit = $u -match $l.want
        if (-not $hit) { $bad++ }
        $notes += ($l.n + ' (' + $l.id + ') -> ' + $u + ' : ' + $(if ($hit) { 'conforme' } else { 'attendu ' + ($l.want -replace '\\', '') }))
    }
    Verdict 'R142' $(if ($bad -eq 0) { 'OK' } else { 'ECHEC' }) (($notes -join ' ; ') + '. URL jugee, pas la page (404 attendu avant la bascule).')
} catch { Verdict 'R142' 'ECHEC' $_.Exception.Message }

# --- R120 : clavier virtuel, titre de la fenetre ---------------------------------------------
try {
    Ensure; CloseOthers
    SetLang 'fr'
    $known = KnownWindows
    Command 1006
    $kb = WaitFor { NewWindow $known } 6
    $title = if ($kb) { [W]::Text($kb) } else { '' }
    $render = if ($kb) { [W2]::Render($kb) } else { @(0, 0) }
    $ok = $title -match '^AZERTY Global, Clavier virtuel$'
    Verdict 'R120' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ("fenetre : '$title' (classe " + $(if ($kb) { [W]::Cls($kb) } else { 'absente' }) + ") ; fond : luminance $($render[1]) sur 255, $($render[0]) couleurs. Un signe par touche, tirets, couche active sous AltGr : a l'oeil.")
    CloseOthers
} catch { Verdict 'R120' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- R118 (part machine) et R119 : Parametres en FR ---------------------------------------------
try {
    Ensure; CloseOthers
    SetLang 'fr'
    [void](Calm)
    Command 1012
    $st = WaitFor { VisibleOf 'AZERTYGlobal_Settings' } 8
    if (-not $st) { throw 'Parametres absents' }
    $strip = [W]::Children($st) | Where-Object { [W]::Cls($_) -eq 'SysTabControl32' } | Select-Object -First 1
    $labels = if ($strip) { [W2]::ChildNames($strip) -join ' | ' } else { '' }
    # "Version 1.3.0" est peinte a droite de la bande d'onglets (SettingsWindow.Paper.cs:248),
    # sans controle : a l'oeil.
    $compat = @(AllTexts $st | Where-Object { $_ -match 'Compatibilit. des applications' }).Count -gt 0
    Verdict 'R118' $(if ($labels -match '^G.n.ral \| Applications \| Langue \| Le.ons$' -and $compat) { 'OK (partiel)' } else { 'ECHEC' }) ("onglets FR : $labels ; section Compatibilite des applications presente=$compat ; onglets aux fleches FR et EN : R47 de la recette auto. Version 1.3.0 (peinte a droite des onglets), ajout de mstsc.exe, Mode pour et refus rouge : a la main.")

    $vk0 = CfgValue 'shortcutVirtualKeyboardVk'
    [void](FrontSure $st)
    $kbField = Child $st 3101
    $fk = TabTo $st { param($h) $h -eq $kbField } 30
    Start-Sleep -Milliseconds 400
    $prompt = (@(StaticTexts $st | Where-Object { $_ -match 'Appuyez sur une lettre' }) | Select-Object -First 1)
    if ($fk -ne [IntPtr]::Zero) { [W]::Key($VK_T) }
    $refusal = WaitFor { @(StaticTexts $st | Where-Object { $_ -match 'rouvre l.onglet ferm. dans les navigateurs\. Choisissez une autre touche\.' }) | Select-Object -First 1 } 3
    Say ('R119 : textes visibles apres T : ' + ((StaticTexts $st | Where-Object { $_ }) -join ' | '))
    $shown = [W2]::Text($kbField)
    [W]::Key($VK_ESCAPE); Start-Sleep -Milliseconds 600
    $stillOpen = [W]::IsWindowVisible($st)
    $vk1 = CfgValue 'shortcutVirtualKeyboardVk'
    $ok = ($fk -ne [IntPtr]::Zero) -and $prompt -and $refusal -and ("$vk0" -eq "$vk1")
    Verdict 'R119' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ("champ du clavier virtuel atteint=$($fk -ne [IntPtr]::Zero) ; invite : '$prompt' ; T : '$refusal', case montre '$shown' ; Echap : Parametres encore ouverts=$stillOpen, raccourci inchange=$("$vk0" -eq "$vk1"). Rouge : banc (parametres-*-message).")
    CloseOthers
} catch { Verdict 'R118/R119' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- R117 : case Rappels d'entrainement decochee par defaut, la cocher -> section Defi ----------
# Section peinte (DrawChallengeSection) : jugee a la hauteur client de Mes statistiques,
# 460 + 130 a l'echelle de la largeur (BASE_WIN_W = 460), comme R39 de recette-auto.ps1.
function StatsHeight {
    CloseOthers; Command 1032
    $sw = WaitFor { VisibleOf 'AZERTYGlobal_UsageStats' } 6
    $s = if ($sw) { [W2]::ClientSize($sw) } else { @(0, 0) }
    CloseOthers
    return , $s
}
try {
    [void](Quit)
    $u = DataFile 'usage-stats.json'; if ($u) { Remove-Item $u -Force }
    Cle 'trainingEnabled' $null; Cle 'showOnboardingAtStartup' $false
    [void](Launch); Start-Sleep -Seconds 3; CloseOthers
    $before = StatsHeight
    Command 1012
    $st = WaitFor { VisibleOf 'AZERTYGlobal_Settings' } 8
    if (-not $st) { throw 'Parametres absents' }
    $chk = Child $st 3118
    $vis = [W]::IsWindowVisible($chk)
    $c0 = Checked $chk
    [void][W]::SendMessage($chk, $BM_CLICK, [IntPtr]::Zero, [IntPtr]::Zero); Start-Sleep -Milliseconds 800
    $c1 = Checked $chk
    $saved = [bool](WaitFor { if ((CfgValue 'trainingEnabled') -eq $true) { $true } } 5)
    CloseOthers
    $known = KnownWindows
    Command 1035
    $ls = WaitFor { VisibleOf 'AZERTYGlobal_Lessons' } 6
    CloseOthers
    $after = StatsHeight
    $sc = if ($after[0] -gt 0) { $after[0] / 460.0 } else { 0 }
    $section = ($sc -gt 0) -and ([Math]::Abs($after[1] - 590 * $sc) -le 3)
    $noSection = ($before[0] -gt 0) -and ([Math]::Abs($before[1] - 460 * ($before[0] / 460.0)) -le 3)
    $ok = $vis -and -not $c0 -and $c1 -and $saved -and $ls -and $section -and $noSection
    Verdict 'R117' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ("case 3118 visible=$vis, cochee par defaut=$c0 ; clic : cochee=$c1, trainingEnabled ecrit=$saved ; Apprendre > Defi du jour (1035) : Lecons ouvertes=$([bool]$ls) ; Mes statistiques, zone client : avant $($before[0]) x $($before[1]) (section absente=$noSection), apres $($after[0]) x $($after[1]) (section presente=$section). Defi affiche dans les Lecons : a l'oeil.")
} catch { Verdict 'R117' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- R116 : annonce unique du Defi (notifications actives, pas d'opt-in), Defi du jour sans opt-in --
try {
    [void](Quit)
    Cle 'trainingEnabled' $false; Cle 'challengeAnnounceDone' $null; Cle 'showOnboardingAtStartup' $false; Cle 'notificationsEnabled' $null
    [void](Launch); Start-Sleep -Seconds 10
    $marked1 = (CfgValue 'challengeAnnounceDone') -eq $true
    $ob1 = [bool](VisibleOf 'AZERTYGlobal_Onboarding')
    [void](Quit); [void](Launch); Start-Sleep -Seconds 10
    $marked2 = (CfgValue 'challengeAnnounceDone') -eq $true
    CloseOthers
    Command 1035
    $ls = WaitFor { VisibleOf 'AZERTYGlobal_Lessons' } 6
    CloseOthers
    $menu = DumpMenuRows
    $learn = @($menu | Where-Object { $_.depth -eq 1 -and $_.text -match '^Le.ons$|^Lessons$|^D.fi du jour$|^Daily challenge$|^Revoir l.accueil$|^Replay the welcome tour$' }).Count
    $ok = $marked1 -and -not $ob1 -and $marked2 -and $ls -and ($learn -eq 3)
    Verdict 'R116' $(if ($ok) { 'OK (partiel)' } else { 'ECHEC' }) ("sans opt-in, notifications par defaut, accueil coupe : annonce marquee au 1er lancement=$marked1 (accueil affiche=$ob1), toujours marquee apres relance=$marked2 (une seule fois : ShouldShowChallengeAnnouncement) ; Defi du jour sans opt-in : Lecons ouvertes=$([bool]$ls) ; entrees d'Apprendre dans la langue courante : $learn sur 3 (FR et EN : R37 de la recette auto). Bulle visible et page Aujourd'hui : a l'oeil.")
} catch { Verdict 'R116' 'ECHEC' $_.Exception.Message; CloseOthers }

# --- Fin ---------------------------------------------------------------------------------------
[void](Quit)
$results | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $out 'resultats.json') -Encoding UTF8
$counts = $results | Group-Object { $_.verdict } | ForEach-Object { $_.Name + ' ' + $_.Count }
Say ('Fin P1 : ' + ($counts -join ', '))
'fin' | Set-Content -Path (Join-Path $out 'done.txt')
if ($Fermer) { Start-Sleep -Seconds 3; shutdown.exe /s /t 0 }
