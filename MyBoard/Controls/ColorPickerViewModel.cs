using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.Controls
{
    internal partial class ColorPickerViewModel : ObservableObject
    {
        private bool isSyncing;

        [ObservableProperty] private double hue;        // 0–360
        [ObservableProperty] private double saturation;  // 0–1
        [ObservableProperty] private double brightness;  // 0–1 (HSV "V")

        [ObservableProperty] private byte r;
        [ObservableProperty] private byte g;
        [ObservableProperty] private byte b;

        [ObservableProperty] private string hexColor = "#FFFFFF";

        // Fires whenever the resulting color changes, regardless of which control (square, slider, or RGB fields) triggered it — 
        // the View uses this single event to know when to redraw cursor/thumb/preview
        public event EventHandler? ColorChanged;

        partial void OnHueChanged(double value) => SyncFromHsv();
        partial void OnSaturationChanged(double value) => SyncFromHsv();
        partial void OnBrightnessChanged(double value) => SyncFromHsv();

        partial void OnRChanged(byte value) => SyncFromRgb();
        partial void OnGChanged(byte value) => SyncFromRgb();
        partial void OnBChanged(byte value) => SyncFromRgb();


        private void SyncFromHsv()
        {
            if (isSyncing) return;
            isSyncing = true;

            var (r, g, b) = HsvToRgb(Hue, Saturation, Brightness);
            R = r; G = g; B = b;
            HexColor = $"#{r:X2}{g:X2}{b:X2}";

            isSyncing = false;
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SyncFromRgb()
        {
            if (isSyncing) return;
            isSyncing = true;

            var (h, s, v) = RgbToHsv(R, G, B);
            Hue = h; Saturation = s; Brightness = v;
            HexColor = $"#{R:X2}{G:X2}{B:X2}";

            isSyncing = false;
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }


        // Loads an existing color (e.g. a board's current color) into the picker
        // without treating it as a fresh user edit
        public void LoadFromHex(string hex)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            isSyncing = true;
            R = color.R; G = color.G; B = color.B;
            var (h, s, v) = RgbToHsv(R, G, B);
            Hue = h; Saturation = s; Brightness = v;
            HexColor = hex;
            isSyncing = false;
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }


        // Standard HSV → RGB conversion
        private static (byte r, byte g, byte b) HsvToRgb(double h, double s, double v)
        {
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = v - c;

            (double r, double g, double b) = h switch
            {
                < 60 => (c, x, 0.0),
                < 120 => (x, c, 0.0),
                < 180 => (0.0, c, x),
                < 240 => (0.0, x, c),
                < 300 => (x, 0.0, c),
                _ => (c, 0.0, x)
            };

            return ((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }


        // Standard RGB → HSV conversion
        private static (double h, double s, double v) RgbToHsv(byte r, byte g, byte b)
        {
            double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
            double max = Math.Max(rf, Math.Max(gf, bf));
            double min = Math.Min(rf, Math.Min(gf, bf));
            double delta = max - min;

            double hue = 0;
            if (delta != 0)
            {
                if (max == rf) hue = 60 * (((gf - bf) / delta) % 6);
                else if (max == gf) hue = 60 * (((bf - rf) / delta) + 2);
                else hue = 60 * (((rf - gf) / delta) + 4);
            }
            if (hue < 0) hue += 360;

            double saturation = max == 0 ? 0 : delta / max;
            double value = max;

            return (hue, saturation, value);
        }
    }
}
