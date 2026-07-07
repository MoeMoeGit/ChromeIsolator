# 浏览器多开（ChromeIsolator）

ChromeIsolator 是 BrowserIsolator 的 Windows 版本：在一台 Windows 电脑上同时运行多个彼此独立的 Chrome 环境。每个环境都有自己的 Cookie、LocalStorage、密码、扩展配置和登录状态，适合同时管理多个账号，而不用反复登录、退出或切换浏览器配置。

项目定位很简单：做好本地浏览器环境隔离。它不是复杂的反检测平台，也不承诺绕过网站风控；它只是把多个浏览器环境清楚地分开，让日常多账号使用更稳定、更省心。

## 主要功能

- **独立环境**：每个环境使用单独的 Chrome 数据目录，登录状态、Cookie、缓存、密码和扩展配置互不影响
- **快速启动和关闭**：在主窗口或系统托盘启动、关闭单个环境，也可以一次关闭全部环境；退出时会先关闭运行中的 Chrome 再退出
- **双击启动 / 唤起**：双击未运行环境直接启动；双击运行中环境会将对应浏览器窗口带到前台
- **右键菜单**：右键环境列表可快速重命名、编辑备注或删除环境
- **智能排序**：运行中的环境自动置顶，最近使用的排在前面；最近使用时间会写入本地配置，重启后仍然保留；显示启动中/关闭中状态
- **专业管理界面**：左侧环境列表用于快速扫描，右侧详情栏展示备注、路径、基础 / 采集 / 差异模式调试端口、磁盘占用、错误和高级信息；支持打开环境目录和复制路径
- **自定义命名**：新增环境后自动弹出重命名对话框，方便立即命名
- **环境备注**：每个环境可保存一条最多 120 个字符的纯文本备注，用于记录账号、用途或注意事项；空备注不占用详情区域
- **安全删除**：删除环境需要输入 `delete` 确认，对话框显示数据大小，数据会先移到 Windows 回收站
- **错误恢复**：环境启动失败时可重试或清除错误状态
- **基础模式**：默认不启用调试端口、不注入页面脚本，降低特定网站兼容问题；可在设置中为已关闭的指定环境单独启用“差异模式”
- **采集模式 / 调试端口常开**：可在设置中为已关闭的指定环境单独启用本机 CDP 调试端口，端口为 `41000 + 环境编号`，便于本机采集工具连接已登录环境；该模式不会自动启用差异注入
- **轻量环境差异**：差异模式会为指定环境注入稳定的 `navigator.hardwareConcurrency` 和 `navigator.deviceMemory` 值，自动处理新打开的标签页，断线后自动重连（最多 5 次指数退避）
- **外部链接**：可在设置中选择系统外部 http / https 链接默认打开到哪个环境，并发起“设为默认浏览器”请求；目标环境未运行时会自动启动，浏览器引擎未就绪时提示复制链接
- **浏览器引擎设置**：首次运行先说明隔离策略；优先使用用户已安装的官方 Stable Chrome 程序文件；未安装 Chrome 时，用户确认后安装官方 Chrome，下载失败可打开 Google 官方 Chrome 下载页手动安装；Edge 只作为最后临时备用
- **设置面板**：查看浏览器引擎状态和版本、打开数据目录、复制路径、浏览器引擎设置、差异模式二级管理、外部链接默认目标、设为默认浏览器、高级详情显示、语言切换，以及帮助与更新信息
- **多语言支持**：内置中文、English、日本語、한국어、Deutsch、Français、Русский 七种语言，自动检测系统语言，可在设置中切换
- **系统托盘**：关闭窗口后驻留在系统托盘，右键菜单可快速启动/关闭环境、全部关闭、打开主窗口、检查更新；退出时自动确认运行中的环境
- **相对时间显示**：环境最近使用时间显示为"今天"、"昨天"、"3 天前"等，更直观
- **更新检查**：通过 GitHub Releases 检查新版本，支持从设置页和托盘菜单触发
- **本地优先**：配置和环境数据都保存在本机，不上传、不收集用户数据

