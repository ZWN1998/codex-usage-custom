---
name: codex-usage-hud
description: Start, stop, check, or configure the customized Codex Usage companion on a local Windows x64 desktop or a separately packaged macOS Apple silicon desktop.
---

Resolve launchers relative to this skill directory. On Windows x64 run
`../../scripts/hud.ps1 -Action Start|Stop|Status|Settings` directly in the current
PowerShell host. The self-contained helper needs no .NET SDK. Start enables the
native per-user login watcher and dispatches through the real Explorer desktop;
the watcher displays the HUD while Codex runs. Stop stops this session; disable
the login setting through the HUD or the uninstall script to persistently stop it.
Settings opens or focuses appearance settings. Restart means Stop then Start.

The Windows release contains only win-x64. For a separate macOS arm64 release,
use `sh ../../scripts/hud.sh Start|Stop|Status` when that launcher and native Mac
bundle are present. Never launch a Windows executable on Mac or claim that source
files alone provide a Mac binary. Intel Mac, Linux, web, mobile, cloud and remote
execution are outside the published packages. GUI starts require a local signed-in
desktop and the platform's normal host approval when required.

For Start, invoke the launcher immediately. Do not inspect the environment first
or issue a separate Status after a successful Start. Diagnose only a failure or
an ambiguous result. For Restart, Stop then Start immediately.

Right-click the overlay and choose “位置、字号与颜色…” to adjust location, offsets,
direction, font size (8–24), presets, hex color or RGB. Changes apply immediately
and save locally. Auto direction stacks text upright at LeftCenter and uses one
horizontal line at other positions. Hover shows full reset dates.

Do not read, display or transmit Codex credentials or send local usage to third
parties. The companion reads account limits through the local Codex app server;
its other request is the anonymous public codex-resets.com status endpoint.
It is a separate native process and does not modify the Codex application.
