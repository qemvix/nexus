# ScrollFlow

Capability: **Mouse**. Enable it from the module library after reviewing Permissions.

## Using it

Tune sensitivity, momentum, acceleration and invert. Use the typed JSON editor for per-process profiles, for example [{"Process":"notepad","Sensitivity":1.2,"Invert":false,"Momentum":0.8}]. Apply JSON, then Apply changes.

## Shortcuts

Configure a toggle shortcut on Hotkeys; Ctrl+Alt+P always stops interception.

## Native behavior and limits

Only original vertical wheel packets are intercepted. Fractional generated deltas are ignored by the hook to prevent recursion. Some programs coalesce or ignore small deltas. The hub's own wheel scrolling passes through unchanged.

Disable or uninstall from Configure to release this feature's resources. Feature uninstall resets its settings; shared code remains in Nexus. See the repository test matrix for remaining hardware validation.

Demo GIF: planned. See [media storyboard](../media/README.md); there is no fabricated demo recording.
