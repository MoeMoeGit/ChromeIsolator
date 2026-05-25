# 当前状态

> **最后更新**：2026-05-25
> **最后更新人**：Codex
> **最近开发日志**：`06-dev-log.md` 中的 2026-05-25（V1.6.9 发布触发）
> **当前可信度**：已复核

## 当前版本

**V1.6.9** — 默认使用兼容模式，不启用调试端口、不注入页面脚本；设置页新增“环境差异模式”，可按已关闭环境单独启用轻量 navigator 参数注入。

## 当前阶段

V1.6.9 准备用于触发 GitHub Actions 构建测试包。当前重点是验证默认兼容模式下特定网站访问、按环境启用差异模式、关闭环境流程和设置页状态刷新。

## 已完成

- 已读取并分析原项目 `D:\projects\BrowserIsolator` 的 README 和核心 Swift 实现。
- 已确认 ChromeIsolator 是 BrowserIsolator 的 Windows 版功能复刻。
- 已确认目标平台：Windows 10 / Windows 11，x86-64，64 位设备。
- 已确认中文产品名继续使用“浏览器多开”，英文 / 项目名为 ChromeIsolator。
- 已确认保留多语言。
- 已确认不迁移 macOS 数据，只复刻功能。
- 已确认提供安装包，优先 MSI。
- 已确认主程序安装到 `%ProgramFiles%\ChromeIsolator`，用户数据放到 `%LOCALAPPDATA%\ChromeIsolator`。
- 已确认关闭窗口隐藏到系统托盘，退出前关闭所有运行环境。
- 已确认删除环境移入 Windows 回收站。
- 已确认只做轻量环境差异，不扩展为完整反检测平台。
- 已确认早期 Release 可未签名发布，Windows 安全提示可接受；签名作为后续增强。
- 已补齐第一版 project-log 规划文档。
- 已将 `project-log/` 和常见 IDE / AI 工具本地文件加入 `.gitignore`。
- 已创建 ChromeIsolator 初版应用图标资产：SVG 源文件、PNG 预览和 Windows ICO。
- 已创建对外 `README.md`、`LICENSE` 和 `NOTICE`。
- 已将 README / License / 图标首个提交推送到 GitHub。
- 已创建 WPF / .NET 8 Windows 桌面应用骨架。
- 已实现第一版 Profile 配置读写、环境列表 UI、新增、重命名、删除到回收站、基础 Chrome 启停、端口分配和托盘驻留。
- 已将代码骨架提交并推送到 GitHub。
- 已安装并验证 .NET SDK，`dotnet build ChromeIsolator.sln` 通过。
- 已实现 CDP 轻量指纹注入服务，并接入 Chrome 启停生命周期。
- 已实现 Chrome / Edge 引擎发现、版本显示和浏览器引擎设置入口。
- 已新增 win-x64 发布脚本并验证 `dotnet publish`。
- 已新增设置窗口，支持打开数据目录、Profiles 目录、Chrome 目录、复制路径、检查更新、打开 Releases / Issues、复制邮箱。
- 已新增 WiX MSI 构建脚本并验证生成 `ChromeIsolator-Setup-x64.msi`。
- 已新增 GitHub Actions Windows 构建工作流，上传 publish 和 MSI artifacts。
- 已统一版本号到 `Directory.Build.props`，当前版本 `1.0.0`。
- 已将 Release 发布改为 self-contained，用户无需预装 .NET Runtime。
- 已将构建产物命名改为带版本号，并在 tag 构建时自动创建 GitHub Release。
- 已新增浏览器引擎设置窗口（DownloadWindow），支持首次说明、用户确认安装官方 Chrome、进度百分比、状态文字、取消。
- 已实现 Profile 排序（运行中优先，最近使用次之）和磁盘占用显示。
- 已实现空状态提示、删除确认（输入环境名称验证）、退出确认（运行中环境提示）。
- 已实现双击环境列表启动 Profile、托盘菜单"检查更新"。
- 已实现高级详情开关（Chrome 版本/路径、CPU 核心、可用内存），设置页可切换显示。
- 已实现多语言支持（7 语言：中/英/日/韩/德/法/俄），自动检测系统语言，设置页可切换，所有 UI 字符串已本地化。
- 已实现 CDP 注入重连机制：WebSocket 断开后最多 5 次指数退避重连（1s/2s/4s/8s/16s），重连后重新注入所有页面。
- 已新增下载页"使用已安装的 Chrome"按钮，支持重新检测并使用系统已安装的 Chrome。
- 已新增设置页"重新安装 Chrome"按钮，支持从设置页打开浏览器引擎设置窗口，由用户确认后安装官方 Chrome。
- 已新增下载失败时"重试"和"复制错误详情"按钮。
- 已新增环境详情"重试"和"清除错误"按钮，支持对启动失败的环境重试或清除错误状态。
- 已新增启动中/关闭中状态显示（橙色指示器，区别于运行中绿色和未启动灰色）。
- 已实现相对日期显示（"今天"、"昨天"、"3 天前"等），替代绝对日期时间格式。
- 已新增删除确认对话框显示数据大小。
- 已新增添加环境后自动弹出重命名对话框。
- 已新增环境列表右键上下文菜单（重命名、删除）。
- 已新增环境详情区"打开环境目录"和"复制环境路径"按钮。
- 已实现磁盘占用异步计算，避免大目录阻塞 UI。
- 已新增托盘菜单"全部关闭并退出"选项，退出前等待 Chrome 进程结束。
- 已按最终 Chrome 策略改造首次运行窗口：不再自动下载 / 自动安装；Chrome 已安装时只说明隔离策略；Chrome 未安装时必须用户点击后才安装官方 Stable Chrome。
- 已新增 Microsoft Edge Stable 备用引擎检测。仅在 Chrome 缺失且用户明确选择后启用，仍使用 ChromeIsolator 独立 `--user-data-dir`。
- 已移除启动参数中的 `--test-type`，降低隔离环境被用户或网站识别为测试浏览器的风险。
- 已为 ChromeManager 进程、端口和注入器字典增加锁保护，降低退出事件和 UI 操作并发导致的状态错乱风险。
- 已修复 MSI 安装后应用静默崩溃问题：`App.xaml.cs` 新增全局异常处理器（`DispatcherUnhandledException` + `AppDomain.UnhandledException`），启动失败时显示错误对话框而非静默退出。
- 已修复 MSI 安装后缺少桌面快捷方式问题：`installer/Product.wxs` 新增桌面快捷方式组件。
- 已修复 `MainViewModel` 构造函数 `NullReferenceException`：`SelectedProfile` 赋值移到命令初始化之后。
- 已修复 WPF 图标资源找不到问题：图标从 `Content` 改为 `EmbeddedResource`，新增 `IconHelper` 类从嵌入资源加载。
- 已修复 XAML `FallbackValue` 使用 `DynamicResource` 导致的 `XamlParseException`：改为绑定 ViewModel 属性。
- 已优化浏览器引擎设置窗口 UX：Chrome 已就绪时隐藏进度条、百分比和关闭按钮，只显示"开始使用"一个按钮。
- 已完成主界面 UI 全面重设计：新增主题系统（Colors.xaml + Controls.xaml）、现代化控件样式（PrimaryButton/SecondaryButton/ToolbarButton/DestructiveButton/InlineStartButton/InlineStopButton/ModernListBoxItem）、行内启动/停止按钮、工具栏逻辑分组、GridSplitter 可调面板、托盘菜单运行中/已停止分组显示、设置页语言下拉框修复、按钮文字缩短统一、SelectedProfileTitle 重命名后刷新。
- 已新增全局异常处理器：`DispatcherUnhandledException` + `AppDomain.UnhandledException`，启动失败时显示错误对话框。
- 已新增 MSI 桌面快捷方式。
- 已修复代码复审发现的 5 项问题：StopSelected/StopAllCommand 异步异常处理、Task.Run 续接 UI 线程、删除确认弹窗多语言自适应高度、窗口偏移改用运行环境计数、Process.Exited handler 原生句柄泄漏。
- 已修复 V1.2.0 用户测试反馈 9 项：环境列表项撑满左侧栏、行内启动按钮可靠触发、行内关闭按钮正常关闭单环境、语言下拉恢复显示、更新检查 403 时 fallback、托盘首次右键可直接呼出、手动删除 profile 目录后不重建、卸载保留数据后重装可重新识别 profile、新增环境使用下一个可用编号。
- 已修复追加代码复审发现的 3 项稳定性问题：CDP 注入 target 集合并发访问、Chrome 启动登记和极快退出竞态、托盘退出流程异步异常未局部处理。
- 已完成多环境隔离与生命周期专项加固：配置 profile 去重避免同目录串台，StopAll 使用 UI 线程快照并禁止批量关闭期间启动新环境，关闭浏览器优先走 CDP `Browser.close` 后 fallback 到窗口关闭和整进程树 kill，注入器释放幂等化，应用 OnExit 增加最后兜底关闭已知环境。
- 已新增 MSI 升级安装运行中检测：安装 / 升级时检测 `ChromeIsolator.exe`，提示用户先从系统托盘退出后重试；构建脚本自动确保 WiX Util 扩展；MSI codepage 明确设为 UTF-8。
- 已完成全界面风格一致性检查与细节页优化：浏览器引擎窗口现代化，设置页按钮自动换行，重命名 / 删除确认弹窗统一卡片、图标、输入框和按钮样式，补充 TextBox / ComboBox / CheckBox / 进度条基础样式。
- 已修复 V1.4.0 用户测试反馈 3 项：环境卡片横向铺满左侧列表区域；删除低编号环境后新增环境复用最小空编号；隐藏到托盘后再次双击桌面图标不再启动第二实例，而是唤醒已有主窗口。
- 已优化设置页“帮助与更新”：展示作者 Lucas、联系邮箱、当前版本 / 更新状态；“查看发布页”改为“打开下载页”，检查到新版本时给出当前版本、最新版本和下载运行安装包的指引。
- 已优化设置页“语言 / Language”：非英语语言标题追加英文 `Language` 作为稳定识别锚点，并为 `ComboBox` / `ComboBoxItem` 补齐统一模板，使下拉框外观与按钮、输入框、卡片风格一致。
- 已将 MSI 安装 / 卸载反馈收敛为自定义最小 UI，只保留欢迎页、进度页和完成页，并继续固定安装到 `%ProgramFiles%\ChromeIsolator`。
- 已完成 MSI 安装器交互收口：只保留欢迎页、进度页和完成页，不引入许可页或目录选择页。
- 已精简 MSI 安装器中文文案，保留安装目录、环境数据保留和运行中重试等关键信息。
- 已统一多语言里“浏览器引擎设置”按钮的动作语义，避免名词化标签。
- 已修复托盘图标左键单击无反馈的问题：左键单击直接唤醒主窗口，右键仍打开托盘菜单，双击保留兼容。
- 已修复托盘更新检查和退出确认弹窗中 `\n\n` 被显示为字面文本的问题。
- 已修复设置页语言下拉框显示 `LanguageOption { ... }` 对象文本的问题。
- 已将默认浏览器启动模式改为兼容模式：默认不启用调试端口、不注入页面脚本；设置页新增“环境差异模式”，可对已关闭的指定环境单独启用轻量 navigator 参数注入。

