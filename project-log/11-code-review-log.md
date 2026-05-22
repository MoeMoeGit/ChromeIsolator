# 代码评审记录

> 当前代码工程已创建。本文件记录独立评审发现，后续修复后应在 `06-dev-log.md` 记录修复内容。

## 评审流程

```text
A 评审（发现） → B 验证 + 修复确认项 + 评审（发现）
→ C 验证 + 修复确认项 + 评审（发现）
→ D ... → 直到无新问题
```

每一步的验证必须独立，不能直接采信上一棒的结论，要自己读代码、跑测试来判断。

确认的 bug 修复后，对应的 `06-dev-log.md` 记录修复内容。

---

## 评审概览

| 棒次 | 评审人 | 日期 | 发现问题数 | 其中被下一棒确认 |
|------|--------|------|-----------|-----------------|
| 1 | Codex | 2026-05-22 | 3 | 3 |
| 2 | Codex | 2026-05-22 | 0 | — |
| 3 | 用户 + Codex | 2026-05-22 | 11 | 11 |
| 4 | 用户 + Codex | 2026-05-22 | 5 | 5 |
| 5 | 用户 | 2026-05-22 | 12 | — |
| 6 | Codex | 2026-05-22 | 5 | — |

---

## 待评审范围

项目创建代码工程后，优先评审以下模块：

- Profile 管理：配置读写、目录创建、删除到回收站、大小扫描。
- Chrome 管理：Chrome 来源处理、启动参数、端口分配、进程关闭。
- CDP 指纹注入：连接、注入、新 target 处理、断线重连、资源清理。
- 托盘和退出行为：窗口隐藏、退出确认、关闭全部环境。
- 安装包：安装、升级、卸载、用户数据保留。

---

## A — 第一轮评审

**评审人**：Codex  
**日期**：2026-05-22  
**范围**：全量代码、构建脚本、README、安装包构建链路

### 验证内容

- `dotnet build ChromeIsolator.sln`：通过，0 警告，0 错误。
- `scripts/publish-win-x64.ps1`：通过，生成 win-x64 发布目录。
- `scripts/build-msi.ps1`：通过，生成 `ChromeIsolator-Setup-x64.msi`。
- 启动冒烟：通过，应用进程启动且响应。
- 多语言资源 key 完整性：7 个资源文件均包含 96 个 key，无缺失。

### 发现的问题

#### 问题 1：自动 Chrome 准备会执行系统级 MSI 安装，和“尽量独立 Chrome”目标不一致

- **类型**：产品边界 / 安装副作用
- **严重程度**：高
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/Services/ChromeManager.cs:117-181`，`README.md:41`
- **描述**：`PrepareChromeAsync` 下载 `GoogleChromeStandaloneEnterprise64.msi` 后用 `msiexec /i ... /passive /norestart` 和 `runas` 执行标准安装。这会修改系统 Chrome 安装状态、注册表和 Google Update，可能创建系统级安装痕迹；而 README 写的是“目标是尽量准备独立的 Chrome 运行文件”。用户早期也明确担心标准安装影响现有 Chrome。当前行为虽然 profile 不串台，但安装层面并不隔离。
- **复现步骤**：在无 Chrome 的干净 Windows 上首次运行应用，选择下载 / 准备 Chrome。
- **建议修复**：在实现前明确策略：要么删除“独立 Chrome”表述并在 UI/README 明确这是系统级官方 Chrome 安装；要么改为先下载但不自动 `msiexec /i`，提示用户；要么研究可私有化提取方式并只在失败时让用户显式选择系统安装。
- **修复确认**：首次运行和“准备 Chrome”窗口不再自动开始下载 / 安装。Chrome 已存在时只展示隔离说明；Chrome 缺失时需要用户明确点击“安装官方 Chrome”并再次确认才运行官方安装包。README 已改为“共享官方 Chrome 程序文件 + 隔离 profile”的真实策略。

#### 问题 2：取消 Chrome 安装时可能只取消等待，不会停止已启动的 msiexec

- **类型**：Bug / 安装流程
- **严重程度**：中
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/Services/ChromeManager.cs:165-174`，`src/ChromeIsolator.App/DownloadWindow.xaml.cs:88-104`
- **描述**：下载窗口取消时只 cancel token。若已经进入 `msiexec` 阶段，`WaitForExitAsync(cancellationToken)` 会抛出 `OperationCanceledException`，但已经提升权限启动的安装进程不会被终止，安装可能继续在后台进行。用户看到“取消/关闭”，但系统 Chrome 安装仍可能继续。
- **复现步骤**：在 Chrome 准备阶段进入安装步骤后点击取消或关闭窗口。
- **建议修复**：安装阶段禁用“取消”或将按钮文案改为“后台安装中”；或者不要把 cancellation token 传给 `WaitForExitAsync`，等待安装完成后再反馈；如果要支持取消，需要明确尝试终止安装进程并处理权限/子进程。
- **修复确认**：进入安装阶段前仍可取消；进入 `msiexec` 阶段后不再把 cancellation token 传给 `WaitForExitAsync`，并禁用取消 / 关闭窗口，避免用户误以为已终止系统安装。

