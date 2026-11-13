# Cursor Lens

Capability: **Screen**. Enable it from the module library after reviewing Permissions.

## Using it

Tune magnification and lens size. Choose circle/square. Presentation crosshair draws a center marker. The lens follows the pointer and stays inside its current monitor.

## Shortcuts

Configure a toggle shortcut on Hotkeys.

## Native behavior and limits

Captures local screen pixels on each shared overlay frame using GDI. The default target is 60 FPS, with higher targets in Settings. Reused capture/image buffers avoid recreating large surfaces every frame. Actual rate depends on capture/composition cost and display refresh. The overlay requests capture exclusion. Protected video and secure desktop are not available. Hardware/HDR/mixed-DPI validation remains open.

Disable or uninstall from Configure to release this feature's resources. Feature uninstall resets its settings; shared code remains in Nexus. See the repository test matrix for remaining hardware validation.

Demo GIF: planned. See [media storyboard](../media/README.md); there is no fabricated demo recording.
