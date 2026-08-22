# MediaDock R1.6.26 development snapshot

Status: verified stable functional primary-navigation repair.

Customer release:
- MediaDock-Setup-R1.6.26.exe
- SHA-256: 14c09e2413f5c52da334e9235f6ea11e0e60c1f62fb08e4fe1c44b71d48306f0

Acceptance gates:
- strict .NET build: 0 warnings / 0 errors
- Download/Stream/Convert real Button.ClickEvent smoke: PASS
- expected primary view visibility after every click: PASS
- Stream primary view wired-action source gate: PASS
- Convert primary view wired-action source gate: PASS
- licensed Stream/Convert enablement: PASS
- Settings/layout/build-identity/license-persistence smokes: PASS
- future R1.6.27 updater receiving self-test: PASS
- exact GitHub Release installer download-back SHA: PASS

Commercial implementation remains private/local.