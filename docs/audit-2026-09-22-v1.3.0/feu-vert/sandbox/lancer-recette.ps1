# Ouvre la recette du candidat 1.3.0 dans Windows Sandbox : le poste hote reste intact
# (ni certificat, ni paquet, ni hook). Pas besoin d'etre administrateur.
#   powershell -ExecutionPolicy Bypass -File lancer-recette.ps1 -Bundle <chemin du .msixbundle>
# Journal d'installation : ..\evidence\recette-<12 premiers caracteres du SHA-256>\.
# Option -AxeDir <dossier d'AxeWindowsCLI.exe> : enchaine l'analyse d'accessibilite
# automatique de chaque fenetre (axe-scan.ps1), resultats dans ...\axe\.
# Option -Auto : enchaine ensuite la recette automatique (recette-auto.ps1), resultats dans
# ...\auto\ (un dossier auto\ d'un passage precedent est renomme auto-<date>, rien n'est
# efface). Elle finit par desinstaller l'app : relancer sans -Auto pour la recette a la main.
# Avec -Auto, le Sandbox s'eteint seul une fois done.txt ecrit (R04), sauf avec -Garder ; ce
# script attend done.txt au plus -Minutes minutes, puis juge resultats.json par
# lire-resultats.ps1 (R06, R07) et rend son code de retour (0 : tout au niveau attendu).
param([Parameter(Mandatory = $true)][string]$Bundle, [string]$AxeDir, [switch]$Auto, [switch]$Garder, [int]$Minutes = 45)
$ErrorActionPreference = 'Stop'

$bundle = (Resolve-Path $Bundle).Path
$kit = Split-Path -Parent $MyInvocation.MyCommand.Path
$sdk = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64'
if (-not (Test-Path (Join-Path $sdk 'signtool.exe'))) { throw "signtool introuvable dans $sdk" }

$hash = (Get-FileHash $bundle -Algorithm SHA256).Hash.ToLower()
$short = $hash.Substring(0, 12)
$results = Join-Path (Split-Path -Parent $kit) ('evidence\recette-' + $short)
New-Item -ItemType Directory -Force $results | Out-Null
$results = (Resolve-Path $results).Path

# Seul le bundle est expose au Sandbox, en lecture seule.
$candidate = Join-Path $env:TEMP ('azg-candidat-' + $short)
New-Item -ItemType Directory -Force $candidate | Out-Null
Copy-Item $bundle (Join-Path $candidate 'candidat.msixbundle') -Force

$axeFolder = ''
$steps = @('installer-candidat.ps1')
if ($AxeDir) {
    $axePath = (Resolve-Path $AxeDir).Path
    if (-not (Test-Path (Join-Path $axePath 'AxeWindowsCLI.exe'))) { throw "AxeWindowsCLI.exe introuvable dans $axePath" }
    $axeFolder = "<MappedFolder><HostFolder>$axePath</HostFolder><SandboxFolder>C:\axe</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>"
    $steps += 'axe-scan.ps1'
}
# Un seul Windows Sandbox a la fois : un second lancement echouerait sans le dire ici.
if (Get-Process -Name 'WindowsSandbox', 'WindowsSandboxClient', 'WindowsSandboxRemoteSession' -ErrorAction SilentlyContinue) {
    throw 'Un Windows Sandbox est deja ouvert : le fermer, puis relancer.'
}
$autoDir = Join-Path $results 'auto'
if ($Auto) {
    $steps += $(if ($Garder) { 'recette-auto.ps1' } else { 'recette-auto.ps1 -Fermer' })
    if (Test-Path $autoDir) { Rename-Item $autoDir ('auto-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
}
$steps = @($steps | ForEach-Object { 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\kit\' + $_ })
$command = if ($steps.Count -eq 1) { $steps[0] } else { 'cmd.exe /c "' + ($steps -join ' &amp; ') + '"' }
$wsb = @"
<Configuration>
  <MappedFolders>
    <MappedFolder><HostFolder>$candidate</HostFolder><SandboxFolder>C:\candidat</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$sdk</HostFolder><SandboxFolder>C:\sdk</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$kit</HostFolder><SandboxFolder>C:\kit</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$results</HostFolder><SandboxFolder>C:\resultats</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
    $axeFolder
  </MappedFolders>
  <LogonCommand><Command>$command</Command></LogonCommand>
</Configuration>
"@
$wsbPath = Join-Path $candidate 'recette.wsb'
Set-Content -Path $wsbPath -Value $wsb -Encoding UTF8
"Candidat SHA-256 : $hash"
"Journal : $results\installation.txt"
$launched = Get-Date
Start-Process $wsbPath
if ($Auto) {
    $done = Join-Path $autoDir 'done.txt'
    $end = (Get-Date).AddMinutes($Minutes)
    "Attente de $done (au plus $Minutes min)"
    while (-not (Test-Path $done) -and (Get-Date) -lt $end) { Start-Sleep -Seconds 15 }
    if (-not (Test-Path $done)) { "done.txt absent apres $Minutes min : voir $autoDir\journal.txt"; exit 2 }
    # R04 : la VM s'eteint seule (shutdown en fin de recette-auto.ps1), mais la fenetre du
    # client Windows Sandbox peut rester ouverte (constate le 28/09) : fermer celle-ci.
    if (-not $Garder) {
        Start-Sleep -Seconds 20
        foreach ($c in @(Get-Process -Name 'WindowsSandbox', 'WindowsSandboxClient', 'WindowsSandboxRemoteSession' -ErrorAction SilentlyContinue | Where-Object { $_.StartTime -ge $launched })) {
            [void]$c.CloseMainWindow(); Start-Sleep -Seconds 5
            if (-not $c.HasExited) { Stop-Process -Id $c.Id -Force }
        }
    }
    & (Join-Path $kit 'lire-resultats.ps1') -Dossier $autoDir
    exit $LASTEXITCODE
}
