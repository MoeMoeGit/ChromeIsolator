# 开发日志

<!-- 填写说明：每轮开发追加一条记录。不要删除旧记录。详见 README 中的格式说明。 -->

---

## 2026-05-25（V1.7.0 发布触发）

**状态：进行中，待 GitHub 推送与 tag 构建**

**触发原因**：用户要求将版本统一推进到 1.7.0，提交 GitHub，打新 tag，并触发 1.7.0 版本构建。

**修改内容**：

1. `Directory.Build.props` — 版本号从 `1.6.11` 推进到 `1.7.0`，同步 Assembly / File / Informational 版本。
2. `README.md` — 当前版本、发布产物文件名和版本 tag 示例统一改为 `1.7.0`。
3. `project-log/05-current-status.md` — 当前版本更新为 `V1.7.0 待发布`，同步当前阶段说明。

**遇到的问题**：

- 项目里保留了历史版本日志，不能把过去的 1.6.x 记录一并改成 1.7.0。

**解决方式**：

- 只更新当前版本展示、发布元数据和当前状态，历史日志保留原始版本轨迹。

**验证方式**：

- 版本字符串全局检索。
- 后续执行 `dotnet build ChromeIsolator.sln`
- 后续执行 Git 提交、tag 和 push

**验证结果**：

- 待执行。

**本地产物清理**：

- 无。

---

## 2026-05-25（详情面板与差异模式管理优化）

**状态：✅ 已完成**

**触发原因**：用户要求优化主界面右侧详情备注展示顺序、备注编辑文案、调试端口模式展示，并解决设置页差异模式直接列出大量环境会撑爆页面的问题。

**修改内容**：

1. `MainWindow.xaml` / `ProfileViewModel.cs` / `MainViewModel.cs` — 备注在右侧基础信息中改为有内容时显示第一项；空备注完全不占位；调试端口字段在基础模式显示“基础模式”，差异模式运行时显示“差异模式 端口号”。
2. `SettingsWindow.xaml` / `SettingsWindow.xaml.cs` / `EnvironmentModeWindow.xaml(.cs)` / `SettingsViewModel.cs` — 设置页差异模式改为启用数量概览和“管理环境”入口；二级窗口支持搜索、仅显示可切换环境，并保持运行中环境不可切换；环境退出事件刷新回到 UI 线程，避免列表视图跨线程刷新。
3. `Resources/Strings*.xaml` — 更新备注编辑提示语，并补齐基础 / 差异模式端口文案、差异模式管理窗口文案的 7 语言资源。
4. `README.md`、`project-log/01-function-design.md`、`10-planning-log.md`、`12-design-decisions.md`、`05-current-status.md` — 同步记录功能描述、ADR、长期设计决策和当前状态。

**遇到的问题**：

- 设置页原本直接渲染全部环境勾选项，环境数量大时会挤压其他设置项。
- 详情面板备注如果只是调换 Grid 行号，空备注隐藏后仍可能留下视觉间距，需要让备注独立成可折叠行。
- 进程退出事件不保证在 UI 线程触发，差异模式列表视图刷新需要调度回 WPF Dispatcher。

**解决方式**：

- 将差异模式环境选择拆到独立二级窗口，设置页只保留概览和入口。
- 将备注行拆成独立可见性控制的 Grid，备注为空时整块折叠；最近使用仍作为无备注时的第一行。
- 设置页收到环境退出事件时检查 Dispatcher 访问权，必要时用 `BeginInvoke` 回到 UI 线程再刷新模式状态。

**验证方式**：

- 多语言资源 key 对齐检查。
- `dotnet build ChromeIsolator.sln`
- `git diff --check`
- `dotnet clean ChromeIsolator.sln`
- 删除本轮生成的 `src/ChromeIsolator.App/bin/` 和 `src/ChromeIsolator.App/obj/`

**验证结果**：

- 通过。7 个资源文件 key 完全一致。
- 通过。编译 0 警告、0 错误。
- 通过。无空白错误。
- 通过。已清理本轮 Debug 构建输出。

**本地产物清理**：

- 已通过 `dotnet clean ChromeIsolator.sln` 清理本轮 Debug 构建输出，并删除剩余的 `src/ChromeIsolator.App/bin/`、`src/ChromeIsolator.App/obj/` 可再生成目录。

---

## 2026-05-25（评审复核与持久化修复）

**状态：✅ 已完成**

**触发原因**：用户要求对前一轮提出的每一条问题和优化建议再次复核，确认后全部落地。

**修改内容**：

1. `Models/Profile.cs` / `ViewModels/ProfileViewModel.cs` — 新增并同步 `LastUsed` 持久化字段，让最近使用时间可写回配置并在重启后保持排序。
2. `ViewModels/MainViewModel.cs` — 单个环境停止失败时恢复运行状态与调试端口，不再让 `IsStopping` 卡住；启动、关闭、运行中追加 URL、全部关闭后都会写回最近使用时间。
3. `Services/AppPaths.cs` / `Services/ConfigStore.cs` — 配置写入改为临时文件 + 原子替换，保留 `.bak` 备份，并在读取主配置失败时回退到备份。
4. `ViewModels/SettingsViewModel.cs` / `SettingsWindow.xaml.cs` — 设置页订阅环境退出事件刷新差异模式可用状态，窗口关闭时取消订阅，避免状态过期和事件悬挂。

**遇到的问题**：

- 之前的 `LastUsed` 只存在于 ViewModel 内存层，重启后会丢失。
- 单个停止失败时如果不在异常路径恢复状态，界面会卡在“关闭中”。
- 直接覆盖写 `config.json` 在断电或异常退出时存在配置损坏风险。

**解决方式**：

- 将最近使用时间下沉到 `Profile` 模型并同步到配置文件。
- 在停止失败路径里主动恢复运行状态与端口，结束时统一复位停止状态。
- 改为先写临时文件，再原子替换主配置，并保留备份文件兜底。

**验证方式**：

- `dotnet build ChromeIsolator.sln`
- `dotnet clean ChromeIsolator.sln`

**验证结果**：

- 通过。编译 0 警告、0 错误。
- 通过。已清理本轮 `bin/` 和 `obj/` 可再生成构建产物。

**本地产物清理**：

- 已通过 `dotnet clean ChromeIsolator.sln` 清理本轮 Debug 构建输出。

---

## 2026-05-25（外部链接复审核修复）

**状态：✅ 已完成，待外部链接实机行为验证**

**触发原因**：用户要求先复核默认浏览器注册、外部链接目标写入、外部链接错误引导和进程句柄风险，再确认问题后先更新文档、再修改代码。

**修改内容**：

1. `project-log/11-code-review-log.md` — 新增第七轮代码复审，记录 2 项已确认问题、2 项已排除产品取舍和优化建议复核结论。
2. `Services/ChromeManager.cs` — `OpenUrl()` 保存并立即释放 `Process.Start()` 返回的短生命周期 `Process` 对象，避免运行中环境频繁追加外部链接时累积原生句柄。
3. `ViewModels/MainViewModel.cs` — 外部链接遇到无环境、浏览器引擎未就绪或启动失败时主动显示主窗口；浏览器未就绪时接入现有浏览器引擎设置流程，用户完成设置后自动重试打开原链接。

**遇到的问题**：

- 默认浏览器注册后不检测、不维持，以及设置页打开时把未配置外部链接目标写成第一个环境，复核后确认都是产品取舍，不作为问题处理。

**解决方式**：

- 保持默认浏览器注册只给 Windows 信号，不抢默认浏览器。
- 保持外部链接默认目标的自动写入逻辑。
- 仅修复确认存在的资源释放问题，并补齐外部链接失败路径的恢复引导。

**验证方式**：

- `dotnet build ChromeIsolator.sln`

**验证结果**：

- 通过。编译 0 警告、0 错误。
- 未验证 Windows 默认应用页实际选择效果和第三方 App 外部链接转发，需要后续实机测试。

**本地产物清理**：

- 已通过 `dotnet clean ChromeIsolator.sln` 清理本轮 `dotnet build` 生成的 Debug 构建输出，并删除 `src/ChromeIsolator.App/bin/`、`src/ChromeIsolator.App/obj/` 可再生成目录。

---

## 2026-05-25（V1.6.10 发布触发）

**状态：✅ 已完成**

**触发原因**：用户要求提交 GitHub、推进一个版本号、打新 tag 并触发构建。

**修改内容**：

1. `Directory.Build.props` — 版本号从 `1.6.9` 推进到 `1.6.10`。
2. `project-log/05-current-status.md` — 同步当前版本与任务状态，标记为 V1.6.10 待发布。

**验证方式**：

- `git diff --check`
- `xmllint --noout src/ChromeIsolator.App/MainWindow.xaml src/ChromeIsolator.App/SettingsWindow.xaml src/ChromeIsolator.App/Resources/Strings*.xaml`
- 多语言资源 key 对齐检查

**验证结果**：

- 通过。XAML / 资源 XML 结构有效。
- 通过。7 个资源文件 key 完全一致。
- 通过。无空白错误。
- 未执行 Windows 编译。当前 macOS 环境没有 `dotnet` / `msbuild`，Release 编译由 GitHub Actions Windows runner 执行。

## 2026-05-25（环境备注与外部链接）

**状态：进行中，待 Windows 实机验证**

**触发原因**：用户提出两个轻量多环境管理需求：环境备注用于辅助识别环境；外部链接用于让 ChromeIsolator 作为系统默认浏览器接收其他 App 打开的 http / https 链接，并固定转发到一个默认环境。

**修改内容**：

1. `Models/Profile.cs` / `ProfileManager.cs` / `ProfileViewModel.cs` — 新增环境备注字段、保存逻辑和详情展示状态。
2. `MainWindow.xaml` / `SimpleInputDialog.cs` — 右侧详情动作和左侧右键菜单新增“编辑备注”，复用统一小弹窗，备注最多 120 个字符，空备注不显示。
3. `Models/AppConfig.cs` / `SettingsWindow.xaml` / `SettingsViewModel.cs` — 新增外部链接默认目标环境配置和“设为默认浏览器”入口。
4. `App.xaml.cs` / `MainViewModel.cs` / `ChromeManager.cs` / `ShellService.cs` — 新增启动参数 URL 解析、单实例管道转发、外部链接目标选择、启动中队列、运行中追加 URL、浏览器未就绪复制链接提示和当前用户级默认浏览器注册请求。
5. `Resources/Strings*.xaml`、`README.md`、`project-log/*.md` — 同步环境备注、外部链接、基础模式 / 差异模式文案。

**验证方式**：

- 静态检查 XAML / 资源 XML 结构。
- 多语言资源 key 对齐检查。
- `git diff --check`。
- 代码路径自查：无环境、无浏览器、目标未运行、目标启动中、目标已运行、冷启动外部链接、第二实例外部链接。

**验证结果**：

- 通过。`MainWindow.xaml`、`SettingsWindow.xaml` 和 7 个资源文件 XML 结构有效。
- 通过。7 个资源文件 key 完全一致。
- 通过。`git diff --check` 无空白错误。
- 通过。未发现残留“兼容模式 / 环境差异模式”等旧入口命名；中文界面使用“基础模式 / 差异模式”。
- 未执行 Windows 编译。当前 macOS 环境没有 `dotnet` / `msbuild`，WPF 编译和系统默认浏览器注册需 Windows 设备验证。

## 2026-05-25（V1.6.9 发布触发）

**状态：✅ 已完成**

**触发原因**：默认基础模式与按环境启用差异功能已完成代码和文档自查，用户要求直接推进新版本、发布、打 tag 并触发 GitHub Actions 构建。

**修改内容**：

1. `Directory.Build.props` — 版本号从 `1.6.8` 推进到 `1.6.9`。
2. `project-log/05-current-status.md` — 同步当前版本、当前阶段和最近开发日志。

**验证方式**：

- `xmllint --noout` 校验设置页和 7 个资源文件。
- 多语言资源 key 对齐检查。
- `git diff --check`

**验证结果**：

- 通过。XAML / 资源 XML 结构有效。
- 通过。7 个资源文件 key 完全一致。
- 通过。无空白错误。
- 未执行。当前 macOS 环境没有 `dotnet` / `msbuild`，Release 编译由 GitHub Actions Windows runner 执行。

---

## 2026-05-25（默认基础模式与按环境启用差异模式）

**状态：进行中，待 Windows 实机验证**

