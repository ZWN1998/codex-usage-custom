Windows x64 专用版，基于 v0.1.5 改造。现有 [Mac Apple silicon v0.1.5](https://github.com/ZWN1998/codex-usage-custom/releases/tag/v0.1.5) 保持不变；本发布不设为仓库最新版本，以保留原 Mac 下载入口。

### 使用

下载完整 Windows ZIP，核对旁边的 SHA-256 文件，完整解压后从资源管理器运行 **Install.cmd**。需要 Windows 10/11 x64 和已登录的本地 Codex 桌面应用，自带 .NET 运行时。

安装器将程序放入当前用户 `.codex/usage-custom-windows/package`，继续从原来的 Codex 开始菜单入口打开即可。右键浮窗打开“位置、字号与颜色…”。详细安装、诊断、卸载与回退见包内 README-Windows.md。

### 功能

- 默认标题栏居中、透明无框、11 号蓝灰单行小字，显示剩余额度与重置倒计时；悬停查看具体时间。
- 六种锚点、位置微调、横竖排、8–24 字号、预设颜色与十六进制/RGB 自定义颜色，自动保存。
- Windows DPI 与标题栏按钮边界适配，支持前台 Codex 窗口切换。
- 独立原生登录管理器，使用当前用户 Startup 登录项；不使用任务计划，不替换 Codex 启动入口，不依赖 VBS 监听器。
- 安装器可迁移已知旧 Windows 监听方案，并保留回退文件；主动退出工具后不会在同一 Codex 会话里立即强制拉起。

### 验证范围

本地已通过编译、核心逻辑、Windows 字体/定位、包校验、PowerShell 5.1 安装、真实登录项及主动退出/显式恢复/单实例检查。详细范围见源码中的 WINDOWS-VALIDATION.md。发布流程还必须独立通过核心、Windows 字体/布局和 DPI/锚点、PowerShell 5.1 语法、自包含打包及校验，才允许上传安装包。

整机重启、全部显示器组合、所有 Codex 版本及其他干净电脑的完整人工 UI 验收不由上述检查替代，尚需分别验证。本包未提供 Windows 代码签名保证。

保留原仓库署名、隐私及来源说明，基于 Jay Hilwig 的 Codex Usage 修改，非上游或 OpenAI 官方发布。
