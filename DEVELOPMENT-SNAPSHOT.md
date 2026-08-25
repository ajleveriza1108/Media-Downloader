# MediaDock R1.6.37 development snapshot

Status: guarded cumulative development update based on verified R1.6.36.

R1.6.37:
- Adds a dedicated Queue Clip Range column. Detected duration stays separate from Title / Video Source.
- New queue items default to the complete detected duration (for example 00:00 -> 02:37), while users can set a start/end range such as 00:05 -> 02:37.
- The selected range is passed to yt-dlp through --download-sections for both MP4 and MP3 output.
- Adds a persistent queue-wide format preference: Keep each item's format, All as MP4, or All as MP3.
- Adds a persistent parallel-download setting from 1 through 5; the hard safety ceiling remains five.
- Removes ETA from the yt-dlp progress contract and customer-facing queue status because the estimate is not reliable enough.
- Preserves strict trial accounting, licensing/entitlement behavior, original/dub audio selection, queue persistence, and the R1.6.36 premium interface.
- Adds non-interactive self-tests for clip-range parsing, download-section formatting, and queue preference normalization.

Commercial/private licensing, trial, entitlement, backend, updater, customer-state, and installer implementation files remain untouched by this public-safe source update.