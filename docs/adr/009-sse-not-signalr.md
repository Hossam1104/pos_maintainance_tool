# ADR 009: Use SSE for progress, not SignalR

Status: DECIDED

## Decision

Use Server-Sent Events for progress and REST for initial/read-only state. Do not add SignalR.

## Justification

`EventSource` supplies reconnect behaviour for the one-way progress path while REST remains the single state contract. SignalR would duplicate update mechanisms and their testing burden.
