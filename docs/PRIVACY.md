# Privacy and permissions

There is no account system, advertising SDK, cloud sync service or telemetry endpoint. Configuration and bounded error summaries stay in `%LOCALAPPDATA%\Nexus`. Errors include area, type and message, not collected input events, clipboard entries, window titles or process memory dumps. Review error messages before sharing logs because an underlying exception may mention a file path.

Capabilities are app-level controls for trusted compiled modules:

| Grant | Features | Data/use |
|---|---|---|
| Mouse | Aura, ClickFX, ScrollFlow, KeyCast | Pointer/buttons/wheel in memory; scroll transformation when enabled |
| Keyboard | KeyCast | Ephemeral display only, no disk history; all-keys mode can reveal sensitive typing |
| Screen | Lens, ScreenRuler | Local pixel capture; picking a color writes its hex code to the clipboard |
| Clipboard | ClipVault | Up to 100 text/RTF items, at most 32,768 text characters each, memory only |
| Windows | WindowGlide, FocusFade, Glassify | Window rectangles, ownership and styles for native control |
| Network | Optional Float weather; update UI | Coordinates to Open-Meteo, or requests to the configured publisher manifest/binary host |

Clipboard capture starts only after enabling ClipVault with permission. Pinning does not make items persistent. Disable, uninstall, pause all or exit clears the history. Password managers and other applications may place secrets on the clipboard; this preview does not inspect every clipboard exclusion format. Disable ClipVault before sensitive copy operations.

KeyCast's shortcuts-only mode avoids ordinary letters and digits without Ctrl/Alt/Win. Other keys such as Space/Enter may still be shown. All-keys mode is a deliberate presentation feature, not a password-safe keyboard monitor. Pause it before typing secrets. No automatic password-field detection is claimed.

Revoking a capability stops dependent modules; network revocation cancels in-flight update requests and stops/restarts optional weather through lifecycle reconciliation. Update checks are manual unless the user explicitly enables daily checks with a configured HTTPS publisher URL/key. No background network activity occurs when modules and automatic checks are disabled and no manual update check is running.

Profiles export feature configuration, not grants or clipboard content. They may include process names and weather coordinates. Do not publish a profile containing information you consider private. Third-party native modules cannot be securely sandboxed using these flags; external loading is intentionally absent in this release.
