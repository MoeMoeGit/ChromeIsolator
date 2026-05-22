# 项目架构

## 系统架构

ChromeIsolator 是本地优先的 Windows 桌面应用。应用本身负责管理配置、profile 目录、Chrome 进程和 CDP 注入，不依赖后端服务。

```text
┌──────────────────────────────┐
│        WPF 主窗口 / 托盘       │
└───────────────┬──────────────┘
                │
┌───────────────▼──────────────┐
│          ViewModels           │
│ 环境列表 / 详情 / 设置 / 状态  │
└───────────────┬──────────────┘
                │
┌───────────────▼────────────────────────────────────┐
│                    Services                         │
│ ProfileManager / ChromeManager / PortAllocator      │
│ FingerprintInjector / TrayService / Localization    │
└───────────────┬────────────────────────────────────┘
                │
┌───────────────▼────────────────────────────────────┐
│                  本地系统资源                       │
│ config.json / Profiles / Browser Process / CDP       │
└────────────────────────────────────────────────────┘
```

核心数据流：

```text
用户操作
→ ViewModel 更新命令状态
→ Service 执行文件系统 / 进程 / 网络 / CDP 操作
→ Observable 状态回写 UI
→ 配置和 profile 数据落盘到 %LOCALAPPDATA%\ChromeIsolator
```

## 目录结构

计划中的代码目录结构：

```text
ChromeIsolator/
├── src/
│   ├── ChromeIsolator.App/              # WPF 桌面应用入口
│   │   ├── App.xaml
│   │   ├── MainWindow.xaml
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   ├── Services/
│   │   ├── Models/
│   │   ├── Localization/
│   │   └── Resources/
│   └── ChromeIsolator.Installer/        # WiX MSI 安装项目
├── tests/
│   └── ChromeIsolator.Tests/            # 纯逻辑单元测试
├── project-log/                         # 开发知识库
├── README.md
└── ChromeIsolator.sln
```

运行时用户数据目录：

```text
%LOCALAPPDATA%\ChromeIsolator\
├── config.json
└── Profiles\
    ├── p1\
    ├── p2\
    └── p3\
```

安装目录：

```text
%ProgramFiles%\ChromeIsolator\
├── ChromeIsolator.exe
└── 应用依赖文件
```

## 关键技术决策

### 决策 1：使用 WPF / C# / .NET 实现 Windows 桌面应用

- **选择**：WPF + C# + .NET。
- **备选方案**：WinUI 3、Electron、Tauri、Qt。
- **原因**：WPF 对 Windows 10/11 支持成熟，托盘、进程管理、Shell API、安装包生态稳定；项目主要是本地工具，不需要 Web 技术栈。
- **参考**：详见 `10-planning-log.md` 的 ADR-001。

### 决策 2：主程序安装到 Program Files，用户数据放到 LocalAppData

- **选择**：应用文件安装到 `%ProgramFiles%\ChromeIsolator`，配置和 profile 放到 `%LOCALAPPDATA%\ChromeIsolator`。
- **备选方案**：全部放用户目录、全部放 Program Files。
- **原因**：程序安装更像标准 Windows 应用；用户 profile 数据必须按 Windows 用户隔离，且需要普通用户可写。
- **参考**：详见 `10-planning-log.md` 的 ADR-002。

### 决策 3：官方 Stable Chrome 优先，禁止使用 Chrome for Testing

- **选择**：优先复用官方 Stable Chrome 程序文件；未安装 Chrome 时，用户确认后安装官方 Stable Chrome；Chrome 缺失且用户明确选择时可使用 Microsoft Edge Stable 备用。
- **备选方案**：Chrome for Testing、第三方便携版 Chromium、私有提取 Chrome、读取用户默认 Chrome profile。
- **原因**：目标使用场景包含 Douyin 等多账号登录，用户明确担心 Testing 版本触发风控；第三方浏览器来源和媒体能力不可控；私有提取官方 Chrome 在 Windows 上授权和稳定性不清晰；默认读取用户 Chrome profile 会串插件、设置和登录态。
- **参考**：详见 `12-design-decisions.md` 的决策 2 和决策 5。

### 决策 4：CDP 只注入轻量 navigator 差异

- **选择**：仅覆盖 `navigator.hardwareConcurrency` 和 `navigator.deviceMemory`。
- **备选方案**：完整指纹模拟、代理 / 设备画像 / Canvas / WebGL 等反检测能力。
- **原因**：复刻 BrowserIsolator 的现有功能和产品边界，不把项目扩展为反检测平台。
- **参考**：详见 `10-planning-log.md` 的 ADR-004。

## 依赖关系

| 依赖 | 版本 | 用途 |
|------|------|------|
| .NET SDK | 待定，优先 .NET 8 LTS 或当前稳定 LTS | 编译 WPF 应用 |
| WPF | 随 .NET | Windows 桌面 UI |
| WiX Toolset | 待定 | 构建 MSI 安装包 |
| Google Chrome Stable | 最新官方 Stable | 浏览器运行时 |
| GitHub Releases API | v3 REST | 检查 ChromeIsolator 新版本 |
| Chrome DevTools Protocol | 随 Chrome | 注入轻量环境差异 |

## 变更记录

| 日期 | 变更内容 | 原因 |
|------|----------|------|
| 2026-05-21 | 初始化 Windows 桌面应用架构 | 确认 ChromeIsolator 的平台、模块和安装/数据目录策略 |
