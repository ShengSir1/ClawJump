using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using ClawJump.Avalonia.Models;
using ClawJump.Avalonia.Services;
using System.Diagnostics;

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
            ToolTipText = "Claw Jump",
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

    private void MergeClaudeHookSettings()
    {
        try
        {
            var port = _config?.Port ?? 47653;

            HookScriptService.MergeToClaudeSettings(port);

            ShowPet();
            _petWindow?.ShowReady();
        }
        catch
        {
            ShowPet();
            _petWindow?.ShowReady();
        }
    }

    private void GenerateClaudeHookScripts()
    {
        try
        {
            var port = _config?.Port ?? 47653;

            HookScriptService.Generate(port);

            ShowPet();
            _petWindow?.ShowReady();
        }
        catch
        {
            ShowPet();
            _petWindow?.ShowReady();
        }
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

        switch (hookEvent.Type?.ToLower())
        {
            case "stop":
            case "notification":
            case "approval_required":
                _petWindow.ShowReady();
                break;

            case "userpromptsubmit":
                _petWindow.SetIdle();
                break;

            default:
                _petWindow.ShowReady();
                break;
        }
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

    private async void ExitApp()
    {
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