#### 问题 3：ChromeManager 的进程/注入字典存在跨线程访问风险

- **类型**：线程安全 / 稳定性
- **严重程度**：中
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/Services/ChromeManager.cs:10-12`、`57-66`、`78-106`
- **描述**：`Process.Exited` 事件可能在线程池线程触发，同时 UI 线程也可能调用 Start/Stop/StopAll 修改 `_processes`、`_debugPorts`、`_fingerprintInjectors`。这些都是普通 `Dictionary`，没有锁或主线程封送。多环境并发关闭、用户手动关闭 Chrome 与应用 StopAll 同时发生时，可能出现竞态、异常或状态错乱。
- **复现步骤**：同时运行多个环境，在 Chrome 内手动关闭窗口，同时在托盘/主窗口执行全部关闭。
- **建议修复**：用锁保护这些集合，或统一将 ChromeManager 状态修改封送到单线程；至少在 `Stop` 和 Exited handler 中对 Remove/Dispose 做幂等保护。
- **修复确认**：`ChromeManager` 对 `_processes`、`_debugPorts`、`_fingerprintInjectors` 的读写已增加 `_syncRoot` 锁，并让 `Stop` 与 `Exited` 处理路径幂等移除。

---

## B — 第二轮评审

**评审人**：Codex  
**日期**：2026-05-22  
**范围**：A 轮问题修复、浏览器引擎策略、资源文件和构建

### 验证内容

- `dotnet build ChromeIsolator.sln`：通过，0 警告，0 错误。
- `scripts/build-msi.ps1`：通过，生成 `ChromeIsolator-Setup-x64.msi`。
- 多语言资源 XML 解析：7 个资源文件均通过。
- 多语言资源 key 完整性：7 个资源文件均包含 105 个 key，无缺失。

### 发现的问题

本轮未发现新的阻塞问题。

### 仍需人工验证

- 在干净 Windows 10 / 11 设备上验证 MSI 安装、升级、卸载。
- 验证首次运行三条路径：已安装 Chrome、未安装 Chrome 但使用 Edge 备用、未安装 Chrome 后用户确认安装官方 Chrome。

---

## C — 第三轮用户视角评审

**评审人**：Codex  
**日期**：2026-05-22  
**范围**：首次运行、主窗口、设置页、托盘、异常恢复、浏览器引擎策略文案

### 评审方法

按普通用户路径检查：首次打开 → 浏览器引擎确认 → 创建 / 启动 / 关闭环境 → 设置 → 托盘 → 异常恢复。

### 发现与修复计划

#### 问题 1：右侧详情区存在布局重叠风险

- **类型**：UI Bug
- **严重程度**：高
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/MainWindow.xaml`
- **描述**：“打开环境目录 / 复制路径”按钮未指定 `Grid.Row`，会落在标题区域；高级信息与基础信息同处 `Grid.Row=1`，打开高级详情后可能覆盖。
- **修复计划**：重排右侧详情区为单一垂直布局，让基础信息、操作按钮、高级信息按顺序排列。
- **修复确认**：右侧详情区已改为 `ScrollViewer` + 单列 `StackPanel`，基础信息、主操作、路径操作、高级信息按顺序显示。

#### 问题 2：全部关闭会更新未运行环境的最近使用时间

