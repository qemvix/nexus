# Nexus implementation plan

Target: Windows 11 / Windows 10 x64, C# 14, .NET 10, WPF. Visual direction: minimal dark glass, violet and cyan accents, strong focus indicators, scalable typography. Repository name `nexus`; package identities `nexus-core`, `nexus-cursor`, `nexus-desktop`, `nexus-productivity`.

## Architecture

`apps/desktop` composes a native WPF hub and a tray host. `packages/core` contains typed contracts, configuration, lifecycle, permissions, profile import/export, shared native input, hotkey registration, overlays, diagnostics and update verification. The three feature packages depend only on core. No web view, service, administrator requirement, or account is required. Feature instances are constructed from a deterministic built-in catalog; external DLLs are not loaded implicitly.

One shared low-level mouse/keyboard input service runs callbacks quickly and routes work to the UI dispatcher. Subscriptions exist only while features need them. Transparent overlays do not activate or intercept pointer input. Each feature owns disposable resources and restores any window changes on disable, profile change and normal exit. A global emergency shortcut stops all modules.

## Models and module API

`AppConfig`: schema version, theme, reduced motion, module states, permission grants, hotkeys, named profiles, update preferences. `ModuleState`: installed, enabled, typed options serialized as JSON. `ModuleManifest`: immutable id, name, category, description, version, requested capabilities. `INexusModule`: manifest, settings schema, Start/Stop, optional settings panel. `ModuleManager`: install/uninstall, capability checks, isolated errors, transactional state persistence and disposal.

Settings use a temporary file and atomic replacement, a recovery backup, validation and bounded values. Capabilities are user-visible application controls, not an OS security sandbox. Native in-process code remains trusted. Clipboard collection and keystroke display are opt-in. Settings/profile exports exclude clipboard payloads. Crash information stays local. No cloud sync backend or telemetry collector is assumed.

## Screens

1. Overview: active feature count, category summaries, quick launch, pause all, runtime diagnostics.
2. Modules: category filtering, feature cards, explicit install/enable/disable/uninstall and configuration.
3. Feature detail: shared typed controls, description, capability requirements, status, shortcut.
4. Profiles: save/apply/delete, file import/export, validation.
5. Hotkeys: editable combinations, collision/error reporting, emergency pause.
6. Permissions: global input, screen capture, clipboard, window management and network; revocation stops dependents.
7. Settings: reduced motion, theme, diagnostics, update manifest check, local crash information.

## Milestones and acceptance

M1: solution, package references, core registry/configuration/lifecycle and meaningful automated tests. M2: native hooks and overlay utilities. M3: clipboard, widgets, window controls and settings integration. M4: accessible native hub with tray and persistence. M5: release build, portable archive, one per-user installer, uninstall entry and CI. M6: screenshot, architecture, honest feature matrix, contribution and release documentation.

Each utility must do useful native work when enabled. Advanced behavior that cannot be validated is documented explicitly rather than represented by a decorative switch. Feature uninstall removes feature state and any feature-owned data; binaries shared by the family remain in the unified distribution.

## Risks and fallbacks

- Input hooks must avoid blocking and ignore injected scroll input. Secure desktop, protected content and higher-integrity windows are outside normal user access.
- Monitor coordinates use physical pixels at the native boundary and WPF DIPs at the overlay boundary. Mixed-DPI displays need hardware validation.
- External-window transparency is best-effort and restored; third-party applications may override it. Arbitrary background blur is not guaranteed by Windows.
- Clipboard history may contain private data. Keep it bounded, memory-only by default, and off until granted.
- Weather needs a selected location and network permission. Media and brightness/device APIs vary by hardware and OS.
- Updates require a configured HTTPS release source and trusted signing key. Never execute an unverified download. Authenticode release signing requires the publisher's certificate.
- Third-party plugins require a later out-of-process broker for meaningful permission isolation. This release exposes a trusted, compiled plugin API.

## Testing and release strategy

Unit tests cover typed options, capability checks, module lifecycle, cleanup on failure, hotkey parsing/collisions, profile validation and persistence recovery. Integration checks cover the complete catalog and Windows hook/overlay start-stop behavior. A smoke mode launches the real hub without enabling privacy-sensitive modules and exits with a machine-readable result. Manual checks cover each utility, multiple monitors, scaling, permissions, elevated/fullscreen apps, sleep/resume and resource usage. CI restores/builds with nullable/type checking and analyzers, runs tests and formatting validation, then publishes a self-contained Windows x64 build. Tagged release builds create downloadable installer/portable/source assets; unsigned artifacts are labeled clearly.

## References

- WPF desktop SDK: https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props-desktop
- Low-level mouse hook restrictions: https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc
- RegisterHotKey: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey
