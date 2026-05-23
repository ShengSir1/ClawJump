namespace ClawJump.Avalonia.Models;

public class AppConfig
{
    public int Port { get; set; } = 47653;

    public bool ShowPetWhenEventReceived { get; set; } = true;

    public bool ShowBalloonWhenEventReceived { get; set; } = false;

    public bool CleanupClaudeHookSettingsOnExit { get; set; } = false;

    public AppConfig Clone()
    {
        return new AppConfig
        {
            Port = Port,
            ShowPetWhenEventReceived = ShowPetWhenEventReceived,
            ShowBalloonWhenEventReceived = ShowBalloonWhenEventReceived,
            CleanupClaudeHookSettingsOnExit = CleanupClaudeHookSettingsOnExit
        };
    }
}