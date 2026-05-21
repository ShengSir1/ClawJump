using Avalonia.Controls;
using Avalonia.Interactivity;
using ClawJump.Avalonia.Models;
using ClawJump.Avalonia.Services;

namespace ClawJump.Avalonia;

public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();

        LogListBox.ItemsSource = EventLogService.Events;

        LogListBox.SelectionChanged += (_, _) =>
        {
            if (LogListBox.SelectedItem is EventLogItem item)
            {
                RawInputTextBox.Text = item.RawInput;
            }
            else
            {
                RawInputTextBox.Text = "";
            }
        };
    }

    private void OpenLogFile_Click(object? sender, RoutedEventArgs e)
    {
        EventLogService.OpenLogFile();
    }

    private void OpenLogDirectory_Click(object? sender, RoutedEventArgs e)
    {
        EventLogService.OpenLogDirectory();
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}