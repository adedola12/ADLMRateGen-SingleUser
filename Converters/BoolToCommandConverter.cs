using System;
using System.Globalization;
using System.Windows;           // ← Add this
using System.Windows.Data;

namespace ADLMRateGen.Converters
{
	public class BoolToCommandConverter : IValueConverter
	{
		public object Convert(object v, Type t, object param, CultureInfo _)
		{
			var parts = (param as string)?.Split('|');
			// Now Application.Current is recognized:
			var vm = Application.Current.MainWindow.DataContext;
			var propName = (v is bool b && b) ? parts[0] : parts[1];
			return vm?.GetType().GetProperty(propName)
					  ?.GetValue(vm);
		}
		//public object ConvertBack(...) => throw new NotImplementedException();

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
