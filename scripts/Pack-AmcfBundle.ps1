<#
Construit le bundle hors Store signe AMCF a partir du bundle Store produit par la CI.

Le bundle Store porte l'editeur Store (CN=7FD049E3-...). Artifact Signing signe au nom
de l'AMCF : signtool refuse un paquet dont l'editeur du manifeste differe du sujet du
certificat. On ne peut donc pas "signer une copie" du bundle Store (msix/README.md, section 8) :
on le deballe, on remplace l'editeur et son nom affiche dans chaque manifeste, on remballe.
Rien d'autre ne change : le script compare ensuite chaque fichier du paquet AMCF a celui du
paquet Store et echoue au premier ecart hors AppxManifest.xml.

Sans -Sign : produit AZERTYGlobal-<version>-amcf-unsigned.msixbundle (non distribuable).
Avec -Sign : produit aussi AZERTY_Global_<x.y.z>.msixbundle, signe par Artifact Signing
(compte amcfazerty, profil AZERTYGlobalPublicTrust, annuaire amcfazerty.onmicrosoft.com),
puis verifie la signature. La signature demande une connexion Azure de amcf@azerty.global.

Exemple :
  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Pack-AmcfBundle.ps1 `
    -Bundle <AZERTYGlobal-1.3.0.0.msixbundle de la CI> -ExpectedSha256 <SHA de la recette> `
    -OutDir <dossier vide> [-Sign]
#>
param(
    [Parameter(Mandatory = $true)] [string]$Bundle,
    [Parameter(Mandatory = $true)] [string]$ExpectedSha256,
    [Parameter(Mandatory = $true)] [string]$OutDir,
    [switch]$Sign,
    [string]$DlibPath = (Join-Path $env:LOCALAPPDATA 'Microsoft\MicrosoftArtifactSigningClientTools\Azure.CodeSigning.Dlib.dll'),
    [string]$Endpoint = 'https://weu.codesigning.azure.net',
    [string]$CodeSigningAccountName = 'amcfazerty',
    [string]$CertificateProfileName = 'AZERTYGlobalPublicTrust',
    [string]$TenantId = 'amcfazerty.onmicrosoft.com'
)

$ErrorActionPreference = 'Stop'

$storePublisher = 'CN=7FD049E3-1C58-42E0-A07F-A9712DE19E38'
$organization = 'Association pour la Modernisation du Clavier Fran' + [char]0x00E7 + 'ais'
$amcfPublisher = "CN=$organization, O=$organization, L=Clermont-Ferrand, S=Puy-de-D" + [char]0x00F4 + 'me, C=FR'
$amcfDisplayName = "$organization (AMCF)"
$footprint = @('AppxManifest.xml', 'AppxBlockMap.xml', 'AppxSignature.p7x', '[Content_Types].xml', 'AppxMetadata\CodeIntegrity.cat')

function Find-SdkTool([string]$name) {
    $tool = Get-ChildItem (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin') -Recurse -Filter $name -ErrorAction SilentlyContinue |
        Where-Object FullName -like "*\x64\$name" | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $tool) { throw "$name introuvable : installer le Windows SDK." }
    return $tool.FullName
}

function Invoke-Tool([string]$exe, [string[]]$arguments) {
    $output = & $exe @arguments 2>&1
    if ($LASTEXITCODE -ne 0) { $output | ForEach-Object { Write-Host $_ }; throw "$(Split-Path $exe -Leaf) a rendu $LASTEXITCODE" }
}

function Get-PayloadHashes([string]$root) {
    $table = @{}
    Get-ChildItem $root -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($root.Length).TrimStart('\')
        if ($footprint -notcontains $relative) { $table[$relative] = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash }
    }
    return $table
}

$Bundle = (Resolve-Path -LiteralPath $Bundle).Path
$actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $Bundle).Hash.ToLower()
if ($actual -ne $ExpectedSha256.ToLower()) { throw "SHA-256 du bundle Store : $actual, attendu $ExpectedSha256" }
Write-Host "Bundle Store : $Bundle"
Write-Host "  SHA-256 $actual (conforme)"

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path -LiteralPath $OutDir).Path
if (Get-ChildItem $OutDir -Force) { throw "Le dossier de sortie doit etre vide : $OutDir" }

