using System.Globalization;
using System.Windows.Data;

namespace ADLMRateGen.Converters
{
	public class FirstNameConverter : IValueConverter
	{
		public object? Convert(object value, Type t, object p, CultureInfo c)
			=> value is string s && s.Contains(' ')
			   ? s[..s.IndexOf(' ')]      // text before first space
			   : value;

		public object? ConvertBack(object v, Type t, object p, CultureInfo c)
			=> throw new NotImplementedException();
	}
}
