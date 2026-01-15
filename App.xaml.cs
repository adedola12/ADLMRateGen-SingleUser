using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using OfficeOpenXml;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ADLMRateGen
{
    public partial class App : Application
    {
        private readonly Uri _lightUri = new Uri("/Resources/ADLMStylesTheme.xaml", UriKind.Relative);
        private readonly Uri _darkUri = new Uri("/Resources/ADLMStylesDarkTheme.xaml", UriKind.Relative);

        private ResourceDictionary? _lightDict;
        private readonly ResourceDictionary _darkDict = new(); // created once
        private bool _isDark; // false = light, true = dark

        private static string LogDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "ADLMRateGen", "Logs");

        private static string NewLogFilePath()
        {
            Directory.CreateDirectory(LogDir);
            return Path.Combine(LogDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1) Global exception hooks
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            base.OnStartup(e);

            try
            {
                // 2) ✅ EPPlus 8+ license MUST be set via ExcelPackage.License (NOT LicenseContext)
                // Choose ONE of these based on your usage:

                // Noncommercial / personal testing:
                ExcelPackage.License.SetNonCommercialOrganization("ADLM Studio (ADLMRateGen)");

                // Commercial use (uncomment and put your key):
                // ExcelPackage.License.SetCommercial("<YOUR_EPPLUS_LICENSE_KEY>");

                // 3) Migrations + data initialization
                DataMigrator.EnsureMigrated();

                MaterialLibraryService.Initialize(new MaterialJsonDataSource(AppPaths.MaterialLibraryFile));
                LabourLibraryService.Initialize(new LabourJsonDataSource(AppPaths.LabourLibraryFile));

                Debug.WriteLine($"[PATH] Using materials: {AppPaths.MaterialLibraryFile}");
                Debug.WriteLine($"[PATH] Using labour   : {AppPaths.LabourLibraryFile}");

                // 4) Seed materials if empty (safe)
                try
                {
                    if (!MaterialLibraryService.GetAllMaterials().Any())
                    {
                        var seedPath = AppPaths.FindFirstNonEmptyDefault(
                            "materials.json", "defaultMaterials.json", "defaultMaterial.json"
                        );

                        if (seedPath != null)
                        {
                            var shipped = new MaterialJsonDataSource(seedPath).LoadMaterials().ToList();
                            if (shipped.Any())
                            {
                                MaterialLibraryService.AddOrUpdateMaterials(shipped);

                                // re-load from the main library file to refresh in-memory state
                                MaterialLibraryService.Initialize(new MaterialJsonDataSource(AppPaths.MaterialLibraryFile));

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
                }
                catch (Exception seedEx)
                {
                    Debug.WriteLine($"[SEED] Failed: {seedEx}");
                }

                // 5) Theme dictionary references (safe)
                _lightDict = FindMergedDictionaryByEndsWith("ADLMStylesTheme.xaml");
                if (_lightDict == null)
                {
                    _lightDict = new ResourceDictionary { Source = _lightUri };
                    Resources.MergedDictionaries.Add(_lightDict);
                }

                _darkDict.Source = _darkUri; // prepare but do not merge initially
                _isDark = false;

                // 6) Show main window
                var wnd = new MainWindow();
                MainWindow = wnd;
                wnd.Show();
            }
            catch (Exception ex)
            {
                ReportFatal(ex, "OnStartup");
                Shutdown(-1);
            }
        }

        private ResourceDictionary? FindMergedDictionaryByEndsWith(string endsWith)
        {
            foreach (var d in Resources.MergedDictionaries)
            {
                var src = d.Source?.OriginalString ?? "";
                if (src.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase))
                    return d;
            }
            return null;
        }

        public void ToggleTheme()
        {
            if (_lightDict == null) return;

            var merged = Resources.MergedDictionaries;

            if (_isDark) // dark -> light
            {
                if (merged.Contains(_darkDict)) merged.Remove(_darkDict);
                if (!merged.Contains(_lightDict)) merged.Add(_lightDict);
            }
            else // light -> dark
            {
                if (merged.Contains(_lightDict)) merged.Remove(_lightDict);
                if (!merged.Contains(_darkDict)) merged.Add(_darkDict);
            }

            _isDark = !_isDark;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ReportFatal(e.Exception, "DispatcherUnhandledException");
            e.Handled = true;
            Shutdown(-1);
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                ReportFatal(ex, "AppDomain.UnhandledException");
            else
                ReportFatal(new Exception("Unknown fatal error"), "AppDomain.UnhandledException");

            Shutdown(-1);
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            ReportFatal(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        }

        public static void ReportFatal(Exception ex, string where)
        {
            var path = NewLogFilePath();

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("ADLM Rate Gen - Fatal Error");
                sb.AppendLine($"Where: {where}");
                sb.AppendLine($"Time:  {DateTime.Now:O}");
                sb.AppendLine();
                sb.AppendLine(ex.ToString());

                File.WriteAllText(path, sb.ToString());

                MessageBox.Show(
                    "ADLM Rate Gen could not start.\n\n" +
                    $"A crash log has been saved to:\n{path}\n\n" +
                    "Please send this log to admin/support.",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch
            {
                try
                {
                    MessageBox.Show(
                        "ADLM Rate Gen could not start due to a fatal error.",
                        "Startup Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch { }
            }

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", LogDir) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