- **类型**：逻辑 Bug
- **严重程度**：高
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/ViewModels/MainViewModel.cs`
- **描述**：`StopAll()` 对所有环境设置 `LastUsed = DateTime.Now`，导致未运行环境也显示“刚刚使用”。
- **修复计划**：只对原本运行 / 启动中 / 关闭中的环境更新状态和最近使用时间。
- **修复确认**：`StopAll()` 已只处理受影响环境，不再更新时间给未运行环境。

#### 问题 3：双击环境只启动，不会按预期切换启动 / 关闭

- **类型**：交互 Bug
- **严重程度**：中
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/MainWindow.xaml.cs`
- **描述**：产品文档和用户直觉是双击启动或关闭，当前只调用 `StartSelected()`。
- **修复计划**：改为调用选中环境的 `ToggleProfile`。
- **修复确认**：双击环境列表改为启动 / 关闭切换。

#### 问题 4：浏览器方案已变更，但 UI 仍大量使用“准备 Chrome”

- **类型**：文案 / 产品一致性
- **严重程度**：中
- **状态**：已修复
- **位置**：`Resources/Strings*.xaml`、主窗口、设置页、首次窗口
- **描述**：当前策略是浏览器引擎设置，包含 Chrome 和 Edge 备用；“准备 Chrome”会让用户困惑。
- **修复计划**：统一改为“浏览器引擎设置 / 浏览器引擎可用”，保留安装官方 Chrome 的明确动作文案。
- **修复确认**：主窗口、设置页、首次窗口和状态文案已统一到“浏览器引擎”口径；安装动作仍明确为“安装官方 Chrome”。

#### 问题 5：启动 / 关闭按钮状态不够明确

- **类型**：交互问题
- **严重程度**：中
- **状态**：已修复
- **位置**：`MainViewModel.cs`
- **描述**：选中环境后“启动”“关闭”都可点击，部分情况下点击无反馈。
- **修复计划**：按运行状态禁用不可用按钮，启动中 / 关闭中禁用启动和关闭。
- **修复确认**：启动 / 关闭命令增加状态判断，操作前后刷新命令状态。

#### 问题 6：主界面工具栏过于工程化，常用动作层级不清

- **类型**：体验问题
- **严重程度**：中
- **状态**：已修复
- **位置**：`MainWindow.xaml`
- **描述**：主要启动 / 关闭动作只在顶部工具栏，详情区缺少明显主操作。
- **修复计划**：在右侧详情区增加主要操作按钮“启动环境 / 关闭环境”，顶部保留快捷按钮。
- **修复确认**：右侧详情区已增加“启动环境 / 关闭环境”主操作按钮。

#### 问题 7：设置页“重新安装 Chrome”文案不准确

- **类型**：文案问题
- **严重程度**：中
- **状态**：已修复
- **位置**：`SettingsWindow.xaml`、`Resources/Strings*.xaml`
- **描述**：入口实际是浏览器引擎设置，不一定重新安装 Chrome。
- **修复计划**：改为“浏览器引擎设置”。
- **修复确认**：设置页按钮已改为“浏览器引擎设置”。

#### 问题 8：错误文案仍是旧口径

- **类型**：文案问题
- **严重程度**：中
- **状态**：已修复
- **位置**：`Resources/Strings*.xaml`
- **描述**：`ChromeNotFound` 仍写“后续版本会提供自动下载和准备”。
- **修复计划**：改为提示打开浏览器引擎设置安装官方 Chrome 或选择 Edge 备用。
- **修复确认**：`ChromeNotFound` 和相关状态文案已更新为浏览器引擎设置口径。

#### 问题 9：首次窗口已检测到 Chrome 时，“确定”不如“开始使用”自然

- **类型**：体验问题
- **严重程度**：低
- **状态**：已修复
- **位置**：`DownloadWindow.xaml.cs`、`Resources/Strings*.xaml`
- **描述**：首次确认更像 onboarding，建议明确下一步。
- **修复计划**：已检测到 Chrome 时按钮显示“开始使用”。
- **修复确认**：首次窗口检测到 Chrome 时主按钮显示“开始使用”。

#### 问题 10：托盘同时有“全部关闭并退出”和“退出”两个相近入口

- **类型**：体验问题
- **严重程度**：低
- **状态**：已修复
- **位置**：`TrayService.cs`
- **描述**：两个入口语义接近，容易让用户犹豫。
- **修复计划**：保留一个“退出”，如有运行环境则提示会全部关闭并退出。
- **修复确认**：托盘菜单移除“全部关闭并退出”，保留“退出”；退出时仍会提示并关闭运行环境。

