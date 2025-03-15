using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace ADLMRateGen.Helpers
{
	public class InverseBooleanToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool flag = (bool)value;
			return flag ? Visibility.Collapsed : Visibility.Visible;
		}
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return ((Visibility)value) != Visibility.Visible;
		}
	}
}
