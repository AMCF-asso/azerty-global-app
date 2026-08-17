param(
    [switch]$SyncPublicRepo,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
function Resolve-FirstExistingPath([string[]]$Candidates, [string]$Label) {
    foreach ($candidate in $Candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate)
        }
    }
    throw "$Label introuvable. Chemins testes: $($Candidates -join '; ')"
}

# Le site s'appelle 'website' depuis la migration du 2026-08-03. Les deux anciens noms
# restent en repli pour un arbre non migre, mais aucun des deux n'existe plus ici: le
# script levait donc avant sa premiere copie, et personne ne s'en apercevait puisque les
# trois JSON etaient deja synchronises a la main.
$siteRoot = Resolve-FirstExistingPath @(
    (Join-Path $projectRoot '..\website'),
    (Join-Path $projectRoot '..\Site AZERTY Global'),
    (Join-Path $projectRoot '..\2026\Site AZERTY Global')
) 'Site AZERTY Global'
$publicRepoRoot = Resolve-FirstExistingPath @(
    (Join-Path $projectRoot '..\..\Microsoft Store - app repo'),
    $projectRoot
) 'Clone public Microsoft Store'

$sourceLayout = Join-Path $siteRoot 'data\AZERTY Global.json'
$sourceIndex = Join-Path $siteRoot 'tester\character-index.json'
$sourceLessons = Join-Path $siteRoot 'tester\lessons.json'
$targetLayout = Join-Path $projectRoot 'src\AZERTY Global 2026.json'
$targetIndex = Join-Path $projectRoot 'src\character-index.json'
$targetLessons = Join-Path $projectRoot 'src\lessons.json'

function Show-GitStatus([string]$RepoPath) {
    $status = git -C $RepoPath status --porcelain
    if ($LASTEXITCODE -ne 0) {
        throw "Impossible de verifier l'etat Git de $RepoPath"
    }
    if ($status) {
        Write-Host "Clone public deja modifie ; seuls les fichiers allowlistes seront synchronises :"
        $status | ForEach-Object { Write-Host " - $_" }
    }
}

function Get-ShortHash([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return 'absent' }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.Substring(0, 16).ToLower()
}

function Copy-Exact([string]$Source, [string]$Destination) {
    $sourceHash = Get-ShortHash $Source
    $destinationHash = Get-ShortHash $Destination
    $name = Split-Path -Leaf $Destination

    if ($sourceHash -ne 'absent' -and $sourceHash -eq $destinationHash) {
        Write-Host " - INCHANGE $name  $destinationHash"
        return
    }

    # Ce script importe l'etat de travail du site, pas une version publiee. Le seul moment
    # ou il ecrit est justement celui ou quelque chose change: il le dit avant de le faire,
    # et -DryRun permet de le lire sans rien ecraser.
    $action = if ($DryRun) { 'A METTRE A JOUR' } else { 'MISE A JOUR    ' }
    Write-Host " - $action $name  $destinationHash -> $sourceHash"
    if ($DryRun) { return }

    try {
        [System.IO.File]::Copy($Source, $Destination, $true)
    } catch [System.UnauthorizedAccessException] {
        $sourceResolved = [System.IO.Path]::GetFullPath($Source)
        $destinationResolved = [System.IO.Path]::GetFullPath($Destination)
        $projectResolved = [System.IO.Path]::GetFullPath($projectRoot)
        $publicResolved = if (Test-Path $publicRepoRoot) { [System.IO.Path]::GetFullPath((Resolve-Path $publicRepoRoot)) } else { '' }
        $isInProject = $destinationResolved.StartsWith($projectResolved, [System.StringComparison]::OrdinalIgnoreCase)
        $isInPublic = $publicResolved -and $destinationResolved.StartsWith($publicResolved, [System.StringComparison]::OrdinalIgnoreCase)

        if (-not ($isInProject -or $isInPublic)) {
            throw "Destination hors perimetre de synchronisation: $destinationResolved"
        }

        $sourceDir = Split-Path -Parent $sourceResolved
        $destinationDir = Split-Path -Parent $destinationResolved
        $fileName = Split-Path -Leaf $sourceResolved
        & robocopy $sourceDir $destinationDir $fileName /R:0 /W:0 /NFL /NDL /NJH /NJS | Out-Null
        if ($LASTEXITCODE -gt 7) {
            throw "Robocopy a echoue pour $destinationResolved (exit $LASTEXITCODE)"
        }
    }
}

