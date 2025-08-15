using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace ADLMRateGen.Helpers
{
    public static class DeviceFingerprint
    {
        public static string Generate()
        {
            // Try to collect 2–3 stable hardware IDs
            string cpu = GetWMI("Win32_Processor", "ProcessorId");
            string bios = GetWMI("Win32_BIOS", "SerialNumber");
            string baseboard = GetWMI("Win32_BaseBoard", "SerialNumber");

            var raw = $"{cpu}|{bios}|{baseboard}";
            if (string.IsNullOrWhiteSpace(raw) || raw.Replace("|", "").Length == 0)
                return null; // don’t fingerprint on empty

            var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return BitConverter.ToString(hash).Replace("-", "");
        }

        private static string GetWMI(string className, string property)
        {
            try
            {
                var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {className}");
                foreach (var obj in searcher.Get())
                    return obj[property]?.ToString() ?? "";
            }
            catch
            {
                // swallow and continue; we’ll hash whatever we get
            }
            return "";
        }
    }
}
