namespace ClawJump.Avalonia.Models;

public class EventLogItem
{
    public DateTime Time { get; set; } = DateTime.Now;

    public string Type { get; set; } = "";

    public string Source { get; set; } = "";

    public string Message { get; set; } = "";

    public string RawInput { get; set; } = "";

    public string DisplayTime => Time.ToString("yyyy-MM-dd HH:mm:ss");

    public string Summary => $"{DisplayTime}  {Type}  {Source}";
}