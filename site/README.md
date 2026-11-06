# Site

The GitHub Pages site at https://codeisnotevil.github.io/MyMonoRepo/: a page promoting the apps
(`src/index.html`) and a download page with install guides (`src/download.html`), in the apps' own
look (Kode Mono, `#DE5833`, light and dark with the system). Plain HTML and CSS, no JavaScript and
nothing loaded from other sites.

The logos glint like the animated profile pictures in `Assets/c-logo/`, in CSS only and at the GIF's
pace: every logo about every four seconds, staggered, and not at all with reduced motion. The apps do
the same with their own logos (GroceryTracker's nav, LaunchHeim's sidebar). The glint layer is masked to the logo's shape, which browsers only allow for images from the
same origin. Preview over HTTP (`python3 -m http.server -d site/_site`), because from `file://`
the masks don't load and no glint shows.

```fish
site/build.py                                   # -> site/_site (git-ignored), then:
python3 -m http.server -d site/_site 8000       # open http://localhost:8000
site/build.py --releases-json sample.json       # preview the download page with made-up releases
```

`build.py` fills in `{{ placeholders }}` and `<!-- if:key --> … <!-- else --> … <!-- end -->` blocks
(which can nest), copies the logos and the font from their real homes (`Assets/`, the apps), and asks
the GitHub API for each app's newest release, including the SHA-256 GitHub stores for every file
(`digest`). Download links and checksums therefore follow releases without editing the pages. Before an app's first release, its section shows build instructions instead.

The download page shows one app at a time, picked by the address (`download.html#grocerytracker`, or
an anchor inside the section such as `#grocerytracker-changes`) through CSS `:target` and `:has()`.
With no anchor, every app shows only its header and a button that picks it. Link to an app's anchor,
not to the bare page, when the reader wants its downloads. Browsers without `:has()` show all in full.

LaunchHeim moved to its own repository and site on 2026-11-06
(https://codeisnotevil.github.io/LaunchHeim/, with its downloads, screenshots and privacy policy). Its
card on the front page and its section on the download page only point there now. The section and
`privacy.html` (a forward) stay because old addresses lead to them: installed copies up to 0.7.1 open
`download.html#launchheim`, and the Microsoft Store listing linked the privacy policy here. Its logo is
a copy in `src/img/`.

The build fails when a rendered page links to an `#anchor` it doesn't have, or opens more `<section>`s
than it closes. Balanced `if`/`end` markers alone didn't catch a template that lost a stretch in the
middle (2026-10-01), and the download page went live mixing the two apps.

Deployments also carry the Windows setups of the three newest LaunchHeim releases made here (up to
0.7.1; newer ones are mirrored by LaunchHeim's own site), at
`downloads/launchheim/<version>/LaunchHeim-<version>-win-x64-setup.exe` (`--mirror-installers`, checked
against GitHub's SHA-256). The Microsoft Store takes the installer by URL and refuses one that redirects,
which GitHub's release links always do. After a release, enter the new version's URL in Partner
Center; the file at a versioned URL never changes.

`.github/workflows/site.yml` builds it on pull requests and deploys it from `main`, after every
release (the release workflows start it, because releases made by a workflow trigger nothing on
their own) and on demand.
