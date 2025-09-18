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

        // Services/MaterialJsonDataSource.cs
        public IEnumerable<MaterialModel> LoadMaterials()
        {
            EnsureSeeded(_jsonFilePath);
            try
            {
                var json = File.Exists(_jsonFilePath) ? File.ReadAllText(_jsonFilePath) : "[]";
                return JsonConvert.DeserializeObject<List<MaterialModel>>(json) ?? new List<MaterialModel>();
            }
            catch { return new List<MaterialModel>(); }
        }

        private static void EnsureSeeded(string targetPath)
        {
            bool needsSeed = !File.Exists(targetPath) || new FileInfo(targetPath).Length == 0;
            if (!needsSeed) return;

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            var seedFrom = AppPaths.FindFirstNonEmptyDefault(
                "materials.json",
                "defaultMaterials.json",
                "defaultMaterial.json"
            );

            if (seedFrom != null)
            {
                File.Copy(seedFrom, targetPath, overwrite: false);
            }
            else
            {
                File.WriteAllText(targetPath, "[]");
            }
        }


        public void SaveMaterials(IEnumerable<MaterialModel> materials)
        {
            var json = JsonConvert.SerializeObject(materials, Formatting.Indented);
            AppPaths.AtomicWrite(_jsonFilePath, json);
        }



        //public IEnumerable<MaterialModel> LoadMaterials()
        //{
        //    // Seed user file if missing or empty
        //    EnsureSeeded(_jsonFilePath);

        //    try
        //    {
        //        var json = File.Exists(_jsonFilePath) ? File.ReadAllText(_jsonFilePath) : "[]";
        //        var items = JsonConvert.DeserializeObject<List<MaterialModel>>(json);
        //        return items ?? new List<MaterialModel>();
        //    }
        //    catch
        //    {
        //        return new List<MaterialModel>();
        //    }
        //}

        //private static void EnsureSeeded(string targetPath)
        //{
        //    // If user file doesn't exist or is empty, copy from shipped defaults
        //    bool needsSeed = !File.Exists(targetPath) ||
        //                     new FileInfo(targetPath).Length == 0;

        //    if (!needsSeed) return;

        //    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        //    var primary = Path.Combine(AppPaths.ShippedDefaultsDir, "materials.json");        // packaged
        //    var fallback = Path.Combine(AppPaths.ShippedDefaultsDir, "defaultMaterials.json"); // older name

        //    var seedFrom = File.Exists(primary) ? primary
        //                : File.Exists(fallback) ? fallback
        //                : null;

        //    if (seedFrom != null)
        //    {
        //        File.Copy(seedFrom, targetPath, overwrite: false);
        //    }
        //    else
        //    {
        //        // last resort: create empty array so we don't crash
        //        File.WriteAllText(targetPath, "[]");
        //    }
        //}

        //public void SaveMaterials(IEnumerable<MaterialModel> materials)
        //{
        //    var json = JsonConvert.SerializeObject(materials, Formatting.Indented);
        //    AppPaths.AtomicWrite(_jsonFilePath, json);
        //}
    }
}
