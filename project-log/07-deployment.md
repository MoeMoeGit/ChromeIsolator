# 部署

## 部署环境

ChromeIsolator 是 Windows 桌面应用，没有后端生产环境。

| 环境 | 用途 | 地址 |
|------|------|------|
| 开发 | 本地开发、调试、手动验证 | Windows 10/11 x86-64 开发机 |
| 预发布 | 安装包试装、升级、卸载验证 | 本地或测试用 Windows 10/11 x86-64 设备 |
| 生产 | GitHub Releases 发布安装包 | 待创建 Releases 页面 |

## 部署步骤

### 前置条件

- Windows 10 / Windows 11 x86-64。
- .NET SDK，版本待实现阶段确认，优先选择 LTS。仅开发 / 构建机器需要；用户安装包采用 self-contained 发布，不要求预装 .NET Runtime。
- WiX Toolset，版本待实现阶段确认。
- 可访问官方 Google Chrome 下载源。仅在用户确认安装 Chrome 时需要。
- 可访问 GitHub Releases 用于发布和更新检查。

### 部署流程

```powershell
# 1. 还原依赖
dotnet restore

# 2. 构建 Release
dotnet build -c Release

# 3. 发布桌面应用
dotnet publish -c Release

# 4. 构建 MSI 安装包
.\scripts\build-msi.ps1

# 5. 在干净 Windows 10/11 设备上试装
# 验证安装、首次启动、浏览器引擎设置、环境启动、托盘、卸载
```

### 安装位置

| 内容 | 路径 | 说明 |
|------|------|------|
| 主程序 | `%ProgramFiles%\ChromeIsolator` | 标准 Windows 应用安装目录 |
| 配置文件 | `%LOCALAPPDATA%\ChromeIsolator\config.json` | 每个 Windows 用户独立 |
| 环境数据 | `%LOCALAPPDATA%\ChromeIsolator\Profiles\` | 每个环境独立目录 |
| Chrome 安装包缓存 | `%LOCALAPPDATA%\ChromeIsolator\Chrome\` | 仅保存用户确认下载的官方 Chrome 安装包，可删除 |

### 版本管理规范

项目版本使用单一来源：

```text
Directory.Build.props
```

发布流程：

1. 更新 `Directory.Build.props` 中的 `Version`、`AssemblyVersion`、`FileVersion`、`InformationalVersion`。
2. 提交代码并推送到 `main`。
3. 创建并推送同版本 tag，例如 `v1.0.0`。
4. GitHub Actions 会校验 tag 版本必须与 `Directory.Build.props` 一致。
5. tag 构建成功后自动创建 GitHub Release，并上传：
   - `ChromeIsolator-win-x64-v{version}.zip`
   - `ChromeIsolator-Setup-x64-v{version}.msi`

版本约束：

- MSI `ProductVersion` 必须使用 `major.minor.patch` 数字格式，例如 `1.0.0`。
- Git tag 使用 `v` 前缀，例如 `v1.0.0`。
- 不在脚本、代码和文档中分别手写不同产品版本。
- `InformationalVersion` 不追加 Git commit hash，确保文件属性、应用内版本和 Release tag 保持清晰一致。

### 回滚方案

```powershell
# 1. 卸载当前版本 ChromeIsolator
# 2. 安装上一版 MSI
# 3. 保留 %LOCALAPPDATA%\ChromeIsolator 下的用户数据
# 4. 启动后验证 config.json 和 Profiles 是否可读取
```

回滚原则：

- 卸载应用不应默认删除用户 profile 数据。
- 若后续版本引入配置格式升级，必须在升级前备份 `config.json`。

### 升级安装注意事项

- 发布给用户的升级包必须使用更高版本号，例如从 `1.2.0` 升级到 `1.2.1`。
- 不建议用同版本 MSI 反复覆盖安装；同版本包更适合作为本地修复 / 维护，不作为正式升级路径。
- MSI 已加入运行中检测：如果 `ChromeIsolator.exe` 正在运行，会提示用户先从系统托盘退出“浏览器多开”，然后点击重试继续安装。
- 不在安装器中强制终止应用进程，避免绕过应用内 StopAll / OnExit 清理逻辑导致浏览器环境残留。
- 升级安装只替换 `%ProgramFiles%\ChromeIsolator` 下的程序文件，默认保留 `%LOCALAPPDATA%\ChromeIsolator` 下的配置和 profile 数据。

## CI/CD

暂不适用，原因：当前处于规划阶段，尚未创建代码工程和安装项目。

后续建议：

- GitHub Actions 构建 Windows Release。
- 生成 MSI 安装包并作为 Release Asset 上传。
- 在 CI 中运行单元测试。
- 安装包签名作为后续发布质量目标，不阻塞早期可用版本发布。

## 代码签名策略

当前采用阶段性策略：

- 早期 Release 可以发布未签名 MSI / EXE。
- 未签名安装包可能触发 Windows SmartScreen、浏览器下载警告或“未知发布者”提示。
- 用户确认“有提示没关系，能用就行”，因此签名不作为 MVP 阻塞项。
- README 安装说明后续需要补充 Windows 安全提示：如果出现 SmartScreen，点击“更多信息”后选择“仍要运行”。
- CI / 发布脚本后续可以预留签名步骤，但默认关闭。
- 正式期如需要提升安装信任度，再购买 OV / EV Code Signing Certificate，并使用 `signtool` 对主程序和安装包签名。

签名目标文件：

```text
ChromeIsolator.exe
ChromeIsolator-Setup-x64-v1.0.0.msi
```

示例命令，具体参数取决于证书和签名服务：

```powershell
signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /a ChromeIsolator.exe
signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /a ChromeIsolator-Setup-x64.msi
```

注意：

- 证书、私钥、token、云签名凭据不得写入仓库。
- 签名必须使用时间戳，避免证书过期后旧安装包签名失效。
- 未签名发布不影响应用功能，只影响下载和安装信任提示。

## 常用运维命令

桌面应用无服务端运维命令。开发和排查常用命令待工程创建后补充：

```powershell
# 构建
dotnet build -c Release

# 运行
dotnet run --project .\src\ChromeIsolator.App\

# 测试
dotnet test
```

## 变更记录

| 日期 | 变更内容 | 原因 |
|------|----------|------|
| 2026-05-21 | 初始化 Windows 安装与发布规划 | 明确 MSI、Program Files 和 LocalAppData 的职责 |
| 2026-05-21 | 增加代码签名策略 | 用户确认早期未签名提示可接受，签名不阻塞 MVP |
