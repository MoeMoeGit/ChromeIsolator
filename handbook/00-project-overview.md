# 项目定义

> 状态：已初始化；项目事实以当前代码、`Directory.Build.props` 和发布配置为准。

## 目标与边界

ChromeIsolator（中文产品名“浏览器多开”）是在 Windows 10/11 x86-64 上运行的本地桌面工具。它让用户用同一份官方浏览器程序启动多个相互隔离的使用环境，每个环境拥有独立的 Cookie、登录状态、密码、扩展和缓存。

- 目标用户：需要同时管理多个网站账号，或隔离工作、客户和用途的 Windows 用户。
- 成功标准：环境数据不串用；启动、关闭、删除和异常恢复可靠；外部链接、系统托盘和安装升级行为清楚；不影响用户日常 Chrome/Edge 配置。
- 核心能力：环境管理、浏览器启停、托盘驻留、外部链接路由、可选采集模式、可选轻量环境差异、多语言、更新检查与 MSI/ZIP 发行。详细规则见 [02 功能设计](02-function-design.md)。
- 明确不做：代理管理、账号托管、自动化运营、网页采集实现、云同步、复杂标签系统、完整反检测浏览器、风控绕过承诺、macOS 数据迁移。

## 核心术语

- **环境 / Profile**：一个 `Profiles\pN` 目录及其配置项。目录名是稳定身份；展示名可修改。
- **基础模式**：不开放 CDP 调试端口，也不注入页面脚本。
- **采集模式**：只在本机回环地址开放 CDP，供 cscout 等本机工具连接已登录环境；不自动启用环境差异。
- **差异模式**：通过 CDP 覆盖 `navigator.hardwareConcurrency` 和 `navigator.deviceMemory`；不是完整设备模拟。
- **浏览器引擎**：优先使用已安装的官方 Stable Chrome；Chrome 缺失且用户明确选择时，才使用 Edge Stable 备用。

## 实际技术与依据

| 项目 | 实际选择 | 版本 / 配置依据 | 核对日期 |
| --- | --- | --- | --- |
| 运行时与框架 | C#、WPF、.NET | `src/ChromeIsolator.App/ChromeIsolator.App.csproj`：`net8.0-windows` | 2026-09-22 |
| UI 结构 | WPF + 轻量 MVVM 分层 | `ViewModels/`、`Themes/`、各窗口 XAML | 2026-09-22 |
| 数据 | JSON 配置 + Chrome profile 文件目录；无数据库 | `Models/`、`ConfigStore.cs`、`ProfileManager.cs` | 2026-09-22 |
| 浏览器控制 | `System.Diagnostics.Process` + HTTP/WebSocket CDP | `ChromeManager.cs`、`FingerprintInjector.cs` | 2026-09-22 |
| 发行 | self-contained win-x64 ZIP + WiX 7 MSI | 构建脚本与 `.github/workflows/build.yml` | 2026-09-22 |
| 版本来源 | `Directory.Build.props` | 当前版本 `1.7.10` | 2026-09-22 |

## 不可破坏的约束

- 必须始终为隔离环境传入 ChromeIsolator 自己的 `--user-data-dir`；不得读取或修改用户默认 Chrome/Edge profile。
- 删除环境必须进入 Windows 回收站；卸载应用默认保留 `%LOCALAPPDATA%\ChromeIsolator` 用户数据。
- CDP 端口只绑定 `127.0.0.1`；未启用采集或差异模式时不开放端口。
- 关闭主窗口只隐藏到托盘；真正退出前关闭所有由应用管理的浏览器环境。
- 不使用 Chrome for Testing、Beta、Dev、Canary 或第三方便携 Chromium。
