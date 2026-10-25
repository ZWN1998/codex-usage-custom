param(
    [Parameter(Mandatory)]
    [ValidateSet('Start', 'Stop', 'Status', 'Settings')]
    [string]$Action
)
$ErrorActionPreference = 'Stop'
$stateRoot = Join-Path $env:USERPROFILE '.codex\usage-custom-windows'
$installed = Join-Path $stateRoot 'package\plugins\codex-usage-custom\bin\win-x64\CodexUsage.Desktop.exe'
$bundled = Join-Path (Split-Path -Parent $PSScriptRoot) 'bin\win-x64\CodexUsage.Desktop.exe'
$helper = if (Test-Path -LiteralPath $installed -PathType Leaf) { $installed } else { $bundled }
if (-not (Test-Path -LiteralPath $helper -PathType Leaf)) { throw "Windows helper not found: $helper" }

if ($Action -eq 'Status') {
    $statusFile = Join-Path $stateRoot 'watcher-status.json'
    if (Test-Path -LiteralPath $statusFile) {
        $state = Get-Content -LiteralPath $statusFile -Encoding UTF8 -Raw | ConvertFrom-Json
        $watcher = Get-Process -Id $state.WatcherPid -ErrorAction SilentlyContinue
        $verified = $false
        if ($watcher) { try { $verified = $watcher.Path -eq $helper } catch {} }
        if ($verified) { $state | ConvertTo-Json; return }
    }
    Write-Output 'Codex Usage Custom watcher is not running.'
    return
}

# Dispatch through the real Explorer desktop, outside the calling Codex job.
# The watcher owns its Startup shortcut and handles repeated Start safely.
$shellApplication = New-Object -ComObject Shell.Application
$windows = $shellApplication.Windows()
$location = [object]0
$rootLocation = [object]$null
$desktopHandle = 0
$desktop = $windows.FindWindowSW([ref]$location, [ref]$rootLocation, 8, [ref]$desktopHandle, 1)
if ($null -eq $desktop) { throw 'Explorer desktop is unavailable. Run this on the signed-in local Windows desktop.' }
$arguments = switch ($Action) {
    'Start' { '--watch --enable-autostart' }
    'Stop' { '--stop' }
    'Settings' { '--settings' }
}
$desktop.Document.Application.ShellExecute($helper, $arguments, (Split-Path -Parent $helper), 'open', 0)
Write-Output "Codex Usage Custom $Action requested on the Windows desktop."
