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
  It runs the official `quay.io/keycloak/keycloak:26.7` in `start-dev` mode (HTTP on :8080) on the
  official `docker.io/library/postgres:17-alpine`. It was verified with Podman on 2026-09-25. The data
  volume is `keycloak-postgres-data`. The old Bitnami `Keycloak_PostgreSQL` volume has an incompatible
  layout and isn't reused.
