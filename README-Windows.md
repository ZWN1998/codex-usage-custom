# Codex Usage Custom 0.1.6 · Windows x64

这是基于本仓库 v0.1.5 的 Windows 适配版。现有 Mac Apple silicon v0.1.5 保持原发布；本包只包含 Windows x64 运行时。

## 外观与设置

默认顶部居中、11 号蓝灰色 `#8B99A6`，透明无框、单行显示：

`5小时 94% · 4小时51分钟后重置 · 每周 97% · 6天15小时后重置`，末尾为小刷新图标。

百分比和时间来自自己的 Codex 账号；以上只是格式示例。鼠标悬停查看具体重置日期、时刻、时区与最后更新时间。右键 → **位置、字号与颜色…**：

- 顶部居中、顶部靠左、顶部靠右、左侧居中、左下角、右下角。
- 横纵偏移、自动/横排/竖排；自动模式仅左侧居中使用正向竖排。
- 8–24 字号、预设颜色、十六进制与 RGB 自定义颜色，即时生效并保存。
- “恢复默认”恢复 Windows 的单行顶部默认值。小窗口显示不下时精简倒计时，完整信息仍可悬停查看。

## 安装

需要 Windows 10/11 x64、已登录的本地 Codex 桌面，以及支持插件安装命令的 Codex CLI。自带运行时，使用者无需安装 .NET SDK。

1. 下载 Windows ZIP 并完整解压；不要只复制其中的 exe。
2. 从资源管理器双击 **Install.cmd**，或在开始菜单单独打开的 Windows PowerShell 中运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-Windows.ps1`。
3. 使用当前 Windows 登录用户安装，不必切换到其他管理员账号。安装器将程序复制到 `%USERPROFILE%\.codex\usage-custom-windows\package` 并登记本地插件。安装完成后原下载解压目录可以移除，安装目标目录需要保留。
4. 继续通过原来的开始菜单入口打开 Codex。后台管理器自动跟随启动浮窗；新聊天中也可使用 `@Codex Usage Custom`。

安装器自动寻找 PATH、已安装 Windows Codex 包和传统安装目录中的 CLI。若未找到，在安装终端临时设置 `$env:CODEX_HUD_CODEX_PATH = '实际的 codex.exe 路径'` 后重试。该配置只指向程序路径，不涉及复制账号凭据。

`-SkipPluginRegistration` 可跳过聊天插件注册，仍可直接运行原生浮窗。`-NoAutostart` 可只打开设置与浮窗而不登记登录联动。

从 Codex 内部直接创建 AppData 启动项可能遇到 MSIX 重定向，因此请按上述桌面安装入口运行。发行程序没有 Developer ID / Windows 代码签名承诺；Windows 下载来源提示由实际分发签名状态决定。

## 自动联动与退出

默认使用真实当前用户 Startup 文件夹里的 **Codex Usage Custom.lnk**，启动目标为安装后的原生程序 `--watch`。不使用任务计划、不替换 Codex 的启动快捷方式，也不依赖 VBScript 监听器。

管理器每约 3 秒检查 Codex：发现后启动一份 HUD；Codex 完全退出时关闭它管理的 HUD，应用重启或再次打开时恢复。电脑重启后登录当前用户，管理器继续等待 Codex。

右键“退出工具”尊重本次退出，不会在同一个 Codex 进程里立即拉起；下次 Codex 完全退出再打开后恢复。取消“跟随 Codex 自动运行”删除本工具登录项并停止管理器，保留当前浮窗。重新勾选可以恢复。

`Start.cmd` 显式恢复自动联动，`Settings.cmd` 打开或唤起设置。重复运行不会重复创建 HUD 或管理器。

## 迁移当前旧 Windows 版本

新包和 CLI 注册成功后，安装器才停用已知旧 `%USERPROFILE%\.codex\usage-autostart\Listen-Codex.vbs` 以及旧插件路径下的 HUD，避免重复。旧插件市场保留，以便回退；不清理其他第三方启动项。

已安装的新包在升级前会保存到 `package-before-时间戳`，已知旧 Startup 快捷方式备份到 `legacy-startup-before-install.lnk`。升级应在源包解压目录运行安装脚本，文件占用报错时先停止工具再重试。

## 状态与手动验收

执行 `powershell.exe -NoProfile -File .\plugins\codex-usage-custom\scripts\hud.ps1 -Action Status` 查看管理器状态；用户数据位于 `%USERPROFILE%\.codex\usage-custom-windows`，包括外观、已选 CLI 路径、额度缓存、`watcher-status.json`、`window-status.json` 和 `layout-status.json`。它们用于区分“管理器启动”和“浮窗实际找到目标窗口”。

安装后需依次确认：单行样式与悬停、六种位置、横竖排、8 与 24 字号、颜色和重开保存、刷新、不同缩放/多屏、最小化、两个 Codex 窗口切换、主动退出、Codex 内重启、完全退出再打开，以及整机重启登录后的真实 Startup 项和恢复结果。

具体构建、运行和重启验证结果应记录在对应 Release 中。源码存在、打包成功与整机重启验证是不同结论，不应相互替代。

## 卸载与回退

在资源管理器打开安装目标目录，用独立 PowerShell 运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Uninstall-Windows.ps1`。它停止本工具、删除登录项、移除 Custom 插件注册，保留程序备份和偏好以供回退。确认所有进程退出后，可手工删除 `%USERPROFILE%\.codex\usage-custom-windows`。

卸载不会自动重启旧版。回退旧监听方案时，在真实桌面环境删除旧 `listener-disabled` 标记并恢复已备份的旧快捷方式；恢复后独立确认 Startup 目标与旧工具运行结果。

## 构建与发布（仅在授权后执行）

开发者需要 .NET 10 SDK。Windows 构建与打包脚本：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-windows.ps1
```

它只发布 `win-x64`，生成完整 ZIP、包内 SHA-256 清单和 ZIP 校验文件，不生成或覆盖 Mac 二进制。SHA-256 用于文件完整性，不等于代码签名或界面验收。

保留上游与本仓库现有来源、隐私及许可声明。工具不读取聊天内容；账号用量经本机 app-server 获取，匿名公共状态请求不携带本地额度。
