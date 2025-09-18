using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace ADLMRateGen.ADLM.Auth
{
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
