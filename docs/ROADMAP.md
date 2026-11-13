# Roadmap

## 0.1 preview — delivered foundation

Native Windows hub and twelve useful feature implementations; trusted module API and lifecycle; capability controls; hotkeys; local configuration and profiles; reduced motion; diagnostics; signed-update primitives; self-contained installer/portable build; tests and CI; documentation and rendered screenshots.

## 0.2 — native reliability and richer controls

- Complete the manual hardware/application matrix and add automated native-output assertions.
- Profile-aware app exclusions and fullscreen suspension; work-area and mixed-DPI refinements.
- WindowGlide drag preview and visual custom-layout editor.
- ClickFX independent left/right presets and local sound files; improved cursor trails.
- Float periodic weather refresh, now-playing metadata and movable widget layout persistence.
- FocusFade eased transitions and distraction exclusions.
- QuickDock Core Audio sliders and supported hardware brightness integrations, with explicit unavailable states.
- Clipboard exclusion formats, optional encrypted history and durable pins with clear privacy controls.
- Theme tokens, high-contrast support, screen-reader audit and complete keyboard shortcut editing.
- Structured crash breadcrumbs, user-consented export, measured CPU/GPU budgets and overlay renderer sharing.

## 0.3 — distribution and ecosystem

- Authenticode signing and a publisher-operated update feed, richer update policy and rollback (opt-in daily checks already exist).
- A versioned plugin SDK, independently downloadable feature packages and package-level uninstall.
- Out-of-process plugin host and capability broker before accepting untrusted native plugins.
- Optional local-folder synchronization with conflict handling; cloud sync only with an explicit privacy design.
- Schema migrations with preservation of incompatible config, release-version consolidation and installation repair.

## Later — additional operating systems

macOS and Linux require separate native adapters and permission/desktop integration work. WPF is Windows-only. Do not advertise the existing binaries as cross-platform; preserve domain contracts and replace the UI/native service boundary if expanding platforms.
