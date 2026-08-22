# MediaDock R1.6.25 development snapshot

Status: R1.6.25 Windows full-license Stream/Convert unlock repair built, runtime-smoke-tested, published, and activated on the stable updater channel.

Current commercial release: R1.6.25 - Full License Feature Unlock Repair.

## Customer distribution

- MediaDock-Setup-R1.6.25.exe
- SHA-256: ff868505fe5cf400d79071031c3dece43fcf8c90723936ed99aea181bc6e2022
- Stable manifest: https://raw.githubusercontent.com/ajleveriza1108/MediaDock-Release/main/latest-stable.json

## R1.6.25 acceptance gates

- strict .NET build: PASS, 0 warnings, 0 errors
- self-contained win-x64 publish: PASS
- engine/updater contract test: PASS
- Settings construction smoke: PASS
- main-window plus Settings layout smoke: PASS
- visible BuildIdentity R1.6.25 smoke: PASS
- protected license persistence smoke: PASS
- full licensed UI smoke: PASS
- Stream enabled while licensed: PASS
- Convert enabled while licensed: PASS
- Stream/Convert Click or Command entry-point source gate: PASS
- licensed Command.CanExecute gate: PASS
- trial default Stream/Convert lock: PASS
- future R1.6.26 updater receiving self-test: PASS
- updater freshness/no-cache contract preserved: PASS
- same Inno AppId and per-user MediaDock install path: PASS
- exact GitHub Release installer download-back SHA verification: PASS

## Publication boundary

The commercial Settings, licensing, strict-trial, downloader, converter, streamer, and updater implementation remains private/local and is not published in this public source repository.

The public repository records the verified development/release snapshot only.