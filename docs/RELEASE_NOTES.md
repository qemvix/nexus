# Nexus 0.2.2

Windows x64 · interface update

- Kept the dark glass surfaces with simpler spacing, borders and controls.
- Removed promotional taglines, the large dashboard artwork and repeated labels.
- Added an overview of running, installed and permission-blocked modules, with direct access to the module library and permissions.
- Added a clear selected-page state in navigation and consistent dark dropdowns.
- Simplified the website around features, real screenshots, installation and downloads. Added custom scrollbars with native fallbacks for touch and forced-color modes.

CursorBlur still runs the original executable supplied by qemvix and has no Configure screen. ScrollFlow and the other native engines are unchanged. Existing settings, profiles and hotkeys are preserved. Use Settings & updates to check for the release and install it in place.

The official installer and portable ZIP include .NET. The CursorBlur engine requires the Microsoft Visual C++ v14 x64 runtime. This release is not Authenticode-signed; the updater separately verifies the signed manifest and installer SHA-256. Existing feature limitations remain documented in the feature guide.

Copyright © 2026 qemvix. Proprietary license; official releases may be used for personal desktop use. See Nexus-LICENSE.txt.


---

# Nexus 0.2.1 preview

Windows x64 bugfix release, 14 November 2026.

- Replaced the nonworking CursorBlur port with the exact working native executable supplied by the owner. The module has enable/disable/uninstall actions and no Configure button.
- Tied the native process to a kill-on-close job, prevented duplicate engines, checked runtime dependencies, and made process failures visible.
- Centered the dashboard diamond with vector geometry rather than a font glyph. Rings and diamond share the same measured center.
- Connected the official signed HTTPS update feed and public key. Updates appear inline with download progress; the new installer waits for Nexus to exit, replaces it and reopens it while keeping settings.
- Hardened update staging, cancellation cleanup, HTTPS redirect checks and digest validation; versioned verified caches survive unrelated failed downloads.
- Fixed settings editors mutating active configuration before Apply changes. Module errors now appear directly on library cards, with a Permissions action when blocked.
- Centralized displayed version/update comparisons, isolated smoke runs from the active user's instance/hotkeys, and qualified icon resource URLs for hosted tests.

The preview remains unsigned by Authenticode. Manifest signatures authenticate the updater, not the Windows publisher dialog. CursorBlur uses its own native timing/motion and requires Microsoft Visual C++ v14 x64. Its WPF predecessor's sliders and FPS/reduced-motion behavior no longer apply. Other 0.2 preview limits remain.

---

# Nexus 0.2.0 preview

Windows x64 update, prepared 13 November 2026.

- ScrollFlow now hosts your Glide engine, with native touchpad gestures, horizontal/vertical fallback, exclusions and fullscreen handling.
- Added CursorBlur as a separate utility, porting the real cursor-image trail algorithm from the supplied source.
- Shared composition-synchronized rendering replaces 12–30 FPS timers. Settings offers 60–240 FPS targets and measured rates; Cursor Lens reuses its capture buffers.
- Created the private qemvix/nexus monorepo for the app and four named packages.
- Applied the owner's supplied proprietary license, adapting NoMiss/gameplay references to Nexus/desktop use. The installer and settings hub embed and display the same notice.

Includes the Nexus hub, tray controls, module catalog and settings screens; all thirteen feature implementations; local typed settings, named profiles and import/export; explicit capability grants; global hotkey ownership; per-user installer/uninstaller and portable executable; local diagnostics; RSA-signed manifest and binary-digest verification; test/build/release automation; architecture and feature guides.

Validation: successful warning-free Release build, unit/platform tests, all thirteen native start/stop checks and self-contained startup/render smoke. See VALIDATION.md for exact recorded results and the distinction between lifecycle testing and behavioral acceptance.

Known limitations: unsigned preview; Windows only; no hosted update feed, third-party DLL loader, binary-by-feature downloader, cloud/live sync, unattended updates, arbitrary window blur, durable clipboard pins or full embedded device controls. Feature removal is logical. Actual FPS depends on hardware/load. This session did not retain the topmost bit on the test window, while opacity/style restoration passed; topmost acceptance remains manual. Broader hardware, installer and accessibility checks remain before stable release.
