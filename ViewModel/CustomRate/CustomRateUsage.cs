using ADLMRateGen.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADLMRateGen.ViewModel.CustomRate
{
    public static class CustomRateUsage
    {
        public static int CountMaterialUsage(string name) =>
            CustomRateServices.LoadCustomRates()
                .Sum(r => r.MaterialItems.Count(i => i.Description?.Equals(name, System.StringComparison.OrdinalIgnoreCase) == true));

        public static int CountLabourUsage(string name) =>
            CustomRateServices.LoadCustomRates()
                .Sum(r => r.LabourItems.Count(i => i.Description?.Equals(name, System.StringComparison.OrdinalIgnoreCase) == true));

        public static void RemoveMaterialEverywhere(string name)
        {
            var all = CustomRateServices.LoadCustomRates().ToList();
            foreach (var r in all)
                r.MaterialItems.RemoveAll(i => i.Description?.Equals(name, System.StringComparison.OrdinalIgnoreCase) == true);
            CustomRateServices.SaveRates(all);
        }

        public static void RemoveLabourEverywhere(string name)
        {
            var all = CustomRateServices.LoadCustomRates().ToList();
            foreach (var r in all)
                r.LabourItems.RemoveAll(i => i.Description?.Equals(name, System.StringComparison.OrdinalIgnoreCase) == true);
            CustomRateServices.SaveRates(all);
        }
    }
}
