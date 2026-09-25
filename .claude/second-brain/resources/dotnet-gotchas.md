---
tags: [resource, dotnet, gotcha]
created: 2026-09-25
updated: 2026-09-25
status: active
---
# .NET gotchas

- **`NETSDK1226: Prune Package data not found`** on Arch/CachyOS means `aspnet-targeting-pack` is
  missing (`sudo pacman -S aspnet-targeting-pack`). Container builds are unaffected.
- **EF migrations need no DB.** `dotnet ef migrations add <Name> --project src/Infrastructure` uses
  `GroceryTrackerDbContextFactory`.
- **The EF in-memory provider can't test the sync engine.** It has no transactions and no raw SQL.
  See [[0002-sqlite-for-sync-tests]].
- **Style drift:** `.editorconfig` says block-scoped namespaces, but the code uses file-scoped ones. Match the code.
