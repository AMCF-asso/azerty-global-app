# Poste hote, dans un PowerShell ouvert a la racine du depot. Joue R01, R02, R03 et R83 de
# docs/recette-2026-09-27-v1.3.0/RECETTE-1.3.0.md sur le bundle d'un run CI :
#   powershell -ExecutionPolicy Bypass -File verifier-candidat.ps1 -Run <id> [-Sha <empreinte de l'en-tete>] [-Commit <sha>]
# R01 telecharge l'artefact msixbundle (sauf s'il est deja dans -Dest) : un seul fichier.
# R02 compare son SHA-256 a -Sha (l'empreinte reportee dans l'en-tete de la fiche) et a la
#     ligne Get-FileHash du bundle, etape SHA256 artefacts du job build : celle dont le chemin
#     finit par AZERTYGlobal-1.3.0.0.msixbundle ; Format-Table tronque ce chemin (azerty-g...),
#     elle est alors celle dont l'empreinte est attestee par le meme job (Attestation created
#     for AZERTYGlobal-1.3.0.0.msixbundle@sha256:...). Jamais par sa position.
# R03 lance gh attestation verify ; le commit source lu dans la sortie JSON doit etre celui
#     du run (et -Commit s'il est donne).
# R83 cherche dans le journal du job build les lignes Verify Release (ci.yml:116-117 ;
#     SHA256 x64 et arm64 a publish = bundle, Release verifiee 1.3.0 / 1.3.0.0) et exige le
#     job build en success. Le job provenance echoue tant que le site n'a pas bascule
#     (attendu) : sa conclusion et celle du run sont notees, pas exigees.
# Journal sans noms d'etapes (UNKNOWN STEP, run 37214922137 par exemple) : les motifs sont
# cherches dans tout le journal du job build, et l'observation le dit.
# Sorties : evidence\recette-<12>\hote\resultats.json et journal.txt, et le chemin
# du bundle a passer a lancer-recette.ps1. Code de retour 0 si les quatre lignes sont OK.
# -GhUser <compte> : jeton de ce compte gh pour ce seul processus (GH_TOKEN) ; le compte
# actif de gh ne change pas.
param(
    [Parameter(Mandatory = $true)][string]$Run,
    [string]$Sha,
    [string]$Commit,
    [string]$Repo = 'AMCF-asso/azerty-global-app',
    [string]$Dest,
    [string]$GhUser,
    [string]$BuildJob = 'build'
)
# Continue : gh ecrit sur stderr ses messages ordinaires, ce qui ferait echouer
# Windows PowerShell 5.1 en mode Stop. Les codes de retour sont lus un a un.
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$kit = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if ($GhUser) { $env:GH_TOKEN = (& gh auth token --user $GhUser) }
if (-not $Dest) { $Dest = Join-Path 'msix' ('ci-' + $Run) }
$bundleName = 'AZERTYGlobal-1.3.0.0.msixbundle'

$results = New-Object System.Collections.ArrayList
$journal = New-Object System.Collections.ArrayList
function Verdict([string]$id, [string]$verdict, [string]$note) {
    [void]$results.Add([ordered]@{ ligne = $id; verdict = $verdict; observation = $note })
    $line = '{0} : {1} - {2}' -f $id, $verdict, $note
    [void]$journal.Add($line); Write-Host $line
}

# --- R01 : telechargement, un seul fichier -------------------------------------------------
$bundle = $null
$existing = @(Get-ChildItem -Path $Dest -File -ErrorAction SilentlyContinue)
if ($existing.Count -eq 0) {
    & gh run download $Run -R $Repo -n msixbundle -D $Dest 2>$null
    $how = "telecharge (gh run download, code $LASTEXITCODE)"
} else { $how = 'deja present, non retelecharge' }
$files = @(Get-ChildItem -Path $Dest -File -ErrorAction SilentlyContinue)
$okR01 = ($files.Count -eq 1) -and ($files[0].Name -eq $bundleName)
if ($files.Count -ge 1) { $bundle = $files[0].FullName }
Verdict 'R01' $(if ($okR01) { 'OK' } else { 'ECHEC' }) ("$Dest : $how ; fichiers : " + $(if ($files) { ($files | ForEach-Object { $_.Name }) -join ', ' } else { 'aucun' }))

