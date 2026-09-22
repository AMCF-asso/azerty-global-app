$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $root 'scripts/Assert-PublishFreshness.ps1')
$failed = 0
foreach ($arch in @('x64', 'arm64')) {
    $exe = Join-Path $root "src/bin/Release/net8.0-windows10.0.17763.0/win-$arch/publish/AZERTY Global.exe"
    try {
        Assert-PublishNotStale -SourceRoot (Join-Path $root 'src') -PublishExe $exe -Architecture $arch
        Write-Output "PASS $arch"
    } catch {
        $failed++
        Write-Output "FAIL $arch : $_"
    }
}
exit ([int]($failed -gt 0))
