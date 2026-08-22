# MediaDock R1.6.22 development snapshot

Status: R1.6.22 Windows installer built, Settings runtime smoke-tested, published, and activated on the stable updater channel.

Current commercial release: R1.6.22 - Settings Crash + Remaining Mojibake Repair.

## Customer distribution

- MediaDock-Setup-R1.6.22.exe
- SHA-256: e3e306afef103058313ea41df021744bb78d8d6519fafcb5753e5bcd85b739e4
- Stable manifest: https://raw.githubusercontent.com/ajleveriza1108/MediaDock-Release/main/latest-stable.json

## R1.6.22 acceptance gates

- strict .NET build: PASS, 0 warnings, 0 errors
- self-contained win-x64 publish: PASS
- engine/updater contract test: PASS
- main-window startup smoke: PASS
- Settings construction smoke: PASS
- Settings click exception containment/logging: PASS
- Settings InitializeComponent logging: PASS
- crash log path LocalAppData\AJCoder\MediaDock\Logs: PASS
- risky R1.6.21 Settings scrollbar template removed: PASS
- visible Settings controls normalized to ASCII: PASS
- rendered Trial footer normalized as exact ASCII ' | ' without replacing its WPF binding: PASS
- licensed entitlement bypasses 5+5 enforcement: PASS
- same Inno AppId and per-user MediaDock install path: PASS
- future R1.6.23 update self-test: PASS
- exact GitHub Release installer download-back SHA verification: PASS

## Publication boundary

The commercial Settings, licensing, strict-trial, and updater implementation remains private/local and is not published in this public source repository.

The public repository records the verified development/release snapshot only.