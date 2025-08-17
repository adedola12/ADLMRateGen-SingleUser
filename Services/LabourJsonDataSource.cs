using System.Collections.Generic;
using System.IO;
using System.Linq;
using ADLMRateGen.Helpers;
using ADLMRateGen.ViewModel.Model;
using Newtonsoft.Json;

namespace ADLMRateGen.Services
{
    public class LabourJsonDataSource : ILabourDataSource
    {
        private readonly string _jsonFilePath;

        // New: parameterless ctor uses the stable AppData path
        public LabourJsonDataSource() : this(AppPaths.LabourLibraryFile) { }

        public LabourJsonDataSource(string jsonFilePath)
        {
            _jsonFilePath = jsonFilePath;
        }

        public IEnumerable<LabourModel> LoadLabours()
        {
            // First-run seed (kept)
            AppPaths.TrySeedFromPackaged(AppPaths.PackagedLaboursFile, _jsonFilePath);

            // Legacy fallback (kept)
            if (!File.Exists(_jsonFilePath))
            {
                var legacy = Path.Combine(AppContext.BaseDirectory, "Data", "defaultLabours.json");
                AppPaths.TrySeedFromPackaged(legacy, _jsonFilePath);
            }

            List<LabourModel> items = new();

            try
            {
                if (File.Exists(_jsonFilePath))
                {
                    var json = File.ReadAllText(_jsonFilePath);
                    items = JsonConvert.DeserializeObject<List<LabourModel>>(json) ?? new();
                }
            }
            catch { items = new(); }

            // NEW: if it’s empty, try to reseed from packaged defaults
            if (items.Count == 0)
            {
                var packaged = AppPaths.PackagedLaboursFile;
                var legacy = Path.Combine(AppContext.BaseDirectory, "Data", "defaultLabours.json");

                if (File.Exists(packaged) || File.Exists(legacy))
                {
                    var src = File.Exists(packaged) ? packaged : legacy;
                    Directory.CreateDirectory(Path.GetDirectoryName(_jsonFilePath)!);
                    File.Copy(src, _jsonFilePath, overwrite: true);

                    try
                    {
                        var json2 = File.ReadAllText(_jsonFilePath);
                        items = JsonConvert.DeserializeObject<List<LabourModel>>(json2) ?? new();
                    }
                    catch { /* swallow */ }
                }
            }

            return items;
        }



        public void SaveLabours(IEnumerable<LabourModel> labours)
        {
            var json = JsonConvert.SerializeObject(labours, Formatting.Indented);
            AppPaths.AtomicWrite(_jsonFilePath, json);
        }
    }
}
