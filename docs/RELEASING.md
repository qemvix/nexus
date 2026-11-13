# Release guide

1. Update version in `Directory.Build.props`, app manifest, visible hub/footer, installer metadata, update comparison and artifact script names. These are explicit in 0.1; consolidate them before frequent releases.
2. Run `./scripts/build.ps1 -Publish -Integration` and `dotnet format Nexus.sln --verify-no-changes --no-restore` on Windows.
3. Run the manual matrix in TESTING.md on supported Windows/display/hardware configurations, including a clean-account install and removal. Confirm no features start without consent.
4. Sign `artifacts/publish/Nexus.exe` with the publisher's Authenticode certificate using Windows SignTool and a trusted timestamp service. No certificate or secrets are included here. Recreate installer/portable copies and hashes **after** signing. Do not publish a build as verified/signed if you skipped this step.
5. Provide a publisher-controlled HTTPS release location and generate an RSA key pair outside the repository. Place only its public PEM key in the client configuration. Publish an immutable release executable and its signed manifest. Private key handling belongs in a protected release secret/service, never a tracked file.
6. Sign the manifest with PowerShell 7+: `./scripts/sign-release.ps1 -Binary <exe> -Url <https-url> -PrivateKeyFile <private-pem> -Version <version> -Output release.json`. The client verifies RSA-PSS/SHA-256 over the exact Payload string, then hashes the downloaded binary. The manifest must describe the final signed binary, not the pre-signing copy.
7. Push the repository to a repository named `nexus`; package identities are already nexus-core/cursor/desktop/productivity. The supplied workflow builds on main/PR and creates a **draft** release for a `v*` tag. It does not publish without review or provision update infrastructure automatically.
8. Review artifacts, hashes, release notes, screenshots, licenses and known issues. Publish the draft only after acceptance. Keep a previous installer available for manual rollback.

The initial source is GitHub-ready and local. A remote repository, Authenticode identity, hosted manifest and production signing pipeline are not provisioned by the local build. Do not infer a download link from an assumed GitHub username.

Installer architecture: the self-contained app launches a native installer when named Nexus-Setup or passed --install. It copies only the single-file executable, creates a Start-menu link using the Windows shell, and registers HKCU uninstall metadata. It does not silently install startup services. A temporary helper removes known installed files on uninstall. Settings remain in a separate local-data folder.
