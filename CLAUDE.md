# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

A personal monorepo of configs, scripts and learning projects. `main` is unprotected and considered unstable; the owner rebases and commits directly to it.

- `Applications/GroceryTracker/` — the only real application (.NET 10). Almost all code work happens here.
- `Configurations/` — dotfiles and infra snippets (Nushell, Keycloak compose, Kubernetes/Helm). The `Helm/ShoppingManager` chart is left over from ShoppingManager, which GroceryTracker replaced.
- `Scripts/dotnet_project.sh` — the scaffolding script that generated the ShoppingManager layout (Api/Application/Domain/Infrastructure/UI + xUnit/bUnit test projects). Its paths are hardcoded to `~/Repositories/MyMonoRepo`.
- `Applications/PlayGround/` is gitignored scratch space.

Style comes from the root `.editorconfig`: 2-space indentation. Existing C# uses file-scoped namespaces and primary constructors, even though `.editorconfig` says block-scoped namespaces. Match the code. Comments explain *why* a decision was made, and there are many of them. Keep that density when you edit.

## GroceryTracker commands

Run these from `Applications/GroceryTracker/`. The solution file is `GroceryTracker.slnx`.

```fish
dotnet build
dotnet test                                                    # all test projects
dotnet test tests/Application.Tests                            # one project
dotnet test --filter "FullyQualifiedName~SyncServiceTests"     # one class
dotnet test --filter "FullyQualifiedName~SyncServiceTests.SomeMethod"

# run API + PWA on :5000 (needs a reachable PostgreSQL; migrates and seeds on startup)
set -x ConnectionStrings__DefaultConnection "Host=localhost;Port=5432;Database=grocerytracker;Username=postgres;Password=postgres"
dotnet run --project src/Api
podman compose up -d postgres                                  # quick local Postgres

# frontend with hot reload on :5173 (set ApiBaseUrl in wwwroot/appsettings.Development.json to http://localhost:5000 first)
dotnet run --project src/UI/GroceryTracker.UI.Blazor

# new EF migration (uses GroceryTrackerDbContextFactory, so no DB and no web host needed)
dotnet ef migrations add <Name> --project src/Infrastructure

# full stack in containers on :8080 (always `podman compose`, never bare `docker-compose`)
cp .env.example .env && podman compose up -d --build
```

On Arch/CachyOS the Api project needs `aspnet-targeting-pack`. Without it the build fails with `NETSDK1226`.

Tests use xUnit, with bUnit for the UI. Sync tests run against in-memory **SQLite** on purpose rather than the EF in-memory provider, because the sync engine depends on transactions and raw SQL for stamp allocation (see `tests/Application.Tests/SyncTestContext.cs`).

## GroceryTracker architecture

It's an offline-first Blazor WebAssembly **standalone** PWA with an ASP.NET Core backend and PostgreSQL. It's deployed to a Raspberry Pi (Debian 13) behind nginx, which serves the PWA and proxies `/api` from the same origin. There is no CORS in production (only the dev-server origins in Development) and no authentication. `deploy/` holds the nginx, systemd, TLS and rollout runbooks.

Project dependencies: `Domain` ← `Contracts` ← `Infrastructure` ← `Application` ← `Api`. `MigrationRunner` is a one-shot container that applies migrations. The UI references only `Domain` and `Contracts`.

**Shared analytics.** `Domain/Analytics/SpendAnalyzer` (and `Domain/Balance/BalanceCalculator`) have no EF or HTTP dependencies. The same code runs on the server (`AnalyticsService`) and in the browser against the IndexedDB cache, so offline figures match the server's. Keep Domain free of infrastructure dependencies.

**Sync protocol** (`Contracts/Sync`, `Application/Sync/SyncService`, UI `Services/SyncEngine` + `wwwroot/js/grocerydb.js`):
- Every entity implements `ISyncEntity` (`SyncStamp`, `UpdatedAtUtc`, `IsDeleted`). Rows are never physically deleted; deletes are tombstones.
- `SyncStamp` comes from a single counter row that is incremented inside the write transaction. The row lock serializes writers, so clients can pull `SyncStamp > cursor` without missing commits. Don't replace this with timestamps.
- One `POST /api/sync` pushes the outbox operations and pulls changes. Operations are idempotent by `OperationId`. Conflicts are resolved last-write-wins on the client edit time and reported back. Unappliable operations are `Rejected` rather than failing the batch. A cursor from the future means the server was reset, and nothing is applied.
- Operations are applied in `SyncEntityType` enum order (parents first). Append new members at the end of the enum.
- The UI never waits on the network. It reads and writes IndexedDB and queues changes in the outbox.

**Adding a syncable entity** touches every layer:
1. The Domain model implementing `ISyncEntity`.
2. An EF configuration, the `DbSet`, and a migration.
3. The `SyncEntityType` member, appended at the end.
4. The `SyncPayload` field.
5. The `SyncService` apply/validate/copy and pull code.
6. The UI: the `ENTITY_STORES` entry, a `DB_VERSION` bump in `grocerydb.js`, and `GroceryDataService`/`SyncEngine`.

Settlement was added this way. Its `AddSettlements` migration and IndexedDB version 2 are the reference for this pattern.

**CSV import** (`Domain/Import/GroceryCsvParser`) runs in the browser. It derives trip ids deterministically (`DeterministicGuid`) from date, amount and repeat index, so a re-import is idempotent.

**PWA caching:** nginx must not cache `index.html`, `service-worker.js` or `service-worker-assets.js`. The supplied configs handle this, so preserve it when you edit `deploy/nginx`.

`Applications/GroceryTracker/README.md` and `src/Domain/Model/README.md` (ER diagram) have the detailed behaviour of the dashboard, balance and import features. Update them when you change that behaviour.

## Second brain

`.claude/second-brain/` is a Markdown knowledge base (Obsidian-compatible, PARA layout plus ADR-style `decisions/`). It records why decisions were made, open questions and gotchas. Start at its `index.md` before non-trivial work. When a session settles a design question or turns up a gotcha, update the relevant note and append to `journal/YYYY-MM-DD.md`. Its `README.md` has the conventions.
