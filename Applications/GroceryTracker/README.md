# GroceryTracker

Tracks and analyses household grocery spending. The frontend is a Blazor WebAssembly **standalone**
PWA that works with no connection at all and reconciles with the backend once it can reach it.

Built to run on a Raspberry Pi with Ubuntu Server: nginx serves the WebAssembly app and proxies the
API to an ASP.NET Core BFF, which talks to PostgreSQL through EF Core.

## Architecture

```
  phone / laptop browser
  ┌──────────────────────────────┐
  │ Blazor WASM PWA              │
  │  • reads/writes IndexedDB    │  ← the UI never waits on the network
  │  • queues changes in outbox  │
  │  • SpendAnalyzer (shared)    │
  └──────────────┬───────────────┘
                 │  POST /api/sync   (one origin, no CORS)
  ┌──────────────▼───────────────┐
  │ nginx (Raspberry Pi)         │  serves wwwroot, proxies /api
  └──────────────┬───────────────┘
  ┌──────────────▼───────────────┐
  │ GroceryTracker.Bff           │  ASP.NET Core
  │  • SyncService               │
  │  • AnalyticsService          │  ← runs the same SpendAnalyzer
  └──────────────┬───────────────┘
  ┌──────────────▼───────────────┐
  │ PostgreSQL (EF Core)         │
  └──────────────────────────────┘
```

The browser only ever talks to one origin. That is what makes this a BFF rather than a public API:
there is no CORS in production, and the database is never exposed.

### Projects

| Project | Role |
|---|---|
| `src/Domain` | Entities, the `ISyncEntity` contract, and `SpendAnalyzer` |
| `src/Contracts` | The sync wire protocol shared by client and server |
| `src/Infrastructure` | EF Core `DbContext`, configurations, migrations |
| `src/Application` | `SyncService`, `AnalyticsService`, household provisioning |
| `src/Bff` | ASP.NET Core host: `/api/sync`, `/api/analytics`, `/health` |
| `src/MigrationRunner` | Applies migrations as a one-shot container |
| `src/UI/GroceryTracker.UI.Blazor` | The PWA |

`SpendAnalyzer` deliberately lives in `Domain` with no EF Core or HTTP dependency, so **the identical
code runs on the server against PostgreSQL and in the browser against the IndexedDB cache**. The
dashboard shows the same numbers offline; only the place they were calculated changes.

## How offline sync works

Every syncable row carries three extra columns:

- `SyncStamp` — a server-assigned value from one global counter
- `UpdatedAtUtc` — when the server last wrote it
- `IsDeleted` — a tombstone, because a physical delete is invisible to a device that was offline

**Stamps are allocated by incrementing a single counter row inside the same transaction as the
write.** That row lock is the whole trick: it serialises writers, so stamps come out in commit
order and a client pulling `SyncStamp > cursor` can never step over a write that committed while it
was reading. A wall-clock cursor cannot promise that.

One round trip does both halves:

```
POST /api/sync  { cursor, operations[] }
        ↓
   apply the device's queued operations (parents before children)
   pull everything with SyncStamp > cursor
        ↓
{ cursor, appliedOperationIds[], conflicts[], payload }
```

- **Idempotency** — each operation carries an `operationId`. The server records the ones it has
  applied, so a device that never saw a response can replay its outbox safely.
- **Conflicts** — the device sends the `SyncStamp` it was editing. If the row has moved on since,
  last-write-wins decides by edit time, and the outcome is reported either way so Settings can show
  that something was overwritten rather than losing it silently.
- **Rejection** — an operation that can never apply (it references a store that does not exist, or
  its payload is malformed) is reported as `Rejected` instead of failing the batch. Without that,
  one bad row would poison a device's outbox forever.
- **Ordering** — operations are applied parents-first, so a trip and its line items can be created
  together on a device's very first sync.

On the client, the UI reads from IndexedDB and writes to IndexedDB plus an outbox; a save never
waits on the network. Sync runs at startup, when the browser regains connectivity, after each edit,
and on a slow timer as a backstop.

> **First launch needs one connection.** A device discovers its household by pulling it, so open the
> app once with the server reachable. Everything after that works offline.

## Running it locally

Needs the .NET 10 SDK and a PostgreSQL you can reach. If you already have Podman or Docker, the
fastest way to get a local Postgres is `podman compose up -d postgres` (see below) — no native
install needed.

```bash
# bash/zsh — one command, serves both the API and the PWA on http://localhost:5000
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=grocerytracker;Username=postgres;Password=postgres"
dotnet run --project src/Bff
```

```fish
# fish — same thing
set -x ConnectionStrings__DefaultConnection "Host=localhost;Port=5432;Database=grocerytracker;Username=postgres;Password=postgres"
dotnet run --project src/Bff
```

The BFF migrates the database and seeds a household with eight starter categories on startup.

To work on the frontend with hot reload, run the client separately. Set `ApiBaseUrl` in
`wwwroot/appsettings.Development.json` to `http://localhost:5000` first — it ships empty, which
means "same origin", because that is what is correct both behind nginx and when the BFF hosts the
app itself. The BFF allows the dev-server origin in development only:

