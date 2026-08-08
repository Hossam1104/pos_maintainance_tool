# ADR 014: Pivot remaining POS work to RMS+ Support Hub merge preparation

Status: DECIDED

## Context

Sessions 00-08 established useful POS domain, application, infrastructure, contracts, Agent,
Angular, and retained WinUI architecture. The remaining standalone migration plan would continue
duplicating frontend responsibilities that will be owned by RMS+ Support Hub.

## Decision

Keep the POS repository separate temporarily and preserve all useful Sessions 00-08 work. Freeze
standalone Angular expansion after the existing Session 08 implementation. Continue only POS-owned
backend, privileged-operation, security, portability, documentation, and merge-readiness work.
Reclassify the remaining work as POS-M01 through POS-M06 in the canonical preparation plan and
prompt file. Do not merge repositories, remove WinUI, build a final standalone installer, or
perform Angular integration until both repositories have completed their agreed preparation work,
the cross-project review is complete, and the owner explicitly authorizes integration.

## Consequences

- POS remains responsible for domain behavior, application use cases, Windows/SQL/SMB adapters,
  Agent contracts, authorization, operation execution, audit, secrets, and portability.
- RMS+ Support Hub owns the final Angular shell, navigation, shared components, design system,
  branding, themes, and integrated POS route experience.
- Restore, cleanup/reset, and downloader backend safety remain valuable preparation work; their
  standalone Angular screens are deferred.
- WinUI remains buildable and publish-validated as the compatibility/parity baseline until the
  cross-project decision and a dedicated cutover approval.
- Sessions 09-14 in the historical migration runbook are not an active execution queue.
