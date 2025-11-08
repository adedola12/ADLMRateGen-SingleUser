using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace ADLMRateGen.Helpers
{
    internal static class NetChecks
    {
        public static bool IsOnline()
            => NetworkInterface.GetIsNetworkAvailable();

        public static async Task<bool> CanReachAdlmAsync(ADLMRateGen.ADLM.Auth.AuthClient auth)
        {
            try { await auth.PingAsync(); return true; }
            catch { return false; }
        }
    }

}
