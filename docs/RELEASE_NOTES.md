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
