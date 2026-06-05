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
| 7 | Codex | 2026-05-25 | 2 | 2 |
| 8 | Codex | 2026-05-25 | 3 | 3 |
| 9 | Codex + 用户 | 2026-05-25 | 2 个待办 + 2 个后续优化 | — |

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

## G — 第七轮代码复审（V1.6.10 外部链接链路）

**评审人**：Codex
**日期**：2026-05-25
**范围**：环境备注、外部链接接收、默认浏览器注册、运行中环境追加 URL、首次外部链接入口

### 评审方法

先阅读 `project-log/README.md`、当前状态、项目概述、功能设计、架构和历史评审记录，再重点审查 V1.6.10 新增的外部链接链路。对每条发现做二次自检，并与用户确认产品取舍。

### 已确认问题

#### 问题 1：运行中环境追加外部链接时未释放短生命周期 Process 对象

- **类型**：资源泄漏 / 稳定性
- **严重程度**：中
- **状态**：待修复
- **位置**：`src/ChromeIsolator.App/Services/ChromeManager.cs:179-196`
- **描述**：运行中环境收到外部链接时，`ChromeManager.OpenUrl()` 会再次调用 `Process.Start(startInfo)`，让 Chrome 把 URL 交给同一 `--user-data-dir` 的已有进程处理。但该方法没有保存并释放返回的 `Process` 对象。高频外部链接场景可能累积原生进程句柄。
- **自检结论**：这条路径不同于主环境启动路径。主启动路径已有 `Process.Exited` 和 `Stop()` 释放逻辑，但追加 URL 的短生命周期进程没有进入 `_processes` 字典，也没有 `Dispose()`。立即释放返回的 `Process` 对象不会关闭 Chrome，只释放本地句柄，因此确认是真问题。
- **建议修复**：将返回值保存为局部变量并立即 `Dispose()`，例如 `using var process = Process.Start(startInfo);`。

#### 问题 2：首次由外部链接启动应用时绕过首次引导

- **类型**：流程 / UX
- **严重程度**：中
- **状态**：待修复
- **位置**：`src/ChromeIsolator.App/App.xaml.cs:63-70`、`src/ChromeIsolator.App/ViewModels/MainViewModel.cs:556-597`
- **描述**：普通启动会显示主窗口并调用 `ShowDownloadIfNeeded()`；但外部链接作为启动参数进入时，只调用 `HandleExternalLink()`。如果浏览器引擎未就绪或没有环境，用户只看到复制链接提示，关闭后应用仍在托盘中，缺少下一步引导。
- **自检结论**：Chrome 已就绪时链接可以打开，不是崩溃级问题；但首次使用和异常恢复路径确实断裂。确认需要优化失败路径：在无法完成外部链接处理时显示主窗口，并给出更明确的下一步。
- **建议修复**：外部链接处理遇到无环境、浏览器引擎未就绪或启动失败时，主动显示主窗口；浏览器未就绪时可继续打开浏览器引擎设置窗口，由用户确认安装 Chrome 或选择 Edge 备用。

### 已排除问题

#### 排除项 1：默认浏览器注册后不检测、不维持

- **类型**：产品取舍
- **状态**：已确认不是问题
- **位置**：`src/ChromeIsolator.App/Services/ShellService.cs:34-67`
- **说明**：当前实现只向 Windows 注册浏览器能力并打开默认应用设置，不检测设置是否成功，也不持续维持默认浏览器身份。用户确认这是产品设计：只给系统信号，不和其他 app 抢默认浏览器，是否设为默认交由用户处理。

#### 排除项 2：设置页打开时把未配置的外部链接目标写成第一个环境

- **类型**：产品取舍
- **状态**：已确认不是问题
- **位置**：`src/ChromeIsolator.App/ViewModels/SettingsViewModel.cs:197-216`
- **说明**：当前实现打开设置页时会把未配置的外部链接目标落为第一个环境。用户确认这是有意设计，用于简化代码逻辑，并交由用户在设置页修改。

### 优化建议复核

