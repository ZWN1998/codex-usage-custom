param([string]$Version = '0.1.6')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repoRoot 'artifacts'
$stage = Join-Path $artifacts ("CodexUsage-Custom-$Version-windows-x64")
if (Test-Path -LiteralPath $stage) { throw "Output already exists; choose a new output/version or archive it: $stage" }
$pluginStage = Join-Path $stage 'plugins\codex-usage-custom'
$runtime = Join-Path $pluginStage 'bin\win-x64'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
& dotnet publish (Join-Path $repoRoot 'src\CodexUsage.Desktop\CodexUsage.Desktop.csproj') `
    -c Release -r win-x64 --self-contained true -p:RuntimeIdentifiers=win-x64 `
    -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false -o $runtime
if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
Get-ChildItem -LiteralPath $runtime -Filter '*.pdb' -Recurse -File | ForEach-Object {
    if (-not $_.FullName.StartsWith(([IO.Path]::GetFullPath($runtime).TrimEnd('\') + '\'), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Debug file is outside the generated Windows runtime.'
    }
    Remove-Item -LiteralPath $_.FullName
}
foreach ($directory in @('.codex-plugin', 'assets', 'skills', 'notices')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $directory) -Destination $pluginStage -Recurse
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'plugin.json') -Destination $pluginStage
foreach ($notice in @('README.md', 'PRIVACY.md', 'ATTRIBUTION.md', 'README-Windows.md', 'WINDOWS-VALIDATION.md')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $notice) -Destination $pluginStage
}
$launcherStage = Join-Path $pluginStage 'scripts'
New-Item -ItemType Directory -Path $launcherStage -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'hud.ps1') -Destination $launcherStage
foreach ($script in @('Install-Windows.ps1', 'Uninstall-Windows.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $script) -Destination $stage
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'README-Windows.md') -Destination $stage
$marketplaceDirectory = Join-Path $stage '.agents\plugins'
New-Item -ItemType Directory -Path $marketplaceDirectory -Force | Out-Null
@{ name='codex-usage-custom-local'; interface=@{displayName='Codex Usage Custom'}; plugins=@(@{
    name='codex-usage-custom'; source=@{source='local';path='./plugins/codex-usage-custom'};
    policy=@{installation='AVAILABLE';authentication='ON_INSTALL'}; category='Productivity'
}) } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $marketplaceDirectory 'marketplace.json') -Encoding utf8
foreach ($action in @('Start','Settings')) {
    $content = '@echo off' + "`r`n" + 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0plugins\codex-usage-custom\scripts\hud.ps1" -Action ' + $action + "`r`n" + 'if errorlevel 1 pause' + "`r`n"
    [IO.File]::WriteAllText((Join-Path $stage ($action + '.cmd')), $content, [Text.Encoding]::ASCII)
}
$installCmd = '@echo off' + "`r`n" + 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Windows.ps1"' + "`r`n" + 'pause' + "`r`n"
[IO.File]::WriteAllText((Join-Path $stage 'Install.cmd'), $installCmd, [Text.Encoding]::ASCII)
Get-ChildItem -LiteralPath $stage -Recurse -File -Force | Where-Object { $_.Name -ne 'SHA256SUMS.csv' } | ForEach-Object {
    [pscustomobject]@{ Path=$_.FullName.Substring($stage.Length+1); SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
} | Export-Csv -LiteralPath (Join-Path $stage 'SHA256SUMS.csv') -NoTypeInformation -Encoding utf8
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = $stage + '.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($zip + '.sha256'), ($hash + '  ' + [IO.Path]::GetFileName($zip) + "`n"), [Text.Encoding]::ASCII)
Write-Output $zip
Write-Output "SHA256: $hash"
