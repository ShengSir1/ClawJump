# Claw Jump

[中文](#中文) | [English](#english)

---

## 中文

Claw Jump 是一个面向 Windows 的 Claude Code 桌面提醒工具。它会在桌面上显示一只可拖拽、可贴边隐藏的小爪子，并通过 Claude Code Hook 接收事件：当 Claude Code 停止输出、发出通知或需要用户关注时，小爪子会切换到提醒状态；当你提交新的提示词后，它会回到空闲状态。

### 功能特性

- 桌面小爪子提醒窗口，默认置顶显示
- 支持空闲、工作中、完成提醒、审批提示、异常离线等多状态图片切换
- 支持拖拽移动，靠近左侧、右侧或顶部边缘时自动贴边隐藏
- 系统托盘菜单：显示/隐藏小爪子、测试提醒、标记已查看、检查更新、打开设置、查看日志等
- 本地回环 HTTP 服务接收 Claude Code Hook 事件
- 自动生成并写入 Claude Code Hook 配置
- Hook 健康检查：检查本地服务、Hook 脚本、Claude settings.json 配置状态
- 事件日志窗口，便于排查 Hook 触发情况
- 单实例运行，避免重复启动多个提醒窗口
- 可选：退出 Claw Jump 时自动清理写入的 Claude Hook 配置

### 系统要求

- Windows
- Claude Code
- .NET 10 SDK（从源码构建时需要）
- Inno Setup 6（仅在构建安装包时需要）

安装 Inno Setup 6：

```powershell
winget install -e --id JRSoftware.InnoSetup
```

### 快速开始

#### 使用安装包

运行发布的 `ClawJump-Avalonia-Setup-*.exe` 安装程序，然后启动 Claw Jump。

升级已有安装时，直接运行新版安装包并覆盖安装到原目录即可。用户配置、日志和生成的 Hook 文件保存在 `%APPDATA%\ClawJump`，不会随安装目录覆盖而删除；如新版调整了 Hook 行为，启动后建议检查 Hook 状态或重新执行“一键写入 Claude Hook 配置”。

启动后，Claw Jump 会：

1. 显示桌面小爪子
2. 启动本地服务，默认监听 `127.0.0.1:47653`
3. 自动生成 Hook 脚本
4. 自动将 Stop、Notification、UserPromptSubmit Hook 写入 `%USERPROFILE%\.claude\settings.json`

> 首次写入 Claude settings.json 时，如果原文件存在，会自动创建 `settings.json.bak-时间戳` 备份。

#### 从源码运行

```powershell
dotnet restore ClawJump.slnx
dotnet run --project src/ClawJump.Avalonia/ClawJump.Avalonia.csproj
```

### 使用说明

右键托盘图标可以打开菜单：

- **显示小爪子**：显示并激活桌面提醒窗口
- **隐藏小爪子**：隐藏桌面提醒窗口
- **测试发光提醒**：手动切换到提醒状态
- **标记已查看**：切换回空闲状态
- **检查 Hook 状态**：检查本地服务和 Claude Hook 配置是否正常
- **当前版本**：查看正在运行的 Claw Jump 版本号
- **检查更新**：从 GitHub Releases 检查新版本，发现更新时自动打开发布页面
- **打开设置**：修改监听端口、提醒行为、退出时是否清理 Hook 配置
- **打开配置目录**：打开 Claw Jump 配置和日志目录
- **打开 Claude 配置目录**：打开 `%USERPROFILE%\.claude`
- **生成 Claude Hook 脚本**：仅生成脚本和配置片段
- **一键写入 Claude Hook 配置**：将 Hook 配置合并到 Claude Code settings.json
- **查看事件日志**：查看已接收的 Hook 事件
- **打开日志目录**：打开日志文件所在目录
- **退出**：退出程序，并在启用选项时清理 Claude Hook 配置

### Hook 工作方式

Claw Jump 会在 `%APPDATA%\ClawJump` 下保存配置和生成文件：

- `config.json`：应用配置
- `hooks\claw-jump-hook.ps1`：Claude Code Hook 调用的 PowerShell 脚本
- `claude-settings-snippet.json`：可手动复制的 Claude settings.json 配置片段
- `logs\claw-jump.log`：事件日志

Hook 流程：

1. Claude Code 触发 Stop、Notification 或 UserPromptSubmit Hook
2. Hook 命令运行 `claw-jump-hook.ps1`
3. 脚本向 `http://127.0.0.1:47653/event` POST 事件 JSON
4. Claw Jump 接收事件并更新小爪子的状态

默认事件行为：

| Hook 事件 | 行为 |
| --- | --- |
| Stop | 显示完成/提醒状态 |
| Notification | 显示提醒状态；当通知内容表示 Claude Code 需要审批/授权时，显示审批提示状态 |
| UserPromptSubmit | 显示工作中状态 |

### 设置项

- **监听端口**：本地 HTTP 服务端口，默认 `47653`
- **收到 Hook 事件时自动显示小爪子**：隐藏窗口时收到事件会自动显示
- **收到 Hook 事件时显示托盘提示**：当前版本暂未启用
- **退出 Claw Jump 时清理 Claude Hook 配置**：退出时从 Claude settings.json 中移除 Claw Jump 写入的 Hook 命令

### 构建

恢复依赖：

```powershell
dotnet restore ClawJump.slnx
```

构建解决方案：

```powershell
dotnet build ClawJump.slnx
```

发布 Windows x64 自包含版本：

```powershell
powershell -ExecutionPolicy Bypass -File ./build-avalonia-release.ps1
```

构建安装包：

```powershell
powershell -ExecutionPolicy Bypass -File ./build-avalonia-installer.ps1
```

### 致谢

感谢 [arczhi/claw-jump](https://github.com/arczhi/claw-jump) 项目带来的灵感与基础创意。

### 许可证

本项目使用 MIT License。

---

## English

Claw Jump is a Windows desktop notifier for Claude Code. It shows a draggable, topmost desktop claw that can hide against screen edges. Through Claude Code hooks, it switches to a ready/attention state when Claude Code stops, sends a notification, or needs your attention, and returns to idle when you submit a new prompt.

### Features

- Desktop claw reminder window with topmost display
- State-specific images for idle, working, ready, approval-required, and offline/error states
- Drag-and-drop positioning with edge docking on the left, right, and top edges
- System tray menu for showing/hiding the claw, testing alerts, marking events as viewed, checking for updates, opening settings, and viewing logs
- Local loopback HTTP server for Claude Code hook events
- Automatic Claude Code hook script generation and settings merge
- Hook health checks for the local service, generated script, and Claude settings.json
- Event log window for troubleshooting hook delivery
- Single-instance app behavior
- Optional cleanup of Claw Jump hook entries from Claude settings.json on exit

### Requirements

- Windows
- Claude Code
- .NET 10 SDK, if building from source
- Inno Setup 6, only if building the installer

Install Inno Setup 6:

```powershell
winget install -e --id JRSoftware.InnoSetup
```

### Quick Start

#### Install from installer

Run the released `ClawJump-Avalonia-Setup-*.exe` installer, then start Claw Jump.

To upgrade an existing installation, run the newer installer and install over the original directory. User settings, logs, and generated hook files are stored under `%APPDATA%\ClawJump`, so they are not removed by overwriting the install directory. If a new version changes hook behavior, check Hook status after startup or run **Write Claude Hook config** again.

On startup, Claw Jump will:

1. Show the desktop claw
2. Start a local service on `127.0.0.1:47653` by default
3. Generate the hook script
4. Automatically write Stop, Notification, and UserPromptSubmit hooks to `%USERPROFILE%\.claude\settings.json`

> If an existing Claude settings.json file is found, Claw Jump creates a `settings.json.bak-timestamp` backup before modifying it.

#### Run from source

```powershell
dotnet restore ClawJump.slnx
dotnet run --project src/ClawJump.Avalonia/ClawJump.Avalonia.csproj
```

### Usage

Right-click the tray icon to open the menu:

- **Show claw**: show and activate the desktop notifier
- **Hide claw**: hide the desktop notifier
- **Test alert**: manually switch to the ready state
- **Mark viewed**: switch back to idle
- **Check Hook status**: check the local service and Claude Hook configuration
- **Current version**: view the running Claw Jump version
- **Check for updates**: check GitHub Releases for a newer version and open the release page when an update is available
- **Open settings**: change the port, alert behavior, and exit cleanup option
- **Open config directory**: open the Claw Jump config and log directory
- **Open Claude config directory**: open `%USERPROFILE%\.claude`
- **Generate Claude Hook script**: generate only the script and settings snippet
- **Write Claude Hook config**: merge the hook configuration into Claude Code settings.json
- **View event logs**: view received hook events
- **Open log directory**: open the log file directory
- **Exit**: quit the app, optionally cleaning up Claude Hook settings

### How Hooks Work

Claw Jump stores configuration and generated files under `%APPDATA%\ClawJump`:

- `config.json`: app configuration
- `hooks\claw-jump-hook.ps1`: PowerShell script invoked by Claude Code hooks
- `claude-settings-snippet.json`: Claude settings.json snippet for manual use
- `logs\claw-jump.log`: event log

Hook flow:

1. Claude Code triggers a Stop, Notification, or UserPromptSubmit hook
2. The hook command runs `claw-jump-hook.ps1`
3. The script POSTs event JSON to `http://127.0.0.1:47653/event`
4. Claw Jump receives the event and updates the claw state

Default event behavior:

| Hook event | Behavior |
| --- | --- |
| Stop | Show ready/attention state |
| Notification | Show ready state; show approval-required state when the notification indicates Claude Code needs permission/approval |
| UserPromptSubmit | Show working state |

### Settings

- **Port**: local HTTP service port, default `47653`
- **Show claw when a Hook event is received**: automatically show the window when an event arrives while hidden
- **Show tray balloon when a Hook event is received**: currently disabled in this version
- **Clean up Claude Hook settings on exit**: remove Claw Jump hook commands from Claude settings.json when quitting

### Build

Restore dependencies:

```powershell
dotnet restore ClawJump.slnx
```

Build the solution:

```powershell
dotnet build ClawJump.slnx
```

Publish a Windows x64 self-contained build:

```powershell
powershell -ExecutionPolicy Bypass -File ./build-avalonia-release.ps1
```

Build the installer:

```powershell
powershell -ExecutionPolicy Bypass -File ./build-avalonia-installer.ps1
```

### Acknowledgements

Thanks to [arczhi/claw-jump](https://github.com/arczhi/claw-jump) for the inspiration and original idea.

### License

This project is licensed under the MIT License.
