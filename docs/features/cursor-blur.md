# CursorBlur

Capability: **Mouse**. Enable this utility to run the original CursorBlur.exe supplied by qemvix. Nexus bundles it, so a separate CursorBlur installation is not needed.

There is no Configure button. Disable, emergency pause, uninstall and normal Nexus exit stop the process Nexus owns. A kill-on-close Windows job also stops it if Nexus is forcibly terminated. Nexus refuses duplicate CursorBlur launches and does not terminate an independently started copy. Unexpected exits become visible module errors.

The executable runs with its own defaults and timing. Nexus's WPF frame target and reduced-motion preference do not modify this unmodified native executable; disable CursorBlur to stop its motion. It requires Microsoft's Visual C++ v14 x64 runtime. Missing DLLs are detected before launch, with an actionable error and Runtime help link.

The extracted executable is cached under `%LOCALAPPDATA%\Nexus\engines\cursorblur-<hash>`. Its SHA-256 is checked against the bundled resource before each start, and altered cache files are repaired. The separate `C:\Program Files\CursorBlur` installation is never modified or removed.

See [integration notes](../CURSORBLUR_INTEGRATION.md).