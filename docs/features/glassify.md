# Glassify

Capability: **Windows**. Enable it from the module library after reviewing Permissions.

## Using it

Set opacity and topmost. Choose Active window or Process list. For Process list, enter comma-separated executable process names without .exe, for example notepad,Code.

## Shortcuts

Configure a toggle shortcut on Hotkeys. Restart an affected app if opacity remains after forced termination.

## Native behavior and limits

Saves extended window style, existing layered attributes and owner process before changing a window. Restores on normal stop and checks ownership to avoid reused handles. Forced termination or other programs' style changes can defeat restoration. Blur is not implemented.

Disable or uninstall from Configure to release this feature's resources. Feature uninstall resets its settings; shared code remains in Nexus. See the repository test matrix for remaining hardware validation.

Demo GIF: planned. See [media storyboard](../media/README.md); there is no fabricated demo recording.
