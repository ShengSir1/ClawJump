# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Claw Jump is a Windows desktop notifier for Claude Code. The active app is `src/ClawJump.Avalonia`, an Avalonia desktop app that shows a draggable/topmost pet window, runs a local loopback HTTP server, and integrates with Claude Code hooks. `src/ClawJump.App` is a minimal WPF project currently included in the solution but not used by the release scripts.

## Commands

- Restore packages: `dotnet restore ClawJump.slnx`
- Build solution: `dotnet build ClawJump.slnx`
- Build without restore: `dotnet build ClawJump.slnx --no-restore`
- Run the Avalonia app: `dotnet run --project src/ClawJump.Avalonia/ClawJump.Avalonia.csproj`
- Run tests: `dotnet test ClawJump.slnx` (there are currently no test projects)
- Run a single test project, once one exists: `dotnet test path/to/TestProject.csproj`
- Check formatting: `dotnet format ClawJump.slnx --verify-no-changes --no-restore`
- Publish Windows x64 self-contained Avalonia build: `powershell -ExecutionPolicy Bypass -File ./build-avalonia-release.ps1`
- Build installer: `powershell -ExecutionPolicy Bypass -File ./build-avalonia-installer.ps1` (requires Inno Setup 6 at `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`)

Notes:
- The project targets .NET 10. The Avalonia app targets `net10.0`; the WPF project targets `net10.0-windows`.
- `dotnet build ClawJump.slnx --no-restore` currently succeeds but emits CA1416 warnings from `StartupService` because Windows registry APIs are used from the cross-targeted Avalonia project.
- `dotnet format ... --verify-no-changes` currently reports whitespace issues in `src/ClawJump.App/AssemblyInfo.cs`.

## Architecture

`Program.cs` boots Avalonia with `App`. `App.axaml.cs` is the composition root: it enforces a single instance, loads config, owns the pet window and tray icon, starts/stops the local HTTP server, opens settings/log windows, checks for updates, and translates incoming hook events into pet state changes.

The hook flow is:

1. `HookScriptService` generates a PowerShell hook script and Claude settings snippet under `%APPDATA%\ClawJump`, and can merge hook commands into `%USERPROFILE%\.claude\settings.json` for `Stop`, `Notification`, and `UserPromptSubmit` events.
2. The generated hook posts JSON to `http://127.0.0.1:{port}/event`.
3. `LocalHttpServer` receives `/event` POSTs, deserializes them as `HookEvent`, and raises `OnHookEventReceived` on the UI thread through `App`.
4. `App.HandleHookEvent` records the event through `EventLogService` and updates `PetWindow`: `UserPromptSubmit` shows the working state, `Stop` shows ready, notification events show ready or approval-required based on message text, and error/offline-like events show the offline/error state.

Persistent app data lives in `%APPDATA%\ClawJump` via `ConfigService`. The config file is `config.json`, generated hook files live in `hooks/`, the manual Claude settings snippet is `claude-settings-snippet.json`, and event logs are written under `logs/claw-jump.log`.

The UI is code-behind driven rather than MVVM:

- `PetWindow` owns the draggable/topmost pet UI, image state, dock-to-edge behavior for left/right/top edges, and idle/working/ready/approval/offline transitions.
- `SettingsWindow` edits `AppConfig`; saving raises `SettingsSaved`, after which `App` regenerates hook scripts and restarts the local server if the port changed.
- `LogWindow` binds to the in-memory `EventLogService.Events` collection and can open the log file or log directory.
- Tray menu actions are created in `App.CreateTrayIcon` and call directly into app/window/service methods.

Release packaging is centered on the Avalonia app. `build-avalonia-release.ps1` publishes `src/ClawJump.Avalonia` to `publish/avalonia-win-x64` as a self-contained single-file `ClawJump.exe`; `build-avalonia-installer.ps1` stops any running `ClawJump` process, runs the publish script, then invokes Inno Setup on `installer/ClawJump-Avalonia.iss` to write `installer-output/ClawJump-Avalonia-Setup-*.exe`.
