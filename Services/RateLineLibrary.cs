using System;
using System.Collections.Generic;
using System.Linq;
using ADLMRateGen.ViewModel.CustomRate;
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.Services
{
    /// <summary>
    /// Keeps the material / labour price libraries in step with rate build-ups.
    ///
    ///  - <see cref="CleanName"/> strips the provenance tags RateGen appends to a
    ///    line description ("[AI]", "(plant)") so it can be matched against, or
    ///    written back to, the library under its real name.
    ///  - <see cref="Harvest"/> folds priced lines that are not yet in the
    ///    library back into it, so anything typed once is reusable next time.
    /// </summary>
    public static class RateLineLibrary
    {
        /// <summary>Category stamped on entries harvested out of a custom rate.</summary>
        public const string HarvestedCategory = "Custom Rate";

        private static readonly string[] Tags = { "[AI]", "(plant)" };

        /// <summary>
        /// Returns the library-facing name for a rate line, i.e. the description
        /// with any trailing provenance tags removed.
        /// </summary>
        public static string CleanName(string? description)
        {
            var name = (description ?? string.Empty).Trim();

            bool stripped;
            do
            {
                stripped = false;
                foreach (var tag in Tags)
                {
                    if (name.EndsWith(tag, StringComparison.OrdinalIgnoreCase))
                    {
                        name = name.Substring(0, name.Length - tag.Length).TrimEnd();
                        stripped = true;
                    }
                }
            } while (stripped && name.Length > 0);

            return name;
        }

        /// <summary>
        /// Adds every priced line that is not already in the library.
        /// Entries that already exist are left untouched — a one-off rate must
        /// never silently rewrite the master price list.
        /// </summary>
        /// <returns>How many new library entries were created.</returns>
        public static int Harvest(
            IEnumerable<RateEntryItem>? materialItems,
            IEnumerable<RateEntryItem>? labourItems)
        {
            var newMaterials = CollectMaterials(materialItems);
            var newLabours = CollectLabours(labourItems);

            if (newMaterials.Count > 0)
                MaterialLibraryService.AddOrUpdateMaterials(newMaterials);

            if (newLabours.Count > 0)
                LabourLibraryService.AddOrUpdateLabours(newLabours);

            return newMaterials.Count + newLabours.Count;
        }

        private static List<MaterialModel> CollectMaterials(IEnumerable<RateEntryItem>? items)
        {
            var added = new List<MaterialModel>();
            if (items == null) return added;

            var nextSerial = NextSerial(MaterialLibraryService.GetAllMaterials().Select(m => m.SerialNumber));

            foreach (var item in items)
            {
                var name = CleanName(item?.Description);
                if (name.Length == 0 || item!.UnitPrice <= 0m) continue;
                if (MaterialLibraryService.FindByName(name) != null) continue;
                if (added.Any(m => string.Equals(m.MaterialName, name, StringComparison.OrdinalIgnoreCase))) continue;

                added.Add(new MaterialModel
                {
                    SerialNumber = nextSerial++,
                    MaterialName = name,
                    MaterialUnit = item.Unit ?? string.Empty,
                    MaterialPrice = item.UnitPrice,
                    MaterialCategory = HarvestedCategory
                });
            }

            return added;
        }

        private static List<LabourModel> CollectLabours(IEnumerable<RateEntryItem>? items)
        {
            var added = new List<LabourModel>();
            if (items == null) return added;

            var nextSerial = NextSerial(LabourLibraryService.GetAllLabours().Select(l => l.SerialNumber));

            foreach (var item in items)
            {
                var name = CleanName(item?.Description);
                if (name.Length == 0 || item!.UnitPrice <= 0m) continue;
                if (LabourLibraryService.FindByName(name) != null) continue;
                if (added.Any(l => string.Equals(l.LabourName, name, StringComparison.OrdinalIgnoreCase))) continue;

                added.Add(new LabourModel
                {
                    SerialNumber = nextSerial++,
                    LabourName = name,
                    LabourUnit = item.Unit ?? string.Empty,
                    LabourPrice = item.UnitPrice,
                    LabourCategory = HarvestedCategory
                });
            }

            return added;
        }

        private static int NextSerial(IEnumerable<int> existing)
        {
            var max = 0;
            foreach (var n in existing)
                if (n > max) max = n;
            return max + 1;
        }
    }
}
