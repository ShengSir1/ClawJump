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
    private const string DockIdleLeftImage = "avares://ClawJump/Assets/claw-peek-idle-left.png";
    private const string DockReadyLeftImage = "avares://ClawJump/Assets/claw-peek-ready-left.png";
    private const string DockIdleTopImage = "avares://ClawJump/Assets/claw-peek-idle-top.png";
    private const string DockReadyTopImage = "avares://ClawJump/Assets/claw-peek-ready-top.png";

    // 靠近边界多少像素以内触发贴边隐藏
    private const int DockThreshold = 36;

    // 隐藏后露出多少像素
    private const int VisibleStrip = 82;

    private readonly Dictionary<string, Bitmap> _bitmapCache = new();

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
            Interval = TimeSpan.FromMilliseconds(200)
        };

        _dockTimer.Tick += (_, _) =>
        {
            _dockTimer.Stop();

            if (_isUserMoving)
            {
                DockIfNeeded();
                RestartDockTimer();
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

        // 已经贴边隐藏时，点击爬窗图，只恢复显示，不再次隐藏
        if (_isDocked)
        {
            RestoreFromDock();
            e.Handled = true;
            return;
        }

        // 使用 Avalonia 原生窗口拖动，更跟手
        _isUserMoving = true;

        // 拖动完整图时，确保显示完整小爪子
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
        if (!_isUserMoving)
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

        var windowLeft = Position.X;
        var windowTop = Position.Y;
        var windowRight = Position.X + (int)Width;

        // 已经靠近或超过左边界
        var hitLeft = windowLeft <= area.X + DockThreshold;

        // 已经靠近或超过右边界
        var hitRight = windowRight >= area.Right - DockThreshold;

        // 已经靠近或超过上边界
        var hitTop = windowTop <= area.Y + DockThreshold;

        if (hitLeft)
        {
            DockToLeft(area);
            return;
        }

        if (hitRight)
        {
            DockToRight(area);
            return;
        }

        if (hitTop)
        {
            DockToTop(area);
            return;
        }

        _isDocked = false;
        _dockSide = DockSide.None;

        SetImage(_isReady ? ReadyImage : IdleImage);
        HideDockImage();
    }

    private void DockToLeft(PixelRect area)
    {
        _isDocked = true;
        _dockSide = DockSide.Left;

        // 关键：移动前先隐藏两张图，避免爬窗图在中间位置闪现
        HideBothImages();

        Position = new PixelPoint(
            area.X - (int)Width + VisibleStrip,
            Clamp(Position.Y, area.Y, area.Bottom - (int)Height));

        ShowOnlyDockImage(DockSide.Left);
    }

    private void DockToRight(PixelRect area)
    {
        _isDocked = true;
        _dockSide = DockSide.Right;

        // 关键：移动前先隐藏两张图，避免爬窗图在中间位置闪现
        HideBothImages();

        Position = new PixelPoint(
            area.Right - VisibleStrip,
            Clamp(Position.Y, area.Y, area.Bottom - (int)Height));

        ShowOnlyDockImage(DockSide.Right);
    }

    private void DockToTop(PixelRect area)
    {
        _isDocked = true;
        _dockSide = DockSide.Top;

        // 关键：移动前先隐藏两张图，避免爬窗图在中间位置闪现
        HideBothImages();

        Position = new PixelPoint(
            Clamp(Position.X, area.X, area.Right - (int)Width),
            area.Y - (int)Height + VisibleStrip);

        ShowOnlyDockImage(DockSide.Top);
    }

    private void RestoreFromDock()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;

        if (screen == null)
        {
            return;
        }

        var area = screen.WorkingArea;

        // 关键：恢复前先隐藏爬窗图，防止爬窗图残留在恢复前的位置
        HideBothImages();

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

        SetImage(_isReady ? ReadyImage : IdleImage);
        ShowOnlyFullImage();
    }

    private void HideBothImages()
    {
        if (_dockImage != null)
        {
            _dockImage.IsVisible = false;
            _dockImage.Opacity = 1;
        }

        if (_clawImage != null)
        {
            _clawImage.IsVisible = false;
            _clawImage.Opacity = 1;
        }
    }

    private void ShowOnlyFullImage()
    {
        if (_dockImage != null)
        {
            _dockImage.IsVisible = false;
            _dockImage.Opacity = 1;
        }

        if (_clawImage != null)
        {
            _clawImage.IsVisible = true;
            _clawImage.Opacity = 1;
        }
    }

    private void ShowOnlyDockImage(DockSide side)
    {
        if (_clawImage != null)
        {
            _clawImage.IsVisible = false;
            _clawImage.Opacity = 1;
        }

        if (_dockImage == null)
        {
            return;
        }

        PrepareDockImageLayout(side);
        UpdateDockImageByState();

        _dockImage.Opacity = 1;
        _dockImage.IsVisible = true;
    }

    private void PrepareDockImageLayout(DockSide side)
    {
        if (_dockImage == null)
        {
            return;
        }

        switch (side)
        {
            case DockSide.Left:
                _dockImage.Width = 88;
                _dockImage.Height = 132;
                _dockImage.HorizontalAlignment = HorizontalAlignment.Right;
                _dockImage.VerticalAlignment = VerticalAlignment.Center;
                break;

            case DockSide.Right:
                _dockImage.Width = 88;
                _dockImage.Height = 132;
                _dockImage.HorizontalAlignment = HorizontalAlignment.Left;
                _dockImage.VerticalAlignment = VerticalAlignment.Center;
                break;

            case DockSide.Top:
                _dockImage.Width = 132;
                _dockImage.Height = 88;
                _dockImage.HorizontalAlignment = HorizontalAlignment.Center;
                _dockImage.VerticalAlignment = VerticalAlignment.Bottom;
                break;
        }
    }

    private void HideDockImage()
    {
        ShowOnlyFullImage();
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

        var uri = _dockSide switch
        {
            DockSide.Left => _isReady ? DockReadyLeftImage : DockIdleLeftImage,
            DockSide.Top => _isReady ? DockReadyTopImage : DockIdleTopImage,
            _ => _isReady ? DockReadyImage : DockIdleImage
        };
        _dockImage.Source = GetBitmap(uri);
    }

    public void SetIdle()
    {
        _isReady = false;
        if (_isDocked)
        {
            UpdateDockImageByState();
            return;
        }

        SetImage(IdleImage);
    }

    public void SetReady()
    {
        _isReady = true;
        if (_isDocked)
        {
            UpdateDockImageByState();
            return;
        }

        SetImage(ReadyImage);
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
        if (_clawImage == null)
        {
            return;
        }

        _clawImage.Source = GetBitmap(uri);
    }

    private Bitmap GetBitmap(string uri)
    {
        if (_bitmapCache.TryGetValue(uri, out var bitmap))
        {
            return bitmap;
        }

        using var stream = AssetLoader.Open(new Uri(uri));
        bitmap = new Bitmap(stream);

        _bitmapCache[uri] = bitmap;

        return bitmap;
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