## 待处理

### 高优先级

- 构建 V1.2.0 MSI 并验证全部修复效果。
- 验证重点：全部关闭异步化是否解决 UI 阻塞和配额耗尽、双击带到前台、语言下拉框、删除确认流程、进程句柄泄漏。

### 中优先级

- 若 Google 后续提供明确可再分发、可私有部署、可自动更新的 Stable Chrome 包，再研究私有程序副本。

### 低优先级

- 编写多语言用户文档。
- 添加自动化测试和发布脚本。
- 规划代码签名。
  - 当前策略：不阻塞 MVP，正式期再补。

## 未解决的问题 / 临时决策

| 问题 | 影响 | 状态 | 备注 |
|------|------|------|------|
| **环境卡片宽度未铺满 / 右侧截断** | **主窗口左侧列表视觉效果差** | **已解决** | **2026-05-23 已确认根因是 `DockPanel` 中 `ListBox` 不是最后填充子元素，已改为 `Grid` 布局并移除 `MinWidth` 硬撑；用户实机验证修复成功** |
| 官方 Stable Chrome 私有程序副本 | 不作为 MVP / 默认方案 | 已收敛 | 当前正式策略是共享官方 Chrome 程序文件 + 隔离 profile；只有 Google 提供清晰私有部署包时才重评 |
| GitHub 仓库 owner / Releases 地址 | 影响更新检查 URL | 已确认 | 代码中使用 `MoeMoeGit/ChromeIsolator` |
| .NET 和 WiX 具体版本尚未锁定 | 影响工程和 CI | 待确认 | 创建工程时选择 LTS 和稳定版本 |
| 早期安装包未签名 | 安装时可能出现 SmartScreen / 未知发布者提示 | 已接受 | 用户确认提示可接受，README 后续补说明 |

