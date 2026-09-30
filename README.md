# My MonoRepo

This is my learning repository. Since 2025 it has held my configurations, scripts and whatever I am
trying to understand at the moment. That's what it is, and that's what it will stay.

Some of what started here as practice grew into applications that work well enough for anyone to use.
They are free, MIT-licensed and released properly, but they still live here next to the experiments,
because building them is how I learn.

**Looking for the apps? Go to the website: [codeisnotevil.github.io/MyMonoRepo](https://codeisnotevil.github.io/MyMonoRepo/)**.
It has the downloads, step-by-step install guides and what changed in each version, and you don't need
a GitHub account or any GitHub knowledge to use it.

## Applications

| App | What it is | Get it |
|---|---|---|
| [GroceryTracker](Applications/GroceryTracker) | Tracks household grocery spending and who owes whom. An offline-first web app (Blazor WebAssembly PWA, ASP.NET Core, PostgreSQL) that runs on your own server, even a Raspberry Pi. | [Download and guide](https://codeisnotevil.github.io/MyMonoRepo/download.html#grocerytracker) |
| [LaunchHeim](Applications/LaunchHeim) | A Valheim mod launcher. Every modpack lives in its own instance with its own BepInEx, and the game install stays vanilla. .NET with a Qt Quick UI, for Linux and Windows. | [Download and guide](https://codeisnotevil.github.io/MyMonoRepo/download.html#launchheim) |

Each app's README explains how it is built and why it works the way it does.

## Everything else

| Folder | What's in it |
|---|---|
| [`Configurations/`](Configurations) | My dotfiles and setups: Nushell and terminal tools, Steam launch options, Docker Compose snippets, and Kubernetes and Helm experiments. |
| [`Scripts/`](Scripts) | Small tools: the logo and font generators behind the apps' look, the changelog reader the releases use, and an old .NET project scaffolder. |
| [`Assets/`](Assets) | Shared logos. |
| [`site/`](site) | The source of the [website](https://codeisnotevil.github.io/MyMonoRepo/). |
| `.claude/second-brain/` | Notes on why things are the way they are: decisions, gotchas and a journal. |

Things I've tried and moved on from stay in the history. ShoppingManager, for example, was the first
attempt at what became GroceryTracker.

## How this repo works

It's a personal repo, so it follows my habits, not a team's:

- **`main` is where work lands.** It's protected, so its history is never rewritten or force-pushed,
  but it moves with whatever I'm learning at the moment. Don't build on a random commit; use the
  releases.
- **Releases are per app.** A tag like `launchheim-v0.2.0` or `grocerytracker-v0.2.0` builds and
  publishes that app, and each app keeps a `CHANGELOG.md`. Only tagged releases are meant for use.

## License

GroceryTracker and LaunchHeim are MIT-licensed; see the `LICENSE` in each app's folder. LaunchHeim
also ships third-party software under its own licenses, listed in its
[`THIRD-PARTY-NOTICES.txt`](Applications/LaunchHeim/THIRD-PARTY-NOTICES.txt). Everything else here is
my personal setup, shared as it is.
