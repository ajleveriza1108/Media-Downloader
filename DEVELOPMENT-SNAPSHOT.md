# MediaDock R1.6.50 development snapshot

R1.6.50 replaces the fragile multi-step BAT preflight with a persistent visible launcher console, prefers the real PowerShell 7 installation before WindowsApps/PATH fallback, and carries forward the executable identity fix plus measured live torrent speed, ETA, peers/seeds, persistent queue, peer-connectivity, and theme-adaptive fixes.

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
- Torrent queue/session persistence is crash-resistant, restores from session backup, and never erases unresolved entries after a failed startup restore.
- Torrent progress, speed, peers, seeds, ETA and ratio refresh on a 350 ms UI cadence; expensive peer enumeration and tracker scrape run off the hot status path.
- Download/upload speed is measured from DataBytesReceived/DataBytesSent over a monotonic live sample window; MonoTorrent aggregate and per-peer monitor rates are fallback signals only.
- Peer totals use the greater of current open connections and the cached background peer enumeration, preventing a valid connection from being hidden by one lagging counter.
- ETA is calculated from the measured effective download rate, and the footer mirrors down/up, peers, seeds, ETA, ratio and received bytes from the same snapshot.
- Isolated TorrentHost, MonoTorrent 3.9 alpha, persistent queue/settings, selective files and torrent streaming remain intact.

Commercial/private licensing, trial, entitlement, updater implementation, backend secrets, customer state and installer implementation remain excluded from this public source repository.
