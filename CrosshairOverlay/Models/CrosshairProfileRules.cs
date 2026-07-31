#nullable enable
using System;

namespace CrosshairOverlay.Models
{
    /// <summary>
    /// Shared validation rules for profiles consumed by both the WPF app and Widget.
    /// This file intentionally uses C# 8-compatible syntax so the UWP project can compile it.
    /// </summary>
    public static class CrosshairProfileRules
    {
        public const int MinThickness = 1;
        public const int MaxThickness = 20;
        public const int MinSize = 2;
        public const int MaxSize = 200;
        public const int MaxGap = 100;
        public const int MinDotSize = 1;
        public const int MaxDotSize = 100;
        public const double MinOpacity = 0.05;
        public const double MaxOpacity = 1.0;
        public const int MinOutlineThickness = 1;
        public const int MaxOutlineThickness = 10;

        public static CrosshairProfile Sanitize(CrosshairProfile profile)
        {
            if (profile == null)
                return new CrosshairProfile();

            var size = Clamp(profile.Size, MinSize, MaxSize);
            var opacity = profile.Opacity;
            if (double.IsNaN(opacity) || double.IsInfinity(opacity))
                opacity = MaxOpacity;

            return new CrosshairProfile
            {
                Style = Enum.IsDefined(typeof(CrosshairStyle), profile.Style)
                    ? profile.Style
                    : CrosshairStyle.Cross,
                Color = NormalizeColor(profile.Color, "#00FF00"),
                Thickness = Clamp(profile.Thickness, MinThickness, MaxThickness),
                Size = size,
                Gap = Clamp(profile.Gap, 0, Math.Min(MaxGap, size)),
                DotSize = Clamp(profile.DotSize, MinDotSize, MaxDotSize),
                Opacity = Clamp(opacity, MinOpacity, MaxOpacity),
                OutlineEnabled = profile.OutlineEnabled,
                OutlineColor = NormalizeColor(profile.OutlineColor, "#000000"),
                OutlineThickness = Clamp(profile.OutlineThickness, MinOutlineThickness, MaxOutlineThickness),
                ExclusiveFullscreenMode = profile.ExclusiveFullscreenMode,
                IsVisible = profile.IsVisible
            };
        }

        public static bool IsHexColor(string? value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value))
                return false;

            var color = value[0] == '#' ? value.Substring(1) : value;
            if (color.Length != 6)
                return false;

            for (var i = 0; i < color.Length; i++)
            {
                var c = color[i];
                var isHex = (c >= '0' && c <= '9')
                    || (c >= 'a' && c <= 'f')
                    || (c >= 'A' && c <= 'F');
                if (!isHex)
                    return false;
            }

            return true;
        }

        private static string NormalizeColor(string? value, string fallback)
        {
            if (value == null || !IsHexColor(value))
                return fallback;

            var color = value[0] == '#' ? value.Substring(1) : value;
            return "#" + color.ToUpperInvariant();
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
