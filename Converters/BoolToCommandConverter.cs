using System;
using System.Globalization;
using System.Windows.Data;

namespace ADLMRateGen.Converters
{
	/// <summary>
	///   ConverterParameter =  "UpdateMaterial|SaveMaterial"
	///   – returns ICommand on DataContext that matches the
	///     first or second pipe‑separated token.
	/// </summary>
	public sealed class BoolToCommandConverter : IValueConverter
	{
		public object Convert(object value, Type targetType,
							  object parameter, CultureInfo culture)
		{
			var tokens = (parameter as string)?.Split('|');
			if (tokens?.Length != 2) return null;

			var isEditing = value is bool b && b;
			var propName = isEditing ? tokens[0] : tokens[1];
			var dc = ConverterHelper.TryGetFrameworkElement()?.DataContext;
			return dc?.GetType().GetProperty(propName)?.GetValue(dc);
		}

		public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
			throw new NotSupportedException();
	}
}
