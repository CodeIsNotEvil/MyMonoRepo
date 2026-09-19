#!/usr/bin/env bash
#
# Creates a private certificate authority (once) and a server certificate for this machine, so the
# app can be served over HTTPS on a home network. Install the CA on every device that should trust
# it; the server certificate itself is never installed anywhere. See deploy/https.md.
#
#   ./deploy/tls/make-cert.sh grocerytracker.fritz.box 192.168.1.50
#
# Pass every name and address the app will be opened by. A browser checks the URL against the
# certificate's SAN list and nothing else, so an address you leave out here will not work — and
# adding one later means a new certificate (though the same CA, so devices need no changes).

set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
out="$(cd "$here/.." && pwd)/certs"

ca_key="$out/ca.key"
ca_crt="$out/ca.crt"
crt="$out/grocerytracker.crt"
key="$out/grocerytracker.key"

# 825 days is the longest leaf certificate Apple's platforms accept; Android and Chrome are happy
# with it too. The CA itself is long-lived, because replacing it means visiting every device again.
leaf_days=825
ca_days=3650

if [ $# -eq 0 ]; then
  echo "usage: $0 <hostname|ip> [more names or ips...]" >&2
  echo "example: $0 grocerytracker.fritz.box 192.168.1.50" >&2
  exit 64
fi

mkdir -p "$out"

# SANs: anything that parses as a bare IPv4/IPv6 literal goes in as IP, everything else as DNS.
sans=""
for name in "$@"; do
  if [[ "$name" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ || "$name" == *:*:* ]]; then
    sans+="IP:$name,"
  else
    sans+="DNS:$name,"
  fi
done
sans="${sans%,}"

if [ ! -f "$ca_key" ] || [ ! -f "$ca_crt" ]; then
  echo "Creating a new certificate authority in $out"
  openssl req -x509 -newkey rsa:2048 -sha256 -days "$ca_days" -nodes \
    -keyout "$ca_key" -out "$ca_crt" \
    -subj "/CN=GroceryTracker local CA/O=GroceryTracker" \
    -addext "basicConstraints=critical,CA:TRUE,pathlen:0" \
    -addext "keyUsage=critical,keyCertSign,cRLSign" 2>/dev/null
  chmod 600 "$ca_key"
else
  echo "Reusing the certificate authority already in $out"
fi

echo "Issuing a server certificate for: $sans"

csr="$(mktemp)"
ext="$(mktemp)"
trap 'rm -f "$csr" "$ext"' EXIT

cat > "$ext" <<EOF
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=$sans
EOF

openssl req -newkey rsa:2048 -sha256 -nodes -keyout "$key" -out "$csr" \
  -subj "/CN=${1}/O=GroceryTracker" 2>/dev/null

openssl x509 -req -in "$csr" -CA "$ca_crt" -CAkey "$ca_key" -CAcreateserial \
  -out "$crt" -days "$leaf_days" -sha256 -extfile "$ext" 2>/dev/null

chmod 600 "$key"
chmod 644 "$crt" "$ca_crt"

echo
echo "Wrote:"
echo "  $ca_crt   <- install this on every phone and laptop"
echo "  $crt      <- served by nginx"
echo "  $key      <- stays on this machine, never leaves it"
echo
openssl x509 -in "$crt" -noout -dates -ext subjectAltName
echo
echo "Next: restart the stack so nginx picks it up —  docker compose up -d --force-recreate web"
