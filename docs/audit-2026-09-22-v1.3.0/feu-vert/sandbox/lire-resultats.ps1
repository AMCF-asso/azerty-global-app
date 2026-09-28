# Poste hote. Juge auto\resultats.json de la recette automatique (R06, R07 et chaque ligne
# Rxx automatisee de docs/recette-2026-09-27-v1.3.0/RECETTE-1.3.0.md) contre le niveau
# attendu de chaque ligne, et ecrit resume-auto.txt a cote. lancer-recette.ps1 -Auto l'appelle.
#   powershell -ExecutionPolicy Bypass -File lire-resultats.ps1 -Dossier <...\recette-<12>\auto>
# Niveaux, du plus bas au plus haut : ECHEC, A VOIR, OK (partiel), OK. Une ligne au-dessus de
# son attendu est conforme. Verdict global : ECHEC si une ligne est en ECHEC ou absente,
# A VOIR si une ligne est sous son attendu sans echec, OK sinon. Code de retour 0 si OK.
param([Parameter(Mandatory = $true)][string]$Dossier)
$ErrorActionPreference = 'Stop'

$rank = @{ 'ECHEC' = 0; 'A VOIR' = 1; 'OK (partiel)' = 2; 'OK' = 3 }
# Ordre d'execution de recette-auto.ps1. OK (partiel) : une partie de la ligne reste a
# l'oeil, au banc de captures ou a un test unitaire, et l'observation dit laquelle.
$expected = [ordered]@{
    'A1' = 'OK'; 'A2' = 'OK'; 'A3' = 'OK'; 'A10' = 'OK'; 'B12' = 'OK'
    'R37' = 'OK (partiel)'; 'R40' = 'OK (partiel)'; 'A5' = 'OK (partiel)'
    'R57b' = 'OK (partiel)'; 'R57' = 'OK (partiel)'
    'R47' = 'OK'; 'R45' = 'OK (partiel)'; 'R49' = 'OK (partiel)'; 'R58' = 'OK'; 'R60' = 'OK (partiel)'
    'R20' = 'OK (partiel)'; 'R22' = 'OK'; 'R23' = 'OK'; 'R24' = 'OK'; 'R25' = 'OK'; 'R26' = 'OK'
    'R39' = 'OK (partiel)'; 'R41' = 'OK'; 'R71' = 'OK (partiel)'; 'R70' = 'OK'; 'R72' = 'OK (partiel)'; 'R73' = 'OK'
    'R74' = 'OK (partiel)'; 'A11' = 'OK (partiel)'; 'R75' = 'OK (partiel)'; 'A12' = 'OK'
}

$file = Join-Path $Dossier 'resultats.json'
if (-not (Test-Path $file)) { "resultats.json absent dans $Dossier : verdict global ECHEC"; exit 1 }
$byId = [ordered]@{}
# Windows PowerShell 5.1 rend un tableau JSON comme un seul objet du pipeline : affecter, puis enumerer.
$parsed = Get-Content $file -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($r in $parsed) { $byId[[string]$r.ligne] = $r }

$rows = @(); $echec = 0; $avoir = 0
foreach ($id in $expected.Keys) {
    $want = $expected[$id]
    if ($byId.Contains($id)) {
        $got = [string]$byId[$id].verdict; $note = [string]$byId[$id].observation
        $g = if ($rank.ContainsKey($got)) { $rank[$got] } else { 0 }
    } else { $got = 'ABSENTE'; $note = ''; $g = -1 }
    $state = if ($g -ge $rank[$want]) { 'conforme' } elseif ($g -le 0) { 'ECHEC' } else { 'A VOIR' }
    if ($state -eq 'ECHEC') { $echec++ } elseif ($state -eq 'A VOIR') { $avoir++ }
    $rows += [pscustomobject]@{ ligne = $id; attendu = $want; verdict = $got; etat = $state; observation = $note }
}
# Lignes inattendues : un bloc tombe en exception rend un identifiant compose (R45/R47/R49).
foreach ($id in $byId.Keys) {
    if (-not $expected.Contains($id)) {
        $got = [string]$byId[$id].verdict
        $state = if ($got -eq 'ECHEC') { 'ECHEC' } else { 'A VOIR' }
        if ($state -eq 'ECHEC') { $echec++ } else { $avoir++ }
        $rows += [pscustomobject]@{ ligne = $id; attendu = '-'; verdict = $got; etat = $state; observation = [string]$byId[$id].observation }
    }
}
$global = if ($echec) { 'ECHEC' } elseif ($avoir) { 'A VOIR' } else { 'OK' }

$report = @(('Recette automatique : ' + $file), ('{0} lignes attendues ; conformes : {1} ; A VOIR : {2} ; ECHEC : {3}' -f $expected.Count, @($rows | Where-Object { $_.etat -eq 'conforme' }).Count, $avoir, $echec), '')
foreach ($r in $rows) {
    $report += ('{0,-6} {1,-9} attendu {2,-13} rendu {3}' -f $r.ligne, $r.etat, $r.attendu, $r.verdict)
    if ($r.etat -ne 'conforme' -and $r.observation) { $report += ('       ' + $r.observation) }
}
$report += @('', ('Verdict global : ' + $global))
Set-Content -Path (Join-Path $Dossier 'resume-auto.txt') -Value $report -Encoding UTF8
$report
if ($global -eq 'OK') { exit 0 } else { exit 1 }
