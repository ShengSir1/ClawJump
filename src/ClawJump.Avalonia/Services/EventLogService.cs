using ClawJump.Avalonia.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

namespace ClawJump.Avalonia.Services;

public static class EventLogService
{
    private const int MaxMemoryCount = 200;

    public static ObservableCollection<EventLogItem> Events { get; } = new();

    public static string LogDirectory =>
        Path.Combine(ConfigService.ConfigDirectory, "logs");

    public static string LogFilePath =>
        Path.Combine(LogDirectory, "claw-jump.log");

    public static void Add(EventLogItem item)
    {
        Events.Insert(0, item);

        while (Events.Count > MaxMemoryCount)
        {
            Events.RemoveAt(Events.Count - 1);
        }

        AppendToFile(item);
    }

    public static void AddSystem(string message)
    {
        Add(new EventLogItem
        {
            Time = DateTime.Now,
            Type = "System",
            Source = "ClawJump",
            Message = message,
            RawInput = ""
        });
    }

    public static void OpenLogDirectory()
    {
        Directory.CreateDirectory(LogDirectory);

        Process.Start(new ProcessStartInfo
        {
            FileName = LogDirectory,
            UseShellExecute = true
        });
    }

    public static void OpenLogFile()
    {
        Directory.CreateDirectory(LogDirectory);

        if (!File.Exists(LogFilePath))
        {
            File.WriteAllText(LogFilePath, "", Encoding.UTF8);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = LogFilePath,
            UseShellExecute = true
        });
    }

    private static void AppendToFile(EventLogItem item)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);

            var line =
                $"[{item.DisplayTime}] [{item.Type}] [{item.Source}] {item.Message}{Environment.NewLine}" +
                $"RawInput: {TrimRawInput(item.RawInput)}{Environment.NewLine}" +
                $"------------------------------------------------------------{Environment.NewLine}";

            File.AppendAllText(LogFilePath, line, Encoding.UTF8);
        }
        catch
        {
            // 日志失败不影响主流程
        }
    }

    private static string TrimRawInput(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return "";
        }

        return rawInput.Length <= 2000
            ? rawInput
            : rawInput[..2000] + "...";
    }
}