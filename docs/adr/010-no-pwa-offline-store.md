# ADR 010: No PWA, service worker, or IndexedDB

Status: DECIDED

## Decision

Do not add a PWA, service worker, or IndexedDB offline store.

## Justification

The loopback Agent is the operational dependency. A cached browser shell cannot safely perform host work without it, so offline caching adds complexity without useful v1 behaviour.
