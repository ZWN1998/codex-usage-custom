param([string]$Version = '0.1.6')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$stage = Join-Path $repoRoot "artifacts\CodexUsage-Custom-$Version-windows-x64"
$manifest = Import-Csv -LiteralPath (Join-Path $stage 'SHA256SUMS.csv')
foreach ($entry in $manifest) {
    $file = [IO.Path]::GetFullPath((Join-Path $stage $entry.Path))
    if (-not $file.StartsWith(([IO.Path]::GetFullPath($stage).TrimEnd('\') + '\'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe manifest entry.' }
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.SHA256) { throw "Hash mismatch: $($entry.Path)" }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipPath = $stage + '.zip'
$zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $paths = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\','/') })
    foreach ($required in @('Install.cmd','Install-Windows.ps1','Uninstall-Windows.ps1',
        'plugins/codex-usage-custom/bin/win-x64/CodexUsage.Desktop.exe',
        'plugins/codex-usage-custom/scripts/hud.ps1','.agents/plugins/marketplace.json','SHA256SUMS.csv')) {
        if (-not ($paths | Where-Object { $_.EndsWith('/' + $required, [StringComparison]::OrdinalIgnoreCase) })) { throw "Missing ZIP entry: $required" }
    }
    if ($paths | Where-Object { $_ -match '/(\.tools|obj|osx-arm64|osx-x64)/|/(appearance|state|watcher-status)\.json$|\.pdb$' }) {
        throw 'Development, debug, user-state or non-Windows file in ZIP.'
    }
    $expectedZipHash = ((Get-Content -LiteralPath ($zipPath + '.sha256') -Raw).Trim() -split '\s+')[0]
    if ((Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash -ne $expectedZipHash) { throw 'ZIP hash mismatch.' }
    [pscustomobject]@{ ManifestFiles=$manifest.Count; ArchiveEntries=$paths.Count; ZipBytes=(Get-Item -LiteralPath $zipPath).Length; Integrity='passed' } | ConvertTo-Json
} finally { $zip.Dispose() }
