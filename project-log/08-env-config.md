# 环境配置

## 环境要求

| 项目 | 版本 | 说明 |
|------|------|------|
| Windows | Windows 10 / Windows 11 x86-64 | 目标运行环境 |
| .NET SDK | 待定，优先 LTS | 本地开发和构建 |
| WPF | 随 .NET | 桌面 UI |
| WiX Toolset | 待定 | MSI 安装包 |
| Google Chrome Stable | 最新官方 Stable | 浏览器运行时 |

## 环境变量

当前无必需环境变量。

| 变量名 | 说明 | 示例值 | 必填 |
|--------|------|--------|------|
| 不适用 | 当前桌面应用不依赖 `.env` 配置 | 不适用 | 否 |

## 敏感信息规则

- project-log 中只记录变量名、用途、假示例值和配置位置。
- `.env.example` 只放占位示例，不放真实密钥。
- 真实密钥只应存放在本地 `.env`、部署平台密钥管理、或团队认可的安全凭据系统中。
- 如果真实密钥曾被提交或写入文档，立即轮换密钥，并在 `06-dev-log.md` 记录处理方式。

## 第三方服务

| 服务 | 用途 | 配置方式 |
|------|------|----------|
| Google Chrome 下载源 | 用户确认后下载官方 Stable Chrome 安装包 | 无密钥，应用内固定官方 URL |
| Microsoft Edge Stable | Chrome 缺失时的备用 Chromium 引擎 | Windows 10/11 通常自带；必须用户明确选择后才使用 |
| GitHub Releases API | 检查 ChromeIsolator 更新 | 无密钥公开访问 |

## 本地开发配置

```powershell
# 1. 克隆项目
git clone <repo-url>

# 2. 进入项目
cd ChromeIsolator

# 3. 安装 .NET SDK 和 WiX Toolset
# 具体版本待工程创建后补充

# 4. 还原依赖
dotnet restore

# 5. 启动应用
dotnet run --project .\src\ChromeIsolator.App\
```

## 本地数据位置

开发和正式运行均使用：

```text
%LOCALAPPDATA%\ChromeIsolator\
```

后续如需开发环境与正式环境隔离，可以增加 Debug 构建专用目录，例如：

```text
%LOCALAPPDATA%\ChromeIsolator.Dev\
```

该隔离策略尚未采用，需实现前确认。

## 变更记录

| 日期 | 变更内容 | 原因 |
|------|----------|------|
| 2026-05-21 | 初始化 Windows 开发和运行环境配置 | 项目进入规划阶段 |
