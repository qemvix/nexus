# ScrollFlow / Glide integration

ScrollFlow directly hosts the native scrolling engine adapted from [qemvix/Glide](https://github.com/qemvix/Glide), source snapshot `32a542b8a0d1223bb6819e1e6a0e58d39fe2d647`, at the owner's request.

## Source provenance

| Upstream file | Blob SHA | Nexus location |
|---|---|---|
| src/Glide/ScrollEngine.cs | 1416aab51eebe376859dfd9e0c904b81b7b82e13 | packages/cursor-mouse/GlideBackend/ScrollEngine.cs |
| src/Glide/Native.cs | 588bb24a613347a87c4d8831075aa9e1760b8045 | packages/cursor-mouse/GlideBackend/Native.cs |
| src/Glide/Touchpad.cs | 1c33cf389f9bb236a630ff603f67debf54b61005 | packages/cursor-mouse/GlideBackend/Touchpad.cs |
| src/Glide/Settings.cs | 5e168197508d44fdb86742438058b4480485b245 | Settings snapshot and Motion pulse implementation |
| src/Glide/Tests.cs | 993c195aa9b0fc676f5bb37a0bdcc0da7fc9bad4 | tests/Nexus.Tests/GlideRegressionTests.cs |

## Preserved behavior

- Dedicated STA input/message thread and background app/permission observer.
- Finite overlapping cubic ease-out pulses, distance conservation, immediate direction reversal and no infinite tail.
- Native synthetic two-finger touchpad output when supported, with horizontal/vertical wheel fallback.
- Precision-wheel pass-through; modifier/button shortcuts and unrelated tagged input bypass.
- Integrity-level checks, exact process exclusions and fullscreen pausing, preserving Explorer/Discord exceptions and ordinary maximized windows.
- Cancellation when target, focus or pointer changes; failed output restores ordinary scrolling.
- High-resolution timing requested only while gliding; native handles, contacts, hooks and threads released on stop.

## Nexus adaptations

Namespaces are isolated in `Nexus.Cursor.GlideBackend`. Legacy nullable annotations remain disabled only for the imported files. Nexus owns the settings and lifecycle, so Glide's XML storage, tray application, installer and global shortcut are not copied. Speed defaults to 100%, glide duration to 320 ms, native gestures and fullscreen pausing are enabled. Per-process speed, inversion and optional duration are Nexus extensions. The old decay/acceleration loop is removed; legacy Momentum/Speed fields may still deserialize but are not used by this engine.

Output/test tags are unique to Nexus. Starting ScrollFlow while standalone Glide is running produces a clear error to prevent two engines transforming the same input. Stop standalone Glide before testing; do not uninstall it unless desired.

The live settings panel displays backend mode, current status and output errors. Native gestures can fall back when Windows lacks/rejects their API. The fallback's fine wheel packets can still be coalesced by some applications.

## Validation

The adapted regression suite covers native ABI layouts, JSON defaults, fullscreen detection, signed scroll-distance conservation across timing intervals, 500 randomized bursts, reversal/cancel/long gaps, bounded bursts, exclusions and settings snapshots. An optional `--glide-integration` run uses a real test window and wheel injection. It requires a quiet interactive desktop and no competing scrolling utility; it is separate from headless CI. Tests that move the pointer restore its original position and focus. Do not equate a successful lifecycle check with complete compatibility in every target application.
