# GUI Review - R1.5.6

## Observed R1.5.5 Windows issues

The real Windows screenshot showed four concrete defects:

1. A YouTube playlist URL failed analysis with a raw JSON type error:
   `The requested operation requires an element of type 'Number', but the target element has type 'Null'.`
2. The apparent `See Diagnostics for details` control was only text inside the source badge and could not be clicked.
3. Global `TextBlock` wrapping could make text expand vertically inside fixed-height rows, creating clipping/overlap pressure.
4. The queue header displayed the global Status string, allowing long technical errors to occupy the same row as queue controls.

## R1.5.6 corrections

### Playlist behavior

- Analysis no longer forces `--no-playlist`.
- yt-dlp playlist discovery uses `--dump-single-json --flat-playlist --yes-playlist`.
- Playlist entries are counted without fully extracting every item during analysis.
- Playlist download uses `--yes-playlist` and continues past individually unavailable entries with `--ignore-errors`.
- The queue shows overall playlist progress using playlist index/count information from yt-dlp's progress template.

### Metadata parser

Numeric metadata helpers now explicitly handle JSON `null` and `undefined` before calling numeric conversion methods.

### Diagnostics UX

Analysis errors expose a real `View Diagnostics` button wired to the same diagnostics window as the gear button. Technical details stay out of the normal status/queue presentation.

### Layout

- default `TextBlock` wrapping changed to `NoWrap`
- explicit wrap retained only where explanatory/error text needs it
- window width/minimum width increased for the approved desktop composition
- converter width reduced slightly
- thumbnail width reduced slightly
- control strip uses explicit balanced column widths
- uploader and Save Location rows use `Grid` rather than unconstrained horizontal `StackPanel`
- queue item status uses ellipsis trimming
- queue header no longer binds arbitrary global status text

## Visual contract retained

The approved futuristic reference remains `APPROVED-GUI-REFERENCE.png`. R1.5.6 does not reintroduce stock WPF tabs, the white title bar, or the earlier temporary layout.
