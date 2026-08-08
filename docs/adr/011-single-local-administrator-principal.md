# ADR 011: Authorize one local-administrator principal

Status: DECIDED

## Decision

Authorize a single principal: a member of the local Administrators group. Do not build a role matrix.

## Justification

The product supports one local technician on one device. Viewer/operator/administrator roles and pairing are LAN-era complexity, not required security for local administrative maintenance.
