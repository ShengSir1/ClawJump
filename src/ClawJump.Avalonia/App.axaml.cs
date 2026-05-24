using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using ClawJump.Avalonia.Models;
using ClawJump.Avalonia.Services;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace ClawJump.Avalonia;

public partial class App : Application
{
    private PetWindow? _petWindow;
    private TrayIcon? _trayIcon;
    private LocalHttpServer? _server;
    private AppConfig? _config;
    private SingleInstanceService? _singleInstance;
    private LogWindow? _logWindow;
    private SettingsWindow? _settingsWindow;
    private string? _lastClaudeHookEventType;
    private NativeMenuItem? _healthSummaryItem;
    private NativeMenuItem? _serverStatusItem;
    private NativeMenuItem? _hookStatusItem;
    private NativeMenuItem? _refreshHealthItem;
    private HookHealthStatus? _lastHealthStatus;
    private bool _isHealthCheckRunning;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        _singleInstance = new SingleInstanceService("ClawJump.Avalonia.SingleInstance");

        if (!_singleInstance.IsFirstInstance)
        {
            Environment.Exit(0);
            return;
        }

        _config = ConfigService.Load();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _petWindow = new PetWindow();

            desktop.MainWindow = _petWindow;
            _petWindow.Show();

            CreateTrayIcon();
        }

        try
        {
            await StartLocalServerAsync();
            AutoMergeClaudeHookSettings();
            await RefreshHealthStatusAsync(logResult: false);
        }
        catch
        {
            ExitApp();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CreateTrayIcon()
    {
        var menu = new NativeMenu();

        var showItem = new NativeMenuItem("显示小爪子");
        showItem.Click += (_, _) => ShowPet();

        var hideItem = new NativeMenuItem("隐藏小爪子");
        hideItem.Click += (_, _) => HidePet();

        var testItem = new NativeMenuItem("测试发光提醒");
        testItem.Click += (_, _) => TestJump();

        var markViewedItem = new NativeMenuItem("标记已查看");
        markViewedItem.Click += (_, _) => MarkViewed();

        _healthSummaryItem = new NativeMenuItem("状态：未检查")
        {
            IsEnabled = false
        };

        _serverStatusItem = new NativeMenuItem("本地服务：未检查")
        {
            IsEnabled = false
        };

        _hookStatusItem = new NativeMenuItem("Claude Hook：未检查")
        {
            IsEnabled = false
        };

        _refreshHealthItem = new NativeMenuItem("检查 Hook 状态");
        _refreshHealthItem.Click += async (_, _) => await RefreshHealthStatusAsync(logResult: true);

        var settingsItem = new NativeMenuItem("打开设置");
        settingsItem.Click += (_, _) => ShowSettingsWindow();

        var openConfigItem = new NativeMenuItem("打开配置目录");
        openConfigItem.Click += (_, _) => OpenConfigDirectory();

        var openClaudeConfigItem = new NativeMenuItem("打开 Claude 配置目录");
        openClaudeConfigItem.Click += (_, _) => HookScriptService.OpenClaudeConfigDirectory();

        var generateHookItem = new NativeMenuItem("生成 Claude Hook 脚本");
        generateHookItem.Click += (_, _) => GenerateClaudeHookScripts();

        var mergeHookItem = new NativeMenuItem("一键写入 Claude Hook 配置");
        mergeHookItem.Click += (_, _) => MergeClaudeHookSettings();

        var showLogItem = new NativeMenuItem("查看事件日志");
        showLogItem.Click += (_, _) => ShowLogWindow();

        var openLogDirectoryItem = new NativeMenuItem("打开日志目录");
        openLogDirectoryItem.Click += (_, _) => EventLogService.OpenLogDirectory();

        var exitItem = new NativeMenuItem("退出");
        exitItem.Click += (_, _) => ExitApp();

        menu.Items.Add(showItem);
        menu.Items.Add(hideItem);
        menu.Items.Add(testItem);
        menu.Items.Add(markViewedItem);
        menu.Items.Add(new NativeMenuItemSeparator());

        menu.Items.Add(_healthSummaryItem);
        menu.Items.Add(_serverStatusItem);
        menu.Items.Add(_hookStatusItem);
        menu.Items.Add(_refreshHealthItem);
        menu.Items.Add(new NativeMenuItemSeparator());

        menu.Items.Add(settingsItem);
        menu.Items.Add(openConfigItem);
        menu.Items.Add(openClaudeConfigItem);
        menu.Items.Add(generateHookItem);
        menu.Items.Add(mergeHookItem);
        menu.Items.Add(new NativeMenuItemSeparator());

        menu.Items.Add(showLogItem);
        menu.Items.Add(openLogDirectoryItem);
        menu.Items.Add(new NativeMenuItemSeparator());

        menu.Items.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            ToolTipText = "Claw Jump\n状态：未检查",
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ClawJump/Assets/claw.ico"))),
            Menu = menu,
            IsVisible = true
        };

        _trayIcon.Clicked += (_, _) => ShowPet();
    }

    private async Task ApplyNewConfigAsync(SettingsSavedEventArgs args)
    {
        _config = args.NewConfig.Clone();

        // 保存配置后，重新生成 Hook 脚本，确保端口同步
        HookScriptService.Generate(_config.Port);

        if (!args.IsPortChanged)
        {
            ShowPet();
            _petWindow?.SetReady();
            await RefreshHealthStatusAsync(logResult: false);
            return;
        }

        try
        {
            await StopLocalServerAsync();
            await StartLocalServerAsync();

            ShowPet();
            _petWindow?.SetReady();
        }
        catch
        {
            ShowPet();
            _petWindow?.SetReady();
        }

        await RefreshHealthStatusAsync(logResult: false);
    }

    private async Task StopLocalServerAsync()
    {
        if (_server == null)
        {
            return;
        }

        await _server.StopAsync();
        _server = null;
    }

    private void ShowSettingsWindow()
    {
        if (_config == null)
        {
            _config = ConfigService.Load();
        }

        if (_settingsWindow == null)
        {
            _settingsWindow = new SettingsWindow(_config);
            _settingsWindow.SettingsSaved += async (_, args) =>
            {
                await ApplyNewConfigAsync(args);
            };
        }
        else
        {
            _settingsWindow.Reload(_config);
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OpenConfigDirectory()
    {
        try
        {
            Directory.CreateDirectory(ConfigService.ConfigDirectory);

            Process.Start(new ProcessStartInfo
            {
                FileName = ConfigService.ConfigDirectory,
                UseShellExecute = true
            });
        }
        catch
        {
            ShowPet();
            _petWindow?.ShowReady();
        }
    }

    private void AutoMergeClaudeHookSettings()
    {
        try
        {
            var port = _config?.Port ?? 47653;

            HookScriptService.MergeToClaudeSettings(port);
            EventLogService.AddSystem("已自动写入 Claude Hook 配置。");

            SyncPetWithClaudeStatus();
        }
        catch (Exception ex)
        {
            EventLogService.AddSystem($"自动写入 Claude Hook 配置失败：{ex.Message}");

            SyncPetWithClaudeStatus();
        }
    }

    private void MergeClaudeHookSettings()
    {
        try
        {
            var port = _config?.Port ?? 47653;

            HookScriptService.MergeToClaudeSettings(port);

            SyncPetWithClaudeStatus();
        }
        catch
        {
            SyncPetWithClaudeStatus();
        }

        _ = RefreshHealthStatusAsync(logResult: false);
    }

    private void GenerateClaudeHookScripts()
    {
        try
        {
            var port = _config?.Port ?? 47653;

            HookScriptService.Generate(port);

            SyncPetWithClaudeStatus();
        }
        catch
        {
            SyncPetWithClaudeStatus();
        }

        _ = RefreshHealthStatusAsync(logResult: false);
    }

    private async Task RefreshHealthStatusAsync(bool logResult)
    {
        if (_isHealthCheckRunning)
        {
            return;
        }

        _isHealthCheckRunning = true;
        UpdateTrayHealthChecking();

        try
        {
            _config ??= ConfigService.Load();

            var status = await HookHealthService.CheckAsync(
                _config.Port,
                _server?.IsRunning == true);

            _lastHealthStatus = status;
            UpdateTrayHealthStatus(status);

            if (logResult)
            {
                EventLogService.AddSystem(BuildHealthLogMessage(status));
            }
        }
        catch (Exception ex)
        {
            UpdateTrayHealthError(ex.Message);

            if (logResult)
            {
                EventLogService.AddSystem($"Hook 状态检查失败：{ex.Message}");
            }
        }
        finally
        {
            _isHealthCheckRunning = false;
        }
    }

    private void UpdateTrayHealthChecking()
    {
        if (_trayIcon != null)
        {
            _trayIcon.ToolTipText = "Claw Jump\n状态：检查中";
        }

        if (_healthSummaryItem != null)
        {
            _healthSummaryItem.Header = "状态：检查中";
        }
    }

    private void UpdateTrayHealthStatus(HookHealthStatus status)
    {
        if (_trayIcon != null)
        {
            _trayIcon.ToolTipText = BuildTrayToolTip(status);
        }

        if (_healthSummaryItem != null)
        {
            _healthSummaryItem.Header = $"状态：{GetSeverityText(status.Severity)}（{status.CheckedAt:HH:mm}）";
        }

        if (_serverStatusItem != null)
        {
            _serverStatusItem.Header = status.ServerHealthEndpointOk
                ? $"本地服务：运行中，端口 {status.Port}"
                : $"本地服务：不可用，端口 {status.Port}";
        }

        if (_hookStatusItem != null)
        {
            var configuredCount = new[]
            {
                status.StopHookConfigured,
                status.NotificationHookConfigured,
                status.UserPromptSubmitHookConfigured
            }.Count(configured => configured);

            _hookStatusItem.Header = status.ClaudeSettingsParseable
                ? $"Claude Hook：{configuredCount}/3 已配置"
                : "Claude Hook：配置异常";
        }
    }

    private void UpdateTrayHealthError(string message)
    {
        if (_trayIcon != null)
        {
            _trayIcon.ToolTipText = $"Claw Jump\n状态：异常\n{message}";
        }

        if (_healthSummaryItem != null)
        {
            _healthSummaryItem.Header = "状态：异常";
        }

        if (_hookStatusItem != null)
        {
            _hookStatusItem.Header = "Claude Hook：检查失败";
        }
    }

    private static string BuildTrayToolTip(HookHealthStatus status)
    {
        return $"Claw Jump\n状态：{GetSeverityText(status.Severity)}\n本地服务：{(status.ServerHealthEndpointOk ? "运行中" : "不可用")}\nClaude Hook：{GetHookStatusText(status)}";
    }

    private static string BuildHealthLogMessage(HookHealthStatus status)
    {
        var details = string.Join("；", status.Items.Select(item => $"{item.Name}: {item.Message}"));
        return $"Hook 状态检查：{GetSeverityText(status.Severity)}。{details}";
    }

    private static string GetSeverityText(HealthSeverity severity)
    {
        return severity switch
        {
            HealthSeverity.Ok => "正常",
            HealthSeverity.Warning => "需要检查",
            HealthSeverity.Error => "异常",
            _ => "未知"
        };
    }

    private static string GetHookStatusText(HookHealthStatus status)
    {
        if (!status.ClaudeSettingsParseable)
        {
            return "配置异常";
        }

        if (!status.HookScriptExists)
        {
            return "脚本缺失";
        }

        if (!status.HookScriptPortMatches)
        {
            return "端口不匹配";
        }

        var configuredCount = new[]
        {
            status.StopHookConfigured,
            status.NotificationHookConfigured,
            status.UserPromptSubmitHookConfigured
        }.Count(configured => configured);

        return configuredCount == 3 ? "已配置" : $"{configuredCount}/3 已配置";
    }

    private void SyncPetWithClaudeStatus()
    {
        ShowPet();
        _petWindow?.SetState(GetPetStateForHookEventType(_lastClaudeHookEventType));
    }

    private void ShowLogWindow()
    {
        if (_logWindow == null)
        {
            _logWindow = new LogWindow();
        }

        _logWindow.Show();
        _logWindow.Activate();
    }

    private void MarkViewed()
    {
        ShowPet();
        _petWindow?.SetIdle();
    }

    private async Task StartLocalServerAsync()
    {
        _config ??= ConfigService.Load();

        _server = new LocalHttpServer(_config.Port);

        _server.OnHookEventReceived += hookEvent =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                HandleHookEvent(hookEvent);
            });
        };

        await _server.StartAsync();

        EventLogService.AddSystem($"本地服务启动成功，监听端口：{_config.Port}");
    }

    private void HandleHookEvent(HookEvent hookEvent)
    {
        EventLogService.Add(new EventLogItem
        {
            Time = DateTime.Now,
            Type = hookEvent.Type ?? "",
            Source = hookEvent.Source ?? "",
            Message = hookEvent.Message ?? "",
            RawInput = hookEvent.RawInput ?? ""
        });

        if (_petWindow == null)
        {
            return;
        }

        if (_config?.ShowPetWhenEventReceived == true && !_petWindow.IsVisible)
        {
            _petWindow.Show();
        }

        _lastClaudeHookEventType = hookEvent.Type;
        _petWindow.ShowState(GetPetStateForHookEvent(hookEvent));
    }

    private static PetState GetPetStateForHookEvent(HookEvent hookEvent)
    {
        var eventType = hookEvent.Type?.Trim().ToLowerInvariant();

        return eventType switch
        {
            null or "" => PetState.Idle,
            "userpromptsubmit" => PetState.Working,
            "notification" when IsApprovalNotification(hookEvent) => PetState.ApprovalRequired,
            "stop" or "notification" => PetState.Ready,
            "approval_required" or "approvalrequired" => PetState.ApprovalRequired,
            "error" or "offline" or "disconnect" or "connection_error" or "hook_error" => PetState.ErrorOffline,
            _ => PetState.Ready
        };
    }

    private static PetState GetPetStateForHookEventType(string? eventType)
    {
        return GetPetStateForHookEvent(new HookEvent { Type = eventType });
    }

    private static bool IsApprovalNotification(HookEvent hookEvent)
    {
        if (!string.Equals(hookEvent.Type?.Trim(), "Notification", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TryReadRawInputNotificationMessage(hookEvent.RawInput, out var notificationMessage) &&
            ContainsApprovalText(notificationMessage))
        {
            return true;
        }

        return ContainsApprovalText(hookEvent.Message) || ContainsApprovalText(hookEvent.RawInput);
    }

    private static bool TryReadRawInputNotificationMessage(string? rawInput, out string? message)
    {
        message = null;

        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return false;
        }

        try
        {
            if (JsonNode.Parse(rawInput) is JsonObject rawObj &&
                rawObj["message"]?.GetValue<string>() is { } rawMessage)
            {
                message = rawMessage;
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool ContainsApprovalText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("approval", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("approve", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("requires your", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("needs your", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("需要", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("审批", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("批准", StringComparison.OrdinalIgnoreCase);
    }

    private void ShowPet()
    {
        if (_petWindow == null)
        {
            _petWindow = new PetWindow();
        }

        _petWindow.Show();
        _petWindow.Activate();
    }

    private void HidePet()
    {
        _petWindow?.Hide();
    }

    private void TestJump()
    {
        ShowPet();
        _petWindow?.ShowReady();
    }

    private void CleanupClaudeHookSettingsOnExitIfEnabled()
    {
        _config ??= ConfigService.Load();

        if (!_config.CleanupClaudeHookSettingsOnExit)
        {
            return;
        }

        try
        {
            var result = HookScriptService.CleanupClaudeSettings();

            if (result.Changed)
            {
                EventLogService.AddSystem($"退出时已清理 Claude Hook 配置，移除 {result.RemovedCount} 项。");
            }
            else
            {
                EventLogService.AddSystem("退出时未发现需要清理的 Claude Hook 配置。");
            }
        }
        catch (Exception ex)
        {
            EventLogService.AddSystem($"退出时清理 Claude Hook 配置失败：{ex.Message}");
        }
    }

    private async void ExitApp()
    {
        CleanupClaudeHookSettingsOnExitIfEnabled();

        if (_server != null)
        {
            await _server.StopAsync();
            _server = null;
        }

        _trayIcon?.Dispose();
        _trayIcon = null;

        _singleInstance?.Dispose();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
        else
        {
            Environment.Exit(0);
        }
    }
}