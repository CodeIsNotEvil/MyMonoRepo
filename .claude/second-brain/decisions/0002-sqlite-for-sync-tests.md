---
tags: [decision, testing]
created: 2026-09-25
updated: 2026-09-25
status: active
---
# 0002: Sync tests run on in-memory SQLite

**Context.** The sync engine relies on transactions and raw SQL for stamp allocation
([[0001-syncstamp-counter]]). The EF in-memory provider supports neither.

**Decision.** `tests/Application.Tests/SyncTestContext.cs` uses in-memory SQLite.

**Consequences.** Tests exercise real transactional behaviour. Dialect differences from PostgreSQL
are possible, so keep raw SQL portable or cover it in `Infrastructure.Tests`.
