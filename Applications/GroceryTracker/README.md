# GroceryTracker

Tracks and analyses household grocery spending. The frontend is a Blazor WebAssembly **standalone**
PWA that works with no connection at all and reconciles with the backend once it can reach it.

Built to run on a Raspberry Pi with Debian 13 (Trixie): nginx serves the WebAssembly app and
proxies the API to an ASP.NET Core backend, which talks to PostgreSQL through EF Core.

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
  │ GroceryTracker.Api           │  ASP.NET Core
  │  • SyncService               │
  │  • AnalyticsService          │  ← runs the same SpendAnalyzer
  └──────────────┬───────────────┘
  ┌──────────────▼───────────────┐
  │ PostgreSQL (EF Core)         │
  └──────────────────────────────┘
```

The browser only ever talks to one origin, and the API is shaped around this one frontend rather
than being a public API: there is no CORS in production, and the database is never exposed.

### Projects

| Project | Role |
|---|---|
| `src/Domain` | Entities, the `ISyncEntity` contract, and `SpendAnalyzer` |
| `src/Contracts` | The sync wire protocol shared by client and server |
| `src/Infrastructure` | EF Core `DbContext`, configurations, migrations |
| `src/Application` | `SyncService`, `AnalyticsService`, household provisioning |
| `src/Api` | ASP.NET Core host: `/api/sync`, `/api/analytics`, `/health` |
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
dotnet run --project src/Api
```

```fish
# fish — same thing
set -x ConnectionStrings__DefaultConnection "Host=localhost;Port=5432;Database=grocerytracker;Username=postgres;Password=postgres"
dotnet run --project src/Api
```

The API migrates the database and seeds a household with two categories, *Groceries* and *Household*, on startup.

To work on the frontend with hot reload, run the client separately. Set `ApiBaseUrl` in
`wwwroot/appsettings.Development.json` to `http://localhost:5000` first — it ships empty, which
means "same origin", because that is what is correct both behind nginx and when the API hosts the
app itself. The API allows the dev-server origin in development only:

```bash
dotnet run --project src/Api                         # :5000
dotnet run --project src/UI/GroceryTracker.UI.Blazor # :5173
```

### Tests

```bash
dotnet test
```

Covers the analyzer's arithmetic, the CSV import parser, the sync engine's
conflict/idempotency/ordering behaviour against a real relational database, the stamp allocation,
and the chart geometry.

On Arch/CachyOS the `Api` project also needs the ASP.NET Core *targeting pack*, which is a separate
package from the runtime. Without it the build stops with `NETSDK1226: Prune Package data not found`
(the container builds are unaffected — the SDK image ships it):

```fish
sudo pacman -S aspnet-targeting-pack
```

## The dashboard

Four ranges: **This month**, 3, 6 and 12 months. The three multi-month ranges chart weekly or
monthly buckets; **This month** charts every shopping day instead and lists each entry underneath,
so a month can be read receipt by receipt rather than as one bar.

Two of the tiles ignore the selected range on purpose, because they answer questions the range
cannot:

- **Year to date** — everything since 1 January of the current year.
- **Per month** — the average over the last twelve *full* months. The running month is excluded: two
  days into a month its total is not comparable to a whole one, and including it would make the
  figure dip and recover every month. A shorter history is averaged over the months it actually
  covers, and the tile says how many that was.

## Recording a trip

The total is what counts as spend; line items are optional and only split it across categories.
Two conveniences make the common case one tap:

