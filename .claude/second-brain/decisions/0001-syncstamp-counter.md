---
tags: [decision, sync]
created: 2026-09-25
updated: 2026-09-25
status: active
---
# 0001: Sync cursors use a locked counter row, not timestamps

**Context.** Clients pull "everything changed since my cursor". With wall-clock timestamps, a
transaction that started earlier but committed later can get a timestamp *below* a cursor a client
already holds, and that write is silently skipped.

**Decision.** `SyncStamp` is allocated by incrementing a single counter row
(`Infrastructure/Data/SyncCounter.cs`) inside the write transaction. The row lock serializes
writers, so stamps follow commit order.

**Consequences.**
- Pulls with `SyncStamp > cursor` can't miss a commit.
- Writers are serialized, which is fine at household scale.
- Tests need a real relational DB. See [[0002-sqlite-for-sync-tests]].
- Don't replace this with timestamps.

Related: [[offline-first-sync]]
