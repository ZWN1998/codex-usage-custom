using CodexUsage.Core.Usage;

namespace CodexUsage.Core.Ui;

public static class TitlebarText
{
    public static string Countdown(DateTimeOffset? reset, DateTimeOffset now, bool compact = false)
    {
        if (reset is null) return compact ? "--" : "重置时间未知";
        var remaining = reset.Value - now;
        if (remaining <= TimeSpan.Zero) return "等待更新";
        if (remaining.TotalMinutes < 1) return "即将重置";
        var days = (int)remaining.TotalDays;
        var hours = remaining.Hours;
        var minutes = remaining.Minutes;
        var duration = days > 0
            ? (compact ? $"{days}天{hours}时" : $"{days}天{hours}小时")
            : hours > 0
                ? (compact ? $"{hours}时{minutes}分" : $"{hours}小时{minutes}分钟")
                : $"{minutes}分钟";
        return compact ? duration : $"{duration}后重置";
    }

    public static string Usage(UsageSnapshot? usage, DateTimeOffset now, int detail = 0)
    {
        var five = $"{Label(usage?.FiveHour, "5小时")} {Percent(usage?.FiveHour)}";
        var week = $"{Label(usage?.Weekly, "每周")} {Percent(usage?.Weekly)}";
        if (detail >= 2) return $"{five} · {week}";
        var separator = detail == 0 ? " · " : "·";
        return string.Join(separator, five, Countdown(usage?.FiveHour?.ResetsAt, now, detail == 1),
            week, Countdown(usage?.Weekly?.ResetsAt, now, detail == 1));
    }

    public static string VerticalUsage(UsageSnapshot? usage, DateTimeOffset now, int detail = 0)
    {
        var five = Label(usage?.FiveHour, "5小时");
        var week = Label(usage?.Weekly, "每周");
        if (detail >= 2) return $"{five} {Percent(usage?.FiveHour)}\n{week} {Percent(usage?.Weekly)}";
        if (detail == 1) return string.Join("\n", $"{five} {Percent(usage?.FiveHour)}",
            Countdown(usage?.FiveHour?.ResetsAt, now, true), $"{week} {Percent(usage?.Weekly)}",
            Countdown(usage?.Weekly?.ResetsAt, now, true));
        return string.Join("\n", five, Percent(usage?.FiveHour), Countdown(usage?.FiveHour?.ResetsAt, now, true),
            week, Percent(usage?.Weekly), Countdown(usage?.Weekly?.ResetsAt, now, true));
    }

    private static string Percent(UsageWindow? window) => window is null ? "暂不可用" : $"{window.RemainingPercent}%";
    private static string Label(UsageWindow? window, string fallback) => window?.WindowDurationMinutes switch
        {
            null => fallback,
            300 => "5小时",
            10080 => "每周",
            var minutes when minutes % 1440 == 0 => $"{minutes / 1440}天",
            var minutes when minutes % 60 == 0 => $"{minutes / 60}小时",
            var minutes => $"{minutes}分钟",
        };

    public static string Details(UsageSnapshot? usage, string? error)
    {
        string Reset(UsageWindow? window) => window?.ResetsAt is { } reset
            ? reset.ToLocalTime().ToString("yyyy/MM/dd HH:mm zzz") : "暂不可用";
        var observed = usage is { ObservedAt: var at } && at != DateTimeOffset.MinValue
            ? at.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") : "尚未成功读取";
        return $"5小时重置：{Reset(usage?.FiveHour)}\n每周重置：{Reset(usage?.Weekly)}\n"
            + $"时区：{TimeZoneInfo.Local.Id}\n最近更新：{observed}"
            + (error is null ? "" : $"\n{error}");
    }
}
