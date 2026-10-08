# Spec: Story Development

## Objective

Give a writer an integrated beat board and character/relationship workspace alongside the script editor. Story-development material is part of the local native project, works offline, and is available without an AI subscription or app account.

## User stories

- As a writer, I can capture, group, reorder, and connect story beats to script scenes while keeping the beat board visible beside the editor.
- As a writer, I can create character profiles with useful attributes and see how the characters relate to one another in a visual map.
- As a writer, I can move between writing, beats, character details, and relationships without losing my place in the script.

## Functional requirements

### Beat board

1. Provide a scrollable beat board in the left workspace pane. Its default visible width follows the shell spec; it supports that spec's expand and collapse controls.
2. Let the writer create, edit, delete, and reorder beat cards. Each beat has a title and notes/description.
3. Let the writer organize beats into user-named groups or sections (for example, acts or sequences) without requiring one story structure for every script format.
4. Support optional beat color/label and a link from a beat to a script scene or position. Selecting a linked beat or scene navigates to the corresponding item in the other view.
5. Preserve beat order, grouping, links, and card content when the project is saved and reopened.

### Character profiles and relationships

6. Let the writer create, edit, and delete character cards with a name and customizable attributes. Include useful default fields such as role, description, goals, and arc, while allowing the writer to add or rename attributes.
7. Show characters as cards in a visual relationship workspace in the right pane's Characters and Relationships tabs.
8. Let the writer connect two character cards and define the relationship with a label and notes. Support both mutual and directional relationships.
9. Let the writer reposition cards and pan/zoom the relationship map so it remains usable with larger casts. Connections stay attached to their characters after cards move.
10. Let the writer search or filter characters by name and relationship label.
11. Preserve characters, custom attributes, card positions, relationship labels/notes, and connection direction in the native project.

### Integration and boundaries

12. Story-development tools are available offline and to all users, regardless of AI subscription or analytics consent.
13. Beat and character data is saved with the associated native project. Google Drive synchronization is handled by the separate Drive module.
14. AI context use is deferred with the AI feature. When enabled in a later release, beat, character, relationship, or script content may be sent only after the writer explicitly selects it for a request, and the app must show the selected context before sending.
15. No collaboration, production breakdown, scheduling, budgeting, or automatic story generation is included in the first release.

## Technology and commands

To be selected in the implementation plan. The relationship map should use an interaction model suitable for card nodes and labeled connections; keep the story graph data independent from its visual layout.

## Project structure

To be finalized in the implementation plan. Suggested boundaries: beat data, character/attribute data, relationship graph data, and pane components, with project-storage APIs as the persistence boundary.

## Code style

To be finalized in the implementation plan. Keep story entities and validation independent of UI components; treat layout coordinates as presentation state rather than relationship meaning.

## Verification strategy

Before release, verify beat creation/editing/reordering/grouping and scene links; character creation and custom attributes; mutual and directional relationship creation/editing; map pan/zoom and card repositioning; save/reopen persistence; navigation to linked scenes; and availability offline without sign-in. No implementation or tests have been run yet.

## Boundaries

- Always: persist beats, characters, custom attributes, and relationships in the native project; keep these tools usable offline; preserve links when cards move.
- Ask first: sending project content to AI, adding collaboration, or changing the requested card-and-connection interaction.
- Never: make core story-development features require a subscription or silently send user-created story content to analytics.

## Success criteria

- The writer can create and organize beat cards in an independently scrollable left pane on the Windows-first desktop release.
- The writer can create character profiles with editable attributes and arrange cards in a relationship map.
- Character connections show a relationship label and whether it is directional or mutual.
- Changes remain intact after saving, closing, and reopening a native project.
- Beats, profiles, and relationships work offline and for free users.

## Open decisions

- Whether the default character attributes should vary by format (for example, theatre vs. screen) or use one shared starter set.
- Whether the map needs export/share output beyond showing the relationship graph in-app.
