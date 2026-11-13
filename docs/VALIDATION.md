# Nexus 0.2 local validation record

Date: 13 November 2026. Platform: Windows x64, .NET SDK 10.0.401 (runtime 10.0.12). Source and tests target .NET 10 / WPF.

| Check | Result |
|---|---|
| Release solution build with warnings treated as errors | Passed, zero warnings and zero errors |
| Unit/platform executable groups | 21 passed, 0 failed; includes the imported Glide core suite and embedded-license verification |
| Actual cursor-image capture, shared frame clock and opacity/style restoration | Passed; topmost flag acceptance remains manual |
| Real module startup/stop, all thirteen utilities | 13 passed, 0 failed |
| Full interactive Glide wheel suite | Passed after closing standalone Glide and leaving the mouse still |
| Overlay frame-clock measurement | About 58–61 callbacks/sec at target 60; 81 FPS observed at target 144 on the reported 144 Hz display |
| Formatting verification | Passed |
| Self-contained win-x64 single-file publish | Passed |
| Packaged executable startup, screen/editor rendering and clean exit | Passed |
| Dashboard and hub screen image inspection | Completed; corrected background and scrollbar styling |
| Installer file staging | Passed in a temporary test directory |

The main runners report **37 passed, 0 failed** (21 unit/platform groups and 16 native checks), plus the full interactive Glide suite. The imported core suite includes 24 timing/direction combinations and 500 randomized bursts. The wheel test verifies exact distance, speed, horizontal output, precision/tagged bypass, pause/exclusions/fullscreen, pointer cancellation, untagged mouse-software input and ordinary maximized windows. Its packet assertions exercise the wheel fallback; touchpad ABI/startup/fallback checks do not establish every application's native-gesture compatibility.

**Manual acceptance gap:** Windows did not retain the topmost bit on the plain Win32 test window in this session. Opacity application and original-style restoration passed. Topmost behavior is not claimed as validated on this host. Frame-clock measurements validate the scheduler under one load, not 240 FPS presentation or every effect combination.

The updater's signature test verifies a valid payload and tamper rejection. No live publisher endpoint, Authenticode certificate or update installation is claimed as tested. The source monorepo is private at https://github.com/qemvix/nexus; check Actions for the remote build result.

Not yet validated: clean-machine installer registration/removal, real upgrade, exhaustive feature-output interaction, mixed-DPI/multiple displays, every app/elevation/fullscreen combination, long-duration memory/CPU/GPU behavior, and a full screen-reader/high-contrast audit. Use TESTING.md as the stable-release acceptance gate. Installation is supplied for review/use as an unsigned preview, with the implementation limits documented in README.md.
