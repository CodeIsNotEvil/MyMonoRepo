---
tags: [resource, containers, gotcha]
created: 2026-09-25
updated: 2026-09-25
status: active
---
# podman compose, never docker-compose

`podman compose` picks a provider (a real `docker-compose` binary if one is installed) and points it
at Podman's socket through `DOCKER_HOST` for that one call. A bare `docker-compose` never gets that
socket, so it looks for a Docker daemon that doesn't exist.

One-time setup: `sudo pacman -S podman docker-compose` and `systemctl --user enable --now podman.socket`.
