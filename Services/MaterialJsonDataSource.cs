using System.Collections.Generic;
using System.IO;
using System.Linq;
using ADLMRateGen.Helpers;
using ADLMRateGen.ViewModel.Model;
using Newtonsoft.Json;

namespace ADLMRateGen.Services
{
    public class MaterialJsonDataSource : IMaterialDataSource
    {
        private readonly string _jsonFilePath;

        // New: parameterless ctor uses the stable AppData path
        public MaterialJsonDataSource() : this(AppPaths.MaterialsFile) { }

        public MaterialJsonDataSource(string jsonFilePath)
        {
            _jsonFilePath = jsonFilePath;
        }

        public IEnumerable<MaterialModel> LoadMaterials()
        {
            // Seed from packaged defaults on first run
            AppPaths.TrySeedFromPackaged(AppPaths.PackagedMaterialsFile, _jsonFilePath);

            try
            {
                if (!File.Exists(_jsonFilePath))
                    return new List<MaterialModel>();

                var json = File.ReadAllText(_jsonFilePath);
                var items = JsonConvert.DeserializeObject<List<MaterialModel>>(json);
                return items ?? new List<MaterialModel>();
            }
            catch
            {
                return new List<MaterialModel>();
            }
        }

        public void SaveMaterials(IEnumerable<MaterialModel> materials)
        {
            var json = JsonConvert.SerializeObject(materials, Formatting.Indented);
            AppPaths.AtomicWrite(_jsonFilePath, json);
        }
    }
}
