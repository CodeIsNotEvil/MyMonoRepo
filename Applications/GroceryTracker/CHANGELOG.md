# Changelog

What changed in each GroceryTracker release, newest first. The download page and the GitHub release
notes are built from this file, so write for users: what they notice, not how it was done.

Format: a `## <version> - <yyyy-mm-dd>` heading per release (the version must match `<Version>` in
`Directory.Build.props`), then `### Added`, `### Changed` or `### Fixed` with one bullet per change.

## 0.2.0 - 2026-09-30

### Added
- Settings → About shows the version you are running, with a link to this list of changes. After an
  update it tells you whether your browser has picked up the new version yet.

## 0.1.0 - 2026-09-27

### Added
- First release: an offline-first grocery spend tracker that installs as an app from the browser.
  Trips, a spending dashboard, balances and settlements between people, and CSV import all work
  without a connection and sync to your own server when it's reachable.
- Prebuilt images for amd64 and arm64 (Raspberry Pi) on GitHub's container registry, installed with
  one `compose.yaml`.
