---
tags: [archive, project]
created: 2026-09-25
updated: 2026-09-25
status: done
---
# ShoppingManager (replaced)

The predecessor to [[grocerytracker]]. It was removed in commit `9f5a014`.

Leftovers still in the repo:
- `Scripts/dotnet_project.sh`: the scaffolder that generated its Api/Application/Domain/Infrastructure/UI
  layout. Its paths are hardcoded to `~/Repositories/MyMonoRepo`, while the repo now lives at `~/repos/MyMonoRepo`.
- `Configurations/Kubernetes/Helm/ShoppingManager/`. See [[kubernetes-learning]].
