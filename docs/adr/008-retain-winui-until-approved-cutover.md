# ADR 008: Retain WinUI until parity approval

Status: DECIDED

## Decision

Keep `PosAdminTool.WinUI` buildable and runnable through parity sign-off. Remove it only in a dedicated Session 14 commit after explicit approval.

## Justification

The migration is a strangler conversion whose UI-modernization value must be demonstrated. Retaining the known tool gives technicians a safe fallback and makes parity failures visible.
