# ADR 012: Windows Service identity

Status: RECOMMENDED — requires representative-device proof before implementation is accepted

## Context

The Agent will replace an elevated interactive WinUI process and must control configured Windows services, reach SQL Server, read/write managed roots, and use SMB for DB Downloader work. The legacy SMB adapter calls `WNetAddConnection2`, which creates the connection in the caller's logon session; the Agent will run in Session 0, not the technician's session.

## Options considered

### LocalSystem

LocalSystem simplifies initial service installation and has broad local privileges, but its outbound network identity is normally the machine account. SQL Server and SMB installations would need to grant that identity access or rely on separately supplied credentials. Its broad local file access weakens least privilege and makes ACL mistakes less visible during testing.

### Dedicated local service account

A dedicated non-interactive local account can be granted only the required rights: Log on as a service, managed-root ACLs, the configured service-control capability, and an explicit SQL Server login/user mapping with the needed database permissions. It is also a stable, auditable principal for local file access.

For SMB, the current `WNetAddConnection2` call still uses the service's Session 0 logon session. The explicit RDB credential passed to the call must therefore be proven against the real SMB server under the installed account; choosing a dedicated account does not itself solve the Session 0 mapping trap.

## Recommendation

Use a dedicated local service account, created and ACLed by the installer. Grant Log on as a service, only the managed-root access required by the Agent, and an explicit SQL Server login mapping. Store no service-account password in the browser, contracts, logs, or source.

This is a recommendation, not completed evidence. Session 06 must prove SQL login and managed-root access under that account. Session 12 must prove `WNetAddConnection2` SMB behaviour in Session 0 against a representative non-production device/server; failure is a recorded blocker, not a reason to silently fall back to LocalSystem.

## Consequences

- Installer work must create the account, assign Log on as a service, provision ACLs, and report only sanitized failure information.
- SQL deployment guidance must identify the service account and least required database permissions.
- Session 12 must test outbound SMB identity and explicit RDB credentials under the actual installed identity.
- The policy avoids LocalSystem's broad local authority but adds installer and support provisioning work.