1. 外部链接错误提示增加恢复引导：保留，必要性中等。无环境或浏览器未就绪时，用户需要知道下一步是创建环境或完成浏览器引擎设置。
2. “设为默认浏览器”按钮点击后增加成功检测或持续状态：撤回，不建议做。默认浏览器状态交由 Windows 和用户处理。
3. 外部链接目标增加“自动：编号最小环境”：撤回，不建议做。当前自动写第一个环境是已确认设计。
4. 外部链接首次启动失败时显示主窗口：保留，必要性较高。能避免用户关闭错误提示后只剩托盘驻留、没有下一步入口。

### 验证内容

- `dotnet build ChromeIsolator.sln`：通过，0 警告，0 错误。
- `dotnet clean ChromeIsolator.sln`：已清理本轮 Debug 构建输出。
- Windows 默认应用页识别和真实外部 App 链接转发仍需实机验证。

---

## H — 第八轮复核（持久化与状态恢复）

**评审人**：Codex
**日期**：2026-05-25
**范围**：最近使用时间持久化、单个关闭失败状态恢复、配置原子写入、设置页状态刷新

### 复核结论

这一轮对前一轮提出的三项确认问题做了二次自检，结论都成立，因此已全部修复。

#### 已确认并修复 1：最近使用时间未持久化

- **类型**：功能缺口 / 排序数据丢失
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/Models/Profile.cs`、`src/ChromeIsolator.App/ViewModels/ProfileViewModel.cs`、`src/ChromeIsolator.App/ViewModels/MainViewModel.cs`
- **自检结论**：`LastUsed` 原先只存在于 `ProfileViewModel`，不会写回 `config.json`，重启后确实会丢失。该项会影响环境排序和“最近使用”展示，属于真实问题。
- **修复结果**：`Profile` 新增 `LastUsed`，`ProfileViewModel` 读写同步到模型，启动、关闭、运行中追加 URL 和全部关闭后都会保存配置。

#### 已确认并修复 2：单个环境关闭失败时状态可能卡住

- **类型**：稳定性 / UI 状态恢复
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/ViewModels/MainViewModel.cs`
- **自检结论**：`StopProfileAsync()` 在 `await _chromeManager.Stop(...)` 抛异常时，后续复位代码不会执行，`IsStopping` 可能一直保持 `true`。这会让按钮和状态显示失真，是真问题。
- **修复结果**：停止流程改为 `try/catch/finally`，失败时恢复运行状态和调试端口，结束时统一复位 `IsStopping`。

#### 已确认并修复 3：配置写入不是原子操作

