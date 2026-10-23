---
name: codex-usage-hud
description: Start, stop, or check the customized native Codex Usage companion on a local macOS Apple silicon desktop.
---

Manage the native companion through `sh ../../scripts/hud.sh Start|Stop|Status`,
resolved relative to this skill directory. Restart means Stop then Start.

This package supports local macOS Apple silicon (arm64) only. Run Start/Restart
while the Codex desktop app is open. For Intel, Windows, Linux, web, mobile, cloud
or remote execution, explain that this package cannot run there; do not launch it.

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
