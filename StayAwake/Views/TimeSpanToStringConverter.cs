using System.Globalization;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;

namespace StayAwake.Views;

public sealed class TimeSpanToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
            return ts.ToString(@"hh\:mm");
        return "00:00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value?.ToString()?.Trim() ?? "";
        if (TimeSpan.TryParseExact(text, new[] { @"h\:mm", @"hh\:mm", @"h\:mm\:ss", @"hh\:mm\:ss" }, culture, out var ts))
            return ts;
        return Binding.DoNothing;
    }
}
