# 设计决策记录

> 记录项目中已经形成的重要设计决策，包括背景、考虑过的方案、最终选择、依据、已知不足和后续触发条件。
> 用于后续改版、多人协作和 AI 接力时理解「为什么现在是这样」，避免重复争论或重复踩坑。

---

## 本文件记录什么

适合记录：

- 产品入口、核心流程、导航方式、信息架构等产品设计决策。
- UI 交互、页面布局、状态展示、用户操作路径等体验决策。
- 架构边界、模块职责、数据流、配置方式等长期影响代码形态的决策。
- 数据库/API/部署方案中已经落地，但未来可能被质疑或改动的取舍。
- 明确「暂时不做」的设计边界，以及未来什么条件下重新考虑。

不适合记录：

- 普通 bug 修复，除非它暴露了设计层面的取舍。
- 一次性实现细节，除非未来维护者很可能问「为什么这么写」。
- 简单任务进度，放在 `05-current-status.md` 或 `06-dev-log.md`。
- 改动前尚未确认的方案规划，先放在 `10-planning-log.md`。

---

## 与 `10-planning-log.md` 的分工

| 文件 | 使用时机 | 关注点 |
|------|----------|--------|
| `10-planning-log.md` | 改动前 | 方案规划、备选方案、确认依据、改动范围 |
| `12-design-decisions.md` | 决策形成后 | 为什么最终这么设计、已知不足、后续何时重评 |

一个较大改动可以先在 `10-planning-log.md` 写 ADR，确认并实现后，再把最终形成的长期设计沉淀到本文件。

---

## 决策记录

## 决策 1：Windows 版坚持“浏览器多开”产品边界

**日期**：2026-05-21

**状态**：已采用

**替代关系**：无

**类型**：产品 / 架构

**背景**：

ChromeIsolator 是 BrowserIsolator 的 Windows 版复刻。用户明确要求从用户使用角度一模一样，不迁移 macOS 数据，不扩展成新产品。

**考虑过的方案**：

| 方案 | 做法 | 优点 | 缺点 |
|------|------|------|------|
| A. 扩展为反检测浏览器 | 增加代理、完整指纹、账号管理等能力 | 市面概念更完整 | 范围巨大，风险高，违背现有产品定位 |
| **B. 最终方案** | 只复刻 BrowserIsolator 已有能力 | 范围清晰，用户体验一致 | 不提供高级反检测承诺 |
| C. 简化为 profile 启动器 | 只启动多个 Chrome profile | 开发最快 | 缺少托盘、设置、下载、轻量差异等现有功能 |

**最终选择**：

选择 B。ChromeIsolator 保持“本地浏览器环境隔离工具”的定位。

**依据**：

- 用户已在 BrowserIsolator 中验证该功能组合好用。
- 复刻目标比扩展目标更明确。
- 不承诺规避 Douyin 或其他网站风控，可以降低误导和维护风险。

**实现原则**：

- 每个环境使用独立 `--user-data-dir`。
- 只做 `hardwareConcurrency` 和 `deviceMemory` 轻量差异。
- 不加入代理、账号托管、自动化运营、完整反检测能力。
- 文档中明确“不承诺绕过网站风控”。

**已知不足**：

- 面对复杂网站风控时，轻量差异可能不足。
- 用户如果期待完整反检测浏览器，需要另行说明项目边界。

**后续触发条件**：

- 用户明确提出并确认要将产品升级为反检测浏览器。
- 现有轻量差异导致严重兼容性问题，需要重新评估是否关闭或调整。

**相关文件**：

- `00-project-overview.md`
- `01-function-design.md`
- `10-planning-log.md` 中的 ADR-004
- `06-dev-log.md` 中的 2026-05-21（初始化 Windows 版规划）

**后续补充**：

- 暂无。

---

## 决策 2：浏览器引擎只接受官方 Stable Chrome，禁止 Testing；Edge 仅作备用

**日期**：2026-05-21

**状态**：已采用

**替代关系**：无

**类型**：架构 / 部署 / 产品

**背景**：

Windows 版需要稳定浏览器运行时。用户明确要求不要使用 Chrome for Testing，因为多账号登录 Douyin 等场景可能更容易触发风控；同时用户要求绝不能影响当前正在用的 Chrome，包括插件、设置、登录用户和启动体验。

