# HTTPS with your own certificate

Serving the app over HTTPS, using a certificate you issue yourself and trust on your own devices.

## Why it is not optional for a phone

A browser only registers a **service worker** in a *secure context*: HTTPS, or `localhost`. The
service worker is the part that caches the app and lets it open in the shop with no signal. Over
plain `http://<pi>:8080` the app still loads and still syncs, but on the phone it is just a web
page: no offline start, and "Add to Home screen" gives you a bookmark rather than an app.

So the chain is: certificate → HTTPS → service worker → an app that works offline.

## Before you start: pick the address and keep it

An origin is the scheme, host and port together. All of the app's local data — the cached
households, trips and the outbox of changes not yet uploaded — lives in IndexedDB, which is **keyed
by origin**. `http://pi:8080` and `https://pi:8443` are two different origins, and the second one
starts empty.

In practice that means:

1. **Open the app on its old address once while the server is reachable, and let it sync**, so
   nothing is left stranded in an outbox you are about to walk away from. Settings shows
   *Waiting to upload — 0* when it is done.
2. Switch to the HTTPS address.
3. The app re-downloads everything from the server. Only device-local settings (*this device belongs
   to*, the remembered payer) need setting again.

Pick the address you can keep. Options, best first:

- **A hostname your router hands out.** Give the Pi a DHCP reservation and a name; a Fritz!Box then
  resolves `grocerytracker.fritz.box` for every device on the network. Stable and readable.
- **A fixed IP**, used directly in the URL. Works fine — a certificate can cover an IP address — but
  changes if you ever renumber the network, and takes the app's data with it.
