# WACK sur le candidat exact de la 1.3.0. A lancer dans un PowerShell administrateur :
#   powershell -ExecutionPolicy Bypass -File wack.ps1 -Bundle <chemin du .msixbundle>
# Le rapport est range sous evidence\wack-<12 premiers caracteres du SHA-256>\.
param([Parameter(Mandatory = $true)][string]$Bundle)
$ErrorActionPreference = 'Stop'

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Lancer ce script dans un PowerShell administrateur.'
}
if (Get-Process -Name 'AZERTY Global' -ErrorAction SilentlyContinue) {
    throw "Quitter AZERTY Global (menu de l'icone) avant le WACK."
}

$bundle = (Resolve-Path $Bundle).Path
$hash = (Get-FileHash $bundle -Algorithm SHA256).Hash.ToLower()
$kit = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $kit ('evidence\wack-' + $hash.Substring(0, 12))
New-Item -ItemType Directory -Force $out | Out-Null
$report = Join-Path $out 'wack-report.xml'

$appcert = 'C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe'
& $appcert reset | Out-Null
& $appcert test -appxpackagepath $bundle -reportoutputpath $report
if (-not (Test-Path $report)) { throw "Aucun rapport WACK produit (code $LASTEXITCODE)." }

[xml]$xml = Get-Content $report
$overall = $xml.REPORT.OVERALL_RESULT
$lines = @("SHA-256 : $hash", "OVERALL_RESULT : $overall", ('X64_ONLY : ' + $xml.REPORT.X64_ONLY), ('Date : ' + (Get-Date -Format s)))
foreach ($requirement in $xml.REPORT.REQUIREMENTS.REQUIREMENT) {
    foreach ($test in $requirement.TEST) {
        $result = $test.RESULT.InnerText
        if ($result -ne 'PASS') { $lines += (' - ' + $requirement.TITLE + ' / ' + $test.NAME + ' : ' + $result) }
    }
}
Set-Content -Path (Join-Path $out 'resume.txt') -Value $lines -Encoding UTF8
$lines
