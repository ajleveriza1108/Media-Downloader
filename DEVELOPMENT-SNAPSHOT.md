# MediaDock R1.6.44 development snapshot

Status: customer/private Windows build and runtime candidate validated by the guarded R1.6.44 developer pipeline. The public source remains intentionally sanitized of commercial/private implementations and is compiler-delta validated against the verified R1.6.41 public baseline before publication.

R1.6.44:
- Renames the former Download workspace to Media for the media queue/library workflow.
- Adds a universal Downloader for browser-intercepted and pasted direct files.
- Adds an IDM-style Download File dialog with category, filename, destination, remembered category folder, Download Later, Start Download, and Cancel.
- Supports resumable HTTP Range transfers through .mediadock.part files and up to four concurrent general downloads.
- Adds Chrome/Edge/Brave Manifest V3 native messaging, ordinary download interception, and the floating media grabber.
- Keeps media-engine analysis for page/media URLs and separates general file transfers from the Media queue.
- Publishes the developer extension under browser-extension/MediaDock.
- Adds a dedicated Torrent workspace powered by MonoTorrent 3.0.2 with magnet/.torrent support, DHT/PEX, fast resume, UPnP/NAT-PMP, pause/resume/stop/recheck, file selection/priorities, peer/rate/progress telemetry, and local HTTP streaming.
- Stream workspace accepts magnet links or .torrent files through the Torrent / Magnet action and plays the selected torrent media inside MediaDock.

Commercial/private licensing, trial, entitlement, updater implementation, backend secrets, customer state, and installer implementation remain excluded from the public source repository.
