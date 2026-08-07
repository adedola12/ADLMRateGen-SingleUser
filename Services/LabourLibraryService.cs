using System;
using System.Collections.Generic;
using System.Linq;
using ADLMRateGen.Helpers;          // ← for AppPaths
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.Services
{
    public static class LabourLibraryService
    {
        private static readonly object _sync = new();

        public static event Action? LibraryChanged;            // NEW
        private static void RaiseChanged() => LibraryChanged?.Invoke(); // NEW

        // Default to JSON file in AppData
        private static ILabourDataSource _dataSource =
            new LabourJsonDataSource(AppPaths.LabourLibraryFile);

        private static List<LabourModel> _labours = new();

        private static void EnsureLoaded()
        {
            if (_labours.Count == 0)
                _labours = _dataSource.LoadLabours().ToList();
        }

        /// <summary>
        /// Initialize the service with a data source (optional; defaults to JSON in AppData).
        /// </summary>
        public static void Initialize(ILabourDataSource? dataSource = null)
        {
            lock (_sync)
            {
                if (dataSource != null)
                    _dataSource = dataSource;

                _labours = _dataSource.LoadLabours().ToList();
            }
            RaiseChanged();
        }

        public static IEnumerable<string> GetAllLabourNames()
        {
            lock (_sync)
                return _labours
                    .Select(l => l.LabourName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        public static decimal GetPrice(string labourName)
        {
            if (string.IsNullOrWhiteSpace(labourName)) return 0m;

            lock (_sync)
            {
                var labour = _labours.FirstOrDefault(l =>
                    string.Equals(l.LabourName, labourName, StringComparison.OrdinalIgnoreCase));
                return labour != null ? labour.LabourPrice : 0m;
            }
        }

        public static IEnumerable<LabourModel> GetAllLabours()
        {
            lock (_sync)
            {
                EnsureLoaded();
                return _labours.ToList();
            }
        }

        public static LabourModel? FindByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            lock (_sync)
            {
                EnsureLoaded();
                return _labours.FirstOrDefault(l =>
                    string.Equals(l.LabourName, name, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Merge incoming labours (add new, update existing by LabourName, keep user edits).
        /// Only updates known fields; add your own if needed.
        /// </summary>
        public static void AddOrUpdateLabours(IEnumerable<LabourModel> incoming)
        {
            if (incoming == null) return;

            lock (_sync)
            {
                // Grouped by name, never keyed by it — see the matching comment
                // in MaterialLibraryService. Keying threw on the first duplicate
                // name and would have dropped every duplicate row on save.
                var byName = _labours
                    .GroupBy(l => l.LabourName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

                foreach (var item in incoming)
                {
                    var key = item.LabourName ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(key)) continue;

                    if (byName.TryGetValue(key, out var existingRows))
                    {
                        foreach (var existing in existingRows)
                        {
                            // Always update price from the incoming list
                            existing.LabourPrice = item.LabourPrice;

                            // Update unit/category if provided (avoid overwriting with null/empty)
                            if (!string.IsNullOrWhiteSpace(item.LabourUnit))
                                existing.LabourUnit = item.LabourUnit;

                            if (!string.IsNullOrWhiteSpace(item.LabourCategory))
                                existing.LabourCategory = item.LabourCategory;

                            // Keep any other user-specific fields on 'existing' intact
                        }
                    }
                    else
                    {
                        // New labour entirely — appended, so existing rows keep
                        // their position in the file.
                        _labours.Add(item);
                        byName[key] = new List<LabourModel> { item };
                    }
                }

                _dataSource.SaveLabours(_labours);
            }

            RaiseChanged();
        }
    }
}
