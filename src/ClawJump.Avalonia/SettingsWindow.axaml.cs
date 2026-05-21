using Avalonia.Controls;
using Avalonia.Interactivity;
using ClawJump.Avalonia.Models;
using ClawJump.Avalonia.Services;

namespace ClawJump.Avalonia;

public partial class SettingsWindow : Window
{
    private AppConfig _oldConfig;
    private AppConfig _editingConfig;

    public event EventHandler<SettingsSavedEventArgs>? SettingsSaved;

    public SettingsWindow()
    : this(ConfigService.Load())
    {
    }
    public SettingsWindow(AppConfig config)
    {
        InitializeComponent();

        _oldConfig = config.Clone();
        _editingConfig = config.Clone();

        LoadConfigToUi();
    }

    public void Reload(AppConfig config)
    {
        _oldConfig = config.Clone();
        _editingConfig = config.Clone();

        LoadConfigToUi();
    }

    private void LoadConfigToUi()
    {
        PortTextBox.Text = _editingConfig.Port.ToString();
        ShowPetWhenEventReceivedCheckBox.IsChecked = _editingConfig.ShowPetWhenEventReceived;
        ShowBalloonWhenEventReceivedCheckBox.IsChecked = _editingConfig.ShowBalloonWhenEventReceived;
        MessageTextBlock.Text = "说明：修改监听端口后，会自动重启本地服务，并重新生成 Hook 脚本。";
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortTextBox.Text?.Trim(), out var port))
        {
            MessageTextBlock.Text = "监听端口必须是数字。";
            return;
        }

        if (port < 1024 || port > 65535)
        {
            MessageTextBlock.Text = "监听端口建议设置在 1024 到 65535 之间。";
            return;
        }

        _editingConfig.Port = port;
        _editingConfig.ShowPetWhenEventReceived =
            ShowPetWhenEventReceivedCheckBox.IsChecked == true;
        _editingConfig.ShowBalloonWhenEventReceived =
            ShowBalloonWhenEventReceivedCheckBox.IsChecked == true;

        ConfigService.Save(_editingConfig);

        SettingsSaved?.Invoke(
            this,
            new SettingsSavedEventArgs(_oldConfig.Clone(), _editingConfig.Clone()));

        Hide();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}