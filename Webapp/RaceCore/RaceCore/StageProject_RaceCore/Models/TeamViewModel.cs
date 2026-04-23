using System;
using System.Collections.Generic;

namespace StageProject_RaceCore.Models
{
    public class TeamIndexViewModel
    {
        public List<TeamViewModel> Teams { get; set; } = new();
        public List<Cyclist> AvailableCyclists { get; set; } = new();
    }

    public class TeamViewModel
    {
        public const int TunicPoints = 10;
        private const string DefaultTeamColor = "#6b7280";

        private static readonly Dictionary<string, string> BrandColorsByTag = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ADC"] = "#00a7e1",
            ["ARK"] = "#00a651",
            ["AST"] = "#00a3e0",
            ["BOH"] = "#56b947",
            ["COF"] = "#e11d48",
            ["DAT"] = "#2563eb",
            ["DQT"] = "#0054a6",
            ["DSM"] = "#ff6f00",
            ["IGD"] = "#b91c1c",
            ["INS"] = "#b91c1c",
            ["IPT"] = "#2563eb",
            ["IWA"] = "#f59e0b",
            ["JAY"] = "#2563eb",
            ["LOT"] = "#d61f45",
            ["LTK"] = "#d62d20",
            ["MOV"] = "#0047ab",
            ["Q36"] = "#334155",
            ["RBH"] = "#1e3a8a",
            ["SOQ"] = "#0054a6",
            ["TBV"] = "#c81e1e",
            ["TDE"] = "#69be28",
            ["TDT"] = "#69be28",
            ["TFS"] = "#d62d20",
            ["TJV"] = "#facc15",
            ["TUD"] = "#8b1e2d",
            ["UAE"] = "#c81e1e",
            ["UXM"] = "#d61f45",
            ["UXT"] = "#d61f45",
            ["VIS"] = "#facc15"
        };

        private static readonly (string Fragment, string Color)[] BrandColorsByName =
        {
            ("uae", "#c81e1e"),
            ("visma", "#facc15"),
            ("jumbo", "#facc15"),
            ("soudal", "#0054a6"),
            ("quick-step", "#0054a6"),
            ("alpecin", "#00a7e1"),
            ("arkea", "#00a651"),
            ("astana", "#00a3e0"),
            ("bahrain", "#c81e1e"),
            ("bora", "#56b947"),
            ("cofidis", "#e11d48"),
            ("decathlon", "#2563eb"),
            ("ag2r", "#2563eb"),
            ("dsm", "#ff6f00"),
            ("ineos", "#b91c1c"),
            ("intermarche", "#f59e0b"),
            ("israel", "#2563eb"),
            ("jayco", "#2563eb"),
            ("lidl", "#d62d20"),
            ("trek", "#d62d20"),
            ("lotto", "#d61f45"),
            ("movistar", "#0047ab"),
            ("q36", "#334155"),
            ("red bull", "#1e3a8a"),
            ("tudor", "#8b1e2d"),
            ("totalenergies", "#69be28"),
            ("uno-x", "#d61f45")
        };

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Color { get; set; } = DefaultTeamColor;

        public int ActiveCyclistsCount { get; set; }
        public int BenchCyclistsCount { get; set; }

        public int TeamPoints => ActiveCyclistsCount * TunicPoints;
        public string ColorSoft => ToRgba(Color, 0.14);
        public string ColorSoftBorder => ToRgba(Color, 0.3);
        public string ColorContrast => GetReadableTextColor(Color);

        public List<CyclistSimple> ActiveCyclists { get; set; } = new();
        public List<CyclistSimple> BenchCyclists { get; set; } = new();

        public static string ResolveBrandColor(string? tag, string? name)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                var normalizedTag = tag.Trim();
                if (BrandColorsByTag.TryGetValue(normalizedTag, out var tagColor))
                {
                    return tagColor;
                }
            }

            var normalizedName = (name ?? string.Empty).Trim();
            foreach (var brandColor in BrandColorsByName)
            {
                if (normalizedName.Contains(brandColor.Fragment, StringComparison.OrdinalIgnoreCase))
                {
                    return brandColor.Color;
                }
            }

            var fallbackKey = string.IsNullOrWhiteSpace(tag) ? normalizedName : tag!;
            return BuildFallbackColor(fallbackKey);
        }

        private static string BuildFallbackColor(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return DefaultTeamColor;
            }

            uint hash = 2166136261;
            foreach (var character in key.ToUpperInvariant())
            {
                hash ^= character;
                hash *= 16777619;
            }

            var hue = (int)(hash % 360);
            return HslToHex(hue, 65, 46);
        }

        private static string ToRgba(string color, double alpha)
        {
            if (!TryParseHexColor(color, out var red, out var green, out var blue))
            {
                return $"rgba(107, 114, 128, {alpha:0.##})";
            }

            return $"rgba({red}, {green}, {blue}, {alpha:0.##})";
        }

        private static string GetReadableTextColor(string color)
        {
            if (!TryParseHexColor(color, out var red, out var green, out var blue))
            {
                return "#ffffff";
            }

            var luminance =
                (0.2126 * ToLinear(red)) +
                (0.7152 * ToLinear(green)) +
                (0.0722 * ToLinear(blue));

            return luminance > 0.45 ? "#111827" : "#ffffff";
        }

        private static double ToLinear(int channel)
        {
            var value = channel / 255d;
            return value <= 0.03928
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        private static string HslToHex(double hue, double saturationPercent, double lightnessPercent)
        {
            var saturation = saturationPercent / 100d;
            var lightness = lightnessPercent / 100d;

            var chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
            var huePrime = hue / 60d;
            var x = chroma * (1 - Math.Abs((huePrime % 2) - 1));

            double red1 = 0;
            double green1 = 0;
            double blue1 = 0;

            if (huePrime < 1)
            {
                red1 = chroma;
                green1 = x;
            }
            else if (huePrime < 2)
            {
                red1 = x;
                green1 = chroma;
            }
            else if (huePrime < 3)
            {
                green1 = chroma;
                blue1 = x;
            }
            else if (huePrime < 4)
            {
                green1 = x;
                blue1 = chroma;
            }
            else if (huePrime < 5)
            {
                red1 = x;
                blue1 = chroma;
            }
            else
            {
                red1 = chroma;
                blue1 = x;
            }

            var match = lightness - (chroma / 2);
            var red = (int)Math.Round((red1 + match) * 255);
            var green = (int)Math.Round((green1 + match) * 255);
            var blue = (int)Math.Round((blue1 + match) * 255);

            return $"#{red:X2}{green:X2}{blue:X2}";
        }

        private static bool TryParseHexColor(string? color, out int red, out int green, out int blue)
        {
            red = 0;
            green = 0;
            blue = 0;

            if (string.IsNullOrWhiteSpace(color))
            {
                return false;
            }

            var value = color.Trim();
            if (value.StartsWith("#", StringComparison.Ordinal))
            {
                value = value[1..];
            }

            if (value.Length == 3)
            {
                value = string.Concat(value[0], value[0], value[1], value[1], value[2], value[2]);
            }

            if (value.Length != 6)
            {
                return false;
            }

            if (!int.TryParse(value[..2], System.Globalization.NumberStyles.HexNumber, null, out red))
            {
                return false;
            }

            if (!int.TryParse(value.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out green))
            {
                return false;
            }

            return int.TryParse(value.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out blue);
        }
    }

    public class CyclistSimple
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
