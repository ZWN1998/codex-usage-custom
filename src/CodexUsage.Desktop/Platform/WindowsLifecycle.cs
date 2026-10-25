using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;

namespace CodexUsage.Desktop.Platform;

// A user-session watcher, started by Explorer/Startup, outlives Codex and its jobs.
// It owns only its HUD child. A clean HUD exit is respected until Codex restarts.
[SupportedOSPlatform("windows")]
internal static class WindowsLifecycle
{
    private const string WatcherMutex = @"Local\CodexUsageCustom.Windows.Watcher";
    private const string HudMutex = @"Local\CodexUsageCustom.Windows.Hud";
    private const string StopWatcherEvent = @"Local\CodexUsageCustom.Windows.StopWatcher";
    private const string StopHudEvent = @"Local\CodexUsageCustom.Windows.StopHud";
    private const string SettingsEvent = @"Local\CodexUsageCustom.Windows.Settings";
    private const string ResumeEvent = @"Local\CodexUsageCustom.Windows.Resume";
    internal static string StateDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "usage-custom-windows");
    internal static string InstalledExecutable => Path.Combine(StateDirectory, "package", "plugins",
        "codex-usage-custom", "bin", "win-x64", "CodexUsage.Desktop.exe");
    private static string EnabledFlag => Path.Combine(StateDirectory, "autostart-enabled");
    private static string StartupLink => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Codex Usage Custom.lnk");
    internal static bool AutoStartEnabled => File.Exists(EnabledFlag) && File.Exists(StartupLink);

    internal static EventWaitHandle StopHudSignal() => new(false, EventResetMode.AutoReset, StopHudEvent);
    internal static EventWaitHandle SettingsSignal() => new(false, EventResetMode.AutoReset, SettingsEvent);

    private static void Signal(string name)
    {
        try { using var signal = EventWaitHandle.OpenExisting(name); signal.Set(); }
        catch (WaitHandleCannotBeOpenedException) { }
    }

    internal static void Stop()
    {
        Signal(StopWatcherEvent);
        Signal(StopHudEvent);
    }

    internal static void OpenSettings() => Signal(SettingsEvent);

    internal static Mutex AcquireHud(out bool first) => new(true, HudMutex, out first);

    internal static void SetAutoStart(bool enabled)
    {
        Directory.CreateDirectory(StateDirectory);
        if (!enabled)
        {
            File.Delete(EnabledFlag);
            File.Delete(StartupLink);
            return; // The watcher notices this and leaves the current HUD usable.
        }

        var executable = File.Exists(InstalledExecutable) ? InstalledExecutable : Environment.ProcessPath!;
        object? shellObject = null;
        object? linkObject = null;
        try
        {
            shellObject = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            dynamic shell = shellObject!;
            linkObject = shell.CreateShortcut(StartupLink);
            dynamic link = linkObject;
            link.TargetPath = executable;
            link.Arguments = "--watch";
            link.WorkingDirectory = Path.GetDirectoryName(executable);
            link.WindowStyle = 7;
            link.Description = "Follow Codex with the customizable usage HUD.";
            link.Save();
            File.WriteAllText(EnabledFlag, "enabled");
        }
        finally
        {
            if (linkObject is not null) Marshal.FinalReleaseComObject(linkObject);
            if (shellObject is not null) Marshal.FinalReleaseComObject(shellObject);
        }
    }

    internal static void EnsureWatcher()
    {
        var executable = File.Exists(InstalledExecutable) ? InstalledExecutable : Environment.ProcessPath!;
        using var process = Process.Start(new ProcessStartInfo(executable, "--watch")
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
    }

    internal static void Watch(bool enableAutoStart)
    {
        if (enableAutoStart) SetAutoStart(true);
        using var mutex = new Mutex(true, WatcherMutex, out var first);
        if (!first) { Signal(ResumeEvent); return; }
        using var stop = new EventWaitHandle(false, EventResetMode.AutoReset, StopWatcherEvent);
        using var resume = new EventWaitHandle(false, EventResetMode.AutoReset, ResumeEvent);
        if (!File.Exists(EnabledFlag)) return;
        // Repair our own login entry, without deleting any unrelated startup items.
        SetAutoStart(true);
        Process? codex = null;
        Process? hud = null;
        var suppressed = false;
        var retryAt = DateTimeOffset.MinValue;
        var stopRequested = false;
        try
        {
            while (!(stopRequested = stop.WaitOne(0)) && File.Exists(EnabledFlag))
            {
                try
                {
                    if (resume.WaitOne(0)) { suppressed = false; retryAt = DateTimeOffset.MinValue; }
                    if (codex is not null && codex.HasExited)
                    {
                        StopChild(hud); hud = null;
                        codex.Dispose(); codex = null;
                        suppressed = false;
                        retryAt = DateTimeOffset.MinValue;
                    }
                    codex ??= FindCodex();
                    if (hud is not null && hud.HasExited)
                    {
                        suppressed = hud.ExitCode == 0;
                        hud.Dispose(); hud = null;
                        retryAt = DateTimeOffset.UtcNow.AddSeconds(10);
                    }
                    if (codex is not null && hud is null && !suppressed && DateTimeOffset.UtcNow >= retryAt)
                    {
                        var executable = File.Exists(InstalledExecutable) ? InstalledExecutable : Environment.ProcessPath!;
                        hud = Process.Start(new ProcessStartInfo(executable, "--hud")
                        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                            WorkingDirectory = Path.GetDirectoryName(executable) });
                        retryAt = DateTimeOffset.UtcNow.AddSeconds(10);
                    }
                    WriteStatus(codex?.Id, hud?.Id, suppressed ? "Paused after user exit"
                        : codex is null ? "Waiting for Codex" : "Following Codex");
                }
                catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
                {
                    WriteStatus(null, null, "Retrying: " + error.GetType().Name);
                    retryAt = DateTimeOffset.UtcNow.AddSeconds(10);
                }
                if (stop.WaitOne(TimeSpan.FromSeconds(3))) { stopRequested = true; break; }
            }
        }
        finally
        {
            if (stopRequested) StopChild(hud);
            else hud?.Dispose(); // Disabling autostart does not dismiss an open HUD.
            codex?.Dispose();
            WriteStatus(null, null, "Watcher stopped");
        }
    }

    private static Process? FindCodex()
    {
        var session = Process.GetCurrentProcess().SessionId;
        Process? selected = null;
        foreach (var name in new[] { "ChatGPT", "Codex" })
        foreach (var process in Process.GetProcessesByName(name))
        {
            try
            {
                if (selected is null && process.SessionId == session && process.MainWindowHandle != 0
                    && WindowsCodexWindowTracker.IsCodexProcess((uint)process.Id))
                { selected = process; continue; }
            }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException) { }
            process.Dispose();
        }
        return selected;
    }

    private static void StopChild(Process? process)
    {
        if (process is null) return;
        try
        {
            if (!process.HasExited)
            {
                Signal(StopHudEvent);
                if (!process.WaitForExit(5000)) process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        finally { process.Dispose(); }
    }

    private static void WriteStatus(int? codex, int? hud, string status)
    {
        try
        {
            Directory.CreateDirectory(StateDirectory);
            var path = Path.Combine(StateDirectory, "watcher-status.json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new
            { UpdatedAt = DateTimeOffset.UtcNow, WatcherPid = Environment.ProcessId, CodexPid = codex, HudPid = hud, Status = status }));
            File.Move(path + ".tmp", path, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
