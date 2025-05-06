using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ADLMRateGen.Converters
{
	public class BoolToStringConverter : IValueConverter
	{
		public object Convert(object v, Type t, object param, CultureInfo _)
		{
			var parts = (param as string)?.Split('|');
			return (v is bool b && b)
				 ? parts?[0]
				 : parts?[1];
		}
		
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
