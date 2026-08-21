# MediaDock R1.6.19 development snapshot

Status: Windows installer candidate build verified; installed update-cycle and stable release activation remain pending.

Current development package: R1.6.19 - Clean License Client + Installer-Only Updater.

## Customer distribution

MediaDock now has one Windows customer distribution format:

- MediaDock-Setup-R1.6.19-Trial.exe

The installed application starts in trial mode when no valid license is active:

- 5 successful Video outputs;
- 5 successful MP3 outputs;
- Stream unavailable while unlicensed;
- Convert unavailable while unlicensed.

Buy MediaDock License and Enter Serial remain available before trial exhaustion. The private Windows build contains a direct license client for Admin/customer activation, validation, protected local persistence, and device release.

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
- live private licensing backend verification: PASS
- direct clean LicenseService + protected persistence source contract: PASS
- centered Enter Serial + Release This Device UI contract: PASS
- strict unlicensed 5+5 entitlement bridge contract: PASS
- Inno Setup trial installer compile: PASS
- staged EXE SHA-256: c18931ebb0d86c23f83b104c47ba5a7ebd9211ff7d69fd837724a368d02ca301
- installer SHA-256: 72076ad83aedfc55dc126c9c1dd98e018389e1fb934af40a30ae42490099ed89

## Publication boundary

Current commercial trial/licensing/updater source is not published in this repository.

The stable pointer remains inactive until the exact installer passes installed update-cycle verification and is hosted as a MediaDock-Release GitHub Release asset.