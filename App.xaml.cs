using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using OfficeOpenXml;
using System;
using System.Diagnostics;
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
            // App.xaml.cs  OnStartup(...)
            DataMigrator.EnsureMigrated();

            MaterialLibraryService.Initialize(new MaterialJsonDataSource(AppPaths.MaterialLibraryFile));
            LabourLibraryService.Initialize(new LabourJsonDataSource(AppPaths.LabourLibraryFile));

            Debug.WriteLine($"[PATH] Using materials: {AppPaths.MaterialLibraryFile}");
            Debug.WriteLine($"[PATH] Using labour   : {AppPaths.LabourLibraryFile}");

            // App.xaml.cs  inside OnStartup after Initialize(...)
            if (!MaterialLibraryService.GetAllMaterials().Any())
            {
                var seedPath = AppPaths.FindFirstNonEmptyDefault("materials.json", "defaultMaterials.json", "defaultMaterial.json");
                if (seedPath != null)
                {
                    var shipped = new MaterialJsonDataSource(seedPath).LoadMaterials().ToList();
                    if (shipped.Any())
                    {
                        MaterialLibraryService.AddOrUpdateMaterials(shipped);
                        MaterialLibraryService.Initialize(); // reload + raise changed
                        Debug.WriteLine($"[SEED] Imported {shipped.Count} materials from {seedPath}");
                    }
                    else
                    {
                        Debug.WriteLine($"[SEED] {seedPath} existed but contained 0 rows (after parse).");
                    }
                }
                else
                {
                    Debug.WriteLine("[SEED] No non-empty shipped materials file found.");
                }
            }




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
