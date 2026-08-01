# ADLM Rate Gen — Release Notes

## v2.6.0

### Your custom rates now build up your price library

Every material and labour line you price on a custom rate is added to your library when
you save it. Type a material once and it is in the dropdown — with its rate and unit —
for every rate you build afterwards.

Prices you already have are never changed. If the library already knows an item, saving
a rate that uses it at a different figure leaves your library price alone; only items
the library has never seen are added. New entries appear under the category
**Custom Rate**. The save confirmation tells you how many were added.

### Build with AI now uses your prices

When the AI drafts a rate build-up, any component that exists in your library is priced
**from your library**, at your rate and your unit — the AI's own figure is discarded.
Only components your library has never heard of keep the AI's estimate, and those stay
tagged `[AI]` so you can see at a glance which lines still need your review.

Previously the AI's price overrode your library on every line.

### Press Enter to sign in

Enter from either the email or the password field signs you in. Enter on
"Forgot password?" and "Create account" still opens those pages.

### Overhead and Profit are readable again

The Overhead and Profit boxes were rendering their value clipped out of view, so they
looked empty. Both now show their percentage clearly, with the cash value each one adds
shown underneath, and the totals block gained **Overhead**, **Profit** and a
**Grand Total** line — the grand total was being calculated but never displayed.

---

### Technical detail

- Added `Services/RateLineLibrary.cs` — `CleanName()` strips the `[AI]` / `(plant)`
  provenance tags from a line description; `Harvest()` upserts unseen priced lines into
  `MaterialLibraryService` / `LabourLibraryService`. Existing entries are skipped, not
  overwritten. Called from `CustomRateEntryViewModel.SaveCustomRate()` inside a
  try/catch so a library write failure cannot make a successful rate save look failed.
- `Services/AiRateService.MapToCustomRate()` now resolves each component against the
  local library first and stores matches under the library's canonical name, letting
  `RateEntryItem.ResolveUnitPrice()` supply price and unit. It previously assigned the
  server price last specifically to override that lookup.
- `CustomRateEntryViewModel` exposes `OverheadPercentText` / `ProfitPercentText`
  (string) plus `OverheadAmount` / `ProfitAmount`. The percent boxes bound straight to
  `decimal`, and a partial entry ("", "1.") failed to convert and blanked the field.
- Fixed `FInputOHP` in `CustomRateEntryView.xaml`: the style set a fixed `Height` while
  `FInput`'s template applies `Padding` to the Border around a vertically-centred
  `PART_ContentHost`, leaving ~23px of content slot and clipping the text out of view.
  Now `MinHeight` with zero vertical padding.
- Fixed `CustomRateEntryViewModel.LoadRate()`: `RateType` was assigned last in the
  object initializer, re-triggering the library lookup and zeroing the price of any
  saved labour line the library does not know (all `[AI]` lines).
- Added `KeyDown` handling on the sign-in fields rather than making Log in the window's
  default button, so Enter on the footer links keeps its own behaviour.

---

## v2.5.0

### Fixed: repeated "already bound to another device" sign-in failures

Some customers were blocked at sign-in with **"This subscription is already bound to
another device (DEVICE_MISMATCH)"** every time they logged in — on the same computer,
after restarting, and after updating, without ever having signed in elsewhere.

The cause was the way Rate Gen identified your computer. It used the network adapter
that happened to be fastest and active at the moment you signed in. That adapter
changes when you connect a docking station or USB network adapter, when a VPN starts
or stops, or when the machine switches between Wi-Fi and a cable — and on laptops with
both Wi-Fi and Ethernet it could change on its own as Wi-Fi signal strength varied.
Each change made the same computer look like a brand-new device to the licence server.

Rate Gen now identifies your computer from hardware that does not change between
sessions — the processor, BIOS and motherboard identifiers — matching the method
already used by ADLM Installer Hub, PlanSwift, QUIV and the Revit plugins.

**You do not need to do anything.** Your existing licence is recognised and moved
across to the new method automatically the first time you sign in after updating.
Docking stations, VPNs, USB adapters and Wi-Fi/Ethernet switching no longer affect
sign-in.

If you were locked out before this update, sign in once after installing v2.5.0 and
access is restored.

---

### Technical detail

- Added `Helpers/HardwareFingerprint.cs` — `SHA-256(CPU ProcessorId | BIOS Serial |
  Baseboard Serial)`, byte-identical to the implementations in ADLM Installer Hub,
  PlanSwift, QUIV, RevitPluginArch2026 and the Revit MEP plugin, with a
  MachineGuid-based fallback when WMI is unavailable.
- `Services/AuthProvider.cs` and `ViewModel/SignInViewModel.cs` now use it. They
  previously called `ADLM.Auth.DeviceFingerprint`, the legacy MAC-based value.
- `ADLM.Auth/DeviceFingerprint.cs` is retained and marked legacy. Its algorithm is
  frozen; it is only used to supply the old value for binding migration.
- Login now sends `x-adlm-fp-version: 2` plus `fp_version` and
  `device_fingerprint_legacy` in the request body, so the server can match an existing
  v1 binding and re-bind it to the stable value instead of rejecting the sign-in.
- Offline licence validation accepts either the v2 or the legacy fingerprint, so cached
  licence tokens issued before this release do not force users back online.
- Added the `System.Management` package reference (WMI).

**Server dependency:** the migration path requires the licence server to read the
legacy fingerprint and re-bind. Confirm the field names match the backend before
release, otherwise upgrading users will bind as new devices.

**Not included in this release:** the self-service device management view, the
"release this device" action and the redesigned mismatch dialog are still outstanding.

---

## v2.4.0

Editable rate build-up quantities with per-user persistence and cross-app cloud sync to
QUIV / HERON via `/rategen-v2/library/user-rates`.