**触发原因**：用户反馈特定网站在系统 Chrome 下正常，但在 ChromeIsolator 环境中访问异常。初步判断风险点不是隔离 profile 本身，而是启动时无条件开启 `--remote-debugging-port` 并通过 CDP 注入 navigator 参数。

**修改内容**：

1. `Models/Profile.cs` — 新增 `EnableEnvironmentVariation`，默认 `false`，老环境升级后默认进入基础模式。
2. `Services/ChromeManager.cs` — 仅在环境启用差异模式时分配调试端口、传入 `--remote-debugging-port` 并启动 `FingerprintInjector`；默认模式只使用独立 `--user-data-dir`。
3. `SettingsWindow.xaml` / `SettingsViewModel.cs` — 设置页新增“差异模式”区域，按环境勾选；运行中的环境不可切换，需关闭后再改。
4. `Resources/Strings*.xaml`、`README.md`、项目文档 — 同步默认基础模式和可选差异模式文案。

**验证方式**：

- `xmllint --noout` 校验设置页和 7 个资源文件。
- `git diff --check`

**验证结果**：

- 通过。XAML / 资源 XML 结构有效。
- 通过。无空白错误。
- 未执行。当前 macOS 环境没有 `dotnet` 命令，WPF 编译和实机行为待 Windows 设备验证。

---

## 2026-05-23（V1.6.8 最终检查与发布触发）

**状态：✅ 已完成**

**触发原因**：用户要求对项目代码再次全面检查，重点复查刚修改的语言下拉框以及之前的托盘、更新提示和安装器修改；确认无误后推进一个小版本号，提交 GitHub，打 tag 并触发构建。

**修改内容**：

1. `Directory.Build.props` — 版本号从 `1.6.7` 推进到 `1.6.8`，用于生成新的正式构建 tag。
2. `project-log/05-current-status.md` — 同步当前版本、当前阶段和任务交接信息。
3. 本轮复查范围包含 `SettingsWindow.xaml`、`TrayService.cs`、`Resources/Strings*.xaml`、安装器构建脚本、MSI UI 文件和 GitHub Actions 发布配置。

**验证方式**：

- `dotnet build ChromeIsolator.sln -c Release`
- `dotnet test ChromeIsolator.sln -c Release --no-build`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1`
- `git diff --check`
- 检查设置页下拉框绑定不再使用 `DisplayMemberPath`，而是显式绑定 `NativeName`
- 多语言资源 key 完整性检查
- 检查 `src/ChromeIsolator.App/Resources` 不再存在 `\n` 字面量
- 检查 GitHub Actions 对 `main` push 和 `v*` tag 的触发配置

**验证结果**：

- 通过。Release 编译 0 警告，0 错误。
- 通过。`dotnet test ChromeIsolator.sln -c Release --no-build` 返回成功；当前解决方案未输出独立测试项目结果。
- 通过。MSI 成功生成于 `artifacts\installer\ChromeIsolator-Setup-x64-v1.6.8.msi`，发布 zip 成功生成于 `artifacts\publish\ChromeIsolator-win-x64-v1.6.8.zip`。
- 通过。`ChromeIsolator.exe` ProductVersion 为 `1.6.8`，FileVersion 为 `1.6.8.0`。
- 通过。`git diff --check` 无错误。
- 通过。设置页语言下拉框不再使用 `DisplayMemberPath`，已显式绑定 `NativeName`。
- 通过。7 个多语言资源文件均包含 116 个相同 key，且资源文件中不再存在 `\n` 字面量。
- 通过。GitHub Actions workflow 已确认 push main 和 `v*` tag 都会触发构建，tag 构建会发布 GitHub Release。

**本地产物清理**：

- 本轮生成 `artifacts/`、`bin/`、`obj/` 构建产物，均为可再生内容并在忽略列表中，不进入提交。

---

## 2026-05-23（语言下拉框显示对象文本修复）

**状态：✅ 已完成**

**触发原因**：用户反馈设置页语言下拉框显示 `LanguageOption { Code ... }`，而不是语言名称。排查确认 `ComboBox` 绑定的是 `LanguageOption` 对象，自定义 ComboBox 样式下 `DisplayMemberPath` 没有正确应用到显示内容。

**修改内容**：

1. `SettingsWindow.xaml` — 将语言下拉框从 `DisplayMemberPath` 改为显式 `ItemTemplate`，直接绑定 `NativeName`。
2. `project-log/05-current-status.md` — 同步本轮修复项。

**验证方式**：

- `dotnet build ChromeIsolator.sln -c Release`

**验证结果**：

- 通过。Release 编译 0 警告，0 错误。
- 通过。项目中只有设置页这一处 `ComboBox ItemsSource`，未发现其他同类下拉框。

**本地产物清理**：

- 本轮仅生成常规 `bin/` / `obj/` 构建产物，均为可再生内容。

---

## 2026-05-23（V1.6.7 最终检查与发布触发）

**状态：✅ 已完成**

**触发原因**：用户要求对项目代码再次全面检查，重点复查刚修改的托盘交互和更新提示换行；确认无误后推进一个小版本号，提交 GitHub，打 tag 并触发构建。

**修改内容**：

1. `Directory.Build.props` — 版本号从 `1.6.6` 推进到 `1.6.7`，用于生成新的正式构建 tag。
2. `project-log/05-current-status.md` — 同步当前版本、当前阶段和任务交接信息。
3. 本轮复查范围包含 `TrayService.cs`、`Resources/Strings*.xaml`、安装器构建脚本、MSI UI 文件和 GitHub Actions 发布配置。

**验证方式**：

- `dotnet build ChromeIsolator.sln -c Release`
- `dotnet test ChromeIsolator.sln -c Release --no-build`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1`
- `git diff --check`
- 多语言资源 key 完整性检查
- 检查 `src/ChromeIsolator.App/Resources` 不再存在 `\n` 字面量
- 检查 GitHub Actions 对 `main` push 和 `v*` tag 的触发配置

**验证结果**：

- 通过。Release 编译 0 警告，0 错误。
- 通过。`dotnet test ChromeIsolator.sln -c Release --no-build` 返回成功；当前解决方案未输出独立测试项目结果。
- 通过。MSI 成功生成于 `artifacts\installer\ChromeIsolator-Setup-x64-v1.6.7.msi`，发布 zip 成功生成于 `artifacts\publish\ChromeIsolator-win-x64-v1.6.7.zip`。
- 通过。`ChromeIsolator.exe` ProductVersion 为 `1.6.7`，FileVersion 为 `1.6.7.0`。
- 通过。`git diff --check` 无错误。
- 通过。7 个多语言资源文件均包含 116 个相同 key，且资源文件中不再存在 `\n` 字面量。
- 通过。GitHub Actions workflow 已确认 push main 和 `v*` tag 都会触发构建，tag 构建会发布 GitHub Release。

**本地产物清理**：

- 本轮生成 `artifacts/`、`bin/`、`obj/` 构建产物，均为可再生内容并在忽略列表中，不进入提交。

---

## 2026-05-23（托盘图标左键单击唤醒主窗口）

**状态：✅ 已完成**

**触发原因**：用户反馈托盘图标需要双击才显示主页面，容易误以为程序卡住；随后反馈托盘菜单“检查更新”弹窗把 `\n\n` 显示为字面文本。当前逻辑确实只处理双击打开，左键单击没有动作；多语言资源中也仍有部分弹窗字符串误用 `\n\n`。

**修改内容**：

1. `Services/TrayService.cs` — 将托盘图标左键单击改为直接唤醒主窗口，右键仍打开菜单，双击保留为兼容行为。
2. `Resources/Strings*.xaml` — 将托盘更新提示和退出确认中的字面 `\n\n` 改为 XML 换行实体。
3. `project-log/05-current-status.md` — 同步最近开发日志和当前修复项。

**验证方式**：

- `dotnet build ChromeIsolator.sln -c Release`
- 检查多语言资源中不再存在弹窗文案用的字面 `\n\n`

**验证结果**：

- 通过。Release 编译 0 警告，0 错误。
- 通过。`src/ChromeIsolator.App/Resources` 中不再存在 `\n` 字面量。
- 通过。7 个多语言资源文件均包含 116 个相同 key。

**本地产物清理**：

- 本轮仅生成常规 `bin/` / `obj/` 构建产物，均为可再生内容。

---

## 2026-05-23（V1.6.6 最终检查与发布触发）

**状态：✅ 已完成**

**触发原因**：用户要求对项目代码，尤其是本轮安装器和多语言修改进行全面复查；确认无误后推进一个小版本号，提交 GitHub，打 tag 并触发构建。

**修改内容**：

1. `Directory.Build.props` — 版本号从 `1.6.5` 推进到 `1.6.6`，用于生成新的正式构建 tag。
2. `project-log/05-current-status.md` — 同步当前版本、当前阶段和任务交接信息。
3. 本轮复查范围包含 `installer/Product.wxs`、`installer/Strings.zh-CN.wxl`、`scripts/build-msi.ps1`、多语言资源和版本发布配置。

**验证方式**：

- `dotnet build ChromeIsolator.sln -c Release`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1`
- `git diff --check`
- 多语言资源 key 完整性检查

**验证结果**：

- 通过。Release 编译 0 警告，0 错误。
- 通过。MSI 成功生成于 `artifacts\installer\ChromeIsolator-Setup-x64-v1.6.6.msi`，发布 zip 成功生成于 `artifacts\publish\ChromeIsolator-win-x64-v1.6.6.zip`。
- 通过。`ChromeIsolator.exe` ProductVersion 为 `1.6.6`，FileVersion 为 `1.6.6.0`。
- 通过。`git diff --check` 无错误。
- 通过。7 个多语言资源文件均包含 116 个相同 key。
- 通过。GitHub Actions workflow 已确认 push main 和 `v*` tag 都会触发构建，tag 构建会发布 GitHub Release。
- `dotnet test ChromeIsolator.sln -c Release --no-build` 返回成功，但当前解决方案未输出独立测试项目结果。

**本地产物清理**：

- 本轮生成 `artifacts/`、`bin/`、`obj/` 构建产物，均为可再生内容并在忽略列表中，不进入提交。

---

## 2026-05-23（多语言文案审查与按钮语义修正）

**状态：✅ 已完成**

**触发原因**：用户要求复查全部多语言文案，确认是否准确、精简、地道；审查中发现“浏览器引擎”按钮在多语言里仍偏名词化，不像动作按钮。

**修改内容**：

1. `src/ChromeIsolator.App/Resources/Strings*.xaml` — 将 `BtnPrepareChrome` / `BtnReinstallChrome` 统一改为动作式文案，中文改为“浏览器引擎设置”，英文改为 “Browser engine settings”，其余语言改为对应的动作表达。

**验证方式**：

- `dotnet build ChromeIsolator.sln -c Release`

**验证结果**：

- 通过。编译 0 警告，0 错误。

**本地产物清理**：

- 本轮仅生成常规 `bin/` / `obj/` 构建产物，均为可再生内容。

---

## 2026-05-23（安装器文案精简）

**状态：✅ 已完成**

**触发原因**：用户要求安装器文案更精简，避免欢迎页和卸载页说明过长。

**修改内容**：

1. `installer/Strings.zh-CN.wxl` — 精简欢迎页、维护页、卸载页、进度页、完成页和运行中提示文案，保留关键信息：固定安装到 Program Files、环境数据保留、运行中需先退出后重试。

**验证方式**：

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1`

**验证结果**：

- 通过。成功生成 `ChromeIsolator-Setup-x64-v1.6.5.msi`。

**本地产物清理**：

- 本轮生成的 `artifacts/`、`bin/`、`obj/` 属于可再生构建产物，按项目规则保留在忽略列表中。

---

## 2026-05-23（MSI 最小安装器 UI 与运行中重试）

**状态：✅ 已完成**

**触发原因**：用户要求安装器保留欢迎页，但不要引入许可页或目录选择页；同时卸载页要更清楚，并在程序运行时给出关闭提示和重试按钮。

**修改内容**：

1. `installer/Product.wxs` — 从内置完整向导收敛为自定义最小 UI，仅保留欢迎页、进度页、完成页和维护流程；安装目录继续固定到 `%ProgramFiles%\ChromeIsolator`。
2. `installer/Strings.zh-CN.wxl` — 新增中文本地化文案，覆盖欢迎页、维护页、卸载页、进度页和完成页，明确说明程序文件与用户环境数据分离。
3. `scripts/build-msi.ps1` — 引入 `WixToolset.UI.wixext` 和中文本地化资源，增加失败时检查，避免旧 MSI 产物掩盖打包错误。
4. `util:CloseApplication` — 更新运行中提示，明确安装、升级和卸载都需要先从系统托盘退出浏览器多开。
5. `project-log/10-planning-log.md`、`05-current-status.md`、`07-deployment.md` — 同步更新规划、状态和部署说明。

