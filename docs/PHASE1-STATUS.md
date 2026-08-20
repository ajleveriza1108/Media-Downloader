# Phase 1 Status

Version target: `0.1.0-alpha.6`
Package baseline: `Phase 1 R1.5.9.2`
Accepted exact predecessors: `Phase 1 R1.5.9.1`, `Phase 1 R1.5.9`, or `Phase 1 R1.5.8.2` managed source payload

## Preserved proven Windows baseline

- Windows PowerShell 5.1 parser gate
- .NET 10 restore/build/publish
- yt-dlp and Deno validation
- FFmpeg / FFprobe validation
- FFmpeg 320 kbps MP3 encode smoke test
- WPF startup smoke test
- custom title bar and active-monitor work-area maximize repair
- auto analysis, queue, MP4/MKV/MP3, playlists, and local conversion

## R1.5.9.1 implementation

- preserves the R1.5.9 adaptive download panel and caps the media preview region
- reflows Media, Queue, and Convert into a vertical stack below 1120 logical pixels
- adds vertical compact-mode scrolling so controls remain reachable at short heights
- reduces the minimum window to 620x340 logical pixels for high-DPI work areas
- keeps the bottom download row anchored while the middle area absorbs extra height
- moves Format to a clear top-level segmented selector
- MP4/MKV mode: Quality + Audio
- MP3 mode: Audio + MP3 Bitrate
- hides irrelevant MP3 bitrate controls for video formats
- hides irrelevant video Quality controls for MP3
- provides idle and analyzing selector labels instead of empty controls
- improves save-path contrast and full-path discoverability
- makes disabled Download visually neutral and self-explanatory
- preserves taskbar-safe maximize hooks from R1.5.8.2

## R1.5.9.1 Windows acceptance gate

1. PowerShell 5.1 parser passes before setup starts.
2. Exact R1.5.9 or R1.5.8.2 managed-source predecessor hashes pass, or the updater stops before mutation.
3. R1.5.9.1 responsive no-crop source contract passes.
4. WPF read-only binding contract passes.
5. .NET restore/build/publish passes.
6. Staged and installed EXE SHA-256 match.
7. Headless media URL classification self-test exits 0.
8. yt-dlp, Deno, FFmpeg, FFprobe, and 320 kbps encode tests pass.
9. GUI startup smoke test exits 0.
10. Normal GUI remains running after launch verification.
11. Maximize stays inside the current monitor work area above the taskbar.
12. MP4/MKV show Quality + Audio and do not show MP3 Bitrate.
13. MP3 shows Audio + MP3 Bitrate and does not show video Quality.
14. Idle/analyzing selectors never appear as unexplained empty boxes.
15. MP4, MKV, and MP3 downloads succeed on the user's Windows machine.

16. At 125%-200% Windows scaling, controls remain reachable through reflow/scroll rather than being clipped.

## R1.5.9.2 viewport-root no-crop repair

- Fixes the 935x485 short-window footer clipping case.
- Media rows are Auto-sized and height-responsive.
- PanelsScrollViewer owns overflow and exposes vertical scrolling.
- Windows startup smoke now verifies the reported 935x485 geometry.


## R1.5.9.2 Windows acceptance gate

1. Windows PowerShell 5.1 parser passes before setup starts.
2. Exact R1.5.9.1, R1.5.9, or R1.5.8.2 predecessor hashes pass.
3. R1.5.9.2 source contract passes.
4. .NET restore/build/publish and staged/installed EXE hash verification pass.
5. Engine/tool/encode gates pass.
6. The GUI startup smoke opens at 935x485 and proves the Download footer is inside the initial viewport.
7. If stacked content extends lower, a visible vertical scrollbar is required.
8. Normal app launch remains running after verification.
