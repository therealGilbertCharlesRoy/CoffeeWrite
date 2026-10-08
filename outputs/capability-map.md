# Screenwriting App — Capability Map

## Confirmed product direction

A polished cross-platform desktop app for individual writers, released Windows-first, for movie, TV, short film, YouTube, and theatre scripts. Core writing and project storage work locally. Writers can optionally connect Google Drive and opt into analytics. AI research is planned, but is a nonfunctional placeholder in the first release while subscription tiers, provider-key handling, and per-user usage controls are decided. Collaboration, schedules, and budgets are out of scope for the first release.

## Capability boundaries

| Module id | Responsibility | Depends on |
|---|---|---|
| `windows-shell` | Cross-platform desktop shell with a Windows-first release, three-pane layout, expandable/collapsible panels, and light/dark themes | — |
| `project-storage` | Local app-specific, unencrypted project files; user-chosen save locations; standalone scripts or TV series with seasons and episode scripts; saving, recovery, and project management | `windows-shell` |
| `writing-export` | Auto-formatting script editor for movie, TV, short film, YouTube (two-column audio/visual layout), and theatre; scene and character navigation; revision tools; Fountain import; PDF export; Final Draft .fdx import deferred pending legal review | `project-storage` |
| `story-development` | Scrollable beat board with editable/reorderable beats; character profiles and customizable attributes; visual relationship cards and connections | `project-storage` |
| `accounts-entitlements` | Future module: Google sign-in, website subscription checkout, and server-side AI entitlements; deferred until AI tiers and usage model are defined | `windows-shell` |
| `google-drive` | Optional Google Drive connection and local-first project sync for any user; limited per-file access and conflict protection | `project-storage` |
| `ai-research` | Nonfunctional AI placeholder in the first release; live web research and selected-context use are deferred | `accounts-entitlements` |
| `optional-analytics` | Consent-based usage and crash reporting, with no script content | `windows-shell` |
| `windows-release` | Signed Windows installer first; later macOS package and Linux `.deb`; `.rpm` may follow | `windows-shell` |

## Proposed build order

1. `windows-shell` and `project-storage`
2. `writing-export` and `story-development`
3. `google-drive` and `optional-analytics`
4. `windows-release`
5. Future: `accounts-entitlements` and `ai-research`, after tiering, provider, and per-user metering decisions

## File compatibility decisions

- Native editable projects use the app-specific project format.
- PDF export is included in the first release.
- Fountain screenplay import is included in the first release.
- Final Draft `.fdx` import is deferred until legal and technical review.

## Platform rollout

- First release: Windows 10 version 22H2 and Windows 11.
- Later releases: macOS and Linux using the cross-platform desktop foundation.
- First Linux package: `.deb`; `.rpm` is deferred.
- Linux distribution/version support will be stated explicitly rather than promising every distribution.

## AI deferral

- First release shows an AI placeholder only; it makes no model or web-research calls and requires no AI sign-in or subscription.
- Future AI calls must go through the product backend. Never ship the provider API key in a desktop binary.
- Future design must define tiers, per-user usage/metering and rate limits, and provider terms before enabling AI.
