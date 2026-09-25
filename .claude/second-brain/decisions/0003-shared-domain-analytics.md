---
tags: [decision, architecture]
created: 2026-09-25
updated: 2026-09-25
status: active
---
# 0003: The same analytics code runs on the server and in the browser

**Context.** The dashboard and balance must work offline and show the same numbers as the server.

**Decision.** `Domain/Analytics/SpendAnalyzer` and `Domain/Balance/BalanceCalculator` have no EF or
HTTP dependencies. The server's `AnalyticsService` and the PWA both call them.

**Consequences.** Offline figures match the server's by construction. `Domain` must stay free of
infrastructure dependencies. Adding one would break the WASM client.
