# Testing and validation

## Automated checks

Run `./scripts/build.ps1 -Integration`. The executable test harness exits nonzero on failure. Unit checks cover bounded/nonfinite settings, schema-version rejection, nested profile validation, settings roundtrip and backup recovery, snapshot isolation, installed/enabled consistency, catalog uniqueness, schema-to-property mapping, hotkey parsing and collisions, lifecycle permission blocking, cleanup after startup failure, installer staging, and RSA signature/tamper detection.

`--integration` checks the actual cursor image, composition clock, opacity/style restoration and all thirteen module lifecycles. It pumps native messages and verifies resource cleanup, calling Stop twice. Weather networking is denied. The local OS did not retain the topmost bit on the test window; this remains a manual acceptance gap, not a claimed pass. WPF-managed windows can override native styles.

Hosted Windows runners can have no live cursor handle. In that case the cursor-image check explicitly loads the stock Windows arrow and exercises the same native image/hotspot conversion and cleanup; it does not claim live mouse testing on a headless runner. Local runs use the actual current cursor when available.

`--glide-integration` is the optional real-wheel regression test. It requires a quiet mouse and standalone Glide closed. It covers distance, speed, horizontal output, precision/tagged input bypass, pause, exclusions, fullscreen, pointer-leave cancellation and maximized windows. Run `./scripts/build.ps1 -ScrollInputTests` locally. Pointer/focus are restored. The final local run passed. It is separate from headless CI.

`Nexus.exe --smoke <output-directory>` runs the hub with isolated settings, renders its screens/editors, writes PNGs and `smoke-result.json`, then exits. CI in qemvix/nexus runs build/analyzer/type checks, regression/native lifecycle tests, formatting, packaging and startup smoke. Consult the actual Actions run for its remote status; local success is not a substitute.

## Manual acceptance matrix before stable release

| Area | Cases | Acceptance |
|---|---|---|
| Cursor overlays | Fast movement, click bursts, all effects, fullscreen and RDP | Pointer input remains usable; overlays never activate or block clicks |
| Scrolling | Browser, Notepad/editor, Explorer, touchpad, game, elevation | Correct direction/profile; no injected-event loop; emergency pause works |
| Lens/ruler | Scaling 100/125/150/200%, negative monitor origin, HDR, rotated display | Correct capture and coordinates; graceful protected-content result |
| Float | Media players, memory/CPU load, offline weather, invalid coordinates | Honest current values and clear unavailable state |
| Window control | Maximized/restored/minimized windows, all layouts, custom zones | Safe work-area placement; no shell/child-window moves |
| Clipboard | RTF/text, pins, duplicates, busy clipboard, oversized items | No disk payload storage; history erased on stop |
| FocusFade | Rapid switching, dialog windows, hub focus, secure desktop | Clear active region and usable input |
| KeyCast | Shortcut/all-keys, held modifiers, mouse buttons, pause | Accurate ephemeral display, no local input log |
| Glassify | Pre-layered/topmost windows, process list, disable and exit | Best-effort change, original attributes restored on normal exit |
| QuickDock | Devices absent, Wi-Fi/Bluetooth toggles in Windows, media | Native controls open or give an error; no fake state |
| Lifecycle | Enable/disable/uninstall all; profile switch; revoke permissions | No orphan hooks, timers, windows or stored clipboard entries |
| Installer | Clean local account, install/upgrade/uninstall, locked file | Shortcut and Installed Apps entry; retained settings; clear failure |
| Accessibility | Keyboard only, screen reader, high contrast, text scaling | Visible focus, named fields, no clipped essential actions |
| Performance | Idle/all-disabled, individual module, all overlays, sleep/resume | Measure CPU/GPU/memory; document real results before promises |

The checked-in preview has build, unit, native lifecycle and startup/render evidence on this host. Clean-machine installer registration, full GUI interaction, each utility's output, multi-monitor hardware, accessibility and long-duration resource tests remain release gates. A screenshot demonstrates rendered UI, not completion of that entire matrix.
