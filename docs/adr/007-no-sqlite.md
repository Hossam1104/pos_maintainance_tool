# ADR 007: No SQLite job store

Status: DECIDED

## Decision

Use an in-memory bounded Agent job registry and append-only JSONL audit records for destructive actions only. Do not add SQLite, schema, or migrations.

## Justification

The current application loses jobs and logs when it exits. Agent memory survives browser refresh, which is the relevant new failure mode, without introducing durable-state complexity; destructive audit records remain necessary.