**遇到的问题**：

- `WixLocalization` 使用 `Codepage="65001"` 时，WiX 报 summary information code page 无效。
- `wix build` 失败后如果旧 MSI 文件还在，脚本会误以为构建成功。

**解决方式**：

- 将本地化文件 code page 改为 `936`。
- 构建前删除旧 MSI，并检查 `$LASTEXITCODE`，让构建失败真正失败。

**验证方式**：

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1`
- `git diff --check`

**验证结果**：

- 通过。成功生成 `artifacts\installer\ChromeIsolator-Setup-x64-v1.6.5.msi`。
- 通过。`git diff --check` 无错误。

**本地产物清理**：

- 本轮生成 `artifacts/`、`src/ChromeIsolator.App/bin/`、`src/ChromeIsolator.App/obj/`，属于可再生构建产物，保留在忽略列表中。

---

## 2026-05-23（MSI 安装和卸载完成反馈）

**状态：✅ 已完成**

**触发原因**：用户反馈双击安装包后安装窗口跑完直接消失，普通用户没有明确的安装完成反馈；卸载流程也缺少清晰反馈，需要安装 / 卸载结束时停留在完成页。

**修改内容**：

1. `installer/Product.wxs` — 先引入 WiX UI 命名空间并启用 `WixUI_Minimal`，随后根据用户反馈收回到默认 MSI UI，避免增加许可页、目录页等额外交互；继续固定安装到 `%ProgramFiles%\ChromeIsolator`。
2. `scripts/build-msi.ps1` — 先补充 `WixToolset.UI.wixext`，随后移除该依赖，回到更保守的默认 MSI 构建路径，只保留必要的 `WixToolset.Util.wixext`。
3. `Directory.Build.props` — 版本号从 `1.6.3` 推进到 `1.6.5`，用于触发新一轮安装包构建。
4. `installer/License.rtf` — 已删除，避免许可页带来额外页面与内容分歧。
5. `07-deployment.md`、`05-current-status.md` — 同步记录 MSI 安装 / 卸载反馈行为。

**遇到的问题**：

- 当前 MSI 未接入 WiX UI 扩展，双击运行时只依赖 Windows Installer 默认行为，完成后不会停留在明确的完成页。

**解决方式**：

- 先前方案引入了额外页面，不符合“只要进度和完成提示”的要求；回退到默认 MSI UI，保持最少干预。

**验证方式**：

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1`

**验证结果**：

- 后续被 ADR-010 的自定义最小 MSI UI 方案替代，并已在 V1.6.6 最终检查中通过 MSI 构建验证。

**本地产物清理**：

- 本轮未保留新的构建产物；待重新构建后再决定是否清理 `artifacts/`。

---

## 2026-05-23（设置页帮助更新与语言控件优化）

**状态：✅ 已完成**

**触发原因**：用户反馈设置页底部“帮助与更新”信息架构不清晰：缺少作者信息，“复制邮箱”意图不明，“查看发布页”对普通用户不友好，检查到更新后缺少版本信息和下载指引；随后继续反馈语言区域标题与下拉框视觉风格不够统一。

**修改内容**：

1. `SettingsWindow.xaml` — “帮助与更新”改为按“作者 / 联系邮箱 / 更新状态”展示信息，按钮保留统一 `SecondaryButton` 风格；语言区继续使用设置页卡片布局。
2. `SettingsViewModel.cs` — 新增 `AuthorName` 和 `ContactEmail` 属性；“打开下载页”打开 GitHub 最新 Release 页面；复制邮箱命令复用展示邮箱，避免展示值和复制值分叉。
3. `UpdateService.cs` / `MainViewModel.cs` — 新增 `LatestReleaseUrl`，设置页和托盘更新提示均跳转到 `/releases/latest`。
4. `Resources/Strings*.xaml` — 全语言更新“打开下载页”“联系作者：复制邮箱”等文案；新增作者、联系邮箱、更新状态标签；更新可用时显示当前版本、最新版本和下载运行安装包的指引；非英语语言标题追加 `Language`。
5. `Themes/Controls.xaml` — 为 `ComboBox` / `ComboBoxItem` 补齐自定义模板，使下拉框边框、圆角、hover、焦点和选中态与现有按钮、输入框和卡片风格一致。
6. `Directory.Build.props` — 版本号从 `1.6.2` 推进到 `1.6.3`，用于触发 GitHub Actions 构建测试包。

**验证方式**：

- `dotnet build ChromeIsolator.sln -c Release`

**验证结果**：

- 构建通过，0 警告，0 错误。

---

## 2026-05-23（环境卡片宽度问题根因修复 — 已验证）

**状态：✅ 已解决**

**触发原因**：继续排查主窗口左侧环境卡片无法稳定横向铺满、右侧被截断的问题。

**根因确认**：

此前分析集中在 `ListBox` / `VirtualizingStackPanel` / `ListBoxItem.MinWidth` 的宽度计算上，但实际布局链路还有一个更上游的问题：左侧容器使用 `DockPanel`，子元素顺序为标题 `TextBlock`、`ListBox`、空状态 `StackPanel`。由于 `DockPanel.LastChildFill` 默认只让最后一个子元素填充剩余空间，而 `ListBox` 不是最后一个子元素，且未设置 `DockPanel.Dock`，因此它会按默认 `Dock.Left` 和自身期望宽度参与布局，不会天然铺满左侧区域。

这会导致：

- `HorizontalContentAlignment="Stretch"` 的父级可用宽度本身不稳定。
- `ListBoxItem.MinWidth = ListBox.ActualWidth` 变成自我补偿式硬撑。
- 卡片铺满时容易刚好顶到父容器裁剪边界，出现右侧圆角 / 边框截断。

**修改内容**：

1. `MainWindow.xaml` — 左侧容器从 `DockPanel` 改为两行 `Grid`：标题行 `Auto`，列表区域 `*`。`ListBox` 和空状态放在同一个内容 `Grid` 中叠放，确保 `ListBox` 明确获得完整可用宽度。
2. `Themes/Controls.xaml` — 移除 `ModernListBoxItem` 中绑定到 `ListBox.ActualWidth` 的 `MinWidth`，恢复由父布局和 `HorizontalContentAlignment="Stretch"` 正常控制 item 宽度。
3. `ViewModels/WidthMinusConverter.cs` — 删除前几轮宽度补偿方案遗留的未引用 converter，避免后续继续沿用像素减法修补。
4. `Directory.Build.props` — 版本号从 `1.6.1` 推进到 `1.6.2`，用于触发 GitHub Actions 构建和测试包发布。

**验证方式**：

- `xmllint --noout src/ChromeIsolator.App/MainWindow.xaml src/ChromeIsolator.App/Themes/Controls.xaml`
- `git diff --check`

**验证结果**：

- XAML XML 结构检查通过。
- 空白检查通过。
- 当前 macOS 环境中 `dotnet` 命令不可用，未能执行 `dotnet build ChromeIsolator.sln`。
- 已推送 `main` 和 `v1.6.2` tag，由 GitHub Actions 执行 Windows 构建。
- 用户在 Windows 实机测试确认：左侧环境卡片布局已完美修复，卡片铺满且右侧不再截断。

---

## 2026-05-22（环境卡片宽度问题排查 — 未解决）

**状态：❌ 未解决**

**触发原因**：主窗口左侧环境列表中，环境卡片没有横向铺满列表容器宽度，卡片只占左侧一部分，右侧留有空白。该问题从 V1.2.0 开始反复出现，经历 V1.4.0、V1.5.0、V1.5.1、V1.6.0、V1.6.1 共 5 个版本尝试修复，始终未彻底解决。

### 问题背景

主窗口布局为左右分栏：左侧是环境卡片列表（`ListBox`），右侧是环境详情。左侧容器为 `Border`（`BorderThickness="1"`, `CornerRadius="8"`）内嵌 `DockPanel`，`ListBox` 在其中填充剩余空间。`ListBox` 设置了 `Margin="4"`、`BorderThickness="0"`、`HorizontalContentAlignment="Stretch"`。

### 涉及的关键文件

| 文件 | 作用 |
|------|------|
| `MainWindow.xaml` 第 68-88 行 | 左侧 Border → DockPanel → ListBox 布局 |
| `Themes/Controls.xaml` 第 246-289 行 | `ModernListBoxItem` 样式（卡片外观、宽度控制） |
| `ViewModels/WidthMinusConverter.cs` | 宽度减法转换器（V1.5.0 引入，V1.6.1 移除） |

### 像素级布局链路分析

```
Grid 列0: Width="360" → 分配 360px
  └─ Border: BorderThickness="1" → 内容区 = 360 - 1×2 = 358px
       └─ DockPanel: 填充 Border 内容 = 358px
            └─ ListBox: Margin="4", BorderThickness="0"
                 → 可用宽度 = 358 - 4×2 = 350px
                 → ActualWidth = 350px
                 → 内容区 = 350px
                      └─ ScrollViewer（WPF ListBox 默认模板）
                           → 默认 Padding="1" → 内容区 = 350 - 1×2 = 348px
                           → 如果 ListBox.Padding="0" → 内容区 = 350px
                                └─ VirtualizingStackPanel（垂直方向）
                                     → 给子元素的可用宽度：理论上 = ScrollViewer 内容区宽度
                                     → 实际行为：可能给"无限大"，子元素按内容自适应
```

### 关键发现：VirtualizingStackPanel 的拉伸行为

WPF 的 `ListBox` 内部使用 `VirtualizingStackPanel`（垂直方向）作为 items 面板。在 `ScrollViewer` 内部，`VirtualizingStackPanel` 对子元素的横向布局行为有两种可能：

1. **给子元素容器宽度**：子元素设置 `HorizontalAlignment="Stretch"` 后可以拉伸到容器宽度
2. **给子元素无限宽度**：子元素按内容自适应宽度，`HorizontalAlignment="Stretch"` 无效

在本项目中观察到的行为更接近第 2 种：**`HorizontalAlignment="Stretch"` 未生效，卡片按内容宽度排列**。但 `MinWidth` 属性可以强制撑大卡片（即使内容宽度小于 MinWidth）。

### 各版本尝试记录

#### V1.4.0（fa6f241）— 效果最好，但右侧截断

**代码**：
```xml
<!-- Controls.xaml -->
<Setter Property="MinWidth" Value="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=ListBox}}" />
```
```xml
<!-- MainWindow.xaml — ListBox 无 Padding="0" -->
<ListBox ... Margin="4" BorderThickness="0" ... />
```

**效果**：卡片横向铺满列表区域，但右侧略微溢出，选中状态的圆角边框右侧被截断。

**分析**：
- `MinWidth = ListBox.ActualWidth = 350px`，强制卡片撑到 350px
- 但 WPF 默认 `ListBox` 模板内 `ScrollViewer` 有 `Padding="1"`，实际内容区只有 348px
- 卡片 350px > 内容区 348px → 溢出 2px → 右侧圆角被父 Border 的 `CornerRadius="8"` 裁切

#### V1.5.0（399cfd7）— 卡片完全不显示

**代码**：
```xml
<!-- Controls.xaml — 新增 WidthMinusConverter，改用 Width 而非 MinWidth -->
<Setter Property="Width" Value="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=ListBox}, Converter={StaticResource WidthMinusConverter}, ConverterParameter=16}" />
```
```xml
<!-- MainWindow.xaml — ListBox 无 Padding="0" -->
```

**效果**：卡片完全不显示，左侧列表区域全空白。

**分析**：
- 首次引入 `WidthMinusConverter`，将 `MinWidth` 改为 `Width`（强制定宽）
- `Width = 350 - 16 = 334px`
- `Width` 是强制定宽，理论上应锁定卡片为 334px
- 卡片不显示的原因可能是：`Width` 属性在 `VirtualizingStackPanel` 中的行为与预期不同，或 converter 绑定初始化时序问题导致 `ActualWidth` 为 0 → `Width` 被设为 0

#### V1.5.1（b2aea3a）— 卡片太窄，右侧留白

