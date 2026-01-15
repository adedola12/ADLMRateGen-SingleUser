using System;

namespace ADLMRateGen.Services
{
    public static class SectionNormalizer
    {
        public static string ToSectionKey(string? raw)
        {
            var s = (raw ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(s)) return "";

            // normalize separators
            s = s.Replace("&", "and").Replace("-", "_").Replace(" ", "_");

            // Accept many aliases, always return your canonical keys
            if (s.Contains("door") || s.Contains("window")) return SectionKeys.DoorsWindows;
            if (s.Contains("steel")) return SectionKeys.Steelwork;
            if (s.Contains("roof")) return SectionKeys.Roofing;
            if (s.Contains("paint")) return SectionKeys.Paint;
            if (s.Contains("ground") || s.Contains("substructure")) return SectionKeys.Ground;
            if (s.Contains("concrete")) return SectionKeys.Concrete;
            if (s.Contains("finish")) return SectionKeys.Finishes;
            if (s.Contains("block")) return SectionKeys.Blockwork;

            // already canonical?
            foreach (var k in SectionKeys.All)
                if (string.Equals(s, k, StringComparison.OrdinalIgnoreCase))
                    return k;

            return s; // unknown, but don’t crash
        }
    }
}