- **The first line item opens with the whole total**, in the category this device used last (the
  household's first category until then) — so "all of it was groceries" needs no typing. Splitting a
  receipt means editing that amount down and adding a second item.
- **Paid by is remembered on this device**, like *this device belongs to* on the Balance screen.
  It is a separate setting, so logging one trip your partner paid for does not reassign who *you*
  are on the balance.

## Importing bookings from a spreadsheet

**Manage → Import bookings from CSV** brings in past spending from a CSV export of a sheet. It runs
in the browser, so it works offline: the trips are written to the device and go up on the next sync
like any other edit.

- **Only dates and amounts are read.** Nothing depends on column names or order — a cell that looks
  like a date is the date, a cell that looks like an amount is an amount, and every filled amount
  cell becomes its own trip (two people shopping on one day is two receipts).
- **You say who each amount column belongs to.** The preview lists every column of amounts with its
  header, booking count and total, and a dropdown to pick an existing person, add a new one, or leave
  it unassigned. A column headed with someone's name starts out as that person. The payer is what the
  balance is built on.
- **Formats.** `81,50 €`, `1.234,56`, `1,234.56`, `-9,50 €`; dates as `04.04.2024`, `4.4.24` or
  `2024-04-04`; comma- or semicolon-separated, with or without a BOM.
- **Everything lands in one category** (you pick it in the preview; *Groceries* by default) at a
  store called *Imported* (rename it under Manage). Totals match the sheet exactly.
- **Re-importing is safe.** A booking's id is derived from its date, amount and which repeat of that
  pair it is, so importing the same file again — or a longer export of the same sheet — only adds
  rows that are not here yet, and trips you deleted afterwards are not resurrected. Importing again
  *with people chosen* also assigns them to bookings that were imported without a payer, rather
  than skipping them, so an earlier import made before people existed can be fixed in place.
- **You see what will happen first**: how many bookings are new, the total, any rows that could not
  be read (with line numbers), refunds (negative amounts, which lower your spend), and dates that sit
  far from every other booking, which is nearly always a typo like 2015 for 2025. Fix those in the
  sheet *before* importing — a corrected row would arrive as a new booking and leave the wrong one
  behind.

## Balancing between people

**Balance** shows who owes whom for the shared groceries and how to settle it up.

- Every trip records **who paid** (set on the trip, or by assigning CSV columns to people).
- Everyone carries a **share** of what was paid: equal by default, or set under **Manage → People**.
  Shares are relative parts — 1 and 1 split evenly, 2 and 1 make the first person carry two thirds.
  A person's balance is what they paid, minus
  their share, plus transfers they sent, minus transfers they received. Positive means they are
  owed; negative means they owe.
- It suggests the **fewest transfers** that settle everything — one payment for two people, at most
  *n − 1* for *n*. Choose **This device belongs to** once and the headline becomes personal: *You owe
  € 58.25*, with a **Mark as paid** button.
- **Transfers are tracked.** Marking a suggestion as paid (or recording one by hand) saves a
  settlement that moves the balance towards zero. Settlements are not spending, so they never show
  on the dashboard. Remove one and the balance comes back.
- **Trips nobody is recorded as having paid for are left out** and called out with their total, so
  the balance is never quietly wrong.
- It is computed from the device's own cache, so it works offline, and settlements sync like
  everything else.

## If the server's database is wiped

A device remembers how far it has synced. If the server's database is reset (for example
`podman compose down -v`), that memory points at data that no longer exists. Rather than let the two
drift apart silently, the server recognises a cursor from the future, applies nothing, and the app
shows **Server reset** with an explanation under Settings. Your queued changes and cached data are
left alone until you decide: **Reset local data** to carry on from what the server has, then
re-import your CSV to bring the bookings back.

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
podman compose logs -f api       # tail one service's logs
podman compose down              # stop and remove containers (the postgres volume survives)
podman compose down -v           # also wipe the database volume — start over from empty
podman compose up -d --build     # rebuild after pulling code changes
```

If `podman compose` suddenly fails with `failed to connect to the docker API at unix:///run/user/…/podman/podman.sock`,
the socket unit can report *active* while its socket file is gone. Recreate it:

```fish
systemctl --user restart podman.socket
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
- **The API's healthcheck used `wget`.** `mcr.microsoft.com/dotnet/aspnet:10.0` ships neither `wget`
  nor `curl`, so the healthcheck could never succeed. Fixed by installing `curl` in that image's
  final layer and switching the compose healthcheck to use it.
- **nginx cached `api`'s IP forever.** `proxy_pass http://api:5000;` (a literal hostname) resolves
  once, when nginx starts, and holds onto that address for as long as the container runs. Rebuild or
  recreate just the `api` container — which is exactly what happens on the Pi every time you update
  only the backend — and it gets a new internal IP that `web` never learns about; every request
  after that hangs until `web` itself is restarted too. Fixed by turning on the base nginx image's
  own resolver detection (`NGINX_ENTRYPOINT_LOCAL_RESOLVERS=1`) and routing `proxy_pass` through a
  variable, which together make nginx re-resolve `api` instead of caching it — verified by recreating
  `api` alone and confirming `web` picked up its new address with no restart of its own.
