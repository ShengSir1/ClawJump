using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace ClawJump.Avalonia;

public partial class PetWindow : Window
{
    private const string IdleImage = "avares://ClawJump/Assets/claw-idle.png";
    private const string ReadyImage = "avares://ClawJump/Assets/claw-ready.png";

    private const string DockIdleImage = "avares://ClawJump/Assets/claw-peek-idle.png";
    private const string DockReadyImage = "avares://ClawJump/Assets/claw-peek-ready.png";

    // 靠近边界多少像素以内触发贴边隐藏
    private const int DockThreshold = 36;

    // 隐藏后露出多少像素
    private const int VisibleStrip = 72;

    private Image? _clawImage;
    private Image? _dockImage;
    private bool _isReady;

    private bool _isDocked;
    private bool _isUserMoving;
    private DockSide _dockSide = DockSide.None;

    private readonly DispatcherTimer _dockTimer;

    public PetWindow()
    {
        InitializeComponent();

        _clawImage = this.FindControl<Image>("ClawImage");
        _dockImage = this.FindControl<Image>("DockImage");

        Opened += (_, _) => MoveToBottomRight();

        PointerPressed += PetWindow_PointerPressed;
        PointerReleased += PetWindow_PointerReleased;
        PositionChanged += (_, _) => RestartDockTimerIfMoving();

        _dockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(450)
        };

        _dockTimer.Tick += (_, _) =>
        {
            _dockTimer.Stop();

            if (_isUserMoving)
            {
                _isUserMoving = false;
                DockIfNeeded();
            }
        };

        HideDockImage();
        SetIdle();
    }

    private enum DockSide
    {
        None,
        Left,
        Right,
        Top
    }

    private void PetWindow_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        // 已经贴边隐藏时，点击露出的小条，只恢复显示，不再次隐藏
        if (_isDocked)
        {
            RestoreFromDock();
            e.Handled = true;
            return;
        }

        // 使用 Avalonia 原生窗口拖动，更跟手
        _isUserMoving = true;
        HideDockImage();

        BeginMoveDrag(e);

        RestartDockTimer();

        e.Handled = true;
    }

    private void PetWindow_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isUserMoving)
        {
            _isUserMoving = false;
            _dockTimer.Stop();
            DockIfNeeded();
        }

        e.Handled = true;
    }

    private void RestartDockTimerIfMoving()
    {
        if (!_isUserMoving || _isDocked)
        {
            return;
        }

        RestartDockTimer();
    }

    private void RestartDockTimer()
    {
        _dockTimer.Stop();
        _dockTimer.Start();
    }

    private void DockIfNeeded()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;

        if (screen == null)
        {
            return;
        }

        var area = screen.WorkingArea;

        var leftDistance = Math.Abs(Position.X - area.X);
        var rightDistance = Math.Abs(area.Right - (Position.X + (int)Width));
        var topDistance = Math.Abs(Position.Y - area.Y);

        if (leftDistance <= DockThreshold)
        {
            DockToLeft(area);
            return;
        }

        if (rightDistance <= DockThreshold)
        {
            DockToRight(area);
            return;
        }

        if (topDistance <= DockThreshold)
        {
            DockToTop(area);
            return;
        }

        _isDocked = false;
        _dockSide = DockSide.None;
        HideDockImage();
    }

    private void DockToLeft(PixelRect area)
    {
        _isDocked = true;
        _dockSide = DockSide.Left;

        Position = new PixelPoint(
            area.X - (int)Width + VisibleStrip,
            Clamp(Position.Y, area.Y, area.Bottom - (int)Height));

        ShowDockImage(DockSide.Left);
    }

    private void DockToRight(PixelRect area)
    {
        _isDocked = true;
        _dockSide = DockSide.Right;

        Position = new PixelPoint(
            area.Right - VisibleStrip,
            Clamp(Position.Y, area.Y, area.Bottom - (int)Height));

        ShowDockImage(DockSide.Right);
    }

    private void DockToTop(PixelRect area)
    {
        _isDocked = true;
        _dockSide = DockSide.Top;

        Position = new PixelPoint(
            Clamp(Position.X, area.X, area.Right - (int)Width),
            area.Y - (int)Height + VisibleStrip);

        ShowDockImage(DockSide.Top);
    }

    private void RestoreFromDock()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;

        if (screen == null)
        {
            return;
        }

        var area = screen.WorkingArea;

        switch (_dockSide)
        {
            case DockSide.Left:
                Position = new PixelPoint(
                    area.X,
                    Clamp(Position.Y, area.Y, area.Bottom - (int)Height));
                break;

            case DockSide.Right:
                Position = new PixelPoint(
                    area.Right - (int)Width,
                    Clamp(Position.Y, area.Y, area.Bottom - (int)Height));
                break;

            case DockSide.Top:
                Position = new PixelPoint(
                    Clamp(Position.X, area.X, area.Right - (int)Width),
                    area.Y);
                break;
        }

        _isDocked = false;
        _dockSide = DockSide.None;

        HideDockImage();
    }

    private void ShowDockImage(DockSide side)
    {
        if (_dockImage == null)
        {
            return;
        }

        UpdateDockImageByState();

        _dockImage.IsVisible = true;

        switch (side)
        {
            case DockSide.Left:
                _dockImage.Width = 72;
                _dockImage.Height = 120;
                _dockImage.HorizontalAlignment = HorizontalAlignment.Right;
                _dockImage.VerticalAlignment = VerticalAlignment.Center;
                break;

            case DockSide.Right:
                _dockImage.Width = 72;
                _dockImage.Height = 120;
                _dockImage.HorizontalAlignment = HorizontalAlignment.Left;
                _dockImage.VerticalAlignment = VerticalAlignment.Center;
                break;

            case DockSide.Top:
                _dockImage.Width = 120;
                _dockImage.Height = 72;
                _dockImage.HorizontalAlignment = HorizontalAlignment.Center;
                _dockImage.VerticalAlignment = VerticalAlignment.Bottom;
                break;
        }
    }

    private void HideDockImage()
    {
        if (_dockImage != null)
        {
            _dockImage.IsVisible = false;
        }
    }

    private void MoveToBottomRight()
    {
        var screen = Screens.Primary;

        if (screen == null)
        {
            return;
        }

        var area = screen.WorkingArea;

        Position = new PixelPoint(
            area.Right - (int)Width - 30,
            area.Bottom - (int)Height - 30);
    }

    private void UpdateDockImageByState()
    {
        if (_dockImage == null)
        {
            return;
        }

        var uri = _isReady ? DockReadyImage : DockIdleImage;
        _dockImage.Source = new Bitmap(AssetLoader.Open(new Uri(uri)));
    }

    public void SetIdle()
    {
        _isReady = false;
        SetImage(IdleImage);

        if (_isDocked)
        {
            UpdateDockImageByState();
        }
    }

    public void SetReady()
    {
        _isReady = true;
        SetImage(ReadyImage);

        if (_isDocked)
        {
            UpdateDockImageByState();
        }
    }

    public void ShowReady()
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            Show();

            if (_isDocked)
            {
                _isReady = true;
                UpdateDockImageByState();
                return;
            }

            SetReady();
        });
    }

    private void SetImage(string uri)
    {
        try
        {
            if (_clawImage == null)
            {
                return;
            }

            _clawImage.Source = new Bitmap(
                AssetLoader.Open(new Uri(uri)));
        }
        catch
        {
            // 图片加载失败时不让程序崩溃
        }
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}