# MediaDock R1.6.41 development snapshot

Status: verified public-source and customer installer release; installed validation passed and the build is eligible for stable activation.

R1.6.41:
- Enables Stream and Convert navigation from the main header for licensed MediaDock users instead of hard-disabling those buttons.
- Rewires the visible queue controls through dedicated R1.6.41 partial handlers; visible controls may not ship as dead/no-op entry points.
- Unifies queue-row selection with bulk Download/Delete selection state.
- Restores MKV in the row selector, download-format catalog, labels, and yt-dlp merge-container path.
- Shows analyzed source-supported video quality choices instead of fixed unavailable resolutions; MP3 rows expose bitrate choices.
- Keeps Audio / Dub, adds Full / Custom clip mode, and allows failed rows to be edited and retried.
- Shows Open File only when an output exists and hides context-only Convert actions when they are not applicable.
- Adds Default MP3 quality to Settings while preserving output folder, theme, concurrency, clipboard detection, watermark handling, updates, and diagnostics.
- Preserves R1.6.40 responsive queue, Refresh/reconciliation, M4A/FLAC, safe-delete, trial accounting, and installer-only distribution.
- R1.6.41 customer distribution remains installer-only; no portable customer package is produced.

Commercial/private licensing, trial, entitlement, updater implementation, backend secrets, customer state, and installer implementation remain excluded from the public source repository.