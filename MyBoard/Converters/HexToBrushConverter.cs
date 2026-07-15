using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;

namespace MyBoard.Converters
{
    // Converts a hex color string (e.g. "#B39DDB") to a SolidColorBrush
    internal class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
                catch { return Brushes.Gray; }
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();

    }
}
