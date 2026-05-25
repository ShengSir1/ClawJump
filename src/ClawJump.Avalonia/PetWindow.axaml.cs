using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ClawJump.Avalonia.Models;

namespace ClawJump.Avalonia;

public partial class PetWindow : Window
{
    private const string IdleImage = "avares://ClawJump/Assets/claw-idle.png";
    private const string ReadyImage = "avares://ClawJump/Assets/claw-ready.png";
    private const string AlertImage = "avares://ClawJump/Assets/claw-alert.png";
    private const string ApprovalImage = "avares://ClawJump/Assets/claw-approval.png";

    private const string DockIdleImage = "avares://ClawJump/Assets/claw-peek-idle.png";
    private const string DockReadyImage = "avares://ClawJump/Assets/claw-peek-ready.png";
    private const string DockApprovalImage = "avares://ClawJump/Assets/claw-peek-approval.png";

    private const string DockIdleLeftImage = "avares://ClawJump/Assets/claw-peek-idle-left.png";
    private const string DockReadyLeftImage = "avares://ClawJump/Assets/claw-peek-ready-left.png";
    private const string DockApprovalLeftImage = "avares://ClawJump/Assets/claw-peek-approval-left.png";

    private const string DockIdleTopImage = "avares://ClawJump/Assets/claw-peek-idle-top.png";
    private const string DockReadyTopImage = "avares://ClawJump/Assets/claw-peek-ready-top.png";
    private const string DockApprovalTopImage = "avares://ClawJump/Assets/claw-peek-approval-top.png";


    // 靠近边界多少像素以内触发贴边隐藏
    private const int DockThreshold = 25;

    // 隐藏后露出多少像素
    private const int VisibleStrip = 88;
    private const int EdgeOffset = 0;
    private const int TopOffset = -7;

    private readonly Dictionary<string, Bitmap> _bitmapCache = new();

    private Image? _clawImage;
    private Image? _dockImage;

    private PetState _state = PetState.Idle;
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

        var wasDocked = _isDocked;

        _isUserMoving = true;

        if (!wasDocked)
        {
            HideDockImage();
        }

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

        var area = screen.Bounds;
        var scale = GetScreenScale(screen);
        var windowWidth = Scale(Width, scale);
        var windowHeight = Scale(Height, scale);
        var dockThreshold = Scale(DockThreshold, scale);

        var windowLeft = Position.X;
        var windowTop = Position.Y;
        var windowRight = Position.X + windowWidth;
        var windowBottom = Position.Y + windowHeight;

        var hitLeft = windowLeft <= area.X + dockThreshold;
        var hitRight = windowRight >= area.Right - dockThreshold;
        var hitTop = windowTop <= area.Y + dockThreshold;
        var hitBottom = windowBottom >= area.Bottom - dockThreshold;

        if (hitLeft)
        {
            DockToLeft(area, scale);
            return;
        }

        if (hitRight)
        {
            DockToRight(area, scale);
            return;
        }

        if (hitTop)
        {
            DockToTop(area, scale);
            return;
        }

        if (_isDocked && !hitLeft && !hitRight && !hitTop && !hitBottom)
        {
            RestoreFromDock();
            return;
        }

        _isDocked = false;
        _dockSide = DockSide.None;

