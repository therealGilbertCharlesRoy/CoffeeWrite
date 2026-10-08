# Spec: Desktop Release and Installation

## Objective

Deliver a professional, locally installed desktop application. The first public release supports Windows 10 version 22H2 and Windows 11. Later releases use the shared codebase to add signed/notarized macOS builds and a Linux `.deb` package for declared Debian/Ubuntu targets. `.rpm` packaging is deferred.

## Windows release requirements

1. Ship a self-contained Windows x64 installer so writers do not need to install a separate .NET runtime.
2. Install the app in a conventional per-user or machine-wide location, with a Start Menu entry, optional desktop shortcut, displayed publisher/app name, version, and an uninstall entry in Windows Settings.
3. Preserve user-created project files and user-chosen save locations during update or uninstall. Uninstall may offer to remove app settings and recovery files only after a clear user choice.
4. Support a clean update path that preserves installed settings and does not replace a newer app with an older build.
5. Check for updates only after the user has enabled the feature or accepted the update policy. Show release version and a short change summary before installation. The installer must never modify user projects.
6. Sign release artifacts with a code-signing certificate before public distribution. The release process must protect the signing credential and produce verifiable signatures.
7. Provide a signed installer download from the product website, with version, architecture, operating-system requirement, file hash, privacy policy, and support information.
8. The installer and the installed app must work without AI, sign-in, Drive connection, or analytics consent. Network access is only needed to download an installer or use an explicitly enabled online feature.
9. Produce Windows x64 builds first. Windows ARM64 is a later packaging target, subject to demand and testing.

## Later platform requirements

### macOS

1. Distribute a signed macOS package for both Apple Silicon and Intel Macs where practical.
2. Notarize public macOS releases and give users normal drag/install, launch, update, and uninstall guidance.
3. Declare minimum macOS versions after verifying the chosen Avalonia version and test matrix.

### Linux

1. Distribute a `.deb` package for documented Debian/Ubuntu x64 targets first, with desktop-menu integration, icon, file association, install, and uninstall behavior.
2. State required system libraries and supported distribution versions in the release notes.
3. Add `.rpm` only after the `.deb` package has a stable release process and user demand supports it.

## Packaging rationale

The Windows release should begin with a signed standalone installer, suited to direct downloads from the product website. MSIX is a later distribution option if its clean install/uninstall and update model becomes useful; Microsoft documents those advantages and its App Installer support on Windows 10 and 11. Avalonia documents `.deb` packaging and desktop integration for Linux. See [Microsoft’s MSIX overview](https://learn.microsoft.com/en-us/windows/msix/overview), [Microsoft App Installer](https://learn.microsoft.com/windows/msix/app-installer/app-installer-root), and [Avalonia Linux deployment](https://docs.avaloniaui.net/docs/deployment/linux).

## Release process

1. Build versioned, self-contained artifacts from a tagged source revision.
2. Run the approved functional, storage-recovery, PDF-export, Drive-sync, and analytics-consent verification suite.
3. Scan release dependencies and confirm no secrets are present in the artifact or release metadata.
4. Sign the Windows installer, verify the signature and installer/uninstaller behavior on a clean Windows 10 22H2 device and a supported Windows 11 device.
5. Publish a versioned download and release notes; retain prior installer versions only where needed for rollback/support.

## Technology and commands

The initial installer definition uses Inno Setup as a per-user installer, so setup does not require administrator privileges. It creates a Start Menu entry, offers an optional desktop shortcut, and installs only under the user's local application directory. A GitHub Actions Windows workflow builds the self-contained x64 application, compiles the installer, and uploads an unsigned artifact plus SHA-256 checksum for 30 days. It can be triggered manually or by a `v*` tag. It does not publish to the website or sign the installer. Update behavior, code signing, and clean Windows install/update/uninstall verification remain release gates.

## Project structure

To be finalized in the implementation plan. Keep application publishing, installer definitions, signing configuration, release metadata, and platform package definitions isolated from application source. Never place certificates or signing secrets in the repository.

## Code style

To be finalized in the implementation plan. Installer logic must use versioned, declarative package definitions where practical, and must never rely on user-project paths as delete/update targets.

## Verification strategy

Before release, test installation, launch, update, uninstall, and recovery on clean supported Windows systems. Confirm that a project in a user-chosen folder is unaffected by updates and uninstall. Verify code signatures, update rollback protection, shortcut creation, settings cleanup choice, offline launch, and error messaging. Later macOS and Linux releases need parallel clean-device tests for each supported platform/distribution. No implementation or tests have been run yet.

## Boundaries

- Always: ship a signed Windows installer; preserve user projects; include uninstall support; test on clean supported devices; publish clear support and privacy information.
- Ask first: enabling forced automatic updates, changing install scope, adding Microsoft Store distribution, or deleting user settings/recovery files without a user-facing choice.
- Never: bundle an AI provider key or user content in an installer; delete user-chosen project locations during uninstall; commit code-signing credentials.

## Success criteria

- A Windows 10 22H2 or supported Windows 11 user can install, launch, update, and uninstall the app through normal Windows flows.
- The installer is signed and its publisher identity is verifiable.
- Local projects survive app update and uninstall.
- The product website provides a clear, versioned installer download and support information.
- A later `.deb` release installs and removes cleanly on the declared Debian/Ubuntu target.

## Open decisions

- Exact Windows installer technology and whether the first release is per-user only or also offers a machine-wide installer.
- Code-signing certificate provider and secure signing workflow.
- Update-hosting provider and retention policy for previous versions.
- Exact macOS version/architecture and Linux distribution support matrix.
