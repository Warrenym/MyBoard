using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;

namespace MyBoard.Services
{
    static class NoteColorPalette
    {
        // Primary + secondary colors from the color wheel, plus a "default"
        // that means "inherit the normal text color" rather than a fixed hex
        public static readonly Dictionary<string, string> TextColors = new()
        {
            ["default"] = "#3A3A3A",
            ["red"] = "#E53935",
            ["orange"] = "#FB8C00",
            ["yellow"] = "#FDD835",
            ["green"] = "#43A047",
            ["blue"] = "#1E88E5",
            ["purple"] = "#8E24AA",
        };

        public static readonly Dictionary<string, string> HighlightColors = new()
        {
            ["none"] = "Transparent",
            ["red"] = "#FADBD8",
            ["orange"] = "#FDEBD0",
            ["yellow"] = "#FCF3CF",
            ["green"] = "#D5F5E3",
            ["blue"] = "#D6EAF8",
            ["purple"] = "#E8DAEF",
        };

        public static Brush GetTextBrush(string? key) =>
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                TextColors.TryGetValue(key ?? "default", out var hex) ? hex : TextColors["default"]));

        public static Brush GetHighlightBrush(string? key) =>
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                HighlightColors.TryGetValue(key ?? "none", out var hex) ? hex : HighlightColors["none"]));
    }
}
