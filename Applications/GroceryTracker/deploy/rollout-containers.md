# Rollout with Compose

For a Raspberry Pi running **Debian 13 (Trixie)** — including 64-bit Raspberry Pi OS, which is built
on it — and for any other machine with Docker or Podman. Commands are written for `docker compose`;
with Podman run `podman compose` instead — the file is plain Compose spec and has been verified on
both. Never call `docker-compose` directly on a Podman host: only `podman compose` points the
provider at Podman's socket.

All commands run from `Applications/GroceryTracker` in a checkout of the repo.

## First install

### 1. Prerequisites

Check what you are on first. The instructions below assume `trixie` and `arm64`:

```bash
. /etc/os-release && echo "$PRETTY_NAME ($VERSION_CODENAME)"
dpkg --print-architecture
```

Install Docker from **Docker's own apt repository**. Debian's `docker.io` package does not ship the
Compose V2 plugin, and the `docker-compose` package is the deprecated V1, which does not understand
the `depends_on: condition:` this stack relies on to order migrations before the API.

```bash
sudo apt update
sudo apt install -y ca-certificates curl git

sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] \
https://download.docker.com/linux/debian $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
  | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

sudo usermod -aG docker "$USER"   # log out and back in for this to take effect
docker compose version            # must print v2.x
```

> On Raspberry Pi OS the repository to use is still `linux/debian`, and `$VERSION_CODENAME` is
> `trixie`. If `dpkg --print-architecture` says `armhf`, you are on a 32-bit system: Docker
> publishes armhf packages, but the .NET SDK image the build needs is arm64-only, so reinstall
> 64-bit Raspberry Pi OS before going further.

### 2. Get the code

```bash
git clone --filter=blob:none --sparse https://github.com/CodeIsNotEvil/MyMonoRepo.git
cd MyMonoRepo
git sparse-checkout set Applications/GroceryTracker
cd Applications/GroceryTracker
```

### 3. Configure

```bash
cp .env.example .env
nano .env        # set a real POSTGRES_PASSWORD, and WEB_PORT if 8080 is taken
```

`.env` is git-ignored and is the only place the password lives. **PostgreSQL only reads
`POSTGRES_PASSWORD` when it initialises an empty data directory** — changing it later does not
change the password in an existing volume, it just makes every connection fail with `28P01`.

### 4. Build and start

```bash
docker compose up -d --build
```

On Pi hardware the Blazor publish step is the slow part and wants memory. On a 2 GB Pi, add swap for
the build:

```bash
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile
sudo mkswap /swapfile && sudo swapon /swapfile
# afterwards: sudo swapoff /swapfile && sudo rm /swapfile
```

### 5. Verify

```bash
docker compose ps                     # postgres healthy, migration-runner exited 0, api up, web up
curl -fsS http://localhost:8080/health
curl -fsS -X POST http://localhost:8080/api/sync \
  -H 'Content-Type: application/json' -d '{"cursor":0,"operations":[]}' | head -c 400
```

The sync call returns one household and the categories *Groceries* and *Household*. `migration-runner`
showing `Exited (0)` is success — it is a one-shot job, not a service that should stay up.

### 6. Reach it from the LAN

Debian installs no firewall by default, so there is usually nothing to open. If you added `ufw`:

```bash
sudo ufw allow 8080/tcp
sudo ufw allow 8443/tcp     # only once you serve HTTPS
```

Then open `http://<pi-hostname>:8080` from a phone or laptop. Every service is
`restart: unless-stopped`, and Docker's daemon is enabled at boot by the packages above, so a
reboot brings the stack back on its own. Rootless Podman needs `loginctl enable-linger $USER` for
the same to be true.

### 7. Serve it over HTTPS

Do this before using the app on a phone. Without HTTPS the browser refuses to register a service
worker, which is exactly the part that makes the app work in the shop with no signal. See
[`https.md`](https.md).

## Updating to a new version

