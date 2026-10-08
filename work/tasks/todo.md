# Screenwriting Desktop App — Task List

Tasks are ordered by dependency. Verification uses builds and manual acceptance checks; no automated test code is included in this initial plan.

## Foundation

### Task 1: Validate editor, PDF, and Fountain technology

**Description:** Compare Avalonia-compatible editor/document and PDF/import options; check platform coverage, performance, licensing, and maintenance. Build a small throwaway paginated-editor/PDF proof if documentation is inconclusive.

**Acceptance criteria:**
- [x] A short decision note names the document model/editor and PDF/Fountain approach with license findings.
- [x] The choice supports Windows 10 22H2/11 and the planned later Avalonia targets, subject to the documented OS test matrix.

**Verification:** Build and manually inspect the proof output if one is needed; review dependency licenses.

**Dependencies:** None.

**Files likely touched:** `work/research/editor-and-format-options.md`, optional isolated proof under `work/prototypes/`.

**Estimated scope:** Small.

### Task 2: Create the solution skeleton and build commands

**Description:** Create the Avalonia solution and the initial App, Core, Editor, Infrastructure, and Export project boundaries.

**Acceptance criteria:**
- [x] The solution builds locally with one documented command.
- [x] A Windows x64 publish command is documented; cross-platform build targets are recorded.
- [x] Third-party packages are limited to choices approved in Task 1.

**Verification:** Run the documented build and publish commands.

**Dependencies:** Task 1.

**Files likely touched:** solution file, project files, `Directory.Build.props`, `README.md`.

**Estimated scope:** Medium.

### Task 3: Add the main desktop window and three-pane shell

**Description:** Create the left beat-board pane, center writing surface, and right tools panel with initial proportions and independent scrolling.

**Acceptance criteria:**
- [x] The panes appear in the required left/center/right order.
- [x] The beat board begins at approximately one third of the window width.
- [x] The editor receives available space when side panes close.

**Verification:** Run on Windows and manually resize the window at common scaling levels.

**Dependencies:** Task 2.

**Files likely touched:** `src/Screenwriter.App/Views/MainWindow.axaml`, code-behind, shell view model, pane styles.

**Estimated scope:** Medium.

### Task 4: Add pane controls, tabs, and AI placeholder

**Description:** Add beat-board expand/collapse, right-pane tabs, and a clearly labeled AI placeholder with no sign-in or network calls.

**Acceptance criteria:**
- [x] The beat board expands and can be fully hidden/restored.
- [x] The right pane navigates between AI placeholder, Characters, and Relationships.
- [x] AI placeholder produces no network request and shows no purchase prompt.

**Verification:** Manually exercise each pane state while offline.

**Dependencies:** Task 3.

**Files likely touched:** shell views, shell view models, pane control styles.

**Estimated scope:** Medium.

### Task 5: Add light/dark themes and local preference

**Description:** Implement the global theme switch and persist its setting locally.

**Acceptance criteria:**
- [x] The user can switch themes from the shell.
- [x] The choice is restored after app restart.
- [x] Both themes have separate surface and text palettes in the shell.

**Verification:** Manually switch themes, restart, and inspect the workspace.

**Dependencies:** Task 3.

**Files likely touched:** theme dictionaries, settings service, settings view model, shell resources.

**Estimated scope:** Small.

### Task 6: Define the native project, series, and episode models

**Description:** Define the app-specific project container and versioned models for standalone scripts and TV series organized into seasons and episodes. Each episode owns a script document; characters and reference data can be shared at series level.

**Acceptance criteria:**
- [x] The project format is app-specific and unencrypted.
- [x] Data models have an explicit format version and migration boundary.
- [x] A project can contain a standalone script or a TV series with seasons and episodes.
- [x] Each episode has its own script document; project-level story data can be shared across the series.
- [x] The model supports script format profiles, beats, characters, and relationships.

**Verification:** Inspect sample standalone-script and multi-season series round-trip representations and migration notes.

