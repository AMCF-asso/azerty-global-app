# Execute dans Windows Sandbox par lancer-recette.ps1. Signe une COPIE du candidat avec
# un certificat jetable au sujet du Publisher Store, l'installe, lance l'app et note
# l'etat de ses protections. Le bundle d'origine n'est jamais modifie.
$ErrorActionPreference = 'Stop'
$log = 'C:\resultats\installation.txt'
function Say([string]$message) {
    $line = '[{0:HH:mm:ss}] {1}' -f (Get-Date), $message
    Add-Content -Path $log -Value $line
    Write-Host $line
}

try {
    $source = 'C:\candidat\candidat.msixbundle'
    Say ('SHA-256 candidat : ' + (Get-FileHash $source -Algorithm SHA256).Hash.ToLower())
    Say ('Windows ' + [Environment]::OSVersion.Version + ' ; ' + (Get-CimInstance Win32_Processor).Name)

    New-Item -ItemType Directory -Force 'C:\work' | Out-Null
    $signed = 'C:\work\candidat-signe.msixbundle'
    Copy-Item $source $signed -Force
    $cert = New-SelfSignedCertificate -Type Custom -Subject 'CN=7FD049E3-1C58-42E0-A07F-A9712DE19E38' `
        -KeyUsage DigitalSignature -CertStoreLocation 'Cert:\CurrentUser\My' `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}') -NotAfter (Get-Date).AddDays(2)
    & 'C:\sdk\signtool.exe' sign /fd SHA256 /sha1 $cert.Thumbprint $signed | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "signtool a rendu $LASTEXITCODE" }
    Export-Certificate -Cert $cert -FilePath 'C:\work\test.cer' | Out-Null
    Import-Certificate -FilePath 'C:\work\test.cer' -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null

    Add-AppxPackage -Path $signed
    $package = Get-AppxPackage -Name 'AZERTYGlobal.AZERTYGlobal'
    Say ('Installe : ' + $package.PackageFullName)

    $sac = Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy' -ErrorAction SilentlyContinue
    Say ('SAC VerifiedAndReputablePolicyState = ' + $sac.VerifiedAndReputablePolicyState)
    if ($sac.VerifiedAndReputablePolicyState -ne 0) {
        Set-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy' -Name VerifiedAndReputablePolicyState -Value 0 -Type DWord
        $ci = Join-Path $env:SystemRoot 'System32\CiTool.exe'
        if (Test-Path $ci) { Say ('CiTool --refresh : ' + ((& $ci --refresh 2>&1) -join ' ')) }
        Start-Sleep -Seconds 5
        Say ('SAC apres coupure = ' + (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy').VerifiedAndReputablePolicyState)
    }
    Say ('Manifest exe : ' + (Get-Content (Join-Path $package.InstallLocation 'AppxManifest.xml') -Raw | Select-String -Pattern 'Executable="[^"]+"' -AllMatches).Matches.Value)
    $t0 = Get-Date
    Start-Process ('shell:AppsFolder\' + $package.PackageFamilyName + '!AZERTYGlobal')
    Start-Sleep -Seconds 25
    $process = Get-Process -Name 'AZERTY Global' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($process) {
        $mitigation = Get-ProcessMitigation -Id $process.Id
        Say ('App lancee (pid ' + $process.Id + ') apres 25 s ; shadow stack ' + $mitigation.UserShadowStack.UserShadowStack + ' ; CFG ' + $mitigation.CFG.Enable)
    } else {
        Say "ECHEC : l'app ne tourne pas 25 s apres son lancement."
        Say ('Processus AZERTY* : ' + ((Get-Process | Where-Object { $_.Name -like '*AZERTY*' } | ForEach-Object { $_.Name + ':' + $_.Id }) -join ', '))
        foreach ($evtLog in 'Microsoft-Windows-CodeIntegrity/Operational', 'Application', 'Microsoft-Windows-AppXDeploymentServer/Operational') {
            Get-WinEvent -LogName $evtLog -MaxEvents 15 -ErrorAction SilentlyContinue | Where-Object { $_.TimeCreated -ge $t0.AddSeconds(-5) } | ForEach-Object { Say ("EVT [$evtLog] " + $_.Id + ' ' + ($_.Message -split "`n")[0]) }
        }
        $exe = Join-Path $package.InstallLocation 'AZERTY Global.exe'
        Say ('Lancement direct : ' + $exe + ' existe=' + (Test-Path $exe))
        $p = Start-Process -FilePath $exe -PassThru -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 10
        if ($p) { Say ('Direct : HasExited=' + $p.HasExited + $(if ($p.HasExited) { ' code=' + $p.ExitCode } else { ' pid=' + $p.Id })) } else { Say 'Direct : Start-Process sans processus' }
    }
    Say 'Pret : derouler recette.md dans cette fenetre, noter chaque ligne.'
} catch {
    Say ('ERREUR : ' + $_.Exception.Message)
}
