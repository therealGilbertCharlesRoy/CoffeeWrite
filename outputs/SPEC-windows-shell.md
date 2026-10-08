# Spec: Desktop Shell and Writing Workspace

## Objective

Build the cross-platform desktop shell for a professional, single-writer scriptwriting app. Release Windows first; preserve the same core shell for later macOS and Linux releases. The workspace places the beat board on the left, writing editor in the center, and research/story-development tools on the right. The core workspace opens without requiring an account and remains usable offline.

## User and product context

The app serves individual writers creating movie, TV, short film, YouTube, and theatre scripts. The first release targets Windows 10 version 22H2 and Windows 11. macOS and Linux follow in later phases; the first Linux package is `.deb`, with `.rpm` deferred. The first release excludes collaboration, production schedules, and budgets. AI research is a nonfunctional placeholder in the first release; its tiers, billing, and usage model are deferred. Optional Google Drive sync is available to any user, and optional analytics requires separate consent.

## Shell requirements

1. Present three primary areas: beat board (left), writing editor (center), and a right-side tool panel.
2. When visible in the normal workspace, the beat board starts at approximately one third of the app width, scrolls independently, and has controls to expand or fully collapse it.
3. The center writing area remains the primary work surface. Collapsing either side panel gives the editor the freed space.
4. The right-side panel has tabs for AI Research, Characters, and Relationships. The AI Research tab displays a clearly labeled placeholder and makes no model or web-research calls in the first release. It does not prompt for AI sign-in or payment. Characters and Relationships remain usable offline.
5. Include a global light/dark mode toggle and remember the user's choice locally.
6. Keep the layout usable when the window is resized and at common Windows display scaling levels. Controls must remain keyboard accessible.
7. Do not require account sign-in to launch, write, or save local projects.

## Proposed technology

Recommend C# with Avalonia for a shared desktop UI codebase targeting Windows, macOS, and Linux. Avalonia documents support for Windows 10/11, macOS, and desktop Linux; platform and distribution support tiers vary. Its current matrix lists Windows 10 22H2 as Tier 2 and notes that native Linux Wayland support is experimental, so the first Linux release should target a documented `.deb` distribution matrix. See [Avalonia supported platforms](https://docs.avaloniaui.net/docs/supported-platforms) and [Linux platform details](https://docs.avaloniaui.net/docs/platform-specific-guides/linux).

## Commands

To be finalized after the app repository and development environment are established.

## Project structure

To be finalized with the implementation plan. The shell will be isolated from script document storage and the writing engine behind module boundaries in the approved capability map.

## Code style

To be finalized with the implementation plan. Prefer Avalonia controls and shared theme resources; avoid embedding product logic in view markup.

## Verification strategy

Before release, manually verify: default pane proportions; independent beat-board scrolling; expand, collapse, and restore behavior; right-panel tab navigation; light/dark persistence; keyboard access; window resizing and display scaling; and launch/write/save while offline and signed out. No implementation or tests have been run yet.

## Boundaries

- Always: keep local writing available without an account; make the editor the primary workspace; support independent panel visibility; store theme preference locally.
- Ask first: changing the three-pane layout, requiring sign-in for basic writing, or removing the light/dark toggle.
- Never: expose live AI before its subscription and usage model is defined; send script content as analytics; bundle an AI provider key in the desktop app.

## Success criteria

- A Windows user on Windows 10 22H2 or a supported Windows 11 release can launch the installed app without signing in and reach the writing workspace.
- The beat board, writing area, and tool panel appear in the intended left/center/right order.
- The user can expand or collapse the beat board, navigate the right panel's tabs, and switch themes.
- The workspace remains usable after resizing the app window and while offline. The shared UI foundation can be packaged for macOS and the declared Linux `.deb` targets in later releases.

## Open decisions

- Whether users can resize pane widths by dragging dividers, in addition to the required expand/collapse controls.
- Exact first-run default for the right panel (AI Research vs. last-opened tab).
