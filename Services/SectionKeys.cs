namespace ADLMRateGen.Services
{
    public static class SectionKeys
    {
        public const string Ground = "ground";
        public const string Concrete = "concrete";
        public const string Blockwork = "blockwork";
        public const string Finishes = "finishes";
        public const string Roofing = "roofing";
        public const string DoorsWindows = "doors_windows";
        public const string Paint = "paint";
        public const string Steelwork = "steelwork";
        public const string CarbonOthers = "carbon";

        public static readonly string[] All =
        {
            Ground,
            Concrete,
            Blockwork,
            Finishes,
            Roofing,
            DoorsWindows,
            Paint,
            Steelwork,
            CarbonOthers // ✅ IMPORTANT: add this
        };
    }
}
