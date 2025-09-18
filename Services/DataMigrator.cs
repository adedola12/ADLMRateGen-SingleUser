using ADLMRateGen.Helpers;
using ADLMRateGen.ViewModel.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
// alias System.Text.Json to avoid the JsonSerializer name clash
using STJ = System.Text.Json;

namespace ADLMRateGen.Services
{
    public static class DataMigrator
    {
        private const int CurrentDataVersion = 3;
        private record VersionInfo(int Version);

        public static void EnsureMigrated()
        {
            try
            {
                var ver = ReadVersion();
                if (ver >= CurrentDataVersion) return;

                BackupUserData();

                // ---- migrate labours.json -> labour.json (plural -> singular) ----
                var plural = Path.Combine(AppPaths.UserDataDir, "labours.json");
                var singular = AppPaths.LabourLibraryFile; // e.g. %AppData%\ADLMRateGen\labour.json

                if (File.Exists(plural))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(singular)!);

                    var from = File.ReadAllText(plural);
                    var to = File.Exists(singular) ? File.ReadAllText(singular) : "[]";

                    var src = JsonConvert.DeserializeObject<List<LabourModel>>(from) ?? new();
                    var dst = JsonConvert.DeserializeObject<List<LabourModel>>(to)   ?? new();

                    // case-insensitive de-duplication by LabourName
                    var merged = src.Concat(dst)
                                    .Where(l => !string.IsNullOrWhiteSpace(l.LabourName))
                                    .GroupBy(l => l.LabourName!, StringComparer.OrdinalIgnoreCase)
                                    .Select(g => g.First())
                                    .ToList();

                    File.WriteAllText(
                        singular,
                        JsonConvert.SerializeObject(merged, Formatting.Indented)
                    );

                    // Optional: File.Delete(plural);
                }

                // ---- seed user files if missing ----
                if (!File.Exists(AppPaths.MaterialLibraryFile))
                    SeedFromDefaultFileNameFallback(
                        primary: "materials.json",
                        fallback: "defaultMaterials.json",
                        targetPath: AppPaths.MaterialLibraryFile);

                if (!File.Exists(AppPaths.LabourLibraryFile))
                    SeedFromDefaultFileNameFallback(
                        primary: "labour.json",
                        fallback: "defaultLabours.json",
                        targetPath: AppPaths.LabourLibraryFile);

                if (!File.Exists(AppPaths.CustomRatesFile))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.CustomRatesFile)!);
                    File.WriteAllText(AppPaths.CustomRatesFile, "[]");
                }

                // ---- merge shipped defaults add-only (don’t override user edits) ----
                MergeDefaults(
                    shippedFileName: "materials.json",
                    fallbackFileName: "defaultMaterials.json",
                    userPath: AppPaths.MaterialLibraryFile,
                    keySelector: e => e.TryGetProperty("MaterialName", out var p) ? p.GetString() : null
                );

                MergeDefaults(
                    shippedFileName: "labour.json",
                    fallbackFileName: "defaultLabours.json",
                    userPath: AppPaths.LabourLibraryFile,
                    keySelector: e =>
                        e.TryGetProperty("LabourName", out var p1) ? p1.GetString()
                        : e.TryGetProperty("Name", out var p2) ? p2.GetString()
                        : null
                );

                WriteVersion(CurrentDataVersion);
            }
            catch
            {
                // swallow or log; don’t block app startup on migration
            }
        }

        private static int ReadVersion()
        {
            if (!File.Exists(AppPaths.DataVersionFile)) return 0;
            try
            {
                var json = File.ReadAllText(AppPaths.DataVersionFile);
                var info = STJ.JsonSerializer.Deserialize<VersionInfo>(json);
                return info?.Version ?? 0;
            }
            catch { return 0; }
        }

        private static void WriteVersion(int v)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.DataVersionFile)!);
            var json = STJ.JsonSerializer.Serialize(new VersionInfo(v), new STJ.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AppPaths.DataVersionFile, json);
        }

        private static void BackupUserData()
        {
            Directory.CreateDirectory(AppPaths.BackupsDir);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var zip = Path.Combine(AppPaths.BackupsDir, $"user-data_{stamp}.zip");

            using var archive = ZipFile.Open(zip, ZipArchiveMode.Create);

            void TryAdd(string path)
            {
                if (File.Exists(path))
                    archive.CreateEntryFromFile(path, Path.GetFileName(path));
            }

            TryAdd(AppPaths.MaterialLibraryFile);
            TryAdd(AppPaths.LabourLibraryFile);
            TryAdd(AppPaths.CustomRatesFile);
            TryAdd(AppPaths.DataVersionFile);
        }
        private static void SeedFromDefaultFileNameFallback(string primary, string fallback, string targetPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            var seedFrom = AppPaths.FindShippedDefault(primary, fallback);
            if (seedFrom != null)
                File.Copy(seedFrom, targetPath, overwrite: false);
        }
        //private static void SeedFromDefaultFileNameFallback(string primary, string fallback, string targetPath)
        //{
        //    var shippedPrimary = Path.Combine(AppPaths.ShippedDefaultsDir, primary);
        //    var shippedFallback = Path.Combine(AppPaths.ShippedDefaultsDir, fallback);

        //    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        //    if (File.Exists(shippedPrimary))
        //        File.Copy(shippedPrimary, targetPath, overwrite: false);
        //    else if (File.Exists(shippedFallback))
        //        File.Copy(shippedFallback, targetPath, overwrite: false);

        //    // NOTE: no "[]" creation here; leave it missing so the data source can seed.
        //}


        private static void MergeDefaults(
            string shippedFileName,
            string fallbackFileName,
            string userPath,
            Func<STJ.JsonElement, string?> keySelector)
        {

            var shipped = AppPaths.FindShippedDefault(shippedFileName, fallbackFileName);
            if (shipped is null || !File.Exists(userPath)) return;

            // choose shipped file; fallback if primary missing
            //var shipped = Path.Combine(AppPaths.ShippedDefaultsDir, shippedFileName);
            if (!File.Exists(shipped))
            {
                var fb = Path.Combine(AppPaths.ShippedDefaultsDir, fallbackFileName);
                if (!File.Exists(fb)) return;
                shipped = fb;
            }
            if (!File.Exists(userPath)) return;

            var shippedArr = STJ.JsonSerializer.Deserialize<List<STJ.JsonElement>>(File.ReadAllText(shipped)) ?? new();
            var userArr = STJ.JsonSerializer.Deserialize<List<STJ.JsonElement>>(File.ReadAllText(userPath)) ?? new();

            var userKeys = new HashSet<string?>(userArr.Select(keySelector), StringComparer.OrdinalIgnoreCase);

            foreach (var item in shippedArr)
            {
                var key = keySelector(item);
                if (!userKeys.Contains(key))
                {
                    userArr.Add(item);
                    userKeys.Add(key);
                }
            }

            var json = STJ.JsonSerializer.Serialize(userArr, new STJ.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(userPath, json);
        }
    }
}