$makeAppx = Find-SdkTool 'makeappx.exe'
$work = Join-Path ([IO.Path]::GetTempPath()) ('amcf-bundle-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $storeParts = Join-Path $work 'store-parts'
    Invoke-Tool $makeAppx @('unbundle', '/p', $Bundle, '/d', $storeParts, '/o')
    [xml]$bundleManifest = [IO.File]::ReadAllText((Join-Path $storeParts 'AppxMetadata\AppxBundleManifest.xml'))
    $version = $bundleManifest.Bundle.Identity.Version
    if ($bundleManifest.Bundle.Identity.Publisher -ne $storePublisher) { throw "Editeur inattendu dans le bundle : $($bundleManifest.Bundle.Identity.Publisher)" }
    Write-Host "  version $version"

    $amcfParts = Join-Path $work 'amcf-parts'
    New-Item -ItemType Directory -Path $amcfParts | Out-Null
    foreach ($part in Get-ChildItem $storeParts -Filter '*.msix') {
        $storeTree = Join-Path $work ('store-' + $part.BaseName)
        $amcfTree = Join-Path $work ('amcf-' + $part.BaseName)
        Invoke-Tool $makeAppx @('unpack', '/p', $part.FullName, '/d', $storeTree, '/o')
        Copy-Item $storeTree $amcfTree -Recurse
        foreach ($name in $footprint | Where-Object { $_ -ne 'AppxManifest.xml' }) {
            $path = Join-Path $amcfTree $name
            if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
        }

        $manifestPath = Join-Path $amcfTree 'AppxManifest.xml'
        [xml]$manifest = [IO.File]::ReadAllText($manifestPath)
        if ($manifest.Package.Identity.Publisher -ne $storePublisher) { throw "Editeur inattendu dans $($part.Name)" }
        $manifest.Package.Identity.Publisher = $amcfPublisher
        $manifest.Package.Properties.PublisherDisplayName = $amcfDisplayName
        $manifest.Save($manifestPath)

        $packed = Join-Path $amcfParts $part.Name
        Invoke-Tool $makeAppx @('pack', '/d', $amcfTree, '/p', $packed, '/o')

        # Preuve : le paquet AMCF remballe ne differe du paquet Store que par son manifeste.
        $check = Join-Path $work ('check-' + $part.BaseName)
        Invoke-Tool $makeAppx @('unpack', '/p', $packed, '/d', $check, '/o')
        $before = Get-PayloadHashes $storeTree
        $after = Get-PayloadHashes $check
        $diff = @(@($before.Keys) + @($after.Keys) | Sort-Object -Unique | Where-Object { $before[$_] -ne $after[$_] })
        if ($diff.Count -gt 0) { throw "Contenu different de $($part.Name) : $($diff -join ', ')" }
        [xml]$checked = [IO.File]::ReadAllText((Join-Path $check 'AppxManifest.xml'))
        if ($checked.Package.Identity.Publisher -ne $amcfPublisher) { throw "Editeur non remplace dans $($part.Name)" }
        Write-Host ("  {0} : {1} fichiers identiques au paquet Store, editeur AMCF" -f $part.Name, $before.Count)
    }

    $unsigned = Join-Path $OutDir "AZERTYGlobal-$version-amcf-unsigned.msixbundle"
    Invoke-Tool $makeAppx @('bundle', '/d', $amcfParts, '/p', $unsigned, '/bv', $version, '/o')
    Write-Host "Bundle AMCF non signe (non distribuable) : $unsigned"
    Write-Host ('  SHA-256 ' + (Get-FileHash -Algorithm SHA256 -LiteralPath $unsigned).Hash.ToLower())

    if ($Sign) {
        if (-not (Test-Path -LiteralPath $DlibPath)) { throw "Dlib Artifact Signing introuvable : $DlibPath (winget install -e --id Microsoft.Azure.ArtifactSigningClientTools)" }
        $signTool = Find-SdkTool 'signtool.exe'
        $semver = ($version -split '\.')[0..2] -join '.'
        $signed = Join-Path $OutDir "AZERTY_Global_$semver.msixbundle"
        Copy-Item $unsigned $signed
        $metadata = Join-Path $work 'metadata.json'
        [IO.File]::WriteAllText($metadata, (@{
            Endpoint = $Endpoint; CodeSigningAccountName = $CodeSigningAccountName
            CertificateProfileName = $CertificateProfileName; CorrelationId = "azerty-global-app-$version-amcf"
        } | ConvertTo-Json))
        $env:AZURE_TENANT_ID = $TenantId
        Invoke-Tool $signTool @('sign', '/v', '/fd', 'SHA256', '/tr', 'http://timestamp.acs.microsoft.com', '/td', 'SHA256', '/dlib', $DlibPath, '/dmdf', $metadata, $signed)
        Invoke-Tool $signTool @('verify', '/pa', '/all', $signed)
        $signature = Get-AuthenticodeSignature -LiteralPath $signed
        if ($signature.Status -ne 'Valid') { throw "Signature : $($signature.Status) ($($signature.StatusMessage))" }
        if ($signature.SignerCertificate.Subject -notlike "CN=$organization,*") { throw "Sujet inattendu : $($signature.SignerCertificate.Subject)" }
        Write-Host "Bundle AMCF signe : $signed"
        Write-Host "  signataire $($signature.SignerCertificate.Subject)"
        Write-Host "  horodatage $($signature.TimeStamperCertificate.Subject)"
        Write-Host ('  SHA-256 ' + (Get-FileHash -Algorithm SHA256 -LiteralPath $signed).Hash.ToLower())
    }
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