**代码**：
```xml
<!-- Controls.xaml — 改回 MinWidth，保留 converter -->
<Setter Property="MinWidth" Value="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=ListBox}, Converter={StaticResource WidthMinusConverter}, ConverterParameter=16}" />
```
```xml
<!-- MainWindow.xaml — ListBox 无 Padding="0" -->
```

**效果**：卡片恢复显示，但只占列表区域左侧一部分，右侧明显留白。

**分析**：
- `MinWidth = 350 - 16 = 342px`
- 如果 `HorizontalAlignment="Stretch"` 生效，卡片应拉伸到 348px（ScrollViewer 内容区），间隙 2px
- 但用户描述的间隙远大于 2px（"只占左侧一部分"），说明 `HorizontalAlignment="Stretch"` 确实未生效
- 卡片停在 `MinWidth = 342px`，与 350px 内容区差 8px → 间隙约 8px

#### V1.6.0（58b8970 + 12521da）— 尝试两种方案均无效

**第一次尝试（58b8970）**：
```xml
<!-- Controls.xaml — MinWidth + 调小 converter 参数 -->
<Setter Property="MinWidth" Value="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=ListBox}, Converter={StaticResource WidthMinusConverter}, ConverterParameter=8}" />
```
```xml
<!-- MainWindow.xaml — 新增 Padding="0" -->
<ListBox ... Margin="4" Padding="0" BorderThickness="0" ... />
```

**效果**：与 V1.5.1 类似，卡片未铺满。

**分析**：
- `MinWidth = 350 - 8 = 342px`，加上 `Padding="0"` 消除了 ScrollViewer 的默认 padding
- 但 `MinWidth=342` 仍小于 350px，`HorizontalAlignment="Stretch"` 仍未生效，卡片停在 342px

**第二次尝试（12521da）**：
```xml
<!-- Controls.xaml — 改用 Width -->
<Setter Property="Width" Value="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=ListBox}, Converter={StaticResource WidthMinusConverter}, ConverterParameter=8}" />
```
```xml
<!-- MainWindow.xaml — Padding="0" 保留 -->
```

**效果**：与 V1.5.0 类似，卡片可能不显示或异常。

#### V1.6.1（1cacdb4）— 回到 V1.4.0 思路，右侧仍截断

**代码**：
```xml
<!-- Controls.xaml — 回到 V1.4.0 的 MinWidth，不减任何值 -->
<Setter Property="MinWidth" Value="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=ListBox}}" />
```
```xml
<!-- MainWindow.xaml — Padding="0" 保留 -->
<ListBox ... Margin="4" Padding="0" BorderThickness="0" ... />
```

**效果**：卡片铺满，但右侧仍然截断（与 V1.4.0 相同的问题）。

**分析**：
- `MinWidth = 350px`，`Padding="0"` 理论上应让 ScrollViewer 内容区 = 350px
- 但右侧仍然截断，说明 `Padding="0"` 可能未生效，或者截断原因不是 ScrollViewer padding
- 可能的原因：
  1. `ListBox.Padding` 通过 `TemplateBinding` 传递给 ScrollViewer 的行为在 .NET 8 WPF 中可能不同
  2. 父 `Border` 的 `CornerRadius="8"` 在右侧边缘裁切了卡片的边框
  3. 选中状态的 `BorderThickness="1"` 是外扩（outset）而非内缩（inset），导致超出容器

### 核心未解问题

1. **`HorizontalAlignment="Stretch"` 为什么不生效？** — `VirtualizingStackPanel` 在 `ScrollViewer` 内的横向布局行为需要进一步确认。可能需要查看 .NET 8 WPF 的默认 `ListBox` ControlTemplate 源码。

2. **`ListBox.Padding="0"` 是否真正传递给了内部 `ScrollViewer`？** — 需要验证 WPF 默认模板中 `Border` 的 `Padding` 是否使用 `TemplateBinding` 绑定到 `ListBox.Padding`。

3. **右侧截断是 ScrollViewer padding 导致还是 CornerRadius 裁切导致？** — 需要分别测试：去掉 CornerRadius、去掉 BorderThickness、测量实际像素。

### 后续建议

1. **查看 .NET 8 WPF 默认 ListBox ControlTemplate 源码**：确认 `ScrollViewer` 的 `Padding` 是否 `TemplateBinding` 到 `ListBox.Padding`，确认 `ItemsPresenter` 的布局行为。

2. **运行时诊断**：在 `MainWindow.xaml.cs` 中添加代码，运行时读取 `ProfilesList.ActualWidth`、内部 `ScrollViewer` 的 `ActualWidth` 和 `Padding`、第一个 `ListBoxItem` 的 `ActualWidth`，输出到调试日志，获取真实像素值。

3. **替代方案探索**：
   - 给 `ListBoxItem` 的模板 `Border` 设置 `Margin="-1,0"` 或类似负边距，补偿间隙
   - 自定义 `ListBox` 的 `ItemsPanelTemplate`，用 `Grid` 替代 `VirtualizingStackPanel`，强制子元素拉伸
   - 直接在 `ListBoxItem` 的 `Loaded` 事件中通过代码设置 `Width = listBox.ActualWidth`

4. **截断问题**：如果确认是 `CornerRadius` 裁切导致，可以给卡片 `Border` 设置 `Margin="0,0,2,0"` 避免溢出到圆角区域，或给父 `Border` 增加 `Padding="2"` 让内容区远离圆角。

---

## 2026-05-22（修复 MsgConfirmInstallChrome 换行符字面量）

**触发原因**：用户发现点击"安装官方 Chrome"时，确认弹窗中"是否继续？"前面显示两个字面 `\n` 而非换行。

**问题根因**：XAML `<sys:String>` 中的 `\n` 不会被解析为换行符，而是作为字面文本保留。此前 `MsgDeleteConfirm` 已在第五轮评审（E.9）中修复过同样问题，但 `MsgConfirmInstallChrome` 遗漏了。

**修改内容**：
1. `Resources/Strings.xaml`（zh）、`Strings.en.xaml`、`Strings.de.xaml`、`Strings.ja.xaml`、`Strings.ko.xaml`、`Strings.ru.xaml`、`Strings.fr.xaml` — `MsgConfirmInstallChrome` 中的 `\n\n` 改为 `&#x0A;&#x0A;`（XML 换行实体）。

**经验记录**：
- WPF XAML `<sys:String>` 资源中，`\n` 是字面量，不是转义序列。需要换行时必须使用 `&#x0A;` 或 `&#x0a;`（XML 字符实体）。
- 此规则适用于所有 7 个语言资源文件，修改时需逐一排查。

**验证方式**：
- `dotnet build ChromeIsolator.sln`，0 警告 0 错误。

---

## 2026-05-22（V1.4.0 用户测试反馈修复 — 3 项）

**触发原因**：用户测试最新版本后反馈 3 个问题：左侧环境卡片未横向铺满列表区域、删除低编号环境后新增环境跳到更高编号、主窗口隐藏到托盘后双击桌面图标会启动第二个应用实例并出现两个托盘图标。

**修改内容**：
1. `Themes/Controls.xaml` — 强化 `ModernListBoxItem` 横向撑满：绑定 `MinWidth` 到宿主 `ListBox.ActualWidth`，模板根 `Border` 设置横向拉伸，并补齐模板 `BorderBrush` / `BorderThickness` 绑定。
2. `ProfileManager.cs` — 删除环境后按 `Folder` 名称从配置中移除 profile，避免对象引用不一致导致配置残留；新增环境继续显式选择最小可用编号。
3. `App.xaml.cs` — 新增单实例检测：使用命名 `Mutex` 阻止第二个完整实例启动；第二次启动通过命名管道通知已有实例显示主窗口，然后退出。
4. `MainWindow.xaml.cs` — 主窗口从托盘 / 第二次启动唤醒时恢复、激活并带到前台。
5. `Directory.Build.props` — 版本号从 `1.3.0` 推进到 `1.4.0`，用于触发新一轮 Release 构建。

**验证方式**：
- `dotnet build ChromeIsolator.sln`
- `git diff --check`

**验证结果**：
- 通过。编译 0 警告、0 错误。
- 通过。空白检查无错误。

**本地产物清理**：
- `dotnet build` 生成 `src/ChromeIsolator.App/bin/` 和 `obj/`，按 `.gitignore` 忽略，未纳入版本变更。

---

## 2026-05-22（准备 V1.3.0 发布）

**触发原因**：用户要求将版本号推进到 1.3.0，发布前全面检查代码，有问题先修复，确认无问题后提交并打包。

**修改内容**：
1. `Directory.Build.props` — 版本号从 `1.2.0` 推进到 `1.3.0`，同步更新 Assembly/File/Informational 版本。
2. `ChromeManager.cs` — 补充 `process.Start()` 异常路径释放 `Process` 对象，避免浏览器启动失败时留下托管 / 原生句柄。

**验证方式**：
- 待执行发布构建和 MSI 打包。

---

## 2026-05-22（全界面风格一致性检查与细节页优化）

**触发原因**：用户担心仍有部分细节页面保留早期 WPF 默认样式，例如删除确认页面等，希望全面检查所有页面风格是否统一。

**检查范围**：
- `MainWindow.xaml` 主窗口。
- `SettingsWindow.xaml` 设置窗口。
- `DownloadWindow.xaml` 浏览器引擎设置 / Chrome 安装窗口。
- `SimpleInputDialog.cs` 重命名 / 删除确认 / 新增后命名弹窗。
- `Themes/Controls.xaml` 基础控件样式。
- 系统托盘菜单和系统 MessageBox 路径。

**修改内容**：
1. `Themes/Controls.xaml` — 新增统一 TextBox、ComboBox、CheckBox 基础样式；新增 `ModernProgressBar` 供下载窗口使用；保留主窗口启动 / 关闭中的默认 indeterminate ProgressBar，避免动画被自定义模板破坏。
2. `SettingsWindow.xaml` — 设置页按钮区从横向 StackPanel 改为 WrapPanel，多语言或窄窗口下自动换行，避免按钮挤压。
3. `DownloadWindow.xaml` — 浏览器引擎窗口从早期 StackPanel 改为“卡片内容 + 底部 WrapPanel 操作区”，标题、说明、状态和进度条样式与主窗口一致。
4. `SimpleInputDialog.cs` — 重命名 / 删除确认弹窗改为带图标、卡片内容、主题输入框和主题按钮的现代样式；设置 Owner 和应用图标，删除确认使用危险色图标。

**验证方式**：
- `dotnet build ChromeIsolator.sln -c Release`，0 警告 0 错误。
- `git diff --check`，无错误。

**仍需人工视觉验证**：
- 中文、英文、德语、俄语下设置页按钮换行是否自然。
- 删除确认弹窗长文案是否完整显示且输入框/按钮不挤压。
- Chrome 缺失、Chrome 已安装、下载失败三种浏览器引擎窗口状态。

---

## 2026-05-22（MSI 升级安装运行中检测）

**触发原因**：用户担心下载新版本 MSI 后直接覆盖安装时，旧版应用仍在运行导致升级异常、文件占用或浏览器环境残留。

**修改内容**：
1. `installer/Product.wxs` — 引入 WiX Util 扩展，增加 `util:CloseApplication` 检测 `ChromeIsolator.exe`。安装 / 升级时若应用正在运行，会提示用户先从系统托盘退出“浏览器多开”，然后点击重试继续安装。
2. `installer/Product.wxs` — MSI `Package` 增加 `Codepage="65001"`，避免中文快捷方式和运行中提示文案在 MSI 数据库中触发 1252 编码问题。
3. `scripts/build-msi.ps1` — 构建前自动确保 `WixToolset.Util.wixext` 已加入 WiX 扩展缓存，并在 `wix build` 中引用 `-ext WixToolset.Util.wixext`。
4. `10-planning-log.md`、`07-deployment.md` — 记录升级安装策略和后续开发计划。

