using System;
using System.Collections.Generic;
using System.Linq;

namespace ADLMRateGen.Services
{
    /// <summary>
    /// Rows where the user's price and the server's price disagree.
    ///
    /// The sync preserves the user's figure and parks the disagreement here rather
    /// than interrupting with a dialog mid-sign-in. The UI can then raise it when
    /// the user is actually looking at the library, and nothing is lost either way:
    /// their price is already what the app is using.
    ///
    /// Only rows where the server has MOVED since the last sync are worth raising.
    /// A user who edits a price the server has never changed should simply keep it,
    /// with no question asked, forever.
    /// </summary>
    public static class PriceConflicts
    {
        private static readonly object _gate = new();
        private static readonly Dictionary<string, SyncBaseline.EditedRow> _pending =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raised after the pending set changes, so a view can refresh.</summary>
        public static event Action Changed;

        public static void Record(IEnumerable<SyncBaseline.EditedRow> edits)
        {
            if (edits == null) return;
            bool changed = false;
            lock (_gate)
            {
                foreach (var e in edits.Where(e => e.ServerChanged))
                {
                    // Keyed by row and side, because a material and a labour row can
                    // legitimately share a name and unit.
                    _pending[(e.IsLabour ? "L|" : "M|") + e.RowKey] = e;
                    changed = true;
                }
            }
            if (changed) Changed?.Invoke();
        }

        public static IReadOnlyList<SyncBaseline.EditedRow> Pending
        {
            get { lock (_gate) return _pending.Values.OrderBy(e => e.IsLabour).ThenBy(e => e.Name).ToList(); }
        }

        public static int Count { get { lock (_gate) return _pending.Count; } }

        public static bool Any => Count > 0;

        /// <summary>Drop rows the user has dealt with, whichever way they chose.</summary>
        public static void Resolve(IEnumerable<SyncBaseline.EditedRow> rows)
        {
            if (rows == null) return;
            lock (_gate)
            {
                foreach (var r in rows)
                    _pending.Remove((r.IsLabour ? "L|" : "M|") + r.RowKey);
            }
            Changed?.Invoke();
        }

        /// <summary>
        /// The user has looked and decided to keep their own prices. Their figures
        /// are already in place, so this only stops the app asking again.
        /// </summary>
        public static void KeepMine()
        {
            lock (_gate) _pending.Clear();
            Changed?.Invoke();
        }
    }
}
