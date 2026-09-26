# 数据约定

> 适用状态：启用。本项目不使用数据库，也没有数据库迁移。

## 当前依据与位置

- 配置模型：`src/ChromeIsolator.App/Models/AppConfig.cs`、`Profile.cs`。
- 读写入口：`Services/ConfigStore.cs`；业务协调：`Services/ProfileManager.cs`。
- 用户数据根目录：`%LOCALAPPDATA%\ChromeIsolator\`。
- 配置：`config.json`；最近一次替换备份：`config.json.bak`。
- 浏览器环境：`Profiles\pN\`；由浏览器管理 Cookie、LocalStorage、密码、扩展和缓存。
- Chrome 安装器缓存：`Chrome\`，不是私有浏览器副本。

## 实体地图

| 概念 | 现有实体 / 存储 | 职责与唯一性 |
| --- | --- | --- |
| 应用配置 | `AppConfig` / `config.json` | 每个 Windows 用户一份；保存环境列表和全局偏好 |
| 环境 | `Profile` + `Profiles\pN` | `Folder` 是不区分大小写的唯一身份；`pN` 中 N 为正整数 |
| 运行状态 | `ProfileViewModel` 与 `ChromeManager` 内存状态 | 不持久化；应用启动后根据受管进程重新建立 |

`Profile` 当前保存 `Folder`、`DisplayName`、`Note`、两个模式开关和 `LastUsed`。`AppConfig` 保存外观（`Appearance`: system / light / dark，缺省为 system）、语言、首次运行、Edge 备用、外部链接目标、窗口布局和高级详情开关。字段的最终事实以模型为准，不在本文复制完整序列化结构。

## 兼容、恢复与删除

- 保存使用同目录临时文件；已有主配置时用 `File.Replace` 原子替换并生成 `.bak`。
- 主配置不可读时尽力保存损坏诊断副本，再尝试备份；两者均不可读时优先按磁盘目录重建，没有目录才生成默认环境。提示区分三种恢复结果。
- 有效配置中的环境即使目录缺失也保留名称、备注与模式，并补建空目录；去重后补回磁盘上未配置的合法环境。补建不等于恢复丢失的登录数据。
- 新建环境使用专用工厂默认开启采集；JSON 缺字段及磁盘重建保持采集关闭。首次配置在补建目录前保存，避免重启误走磁盘恢复。
- 保存失败保留当前内存状态并提示，不触发应用致命退出；批量模式变更一次保存。
- 删除环境必须走 Windows 回收站；先持久化移除后的配置及外链目标，再移动目录；保存失败不移动，移动失败回滚配置并提示。
- 卸载默认保留整个用户数据目录；回滚前保留配置和 Profiles。
- `LastUsed` 使用本地 `DateTime` 并仅用于本机排序和显示；不要把它误作跨设备审计时间。

配置规模很小，当前 JSON 足够。只有出现复杂查询、审计历史或并发写入需求时才重评 SQLite；不得为单个新页面提前增加数据库。

## 语言兼容

界面仅支持中文（zh）、英文（en）、日文（ja）。旧偏好中的其他语言按受支持的系统语言回退，无匹配时使用中文；不迁移或删除环境数据，也不因语言回退重写配置。
