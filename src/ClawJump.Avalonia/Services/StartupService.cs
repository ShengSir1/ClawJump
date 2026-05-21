using Microsoft.Win32;
using System.Diagnostics;

namespace ClawJump.Avalonia.Services;

public static class StartupService
{
    private const string AppName = "ClawJump";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);

        var value = key?.GetValue(AppName) as string;

        return !string.IsNullOrWhiteSpace(value);
    }

    public static void Enable()
    {
        var exePath = GetExePath();

        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new InvalidOperationException("无法获取程序路径");
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);

        if (key == null)
        {
            throw new InvalidOperationException("无法打开注册表启动项");
        }

        key.SetValue(AppName, $"\"{exePath}\" --startup", RegistryValueKind.String);
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);

        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }

    private static string? GetExePath()
    {
        return Environment.ProcessPath
               ?? Process.GetCurrentProcess().MainModule?.FileName;
    }
}