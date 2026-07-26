# Migration session log

## Session 00 — Baseline, parity matrix, screen map, and ADRs

Date: 2026-07-26
Scope: Documentation and ADRs only; no runtime, dependency, target-framework, project, or WinUI file change.

### Decisions and changes

- Re-measured the source baseline: 61 C# files, 4,198 C# lines, and 11 WinUI XAML artifacts.
- Re-verified all 15 findings from plan section 3.4 and corrected citations in `CURRENT_STATE.md`.
- Added a command-level parity matrix, all-XAML UI map, and risk register.
- Recorded the 11 already-decided plan decisions and two open-decision recommendations as ADRs `001`–`013`.
- Recommended a dedicated local service account (pending real-device SQL/SMB proof) and retaining C# 13 during this migration unless a measured Session 01 need justifies an ADR amendment.

### Verification

`git status --porcelain` before edits showed pre-existing untracked `.cc-history/`, `docs/`, and `excute_prompt.md`; these were preserved.

`dotnet build PosAdminTool.sln -c Release` output:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:42.92
```

`dotnet test PosAdminTool.sln -c Release` output:

```text
Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 565 ms - PosAdminTool.Domain.Tests.dll (net10.0)

Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5, Duration: 1 s - PosAdminTool.Infrastructure.Tests.dll (net10.0)

Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 9 s - PosAdminTool.Application.Tests.dll (net10.0)
```

The runner executed 17 cases (4 + 5 + 8), while the plan's 14 count is the 14 declared test methods; `CURRENT_STATE.md` inventories both accurately.

### Risks and prerequisites for Session 01

- The highest-risk identity decision is documented but not proven: Session 06 must validate SQL login and managed-root ACLs, and Session 12 must validate SMB under the installed service identity.
- Wildcard packages and the C# 13 choice are baseline findings for Session 01; do not change them in this documentation session.
- WinUI was not published: the shared preamble prohibits publish without the assigned session explicitly requesting it and the user authorizing it. Session 00 only requires build and test.

### Next session

Session 01 is unblocked for deterministic toolchain/skeleton work after review of this baseline and the Session 00 commit.
