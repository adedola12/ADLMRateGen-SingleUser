using System;

namespace ADLMRateGen.ViewModel.Model
{
    /// <summary>
    /// A single user-saved Quantity override for one component row inside
    /// one item's rate build-up. Unit Price and other fields are intentionally
    /// NOT overridable here — those come from the Material / Labour libraries
    /// and remain centrally managed.
    /// </summary>
    public sealed class UserRateEdit
    {
        /// <summary>Section key as defined in <see cref="Services.SectionKeys"/> (e.g. "concrete", "blockwork").</summary>
        public string SectionKey { get; set; } = string.Empty;

        /// <summary>1-based item number within the section (matches ComputeItemN).</summary>
        public int ItemNo { get; set; }

        /// <summary>Exact ComponentName string of the breakdown line being overridden.</summary>
        public string ComponentName { get; set; } = string.Empty;

        /// <summary>The user's overridden Quantity value.</summary>
        public double OverrideQuantity { get; set; }

        /// <summary>UTC timestamp of last edit. Used for sync conflict resolution.</summary>
        public DateTime EditedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
