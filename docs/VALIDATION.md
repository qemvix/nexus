# Nexus 0.2.1 validation

14 November 2026, Windows x64, .NET 10 / WPF.

Release builds treat warnings as errors. Unit tests cover configuration, module permissions, upstream Glide regression cases, bundled CursorBlur digest/cache repair, missing runtimes, signed payloads, malformed metadata, HTTPS downgrades, bounded downloads, cancellation cleanup, cache preservation and upgrade staging with settings preservation.

Native tests cover actual helper start/stop, duplicate prevention, simulated host-crash cleanup, stopped-engine fault reporting, exact diamond/ring alignment, WPF frame scheduling, opacity restoration, all 13 module lifecycles, the missing CursorBlur Configure button, editor Apply semantics and update controls. The imported Glide engine has no scrolling algorithm changes in this release; its previous full wheel-input acceptance remains recorded in the 0.2 history. No fresh wheel injection was needed for these lifecycle/UI changes.

Manual acceptance limits remain: topmost acceptance on this host, clean-machine runtime/install/removal, mixed-DPI/HDR, long-duration performance, exhaustive effect combinations, all application/elevation cases and a full screen-reader/high-contrast audit. Native CursorBlur output uses the owner's known-working executable; automated lifecycle tests do not certify every trail image. Native CursorBlur timing is independent of the WPF target.

A live signed manifest and public downloads are provisioned for this release. The existing 0.2.0 client can consume it after a one-time feed/key configuration. Authenticode publisher signing and an unattended cloud signing pipeline remain unprovisioned. Record the public release and CI URLs when publishing.