#### 问题 11：Edge 备用时状态仍显示“Chrome 可用”

- **类型**：文案 Bug
- **严重程度**：中
- **状态**：已修复
- **位置**：`MainViewModel.cs`、`SettingsViewModel.cs`、`Resources/Strings*.xaml`
- **描述**：当前引擎为 Edge 时使用 `ChromeAvailable` 文案不准确。
- **修复计划**：改为“浏览器引擎可用：版本（来源）”。
- **修复确认**：状态文案已改为“浏览器引擎可用：版本（来源）”，Edge 备用时不再显示为 Chrome 可用。

### 额外修复

- 修复环境列表右键菜单命令绑定：`ContextMenu` 不在 Window 视觉树内，改为通过 `PlacementTarget.DataContext` 访问主 ViewModel。

### 验证内容

- `dotnet build ChromeIsolator.sln`：通过，0 警告，0 错误。
- `scripts/build-msi.ps1`：通过，生成 `ChromeIsolator-Setup-x64.msi`。
- 多语言资源 XML 解析：7 个资源文件均通过。
- 多语言资源 key 完整性：7 个资源文件均包含 108 个 key，无缺失。

---

## D — 第四轮 MSI 安装运行时验证

**评审人**：用户 + Codex
**日期**：2026-05-22
**范围**：MSI 安装后首次启动全流程

### 评审方法

用户从 GitHub Release 下载 v1.0.0 MSI 安装后实际运行，逐步暴露运行时问题。

### 发现的问题

#### 问题 1：MSI 安装后缺少桌面快捷方式

- **类型**：安装包缺陷
- **严重程度**：中
- **状态**：已修复
- **位置**：`installer/Product.wxs`
- **描述**：WiX 安装包只定义了 Start Menu 快捷方式，没有桌面快捷方式。用户安装后桌面无图标。
- **修复确认**：新增 `DesktopFolder` 和桌面快捷方式组件。

#### 问题 2：应用启动静默崩溃，无任何错误提示

- **类型**：致命缺陷
- **严重程度**：高
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/App.xaml.cs`
- **描述**：`App.xaml.cs` 没有任何全局异常处理。`OnStartup` 中任何未捕获异常都会导致 WPF 进程静默退出——没有错误对话框、没有日志、任务管理器里看不到进程。
- **修复确认**：新增 `DispatcherUnhandledException` 和 `AppDomain.CurrentDomain.UnhandledException` 处理器，启动失败时弹出 MessageBox 显示完整错误。

#### 问题 3：MainViewModel 构造函数 NullReferenceException

- **类型**：致命缺陷
- **严重程度**：高
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/ViewModels/MainViewModel.cs:30`
- **描述**：构造函数中 `SelectedProfile = Profiles.FirstOrDefault()` 写在命令初始化之前。setter 触发 `RaiseCommandState()` 时命令属性（`StartSelectedCommand` 等）还是 null，导致 `NullReferenceException`。
- **修复确认**：将 `SelectedProfile` 赋值移到所有命令初始化之后。

#### 问题 4：WPF 图标资源找不到（XAML pack URI 解析失败）

- **类型**：致命缺陷
- **严重程度**：高
- **状态**：已修复
- **位置**：`MainWindow.xaml`、`DownloadWindow.xaml`、`SettingsWindow.xaml`、`ChromeIsolator.App.csproj`
- **描述**：XAML 中 `Icon="Assets/AppIcon.ico"` 使用相对 URI，WPF 将其解析为 pack URI `pack://application:,,,/Assets/AppIcon.ico`。MSI 安装后 self-contained 发布目录中 `Content` 类型的文件无法被 pack URI 正确解析，抛出 `IOException: 找不到资源"assets/appicon.ico"`。
- **修复确认**：将图标从 `Content` 改为 `EmbeddedResource` 嵌入程序集；移除 XAML 中的 `Icon` 引用；新增 `IconHelper` 类从嵌入资源加载图标，在所有窗口构造函数中调用 `IconHelper.ApplyIcon(this)`。

#### 问题 5：XAML FallbackValue 使用 DynamicResource 导致 XamlParseException

