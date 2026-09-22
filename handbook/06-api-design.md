# 接口约定

> 适用状态：启用。本项目不提供对外 HTTP API 或后端服务；这里记录系统和本机接口边界。

| 能力 / 操作 | 实现或契约入口 | 调用方 | 当前状态 |
| --- | --- | --- | --- |
| 单实例唤醒与链接转发 | `App.xaml.cs` 命名管道 | ChromeIsolator 第二实例 | 已实现；管道名按 Windows 用户 SID 隔离 |
| http/https 默认应用 | `ShellService.RequestDefaultBrowser` 和 HKCU 注册项 | Windows Shell | 已实现，仍需 Windows 实机验证 |
| Chrome DevTools Protocol | `ChromeManager`、`FingerprintInjector` | 本应用和本机采集工具 | 已实现；仅回环地址 |
| GitHub latest release | `UpdateService.cs` | 设置页与托盘 | 已实现；公开 API，无密钥 |

## 契约边界

- 命令行和单实例管道只接受绝对 `http`/`https` URL。管道消息使用 `show` 或 `open-url {Base64 UTF-8 URL}`；转发失败时不能静默吞掉链接。
- 默认浏览器注册写入当前用户 `HKCU`，然后打开 Windows 默认应用设置；应用不能绕过系统让自己强制成为默认浏览器，也不宣称设置成功。
- CDP 使用 `/json/version`、`/json` 和 browser-level WebSocket。采集端口为 `41000 + N` 起始，差异端口为 `40000 + N` 起始；实际端口可能因冲突向后调整。
- CDP 无认证，因此只允许 `127.0.0.1`，且基础模式不开放端口。采集工具必须自行管理 target 生命周期，不依赖用户预先打开页面。
- 更新检查访问 `MoeMoeGit/ChromeIsolator` 的 latest release，失败只影响更新提示，不影响本地功能。

接口实现变化时同时检查命令行启动、已有实例转发、Windows 注册表项、设置页状态和错误恢复；当前没有自动化契约测试。
