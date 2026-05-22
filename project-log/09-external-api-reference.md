# 外部服务 / API 参考

---

## 外部服务清单

| 服务 | 用途 | 官方文档 | 备注 |
|------|------|----------|------|
| Google Chrome 下载页 | 获取官方 Chrome Stable Windows 安装包来源 | https://www.google.com/chrome/ | 只使用 Stable，不使用 Testing / Beta / Dev / Canary |
| Chrome Enterprise Windows 部署文档 | 参考 Windows MSI 部署方式 | https://support.google.com/chrome/a/answer/9023663 | 当前仅作为官方 Stable 安装包来源参考，不做私有 Chrome 副本 |
| Chrome DevTools Protocol | 对 Chrome page target 注入脚本 | https://chromedevtools.github.io/devtools-protocol/ | 通过本地 remote debugging port 访问 |
| GitHub Releases API | 检查 ChromeIsolator 应用更新 | https://docs.github.com/en/rest/releases/releases#get-the-latest-release | 公开 API，无需密钥 |

---

## 服务一：Google Chrome Stable 下载源

### 基本信息

| 项目 | 内容 |
|------|------|
| 文档地址 | https://chromeenterprise.google/download/ |
| API 地址 | `https://dl.google.com/chrome/install/GoogleChromeStandaloneEnterprise64.msi` |
| 认证方式 | 无 |
| SDK / 包名 | 不适用 |
| 关键限制 | 必须使用官方 Stable Chrome；不得使用 Chrome for Testing 或第三方便携版 |

### 常用接口 / 模型

| 名称 | 说明 | 备注 |
|------|------|------|
| Chrome Enterprise Stable MSI | 官方 Chrome Stable Windows 安装包 | 仅在用户确认后下载并运行，属于系统 / 用户 Chrome 安装，不作为私有副本 |
| Google Chrome 中国下载页 | 官方 Chrome 下载页 | 自动下载失败时提供给用户手动安装，URL：`https://www.google.cn/chrome/` |

### 参考代码

```csharp
// 伪代码：只有用户明确确认后才下载并运行官方 Chrome 安装包
await chromeDownloader.DownloadStableInstallerAsync(destinationPath, progress);
await chromeInstaller.RunOfficialInstallerAsync(destinationPath);
```

### 已知问题 / 踩坑记录

| 日期 | 问题 | 解决方案 |
|------|------|------|
| 2026-05-22 | 浏览器方案已定：不做私有 Chrome 副本，不自动静默安装 Chrome | 优先复用官方 Stable Chrome 程序文件；Chrome 缺失时用户确认后安装官方 Chrome；Edge Stable 仅作为用户确认的备用引擎；所有环境强制使用独立 `--user-data-dir` |
| 2026-05-22 | 大陆网络下 `dl.google.com` 直链不保证始终可达 | 自动下载失败时只提供 Google 官方下载页；不内置第三方国内下载站；Edge 仅作为最后临时备用 |

---

## 服务二：Chrome DevTools Protocol

### 基本信息

| 项目 | 内容 |
|------|------|
| 文档地址 | https://chromedevtools.github.io/devtools-protocol/ |
| API 地址 | `http://127.0.0.1:{port}/json/version`、`http://127.0.0.1:{port}/json`、browser-level WebSocket |
| 认证方式 | 本机端口，无认证 |
| SDK / 包名 | 优先使用 .NET 原生 HTTP / WebSocket |
| 关键限制 | 只监听由 ChromeIsolator 启动的 Chrome 实例端口 |

### 常用接口 / 模型

| 名称 | 说明 | 备注 |
|------|------|------|
| `/json/version` | 获取 browser-level WebSocket 地址 | 启动后轮询等待就绪 |
| `/json` | 获取当前 targets | 用于启动时同步已有 page target |
| `Target.setDiscoverTargets` | 发现 target | browser-level WebSocket 命令 |
| `Target.setAutoAttach` | 自动附加新 target | browser-level WebSocket 命令 |
| `Page.addScriptToEvaluateOnNewDocument` | 新文档提前注入脚本 | 对 page session 执行 |
| `Runtime.evaluate` | 当前页面立即执行脚本 | 对 page session 执行 |

### 参考代码

```json
{
  "id": 1,
  "method": "Target.setAutoAttach",
  "params": {
    "autoAttach": true,
    "waitForDebuggerOnStart": false,
    "flatten": true
  }
}
```

### 已知问题 / 踩坑记录

| 日期 | 问题 | 解决方案 |
|------|------|------|
| 2026-05-21 | Chrome 刚启动时 CDP 端口可能尚未就绪 | 使用指数退避轮询 `/json/version` |
| 2026-05-21 | 新打开标签页需要持续注入 | 使用 browser-level WebSocket 的 Target auto attach |

---

## 服务三：GitHub Releases API

### 基本信息

| 项目 | 内容 |
|------|------|
| 文档地址 | https://docs.github.com/en/rest/releases/releases#get-the-latest-release |
| API 地址 | `https://api.github.com/repos/{owner}/ChromeIsolator/releases/latest` |
| 认证方式 | 无密钥公开访问 |
| SDK / 包名 | .NET 原生 HTTP |
| 关键限制 | 仓库 owner 和 Release 策略待项目发布前确认 |

### 常用接口 / 模型

| 名称 | 说明 | 备注 |
|------|------|------|
| Get latest release | 获取最新 Release tag | 对比当前版本号 |

### 参考代码

```csharp
// 伪代码
var latest = await releasesClient.GetLatestReleaseAsync("vivalucas", "ChromeIsolator");
```

### 已知问题 / 踩坑记录

| 日期 | 问题 | 解决方案 |
|------|------|------|
| 2026-05-21 | 仓库地址尚未确认 | 实现前确认 GitHub owner / repo |

---

## 变更记录

| 日期 | 变更内容 | 原因 |
|------|----------|------|
| 2026-05-21 | 初始化外部 API 与服务参考 | 明确 Chrome 下载、CDP 和更新检查依赖 |
