using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ADLMRateGen.Helpers;           // << uses AppPaths.UserDataDir / AppPaths.CustomRatesFile
using ADLMRateGen.ViewModel.CustomRate;
using Newtonsoft.Json;

namespace ADLMRateGen.Services
{
    public static class CustomRateServices
    {
        // Events
        public static event Action<CustomRate>? OnCustomRateSaved;
        public static event Action<CustomRate>? OnCustomRateUpdated;

        // Thread-safety for file IO
        private static readonly object _sync = new object();

        // Centralized, roaming AppData path (e.g. %AppData%\ADLMRateGen\custom-rates.json)
        private static string FilePath => AppPaths.CustomRatesFile;

        public static IEnumerable<CustomRate> LoadCustomRates()
        {
            lock (_sync)
            {
                try
                {
                    if (!File.Exists(FilePath))
                        return new List<CustomRate>();

                    var json = File.ReadAllText(FilePath);
                    return JsonConvert.DeserializeObject<List<CustomRate>>(json) ?? new List<CustomRate>();
                }
                catch
                {
                    // If file is corrupted or unreadable, fail gracefully
                    return new List<CustomRate>();
                }
            }
        }

        public static void SaveCustomRate(CustomRate rate)
        {
            lock (_sync)
            {
                var rates = LoadCustomRates().ToList();
                rates.Add(rate);
                SaveRates(rates);
            }

            OnCustomRateSaved?.Invoke(rate);
        }

        /// <summary>
        /// Update an existing CustomRate by matching on Id.
        /// If not found, adds it. Then fires OnCustomRateUpdated.
        /// </summary>
        public static void UpdateCustomRate(CustomRate updatedRate)
        {
            lock (_sync)
            {
                var rates = LoadCustomRates().ToList();

                var idx = rates.FindIndex(r => r.Id == updatedRate.Id);
                if (idx >= 0)
                {
                    // Replace the whole object to ensure we persist all fields consistently
                    rates[idx] = updatedRate;
                }
                else
                {
                    rates.Add(updatedRate);
                }

                SaveRates(rates);
            }

            OnCustomRateUpdated?.Invoke(updatedRate);
        }

        public static void SaveRates(IEnumerable<CustomRate> rates)
        {
            lock (_sync)
            {
                var dir = Path.GetDirectoryName(FilePath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Atomic write: write to temp, then replace
                var json = JsonConvert.SerializeObject(rates, Formatting.Indented);
                var tmp = FilePath + ".tmp";

                File.WriteAllText(tmp, json);
                // Replace the existing file (if any) with the temp file atomically where possible
                if (File.Exists(FilePath))
                {
                    // Try a safe replace; fallback to delete+move if Replace isn't available on the platform
                    try
                    {
                        File.Replace(tmp, FilePath, null);
                    }
                    catch
                    {
                        File.Delete(FilePath);
                        File.Move(tmp, FilePath);
                    }
                }
                else
                {
                    File.Move(tmp, FilePath);
                }
            }
        }
    }
}