**Dependencies:** Task 1.

**Files likely touched:** `src/Screenwriter.Core/Projects/`, `Documents/`, format-version code.

**Estimated scope:** Medium.

### Task 7: Add local create/open/save/Save As and series navigation

**Description:** Let writers select paths and save/open native projects without an account or network connection. Provide series, season, and episode navigation for series projects.

**Acceptance criteria:**
- [ ] Create, Open, Save, and Save As work through native file dialogs.
- [ ] A project reopens with its content intact.
- [x] Writers can add, rename, reorder, and open seasons and episodes within a series.
- [ ] Series-level characters and reference details are visible from the episode writing workflow.
- [ ] Local file operations make no network request.

**Verification:** Manually create and reopen a standalone project and a multi-season series with several episodes while offline.

**Dependencies:** Tasks 2, 6.

**Files likely touched:** project storage service, project commands, file-dialog adapter, shell menus.

**Estimated scope:** Medium.

### Task 8: Add safe writes, autosave, recovery, and migration handling

**Description:** Prevent partial-file replacement and recover unsaved work after an unexpected close.

**Acceptance criteria:**
- [x] Successful saves are atomic from the user's perspective.
- [x] Recovery state is detected and offered after an interrupted session.
- [ ] Older project versions migrate without silently dropping fields.

**Verification:** Manually interrupt a save/restart flow and open projects from each supported format version.

**Dependencies:** Tasks 6, 7.

**Files likely touched:** project storage service, recovery manager, migration registry, recovery prompt.

**Estimated scope:** Medium.

## Writing and export

### Task 9: Define the writing document model and format profiles

**Description:** Define typed writing elements and data-driven profiles for the five requested script formats.

**Acceptance criteria:**
- [x] The model represents scene heading, action, character, dialogue, parenthetical, transition, and optional shot elements, plus TV, stage, audio/visual, revision, and page metadata.
- [x] Format profiles define element behavior and page layout without UI-specific rules.
- [x] The selected profile persists in the native project.

**Verification:** Review sample documents for Movie, TV, YouTube, and Theatre profiles.

**Dependencies:** Tasks 1, 6.

**Files likely touched:** `src/Screenwriter.Core/Documents/`, `Formatting/`, model converters.

**Estimated scope:** Medium.

### Task 10: Implement common editor interactions

**Description:** Add insertion/editing of structured elements, keyboard transitions, undo/redo, find/replace, and spellcheck integration.

**Acceptance criteria:**
- [x] Writers can type and change element types without losing text.
- [x] Common character and scene-heading suggestions work.
- [x] Undo/redo and find/replace behave predictably.
- [x] Select and integrate a cross-platform spellcheck provider for the editor.

**Verification:** Manually write and edit a short sample scene using keyboard-only navigation.

**Dependencies:** Tasks 3, 9.

**Files likely touched:** editor view, editor interaction service, element command map, suggestion service.

**Estimated scope:** Medium.

### Task 11: Implement page layout and pagination

**Description:** Render a page-oriented screenplay surface with page breaks, title-page flow, headers, and numbering.

**Acceptance criteria:**
- [ ] The editor shows stable paginated pages as content changes.
- [ ] Page numbers, title page, and page setup follow the selected profile.
- [ ] Screen and print pagination use the same layout rules.

**Verification:** Manually compare short and long sample scripts across page boundaries.

**Dependencies:** Tasks 1, 9, 10.

**Files likely touched:** page layout engine, pagination service, page surface view, page settings model.

**Estimated scope:** Medium.

### Task 12: Add Movie and Short Film presets

**Description:** Configure feature-film and short-film screenplay formats on the shared editor model.

**Acceptance criteria:**
- [ ] Both presets provide screenplay elements and page layout.
- [ ] New projects retain their chosen preset.
- [ ] Common screenplay keyboard behavior is available.

**Verification:** Manually create and print-preview one sample in each preset.

