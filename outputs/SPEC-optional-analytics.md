# Spec: Optional Analytics and Crash Reporting

## Objective

Give the product owner useful, privacy-conscious measures of active use, feature adoption, retention, and stability without collecting screenplay content or requiring an account. Analytics is optional, off by default, and independent of Google Drive and the deferred AI feature.

## User stories

- As a writer, I can use the app without sharing analytics data.
- As a writer who opts in, I can see what is collected and withdraw consent at any time.
- As the product owner, I can understand aggregate active installs, feature use, retention, and crash patterns without receiving script text or personal writing details.

## Functional requirements

1. Show a clear analytics choice during first-run setup and in Settings. The default is off.
2. Use a locally generated, pseudonymous installation identifier only after the writer opts in. Do not require account creation or Google sign-in.
3. Collect only the minimum product events needed to answer the approved questions: app launched, active day, app version/platform, selected non-content feature used, and aggregate error or crash category.
4. Allow events that reveal adoption of features such as editor use, beat board, character/relationship tracker, Fountain import, PDF export, Drive connection, Drive sync result, theme selection, and AI-placeholder view. Do not include project titles, filenames, folder paths, script text, character names, beat titles, relationship labels, prompts, or file contents.
5. Collect only technical crash data needed to diagnose failures: app version, operating-system version, device architecture, error type, stack trace, and an anonymized installation identifier. Remove user paths and other project-derived values before transmission.
6. Keep analytics and crash reporting behind a single initial consent choice, with an option to split these controls later if user research calls for it.
7. When consent is withdrawn, stop collecting and sending new events immediately, clear the local analytics queue and installation identifier, and explain that previously received aggregate reports may be retained under the published retention policy.
8. If the app is offline, queue only consented events locally with a bounded queue; discard the oldest queued events when the cap is reached. Send only when a connection is available.
9. Do not use analytics data for advertising, data sale, profiling based on screenplay content, or unrelated tracking. Do not share it with Google Drive or any future AI provider.
10. Publish a privacy notice before public release that identifies the analytics/crash provider, collected event categories, purpose, retention period, opt-out path, and contact/deletion route where applicable.
11. Analytics failures must never block writing, saving, exporting, Drive syncing, or app startup.

## Data model and boundaries

The first release needs aggregate measures, not a user dossier. The expected reporting views are daily/monthly active opted-in installs, returning-install rate, app-version distribution, feature adoption counts, sync success/error rates, and crash-free install rate.

Every event must be validated against an allowlist of event names and non-content properties. The analytics SDK or backend must not use automatic screen capture, session replay, full-text logs, or broad custom-property collection. No raw project or writing data may be sent.

## Technology and commands

The analytics/crash provider is an implementation-plan choice. Choose one that permits event allowlists, data minimization, explicit consent control, deletion handling, and configurable retention. Build, run, and verification commands will be recorded once the repository is established.

## Project structure

To be finalized in the implementation plan. Keep consent state, event construction, event queueing, and provider transport behind a small analytics boundary that the writing and storage modules cannot bypass.

## Code style

To be finalized in the implementation plan. Define typed event names and typed, allowlisted properties. Do not accept arbitrary caller-supplied metadata.

## Verification strategy

The local first-run consent choice and Privacy control are implemented. PostHog Cloud usage-event transport is integrated behind both explicit opt-in and release configuration. The checked-in configuration has an empty project token and region, so this build creates no analytics identifier, queue, or transmission. When enabled, the client sends only fixed event names and allowlisted enum properties, keeps a bounded offline queue, and deletes its queued events and installation identifier on opt-out. Screenplay/project content and crash reports are not collected. Before release, verify default-off behavior, payloads, opt-out deletion, offline queue limits, and behavior when analytics is unavailable; configure the PostHog project and publish a privacy notice. Crash reporting remains a separate decision.

## Boundaries

- Always: collect consent before any analytics/crash transmission; minimize data; exclude all writing and project content; let writers revoke consent.
- Ask first: adding an event category, changing retention, adding a new analytics vendor, or enabling session replay.
- Never: send script text, titles, character or beat data, file paths, OAuth credentials, Drive contents, or payment/AI data to analytics.

## Success criteria

- A writer can leave analytics disabled and use every local feature normally.
- An opted-in writer can revoke consent, after which queued and new telemetry stops.
- The owner can see aggregate active installs, feature adoption, and crash trends without access to screenplay content.
- No permitted event schema can carry a project-derived content field or local path.

## Open decisions

- Crash-reporting provider, if crash reporting is approved later.
- Retention period for analytics, crash reports, and backups.
- Whether crash reporting should become a separate optional switch after the first release.