**验证方式**：
- `dotnet build ChromeIsolator.sln -c Release`，0 警告 0 错误。
- `git diff --check`，无错误。
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1`，成功生成 `artifacts\installer\ChromeIsolator-Setup-x64-v1.2.0.msi`。

**设计决策**：
- 安装器只提示用户退出，不强制 kill 应用。强杀会绕过应用内浏览器关闭逻辑，增加 Chrome 环境残留风险。

---

## 2026-05-22（多环境隔离与生命周期专项加固）

**触发原因**：用户担心不同环境串台、高频连续点击导致启动 / 关闭错乱、浏览器关闭不彻底、残留进程、内存 / 句柄泄漏和其他边界问题。

**修改内容**：
1. `ProfileManager.cs` — 启动时对配置和磁盘 profile 做合并去重；若配置中出现重复 folder，只保留第一项，避免两个 UI 环境指向同一个 `Profiles\pN` 目录造成串台。
2. `MainViewModel.cs` — `StopAllAsync()` 在 UI 线程先快照 profile model 列表，避免后台线程枚举 `ObservableCollection`；新增 `_isBulkStopping`，批量关闭时禁止启动新环境，降低快速连点导致的状态交错。
3. `ChromeManager.cs` — 单环境关闭时优先通过 CDP `Browser.close` 关闭整个浏览器实例，再 fallback 到 `CloseMainWindow()` 和 `Kill(entireProcessTree: true)`；Kill 后等待退出，减少残留进程概率。
4. `FingerprintInjector.cs` — `DisposeAsync()` 改为幂等，等待注入后台任务结束后再释放锁和 CTS，避免 Stop 与 Exited 并发触发二次释放或后台任务访问已释放对象。
5. `App.xaml.cs` — 应用退出时增加最后兜底清理，调用 `ChromeManager.StopAll()` 关闭当前已知环境，降低异常退出 / 系统退出路径留下浏览器进程的概率。

**验证方式**：
- `dotnet build ChromeIsolator.sln -c Release`，0 警告 0 错误。
- `git diff --check`，无错误。

**仍需人工压力验证**：
- 快速连续点击启动 / 关闭同一环境。
- 同时多开多个环境后连续点击全部关闭、托盘退出、手动关闭 Chrome 窗口。
- 重启应用后确认无残留 ChromeIsolator profile 对应浏览器进程。

---

## 2026-05-22（V1.2.0 追加代码复审修复 — 3 项稳定性问题）

**触发原因**：用户要求全面检查项目代码，继续寻找 bug 和可优化点。

**修改内容**：
1. `FingerprintInjector.cs` — 将 `_injectedTargets` / `_attachingTargets` 从 `HashSet` 改为 `ConcurrentDictionary`，修复 ReceiveLoop、注入任务和重连清理之间并发读写集合的线程安全风险。
2. `ChromeManager.cs` — `Start()` 在同一锁内完成“是否已运行”判断、进程启动和字典登记；若存在已退出的残留进程引用，启动前先清理进程、端口和注入器，避免极快退出或重复启动导致状态错乱 / 句柄残留。
3. `MainWindow.xaml.cs` — 托盘退出路径捕获 `StopAllAsync()` 异常，失败时恢复关闭保护并弹窗提示，避免 async void 退出流程异常进入全局 fatal handler。

**验证方式**：
- `dotnet build ChromeIsolator.sln -c Release`，0 警告 0 错误。
- `git diff --check`，无错误。

---

## 2026-05-22（V1.2.0 用户测试反馈修复 — 9 项问题）

**触发原因**：用户测试 V1.2.0 时反馈主窗口列表显示、行内按钮、单环境关闭、语言下拉、更新检查、托盘右键、手动删除 profile、卸载残留和重装识别等问题。

**修改内容**：
1. `MainWindow.xaml`、`Themes/Controls.xaml` — 修复环境列表项未撑满左侧栏的问题，显式拉伸 ListBoxItem 内容和 DataTemplate 根布局。
2. `MainViewModel.cs`、`MainWindow.xaml` — 行内启动 / 停止按钮拆成独立命令，停止按钮不再复用“已运行则置前”的 Toggle 逻辑；行内按钮显式传入当前 profile。
3. `MainWindow.xaml.cs` — 右键环境行时先选中对应行，避免右键菜单作用到旧选中项。
4. `L10n.cs`、`SettingsViewModel.cs`、`SettingsWindow.xaml` — 多语言列表从 ValueTuple 改为显式 `LanguageOption`，修复语言下拉为空。
5. `UpdateService.cs` — GitHub API 返回 403 时 fallback 到 GitHub Releases `/latest` 重定向页解析版本号。
6. `TrayService.cs` — 托盘右键改为 MouseUp 时即时构建并显示菜单，避免首次右键无反应。
7. `ProfileManager.cs` — 启动时用磁盘 `Profiles\pN` 目录和配置合并：手动删除目录后不再重建；卸载后保留目录、重装时可重新识别；新增环境改为使用下一个可用编号。

**验证方式**：
- `dotnet build ChromeIsolator.sln -c Release`，0 警告 0 错误。
- `git diff --check`，无错误。

**设计决策**：
- 卸载默认继续保留 `%LOCALAPPDATA%\ChromeIsolator` 用户数据，避免误删浏览器登录态和 profile；本轮重点修复“保留后重装不能识别”的问题。

---

## 2026-05-22（V1.1.0 代码复审修复 — 5 项问题）

**触发原因**：对 V1.1.0 用户测试 12 项修复进行逐项代码复审，发现 4 项代码缺陷 + 1 项内存泄漏。

**修改内容**：
1. `MainViewModel.cs` — `StopSelected` 和 `StopAllCommand` 的 fire-and-forget 异步调用改为 `StopProfileSafeAsync` / `StopAllSafeAsync`（internal），内部 try-catch 弹窗提示异常，避免 `UnobservedTaskException`；`StopAllAsync` 和 `StopProfileAsync` 的 `Task.Run` 加 `.ConfigureAwait(true)` 确保续接 UI 线程。
2. `TrayService.cs` — 托盘菜单的 StopProfile/StopAll 调用改为 `StopProfileSafeAsync` / `StopAllSafeAsync`。
3. `SimpleInputDialog.cs` — 弹窗从固定 `Height=200` 改为 `SizeToContent=Height` + `MaxHeight=400`，修复德语/俄语等长文案语言显示不全。
4. `ChromeManager.cs` — 窗口偏移从 `InstanceNumber` 改为 `_processes.Count`（当前运行环境数），避免删除环境后偏移过远；`Process.Exited` handler 新增 `exitedProcess?.Dispose()` 修复原生进程句柄泄漏（与 `Stop()` 的 `finally { process.Dispose(); }` 通过字典 Remove 互斥，不会双重释放）。

**验证方式**：
- `dotnet build ChromeIsolator.sln`，0 警告 0 错误。

**验证结果**：
- 构建通过。待用户实际安装验证。

---

## 2026-05-22（V1.1.0 用户测试反馈修复 — 12 项问题）

**触发原因**：用户 V1.1.0 全量测试发现 12 项问题，含 2 个严重 Bug（UI 线程阻塞、配额耗尽崩溃）。

**修改内容**：
1. `MainViewModel.cs` — `StopAll()` 改为 `StopAllAsync()`，使用 `Task.Run` 异步执行避免阻塞 UI 线程；`StopProfile()` 改为 `StopProfileAsync()`；`OnProfileExited` 改用 `Dispatcher.BeginInvoke` 非阻塞回调；`ToggleProfile` 已运行环境改为调用 `BringToFront`；`DeleteSelected` 确认逻辑改为输入 `delete`。
2. `ChromeManager.cs` — 新增 `BringToFront` 方法，使用 Win32 `SetForegroundWindow` + `ShowWindow` 将 Chrome 窗口带到前台；启动参数新增 `--window-position` 按环境编号错开窗口位置。
3. `MainWindow.xaml.cs` — `ExitFromTray` 改为 async，退出前等待 `StopAllAsync` 完成。
4. `TrayService.cs` — 异步化 StopProfile/StopAll 调用；移除重复的"全部关闭并退出"入口；重组菜单结构：打开管理面板置顶 → 运行中环境关闭 → 全部关闭 → 已停止环境启动 → 检查更新 → 退出。
5. `MainWindow.xaml` — 移除工具栏"浏览器引擎设置"按钮；调整顺序为添加环境/全部关闭/设置；三个按钮统一添加 Segoe Fluent Icons 图标；ListBox 添加 `HorizontalContentAlignment="Stretch"` 修复选中框宽度；操作按钮区改用 WrapPanel，删除按钮不再全宽拉伸。
6. `SettingsWindow.xaml` — 语言下拉框改用 `DisplayMemberPath="Item2"` 替代 DataTemplate 绑定。
7. `SimpleInputDialog.cs` — 应用主题色（PrimaryButton/SecondaryButton 样式、TextPrimaryBrush、WindowBackgroundBrush），增大输入框尺寸。
8. `Resources/Strings*.xaml`（7 语言）— `MsgDeleteConfirm` 中 `\n` 字面量改为 `&#x0a;` 真正换行；确认文案改为"输入 delete 确认"。
9. `Themes/Controls.xaml` — `ModernListBoxItem` 新增 `HorizontalAlignment="Stretch"`。
10. `11-code-review-log.md` — 新增第五轮用户测试反馈记录（12 项问题）。
11. `05-current-status.md` — 更新当前阶段和高优先级待办。

**设计决策**：
- 双击已运行环境：通过 `process.MainWindowHandle` + `SetForegroundWindow` 带到前台，只激活主窗口，简单合理。
- 删除确认：改为输入固定英文 `delete`，避免用户混淆输入"环境名"还是"自定义名"。
- 托盘菜单：合并"全部关闭并退出"和"退出"为单一"退出"入口；"打开管理面板"置顶。
- 异步化：`Stop` 仍为同步方法（内含进程等待逻辑），但通过 `Task.Run` 包装避免阻塞 UI 线程。

**验证方式**：
- `dotnet build ChromeIsolator.sln`，0 警告 0 错误。

**验证结果**：
- 构建通过。待用户实际安装验证。

---

## 2026-05-22（主界面 UI 全面重设计）

**触发原因**：用户反馈主界面"太丑"，与 BrowserIsolator macOS 版差距明显。具体痛点：(1) 左侧环境列表标签只显示一半；(2) 列表行没有 BrowserIsolator 的行内启动/停止按钮；(3) 工具栏 9 个按钮无逻辑堆叠。

**修改内容**：
1. 新增 `Themes/Colors.xaml` — 定义所有颜色 Brush 资源（主题色、文字色、背景色、边框色、状态色）和 CornerRadius 常量，替换散落在各处的硬编码颜色值。
2. 新增 `Themes/Controls.xaml` — 定义现代化控件样式：PrimaryButton（主题色填充）、SecondaryButton（边框样式）、ToolbarButton（工具栏扁平按钮）、ListBoxItem 自定义 ControlTemplate（圆角选中高亮、悬停效果）。
3. `MainWindow.xaml` — 替换 ToolBar 为 Border+StackPanel 分组工具栏（环境操作、引擎/设置、编辑操作三组）；重做 ListBoxItem 行模板，加入行内启动/停止按钮和图标元数据；修复左侧标签截断；添加 GridSplitter 支持拖拽调整面板宽度；重排详情面板顺序对齐 BrowserIsolator；添加列表空状态和详情面板无选中空状态。
4. `MainWindow.xaml.cs` — 新增行内按钮点击事件处理，按钮 Focusable=false 防止点击时改变行选中。
5. `TrayService.cs` — 运行中/已停止环境分组显示，按最近使用排序，新增"全部关闭并退出"快捷操作。
6. `SettingsWindow.xaml`、`DownloadWindow.xaml` — 应用新的 PrimaryButton/SecondaryButton 样式。
7. `App.xaml` — 合并 Themes 资源字典。
8. 7 语言资源文件 — 新增 UI 重设计所需的多语言字符串。

**设计决策**：
- 不引入 NuGet UI 框架，手写样式保持零依赖。
- 图标使用 Windows 10/11 系统自带 Segoe Fluent Icons 字体，零额外资源。
- 选中高亮用纯色近似 BrowserIsolator 的毛玻璃效果（WPF 无法实现真正的 ultraThinMaterial）。
- 行内按钮用 Focusable=false + 事件冒泡处理，避免点击按钮改变行选中。
- 保留系统窗口标题栏，不做自定义 WindowChrome。

**验证方式**：
- `dotnet build ChromeIsolator.sln -c Release`，0 警告 0 错误。
- MSI 重新构建。
- 用户实际安装验证视觉效果和交互。

**验证结果**：
- 构建通过，0 警告 0 错误。
- 全面审查通过（见下一条记录）。

---

## 2026-05-22（最终审查修复 + 版本发布 1.1.0）

**触发原因**：主界面 UI 重设计完成后，进行全面代码审查，发现并修复遗漏问题，准备发布 V1.1.0。