# --- Journal et commit du run ---------------------------------------------------------------
$info = $null
$infoText = & gh run view $Run -R $Repo --json headSha,conclusion,status,headBranch,jobs 2>$null
if ($LASTEXITCODE -eq 0) { $info = ($infoText -join "`n") | ConvertFrom-Json }
$log = @(& gh run view $Run -R $Repo --log 2>$null)
$logOk = ($LASTEXITCODE -eq 0) -and ($log.Count -gt 0)
# gh run view --log : job<TAB>etape<TAB>horodatage ligne. Seul le job build est lu.
$buildLog = @($log | Where-Object { $_.StartsWith($BuildJob + "`t") })
$unnamed = @($buildLog | Where-Object { $_.StartsWith($BuildJob + "`tUNKNOWN STEP`t") }).Count
# Lignes d'une etape du job build ; a defaut, tout le job, perimetre note dans l'observation.
function StepLines([string]$step) {
    $lines = @($buildLog | Where-Object { $_.StartsWith($BuildJob + "`t" + $step + "`t") })
    if ($lines.Count) { return @{ lines = $lines; scope = "etape $step" } }
    $why = if ($unnamed) { "noms d'etapes absents (UNKNOWN STEP)" } else { "etape $step introuvable" }
    return @{ lines = $buildLog; scope = "tout le job $BuildJob, $why" }
}
function JobConclusion([string]$name) {
    if (-not $info) { return '?' }
    $j = @($info.jobs | Where-Object { $_.name -eq $name }) | Select-Object -First 1
    if (-not $j) { return 'absent' }
    if ($j.conclusion) { return [string]$j.conclusion }
    return [string]$j.status
}

# --- R02 : empreinte ------------------------------------------------------------------------
$hash = if ($bundle) { (Get-FileHash $bundle -Algorithm SHA256).Hash.ToUpper() } else { '' }
$shaStep = StepLines 'SHA256 artefacts'
$rows = @($shaStep.lines | ForEach-Object {
    $m = [regex]::Match($_, 'SHA256\s+([0-9A-Fa-f]{64})\s+(\S.*?)\s*$')
    if ($m.Success) { New-Object PSObject -Property @{ hash = $m.Groups[1].Value.ToUpper(); path = $m.Groups[2].Value } }
})
$attPattern = 'Attestation created for ' + [regex]::Escape($bundleName) + '@sha256:([0-9A-Fa-f]{64})'
$attested = @($buildLog | ForEach-Object { $m = [regex]::Match($_, $attPattern); if ($m.Success) { $m.Groups[1].Value.ToUpper() } } | Select-Object -Unique)
$ciRow = $null; $which = ''
$byPath = @($rows | Where-Object { $_.path -match ([regex]::Escape($bundleName) + '$') })
if ($byPath.Count -eq 1) { $ciRow = $byPath[0]; $which = 'chemin du bundle' }
elseif ($byPath.Count -eq 0 -and $attested.Count -eq 1) {
    $byAtt = @($rows | Where-Object { $_.hash -eq $attested[0] })
    if ($byAtt.Count -eq 1) { $ciRow = $byAtt[0]; $which = "chemin tronque, empreinte attestee pour $bundleName" }
}
$ci = if ($ciRow) { $ciRow.hash } else { '' }
$vsCi = [bool]$ci -and ($hash -eq $ci)
$vsHeader = if ($Sha) { $hash -eq $Sha.Trim().ToUpper() } else { $null }
$v = if (-not $vsCi -or $vsHeader -eq $false) { 'ECHEC' } elseif ($null -eq $vsHeader) { 'OK (partiel)' } else { 'OK' }
$found = if ($ci) { "ligne Get-FileHash du bundle ($which ; $($shaStep.scope)) $ci (identique=$vsCi" + $(if ($vsCi) { ')' } else { ', empreinte differente)' }) } else { "ligne du bundle non identifiee ($($shaStep.scope) : $($rows.Count) ligne(s) Get-FileHash, $($byPath.Count) par le chemin, $($attested.Count) attestation(s) de $bundleName)" }
Verdict 'R02' $v ("SHA-256 du fichier $hash ; $found ; en-tete de la fiche : " + $(if ($Sha) { "$Sha (identique=$vsHeader)" } else { 'non donnee (-Sha), a reporter puis a comparer' }))

