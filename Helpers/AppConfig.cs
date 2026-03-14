namespace ADLMRateGen.Helpers
{
    public class AppConfig
    {
        public string? AuthToken { get; set; }
        public DateTime? AuthExpiry { get; set; }

        public string Zone { get; set; } = "Lagos";
        public bool IsDarkTheme { get; set; }

        // ✅ catalog tracking
        public int LastCatalogVersion { get; set; } = 0;
        public DateTime LastCatalogCheckUtc { get; set; } = DateTime.MinValue;
    }
}