- Also, unrelated to Podman: **every `wget`/curl-less container that talks to Postgres logged
  `Cannot load library libgssapi_krb5.so.2`** on every single connection attempt, success or not —
  Npgsql probing for a Kerberos library the slim runtime images don't ship, then quietly falling back
  to normal password/SCRAM auth. Harmless, but it looks exactly like a fatal error and is a red
  herring next to a *real* connection failure. Installed `libgssapi-krb5-2` in both the API and
  migration-runner images to make the logs trustworthy again.

## Deploying to the Raspberry Pi

The step-by-step runbooks live in [`deploy/`](deploy/README.md): [rollout with
Compose](deploy/rollout-containers.md) covers installing, updating, backups and rolling back, and
[rollout without containers](deploy/rollout-native.md) covers the systemd and nginx route. The rest
of this section is the short version.

The Pi target is **Debian 13 (Trixie)** on arm64, which is also what 64-bit Raspberry Pi OS is
built on. Docker Engine from Docker's own apt repository is the path of least friction: Debian's
`docker.io` package does not ship the Compose V2 plugin this file needs. If you'd rather run Podman
on the Pi too, for consistency with your dev machine, the compose file works identically there.

### 1. Prepare the Pi

SSH in, then:

```bash
sudo apt update && sudo apt full-upgrade -y
sudo reboot
```

Note the Pi's hostname or IP (`hostname -I`) — you'll use it from your phone and laptop.

### 2. Install a container engine + Compose

**Docker (recommended on Debian):** use Docker's apt repository, which publishes arm64 packages for
Trixie and includes the Compose V2 plugin.

```bash
sudo apt install -y ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] \
https://download.docker.com/linux/debian $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
  | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker "$USER"
newgrp docker   # or log out and back in
docker compose version   # confirms the Compose V2 plugin is present
```

**Podman instead**, if you'd rather match your dev machine — install it from apt, but get the
`docker-compose` binary straight from its GitHub releases rather than apt's package. Debian ships
the old, deprecated Compose V1 under that name (it doesn't understand `depends_on: condition:`,
which this compose file relies on); the binary below is the same current release you're already
running on your dev machine:

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

The last command should return a JSON payload with one seeded household and two categories (Groceries, Household).

### 6. Open it up to your LAN

```bash
# Debian installs no firewall by default — skip this unless you added one.
sudo ufw allow 8080/tcp    # only if ufw is installed and active; check with `sudo ufw status`
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
containers whose image actually changed — usually just `api` — and `web` correctly picks up its new
address without needing a restart of its own (see the nginx resolver note above).

### Native install, without containers

If you'd rather not run a container engine on the Pi at all:

```bash
# 1. the API
dotnet publish src/Api -c Release -r linux-arm64 --self-contained false -o /opt/grocerytracker
sudo cp deploy/systemd/grocerytracker-api.service /etc/systemd/system/
sudo systemctl enable --now grocerytracker-api       # listens on 127.0.0.1:5000

# 2. the PWA
dotnet publish src/UI/GroceryTracker.UI.Blazor -c Release -o /tmp/gt
sudo rsync -a --delete /tmp/gt/wwwroot/ /var/www/grocerytracker/

# 3. nginx
sudo cp deploy/nginx/grocerytracker.site.conf /etc/nginx/sites-available/grocerytracker
sudo ln -s /etc/nginx/sites-available/grocerytracker /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

This needs the .NET 10 SDK on the Pi (for `dotnet publish`) and a PostgreSQL you've set up
separately. The connection string holds a password, so it goes in `/etc/grocerytracker/api.env`
(root-readable only) rather than in the unit file.

### Installing it on your phone

Open the site in Chrome on Android and choose **Add to Home screen**. It then launches standalone
and keeps working in the shop with no signal.

Two caveats worth knowing:

- **Service workers need a secure context.** `http://` works on `localhost`, but for the PWA to
  install and cache on your phone the site has to be `https://`. [`deploy/https.md`](deploy/https.md)
  walks through issuing your own certificate, trusting it on Android and the rest, and turning on
  the HTTPS port — the compose stack serves it as soon as a certificate exists in `deploy/certs`.
- **`nginx` must not cache `index.html`, `service-worker.js` or `service-worker-assets.js`.** Those
  three decide when a device picks up a new build; the supplied configs already set `no-cache` on
  them and cache the fingerprinted `_framework/` assets forever.

## Security

There is no authentication. The data model carries `Household` and `Member` so it can be added later
without a migration rewrite, but as it stands **anyone who can reach the port can read and write
everything**. Keep it on your LAN or behind Tailscale; do not port-forward it.
