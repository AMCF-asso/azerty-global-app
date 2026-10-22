# Smoke-test du bundle hors Store signe AMCF dans Windows Sandbox, machine propre : aucun
# certificat importe, aucun AZERTY Global installe (action smoke-test-msix-amcf-propre).
#   powershell -ExecutionPolicy Bypass -File lancer-smoke-amcf.ps1 -Bundle <AZERTY_Global_x.y.z.msixbundle> -Sha256 <attendu>
# Le SHA-256 attendu est celui de la fiche de go, pas celui recalcule ici : le script refuse
# de lancer le Sandbox si le fichier n'est pas celui-la.
# Journal : ..\evidence\smoke-amcf-<12 premiers caracteres du SHA-256>\smoke.txt, verdict en derniere ligne.
param([Parameter(Mandatory = $true)][string]$Bundle, [Parameter(Mandatory = $true)][string]$Sha256)
$ErrorActionPreference = 'Stop'

$bundle = (Resolve-Path $Bundle).Path
$hash = (Get-FileHash $bundle -Algorithm SHA256).Hash.ToLower()
if ($hash -ne $Sha256.ToLower()) { throw "SHA-256 du bundle : $hash, attendu $Sha256. Smoke-test non lance." }
$kit = Split-Path -Parent $MyInvocation.MyCommand.Path
$short = $hash.Substring(0, 12)
$results = Join-Path (Split-Path -Parent $kit) ('evidence\smoke-amcf-' + $short)
if (Test-Path (Join-Path $results 'smoke.txt')) { Rename-Item $results ('smoke-amcf-' + $short + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
New-Item -ItemType Directory -Force $results | Out-Null
$results = (Resolve-Path $results).Path

# Seul le bundle et son SHA attendu sont exposes au Sandbox, en lecture seule.
$candidate = Join-Path $env:TEMP ('azg-smoke-amcf-' + $short)
New-Item -ItemType Directory -Force $candidate | Out-Null
Copy-Item $bundle (Join-Path $candidate 'amcf.msixbundle') -Force
Set-Content -Path (Join-Path $candidate 'attendu.txt') -Value $hash -Encoding ASCII

if (Get-Process -Name 'WindowsSandbox', 'WindowsSandboxClient', 'WindowsSandboxRemoteSession' -ErrorAction SilentlyContinue) {
    throw 'Un Windows Sandbox est deja ouvert : le fermer, puis relancer.'
}
$wsb = @"
<Configuration>
  <Networking>Enable</Networking>
  <MappedFolders>
    <MappedFolder><HostFolder>$candidate</HostFolder><SandboxFolder>C:\candidat</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$kit</HostFolder><SandboxFolder>C:\kit</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$results</HostFolder><SandboxFolder>C:\resultats</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
  </MappedFolders>
  <LogonCommand><Command>powershell.exe -NoExit -NoProfile -ExecutionPolicy Bypass -File C:\kit\smoke-amcf.ps1</Command></LogonCommand>
</Configuration>
"@
$wsbPath = Join-Path $candidate 'smoke-amcf.wsb'
Set-Content -Path $wsbPath -Value $wsb -Encoding UTF8
"Bundle AMCF SHA-256 : $hash (conforme)"
"Journal : $results\smoke.txt"
Start-Process $wsbPath
