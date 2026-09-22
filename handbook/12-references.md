# 外部服务与参考资料

> 适用状态：启用。仅登记当前实现实际依赖的官方资料；链接内容变化时重新核对。

| 服务 / 资料 | 官方来源 | 本项目用途 | 实现入口 |
| --- | --- | --- | --- |
| Google Chrome | https://www.google.com/chrome/ | 用户手动下载安装 Stable Chrome | `DownloadWindow`、`ChromeManager` |
| Chrome Enterprise MSI | https://chromeenterprise.google/download/ | 用户确认后的官方 Stable MSI 来源 | `ChromeManager.PrepareChromeAsync` |
| Chrome DevTools Protocol | https://chromedevtools.github.io/devtools-protocol/ | 差异注入和本机采集连接 | `ChromeManager`、`FingerprintInjector` |
| GitHub Releases REST API | https://docs.github.com/en/rest/releases/releases#get-the-latest-release | 检查最新版本 | `UpdateService.cs` |
| WiX Toolset | https://wixtoolset.org/docs/ | 构建 Windows MSI | `installer/`、`scripts/build-msi.ps1` |

## 已采用的接口与限制

- Chrome 安装器当前使用 `https://dl.google.com/chrome/install/GoogleChromeStandaloneEnterprise64.msi`，最多重试三次；下载失败只提供官方页面，不引入第三方镜像。
- CDP 轮询 `/json/version`，通过 browser-level WebSocket 使用 Target 自动附加，并对 page session 执行 `Page.addScriptToEvaluateOnNewDocument` / `Runtime.evaluate`。仅连接应用自己启动的回环端口。
- GitHub 更新检查读取 `https://api.github.com/repos/MoeMoeGit/ChromeIsolator/releases/latest`，无需密钥；仓库 owner 已由代码和 Git remote 确认为 `MoeMoeGit`。
- CI 使用 `actions/setup-dotnet@v4`、`actions/upload-artifact@v4`、`softprops/action-gh-release@v2` 和 WiX `7.0.0`。升级 action、WiX、.NET 或 Chrome 接入时，应以当时官方文档重新核对兼容性。
