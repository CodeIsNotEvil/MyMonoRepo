---
tags: [moc]
created: 2026-09-25
updated: 2026-10-16
status: active
---
# Index

The map of content. Keep it short: link to hub notes, not to every note.

## Projects
- [[grocerytracker]]: offline-first grocery spend tracker (.NET 10, Blazor WASM PWA, PostgreSQL)
- [[app-site]]: GitHub Pages site promoting the apps, with downloads and install guides
- [[launchheim]]: Valheim mod launcher with per-instance BepInEx (.NET 10, Qml.Net / Qt Quick)
- [[druckwerk]] (plan, working name): MIT C# libraries for barcodes, MDR labels, fiscal receipts and vector output, printing over any connection

## Areas
- [[raspberry-pi-homelab]]: the Debian 13 Pi that hosts GroceryTracker
- [[dotfiles]]: Nushell, Starship, fonts, Steam launch options
- [[kubernetes-learning]]: Minikube, kubectl, Helm experiments

## Resources
- [[offline-first-sync]]: the push/pull and stamp pattern, reusable beyond this app
- [[dotnet-gotchas]]: toolchain snags on Arch/CachyOS and with EF Core
- [[podman-compose]]: why it's always `podman compose`
- [[mdr-labelling]]: what the EU MDR/IVDR (UDI, ISO 15223-1, ISO 13485 validation) asks of label software
- [[fiscal-receipts]]: what KassenSichV, RKSV and friends ask of a receipt printer library

## Decisions
- [[0001-syncstamp-counter]]: sync cursors use a locked counter row, not timestamps
- [[0002-sqlite-for-sync-tests]]: sync tests run on in-memory SQLite, not the EF in-memory provider
- [[0003-shared-domain-analytics]]: the same analytics code runs on the server and in the browser
- [[0004-cine-root-namespace]]: all .NET namespaces start with `CINE.`; assembly names do not
- [[0005-launchheim-distro-packages-use-system-qt]]: pacman/deb/rpm builds use the distro's Qt 5.15
- [[0006-launchheim-windows-keeps-qml]]: Windows keeps the QML UI via a patched QmlNet.dll, no WinUI
- [[0007-per-app-releases-and-prebuilt-images]]: tag-prefixed releases, GHCR images, a download page that follows them
- [[0008-changelog-per-app]]: CHANGELOG.md per app drives release notes and the site's version history
- [[0009-launchheim-modpacks-are-r2z]]: LaunchHeim modpacks are r2modman `.r2z` files plus `launchheim.json`
- [[0010-launchheim-direct-play-uses-the-games-own-options]]: joining servers and opening worlds uses `+connect` and Valheim's PlayerPrefs, no plugin
- [[0011-launchheim-companion-syncs-packs-over-localsend]]: the Android companion (Kotlin) syncs `.r2z` packs and the server list over LocalSend
- [[0012-launchheim-windows-self-signed-code-signing]]: the Windows exe and own DLLs are Authenticode-signed with a self-signed certificate
- [[0013-launchheim-windows-setup-with-inno-setup]]: the Windows setup is Inno Setup; installed copies update by running the new setup
- [[0014-launchheim-steam-overlay]]: Steam's overlay via preload on Linux and `steam.exe -applaunch` on Windows
- [[0015-launchheim-steam-launch-options-wrap-steams-command]]: `launchheim steam %command%` in Valheim's launch options starts the last play from Steam
- [[0016-launchheim-instant-start-plugin]]: "Start instantly" is a BepInEx plugin pressing Start in Valheim's menus, opt-in per server or world
- [[0017-launchheim-keeps-achievements-with-the-games-own-switch]]: "Keep Steam achievements with mods" sets the dev command's character key, and only undoes its own
- [[0018-apps-get-their-own-repo-at-1-0]]: apps stay in the monorepo while 0.x and move to their own repository at 1.0

## Archive
- [[shoppingmanager]]: the predecessor to GroceryTracker

## Inbox
Untriaged captures live in `inbox/`. Empty it now and then.
