# API 设计

> 本项目当前不提供后端 API。ChromeIsolator 是本地 Windows 桌面应用，所有核心功能在本机完成。

## API 概览

| 类型 | 是否提供 | 说明 |
|------|----------|------|
| 对外 HTTP API | 否 | 不作为服务端对外提供接口 |
| 后端业务 API | 否 | 无后端服务 |
| 本地 Chrome DevTools Protocol | 是，作为外部依赖调用 | 仅连接 ChromeIsolator 启动的 Chrome 本机调试端口 |
| GitHub Releases API | 是，作为外部依赖调用 | 用于检查应用更新 |

## 认证方式

不适用。项目不提供需要认证的对外 API。

## 本地外部接口调用

ChromeIsolator 会调用以下外部 / 本机接口，详细信息见 `09-external-api-reference.md`：

- Chrome DevTools Protocol：
  - `http://127.0.0.1:{port}/json/version`
  - `http://127.0.0.1:{port}/json`
  - browser-level WebSocket
- GitHub Releases API：
  - `https://api.github.com/repos/{owner}/ChromeIsolator/releases/latest`

## 设计决策

- 不开放本地 HTTP 控制 API，避免额外攻击面。
- CDP 调试端口只绑定本机回环地址，并且只用于应用自己启动的 Chrome 实例。
- 更新检查只读取公开 Release 信息，不上传本机数据。

## 变更记录

| 日期 | 变更内容 | 原因 |
|------|----------|------|
| 2026-05-21 | 标注当前无后端 API，并说明外部接口调用范围 | ChromeIsolator 当前是纯本地桌面应用 |