**修改内容**：
1. `MainViewModel.cs` — 修复 `SelectedProfileTitle` 在重命名和新建环境后不刷新的问题：在 `RenameSelected()` 和 `AddProfile()` 中补充 `OnPropertyChanged(nameof(SelectedProfileTitle))`。
2. `Directory.Build.props` — 版本号从 1.0.0 升级到 1.1.0。
3. `project-log/05-current-status.md` — 更新当前状态，UI 重设计从"进行中"移至"已完成"。

**审查确认项**：
- ToggleProfile 空值保护：正确。
- 行内按钮命令传递（RelativeSource AncestorType=ListBox）：正确。
- 语言下拉框 Item2 绑定 ValueTuple：正确。
- UniformGrid 双列按钮布局：正确。
- 7 语言资源键一致性：全部一致。
- 全局异常处理器：已就位。
- 主题系统合并：正确。
- 托盘菜单逻辑分组：正确。
- 安装程序桌面快捷方式：已定义。

**验证方式**：
- `dotnet build ChromeIsolator.sln -c Release`，0 警告 0 错误。
- 全面代码审查确认无遗漏。

**验证结果**：
- 构建通过，审查通过。

---

## 2026-05-22（浏览器引擎设置窗口 UX 优化）

**触发原因**：用户首次启动时，Chrome 已安装的情况下弹出的浏览器引擎设置窗口仍然显示进度条（0%）和"关闭"按钮，与"开始使用"按钮并列，用户不知该点哪个。

**修改内容**：
1. `src/ChromeIsolator.App/DownloadWindow.xaml.cs` — Chrome 已就绪时隐藏 `ProgressBar` 和 `PercentText`（无下载任务时进度条无意义）；隐藏 `CancelButton`（用户可通过窗口 X 关闭，只保留"开始使用"一个明确动作）。

**验证方式**：
- `dotnet build ChromeIsolator.sln -c Release`，0 警告 0 错误。
- MSI 重新构建。

**验证结果**：
- 构建通过。MSI 已重新构建，待用户验证。

---

## 2026-05-22（修复 MSI 安装后静默崩溃和缺少桌面快捷方式）

**触发原因**：用户从 GitHub Release 下载 v1.0.0 MSI 安装后，发现两个问题：(1) 安装后没有桌面快捷方式，只有开始菜单有；(2) 从开始菜单双击图标后无任何显示，任务管理器也没有进程。

**修改内容**：
1. `src/ChromeIsolator.App/App.xaml.cs` — 新增全局异常处理器：`App` 构造函数中订阅 `DispatcherUnhandledException` 和 `AppDomain.CurrentDomain.UnhandledException`；`OnStartup` 包裹 try-catch；启动失败时弹出 `MessageBox` 显示错误信息并优雅退出，而非静默崩溃。
2. `installer/Product.wxs` — 新增 `DesktopFolder` 目录和桌面快捷方式组件 `DesktopShortcut`，安装后在桌面创建"浏览器多开"快捷方式。
3. `src/ChromeIsolator.App/ViewModels/MainViewModel.cs` — 修复构造函数中 `SelectedProfile = Profiles.FirstOrDefault()` 放在命令初始化之前导致的 `NullReferenceException`：setter 触发 `RaiseCommandState()` 时命令属性还是 null。将 `SelectedProfile` 赋值移到所有命令初始化之后。
4. `src/ChromeIsolator.App/ChromeIsolator.App.csproj` — 将图标从 `Content` 改为 `EmbeddedResource`，解决 MSI 安装后 WPF pack URI 找不到图标资源的问题。
5. `src/ChromeIsolator.App/MainWindow.xaml`、`DownloadWindow.xaml`、`SettingsWindow.xaml` — 移除 XAML 中的 `Icon="Assets/AppIcon.ico"` 引用。
6. `src/ChromeIsolator.App/Services/IconHelper.cs` — 新增图标辅助类，从嵌入资源加载 `AppIcon.ico` 并应用到窗口。
7. `src/ChromeIsolator.App/MainWindow.xaml.cs`、`DownloadWindow.xaml.cs`、`SettingsWindow.xaml.cs` — 在构造函数中调用 `IconHelper.ApplyIcon(this)`。
8. `src/ChromeIsolator.App/MainWindow.xaml` — 将 `FallbackValue={DynamicResource SelectProfile}` 改为绑定 ViewModel 属性 `SelectedProfileTitle`，解决 `XamlParseException`（`FallbackValue` 不支持 `DynamicResource`）。

**遇到的问题**：
- 原应用没有任何全局异常处理，`OnStartup` 中任何未捕获异常都会导致 WPF 进程静默退出，用户看不到任何错误信息。
- WiX 安装包只定义了 Start Menu 快捷方式，缺少桌面快捷方式。
- 加了全局异常处理后发现真正的崩溃原因：`MainViewModel` 构造函数中 `SelectedProfile` 赋值过早，触发 `RaiseCommandState()` 时命令未初始化。

**解决方式**：
- 在 `App` 构造函数中订阅 WPF 和 AppDomain 级别的未处理异常事件，统一调用 `ShowFatalError` 弹出错误对话框。
- 在 `Product.wxs` 中新增 `DesktopFolder` 标准目录引用和桌面快捷方式组件。
- 将 `SelectedProfile = Profiles.FirstOrDefault()` 移到命令初始化之后。

**验证方式**：
- 运行 `dotnet build ChromeIsolator.sln`，0 警告 0 错误。
- 构建 MSI 并安装测试。

**验证结果**：
- 构建通过。MSI 已重新构建，待用户验证安装后启动。

**本地产物清理**：
- `artifacts/` 目录保留待用户测试后清理。

---

## 2026-05-21（BrowserIsolator 功能对齐）

**触发原因**：用户要求将功能设计文档中所有未实现的功能全部完成。

**修改内容**：
1. `ViewModels/ProfileViewModel.cs` — 新增 `IsStarting`、`IsStopping` 状态属性（橙色指示器）；实现相对日期格式化（今天/昨天/N天前）；新增 `RefreshDiskSizeAsync()` 异步磁盘计算；新增 `DiskSizeRaw` 属性用于删除确认。
2. `ViewModels/MainViewModel.cs` — `StartProfile`/`StopProfile` 使用 starting/stopping 状态；`AddProfile` 自动弹出重命名对话框；`DeleteSelected` 显示数据大小；新增 `OpenProfileFolderCommand`、`CopyProfilePathCommand`、`StopAllAndQuitAsync()`；磁盘大小改为异步刷新。
3. `Services/TrayService.cs` — 新增"全部关闭并退出"菜单项，退出前等待 Chrome 进程结束。
4. `MainWindow.xaml` — 新增环境列表右键上下文菜单（重命名/删除）；新增详情区"打开环境目录"和"复制环境路径"按钮。
5. `Resources/Strings.*.xaml`（7 语言）— 新增 `StatusStarting`、`StatusStopping`、`DateJustNow`、`DateMinutesAgo`、`DateToday`、`DateYesterday`、`DateDaysAgo`、`TrayStopAllAndQuit`；更新 `MsgDeleteConfirm` 包含数据大小。
6. `05-current-status.md`、`06-dev-log.md` — 更新文档。

**验证方式**：
- 运行 `dotnet build src/ChromeIsolator.App/ChromeIsolator.App.csproj`，0 警告 0 错误。

**验证结果**：
- 构建通过。

---

## 2026-05-21（设置页重装、下载重试与错误处理）

**触发原因**：用户要求提交 GitHub 后继续开发。

**修改内容**：
1. `SettingsWindow.xaml` — 新增"重新安装 Chrome"按钮。
2. `ViewModels/SettingsViewModel.cs` — 新增 `ReinstallChromeCommand`，接受回调打开 DownloadWindow。
3. `ViewModels/MainViewModel.cs` — 新增 `ClearErrorCommand`、`RetrySelectedCommand`；`OpenSettings` 传入重装回调；`RaiseCommandState` 扩展。
4. `DownloadWindow.xaml` — 新增"重试"和"复制错误详情"按钮（默认隐藏，失败后显示）。
5. `DownloadWindow.xaml.cs` — 新增 `StartDownloadAsync()` 提取下载逻辑支持重试；新增 `RetryButton_Click`、`CopyErrorButton_Click`；错误时显示重试和复制按钮。
6. `MainWindow.xaml` — 错误行新增"重试"和"清除错误"按钮。
7. `Resources/Strings.*.xaml`（7 语言）— 新增 `BtnReinstallChrome`、`BtnRetry`、`BtnCopyError`、`BtnClearError`。
8. `05-current-status.md`、`06-dev-log.md` — 更新文档。

**遇到的问题**：
- `Clipboard.SetText` 在同时引用 WPF 和 WinForms 时不明确。

**解决方式**：
- 使用 `System.Windows.Clipboard.SetText` 全限定。

**验证方式**：
- 运行 `dotnet build src/ChromeIsolator.App/ChromeIsolator.App.csproj`，0 警告 0 错误。

**验证结果**：
- 构建通过。

---

## 2026-05-21（CDP 重连与下载页优化）

**触发原因**：用户要求阅读项目文件和 project-log 文档后继续开发。

**修改内容**：
1. `Services/FingerprintInjector.cs` — 新增 CDP WebSocket 断线重连机制：最多 5 次重试，指数退避（1s/2s/4s/8s/16s），重连后清除已注入目标并重新注入所有页面；提取 `CleanupWebSocketAsync()` 统一 WebSocket 清理逻辑。
2. `DownloadWindow.xaml` — 新增"使用已安装的 Chrome"按钮，支持跳过下载直接使用系统 Chrome。
3. `DownloadWindow.xaml.cs` — 新增 `UseInstalled` 属性和按钮点击处理。
4. `ViewModels/MainViewModel.cs` — `PrepareChrome()` 处理 `UseInstalled` 结果，若系统 Chrome 不可用则提示。
5. `Resources/Strings.xaml`、`Strings.en.xaml`、`Strings.ja.xaml`、`Strings.ko.xaml`、`Strings.de.xaml`、`Strings.fr.xaml`、`Strings.ru.xaml` — 新增 `BtnUseInstalledChrome` 多语言字符串。
6. `05-current-status.md` — 更新已完成项，移除已实现的中优先级待办。

**遇到的问题**：
- `CleanupWebSocket` 同步调用 `CloseAsync.GetAwaiter().GetResult()` 在异步上下文中可能死锁。

**解决方式**：
- 将 `CleanupWebSocket` 改为 `async Task CleanupWebSocketAsync()`，使用 `ConfigureAwait(false)` 避免死锁。

**验证方式**：
- 运行 `dotnet build src/ChromeIsolator.App/ChromeIsolator.App.csproj`，0 警告 0 错误。

**验证结果**：
- 构建通过。

---

## 2026-05-21（用户体验完善、高级详情与多语言）

**触发原因**：用户要求按优先顺序继续开发，完成一项更新一次进度文档。

**修改内容**：
1. `DownloadWindow.xaml`、`DownloadWindow.xaml.cs` — 新增 Chrome 下载进度窗口，支持进度百分比、状态文字、取消。
2. `Services/ChromeManager.cs` — `PrepareChromeAsync` 改为 `IProgress<(double, string)>`，下载 0-80%、安装 80-90%、验证 90-100%。
3. `ViewModels/MainViewModel.cs` — 新增 `ShowDownloadIfNeeded()`、`CheckForUpdatesFromTrayAsync()`、`ShowAdvancedDetails`、`ChromeVersionText`、`ChromePathText`、`CpuCoresText`、`MemoryText`、`SortProfiles()`、`RefreshDiskSizes()`；接入 `L10n.LanguageChanged` 刷新本地化属性。
4. `ViewModels/ProfileViewModel.cs` — 新增 `DiskSizeBytes`、`DiskSizeText`、`RefreshDiskSize()`、`RefreshLocalizedProperties()`，Subtitle 组合最近使用与磁盘占用。
5. `MainWindow.xaml` — 新增空状态提示、高级信息区（Chrome 版本/路径、CPU、内存），所有字符串改为 `{DynamicResource}` 绑定。
6. `Models/AppConfig.cs` — 新增 `ShowAdvancedDetails` 和 `Language` 配置项。
7. `Services/TrayService.cs` — 托盘退出前检查运行中环境数量并提示确认；新增"检查更新"菜单项；所有字符串改为 `L10n.GetString()`。
8. `SettingsWindow.xaml` — 新增"界面"分组（高级详情 CheckBox）、"语言"分组（ComboBox 语言切换器）；所有字符串改为 `{DynamicResource}`。
9. `ViewModels/SettingsViewModel.cs` — 新增 `ProfileManager` 参数、`ShowAdvancedDetails`、`Languages`、`SelectedLanguage` 属性；语言切换时调用 `L10n.SetLanguage()` 并持久化。
10. `App.xaml.cs` — 启动后调用 `ShowDownloadIfNeeded()` 和 `L10n.Initialize()`。
11. `MainWindow.xaml.cs` — 新增双击环境列表启动 Profile。
12. `Services/L10n.cs` — 新增静态本地化服务，管理 XAML ResourceDictionary 加载/切换、`GetString()`、`Format()`、`LanguageChanged` 事件。
13. `Resources/Strings.xaml`（zh-CN）、`Strings.en.xaml`、`Strings.ja.xaml`、`Strings.ko.xaml`、`Strings.de.xaml`、`Strings.fr.xaml`、`Strings.ru.xaml` — 新增 7 语言资源字典，覆盖所有 UI 字符串。