## 系统要求

- Windows 10 或 Windows 11
- x86-64 处理器
- 64 位系统

## 当前状态

核心功能已完成，包括环境管理、环境备注、外部链接接收、浏览器引擎设置、可选采集模式、可选差异模式、多语言、设置面板、系统托盘和 MSI 安装包构建。当前版本为 V1.7.6，项目继续验证采集模式、默认浏览器注册、真实外部链接行为和本轮稳定性优化。

安装包使用 self-contained 发布，普通用户无需预先安装 .NET Runtime。

## 浏览器引擎

ChromeIsolator 优先使用官方 Stable Google Chrome。项目不会使用 Chrome for Testing、Beta、Dev 或 Canary 渠道。

ChromeIsolator 的核心策略是“浏览器程序文件可共享，用户数据目录必须隔离”。如果用户已经安装 Chrome，ChromeIsolator 只复用 `chrome.exe` 程序文件；启动隔离环境时始终传入自己的 `--user-data-dir=%LOCALAPPDATA%\ChromeIsolator\Profiles\pN`，不会读取或修改用户默认 Chrome profile，也不会影响用户日常 Chrome 的插件、设置、登录用户、启动提示、Cookie、扩展或密码。

如果用户没有安装 Chrome，首次运行窗口会让用户明确选择：

- 下载并安装 Google 官方 Stable Chrome。安装完成后，用户从桌面正常打开 Chrome 时仍使用 Chrome 自己的默认用户数据；ChromeIsolator 环境继续使用独立 profile。
- 如果自动下载失败，打开 Google 官方 Chrome 下载页手动安装，安装后回到 ChromeIsolator 重新检测。
- 如果暂时无法安装 Chrome，可临时使用 Windows 系统自带的 Microsoft Edge Stable 作为最后备用。Edge 不作为推荐主路径；Edge 备用同样只使用 ChromeIsolator 的独立 profile 目录，不触碰 Edge 默认用户数据。

## 数据位置

所有用户数据计划保存在：

```text
%LOCALAPPDATA%\ChromeIsolator\
├── config.json
├── config.json.bak
├── Chrome\
└── Profiles\
    ├── p1\
    ├── p2\
    └── p3\
```

其中 `Chrome\` 用于保存官方 Chrome 安装包下载和浏览器引擎相关临时文件。

应用程序本体计划通过安装包安装到：

```text
%ProgramFiles%\ChromeIsolator\
```

## 功能边界

ChromeIsolator 不包含：

- 代理管理
- 账号托管
- 自动化运营
- 账号资料管理或标签系统
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
artifacts\publish\ChromeIsolator-win-x64-v1.7.6.zip
```

### 构建 MSI 安装包

需要安装 WiX Toolset。早期安装包未签名，Windows 可能显示 SmartScreen 或未知发布者提示。

```powershell
.\scripts\build-msi.ps1
```

安装包产物位于：

```text
artifacts\installer\ChromeIsolator-Setup-x64-v1.7.6.msi
```

## 版本管理

项目版本统一写在根目录 `Directory.Build.props`。发布新版本时只修改其中的 `Version`、`AssemblyVersion`、`FileVersion` 和 `InformationalVersion`，然后创建同版本 tag，例如 `v1.7.6`。

GitHub Actions 会在 tag 推送时自动构建，并创建 GitHub Release，上传带版本号的 win-x64 zip 和 MSI 安装包。

## 许可证

ChromeIsolator 源码以 [Apache License 2.0](LICENSE) 授权。

Google Chrome 是 Google LLC 的产品，受 Google 自身条款约束，不属于本项目 Apache License 2.0 授权范围。ChromeIsolator 仅下载、启动或调用官方 Stable Google Chrome。
