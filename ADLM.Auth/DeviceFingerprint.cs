using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace ADLMRateGen.ADLM.Auth
{
    /// <summary>
    /// LEGACY (v1) device fingerprint — MachineName + fastest active NIC MAC + UserName.
    ///
    /// DO NOT use this to bind new devices. It is unstable across sessions: the
    /// selected NIC changes whenever a VPN, dock, USB ethernet or Wi-Fi adapter
    /// comes up or goes down, which produces a different hash on the same
    /// physical machine and makes the server reject the login as DEVICE_MISMATCH.
    /// Helpers.HardwareFingerprint (v2) is the current algorithm.
    ///
    /// This is kept ONLY so the client can present its old fingerprint during the
    /// v1 to v2 migration window, letting the server recognise an existing binding
    /// and re-bind it to the stable value. The algorithm must stay frozen — any
    /// change here breaks migration for users who have not yet upgraded.
    /// </summary>
    public static class DeviceFingerprint
    {
        public static string Generate()
        {
            try
            {
                var sb = new StringBuilder();

                sb.Append(Environment.MachineName ?? "");

                var mac = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic =>
                        nic.OperationalStatus == OperationalStatus.Up &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .OrderByDescending(nic => nic.Speed)
                    .Select(nic => nic.GetPhysicalAddress()?.ToString())
                    .FirstOrDefault(addr => !string.IsNullOrWhiteSpace(addr)) ?? "";

                sb.Append(mac);
                sb.Append(Environment.UserName ?? "");



                using (var sha = SHA256.Create())
                {
                    var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    var hash = sha.ComputeHash(bytes);
                    var hex = new StringBuilder(hash.Length * 2);
                    foreach (var b in hash) hex.Append(b.ToString("x2"));
                    return hex.ToString();
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
