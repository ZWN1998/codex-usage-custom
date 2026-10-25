param([switch]$NoAutostart, [switch]$SkipPluginRegistration)

$ErrorActionPreference = 'Stop'
$packageRoot = $PSScriptRoot
$sourcePlugin = Join-Path $packageRoot 'plugins\codex-usage-custom'
$sourceHelper = Join-Path $sourcePlugin 'bin\win-x64\CodexUsage.Desktop.exe'
if (-not (Test-Path -LiteralPath $sourceHelper -PathType Leaf)) {
    throw 'Extract the complete Windows release ZIP first. This script must be next to plugins\codex-usage-custom.'
}
$manifest = Get-Content -LiteralPath (Join-Path $sourcePlugin '.codex-plugin\plugin.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if ($manifest.name -ne 'codex-usage-custom') { throw 'Unexpected plugin manifest.' }
$stateRoot = Join-Path $env:USERPROFILE '.codex\usage-custom-windows'
$installedPackage = Join-Path $stateRoot 'package'
$installedHelper = Join-Path $installedPackage 'plugins\codex-usage-custom\bin\win-x64\CodexUsage.Desktop.exe'

function Find-CodexExecutable {
    if ($env:CODEX_HUD_CODEX_PATH -and (Test-Path -LiteralPath $env:CODEX_HUD_CODEX_PATH -PathType Leaf)) {
        return $env:CODEX_HUD_CODEX_PATH
    }
    $command = Get-Command codex.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $package = Get-AppxPackage -Name OpenAI.Codex -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($package) {
        $resources = Join-Path $package.InstallLocation 'app\resources'
        if (Test-Path -LiteralPath $resources -PathType Container) {
            $candidate = Get-ChildItem -LiteralPath $resources -Filter codex.exe -Recurse -File -ErrorAction SilentlyContinue |
                Select-Object -First 1 -ExpandProperty FullName
            if ($candidate) { return $candidate }
        }
    }
    $binRoot = Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\bin'
    if (Test-Path -LiteralPath $binRoot -PathType Container) {
        return Get-ChildItem -LiteralPath $binRoot -Filter codex.exe -Recurse -File |
            Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
    return $null
}

$codexExecutable = Find-CodexExecutable
if (-not $SkipPluginRegistration -and -not $codexExecutable) {
    throw 'Codex CLI not found. Set CODEX_HUD_CODEX_PATH to its full executable path, then retry.'
}
New-Item -ItemType Directory -Path $stateRoot -Force | Out-Null

# Stop this product through its named session signals; do not kill all codex.exe processes.
if (Test-Path -LiteralPath $installedHelper -PathType Leaf) {
    $stopper = Start-Process -FilePath $installedHelper -ArgumentList '--stop' -WindowStyle Hidden -PassThru
    $stopper.WaitForExit()
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $remaining = @(Get-Process -Name CodexUsage.Desktop -ErrorAction SilentlyContinue | Where-Object {
            try { $_.Path -eq $installedHelper } catch { $false }
        })
        if ($remaining.Count -eq 0) { break }
        Start-Sleep -Milliseconds 500
    }
    if ($remaining.Count -gt 0) { throw 'The existing version is still exiting. Leave the package in place and retry installation shortly.' }
}

# Preserve an existing package for rollback, without recursively deleting it.
$sourceFull = [IO.Path]::GetFullPath($packageRoot).TrimEnd('\')
$targetFull = [IO.Path]::GetFullPath($installedPackage).TrimEnd('\')
if ($sourceFull -ne $targetFull) {
    if (Test-Path -LiteralPath $installedPackage) {
        $resolved = (Resolve-Path -LiteralPath $installedPackage).Path
        if (-not $resolved.StartsWith(([IO.Path]::GetFullPath($stateRoot).TrimEnd('\') + '\'), [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Rollback path is outside this product directory.'
        }
        $backup = Join-Path $stateRoot ('package-before-' + (Get-Date -Format 'yyyyMMddHHmmssfff'))
        Move-Item -LiteralPath $resolved -Destination $backup
    }
    New-Item -ItemType Directory -Path $installedPackage -Force | Out-Null
    foreach ($entry in @('plugins', '.agents', 'Install-Windows.ps1', 'Uninstall-Windows.ps1', 'Start.cmd', 'Settings.cmd', 'README-Windows.md', 'SHA256SUMS.csv')) {
        $source = Join-Path $packageRoot $entry
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $installedPackage -Recurse -Force }
    }
}
if ($codexExecutable) {
    [IO.File]::WriteAllText((Join-Path $stateRoot 'codex-path.txt'), $codexExecutable, [Text.UTF8Encoding]::new($false))
}

if (-not $SkipPluginRegistration) {
    # Register only this custom marketplace. Leave the original marketplace installed for rollback.
    $previousPreference = $ErrorActionPreference
    try {
        # Missing registrations are expected on first install. PowerShell 5.1
        # surfaces native stderr as errors even when the stream is redirected.
        $ErrorActionPreference = 'SilentlyContinue'
        & $codexExecutable plugin remove codex-usage-custom@codex-usage-custom-local *> $null
        & $codexExecutable plugin marketplace remove codex-usage-custom-local *> $null
    } finally { $ErrorActionPreference = $previousPreference }
    & $codexExecutable plugin marketplace add $installedPackage
    if ($LASTEXITCODE -ne 0) { throw 'Could not register the custom marketplace. Resolve the CLI error, then retry.' }
    & $codexExecutable plugin add codex-usage-custom@codex-usage-custom-local
    if ($LASTEXITCODE -ne 0) { throw 'Could not install the custom plugin. Resolve the CLI error, then retry.' }
}

# Retire only the known legacy listener after the new package has been registered.
$legacy = Join-Path $env:USERPROFILE '.codex\usage-autostart'
$legacyListener = Join-Path $legacy 'Listen-Codex.vbs'
$currentSession = (Get-Process -Id $PID).SessionId
if (Test-Path -LiteralPath $legacyListener -PathType Leaf) {
    $legacyFlag = Join-Path $legacy 'listener-disabled'
    [IO.File]::WriteAllText($legacyFlag, 'disabled by Windows custom installer')
    Get-CimInstance Win32_Process -Filter "Name='wscript.exe' OR Name='cscript.exe'" |
        Where-Object { $_.SessionId -eq $currentSession -and $_.CommandLine -and $_.CommandLine.IndexOf($legacyListener, [StringComparison]::OrdinalIgnoreCase) -ge 0 } |
        ForEach-Object { Invoke-CimMethod -InputObject $_ -MethodName Terminate -ErrorAction SilentlyContinue | Out-Null }
    $legacyLink = Join-Path ([Environment]::GetFolderPath('Startup')) 'Codex Usage Listener.lnk'
    if (Test-Path -LiteralPath $legacyLink -PathType Leaf) {
        Copy-Item -LiteralPath $legacyLink -Destination (Join-Path $stateRoot 'legacy-startup-before-install.lnk') -Force
        Remove-Item -LiteralPath $legacyLink
    }
}
$legacyCache = [IO.Path]::GetFullPath((Join-Path $env:USERPROFILE '.codex\plugins\cache\codex-usage-local\codex-usage')).TrimEnd('\') + '\'
Get-Process -Name CodexUsage.Desktop -ErrorAction SilentlyContinue | ForEach-Object {
    try { if ($_.SessionId -eq $currentSession -and $_.Path.StartsWith($legacyCache, [StringComparison]::OrdinalIgnoreCase)) { Stop-Process -Id $_.Id } } catch {}
}

if ($NoAutostart) {
    $disable = Start-Process -FilePath $installedHelper -ArgumentList '--disable-autostart' -WindowStyle Hidden -PassThru
    $disable.WaitForExit()
    & (Join-Path $installedPackage 'plugins\codex-usage-custom\scripts\hud.ps1') -Action Settings
} else {
    & (Join-Path $installedPackage 'plugins\codex-usage-custom\scripts\hud.ps1') -Action Start
}
Write-Output "Installed Windows version $($manifest.version) at $installedPackage"
Write-Output 'Open Codex from its usual Start menu entry. Right-click the HUD to change position, font and color.'
Write-Output 'Check watcher-status.json and the real Startup shortcut after installation. Reboot verification is a separate step.'
