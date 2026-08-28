using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ADLMRateGen.Helpers
{
    /// <summary>
    /// What a labour or plant item is, and what it produces in a day.
    ///
    /// Reference reading only. Nothing here feeds a calculation: it exists so an
    /// estimator pricing a day rate can see the output that rate has to cover,
    /// which is the number a day rate is really a proxy for.
    /// </summary>
    public sealed class LabourSpec
    {
        /// <summary>What the machine or trade is, in one line.</summary>
        public string Spec { get; set; } = "";

        /// <summary>Expected output, always a range.</summary>
        public string Output { get; set; } = "";

        /// <summary>
        /// The assumption the output rests on. Never omit this where an output is
        /// given: soil, haul, layer depth and gang size move these figures by more
        /// than the choice of machine does, and a bare number reads as a promise.
        /// </summary>
        public string Basis { get; set; } = "";

        /// <summary>Fuel burn for plant, or gang make-up for a trade.</summary>
        public string Running { get; set; } = "";

        /// <summary>Where the figures come from, so a user can weigh them.</summary>
        public string Source { get; set; } = "";

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Spec) &&
            string.IsNullOrWhiteSpace(Output) &&
            string.IsNullOrWhiteSpace(Basis) &&
            string.IsNullOrWhiteSpace(Running);
    }

    /// <summary>
    /// Loads the bundled specification reference.
    ///
    /// Shipped with the build rather than held in the master library: this is
    /// editorial reference text, not zone-priced data, and the sync deliberately
    /// overwrites the local catalogue from the server on every run. Putting it in
    /// the catalogue would mean carrying it through that rewrite for no gain.
    /// </summary>
    public static class LabourSpecs
    {
        private static Dictionary<string, LabourSpec>? _cache;

        private static string FilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "labourSpecs.json");

        private static Dictionary<string, LabourSpec> All
        {
            get
            {
                if (_cache != null) return _cache;

                var map = new Dictionary<string, LabourSpec>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    if (File.Exists(FilePath))
                    {
                        var root = JObject.Parse(File.ReadAllText(FilePath));
                        foreach (var prop in root.Properties())
                        {
                            // The file documents itself in a _readme array; skip the
                            // note rather than trying to read it as a spec.
                            if (prop.Name.StartsWith("_")) continue;
                            if (prop.Value.Type != JTokenType.Object) continue;

                            var spec = prop.Value.ToObject<LabourSpec>();
                            if (spec != null && !spec.IsEmpty) map[prop.Name] = spec;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Reference text is never worth failing a screen over.
                    System.Diagnostics.Debug.WriteLine($"[LabourSpecs] load failed: {ex.Message}");
                }

                return _cache = map;
            }
        }

        /// <summary>
        /// The entry for a rate, or null when none is recorded. Falls back through
        /// the row's former spellings, so an entry survives the catalogue being
        /// renamed underneath it.
        /// </summary>
        public static LabourSpec? Find(string labourName)
        {
            if (string.IsNullOrWhiteSpace(labourName)) return null;
            if (All.TryGetValue(labourName, out var hit)) return hit;

            foreach (var alias in RateNameAliases.Alternates(labourName))
                if (All.TryGetValue(alias, out var viaAlias))
                    return viaAlias;

            return null;
        }

        /// <summary>Names carrying an entry. Used by the coverage test.</summary>
        public static IReadOnlyCollection<string> CoveredNames => All.Keys.ToList();
    }
}
