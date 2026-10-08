# Implementation Plan: Screenwriting Desktop App

## Overview

Build a local-first, cross-platform screenwriting app and ship Windows first. The first release includes a native project format, five writing presets, Fountain import, PDF export, revision tools, a beat board, character/relationship tracking, optional Google Drive sync, optional analytics, and a signed Windows installer. macOS and Linux follow; Linux starts with `.deb`. The AI pane is a nonfunctional placeholder until subscription tiers and multi-user AI usage are designed.

Approved requirements live in `outputs/capability-map.md` and the module specs under `outputs/SPEC-*.md`.

## Architecture decisions

- Use C#/.NET with Avalonia as the proposed shared desktop UI foundation; release Windows first.
- Keep writing, project storage, beats, characters, relationships, and PDF export local and usable offline.
- Use a versioned app-specific, unencrypted native project format. Save locally first; cloud is optional.
- Treat the writing document model and format profiles as separate from screen layout and file persistence.
- Use Google Drive per-file access for projects selected by the user. Auto-sync is per project, off by default; manual sync remains available.
- Keep analytics off by default, typed and allowlisted, with no script or project content.
- The first release has no live AI calls, AI login, or subscription checkout. Keep only the clearly labeled placeholder.
- Distribute a self-contained, signed Windows x64 installer first. Defer macOS and Linux packaging until the Windows release is stable; start Linux with documented `.deb` targets and defer `.rpm`.
- Keep document/PDF/import library choices open until an early technical spike checks format fidelity, performance, cross-platform support, and licenses.

## Provisional v1 scope assumptions

- Native projects can represent either a standalone script or a TV series with seasons and episodes. Each episode has its own script document and can share series-level characters and reference details.
- The first implementation targets Windows 10 22H2 and supported Windows 11 releases. Later platform minimums are decided before those releases.
- The first cross-platform UI uses resizable pane dividers in addition to the required expand/collapse controls.
- AI is represented by a placeholder tab, initially visible in the right pane; it makes no network requests.
- The app opens in a useful writing/story-development view without sign-in. Analytics consent is a separate first-run choice.

## Project structure proposal

- `src/Screenwriter.App/` — Avalonia desktop shell and platform entry points.
- `src/Screenwriter.Core/` — project, document, format-profile, beat, character, and relationship models.
- `src/Screenwriter.Editor/` — editor interactions, page layout, screenplay formatting, and revision workflows.
- `src/Screenwriter.Infrastructure/` — local project storage, migration, recovery, Drive, and analytics adapters.
- `src/Screenwriter.Export/` — PDF rendering and Fountain import.
- `packaging/` — Windows installer definitions and later macOS/Linux package definitions.
- `docs/` — approved specifications and product documentation.

Adjust this layout during the foundation task if the selected Avalonia/document-engine integration has a clearer separation.

## Task List

### Phase 0: Technical foundation and risk checks

- [x] Task 1: Validate cross-platform UI, editor, PDF, and Fountain-import options. Decision and remaining proof requirements are recorded in `work/research/editor-and-format-options.md`.
- [x] Task 2: Create the solution skeleton and reproducible local build/publish commands. The solution builds; Windows x64 publish is being checked.

### Checkpoint: Foundation selection

- [ ] Avalonia and chosen editor/document libraries work together on the Windows-first target.
- [ ] License and platform-support checks are recorded for every chosen dependency.
- [ ] User reviews any change to the approved architecture before feature work proceeds.

### Phase 1: Desktop shell and local projects

- [x] Task 3: Build the three-pane shell and primary writing workspace.
- [x] Task 4: Add beat-board controls, right-pane tabs, and the offline AI placeholder.
- [x] Task 5: Persist the light/dark preference locally.
- [x] Task 6: Define versioned native project, series, season, and episode models.
- [x] Task 7: Add native project create/open/save/Save As flows and season/episode navigation.
- [x] Task 8: Add safe writes and recovery offer after an interrupted session. Version migration remains a follow-up.

### Checkpoint: Offline foundation (after Task 8)

- [ ] App launches signed out and offline.
- [ ] User can create, save, close, and reopen a project at a chosen path.
- [ ] Beat, character, relationship, and script data can share one project model.

### Phase 2: Writing and PDF export

- [x] Task 9: Define the structured writing document model and data-driven format profiles.
- [x] Task 10: Implement common editor interactions.
- [ ] Task 11: Implement the paginated editor.
- [ ] Task 12: Add Movie and Short Film presets.
- [ ] Task 13: Add the TV teleplay preset.
- [ ] Task 14: Add the two-column YouTube Audio/Visual preset.
- [ ] Task 15: Add the Theatre preset.
- [ ] Task 16: Import Fountain files with a summary for unsupported constructs.
- [ ] Task 17: Export paginated PDFs, including optional title pages.
- [ ] Task 18: Add revision snapshots, colored revision marks/pages, and page locking.

