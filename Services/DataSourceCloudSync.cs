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
                        MaterialCategory = el.TryGetProperty("category", out var cat) ? (cat.GetString() ?? "") : ""

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
                    MaterialCategory = r.category ?? ""   // user rows may not have category; keep blank or your own tag
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
                        LabourCategory = el.TryGetProperty("category", out var c) ? (c.GetString() ?? "") : ""

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
                    LabourCategory = r.category ?? ""
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