- **类型**：稳定性 / 数据安全
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/Services/ConfigStore.cs`、`src/ChromeIsolator.App/Services/AppPaths.cs`
- **自检结论**：原先直接覆盖写 `config.json`，如果写入中断，配置有损坏风险。因为这是本地用户数据入口，哪怕概率不高，也值得修。
- **修复结果**：保存改为写临时文件后 `File.Replace`，同时保留 `config.json.bak`；读取主配置失败时回退备份。

### 复核的优化项

1. 设置页差异模式状态刷新：保留并落地。通过订阅环境退出事件刷新状态，减少设置页长时间打开时的过期显示。
2. 外部链接失败时恢复引导：上一轮已落地，保留。

### 验证内容

- `dotnet build ChromeIsolator.sln`
- `dotnet clean ChromeIsolator.sln`

### 验证结果

- 通过。编译 0 警告、0 错误。
- 通过。Debug 构建输出已清理。

## I — 第九轮复核（冷启动唤醒、浏览器引擎展示与后续待办）

**评审人**：Codex + 用户
**日期**：2026-05-25
**范围**：上一轮评审中提出的单实例唤醒可靠性、主窗口高级浏览器信息刷新、Chrome 官方下载入口、多语言默认环境名。

### 复核结论

本轮只做复核和记录，不修改实现代码。

#### 待办 1：冷启动期间第二实例 / 外部链接唤醒可能丢失

- **类型**：可靠性 / 外部链接入口
- **状态**：待后续版本处理
- **位置**：`src/ChromeIsolator.App/App.xaml.cs`
- **复核结论**：确认更接近 bug，不是产品特性。当前第一实例在完成配置加载、ViewModel / Window / Tray 初始化后才启动 named pipe 监听；第二实例发现 mutex 已存在后只连接 pipe 5 次，每次 250ms，失败后静默退出。正常已启动状态下没问题，但在冷启动极短窗口内连续双击或系统把外部链接交给正在启动的实例时，唤醒消息存在丢失风险。
- **修改风险评估**：直接延长重试风险较低，但会让失败的第二实例多停留一会；把监听前置或引入启动握手更彻底，但改动范围更大，需要注意 ViewModel 尚未就绪时的消息缓存、Dispatcher 生命周期和退出清理。
- **后续建议**：优先采用低风险方案：延长第二实例连接重试并在第一实例 listener ready 后再接受唤醒；如要前置监听，需要先设计消息暂存队列。

#### 待办 2：主窗口高级浏览器信息在引擎变化后可能不刷新

- **类型**：UI 展示 bug
- **状态**：待后续版本处理
- **位置**：`src/ChromeIsolator.App/ViewModels/MainViewModel.cs`
- **复核结论**：确认是 bug，不是特性。`ChromeVersionText` 和 `ChromePathText` 是基于 `_chromeManager.CurrentChrome` 的只读计算属性；浏览器引擎完成安装、重新检测或启用 Edge 备用后，`RefreshChromeStatus()` 只更新 `ChromeStatusText`，没有通知这两个属性变化。影响范围限于高级详情里的版本 / 路径展示，不影响启动环境。
- **修改风险评估**：低。可在 `RefreshChromeStatus()` 内补发 `OnPropertyChanged(nameof(ChromeVersionText))` 和 `OnPropertyChanged(nameof(ChromePathText))`。风险主要是多触发几次文件 / 注册表查询，但频率很低。

#### 待办 3：Chrome 官方下载入口改为优先 com，失败再 cn

- **类型**：体验 / 网络可达性
- **状态**：待后续版本处理
- **位置**：`src/ChromeIsolator.App/DownloadWindow.xaml.cs`
- **用户确认**：原设想是优先连通 Google Chrome 的 `.com` 官方网站，连通不了再走 `.cn` 官方页面。当前代码固定打开 `https://www.google.cn/chrome/`，先记录，后续再改逻辑。
- **修改风险评估**：中低。需要避免点击按钮后卡住 UI；建议异步探测 `.com` 可达性，短超时失败后打开 `.cn`，并确保两个入口都只指向 Google 官方域名。

#### 后续优化 1：多语言默认环境名应本地化

- **类型**：多语言 / 产品完整性
- **状态**：后续版本实现
- **位置**：`src/ChromeIsolator.App/ViewModels/ProfileViewModel.cs`、`src/ChromeIsolator.App/ViewModels/SettingsViewModel.cs`
- **说明**：当前默认环境名只按英文 / 非英文二分，非英文语言会显示中文“环境N”。用户确认要改，但不是本轮实现。后续应新增资源字符串，例如 `ProfileDefaultNameFormat`，各语言分别提供“Profile {0} / 环境{0} / プロファイル {0} ...”。

#### 后续优化 2：继续收敛外部链接和唤醒入口的可靠性

- **类型**：流程 / 可用性
- **状态**：后续版本处理
- **说明**：与“待办 1”关联。后续优化时同时检查普通双击唤醒、外部链接冷启动、外部链接转发到已启动实例三条路径，避免只修其中一条。

---

## J — 第十轮复核（主窗口位置与左右栏持久化）

**评审人**：Codex
**日期**：2026-05-26
**范围**：主窗口大小 / 位置 / 最大化状态恢复、左右栏拖动位置持久化、启动入口与 UI 一致性。

### 评审方法

先复读这轮 diff，再检查窗口生命周期、配置读写、启动入口和布局约束，重点看是否会引入小屏恢复失衡、最小化状态污染配置或重复恢复的问题。

