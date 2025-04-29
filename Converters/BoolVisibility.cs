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
}
