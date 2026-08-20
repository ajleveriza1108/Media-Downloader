# Roadmap

## Phase 1 - Windows foundation and approved GUI

- .NET 10 WPF application
- Approved futuristic main-window layout
- Custom dark title bar
- URL analysis and real format discovery
- MP4 / MKV / MP3 workflows
- Local video-to-MP3 conversion
- Download queue/progress presentation
- Diagnostics behind the gear button
- Transactional one-click Windows updater

## Phase 2 - Download reliability

- Cancel current download
- Retry policy by error category
- Bounded multi-item concurrency
- Pause/resume where the protocol supports it
- FFprobe post-download verification summary
- File collision policy
- Interrupted-download recovery
- Atomic final-file move

## Phase 3 - Broader platform coverage

- Direct media URL fallback
- Generic embedded media flow
- Non-DRM HLS/DASH fallback
- HTTP headers/referer support
- Browser-cookie authentication flow
- Site capability diagnostics

## Phase 4 - YouTube reliability

- Deno/EJS verification
- PO-token provider integration only when current yt-dlp diagnostics require it
- No manually forced global player-client list
- YouTube health test
- Live stream handling
- Shorts, playlists, channel/video tabs

## Phase 5 - Media workflows

- Playlist/batch downloads (basic playlist batch workflow implemented in R1.5.6; per-item advanced queue controls remain future work)
- Download archive/history
- Subtitle selection
- Metadata and chapters
- Thumbnail embedding
- Audio presets
- Remux/transcode presets
- Clip/time-range downloads

## Phase 6 - Church presets

- Church Presentation: compatible MP4/H.264/AAC target
- Sermon Audio preset
- Worship/choir archive
- Series/playlist folder naming
- Subtitle/caption archive
- Projector/older-PC compatibility checks


## R1.5.9 UX refinement
- Adaptive format-specific download controls and compact media preview.
- Preserve the three-panel shell and existing Phase 1 engine behavior.


## R1.5.9.1 responsive no-crop repair
- Reflow the three-panel shell below 1120 logical pixels.
- Keep all actions reachable at short/high-DPI work areas with vertical scrolling.
- Preserve R1.5.9 adaptive controls and R1.5.8.2 taskbar-safe maximize behavior.

## R1.5.9.2 viewport-root no-crop repair

- Fixes the 935x485 short-window footer clipping case.
- Media rows are Auto-sized and height-responsive.
- PanelsScrollViewer owns overflow and exposes vertical scrolling.
- Windows startup smoke now verifies the reported 935x485 geometry.

