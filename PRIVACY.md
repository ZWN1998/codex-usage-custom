# 隐私说明

更新日期：2026-10-23。适用于 Codex Usage Custom 本地修改版。

本工具通过本机 Codex app-server 读取账号额度和重置时间，认证由 Codex 管理。
不读取聊天内容，不收集开发者遥测，也没有单独账号系统。

工具会匿名请求 `https://codex-resets.com/api/v1/status` 获取公共重置状态。
本地额度不会被发送给该服务；该服务可接收普通 HTTP 连接元数据（如来源 IP）。

额度快照、公共状态缓存、外观配置及数值布局诊断留在本机
`~/Library/Application Support/CodexUsageHud/`，不包含聊天或认证令牌。
退出工具后可删除此目录清除本地数据。

反馈：[GitHub Issues](https://github.com/ZWN1998/codex-usage-custom/issues)。
请勿在公开反馈中附上认证信息、真实账号资料或私人聊天截图。

## Windows 适配版

Windows 外观、CLI 路径提示、额度缓存与管理器状态保存在当前用户 .codex/usage-custom-windows；数值布局诊断可能保存在 LocalAppData/CodexUsageHud。用户登录项仅指向本工具。发行包不包含开发者的用户配置、账号状态、聊天或日志。
