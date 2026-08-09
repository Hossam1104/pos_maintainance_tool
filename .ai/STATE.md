# Current Project State

- **Updated:** 2026-08-10
- **Branch:** `main` after the verified POS-M04R2 remote-trigger uncertainty corrective merge
- **Release or milestone:** .NET 10 + Angular 22 migration; Sessions 00-08 complete; standalone Angular expansion frozen; POS preparation programme active

## Working State

- `PosAdminTool.WinUI` remains the compatibility/parity baseline for configuration, service
  control, backup/restore, maintenance, downloader, and activity workflows.
- The solution contains Domain, Application, Infrastructure, Contracts, Agent, retained WinUI,
  Angular Web, and four xUnit test projects. The Agent is Windows-only, same-origin, loopback-only,
  Negotiate/local-Administrators authorized, antiforgery-protected, and DPAPI-backed for secrets.
- Long work uses the bounded in-memory Agent operation registry: REST is state truth, SSE is
  transport, idempotency is principal-scoped, named locks serialize conflicts, and destructive
  completions are appended to sanitized JSONL audit.
- POS owns backend/domain/security/privileged-operation and merge-readiness work. RMS+ Support Hub
  owns the final Angular shell, navigation, shared visual system, branding, themes, and integrated
  route experience. Standalone Angular expansion remains frozen.

## Verified Baseline and Earlier Gates

- Sessions 00-08 established the .NET 10/Angular 22 baseline; 125 .NET tests, Angular tests,
  backup E2E, and retained WinUI publish passed without real SQL execution.
- POS-M01, POS-M02/POS-M02R/POS-M02R2/POS-M02R3, POS-M03, and POS-M03R are complete with their
  bounded-state, restore-safety, canonical-root, partial-outcome, and exact-target gates; all
  evidence was fake/temp-only and the early Restore review gate was explicitly cleared.

## Verified POS-M04 Through POS-M04R

- POS-M04/M04R retain the server-owned downloader boundary, DPAPI secret isolation, bounded
  operation/idempotency/audit, connection-bound DNS/SSRF validation, manual redirects, canonical
  SMB ownership/path safety, stable archive discovery, opaque artifacts, and accepted-trigger
  truth through later repository failure, partial completion, and cancellation.
- Prior M04/M04R gates passed 247/257 full Release .NET tests, zero-warning builds, and retained
  WinUI publish; no real endpoint, SMB share, service identity, device mutation, or Angular
  Downloader UI evidence was used.

## Verified POS-M04R2 Remote Trigger Uncertainty Truth Closure

- Dispatch history is explicit: pre-dispatch validation/cancellation/connection-bound SSRF is
  `NotAttempted`; definitive rejection is `Failed`; positive acknowledgement is `Accepted`; and
  post-dispatch cancellation, timeout, connection loss, response transport, or local response
  policy failure is `OutcomeUnknown` with `downloader.trigger_outcome_unknown` and safe guidance.
- REST/audit expose the explicit string-enum `TriggerState` while `TriggerAccepted` is derived;
  unknown is terminal `Failed`, stops before SMB/artifacts, never auto-retries, and preserves
  sanitized state/code. Local operation idempotency is not remote idempotency; no remote
  job-status/reconciliation/trigger-idempotency capability is verified.
- Three Infrastructure, one Application, one Agent worker, and one contract test were added;
  complete Release validation passed 263 .NET tests, zero-warning solution build passed, and
  retained WinUI `win-x64` publish passed using fakes/temp-only infrastructure.

## Active Programme

- Canonical plan: `docs/POS_SUPPORT_HUB_MERGE_PREPARATION_PLAN.md`.
- Canonical prompts: `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`.
- `TASK.md` contains the complete POS-M05 landing/collision audit prompt pending owner authorization;
  POS-M04R2 is complete, POS-M05 requires the Claude Opus 5 R1 review outcome before POS-M06, and
  POS-M06 is review-gated.
- Repository merge beyond the authorized session lifecycle, Angular integration, standalone
  installer cutover, and WinUI removal are not otherwise authorized.

## Known Risks and Gates

- Managed-root behavior under LocalSystem, representative-device SCM control, and SMB Session 0
  behavior still require safe representative-device evidence. Fake tests do not replace that gate.
- Manual live-Agent SSE and real browser Negotiate/admin evidence remain operational evidence gaps.
- ADR-012's representative LocalSystem/Session 0 SMB gate remains open: installed-service proof on
  an isolated non-production device/server must demonstrate WNetAddConnection2, enumeration,
  newest-batch discovery, ZIP read/download, cancellation/timeout cleanup, and scoped disconnect;
  fake tests do not replace it.
- Remote trigger reconciliation/idempotency remains open: no independently verified remote
  job-status or idempotency contract exists, so an unknown trigger must be checked remotely before
  any operator-directed retry.
- WinUI remains retained until cross-project RMS+ Support Hub review and explicit owner-approved dedicated cutover.
