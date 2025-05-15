using System;
using System.Linq;
using System.Windows;

namespace ADLMRateGen
{
	public partial class App : Application
	{
		private readonly Uri _lightUri = new Uri("/Resources/ADLMStylesTheme.xaml", UriKind.Relative);
		private readonly Uri _darkUri = new Uri("/Resources/ADLMStylesDarkTheme.xaml", UriKind.Relative);

		private ResourceDictionary? _lightDict;
		private readonly ResourceDictionary _darkDict = new();   // created once

		private bool _isDark;   // false = light, true = dark

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			// get reference to the light dictionary we already loaded in XAML
			_lightDict = Resources.MergedDictionaries
								   .First(d => d.Source != null &&
											   d.Source.OriginalString.EndsWith("ADLMStylesTheme.xaml",
																				StringComparison.OrdinalIgnoreCase));

			_darkDict.Source = _darkUri;   // prepare but DO NOT merge
		}

		/// <summary>Switches between the two theme dictionaries.</summary>
		public void ToggleTheme()
		{
			if (_lightDict == null) return;

			var merged = Resources.MergedDictionaries;

			if (_isDark)        // currently dark ➜ switch to light
			{
				merged.Remove(_darkDict);
				merged.Add(_lightDict);        // add last ⇒ highest precedence
			}
			else                // currently light ➜ switch to dark
			{
				merged.Remove(_lightDict);
				merged.Add(_darkDict);
			}

			_isDark = !_isDark;
		}
	}
}
