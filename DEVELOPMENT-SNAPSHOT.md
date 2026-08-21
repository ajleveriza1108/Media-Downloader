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

The existing Buy/Payhip license action remains usable before trial exhaustion. R1.6.19 also provides a dedicated Enter Serial action that is visible before the 5+5 trial is exhausted, so a customer can paste the serial/license key received from Payhip at any time.

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
- always-available Payhip serial-entry runtime contract: PASS
- Inno Setup trial installer compile: PASS
- staged EXE SHA-256: bb512b3e24975a9142b08429cdec4990cff964f91fe7c619e6d97d8027758157
- installer SHA-256: c6a64a24ab48cf1b01b21ce869605bf949bb92232f6a62794253c1b929b27d2d

## Publication boundary

Current commercial trial/licensing/updater source is not published in this repository.

The stable pointer remains inactive until the exact installer passes installed update-cycle verification and is hosted as a MediaDock-Release GitHub Release asset.