```bash
cd ~/MyMonoRepo/Applications/GroceryTracker

# 1. Back up first if this update carries a migration (see below).
docker compose exec -T postgres pg_dump -U postgres -d grocerytracker \
  | gzip > ~/grocerytracker-$(date +%F-%H%M).sql.gz

# 2. Take the new code.
git pull

# 3. Rebuild and restart what changed.
docker compose up -d --build

# 4. Check.
docker compose ps
curl -fsS http://localhost:8080/health
```

Data survives: the Postgres volume is never touched by a rebuild, and `migration-runner` applies any
new migrations before the API starts. Only containers whose image actually changed are recreated —
usually `api` and `web`. `web` re-resolves `api`'s new address on its own; it does not need a
restart when `api` is replaced.

**Does this update carry a migration?** `git diff --stat HEAD@{1} -- src/Infrastructure/Migrations`
after a pull, or `git log -p --name-only <old>..<new> -- src/Infrastructure/Migrations`. If files
were added there, take the dump.

### On the devices

Phones and laptops do not update the instant you deploy. The service worker fetches the new build in
the background on the next visit and installs it on the visit after that, so give an installed PWA
two loads before concluding a change did not ship. A hard reload or closing and reopening the
installed app is usually enough.

## Rolling back

Without a migration, roll the code back:

```bash
git log --oneline -10          # find the commit that worked
git checkout <commit>
docker compose up -d --build
```

**With a migration, code alone is not enough.** The old code will not understand a schema it never
knew about, and nothing reverts it automatically. Restore the dump you took before the rollout:

```bash
docker compose stop api
gunzip -c ~/grocerytracker-<timestamp>.sql.gz | \
  docker compose exec -T postgres psql -U postgres -d grocerytracker
docker compose start api
```

Restoring loses everything recorded since the dump, including changes that devices have already
synced. Devices that still hold those changes locally will re-upload them on their next sync, which
is the one case where the offline outbox works in your favour.

Stepping the schema down instead of restoring is possible, but the database is deliberately not
published to the host, so it needs a shell that can reach it — the .NET SDK and
`dotnet ef database update <PreviousMigrationName> --project src/Infrastructure
--startup-project src/Infrastructure --connection "<connection string>"`. For a household app the
dump is almost always the better answer.

## Operating it

```bash
docker compose logs -f api        # tail the API
docker compose logs migration-runner
docker compose restart api
docker compose down               # stop everything; the database volume survives
docker compose down -v            # also delete the database. Read the warning below first.
```

`down -v` destroys all spending data and makes every device that already has the app show a
*"Server reset"* banner until its local data is cleared under **Settings → Reset local data**. It is
for starting over from empty, not for troubleshooting.

### Backups worth having

A weekly dump, kept off the Pi:

```bash
# /etc/cron.weekly/grocerytracker-backup  (chmod +x)
#!/bin/sh
cd /home/pi/MyMonoRepo/Applications/GroceryTracker || exit 1
docker compose exec -T postgres pg_dump -U postgres -d grocerytracker \
  | gzip > "/home/pi/backups/grocerytracker-$(date +%F).sql.gz"
find /home/pi/backups -name 'grocerytracker-*.sql.gz' -mtime +60 -delete
```

Verify a dump can actually be read back before you rely on it.

## When something is wrong

| Symptom | Cause |
|---|---|
| `migration-runner` exits non-zero with `28P01 password authentication failed` | `.env` password does not match the one baked into the existing volume. Either restore the old password or start the volume over with `down -v`. |
| Browser downloads a file instead of showing the page | nginx lost its MIME table. The `types { }` block in a server context *replaces* the inherited table, so `include /etc/nginx/mime.types;` has to come first. |
| The app loads but every sync fails | `api` is not healthy. `docker compose logs api` — usually it cannot reach PostgreSQL. |
| `api` is healthy but the page 502s | `web` cached a stale address for `api`. The shipped config re-resolves it; if you replaced that config, put the `resolver` directive and the variable `proxy_pass` back. |
| Devices show a "Server reset" banner | The server's database is younger than the device's cache — a `down -v` or a restore. Clear local data on the device under **Settings**. |
| `docker compose` says `depends_on contains an invalid type` | Compose V1 is being used. Check `docker compose version`; the plugin from Docker's repo reports v2.x. |
