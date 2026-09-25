---
tags: [area, homelab, deploy]
created: 2026-09-25
updated: 2026-09-25
status: active
---
# Raspberry Pi homelab

Hosts [[grocerytracker]]. It runs Debian 13 (Trixie) on arm64. Runbooks: `Applications/GroceryTracker/deploy/`.

## Facts worth remembering
- The container rollout (`rollout-containers.md`) is the default. The native systemd and nginx
  rollout is for a Pi that already runs Postgres and nginx, or that has too little RAM to build images.
- HTTPS goes through a local CA (`deploy/tls/make-cert.sh`). Phones won't install the PWA without it.
- nginx must never cache `index.html`, `service-worker.js` or `service-worker-assets.js`.
- **Never wipe the server DB to clean up.** Clients see a cursor from the future and stop at "Server reset".
- Take a `pg_dump` before any rollout that includes a migration.

## Open
- [ ] Scheduled backups (a cron or systemd timer running `pg_dump`)
