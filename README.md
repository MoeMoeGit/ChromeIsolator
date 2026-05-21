# 浏览器多开（ChromeIsolator）

ChromeIsolator 是 BrowserIsolator 的 Windows 版本：在一台 Windows 电脑上同时运行多个彼此独立的 Chrome 环境。每个环境都有自己的 Cookie、LocalStorage、密码、扩展配置和登录状态，适合同时管理多个账号，而不用反复登录、退出或切换浏览器配置。

项目定位很简单：做好本地浏览器环境隔离。它不是复杂的反检测平台，也不承诺绕过网站风控；它只是把多个浏览器环境清楚地分开，让日常多账号使用更稳定、更省心。

## 主要功能

- **独立环境**：每个环境使用单独的 Chrome 数据目录，登录状态、Cookie、缓存、密码和扩展配置互不影响
- **快速启动和关闭**：在主窗口或系统托盘启动、关闭单个环境，也可以一次关闭全部环境
- **双击启动**：双击环境列表项直接启动或关闭环境
- **智能排序**：运行中的环境自动置顶，最近使用的排在前面
- **专业管理界面**：左侧环境列表用于快速扫描，右侧详情栏展示路径、调试端口、磁盘占用、错误和高级信息
- **自定义命名**：新增环境后可以直接命名，也可以之后重命名，方便对应不同账号或用途
- **安全删除**：删除环境需要输入环境名称确认，数据会先移到 Windows 回收站
- **错误恢复**：环境启动失败时可重试或清除错误状态
- **轻量环境差异**：为不同环境注入稳定的 `navigator.hardwareConcurrency` 和 `navigator.deviceMemory` 值，自动处理新打开的标签页，断线后自动重连（最多 5 次指数退避）
- **自动浏览器准备**：首次运行时自动下载并安装官方 Stable Google Chrome Enterprise，带进度显示；也支持跳过下载直接使用系统已安装的 Chrome；下载失败可重试或复制错误详情
- **设置面板**：查看 Chrome 状态和版本、打开数据目录、复制路径、重新安装 Chrome、高级详情显示、语言切换，以及帮助与更新信息
- **多语言支持**：内置中文、English、日本語、한국어、Deutsch、Français、Русский 七种语言，自动检测系统语言，可在设置中切换
- **系统托盘**：关闭窗口后驻留在系统托盘，右键菜单可快速启动/关闭环境、检查更新；退出时自动确认运行中的环境
- **更新检查**：通过 GitHub Releases 检查新版本，支持从设置页和托盘菜单触发
- **本地优先**：配置、浏览器和环境数据都保存在本机，不上传、不收集用户数据

## 系统要求

- Windows 10 或 Windows 11
- x86-64 处理器
- 64 位系统

## 当前状态

核心功能已完成，包括环境管理、Chrome 准备、CDP 注入、多语言、设置面板、系统托盘和 MSI 安装包构建。项目正在验证 MSI 安装体验和完善细节。

## 浏览器引擎

ChromeIsolator 使用官方 Stable Google Chrome。项目不会使用 Chrome for Testing、Beta、Dev 或 Canary 渠道。

ChromeIsolator 的目标是尽量准备独立的 Chrome 运行文件，同时始终使用自己的 profile 数据目录。即使在 Windows 平台上需要调用系统已安装的官方 Stable Chrome，也不会读取用户默认 Chrome profile，不会复用用户日常 Chrome 的登录状态、Cookie、扩展或密码。

## 数据位置

所有用户数据计划保存在：

```text
%LOCALAPPDATA%\ChromeIsolator\
├── config.json
├── Chrome\
└── Profiles\
    ├── p1\
    ├── p2\
    └── p3\
```

应用程序本体计划通过安装包安装到：

```text
%ProgramFiles%\ChromeIsolator\
```

## 功能边界

ChromeIsolator 不包含：

- 代理管理
- 账号托管
- 自动化运营
- 网页内容采集
- 完整反检测浏览器能力
- 对任何平台风控的绕过承诺

## 从源码构建

项目使用 C#、WPF 和 .NET 8 实现。

### 前置条件

- Windows 10 / Windows 11 x64
- .NET 8 SDK
- Visual Studio 2022 的 .NET Desktop Development 组件，或等价 Build Tools

### 构建

```powershell
dotnet build ChromeIsolator.sln
```

### 运行

```powershell
dotnet run --project .\src\ChromeIsolator.App\ChromeIsolator.App.csproj
```

### 发布 win-x64

```powershell
.\scripts\publish-win-x64.ps1
```

发布产物位于：

```text
artifacts\publish\win-x64\
```

### 构建 MSI 安装包

需要安装 WiX Toolset。早期安装包未签名，Windows 可能显示 SmartScreen 或未知发布者提示。

```powershell
.\scripts\build-msi.ps1
```

安装包产物位于：

```text
artifacts\installer\ChromeIsolator-Setup-x64.msi
```

## 许可证

ChromeIsolator 源码以 [Apache License 2.0](LICENSE) 授权。

Google Chrome 是 Google LLC 的产品，受 Google 自身条款约束，不属于本项目 Apache License 2.0 授权范围。ChromeIsolator 仅下载、启动或调用官方 Stable Google Chrome。
