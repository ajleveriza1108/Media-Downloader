# MediaDock R1.6.46 development snapshot

R1.6.46 fixes peer connectivity and delivers theme-adaptive UI on top of the R1.6.45 torrent/network baseline.

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
- Isolated TorrentHost, MonoTorrent 3.9 alpha, persistent queue/settings, selective files and torrent streaming remain intact.

Commercial/private licensing, trial, entitlement, updater implementation, backend secrets, customer state and installer implementation remain excluded from this public source repository.
