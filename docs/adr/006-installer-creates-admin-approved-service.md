# ADR 006: Installer creates an administrator-approved Windows Service

Status: DECIDED

## Decision

The offline installer creates the Agent Windows Service and requires administrator approval.

## Justification

The Agent owns privileged local service, SQL, SMB, and managed-file operations. Installation must establish this boundary explicitly rather than rely on browser or interactive-process elevation.
