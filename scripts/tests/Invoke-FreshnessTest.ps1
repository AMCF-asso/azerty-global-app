param([string]$SourceRoot, [string]$ReferencePath, [switch]$Assert)
$ErrorActionPreference = 'Stop'
# Sans cette bascule, l'hote ecrit le message d'erreur dans la page de codes OEM : le
# premier accent casse le decodage UTF-8 cote appelant et la preuve arrive vide.
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot '..\Assert-PublishFreshness.ps1')
try {
    if ($Assert) {
        # Le flux d'avertissement n'arrive pas sur stderr sous -File : le rediriger a la
        # main, sinon l'echappatoire passe pour un pack silencieux.
        $avertissements = @()
        Assert-PublishNotStale -SourceRoot $SourceRoot -PublishExe $ReferencePath -Architecture 'x64' `
            -WarningVariable +avertissements -WarningAction SilentlyContinue
        foreach ($avertissement in $avertissements) {
            [Console]::Error.WriteLine($avertissement.Message)
        }
        'PACK-AUTORISE'
    }
    else {
        ConvertTo-Json -Compress -InputObject @(Get-StaleSourceFiles -SourceRoot $SourceRoot -ReferencePath $ReferencePath)
    }
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
