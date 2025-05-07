using System.Windows;

namespace ADLMRateGen.Converters
{
	/// <summary>
	/// Very small helper used by BoolToCommandConverter to
	/// reach a FrameworkElement whose DataContext is the view‑model.
	/// </summary>
	internal static class ConverterHelper
	{
		/// <returns>
		/// The active <see cref="Window"/> if one exists;
		/// otherwise <see cref="Application.MainWindow"/>.
		/// </returns>
		public static FrameworkElement TryGetFrameworkElement()
		{
			// Prefer the window that currently has focus
			var active = Application.Current?
								   .Windows
								   .OfType<Window>()
								   .FirstOrDefault(w => w.IsActive);

			return active ?? Application.Current?.MainWindow;
		}
	}
}
