param([string]$BundlePath, [string]$ArchiveRoot, [switch]$ReadVersion)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\Archive-StableBundle.ps1')
if ($ReadVersion) {
    Get-MSIXBundleVersion $BundlePath
}
else {
    Backup-StableMSIXBundle $BundlePath $ArchiveRoot | ConvertTo-Json -Compress
}

