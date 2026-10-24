# Codex Usage Custom

可自定义位置、方向、字号和颜色的 Codex 桌面用量浮窗。
本发行包支持 **macOS Apple 芯片（arm64）**；基于 [Jay Hilwig 的 Codex Usage](https://github.com/jayhilwig/codexusage) 修改，非上游官方发行版。

## 下载安装

从 [v0.1.5 发行页](https://github.com/ZWN1998/codex-usage-custom/releases/tag/v0.1.5) 下载一个完整安装文件：

**[CodexUsage-Custom-0.1.5-macos-arm64.tar.xz](https://github.com/ZWN1998/codex-usage-custom/releases/download/v0.1.5/CodexUsage-Custom-0.1.5-macos-arm64.tar.xz)**

需要 macOS 13+ 和已登录的本地 Codex 桌面应用，无需单独安装 .NET。

1. 用 macOS 归档实用工具解压安装包到长期保留的目录；或运行 `tar -xJf CodexUsage-Custom-0.1.5-macos-arm64.tar.xz`。
2. 在终端进入解压后的 `CodexUsage-Custom-macos-arm64` 目录，运行 `sh ./install-macos.sh`。
3. 在新的本地聊天中使用 `@Codex Usage Custom` 启动浮窗。
4. 右键进入 **位置、字号与颜色…** 调整外观。

完整安装文件的 SHA-256：

```text
ae76f3934a58e56182c2bccd4f30143b88e2a94b5a4cb4e758b1e36bbd3f0aa9
```

可用 `shasum -a 256 CodexUsage-Custom-0.1.5-macos-arm64.tar.xz` 核对。更详细的安装步骤见 [INSTALL.md](INSTALL.md)。

也可以在解压目录的 `plugins/codex-usage-custom` 内执行：

```sh
sh scripts/hud.sh Start
sh scripts/hud.sh Status
sh scripts/hud.sh Stop
```

安装器只安装独立的本地插件来源，不自动启动 GUI，也不配置登录自启。
请只运行一个版本的浮窗；若原版仍在运行，先停止原版，避免重复。
安装后请保留解压目录；移动目录后重新运行安装脚本登记新路径。

## 功能

- 透明无框，显示五小时和每周剩余额度、重置倒计时。
- 六种锚点及横纵偏移；自动模式在左侧居中使用正向竖排，其余位置横排。
- 可手动指定横排/竖排，字号 8–24，刷新按钮同步缩放。
- 预设颜色、十六进制颜色和 RGB 调节；即时生效并保存在本机。
- 点击刷新读取额度，悬停查看精确重置时间；已修复本机 Mac 全屏定位下移。

默认顶部居中、11 号、雾紫灰 `#8C879F`。旧用户配置会继续沿用。

## 运行范围与验证

本次只发布 macOS arm64 二进制；Intel Mac、Windows、Linux、网页、移动端、云端和远程执行不在本包支持范围。
共享源代码仍保留上游平台实现，不代表本发行版已验证其他平台。
App 为本地 ad-hoc 签名，尚无 Apple Developer ID 分发签名或公证；其他 Mac 从互联网下载时可能遇到 Gatekeeper 阻止。

已通过核心逻辑、配置保存/迁移、方向和字号边界、真实 Avalonia 字体尺寸测量、构建与严格签名校验。
发行包保留 macOS 签名元数据，解压后 App 的严格签名校验通过；TAR 包中的 226 个程序文件与已验证的构建完全一致。
尚未在第二台干净 Mac 上完成下载、Gatekeeper 与全部 UI 操作验收。

先前发布的分卷和自动合并脚本仍保留，已有下载链接继续可用；普通安装直接使用上面的完整安装文件。

## 数据与隐私

额度经本机 Codex app-server 读取，认证仍由 Codex 管理。
另匿名访问 `https://codex-resets.com/api/v1/status` 获取公共状态，不附带本地额度。
不读取聊天内容，不包含开发者账号系统或遥测。配置和缓存保存在
`~/Library/Application Support/CodexUsageHud/`。
发行包不含开发者本机的账号、额度历史、偏好、日志或聊天截图。详见 [隐私说明](PRIVACY.md)。

## 从源码运行

需要 .NET 10 SDK 和本地 Codex CLI。使用已发布安装包不需要 SDK。

```sh
dotnet run --project src/CodexUsage.Desktop/CodexUsage.Desktop.csproj -c Release -r osx-arm64 -p:RuntimeIdentifiers=osx-arm64
dotnet run --project tests/CodexUsage.Core.Tests/CodexUsage.Core.Tests.csproj -c Release
dotnet run --project tests/CodexUsage.Desktop.Tests/CodexUsage.Desktop.Tests.csproj -c Release -r osx-arm64 -p:RuntimeIdentifiers=osx-arm64
```

如果 CLI 未被自动找到，可将 `CODEX_HUD_CODEX_PATH` 设为本机 Codex 可执行文件路径。
桌面测量测试使用隐藏窗口，不请求真实账号数据。

## 卸载

先停止浮窗，再执行：

```sh
codex plugin remove codex-usage-custom@codex-usage-custom-local
codex plugin marketplace remove codex-usage-custom-local
```

本地配置会保留；如需清除，可在退出浮窗后删除 `~/Library/Application Support/CodexUsageHud/`。

## 来源与许可说明

上游作者：Jay Hilwig；基线提交 `f3a3cfd40f1cace21ea5e4f61234a25e2619dd16`。
本修改版由 ZWN1998 整理发布。原有代码和资源的权利归各自作者所有，未为上游代码重新授予开源许可证。
所采用的上游基线未附带 LICENSE。更多来源及依赖声明见 [ATTRIBUTION.md](ATTRIBUTION.md) 和 `notices/`。
问题反馈请使用[本仓库 Issues](https://github.com/ZWN1998/codex-usage-custom/issues)。
