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
        //public static void SaveMaterialsFromDto(JsonElement arr)
        //{
        //    var list = new ObservableCollection<MaterialModel>();

        //    if (arr.ValueKind == JsonValueKind.Array)
        //    {
        //        foreach (var e in arr.EnumerateArray())
        //        {
        //            int sn = TryInt(e, "sn");
        //            string name = TryString(e, "description");
        //            string unit = TryString(e, "unit");
        //            decimal price = TryDecimal(e, "price");

        //            list.Add(new MaterialModel
        //            {
        //                SerialNumber = sn > 0 ? sn : list.Count + 1,
        //                MaterialName = name,
        //                MaterialUnit = unit,
        //                MaterialPrice = price,
        //                MaterialCategory = string.Empty
        //            });
        //        }
        //    }

        //    var ds = new MaterialJsonDataSource(AppPaths.MaterialLibraryFile);
        //    ds.SaveMaterials(list);
        //    MaterialLibraryService.Initialize(ds);
        //}

        //public static void SaveLaboursFromDto(JsonElement arr)
        //{
        //    var list = new ObservableCollection<LabourModel>();

        //    if (arr.ValueKind == JsonValueKind.Array)
        //    {
        //        foreach (var e in arr.EnumerateArray())
        //        {
        //            int sn = TryInt(e, "sn");
        //            string name = TryString(e, "description");
        //            string unit = TryString(e, "unit");
        //            decimal price = TryDecimal(e, "price");

        //            list.Add(new LabourModel
        //            {
        //                SerialNumber = sn > 0 ? sn : list.Count + 1,
        //                LabourName = name,
        //                LabourUnit = unit,
        //                LabourPrice = price,
        //                LabourCategory = string.Empty
        //            });
        //        }
        //    }

        //    var ds = new LabourJsonDataSource(AppPaths.LabourLibraryFile);
        //    ds.SaveLabours(list);
        //    LabourLibraryService.Initialize(ds);
        //}

        //public static void ApplyMaterialPricesFromDto(JsonElement arr)
        //{
        //    var ds = new MaterialJsonDataSource(AppPaths.MaterialLibraryFile);
        //    var local = new ObservableCollection<MaterialModel>(ds.LoadMaterials());

        //    // Make a fast lookup by description
        //    var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        //    if (arr.ValueKind == JsonValueKind.Array)
        //    {
        //        foreach (var e in arr.EnumerateArray())
        //        {
        //            var name = TryString(e, "description");
        //            var price = TryDecimal(e, "price");
        //            if (!string.IsNullOrWhiteSpace(name))
        //                map[name] = price;
        //        }
        //    }

        //    // Apply ONLY price updates where description matches
        //    foreach (var m in local)
        //    {
        //        if (m?.MaterialName == null) continue;
        //        if (map.TryGetValue(m.MaterialName, out var newPrice))
        //            m.MaterialPrice = newPrice; // ← only price is updated
        //    }

        //    ds.SaveMaterials(local);
        //    MaterialLibraryService.Initialize(ds);
        //}

        //public static void ApplyLabourPricesFromDto(JsonElement arr)
        //{
        //    var ds = new LabourJsonDataSource(AppPaths.LabourLibraryFile);
        //    var local = new ObservableCollection<LabourModel>(ds.LoadLabours());

        //    var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        //    if (arr.ValueKind == JsonValueKind.Array)
        //    {
        //        foreach (var e in arr.EnumerateArray())
        //        {
        //            var name = TryString(e, "description");
        //            var price = TryDecimal(e, "price");
        //            if (!string.IsNullOrWhiteSpace(name))
        //                map[name] = price;
        //        }
        //    }

        //    foreach (var l in local)
        //    {
        //        if (l?.LabourName == null) continue;
        //        if (map.TryGetValue(l.LabourName, out var newPrice))
        //            l.LabourPrice = newPrice; // ← only price is updated
        //    }

        //    ds.SaveLabours(local);
        //    LabourLibraryService.Initialize(ds);
        //}

        //// Optional helpers if you want to apply a *factor* instead of per-item prices:
        //public static void ApplyMaterialFactor(decimal factor)
        //{
        //    var ds = new MaterialJsonDataSource(AppPaths.MaterialLibraryFile);
        //    var local = new ObservableCollection<MaterialModel>(ds.LoadMaterials());
        //    foreach (var m in local) m.MaterialPrice = decimal.Round(m.MaterialPrice * factor, 2);
        //    ds.SaveMaterials(local);
        //    MaterialLibraryService.Initialize(ds);
        //}

        //public static void ApplyLabourFactor(decimal factor)
        //{
        //    var ds = new LabourJsonDataSource(AppPaths.LabourLibraryFile);
        //    var local = new ObservableCollection<LabourModel>(ds.LoadLabours());
        //    foreach (var l in local) l.LabourPrice = decimal.Round(l.LabourPrice * factor, 2);
        //    ds.SaveLabours(local);
        //    LabourLibraryService.Initialize(ds);
        //}

        // ----------------- small JSON readers -----------------


        /* ---------- MATERIALS ---------- */

        public static void SaveMaterialsFromDto(JsonElement dtoMaterials)
        {
            // 1) Convert master → MaterialModel list
            var master = new List<MaterialModel>();
            if (dtoMaterials.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in dtoMaterials.EnumerateArray())
                {
                    master.Add(new MaterialModel
                    {
                        SerialNumber   = el.GetProperty("sn").GetInt32(),
                        MaterialName   = el.GetProperty("description").GetString() ?? "",
                        MaterialUnit   = el.GetProperty("unit").GetString() ?? "",
                        MaterialPrice  = el.GetProperty("price").GetDecimal(),
                        MaterialCategory = el.TryGetProperty("category", out var cat)
                                           ? (cat.GetString() ?? "")
                                           : "" // master may not carry category
                    });
                }
            }

            // 2) Bring the latest user rows from the server cache
            //    (If the app just started: LoadAsync has probably run already via Load on app enter)
            var userRows = UserLibrarySync.Instance.MyMaterials
                .Select(r => new MaterialModel
                {
                    SerialNumber  = r.sn,
                    MaterialName  = r.description,
                    MaterialUnit  = r.unit,
                    MaterialPrice = r.price,
                    MaterialCategory = ""   // user rows may not have category; keep blank or your own tag
                })
                .ToList();

            // 3) Re-number user rows to avoid collisions with master if needed
            var masterMaxSn = master.Count == 0 ? 0 : master.Max(m => m.SerialNumber);
            var used = new HashSet<int>(master.Select(m => m.SerialNumber));
            int nextSn = masterMaxSn + 1;

            foreach (var u in userRows.OrderBy(x => x.SerialNumber))
            {
                if (!used.Add(u.SerialNumber))
                {
                    // collision → give it the next free S/N after master
                    u.SerialNumber = nextSn++;
                    used.Add(u.SerialNumber);
                }
                else if (u.SerialNumber <= masterMaxSn)
                {
                    // keep user rows visually after master block
                    u.SerialNumber = nextSn++;
                }
            }

            // 4) Merge & save
            var merged = master.Concat(userRows)
                               .OrderBy(m => m.SerialNumber)
                               .ToList();

            var ds = new MaterialJsonDataSource(AppPaths.MaterialLibraryFile);
            ds.SaveMaterials(merged);
            MaterialLibraryService.Initialize(ds); // keep the in-memory cache fresh
        }


        /* ---------- LABOUR ---------- */

        public static void SaveLaboursFromDto(JsonElement dtoLabour)
        {
            // 1) Convert master → LabourModel list
            var master = new List<LabourModel>();
            if (dtoLabour.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in dtoLabour.EnumerateArray())
                {
                    master.Add(new LabourModel
                    {
                        SerialNumber  = el.GetProperty("sn").GetInt32(),
                        LabourName    = el.GetProperty("description").GetString() ?? "",
                        LabourUnit    = el.GetProperty("unit").GetString() ?? "",
                        LabourPrice   = el.GetProperty("price").GetDecimal(),
                        LabourCategory = "" // optional
                    });
                }
            }

            // 2) Latest user rows
            var userRows = UserLibrarySync.Instance.MyLabour
                .Select(r => new LabourModel
                {
                    SerialNumber = r.sn,
                    LabourName   = r.description,
                    LabourUnit   = r.unit,
                    LabourPrice  = r.price,
                    LabourCategory = ""
                })
                .ToList();

            // 3) Avoid S/N collisions with master
            var masterMaxSn = master.Count == 0 ? 0 : master.Max(m => m.SerialNumber);
            var used = new HashSet<int>(master.Select(m => m.SerialNumber));
            int nextSn = masterMaxSn + 1;

            foreach (var u in userRows.OrderBy(x => x.SerialNumber))
            {
                if (!used.Add(u.SerialNumber))
                {
                    u.SerialNumber = nextSn++;
                    used.Add(u.SerialNumber);
                }
                else if (u.SerialNumber <= masterMaxSn)
                {
                    u.SerialNumber = nextSn++;
                }
            }

            // 4) Merge & save
            var merged = master.Concat(userRows)
                               .OrderBy(l => l.SerialNumber)
                               .ToList();

            var ds = new LabourJsonDataSource(AppPaths.LabourLibraryFile);
            ds.SaveLabours(merged);
            LabourLibraryService.Initialize(ds);
        }
    

}
}
