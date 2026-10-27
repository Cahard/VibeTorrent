# Changelog

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
