$ErrorActionPreference = 'Stop'

# Architectures cibles (x64 + ARM64 natif)
$architectures = @('x64', 'arm64')

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$srcDir = Join-Path $projectRoot 'src'
$msixDir = Join-Path $projectRoot 'msix'
$bundleStagingDir = Join-Path $projectRoot '.msix-bundle-staging'
$archivesDir = Join-Path $projectRoot 'Archives'
$msixArchiveRoot = Join-Path $archivesDir 'msix-previous'
$csprojPath = Join-Path $srcDir 'AZERTYGlobal.csproj'

# Chemins de sortie
$stableBundlePath = Join-Path $msixDir 'AZERTYGlobal.msixbundle'

# Seuls les visuels que le manifeste référence entrent dans le package : les quatre
# logos déclarés, plus leurs variantes .scale-200 résolues par convention de nommage.
# Tout le reste de msix/Assets/ est du matériel de fiche Store (captures, posters,
# gabarits HTML) : copié en bloc, il gonflait chaque .msix de 3,4 Mo — mesuré le
# 2026-08-24 en ouvrant le bundle (audit v1.2.0, finding R-1).
$packagedAssets = @(
    'StoreLogo.png',
    'Square44x44Logo.png',
    'Square44x44Logo.scale-200.png',
    'Square150x150Logo.png',
    'Square150x150Logo.scale-200.png',
    'Wide310x150Logo.png'
)

function Copy-DirectoryContent([string]$Source, [string]$Destination) {
    Get-ChildItem $Source -Force |
        Where-Object { $_.Name -notlike '*.msix' -and $_.Name -notlike '*.msixbundle' -and
                       $_.Name -notlike '*.exe' -and $_.Name -notlike '*.md' -and
                       $_.Name -notlike 'wack-report*' -and $_.Name -ne 'Assets' } |
        ForEach-Object {
            $target = Join-Path $Destination $_.Name
            if ($_.PSIsContainer) {
                Copy-Item $_.FullName -Destination $target -Recurse -Force
            }
            else {
                Copy-Item $_.FullName -Destination $target -Force
            }
        }
}

function Resolve-MakeAppx() {
    $direct = Get-Command MakeAppx.exe -ErrorAction SilentlyContinue
    if ($direct) {
        return $direct.Source
    }

    $sdkTool = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Recurse -Filter MakeAppx.exe -ErrorAction SilentlyContinue |
        Where-Object FullName -like '*\x64\MakeAppx.exe' |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $sdkTool) {
        throw 'MakeAppx.exe introuvable. Installer le Windows 10/11 SDK.'
    }

    return $sdkTool.FullName
}

# Lire la version depuis le .csproj
[xml]$csproj = [System.IO.File]::ReadAllText($csprojPath)
$version = $csproj.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Version introuvable dans $csprojPath"
}

# Le manifest MSIX exige exactement 4 segments (Major.Minor.Build.Revision).
# Si le csproj declare 3 segments, on complete avec .0. Si 4 deja presents, on les utilise tels quels.
$storeVersion = if (($version -split '\.').Count -eq 4) { $version } else { "$version.0" }
$versionedBundlePath = Join-Path $msixDir ("AZERTYGlobal-{0}.msixbundle" -f $storeVersion)
$msixArchiveDir = Join-Path (Join-Path $msixArchiveRoot 'by-version') $storeVersion

# Vérifier que les exécutables publiés existent
foreach ($arch in $architectures) {
    $publishExe = Join-Path $srcDir "bin\Release\net8.0-windows10.0.17763.0\win-$arch\publish\AZERTY Global.exe"
    if (-not (Test-Path $publishExe)) {
        # Le prefixe de PATH n'est pas decoratif : sans vswhere.exe l'edition de liens
        # native AOT echoue sur un MSB3073 qui designe link.exe, pas la cause reelle.
        # Meme prerequis que msix/README.md, repete ici car c'est cette commande-la
        # qui est copiee quand le pack s'arrete.
        throw "Publish introuvable pour $arch : $publishExe`nLancer:`n  `$env:PATH += `";C:\Program Files (x86)\Microsoft Visual Studio\Installer`"`n  dotnet publish -c Release -r win-$arch"
    }
}

