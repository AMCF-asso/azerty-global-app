# Execute dans Windows Sandbox par lancer-smoke-amcf.ps1. Installe le bundle signe AMCF tel
# quel, sans importer de certificat : la signature doit suffire sur une machine neuve.
# S1-S5 sont automatiques ; S6 est la frappe, jugee par Antoine ; S7 desinstalle ; S8 reinstalle
# et verifie que la config partie avec le paquet ne saute pas l'accueil.
$ErrorActionPreference = 'Stop'
$log = 'C:\resultats\smoke.txt'
$script:fails = @()
function Say([string]$message) {
    $line = '[{0:HH:mm:ss}] {1}' -f (Get-Date), $message
    Add-Content -Path $log -Value $line -Encoding UTF8
    Write-Host $line
}
function Check([string]$id, [bool]$ok, [string]$detail) {
    Say ('{0} {1} : {2}' -f $id, $(if ($ok) { 'OK' } else { 'ECHEC' }), $detail)
    if (-not $ok) { $script:fails += $id }
}

$org = 'Association pour la Modernisation du Clavier Fran' + [char]0x00E7 + 'ais'
$bundle = 'C:\candidat\amcf.msixbundle'
try {
    Say ('Windows ' + [Environment]::OSVersion.Version + ' ; ' + $env:PROCESSOR_ARCHITECTURE)
    $before = @(Get-AppxPackage -Name 'AZERTYGlobal.*')
    $amcfCerts = @(Get-ChildItem Cert:\LocalMachine\TrustedPeople, Cert:\LocalMachine\Root, Cert:\CurrentUser\TrustedPeople -ErrorAction SilentlyContinue | Where-Object Subject -like "CN=$org*")
    Check 'S0' ($before.Count -eq 0 -and $amcfCerts.Count -eq 0) ("machine propre : {0} paquet AZERTY Global, {1} certificat AMCF de confiance" -f $before.Count, $amcfCerts.Count)

    $expected = (Get-Content 'C:\candidat\attendu.txt' -Raw).Trim().ToLower()
    $hash = (Get-FileHash $bundle -Algorithm SHA256).Hash.ToLower()
    Check 'S1' ($hash -eq $expected) "SHA-256 $hash"

    $sig = Get-AuthenticodeSignature -LiteralPath $bundle
    $subject = if ($sig.SignerCertificate) { $sig.SignerCertificate.Subject } else { '(aucun)' }
    $stamp = if ($sig.TimeStamperCertificate) { $sig.TimeStamperCertificate.Subject } else { '(aucun)' }
    Check 'S2' ($sig.Status -eq 'Valid' -and $subject -like "CN=$org,*" -and $sig.TimeStamperCertificate) ("signature {0} ; signataire {1} ; horodatage {2}" -f $sig.Status, $subject, $stamp)

    Add-AppxPackage -Path $bundle
    $package = Get-AppxPackage -Name 'AZERTYGlobal.AZERTYGlobal'
    Check 'S3' ($package -and $package.Version -eq '1.3.0.0' -and $package.Publisher -like "CN=$org,*") ("installe sans certificat importe : {0} ; signature {1}" -f $package.PackageFullName, $package.SignatureKind)
    Check 'S4' ($package.Publisher -notlike 'CN=7FD049E3*') ("canal AMCF (famille hors Store) : {0}" -f $package.PackageFamilyName)

    Start-Process ('shell:AppsFolder\' + $package.PackageFamilyName + '!AZERTYGlobal')
    Start-Sleep -Seconds 8
    $process = Get-Process -Name 'AZERTY Global' -ErrorAction SilentlyContinue | Select-Object -First 1
    Check 'S5' ([bool]$process) $(if ($process) { 'app lancee, pid ' + $process.Id } else { "l'app ne tourne pas 8 s apres son lancement" })

    Start-Process notepad.exe
    Write-Host ''
    Write-Host 'S6, dans le Bloc-notes (fermer l''accueil de l''app s''il s''ouvre) :'
    Write-Host '  a) AltGr+O -> oe lie (U+0153)'
    Write-Host '  b) AltGr+Maj+* (touche a droite de u accent grave), puis o -> petit o barre cyrillique (U+04E9, 2026.1)'
    $answer = Read-Host 'Les deux caracteres sont-ils justes ? (o/n)'
    Check 'S6' ($answer -match '^[oOyY]') ("frappe AltGr jugee par Antoine : " + $answer)

    Get-Process -Name 'AZERTY Global' -ErrorAction SilentlyContinue | Stop-Process -Force
    Remove-AppxPackage -Package $package.PackageFullName
    Start-Sleep -Seconds 3
    $left = @(Get-AppxPackage -Name 'AZERTYGlobal.*')
    $running = @(Get-Process -Name 'AZERTY Global' -ErrorAction SilentlyContinue)
    Check 'S7' ($left.Count -eq 0 -and $running.Count -eq 0) ("desinstallation : {0} paquet restant, {1} processus" -f $left.Count, $running.Count)

    # S8 (remontee n. 1 d'Antoine, 07/11) : la config vit dans le dossier prive du paquet, la
    # desinstallation l'efface, et une reinstallation repart a neuf, accueil compris.
    $realDir = Join-Path $env:LOCALAPPDATA 'AZERTY Global'
    $privateConfig = Join-Path $env:LOCALAPPDATA ('Packages\' + $package.PackageFamilyName + '\LocalCache\Local\AZERTY Global\config.json')
    $leftReal = Test-Path $realDir
    $leftPrivate = Test-Path $privateConfig
    Add-AppxPackage -Path $bundle
    $again = Get-AppxPackage -Name 'AZERTYGlobal.AZERTYGlobal'
    Start-Process ('shell:AppsFolder\' + $again.PackageFamilyName + '!AZERTYGlobal')
    Start-Sleep -Seconds 8
    $welcome = @(Get-Process -Name 'AZERTY Global' -ErrorAction SilentlyContinue | Where-Object MainWindowTitle -eq 'AZERTY Global')
    $newPrivate = Test-Path $privateConfig
    $newReal = Test-Path $realDir
    Check 'S8' ((-not $leftReal) -and (-not $leftPrivate) -and $newPrivate -and (-not $newReal) -and $welcome.Count -eq 1) `
        ("apres desinstallation : dossier reel {0}, config privee {1} ; apres reinstallation : config privee {2}, dossier reel {3}, fenetre d'accueil {4}" -f `
            $leftReal, $leftPrivate, $newPrivate, $newReal, $welcome.Count)
    Get-Process -Name 'AZERTY Global' -ErrorAction SilentlyContinue | Stop-Process -Force
    Remove-AppxPackage -Package $again.PackageFullName
} catch {
    Say ('ERREUR : ' + $_.Exception.Message)
    $script:fails += 'ERREUR'
}
Say $(if ($script:fails.Count -eq 0) { 'VERDICT : CONFORME (S0-S8)' } else { 'VERDICT : ECHEC (' + ($script:fails -join ', ') + ')' })
Write-Host 'Fermer le Sandbox : rien n''en sort hors de ce journal.'
