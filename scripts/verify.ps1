<#
.SYNOPSIS
    Vérification locale : les étapes bloquantes de la CI exécutables sur Windows.

.DESCRIPTION
    Chaque étape porte exactement la commande que la CI exécute : le job build de ci.yml
    appelle ce script étape par étape (`-Etape`), et le hook .githooks/pre-push l'appelle
    en entier avant chaque push. Restent hors du script, dans la CI seule : l'installation
    des outils (Python, .NET, jsonschema), le job provenance (réseau, non bloquant),
    l'empaquetage MSIX, Verify-Release et BinSkim (artefacts de release, téléchargement de
    BinSkim), les empreintes, les uploads et l'attestation.

    Un test n'est compté réussi que si son résumé est lu : « Total » à zéro ou résumé
    absent font échouer l'étape, même quand dotnet rend 0.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1

.EXAMPLE
    pwsh ./scripts/verify.ps1 -Etape tests-app
#>
param(
    # tout, ou une liste séparée par des virgules parmi les clés de $etapes ci-dessous.
    [string[]]$Etape = @('tout')
)

$ErrorActionPreference = 'Stop'
$racine = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $racine
# Résumés de dotnet test en anglais quelle que soit la langue du poste : ils sont lus.
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:DOTNET_NOLOGO = '1'
# L'édition de liens native AOT exige vswhere.exe dans le PATH, sinon MSB3073 (README).
$installeurVs = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer'
if (-not (Get-Command vswhere.exe -ErrorAction SilentlyContinue) -and (Test-Path $installeurVs)) {
    $env:PATH = "$env:PATH;$installeurVs"
}

# Sous Windows PowerShell 5.1, une ligne sur stderr d'un programme peut devenir une
# erreur PowerShell et, avec Stop, interrompre l'étape : seul le code de sortie compte.
function Invoke-Commande([string]$Programme, [string[]]$Arguments) {
    Write-Host "> $Programme $($Arguments -join ' ')"
    $ErrorActionPreference = 'Continue'
    & $Programme @Arguments | Out-Host
    $code = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($code -ne 0) { throw "$Programme a rendu $code" }
}

function Invoke-Tests([string]$Projet) {
    Write-Host "> dotnet test $Projet -c Release"
    $ErrorActionPreference = 'Continue'
    & dotnet test $Projet -c Release | Tee-Object -Variable sortie | Out-Host
    $code = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    $motif = '(?<etat>Passed|Failed)!\s+-\s+Failed:\s+(?<echecs>\d+),\s+Passed:\s+(?<reussis>\d+),\s+Skipped:\s+(?<ignores>\d+),\s+Total:\s+(?<total>\d+)'
    $resumes = @($sortie | ForEach-Object { [regex]::Match([string]$_, $motif) } | Where-Object Success)
    if ($resumes.Count -eq 0) { throw "résumé de dotnet test introuvable (code $code)" }
    $compte = @{ Echecs = 0; Reussis = 0; Ignores = 0; Total = 0 }
    foreach ($r in $resumes) {
        $compte.Echecs += [int]$r.Groups['echecs'].Value
        $compte.Reussis += [int]$r.Groups['reussis'].Value
        $compte.Ignores += [int]$r.Groups['ignores'].Value
        $compte.Total += [int]$r.Groups['total'].Value
    }
    $texte = "Passed $($compte.Reussis) / Failed $($compte.Echecs) / Skipped $($compte.Ignores) / Total $($compte.Total)"
    if ($code -ne 0 -or $compte.Echecs -gt 0) { throw "tests en échec : $texte (code $code)" }
    if ($compte.Total -eq 0) { throw "aucun test exécuté" }
    return $texte
}

$etapes = [ordered]@{
    'identite'        = { Invoke-Commande python @('scripts/list-identity-literals.py') }
    'schema'          = { Invoke-Commande python @('scripts/validate-layout.py') }
    'temoins'         = { Invoke-Commande python @('-m', 'unittest', 'discover', '-s', 'scripts/tests', '-v') }
    'store-analytics' = {
        # Même suite que l'étape « Tester » de store-analytics.yml, sans installer le paquet.
        $precedent = $env:PYTHONPATH
        $env:PYTHONPATH = Join-Path $racine 'store-analytics/src'
        try { Invoke-Commande python @('-m', 'unittest', 'discover', '-s', 'store-analytics/tests') }
        finally { $env:PYTHONPATH = $precedent }
    }
    'publish-x64'     = { Invoke-Commande dotnet @('publish', 'src/AZERTYGlobal.csproj', '-c', 'Release', '-r', 'win-x64') }
    'publish-arm64'   = { Invoke-Commande dotnet @('publish', 'src/AZERTYGlobal.csproj', '-c', 'Release', '-r', 'win-arm64') }
    'tests-core'      = { Invoke-Tests 'src/TypingEngine.Core.Tests/TypingEngine.Core.Tests.csproj' }
    'tests-windows'   = { Invoke-Tests 'src/TypingEngine.Windows.Tests/TypingEngine.Windows.Tests.csproj' }
    'tests-app'       = { Invoke-Tests 'src/AZERTYGlobal.Tests/AZERTYGlobal.Tests.csproj' }
}

# `-File` passe « a,b » comme une seule chaîne : on découpe.
$demandees = @($Etape | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$choisies = if ($demandees -contains 'tout') { @($etapes.Keys) } else { $demandees }
$inconnues = @($choisies | Where-Object { -not $etapes.Contains($_) })
if ($inconnues.Count -gt 0) {
    Write-Host "Étape inconnue : $($inconnues -join ', '). Étapes : tout, $(@($etapes.Keys) -join ', ')."
    exit 2
}
$bilan = @()
$echec = $null
foreach ($nom in $choisies) {
    Write-Host ""
    Write-Host "=== $nom ==="
    $chrono = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $detail = & $etapes[$nom]
        $bilan += [pscustomobject]@{ Etape = $nom; Resultat = 'ok'; Duree = [int]$chrono.Elapsed.TotalSeconds; Detail = "$detail" }
    }
    catch {
        $bilan += [pscustomobject]@{ Etape = $nom; Resultat = 'ÉCHEC'; Duree = [int]$chrono.Elapsed.TotalSeconds; Detail = "$($_.Exception.Message)" }
        $echec = $nom
        break
    }
}

Write-Host ""
Write-Host "=== Bilan ==="
$bilan | ForEach-Object { Write-Host ("{0,-16} {1,-6} {2,5} s  {3}" -f $_.Etape, $_.Resultat, $_.Duree, $_.Detail) }
if ($echec) {
    Write-Host "Vérification en échec à l'étape « $echec »."
    exit 1
}
Write-Host "Vérification réussie ($($bilan.Count) étape(s))."
exit 0
