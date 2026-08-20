# Media Downloader

Windows desktop media downloader built with .NET 10 and WPF.

## Development snapshot

This repository currently tracks **Phase 1 R1.5.9.8.1** source as a **development snapshot**.

Windows build/runtime verification for R1.5.9.8.1 is still pending on the target machine. Do not treat this commit as a stable release until the local Windows PowerShell 5.1 installer/build/runtime gates complete successfully.

### Current development behavior

- MP4, MKV, and MP3 workflows.
- Prefer 1080p video when available, otherwise 720p, otherwise the highest remaining available video quality.
- Prefer the highest available concrete audio stream.
- Single-video YouTube URLs remain single-video downloads even when a Mix/radio list parameter is present.
- Responsive WPF layout with no-crop/no-hide repairs.
- Download runtime crash containment and persistent crash/download-attempt evidence.

## Build requirements

- Windows 10/11 x64
- .NET SDK 10.x
- Windows PowerShell 5.1 for the guarded local installer/update workflow

Build from the repository root:

```powershell
 dotnet restore .\MediaDownloader.sln
 dotnet build .\MediaDownloader.sln --configuration Release --no-incremental
```

Runtime downloading also requires yt-dlp and FFmpeg. Deno is used where supported by the local tool bootstrap. Binaries are intentionally **not** committed to this repository.

## Repository hygiene

Generated binaries, downloaded tools, `.build`, `dist`, logs, crash evidence, backups, caches, and local runtime state are excluded from source publication.

See `docs/` for architecture, roadmap, content-policy, and GUI notes.
