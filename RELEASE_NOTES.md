# ADLM Rate Gen — Release Notes

## v2.9.1

### Fixed: "Build with AI" was missing after the update

The AI panel on the Custom Rate form did not appear for anyone who installed
the update. Nothing was wrong with the feature itself — it was hidden.

The panel is shown only when the app knows where the ADLM AI Service is, and
that address was read from an `ADLM_AI_URL` environment variable with no
fallback. Nothing sets that variable on a user's machine: the installer does
not write it and the app does not create it. So the address came back empty on
every install, the AI client was never created, and the section was hidden —
the one machine where it appeared was the development machine, which had the
variable set by hand. The feature shipped invisible.

The app now falls back to the deployed service address, so a plain install has
the AI panel with nothing to configure. Update to 2.9.1 and it is there; no
environment variable, registry entry or reinstall is needed.

This is a patch on top of 2.9.0 and carries nothing else. It is numbered 2.9.1
rather than folded into 2.9.0 because InstallerHub decides whether to offer an
update by comparing the published version string: republishing under 2.9.0
would have left everyone already on 2.9.0 — which is everyone — never offered
the fix.

Using it still requires being signed in with an active subscription — the
service checks your licence on every request and says so plainly if it
declines.

### Technical detail

- `AppEnvironment.AiServiceUrl` resolves in the same order as `ApiBaseUrl`: the
  product override (`ADLM_RATEGEN_AI_URL`), then the fleet-wide variable
  (`ADLM_AI_URL`), then the new `DefaultAiServiceUrl` constant. Trailing
  slashes are trimmed, since the SDK appends `/api/ai/...` verbatim.
- Either variable set to `off` (or `none`/`disabled`/`false`/`0`) disables AI
  deliberately and hides the panel. That is now the only way it disappears; a
  blank value is ignored rather than read as "off", so an installer writing an
  empty string cannot switch the feature off by accident.
- Deliberately *not* fixed in the installer. Writing the address into
  `HKCU\Environment` would recreate the stale-variable problem v2.6.0 had to
  clean up after with the retired API host: a value baked in at install time
  outlives the machine it was right for. The default lives in the app, where a
  release can change it.
- 11 new test cases in `Tests/ADLMRateGen.Tests` covering the empty environment, both
  overrides, the off switch and blank values. `InternalsVisibleTo` added so the
  test project can see `AppEnvironment`. Suite is 35 cases, all passing.
- `scripts/push-installer-update.mjs` publishes a build to InstallerHub in one
  command: it hashes the package, reads the deployment record, uploads to R2 and
  writes `packageUri`, `version` and `sha256` back together. It reads the record
  before writing because a `PUT` to `/admin/deployments/:productKey` replaces
  rather than patches — sending only the changed fields silently wipes the
  install operations — and it refuses a push that would change the package kind,
  because the operations and the package have to agree. RateGen's deployment is
  a ZIP containing `app/`, which InstallerHub extracts itself; the Inno installer
  is the manual-download artifact and is not what this channel consumes.

---

## v2.9.0

### The library now says what a rate is meant to produce

A day rate is a proxy for an output, but the library only ever showed the
money. Deciding whether ₦250,000 a day is right for a D6 meant knowing from
memory that a D6 shifts 150–250 m³ an hour.

The labour edit popup now carries a reference panel: what the item is, what it
produces, what that assumes, and what it burns or who staffs it. All 84 labour
and plant rates have one. Every output is given as a range with its basis,
because published sources disagree by up to a factor of two on the same
machine, and a single number would imply a precision that does not exist.

It is read-only and feeds no calculation. It is there to sanity-check a rate,
not to drive one.

Rates you add yourself can now carry a note of your own — your plant, on your
terms. Your note wins over the bundled entry, since you know your own machine
better than a published table does, and a rate you added has no bundled entry
at all. Notes travel with the rate to the website.

The catalogue also moves from three categories to 24, and sorts by size rather
than alphabetically, so 12mm comes before 100mm.

### Published prices can be accepted one row at a time

The price notice was a one-line bar with two buttons that applied to everything
at once. It named the affected rates but never showed a figure, so the only way
to judge an offer was to accept it and go and look. It could not be dismissed
either — it sat above the library until answered.

