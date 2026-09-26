# 部署、发行与恢复

> 适用状态：启用。项目没有服务端部署；发行物为 Windows x64 ZIP 和 MSI。

## 当前环境与触发关系

- 唯一版本来源：`Directory.Build.props`，当前 `1.7.11`。
- 最新已发布版本仍为 1.7.9；1.7.11 此次仅同步源码，不创建发布标签或 Release。
- `main` push 和 PR：GitHub Actions 执行 Release 构建并上传 workflow artifacts。
- `v*` tag：先校验 tag 与项目版本一致，再自动创建 GitHub Release 并上传 ZIP/MSI。
- 普通 push 不授权修改版本、创建 tag 或发布 Release；发布必须由用户明确要求。

## 本地构建

```powershell
dotnet restore ChromeIsolator.sln
dotnet build ChromeIsolator.sln -c Release
.\scripts\publish-win-x64.ps1
.\scripts\build-msi.ps1
```

产物：

- `artifacts\publish\ChromeIsolator-win-x64-v{version}.zip`
- `artifacts\installer\ChromeIsolator-Setup-x64-v{version}.msi`

MSI 固定安装到 `%ProgramFiles%\ChromeIsolator`，创建开始菜单和桌面快捷方式。安装、升级或卸载发现应用运行时，应提示用户从托盘退出，不强制杀进程。完成页默认启动应用。卸载只移除程序文件和快捷方式，默认保留 `%LOCALAPPDATA%\ChromeIsolator`。

## 发布流程

1. 检查工作区、目标版本和 Release 说明，更新 `Directory.Build.props` 的四个版本字段。
2. 在 Windows 上完成 Release 构建、ZIP、MSI 和关键用户路径验证。
3. 提交并推送 `main`；创建同版本 `v{version}` tag 并推送。
4. 核对 Actions 成功、Release 版本和两个附件名称正确。
5. 在干净 Windows 设备验证安装、升级、首次启动、托盘退出和卸载保留数据。

早期发行物未签名，可能触发 SmartScreen；README 与 Release 说明必须保留来源确认指引。代码签名是后续质量增强，证书和令牌不得进入仓库。

## 回滚与恢复

1. 从托盘退出应用并确认受管浏览器已关闭。
2. 备份 `%LOCALAPPDATA%\ChromeIsolator`，尤其是 `config.json`、`.bak` 和 `Profiles`。
3. 卸载当前版本，安装上一版 MSI；卸载本身应保留用户数据。
4. 启动后确认配置、环境列表和 profile 可读。若新版本改变配置格式，必须先评估旧版本兼容性；当前没有自动降级迁移。

## 验证状态

| 平台 / 环境 | 最近已知结果 | 当前未覆盖 |
| --- | --- | --- |
| GitHub Actions `windows-latest` | 历史标签已生成 ZIP/MSI | main 推送会重新运行测试、构建与打包 |
| Windows 10/11 x64 | 历史上完成安装、升级与功能回归 | 01 中列出的默认浏览器和异常恢复路径 |
| macOS / Ubuntu | 非运行目标 | 不支持 WPF 应用验证 |
