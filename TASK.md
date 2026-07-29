# Current Task

- **Task ID:** MIGRATION-SESSION-05
- **Status:** Ready
- **Owner:** Unassigned
- **Role:** Implement

## Objective

Implement the Angular 22 "Branch Signal Desk" design system and responsive application shell, without feature business logic, so the Session 05 release-gate checks pass.

## Done When

- Locally bundled licensed fonts and one outline icon set produce no runtime request outside the Agent origin.
- Semantic light/dark tokens, required primitives, responsive navigation, device context, activity rail, unreachable/error states, and the branch signal path match the Session 05 design authority.
- The signal path covers loading, ready, degraded, unreachable, stale, and unknown states with keyboard access, evidence, timestamps, non-colour semantics, and diagnostic routing.
- Routes/placeholders exist for Overview, Device, Services, Backups, Restore, Maintenance, Downloads, Activity, and Settings; a component gallery is Development-only.
- Shell and dialog tests cover navigation/status semantics, keyboard/focus behavior, reduced motion, WCAG 2.2 AA contrast, and light/dark accessibility.
- `npm --prefix src/PosAdminTool.Web run lint`, `npm --prefix src/PosAdminTool.Web run test -- --run`, `npm --prefix src/PosAdminTool.Web run build`, the external-asset audit, and `npm --prefix src/PosAdminTool.Web run e2e` pass.
- The required design self-critique and real validation results are recorded in `docs/migration/SESSION_LOG.md`.

## Scope

### Read First

- `docs/NET10_ANGULAR22_SESSION_PROMPTS.md`, "Session 05 — Angular design system and application shell"
- `docs/NET10_ANGULAR22_MIGRATION_PLAN.md`, sections 7, 8, 9 (Session 05), 10.2-10.3, and 11
- `docs/migration/UI_PARITY_MAP.md`
- `src/PosAdminTool.Web/package.json`
- `src/PosAdminTool.Web/angular.json`
- `src/PosAdminTool.Web/src/app/app.*`
- `src/PosAdminTool.Web/src/styles.scss`
- `src/PosAdminTool.Web/playwright.config.ts`
- `.ai/DECISIONS.md`, ADR-0002, ADR-0003, ADR-0005, ADR-0008, ADR-0009, and ADR-0010

### May Change

- `src/PosAdminTool.Web/`
- `src/PosAdminTool.Web/package.json` and `package-lock.json`
- `docs/migration/SESSION_LOG.md`
- Task-related shared-memory files required by `AGENTS.md`

### Out of Scope

- Agent, Contracts, Domain, Application, Infrastructure, and WinUI behavior
- Session 06 feature business logic or live device/configuration data
- Responsive visual snapshot baselines, which are deferred to Session 13
- PWA/offline storage, LAN/public access, cloud services, SignalR, SQLite, and a global state framework
- WinUI removal, installer work, deployment, commit, or push

## Constraints

- Windows 10/11 x64 and the pinned .NET 10, C# 13, Node 24.18.0, npm 12.0.1, Angular 22.0.8, and TypeScript 6.0.3 baseline remain unchanged unless a required package is added at an exact version.
- Assets must be local and redistributable with required notices; no CDN, Google Fonts request, emoji iconography, analytics, or internet-hosted asset.
- Use Angular standalone components, route-level lazy loading, signals for local state, RxJS for streams, and Angular CDK accessibility/overlay primitives with custom DBS styling.
- Support light/dark themes, reduced motion, keyboard-only use, 200% zoom, and widths down to 360 px; status cannot rely on colour alone.
- Keep secrets out of browser storage and contracts. Do not queue mutations while the Agent is unreachable or API-incompatible.

## Plan

1. Add exact UI/testing dependencies and locally licensed font/icon assets with notices.
2. Implement semantic tokens, typography, spacing, shape, focus, elevation, motion, and theme behavior.
3. Build the shared accessible UI primitives and development-only gallery.
4. Build responsive desktop, tablet, and phone shell navigation plus device/activity/error surfaces.
5. Build the accessible branch signal path and realistic typed fixtures for every required state.
6. Add lazy routes and feature placeholders without feature business logic.
7. Add unit, keyboard, accessibility, contrast, reduced-motion, and Playwright coverage.
8. Run all Session 05 checks, perform the required self-critique, and record evidence.

## Current Checkpoint

- **Baseline commit:** `1d230bf0e0af55b96d1ce1c4f39992d47a05b6a3`
- **Starting condition:** Sessions 00-04 are committed; the Angular workspace still has the generated starter page, no routes, scaffold-level tests, and a placeholder Playwright check.
- **Required next action:** Start Session 05 at plan step 1; do not implement Session 06 data flows.
