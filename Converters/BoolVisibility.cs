// Converters/BoolVisibility.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ADLMRateGen.Converters
{
	public class BoolToVis : IValueConverter          // True  → Visible,  False → Collapsed
	{
		public object Convert(object value, Type t, object p, CultureInfo c)
			=> (bool)value ? Visibility.Visible : Visibility.Collapsed;
		public object ConvertBack(object v, Type t, object p, CultureInfo c)
			=> (Visibility)v == Visibility.Visible;
	}

	public class InverseBoolToVis : IValueConverter   // True  → Collapsed, False → Visible
	{
		public object Convert(object value, Type t, object p, CultureInfo c)
			=> (bool)value ? Visibility.Collapsed : Visibility.Visible;
		public object ConvertBack(object v, Type t, object p, CultureInfo c)
			=> (Visibility)v != Visibility.Visible;
	}

	/// <summary>
	/// Converts a bool to a GridLength for sidebar collapse:
	/// false (expanded) → parameter width (default 250), true (collapsed) → 60px.
	/// </summary>
	public class BoolToGridLengthConverter : IValueConverter
	{
		public object Convert(object value, Type t, object p, CultureInfo c)
		{
			bool collapsed = value is bool b && b;
			double expanded = 250;
			if (p is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
				expanded = v;
			return new GridLength(collapsed ? 60 : expanded);
		}

		public object ConvertBack(object v, Type t, object p, CultureInfo c)
			=> Binding.DoNothing;
	}
}
