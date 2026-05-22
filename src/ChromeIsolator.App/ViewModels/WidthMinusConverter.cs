using System.Globalization;
using System.Windows.Data;

namespace ChromeIsolator.ViewModels;

public sealed class WidthMinusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double width)
        {
            return value;
        }

        var minus = 0.0;
        if (parameter is string text)
        {
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out minus);
        }

        return Math.Max(0, width - minus);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