**遇到的问题**：
- `ObservableCollection` 不支持原地排序，需要用 Clear + 重新添加。
- SettingsViewModel 和 MainViewModel 都需要访问 ShowAdvancedDetails，通过共享 ProfileManager.Config 实现同步。
- WPF XAML 不支持 `<string>` 标签，需用 `xmlns:sys` + `<sys:String>`。
- `Localization` 类名与 `System.Windows.Localization` 冲突，重命名为 `L10n`。
- L10n.cs 中 `Application` 在同时引用 WPF 和 WinForms 时有歧义，需用 `System.Windows.Application` 全限定。

**解决方式**：
- 使用 `Clear()` + 逐个 `Add()` 实现排序，配合 `SelectedProfile` 恢复。
- SettingsWindow 关闭后 MainViewModel 从 Config 重新读取 ShowAdvancedDetails。
- 资源字典使用 `sys:String` 并声明 `xmlns:sys` 命名空间。
- 类名改为 `L10n` 避免冲突。
- 全限定 `System.Windows.Application.Current`。

**验证方式**：
- 运行 `dotnet build src/ChromeIsolator.App/ChromeIsolator.App.csproj`，0 警告 0 错误。

**验证结果**：
- 构建通过。

---

## 2026-05-21（设置页、MSI 构建与 CI）

**触发原因**：用户询问还差什么并要求继续开发。

**修改内容**：
1. `Services/ShellService.cs` — 新增打开文件夹、打开 URL、复制文本能力。
2. `Services/UpdateService.cs`、`UpdateCheckResult.cs` — 新增 GitHub Releases 更新检查。
3. `ViewModels/SettingsViewModel.cs` — 新增设置页 ViewModel。
4. `SettingsWindow.xaml`、`SettingsWindow.xaml.cs` — 新增设置窗口。
5. `MainWindow.xaml`、`MainViewModel.cs`、`App.xaml.cs` — 接入设置按钮和设置窗口。
6. `installer/Product.wxs` — 新增 WiX MSI 安装定义。
7. `scripts/build-msi.ps1` — 新增发布并构建 MSI 的脚本。
8. `README.md` — 补充 MSI 构建说明和未签名提示。
9. `.github/workflows/build.yml` — 新增 GitHub Actions Windows 构建工作流。

**遇到的问题**：
- WiX v7 需要接受 OSMF EULA；脚本中 `--acceptEula` 必须传入 `wix7`。
- Windows PowerShell 里的 `[IO.Path]::GetRelativePath` 不可用。

**解决方式**：
- 构建脚本使用 `wix build --acceptEula wix7`。
- 改用 `System.Uri.MakeRelativeUri` 生成相对路径。

**验证方式**：
- 运行 `dotnet build ChromeIsolator.sln`。
- 启动应用做冒烟测试。
- 运行 `scripts/build-msi.ps1` 生成 MSI。
- 运行 `git diff --check`。
- 分三次提交并推送 GitHub：
  - `feat: add settings and update check`
  - `chore: add msi installer build`
  - `ci: add windows build workflow`

**验证结果**：
- 构建通过，0 警告，0 错误。
- 应用启动冒烟通过。
- MSI 生成成功：`ChromeIsolator-Setup-x64.msi`。
- GitHub 已推送最新提交。

**本地产物清理**：
- 本轮生成了 `artifacts/`、`bin/`、`obj/`，已在本轮结束前清理。

---

## 2026-05-21（SDK 编译闭环与 CDP 注入）

**触发原因**：用户安装好开发环境后要求继续开发。

**修改内容**：
1. `src/ChromeIsolator.App/GlobalUsings.cs` — 新增全局 `System.IO` using。
2. `App.xaml.cs`、`ProfileViewModel.cs`、`SimpleInputDialog.cs`、`MainViewModel.cs` — 修复 WPF 与 WinForms 类型命名冲突。
3. `Services/FingerprintInjector.cs` — 新增 Chrome DevTools Protocol 注入服务，支持 browser-level WebSocket、page target attach、`hardwareConcurrency` / `deviceMemory` 注入。
4. `Services/ChromeManager.cs` — 接入 CDP 注入生命周期，补 Chrome 路径发现、版本读取和官方 Enterprise MSI 准备入口。
5. `Services/ChromeInfo.cs` — 新增 Chrome 来源和版本信息模型。
6. `MainWindow.xaml`、`MainViewModel.cs` — 增加 Chrome 状态显示和“准备 Chrome”命令。
7. `README.md`、`scripts/publish-win-x64.ps1` — 增加构建、运行和 win-x64 发布说明 / 脚本。

**遇到的问题**：
- 初次构建出现 WPF 与 WinForms 的 `Application`、`Button`、`TextBox`、`MessageBox`、`Brush` 等类型冲突。
- CDP WebSocket 接收循环中不能同步等待注入命令响应，否则响应无人读取。

**解决方式**：
- 使用显式 WPF 类型别名消除冲突。
- 将 CDP event 触发的注入放入后台任务，保持接收循环继续读取响应。

**验证方式**：
- 运行 `dotnet build ChromeIsolator.sln`。
- 启动应用做冒烟测试，确认进程存活且响应。
- 运行 `scripts/publish-win-x64.ps1` 验证发布目录可生成。
- 运行 `git diff --check` 检查空白问题。
- 分三次提交并推送 GitHub：
  - `feat: add cdp fingerprint injection`
  - `feat: add chrome discovery and prepare action`
  - `chore: add windows publish script`

**验证结果**：
- `dotnet build ChromeIsolator.sln` 通过，0 警告，0 错误。
- 应用启动冒烟通过。
- win-x64 发布脚本执行成功。

**本地产物清理**：
- 本轮生成了 `artifacts/`、`bin/`、`obj/`，已在本轮结束前清理。

---

## 2026-05-21（记录未签名发布策略）

**触发原因**：用户询问 Windows 安装包是否会有签名问题，并确认“有提示没关系，能用就行”。

**修改内容**：
1. `07-deployment.md` — 增加代码签名策略，明确早期 Release 可以未签名，签名作为后续发布质量目标。
2. `10-planning-log.md` — 新增 ADR-007，记录早期 Release 代码签名策略。
3. `12-design-decisions.md` — 新增长期决策：早期 Windows 安装包允许未签名发布。
4. `05-current-status.md` — 更新当前状态、低优先级待办和已接受风险。
5. `06-dev-log.md` — 记录本轮文档更新。

**遇到的问题**：
- 未签名安装包可能触发 SmartScreen、下载警告或“未知发布者”提示。

**解决方式**：
- 将未签名发布作为 MVP 可接受策略，后续 README 安装说明补充安全提示。
- 保留后续代码签名规划，但不让证书采购阻塞功能开发。

**验证方式**：
- 复核部署、ADR、设计决策和当前状态文档是否写入一致结论。
- 运行 `git status --short --ignored` 检查仓库状态。

**验证结果**：
- 通过。相关策略已写入本地 project-log。
- 本轮没有源码改动；`project-log/` 被 `.gitignore` 忽略，不进入 Git。

**本地产物清理**：
- 无。本轮未生成构建、测试、截图或临时产物。

---

## 2026-05-21（README 首次发布与 WPF 工程骨架）

**触发原因**：用户要求开始开发，先写新项目 README 并提交 GitHub 一份，然后开始开发代码。

**修改内容**：
1. `README.md` — 新增面向用户的项目说明、主要功能、系统要求、Chrome 来源说明、数据位置、功能边界和许可证说明。
2. `LICENSE` — 新增 Apache License 2.0。
3. `NOTICE` — 新增项目版权、Google Chrome 许可边界和 MiSans 图标说明。
4. `ChromeIsolator.sln` — 新增 Visual Studio 解决方案。
5. `src/ChromeIsolator.App/ChromeIsolator.App.csproj` — 新增 WPF / .NET 8 Windows 桌面应用项目。
6. `src/ChromeIsolator.App/App.xaml`、`App.xaml.cs` — 新增应用入口，手动注入服务和主窗口。
7. `src/ChromeIsolator.App/MainWindow.xaml`、`MainWindow.xaml.cs` — 新增第一版主窗口、环境列表、详情栏和关闭到托盘行为。
8. `src/ChromeIsolator.App/Models/` — 新增 `Profile`、`AppConfig`。
9. `src/ChromeIsolator.App/Services/` — 新增路径、配置存储、Profile 管理、端口分配、基础 Chrome 启停和托盘服务。
10. `src/ChromeIsolator.App/ViewModels/` — 新增 ObservableObject、RelayCommand、ProfileViewModel、MainViewModel 和简单输入弹窗。
11. `.editorconfig`、`.gitattributes` — 新增基础格式和行尾规则。

**遇到的问题**：
- 当前机器只有 .NET Runtime，没有 .NET SDK，`dotnet build ChromeIsolator.sln` 无法执行。
- WPF `StartupUri` 会要求无参窗口构造；当前主窗口需要注入 ViewModel。

**解决方式**：
- 先手写工程文件和源码骨架，等待安装 SDK 后做编译验证。
- 移除 `StartupUri`，在 `App.OnStartup` 中手动创建服务、ViewModel 和主窗口。

**验证方式**：
- 执行 `git status --short --ignored` 确认 `project-log/` 仍被忽略。
- 执行 `git diff --check` 检查空白问题。
- 执行 `dotnet build ChromeIsolator.sln` 验证构建环境。
- 分两次提交并推送 GitHub：
  - `docs: initialize project readme`
  - `feat: scaffold windows app`

**验证结果**：
- `git diff --check` 通过。
- GitHub 推送成功。
- `dotnet build` 未通过，原因是本机未安装 .NET SDK：`No .NET SDKs were found.`

**本地产物清理**：
- 无。本轮未生成构建、测试、截图或临时产物。

---

## 2026-05-21（忽略本地规划目录并创建应用图标）

**触发原因**：用户要求将 `project-log`、`.claude`、`claude.md` 以及常见 IDE / AI 工具可能创建的文件和文件夹加入 Git 忽略；同时将原 BrowserIsolator 蓝底 `M` 图标改为 ChromeIsolator 的 `C` 图标，并保持整体风格。

**修改内容**：
1. `.gitignore` — 新增项目规划目录、常见 OS 文件、Visual Studio / .NET、JetBrains、VS Code、Claude、Cursor、Windsurf、Continue、Aider、Codex、Roo、Kilo Code、日志临时文件和安装发布产物的忽略规则。
2. `assets/icons/AppIcon.svg` — 新增可编辑 SVG 源图标，保持蓝色圆角底和白色粗体 `C`。
3. `assets/icons/AppIcon.png` — 新增 1024x1024 PNG 预览图标。
4. `assets/icons/AppIcon.ico` — 新增 Windows 可用多尺寸 ICO 图标。
5. `05-current-status.md` — 更新当前状态和相关文件。
6. `06-dev-log.md` — 记录本轮改动。

**遇到的问题**：
- 原图标是 macOS `.icns`，Windows 工程需要 `.ico`。
- `project-log/` 本轮按用户要求加入 `.gitignore` 后，后续规划文档会作为本地协作知识库保留，不进入 Git。

**解决方式**：
- 使用原 `AppIcon.icns` 作为视觉参考，生成同风格的 `C` 版 SVG / PNG / ICO。
- 将 `project-log/` 和常见本地工具目录加入 `.gitignore`，避免进入仓库。

