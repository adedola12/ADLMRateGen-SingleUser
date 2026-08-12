using System;
using System.Windows.Input;
using ADLMRateGen.Command;

namespace ADLMRateGen.Helpers
{
    /// <summary>
    /// Turns a breakdown line into a link to the library row it is priced from.
    ///
    /// WHY THIS IS SHARED AND STATIC
    /// There are ten breakdown line types, one per section, and they are already
    /// near-identical copies of each other. Wiring a command and an event through
    /// ten view models, ten popup hosts and ten XAML files would be ten chances to
    /// get it subtly wrong, and the Carbon version already proved how quiet that
    /// failure is: a binding that resolves to the wrong object produces a link
    /// that looks right and does nothing.
    ///
    /// Instead each line exposes this one command, bound directly against its own
    /// data context. No RelativeSource, no Tag, nothing to thread through a view
    /// model. The two hooks below are set once at startup.
    /// </summary>
    public static class LibraryLink
    {
        /// <summary>Called with (kind, name) to show the library at a row.</summary>
        public static Action<string, string> Navigate { get; set; }

        /// <summary>Does a material of this exact name exist in the library?</summary>
        public static Func<string, bool> IsMaterial { get; set; }

        /// <summary>Does a labour row of this exact name exist?</summary>
        public static Func<string, bool> IsLabour { get; set; }

        /// <summary>
        /// Does ANY material name contain this term? The hardcoded sections label
        /// their lines for reading rather than for lookup: "Cement" where the
        /// library says "Cement (50kg bag)", "Mason." with a full stop. Requiring
        /// an exact match linked only 23% of the lines that name a real item;
        /// falling back to a contains match takes it to 48%.
        ///
        /// A loose match is still useful because the link FILTERS the library by
        /// the term rather than selecting one row, so "Sand" showing every sand
        /// row is a good answer rather than a wrong one.
        /// </summary>
        public static Func<string, bool> MaterialContains { get; set; }

        /// <summary>As above, for labour.</summary>
        public static Func<string, bool> LabourContains { get; set; }

        /// <summary>
        /// Work out what library row a displayed component name refers to.
        ///
        /// Names arrive in two shapes. Cloud compute items render as
        /// "material: Cement (50kg bag)", carrying their own kind. Hardcoded
        /// sections write the bare library name, "Diesel" or "Operator", and the
        /// kind has to be discovered by looking.
        ///
        /// Materials are checked first because that is the larger library and the
        /// common case. Anything that matches neither returns false, which is what
        /// keeps totals, warnings and percentage rows from pretending to be links.
        /// </summary>
        public static bool TryResolve(string componentName, out string kind, out string name)
        {
            kind = null;
            name = null;

            var s = (componentName ?? "").Trim();
            if (s.Length == 0) return false;

            // Rows that are arithmetic or commentary, never library items.
            if (s.StartsWith("-") || s.StartsWith("⚠")) return false;
            if (s.IndexOf("Compute PO/Uplift", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (s.IndexOf("total", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            // "material: X" / "labour: X" state their own kind.
            var colon = s.IndexOf(':');
            if (colon > 0)
            {
                var prefix = s.Substring(0, colon).Trim();
                var rest = s.Substring(colon + 1).Trim();
                if (prefix.Equals("material", StringComparison.OrdinalIgnoreCase))
                {
                    kind = "material"; name = rest; return true;
                }
                if (prefix.Equals("labour", StringComparison.OrdinalIgnoreCase) ||
                    prefix.Equals("labor", StringComparison.OrdinalIgnoreCase))
                {
                    kind = "labour"; name = rest; return true;
                }
                // Any other colon belongs to the name itself, so fall through
                // rather than truncating it. Several library rows carry one.
            }

            // Trailing punctuation is presentation, not part of a name: several
            // sections write "Mason." and "Mixing crew - labour."
            var term = s.TrimEnd('.', ',', ';', ':').Trim();
            if (term.Length == 0) return false;

            if (IsMaterial?.Invoke(term) == true) { kind = "material"; name = term; return true; }
            if (IsLabour?.Invoke(term) == true) { kind = "labour"; name = term; return true; }

            // Nothing named exactly that, but something in the library mentions it.
            // Worth offering, because the link filters rather than selects.
            if (MaterialContains?.Invoke(term) == true) { kind = "material"; name = term; return true; }
            if (LabourContains?.Invoke(term) == true) { kind = "labour"; name = term; return true; }

            return false;
        }

        public static bool CanOpen(string componentName) => TryResolve(componentName, out _, out _);

        /// <summary>
        /// Bound by every breakdown template. Takes the component name as its
        /// parameter so no line type needs to carry extra state.
        /// </summary>
        public static ICommand OpenCommand { get; } = new RelayCommand(p =>
        {
            if (TryResolve(p as string, out var kind, out var name))
                Navigate?.Invoke(kind, name);
        });
    }
}
