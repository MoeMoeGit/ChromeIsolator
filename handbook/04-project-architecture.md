# 架构约定

> 适用状态：启用。项目是单进程 WPF 桌面应用，没有服务端。

## 系统边界

| 模块 / 目录 | 职责 | 主要依赖 | 不承担什么 |
| --- | --- | --- | --- |
| `App.xaml.cs` | 启动、单实例、命名管道、全局异常、退出兜底 | ViewModel、托盘、配置与浏览器服务 | 业务 UI |
| `Models/` | 配置和环境的持久化模型 | 无 | 进程状态和界面逻辑 |
| `Services/ProfileManager.cs` | 环境编号、配置与磁盘目录协调、回收站删除 | `ConfigStore`、`AppPaths` | 浏览器进程控制 |
| `Services/ChromeManager.cs` | 浏览器发现、启动、跟踪、关闭、调试端口 | Process、注册表、CDP | 页面业务采集 |
| `Services/FingerprintInjector.cs` | CDP 连接和两个 navigator 属性注入 | HTTP、WebSocket | 完整指纹模拟 |
| `ViewModels/` | UI 状态、命令、排序、外部链接队列 | Models、Services | 持久化底层细节 |
| `*.xaml`、`Themes/`、`Resources/` | 窗口、样式和三语言资源 | ViewModels | 核心浏览器生命周期 |
| `installer/`、`scripts/`、`.github/` | 发布、MSI 和 CI | .NET、WiX、GitHub Actions | 运行时用户数据 |

## 关键路径

- 启动：`App.OnStartup` → 单实例判断 → `ConfigStore.Load` → `ProfileManager` 对齐磁盘 → `MainViewModel` → 主窗口与托盘。
- 浏览器：UI 命令 → `MainViewModel` → `ChromeManager.Start/StopAsync` → 独立 profile、可选 CDP → 进程事件回写状态。
- 配置：ViewModel/Manager 修改模型 → `ConfigStore.Save` → 临时文件 + `File.Replace` → 主配置与 `.bak`。
- 外部链接：进程参数 → 用户专属命名管道 → `HandleExternalLink` → 选定环境 → 启动、排队或追加 URL。
- 发行：`Directory.Build.props` 版本 → PowerShell 发布脚本 → WiX MSI → tag 触发 GitHub Release。

## 公共能力与约束

- `AppPaths` 是用户数据路径的唯一入口；不要在功能代码重复拼接 `%LOCALAPPDATA%`。
- `ProfileManager` 是环境持久化操作入口，`ChromeManager` 是运行时进程状态入口；不要建立第二套环境身份或进程字典。
- `L10n` 和资源字典是用户文案入口；新增文案必须同步三语言或明确回退策略。
- 浏览器进程、端口和注入器字典受锁保护；退出事件和 UI 命令会并发访问，相关改动必须复核竞态和释放。
- WPF UI 状态只能安全地在 UI 线程更新；异步停止和事件回调不得直接跨线程改绑定属性。

架构选择的原因见 [11 决策与经验](11-decisions.md)，验证方式见 [10](10-quality.md)。
