# Deploying GroceryTracker

This folder holds everything needed to run the app on a server, plus the runbooks for putting a new
version on it.

| File | What it is |
|---|---|
| [`rollout-containers.md`](rollout-containers.md) | **Start here.** Install and update the whole stack with Compose (Docker or Podman) |
| [`rollout-native.md`](rollout-native.md) | The same app without a container engine: systemd + nginx + a system PostgreSQL |
| [`nginx/grocerytracker.container.conf.template`](nginx/grocerytracker.container.conf.template) | nginx config baked into the `web` image. Not used by the native install |
| [`nginx/grocerytracker.site.conf`](nginx/grocerytracker.site.conf) | nginx site for the native install |
| [`systemd/grocerytracker-api.service`](systemd/grocerytracker-api.service) | systemd unit for the native install |

## Which one

Containers unless you have a reason not to. One command builds and starts everything, migrations run
in the right order by themselves, and PostgreSQL never leaves the stack's own network. The native
install exists for a Pi that already runs PostgreSQL and nginx for something else, or that has too
little memory to build images locally.

## What a rollout actually consists of

Four things ship together and all of them come from the same commit:

1. **The database schema** — EF Core migrations, applied by the `migration-runner` container (or by
   the API at startup on the native install) before anything serves traffic.
2. **The API** — `src/Api`, an ASP.NET Core app on port 5000, never published to the network
   directly.
3. **The PWA** — static files from `src/UI/GroceryTracker.UI.Blazor`, served by nginx.
4. **nginx** — the only thing listening on a published port. It serves the PWA and proxies `/api`
   to the API, which is what keeps the browser on a single origin.

Two properties of this app shape every rollout:

- **Migrations are forward-only.** Checking out the previous commit does not undo a schema change
  that has already been applied. Take a dump before a rollout that includes a migration; that dump
  *is* the rollback plan. See [Rolling back](rollout-containers.md#rolling-back).
- **Clients are offline-first and cache themselves.** A phone that already has the PWA installed
  keeps running the old build until its service worker picks up the new one, and it holds unsynced
  changes in its own IndexedDB outbox. Never wipe the server database to "clean up" — a client that
  still has the old data will be told the server was reset, and the user has to clear the app's
  local data by hand before it syncs again.
