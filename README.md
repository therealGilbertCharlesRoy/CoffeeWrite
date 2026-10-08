# Draftroom

Draftroom is a local-first screenwriting desktop app. The Windows release is the first target; the codebase is structured for macOS and Linux packages later.

## Build and run

Requirements: .NET 10 SDK.

```sh
dotnet restore Screenwriter.slnx
dotnet build Screenwriter.slnx
dotnet run --project src/Screenwriter.App/Screenwriter.App.csproj
```

Create a self-contained Windows x64 publish output with:

```sh
dotnet publish src/Screenwriter.App/Screenwriter.App.csproj -c Release -r win-x64 --self-contained true
```

The publish output is not yet an installer. The Inno Setup definition is in `packaging/windows/Draftroom.iss`; it installs per-user, creates a Start Menu shortcut, optionally creates a desktop shortcut, and does not remove user projects on uninstall. A Windows installer can be built from Ubuntu by pushing this repository to GitHub and running the **Windows installer** workflow from the Actions tab, or by pushing a `v*` tag. The workflow uploads an unsigned installer and its SHA-256 checksum as a 30-day artifact. It does not publish a public release. Code signing and Windows-device install/update/uninstall checks remain release requirements.

## Project map

- `src/Screenwriter.App` — Avalonia desktop app and UI.
- `src/Screenwriter.Core` — platform-independent product and document models.
- `src/Screenwriter.Editor` — writing and screenplay editing behavior.
- `src/Screenwriter.Infrastructure` — local storage and later optional integrations.
- `src/Screenwriter.Export` — import and export functionality.
- `outputs` — approved product specifications.
- `work/tasks` — implementation plan and task checklist.

The AI panel is a placeholder in this first stage. It makes no network requests. Local editing and saving are designed to work independently of online services.

Analytics is off by default and has a first-run choice plus a Privacy control. With consent enabled, Draftroom sends only named, allowlisted product events to PostHog Cloud. Events use a random installation ID created after opt-in and include app version/platform; they exclude script text, project names, paths, character and beat details, prompts, and file contents. The queue is local, bounded to 500 events, and is cleared with the ID when consent is withdrawn. Analytics errors never block normal app work.

PostHog remains inactive until a release configures `ProjectToken` and `Region` (`us` or `eu`) in `src/Screenwriter.App/posthog-analytics.json`, or provides `DRAFTROOM_POSTHOG_PROJECT_TOKEN` and `DRAFTROOM_POSTHOG_REGION`. The project token is a public write-only token, not a personal API key. Before release, disable IP data capture in PostHog project settings, keep autocapture and session replay off, and publish the required privacy notice. This build does not send crash stack traces.

## Google Drive release configuration

Google Drive desktop authorization uses the system browser, PKCE, and the narrow `drive.file` scope. To enable it for release, set the Google Cloud OAuth client ID for a Desktop app in `src/Screenwriter.App/google-drive-oauth.json` (`ClientId`) or provide `DRAFTROOM_GOOGLE_DRIVE_CLIENT_ID` to the app build environment. The client ID is public application configuration; never place a client secret or user refresh token in this file. Development builds without a configured ID remain local-only. The current Drive implementation supports Windows Credential Manager, per-project opt-in, manual sync, optional auto-sync, an offline retry flag, and conflict-copy preservation. Linux and macOS credential storage and transfer support are not implemented yet. Live OAuth and transfer verification requires the release client ID.
