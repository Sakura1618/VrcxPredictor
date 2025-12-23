using System;
using System.Globalization;
using System.Windows.Data;

namespace VrcxPredictor.App.Converters;

public sealed class PercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
            return $"{d * 100:0.0}%";
        return "—";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
