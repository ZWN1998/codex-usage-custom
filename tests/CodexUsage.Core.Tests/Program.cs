using CodexUsage.Core.Reset;
using CodexUsage.Core.Ui;
using CodexUsage.Core.Usage;
using L = CodexUsage.Core.Localization.Localization;
using System.Reflection;
using System.Text.Json;

var now = new DateTimeOffset(2026, 8, 28, 20, 0, 0, TimeSpan.Zero);
var resolver = new ResetStateResolver();
var emptyState = new PersistedCompanionState();

var announcement = new ResetAnnouncement(
    "event-1",
    "regular",
    now - TimeSpan.FromMinutes(10),
    "Public reset announcement",
    new ResetSource("x_post", "thsottiaux", "https://example.test/event-1"));
var publicStatus = new PublicResetStatus(
    new ResetStatusData(announcement, null),
    new ResetStatusMeta("v1", now));

AssertEqual(
    ResetVisualState.Unknown,
    resolver.Resolve(new PublicResetFetch(false, publicStatus, now), null, null, emptyState, now).State,
    "offline public source is unknown");

AssertEqual(
    ResetVisualState.Announced,
    resolver.Resolve(new PublicResetFetch(true, publicStatus, now), null, null, emptyState, now).State,
    "announcement without local evidence remains amber");

var previous = Snapshot(30, 25, now - TimeSpan.FromMinutes(15), now + TimeSpan.FromHours(4), now + TimeSpan.FromDays(4));
var refreshed = Snapshot(98, 96, now - TimeSpan.FromMinutes(1), now + TimeSpan.FromHours(5), now + TimeSpan.FromDays(7));
AssertEqual(
    ResetVisualState.Confirmed,
    resolver.Resolve(new PublicResetFetch(true, publicStatus, now), previous, refreshed, emptyState, now).State,
    "large simultaneous non-natural refresh confirms green");

var naturallyDuePrevious = Snapshot(
    30,
    25,
    now - TimeSpan.FromMinutes(15),
    now - TimeSpan.FromMinutes(2),
    now - TimeSpan.FromMinutes(2));
AssertEqual(
    ResetVisualState.Announced,
    resolver.Resolve(
        new PublicResetFetch(true, publicStatus, now),
        naturallyDuePrevious,
        refreshed,
        emptyState,
        now).State,
    "natural reset timing blocks green attribution");

var oldStatus = publicStatus with
{
    Data = publicStatus.Data with { LatestReset = announcement with { AnnouncedAt = now - TimeSpan.FromHours(10) } },
};
AssertEqual(
    ResetVisualState.Neutral,
    resolver.Resolve(new PublicResetFetch(true, oldStatus, now), null, null, emptyState, now).State,
    "old announcement returns to neutral");

var watch = new ResetWatch(
    "strong",
    80,
    "next two hours",
    now - TimeSpan.FromMinutes(5),
    now + TimeSpan.FromHours(1),
    "Strong watch",
    new ResetSource("x_post", "thsottiaux", "https://example.test/watch"));
var watchStatus = new PublicResetStatus(
    new ResetStatusData(null, watch),
    new ResetStatusMeta("v1", now));
AssertEqual(
    ResetVisualState.Announced,
    resolver.Resolve(new PublicResetFetch(true, watchStatus, now), null, null, emptyState, now).State,
    "active strong watch is amber");

AssertEqual(
    QuotaStatusLevel.Normal,
    QuotaStatusEvaluator.EvaluateFiveHour(Window(21, now + TimeSpan.FromHours(5), 300)),
    "five-hour above 20 percent is normal");
AssertEqual(
    QuotaStatusLevel.Amber,
    QuotaStatusEvaluator.EvaluateFiveHour(Window(20, now + TimeSpan.FromHours(5), 300)),
    "five-hour 10 to 20 percent is amber");
AssertEqual(
    QuotaStatusLevel.Red,
    QuotaStatusEvaluator.EvaluateFiveHour(Window(10, now + TimeSpan.FromHours(5), 300)),
    "five-hour 10 percent or less is red");
AssertEqual(
    QuotaStatusLevel.Normal,
    QuotaStatusEvaluator.EvaluateWeekly(Window(80, now + TimeSpan.FromDays(7), 10_080), now),
    "weekly runway of 0.8 is normal");
AssertEqual(
    QuotaStatusLevel.Amber,
    QuotaStatusEvaluator.EvaluateWeekly(Window(60, now + TimeSpan.FromDays(7), 10_080), now),
    "weekly runway between 0.5 and 0.8 is amber");
