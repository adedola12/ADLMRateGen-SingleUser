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
        public LabourJsonDataSource() : this(AppPaths.LaboursFile) { }

        public LabourJsonDataSource(string jsonFilePath)
        {
            _jsonFilePath = jsonFilePath;
        }

        public IEnumerable<LabourModel> LoadLabours()
        {
            // Seed from packaged defaults on first run
            AppPaths.TrySeedFromPackaged(AppPaths.PackagedLaboursFile, _jsonFilePath);

            try
            {
                if (!File.Exists(_jsonFilePath))
                    return new List<LabourModel>();

                var json = File.ReadAllText(_jsonFilePath);
                var items = JsonConvert.DeserializeObject<List<LabourModel>>(json);
                return items ?? new List<LabourModel>();
            }
            catch
            {
                return new List<LabourModel>();
            }
        }

        public void SaveLabours(IEnumerable<LabourModel> labours)
        {
            var json = JsonConvert.SerializeObject(labours, Formatting.Indented);
            AppPaths.AtomicWrite(_jsonFilePath, json);
        }
    }
}
