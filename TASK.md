# Current Task

- **Task ID:** POS-M04
- **Status:** PENDING OWNER AUTHORIZATION
- **Role:** Implement
- **Source:** `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`

## Authorized Session Prompt

```text
Role:
Implement one owner-authorized backend/portability session. No standalone Angular Downloader UI.

Objective:
Extract the reusable backend portion of historical Session 12, preserve current downloader
behavior, and make the capability safe under the Agent/service-identity boundary.

Entry conditions:
- POS-M01 is complete; POS-M03 is complete if shared operation, artifact, or resource-lock code is
  changed.
- Read DbDownloadService, BackupApiClient, SMB repository/scope/path resolver, downloader settings,
  Agent operation/artifact contracts, secret store, and ADR-012.

Required backend capability:
1. Model downloader work as an Agent operation with operation ID, per-branch progress, state truth,
   idempotency, cancellation, timeout, resource locks, sanitized messages, and audit where required.
2. Preserve backup trigger behavior, newest-created-folder discovery, exact branch ZIP matching,
   stable-size observation, independent per-branch progress, timeouts, and partial outcomes.
3. Enforce SSRF defenses for the trigger endpoint: safe schemes, approved target policy, no loopback/
   metadata/private-network bypass beyond the explicit local policy, bounded requests, and no
   production calls in tests.
4. Validate SMB/UNC target policy, canonical roots, safe filenames, share behavior, cancellation,
   connection cleanup, and credential isolation. Never send RDB credentials to the browser.
5. Use principal-scoped opaque artifact IDs and safe streamed download behavior. Do not return raw
   UNC paths, connection strings, or server credentials.
6. Validate service identity behavior and document the exact evidence gate for LocalSystem/Session 0
   SMB behavior. If a representative-device proof cannot safely be obtained, record the exact gate;
   do not infer success.

Required tests:
- Newest-created-folder selection and exact branch ZIP matching are preserved.
- Stable-size observation, per-branch progress, timeout, cancellation, partial completion, retry,
  and failure semantics are deterministic under fake clocks/adapters.
- Unsafe URL schemes, host forms, redirects, private/metadata targets, malformed branches, unsafe
  SMB roots, and path traversal fail closed.
- Credentials never appear in API responses, logs, audit, operation messages, or artifacts.
- Artifact IDs are opaque and principal-scoped; streamed downloads are cancellable and safe.
- SMB connection scope is disposed on success, failure, cancellation, and timeout.
- Service identity validation is tested with fakes and its representative-device gate is documented.

Verification:
  dotnet build PosAdminTool.sln -c Release --no-restore
  dotnet test PosAdminTool.sln -c Release --no-restore
  git diff --check

No Angular work:
Do not build the final standalone Downloader feature. Preserve backend behavior and acceptance
criteria for Support Hub integration.

Stop:
Do not call real Production endpoints, real SMB shares, or a real service identity. Do not execute
POS-M05 automatically.
```
