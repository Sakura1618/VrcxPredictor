using System;
using System.Globalization;
using System.Windows.Data;

namespace VrcxPredictor.App.Converters;

public sealed class BoolToTextConverter : IValueConverter
{
    public string TrueText { get; set; } = "是";
    public string FalseText { get; set; } = "否";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? (b ? TrueText : FalseText) : "—";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
