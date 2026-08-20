# Approved GUI Contract

The approved Media Downloader GUI is a dark, futuristic, minimal Windows desktop interface with a custom title bar and neon blue/violet accent treatment.

## Permanent layout

The URL strip is full-width at the top of the content area and automatically analyzes valid pasted/typed links. There is no Analyze button.

Below it are exactly three primary panels:

1. **Video / Details** — largest panel.
2. **Download Queue** — center panel.
3. **Convert** — right panel.

Recommended proportional widths are approximately 50% / 30% / 20%.

## Video / Details panel

- real thumbnail
- title, source/uploader, duration/capability
- actual available quality choices
- 2x2 settings grid:
  - Quality | Format
  - Audio | MP3 Bitrate
- Save location + Browse + Open folder + Download

No setting may be clipped or overlap adjacent controls.

## Queue panel

Compact vertical cards for active/completed jobs with title, format/quality, progress, status, and speed. Empty state says only `No downloads yet.`

## Convert panel

Drag/drop or browse local video, choose MP3 bitrate, and convert. Keep this panel compact and independent of download settings.

## Diagnostics

Diagnostics never occupies a primary panel. It opens from the gear or a real View Details button.
