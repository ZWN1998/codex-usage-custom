# Codex Usage Custom v0.1.5 安装

此发行版通过三个分卷保留完整安装 ZIP；合并后的 SHA-256：
`c5a24b50cbaae58092b4c61c056df44cfee7e723a6df605deacb767575eb6f97`。

在 Apple 芯片 Mac 的终端中运行：

```sh
curl -fL -o install-macos-release.sh https://github.com/ZWN1998/codex-usage-custom/releases/download/v0.1.5/install-macos-release.sh
sh install-macos-release.sh
```

脚本先下载三个分卷，合并并核对内置 SHA-256，再通过 macOS ditto 解压保留签名，最后调用安装包的插件安装器。
默认保留到 `~/Applications/CodexUsage-Custom-0.1.5/`。不会自动启动 GUI 或添加登录自启。
安装后在新的本地聊天中使用 `@Codex Usage Custom` 启动浮窗。

手动下载也可：将三个 `.part01` / `.part02` / `.part03` 附件放在同一目录，按顺序 `cat` 合并为 ZIP，再用 macOS 归档实用工具或 `ditto` 解压。
仅支持 macOS 13+ Apple 芯片。App 为本地 ad-hoc 签名，尚未公证。
