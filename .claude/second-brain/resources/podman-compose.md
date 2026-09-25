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

## Portability rules (see CLAUDE.md, "Containers: Docker and Podman")
Every compose file must work under both Docker and Podman:
- Compose spec only, and no dependency on `docker.sock`
- Fully qualified image names (`docker.io/library/...`)
- Host ports of 1024 or above for rootless Podman
- Named volumes, and no assumption of root on the host

## Known violations (as of 2026-09-25)
- [ ] `Applications/GroceryTracker/docker-compose.yml`: `postgres:16-alpine` is a short name
- [ ] `Applications/GroceryTracker/src/UI/Dockerfile`: `FROM nginx:alpine` is a short name
- [ ] `Configurations/Docker/Compose/Keycloak/compose.yaml`: `bitnami/postgresql`, `bitnami/keycloak` are short names

The `mcr.microsoft.com/...` images are already fully qualified.
