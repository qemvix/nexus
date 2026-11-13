# WindowGlide

Capability: **Windows**. Enable it from the module library after reviewing Permissions.

## Using it

Choose halves, quarters, thirds or Custom. Custom zones are a JSON array of up to four normalized X/Y/Width/Height rectangles in left/right/up/down order. The default custom layout includes two halves, full screen and a centered window.

## Shortcuts

Ctrl+Alt+Left/Right/Up/Down. These action shortcuts are fixed in 0.1.

## Native behavior and limits

Captioned top-level windows are animated into monitor work-area bounds. Move-end events implement magnetic left/right edge snapping. Elevated apps, child windows and protected windows may ignore requests. Window positions intentionally remain after disable.

Disable or uninstall from Configure to release this feature's resources. Feature uninstall resets its settings; shared code remains in Nexus. See the repository test matrix for remaining hardware validation.

Demo GIF: planned. See [media storyboard](../media/README.md); there is no fabricated demo recording.
