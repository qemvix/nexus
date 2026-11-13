# ScreenRuler

Capability: **Screen**. Enable it from the module library after reviewing Permissions.

## Using it

Choose crosshair, ruler or measure. Move the pointer to inspect physical coordinates. Set an anchor to measure a rectangle; clear it to reset. Copy the pixel's hex color to the clipboard.

## Shortcuts

Ctrl+Alt+R sets/clears anchor. Ctrl+Alt+C copies screen color.

## Native behavior and limits

Measurements are physical-pixel differences; WPF draws in monitor-local DIPs. Ruler tick labels are local to the monitor. Protected/HDR content and unusual scaling require validation. Picking writes to, but does not collect, the clipboard.

Disable or uninstall from Configure to release this feature's resources. Feature uninstall resets its settings; shared code remains in Nexus. See the repository test matrix for remaining hardware validation.

Demo GIF: planned. See [media storyboard](../media/README.md); there is no fabricated demo recording.