### 发现与修复

#### 问题 1：左栏宽度恢复未按当前窗口可用空间做上限约束

- **类型**：UI 布局 bug
- **严重程度**：中
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/MainWindow.xaml(.cs)`
- **描述**：如果用户在大窗口里把左栏拖得很宽，随后在较小分辨率或更窄窗口里启动，直接套用保存值会压缩右侧详情区，甚至让布局显得失衡。
- **修复确认**：恢复时根据当前窗口宽度、主区域边距、分隔条宽度和右栏最小宽度对左栏宽度做上限约束，保证左右栏仍可正常阅读。

### 验证内容

- `dotnet build ChromeIsolator.sln`

### 验证结果

- 通过。编译 0 警告、0 错误。

---

## I — 第九轮复核（差异模式、默认浏览器和外部链接兜底）

**评审人**：Codex
**日期**：2026-06-05
**范围**：对 2026-06-05 初步评审中提出的 bug / 风险 / 优化项逐条二次自检，并修复确认项

### 复核结论

本轮对每一条候选问题重新读代码、对照项目设计和用户路径后，确认 6 项需要落地：4 项已确认问题、2 项待确认风险的防御性优化。未保留“外部链接默认目标自动写入配置”为问题，因为项目历史记录已确认这是有意设计。

#### 已确认并修复 1：差异模式注入失败会静默降级

- **类型**：功能可靠性 / 状态反馈
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/Services/FingerprintInjector.cs`、`src/ChromeIsolator.App/Services/ChromeManager.cs`、`src/ChromeIsolator.App/ViewModels/MainViewModel.cs`
- **自检结论**：`ChromeManager.Start()` 使用 `_ = injector.StartAsync()` 后台启动注入器；`FingerprintInjector.RunAsync()` 重试耗尽后直接退出，没有把失败回写 UI。用户会看到差异模式和端口，但实际可能未注入 navigator 差异，是真问题。
- **修复结果**：`FingerprintInjector` 增加失败事件，`ChromeManager` 转发到 profile 级警告，`MainViewModel` 在 UI 线程写入环境错误提示，说明浏览器可继续使用但本次可能按基础模式运行，并提示关闭后重新启动可重试。

#### 已确认并修复 2：“设为默认浏览器”异常会走全局未处理异常路径

- **类型**：稳定性 / 设置流程
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/Services/ShellService.cs`、`src/ChromeIsolator.App/ViewModels/SettingsViewModel.cs`
- **自检结论**：设置页命令直接执行注册表写入和 `ms-settings:defaultapps`，异常未局部捕获；Shell 或注册表失败时可能进入全局 fatal handler。该操作依赖 Windows 系统状态，必须局部处理。
- **修复结果**：设置页改为包装方法，成功时提示用户在 Windows 默认应用设置中选择 ChromeIsolator，失败时用普通错误弹窗展示原因，不退出应用。

#### 已确认并修复 3：默认浏览器注册信息偏薄，真实识别仍需实机验证

- **类型**：兼容性风险 / Windows 注册
- **状态**：已加固，仍需实机验证
- **位置**：`src/ChromeIsolator.App/Services/ShellService.cs`
- **自检结论**：原实现写了 `RegisteredApplications`、Capabilities 和 ProgId；这可能足够，但默认浏览器路径在 Windows 10/11 上存在版本差异。补齐 StartMenuInternet 客户端入口、图标和 open command 可以降低默认应用设置页识别风险。
- **修复结果**：在 HKCU 下补充 `Software\Clients\StartMenuInternet\ChromeIsolator` 的默认名称、图标和启动命令。真实默认应用 UI 选择与 http / https 转发仍需 Windows 实机验证。

#### 已确认并修复 4：高级信息里的内存值语义不准确

- **类型**：用户信息准确性
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/ViewModels/MainViewModel.cs`、`src/ChromeIsolator.App/Resources/Strings*.xaml`
- **自检结论**：原先用 `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` 展示“内存”，这不是系统物理可用内存，用户容易误读。
- **修复结果**：改用 Windows `GlobalMemoryStatusEx` 获取可用物理内存；7 语言标签同步改为“可用物理内存”语义；高级信息标签列加宽并允许换行，避免多语言长标签截断。

