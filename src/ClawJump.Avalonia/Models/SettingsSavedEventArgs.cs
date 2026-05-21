namespace ClawJump.Avalonia.Models;

public class SettingsSavedEventArgs : EventArgs
{
    public AppConfig OldConfig { get; }

    public AppConfig NewConfig { get; }

    public bool IsPortChanged => OldConfig.Port != NewConfig.Port;

    public SettingsSavedEventArgs(AppConfig oldConfig, AppConfig newConfig)
    {
        OldConfig = oldConfig;
        NewConfig = newConfig;
    }
}