**Dependencies:** Tasks 9-11.

**Files likely touched:** movie/short profiles, preset selector, template defaults.

**Estimated scope:** Small.

### Task 13: Add the TV teleplay preset

**Description:** Add TV episode metadata, acts/scenes, and teleplay layout.

**Acceptance criteria:**
- [ ] A TV script can store episode title/number and act/scene structure.
- [ ] TV-specific structure appears in page layout and PDF output.

**Verification:** Manually create and print-preview a multi-act sample.

**Dependencies:** Tasks 9-11.

**Files likely touched:** TV profile, metadata form, act/scene rendering.

**Estimated scope:** Small.

### Task 14: Add the two-column YouTube preset

**Description:** Add a video script with synchronized Audio and Visual columns and optional timing/section cues.

**Acceptance criteria:**
- [ ] Audio and Visual content can be edited independently row by row.
- [ ] Rows remain aligned when text wraps or moves to another page.
- [ ] The PDF preserves the two-column layout.

**Verification:** Manually create a multi-page sample with long audio and visual entries.

**Dependencies:** Tasks 9, 11.

**Files likely touched:** YouTube profile, two-column editor surface, row model, PDF layout rule.

**Estimated scope:** Medium.

### Task 15: Add the Theatre preset

**Description:** Add stage-play structure with acts, scenes, characters, dialogue, and stage directions.

**Acceptance criteria:**
- [ ] New Theatre projects use a stage-play profile.
- [ ] Acts/scenes and dialogue are distinguishable in editing and PDF output.

**Verification:** Manually create and print-preview a short two-act play.

**Dependencies:** Tasks 9-11.

**Files likely touched:** Theatre profile, act/scene structure view, layout settings.

**Estimated scope:** Small.

### Task 16: Import Fountain scripts

**Description:** Convert Fountain source into the native structured document and report unsupported constructs.

**Acceptance criteria:**
- [ ] Recognized content is preserved in an editable project.
- [ ] Unsupported syntax appears in an import summary and is not silently discarded.
- [ ] Import works offline and does not require an account.

**Verification:** Manually import representative Fountain samples and inspect text/layout.

**Dependencies:** Tasks 6, 9, 10.

**Files likely touched:** Fountain parser adapter, import command, import summary view, element mapping.

**Estimated scope:** Medium.

### Task 17: Export PDFs

**Description:** Export a script with stable pagination and optional title page.

**Acceptance criteria:**
- [ ] Writer can export script-only or script plus title page.
- [ ] All five presets preserve their intended layout.
- [ ] Export works offline without account/AI/Drive/analytics.

**Verification:** Manually inspect PDFs for all five presets and compare page breaks to the editor.

**Dependencies:** Tasks 11-16.

**Files likely touched:** PDF renderer, export dialog, format-to-PDF mapping, title-page renderer.

**Estimated scope:** Medium.

### Task 18: Add revision snapshots and production-style marks

**Description:** Store draft snapshots and provide colored revision marks/pages plus page locking.

**Acceptance criteria:**
- [ ] A writer can create and reopen draft snapshots.
- [ ] Changes can be marked using revision colors/pages.
- [ ] Locked pages remain stable while later edits are made elsewhere.

**Verification:** Manually revise a sample, inspect marks/locked pages, close, and reopen.

**Dependencies:** Tasks 6, 9-11.

**Files likely touched:** revision model, diff/marking service, page lock state, revision UI.

**Estimated scope:** Medium.

## Story development

### Task 19: Add beat cards and groups

**Description:** Add editable, reorderable beat cards with notes, colors/labels, and user-named groups.

**Acceptance criteria:**
- [ ] Beat board scrolls independently and follows shell expand/collapse.
- [ ] Beats can be created, edited, deleted, reordered, and grouped.
- [ ] Changes persist in the native project.

**Verification:** Manually edit and reorder a board, save, reopen, and confirm order/groups.

**Dependencies:** Tasks 3, 6.