#### 已确认并修复 5：README 双击行为与实际设计不一致

- **类型**：文档准确性 / 用户预期
- **状态**：已修复
- **位置**：`README.md`
- **自检结论**：代码和历史日志确认双击已运行环境的设计是带到前台，不是关闭；README 仍写“启动或关闭环境”，属于过期文档。
- **修复结果**：README 改为“双击未运行环境启动；双击运行中环境带到前台”。

#### 已确认并修复 6：第二实例管道转发失败时请求会静默丢失

- **类型**：稳定性 / 外部链接兜底
- **状态**：已修复
- **位置**：`src/ChromeIsolator.App/App.xaml.cs`
- **自检结论**：第二实例连接已有实例管道失败后直接退出。虽然正常情况下概率不高，但如果已有实例卡住或监听异常，桌面双击唤醒或外部链接会无反馈消失。
- **修复结果**：`NotifyExistingInstance` 返回成功/失败；失败时第二实例弹窗说明已有实例无法唤醒或链接无法发送；外部链接转发失败时会自动复制链接，避免静默吞掉用户操作。

### 验证内容

- 7 个多语言资源 XAML 文件 XML 解析。
- 7 个多语言资源 key 完整性检查。
- `git diff --check`。
- 本地产物检查。
- `dotnet build ChromeIsolator.sln`：未运行成功，原因是当前机器没有 `dotnet` 命令。

### 验证结果

- 通过。7 个资源文件 XML 均可解析。
- 通过。7 个资源文件均为 144 个 key，无缺失、无额外 key。
- 通过。无空白错误。
- 通过。本轮未生成 `bin/`、`obj/`、`artifacts/` 等产物。
- 未通过构建验证。需要在安装 .NET 8 SDK / Windows WPF 构建环境后再执行 `dotnet build ChromeIsolator.sln`。

---

## 变更记录

| 日期 | 变更内容 | 原因 |
|------|----------|------|
| 2026-06-05 | 新增第九轮复核，确认并修复差异模式失败反馈、默认浏览器异常处理和注册加固、内存信息语义、README 双击描述、第二实例转发失败兜底 | 对用户要求的每条 bug / 优化项二次自检后落地确认项 |
| 2026-05-25 | 新增第九轮复核，记录冷启动唤醒可靠性、高级浏览器信息刷新、Chrome 下载入口和多语言默认环境名后续待办 | 用户要求先全面复核确认问题性质，并只记录后续待办，不立即实现 |
| 2026-05-25 | 新增第八轮复核，确认并修复最近使用时间持久化、单个关闭失败状态恢复和配置原子写入 | 对前一轮提出的问题再次自检后，确认需要全部落地 |
| 2026-05-25 | 新增第七轮代码复审，确认外部链接链路 2 项问题并排除 2 项产品取舍 | 复核 V1.6.10 环境备注和外部链接改动，明确默认浏览器注册与默认目标写入的边界 |
| 2026-05-22 | 新增第六轮代码复审，发现 5 项问题（4 代码缺陷 + 1 内存泄漏） | 对 V1.1.0 用户测试 12 项修复进行逐项代码审查和内存泄漏审计 |
| 2026-05-22 | 新增第四轮 MSI 安装运行时验证，发现 5 项运行时致命/中等问题 | 用户从 Release 安装后实际运行，暴露编译时无法发现的运行时问题 |
| 2026-05-22 | 新增第三轮用户视角评审和 11 项修复计划 | 用户要求从整体页面、交互、逻辑重新检查并修复 |
| 2026-05-22 | 新增第一轮代码评审记录 | 同事完成后续开发后进行验收和独立评审 |
| 2026-05-21 | 标注尚未进入代码评审阶段，并记录后续重点范围 | 当前只有规划文档，尚无代码工程 |
