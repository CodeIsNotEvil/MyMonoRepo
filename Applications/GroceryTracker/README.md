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

Needs the .NET 10 SDK and a PostgreSQL you can reach.

```bash
# one command, serves both the API and the PWA on http://localhost:5000
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=grocerytracker;Username=postgres;Password=postgres"
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

## Deploying to the Raspberry Pi

### With Docker Compose (simplest)

```bash
cp .env.example .env     # change the password
docker compose up -d --build
```

The app is then on `http://<pi-hostname>:8080`. All images are multi-arch, so this builds on arm64
unchanged. PostgreSQL is not published to the host — only nginx is.

### Native, with the system nginx

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

The connection string holds a password, so it goes in `/etc/grocerytracker/bff.env` (root-readable
only) rather than in the unit file.

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