AssertEqual(
    QuotaStatusLevel.Red,
    QuotaStatusEvaluator.EvaluateWeekly(Window(49, now + TimeSpan.FromDays(7), 10_080), now),
    "weekly runway below 0.5 is red");
AssertEqual(
    QuotaStatusLevel.Normal,
    QuotaStatusEvaluator.EvaluateWeekly(Window(40, now + TimeSpan.FromDays(3.5), 10_080), now),
    "weekly quota tracks remaining time proportionally");
AssertEqual(
    QuotaStatusLevel.Normal,
    QuotaStatusEvaluator.EvaluateWeekly(Window(10, now - TimeSpan.FromMinutes(1), 10_080), now),
    "invalid weekly reset falls back to normal");
AssertEqual(
    QuotaStatusLevel.Normal,
    QuotaStatusEvaluator.EvaluateWeekly(new UsageWindow(10, 90, null, 10_080), now),
    "missing weekly reset falls back to normal");
AssertEqual(
    QuotaStatusLevel.Normal,
    QuotaStatusEvaluator.EvaluateWeekly(Window(10, now + TimeSpan.FromDays(8), 10_080), now),
    "weekly reset beyond its window falls back to normal");

AssertEqual(
    7.84m,
    ParseUsageSnapshot("""
        {
          "rateLimits": { "primary": { "usedPercent": 20 }, "secondary": { "usedPercent": 40 } },
          "rateLimitsByLimitId": {
            "codex": {
              "primary": { "usedPercent": 10, "windowDurationMins": 300 },
              "secondary": { "usedPercent": 20, "windowDurationMins": 10080 },
              "credits": { "hasCredits": false, "unlimited": false, "balance": "7.84" }
            }
          }
        }
        """).Credits?.Balance ?? -1m,
    "codex credit balance string takes priority over hasCredits");

AssertEqual(
    3.5m,
    ParseUsageSnapshot("""
        {
          "rateLimits": {
            "primary": { "usedPercent": 10, "windowDurationMins": 300 },
            "secondary": { "usedPercent": 20, "windowDurationMins": 10080 },
            "credits": { "unlimited": false, "balance": 3.5 }
          }
        }
        """).Credits?.Balance ?? -1m,
    "rate limits credits is the fallback");

AssertEqual(
    true,
    ParseUsageSnapshot("""
        {
          "rateLimits": {
            "primary": { "usedPercent": 10, "windowDurationMins": 300 },
            "secondary": { "usedPercent": 20, "windowDurationMins": 10080 },
            "credits": { "unlimited": true, "balance": "7.84" }
          }
        }
        """).Credits is null,
    "unlimited accounts do not show a purchased credit balance");

foreach (var locale in new[] { "en", "de", "ja", "fr", "es" })
{
    L.SetLocale(locale);
    AssertEqual(true, L.Get("UsageRemaining").Length > 0, $"{locale} usage title is available");
    AssertEqual(true, L.Get("CreditCount", 176).Length > 0, $"{locale} credit count is available");
    AssertEqual(true, L.Get("ViewSource").Length > 0, $"{locale} source link is available");
    AssertEqual(true, HudViewModel.FormatUsageReset(Window(50, now + TimeSpan.FromHours(2), 300), now).Length > 0,
        $"{locale} reset formatting is available");
}
L.SetLocale("en");

AssertEqual("4小时51分钟后重置", TitlebarText.Countdown(now.AddHours(4).AddMinutes(51), now), "hours and minutes stay inline");
AssertEqual("6天15小时后重置", TitlebarText.Countdown(now.AddDays(6).AddHours(15), now), "weekly reset stays a countdown");
AssertEqual("即将重置", TitlebarText.Countdown(now.AddSeconds(59), now), "sub-minute reset");
AssertEqual("等待更新", TitlebarText.Countdown(now, now), "elapsed reset waits for actual usage");
AssertEqual("重置时间未知", TitlebarText.Countdown(null, now), "missing reset is explicit");
AssertEqual("6天15时", TitlebarText.Countdown(now.AddDays(6).AddHours(15), now, true), "narrow-window countdown");
AssertEqual("5小时 94% · 4小时51分钟后重置 · 每周 97% · 6天15小时后重置",
    TitlebarText.Usage(Snapshot(94, 97, now, now.AddHours(4).AddMinutes(51), now.AddDays(6).AddHours(15)), now),
    "single line matches the supplied reference");
