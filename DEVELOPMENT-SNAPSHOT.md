# MediaDock R1.6.15 development snapshot

Status: **Windows runtime verification pending**.

Current development package: **R1.6.15 — Internal Media Capture Streaming + Resolution + Full Screen + MP4/MP3**.

## Cumulative behavior

- internal WebView2 Stream media-capture surface;
- detection of direct media, HLS and DASH network candidates;
- JavaScript-created `<video>` isolation where supported;
- Auto internal detector plus direct combined-resolution choices;
- Stream Full Screen with Esc restore;
- MP4 and MP3 actions routed through the normal persistent queue;
- whole-window dragged-link acceptance;
- TXT/CSV batch URL import;
- strict 5-video + 5-MP3 unlicensed trial model.

The rejected R1.6.13.2 LibVLC branch is not part of the active lineage.

## Package verification already completed

- static validation: 62/62;
- exact R1.6.14 predecessor: 43/43;
- R1.6.15 managed source/assets: 47/47;
- release manifest: 142/142;
- package SHA-256: `926786d101c900f4d1cea2bd7cddb4643ee3493a7e7c1c9fae5c9a34f47c5f01`.

## Publication boundary

The current commercial source is intentionally withheld from this public repository while MediaDock licensing is being implemented.

Do not publish license-key inventories, backend secrets, Payhip credentials, customer records, activation/device records, runtime state, logs, cookies, or build caches here.

R1.6.15 must not be called stable until the guarded Windows PowerShell 5.1 installer/build/runtime gates and live Stream/Download interactions pass on the target Windows machine.
