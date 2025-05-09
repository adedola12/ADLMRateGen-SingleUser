using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ADLMRateGen.Converters
{
	public class CountToBoolConverter : IValueConverter
	{
		public object? Convert(object value, Type targetType,
							   object parameter, CultureInfo culture) =>
			value is int n && n > 0;

		public object ConvertBack(object value, Type t, object p,
								  CultureInfo c) => throw new NotImplementedException();
	}
}