### Checkpoint: Writing flow

- [ ] Each format can create and edit a script in the center writing area.
- [ ] Fountain import preserves supported text and reports unsupported items.
- [ ] PDF export works offline and preserves selected layout.
- [ ] Revision changes are visible and page locks survive reopening.

### Phase 3: Story development

- [ ] Task 19: Add beat cards and groups.
- [ ] Task 20: Link beats and scenes.
- [ ] Task 21: Add character profiles and customizable attributes.
- [ ] Task 22: Add the visual relationship map with mutual/directional labeled links.

### Checkpoint: Story tools

- [ ] Beat and character data round-trip through native project storage.
- [ ] Relationship graph remains usable with pan/zoom and movable cards.
- [ ] All story-development features work offline and without sign-in.

### Phase 4: Optional online services

- [ ] Task 23: Add Google Drive connection with per-file access.
- [ ] Task 24: Add Drive project selection and folder handling.
- [ ] Task 25: Add per-project auto-sync, manual sync, and offline queue.
- [ ] Task 26: Add Drive conflict protection.
- [x] Task 27: Add analytics consent, off by default, and opt-out behavior. The first-run Privacy choice and later Privacy control are implemented; no provider or transport is active.
- [ ] Task 28: Complete analytics privacy verification and decide whether to add scrubbed crash reporting. PostHog usage events and bounded consent-gated queue are implemented; crash data is not collected.

### Checkpoint: Optional services

- [ ] Drive works without AI and never blocks local saving.
- [ ] Auto-sync can be enabled/disabled per project; conflicts never silently overwrite.
- [ ] No analytics is sent without opt-in; project content and local paths are excluded.

### Phase 5: Windows release

- [x] Task 29: Add a per-user Inno Setup installer definition and Ubuntu-triggerable Windows build workflow. Installer artifact creation and Windows install/update/uninstall verification remain pending.
- [ ] Task 30: Add release signing, update behavior, and website download metadata.

### Checkpoint: Windows release candidate

- [ ] Installer and uninstall preserve user projects.
- [ ] Release artifact signature is verifiable.
- [ ] Clean Windows 10 22H2 and Windows 11 install/update/uninstall flows are verified.
- [ ] Product/privacy information and support contact are ready for public release.

### Later phases

- [ ] Add macOS packaging, signing/notarization, and platform verification.
- [ ] Add Linux `.deb` for named Debian/Ubuntu targets; consider `.rpm` after `.deb` is stable.
- [ ] Revisit live AI, website billing, account sign-in, tiers, provider terms, and per-user metering only after a dedicated approved specification.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Page-accurate editing across five formats is complex | High | Prototype pagination and PDF output before building out the whole editor; keep format rules data-driven. |
| Native project data can be lost or corrupted | High | Versioned container, safe writes, recovery copies, and explicit migration handling. |
| Cross-platform support varies by OS and Linux distribution | Medium | Windows first; publish a narrow tested matrix; defer macOS and `.deb` packaging until the Windows flow is stable. |
| Drive sync can overwrite edits made on another device | High | Local-first saves, remote version checks, preserve both copies, and require explicit conflict resolution. |
| Google OAuth review or consent changes can delay Drive release | Medium | Use narrow file scope, clear privacy disclosures, and start verification before public release. |
| Third-party writing/PDF libraries may have restrictive licenses | High | Review licenses and redistribution requirements during Task 1 before committing to a dependency. |
| Series and episode organization expands project navigation and data model | Medium | Keep a clear hierarchy of series, seasons, episodes, and script documents; validate creation, navigation, and Drive sync behavior in the project-model phase. |
| Public installer warning or signing delays release | Medium | Keep signing as a release gate; allow unsigned internal builds only, never present them as public releases. |

## Open questions

- Which exact TV and Theatre templates and writing languages are needed first?
- Which analytics provider and data-retention period should be used?
- Which Windows installer format and update delivery approach best match the website download flow?
- Which Debian/Ubuntu versions should the first `.deb` support?

## Verification approach

This plan uses focused build and manual acceptance checks at each checkpoint. Automated test code is not included in the initial task breakdown; add it only if requested. Do not release until local recovery, PDF fidelity, Drive conflict handling, analytics consent, and installer lifecycle have been manually verified.
