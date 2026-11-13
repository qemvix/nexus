# Local validation record

Date: 13 November 2026. Platform: Windows x64, .NET SDK 10.0.401 (runtime 10.0.12). Source and tests target .NET 10 / WPF.

| Check | Result |
|---|---|
| Release solution build with warnings treated as errors | Passed, zero warnings and zero errors |
| Unit/platform executable tests | 16 passed, 0 failed |
| Actual Win32 opacity/topmost change and restoration | Passed |
| Real module startup/stop, all twelve utilities | 12 passed, 0 failed |
| Formatting verification | Passed |
| Self-contained win-x64 single-file publish | Passed |
| Packaged executable startup, screen/editor rendering and clean exit | Passed |
| Dashboard and hub screen image inspection | Completed; corrected background and scrollbar styling |
| Installer file staging | Passed in a temporary test directory |

Total automated assertions/groups reported by the test runner: **29 passed, 0 failed** (16 unit/platform, 13 native integration). Startup/render smoke is additional. The native test first exposed a WPF window overriding its native layering change; the final behavior uses a restoration lease, skips unsupported window types, and verifies the layer/topmost roundtrip on a plain Win32 window.

The updater's signature test verifies both a valid signed payload and rejection after tampering. No live publisher endpoint, real certificate, or network download/installation was claimed as tested. GitHub Actions is configured locally but not executed remotely. No public GitHub repository or release has been created.

Not yet validated: clean-machine installer registration/removal, real upgrade, exhaustive feature-output interaction, mixed-DPI/multiple displays, every app/elevation/fullscreen combination, long-duration memory/CPU/GPU behavior, and a full screen-reader/high-contrast audit. Use TESTING.md as the stable-release acceptance gate. Installation is supplied for review/use as an unsigned preview, with the implementation limits documented in README.md.
