# Changelog

## 1.4.2 — 2026-10-28

### Fixed

- Light-theme active selectors and primary buttons now use white text.
- The info hash on the cover card is white in every theme.
- A maximized borderless window respects the Windows taskbar work area.
- “Check for updates” now sits at the bottom of the sidebar, above the storage card.

### Added

- GitHub link with the GitHub icon next to the version in Settings.

## 1.4.1 — 2026-10-28

### Added

- The download folder chosen while adding a torrent is remembered and used as the starting folder for the next torrent.
- Moving an active torrent to a new folder also makes that folder the new default.

## 1.4.0 — 2026-10-28

### Added

- Automatic update checks against GitHub Releases with a configurable interval.
- Optional silent download, install and restart; when disabled, updates are checked and installed manually from the sidebar.
- "Check for updates" action in the left sidebar.

### Fixed

- Download traffic is green and upload traffic red in the light theme; the same semantic colors are used in the green theme.
- Upload chart stroke uses the same color family as the rest of the theme.
- Pausing a torrent now reports zero speed immediately in the UI and clears the graph.
- Light-theme primary buttons use a blue gradient with white text; selected chips are blue.
- Info hash is readable in the light theme.
- "Add by link" matches the other sidebar actions; the tiny brand subtitle was removed.

## 1.3.0 — 2026-10-28

### Added

- Automatic settings persistence; the settings screen no longer has Save/Cancel buttons and closes when you switch to another library view.
- Three themes: Midnight, Green and Light, switchable live from Settings.
- Per-torrent download/upload traffic chart in the details pane.
- “Waiting for peers” state for torrents that have reached the last pieces but are not progressing.

### Fixed

- Pause now clears the traffic graph immediately instead of leaving the previous speed plateau.
- Torrent row action buttons have their own column and no longer cover size/downloaded values.
- Library filter tabs are rounded rectangles instead of pills.
- The details-panel button uses a proper sidebar icon.
- Speed arrows in the title bar share the same baseline as the values.
- The Settings navigation item is no longer clipped at the bottom.
- Primary button shimmer is clipped to the button's rounded corners.
- Settings page can be closed by navigating to any library filter.

## 1.2.0 — 2026-10-27

### Fixed

- Progress is now normalized from MonoTorrent's 0–100 percentage to the UI's 0–1 fraction for torrents, files and peers.
- Opening a `.torrent` from Explorer now opens the add dialog with folder/file selection instead of silently adding it.
- Per-file move preserves the `<torrent name>` folder and survives a restart without re-downloading data.
- Per-torrent `0` speed limit now means "no per-torrent cap" instead of inheriting the global limit.

### Added

- Manual High/Normal/Low torrent queue with Queued status.
- Per-torrent download and upload speed limits.
- Move download folder during an active download.
- Post-completion PC action: Shut down, Sleep, Hibernate, Restart or Do nothing.
- Rectangular language selector.

## 1.1.0 — 2026-10-27

### Added

- English, Russian and Korean interface localization; English is the default.
- Live language switcher in Settings.
- Per-user Windows installer and a self-contained portable build.
- Localized README (EN/RU/KO), localized screenshots and download buttons.
- Explicit FastResume cache documentation (`cache\fastresume`) and restart notes.

### Changed

- All UI, engine errors and formatting strings now use the localization layer.
- Repository version bumped to 1.1.0; release assets use stable installer/portable names.

## 1.0.0 — 2026-10-27

First public release.

### Added

- Native Windows 11 torrent client on .NET 10 + WPF + MonoTorrent 3.0.2.
- Dark Fluent-inspired interface with animated gradient CTAs and aurora background.
- `.torrent` and magnet support, drag & drop, command line, single-instance forwarding.
- Library with search, state filters, sorting and per-torrent actions.
- Details panel: overview, files with priorities, trackers and peers.
- Global speed/connection limits, DHT, PEX, LSD, UPnP, MSE/PE.
- Session persistence, fast resume, safe restart after crash.
- System tray, completion/error notifications, optional Windows autostart.
- Token-based, path-safe file handling (traversal, UNC, ADS, reserved names).
- 24 xUnit tests, including local-engine add/pause/resume/remove and session restore.
- GitHub Actions CI and automated tagged release publishing.
