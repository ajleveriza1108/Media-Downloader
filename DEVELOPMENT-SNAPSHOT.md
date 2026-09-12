# MediaDock R1.6.59 development snapshot

R1.6.59 makes torrent Down/Up/ETA react like the normal Downloader by measuring consecutive cumulative byte snapshots in the WPF row at a 150 ms refresh cadence. It also accepts .magnet files, adds direct torrent/magnet streaming from the Stream workspace, and preserves the adjustable media grabber, durable torrent persistence, first-load discovery, and no-force publication safeguards.

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
- Torrent progress, Down/Up, peers, seeds, ETA and ratio refresh on a 150 ms UI cadence; visible speed is calculated from consecutive cumulative byte snapshots like the normal Downloader, while expensive peer enumeration and tracker scrape stay off the hot status path.
- TorrentHost still samples DataBytesReceived/DataBytesSent independently, while WPF computes the visible rate from consecutive Downloaded/Uploaded snapshot deltas; engine/host rates are first-snapshot warm-up hints only.
- Peer totals use the greater of current open connections and the cached background peer enumeration, preventing a valid connection from being hidden by one lagging counter.
- ETA is calculated from the measured effective download rate, and the footer mirrors down/up, peers, seeds, ETA, ratio and received bytes from the same snapshot.
- Isolated TorrentHost, MonoTorrent 3.9 alpha, persistent queue/settings, selective files and torrent streaming remain intact.

Commercial/private licensing, trial, entitlement, updater implementation, backend secrets, customer state and installer implementation remain excluded from this public source repository.