- **类型**：致命缺陷
- **严重程度**：高
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/MainWindow.xaml:101`
- **描述**：`{Binding SelectedProfile.Title, FallbackValue={DynamicResource SelectProfile}}` 中，`FallbackValue` 是 `Binding` 类型的属性，不是 `DependencyProperty`，不能使用 `DynamicResource`。编译通过但运行时抛出 `XamlParseException`。
- **修复确认**：改为绑定 ViewModel 属性 `SelectedProfileTitle`，在属性内部使用 `L10n.GetString("SelectProfile")` 作为无选中时的回退文本。

### 验证内容

- `dotnet build ChromeIsolator.sln`：通过，0 警告，0 错误。
- `scripts/build-msi.ps1`：通过，生成 MSI。
- 用户实际安装测试：待验证。

## E — 第五轮 V1.1.0 用户测试反馈

**评审人**：用户
**日期**：2026-05-22
**范围**：V1.1.0 全量功能测试

### 发现的问题

#### 问题 1：工具栏"浏览器引擎设置"按钮多余

- **类型**：UX 冗余
- **严重程度**：低
- **状态**：已修复
- **位置**：`MainWindow.xaml` 工具栏
- **描述**：设置页已有"浏览器引擎设置"入口，工具栏重复出现。该操作频率极低，不应占用工具栏空间。
- **修复计划**：移除工具栏"浏览器引擎设置"按钮。

#### 问题 2：工具栏按钮顺序和图标一致性

- **类型**：UX / 视觉一致性
- **严重程度**：低
- **状态**：已修复
- **位置**：`MainWindow.xaml` 工具栏
- **描述**：移除"浏览器引擎设置"后剩三个按钮：添加环境、全部关闭、设置。需要调整顺序为"添加环境、全部关闭、设置"。"全部关闭"有图标（红色 X），其他两个按钮也应加同风格图标保持一致。
- **修复计划**：调整顺序，为三个按钮统一添加 Segoe Fluent Icons 图标。

#### 问题 3：左侧环境列表标签框太小、缩到左侧

- **类型**：UI Bug
- **严重程度**：中
- **状态**：已修复
- **位置**：`MainWindow.xaml` 左侧 ListBox / ListBoxItem
- **描述**：环境列表行的显示区域很窄，文字被截断或挤在一起，整体缩到左侧。
- **修复计划**：检查 `ModernListBoxItem` 的 `HorizontalContentAlignment`、Padding 和 ListBox 宽度设置，确保行内容正确撑开。

#### 问题 4：全部关闭没有反应（UI 线程阻塞）

- **类型**：严重 Bug
- **严重程度**：高
- **状态**：已修复
- **位置**：`MainViewModel.StopAll()`、`ChromeManager.Stop()`
- **描述**：`StopAll()` 是同步方法，在 UI 线程调用 `_chromeManager.StopAll()`。`ChromeManager.Stop()` 内部对每个环境串行执行：`injector.DisposeAsync().AsTask().Wait(2秒)` + `process.CloseMainWindow()` + `WaitForExit(5000)` + `Kill`。多个环境叠加后 UI 线程长时间阻塞，界面无响应。
- **修复计划**：将 `StopAll` 改为异步执行，使用 `Task.Run` 避免阻塞 UI 线程；或改为并发关闭多个环境。

#### 问题 5：双击已运行的环境应带到前台

- **类型**：交互逻辑
- **严重程度**：中
- **状态**：已修复
- **位置**：`MainWindow.xaml.cs` 双击事件、`MainViewModel.ToggleProfile()`
- **描述**：当前双击已运行环境会触发 `StopProfile`。用户希望双击已运行环境时将其 Chrome 窗口带到前台。
- **已确认方案**：
  - 双击未运行环境：正常启动（保持现有行为）。
  - 双击已运行环境：通过 `process.MainWindowHandle` + Win32 `SetForegroundWindow` 将主窗口带到前台。若开了多个窗口，只激活主窗口，简单合理。

#### 问题 6：手动关闭 Chrome 后 app 状态不同步 + 配额耗尽崩溃

- **类型**：严重 Bug / 稳定性
- **严重程度**：高
- **状态**：已修复
- **位置**：`ChromeManager` Process.Exited 处理、`MainViewModel.OnProfileExited`
- **描述**：用户手动关闭 Chrome 窗口后，app 主界面环境状态仍显示为"运行中"。随后"全部关闭"无响应。最终触发 `Win32Exception (1816): 配额不足，无法处理此命令`，这是 Windows GDI/User handles 耗尽的典型表现。
- **根因分析**：
  - `Process.Exited` 在线程池触发 → `Dispatcher.Invoke` 回 UI 线程。如果 `Stop()` 阻塞 UI 线程（问题 4），Exited 回调堆积导致 Dispatcher 消息队列积压。
  - `FingerprintInjector` 的 WebSocket 和 HttpClient 可能存在句柄泄漏。
  - 多个环境并发 Exited + StopAll 同步阻塞 = 句柄耗尽。
- **修复计划**：与问题 4 一起修复，核心是将 Stop/StopAll 异步化，确保 Exited 回调不被阻塞。

#### 问题 7：设置页多语言下拉框为空

- **类型**：Bug
- **严重程度**：中
- **状态**：已修复
- **位置**：`SettingsWindow.xaml` ComboBox、`SettingsViewModel`
- **描述**：语言下拉框显示为空，没有可选项。
- **修复计划**：检查 `Languages` 绑定和 `ItemTemplate` 的 `Item2` 绑定是否正确匹配 `(string Code, string NativeName)` ValueTuple。

#### 问题 8：删除按钮过大，操作按钮排布需美化

- **类型**：UX / 视觉
- **严重程度**：低
- **状态**：已修复
- **位置**：`MainWindow.xaml` 右侧详情区操作按钮
- **描述**：删除按钮使用 `DestructiveButton` 样式且 `HorizontalAlignment="Stretch"` 全宽显示，但删除是低频操作，不应如此突出。三个操作按钮（打开目录、复制路径、删除）排布需重新美化。
- **修复计划**：缩小删除按钮，统一三个按钮的视觉权重。

#### 问题 9：删除确认弹窗问题（样式、文案、换行符）

- **类型**：UX / Bug
- **严重程度**：中
- **状态**：已修复
- **位置**：`SimpleInputDialog`、`Resources/Strings*.xaml` MsgDeleteConfirm
- **描述**：
  - (a) 确认弹窗是手写 WPF 窗口，样式与应用主题不统一，输入框很小。
  - (b) 用户不确定要输入"环境1"还是重命名后的自定义名称。当前 `Title` 属性在有自定义名时返回 `"环境1 - 自定义名"`，没有时返回 `"环境1"`，验证逻辑是 `confirm != SelectedProfile.Title`，用户需要精确匹配整个 Title 字符串。
  - (c) `MsgDeleteConfirm` 资源字符串中有 `\n` 字面量未正确渲染为换行。
- **已确认方案**：
  - 美化 `SimpleInputDialog` 样式，应用主题色和一致的控件风格。
  - (b) 改为输入固定英文 `delete` 确认，不再要求匹配环境名称。
  - (c) 修复换行符：XAML `<sys:String>` 中 `\n` 是字面量，需改为 `&#x0a;`。