**验证方式**：
- 读取原项目 `D:\projects\BrowserIsolator\AppIcon.icns` 并导出预览确认风格。
- 打开新生成的 `assets/icons/AppIcon.png` 进行视觉检查。
- 运行 `git status --short` 检查忽略规则生效。

**验证结果**：
- 通过。`project-log/` 已被忽略，Git 状态只显示 `.gitignore` 和 `assets/`。
- 通过。图标为蓝色圆角方块 + 白色粗体 `C`，与原图标风格一致。

**本地产物清理**：
- 原图标预览临时文件位于系统临时目录，不属于项目产物；项目目录内无需要清理的构建或测试产物。

**后续补充**：
- 2026-05-21：用户提醒字体商用授权问题后，确认本机安装 MiSans，并将 `AppIcon.svg` 的首选字体改为 MiSans。随后用 `MiSans-Heavy.otf` 重新生成 `AppIcon.png` 和 `AppIcon.ico`。MiSans 官方页面标注为全球免费商用字体，授权边界比使用系统 Arial 更清楚。

---

## 2026-05-21（初始化 Windows 版规划）

**触发原因**：用户希望将已使用良好的 macOS 项目 BrowserIsolator 从用户使用角度一模一样复刻为 Windows 10/11 x86-64 版本，项目命名为 ChromeIsolator，并要求先完成项目规划。

**修改内容**：
1. `00-project-overview.md` — 写入项目名称、背景、目标用户、核心功能、核心概念、技术栈、边界和约束。
2. `01-function-design.md` — 按 Profile 管理、Chrome 管理、CDP 注入、主窗口、系统托盘、设置、本地化等模块拆解功能。
3. `04-project-architecture.md` — 写入 Windows WPF 架构、计划目录结构、运行时数据目录和关键技术决策。
4. `05-current-status.md` — 更新当前阶段、已完成事项、待办、风险和任务交接。
5. `07-deployment.md` — 规划 MSI 安装、Program Files 安装目录、LocalAppData 用户数据目录和回滚原则。
6. `08-env-config.md` — 记录 Windows、.NET、WiX、Chrome Stable 等环境要求和本地数据位置。
7. `09-external-api-reference.md` — 记录 Google Chrome Stable 下载源、Chrome DevTools Protocol、GitHub Releases API。
8. `10-planning-log.md` — 新增 ADR-001 到 ADR-006，记录技术栈、安装目录、Chrome 来源、指纹边界、安装包和托盘行为。
9. `12-design-decisions.md` — 沉淀产品边界、Chrome 来源和 Windows 托盘体验三项长期设计决策。

**遇到的问题**：
- 当时尚未确定 Windows 官方 Stable Chrome 安装包是否能像 macOS `.app` 一样私有化安装 / 提取。
- 当时需要在“不使用 Chrome for Testing”和“不影响用户现有 Chrome”之间找到稳定实现方式。

**解决方式**：
- 在规划中明确 Chrome 来源只接受官方 Stable。
- 当时将“优先私有 Chrome；不可行时受控使用官方 Stable 程序文件 + 独立 `--user-data-dir`”记录为临时方向。
- 2026-05-22 已收敛为最终方案：不做私有 Chrome 副本；优先复用官方 Stable Chrome 程序文件，Chrome 缺失时用户确认安装官方 Chrome，Edge Stable 仅作用户确认备用引擎。

**验证方式**：
- 阅读原项目 `D:\projects\BrowserIsolator` 的 README、`App.swift`、`BrowserManager.swift`、`FingerprintInjector.swift`、`Models.swift`、`Localization.swift`。
- 对照用户确认的 8 项需求逐项写入 project-log。
- 检查更新后的文档是否覆盖产品边界、功能模块、架构、部署、外部 API、ADR 和交接状态。

**验证结果**：
- 通过。规划文档已覆盖当前已确认需求。
- 未运行代码测试，原因：本轮只更新文档，尚未创建代码工程。

**本地产物清理**：
- 无。本轮未生成构建、测试、截图或临时产物。

---

<!-- 新记录追加在上方分隔线之后、旧记录之前 -->

## 2026-05-22（1.0.0 版本管理和 self-contained 发布）

**触发原因**：用户要求统一版本管理，后续打版本 tag 自动触发构建；当前版本整体推进到 1.0.0；构建产物文件名带版本号；普通用户在没有 .NET Runtime 的干净 Windows 上也能安装运行。

**修改内容**：
1. `Directory.Build.props` — 新增统一版本源，当前版本 `1.0.0`，同时设置 Assembly/File/Informational 版本。
2. `UpdateService.cs` — 应用内当前版本读取 `AssemblyInformationalVersion`，避免默认程序集版本和 MSI 版本不一致。
3. `scripts/publish-win-x64.ps1` — 改为 self-contained 发布，生成 `ChromeIsolator-win-x64-v{version}.zip`。
4. `scripts/build-msi.ps1` — 从统一版本源读取版本，生成 `ChromeIsolator-Setup-x64-v{version}.msi`。
5. `.github/workflows/build.yml` — tag 构建校验 tag 版本和 `Directory.Build.props` 一致；tag 构建成功后自动创建 GitHub Release 并上传 zip / MSI。
6. `README.md`、`07-deployment.md`、`05-current-status.md` — 补充版本管理、self-contained 和产物命名规范。

**验证方式**：
- `dotnet build ChromeIsolator.sln -c Release`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1`
- 检查 `ChromeIsolator.exe` 文件属性版本。
- 多语言资源 XML 解析和 key 完整性检查。
- `git diff --check`

**验证结果**：
- 通过。编译 0 警告、0 错误。
- 通过。self-contained zip 生成于 `artifacts\publish\ChromeIsolator-win-x64-v1.0.0.zip`。
- 通过。MSI 生成于 `artifacts\installer\ChromeIsolator-Setup-x64-v1.0.0.msi`。
- 通过。`ChromeIsolator.exe` ProductVersion 为 `1.0.0`，FileVersion 为 `1.0.0.0`。
- 通过。7 个资源文件均为 111 个 key，无缺失。
- 通过。`git diff --check` 无错误。

**本地产物清理**：
- 已清理 `artifacts/`、`src/ChromeIsolator.App/bin/`、`src/ChromeIsolator.App/obj/`。

---

## 2026-05-22（Chrome 下载失败与 Edge 最后备用交互）

**触发原因**：用户确认 Edge 只能作为最后备用，不推荐优先使用；Chrome 自动下载在中国大陆网络下可能失败，需要优先提供 Google 官方下载页而不是第三方下载站。

**修改内容**：
1. `12-design-decisions.md` — 补充浏览器引擎交互决策：Chrome 官方安装 / 官方下载页是主路径，Edge 是最后备用，不与 Chrome 并列展示。
2. `DownloadWindow.xaml(.cs)` — 调整浏览器引擎设置窗口层级：主按钮保留安装官方 Chrome、打开官方 Chrome 下载页、重新检测；Edge 放到底部备用区。
3. `DownloadWindow.xaml.cs` — 新增 Google 官方 Chrome 下载页入口 `https://www.google.cn/chrome/`；下载失败后显示官方下载页按钮和 Edge 备用区。
4. `DownloadWindow.xaml.cs` — 用户点击 Edge 备用前增加二次确认，明确不推荐优先使用。
5. `Resources/Strings*.xaml` — 更新 7 语言文案，明确 Edge 是最后备用，并新增官方下载页按钮和确认提示。

**验证方式**：
- `dotnet build ChromeIsolator.sln`
- `powershell -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1`
- 多语言资源 XML 解析和 key 完整性检查。

**验证结果**：
- 通过。编译 0 警告、0 错误。
- 通过。MSI 生成于 `artifacts\installer\ChromeIsolator-Setup-x64.msi`。
- 通过。7 个资源文件均为 111 个 key，无缺失。

**本地产物清理**：
- 已清理 `artifacts/`、`src/ChromeIsolator.App/bin/`、`src/ChromeIsolator.App/obj/`。

---

## 2026-05-22（用户视角交互收口）

**触发原因**：用户要求针对 11 条页面、交互、逻辑优化建议逐条复核，确认问题后先补文档再修复代码。

**修改内容**：
1. `11-code-review-log.md` — 新增第三轮用户视角评审，记录 11 项问题、修复计划和修复确认。
2. `MainWindow.xaml` — 右侧详情区改为顺序滚动布局，避免基础信息、路径操作和高级信息重叠；新增“启动环境 / 关闭环境”主操作按钮。
3. `MainWindow.xaml`、`MainWindow.xaml.cs` — 修复环境列表右键菜单命令绑定；双击环境改为启动 / 关闭切换。
4. `MainViewModel.cs` — 启动 / 关闭命令按状态启用；`StopAll()` 只更新时间给真实受影响环境；统一处理浏览器引擎设置窗口返回结果。
5. `DownloadWindow.xaml.cs` — 已检测到 Chrome 时按钮显示“开始使用”。
6. `TrayService.cs` — 托盘菜单移除重复的“全部关闭并退出”，保留“退出”并继续在运行环境存在时提示。
7. `Resources/Strings*.xaml` — 7 语言统一“浏览器引擎设置 / 浏览器引擎可用”文案，新增“启动环境 / 关闭环境 / 开始使用”。

**验证方式**：
- `dotnet build ChromeIsolator.sln`
- `powershell -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1`
- 多语言资源 XML 解析。
- 多语言资源 key 完整性检查。

**验证结果**：
- 通过。编译 0 警告、0 错误。
- 通过。MSI 生成于 `artifacts\installer\ChromeIsolator-Setup-x64.msi`。
- 通过。7 个资源文件均为 108 个 key，无缺失。

**本地产物清理**：
- 已清理 `artifacts/`、`src/ChromeIsolator.App/bin/`、`src/ChromeIsolator.App/obj/`。

---

## 2026-05-22（落实 Chrome 不干扰策略和 Edge 备用）

**触发原因**：用户明确要求 ChromeIsolator 不能影响用户原本 Chrome 的插件、设置、登录用户、启动体验或正常使用；没有 Chrome 的用户可安装 Chrome，但后续桌面 Chrome 也不能与隔离环境冲突。

**修改内容**：
1. `ChromeManager.cs` — Chrome 优先、Edge 备用；Edge 仅在配置允许时启用；启动隔离环境始终传入 `%LOCALAPPDATA%\ChromeIsolator\Profiles\pN` 作为独立 `--user-data-dir`。
2. `ChromeManager.cs` — 移除 `--test-type` 启动参数，保留必要的 `--remote-debugging-port` 和 `--no-first-run`。
3. `ChromeManager.cs` — 为进程、调试端口、指纹注入器集合增加锁保护，降低退出事件与 UI 操作并发风险。
4. `DownloadWindow.xaml(.cs)` — 首次 / 准备窗口不再自动下载和安装 Chrome；用户必须点击“安装官方 Chrome”并确认后才会运行官方安装包。
5. `DownloadWindow.xaml(.cs)` — Chrome 已存在时只展示隔离说明；Chrome 缺失时提供安装官方 Chrome、重新检测、使用 Edge 备用三种入口。
6. `DownloadWindow.xaml(.cs)` — 进入 `msiexec` 安装阶段后禁用取消和关闭窗口，避免用户误以为取消会终止已提升权限的系统安装。
7. `AppConfig.cs` — 新增 `FirstRunCompleted` 和 `AllowEdgeFallback` 配置项。
8. `MainViewModel.cs` — 保存首次运行完成状态和 Edge 备用选择。
9. `SettingsViewModel.cs` — “打开 Chrome 文件夹”改为打开当前实际浏览器引擎所在目录。
10. `README.md` — 更新浏览器引擎策略说明，明确共享官方 Chrome 程序文件、严格隔离 profile、不触碰默认 Chrome 用户数据。
11. `Resources/Strings*.xaml` — 补齐首次运行 / 浏览器引擎设置相关多语言文案。

**验证方式**：
- `dotnet build ChromeIsolator.sln`
- `powershell -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1`
- 解析 7 个多语言资源 XAML 文件。
- 对比 7 个资源文件的 key 数量和缺失项。

**验证结果**：
- 通过。编译 0 警告、0 错误。
- 通过。MSI 生成于 `artifacts\installer\ChromeIsolator-Setup-x64.msi`。
- 通过。7 个资源文件均为 105 个 key，无缺失。

**本地产物清理**：
- 本轮 `dotnet build` 生成的 `bin/` 和 `obj/` 后续提交前清理。

---
