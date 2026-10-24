# Codex Usage Custom v0.1.5：单文件安装

下载 [完整安装包](https://github.com/ZWN1998/codex-usage-custom/releases/download/v0.1.5/CodexUsage-Custom-0.1.5-macos-arm64.tar.xz)。只需这一个文件，约 31.3 MB。

1. 双击文件使用 macOS 归档实用工具解压，或在终端运行：

   ```sh
   tar -xJf CodexUsage-Custom-0.1.5-macos-arm64.tar.xz
   ```

2. 将解压出的 CodexUsage-Custom-macos-arm64 文件夹放在长期保留的位置。
3. 在终端进入这个文件夹，运行 `sh ./install-macos.sh`。
4. 在新的本地聊天中使用 `@Codex Usage Custom` 启动浮窗。
5. 右键浮窗进入“位置、字号与颜色…”。

需要 macOS 13+ Apple 芯片（arm64），以及已登录的本地 Codex 桌面应用。
无需单独安装 .NET。安装器不会自动启动 GUI，也不添加登录自启。
App 为本地 ad-hoc 签名，尚未完成 Apple Developer ID 分发签名或公证。

SHA-256：

```text
ae76f3934a58e56182c2bccd4f30143b88e2a94b5a4cb4e758b1e36bbd3f0aa9
```

核对命令：`shasum -a 256 CodexUsage-Custom-0.1.5-macos-arm64.tar.xz`。

先前的三个分卷与合并脚本保留作为备用；普通安装直接下载本页的完整安装包。