#### 问题 10：托盘右键菜单逻辑和样式

- **类型**：UX
- **严重程度**：低
- **状态**：已修复
- **位置**：`TrayService.cs`
- **描述**：用户要求从专业产品经理角度检查托盘菜单逻辑并美化样式。
- **已确认方案**：
  - 逻辑优化：当前"全部关闭并退出"和"退出"仍有语义重叠。建议合并为一个"退出"，退出时始终提示并关闭运行环境。"全部关闭"保留为独立操作（不退出）。"打开管理面板"放在更显眼位置（环境列表之后、其他操作之前）。
  - 样式美化：ContextMenuStrip 无法深度自定义样式（WinForms 限制），但可以通过调整菜单项文案和分组来改善体验。

#### 问题 11：多环境启动后浏览器窗口重叠

- **类型**：UX
- **严重程度**：中
- **状态**：已修复
- **位置**：`ChromeManager.Start()` 启动参数
- **描述**：启动多个环境后，Chrome 窗口完全重叠在同一位置。
- **修复计划**：在启动参数中添加 `--window-position=x,y`，按环境编号偏移窗口位置。

#### 问题 12：修复策略

- **描述**：能直接修复的先修，涉及操作或代码逻辑变动的先讨论。

---

## F — 第六轮代码复审（V1.1.0 修复后）

