using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ADLMRateGen.Services;
using System.Windows.Data;

namespace ADLMRateGen.Converters
{
	public class CurrencyDisplayConverter : IValueConverter
	{
		public object? Convert(object value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value == null) return null;

			var amount = System.Convert.ToDouble(value);          // NGN
			var rate = CurrencyService.Instance.Rate;

			var conv = amount * rate;                             // NGN → selected

			var curr = CurrencyService.Instance.Code;   // was “Selected”


			//var curr = CurrencyService.Instance.Selected;
			return string.Format(culture, "{0} {1:N2}", curr, conv);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();
	}
}