## 下一步

1. 发布 V1.2.0 Release，构建 MSI。
2. 验证 MSI 安装体验：桌面快捷方式出现、应用正常启动。
3. 验证主界面 UI 重设计效果。
4. 在干净系统上分别验证 Chrome 已安装、Chrome 未安装但 Edge 可用、用户确认安装 Chrome 三条路径。

## 任务交接

**当前任务**：Windows 版 ChromeIsolator 应用开发。

**已完成**：V1.0.0 全部功能 + V1.1.0 主界面 UI 全面重设计 + V1.1.0 用户测试反馈修复（12 项问题）+ V1.2.0 代码复审修复（5 项问题）+ V1.6.8 语言下拉、托盘和更新提示修复。

**未完成**：尚未在干净 Windows 设备上验证 Chrome Stable 安装包行为。

**下一步建议**：在干净 Windows 设备上验证欢迎页、进度页、完成页、桌面快捷方式和升级安装运行中检测。

**风险 / 阻塞**：Chrome 官方 Stable Windows 安装器是系统级安装，必须保持用户确认流程；当前代码不会在首次打开时自动安装。

**相关文件**：

- `00-project-overview.md`
- `01-function-design.md`
- `04-project-architecture.md`
- `09-external-api-reference.md`
- `10-planning-log.md`
- `12-design-decisions.md`
- `.gitignore`
- `README.md`
- `LICENSE`
- `NOTICE`
- `assets/icons/AppIcon.svg`
- `assets/icons/AppIcon.png`
- `assets/icons/AppIcon.ico`
- `ChromeIsolator.sln`
- `src/ChromeIsolator.App/`
- `scripts/publish-win-x64.ps1`
- `scripts/build-msi.ps1`
- `installer/Product.wxs`
- `.github/workflows/build.yml`
