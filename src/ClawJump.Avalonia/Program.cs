using Avalonia;
using ClawJump.Avalonia.Services;
using System.Runtime.InteropServices;

namespace ClawJump.Avalonia;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--cleanup-claude-hooks", StringComparer.OrdinalIgnoreCase))
        {
            return CleanupClaudeHooks();
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        return 0;
    }

    private static int CleanupClaudeHooks()
    {
        AttachConsoleIfNeeded();

        try
        {
            var result = HookScriptService.CleanupClaudeSettings();

            Console.WriteLine(result.Changed
                ? $"Removed {result.RemovedCount} ClawJump hook(s) from {result.SettingsPath}."
                : $"No ClawJump hooks found in {result.SettingsPath}.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to cleanup ClawJump hooks: {ex.Message}");
            return 1;
        }
    }

    private static void AttachConsoleIfNeeded()
    {
        if (OperatingSystem.IsWindows())
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
