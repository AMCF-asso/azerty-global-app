# Sonde CET dans Windows Sandbox : lance chaque variante de l'app 1.3.0 avec un
# config.json invalide (JsonException rattrapée dès Main), lit la politique de
# shadow stack du processus vivant, puis l'arrête. Résultats dans C:\results.
$ErrorActionPreference = 'Continue'
$out = @{ cpu = (Get-CimInstance Win32_Processor).Name; os = [Environment]::OSVersion.Version.ToString(); runs = @() }
foreach ($v in @('nocet', 'cet')) {
    $run = "C:\run\$v"
    New-Item -ItemType Directory -Force $run | Out-Null
    Copy-Item "C:\probe\$v\AZERTY Global.exe" $run
    Set-Content -Path "$run\config.json" -Value '[1]' -NoNewline -Encoding ascii
    $r = [ordered]@{ variant = $v; sha256 = (Get-FileHash "$run\AZERTY Global.exe").Hash }
    try {
        $p = Start-Process -FilePath "$run\AZERTY Global.exe" -PassThru -ErrorAction Stop
        Start-Sleep -Seconds 5
        $r.aliveAt5s = -not $p.HasExited
        if ($p.HasExited) { $r.exitCode = ('0x{0:X8}' -f $p.ExitCode) }
        else {
            try {
                $m = Get-ProcessMitigation -Id $p.Id -ErrorAction Stop
                $r.userShadowStack = [string]$m.UserShadowStack.UserShadowStack
                $r.userShadowStackStrict = [string]$m.UserShadowStack.UserShadowStackStrictMode
                $r.cfg = [string]$m.CFG.Enable
            } catch { $r.mitigationError = $_.Exception.Message }
            Stop-Process -Id $p.Id -Force
        }
    } catch { $r.startError = $_.Exception.Message }
    $r.errorLog = (Get-Content "$run\error.log" -ErrorAction SilentlyContinue) -join ' | '
    $r.crashEvents = @(Get-WinEvent -FilterHashtable @{LogName='Application'; ProviderName='Application Error'} -ErrorAction SilentlyContinue |
        Where-Object { $_.Message -match [regex]::Escape("C:\run\$v") } | ForEach-Object { ($_.Message -replace '\s+',' ').Substring(0,260) })
    $out.runs += $r
}
$out | ConvertTo-Json -Depth 5 | Set-Content -Path C:\results\cet-probe.json -Encoding utf8
'fin' | Set-Content -Path C:\results\done.txt