- **A Tailscale name** (`pi.<tailnet>.ts.net`), if you want it to work away from home too. See
  [the end of this page](#alternative-tailscale-no-certificate-to-install).

Avoid `.local` / mDNS names: Android does not resolve them reliably in Chrome.

## 1. Make the certificate

On the Pi, in the checkout, pass **every** name and address you will open the app by:

```bash
./deploy/tls/make-cert.sh grocerytracker.fritz.box 192.168.1.50
```

That writes three files into `deploy/certs/` (git-ignored, so keys never reach the repo):

| File | What it is |
|---|---|
| `ca.crt` | Your certificate authority. **This is the one you install on phones and laptops.** |
| `ca.key` | The CA's private key. Never leaves the Pi; anything holding it can impersonate any site to your devices. |
| `grocerytracker.crt` / `.key` | The server certificate nginx serves. Installed nowhere. |

Running the script again reuses the CA and issues a fresh server certificate, so **adding an address
later does not mean re-trusting anything on your devices**. Losing `ca.key` does, which is why it is
worth a backup alongside your database dumps.

A browser checks the URL against the certificate's SAN list and ignores everything else, so an
address you leave out simply will not work. The script prints the list it used.

## 2. Turn on HTTPS

### Containers

Nothing to configure: `web` already mounts `deploy/certs` and publishes 8443. The container looks
for a certificate at start and logs which mode it chose.

```bash
docker compose up -d --force-recreate web
docker compose logs web | grep -i tls
# -> certificate found — also serving HTTPS on 443
```

Port 8443 rather than 443 because rootless Podman cannot bind a privileged port. With Docker on the
Pi you can have the real thing — set `WEB_TLS_PORT=443` in `.env` and recreate — which gets you
`https://grocerytracker.fritz.box` with no port in the URL.

Plain HTTP stays on 8080. It is useful for `curl` and for a desktop that has not got the CA, and
leaving it on costs nothing; drop the `${WEB_PORT:-8080}:80` line from `docker-compose.yml` if you
would rather it were gone.

### Native install

Copy the certificate somewhere nginx can read and add a second server block:

```bash
sudo mkdir -p /etc/nginx/certs
sudo cp deploy/certs/grocerytracker.crt /etc/nginx/certs/
sudo cp deploy/certs/grocerytracker.key /etc/nginx/certs/
sudo chmod 600 /etc/nginx/certs/grocerytracker.key
```

In `/etc/nginx/sites-available/grocerytracker`, copy the existing `server { }` block, and in the
copy replace the two `listen` lines with:

```nginx
listen 443 ssl;
listen [::]:443 ssl;
http2 on;

ssl_certificate     /etc/nginx/certs/grocerytracker.crt;
ssl_certificate_key /etc/nginx/certs/grocerytracker.key;
ssl_protocols TLSv1.2 TLSv1.3;
```

Then `sudo nginx -t && sudo systemctl reload nginx`.

## 3. Check it from the Pi

```bash
curl --cacert deploy/certs/ca.crt https://grocerytracker.fritz.box:8443/health
```

`Healthy` means the certificate, the chain and the proxy to the API are all right. A certificate
error here is a certificate problem; fix it before touching any phone.

## 4. Trust the CA on your devices

Only `ca.crt` is ever installed, and only on devices you own.

### Android

1. Get `ca.crt` onto the phone — email it to yourself, or download it from the Pi.
2. **Set a screen lock first if you have none.** Android refuses to install a CA without a PIN,
   pattern or password.
3. **Settings → Security & privacy → More security & privacy → Encryption & credentials → Install a
   certificate → CA certificate**. The exact path moves between Android versions; searching the
   settings for *"CA certificate"* finds it.
4. Android shows a blunt warning and then asks for the file. Pick `ca.crt`.
5. Afterwards the phone shows a persistent "network may be monitored" notice. That is expected: it
   is Android telling you a non-manufacturer CA is installed — the one you just made.

Chrome for Android uses the system store, so the site is trusted from then on.

### Linux (your CachyOS machine)

```bash
sudo cp deploy/certs/ca.crt /etc/ca-certificates/trust-source/anchors/grocerytracker-ca.crt
sudo trust extract-compat
```

On Debian or Ubuntu it is `/usr/local/share/ca-certificates/` and `sudo update-ca-certificates`.

That covers `curl`. **Chrome and Firefox keep their own store** and need it separately — Chrome:
*Settings → Privacy and security → Security → Manage certificates → Authorities → Import*, ticking
"Trust this certificate for identifying websites". Firefox: *Settings → Privacy & Security →
Certificates → View Certificates → Authorities → Import*.

### Windows

Double-click `ca.crt` → **Install Certificate** → *Local Machine* → *Place all certificates in the
following store* → **Trusted Root Certification Authorities**. Chrome and Edge use this store;
Firefox does not, and needs its own import as above.

### iPhone / iPad

Two steps, and people miss the second one: install the profile (*Settings → General → VPN & Device
Management*), **then** switch it on under *Settings → General → About → Certificate Trust
Settings*. Until that toggle is on, the certificate is installed but not trusted.

## 5. Install the app on the phone

Open `https://grocerytracker.fritz.box:8443` in Chrome. No warning should appear — if it does, the
CA did not install or the address is not in the certificate.

Then **⋮ → Add to Home screen**.

> **What you get.** Chrome builds a real installed app (a WebAPK) by asking Google's servers to
> package your manifest — and those servers cannot reach a machine on your home network. So on a
> LAN-only address you get the fallback: a home-screen icon that opens in Chrome rather than a
> separate app window. **The offline behaviour is unaffected** — the service worker is what caches
> the app, and that now works. If a standalone app window matters to you, that is an argument for
> the Tailscale route below, though it is reachable only from your tailnet too.

Check it the honest way: open the app, turn on flight mode, open it again. It should start and show
your data. A trip added offline uploads on the next sync.

## Renewing

The server certificate is valid for 825 days, the CA for 10 years. When the server certificate runs
out, re-run the same command and recreate the container:

```bash
./deploy/tls/make-cert.sh grocerytracker.fritz.box 192.168.1.50
docker compose up -d --force-recreate web
```

Devices need nothing — they trust the CA, not the certificate. Check the date any time with:

```bash
openssl x509 -in deploy/certs/grocerytracker.crt -noout -dates -ext subjectAltName
```

## Alternative: Tailscale, no certificate to install

If installing a CA on every device sounds like too much, Tailscale issues a *publicly* trusted
certificate for a `*.ts.net` name, so nothing has to be trusted by hand:

```bash
curl -fsSL https://tailscale.com/install.sh | sh
sudo tailscale up
tailscale status          # the Pi's full name, e.g. pi.tail1234.ts.net
sudo tailscale cert pi.tail1234.ts.net
```

That writes a `.crt` and `.key` you can point nginx at exactly like the ones above (`tailscale
serve` can also front the app directly). The trade-off: it needs a Tailscale account, and every
device has to be on your tailnet — in exchange the app also works away from home, which the LAN-only
setup does not.

## When it does not work

| Symptom | Cause |
|---|---|
| `NET::ERR_CERT_AUTHORITY_INVALID` | The CA is not installed on that device, or Chrome/Firefox has its own store you have not imported into. |
| `NET::ERR_CERT_COMMON_NAME_INVALID` | You are using an address that is not in the certificate's SAN list. Re-run the script with it included. |
| Site loads, but "Add to Home screen" offers only a bookmark | Expected on a LAN-only address — see the note in step 5. Offline still works. |
| App works in the browser but not offline | The service worker never registered. Check `isSecureContext` is `true` in the console, and that the page was loaded over HTTPS rather than HTTP. |
| `nginx: [emerg] cannot load certificate` | The key is unreadable inside the container. `ls -l deploy/certs` — the mount is read-only, so this is a permissions problem on the host. |
| Everything works, but the app is empty | A different origin has its own IndexedDB. The data is on the server; let it sync. |