**考虑过的方案**：

| 方案 | 做法 | 优点 | 缺点 |
|------|------|------|------|
| A. Chrome for Testing ZIP | 下载 ZIP 解压到应用目录 | 技术上最容易私有化 | 用户明确不接受 Testing |
| B. 官方 Chrome 自动静默安装 | 自动下载并安装 Stable Chrome | 官方 Stable，来源可靠 | 用户不理解时可能误以为应用改动了系统 Chrome |
| **C. 最终方案** | 复用官方 Stable Chrome 程序文件；缺失时用户确认安装 | 来源稳定，最不影响用户原有 Chrome | 程序版本跟随系统 Chrome |
| D. 私有提取 Chrome | 尝试从 MSI / Bundle 提取到应用目录 | 程序文件也隔离 | 授权、更新、稳定性不清晰 |
| E. 第三方 Chromium | 使用 Brave、ungoogled、Thorium 等 | 部分更易携带 | 来源、媒体能力、风控表现和维护不可控 |
| F. Microsoft Edge Stable | Windows 自带 Chromium 浏览器 | 稳定主流，视频能力完整 | 不是 Chrome，只能作备用 |

**最终选择**：

选择 C，并把 F 作为最后兜底备用。ChromeIsolator 优先使用官方 Stable Chrome 程序文件；如果未检测到 Chrome，首次运行窗口说明隔离策略，用户确认后才下载并运行官方 Chrome 安装包。自动下载失败时，引导用户打开 Google 官方 Chrome 下载页手动安装后重新检测。Microsoft Edge Stable 只在 Chrome 缺失、Chrome 官方下载 / 手动安装路径暂时不可用、且用户明确选择时作为最后备用 Chromium 引擎。

**依据**：

- 用户明确禁止 Testing。
- 共享浏览器程序文件不会导致登录态、插件、设置串台；串台风险来自共享 user data dir。
- 独立 `--user-data-dir` 能隔离 Cookie、登录态、密码、扩展配置。
- 标准 Chrome 安装器属于系统 / 用户浏览器安装动作，必须由用户确认。
- Edge Stable 稳定主流、视频能力完整，可解决无 Chrome 设备的临时可用性，但不是产品推荐主路径。

**实现原则**：

- 禁止使用 Chrome for Testing、Beta、Dev、Canary 作为默认引擎。
- 禁止第三方便携 Chromium 作为默认引擎。
- 禁止读取用户默认 Chrome profile。
- 禁止自动静默安装 Chrome。
- 禁止把 Windows 官方 Chrome MSI 私有提取作为默认方案。
- 设置页显示当前浏览器引擎来源、路径和版本。
- Chrome 一旦可用，优先级高于 Edge 备用。
- 首次窗口不能把 Edge 和 Chrome 安装并列展示；Edge 必须放在最后备用区域，并明确“不推荐优先使用”。
- 自动下载 Chrome 失败后，只能提供 Google 官方下载页，不内置第三方国内下载站或镜像。

**已知不足**：

- 如果 fallback 到系统 Chrome 程序文件，应用对 Chrome 更新节奏的控制会变弱。
- 如果用户只选择 Edge 备用，产品名与实际引擎存在轻微认知差异，所以 UI 必须明确标注“备用 Edge / 不推荐优先使用”。

**后续触发条件**：

- Google 提供明确可再分发、可私有部署、可自动更新的 Stable Chrome 包。
- 用户反馈系统 Chrome 程序文件共享带来不可接受的问题。
- Edge 备用在核心网站视频播放或登录场景出现明显兼容问题。

**相关文件**：

- `04-project-architecture.md`
- `09-external-api-reference.md`
- `10-planning-log.md` 中的 ADR-003
- `06-dev-log.md` 中的 2026-05-21（初始化 Windows 版规划）

**后续补充**：

- 2026-05-22：实现层增加 Microsoft Edge Stable 备用引擎。Edge 只在 Chrome 缺失且用户明确选择后启用，仍使用 ChromeIsolator 独立 profile 目录；Chrome 一旦可用仍优先使用 Chrome。
- 2026-05-22：交互层明确 Edge 是最后备用，不推荐优先使用；Chrome 自动下载失败时优先提供 Google 官方下载页和重新检测，不切换到第三方下载站。

