# Refuser d'empaqueter un publish plus ancien que les sources qu'il est censé contenir.
#
# Mesuré le 2026-09-20 (audit v1.3.0, bloquant B1) : le bundle 1.3.0.0 a été produit à
# 17:27 sur un publish de 17:26, alors que huit fichiers de src/ dataient de 19:02. Le
# bundle soumissible ne contenait donc ni le rattrapage d'avis ni quatre ports de
# correctifs. Pack-MSIX.ps1 ne testait que l'existence du publish, et Verify-Release.ps1
# ne compare que publish <-> bundle : personne ne regardait source <-> publish.
#
# Échappatoire volontaire : $env:AZERTYGLOBAL_ALLOW_STALE_PUBLISH = '1' dégrade le refus
# en avertissement, pour le cas où seul un fichier non compilé a bougé. Elle laisse une
# trace dans la sortie du pack.

Set-StrictMode -Version Latest

# Ce qui ne part pas dans le binaire publié : artefacts de build et projets de test.
# Les exclure évite de bloquer un pack parce qu'un test a été édité après le publish.
$script:PublishFreshnessExclusions = @(
    '\\bin\\',
    '\\obj\\',
    '\\TestSupport\\',
    '\.Tests\\'
)

function Get-StaleSourceFiles {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SourceRoot,
        [Parameter(Mandatory)][string]$ReferencePath
    )

    if (-not (Test-Path -LiteralPath $ReferencePath)) {
        throw "Référence de fraîcheur introuvable : $ReferencePath"
    }
    if (-not (Test-Path -LiteralPath $SourceRoot)) {
        throw "Racine des sources introuvable : $SourceRoot"
    }

    $reference = (Get-Item -LiteralPath $ReferencePath).LastWriteTimeUtc
    $root = (Resolve-Path -LiteralPath $SourceRoot).Path

    Get-ChildItem -LiteralPath $root -Recurse -File -Force |
        Where-Object {
            $relative = $_.FullName.Substring($root.Length)
            $excluded = $false
            foreach ($pattern in $script:PublishFreshnessExclusions) {
                if ($relative -match $pattern) { $excluded = $true; break }
            }
            (-not $excluded) -and ($_.LastWriteTimeUtc -gt $reference)
        } |
        Sort-Object FullName |
        ForEach-Object { $_.FullName }
}

function Assert-PublishNotStale {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SourceRoot,
        [Parameter(Mandatory)][string]$PublishExe,
        [Parameter(Mandatory)][string]$Architecture
    )

    $stale = @(Get-StaleSourceFiles -SourceRoot $SourceRoot -ReferencePath $PublishExe)
    if ($stale.Count -eq 0) {
        return
    }

    $root = (Resolve-Path -LiteralPath $SourceRoot).Path
    $shown = $stale | Select-Object -First 10 | ForEach-Object { '  - ' + $_.Substring($root.Length).TrimStart('\') }
    $suite = if ($stale.Count -gt 10) { "`n  … et {0} autre(s)" -f ($stale.Count - 10) } else { '' }
    $resume = "{0} fichier(s) de src/ sont plus récents que le publish $Architecture :`n{1}{2}" -f $stale.Count, ($shown -join "`n"), $suite

    if ($env:AZERTYGLOBAL_ALLOW_STALE_PUBLISH -eq '1') {
        Write-Warning "$resume`nAZERTYGLOBAL_ALLOW_STALE_PUBLISH=1 : empaquetage d'un publish périmé, assumé."
        return
    }

    throw @"
$resume

Le paquet contiendrait un binaire antérieur à ces sources (bloquant B1 de l'audit du 2026-09-20).
Republier avant d'empaqueter :
  `$env:PATH += ";C:\Program Files (x86)\Microsoft Visual Studio\Installer"
  dotnet publish -c Release -r win-$Architecture

Pour empaqueter quand même (seul un fichier non compilé a bougé) :
  `$env:AZERTYGLOBAL_ALLOW_STALE_PUBLISH = '1'
"@
}
