// Helpers/AppPaths.cs
using System;
using System.IO;

namespace ADLMRateGen.Helpers
{
    /// <summary>
    /// Central place for app file/folder paths + safe IO helpers.
    /// Keeps user data in %AppData%\ADLMRateGen and supports
    /// shipping read-only defaults next to the EXE in /Defaults.
    /// </summary>
    public static class AppPaths
    {
        /* ----------------- Folders & Files (User) ----------------- */

        /// <summary>%AppData%\ADLMRateGen (Roaming). Always exists.</summary>
        public static string UserDataDir
        {
            get
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); // Roaming
                var dir = Path.Combine(root, "ADLMRateGen");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>%AppData%\ADLMRateGen\backups. Always exists.</summary>
        public static string BackupsDir
        {
            get
            {
                var dir = Path.Combine(UserDataDir, "backups");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>%AppData%\ADLMRateGen\data-version.json</summary>
        public static string DataVersionFile => Path.Combine(UserDataDir, "data-version.json");

        /// <summary>%AppData%\ADLMRateGen\custom-rates.json</summary>
        public static string CustomRatesFile => Path.Combine(UserDataDir, "custom-rates.json");

        /// <summary>%AppData%\ADLMRateGen\materials.json</summary>
        public static string MaterialLibraryFile => Path.Combine(UserDataDir, "materials.json");

        /// <summary>%AppData%\ADLMRateGen\labour.json</summary>
        public static string LabourLibraryFile => Path.Combine(UserDataDir, "labour.json");

        // Aliases (to avoid refactors in existing code)
        public static string MaterialsFile => MaterialLibraryFile;
        public static string LaboursFile => LabourLibraryFile;

        /* ----------------- Packaged (read-only) defaults ----------------- */

        /// <summary>Folder next to the EXE where you ship defaults, e.g. Defaults\materials.json.</summary>
        public static string ShippedDefaultsDir
        {
            get
            {
                var exeDir = AppContext.BaseDirectory;
                return Path.Combine(exeDir, "Defaults");
            }
        }

        public static string PackagedMaterialsFile => Path.Combine(ShippedDefaultsDir, "materials.json");
        public static string PackagedLaboursFile => Path.Combine(ShippedDefaultsDir, "labour.json");
        public static string PackagedCustomRatesFile => Path.Combine(ShippedDefaultsDir, "custom-rates.json");
        public static string PackagedDataVersionFile => Path.Combine(ShippedDefaultsDir, "data-version.json");

        /* ----------------- Seeding & Safe IO helpers ----------------- */

        /// <summary>
        /// Copy a shipped default file to the user location if the user file does not exist.
        /// Creates an empty JSON list if no packaged file is found.
        /// </summary>
        public static void TrySeedFromPackaged(string packagedPath, string userPath)
        {
            try
            {
                if (File.Exists(userPath)) return;

                var dir = Path.GetDirectoryName(userPath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(packagedPath))
                {
                    File.Copy(packagedPath, userPath, overwrite: false);
                }
                else
                {
                    // Create a valid empty JSON file so deserializers don't choke
                    File.WriteAllText(userPath, "[]");
                }
            }
            catch
            {
                // Best effort seeding — intentionally swallow exceptions
            }
        }

        /// <summary>
        /// Writes content atomically: write to temp, then replace/rename over target.
        /// Prevents partial/corrupt files on crashes.
        /// </summary>
        public static void AtomicWrite(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";

            // Write temp first
            File.WriteAllText(tmp, content);

            // Replace existing or move into place
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tmp, path, null);
                }
                catch
                {
                    // Fallback if Replace fails on some FS
                    File.Delete(path);
                    File.Move(tmp, path);
                }
            }
            else
            {
                File.Move(tmp, path);
            }
        }

        /// <summary>
        /// Creates a timestamped copy of an existing file into BackupsDir.
        /// Returns the backup path or empty string if source doesn't exist.
        /// </summary>
        public static string MakeTimestampedBackup(string sourcePath)
        {
            try
            {
                if (!File.Exists(sourcePath)) return string.Empty;

                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var name = Path.GetFileName(sourcePath);
                var safeName = SafeFileName(Path.GetFileNameWithoutExtension(name));
                var ext = Path.GetExtension(name);

                var dest = Path.Combine(BackupsDir, $"{safeName}_{stamp}{ext}");
                File.Copy(sourcePath, dest, overwrite: false);
                return dest;
            }
            catch
            {
                return string.Empty; // best effort
            }
        }

        /// <summary>
        /// Read a simple data-version string from user file (or empty if none).
        /// </summary>
        public static string ReadUserDataVersion()
        {
            try
            {
                if (!File.Exists(DataVersionFile)) return string.Empty;
                return File.ReadAllText(DataVersionFile).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Save a simple data-version string to the user file atomically.
        /// </summary>
        public static void WriteUserDataVersion(string version)
        {
            try
            {
                AtomicWrite(DataVersionFile, version ?? string.Empty);
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Get a simple shipped data-version string (from Defaults\data-version.json).
        /// </summary>
        public static string ReadShippedDataVersion()
        {
            try
            {
                if (!File.Exists(PackagedDataVersionFile)) return string.Empty;
                return File.ReadAllText(PackagedDataVersionFile).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        /* ----------------- Internals ----------------- */

        private static string SafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
                name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "file" : name;
        }
    }
}
