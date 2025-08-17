using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace ADLMRateGen.Helpers
{
    public static class DeviceFingerprint
    {
        public static string Generate()
        {
            var parts = new List<string>();

            // 1) Machine GUID (most stable)
            parts.Add(ReadMachineGuid());

            // 2) Primary MAC addresses (stable-ish)
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic == null) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;

                    var mac = nic.GetPhysicalAddress()?.ToString();
                    if (!string.IsNullOrWhiteSpace(mac)) parts.Add(mac);
                }
            }
            catch { /* ignore */ }

            // 3) System drive volume serial (via Win32 API)
            try
            {
                var sysDrive = Path.GetPathRoot(Environment.SystemDirectory);
                var volSerial = GetVolumeSerial(sysDrive ?? "C:\\");
                if (!string.IsNullOrWhiteSpace(volSerial)) parts.Add(volSerial);
            }
            catch { /* ignore */ }

            // 4) Some OS/machine facts to salt
            parts.Add(Environment.MachineName);
            parts.Add(Environment.OSVersion.VersionString);
            parts.Add(Environment.ProcessorCount.ToString());

            var raw = string.Join("|", parts);
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return BitConverter.ToString(hash).Replace("-", "");
        }

        private static string ReadMachineGuid()
        {
            // Try 64-bit view first, then 32-bit, then fallback
            string Read(RegistryView view)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", false);
                    return key?.GetValue("MachineGuid")?.ToString() ?? string.Empty;
                }
                catch { return string.Empty; }
            }

            var guid = Read(RegistryView.Registry64);
            if (string.IsNullOrWhiteSpace(guid))
                guid = Read(RegistryView.Registry32);
            return guid ?? string.Empty;
        }

        // P/Invoke for volume serial; avoids System.Management/WMI
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetVolumeInformation(
            string rootPathName,
            StringBuilder volumeNameBuffer, int volumeNameSize,
            out uint volumeSerialNumber,
            out uint maximumComponentLength,
            out uint fileSystemFlags,
            StringBuilder fileSystemNameBuffer, int nFileSystemNameSize);

        private static string GetVolumeSerial(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath)) rootPath = "C:\\";
            if (!rootPath.EndsWith("\\", StringComparison.Ordinal)) rootPath += "\\";

            var volName = new StringBuilder(261);
            var fsName = new StringBuilder(261);
            if (GetVolumeInformation(rootPath, volName, volName.Capacity,
                                     out uint serial, out _, out _, fsName, fsName.Capacity))
            {
                return serial.ToString("X"); // hex
            }
            return string.Empty;
        }
    }
}
