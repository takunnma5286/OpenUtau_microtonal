using System;
using System.Globalization;
using System.Text;
using Avalonia.Data.Converters;

namespace OpenUtau.App.ViewModels {
    public class CultureNameConverter : IValueConverter {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            if (value is CultureInfo cultureInfo) {
                return cultureInfo == CultureInfo.InvariantCulture ? ThemeManager.GetString("languages.invariant") : cultureInfo.NativeName;
            }
            return string.Empty;
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class EncodingNameConverter : IValueConverter {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => (value as Encoding)?.EncodingName ?? string.Empty;
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
    public class LinearScaleConverter : IValueConverter {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            if (value is double v && double.TryParse(parameter?.ToString(), out double scale)) {
                return v * scale;
            }
            return 0.0;
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