**Files likely touched:** beat entities, beat-board view, card view model, project persistence mapping.

**Estimated scope:** Medium.

### Task 20: Link beats and scenes

**Description:** Connect beats to script scenes/positions and navigate between them.

**Acceptance criteria:**
- [ ] A beat can link to and unlink from a script scene or position.
- [ ] Selecting a link navigates to the target in the editor.
- [ ] Links survive reordering and project reopen.

**Verification:** Manually link, reorder, navigate, save, and reopen.

**Dependencies:** Tasks 9-11, 19.

**Files likely touched:** beat link model, scene navigator, editor navigation command, beat card action.

**Estimated scope:** Small.

### Task 21: Add character profiles and custom attributes

**Description:** Add character cards, default fields, and user-defined attributes.

**Acceptance criteria:**
- [ ] Writers can add/edit/delete characters.
- [ ] Default and custom attributes persist and can be edited.
- [ ] Character search/filter works offline.

**Verification:** Manually create varied characters, edit attributes, save, and reopen.

**Dependencies:** Tasks 3, 6.

**Files likely touched:** character entity, attribute model, character tab, profile editor, search view model.

**Estimated scope:** Medium.

### Task 22: Add the character relationship map

**Description:** Draw movable character cards and labeled mutual/directional connections.

**Acceptance criteria:**
- [ ] Connections have relationship labels and optional notes.
- [ ] Relationships can be mutual or directional.
- [ ] Moving cards, pan/zoom, and map state persist after reopening.

**Verification:** Manually build and reopen a graph with at least ten connected characters.

**Dependencies:** Task 21.

**Files likely touched:** relationship model, graph canvas, connection renderer, map interaction view model.

**Estimated scope:** Medium.

## Optional services

### Task 23: Connect Google Drive with per-file access

**Description:** Add user-initiated Google authorization using the narrowest practical scope and a clear destination flow.

**Acceptance criteria:**
- [ ] Drive connection is separate from AI and available to every user.
- [ ] Consent requests per-file access only for selected project files.
- [ ] Credentials are stored using protected OS storage and can be revoked.

**Verification:** Manually connect, revoke, reconnect, and inspect consent scope on each supported Windows target.

**Dependencies:** Tasks 2, 6-8.

**Files likely touched:** Drive OAuth adapter, token store, Drive settings view, connection state model.

**Estimated scope:** Medium.

### Task 24: Add Drive project selection and folder handling

**Description:** Let writers select which native projects sync and the Drive destination.

**Acceptance criteria:**
- [ ] Unselected projects remain local only.
- [ ] User can see which Drive project is linked to each local project.
- [ ] Disconnect preserves local files and does not delete cloud files.

**Verification:** Manually link projects and inspect local/cloud copies after disconnect.

**Dependencies:** Task 23.

**Files likely touched:** Drive project registry, destination selector, project settings view, file metadata mapper.

**Estimated scope:** Medium.

### Task 25: Add auto-sync toggle, manual sync, and offline queue

**Description:** Sync local changes when a project toggle is enabled; retain manual sync when it is off.

**Acceptance criteria:**
- [x] Auto-sync is off by default and configurable per project.
- [x] When off, manual Sync remains available.
- [x] When offline, local saves continue and queued sync retries when connectivity returns, the project opens, or Drive reconnects.

**Verification:** Manually exercise both toggle states across offline/online transitions.

**Dependencies:** Tasks 7, 8, 24.

**Files likely touched:** sync coordinator, per-project sync settings, status indicator, retry queue.

**Estimated scope:** Medium.

### Task 26: Add Drive conflict protection

**Description:** Detect remote changes and preserve both project versions for user resolution.

**Acceptance criteria:**
- [x] A stale local copy cannot silently replace a newer Drive copy.
- [x] Conflicting versions are both recoverable.
- [x] User can open the preserved Drive copy or explicitly keep the local copy and update Drive.

