# Rollout without containers

For a Pi that already runs PostgreSQL and nginx, or one too small to build container images. You
need the .NET 10 SDK on the machine, because both the API and the PWA are published there.

The container route in [`rollout-containers.md`](rollout-containers.md) is less work and is what the
rest of the documentation assumes. Use this one deliberately.

## The pieces

| Piece | Where it ends up | Config |
|---|---|---|
| API | `/opt/grocerytracker`, run by systemd on `127.0.0.1:5000` | [`systemd/grocerytracker-api.service`](systemd/grocerytracker-api.service) |
| PWA | `/var/www/grocerytracker` | — |
| nginx | port 80, serves the PWA and proxies `/api` | [`nginx/grocerytracker.site.conf`](nginx/grocerytracker.site.conf) |
| PostgreSQL | the system instance | `/etc/grocerytracker/api.env` |

## First install

### 1. Prerequisites

```bash
sudo apt update
sudo apt install -y nginx postgresql dotnet-sdk-10.0
```

### 2. Database and service account

```bash
sudo -u postgres createuser grocerytracker --pwprompt
sudo -u postgres createdb grocerytracker -O grocerytracker

sudo useradd --system --no-create-home --shell /usr/sbin/nologin grocerytracker
sudo mkdir -p /opt/grocerytracker /var/log/grocerytracker /etc/grocerytracker
sudo chown grocerytracker:grocerytracker /var/log/grocerytracker
```

The connection string holds a password, so it goes in a root-only environment file rather than in
the unit:

```bash
sudo tee /etc/grocerytracker/api.env >/dev/null <<'EOF'
ConnectionStrings__DefaultConnection=Host=127.0.0.1;Port=5432;Database=grocerytracker;Username=grocerytracker;Password=<the password>
EOF
sudo chmod 600 /etc/grocerytracker/api.env
```

### 3. Publish both halves

```bash
cd ~/MyMonoRepo/Applications/GroceryTracker

sudo dotnet publish src/Api -c Release -r linux-arm64 --self-contained false \
  -o /opt/grocerytracker

dotnet publish src/UI/GroceryTracker.UI.Blazor -c Release -o /tmp/gt
sudo mkdir -p /var/www/grocerytracker
sudo rsync -a --delete /tmp/gt/wwwroot/ /var/www/grocerytracker/
```

`--delete` matters: stale `_framework` files from an older build confuse the service worker, which
checks the integrity of every asset it was told to expect.

### 4. Start the API

```bash
sudo cp deploy/systemd/grocerytracker-api.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now grocerytracker-api
systemctl status grocerytracker-api
curl -fsS http://127.0.0.1:5000/health
```

The API applies EF Core migrations itself at startup, so the first start also creates the schema.

### 5. nginx

```bash
sudo cp deploy/nginx/grocerytracker.site.conf /etc/nginx/sites-available/grocerytracker
sudo nano /etc/nginx/sites-available/grocerytracker   # set server_name to this machine
sudo ln -sf /etc/nginx/sites-available/grocerytracker /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t && sudo systemctl reload nginx
curl -fsS http://localhost/health
```

## Updating

```bash
cd ~/MyMonoRepo/Applications/GroceryTracker
git pull

# Back up before a release that adds a migration.
pg_dump -h 127.0.0.1 -U grocerytracker grocerytracker | gzip > ~/grocerytracker-$(date +%F).sql.gz

sudo systemctl stop grocerytracker-api
sudo dotnet publish src/Api -c Release -r linux-arm64 --self-contained false -o /opt/grocerytracker
sudo systemctl start grocerytracker-api

dotnet publish src/UI/GroceryTracker.UI.Blazor -c Release -o /tmp/gt
sudo rsync -a --delete /tmp/gt/wwwroot/ /var/www/grocerytracker/

systemctl status grocerytracker-api
curl -fsS http://localhost/health
```

Stopping the API before republishing is not optional: the files are in use while it runs.

The PWA does not need an nginx reload — the files are read per request — but installed clients still
take two visits to pick up a new build, the same as with containers.

## Rolling back

```bash
git checkout <previous commit>
sudo systemctl stop grocerytracker-api
sudo dotnet publish src/Api -c Release -r linux-arm64 --self-contained false -o /opt/grocerytracker
sudo systemctl start grocerytracker-api
dotnet publish src/UI/GroceryTracker.UI.Blazor -c Release -o /tmp/gt
sudo rsync -a --delete /tmp/gt/wwwroot/ /var/www/grocerytracker/
```

If the version you are leaving applied a migration, the schema does not come back with the code.
Either step the database down from a checkout of the newer code:

```bash
dotnet ef database update <PreviousMigrationName> \
  --project src/Infrastructure --startup-project src/Infrastructure \
  --connection "Host=127.0.0.1;Database=grocerytracker;Username=grocerytracker;Password=<the password>"
```

`--connection` is required: the design-time factory points at a dummy database, because its only job
is to let `dotnet ef migrations add` build the model without the ASP.NET Core targeting pack.

or restore the dump you took first:

```bash
sudo systemctl stop grocerytracker-api
gunzip -c ~/grocerytracker-<date>.sql.gz | psql -h 127.0.0.1 -U grocerytracker grocerytracker
sudo systemctl start grocerytracker-api
```

## Logs

```bash
journalctl -u grocerytracker-api -f
sudo tail -f /var/log/nginx/error.log
```

The unit runs with `ProtectSystem=strict` and only `/var/log/grocerytracker` writable. A rollout that
needs the API to write somewhere else has to add that path to `ReadWritePaths` — it will otherwise
fail with a permission error that looks nothing like one.