---

## 决策 3：Windows 等价菜单栏体验采用系统托盘

**日期**：2026-05-21

**状态**：已采用

**替代关系**：无

**类型**：交互 / 产品

**背景**：

BrowserIsolator 在 macOS 上关闭最后窗口后不会退出，仍可通过菜单栏图标启动和关闭环境。Windows 需要等价交互。

**考虑过的方案**：

| 方案 | 做法 | 优点 | 缺点 |
|------|------|------|------|
| A. 关闭窗口即退出 | 点 X 直接退出应用 | 简单 | 容易误关运行环境，不复刻原体验 |
| B. 最小化到任务栏 | 点 X 或最小化后留在任务栏 | 实现简单 | 快速操作入口弱 |
| **C. 最终方案** | 关闭窗口隐藏到系统托盘 | 最接近 macOS 菜单栏体验 | 需要处理托盘生命周期和退出确认 |

**最终选择**：

选择 C。窗口关闭隐藏到系统托盘；真正退出必须从托盘或应用菜单触发，并在有运行环境时确认。

**依据**：

- 系统托盘是 Windows 上最接近 macOS 菜单栏驻留的机制。
- 多开工具需要低打扰常驻，方便快速开关环境。

**实现原则**：

- 点窗口关闭只隐藏，不退出。
- 托盘菜单实时反映环境运行状态。
- 退出时如有运行环境，提示并关闭全部。

**已知不足**：

- 部分用户可能不熟悉托盘隐藏，需要在 README 或首次使用文案中说明。

**后续触发条件**：

- 用户大量反馈窗口关闭行为不符合预期。
- Windows 通知区域策略导致托盘图标不可见，需要增加替代入口。

**相关文件**：

- `01-function-design.md`
- `10-planning-log.md` 中的 ADR-006

**后续补充**：

- 暂无。

---

## 决策 4：早期 Windows 安装包允许未签名发布

**日期**：2026-05-21

**状态**：已采用

**替代关系**：无

**类型**：部署 / 产品

**背景**：

Windows 未签名安装包可能出现 SmartScreen、浏览器下载警告或“未知发布者”提示。代码签名可以改善信任体验，但需要购买 OV / EV 代码签名证书，并处理硬件 token 或云签名服务。

**考虑过的方案**：

| 方案 | 做法 | 优点 | 缺点 |
|------|------|------|------|
| A. MVP 前必须签名 | 先购买证书，再发布安装包 | 用户安装信任度更高 | 证书采购和签名流程会阻塞开发 |
| **B. 最终方案** | 早期允许未签名，README 提示 SmartScreen | 不阻塞功能开发和发布闭环 | 用户安装时可能多一步确认 |
| C. 永久不签名 | 不做签名规划 | 最简单 | 正式期用户信任和下载体验较差 |

**最终选择**：

选择 B。早期 GitHub Releases 可以发布未签名安装包；后续需要更正式分发时再补代码签名。

**依据**：

- 用户确认“有提示没关系，能用就行”。
- 未签名不影响功能，只影响安装信任提示。
- 当前优先级是完成 Windows 版功能复刻。

**实现原则**：

- README 安装说明需要明确未签名提示和处理方式。
- CI / 发布脚本可预留签名步骤，但默认关闭。
- 证书和签名凭据不得写入仓库。
- 如果后续签名，主程序和安装包都需要签名并带时间戳。

**已知不足**：

- GitHub Releases 的早期安装包可能被 SmartScreen 或浏览器提示风险。
- 未签名安装包的用户信任感较弱。

**后续触发条件**：

- 准备正式公开推广。
- 用户反馈安装提示影响使用。
- 决定购买 OV / EV Code Signing Certificate 或接入云签名服务。

**相关文件**：

- `07-deployment.md`
- `10-planning-log.md` 中的 ADR-007

**后续补充**：

- 暂无。

---

## 决策 5：Chrome 程序可共享，用户默认 Chrome profile 绝不触碰

**日期**：2026-05-22

**状态**：已采用

**替代关系**：补充「决策 2」

**类型**：产品 / 架构

**背景**：

