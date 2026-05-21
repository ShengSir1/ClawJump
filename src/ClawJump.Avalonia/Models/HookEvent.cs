// Models/HookEvent.cs
namespace ClawJump.Avalonia.Models;

public class HookEvent
{
    public string? Type { get; set; }

    public string? Message { get; set; }

    public string? Source { get; set; }

    public string? RawInput { get; set; }

    public DateTime Time { get; set; } = DateTime.Now;
}