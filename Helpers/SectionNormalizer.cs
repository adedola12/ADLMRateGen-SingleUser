using System;
using System.Text.RegularExpressions;

namespace ADLMRateGen.Services
{
    public static class SectionNormalizer
    {
        public static string ToSectionKey(string? raw)
        {
            var s = (raw ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(s)) return "";

            // normalize separators + common variants
            s = s.Replace("&", "and")
                 .Replace("/", " ")
                 .Replace("-", " ")
                 .Replace("_", " ");

            s = Regex.Replace(s, @"\s+", " ").Trim();

            // map to canonical keys
            if (s.Contains("door") || s.Contains("window")) return SectionKeys.DoorsWindows;
            if (s.Contains("steel")) return SectionKeys.Steelwork;
            if (s.Contains("roof")) return SectionKeys.Roofing;
            if (s.Contains("paint")) return SectionKeys.Paint;
            if (s.Contains("ground") || s.Contains("substructure")) return SectionKeys.Ground;
            if (s.Contains("concrete")) return SectionKeys.Concrete;
            if (s.Contains("finish")) return SectionKeys.Finishes;
            if (s.Contains("block")) return SectionKeys.Blockwork;
            if (s.Contains("carbon") || s.Contains("other")) return SectionKeys.CarbonOthers;

            // if it's already canonical, return it
            var canonical = s.Replace(" ", "_");
            foreach (var k in SectionKeys.All)
                if (string.Equals(canonical, k, StringComparison.OrdinalIgnoreCase))
                    return k;

            return canonical; // unknown, but don’t crash
        }
    }
}