$makeAppx = Resolve-MakeAppx
$msixFiles = @()

# Créer un .msix par architecture
foreach ($arch in $architectures) {
    $stagingDir = Join-Path $projectRoot ".msix-staging-$arch"
    $publishExe = Join-Path $srcDir "bin\Release\net8.0-windows10.0.17763.0\win-$arch\publish\AZERTY Global.exe"
    $msixPath = Join-Path $projectRoot ("AZERTYGlobal-{0}-{1}.msix" -f $storeVersion, $arch)

    Write-Host "--- Construction $arch ---"

    # Nettoyer le staging
    if (Test-Path $stagingDir) {
        Remove-Item -LiteralPath $stagingDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $stagingDir | Out-Null

    # Copier le template msix/ (manifest, config) — Assets/ est exclu de la copie
    # récursive et repeuplé ci-dessous par la liste blanche $packagedAssets.
    Copy-DirectoryContent $msixDir $stagingDir
    $stagingAssets = Join-Path $stagingDir 'Assets'
    New-Item -ItemType Directory -Path $stagingAssets | Out-Null
    foreach ($asset in $packagedAssets) {
        $assetSource = Join-Path (Join-Path $msixDir 'Assets') $asset
        if (-not (Test-Path $assetSource)) {
            throw "Asset packagé introuvable: $assetSource (référencé par AppxManifest.xml)"
        }
        Copy-Item $assetSource (Join-Path $stagingAssets $asset) -Force
    }

    # Copier l'exécutable publié
    Copy-Item $publishExe (Join-Path $stagingDir 'AZERTY Global.exe') -Force

    # Ajuster ProcessorArchitecture dans le manifest copié
    $manifestPath = Join-Path $stagingDir 'AppxManifest.xml'
    [xml]$manifest = [System.IO.File]::ReadAllText($manifestPath)
    $manifest.Package.Identity.ProcessorArchitecture = $arch
    $manifest.Save($manifestPath)

    # Empaqueter
    if (Test-Path $msixPath) {
        Remove-Item -LiteralPath $msixPath -Force
    }
    & $makeAppx pack /d $stagingDir /p $msixPath
    if ($LASTEXITCODE -ne 0) { throw "MakeAppx pack a échoué pour $arch" }

    $msixFiles += $msixPath

    # Nettoyer le staging
    Remove-Item -LiteralPath $stagingDir -Recurse -Force
}

# Créer le bundle
Write-Host "--- Construction du bundle ---"
if (Test-Path $bundleStagingDir) {
    Remove-Item -LiteralPath $bundleStagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $bundleStagingDir | Out-Null

foreach ($msix in $msixFiles) {
    Copy-Item $msix $bundleStagingDir -Force
}

if (Test-Path $versionedBundlePath) {
    Remove-Item -LiteralPath $versionedBundlePath -Force
}

# /bv force la version du bundle ; sans ce flag, MakeAppx genere YYYY.MMdd.HHmm.0
# (timestamp du pack), ce qui ecrase la version manifest dans Partner Center.
& $makeAppx bundle /d $bundleStagingDir /p $versionedBundlePath /bv $storeVersion
if ($LASTEXITCODE -ne 0) { throw "MakeAppx bundle a échoué" }

# Archiver l'ancien bundle stable
if (Test-Path $stableBundlePath) {
    New-Item -ItemType Directory -Path $msixArchiveDir -Force | Out-Null
    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backupPath = Join-Path $msixArchiveDir ("AZERTYGlobal-{0}-stable-backup-{1}.msixbundle" -f $storeVersion, $timestamp)
    Move-Item -LiteralPath $stableBundlePath -Destination $backupPath -Force
}

# Copier le bundle versionné en stable
Copy-Item $versionedBundlePath $stableBundlePath -Force

# Nettoyer
Remove-Item -LiteralPath $bundleStagingDir -Recurse -Force
foreach ($msix in $msixFiles) {
    Remove-Item -LiteralPath $msix -Force
}

Write-Host ""
Write-Host "MSIX bundle reconstruit:"
Write-Host " - Versioned : $versionedBundlePath"
Write-Host " - Stable    : $stableBundlePath"
Write-Host " - Archs     : $($architectures -join ', ')"
