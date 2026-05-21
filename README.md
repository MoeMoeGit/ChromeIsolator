# 浏览器多开（ChromeIsolator）

ChromeIsolator 是 BrowserIsolator 的 Windows 版本：在一台 Windows 电脑上同时运行多个彼此独立的 Chrome 环境。每个环境都有自己的 Cookie、LocalStorage、密码、扩展配置和登录状态，适合同时管理多个账号，而不用反复登录、退出或切换浏览器配置。

项目定位很简单：做好本地浏览器环境隔离。它不是复杂的反检测平台，也不承诺绕过网站风控；它只是把多个浏览器环境清楚地分开，让日常多账号使用更稳定、更省心。

## 主要功能

- **独立环境**：每个环境使用单独的 Chrome 数据目录，登录状态、Cookie、缓存、密码和扩展配置互不影响
- **快速启动和关闭**：在主窗口或系统托盘启动、关闭单个环境，也可以一次关闭全部环境
- **专业管理界面**：左侧环境列表用于快速扫描，右侧详情栏展示路径、调试端口、错误、操作和高级信息
- **自定义命名**：新增环境后可以直接命名，也可以之后重命名，方便对应不同账号或用途
- **安全删除**：删除环境需要输入环境名称确认，数据会先移到 Windows 回收站
- **轻量环境差异**：为不同环境注入稳定的 `navigator.hardwareConcurrency` 和 `navigator.deviceMemory` 值，并持续处理新打开的标签页
- **自动浏览器准备**：首次运行时自动准备官方 Stable Google Chrome
- **设置面板**：查看 Chrome 状态和版本、打开数据目录、复制路径、重新准备 Chrome、切换语言、高级详情显示，以及帮助与更新信息
- **本地优先**：配置、浏览器和环境数据都保存在本机，不上传、不收集用户数据

## 系统要求

- Windows 10 或 Windows 11
- x86-64 处理器
- 64 位系统

## 当前状态

项目正在开发中。当前仓库已完成 Windows 版规划、图标和基础文档，应用代码会逐步实现。

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

项目计划使用 C#、WPF 和 .NET 实现。代码工程创建后，会在这里补充完整构建步骤。

## 许可证

ChromeIsolator 源码以 [Apache License 2.0](LICENSE) 授权。

Google Chrome 是 Google LLC 的产品，受 Google 自身条款约束，不属于本项目 Apache License 2.0 授权范围。ChromeIsolator 仅下载、启动或调用官方 Stable Google Chrome。
