# ADLM Rate Gen — Release Notes

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
