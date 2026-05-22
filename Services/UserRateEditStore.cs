using ADLMRateGen.Helpers;
using ADLMRateGen.ViewModel.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ADLMRateGen.Services
{
    /// <summary>
    /// Singleton in-memory cache of per-user breakdown Quantity overrides.
    /// The backend (/rategen-v2/library/user-rates) is the canonical source —
    /// this store mirrors it locally for offline resilience and instant boot.
    ///
    /// Read pattern: ComputeItemN methods call <see cref="TryGetOverride"/>
    /// inside a Qty(...) helper to decide whether to use the user's saved
    /// Quantity or the shipped default.
    ///
    /// Write pattern: editing UI calls <see cref="SetOverride"/> immediately
    /// on each Quantity change so the next RecomputeAll picks up the new
    /// value live, then the parent VM raises <see cref="OverridesChanged"/>.
    /// Persisting to disk + pushing to backend happens on explicit Save.
    /// </summary>
    public sealed class UserRateEditStore
    {
        public static UserRateEditStore Current { get; } = new UserRateEditStore();

        // (sectionKey|itemNo|componentName) → edit
        private readonly ConcurrentDictionary<string, UserRateEdit> _edits =
            new ConcurrentDictionary<string, UserRateEdit>(StringComparer.OrdinalIgnoreCase);

        private readonly object _ioLock = new object();

        /// <summary>Raised after the store mutates so VMs can RecomputeAll.</summary>
        public event EventHandler? OverridesChanged;

        private UserRateEditStore()
        {
            TryLoadFromDisk();
        }

        /* ----------------- Read ----------------- */

        /// <summary>Returns the overridden Quantity, or null if no override exists.</summary>
        public double? TryGetOverride(string sectionKey, int itemNo, string componentName)
        {
            var key = BuildKey(sectionKey, itemNo, componentName);
            return _edits.TryGetValue(key, out var edit) ? edit.OverrideQuantity : (double?)null;
        }

        /// <summary>
        /// Convenience helper for ComputeItemN methods.
        /// Returns the user's overridden Quantity if one exists, otherwise the shipped default.
        /// Use as: <c>var boardsQty = UserRateEditStore.Current.Qty(SectionKey, itemNo, "25 x 300mm x 3600mm boards.", 0.93);</c>
        /// </summary>
        public double Qty(string sectionKey, int itemNo, string componentName, double defaultValue)
        {
            return TryGetOverride(sectionKey, itemNo, componentName) ?? defaultValue;
        }

        /// <summary>True if any overrides exist for this item.</summary>
        public bool HasOverridesForItem(string sectionKey, int itemNo)
        {
            var prefix = BuildItemPrefix(sectionKey, itemNo);
            foreach (var key in _edits.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>True if any overrides exist anywhere.</summary>
        public bool HasAnyOverrides => !_edits.IsEmpty;

        /// <summary>Total override count — used by the global reset confirm dialog.</summary>
        public int TotalOverrideCount => _edits.Count;

        public IReadOnlyList<UserRateEdit> Snapshot() => _edits.Values.ToList();

        /* ----------------- Write ----------------- */

        /// <summary>Upsert an override. Fires OverridesChanged. Does NOT persist to disk — call <see cref="SaveToDisk"/> explicitly.</summary>
        public void SetOverride(string sectionKey, int itemNo, string componentName, double overrideQuantity)
        {
            if (string.IsNullOrWhiteSpace(sectionKey) || string.IsNullOrWhiteSpace(componentName)) return;

            var key = BuildKey(sectionKey, itemNo, componentName);
            _edits[key] = new UserRateEdit
            {
                SectionKey = sectionKey,
                ItemNo = itemNo,
                ComponentName = componentName,
                OverrideQuantity = overrideQuantity,
                EditedAtUtc = DateTime.UtcNow
            };

            RaiseOverridesChanged();
        }

        /// <summary>Remove a single override. Fires OverridesChanged if removed.</summary>
        public bool ClearOverride(string sectionKey, int itemNo, string componentName)
        {
            var key = BuildKey(sectionKey, itemNo, componentName);
            var removed = _edits.TryRemove(key, out _);
            if (removed) RaiseOverridesChanged();
            return removed;
        }

        /// <summary>Remove all overrides for one item (per-popup Reset). Fires OverridesChanged if any removed.</summary>
        public int ClearItem(string sectionKey, int itemNo)
        {
            var prefix = BuildItemPrefix(sectionKey, itemNo);
            var removed = 0;
            foreach (var key in _edits.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (_edits.TryRemove(key, out _)) removed++;
            }
            if (removed > 0) RaiseOverridesChanged();
            return removed;
        }

        /// <summary>Remove all overrides in one section.</summary>
        public int ClearSection(string sectionKey)
        {
            var prefix = (sectionKey ?? string.Empty).ToLowerInvariant() + "|";
            var removed = 0;
            foreach (var key in _edits.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (_edits.TryRemove(key, out _)) removed++;
            }
            if (removed > 0) RaiseOverridesChanged();
            return removed;
        }

        /// <summary>Global reset. Fires OverridesChanged if any removed.</summary>
        public int ClearAll()
        {
            var count = _edits.Count;
            _edits.Clear();
            if (count > 0) RaiseOverridesChanged();
            return count;
        }

        /// <summary>Replace the entire store from server pull. Fires OverridesChanged once.</summary>
        public void ReplaceAll(IEnumerable<UserRateEdit> edits)
        {
            _edits.Clear();
            if (edits != null)
            {
                foreach (var e in edits)
                {
                    if (e == null || string.IsNullOrWhiteSpace(e.SectionKey) || string.IsNullOrWhiteSpace(e.ComponentName)) continue;
                    var key = BuildKey(e.SectionKey, e.ItemNo, e.ComponentName);
                    _edits[key] = e;
                }
            }
            RaiseOverridesChanged();
        }

        /* ----------------- Persistence ----------------- */

        /// <summary>Write current state to %AppData%\ADLMRateGen\user-rate-edits.json atomically.</summary>
        public void SaveToDisk()
        {
            lock (_ioLock)
            {
                try
                {
                    var payload = _edits.Values.ToList();
                    var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
                    AppPaths.AtomicWrite(AppPaths.UserRateEditsFile, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UserRateEditStore] SaveToDisk failed: {ex.Message}");
                }
            }
        }

        /// <summary>Reload state from disk, discarding any in-memory uncommitted edits.
        /// Used by the popup's Cancel button to revert unsaved Quantity changes.</summary>
        public void ReloadFromDisk()
        {
            _edits.Clear();
            TryLoadFromDisk();
            RaiseOverridesChanged();
        }

        private void TryLoadFromDisk()
        {
            lock (_ioLock)
            {
                try
                {
                    var path = AppPaths.UserRateEditsFile;
                    if (!File.Exists(path)) return;

                    var json = File.ReadAllText(path);
                    if (string.IsNullOrWhiteSpace(json)) return;

                    var list = JsonConvert.DeserializeObject<List<UserRateEdit>>(json) ?? new List<UserRateEdit>();
                    foreach (var e in list)
                    {
                        if (e == null || string.IsNullOrWhiteSpace(e.SectionKey) || string.IsNullOrWhiteSpace(e.ComponentName)) continue;
                        var key = BuildKey(e.SectionKey, e.ItemNo, e.ComponentName);
                        _edits[key] = e;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UserRateEditStore] TryLoadFromDisk failed: {ex.Message}");
                }
            }
        }

        /* ----------------- Helpers ----------------- */

        private static string BuildKey(string sectionKey, int itemNo, string componentName)
        {
            // Lowercased section + numeric itemNo + trimmed component name.
            return string.Concat(
                (sectionKey ?? string.Empty).Trim().ToLowerInvariant(),
                "|",
                itemNo.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "|",
                (componentName ?? string.Empty).Trim());
        }

        private static string BuildItemPrefix(string sectionKey, int itemNo)
        {
            return string.Concat(
                (sectionKey ?? string.Empty).Trim().ToLowerInvariant(),
                "|",
                itemNo.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "|");
        }

        private void RaiseOverridesChanged()
        {
            try
            {
                OverridesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserRateEditStore] OverridesChanged handler threw: {ex.Message}");
            }
        }
    }
}
