# Contributing

Nexus is proprietary. Review LICENSE and obtain qemvix's written permission for modification, redistribution or derivative work where required by its terms. Authorized contributors should follow the workflow below; it does not grant additional license rights.

Use Windows and the .NET 10 SDK. Open `Nexus.sln` in Visual Studio with desktop development support or use the documented CLI commands. Format using `dotnet format Nexus.sln` before submitting a change.

Keep feature code in its family package, native and shared ownership rules in core, and app composition in the desktop application. Public module APIs use typed models and declare capabilities. Do not load unsigned or untrusted external code into the main process. Avoid adding a package dependency when an OS/framework API is enough; document any new dependency and its license.

Every resource created by `OnStart` belongs in `Resources`. Add cleanup immediately after acquisition. Cleanup must be idempotent and survive a partially failed startup. Never block a Windows hook callback with disk/network work or UI dialogs. Never persist keystrokes or clipboard payloads in diagnostics. Test exceptions, denied capabilities, disable/uninstall and normal exit.

Run `./scripts/build.ps1 -Integration` and formatting verification. Add behavioral tests for meaningful changes rather than assertions that duplicate implementation details. Run the manual matrix for any native boundary change. Describe what behavior changed, what was tested and the remaining platform limitations in your pull request.

Issues should include Windows build, app version, display arrangement/scaling and minimal reproduction steps. Remove private data before attaching settings or local diagnostics. Keep security reports private to the repository owner until fixed; no security-report mailbox has been configured yet.
