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

# Les tests créent de vraies fenêtres : elles vivent sur un bureau Win32 dédié, jamais à l'écran.
. (Join-Path $PSScriptRoot 'BureauCache.ps1')

# Audit du 30/10, M8 : un test ignoré ne prouve rien et ne doit pas se perdre dans un « 1401/1403 ».
# Seuls ces trois témoins de la vraie souris peuvent l'être : ils exigent le bureau d'entrée, que le
# bureau dédié ci-dessus n'est pas. Tout autre ignoré fait échouer l'étape.
$ignoresAttendus = @(
    'CurseurSimuleTests.UnClicheLaisseLaVraieSourisOuElleEtait',
    'CurseurSimuleTests.LesLeconsLaissentLaVraieSourisOuElleEtait',
    'CurseurSimuleTests.LaCoucheRendLaPositionDeLaVraieSouris'
)

function Invoke-Tests([string]$Projet, [string]$Filtre = '', [switch]$BureauCourant, [string[]]$Attendus = @()) {
    $arguments = @('test', $Projet, '-c', 'Release', '--logger', 'console;verbosity=normal')
    if ($Filtre) { $arguments += @('--filter', $Filtre) }
    $lieu = if ($BureauCourant) { "bureau d'entrée" } else { 'bureau dédié' }
    Write-Host "> dotnet $($arguments -join ' ') ($lieu)"
    $ErrorActionPreference = 'Continue'
    # La sortie est déjà affichée au fil de l'eau par Invoke-SurBureauCache.
    if ($BureauCourant) {
        $sortie = @(& dotnet @arguments 2>&1 | ForEach-Object { Write-Host "$_"; "$_" })
    } else {
        $sortie = @(Invoke-SurBureauCache dotnet $arguments)
    }
    $code = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    # Deux formats de résumé selon la verbosité du logger console :
    # minimal : « Passed!  - Failed: 0, Passed: 18, Skipped: 0, Total: 18 » (une ligne) ;
    # normal  : « Total tests: 18 » puis « Passed: 18 », « Failed: n », « Skipped: n » (lignes à zéro omises).
    $motif = '(?<etat>Passed|Failed)!\s+-\s+Failed:\s+(?<echecs>\d+),\s+Passed:\s+(?<reussis>\d+),\s+Skipped:\s+(?<ignores>\d+),\s+Total:\s+(?<total>\d+)'
    $compte = @{ Echecs = 0; Reussis = 0; Ignores = 0; Total = 0 }
    $resumes = 0
    $blocNormal = $false
    foreach ($ligne in $sortie) {
        $l = [string]$ligne
        $r = [regex]::Match($l, $motif)
        if ($r.Success) {
            $resumes++
            $blocNormal = $false
            $compte.Echecs += [int]$r.Groups['echecs'].Value
            $compte.Reussis += [int]$r.Groups['reussis'].Value
            $compte.Ignores += [int]$r.Groups['ignores'].Value
            $compte.Total += [int]$r.Groups['total'].Value
            continue
        }
        $t = [regex]::Match($l, '^\s*Total tests:\s*(?<n>\d+)\s*$')
        if ($t.Success) { $resumes++; $blocNormal = $true; $compte.Total += [int]$t.Groups['n'].Value; continue }
        if (-not $blocNormal) { continue }
        $c = [regex]::Match($l, '^\s*(?<cle>Passed|Failed|Skipped):\s*(?<n>\d+)\s*$')
        if ($c.Success) {
            switch ($c.Groups['cle'].Value) {
                'Passed'  { $compte.Reussis += [int]$c.Groups['n'].Value }
                'Failed'  { $compte.Echecs += [int]$c.Groups['n'].Value }
                'Skipped' { $compte.Ignores += [int]$c.Groups['n'].Value }
            }
        } elseif ($l -notmatch '^\s*$') {
            $blocNormal = $false
        }
    }
    if ($resumes -eq 0) { throw "résumé de dotnet test introuvable (code $code)" }
    $texte = "Passed $($compte.Reussis) / Failed $($compte.Echecs) / Skipped $($compte.Ignores) / Total $($compte.Total)"
    if ($code -ne 0 -or $compte.Echecs -gt 0) { throw "tests en échec : $texte (code $code)" }
    if ($compte.Total -eq 0) { throw "aucun test exécuté" }
    if ($compte.Ignores -gt 0) {
        $nomsIgnores = @($sortie | ForEach-Object { [regex]::Match([string]$_, '^\s*Skipped\s+(?<nom>\S+)') } |
            Where-Object Success | ForEach-Object { $_.Groups['nom'].Value })
        if ($nomsIgnores.Count -ne $compte.Ignores) {
            throw "$($compte.Ignores) test(s) ignoré(s), $($nomsIgnores.Count) nom(s) lu(s) : $texte"
        }
        $inattendus = @($nomsIgnores | Where-Object { $nom = $_; -not ($Attendus | Where-Object { $nom.EndsWith($_) }) })
        if ($inattendus.Count -gt 0) { throw "test(s) ignoré(s) : $($inattendus -join ', ')" }
    }
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
    'tests-windows'   = { Invoke-Tests 'src/TypingEngine.Windows.Tests/TypingEngine.Windows.Tests.csproj' 'Categorie!=EntreeReelle' }
    # M8 : vrai hook, vrai SendInput. Le bureau d'entrée est celui du poste : le test avale ses
    # propres frappes (hook « puits »), rien n'atteint la fenêtre au premier plan.
    'tests-entree-reelle' = { Invoke-Tests 'src/TypingEngine.Windows.Tests/TypingEngine.Windows.Tests.csproj' 'Categorie=EntreeReelle' -BureauCourant }
    'tests-app'       = { Invoke-Tests 'src/AZERTYGlobal.Tests/AZERTYGlobal.Tests.csproj' '' -Attendus $ignoresAttendus }
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