用户明确要求：大多数用户已经安装并正常使用 Chrome，ChromeIsolator 不能影响用户原本 Chrome 的插件、设置、登录用户、启动体验或用户选择提示。对没有安装 Chrome 的用户，可以帮他安装 Chrome，但之后用户自己从桌面打开 Chrome 使用时，也不能和隔离环境冲突。

**考虑过的方案**：

| 方案 | 做法 | 优点 | 缺点 |
|------|------|------|------|
| A. 使用默认 Chrome profile | 直接打开现有 Chrome 用户数据 | 简单 | 会串插件、设置、登录态，绝不接受 |
| B. 私有 Chrome 程序副本 | 应用内部复制 / 提取 Chrome | 程序文件也隔离 | Windows 官方 MSI 不适合私有提取，授权、更新和稳定性不清晰 |
| **C. 最终方案** | 共享官方 Chrome 程序文件，隔离 profile 数据 | 不影响用户默认 Chrome，最稳定透明 | 程序文件版本跟随系统 Chrome |
| D. Edge Stable 备用 | Chrome 缺失时用户确认使用 Edge 程序文件 | 无 Chrome 设备可直接可用，视频能力完整 | 不是 Chrome，只能作为备用 |

**最终选择**：

选择 C，D 作为最后备用。ChromeIsolator 可以使用用户已安装的官方 Stable Chrome 程序文件；没有 Chrome 时，由用户确认后安装官方 Chrome，或打开 Google 官方 Chrome 下载页手动安装后重新检测。若 Chrome 缺失、Chrome 安装路径暂时不可用且用户明确选择 Edge 备用，可以临时使用 Microsoft Edge Stable 程序文件。所有隔离环境始终使用 `%LOCALAPPDATA%\ChromeIsolator\Profiles\pN` 作为独立 `--user-data-dir`。

**依据**：

- Chrome 程序文件和 profile 数据是两回事。
- 共享 `chrome.exe` 不会天然串台；串台风险来自使用同一个 user data dir。
- 独立 `--user-data-dir` 可以隔离 Cookie、登录态、扩展、密码和设置。

**实现原则**：

- 不读写用户默认 Chrome profile。
- 不修改用户默认 Chrome 的插件、设置、登录状态。
- 不自动静默安装 Chrome。
- 安装 Chrome 必须用户确认。
- README 和 UI 必须诚实说明 Chrome 来源和数据隔离方式。
- Edge 备用必须由用户明确选择，且 UI 标注为最后备用、不推荐优先使用。

**已知不足**：

- 如果系统 Chrome 被用户卸载或损坏，ChromeIsolator 也会受影响，需要重新安装 / 修复 Chrome。

**后续触发条件**：

- Google 提供明确可再分发、可私有部署、可自动更新的 Stable Chrome 包。
- 用户反馈系统 Chrome 共享程序文件导致不可接受的问题。

**相关文件**：

- `10-planning-log.md` 中的 ADR-008
- `ChromeManager.cs`
- `README.md`

**后续补充**：

- 暂无。

---

## 决策 6：环境卡片横向铺满左侧列表（未解决）

**日期**：2026-05-22

**状态**：❌ 未解决，当前采用临时方案

**类型**：UI / 布局

**背景**：

主窗口左侧是环境卡片列表（`ListBox`），右侧是环境详情。期望卡片横向铺满左侧列表区域宽度，但实际卡片只占左侧一部分，右侧留有空白。该问题从 V1.2.0 开始反复出现，经历 V1.4.0 ~ V1.6.1 共 5 个版本尝试，始终未彻底解决。

**布局结构**：

```
Grid 列0: Width="360"（左侧面板）
  └─ Border: BorderThickness="1", CornerRadius="8"（卡片容器）
       └─ DockPanel
            ├─ TextBlock: DockPanel.Dock="Top"（标题）
            └─ ListBox: Margin="4", BorderThickness="0", Padding="0"
                 └─ ListBoxItem（ModernListBoxItem 样式）
                      └─ Border: CornerRadius="8", Padding="10,8"（卡片模板）
```

**关键像素值**：

- Border 内容区 = 360 - 2 = 358px
- ListBox 可用宽度 = 358 - 8 = 350px（Margin="4" × 2）
- ScrollViewer 内容区 = 350px（ListBox.Padding="0"）或 348px（默认 Padding="1"）

