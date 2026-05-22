# 数据库设计

> 本项目当前不使用数据库。ChromeIsolator 是本地 Windows 桌面应用，配置使用 JSON 文件保存，环境数据由 Chrome profile 目录保存。

## 数据库选型

| 项目 | 选择 | 说明 |
|------|------|------|
| 数据库类型 | 不适用 | 当前无数据库 |
| 版本 | 不适用 | 当前无数据库 |
| ORM / 驱动 | 不适用 | 当前无数据库 |

## 数据存储方式

| 数据 | 存储位置 | 说明 |
|------|----------|------|
| 应用配置 | `%LOCALAPPDATA%\ChromeIsolator\config.json` | 保存环境列表和自定义名称 |
| 环境数据 | `%LOCALAPPDATA%\ChromeIsolator\Profiles\pN\` | Chrome profile 数据，包括 Cookie、LocalStorage、扩展、密码、缓存等 |
| Chrome 安装包缓存 | `%LOCALAPPDATA%\ChromeIsolator\Chrome\` | 仅用于临时保存用户确认下载的官方 Chrome 安装包，不作为浏览器私有副本目录 |

## 设计决策

- 当前不引入 SQLite 或其他数据库，原因是配置数据规模很小，JSON 文件足够直观、易备份、易排查。
- Chrome profile 数据由 Chrome 自己管理，应用只负责创建目录、移动到回收站、计算大小和传入 `--user-data-dir`。
- 如果未来需要记录运行历史、审计日志、批量标签或复杂搜索，再重新评估是否引入 SQLite。

## 变更记录

| 日期 | 变更内容 | 原因 |
|------|----------|------|
| 2026-05-21 | 标注当前无数据库并说明本地文件存储 | ChromeIsolator 当前是纯本地桌面应用 |
