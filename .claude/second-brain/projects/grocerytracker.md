---
tags: [project, dotnet, blazor, pwa]
created: 2026-09-25
updated: 2026-09-25
status: active
---
# GroceryTracker

Tracks and analyses household grocery spending, and balances who owes whom. It's the only real
application in the repo. Code: `Applications/GroceryTracker/`. Behaviour docs:
`Applications/GroceryTracker/README.md`. Commands and architecture: root `CLAUDE.md`.

## Shape in one breath
Blazor WASM standalone PWA → IndexedDB + outbox → `POST /api/sync` → ASP.NET Core (`SyncService`)
→ PostgreSQL. It's served by nginx on the [[raspberry-pi-homelab]] from a single origin, with no
auth and no CORS in production.

## Key ideas
- [[offline-first-sync]]: the UI never waits on the network.
- [[0001-syncstamp-counter]]: why pulls can't miss commits.
- [[0003-shared-domain-analytics]]: offline figures match the server's.
- CSV import derives trip ids deterministically, so re-importing is idempotent.

## Features
- Dashboard: this month, 3, 6 and 12 month ranges, year-to-date and per-month tiles.
- Trips with optional line items split by category.
- Balance between people, with weighted shares (migration `AddMemberShareWeight`), fewest-transfer
  settlement suggestions, and tracked settlements.
- CSV import from a spreadsheet export.

## Schema history
| Migration | Date | What |
|---|---|---|
| `InitialCreate` | 2026-09-18 | Households, members, stores, categories, trips, items, sync counter |
| `AddSettlements` | 2026-09-18 | Settlements. The reference for adding a syncable entity (IndexedDB v2) |
| `AddMemberShareWeight` | 2026-09-18 | Weighted balance shares |

## Open questions / ideas
- [ ] Authentication, if the app is ever exposed beyond the LAN
- [ ] Could the Keycloak compose snippet in `Configurations/` serve that? See [[kubernetes-learning]]
- [ ] Backup automation for the Pi's PostgreSQL. Migrations are forward-only, so the dump *is* the rollback plan

## Log
- 2026-09-25: Second brain created. See [[2026-09-25]].
