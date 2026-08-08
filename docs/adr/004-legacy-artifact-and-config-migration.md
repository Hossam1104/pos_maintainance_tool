# ADR 004: Preserve backup ZIP compatibility; import non-secret config only

Status: DECIDED

## Decision

Existing backup ZIPs remain readable. Legacy `config.json` imports only non-secret settings; SQL and RDB passwords are re-entered once.

## Justification

The legacy encryption derives key material from the interactive identity, which a Windows Service cannot safely reproduce. Re-entry of two secrets eliminates an unsafe compatibility path while retaining the useful operational configuration and archive format.
