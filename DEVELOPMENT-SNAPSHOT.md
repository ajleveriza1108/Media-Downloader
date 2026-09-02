# MediaDock R1.6.45 development snapshot

R1.6.45 focuses on torrent reliability and keeps the existing Media, universal Downloader, converter, streamer, native browser messaging and persistent floating extension integration.

Torrent changes:
- Isolated TorrentHost upgraded to MonoTorrent 3.9 alpha, whose June 2026 release improves DHT bootstrapping performance/reliability.
- Explicit BitTorrent/uTorrent/Transmission/Aelitis/BitComet/libtorrent DHT bootstrap routers.
- Early forced tracker announces during peer recovery, then normal tracker cadence.
- Mixed HTTPS + UDP tracker fallback for public torrents.
- DHT-ready/node telemetry, tracker count, persistent torrent queue/order/settings, selective files, queue priority and streaming.
- Fast resume and stable DHT cache retained.

Commercial/private licensing, trial, entitlement, updater implementation, backend secrets, customer state and installer implementation remain excluded from this public source repository.
