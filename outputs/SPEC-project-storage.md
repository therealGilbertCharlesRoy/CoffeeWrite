# Spec: Local Project Storage

## Objective

Let a writer create, open, and save screenplay projects locally in an app-specific format at a location they choose. The project format is proprietary to this app but is not encrypted. Local writing and saving must work without an account or internet connection.

## Scope

A native project stores the writer's editable script material and its related story-development data, including beats, character profiles, attributes, and relationships. PDF is an export, not the editable project format. Google Drive sync is specified separately and must operate on the same project format.

A project may contain either one standalone script or a TV series organized into seasons and episode scripts. Series-level character and reference details may be shared across episodes.

## Functional requirements

1. Let the user choose a location when creating a project and offer Save, Save As, and Open flows using standard Windows file dialogs.
2. Store project data in a versioned, app-specific file format. The format is not encrypted and does not require an account or network connection to open or save.
3. Preserve the script and linked story-development data together when saving and reopening a project.
4. Support standalone scripts and TV series with seasons and multiple episode documents in one project.
5. Save edits reliably, including a recovery path after an interrupted save or unexpected app closure. Use safe writes and retain recoverable prior state where feasible.
6. Support project-format versioning so future app releases can migrate older projects without silently discarding data.
7. Keep project storage independent from AI, analytics, and Google Drive services. Local save/open must not make network requests.
8. Accept imported Fountain scripts as source material for a native project; the Fountain import behavior and formatting fidelity belong to the writing module specification.

## Technology and file design

The initial implementation uses a `.draftroom` app-specific container with a format-version header and compressed structured project data. It is not encrypted and must not be described as secure or confidential. Keep the container versioned, preserve structured script/story data, and update it transactionally.

## Commands

See the repository `README.md` for build and publish commands. Windows x64 publish is self-contained so end users do not need a separate .NET runtime.

## Project structure

To be finalized with the implementation plan. Storage APIs should be a distinct module consumed by the editor, story-development tools, and later Google Drive sync.

## Code style

To be finalized with the implementation plan. Keep persistence/migration logic separate from UI and document editing logic.

## Verification strategy

Before release, verify create/open/save/save-as at user-selected paths; round-trip preservation of scripts, beats, characters, attributes, and relationships; operation without internet or sign-in; safe handling of interrupted writes; and migration behavior for older project versions. No implementation or tests have been run yet.

## Boundaries

- Always: local storage works offline; the user chooses the project location; preserve linked story data; keep project files unencrypted as agreed.
- Ask first: changing the app-specific project format or requiring an account to read/write local projects.
- Never: make local save depend on AI access, Google Drive, or analytics consent.

## Success criteria

- A writer can create, save, close, and reopen a project from a location they chose.
- The same project reopens with script and story-development data intact.
- The app can save and reopen local projects while offline and signed out.
- An interrupted save does not silently replace a valid project with a partial file.

## Open decisions

- How many automatic local recovery copies to retain and how long to keep them.
