namespace ClawJump.Avalonia.Models;

public enum HealthSeverity
{
    Ok,
    Warning,
    Error
}

public sealed record HealthCheckItem(
    string Name,
    HealthSeverity Severity,
    string Message);

public sealed record HookHealthStatus(
    DateTime CheckedAt,
    int Port,
    bool ServerObjectRunning,
    bool ServerHealthEndpointOk,
    bool HookScriptExists,
    bool HookScriptPortMatches,
    bool ClaudeSettingsExists,
    bool ClaudeSettingsParseable,
    bool StopHookConfigured,
    bool NotificationHookConfigured,
    bool UserPromptSubmitHookConfigured,
    IReadOnlyList<HealthCheckItem> Items)
{
    public HealthSeverity Severity => Items.Any(item => item.Severity == HealthSeverity.Error)
        ? HealthSeverity.Error
        : Items.Any(item => item.Severity == HealthSeverity.Warning)
            ? HealthSeverity.Warning
            : HealthSeverity.Ok;
}
