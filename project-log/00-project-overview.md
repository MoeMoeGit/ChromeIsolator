# 项目概述

## 项目名称

ChromeIsolator

中文产品名：浏览器多开

## 项目背景

ChromeIsolator 是 BrowserIsolator 的 Windows 版本复刻。BrowserIsolator 当前在 macOS 上提供多个相互隔离的 Chrome 使用环境，用户可以在同一台设备上同时登录和管理多个账号，而不用反复退出、切换浏览器配置或污染日常 Chrome。

本项目目标是在 Windows 10 / Windows 11 的 x86-64 设备上，从用户使用角度一模一样地复刻 BrowserIsolator 的核心体验。

项目定位保持一致：做好本地浏览器环境隔离。它不是复杂的反检测平台，不承诺绕过网站风控；只提供多个本地浏览器环境的独立数据目录、稳定启动关闭、轻量环境差异和清晰管理界面。

## 用户 / 使用场景

- 需要在同一台 Windows 电脑上同时管理多个账号的个人用户。
- 需要把不同用途的浏览器登录状态、Cookie、扩展配置、密码和缓存隔离开的用户。
- 典型场景包括 Douyin 等网站的多账号登录和日常运营，但项目不承诺规避任何平台风控。

## 核心功能

1. 多个彼此独立的浏览器环境，每个环境使用独立 profile 数据目录。
2. 启动、关闭、全部关闭、新增、重命名、删除环境。
3. 主窗口提供环境列表和环境详情；系统托盘提供快速启动 / 关闭入口。
4. 首次运行说明隔离策略；优先使用已安装的官方 Stable Chrome，缺失时用户确认安装官方 Chrome，或用户明确选择 Edge Stable 备用。
5. 默认使用基础模式，仅隔离 profile 数据目录；可在设置中为已关闭的指定环境启用 Chrome DevTools Protocol 轻量环境差异：
   - `navigator.hardwareConcurrency`
   - `navigator.deviceMemory`
6. 每个环境可选保存一条轻量纯文本备注，辅助区分账号、客户、用途或注意事项。
7. 可将系统外部 http / https 链接默认转发到用户选择的一个环境。
8. 设置面板提供浏览器引擎状态、版本、外部链接目标、数据目录、路径复制、安装官方 Chrome、语言切换、更新检查等能力。
9. 删除环境时将 profile 数据移入 Windows 回收站。
10. 本地优先：配置和环境数据保存在本机，不上传、不收集用户数据。

## 核心概念

- **环境 / Profile**：一个独立浏览器使用环境，对应一个 `Profiles\pN` 目录。
- **独立数据目录**：启动浏览器时通过 `--user-data-dir` 指向环境目录，隔离 Cookie、LocalStorage、登录状态、密码、缓存、扩展配置等数据。
- **浏览器引擎**：优先使用官方 Stable Chrome 程序文件；Chrome 缺失且用户确认时可安装官方 Chrome；Edge Stable 只作为用户明确选择的备用引擎。
- **托盘驻留**：关闭主窗口不退出应用；应用继续驻留系统托盘，便于快速启动和关闭环境。
- **基础模式**：默认不启用调试端口、不注入页面脚本，优先保证特定网站兼容性。
- **轻量环境差异**：可按环境启用；通过 CDP 在页面中覆盖少量 navigator 属性，让不同环境有稳定但不同的 CPU 核心数和内存值。
- **环境备注**：环境级可选纯文本备注，只用于辅助识别，不参与登录、标签、同步或自动化逻辑。
- **外部链接目标环境**：系统外部进入的 http / https 链接固定打开到一个默认环境；未配置时使用编号最小的 `pN`。

## 技术栈

| 层级 | 技术 | 说明 |
|------|------|------|
| 桌面应用 | C# / WPF / .NET | Windows 10/11 x86-64 桌面应用 |
| UI 架构 | MVVM-ish | 使用清晰 ViewModel / Service 分层，不过度引入框架 |
| 浏览器控制 | `System.Diagnostics.Process` | 启动、关闭和跟踪浏览器进程 |
| CDP 通信 | HTTP + WebSocket | 仅在差异模式开启时访问 `/json/version`、`/json`，通过 browser-level WebSocket 注入脚本 |
| 外部链接接收 | Windows URL Protocol / 默认应用 | 注册为可选 http / https 处理程序，接收系统外部链接并转发到指定环境 |
| 配置存储 | JSON 文件 | `%LOCALAPPDATA%\ChromeIsolator\config.json` |
| 数据存储 | 文件系统 | `%LOCALAPPDATA%\ChromeIsolator\Profiles\` |
| 安装包 | WiX MSI（优先） | 安装主程序到 `%ProgramFiles%\ChromeIsolator` |
| 其他 | Windows Shell API | 托盘、回收站、打开文件夹、剪贴板等 |

## 项目边界

- 不包含：完整反检测浏览器能力。
- 不包含：代理管理、账号管理、自动化运营、脚本执行、网页内容采集。
- 不包含：复杂账号资料管理、标签系统或备注云同步。
- 不包含：macOS 数据迁移到 Windows。
- 不包含：读取或复用用户默认 Chrome profile。
- 不包含：云同步、用户数据上传、远程配置管理。
- 不包含：承诺规避 Douyin 或其他网站风控。

## 项目约束

- 支持系统：Windows 10 / Windows 11。
- 支持架构：x86-64，64 位设备。
- 浏览器来源：优先官方 Stable Chrome；Chrome 缺失时用户确认安装官方 Chrome；Edge Stable 仅作用户确认备用；不使用 Chrome for Testing、Beta、Dev、Canary 或第三方便携 Chromium。
- 数据隔离：不得读取或修改用户默认 Chrome / Edge profile。
- 用户体验：从用户使用角度尽量与 BrowserIsolator 保持一致。
- 数据位置：程序文件和用户数据分离；用户 profile 数据必须按 Windows 用户隔离。
- 安全删除：删除环境必须移入回收站，避免直接永久删除。
- 应用退出：窗口关闭隐藏到托盘；真正退出前关闭所有运行中的环境。
