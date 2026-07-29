# Technical Decisions

## Decision Index

| ID | Decision | Status | Evidence |
|---|---|---|---|
| DEC-001 | Windows x64, per-device, loopback-only topology | Accepted | ADRs 001-003; Agent binding and project TFMs |
| DEC-002 | Preserve backup archives; import non-secret legacy configuration only | Accepted | ADR 004 |
| DEC-003 | English-first, structurally RTL-ready UI | Accepted | ADR 005 |
| DEC-004 | Administrator-approved installer creates the Agent Windows Service | Accepted | ADR 006 |
| DEC-005 | In-memory jobs, JSONL destructive audit, REST/SSE, and no browser offline store | Accepted | ADRs 007, 009, 010 |
| DEC-006 | Retain WinUI until explicit parity approval | Accepted | ADR 008 |
| DEC-007 | Authorize one local-administrator principal | Accepted | ADR 011; Agent authorization implementation |
| DEC-008 | Use a dedicated local service account | Proposed | ADR 012 recommendation; representative-device proof pending |
| DEC-009 | Retain C# 13 during migration | Proposed | ADR 013 recommendation; current build configuration follows it |

## DEC-001 — Windows x64, Per-Device, Loopback-Only Topology

- Status: Accepted
- Evidence: `docs/adr/001-platform-win-x64-only.md` through `003-loopback-only.md`
- Affected areas: Agent hosting, deployment, security, testing

### Context
The tool administers one Windows POS device and does not require central or remote management.

### Decision
Support Windows 10/11 x64 only, with no cloud/central server, and bind the Agent only to `127.0.0.1`.

### Rationale
The retained app is x64 and local operation meets v1 needs while minimizing HTTP exposure and platform scope.

### Consequences
LAN, public exposure, pairing, remote roles, and `win-arm64` are out of scope; loopback binding remains a regression gate.

### Files or Modules
`src/PosAdminTool.Agent/LoopbackBinding.cs`, Agent/WinUI project files, ADRs 001-003.

### Follow-up
Keep a runtime check proving no non-loopback listener exists.

## DEC-002 — Legacy Artifact and Configuration Compatibility

- Status: Accepted
- Evidence: `docs/adr/004-legacy-artifact-and-config-migration.md`
- Affected areas: Restore, configuration migration, secret handling

### Context
Legacy archives are operationally valuable, but legacy ciphertext is tied to the interactive identity.

### Decision
Keep existing backup ZIPs readable; import only non-secret legacy settings and require both secrets to be re-entered.

### Rationale
This preserves useful compatibility without reproducing unsafe identity-dependent secret handling.

### Consequences
Archive compatibility must be tested; Session 03 must leave the legacy file unchanged and never import its secrets.

### Files or Modules
Application restore/configuration services, Agent configuration, ADR 004.

### Follow-up
Implement and verify in Sessions 03 and 09.

## DEC-003 — English-First, RTL-Ready Structure

- Status: Accepted
- Evidence: `docs/adr/005-english-first-localization-ready.md`
- Affected areas: Angular UI, content, layout

### Context
Arabic/RTL may be needed later, but a localization subsystem is not a v1 requirement.

### Decision
Ship English first, use CSS logical properties, and extract user-facing strings without building full localization in v1.

### Rationale
It preserves a low-cost RTL path without delaying UI modernization.

### Consequences
New UI must avoid direction-dependent layout assumptions; full localization remains deferred.

### Files or Modules
`src/PosAdminTool.Web`, ADR 005.

### Follow-up
Enforce during shell and feature-screen sessions.

## DEC-004 — Installer-Created Windows Service

- Status: Accepted
- Evidence: `docs/adr/006-installer-creates-admin-approved-service.md`
- Affected areas: Agent hosting, installer, permissions, deployment

### Context
The replacement Agent will own privileged local service, SQL, SMB, and file operations.

### Decision
An offline installer will create the Agent Windows Service and require administrator approval.

### Rationale
Installation must establish the privilege boundary explicitly instead of relying on browser elevation.

### Consequences
Installer, offline upgrade, rollback, service configuration, and sanitized failure handling are required before cutover.

### Files or Modules
Agent and future installer; ADR 006.