# --- R03 : attestation ----------------------------------------------------------------------
$attOut = if ($bundle) { @(& gh attestation verify $bundle -R $Repo --format json 2>$null) } else { @() }
$attCode = $LASTEXITCODE
$digest = ''; $ref = ''
try {
    $att = ($attOut -join "`n") | ConvertFrom-Json
    $cert = @($att)[0].verificationResult.signature.certificate
    $digest = [string]$cert.sourceRepositoryDigest; $ref = [string]$cert.sourceRepositoryRef
} catch { }
$head = if ($info) { [string]$info.headSha } else { '' }
$okR03 = ($attCode -eq 0) -and $digest -and ($digest -eq $head) -and (-not $Commit -or $digest.StartsWith($Commit.Trim().ToLower()))
Verdict 'R03' $(if ($okR03) { 'OK' } else { 'ECHEC' }) ("gh attestation verify : code $attCode ; commit source $digest ($ref) ; commit du run $head" + $(if ($Commit) { " ; attendu $Commit" } else { '' }))

# --- R83 : Verify Release dans le journal du job build, job build en success ----------------
$vrStep = StepLines 'Verify Release'
$vr = @($vrStep.lines)
$x64 = @($vr | Where-Object { $_ -match 'SHA256 x64 : [0-9A-F]{64} \(publish = bundle\)' }).Count
$arm = @($vr | Where-Object { $_ -match 'SHA256 arm64 : [0-9A-F]{64} \(publish = bundle\)' }).Count
$rel = @($vr | Where-Object { $_ -match 'Release v\S+: version 1\.3\.0 / package 1\.3\.0\.0' }).Count
$concl = if ($info) { [string]$info.conclusion } else { '?' }
$build = JobConclusion $BuildJob; $prov = JobConclusion 'provenance'; $lint = JobConclusion 'actionlint'
$provNote = if ($prov -eq 'failure') { 'provenance : failure, attendu avant la bascule du site' } else { "provenance : $prov" }
$okR83 = $logOk -and $x64 -ge 1 -and $arm -ge 1 -and $rel -ge 1 -and $build -eq 'success'
Verdict 'R83' $(if ($okR83) { 'OK' } else { 'ECHEC' }) ("journal lu=$logOk ($($log.Count) lignes, $($buildLog.Count) du job $BuildJob ; $($vrStep.scope)) ; SHA256 x64 publish = bundle : $x64 ; arm64 : $arm ; Release verifiee 1.3.0 / 1.3.0.0 : $rel ; job $($BuildJob) : $build ; $provNote ; actionlint : $lint ; conclusion du run : $concl ; branche " + $(if ($info) { $info.headBranch } else { '?' }))

# --- Sorties --------------------------------------------------------------------------------
if ($hash) {
    $dir = Join-Path $kit ('evidence\recette-' + $hash.Substring(0, 12).ToLower() + '\hote')
    New-Item -ItemType Directory -Force $dir | Out-Null
    $results | ConvertTo-Json -Depth 3 | Set-Content -Path (Join-Path $dir 'resultats.json') -Encoding UTF8
    @("Run $Run ($Repo), commit $head, $(Get-Date -Format 'yyyy-MM-dd HH:mm')") + $journal | Set-Content -Path (Join-Path $dir 'journal.txt') -Encoding UTF8
    "Releve : $dir"
}
if ($bundle) { "Bundle : $bundle" }
$bad = @($results | Where-Object { $_.verdict -eq 'ECHEC' }).Count
if ($bad) { "Verdict : ECHEC ($bad ligne(s))"; exit 1 }
$partial = @($results | Where-Object { $_.verdict -ne 'OK' }).Count
if ($partial) { 'Verdict : OK (partiel) : reporter l''empreinte dans l''en-tete, puis relancer avec -Sha'; exit 1 }
'Verdict : OK'
exit 0
