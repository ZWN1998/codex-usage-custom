using Avalonia;
using CodexUsage.Core.Ui;
using CodexUsage.Core.Window;

namespace CodexUsage.Desktop.Platform;

internal static class WindowsHudPlacement
{
    internal static bool IsTitlebar(HudAppearance appearance) =>
        appearance.Anchor is HudAnchor.TopCenter or HudAnchor.TopLeft or HudAnchor.TopRight;

    internal static (int Left, int Right, int Top, int Height) Titlebar(CodexWindowSnapshot target)
    {
        var scale = Math.Max(1, target.DisplayScale);
        var inset = WindowsOverlayInterop.GetMaximizedTopInset(target.NativeHandle, target.Bounds.Top);
        var top = target.Bounds.Top + inset;
        var height = (int)Math.Round(40 * scale);
        var right = target.Bounds.Right - (int)Math.Round(146 * scale);
        if (target.CaptionButtons is { Width: > 0, Height: > 0 } buttons)
        {
            top = Math.Max(top, target.Bounds.Top + buttons.Top);
            height = buttons.Height;
            right = target.Bounds.Left + buttons.Left - (int)Math.Round(8 * scale);
        }
        var left = Math.Min(right, target.Bounds.Left + (int)Math.Round(300 * scale));
        return (left, right, top, height);
    }

    internal static double AvailableWidth(CodexWindowSnapshot target, HudAppearance appearance)
    {
        var scale = Math.Max(1, target.DisplayScale);
        var bar = Titlebar(target);
        return IsTitlebar(appearance) ? Math.Max(0, bar.Right - bar.Left) / scale
            : Math.Max(0, target.Bounds.Width / scale - 32);
    }

    internal static PixelPoint Position(CodexWindowSnapshot target, int width, int height, HudAppearance appearance)
    {
        var scale = Math.Max(1, target.DisplayScale);
        if (!IsTitlebar(appearance))
        {
            var point = HudPlacement.Position(target.Bounds, width, height, scale, appearance);
            return new PixelPoint(point.X, point.Y);
        }

        var bar = Titlebar(target);
        var x = appearance.Anchor switch
        {
            HudAnchor.TopLeft => bar.Left,
            HudAnchor.TopRight => bar.Right - width,
            _ => target.Bounds.Left + (target.Bounds.Width - width) / 2,
        };
        x += (int)Math.Round(appearance.OffsetX * scale);
        var y = bar.Top + Math.Max(0, (bar.Height - height) / 2)
            + (int)Math.Round(appearance.OffsetY * scale);
        return new PixelPoint(
            Math.Clamp(x, bar.Left, Math.Max(bar.Left, bar.Right - width)),
            Math.Clamp(y, target.Bounds.Top, Math.Max(target.Bounds.Top, target.Bounds.Bottom - height)));
    }
}
