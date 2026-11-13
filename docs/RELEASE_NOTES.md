# Nexus 0.1.0 preview

Initial Windows x64 native desktop build, prepared 13 November 2026.

Includes the Nexus hub, tray controls, module catalog and settings screens; all twelve feature implementations; local typed settings, named profiles and import/export; explicit capability grants; global hotkey ownership; per-user installer/uninstaller and portable executable; local diagnostics; RSA-signed manifest and binary-digest verification; test/build/release automation; architecture and feature guides.

Validation: successful warning-free Release build, unit/platform tests, all twelve native start/stop checks and self-contained startup/render smoke. See VALIDATION.md for exact recorded results and the distinction between lifecycle testing and behavioral acceptance.

Known limitations: unsigned preview; Windows only; no remote repository/update feed provisioned; no third-party DLL loader, binary-by-feature downloader, cloud/live settings sync, automatic unattended update installation, arbitrary window blur, durable clipboard pins, now-playing metadata or complete embedded device controls. Feature install/removal is logical within the unified executable. The README documents per-feature gaps. Broader monitor, application, accessibility, installer registration and performance testing remains before a stable release.
