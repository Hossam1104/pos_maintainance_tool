# Current Project State

- **Updated:** 2026-08-10
- **Branch:** `main` after the verified POS-M05 landing/collision audit merge
- **Release or milestone:** .NET 10 + Angular 22 migration; Sessions 00-08 and POS-M01 through POS-M05 complete; standalone Angular expansion frozen; Claude Opus 5 R1 required before POS-M06

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

## Verified POS-M05 Landing and Collision Audit

- POS-M05 reviewed POS `main` at `810658467f77b0e2a37aa4a28a66ee3df6519933` and RMS+ Support Hub
  `main` at `954b35698f5778386ad45826589f2a1ed7dff108`; the Support Hub checkout was inspected
  read-only at that commit on `ui/online-orders-opus-redesign` and had pre-existing uncommitted
  changes. No Support Hub source or generated output was modified by POS-M05.
- The actual Support Hub structure is a .NET 10 `Core -> Data -> Api` backend, one backend xUnit
  project, and an Angular 22 frontend whose final shell/shared UI/branding owns the live
  `/tools/pos-maintenance` informational placeholder. It has no POS Agent or equivalent Windows
  Negotiate/local-admin/antiforgery/loopback boundary.
- The canonical preparation plan now records a detailed project/file landing map with exact POS
  and Support Hub package/framework baselines, namespace/route/error compatibility, DI/config/
  security/audit/resource/test/generated/build ownership, concrete collisions, and explicit
  cross-project decisions. The recommended separate Windows POS Agent topology remains undecided.
- POS-M05 is complete as planning/governance only. `TASK.md` now contains POS-M06 as
  `BLOCKED - CLAUDE OPUS 5 REVIEW REQUIRED`; POS-M06 was not executed and RMS+ Support Hub
  integration remains unauthorized.

## Active Programme

- Canonical plan: `docs/POS_SUPPORT_HUB_MERGE_PREPARATION_PLAN.md`.
- Canonical prompts: `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`.
- `TASK.md` contains the complete POS-M06 final-candidate prompt with status
  `BLOCKED - CLAUDE OPUS 5 REVIEW REQUIRED`; POS-M01 through POS-M05 are complete, POS-M05
  requires the Claude Opus 5 R1 review outcome, and POS-M06 is not authorized.
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
- Claude Opus 5 R1 must review the POS-M05 landing/collision map, the separate-Agent versus shared-
  host recommendation, package/route/error/security collisions, and the open HTTP rejection
  semantics question before any POS-M06 authorization.
- WinUI remains retained until cross-project RMS+ Support Hub review and explicit owner-approved dedicated cutover.
