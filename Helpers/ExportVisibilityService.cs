using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ADLMRateGen.Helpers;

namespace ADLMRateGen.Services
{
    public static class ExportVisibilityService
    {
        private sealed class State
        {
            public Dictionary<string, DateTime> FirstLoginByMonth { get; set; } = new();
        }

        private static readonly string FilePath =
            Path.Combine(AppPaths.UserDataDir, "export-visibility.json");

        public static bool ShouldShowOnThisLogin(DateTime now)
        {
            now = now.Date; // day-level precision
            var key = now.ToString("yyyy-MM"); // monthly bucket

            var state = Load();

            if (!state.FirstLoginByMonth.TryGetValue(key, out var firstLogin))
            {
                // First login this calendar month
                firstLogin = now;
                state.FirstLoginByMonth[key] = firstLogin;
                Save(state);
                return true; // visible on first login
            }

            // Second time: exactly 20 days after first login this month
            var secondDay = firstLogin.AddDays(20).Date;
            return now == firstLogin || now == secondDay;
        }

        private static State Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new State();
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<State>(json) ?? new State();
            }
            catch
            {
                return new State();
            }
        }

        private static void Save(State s)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch
            {
                // swallow — visibility just falls back next run
            }
        }
    }
}
