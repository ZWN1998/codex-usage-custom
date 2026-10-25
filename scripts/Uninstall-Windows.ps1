param([switch]$KeepPluginRegistration)
$ErrorActionPreference = 'Stop'
$root = Join-Path $env:USERPROFILE '.codex\usage-custom-windows'
$helper = Join-Path $root 'package\plugins\codex-usage-custom\bin\win-x64\CodexUsage.Desktop.exe'
if (Test-Path -LiteralPath $helper -PathType Leaf) {
    foreach ($argument in @('--disable-autostart', '--stop')) {
        $process = Start-Process -FilePath $helper -ArgumentList $argument -WindowStyle Hidden -PassThru
        $process.WaitForExit()
    }
}
$flag = Join-Path $root 'autostart-enabled'
if (Test-Path -LiteralPath $flag -PathType Leaf) { Remove-Item -LiteralPath $flag }
$link = Join-Path ([Environment]::GetFolderPath('Startup')) 'Codex Usage Custom.lnk'
if (Test-Path -LiteralPath $link -PathType Leaf) { Remove-Item -LiteralPath $link }
if (-not $KeepPluginRegistration) {
    $pathFile = Join-Path $root 'codex-path.txt'
    $command = Get-Command codex.exe -ErrorAction SilentlyContinue
    $cli = if ($command) { $command.Source } elseif (Test-Path -LiteralPath $pathFile) { (Get-Content -LiteralPath $pathFile -Encoding UTF8 -Raw).Trim() } else { $null }
    if ($cli -and (Test-Path -LiteralPath $cli -PathType Leaf)) {
        & $cli plugin remove codex-usage-custom@codex-usage-custom-local
        if ($LASTEXITCODE -ne 0) { throw 'Plugin removal failed; retry with the current Codex CLI.' }
        & $cli plugin marketplace remove codex-usage-custom-local
        if ($LASTEXITCODE -ne 0) { throw 'Marketplace removal failed; retry with the current Codex CLI.' }
    } else { Write-Warning 'CLI not found. Remove codex-usage-custom and its local marketplace from Codex manually.' }
}
Write-Output 'Autostart stopped and removed. Package, rollback copies and preferences remain in:'
Write-Output $root
Write-Output 'After all processes exit you can delete this product directory manually. The old listener is not automatically re-enabled.'
