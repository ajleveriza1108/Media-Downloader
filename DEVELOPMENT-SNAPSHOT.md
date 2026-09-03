# MediaDock R1.6.55 development snapshot

R1.6.55 fixes live torrent telemetry so Down/Up are driven by fresh byte deltas instead of stale monitor maxima, and hardens loaded-torrent persistence against release smoke tests, early/forced close, and startup restore races. It keeps canonical .torrent metadata, embedded session recovery, the larger Add New Torrent selector, first-load start/discovery, fast peer connectivity, the persistent visible launcher, and no-force publication safeguards.

UI/theme changes:
- The update confirmation is a MediaDock-owned WPF dialog instead of a native Windows MessageBox.
- Normal in-app MessageBox flows use the same active MediaDock theme; the fatal startup fallback remains native by design.
- Torrent workspace, queue, context menu, empty state, footer, progress and toolbar now use DynamicResource theme brushes instead of fixed dark colors.
- Torrent toolbar uses responsive wrapping instead of rigid fixed columns, preventing blank/clipped actions when the window or text scale changes.
- Torrent Files, Torrent Details, Torrent Settings and the universal Download File dialog follow the current theme and responsive work area.

Torrent peer connectivity changes:
- TorrentHost always binds a real automatic peer listener in normal runtime, independent of router mapping, so tracker announces carry a valid peer port.
- Trackerless magnets get immediate public tracker bootstrap while metadata is pending; known private torrents never receive public fallback injection.
- TorrentEvent.Started is emitted once per run; recovery uses normal announces and shorter non-blocking DHT waits.
- Peer discovery telemetry identifies tracker/DHT/PEX/local sources, listener readiness and connection failures.
- Torrent queue/session persistence is crash-resistant, restores from session backup, primes saved entries before UI restore, ignores noninteractive release smoke tests, and commits Add Torrent immediately with write-through session durability.
- Torrent progress, speed, peers, seeds, ETA and ratio refresh on a 350 ms UI cadence; expensive peer enumeration and tracker scrape run off the hot status path.
- Download/upload speed is sampled from fresh DataBytesReceived/DataBytesSent deltas every live status interval; engine/per-peer rates are first-frame hints only so stale monitor values cannot pin the UI.
- Peer totals use the greater of current open connections and the cached background peer enumeration, preventing a valid connection from being hidden by one lagging counter.
- ETA is calculated from the measured effective download rate, and the footer mirrors down/up, peers, seeds, ETA, ratio and received bytes from the same snapshot.
- Isolated TorrentHost, MonoTorrent 3.9 alpha, persistent queue/settings, selective files and torrent streaming remain intact.

Commercial/private licensing, trial, entitlement, updater implementation, backend secrets, customer state and installer implementation remain excluded from this public source repository.
