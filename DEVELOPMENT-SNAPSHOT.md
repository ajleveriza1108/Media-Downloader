# MediaDock R1.6.53 development snapshot

R1.6.53 enlarges the themed Add New Torrent content selector and makes torrent persistence self-contained. MediaDock now stores canonical .torrent metadata under its persistent TorrentClient state, embeds normal-sized .torrent metadata in session.json as a recovery copy, restores the queue after the main WPF window is loaded, and migrates legacy TorrentHost metadata. It carries forward first-load start/discovery, measured live speed/ETA/peer telemetry, fast peer connectivity, the persistent visible launcher, and no-force publication safeguards.

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
