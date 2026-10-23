using System.Text.Json;
using System.Text.Json.Serialization;
using CodexUsage.Core.Window;

namespace CodexUsage.Core.Ui;

public enum HudAnchor { TopCenter, TopLeft, TopRight, LeftCenter, BottomLeft, BottomRight }

public enum HudArrangement { Auto, Horizontal, Vertical }

public sealed record HudAppearance(
    HudAnchor Anchor = HudAnchor.TopCenter,
    int OffsetX = 0,
    int OffsetY = 0,
    string TextColor = "#8C879F",
    double FontSize = 11,
    HudArrangement Arrangement = HudArrangement.Auto)
{
    [JsonIgnore]
    public bool IsVertical => Arrangement == HudArrangement.Vertical
        || (Arrangement == HudArrangement.Auto && Anchor == HudAnchor.LeftCenter);

    [JsonIgnore]
    public double RowHeight => Math.Max(22, Math.Ceiling(NormalizeFontSize(FontSize) * 1.4) + 6);

    public static double NormalizeFontSize(double size) => double.IsFinite(size) ? Math.Clamp(size, 8, 24) : 11;

    public HudAppearance Normalize() => this with
    {
        Anchor = Enum.IsDefined(Anchor) ? Anchor : HudAnchor.TopCenter,
        Arrangement = Enum.IsDefined(Arrangement) ? Arrangement : HudArrangement.Auto,
        FontSize = NormalizeFontSize(FontSize),
        OffsetX = Math.Clamp(OffsetX, -2000, 2000),
        OffsetY = Math.Clamp(OffsetY, -2000, 2000),
        TextColor = TryNormalizeColor(TextColor, out var color) ? color : "#8C879F",
    };

    public static bool TryNormalizeColor(string? input, out string color)
    {
        var hex = input?.Trim();
        if (hex?.StartsWith('#') == true) hex = hex[1..];
        if (hex is { Length: 6 } && hex.All(char.IsAsciiHexDigit))
        {
            color = "#" + hex.ToUpperInvariant();
            return true;
        }
        color = "#8C879F";
        return false;
    }
}

public readonly record struct HudPoint(int X, int Y);

public static class HudPlacement
{
    public static HudPoint Position(ScreenRect bounds, int width, int height, double scale, HudAppearance appearance)
    {
        appearance = appearance.Normalize();
        scale = double.IsFinite(scale) ? Math.Max(1, scale) : 1;
        var margin = (int)Math.Round(16 * scale);
        var titleInset = (int)Math.Round(120 * scale);
        var top = bounds.Top + Math.Max(0, ((int)Math.Round(44 * scale) - height) / 2);
        var x = appearance.Anchor switch
        {
            HudAnchor.TopLeft => bounds.Left + titleInset,
            HudAnchor.TopRight => bounds.Right - titleInset - width,
            HudAnchor.LeftCenter or HudAnchor.BottomLeft => bounds.Left + margin,
            HudAnchor.BottomRight => bounds.Right - margin - width,
            _ => bounds.Left + (bounds.Width - width) / 2,
        };
        var y = appearance.Anchor switch
        {
            HudAnchor.LeftCenter => bounds.Top + (bounds.Height - height) / 2,
            HudAnchor.BottomLeft or HudAnchor.BottomRight => bounds.Bottom - margin - height,
            _ => top,
        };
        x += (int)Math.Round(appearance.OffsetX * scale);
        y += (int)Math.Round(appearance.OffsetY * scale);
        return new HudPoint(
            Math.Clamp(x, bounds.Left, Math.Max(bounds.Left, bounds.Right - width)),
            Math.Clamp(y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - height)));
    }
}

public sealed class HudAppearanceStore(string path)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public HudAppearance Read()
    {
        try { return (JsonSerializer.Deserialize<HudAppearance>(File.ReadAllText(path), Options) ?? new()).Normalize(); }
        catch (IOException) { return new(); }
        catch (UnauthorizedAccessException) { return new(); }
        catch (JsonException) { return new(); }
    }

    public void Write(HudAppearance appearance)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(appearance.Normalize(), Options));
        File.Move(temporary, path, overwrite: true);
    }
}
