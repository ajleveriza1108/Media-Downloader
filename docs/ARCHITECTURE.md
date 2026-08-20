# Architecture

## Design goal

Media Downloader is a Windows-native orchestration application. It does not reimplement site extraction logic. Site-specific changes remain isolated inside replaceable media engines.

## Layers

### 1. Presentation

WPF views and view models. The UI never executes command-line tools directly.

### 2. Application services

Coordinates analysis, format selection, downloads, local MP3 conversion, diagnostics, history, presets, and later playlist/batch workflows.

### 3. Media engines

Phase 1 ships these engine adapters:

- `YtDlpService`
- `FfmpegConversionService`

Later phases can add:

- direct HTTP media engine
- native HLS/DASH manifest engine for simple non-DRM sources
- browser-assisted discovery adapter

### 4. Tooling

External tools are isolated behind services:

- yt-dlp
- Deno
- FFmpeg
- FFprobe

Their versions and health are checked independently.

## Important rules

1. Analyze before download.
2. Do not invent formats.
3. Do not silently downgrade the chosen resolution.
4. Do not force obsolete site-specific clients globally.
5. Preserve stderr/warnings for diagnostics.
6. Authentication is opt-in and user-controlled.
7. Never package cookie/session files.
8. DRM-protected streams are not bypassed.
9. Tool updates must be versioned and independently replaceable.
10. The normal UI should stay focused; diagnostics belong in a separate area.

## Output workflows

### Video downloads

- **MP4**: preferred compatibility-oriented container
- **MKV**: robust alternative container for video downloads

### Audio downloads

- **MP3**: use the best available source audio, then convert to the selected bitrate

### Local conversion

- Convert supported local video files to MP3 with FFmpeg

## Authentication

A later phase will expose browser-cookie import as an explicit per-job/per-site option. Cookies remain local to the current Windows user and are never committed to source control.


## Playlist workflow

Playlist URLs are analyzed as collections rather than forced into single-video mode. The analysis service uses yt-dlp flat-playlist JSON to discover collection metadata efficiently. Playlist downloads run as batch jobs and expose playlist index/count through the existing queue progress protocol. Exact per-video height is not assumed across a collection; the playlist quality label is explicitly `Best available per video`.

## Metadata resilience

External extractor JSON is treated as partially optional. Numeric values such as duration, FPS, file size, audio bitrate, and video bitrate may be JSON null and must be parsed defensively. A null metadata field must never crash the WPF application.
