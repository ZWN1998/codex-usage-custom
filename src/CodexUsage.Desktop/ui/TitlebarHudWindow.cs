using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CodexUsage.Core.Ui;
using CodexUsage.Core.Window;
using CodexUsage.Desktop.Platform;

namespace CodexUsage.Desktop.Ui;

internal sealed class TitlebarHudWindow : Window
{
    private readonly HudViewModel _viewModel;
    private readonly ICodexWindowTracker _tracker;
    private readonly IPlatformHudHost _platformHud = PlatformHudHostFactory.Create();
    private readonly TextBlock _usage;
    private readonly TextBlock _refresh;
    private readonly Border _root;
    private readonly StackPanel _panel;
    private readonly Border _refreshButton;
    private readonly DispatcherTimer _timer;
    private readonly HudAppearanceStore _appearanceStore = new(OperatingSystem.IsWindows()
        ? System.IO.Path.Combine(WindowsLifecycle.StateDirectory, "appearance.json")
        : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUsageHud", "appearance.json"));
    private HudAppearance _appearance;
    private HudSettingsWindow? _settingsWindow;
    private CodexWindowSnapshot? _target;
    private string? _lastLayoutKey;
    private string? _lastPositionReport;
    private DateTimeOffset _hiddenUntil;
    private DateTimeOffset _nextWindowStatus;

    public event Func<Task>? RefreshRequested;

    public TitlebarHudWindow(HudViewModel viewModel, ICodexWindowTracker tracker, Action shutdown, HudAppearance? appearance = null)
    {
        _viewModel = viewModel;
        _tracker = tracker;
        _appearance = (appearance ?? _appearanceStore.Read(DefaultAppearance)).Normalize();
        Title = "Codex Usage";
        Width = 420;
        Height = _appearance.RowHeight;
        CanResize = false;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = false;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        WindowStartupLocation = WindowStartupLocation.Manual;
        _platformHud.Initialize(this);

        _usage = MakeText("读取中…");
        _refresh = MakeText("↻");
        _refresh.FontSize = 12;
        _refresh.Width = 14;
        _refresh.TextAlignment = TextAlignment.Center;
        _refreshButton = new Border
        {
            Child = _refresh,
            Width = 20,
            Height = _appearance.RowHeight,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        ToolTip.SetTip(_refreshButton, "刷新");
        _refreshButton.PointerEntered += (_, _) => _refreshButton.Background = new SolidColorBrush(Color.Parse(_appearance.TextColor), 0.08);
        _refreshButton.PointerExited += (_, _) => _refreshButton.Background = Brushes.Transparent;
        _refreshButton.PointerReleased += async (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left && _refreshButton.IsPointerOver)
                await RequestRefreshAsync();
        };
        ToolTip.SetShowDelay(_usage, 350);
        _panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _usage, _refreshButton },
        };
        _root = new Border { Child = _panel };
        _platformHud.ConfigureRoot(_root);
        Content = _root;
        var refreshItem = new MenuItem { Header = "立即刷新" };
        refreshItem.Click += async (_, _) => await RequestRefreshAsync();
        var settingsItem = new MenuItem { Header = "位置、字号与颜色…" };
        settingsItem.Click += (_, _) => ShowSettings();
        var hideItem = new MenuItem { Header = "暂时隐藏（5分钟）" };
        hideItem.Click += (_, _) => { _hiddenUntil = DateTimeOffset.UtcNow.AddMinutes(5); Hide(); };
        var exit = new MenuItem { Header = "退出工具" };
        exit.Click += (_, _) => shutdown();
        _root.ContextMenu = new ContextMenu { Items = { refreshItem, settingsItem, hideItem, exit } };
        if (OperatingSystem.IsWindows())
        {
            var autoStart = new MenuItem { Header = "跟随 Codex 自动运行", ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = WindowsLifecycle.AutoStartEnabled };
            autoStart.Click += (_, _) =>
            {
                if (!OperatingSystem.IsWindows()) return;
                try
                {
                    WindowsLifecycle.SetAutoStart(autoStart.IsChecked);
                    if (autoStart.IsChecked) WindowsLifecycle.EnsureWatcher();
                }
                catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
                { autoStart.IsChecked = WindowsLifecycle.AutoStartEnabled; ToolTip.SetTip(autoStart, "登录项设置失败，请从安装目录重试"); }
            };
            _root.ContextMenu.Items.Insert(2, autoStart);
        }
        _viewModel.Changed += OnChanged;
        Opened += (_, _) => ConfigureNativeWindow();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, TrackTarget);
        _timer.Start();
        Closed += (_, _) => { _settingsWindow?.Close(); _timer.Stop(); _viewModel.Changed -= OnChanged; };
        ApplyViewModel();
    }

    private TextBlock MakeText(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = _appearance.FontSize,
            FontWeight = FontWeight.Normal,
            Foreground = new SolidColorBrush(Color.Parse(_appearance.TextColor)),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            UseLayoutRounding = true,
        };
        _platformHud.ConfigureText(block);
        return block;
    }

    public void ShowSettings()
    {
        if (_settingsWindow is not null) { _settingsWindow.Activate(); return; }
        _settingsWindow = new HudSettingsWindow(_appearance, _appearanceStore, ApplyAppearance, _usage.Text ?? "Codex Usage", DefaultAppearance);
        _settingsWindow.Closed += (_, _) => { _settingsWindow = null; _lastPositionReport = null; };
        _settingsWindow.ShowFor(this);
    }

    private static HudAppearance DefaultAppearance => OperatingSystem.IsWindows()
        ? new HudAppearance(TextColor: "#8B99A6") : new HudAppearance();

    private void ApplyAppearance(HudAppearance appearance)
    {
        _appearance = appearance.Normalize();
        var brush = new SolidColorBrush(Color.Parse(_appearance.TextColor));
        _usage.Foreground = brush;
        _refresh.Foreground = brush;
        _lastLayoutKey = null;
        _lastPositionReport = null;
        UpdateLayoutText();
        TrackTarget(this, EventArgs.Empty);
    }

    private void OnChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(ApplyViewModel);

    private async Task RequestRefreshAsync()
    {
        if (!_viewModel.IsRefreshing && RefreshRequested is { } refresh)
            await refresh();
    }

    private void ApplyViewModel()
    {
        _lastLayoutKey = null;
        _refresh.Text = _viewModel.IsRefreshing ? "⋯" : "↻";
        _refreshButton.IsEnabled = !_viewModel.IsRefreshing;
        ToolTip.SetTip(_refreshButton, _viewModel.IsRefreshing ? "正在刷新…" : "刷新");
        ToolTip.SetTip(_usage, TitlebarText.Details(_viewModel.Usage, _viewModel.UsageError));
        UpdateLayoutText();
        _settingsWindow?.UpdatePreview(_usage.Text ?? "用量文字 · 重置倒计时 ↻");
    }

    private void UpdateLayoutText()
    {
        var now = DateTimeOffset.UtcNow;
        var scale = Math.Max(1, _target?.DisplayScale ?? 1);
        var vertical = _appearance.IsVertical;
        var available = _target is not null && OperatingSystem.IsWindows()
            ? WindowsHudPlacement.AvailableWidth(_target, _appearance)
            : _target is null ? 600 : Math.Max(0,
            _target.Bounds.Width / scale
                - (_appearance.Anchor is HudAnchor.TopCenter or HudAnchor.TopLeft or HudAnchor.TopRight ? 240 : 32));
        if (available < 20) { Width = 20; _usage.Text = ""; return; }
        if (vertical) available = Math.Min(180, available);
        var availableHeight = _target is null ? 600 : Math.Max(22, _target.Bounds.Height / scale - 16);
        var key = $"{now.ToUnixTimeSeconds() / 60}:{available}:{availableHeight}:{_appearance}:{_viewModel.UsageError}:{_viewModel.IsRefreshing}";
        if (key == _lastLayoutKey) return;
        _lastLayoutKey = key;
        var rowHeight = _appearance.RowHeight;
        _usage.FontSize = _appearance.FontSize;
        _usage.LineHeight = Math.Ceiling(_appearance.FontSize * 1.4);
        _usage.TextAlignment = vertical ? TextAlignment.Center : TextAlignment.Left;
        _usage.HorizontalAlignment = vertical ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _refresh.FontSize = _appearance.FontSize + 1;
        _refresh.Width = Math.Ceiling(_appearance.FontSize + 6);
        _refreshButton.Width = Math.Max(20, Math.Ceiling(_appearance.FontSize + 8));
        _refreshButton.Height = rowHeight;
        _refreshButton.HorizontalAlignment = vertical ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _panel.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        _panel.Spacing = vertical ? 4 : 0;
        for (var detail = 0; detail <= 2; detail++)
        {
            var text = _viewModel.Usage is null
                ? (_viewModel.IsRefreshing ? "读取中…" : "暂不可用")
                : vertical ? TitlebarText.VerticalUsage(_viewModel.Usage, now, detail)
                    : TitlebarText.Usage(_viewModel.Usage, now, detail);
            if (_viewModel.UsageError is not null && _viewModel.Usage is not null)
                text += vertical ? "\n已过期" : " · 已过期";
            _usage.Text = text;
            _usage.MaxWidth = double.PositiveInfinity;
            _usage.MaxHeight = double.PositiveInfinity;
            _usage.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desiredWidth = vertical ? Math.Max(_usage.DesiredSize.Width, _refreshButton.Width)
                : _usage.DesiredSize.Width + _refreshButton.Width;
            var desiredHeight = vertical ? _usage.DesiredSize.Height + rowHeight + 4 : rowHeight;
            if ((desiredWidth <= available && desiredHeight <= availableHeight) || detail == 2)
            {
                Width = Math.Max(_refreshButton.Width, Math.Min(available, Math.Ceiling(desiredWidth)));
                Height = Math.Min(availableHeight, Math.Ceiling(desiredHeight));
                _usage.MaxWidth = Math.Max(0, vertical ? Width : Width - _refreshButton.Width);
                _usage.MaxHeight = Math.Max(0, vertical ? Height - rowHeight - 4 : Height);
                _usage.TextTrimming = TextTrimming.CharacterEllipsis;
                break;
            }
        }
        _settingsWindow?.UpdatePreview(_usage.Text ?? "用量文字 · 重置倒计时 ↻");
    }

    private void TrackTarget(object? sender, EventArgs e)
    {
        try
        {
            if (DateTimeOffset.UtcNow < _hiddenUntil)
            {
                ReportWindowStatus("Temporarily hidden", false);
                if (IsVisible) Hide();
                return;
            }
            if (!_tracker.TryGetSnapshot(out var target) || target is null || !target.IsVisible || target.IsMinimized)
            {
                ReportWindowStatus(target?.IsMinimized == true ? "Codex minimized" : "No visible Codex window", false);
                if (IsVisible) Hide();
                return;
            }
            _target = target;
            if (OperatingSystem.IsWindows() && WindowsHudPlacement.AvailableWidth(target, _appearance) < 20)
            { ReportWindowStatus("Titlebar too narrow", false); if (IsVisible) Hide(); return; }
            UpdateLayoutText();
            _platformHud.ApplyTheme(this, _root, target);
            var scale = Math.Max(1, target.DisplayScale);
            var width = (int)Math.Round(Width * scale);
            var height = (int)Math.Round(Height * scale);
            var position = _platformHud.GetPosition(target, width, height);
            if (OperatingSystem.IsWindows())
                position = WindowsHudPlacement.Position(target, width, height, _appearance);
            else if (OperatingSystem.IsMacOS())
            {
                var placement = HudPlacement.Position(target.Bounds, width, height, scale, _appearance);
                position = new PixelPoint(placement.X, placement.Y);
            }
            _platformHud.Position(this, target, position.X, position.Y, width, height);
            if (!IsVisible) { Show(); ConfigureNativeWindow(); }
            ReportWindowStatus("Tracking Codex", true);
            ReportPosition(position, target);
        }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine($"HUD tracker failed: {error}");
            ReportWindowStatus("Tracker error: " + error.GetType().Name + ": " + error.Message, false);
            Hide();
        }
    }

    private void ReportWindowStatus(string status, bool visible)
    {
        if (!OperatingSystem.IsWindows() || DateTimeOffset.UtcNow < _nextWindowStatus) return;
        _nextWindowStatus = DateTimeOffset.UtcNow.AddSeconds(3);
        try
        {
            var directory = WindowsLifecycle.StateDirectory;
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "window-status.json"),
                System.Text.Json.JsonSerializer.Serialize(new { UpdatedAt = DateTimeOffset.UtcNow, Status = status, Visible = visible }));
        }
        catch (System.IO.IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void ReportPosition(PixelPoint requested, CodexWindowSnapshot target)
    {
        var actual = Position;
        var key = $"{target.Bounds}:{requested}:{actual}:{_settingsWindow?.IsVisible}";
        if (key == _lastPositionReport) return;
        _lastPositionReport = key;
        try
        {
            var directory = OperatingSystem.IsWindows() ? WindowsLifecycle.StateDirectory
                : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUsageHud");
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "layout-status.json"),
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    UpdatedAt = DateTimeOffset.UtcNow,
                    TargetBounds = target.Bounds,
                    RequestedHudPosition = requested,
                    ActualHudPosition = actual,
                    HudWidth = Width,
                    HudHeight = Height,
                    FontSize = _appearance.FontSize,
                    Arrangement = _appearance.IsVertical ? "Vertical" : "Horizontal",
                    TextLineCount = (_usage.Text ?? "").Split('\n').Length,
                    TextColor = _appearance.TextColor,
                    SettingsVisible = _settingsWindow?.IsVisible ?? false,
                    Anchor = _appearance.Anchor.ToString(),
                    OffsetX = _appearance.OffsetX,
                    OffsetY = _appearance.OffsetY,
                }));
        }
        catch (System.IO.IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void ConfigureNativeWindow()
    {
        if (_target is not null) _platformHud.ConfigureNativeWindow(this, _target);
    }
}
