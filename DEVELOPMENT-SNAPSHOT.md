# MediaDock R1.6.23 development snapshot

Status: R1.6.23 Windows updater-detection repair built, runtime-smoke-tested, published, and activated on the stable updater channel.

Current commercial release: R1.6.23 - Updater Detection + Build Identity Repair.

## Customer distribution

- MediaDock-Setup-R1.6.23.exe
- SHA-256: 9617daaabd71bf6b88380a1424446bfcd1e4d07731bb8e8b6a413a7b284aca34
- Stable manifest: https://raw.githubusercontent.com/ajleveriza1108/MediaDock-Release/main/latest-stable.json

## R1.6.23 acceptance gates

- strict .NET build: PASS, 0 warnings, 0 errors
- self-contained win-x64 publish: PASS
- engine/updater contract test: PASS
- Settings construction smoke: PASS
- main-window plus Settings layout smoke: PASS
- licensed UI + trial-unlock smoke: PASS
- stable manifest cache-busting URI: PASS
- Cache-Control no-cache/no-store request: PASS
- informational-version-first current version resolution: PASS
- updater decision diagnostics: PASS
- future R1.6.24 receiving self-test: PASS
- existing R1.6.22 Settings/license/trial features preserved: PASS
- same Inno AppId and per-user MediaDock install path: PASS
- exact GitHub Release installer download-back SHA verification: PASS

## Publication boundary

The commercial Settings, licensing, strict-trial, downloader, and updater implementation remains private/local and is not published in this public source repository.

The public repository records the verified development/release snapshot only.