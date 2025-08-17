using ADLMRateGen.Services;
using OfficeOpenXml;
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
        //ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
            DataMigrator.EnsureMigrated();
            MaterialLibraryService.Initialize();   
            LabourLibraryService.Initialize();

            //// IMPORTANT: load both libraries now
            //MaterialLibraryService.Initialize(new MaterialJsonDataSource());
            //LabourLibraryService.Initialize(new LabourJsonDataSource());

            //// where you Initialize(...)
            //MaterialLibraryService.Initialize(
            //    new MaterialJsonDataSource(ADLMRateGen.Helpers.AppPaths.MaterialLibraryFile));

            //LabourLibraryService.Initialize(
            //    new LabourJsonDataSource(ADLMRateGen.Helpers.AppPaths.LabourLibraryFile));

            //// --- FIX: if AppData file is empty, import the shipped labour.json ---
            //if (!LabourLibraryService.GetAllLabourNames().Any())
            //{
            //    var shippedLabourPath1 = System.IO.Path.Combine(AppContext.BaseDirectory, "Defaults", "labour.json");
            //    var shippedLabourPath2 = System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "labour.json"); // fallback
            //    var path = System.IO.File.Exists(shippedLabourPath1) ? shippedLabourPath1 : shippedLabourPath2;

            //    var seed = new LabourJsonDataSource(path).LoadLabours();
            //    LabourLibraryService.AddOrUpdateLabours(seed);   // writes to AppData path
            //}


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
