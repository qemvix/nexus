# ClipVault

Capability: **Clipboard**. Enable it from the module library after reviewing Permissions.

## Using it

Enable, copy text in another app, then open the vault. Search text, pin an item, copy its RTF/plain text, delete an item or clear everything. Pinned entries sort first.

## Shortcuts

Ctrl+Alt+V opens the vault. Configure also provides an Open ClipVault button.

## Native behavior and limits

Keeps at most 100 entries in memory; large entries and non-text payloads are skipped. History and pins are cleared on stop/exit. There is no password-manager exclusion enforcement or disk encryption because payloads are not written to disk.

Disable or uninstall from Configure to release this feature's resources. Feature uninstall resets its settings; shared code remains in Nexus. See the repository test matrix for remaining hardware validation.

Demo GIF: planned. See [media storyboard](../media/README.md); there is no fabricated demo recording.
