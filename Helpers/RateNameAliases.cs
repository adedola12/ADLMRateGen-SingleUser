using System.Collections.Generic;

namespace ADLMRateGen.Helpers
{
    /// <summary>
    /// Former spellings of catalogue rows, so a build-up written against either
    /// one still finds its price.
    ///
    /// Rate names are keys: GetLabourRate does an exact string match and returns
    /// zero when it misses, which prices a build-up line at nothing without
    /// saying so. That makes correcting a name in the master library a breaking
    /// change for every copy of the app already installed, because those builds
    /// keep asking for the old spelling until their user updates through the Hub.
    ///
    /// Keeping the old spellings here decouples the two: the library can be
    /// corrected whenever, and a build carrying this table prices correctly
    /// against a library that has been renamed and one that has not.
    ///
    /// An entry becomes safe to delete once no shipped build asks for that name,
    /// which in practice means a long time after the rename. There is no cost to
    /// leaving it: it is consulted only after an exact match has already failed.
    /// </summary>
    public static class RateNameAliases
    {
        /// <summary>Every spelling a row has been known by, grouped together.</summary>
        private static readonly string[][] Groups =
        {
            // "whelled" is not a word; corrected 2026-08.
            new[] { "Vibratory whelled roller (8 to 10 tons)",  "Vibratory wheeled roller (8 to 10 tons)" },
            new[] { "Vibratory whelled roller (10 to 20 tons)", "Vibratory wheeled roller (10 to 20 tons)" },

            // A pneumatic roller runs on tyres; corrected 2026-08.
            new[] { "Pneumatic tired roller (2.7 to 10 tonnes)",  "Pneumatic tyred roller (2.7 to 10 tonnes)" },
            new[] { "Pneumatic tired roller (10 to 20 tonnes)",   "Pneumatic tyred roller (10 to 20 tonnes)" },
            new[] { "Pneumatic tired roller (20 to 31.8 tonnes)", "Pneumatic tyred roller (20 to 31.8 tonnes)" },
        };

        private static readonly Dictionary<string, string[]> ByName = Build();

        private static Dictionary<string, string[]> Build()
        {
            var map = new Dictionary<string, string[]>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var group in Groups)
                foreach (var name in group)
                    map[name] = group;
            return map;
        }

        /// <summary>
        /// The other names this row has gone by, excluding the one asked for.
        /// Empty when the name has never been changed, which is the usual case.
        /// </summary>
        public static IEnumerable<string> Alternates(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) yield break;
            if (!ByName.TryGetValue(name, out var group)) yield break;

            foreach (var candidate in group)
                if (!string.Equals(candidate, name, System.StringComparison.OrdinalIgnoreCase))
                    yield return candidate;
        }
    }
}