        SetImage(GetFullImageUri(_state));
        HideDockImage();
    }

    private void DockToLeft(PixelRect area, double scale)
    {
        _isDocked = true;
        _dockSide = DockSide.Left;

        // 关键：移动前先隐藏两张图，避免爬窗图在中间位置闪现
        HideBothImages();

        var windowWidth = Scale(Width, scale);
        var windowHeight = Scale(Height, scale);
        var visibleStrip = Scale(VisibleStrip, scale);
        var edgeOffset = Scale(EdgeOffset, scale);

        Position = new PixelPoint(
            area.X - windowWidth + visibleStrip + edgeOffset,
            Clamp(Position.Y, area.Y, area.Bottom - windowHeight));

        ShowOnlyDockImage(DockSide.Left);
    }

    private void DockToRight(PixelRect area, double scale)
    {
        _isDocked = true;
        _dockSide = DockSide.Right;

        // 关键：移动前先隐藏两张图，避免爬窗图在中间位置闪现
        HideBothImages();

        var windowHeight = Scale(Height, scale);
        var visibleStrip = Scale(VisibleStrip, scale);
        var edgeOffset = Scale(EdgeOffset, scale);

        Position = new PixelPoint(
            area.Right - visibleStrip + edgeOffset,
            Clamp(Position.Y, area.Y, area.Bottom - windowHeight));

        ShowOnlyDockImage(DockSide.Right);
    }

    private void DockToTop(PixelRect area, double scale)
    {
        _isDocked = true;
        _dockSide = DockSide.Top;

        // 关键：移动前先隐藏两张图，避免爬窗图在中间位置闪现
        HideBothImages();

        var windowWidth = Scale(Width, scale);
        var windowHeight = Scale(Height, scale);
        var visibleStrip = Scale(VisibleStrip, scale);
        var topOffset = Scale(TopOffset, scale);

        Position = new PixelPoint(
            Clamp(Position.X, area.X, area.Right - windowWidth),
            area.Y - windowHeight + visibleStrip + topOffset);

        ShowOnlyDockImage(DockSide.Top);
    }

    private void RestoreFromDock()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;

        if (screen == null)
        {
            return;
        }

        var area = screen.Bounds;
        var scale = GetScreenScale(screen);
        var windowWidth = Scale(Width, scale);
        var windowHeight = Scale(Height, scale);

        // 关键：恢复前先隐藏爬窗图，防止爬窗图残留在恢复前的位置
        HideBothImages();

        switch (_dockSide)
        {
            case DockSide.Left:
                Position = new PixelPoint(
                    area.X,
                    Clamp(Position.Y, area.Y, area.Bottom - windowHeight));
                break;

            case DockSide.Right:
                Position = new PixelPoint(
                    area.Right - windowWidth,
                    Clamp(Position.Y, area.Y, area.Bottom - windowHeight));
                break;

            case DockSide.Top:
                Position = new PixelPoint(
                    Clamp(Position.X, area.X, area.Right - windowWidth),
                    area.Y);
                break;
        }

        _isDocked = false;
        _dockSide = DockSide.None;

        SetImage(GetFullImageUri(_state));
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

        var area = screen.Bounds;
        var scale = GetScreenScale(screen);
        var windowWidth = Scale(Width, scale);
        var windowHeight = Scale(Height, scale);
        var margin = Scale(30, scale);

        Position = new PixelPoint(
            area.Right - windowWidth - margin,
            area.Bottom - windowHeight - margin);
    }

    private void UpdateDockImageByState()
    {
        if (_dockImage == null)
        {
            return;
        }

        var uri = GetDockImageUri(_state, _dockSide);
        _dockImage.Source = GetBitmap(uri);
    }

    public void SetIdle()
    {
        SetState(PetState.Idle);
    }

    public void SetReady()
    {
        SetState(PetState.Ready);
    }

    public void SetState(PetState state)
    {
        _state = state;
        if (_isDocked)
        {
            UpdateDockImageByState();
            return;
        }

        SetImage(GetFullImageUri(_state));
    }

    public void ShowReady()
    {
        ShowState(PetState.Ready);
    }

    public void ShowState(PetState state)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            Show();
            SetState(state);
        });
    }

    private static string GetFullImageUri(PetState state)
    {
        return state switch
        {
            PetState.Ready => ReadyImage,
            PetState.ApprovalRequired => ApprovalImage,
            PetState.ErrorOffline => AlertImage,
            _ => IdleImage
        };
    }

    private static string GetDockImageUri(PetState state, DockSide side)
    {
        return (state, side) switch
        {
            (PetState.ApprovalRequired, DockSide.Left) => DockApprovalLeftImage,
            (PetState.ApprovalRequired, DockSide.Top) => DockApprovalTopImage,
            (PetState.ApprovalRequired, _) => DockApprovalImage,
            (PetState.Ready or PetState.ErrorOffline, DockSide.Left) => DockReadyLeftImage,
            (PetState.Ready or PetState.ErrorOffline, DockSide.Top) => DockReadyTopImage,
            (PetState.Ready or PetState.ErrorOffline, _) => DockReadyImage,
            (_, DockSide.Left) => DockIdleLeftImage,
            (_, DockSide.Top) => DockIdleTopImage,
            _ => DockIdleImage
        };
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

    private static double GetScreenScale(Screen screen)
    {
        return screen.Scaling <= 0 ? 1 : screen.Scaling;
    }

    private static int Scale(double value, double scale)
    {
        return (int)Math.Round(value * scale);
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