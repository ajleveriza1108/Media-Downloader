# GUI Review R1.5.8.2

Approved layout rule:

`URL strip`

then exactly:

`Video / Details | Download Queue | Convert`

## Repairs

- Removed the Analyze button. Valid URLs auto-analyze after a 550 ms debounce.
- Centered the normal workflow around three persistent panels.
- Moved Download Queue from a full-width bottom strip into the center panel.
- Kept Convert as the right panel.
- Rebuilt the left control area as a 2x2 grid:
  - Quality | Format
  - Audio | MP3 Bitrate
- Quality labels include actual resolution/FPS and dimensions when available.
- Audio is now a real selector populated from available audio-only formats.
- Save controls use compact fixed widths so they do not collide with Download.
- Diagnostics remains accessible from the gear and from a real View Details button on errors.

## YouTube classification

- `/watch?v=...` => single video, regardless of `list`, `index`, or `start_radio`.
- `youtu.be/...` => single video.
- `/playlist?list=...` => playlist.
- YouTube Mix/radio (`list=RD...`) is never enumerated when it arrives through a watch URL.
