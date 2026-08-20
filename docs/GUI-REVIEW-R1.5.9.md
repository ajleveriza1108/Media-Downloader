# GUI Review R1.5.9

## User-visible issue
The R1.5.8.2 crop repair made all controls visible, but the media panel still gave too much space to an empty preview and displayed MP3 bitrate even when MP4/MKV was selected. Empty pre-analysis selectors also looked unfinished.

## R1.5.9 decisions
- Cap preview height and keep the download footer anchored.
- Treat Format as the primary mode switch.
- Reveal only controls relevant to the selected format.
- Keep meaningful defaults visible before analysis.
- Preserve the approved three-panel shell and all downloader behavior.

This is a focused hierarchy/clarity repair, not a broad GUI redesign.
