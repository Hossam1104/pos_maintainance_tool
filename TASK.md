# Current Task

- **Task ID:** POS-M06
- **Status:** BLOCKED — CLAUDE OPUS 5 REVIEW REQUIRED
- **Role:** Implement
- **Source:** `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`
- **Authorization:** Not granted. POS-M06 must not begin until Claude Opus 5 R1 review has completed and the owner explicitly authorizes continuation.

## Authorized Session Prompt

## POS-M06 - Final Merge-Ready Candidate Audit

```text
Role:
Perform one owner-authorized, review-gated final POS preparation audit. Do not merge repositories
or integrate Angular.

Entry conditions (all required):
- POS-M01 through POS-M05 are complete.
- Claude Opus 5 R1 review has completed and its findings are addressed or explicitly accepted.
- The owner explicitly authorizes POS-M06 continuation.
- The workspace is clean and synchronized according to AGENTS.md.

Verify, with evidence:
1. Domain/Application portability and absence of privileged host leakage.
2. Infrastructure isolation for SQL, SCM, SMB, filesystem, configuration, and secrets.
3. Agent loopback binding, Negotiate/local-admin authorization, antiforgery, redaction, correlation,
   safe Problem Details, operation contracts, and audit behavior.
4. Bounded runtime state: operation entries, idempotency, events, activity, artifacts, cancellation,
   and resource cleanup.
5. Restore backend archive safety, preview/challenge/execute-time policy, locks, cancellation, and
   post-restore verification.
6. Cleanup/reset path policy, protected roots, previews, challenge/recomputation, locks, audit,
   and partial failure semantics.
7. DB Downloader backend, SSRF/SMB policy, credential isolation, cancellation, artifacts, and the
   exact service-identity evidence gate.
8. Configuration ownership, secret handling, operation messages, and no browser path/secret leak.
9. Stable versioned contracts and generated-file hygiene.
10. Repository cleanliness, namespace/dependency/DI/config/logging/test/resource ownership, landing
    map, collision analysis, and no duplicate standalone Angular plan.
11. Retained WinUI presence and required publish/buildability evidence.
12. Documentation consistency and Git cleanliness.

Required output if every gate passes:

  POS PREPARATION:
  COMPLETE

  POS STATUS:
  MERGE-READY CANDIDATE

  STANDALONE ANGULAR EXPANSION:
  FROZEN

  WINUI:
  RETAINED UNTIL CROSS-PROJECT DECISION

  REPOSITORY MERGE:
  NOT AUTHORIZED

  NEXT:
  WAIT FOR RMS+ SUPPORT HUB SESSION 08 AND CROSS-PROJECT REVIEW

Required validation:
Run the targeted checks for every changed area, the full agreed .NET/Angular gates where the
environment supports them, `git diff --check`, memory checks, and the retained WinUI publish gate.
Report actual results and distinguish unavailable representative-device evidence from passing fake
tests.

Stop:
If any safety, security, portability, build, documentation, or collision gate fails, do not call
the repository merge-ready. Record the blocker, update TASK.md/HANDOFF.md, and stop. Even a
successful POS-M06 audit does not authorize a repository merge or Angular integration.
```