It is now a card, one row per rate: a tick box, the unit, the price you typed,
the price since published, and the percentage between them, coloured by
direction. Both buttons act on ticked rows only, so you can take the new cement
price and keep your own on diesel. Whatever you leave unticked stays pending
and the card stays up with the remainder. The header has a select-all, the
footer counts what you have selected.

Closing it hides it without deciding. The rows stay pending, and a pinned card
in the notifications popup brings the review back.

### Sync moved to the header, and now says why a step failed

Sync sits with the other app-wide actions in the header instead of in the
sidebar, with its status on the button's tooltip.

It also explains itself. A failed step used to read "Master prices: FAIL" with
no reason — the error handling replaced the exception with that literal string
and threw away a message that already carried the HTTP status and the server's
own text. And an empty response was indistinguishable from a crash, so "the
server had nothing for you" arrived looking like a fault. Both now say what
actually happened.

**Fixed: sync could die on a duplicated library row.** Merging your rows onto
the master list appended a twin instead of folding your price onto the matching
row, so any item you had priced could end up in the library twice. Six pairs had
built up this way, and the duplicate then crashed the sync outright. Existing
duplicates clear themselves on the next sync.

### The rate tables have room

A paying user reported seeing only one line of a rate table at a time. The
welcome banner takes 260px off the top, and every rate view carried a negative
bottom margin that pushed its card past the workspace, clipping the last row in
half.

A chevron in the header now collapses the banner and hands its height to the
table, and the negative margins are gone. The choice persists between sessions,
and is kept out of the file that is cleared on sign-out, so signing out does not
reset it.

### Screenshots carry their source

A captured price list is now marked with its origin. Earlier in this release the
mark was drawn over the window itself, which worked but sat over the figures all
day; it now goes onto the captured image instead, so the app is clean to work in.
PrintScreen, Alt+PrintScreen and Win+Shift+S all route through the clipboard,
which is where the mark is applied.

Note the limit honestly: a photograph of a monitor carries no mark, and no
software control reaches that route.

### Fixed: names, locations and dark mode

- **Roller and grader names corrected.** "Vibratory whelled roller" was not a
  word, and a pneumatic roller runs on tyres. "Grader (Cat 1406)" is now
  "Grader (Cat 140G)" — 1406 is not a Caterpillar model, and the G was being
  read as a 6. A rate name is a lookup key, so the old spellings are aliased:
  an installation that has not yet updated still prices these rates instead of
  silently costing them at zero.
- **The pricing-location note keeps up with the sync.** The library could read
  "Lagos: priced from south west rates" for an account whose profile says Imo.
  Nothing was mispriced — only the sentence was stale, because it was re-read
  on the way into the library and never again.
- **Dark mode.** The Material Name and Labour Item columns went blank, the
  price review was unreadable, and the save button and sign-out took the wrong
  colour. These set a brush that resolves once at load and never follows a
  theme change.

### Technical detail

- `PriceConflictRow` wraps the price-conflict DTO rather than extending it: that
  type is written to disk and compared by value, and a tick box has no business
  in a persisted record.
- The banner state lives in `ui.preferences.json` under `AppPaths.UserDataDir`,
  deliberately apart from `AppConfig`, which is rewritten on sign-in and cleared
  on sign-out.
- The duplicate-row crash was `FindEdits` emitting two edits sharing a `RowKey`
  while every consumer builds a dictionary off that key. The merge now folds a
  matching user row onto its master row, `FindEdits` dedupes the way its incoming
  side already did, and the four `RowKey` dictionary builds group first.
- `GetJsonRawAsync` turns 404 and 204 into `"[]"`, so an empty response reached
  `SyncAsync` as a JSON array and every `TryGetProperty` threw — which also made
  the `Is404` "SKIPPED" branch unreachable. The root kind is checked now.
- `csproj` and `Installer.iss` move together, so a package cannot install 2.9.0
  binaries while announcing 2.8.1.

---

## v2.6.2

### Fixed: saving a rate could not add to your library, and risked losing rows

Your material and labour libraries can hold more than one entry under the same
name — the same material at a different unit, or in a different category. The
shipped material list does this 23 times.

