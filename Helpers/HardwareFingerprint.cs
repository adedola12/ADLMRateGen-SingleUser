using System;
using System.Management;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;

namespace ADLMRateGen.Helpers
{
    /// <summary>
    /// Hardware-bound device fingerprint (v2) for ADLM RateGen.
    ///
    /// Composition: SHA-256( CPU ProcessorId | BIOS SerialNumber | Motherboard SerialNumber ).
    /// Falls back to a MachineGuid-derived value if WMI is unavailable.
    ///
    /// MUST stay byte-identical to the implementations in every other ADLM
    /// app (InstallerHub.DeviceFingerprintService, PlanswiftApp.DeviceFingerprint,
    /// RevitPluginArch.HardwareFingerprint) — the server binds seats by
    /// fingerprint, so divergence would lock users out. Sent alongside the
    /// "x-adlm-fp-version: 2" header so the server can run the v1 to v2
    /// migration path for users coming from the old MAC-based fingerprint
    /// (see ADLM.Auth.DeviceFingerprint).
    /// </summary>
    public static class HardwareFingerprint
    {
        public const int FingerprintVersion = 2;

        private static string _cached;

        public static string Get()
        {
            if (!string.IsNullOrEmpty(_cached)) return _cached;

            try
            {
                string cpu = SafeWmi("Win32_Processor", "ProcessorId");
                string bios = SafeWmi("Win32_BIOS", "SerialNumber");
                string board = SafeWmi("Win32_BaseBoard", "SerialNumber");

                string raw;
                if (string.IsNullOrWhiteSpace(cpu) &&
                    string.IsNullOrWhiteSpace(bios) &&
                    string.IsNullOrWhiteSpace(board))
                {
                    raw = $"FB|{SafeMachineGuid()}|{Environment.MachineName}";
                }
                else
                {
                    raw = string.Join("|",
                        (cpu ?? "").Trim().ToUpperInvariant(),
                        (bios ?? "").Trim().ToUpperInvariant(),
                        (board ?? "").Trim().ToUpperInvariant());
                }

                _cached = Sha256Hex(raw);
                return _cached;
            }
            catch
            {
                try
                {
                    _cached = Sha256Hex($"FB|{SafeMachineGuid()}|{Environment.MachineName}");
                    return _cached;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public static string GetDeviceName() => Environment.MachineName ?? "";

        private static string SafeWmi(string wmiClass, string property)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var val = obj[property]?.ToString();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        private static string SafeMachineGuid()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Cryptography", writable: false))
                {
                    var val = key?.GetValue("MachineGuid") as string;
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
            }
            catch { }
            return "NO_MACHINE_GUID";
        }

        private static string Sha256Hex(string input)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input ?? "");
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
