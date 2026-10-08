# Spec: Optional Google Drive Sync

## Objective

Let any user optionally sync selected native projects to their own Google Drive. Drive is separate from AI, accounts/subscriptions, and analytics. Local projects remain usable offline and are saved locally before any cloud transfer.

## User stories

- As a writer, I can connect my Google account when I choose to sync a project.
- As a writer, I can keep working offline and have local changes sync when Drive is available.
- As a writer using the same project on another computer, I can get the latest version without silently losing either device's edits.
- As a writer, I can disconnect Drive without losing the local project.

## Functional requirements

1. Drive sync is optional, available to free and paid users, and requires the writer to explicitly connect Google Drive.
2. Request the narrowest practical per-file access. The proposed scope is Google Drive's `drive.file` scope; use an authorized file-selection flow for projects the writer chooses. Do not request broad access to all Drive files unless a required feature cannot work with per-file access and the writer approves that scope change.
3. Sync the app-specific native project file and its related data. Do not upload PDFs, analytics, or project content to the product's own server as part of Drive sync.
4. Save locally first. Show a clear status for local-only, syncing, synced, offline, and sync-error states. Queue eligible local changes while offline and retry when connected if auto-sync is enabled.
5. Let the writer choose which projects sync and provide a clear Drive destination or app folder. A project not selected for sync remains local only. For each Drive-linked project, provide an Auto-sync checkbox, off by default. When checked, local saves queue for sync; when unchecked, the writer can still trigger Sync manually.
6. Detect remote changes before replacing a cloud copy. Never silently overwrite a newer or concurrently modified project. Preserve both versions and offer a clear conflict-resolution path.
7. Keep a local copy available after disconnecting Drive. Disconnecting must revoke the app's Google access; cloud copies are not deleted unless the writer separately chooses to delete them.
8. Store Google OAuth refresh credentials in the operating system's protected credential store. Request Drive authorization separately from any future AI sign-in.
9. Provide a way to reconnect after access is revoked or expires and explain any required action.
10. Publish a privacy policy and complete applicable Google OAuth app verification before public release. Disclose what Drive files are accessed, where project content is stored, and how users disconnect or delete synced copies.
11. Drive API failures, quota limits, or service interruptions must not block local save, open, or editing.
12. Drive sync remains available on the Windows-first release and later cross-platform releases; it must not depend on AI being enabled.

## Security and data boundaries

- Drive project bytes move between the user's device and Google Drive using Google's documented API.
- Do not copy Drive project content to a product backend for sync or analytics.
- Validate OAuth state and tokens, store refresh credentials securely, and use least-privilege scopes.
- Do not log project names, script text, OAuth tokens, or file contents in sync telemetry.
- Google Drive authorization is independent from Google sign-in for the deferred AI feature.

## Technology and commands

Use Google Drive API v3 with the smallest scope that supports selected app project files. Google documents `drive.file` as per-file access for files created/opened by an app or shared through its Picker/file-selection flow, and recommends narrow scopes. The exact cross-platform OAuth and file-selection implementation is for the technical plan. See [Google Drive scope guidance](https://developers.google.com/workspace/drive/api/guides/api-specific-auth), [change tracking](https://developers.google.com/workspace/drive/api/guides/about-changes), and [Google API Services User Data Policy](https://developers.google.com/terms/api-services-user-data-policy).

Build, run, and verification commands will be recorded once the repository is established.

## Project structure

To be finalized in the implementation plan. Keep Drive API client, OAuth/token storage, sync state, conflict handling, and project-storage integration separate. The local project-storage module remains authoritative for offline work.

## Code style

To be finalized in the implementation plan. Treat cloud data and API responses as untrusted; keep provider-specific details behind the Drive adapter.

## Verification strategy

Before release, verify explicit connect/disconnect; minimum-scope consent; selected-project sync; offline queue and retry; remote-change detection; conflict preservation; token revocation; OAuth failure handling; local editing during Drive outages; and that no project text is sent to analytics or the product backend. No implementation or tests have been run yet.

## Boundaries

- Always: local-first saving; per-project opt-in; auto-sync checkbox is visible and off by default; manual sync remains available when auto-sync is off; Drive available without AI; no silent overwrite; disconnect never deletes local work.
- Ask first: requesting full Drive access, syncing every project by default, or storing project content on the product backend.
- Never: require an AI subscription for Drive; upload script content to analytics; log OAuth credentials or project contents.

## Success criteria

- A user can connect Google Drive, select a project, and choose whether auto-sync is enabled without enabling AI or analytics.
- With auto-sync enabled, local edits remain available offline and sync after connectivity returns. With auto-sync disabled, the writer can sync manually.
- A cloud conflict preserves both versions for the writer to resolve.
- Disconnecting Drive revokes access while preserving local projects.
- A user can review and understand the requested permissions and disconnect the integration.

## Open decisions

- Whether the app creates a dedicated Drive folder or lets the writer choose an existing folder.
- Exact file/folder selection flow compatible with Avalonia on each desktop platform.
- Conflict-resolution UI details and whether it offers a side-by-side comparison.