When a rate was saved, the step that adds new materials and labour to your
library rebuilt the whole list keyed on the name. With duplicates present that
step failed outright, and because the failure was caught and ignored, saving
appeared to work while nothing was ever added to the library. Had it not failed,
it would have written the list back with only one row per name and discarded the
rest.

The merge now keeps every row. Existing entries are updated in place, so
duplicates and the order of your library are both preserved, and genuinely new
items are added at the end. When an update matches a name held by several rows,
all of them take the new price.

If your library lost rows before this release, this does not bring them back —
restore from a backup if you have one.

---

### Technical detail

- `MaterialLibraryService.AddOrUpdateMaterials` and
  `LabourLibraryService.AddOrUpdateLabours` used
  `ToDictionary(m => m.MaterialName)`, which throws `ArgumentException` on a
  duplicate key; `CustomRateEntryViewModel.SaveCustomRate` wraps `Harvest` in a
  try/catch, so the throw was swallowed and the harvest silently no-opped.
  Reassigning the backing list from `dict.Values` would also have dropped every
  duplicate row.
- Both now group by name and mutate the matched rows in place, appending only
  unseen names, so row count and ordering are stable.
- 5 new tests in `Tests/ADLMRateGen.Tests` (24 total), verified as a negative
  control: against the previous implementation they fail 4/24.

---

## v2.6.1

### Fixed: AI-built rates saved with every price at 0.00

Building a rate with AI filled the form correctly, but saving it wrote every
price as 0.00 — and reopening the rate showed zeros. The quantities, units and
names were all fine; only the money was gone.

Saving a rate adds any new materials and labour to your library and then reloads
it. That reload was re-pricing every line on the form, and because an AI line is
labelled "Cement (Portland 42.5R) [AI]" it never matched the library entry named
"Cement (Portland 42.5R)" — so it was set to zero. This happened in the moment
between you pressing Save and the file being written.

Lines now match their library entry regardless of the label, and a reload can no
longer clear a price that is already there. Your existing zeroed rates cannot be
recovered — rebuild them and they will save correctly.

### Fixed: Overhead and Profit are no longer wiped by a bad keystroke

Clearing the Overhead or Profit box left it looking empty while the old value
was still being used in the total. Both boxes now keep exactly what you type.

### ADLM AI: clearer feedback while it works, and when it doubts itself

- The AI panel glows while a build-up is being prepared, the button reads
  "Building…", and both it and the prompt box grey out. They were already
  disabled during a request, but looked live.
- A build-up that comes back with no prices at all is now refused rather than
  presented as ready to save.
- When ADLM's checks flag a build-up — a labour line priced per day that was
  never pro-rated to one unit, a rate wildly out against comparable library
  rates, a single line costing more than a whole comparable rate — the reasons
  are listed in an amber panel under the AI box. The draft is still shown, but
  you are told what to correct before saving.

---

### Technical detail

- `RateEntryItem.ResolveUnitPrice` matched on the raw description and cleared the
  price on every miss. It is wired to the static
  `MaterialLibraryService.LibraryChanged`, which `SaveCustomRate` raises via
  Harvest + `RefreshLookups`, so every unmatched line was zeroed mid-save. It now
  matches on `RateLineLibrary.CleanName` and only clears when the user actively
  picks a different item (`clearWhenUnknown`).
- `AiRateService` derives a unit price from `totalNgn / quantity` when
  `unitPriceNgn` is absent, and `IsUnpriced` gates an all-zero build-up.
- `AiRateResult.Warnings` carries the service's check failures through to
  `CustomRateEntryViewModel.AiWarnings`.
- `OverheadPercentText` / `ProfitPercentText` hold the raw text; a decimal
  binding blanked the field on a partial entry. `FInputOHP` also had a fixed
  `Height` while `FInput`'s template puts `Padding` on the Border, leaving ~23px
  of content slot and clipping the value out of view.
- Added `Tests/ADLMRateGen.Tests` — 19 tests over the pricing and harvest rules,
  hermetic via in-memory data sources. Verified as a negative control: against
  the previous `RateEntryItem` they fail 5/19.

**Server-side, already deployed:** the AI service now enforces pro-rating and
sanity checks in code rather than only in the prompt, and escalates to the
stronger model when a build-up fails them.

---

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