```bash
dotnet run --project src/Bff                         # :5000
dotnet run --project src/UI/GroceryTracker.UI.Blazor # :5173
```

### Tests

```bash
dotnet test
```

Covers the analyzer's arithmetic, the sync engine's conflict/idempotency/ordering behaviour against
a real relational database, the stamp allocation, and the chart geometry.

## Running the full stack locally with Podman

`docker-compose.yml` is a plain Compose-spec file; nothing in it is Docker-specific, and it has been
verified against Podman directly (rootless, netavark networking, named volumes, `depends_on:
condition: service_healthy` / `service_completed_successfully`, the lot).

**Always run `podman compose ...`, never a bare `docker-compose ...`.** `podman compose` is Podman's
own subcommand: it picks a compose provider (preferring a real `docker-compose` binary if one is
installed, falling back to `podman-compose` otherwise) and, for that one invocation, points it at
Podman's API socket via `DOCKER_HOST`. Run `docker-compose` directly and that socket is never wired
up, so it goes looking for a Docker daemon that doesn't exist on a Podman-only machine.

One-time setup (Arch/CachyOS):

```fish
sudo pacman -S podman docker-compose   # docker-compose gives podman compose the most complete provider
systemctl --user enable --now podman.socket
podman compose version   # should print a docker-compose version, run through Podman
```

Then, from this directory:

```fish
cp .env.example .env
# edit .env and set a real POSTGRES_PASSWORD

podman compose up -d --build
podman compose ps
curl http://localhost:8080/health
```

The app is now at `http://localhost:8080`. Useful commands, all with the `podman` prefix instead of
`docker`:

```fish
podman compose logs -f bff       # tail one service's logs
podman compose down              # stop and remove containers (the postgres volume survives)
podman compose down -v           # also wipe the database volume — start over from empty
podman compose up -d --build     # rebuild after pulling code changes
```

### What had to change to make this Podman-clean

A few bugs surfaced while verifying this, all of which would have broken **plain Docker too** —
none were actually Podman-specific, just never exercised until now:

- **No `.dockerignore`.** `COPY src/ src/` in every Dockerfile was pulling in whatever `bin/`/`obj/`
  happened to exist locally from running `dotnet build` on the host. Those directories embed
  absolute paths to your host's NuGet package cache (`~/.nuget/packages/...`), which don't exist
  inside the build container — so restore would succeed, and the immediately following publish
  would fail claiming a package "wasn't found", even though it had just been restored. Fixed by
  adding `.dockerignore`.
- **The BFF's healthcheck used `wget`.** `mcr.microsoft.com/dotnet/aspnet:10.0` ships neither `wget`
  nor `curl`, so the healthcheck could never succeed. Fixed by installing `curl` in that image's
  final layer and switching the compose healthcheck to use it.
- **nginx cached `bff`'s IP forever.** `proxy_pass http://bff:5000;` (a literal hostname) resolves
  once, when nginx starts, and holds onto that address for as long as the container runs. Rebuild or
  recreate just the `bff` container — which is exactly what happens on the Pi every time you update
  only the backend — and it gets a new internal IP that `web` never learns about; every request
  after that hangs until `web` itself is restarted too. Fixed by turning on the base nginx image's
  own resolver detection (`NGINX_ENTRYPOINT_LOCAL_RESOLVERS=1`) and routing `proxy_pass` through a
  variable, which together make nginx re-resolve `bff` instead of caching it — verified by recreating
  `bff` alone and confirming `web` picked up its new address with no restart of its own.
- Also, unrelated to Podman: **every `wget`/curl-less container that talks to Postgres logged
  `Cannot load library libgssapi_krb5.so.2`** on every single connection attempt, success or not —
  Npgsql probing for a Kerberos library the slim runtime images don't ship, then quietly falling back
  to normal password/SCRAM auth. Harmless, but it looks exactly like a fatal error and is a red
  herring next to a *real* connection failure. Installed `libgssapi-krb5-2` in both the BFF and
  migration-runner images to make the logs trustworthy again.

## Deploying to the Raspberry Pi

The Pi target is Ubuntu Server, so Docker Engine is the path of least friction there — it is what
Ubuntu's own docs and apt repo cover, and its restart policies and socket activation need no extra
setup. If you'd rather run Podman on the Pi too for consistency with your dev machine, the compose
file works identically there; see the callout at the end of each step.

### 1. Prepare the Pi

SSH in, then:

```bash
sudo apt update && sudo apt full-upgrade -y
sudo reboot
```

Note the Pi's hostname or IP (`hostname -I`) — you'll use it from your phone and laptop.

### 2. Install a container engine + Compose

**Docker (recommended for Ubuntu Server):**

```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker "$USER"
newgrp docker   # or log out and back in
docker compose version   # confirms the compose plugin is present
```

**Podman instead**, if you'd rather match your dev machine — install it from apt, but get the
`docker-compose` binary straight from its GitHub releases rather than apt's package. Ubuntu/Debian
have a history of shipping the old, deprecated Compose V1 (which doesn't understand
`depends_on: condition:`, and this compose file relies on that); the binary below is the same
current release you're already running on your dev machine:

```bash
sudo apt install -y podman
sudo curl -fsSL -o /usr/local/bin/docker-compose \
  https://github.com/docker/compose/releases/latest/download/docker-compose-linux-aarch64
sudo chmod +x /usr/local/bin/docker-compose

systemctl --user enable --now podman.socket
# rootless podman needs a user session that survives logout for the service to keep running:
sudo loginctl enable-linger "$USER"
podman compose version   # should print a docker-compose version, run through Podman
```

From here on, every `docker compose` command below is `podman compose` if you went that route —
nothing else changes.

### 3. Get the project onto the Pi

A full clone is simplest and the repo isn't large; if you'd rather not pull the rest of the
monorepo, a sparse checkout works too:

```bash
git clone --filter=blob:none --sparse https://github.com/CodeIsNotEvil/MyMonoRepo.git
cd MyMonoRepo
git sparse-checkout set Applications/GroceryTracker
cd Applications/GroceryTracker
```

### 4. Configure and build

```bash
cp .env.example .env
nano .env   # set a real POSTGRES_PASSWORD

docker compose up -d --build
```

> **Building the .NET SDK images natively on Pi hardware is slow and memory-hungry** — the Blazor
> WebAssembly publish step (IL trimming/linking) is the heaviest part and can be killed by the OOM
> killer on a Pi with 2 GB of RAM or less. If the build stalls or a container disappears mid-build:
> ```bash
> # add 2 GB of temporary swap for the build
> sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile
> sudo mkswap /swapfile && sudo swapon /swapfile
> ```
> A Pi 4/5 with 4 GB+ RAM builds without any of this, just slower than a desktop (10–20 minutes is
> normal). The swap file can be removed again afterwards (`sudo swapoff /swapfile && sudo rm
> /swapfile`) once the images are built — they don't need it to run, only to build.

### 5. Verify

```bash
docker compose ps                        # everything should be "Up" / "healthy"
curl http://localhost:8080/health        # -> Healthy
curl -X POST http://localhost:8080/api/sync \
  -H 'Content-Type: application/json' -d '{"cursor":0,"operations":[]}'
```

The last command should return a JSON payload with one seeded household and eight categories.

### 6. Open it up to your LAN

```bash
sudo ufw allow 8080/tcp    # only if ufw is active; check with `sudo ufw status`
```

Visit `http://<pi-hostname-or-ip>:8080` from your phone and laptop.

### 7. Survive a reboot

Docker's own daemon restarts on boot, and every service in the compose file is
`restart: unless-stopped`, so nothing extra is needed — reboot the Pi and check `docker compose ps`
once it's back. (With Podman, `loginctl enable-linger` from step 2 is what makes the equivalent true
for a rootless user session.)

### Updating later

```bash
cd ~/MyMonoRepo/Applications/GroceryTracker   # or wherever you cloned it
git pull
docker compose up -d --build
```

Existing data survives: the Postgres volume is untouched by a rebuild, and EF Core migrations run
automatically via the `migration-runner` service on every `up`. This also only recreates the
containers whose image actually changed — usually just `bff` — and `web` correctly picks up its new
address without needing a restart of its own (see the nginx resolver note above).

### Native install, without containers

If you'd rather not run a container engine on the Pi at all:

```bash
# 1. the API
dotnet publish src/Bff -c Release -r linux-arm64 --self-contained false -o /opt/grocerytracker
sudo cp deploy/systemd/grocerytracker-bff.service /etc/systemd/system/
sudo systemctl enable --now grocerytracker-bff       # listens on 127.0.0.1:5000

# 2. the PWA
dotnet publish src/UI/GroceryTracker.UI.Blazor -c Release -o /tmp/gt
sudo rsync -a --delete /tmp/gt/wwwroot/ /var/www/grocerytracker/

# 3. nginx
sudo cp deploy/nginx/grocerytracker.site.conf /etc/nginx/sites-available/grocerytracker
sudo ln -s /etc/nginx/sites-available/grocerytracker /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

This needs the .NET 10 SDK on the Pi (for `dotnet publish`) and a PostgreSQL you've set up
separately. The connection string holds a password, so it goes in `/etc/grocerytracker/bff.env`
(root-readable only) rather than in the unit file.

### Installing it on your phone

Open the site in Chrome on Android and choose **Add to Home screen**. It then launches standalone
and keeps working in the shop with no signal.

Two caveats worth knowing:

- **Service workers need a secure context.** `http://` works on `localhost`, but for the PWA to
  install and cache on your phone the site has to be `https://` or a `localhost` tunnel. The
  easiest route on a home network is a Tailscale hostname with its certificate, or a local CA.
- **`nginx` must not cache `index.html`, `service-worker.js` or `service-worker-assets.js`.** Those
  three decide when a device picks up a new build; the supplied configs already set `no-cache` on
  them and cache the fingerprinted `_framework/` assets forever.

## Security

There is no authentication. The data model carries `Household` and `Member` so it can be added later
without a migration rewrite, but as it stands **anyone who can reach the port can read and write
everything**. Keep it on your LAN or behind Tailscale; do not port-forward it.
