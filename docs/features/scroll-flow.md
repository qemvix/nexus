# ScrollFlow

Capability: **Mouse**. Enable it from the module library after reviewing Permissions.

## Using it

ScrollFlow now hosts your Glide engine. Tune speed (1 = 100%), glide duration (100–700 ms), native touchpad output, fullscreen pausing, exclusions and inversion. Use typed JSON for per-process profiles, for example [{"Process":"notepad","Sensitivity":1.2,"Invert":false,"GlideMilliseconds":320}]. Apply JSON, then Apply changes. Exit standalone Glide first.

## Shortcuts

Configure a toggle shortcut on Hotkeys; Ctrl+Alt+P always stops interception.

## Native behavior and limits

The engine preserves Glide's cubic eased pulses, vertical/horizontal output, synthetic touchpad backend with wheel fallback, precision/modifier pass-through, integrity checks, app exclusions, fullscreen policy and target-change cancellation. It runs on its own native input thread and excludes the hub itself. The settings panel shows backend and current status. Some apps coalesce fallback wheel packets. See [integration details](../GLIDE_INTEGRATION.md).

Disable or uninstall from Configure to release this feature's resources. Feature uninstall resets its settings; shared code remains in Nexus. See the repository test matrix for remaining hardware validation.

Demo GIF: planned. See [media storyboard](../media/README.md); there is no fabricated demo recording.