### Follow-up
Implement and validate in Session 14.

## DEC-005 — Volatile Jobs, JSONL Audit, REST/SSE, No Offline Browser Store

- Status: Accepted
- Evidence: `docs/adr/007-no-sqlite.md`, `009-sse-not-signalr.md`, `010-no-pwa-offline-store.md`
- Affected areas: Operations, progress, audit, Angular state

### Context
Browser refresh must not cancel Agent work, but durable job recovery and offline browser operation are not required.

### Decision
Use a bounded in-memory Agent job registry, append-only JSONL audit records for destructive actions, REST for state, and SSE for progress; do not add SQLite, SignalR, PWA, service worker, or IndexedDB.

### Rationale
This covers browser reconnect and audit needs without duplicate state mechanisms or durable-store complexity.

### Consequences
Agent restart loses in-flight jobs; destructive audit survives; the browser never retries a mutation merely to recover state.

### Files or Modules
Future Agent operation engine/activity API and Angular stores; ADRs 007, 009, 010.

### Follow-up
Implement and test in Session 04.

## DEC-006 — Retain WinUI Until Approved Cutover

- Status: Accepted
- Evidence: `docs/adr/008-retain-winui-until-approved-cutover.md`
- Affected areas: Solution structure, CI, migration sequencing

### Context
The Angular replacement must prove operational and usability parity before replacing the known tool.

### Decision
Keep WinUI buildable and runnable until explicit parity sign-off; remove it only in a dedicated Session 14 commit.

### Rationale
The retained app provides a fallback and makes parity regressions visible.

### Consequences
CI must keep publishing WinUI; migration work must avoid breaking it.

### Files or Modules
`src/PosAdminTool.WinUI`, solution, CI, parity documents, ADR 008.

### Follow-up
Require explicit approval at cutover.

## DEC-007 — Single Local-Administrator Principal

- Status: Accepted
- Evidence: `docs/adr/011-single-local-administrator-principal.md`; Agent authorization source/tests
- Affected areas: Authentication, authorization, session API

### Context
The v1 product supports one local technician on one device.

### Decision
Use Windows Negotiate authentication and authorize members of the local Administrators group; do not build a role matrix.

### Rationale
Viewer/operator roles and pairing are remote-management complexity outside the local tool.

### Consequences
Protected operations require the administrator policy; authenticated non-admins receive explanatory session state.

### Files or Modules
`src/PosAdminTool.Agent/Authorization`, `src/PosAdminTool.Agent/Endpoints/SessionEndpoints.cs`, ADR 011.

### Follow-up
Verify a real browser/SSPI round trip on a representative device.

## DEC-008 — Dedicated Local Service Account

- Status: Proposed
- Evidence: `docs/adr/012-windows-service-identity.md`
- Affected areas: Installer, SQL, SMB, service control, filesystem ACLs

### Context
The Agent must perform privileged local and network work from Windows Session 0.

### Decision
Recommended: create a dedicated non-interactive local account with only required rights instead of using LocalSystem.

### Rationale
It gives a stable, auditable, least-privilege identity; the rationale is documented but not yet proven in the target environment.

### Consequences
The installer must provision account rights/ACLs, and SQL plus explicit-credential SMB behavior must be proven under that identity.

### Files or Modules
Future installer and Agent service configuration; ADR 012; Infrastructure SQL/SMB adapters.

### Follow-up
Accept only after SQL/managed-root proof in Session 06 and SMB Session-0 proof in Session 12.

## DEC-009 — Retain C# 13 During Migration

- Status: Proposed
- Evidence: `docs/adr/013-csharp-14-versus-13.md`; `Directory.Build.props`
- Affected areas: All .NET projects

### Context
.NET 10 supports C# 14, but the migration has no measured need for it.

### Decision
Recommended and currently implemented: keep `LangVersion` at `13.0` unless a specific benefit justifies a formal change.

### Rationale
It preserves the known compilation baseline and avoids unrelated language/toolchain churn.

### Consequences
New source must remain C# 13-compatible; a future change requires amending the ADR deliberately.

### Files or Modules
`Directory.Build.props`, all C# projects, ADR 013.

### Follow-up
None unless a measured C# 14 requirement emerges.