function Copy-AllowedPublicFile([string]$Source, [string]$Destination, [switch]$AllowCreate) {
    if (-not (Test-Path $Source)) {
        throw "Source allowlistee introuvable: $Source"
    }
    if (-not $AllowCreate -and -not (Test-Path $Destination)) {
        throw "Destination allowlistee introuvable: $Destination"
    }
    Copy-Exact $Source $Destination
}

# La validation vit dans scripts/validate-layout.py: schema ferme, cinq compteurs
# recalcules depuis la donnee, jetons dk_* recoupes avec les touches mortes declarees. Le
# here-string Node qui occupait cette place tenait quatre comptes en dur, et 60 de ses 63
# assertions etaient le double mot pour mot de
# src/AZERTYGlobal.Tests/ResourceAlignmentTests.cs. Les trois restantes etaient justement
# des comptes, que le recalcul rend inutiles.
function Invoke-LayoutValidation {
    & python (Join-Path $projectRoot 'scripts\validate-layout.py')
    switch ($LASTEXITCODE) {
        0 { return }
        2 {
            # Sortie 2 = controle impossible, pas violation: jsonschema est une dependance
            # de CI seulement, et un poste sans elle ne doit pas voir sa copie echouer.
            Write-Warning 'Validation impossible ici (voir ci-dessus). La CI la rejoue, et la bloque.'
        }
        default { throw 'Validation de la disposition echouee' }
    }
}

if (-not (Test-Path $sourceLayout)) { throw "Source layout introuvable: $sourceLayout" }
if (-not (Test-Path $sourceIndex)) { throw "Source character-index introuvable: $sourceIndex" }
if (-not (Test-Path $sourceLessons)) { throw "Source lessons introuvable: $sourceLessons" }

if ($SyncPublicRepo) {
    if (-not (Test-Path $publicRepoRoot)) {
        throw "Clone public introuvable: $publicRepoRoot"
    }
    Show-GitStatus $publicRepoRoot
}

Copy-Exact $sourceLayout $targetLayout
Copy-Exact $sourceIndex $targetIndex
Copy-Exact $sourceLessons $targetLessons

Invoke-LayoutValidation

if ($SyncPublicRepo) {
    $publicRootResolved = Resolve-Path $publicRepoRoot
    Copy-AllowedPublicFile $targetLayout (Join-Path $publicRootResolved 'src\AZERTY Global 2026.json')
    Copy-AllowedPublicFile $targetIndex (Join-Path $publicRootResolved 'src\character-index.json')
    Copy-AllowedPublicFile $targetLessons (Join-Path $publicRootResolved 'src\lessons.json') -AllowCreate
    Copy-AllowedPublicFile $PSCommandPath (Join-Path $publicRootResolved 'scripts\Sync-LayoutResources.ps1')
    Copy-AllowedPublicFile (Join-Path $projectRoot 'src\AZERTYGlobal.Tests\ResourceAlignmentTests.cs') (Join-Path $publicRootResolved 'src\AZERTYGlobal.Tests\ResourceAlignmentTests.cs')
    Copy-AllowedPublicFile (Join-Path $projectRoot 'Changelog.md') (Join-Path $publicRootResolved 'Changelog.md')
    Copy-AllowedPublicFile (Join-Path $projectRoot 'msix\Fiche Store.md') (Join-Path $publicRootResolved 'msix\Fiche Store.md')
    # Pas de seconde validation: les copies ci-dessus sont identiques octet pour octet
    # aux fichiers deja valides, et Copy-Exact leve si une copie echoue.
}

if ($DryRun) {
    Write-Host 'Aucune ecriture: -DryRun. Les lignes ci-dessus disent ce qui serait copie.'
} else {
    Write-Host 'Ressources layout synchronisees.'
}

# Deux lignes affichaient ici '29 touches mortes' et '1034 entrees' en dur: un troisieme
# endroit a corriger a la main apres chaque evolution de la disposition. Elles sont
# retirees plutot que recalculees, PowerShell etant incapable de lire ce fichier:
# ConvertFrom-Json de la 5.1 est insensible a la casse et rejette les tables de touches
# mortes, qui contiennent a la fois 'a' et 'A'. Plus aucun compte n'est assere a la main:
# scripts/validate-layout.py les recalcule depuis la donnee.
Write-Host ' - Disposition validee contre son schema, ses compteurs et ses references.'
Write-Host ' - character-index.json est asserre par dotnet test (ResourceAlignmentTests).'
if ($SyncPublicRepo) {
    Write-Host " - Clone public synchronise: $publicRepoRoot"
}
