# ADR 003: Bind the Agent to local loopback only

Status: DECIDED

## Decision

The Agent binds only to `127.0.0.1`; there is no configuration path for a LAN or remote bind.

## Justification

Loopback-only local operation meets the v1 topology while sharply constraining the new HTTP attack surface. LAN mode, pairing, revocation, and role work are explicitly deferred rather than scaffolded.
