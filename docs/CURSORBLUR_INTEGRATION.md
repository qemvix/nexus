# CursorBlur and overlay rendering

## Native engine in 0.2.1

The C# rendering port from 0.2.0 has been removed. Nexus embeds the exact working `C:\Program Files\CursorBlur\CursorBlur.exe` supplied by qemvix (26,112 bytes, SHA-256 `1def15d2653a09c21826d430cab3a7c8f24778d385dd0e15177b786582207f2c`). The reference C++ source remains under `Upstream/CursorBlur.cpp`; provenance includes [qemvix/CursorBlur](https://github.com/qemvix/CursorBlur) and its upstream attribution.

Enabling the module extracts/validates a versioned per-user cache and starts the original executable without arguments. Nexus assigns only its own child to a Windows job with KILL_ON_JOB_CLOSE. Disable, emergency pause, resource cleanup, host termination and uninstall release that job. An existing standalone CursorBlur is reported rather than adopted or killed. A stopped helper changes the module status to Faulted on the health tick.

CursorBlur has no schema/editor or Configure button. Native frame pacing, colors and motion remain the executable's defaults; WPF FPS and reduced-motion settings do not affect it. It is not included in WPF overlay frame metrics. Disable it when reduced motion is wanted.

The imported executable depends on MSVCP140.dll, VCRUNTIME140.dll and VCRUNTIME140_1.dll. Nexus checks for those before launch; missing dependencies produce an error instead of an unusable Running state. See [Microsoft's supported Visual C++ runtime downloads](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist). The owner has this runtime installed. No runtime installer is silently downloaded or executed.

## Nexus-rendered overlays

Cursor Aura, ClickFX, Cursor Lens, FocusFade, ScreenRuler and KeyCast continue using the shared composition-synchronized WPF clock. Settings offers 60, 90, 120, 144, 165 and 240 FPS targets and measured callback rates. Actual presentation depends on monitor refresh, composition, capture cost and load. Targets are not guarantees.

Cursor Lens samples once per shared frame on the pointer's monitor and reuses capture/image buffers. Protected video, HDR and mixed-DPI behavior still need hardware validation.

Automated tests cover the bundled digest, altered-cache repair, duplicate prevention, start/stop, crashed-host cleanup and stopped-engine health reporting. Those tests establish lifecycle behavior; visual trail acceptance still depends on real input and the supplied executable's behavior.