**Verification:** Manually edit the same test project on two installations and trigger sync.

**Dependencies:** Task 25.

**Files likely touched:** remote version tracker, conflict state model, conflict dialog, local duplicate workflow.

**Estimated scope:** Medium.

### Task 27: Add optional analytics consent

**Description:** Add default-off consent during first run and a Settings control to withdraw consent.

**Acceptance criteria:**
- [x] No analytics identifier or event is created/sent before opt-in; this build creates or sends none at all.
- [x] Opt-out is available at first run and from Privacy settings; no event queue or identifier exists to retain.
- [x] Analytics cannot block core features because no analytics transport is active.

**Verification:** Manually inspect app network behavior with consent on/off and offline.

**Dependencies:** Task 2.

**Files likely touched:** consent model, first-run settings, preferences view, analytics boundary.

**Estimated scope:** Small.

### Task 28: Verify analytics privacy and decide on crash reporting

**Description:** PostHog usage events and a bounded offline queue are implemented behind explicit consent and release configuration. Verify privacy behavior and decide whether scrubbed crash reporting should be added; crash data is currently not collected.

**Acceptance criteria:**
- [ ] Only typed allowlisted events/properties can be sent.
- [ ] Script text, project names, character/beat data, prompts, and local paths are excluded.
- [ ] Queued events are bounded and discarded safely when consent is withdrawn.

**Verification:** Manually inspect emitted sample payloads and opt-out behavior. If crash reporting is later approved, inspect sanitized crash reports before enabling it.

**Dependencies:** Task 27.

**Files likely touched:** analytics client, privacy UI, optional crash redactor if approved.

**Estimated scope:** Medium.

## Windows packaging

### Task 29: Build the Windows installer and lifecycle

**Description:** Per-user Inno Setup definition and a GitHub Actions workflow now package the self-contained Windows x64 app, Start Menu shortcut, optional desktop shortcut, uninstall entry, and SHA-256 checksum. The workflow supports manual dispatch and `v*` tags and uploads an unsigned, 30-day artifact. Update behavior and actual Windows lifecycle checks are still open.

**Acceptance criteria:**
- [ ] Installer artifact is produced by the Windows workflow.
- [ ] Installer works on Windows 10 22H2 and supported Windows 11.
- [ ] App appears in Start Menu and Settings uninstall list.
- [ ] Update and uninstall preserve user projects.

**Verification:** Manually install, update, uninstall, and relaunch projects on clean Windows systems.

**Dependencies:** Tasks 2-28.

**Files likely touched:** `packaging/windows/`, publish configuration, app identity/resources, install documentation.

**Estimated scope:** Medium.

### Task 30: Sign and publish the Windows release

**Description:** Add protected code signing and public website release metadata/downloads.

**Acceptance criteria:**
- [ ] Release signature verifies to the expected publisher.
- [ ] Download page shows version, architecture, OS requirements, hash, privacy, and support details.
- [ ] Release artifacts contain no secrets.

**Verification:** Verify signature and compare published hash with the built artifact.

**Dependencies:** Task 29 and availability of the user's signing certificate.

**Files likely touched:** release workflow, signing configuration, website download metadata, release notes.

**Estimated scope:** Medium.

## Checkpoints

### Checkpoint A: After Task 8
- [ ] Build succeeds; shell, local storage, offline behavior, and recovery are manually reviewed.

### Checkpoint B: After Task 18
- [ ] All five writing formats, Fountain import, PDF export, and revisions are manually reviewed.
- [ ] A multi-season TV series with multiple episode scripts reopens with its hierarchy and shared series details intact.

### Checkpoint C: After Task 22
- [ ] Beat, character, and relationship workflows round-trip through a local project.

### Checkpoint D: After Task 28
- [ ] Drive and analytics remain optional; conflicts preserve data; consent boundaries are manually reviewed.

### Checkpoint E: After Task 30
- [ ] Signed Windows installer lifecycle and release page are reviewed before public distribution.
