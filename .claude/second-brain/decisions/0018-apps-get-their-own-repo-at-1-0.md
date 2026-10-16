---
tags: [decision, repository, releases, druckwerk]
created: 2026-10-16
updated: 2026-10-16
status: active
supersedes:
---
# 0018: Apps live in the monorepo while they're 0.x, and get their own repository at 1.0

**Context.** Planning [[druckwerk]] raised the question of whether a library meant for everyone on
NuGet belongs in a personal monorepo or in its own repository.

**Decision.** The owner's rule (2026-10-16): an application stays in `Applications/` as long as its
version is `0.x`. Only an application that reaches 1.0 earns its own repository. This applies to
every app, not only Druckwerk.

**Alternatives.**
- *Own repository from the start.* Rejected: most projects never reach 1.0, and early on the shared CI
  conventions, the site and the second brain are worth more than a clean home for outside
  contributors.
- *Stay in the monorepo forever.* Rejected: a stable app with users deserves its own issues,
  releases and "latest" release flag (the monorepo can only mark one release as latest, see
  [[0007-per-app-releases-and-prebuilt-images]]).

**Consequences.**
- The move happens as part of the 1.0 release. Use `git subtree split --prefix Applications/<App>` (or
  `git filter-repo --subdirectory-filter`) so the app keeps its history, then leave a short README in
  the old folder pointing at the new repository instead of deleting the folder silently.
- Keep each app self-contained while it lives here (its own `Directory.Build.props`, `LICENSE`,
  `CHANGELOG.md`, workflow, as [[0004-cine-root-namespace]] already requires), so the split is
  mechanical.
- Things that point into the monorepo have to move along: workflow files, release tags
  (`<app>-vX.Y.Z` become `vX.Y.Z` there), the site's download page, package `RepositoryUrl`s and the
  app's second-brain notes (or a link to where they went).
- GroceryTracker (0.2.0) and LaunchHeim (0.7.1) fall under the same rule.

Related: [[druckwerk]], [[0007-per-app-releases-and-prebuilt-images]], [[0008-changelog-per-app]]
