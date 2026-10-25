using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CodexUsage.Core.Ui;
using CodexUsage.Core.Usage;
using CodexUsage.Core.Window;
using CodexUsage.Desktop;
using CodexUsage.Desktop.Ui;
using CodexUsage.Desktop.Platform;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Real Avalonia text/font measurement, without showing a window or
        // connecting to Codex. The application lifetime is deliberately absent.
        AppBuilder.Configure<App>()
            .With(new MacOSPlatformOptions { ShowInDock = false })
            .UsePlatformDetect().SetupWithoutStarting();
        var now = DateTimeOffset.UtcNow;
        var model = new HudViewModel();
        model.SetUsage(new UsageSnapshot(new(72, 28, now.AddHours(3).AddMinutes(57), 300),
            new(77, 23, now.AddDays(6).AddHours(3), 10080), now));
        double previousWidth = 0;
        double previousHeight = 0;
        foreach (var font in new[] { 8d, 11d, 24d })
        {
            var hud = new TitlebarHudWindow(model, new HiddenTracker(), () => { },
                new HudAppearance(HudAnchor.LeftCenter, FontSize: font));
            var panel = (StackPanel)((Border)hud.Content!).Child!;
            var text = (TextBlock)panel.Children[0];
            var refresh = (Border)panel.Children[1];
            Check(panel.Orientation == Orientation.Vertical, "left side uses a vertical control stack");
            Check(text.Text!.Split('\n').Length == 6, "both quota blocks and countdowns remain visible");
            Check(text.Text.Contains("72%") && text.Text.Contains("77%"), "real model percentages remain visible");
            Check(text.FontSize == font, "font control changes actual rendered text size");
            Check(hud.Width < hud.Height, "left HUD is a narrow upright strip");
            Check(hud.Height >= text.DesiredSize.Height + refresh.Height + 4, "large font and refresh button are not clipped");
            Check(hud.Width >= previousWidth && hud.Height > previousHeight, "column expands as font size increases");
            previousWidth = hud.Width;
            previousHeight = hud.Height;
            Console.WriteLine($"Vertical font {font}: {hud.Width} x {hud.Height}, 6 text lines");
            hud.Close();
        }
        foreach (var font in new[] { 8d, 11d, 24d })
        {
            var hud = new TitlebarHudWindow(model, new HiddenTracker(), () => { }, new HudAppearance(FontSize: font));
            var panel = (StackPanel)((Border)hud.Content!).Child!;
            var text = (TextBlock)panel.Children[0];
            Check(panel.Orientation == Orientation.Horizontal, "top remains a horizontal control stack");
            Check(!text.Text!.Contains('\n'), "top remains a single line at every font size");
            Check(hud.Height == new HudAppearance(FontSize: font).RowHeight, "height follows requested typography");
            Console.WriteLine($"Horizontal font {font}: {hud.Width} x {hud.Height}, 1 text line");
            hud.Close();
        }
        if (OperatingSystem.IsWindows())
        {
            foreach (var scale in new[] { 1d, 1.5d, 2d })
            {
                var left = -1920;
                var top = 40;
                var screenWidth = (int)(1600 * scale);
                var bounds = new ScreenRect(left, top, left + screenWidth, top + (int)(900 * scale));
                var buttons = new ScreenRect(screenWidth - (int)(138 * scale), 0, screenWidth, (int)(40 * scale));
                var snapshot = new CodexWindowSnapshot(0, bounds, buttons, scale, true, false, false);
                var width = (int)(360 * scale);
                var height = (int)(22 * scale);
                foreach (var anchor in Enum.GetValues<HudAnchor>())
                {
                    var appearance = new HudAppearance(anchor);
                    var point = WindowsHudPlacement.Position(snapshot, width, height, appearance);
                    Check(point.X >= bounds.Left && point.Y >= bounds.Top, "negative-origin monitor placement stays inside target");
                    Check(point.X + width <= bounds.Right && point.Y + height <= bounds.Bottom, "placement respects right/bottom bounds");
                    if (WindowsHudPlacement.IsTitlebar(appearance))
                        Check(point.X + width < bounds.Left + buttons.Left, "titlebar anchors avoid native caption buttons");
                }
                var centered = WindowsHudPlacement.Position(snapshot, width, height, new());
                Check(centered.X == bounds.Left + (bounds.Width - width) / 2, "default anchor centers on Codex at each DPI");
                var moved = WindowsHudPlacement.Position(snapshot, width, height, new(OffsetX: 10, OffsetY: 5));
                Check(moved.X - centered.X == (int)Math.Round(10 * scale), "horizontal offset scales with target DPI");
                Check(moved.Y - centered.Y == (int)Math.Round(5 * scale), "vertical offset scales with target DPI");
                var extreme = WindowsHudPlacement.Position(snapshot, width, height, new(OffsetX: 2000, OffsetY: -2000));
                Check(extreme.X + width <= bounds.Left + buttons.Left && extreme.Y >= bounds.Top, "extreme offsets are clamped safely");
                Console.WriteLine($"Windows placement: six anchors, offsets, caption safety and negative monitor origin at {scale * 100}% DPI passed.");
            }
        }
        Console.WriteLine("Native text measurement and layout checks passed; no test windows were shown.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class HiddenTracker : ICodexWindowTracker
    {
        public bool TryGetSnapshot(out CodexWindowSnapshot? snapshot) { snapshot = null; return false; }
    }
}
