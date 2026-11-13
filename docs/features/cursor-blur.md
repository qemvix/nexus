# CursorBlur

Capability: **Mouse**. This is a new, separate utility based on your CursorBlur source.

Enable it from Cursor & mouse. Tune speed sensitivity, trail fade in milliseconds, maximum alpha and tint. Defaults match upstream (0.03 / 50 ms / 10 / white). The trail is drawn from the actual system cursor bitmap, including its hotspot, rather than a generic glow.

The utility follows the shared overlay FPS target in Settings (60–240). Select a rate appropriate to your monitor; Settings also shows actual rendered callback rates. Hardware load may reduce delivered FPS. Reduced motion suppresses the effect. Hidden cursors clear the trail, and disable/uninstall releases the cached bitmap and overlay.

See [source integration notes](../CURSORBLUR_INTEGRATION.md). The original C++ source is kept for reference; Nexus runs a C# port with bounded interpolation work. No C++ compiler is needed by end users.
