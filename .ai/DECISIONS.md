# Decision Index

Read only when the current task touches an affected area.
Detailed rationale remains in the canonical `docs/adr/` records; do not duplicate it under `.ai/`.

| ID | Status | Decision | Affected Area | Detail |
|---|---|---|---|---|
| ADR-0001 | Accepted | Support Windows 10/11 x64 only | Targeting, packaging, test hosts | `docs/adr/001-platform-win-x64-only.md` |
| ADR-0002 | Accepted | Keep the product per-device with no cloud, central server, or public exposure | Product topology | `docs/adr/002-no-cloud-or-public-exposure.md` |
| ADR-0003 | Accepted | Bind the Agent to loopback only with no LAN configuration | Agent hosting and security | `docs/adr/003-loopback-only.md` |
| ADR-0004 | Accepted | Preserve legacy backup ZIPs; import only non-secret legacy configuration | Restore, configuration, secrets | `docs/adr/004-legacy-artifact-and-config-migration.md` |
| ADR-0005 | Accepted | Ship English first while keeping UI structure RTL-ready | Angular content and layout | `docs/adr/005-english-first-localization-ready.md` |
| ADR-0006 | Accepted | An administrator-approved offline installer creates the Agent Windows Service | Deployment and privilege | `docs/adr/006-installer-creates-admin-approved-service.md` |
| ADR-0007 | Accepted | Use in-memory jobs and destructive JSONL audit; do not add SQLite | Operations and audit | `docs/adr/007-no-sqlite.md` |
| ADR-0008 | Accepted | Retain WinUI until explicit parity approval and dedicated cutover | Migration and CI | `docs/adr/008-retain-winui-until-approved-cutover.md` |
| ADR-0009 | Accepted | Use REST plus SSE for progress; do not add SignalR | Agent/Web realtime | `docs/adr/009-sse-not-signalr.md` |
| ADR-0010 | Accepted | Do not add PWA, service-worker, or IndexedDB storage | Browser runtime | `docs/adr/010-no-pwa-offline-store.md` |
| ADR-0011 | Accepted | Authorize one Windows local-administrator principal; no role matrix | Authentication/authorization | `docs/adr/011-single-local-administrator-principal.md` |
| ADR-0012 | Accepted | Run the Agent as LocalSystem; SQL identity proof passed, with managed-root and SMB proof still required | Installer and external access | `docs/adr/012-windows-service-identity.md` |
| ADR-0013 | Proposed | Retain C# 13 until a measured benefit justifies a deliberate change | All .NET projects | `docs/adr/013-csharp-14-versus-13.md` |
| ADR-0014 | Accepted | Freeze standalone Angular expansion and prepare POS for an owner-approved RMS+ Support Hub integration | Programme scope, merge boundaries, WinUI retention | `docs/adr/014-support-hub-merge-preparation.md` |
