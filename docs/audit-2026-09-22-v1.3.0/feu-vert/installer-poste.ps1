# Installe le candidat 1.3.0 sur le poste d'Antoine pour la recette a la main (choix du
# 2026-09-23 : recette sur le poste plutot qu'en Sandbox). Signe une COPIE du bundle avec le
# certificat de test deja approuve sur ce poste (sujet = Publisher Store, 18/08/2026) ; le
# bundle d'origine n'est jamais modifie. Pas besoin d'etre administrateur.
#   powershell -ExecutionPolicy Bypass -File installer-poste.ps1 -Bundle <chemin du .msixbundle>
# Journal : evidence\recette-<12 premiers caracteres du SHA-256>\installation-poste.txt.
# Desinstaller ensuite par Parametres > Applications (ligne A12).
param([Parameter(Mandatory = $true)][string]$Bundle)
$ErrorActionPreference = 'Stop'

$bundlePath = (Resolve-Path $Bundle).Path
$kit = Split-Path -Parent $MyInvocation.MyCommand.Path
$signtool = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe'
$subject = 'CN=7FD049E3-1C58-42E0-A07F-A9712DE19E38'

$hash = (Get-FileHash $bundlePath -Algorithm SHA256).Hash.ToLower()
$results = Join-Path $kit ('evidence\recette-' + $hash.Substring(0, 12))
New-Item -ItemType Directory -Force $results | Out-Null
$log = Join-Path $results 'installation-poste.txt'
function Say([string]$message) {
    $line = '[{0:HH:mm:ss}] {1}' -f (Get-Date), $message
    Add-Content -Path $log -Value $line -Encoding UTF8
    Write-Host $line
}

try {
    Say ('SHA-256 candidat : ' + $hash)
    Say ('Windows ' + [Environment]::OSVersion.Version + ' ; ' + (Get-CimInstance Win32_Processor).Name)
    if (Get-Process -Name 'AZERTY Global' -ErrorAction SilentlyContinue) {
        throw "Une version d'AZERTY Global tourne : la quitter par son icone, puis relancer."
    }
    if (-not (Test-Path $signtool)) { throw "signtool introuvable : $signtool" }

    $trusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Subject -eq $subject
    $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $subject -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) -and ($trusted.Thumbprint -contains $_.Thumbprint) } | Select-Object -First 1
    if (-not $cert) { throw "Aucun certificat de test approuve avec cle privee pour $subject" }

    $copy = Join-Path $env:TEMP ('azg-poste-' + $hash.Substring(0, 12) + '.msixbundle')
    Copy-Item $bundlePath $copy -Force
    & $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $copy | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "signtool a rendu $LASTEXITCODE" }

    # Un candidat precedent de meme version (1.3.0.0) bloque l'installation (0x80073CFB) :
    # le retirer d'abord. Sa configuration part avec lui : la recette repart d'un premier lancement.
    $previous = Get-AppxPackage -Name 'AZERTYGlobal.AZERTYGlobal' | Where-Object Version -eq '1.3.0.0'
    foreach ($old in $previous) {
        Remove-AppxPackage -Package $old.PackageFullName
        Say ('Candidat precedent retire : ' + $old.PackageFullName)
    }

    Add-AppxPackage -Path $copy
    $package = Get-AppxPackage -Name 'AZERTYGlobal.AZERTYGlobal' | Where-Object Version -eq '1.3.0.0' | Select-Object -First 1
    Say ('Installe : ' + $package.PackageFullName)

    Start-Process ('shell:AppsFolder\' + $package.PackageFamilyName + '!AZERTYGlobal')
    Start-Sleep -Seconds 8
    $process = Get-Process -Name 'AZERTY Global' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($process) {
        $mitigation = Get-ProcessMitigation -Id $process.Id
        Say ('App lancee (pid ' + $process.Id + ') ; shadow stack ' + $mitigation.UserShadowStack.UserShadowStack + ' ; CFG ' + $mitigation.CFG.Enable)
        Say 'Pret : derouler recette.md, noter chaque ligne.'
    } else {
        Say "ECHEC : l'app ne tourne pas 8 s apres son lancement (Smart App Control ?)."
    }
} catch {
    Say ('ERREUR : ' + $_.Exception.Message)
}
