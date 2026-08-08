# ADR 013: C# language version for migration work

Status: RECOMMENDED

## Context

The projects use .NET 10 but explicitly set `LangVersion` to `13.0`. The SDK can support C# 14, but this session must not change toolchain or source behaviour.

## Options considered

### Move to C# 14

This would allow current language features, but the migration does not need a language feature to meet its primary UI and safety goals. Moving it adds a toolchain compatibility decision and another variable while package versions and the Agent skeleton are being stabilized.

### Remain on C# 13

Keeping the explicit current version preserves the known compilation baseline and avoids unrelated source churn. .NET 10 APIs remain available where they do not require a C# 14 syntax feature.

## Recommendation

Remain on C# 13 for this migration until a specific, measured implementation benefit requires C# 14. If that occurs, amend this ADR and change the pinned version deliberately in the session that owns deterministic toolchain work; do not make a silent opportunistic switch.

## Consequences

- Session 00 intentionally leaves `Directory.Build.props` unchanged.
- Session 01 records the final toolchain decision alongside SDK and lockfile pinning.
- New code must remain compatible with the retained language version unless the ADR is formally amended.
