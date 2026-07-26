# ADR 005: Ship English first, remain RTL-ready structurally

Status: DECIDED

## Decision

Ship English in v1. Use CSS logical properties and extract user-facing strings, but do not build a localization framework in v1.

## Justification

This preserves a low-cost route to Arabic/RTL later without delaying the UI modernization goal with an unneeded localization subsystem.
