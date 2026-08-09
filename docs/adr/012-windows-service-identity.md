# ADR 012: Windows Service identity

Status: ACCEPTED

## Context

The Agent will replace an elevated interactive WinUI process and must control configured Windows services, reach SQL Server, read/write managed roots, and use SMB for DB Downloader work. The legacy SMB adapter calls `WNetAddConnection2`, which creates the connection in the caller's logon session; the Agent will run in Session 0, not the technician's session.

## Options considered

### LocalSystem

LocalSystem simplifies initial service installation and has broad local privileges, but its outbound network identity is normally the machine account. SQL Server and SMB installations would need to grant that identity access or rely on separately supplied credentials. Its broad local file access weakens least privilege and makes ACL mistakes less visible during testing.

### Dedicated local service account

A dedicated non-interactive local account can be granted only the required rights: Log on as a service, managed-root ACLs, the configured service-control capability, and an explicit SQL Server login/user mapping with the needed database permissions. It is also a stable, auditable principal for local file access.

For SMB, the current `WNetAddConnection2` call still uses the service's Session 0 logon session. The explicit RDB credential passed to the call must therefore be proven against the real SMB server under the installed account; choosing a dedicated account does not itself solve the Session 0 mapping trap.

## Decision

Run the Agent as `LocalSystem`, matching `RMS.BranchService`, `RMS.CashierService`, and
`RMSServiceManager` on the representative device. The Agent uses explicitly configured SQL
authentication rather than the Windows service principal for database login.

On 2026-07-29, the existing `TestConnectionUseCase`/`SqlCmdExecutor` path ran as
`NT AUTHORITY\SYSTEM`; SQL authentication and `SELECT 1` succeeded against the branch database.
Separate read-only SQL-client checks under the interactive identity succeeded against both
configured branch and cashier databases. No credential or connection string is retained in
project memory.

Session 12 must still prove `WNetAddConnection2` SMB behaviour in Session 0 against a
representative non-production device/server. Failure remains a recorded blocker rather than a
reason to bypass the explicit SMB credential flow.

POS-M04 records the automated portability evidence without claiming that representative proof:
fakes cover a connection established by the scope, a compatible pre-existing connection, the
no-credential service-identity path, credential conflict, safe root/path translation, bounded
partial-file cleanup, cancellation, and stable sanitized failure outcomes. No real `WNetAddConnection2`
call was made during POS-M04.

The remaining evidence gate is an isolated, non-production device where the Agent is installed as
`LocalSystem`, a non-production SMB server/share exposes the configured backup root, and the
explicit RDB credential is provisioned. The proof must show, under the installed service in
Session 0, successful `WNetAddConnection2`, directory enumeration, newest-batch discovery, ZIP
read/download, cancellation/timeout cleanup, and scoped disconnect without cancelling an unrelated
pre-existing connection. Any failure blocks the claim that the downloader is portable under the
chosen service identity; it does not authorize a production test or a credential/path-policy
bypass.

## Consequences

- Installer work must register the Agent as `LocalSystem`; it must not create or retain a service-account password.
- LocalSystem's broad authority increases the importance of loopback-only hosting, administrator authorization, server-side allowlists, and strict operation policy.
- SQL credentials remain protected configuration and must never enter browser state, contracts, logs, or source.
- Session 12 must test outbound SMB identity and explicit RDB credentials under the actual installed identity.
- Managed-root access must still be verified on the representative device even though LocalSystem normally has broad local access.
