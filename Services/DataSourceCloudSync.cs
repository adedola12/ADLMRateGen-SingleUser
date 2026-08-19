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
    ///
    /// PRICES THE USER EDITED ARE NEVER OVERWRITTEN HERE.
    /// This used to write "master rows + rows you added yourself", which meant a
    /// price edited on a master row was in neither list and was destroyed on every
    /// sign-in, silently and with no way back. Now every edit is carried forward,
    /// and where the server has genuinely moved a price the disagreement is parked
    /// in PriceConflicts for the user to accept or ignore. Nothing is discarded on
    /// their behalf.
    ///
    /// The library is also archived before each write, so any of this is undoable.
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

            // An empty payload is never a real catalog. Treated as data it would
            // reduce the library to the user's own rows and destroy everything
            // else, so a bad response or a state with no rows yet would look
            // exactly like a wipe. Leave the library alone and let the caller
            // report the failure.
            if (master.Count == 0) return;

            // 1a) What is on disk right now, before the server overwrites it. This
            //     is the only place the user's edits still exist.
            List<MaterialModel> local;
            try { local = new MaterialJsonDataSource(AppPaths.MaterialLibraryFile).LoadMaterials()?.ToList() ?? new(); }
            catch { local = new List<MaterialModel>(); }

            // 1b) Baseline records what the cloud sent last time, so a price the
            //     user typed can be told apart from a price the server sent.
            var edits = SyncBaseline.FindEdits(local, master, null, null);

            // The server's prices, captured before the preservation step below
            // overwrites them in place.
            var serverPrices = SyncBaseline.Snapshot(master);

            // Take a copy before touching anything, so the whole write can be undone.
            LibraryArchive.Create("before price sync");

            // 1c) Carry every edit forward. The server value is remembered so the
            //     user can still switch to it later from the conflicts prompt.
            if (edits.Count > 0)
            {
                // Grouped, not ToDictionary: a repeated RowKey here used to throw and
                // abort the sync rather than lose one row.
                var keep = edits.GroupBy(e => e.RowKey, StringComparer.OrdinalIgnoreCase)
                                .ToDictionary(g => g.Key, g => g.First().YourPrice, StringComparer.OrdinalIgnoreCase);
                foreach (var m in master)
                {
                    if (keep.TryGetValue(SyncBaseline.Key(m.MaterialName, m.MaterialUnit), out var mine))
                        m.MaterialPrice = mine;
                }
                PriceConflicts.Record(edits);
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
            //
            // A user row naming an item master already carries is the user's PRICE
            // for that item, not a second item. Concatenating produced visible
            // twins — same name, unit and price, one with the master category and
            // one blank — and a repeated key is what crashed FindEdits. Fold the
            // user's price onto the master row and keep master's category.
            var masterByKey = new Dictionary<string, MaterialModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in master)
                masterByKey[SyncBaseline.Key(m.MaterialName, m.MaterialUnit)] = m;

            var extraUserRows = new List<MaterialModel>();
            foreach (var u in userRows)
            {
                if (masterByKey.TryGetValue(SyncBaseline.Key(u.MaterialName, u.MaterialUnit), out var hit))
                    hit.MaterialPrice = u.MaterialPrice;
                else
                    extraUserRows.Add(u);
            }

            var merged = master.Concat(extraUserRows)
                               .OrderBy(m => m.SerialNumber)
                               .ToList();

            var ds = new MaterialJsonDataSource(AppPaths.MaterialLibraryFile);
            ds.SaveMaterials(merged);
            MaterialLibraryService.Initialize(ds); // keep the in-memory cache fresh

            // 5) Record what the SERVER sent, not what was saved. The two differ
            //    wherever an edit was preserved, and that difference is exactly
            //    what keeps the edit recognisable as an edit next time.
            SyncBaseline.WriteMaterials(serverPrices);
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

            // Same guard as materials: an empty payload would wipe the library.
            if (master.Count == 0) return;

            // 1a) Same protection as materials: read the local rows before the
            //     server replaces them, and carry any edited price forward.
            List<LabourModel> local;
            try { local = new LabourJsonDataSource(AppPaths.LabourLibraryFile).LoadLabours()?.ToList() ?? new(); }
            catch { local = new List<LabourModel>(); }

            var edits = SyncBaseline.FindEdits(null, null, local, master);

            var serverPrices = SyncBaseline.Snapshot(master);

            LibraryArchive.Create("before price sync");

            if (edits.Count > 0)
            {
                // Grouped, not ToDictionary: a repeated RowKey here used to throw and
                // abort the sync rather than lose one row.
                var keep = edits.GroupBy(e => e.RowKey, StringComparer.OrdinalIgnoreCase)
                                .ToDictionary(g => g.Key, g => g.First().YourPrice, StringComparer.OrdinalIgnoreCase);
                foreach (var l in master)
                {
                    if (keep.TryGetValue(SyncBaseline.Key(l.LabourName, l.LabourUnit), out var mine))
                        l.LabourPrice = mine;
                }
                PriceConflicts.Record(edits);
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

            // 4) Merge & save — same twinning fix as materials above.
            var masterByKey = new Dictionary<string, LabourModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in master)
                masterByKey[SyncBaseline.Key(l.LabourName, l.LabourUnit)] = l;

            var extraUserRows = new List<LabourModel>();
            foreach (var u in userRows)
            {
                if (masterByKey.TryGetValue(SyncBaseline.Key(u.LabourName, u.LabourUnit), out var hit))
                    hit.LabourPrice = u.LabourPrice;
                else
                    extraUserRows.Add(u);
            }

            var merged = master.Concat(extraUserRows)
                               .OrderBy(l => l.SerialNumber)
                               .ToList();

            var ds = new LabourJsonDataSource(AppPaths.LabourLibraryFile);
            ds.SaveLabours(merged);
            LabourLibraryService.Initialize(ds);

            SyncBaseline.WriteLabour(serverPrices);
        }


        /* ---------- accepting the server's price after all ---------- */

        /// <summary>
        /// Apply the server price to rows the user has agreed to give up. Called
        /// from the conflicts prompt, never automatically.
        /// </summary>
        public static void AcceptServerPrices(IEnumerable<SyncBaseline.EditedRow> rows)
        {
            var list = rows?.ToList() ?? new List<SyncBaseline.EditedRow>();
            if (list.Count == 0) return;

            LibraryArchive.Create("before accepting new prices", force: true);

            var mat = list.Where(r => !r.IsLabour)
                          .GroupBy(r => r.RowKey, StringComparer.OrdinalIgnoreCase)
                          .ToDictionary(g => g.Key, g => g.First().ServerPrice, StringComparer.OrdinalIgnoreCase);
            if (mat.Count > 0)
            {
                var ds = new MaterialJsonDataSource(AppPaths.MaterialLibraryFile);
                var rowsOnDisk = ds.LoadMaterials()?.ToList() ?? new List<MaterialModel>();
                foreach (var m in rowsOnDisk)
                    if (mat.TryGetValue(SyncBaseline.Key(m.MaterialName, m.MaterialUnit), out var p))
                        m.MaterialPrice = p;
                ds.SaveMaterials(rowsOnDisk);
                MaterialLibraryService.Initialize(ds);
            }

            var lab = list.Where(r => r.IsLabour)
                          .GroupBy(r => r.RowKey, StringComparer.OrdinalIgnoreCase)
                          .ToDictionary(g => g.Key, g => g.First().ServerPrice, StringComparer.OrdinalIgnoreCase);
            if (lab.Count > 0)
            {
                var ds = new LabourJsonDataSource(AppPaths.LabourLibraryFile);
                var rowsOnDisk = ds.LoadLabours()?.ToList() ?? new List<LabourModel>();
                foreach (var l in rowsOnDisk)
                    if (lab.TryGetValue(SyncBaseline.Key(l.LabourName, l.LabourUnit), out var p))
                        l.LabourPrice = p;
                ds.SaveLabours(rowsOnDisk);
                LabourLibraryService.Initialize(ds);
            }

            PriceConflicts.Resolve(list);
        }
    }
}
