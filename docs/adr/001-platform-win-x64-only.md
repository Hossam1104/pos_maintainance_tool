# ADR 001: Windows x64 is the only agent platform

Status: DECIDED

## Decision

Support Windows 10/11 x64 only. Do not build or package `win-arm64` in v1.

## Justification

The current WinUI app already targets x64, and the RMS estate is expected to match it. A second architecture adds installer, adapter, and real-device verification work without serving the UI-modernization driver.