AssertEqual("5小时 94% · 每周 97%",
    TitlebarText.Usage(Snapshot(94, 97, now, now.AddHours(4), now.AddDays(6)), now, 2),
    "narrowest mode preserves both quota values");
AssertEqual(false, TitlebarText.Usage(null, now).Contains("100%"), "missing usage never fabricates full quota");
AssertEqual(true, TitlebarText.Details(refreshed, null).Contains("时区："), "hover includes timezone");
AssertEqual(true, TitlebarText.Details(refreshed, "读取失败").Contains("读取失败"), "hover retains refresh failure");
var frame = new CodexUsage.Core.Window.ScreenRect(0, 0, 1920, 1080);
AssertEqual(new HudPoint(798, 11), HudPlacement.Position(frame, 324, 22, 1, new()), "default keeps corrected fullscreen baseline");
AssertEqual(new HudPoint(120, 11), HudPlacement.Position(frame, 324, 22, 1, new(HudAnchor.TopLeft)), "top left reserves toolbar controls");
AssertEqual(new HudPoint(1476, 11), HudPlacement.Position(frame, 324, 22, 1, new(HudAnchor.TopRight)), "top right reserves toolbar controls");
AssertEqual(new HudPoint(16, 529), HudPlacement.Position(frame, 324, 22, 1, new(HudAnchor.LeftCenter)), "left side anchor");
AssertEqual(new HudPoint(16, 1042), HudPlacement.Position(frame, 324, 22, 1, new(HudAnchor.BottomLeft)), "bottom left anchor");
AssertEqual(new HudPoint(1580, 1042), HudPlacement.Position(frame, 324, 22, 1, new(HudAnchor.BottomRight)), "bottom right anchor");
AssertEqual(new HudPoint(828, 6), HudPlacement.Position(frame, 324, 22, 1, new(OffsetX: 30, OffsetY: -5)), "manual offsets apply relative to anchor");
AssertEqual(new HudPoint(0, 1058), HudPlacement.Position(frame, 324, 22, 1, new(OffsetX: -2000, OffsetY: 2000)), "manual offsets cannot lose the HUD outside window");
AssertEqual(new HudPoint(-1122, -89), HudPlacement.Position(new(-1920, -100, 0, 980), 324, 22, 1, new()), "negative monitor coordinates remain valid");
AssertEqual(new HudPoint(1596, 22), HudPlacement.Position(new(0, 0, 3840, 2160), 648, 44, 2, new()), "DPI scales titlebar and controls consistently");
AssertEqual(true, HudAppearance.TryNormalizeColor(" 8c879f ", out var normalizedColor), "custom RGB accepts case and surrounding spaces");
AssertEqual("#8C879F", normalizedColor, "color normalizes for saving");
AssertEqual(false, HudAppearance.TryNormalizeColor("##8C879F", out _), "multiple hash symbols rejected");
AssertEqual(false, HudAppearance.TryNormalizeColor("#ZZ1234", out _), "invalid hexadecimal rejected");
AssertEqual("#8C879F", new HudAppearance(TextColor: "invalid").Normalize().TextColor, "corrupted colors recover to current default");
AssertEqual(HudAnchor.TopCenter, new HudAppearance(Anchor: (HudAnchor)99).Normalize().Anchor, "unknown anchors recover safely");
AssertEqual(true, new HudAppearance(HudAnchor.LeftCenter).IsVertical, "left side automatically uses a vertical strip");
AssertEqual(false, new HudAppearance(HudAnchor.TopCenter).IsVertical, "top stays horizontal by default");
AssertEqual(false, new HudAppearance(HudAnchor.LeftCenter, Arrangement: HudArrangement.Horizontal).IsVertical, "manual horizontal overrides left automatic mode");
AssertEqual(true, new HudAppearance(Arrangement: HudArrangement.Vertical).IsVertical, "manual vertical works at other positions");
AssertEqual(8d, new HudAppearance(FontSize: 2).Normalize().FontSize, "minimum readable font size");
AssertEqual(24d, new HudAppearance(FontSize: 100).Normalize().FontSize, "maximum font size stays within titlebar");
AssertEqual(11d, new HudAppearance(FontSize: double.NaN).Normalize().FontSize, "invalid numeric font size recovers");
AssertEqual(22d, new HudAppearance().RowHeight, "default typography keeps existing fullscreen alignment");
AssertEqual(40d, new HudAppearance(FontSize: 24).RowHeight, "large fonts receive enough vertical space");
AssertEqual(new HudPoint(798, 2), HudPlacement.Position(frame, 324, 40, 1, new(FontSize: 24)), "larger font stays centered in titlebar");
AssertEqual(new HudPoint(16, 478), HudPlacement.Position(frame, 55, 124, 1, new(HudAnchor.LeftCenter)), "vertical strip is vertically centered");
var verticalSample = Snapshot(94, 97, now, now.AddHours(4).AddMinutes(51), now.AddDays(6).AddHours(15));
AssertEqual("5小时\n94%\n4时51分\n每周\n97%\n6天15时", TitlebarText.VerticalUsage(verticalSample, now), "vertical mode preserves both quotas and reset countdowns");
AssertEqual(4, TitlebarText.VerticalUsage(verticalSample, now, 1).Split('\n').Length, "small windows use four vertical lines");
AssertEqual("5小时 94%\n每周 97%", TitlebarText.VerticalUsage(verticalSample, now, 2), "shortest vertical mode retains both percentages");
AssertEqual(false, TitlebarText.VerticalUsage(null, now).Contains("100%"), "missing vertical usage is not fabricated");
var appearanceDirectory = Path.Combine(Path.GetTempPath(), "codex-usage-appearance-tests-" + Guid.NewGuid().ToString("N"));
try
{
    var appearancePath = Path.Combine(appearanceDirectory, "appearance.json");
    var appearanceStore = new HudAppearanceStore(appearancePath);
    AssertEqual(new HudAppearance(), appearanceStore.Read(), "first run preserves current purple centered default");
    var selection = new HudAppearance(HudAnchor.LeftCenter, 34, -27, "#748395");
    appearanceStore.Write(selection);
    AssertEqual(selection, appearanceStore.Read(), "position offsets and color survive restart");
    appearanceStore.Write(selection with { Anchor = HudAnchor.BottomRight, OffsetX = 5000 });
    AssertEqual(2000, appearanceStore.Read().OffsetX, "saved offsets stay bounded");
    var fontSelection = selection with { FontSize = 18, Arrangement = HudArrangement.Vertical };
    appearanceStore.Write(fontSelection);
    AssertEqual(fontSelection, appearanceStore.Read(), "font size and orientation persist together with position and color");
    File.WriteAllText(appearancePath, "{\"Anchor\":\"LeftCenter\",\"OffsetX\":51,\"OffsetY\":11,\"TextColor\":\"#978578\"}");
    var migrated = appearanceStore.Read();
    AssertEqual(11d, migrated.FontSize, "older preferences receive the existing font size");
    AssertEqual(HudArrangement.Auto, migrated.Arrangement, "older preferences opt into automatic orientation");
    AssertEqual("#978578", migrated.TextColor, "older custom colors are preserved");
    AssertEqual(51, migrated.OffsetX, "older offsets are preserved");
    File.WriteAllText(appearancePath, "{bad json");
    AssertEqual(new HudAppearance(), appearanceStore.Read(), "damaged preferences do not prevent startup");
}
finally
{
    if (Directory.Exists(appearanceDirectory)) Directory.Delete(appearanceDirectory, recursive: true);
}
Console.WriteLine("Core, titlebar and appearance settings tests passed.");

static UsageSnapshot Snapshot(
    int fiveHourRemaining,
    int weeklyRemaining,
    DateTimeOffset observedAt,
    DateTimeOffset fiveHourReset,
    DateTimeOffset weeklyReset) => new(
    new UsageWindow(fiveHourRemaining, 100 - fiveHourRemaining, fiveHourReset, 300),
    new UsageWindow(weeklyRemaining, 100 - weeklyRemaining, weeklyReset, 10_080),
    observedAt);

static UsageWindow Window(int remaining, DateTimeOffset resetsAt, long durationMinutes) =>
    new(remaining, 100 - remaining, resetsAt, durationMinutes);

static UsageSnapshot ParseUsageSnapshot(string json)
{
    using var document = JsonDocument.Parse(json);
    var parseSnapshot = typeof(CodexRateLimitsClient).GetMethod(
        "ParseSnapshot",
        BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Usage parser was not found.");
    return (UsageSnapshot)(parseSnapshot.Invoke(null, [document.RootElement])
        ?? throw new InvalidOperationException("Usage parser returned null."));
}

static void AssertEqual<T>(T expected, T actual, string name) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
    }
}
