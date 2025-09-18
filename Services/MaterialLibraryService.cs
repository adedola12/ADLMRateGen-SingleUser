using System;
using System.Collections.Generic;
using System.Linq;
using ADLMRateGen.Helpers;          // ← for AppPaths
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.Services
{
    public static class MaterialLibraryService
    {
        private static readonly object _sync = new();

        public static event Action? LibraryChanged;           // NEW
        private static void RaiseChanged() => LibraryChanged?.Invoke(); // NEW

        // Default to JSON file in AppData
        private static IMaterialDataSource _dataSource =
            new MaterialJsonDataSource(AppPaths.MaterialLibraryFile);

        private static List<MaterialModel> _materials = new();

        /// <summary>
        /// Initialize the service with a data source (optional; defaults to JSON in AppData).
        /// </summary>
        public static void Initialize(IMaterialDataSource? dataSource = null)
        {
            lock (_sync)
            {
                if (dataSource != null)
                    _dataSource = dataSource;

                _materials = _dataSource.LoadMaterials().ToList();
            }
            RaiseChanged();
        }

        public static IEnumerable<string> GetAllMaterialNames()
        {
            lock (_sync)
                return _materials
                    .Select(m => m.MaterialName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        public static decimal GetPrice(string materialName)
        {
            if (string.IsNullOrWhiteSpace(materialName)) return 0m;

            lock (_sync)
            {
                var mat = _materials.FirstOrDefault(m =>
                    string.Equals(m.MaterialName, materialName, StringComparison.OrdinalIgnoreCase));
                return mat != null ? mat.MaterialPrice : 0m;
            }
        }

        public static IEnumerable<MaterialModel> GetAllMaterials()
        {
            lock (_sync) return _materials.ToList();
        }

        /// <summary>
        /// Merge incoming materials into the library (add new, update existing by MaterialName, keep user edits).
        /// Only updates known fields; add your own if needed.
        /// </summary>
        public static void AddOrUpdateMaterials(IEnumerable<MaterialModel> incoming)
        {
            if (incoming == null) return;

            lock (_sync)
            {
                var dict = _materials.ToDictionary(
                    m => m.MaterialName ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);

                foreach (var item in incoming)
                {
                    var key = item.MaterialName ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(key)) continue;

                    if (dict.TryGetValue(key, out var existing))
                    {
                        // Always update price from the incoming list
                        existing.MaterialPrice = item.MaterialPrice;

                        // Update unit/category if provided (avoid overwriting with null/empty)
                        if (!string.IsNullOrWhiteSpace(item.MaterialUnit))
                            existing.MaterialUnit = item.MaterialUnit;

                        if (!string.IsNullOrWhiteSpace(item.MaterialCategory))
                            existing.MaterialCategory = item.MaterialCategory;

                        // Keep any other user-specific fields on 'existing' intact
                    }
                    else
                    {
                        // New material entirely
                        dict[key] = item;
                    }
                }

                _materials = dict.Values.ToList();
                _dataSource.SaveMaterials(_materials);
            }
            RaiseChanged();
        }

        public static MaterialModel? FindByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            lock (_sync)
                return _materials.FirstOrDefault(m =>
                    string.Equals(m.MaterialName, name, StringComparison.OrdinalIgnoreCase));
        }

    }
}
