# MediaDock R1.6.27 development snapshot

Status: verified stable compact fixed-window GUI polish.

Customer release:
- MediaDock-Setup-R1.6.27.exe
- SHA-256: 989ea6a152c272a3d1c0c51e1d5a222db4e2d1987ae8d7bd8acfe532fec847c7

Acceptance gates:
- strict .NET build: 0 warnings / 0 errors
- fixed 1240x700 main viewport: PASS
- UTF-8 source round-trip + targeted mojibake repair fixtures: PASS
- UI source mojibake scan: PASS
- Download visible-button no-crop/no-overlap geometry: PASS
- Stream real Click + visible-button geometry: PASS
- Convert real Click + visible-button geometry: PASS
- Settings fixed 700x540 cleanup: PASS
- Support & Diagnostics removed: PASS
- Open Diagnostics removed: PASS
- obsolete Always open maximized removed: PASS
- duplicate lower Settings X removed: PASS
- license buttons compact/contextual: PASS
- Settings visible-button no-crop/no-overlap geometry: PASS
- license persistence / licensed UI / build identity smokes: PASS
- future R1.6.28 updater receiving self-test: PASS
- exact GitHub Release installer download-back SHA: PASS

Commercial implementation remains private/local.