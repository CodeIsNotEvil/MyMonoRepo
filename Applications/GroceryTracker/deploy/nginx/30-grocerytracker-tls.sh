#!/bin/sh
# Turns the HTTPS server block on only when a certificate is actually mounted.
#
# The nginx image runs every executable /docker-entrypoint.d/*.sh at start, in name order. This one
# comes after the stock 20-envsubst-on-templates.sh, so the HTTP config already exists by now.
# Shipping tls-server.conf as a plain config instead would make nginx refuse to start on every
# machine without a certificate, which is the normal case for local development.

set -e

cert=/etc/nginx/certs/grocerytracker.crt
key=/etc/nginx/certs/grocerytracker.key

if [ ! -r "$cert" ] || [ ! -r "$key" ]; then
  echo "$0: no certificate at $cert — serving HTTP only (see deploy/https.md)"
  exit 0
fi

# Only the resolver is substituted; leaving $host, $uri and friends alone is the whole point of
# naming the variables explicitly here.
envsubst '${NGINX_LOCAL_RESOLVERS}' \
  < /etc/nginx/grocerytracker/tls-server.conf.template \
  > /etc/nginx/conf.d/tls-server.conf

echo "$0: certificate found — also serving HTTPS on 443"
