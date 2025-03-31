using System.Windows;

namespace ADLMRateGen.Helpers
{
	public class ButtonHelper : DependencyObject
	{
		public static readonly DependencyProperty IsActiveProperty =
			DependencyProperty.RegisterAttached(
				"IsActive",
				typeof(bool),
				typeof(ButtonHelper),
				new PropertyMetadata(false)
			);

		public static bool GetIsActive(DependencyObject obj)
		{
			return (bool)obj.GetValue(IsActiveProperty);
		}

		public static void SetIsActive(DependencyObject obj, bool value)
		{
			obj.SetValue(IsActiveProperty, value);
		}
	}
}