**考虑过的方案**：

| 版本 | 方案 | 做法 | 效果 |
|------|------|------|------|
| V1.4.0 | A. MinWidth = ActualWidth | `MinWidth` 绑定 `ListBox.ActualWidth`，不减任何值 | 卡片铺满，但右侧溢出 2px，选中圆角被截断 |
| V1.5.0 | B. Width = ActualWidth - 16 | 引入 `WidthMinusConverter`，用 `Width`（强制定宽）减 16px | 卡片完全不显示 |
| V1.5.1 | C. MinWidth = ActualWidth - 16 | 改回 `MinWidth`，保留 converter 减 16px | 卡片太窄，右侧约 8px 空白 |
| V1.6.0 | D. MinWidth = ActualWidth - 8 + Padding="0" | 减 8px + ListBox 新增 `Padding="0"` | 卡片未铺满（同 V1.5.1） |
| V1.6.0 | E. Width = ActualWidth - 8 + Padding="0" | 改用 `Width` + Padding="0" | 卡片可能不显示（同 V1.5.0） |
| V1.6.1 | F. MinWidth = ActualWidth + Padding="0" | 回到 V1.4.0 思路 + Padding="0" | 卡片铺满，但右侧仍然截断（同 V1.4.0） |

**当前临时方案**：

采用方案 F（V1.6.1）：`MinWidth = ListBox.ActualWidth` + `ListBox.Padding="0"`。卡片基本铺满，但选中状态的右侧圆角边框仍被轻微截断。

**核心未解问题**：

1. **`VirtualizingStackPanel` 的横向布局行为**：WPF `ListBox` 内部的 `VirtualizingStackPanel`（垂直方向）在 `ScrollViewer` 内部时，给子元素的可用宽度是容器宽度还是无限大？`HorizontalAlignment="Stretch"` 是否生效？观察到的行为是：`Stretch` 未生效，卡片按内容自适应宽度，只有 `MinWidth` 能强制撑大。

2. **`ListBox.Padding="0"` 是否传递给内部 ScrollViewer**：WPF 默认 `ListBox` ControlTemplate 中，`Border` 的 `Padding` 是否通过 `TemplateBinding` 绑定到 `ListBox.Padding`？如果未绑定，`Padding="0"` 不会影响 ScrollViewer 内容区宽度。

3. **右侧截断的真实原因**：是 ScrollViewer 的默认 `Padding="1"` 导致内容区比 ListBox 内容区小 2px？还是父 `Border` 的 `CornerRadius="8"` 在右侧边缘裁切了卡片的边框？还是选中状态的 `BorderThickness="1"` 是外扩导致超出？

**后续建议**：

1. 查看 .NET 8 WPF 默认 `ListBox` ControlTemplate 源码，确认 `ScrollViewer` 的 `Padding` 绑定关系和 `VirtualizingStackPanel` 的布局行为。
2. 运行时诊断：在代码中读取 `ListBox`、`ScrollViewer`、`ListBoxItem` 的 `ActualWidth`、`Padding` 等属性，输出调试日志获取真实像素值。
3. 替代方案：自定义 `ListBox` 的 `ItemsPanelTemplate`，用 `Grid` 替代 `VirtualizingStackPanel` 强制子元素拉伸；或在 `ListBoxItem.Loaded` 事件中通过代码设置宽度。

**相关文件**：

- `MainWindow.xaml` 第 68-88 行（左侧面板布局）
- `Themes/Controls.xaml` 第 246-289 行（ModernListBoxItem 样式）
- `06-dev-log.md` 2026-05-22 记录（完整排查过程）

---

## 变更记录

| 日期 | 变更内容 |
|------|----------|
| 2026-05-22 | 新增决策 6：环境卡片横向铺满问题（未解决），记录 5 个版本尝试和后续建议 |
| 2026-05-22 | 收敛浏览器引擎最终方案，补充 Chrome / Edge 选择过程和放弃项 |
| 2026-05-22 | 增加 Chrome 程序共享与 profile 绝对隔离策略 |
| 2026-05-21 | 初始化设计决策记录，并沉淀产品边界、Chrome 来源、托盘行为三项长期决策 |
| 2026-05-21 | 增加早期未签名发布策略 |
