# Current Project State

- **Updated:** 2026-08-11
- **Branch:** `main` after the verified POS-M06 final-audit merge and owner-authorized INT-03R provenance correction
- **Release or milestone:** .NET 10 + Angular 22 migration; POS-M01 through POS-M06 complete; the Agent source snapshot is self-consistent with `ArtifactCatalog.cs` tracked; POS remains a merge-ready candidate; standalone Angular expansion frozen; Claude Opus 5 R2 review required before integration

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
- The canonical preparation plan records the detailed project/file landing map, exact package and
  framework baselines, ownership/collision analysis, and explicit cross-project decisions; the
  recommended separate Windows POS Agent topology remains undecided.
- POS-M05 was planning-only; POS-M06 completed the final candidate audit with RMS+ Support Hub
  integration still unauthorized.

## Verified POS-M05R R1 corrective closure

- Post-dispatch HTTP response truth is conservative: without an authoritative remote RMS response
  contract, every non-success response from `BackupApiClient` maps to terminal `OutcomeUnknown`
  with `downloader.trigger_outcome_unknown`; 2xx remains `Accepted`, and pre-dispatch validation,
  cancellation, endpoint-policy/SSRF rejection, and connection rejection before HTTP bytes remain
  `NotAttempted`. Unknown does not retry, enter SMB discovery, or publish artifacts and carries
  safe guidance to check remote state before retrying.
- Focused Infrastructure coverage includes 400, 401, 403, 408, 409, 422, 429, 500, 502, 503, and
  504, positive 2xx, pre-dispatch SSRF, cancellation, and transport truth. A Worker-path test
  drives the real `BackupApiClient` through a disposable HTTP response handler and proves unknown
  REST/audit state, no SMB discovery/artifact, and response/body/URL/IP/socket/exception redaction.
- Agent runtime OpenAPI is Development-only while build-time `Microsoft.Extensions.ApiDescription.Server`
  generation remains enabled; health endpoints stay fixed status-only loopback probes. Focused Agent
  tests cover Development availability and Production non-exposure.
- The measured snapshot contained 405 tracked `.artifacts/` entries totaling 164,321,637 bytes
  (156.71 MiB); those files and tracked `host_trace.txt`/`trace.txt` were removed without history
  rewriting. Root ignore rules now prevent recurrence; clean source snapshot/import is preferred and
  raw history merge is prohibited.
- POS-M06 re-resolved the read-only Support Hub `main` at `2a4a38aba2113f30c5751eb7b1fbf8a6cb13a91b`;
  its checkout was clean and synchronized. The readiness document remains `READY WITH FINDINGS`,
  the `/tools/pos-maintenance` page is informational with no POS backend, separate Windows POS
  Agent remains recommended, and final proxy/origin/deployment topology remains a cross-project decision.

## Active Programme

- Canonical plan: `docs/POS_SUPPORT_HUB_MERGE_PREPARATION_PLAN.md`.
- Canonical prompts: `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`.
- `TASK.md` stages `CLAUDE OPUS 5 R2 FINAL PRE-INTEGRATION REVIEW` with `REVIEW REQUIRED / NO
  EXECUTION AUTHORIZED`; POS-M06 is complete and its audit evidence is recorded in the canonical plan.
- The POS-M06 session branch was merged to POS `main`; cross-project merge, Angular integration,
  standalone installer cutover, and WinUI removal remain unauthorized.
- INT-03R narrowed the root generated-artifact ignore rule and tracked the existing Agent
  `Artifacts/ArtifactCatalog.cs`; no Agent runtime or other POS implementation was changed.

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
- Claude Opus 5 R2 must review the completed POS-M06 candidate and all open evidence/cross-project
  decisions before any integration planning or execution.
- WinUI remains retained until cross-project RMS+ Support Hub review and explicit owner-approved dedicated cutover.
