---
tags: [moc]
created: 2026-09-25
updated: 2026-09-25
status: active
---
# Index

The map of content. Keep it short: link to hub notes, not to every note.

## Projects
- [[grocerytracker]]: offline-first grocery spend tracker (.NET 10, Blazor WASM PWA, PostgreSQL)

## Areas
- [[raspberry-pi-homelab]]: the Debian 13 Pi that hosts GroceryTracker
- [[dotfiles]]: Nushell, Starship, fonts, Steam launch options
- [[kubernetes-learning]]: Minikube, kubectl, Helm experiments

## Resources
- [[offline-first-sync]]: the push/pull and stamp pattern, reusable beyond this app
- [[dotnet-gotchas]]: toolchain snags on Arch/CachyOS and with EF Core
- [[podman-compose]]: why it's always `podman compose`

## Decisions
- [[0001-syncstamp-counter]]: sync cursors use a locked counter row, not timestamps
- [[0002-sqlite-for-sync-tests]]: sync tests run on in-memory SQLite, not the EF in-memory provider
- [[0003-shared-domain-analytics]]: the same analytics code runs on the server and in the browser
- [[0004-cine-root-namespace]]: all .NET namespaces start with `CINE.`; assembly names do not

## Archive
- [[shoppingmanager]]: the predecessor to GroceryTracker

## Inbox
Untriaged captures live in `inbox/`. Empty it now and then.
