using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ADLMRateGen.Helpers;

namespace ADLMRateGen.Services
{
    /// <summary>
    /// Point-in-time copies of the material and labour libraries, so any change
    /// that rewrites them can be undone.
    ///
    /// WHY THIS EXISTS
    /// The cloud sync replaces the local library with "master rows + the rows you
    /// added yourself". A price you edited on a MASTER row was in neither list, so
    /// it was destroyed on every sign-in with nothing said and no way back. That
    /// is the failure this exists to make survivable: an archive is taken before
    /// anything overwrites the library, and Restore puts it back.
    ///
    /// Restore archives the current state first, so undo is itself undoable.
    /// </summary>
    public static class LibraryArchive
    {
        private const int KeepCount = 20;

        public sealed class Entry
        {
            public string Id { get; set; } = "";
            public DateTime TakenAt { get; set; }
            public string Reason { get; set; } = "";
            public int MaterialRows { get; set; }
            public int LabourRows { get; set; }
            public string FolderPath { get; set; } = "";

            public string Label =>
                $"{TakenAt:dd MMM HH:mm}  {Reason}  ({MaterialRows} materials, {LabourRows} labour)";
        }

        private static string ArchiveRoot
        {
            get
            {
                var dir = Path.Combine(AppPaths.BackupsDir, "library-archive");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static int CountRows(string path)
        {
            try
            {
                if (!File.Exists(path)) return 0;
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                return doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.GetArrayLength() : 0;
            }
            catch { return 0; }
        }

        // A sync saves materials and labour in two back-to-back calls. The archive
        // taken by the first copies BOTH files while both are still untouched, so
        // it already covers the second. Without this the user would collect two
        // snapshots per sync, half of them useless, and the twenty kept would only
        // reach back half as far.
        private static DateTime _lastCreate = DateTime.MinValue;
        private static readonly TimeSpan CoalesceWindow = TimeSpan.FromSeconds(30);
        private static readonly object _gate = new();

        /// <summary>
        /// Copy the current libraries into a new archive folder. Returns null when
        /// there is nothing to archive, which is not an error: a first run has no
        /// library yet.
        /// </summary>
        /// <param name="force">Take a snapshot even inside the coalescing window.
        /// Used by deliberate user actions, which must always be undoable.</param>
        public static Entry Create(string reason, bool force = false)
        {
            try
            {
                var mat = AppPaths.MaterialLibraryFile;
                var lab = AppPaths.LabourLibraryFile;
                if (!File.Exists(mat) && !File.Exists(lab)) return null;

                lock (_gate)
                {
                    if (!force && DateTime.Now - _lastCreate < CoalesceWindow) return null;
                    _lastCreate = DateTime.Now;
                }

                var id = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var folder = Path.Combine(ArchiveRoot, id);
                Directory.CreateDirectory(folder);

                if (File.Exists(mat)) File.Copy(mat, Path.Combine(folder, "materials.json"), true);
                if (File.Exists(lab)) File.Copy(lab, Path.Combine(folder, "labour.json"), true);

                var entry = new Entry
                {
                    Id = id,
                    TakenAt = DateTime.Now,
                    Reason = string.IsNullOrWhiteSpace(reason) ? "manual" : reason.Trim(),
                    MaterialRows = CountRows(mat),
                    LabourRows = CountRows(lab),
                    FolderPath = folder,
                };

                AppPaths.AtomicWrite(
                    Path.Combine(folder, "archive.json"),
                    JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true }));

                Prune();
                return entry;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LibraryArchive] Create failed: {ex.Message}");
                return null;
            }
        }

        public static IReadOnlyList<Entry> List()
        {
            try
            {
                return Directory.GetDirectories(ArchiveRoot)
                    .Select(d => Path.Combine(d, "archive.json"))
                    .Where(File.Exists)
                    .Select(f =>
                    {
                        try { return JsonSerializer.Deserialize<Entry>(File.ReadAllText(f)); }
                        catch { return null; }
                    })
                    .Where(e => e != null)
                    .OrderByDescending(e => e.TakenAt)
                    .ToList();
            }
            catch { return new List<Entry>(); }
        }

        /// <summary>
        /// Put an archived library back. The current state is archived first, so a
        /// restore can itself be undone.
        /// </summary>
        public static bool Restore(string id)
        {
            try
            {
                var folder = Path.Combine(ArchiveRoot, id);
                if (!Directory.Exists(folder)) return false;

                // Forced: a restore is a deliberate act and must always leave a way
                // back, even if a sync archived moments ago.
                Create("before restore", force: true);

                var mat = Path.Combine(folder, "materials.json");
                var lab = Path.Combine(folder, "labour.json");
                if (File.Exists(mat)) File.Copy(mat, AppPaths.MaterialLibraryFile, true);
                if (File.Exists(lab)) File.Copy(lab, AppPaths.LabourLibraryFile, true);

                // The synced baseline no longer describes what is on disk, so drop
                // it. The next sync then treats every row as unedited rather than
                // reporting differences against a baseline from another point in
                // time, which would flag the restore itself as user edits.
                SyncBaseline.Clear();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LibraryArchive] Restore failed: {ex.Message}");
                return false;
            }
        }

        private static void Prune()
        {
            try
            {
                foreach (var e in List().Skip(KeepCount))
                    Directory.Delete(e.FolderPath, true);
            }
            catch { }
        }
    }
}
