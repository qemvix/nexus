# Installation, portable mode and removal

1. Obtain the supplied installer or build the source from the private [qemvix/nexus](https://github.com/qemvix/nexus) monorepo. CI also supplies build artifacts. Access requires your GitHub account.
2. Review the release notes, publisher signature if present and hashes. The locally produced preview is unsigned.
3. Open `Nexus-Setup.exe` and choose Install Nexus. The install destination is fixed to `%LOCALAPPDATA%\Programs\Nexus`; no elevation is needed.
4. Launch from Start. Use Permissions and the module library to choose your features.

Portable: unzip the portable release and open `Nexus.exe`. Portable mode still stores settings under `%LOCALAPPDATA%\Nexus`, so installed and portable copies share a profile; a per-user mutex prevents running them together. A second launch tells you to use the tray icon; it does not automatically activate the existing instance.

Upgrade: use Settings & updates → Check for updates → Download and install. The official feed and public key are bundled in 0.2.1. Network permission is required; daily checks are optional. The updater verifies the manifest signature and binary hash, exits the app, replaces the per-user installation and reopens Nexus. Settings are kept. Portable copies receive a per-user installation. The existing 0.2.0 client can read the same feed once its URL/key are configured; it opens the newer installer, where Update Nexus replaces the old copy without uninstalling. Automatic rollback is not implemented.

Remove: exit other running copies, then use Windows Settings → Apps → Installed apps → Nexus → Uninstall. The uninstaller confirms removal and retains local settings. The helper executable remains in Windows temporary storage for normal temporary-file cleanup. To erase personal settings manually, remove `%LOCALAPPDATA%\Nexus` after exiting. The current uninstall retains settings intentionally, so it does not needlessly erase profiles or logs.

Module removal: use Configure → Uninstall feature. This stops the feature and resets its options. Clipboard history is erased when ClipVault stops. Shared code remains in the unified executable; feature removal is not a disk-space package downloader.

Troubleshooting: Ctrl+Alt+P pauses modules, Ctrl+Alt+N opens the hub. If a shortcut conflicts, use the tray controls and edit toggle shortcuts. Protected/elevated applications may reject control from an unelevated app. Screen capture can omit protected video or secure desktop. For an opacity left behind after forced termination, restarting the affected third-party app restores its normal window state.
