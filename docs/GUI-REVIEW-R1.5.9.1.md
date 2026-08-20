# GUI Review R1.5.9.1

## User-visible issue
R1.5.9 improved hierarchy but still relied on a wide three-column layout and a large minimum window. At Windows scaling or smaller working areas, right-side panels and lower controls could be cropped or become unreachable.

## R1.5.9.1 decisions
- Keep the approved three-panel layout when there is enough logical width.
- Reflow to a single-column stack below 1120 logical pixels instead of compressing controls.
- Add vertical scrolling for compact/short work areas.
- Reduce the minimum window to 620x340 logical pixels.
- Preserve active-monitor taskbar-safe maximization.
- Keep all download behavior and R1.5.9 conditional format controls unchanged.

This repair prioritizes reachability: no action should disappear merely because Windows scaling reduced the logical work area.
