using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using CodexUsage.Core.Ui;
using CodexUsage.Desktop.Platform;

namespace CodexUsage.Desktop.Ui;

internal sealed class HudSettingsWindow : Window
{
    private static readonly string[] Anchors = ["顶部居中", "顶部靠左", "顶部靠右", "左侧居中", "左下角", "右下角"];
    private readonly HudAppearanceStore _store;
    private readonly Action<HudAppearance> _apply;
    private readonly ComboBox _anchor;
    private readonly ComboBox _arrangement;
    private readonly NumericUpDown _fontSize;
    private readonly NumericUpDown _offsetX;
    private readonly NumericUpDown _offsetY;
    private readonly TextBox _hex;
    private readonly TextBlock _preview;
    private readonly TextBlock _status;
    private readonly Slider[] _channels = new Slider[3];
    private readonly TextBlock[] _channelValues = new TextBlock[3];
    private readonly DispatcherTimer _saveTimer;
    private HudAppearance _appearance;
    private bool _syncing;
    private bool _pendingSave;

    public HudSettingsWindow(HudAppearance appearance, HudAppearanceStore store,
        Action<HudAppearance> apply, string previewText)
    {
        _appearance = appearance;
        _store = store;
        _apply = apply;
        Title = "Codex Usage · 外观设置";
        Width = 460;
        Height = 760;
        MinWidth = 420;
        MinHeight = 560;
        CanResize = true;
        ShowInTaskbar = false;
        Topmost = OperatingSystem.IsMacOS();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#FAFAFC"));
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += (_, _) => Save();
        _status = Label("修改即时生效，并自动保存", 11, "#7D788A");
        var body = new StackPanel { Spacing = 12, Margin = new Thickness(24, 18) };
        body.Children.Add(Label("把浮窗放在顺手的位置", 19, "#393541"));
        body.Children.Add(Label("位置随 Codex 窗口移动，全屏时同样生效。", 12, "#827D8B"));
        body.Children.Add(Label("位置", 13));
        _anchor = new ComboBox { ItemsSource = Anchors, HorizontalAlignment = HorizontalAlignment.Stretch };
        _anchor.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _anchor.SelectedIndex >= 0)
                Change(_appearance with { Anchor = (HudAnchor)_anchor.SelectedIndex });
        };
        body.Children.Add(_anchor);
        body.Children.Add(Label("排列方向", 13));
        _arrangement = new ComboBox
        {
            ItemsSource = new[] { "自动：左侧竖排，顶部横排", "横排", "竖排" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _arrangement.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _arrangement.SelectedIndex >= 0)
                Change(_appearance with { Arrangement = (HudArrangement)_arrangement.SelectedIndex });
        };
        body.Children.Add(_arrangement);
        body.Children.Add(Label("字体大小（8–24）", 13));
        _fontSize = new NumericUpDown
        {
            Minimum = 8, Maximum = 24, Increment = 1, FormatString = "0", HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _fontSize.ValueChanged += (_, _) =>
        {
            if (!_syncing) Change(_appearance with { FontSize = (double)(_fontSize.Value ?? 11) });
        };
        body.Children.Add(_fontSize);
        var offsets = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,*") };
        _offsetX = MakeOffset();
        _offsetY = MakeOffset();
        _offsetX.ValueChanged += (_, _) =>
        {
            if (!_syncing) Change(_appearance with { OffsetX = (int)(_offsetX.Value ?? 0) });
        };
        _offsetY.ValueChanged += (_, _) =>
        {
            if (!_syncing) Change(_appearance with { OffsetY = (int)(_offsetY.Value ?? 0) });
        };
        var xGroup = new StackPanel { Spacing = 5, Children = { Label("左右微调", 12), _offsetX } };
        var yGroup = new StackPanel { Spacing = 5, Children = { Label("上下微调", 12), _offsetY } };
        Grid.SetColumn(yGroup, 2);
        offsets.Children.Add(xGroup);
        offsets.Children.Add(yGroup);
        body.Children.Add(offsets);
        body.Children.Add(Label("正数向右 / 向下，负数向左 / 向上；超出窗口时自动收回。", 11, "#827D8B"));
        body.Children.Add(Label("文字颜色", 13));
        var presets = new WrapPanel();
        foreach (var (name, color) in new[]
        {
            ("雾紫灰", "#8C879F"), ("蓝灰", "#7D8B99"), ("深灰", "#626875"),
            ("薰衣草", "#9585AD"), ("鼠尾草", "#758B7E"), ("暖灰", "#978578"),
        })
        {
            var button = new Button
            {
                Content = new Border { Background = new SolidColorBrush(Color.Parse(color)), Width = 32, Height = 20, CornerRadius = new CornerRadius(3) },
                Padding = new Thickness(5), Margin = new Thickness(0, 0, 8, 0),
            };
            ToolTip.SetTip(button, $"{name} {color}");
            Avalonia.Automation.AutomationProperties.SetName(button, $"{name} {color}");
            button.Click += (_, _) => Change(_appearance with { TextColor = color });
            presets.Children.Add(button);
        }
        body.Children.Add(presets);
        _hex = new TextBox { PlaceholderText = "#RRGGBB，例如 #8C879F", MaxLength = 7 };
        _hex.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            if (HudAppearance.TryNormalizeColor(_hex.Text, out var color))
                Change(_appearance with { TextColor = color }, preserveHexInput: true);
            else _status.Text = "请输入 6 位十六进制色值，例如 #8C879F";
        };
        _hex.LostFocus += (_, _) =>
        {
            if (!HudAppearance.TryNormalizeColor(_hex.Text, out _))
            {
                SyncControls();
                _status.Text = "色值无效，已保留上一次有效颜色";
            }
        };
        body.Children.Add(_hex);
        for (var i = 0; i < 3; i++)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("24,*,36") };
            var channel = i;
            _channels[i] = new Slider { Minimum = 0, Maximum = 255, TickFrequency = 1, IsSnapToTickEnabled = true };
            _channelValues[i] = Label("0", 11, "#827D8B");
            _channelValues[i].HorizontalAlignment = HorizontalAlignment.Right;
            _channels[i].PropertyChanged += (_, e) =>
            {
                if (!_syncing && e.Property == Slider.ValueProperty)
                {
                    var rgb = _channels.Select(x => (byte)Math.Round(x.Value)).ToArray();
                    Change(_appearance with { TextColor = $"#{rgb[0]:X2}{rgb[1]:X2}{rgb[2]:X2}" });
                }
            };
            Grid.SetColumn(_channels[i], 1);
            Grid.SetColumn(_channelValues[i], 2);
            row.Children.Add(Label(new[] { "R", "G", "B" }[channel], 12));
            row.Children.Add(_channels[i]);
            row.Children.Add(_channelValues[i]);
            body.Children.Add(row);
        }
        _preview = Label(previewText, 11);
        _preview.TextWrapping = TextWrapping.NoWrap;
        _preview.TextTrimming = TextTrimming.CharacterEllipsis;
        body.Children.Add(new Border
        {
            Child = new StackPanel { Spacing = 8, Children = { Label("预览", 11, "#827D8B"), _preview } },
            Background = Brushes.White, Padding = new Thickness(12), CornerRadius = new CornerRadius(8),
        });
        var reset = new Button { Content = "恢复默认" };
        reset.Click += (_, _) => Change(new HudAppearance());
        var done = new Button { Content = "完成", HorizontalAlignment = HorizontalAlignment.Right };
        done.Click += (_, _) => Close();
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(24, 4, 24, 18) };
        footer.Children.Add(reset);
        Grid.SetColumn(done, 1);
        footer.Children.Add(done);
        var layout = new DockPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(footer);
        var statusBorder = new Border { Child = _status, Margin = new Thickness(24, 4, 24, 12) };
        DockPanel.SetDock(statusBorder, Dock.Bottom);
        layout.Children.Add(statusBorder);
        layout.Children.Add(new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        Content = layout;
        SyncControls();
        Opened += (_, _) => { ConfigureForFullscreen(); _pendingSave = true; Save(); ReportVisibility(true); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Closed += (_, _) => { if (_pendingSave) Save(); _saveTimer.Stop(); ReportVisibility(false); };
    }

    public void ShowFor(Window owner)
    {
        ConfigureForFullscreen();
        if (owner.IsVisible) Show(owner);
        else { WindowStartupLocation = WindowStartupLocation.CenterScreen; Show(); }
        Activate();
    }

    public void UpdatePreview(string text) => _preview.Text = text;

    private void ReportVisibility(bool visible)
    {
        try
        {
            var directory = System.IO.Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData), "CodexUsageHud");
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "settings-status.json"),
                System.Text.Json.JsonSerializer.Serialize(new { UpdatedAt = DateTimeOffset.UtcNow, Visible = visible, Width, Height }));
        }
        catch (System.IO.IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void ConfigureForFullscreen()
    {
        if (!OperatingSystem.IsMacOS()) return;
#pragma warning disable CS0618
        var handle = TryGetPlatformHandle() as IMacOSTopLevelPlatformHandle;
#pragma warning restore CS0618
        MacOverlayInterop.ConfigureCompanion(handle?.NSWindow ?? nint.Zero);
    }

    private static NumericUpDown MakeOffset() => new()
    {
        Minimum = -2000, Maximum = 2000, Increment = 1, FormatString = "0", HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    private static TextBlock Label(string text, double size, string color = "#504B59") => new()
    {
        Text = text, FontSize = size, Foreground = new SolidColorBrush(Color.Parse(color)),
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
    };

    private void Change(HudAppearance appearance, bool preserveHexInput = false)
    {
        _appearance = appearance.Normalize();
        _apply(_appearance);
        SyncControls(preserveHexInput);
        _pendingSave = true;
        _saveTimer.Stop();
        _saveTimer.Start();
        _status.Text = "已预览，正在保存…";
    }

    private void SyncControls(bool preserveHexInput = false)
    {
        _syncing = true;
        try
        {
            _anchor.SelectedIndex = (int)_appearance.Anchor;
            _arrangement.SelectedIndex = (int)_appearance.Arrangement;
            _fontSize.Value = (decimal)_appearance.FontSize;
            _offsetX.Value = _appearance.OffsetX;
            _offsetY.Value = _appearance.OffsetY;
            if (!preserveHexInput) _hex.Text = _appearance.TextColor;
            var color = Color.Parse(_appearance.TextColor);
            var rgb = new[] { color.R, color.G, color.B };
            for (var i = 0; i < 3; i++)
            {
                _channels[i].Value = rgb[i];
                _channelValues[i].Text = rgb[i].ToString();
            }
            _preview.Foreground = new SolidColorBrush(color);
            _preview.FontSize = _appearance.FontSize;
            _preview.LineHeight = Math.Ceiling(_appearance.FontSize * 1.4);
            _preview.TextAlignment = _appearance.IsVertical ? TextAlignment.Center : TextAlignment.Left;
        }
        finally { _syncing = false; }
    }

    private void Save()
    {
        _saveTimer.Stop();
        try
        {
            _store.Write(_appearance);
            _pendingSave = false;
            _status.Text = "已保存，下次启动继续使用";
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        {
            _status.Text = "保存失败，请检查配置目录的写入权限";
        }
    }
}
