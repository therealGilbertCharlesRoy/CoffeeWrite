# Spec: Writing and PDF Export

## Objective

Provide a professional cross-platform desktop script editor, released Windows-first for movie, TV, short film, YouTube, and theatre writing. Writers should focus on content while the app handles each format's structure and page layout. The editor stores work in the app-specific native project format and exports shareable PDFs.

## User stories

- As a writer, I can create a script from a format preset and write using the correct element types and page layout.
- As a writer, I can navigate scenes and characters, import an existing Fountain screenplay, and export a clean PDF.
- As a writer revising a draft, I can save versions and identify revisions without collaboration features.

## Functional requirements

### Editor and screenplay formatting

1. Provide structured writing elements and automatic formatting for screenplay styles: scene heading, action, character cue, dialogue, parenthetical, transition, and shot where appropriate.
2. Support keyboard-driven element changes and predictable Tab/Enter behavior, with suggestions for scene headings, character names, and common element continuations.
3. Support dual dialogue for simultaneous speech in screenplay formats.
4. Provide paginated page view, automatic page breaks, title page, page numbers, and configurable page setup. Keep screen appearance and PDF pagination consistent.
5. Provide find/replace, undo/redo, spellcheck, and a scene navigator.
6. Provide optional scene numbering and a revision workflow with saved draft snapshots, marked changes, colored revision marks/pages, and page locking. Omitted-scene controls remain out of scope unless later requested.
7. Keep editor content readable and keyboard accessible in both themes. The writing surface remains in the center pane.

### Format presets

8. First-release presets cover:
   - **Movie / Short Film:** screenplay page layout and screenplay elements.
   - **TV:** teleplay layout with episode metadata and act/scene structure.
   - **YouTube:** a two-column Audio/Visual layout, with timing/section cues available for a video script.
   - **Theatre:** stage-play structure with acts/scenes, character cues, dialogue, and stage directions.
9. Treat presets as format profiles over a shared editable document model, so future formats can be added without replacing the project file system.
10. Let the writer select a format when creating a script and preserve that selection with the native project.

### Import and export

11. Import Fountain screenplay files into a new or existing native project. Preserve recognized screenplay elements and source text; show a warning or import summary for unsupported constructs instead of silently dropping content.
12. Defer Final Draft `.fdx` import until legal and technical review. Do not use Final Draft libraries, branding, or proprietary code in the first release.
13. Export a script to PDF, with options to include or omit the title page. The PDF must preserve layout, page numbering, and the selected script format.
14. The app-specific native project remains the editable source of truth; PDF is a sharing/export format.

### Boundaries

15. Include writing, formatting, story-development integration, and draft revision support. Exclude production schedules, budgets, breakdowns, and collaboration in the first release.
16. Do not require internet access, app sign-in, AI subscription, Google Drive, or analytics consent to edit, import Fountain, or export a local PDF. The first release AI tab is a placeholder and does not participate in the writing workflow.

## Research basis

Current official product documentation describes common professional capabilities such as automatic formatting, character/location auto-completion, alternate dialogue, page-view and beat/outline workflows, title pages, scene navigation, scene numbering, and revision colors/locked pages. These examples informed the proposed feature baseline, not a requirement to copy any product's implementation. See [Final Draft Features](https://www.finaldraft.com/products/features/), [Final Draft screenplay formatting guide](https://www.finaldraft.com/learn/how-to-format-a-screenplay/), [Final Draft revision mode](https://kb.finaldraft.com/hc/en-us/articles/27813588722708-How-do-I-use-Revision-Mode), and [Celtx Film & TV Script Editor](https://support.celtx.com/hc/en-us/articles/360009310173-The-Film-TV-Script-Editor).

Fountain is selected as the initial interchange format because its official site describes it as a free, open-source screenplay markup language. Its syntax does not represent every production revision feature, so import must report unsupported items. See [Fountain](https://fountain.io/) and [Fountain syntax](https://fountain.io/syntax/).

## Technology and commands

The editor/document engine and PDF-generation libraries are to be selected in the implementation plan after validating fit with the cross-platform Avalonia shell and native project model. Build, run, and verification commands will be recorded once the repository is established.

## Project structure

To be finalized in the implementation plan. Keep format profiles, the editable document model, page layout/PDF rendering, Fountain import, and editor UI separated behind clear module boundaries.

## Code style

To be finalized in the implementation plan. Formatting rules should be data-driven per element/format profile rather than scattered through UI code.

## Verification strategy

Before release, validate each format preset; keyboard element transitions; pagination and page breaks; title page inclusion/exclusion; Fountain round-trip preservation for supported constructs; import warnings for unsupported constructs; PDF page layout; and editing/export while offline and signed out. No implementation or tests have been run yet.

## Boundaries

- Always: preserve editable content in the native project; keep local editing offline; export page-accurate PDFs; report import limitations clearly.
- Ask first: adding another exchange format, expanding into production tracking, or changing the five format presets.
- Never: silently discard imported text; make local editing dependent on an account or paid AI access; promise `.fdx` compatibility before legal/technical review.

## Success criteria

- A writer can create and edit scripts in each of the five requested format presets.
- The editor automatically formats supported script elements and keeps page layout stable as text is edited.
- A Fountain file imports into an editable native project, with unsupported constructs surfaced to the user.
- The user can export a readable PDF with or without a title page while offline.
- Draft revisions can be saved and reviewed without collaboration, scheduling, or budgeting features.

## Open decisions

- Which exact stage-play preset and TV teleplay styles to ship first.
- Whether TV metadata needs fields beyond episode title/number and the series/season hierarchy.
- Language and locale requirements for spellcheck, date formats, and typographic conventions.
