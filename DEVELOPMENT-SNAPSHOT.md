# MediaDock R1.6.24 development snapshot

Status: R1.6.24 Windows license-persistence repair built, runtime-smoke-tested, published, and activated on the stable updater channel.

Current commercial release: R1.6.24 - License Activation Persistence Repair.

## Customer distribution

- MediaDock-Setup-R1.6.24.exe
- SHA-256: e0a610e01924bab8a846b516d3ae6e517446322b3c879a596518327d3cd5d0ee
- Stable manifest: https://raw.githubusercontent.com/ajleveriza1108/MediaDock-Release/main/latest-stable.json

## R1.6.24 acceptance gates

- strict .NET build: PASS, 0 warnings, 0 errors
- self-contained win-x64 publish: PASS
- engine/updater contract test: PASS
- Settings construction smoke: PASS
- main-window plus Settings layout smoke: PASS
- visible BuildIdentity smoke: PASS
- isolated Credential Manager + DPAPI persistence round-trip: PASS
- licensed UI + Trial-binding rebound smoke: PASS
- startup validation / activation / release serialization source gate: PASS
- protected production-store write/read-back verification preserved: PASS
- future R1.6.25 updater receiving self-test: PASS
- existing R1.6.23 updater freshness/no-cache contract preserved: PASS
- same Inno AppId and per-user MediaDock install path: PASS
- exact GitHub Release installer download-back SHA verification: PASS

## Publication boundary

The commercial Settings, licensing, strict-trial, downloader, and updater implementation remains private/local and is not published in this public source repository.

The public repository records the verified development/release snapshot only.