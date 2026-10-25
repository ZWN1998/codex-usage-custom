using Avalonia;
using CodexUsage.Desktop.Platform;

namespace CodexUsage.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (OperatingSystem.IsWindows())
        {
            if (args.Contains("--stop")) { WindowsLifecycle.Stop(); return; }
            if (args.Contains("--disable-autostart")) { WindowsLifecycle.SetAutoStart(false); return; }
            if (args.Contains("--watch"))
            { WindowsLifecycle.Watch(args.Contains("--enable-autostart")); return; }
            using var singleInstance = WindowsLifecycle.AcquireHud(out var first);
            if (!first)
            { if (args.Contains("--settings")) WindowsLifecycle.OpenSettings(); return; }
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return;
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .With(new MacOSPlatformOptions
        {
            ShowInDock = false,
        })
        .UsePlatformDetect();
}
