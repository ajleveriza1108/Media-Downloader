# MediaDock R1.6.19 development snapshot

Status: Windows installer candidate build verified; installed update-cycle and stable release activation remain pending.

Current development package: R1.6.19 - Installer-Only Release Updater.

## Customer distribution

MediaDock now has one Windows customer distribution format:

- MediaDock-Setup-R1.6.19-Trial.exe

The installed application starts in trial mode when no valid license is active:

- 5 successful Video outputs;
- 5 successful MP3 outputs;
- Stream unavailable while unlicensed;
- Convert unavailable while unlicensed.

The existing License access entry remains usable before trial exhaustion, so a customer can buy or activate at any time.

## Stable updater contract v3

The installed commercial application reads only:

https://raw.githubusercontent.com/ajleveriza1108/MediaDock-Release/main/latest-stable.json

It accepts an update only when the manifest is schema version 3, identifies MediaDock/stable, is newer than the installed version, has stable/runtime/installer/updater gates enabled, points to an HTTPS installer under the official MediaDock-Release GitHub Releases path, and supplies a valid SHA-256.

The downloaded installer is independently hashed before it can launch.

latest-development.json never triggers customer updates.

## Windows candidate verification

- .NET restore: PASS
- .NET Release build: PASS
- self-contained win-x64 publish: PASS
- engine + installer-updater contract test: PASS
- GUI startup smoke: PASS
- Inno Setup trial installer compile: PASS
- staged EXE SHA-256: fd610f7eb075f7074b47f771be640b702d9afaee046ea8366360c4d4fa9fb98d
- installer SHA-256: 0b729e8101e2f9720cfb1b90bb97b32333c086c8c793c654bcb8bd345c07e033

## Publication boundary

Current commercial trial/licensing/updater source is not published in this repository.

The stable pointer remains inactive until the exact installer passes installed update-cycle verification and is hosted as a MediaDock-Release GitHub Release asset.