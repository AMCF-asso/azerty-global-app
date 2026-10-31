# Apercu rapide de l'interface : exe Debug sans hook clavier, hot reload.
# Usage : powershell -File scripts\dev-preview.ps1 [-Windows vkbd,lessons,settings,about]
# Debug seulement : le hook et l'ouverture des fenetres sont compiles hors Release.
param(
    [string]$Windows = 'vkbd,lessons,settings,about'
)
$project = Join-Path $PSScriptRoot '..\src\AZERTYGlobal.csproj'
$env:AG_DEV_PREVIEW = $Windows
dotnet watch run --project $project -c Debug --non-interactive --property:PublishAot=false
