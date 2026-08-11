using System;
using System.Collections.Generic;
using System.Linq;

namespace ADLMRateGen.Helpers
{
    /// <summary>
    /// The 36 states of Nigeria plus the FCT, each with the geopolitical zone it
    /// belongs to.
    ///
    /// This mirrors ADLMWebsite/server/util/states.js and the two must agree: the
    /// key sent as ?state= has to be a key the server recognises, or the server
    /// silently falls back to a zone and the user prices against the wrong place
    /// without being told.
    ///
    /// Prices are evidenced at ZONE level. A state carries its zone's price until
    /// someone prices that state specifically, so Kano and Katsina are the same
    /// today. Choosing a state is still worth doing: it is what lets them differ
    /// later without anyone changing a schema.
    /// </summary>
    public sealed class NigerianState
    {
        public string Key { get; init; } = "";
        public string Label { get; init; } = "";
        public string Zone { get; init; } = "";
        public override string ToString() => Label;
    }

    public static class NigerianStates
    {
        public static readonly IReadOnlyList<NigerianState> All = new List<NigerianState>
        {
            new() { Key = "abia",        Label = "Abia",         Zone = "south_east" },
            new() { Key = "adamawa",     Label = "Adamawa",      Zone = "north_east" },
            new() { Key = "akwa_ibom",   Label = "Akwa Ibom",    Zone = "south_south" },
            new() { Key = "anambra",     Label = "Anambra",      Zone = "south_east" },
            new() { Key = "bauchi",      Label = "Bauchi",       Zone = "north_east" },
            new() { Key = "bayelsa",     Label = "Bayelsa",      Zone = "south_south" },
            new() { Key = "benue",       Label = "Benue",        Zone = "north_central" },
            new() { Key = "borno",       Label = "Borno",        Zone = "north_east" },
            new() { Key = "cross_river", Label = "Cross River",  Zone = "south_south" },
            new() { Key = "delta",       Label = "Delta",        Zone = "south_south" },
            new() { Key = "ebonyi",      Label = "Ebonyi",       Zone = "south_east" },
            new() { Key = "edo",         Label = "Edo",          Zone = "south_south" },
            new() { Key = "ekiti",       Label = "Ekiti",        Zone = "south_west" },
            new() { Key = "enugu",       Label = "Enugu",        Zone = "south_east" },
            new() { Key = "fct",         Label = "FCT (Abuja)",  Zone = "north_central" },
            new() { Key = "gombe",       Label = "Gombe",        Zone = "north_east" },
            new() { Key = "imo",         Label = "Imo",          Zone = "south_east" },
            new() { Key = "jigawa",      Label = "Jigawa",       Zone = "north_west" },
            new() { Key = "kaduna",      Label = "Kaduna",       Zone = "north_west" },
            new() { Key = "kano",        Label = "Kano",         Zone = "north_west" },
            new() { Key = "katsina",     Label = "Katsina",      Zone = "north_west" },
            new() { Key = "kebbi",       Label = "Kebbi",        Zone = "north_west" },
            new() { Key = "kogi",        Label = "Kogi",         Zone = "north_central" },
            new() { Key = "kwara",       Label = "Kwara",        Zone = "north_central" },
            new() { Key = "lagos",       Label = "Lagos",        Zone = "south_west" },
            new() { Key = "nasarawa",    Label = "Nasarawa",     Zone = "north_central" },
            new() { Key = "niger",       Label = "Niger",        Zone = "north_central" },
            new() { Key = "ogun",        Label = "Ogun",         Zone = "south_west" },
            new() { Key = "ondo",        Label = "Ondo",         Zone = "south_west" },
            new() { Key = "osun",        Label = "Osun",         Zone = "south_west" },
            new() { Key = "oyo",         Label = "Oyo",          Zone = "south_west" },
            new() { Key = "plateau",     Label = "Plateau",      Zone = "north_central" },
            new() { Key = "rivers",      Label = "Rivers",       Zone = "south_south" },
            new() { Key = "sokoto",      Label = "Sokoto",       Zone = "north_west" },
            new() { Key = "taraba",      Label = "Taraba",       Zone = "north_east" },
            new() { Key = "yobe",        Label = "Yobe",         Zone = "north_east" },
            new() { Key = "zamfara",     Label = "Zamfara",      Zone = "north_west" },
        };

        public const string DefaultKey = "lagos";

        /// <summary>
        /// Accepts a key, a label, or the loose spellings that are already sitting
        /// in config files: "Lagos", "lagos state", "Abuja", "Akwa-Ibom".
        /// Returns null when nothing matches, so the caller can decide rather than
        /// being handed a silent default.
        /// </summary>
        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            var k = new string(input.Trim().ToLowerInvariant()
                        .Select(c => char.IsLetter(c) || c == ' ' ? c : ' ').ToArray());
            k = string.Join(" ", k.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                  .Where(w => w != "state"));

            if (k is "abuja" or "federal capital territory" or "fct abuja") return "fct";

            var underscored = k.Replace(' ', '_');
            var byKey = All.FirstOrDefault(s => s.Key == underscored);
            if (byKey != null) return byKey.Key;

            var flat = new string(k.Where(char.IsLetter).ToArray());
            var byLabel = All.FirstOrDefault(s =>
                new string(s.Label.ToLowerInvariant().Where(char.IsLetter).ToArray()) == flat);
            return byLabel?.Key;
        }

        public static NigerianState Find(string keyOrLabel)
        {
            var k = Normalize(keyOrLabel);
            return k == null ? null : All.FirstOrDefault(s => s.Key == k);
        }

        public static string ZoneFor(string keyOrLabel) => Find(keyOrLabel)?.Zone;
    }
}
