using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.Services
{
    /// <summary>
    /// Converts server DTO [{ sn, description, unit, price }] into
    /// local Material/Labour JSON and persists to AppData.
    /// </summary>
    public static class DataSourceCloudSync
    {
        public static void SaveMaterialsFromDto(JsonElement arr)
        {
            var list = new ObservableCollection<MaterialModel>();

            if (arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in arr.EnumerateArray())
                {
                    int sn = TryInt(e, "sn");
                    string name = TryString(e, "description");
                    string unit = TryString(e, "unit");
                    decimal price = TryDecimal(e, "price");

                    list.Add(new MaterialModel
                    {
                        SerialNumber = sn > 0 ? sn : list.Count + 1,
                        MaterialName = name,
                        MaterialUnit = unit,
                        MaterialPrice = price,
                        MaterialCategory = string.Empty
                    });
                }
            }

            var ds = new MaterialJsonDataSource(AppPaths.MaterialLibraryFile);
            ds.SaveMaterials(list);
            MaterialLibraryService.Initialize(ds);
        }

        public static void SaveLaboursFromDto(JsonElement arr)
        {
            var list = new ObservableCollection<LabourModel>();

            if (arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in arr.EnumerateArray())
                {
                    int sn = TryInt(e, "sn");
                    string name = TryString(e, "description");
                    string unit = TryString(e, "unit");
                    decimal price = TryDecimal(e, "price");

                    list.Add(new LabourModel
                    {
                        SerialNumber = sn > 0 ? sn : list.Count + 1,
                        LabourName = name,
                        LabourUnit = unit,
                        LabourPrice = price,
                        LabourCategory = string.Empty
                    });
                }
            }

            var ds = new LabourJsonDataSource(AppPaths.LabourLibraryFile);
            ds.SaveLabours(list);
            LabourLibraryService.Initialize(ds);
        }

        public static void ApplyMaterialPricesFromDto(JsonElement arr)
        {
            var ds = new MaterialJsonDataSource(AppPaths.MaterialLibraryFile);
            var local = new ObservableCollection<MaterialModel>(ds.LoadMaterials());

            // Make a fast lookup by description
            var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            if (arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in arr.EnumerateArray())
                {
                    var name = TryString(e, "description");
                    var price = TryDecimal(e, "price");
                    if (!string.IsNullOrWhiteSpace(name))
                        map[name] = price;
                }
            }

            // Apply ONLY price updates where description matches
            foreach (var m in local)
            {
                if (m?.MaterialName == null) continue;
                if (map.TryGetValue(m.MaterialName, out var newPrice))
                    m.MaterialPrice = newPrice; // ← only price is updated
            }

            ds.SaveMaterials(local);
            MaterialLibraryService.Initialize(ds);
        }

        public static void ApplyLabourPricesFromDto(JsonElement arr)
        {
            var ds = new LabourJsonDataSource(AppPaths.LabourLibraryFile);
            var local = new ObservableCollection<LabourModel>(ds.LoadLabours());

            var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            if (arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in arr.EnumerateArray())
                {
                    var name = TryString(e, "description");
                    var price = TryDecimal(e, "price");
                    if (!string.IsNullOrWhiteSpace(name))
                        map[name] = price;
                }
            }

            foreach (var l in local)
            {
                if (l?.LabourName == null) continue;
                if (map.TryGetValue(l.LabourName, out var newPrice))
                    l.LabourPrice = newPrice; // ← only price is updated
            }

            ds.SaveLabours(local);
            LabourLibraryService.Initialize(ds);
        }

        // Optional helpers if you want to apply a *factor* instead of per-item prices:
        public static void ApplyMaterialFactor(decimal factor)
        {
            var ds = new MaterialJsonDataSource(AppPaths.MaterialLibraryFile);
            var local = new ObservableCollection<MaterialModel>(ds.LoadMaterials());
            foreach (var m in local) m.MaterialPrice = decimal.Round(m.MaterialPrice * factor, 2);
            ds.SaveMaterials(local);
            MaterialLibraryService.Initialize(ds);
        }

        public static void ApplyLabourFactor(decimal factor)
        {
            var ds = new LabourJsonDataSource(AppPaths.LabourLibraryFile);
            var local = new ObservableCollection<LabourModel>(ds.LoadLabours());
            foreach (var l in local) l.LabourPrice = decimal.Round(l.LabourPrice * factor, 2);
            ds.SaveLabours(local);
            LabourLibraryService.Initialize(ds);
        }

        // ----------------- small JSON readers -----------------
        private static string TryString(JsonElement e, string name)
            => e.TryGetProperty(name, out var p) ? (p.GetString() ?? string.Empty) : string.Empty;

        private static decimal TryDecimal(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var p)) return 0m;
            if (p.ValueKind != JsonValueKind.Number) return 0m;
            if (p.TryGetDecimal(out var d)) return d;
            if (p.TryGetDouble(out var dbl)) return (decimal)dbl;
            if (p.TryGetInt64(out var i64)) return i64;
            if (p.TryGetInt32(out var i32)) return i32;
            return 0m;
        }

        // ----------------- small helpers -----------------
        private static int TryInt(JsonElement e, string name)
            => e.TryGetProperty(name, out var p) && p.TryGetInt32(out var v) ? v : 0;

    }
}
