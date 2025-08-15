using ADLMRateGen.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ADLMRateGen.Services
{
    public static class DataMigrator
    {
        private const int CurrentDataVersion = 2; // bump this when you change data schema or shipped defaults

        private record VersionInfo(int Version);

        public static void EnsureMigrated()
        {
            try
            {
                var ver = ReadVersion();
                if (ver >= CurrentDataVersion) return;

                BackupUserData();

                // 1) Ensure user files exist
                if (!File.Exists(AppPaths.MaterialLibraryFile))
                    SeedFromDefault("materials.json", AppPaths.MaterialLibraryFile);

                if (!File.Exists(AppPaths.LabourLibraryFile))
                    SeedFromDefault("labour.json", AppPaths.LabourLibraryFile);

                if (!File.Exists(AppPaths.CustomRatesFile))
                    File.WriteAllText(AppPaths.CustomRatesFile, "[]");

                // 2) Merge new defaults into existing (add-only; no overwrite of user edits)
                MergeDefaults("materials.json", AppPaths.MaterialLibraryFile, keySelector: m => m.GetProperty("MaterialName").GetString());
                MergeDefaults("labour.json", AppPaths.LabourLibraryFile, keySelector: l => l.GetProperty("Name").GetString());

                // TODO: if you change schemas between versions, do your per-version steps here
                // if (ver < 2) { /* migrate fields, rename props, etc. */ }

                WriteVersion(CurrentDataVersion);
            }
            catch
            {
                // swallow or log; we don't want a failed merge to prevent the app from starting
            }
        }

        private static int ReadVersion()
        {
            if (!File.Exists(AppPaths.DataVersionFile)) return 0;
            try
            {
                var info = JsonSerializer.Deserialize<VersionInfo>(File.ReadAllText(AppPaths.DataVersionFile));
                return info?.Version ?? 0;
            }
            catch { return 0; }
        }

        private static void WriteVersion(int v)
        {
            var json = JsonSerializer.Serialize(new VersionInfo(v), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AppPaths.DataVersionFile, json);
        }

        private static void BackupUserData()
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var zip = Path.Combine(AppPaths.BackupsDir, $"user-data_{stamp}.zip");

            using var archive = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create);
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

        private static void SeedFromDefault(string defaultFileName, string targetPath)
        {
            var shipped = Path.Combine(AppPaths.ShippedDefaultsDir, defaultFileName);
            if (File.Exists(shipped))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(shipped, targetPath, overwrite: false);
            }
            else
            {
                // fall back to empty
                File.WriteAllText(targetPath, "[]");
            }
        }

        /// <summary>
        /// Add-only merge: any item in the shipped defaults that is not present in the user file is appended.
        /// </summary>
        private static void MergeDefaults(string defaultFileName, string userPath, Func<System.Text.Json.JsonElement, string?> keySelector)
        {
            var shipped = Path.Combine(AppPaths.ShippedDefaultsDir, defaultFileName);
            if (!File.Exists(shipped) || !File.Exists(userPath)) return;

            var shippedArr = JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(File.ReadAllText(shipped)) ?? new();
            var userArr = JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(File.ReadAllText(userPath)) ?? new();

            var userKeys = new HashSet<string?>(userArr.Select(keySelector));
            foreach (var item in shippedArr)
            {
                var key = keySelector(item);
                if (!userKeys.Contains(key))
                    userArr.Add(item);
            }

            var json = JsonSerializer.Serialize(userArr, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(userPath, json);
        }
    }
}