**评审人**：Codex
**日期**：2026-05-22
**范围**：V1.1.0 用户测试 12 项修复的逐项代码审查 + 内存泄漏审计

### 评审方法

逐项检查 12 项修复的完整性，同时审计 FingerprintInjector、ChromeManager、TrayService 的资源释放和事件订阅。

### 发现的问题

#### 问题 1：StopSelected / StopAllCommand 异步调用无异常处理

- **类型**：Bug / 稳定性
- **严重程度**：中
- **状态**：已修复
- **位置**：`MainViewModel.cs:321-326`、`MainViewModel.cs:36`、`TrayService.cs:62,65`
- **描述**：`StopSelected` 和 `StopAllCommand` 使用 `_ = StopProfileAsync()` / `_ = StopAllAsync()` fire-and-forget。异常会变成未观察的 Task 异常，触发 `UnobservedTaskException`，用户看到通用错误弹窗。
- **修复确认**：新增 `StopProfileSafeAsync` / `StopAllSafeAsync`（internal），内部 try-catch 弹窗提示。TrayService 同步改用安全包装。

#### 问题 2：StopAllAsync / StopProfileAsync 的 Task.Run 后续代码在非 UI 线程

- **类型**：线程安全
- **严重程度**：中
- **状态**：已修复
- **位置**：`MainViewModel.cs:169`、`MainViewModel.cs:439`
- **描述**：`Task.Run` 默认不捕获 `SynchronizationContext`，续接代码可能在非 UI 线程执行，与 `OnProfileExited` 的 `BeginInvoke` 竞态操作 `profile.IsRunning` 等 UI 属性。
- **修复确认**：两处 `Task.Run` 均加 `.ConfigureAwait(true)` 确保续接 UI 线程。

#### 问题 3：删除确认弹窗高度对长文案语言不够

- **类型**：UI Bug
- **严重程度**：低
- **状态**：已修复
- **位置**：`SimpleInputDialog.cs:91`
- **描述**：固定 `Height=200`，德语/俄语文案较长时显示不全。
- **修复确认**：改为 `SizeToContent=Height` + `MaxHeight=400`。

#### 问题 4：窗口偏移用 InstanceNumber 导致删除环境后偏移过远

- **类型**：UX 问题
- **严重程度**：低
- **状态**：已修复
- **位置**：`ChromeManager.cs:83-86`
- **描述**：`--window-position` 按 `profile.InstanceNumber` 偏移，删除低编号环境后保留的高编号环境偏移过大。
- **修复确认**：改为按 `_processes.Count`（当前运行环境数）偏移。

#### 问题 5：Process.Exited handler 未调用 process.Dispose()，原生句柄泄漏

- **类型**：内存泄漏
- **严重程度**：高
- **状态**：已修复
- **位置**：`ChromeManager.cs:102-121`
- **描述**：`Process.Exited` handler 从字典移除 process 引用但未调用 `Dispose()`，导致 `SafeProcessHandle` 等原生资源泄漏。多环境反复开关后句柄累积。
- **修复确认**：handler 中取出 `exitedProcess` 引用后调用 `exitedProcess?.Dispose()`。与 `Stop()` 的 `finally { process.Dispose(); }` 通过字典 `Remove` 互斥，不会双重释放。

### 验证内容

- `dotnet build ChromeIsolator.sln`：通过，0 警告，0 错误。

---

## 变更记录

| 日期 | 变更内容 | 原因 |
|------|----------|------|
| 2026-05-22 | 新增第六轮代码复审，发现 5 项问题（4 代码缺陷 + 1 内存泄漏） | 对 V1.1.0 用户测试 12 项修复进行逐项代码审查和内存泄漏审计 |
| 2026-05-22 | 新增第四轮 MSI 安装运行时验证，发现 5 项运行时致命/中等问题 | 用户从 Release 安装后实际运行，暴露编译时无法发现的运行时问题 |
| 2026-05-22 | 新增第三轮用户视角评审和 11 项修复计划 | 用户要求从整体页面、交互、逻辑重新检查并修复 |
| 2026-05-22 | 新增第一轮代码评审记录 | 同事完成后续开发后进行验收和独立评审 |
| 2026-05-21 | 标注尚未进入代码评审阶段，并记录后续重点范围 | 当前只有规划文档，尚无代码工程 |
