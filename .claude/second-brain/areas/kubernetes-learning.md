---
tags: [area, learning, kubernetes]
created: 2026-09-25
updated: 2026-09-25
status: idea
---
# Kubernetes learning

Notes and manifests live in `Configurations/Kubernetes/`: kubectl cheatsheet, namespaces, and an nginx deployment.

- The `Helm/ShoppingManager` chart is a leftover from [[shoppingmanager]] and doesn't deploy
  [[grocerytracker]]. Either port it or delete it.
- `Configurations/Docker/Compose/Keycloak/compose.yaml` is a possible identity provider if auth is ever needed.
  It runs the official `quay.io/keycloak/keycloak:26.7` in `start-dev` mode (HTTP on :8080), verified
  with Podman on 2026-09-25. Its Postgres is still `docker.io/bitnami/postgresql:latest`, which Bitnami
  no longer updates. Swapping it for `docker.io/library/postgres` changes the env vars and the data path,
  so it needs a fresh volume.
