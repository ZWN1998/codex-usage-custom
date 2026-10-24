#!/bin/sh
set -eu
release_base=https://github.com/ZWN1998/codex-usage-custom/releases/download/v0.1.5
filename=CodexUsage-Custom-0.1.5-macos-arm64.zip
expected=c5a24b50cbaae58092b4c61c056df44cfee7e723a6df605deacb767575eb6f97
if [ "$(uname -s)" != Darwin ] || [ "$(uname -m)" != arm64 ]; then
    echo '此发行版仅支持 macOS Apple 芯片（arm64）。' >&2
    exit 1
fi
# Keep the marketplace source at a stable user-local path after installation.
destination="${CODEX_USAGE_INSTALL_DIR:-$HOME/Applications/CodexUsage-Custom-0.1.5}"
mkdir -p "$destination/downloads"
cd "$destination/downloads"
for part in 01 02 03; do
    item="$filename.part$part"
    echo "Downloading $item ..."
    curl --fail --location --retry 3 --connect-timeout 20 --output "$item.download" "$release_base/$item"
    mv "$item.download" "$item"
done
cat "$filename.part01" "$filename.part02" "$filename.part03" > "$filename.download"
actual=$(shasum -a 256 "$filename.download" | awk '{print $1}')
if [ "$actual" != "$expected" ]; then
    echo 'SHA-256 校验失败；已停止，不执行安装。请重试或检查下载文件。' >&2
    exit 1
fi
mv "$filename.download" "$filename"
ditto -x -k "$filename" "$destination"
sh "$destination/CodexUsage-Custom-macos-arm64/install-macos.sh"
echo "Installed from $destination/CodexUsage-Custom-macos-arm64"
