using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace ADLMCivilPlugin.Converters
{
    public class ContainsStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            string input = value as string;
            string substring = parameter as string;
            if (!string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(substring))
            {
                if (input.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0)
                    return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
