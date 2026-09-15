# Lecture de la version embarquée et sauvegarde vérifiée du bundle précédent.
function Get-MSIXBundleVersion([string]$BundlePath) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $bundle = [System.IO.Compression.ZipFile]::OpenRead($BundlePath)
    try {
        $entries = @($bundle.Entries | Where-Object { $_.FullName -ceq 'AppxMetadata/AppxBundleManifest.xml' })
        if ($entries.Count -ne 1) { throw 'Le bundle doit contenir un manifeste unique.' }
        $stream = $entries[0].Open()
        $settings = [System.Xml.XmlReaderSettings]::new()
        $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
        $settings.XmlResolver = $null
        $reader = [System.Xml.XmlReader]::Create($stream, $settings)
        try {
            $manifest = [System.Xml.XmlDocument]::new()
            $manifest.XmlResolver = $null
            $manifest.Load($reader)
            $ns = [System.Xml.XmlNamespaceManager]::new($manifest.NameTable)
            $ns.AddNamespace('b', 'http://schemas.microsoft.com/appx/2013/bundle')
            $identities = $manifest.SelectNodes('/b:Bundle/b:Identity', $ns)
            if ($identities.Count -ne 1) { throw 'Identité du bundle absente ou ambiguë.' }
            $rawVersion = $identities[0].GetAttribute('Version')
            if ($rawVersion -notmatch '^\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}$') {
                throw 'Version du bundle invalide.'
            }
            foreach ($part in $rawVersion.Split('.')) {
                if ([int]$part -gt 65535) { throw 'Version du bundle hors limites MSIX.' }
            }
            return ([version]$rawVersion).ToString(4)
        }
        finally {
            $reader.Dispose()
            $stream.Dispose()
        }
    }
    finally { $bundle.Dispose() }
}

function Backup-StableMSIXBundle([string]$BundlePath, [string]$ArchiveRoot) {
    $source = (Get-Item -LiteralPath $BundlePath -ErrorAction Stop).FullName
    $oldVersion = Get-MSIXBundleVersion $source
    $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    $root = [System.IO.Path]::GetFullPath($ArchiveRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    $archiveDirectory = [System.IO.Path]::GetFullPath((Join-Path (Join-Path $root 'by-version') $oldVersion))
    if (-not $archiveDirectory.StartsWith($root + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Le chemin de sauvegarde sort du dossier autorisé.'
    }
    $suffix = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N')
    $backupPath = Join-Path $archiveDirectory ("AZERTYGlobal-{0}-stable-backup-{1}.msixbundle" -f $oldVersion, $suffix)
    New-Item -ItemType Directory -Path $archiveDirectory -Force | Out-Null
    # Garder le stable disponible jusqu'à la copie du nouveau bundle prêt.
    Copy-Item -LiteralPath $source -Destination $backupPath -ErrorAction Stop
    $backupHash = (Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash
    if ($backupHash -cne $sourceHash) { throw 'La sauvegarde du bundle précédent ne correspond pas à sa source.' }
    $proof = [pscustomobject]@{ Version = $oldVersion; SHA256 = $backupHash; Path = $backupPath }
    [System.IO.File]::WriteAllText($backupPath + '.json', ($proof | ConvertTo-Json), [System.Text.UTF8Encoding]::new($false))
    return $proof
}

