---
tags: [resource, architecture, sync]
created: 2026-09-25
updated: 2026-09-25
status: active
---
# Offline-first sync pattern

A reusable recipe, as implemented in [[grocerytracker]].

1. **The client owns a local store** (IndexedDB), and every write also appends to an **outbox**.
2. **Rows are never physically deleted.** Tombstones (`IsDeleted`) let offline devices learn about deletes.
3. **A monotonic server stamp** is allocated inside the write transaction from one locked counter row.
   Clients pull `stamp > cursor`. See [[0001-syncstamp-counter]].
4. **One round trip** pushes the outbox and pulls changes.
5. **Idempotency by operation id**, so a lost response can be replayed safely.
6. **Last-write-wins on the client edit time**, and conflicts are *reported*, not hidden.
7. **Reject, don't fail**: one unappliable operation must not poison the outbox.
8. **Apply parents before children** (enum order), so a parent and child created offline sync together.
9. **A cursor from the future means the server was reset.** Apply nothing and let the user decide.

Trade-offs: a single counter row serializes writers. That's fine for one household, and it would be
the bottleneck at scale.
