# MediaDock R1.6.40 development snapshot

Status: verified public-source and customer installer release; installed validation passed and the build is eligible for stable activation.

R1.6.40:
- Keeps Download, Stream, Convert, and Settings inside the existing MediaDock main window.
- Makes Download Queue columns responsive and disables horizontal queue scrolling so normal controls are not cropped or hidden.
- Keeps Title / Video Source and Audio / Dub flexible while Format, Quality, Clip Range, Progress, Status, Refresh, and Actions remain compact and visible.
- Adds Refresh All beside Download Queue and Refresh on every row.
- Reconciles persisted output paths plus MediaDock download/conversion folders for existing video/audio files.
- Detects completed, converted, missing, and partial/in-progress output states and prevents silent duplicate downloads when a matching completed output already exists.
- Preserves R1.6.39 Audio / Dub, Clip Range, M4A/FLAC, safe-delete, clipboard detection, trial accounting, and parallel-download behavior.
- R1.6.40 customer distribution remains installer-only; no portable customer package is produced.

Commercial/private licensing, trial, entitlement, updater implementation, backend secrets, customer state, and installer implementation remain excluded from the public source repository.