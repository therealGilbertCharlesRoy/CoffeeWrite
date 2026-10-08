# Deferred Spec: Accounts and AI Entitlements

> **Status:** Not part of the first release. The first release includes only a nonfunctional AI placeholder. This future spec is a planning note; subscription tiers, payment provider, provider-key model, and per-user metering must be decided before implementation.

## Objective

When AI is ready to launch, let users subscribe through the product website and sign into the desktop app with Google to access it. Local writing, native project files, PDF export, story-development tools, and optional Google Drive sync must remain available without an app account or AI subscription.

## User stories

- As a writer, I can use the local writing app without creating an account.
- As an AI subscriber, I can sign in with Google and use the AI assistant while my website subscription is active.
- As a subscriber, I can see my subscription status and manage the subscription through the website.
- As a user whose subscription is inactive or whose connection is unavailable, I can still write locally; AI requests are unavailable until entitlement can be verified.

## Functional requirements

1. Use Google sign-in for AI identity. App account sign-in is only prompted when the writer chooses to use or manage AI access.
2. Subscription checkout is on the product website. The selected payment provider and the website's billing implementation are unresolved; do not collect or store raw payment-card details in the desktop app or the app's own account database.
3. Bind subscription status to a stable Google account identifier, not an email address alone.
4. Verify subscription status on the server for every AI request. The client must not be the authority for paid entitlement.
5. The website/backend must process verified payment-provider subscription events, including activation, renewal, cancellation, expiration, and refund/revocation as supported by the chosen provider. Event handling must be authenticated and idempotent.
6. Return clear app states for signed out, active subscription, canceled-but-active-through-period, expired, and verification unavailable. The user can open the website to manage billing.
7. If entitlement cannot be verified online, deny the AI request with a clear connection/status message while leaving local writing and project access available.
8. Store sign-in credentials using the host operating system's protected credential storage. Use revocable, short-lived access tokens where supported; do not place secrets or long-lived tokens in ordinary project files or analytics.
9. Keep Google Drive authorization separate from AI sign-in and entitlement. Signing in for AI must not automatically grant Drive access, and connecting Drive must not imply an AI subscription.
10. The desktop client must never contain the AI provider API key; future AI requests go through the product backend, which performs authorization and per-user rate limiting before calling the model provider.
11. Account and billing events may be recorded for security and subscription support, but must not include screenplay text, prompts, tokens, or payment-card data.
12. Provide a path to sign out, revoke the app's Google authorization, and request account deletion. Account data retention and deletion timelines must be decided before release.

## Future trust boundaries and abuse cases

- **Desktop client ↔ product backend:** treat client claims about identity, subscription, and entitlement as untrusted. Revalidate identity and entitlement server-side.
- **Website/payment provider ↔ subscription backend:** reject unsigned or replayed subscription events; make event processing idempotent.
- **Desktop client ↔ AI backend/provider:** prevent non-subscribers from calling paid AI endpoints; keep provider credentials server-side; rate-limit abusive usage.
- **User/project content ↔ AI request:** only include project material explicitly selected by the writer for that request; show the context being sent and avoid logging it.

Future assets include Google account identifiers, subscription state, authentication tokens, AI provider credentials, per-user usage records, and any selected script context. The system must minimize stored account data and never store payment-card credentials.

## Technology and commands

Identity, payment-provider, backend, and desktop token-storage technologies are open implementation decisions. Select them in the technical plan. Build, run, and verification commands will be recorded once the repository is established.

## Project structure

To be finalized in the implementation plan. Keep website checkout/webhooks, account identity, entitlement checks, and desktop sign-in as separate components. The AI service must consume a server-side entitlement result, not a client-supplied flag.

## Code style

To be finalized in the implementation plan. Validate all external inputs, keep authorization checks server-side, and avoid logging sensitive values.

## Verification strategy

Before release, verify Google sign-in and sign-out; active, canceled, expired, and unknown subscription states; forged client-side entitlement values; invalid/replayed payment events; token revocation; unavailable network behavior; AI denial without active entitlement; and that local writing remains usable throughout. No implementation or tests have been run; this module is deferred.

## Boundaries

- Always: website subscription checkout; Google sign-in for AI; server-side entitlement checks; local app features remain account-free; no provider secret in the desktop binary.
- Ask first: adding another identity provider, requiring an account for local writing or Drive, or changing the billing channel.
- Never: trust a client-only subscription flag; send selected script context without explicit writer action; store raw payment-card details in the desktop app.

## Future success criteria

- A user can subscribe on the website and sign in with the same Google identity in the desktop app.
- Only a server-verified active subscription can make an AI request.
- Cancelled or expired accounts cannot initiate further AI requests after their paid access ends.
- A network or identity-service outage blocks AI only; local writing and project access continue.
- Google Drive authorization remains independent from AI subscription state.

## Open decisions

- Subscription tiers and price structure.
- Payment processor and subscription management portal.
- Whether the user may switch the Google account linked to an existing subscription.
- Data retention and deletion timelines for account and billing metadata.
- Provider terms and key ownership model for serving multiple users under the product account.
- Per-user usage metering, tier limits, rate limits, and any grace period for transient entitlement-service outages.
