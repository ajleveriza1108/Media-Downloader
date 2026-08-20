# Runtime Tools

Do not commit tool executables to this repository.

For local development, place these files here:

```text
yt-dlp.exe
deno.exe
ffmpeg.exe
ffprobe.exe
```

For installed builds, the application also checks a `Tools` directory next to the installed executable.

Future release tooling will download pinned, verified tool versions and validate checksums before packaging.
