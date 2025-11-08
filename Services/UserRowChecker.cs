using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADLMRateGen.Services
{
    public static class UserRowChecker
    {
        public static bool IsUserMaterial(int sn, string name) =>
            UserLibrarySync.Instance.MyMaterials.Any(m => m.sn == sn ||
                m.description.Equals(name ?? "", System.StringComparison.OrdinalIgnoreCase));

        public static bool IsUserLabour(int sn, string name) =>
            UserLibrarySync.Instance.MyLabour.Any(l => l.sn == sn ||
                l.description.Equals(name ?? "", System.StringComparison.OrdinalIgnoreCase));
    }
}
