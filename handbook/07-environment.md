# 环境、换机与运维

> 适用状态：启用。开发和运行目标仅为 Windows；macOS 可做文档与静态检查，不能代表 Windows 验证。

## 环境矩阵

| 环境 | 工具链依据 | 安装与启动入口 | 已验证范围 |
| --- | --- | --- | --- |
| Windows 10/11 x64 | PowerShell、.NET 8 SDK；MSI 另需 WiX 7 | `dotnet run --project .\src\ChromeIsolator.App\ChromeIsolator.App.csproj` | 历史上已完成构建、发布和实机流程；当前基线需按任务重验 |
| macOS | 当前 shell、Git、Python | 不能运行 WPF | 可做文档、源码和 Git 检查 |
| Ubuntu | 不适用 | 不支持 WPF 桌面运行 | 未支持 |

## 首次开发与构建

```powershell
dotnet restore ChromeIsolator.sln
dotnet build ChromeIsolator.sln
dotnet run --project .\src\ChromeIsolator.App\ChromeIsolator.App.csproj
```

运行用户需要 Windows x64 和 Chrome Stable；self-contained ZIP/MSI 不要求预装 .NET Runtime。构建 MSI 时安装 WiX 7 CLI；CI 固定使用 `wix 7.0.0`，脚本会确保 Util/UI 扩展存在。

## 配置与数据

- 无必需环境变量、`.env`、服务端、数据库或密钥。
- 用户配置与环境统一位于 `%LOCALAPPDATA%\ChromeIsolator`；应用程序安装在 `%ProgramFiles%\ChromeIsolator`。
- 换机时先同步代码并重新安装工具链，不复制 `bin/`、`obj/`、`artifacts/`。用户 profile 含敏感登录数据，不通过 Git 同步。
- 若人工搬迁用户数据，必须在应用和所有受管浏览器都退出时进行，并保留完整目录结构；当前没有官方跨设备迁移流程。

## 调试端口

- 基础模式：无端口。
- 采集模式：从 `41000 + 环境编号` 开始，只绑定 `127.0.0.1`。
- 差异模式：未启用采集时从 `40000 + 环境编号` 开始，只绑定 `127.0.0.1`。
- 两种模式同时启用时使用采集端口并继续差异注入。

发布、回滚和安装包流程见 [08](08-deployment.md)。
